using UnityEngine;
using System.Collections.Generic;
using Pathfinding.Util;
using Pathfinding.Serialization;

namespace Pathfinding {
	/// <summary>
	/// Exposes internal methods for graphs.
	/// This is used to hide methods that should not be used by any user code
	/// but still have to be 'public' or 'internal' (which is pretty much the same as 'public'
	/// as this library is distributed with source code).
	///
	/// Hiding the internal methods cleans up the documentation and IntelliSense suggestions.
	/// </summary>
	public interface IGraphInternals {
		string SerializedEditorSettings { get; set; }
		void OnDestroy ();
		void DestroyAllNodes ();
		IEnumerable<Progress> ScanInternal ();
		void SerializeExtraInfo (GraphSerializationContext ctx);
		void DeserializeExtraInfo (GraphSerializationContext ctx);
		void PostDeserialization (GraphSerializationContext ctx);
		void DeserializeSettingsCompatibility (GraphSerializationContext ctx);
	}

	/// <summary>Base class for all graphs</summary>
	public abstract class NavGraph : IGraphInternals {
		/// <summary>Reference to the AstarPath object in the scene</summary>
		public AstarPath active;

		/// <summary>
		/// Used as an ID of the graph, considered to be unique.
		/// Note: This is Pathfinding.Util.Guid not System.Guid. A replacement for System.Guid was coded for better compatibility with iOS
		/// </summary>
		[JsonMember]
		public Guid guid;

		/// <summary>Default penalty to apply to all nodes</summary>
		[JsonMember]
		public uint initialPenalty;

		/// <summary>Is the graph open in the editor</summary>
		[JsonMember]
		public bool open;

		/// <summary>Index of the graph, used for identification purposes</summary>
		public uint graphIndex;

		/// <summary>
		/// Name of the graph.
		/// Can be set in the unity editor
		/// </summary>
		[JsonMember]
		public string name;

		/// <summary>
		/// Enable to draw gizmos in the Unity scene view.
		/// In the inspector this value corresponds to the state of
		/// the 'eye' icon in the top left corner of every graph inspector.
		/// </summary>
		[JsonMember]
		public bool drawGizmos = true;

		/// <summary>
		/// Used in the editor to check if the info screen is open.
		/// Should be inside UNITY_EDITOR only \<see cref="ifs"/> but just in case anyone tries to serialize a NavGraph instance using Unity, I have left it like this as it would otherwise cause a crash when building.
		/// Version 3.0.8.1 was released because of this bug only
		/// </summary>
		[JsonMember]
		public bool infoScreenOpen;

		/// <summary>Used in the Unity editor to store serialized settings for graph inspectors</summary>
		[JsonMember]
		string serializedEditorSettings;


		/// <summary>True if the graph exists, false if it has been destroyed</summary>
		internal bool exists { get { return active != null; } }

		/// <summary>
		/// Number of nodes in the graph.
		/// Note that this is, unless the graph type has overriden it, an O(n) operation.
		///
		/// This is an O(1) operation for grid graphs and point graphs.
		/// For layered grid graphs it is an O(n) operation.
		/// </summary>
		public virtual int CountNodes () {
			int count = 0;

			GetNodes(node => count++);
			return count;
		}

		/// <summary>Calls a delegate with all nodes in the graph until the delegate returns false</summary>
		public void GetNodes (System.Func<GraphNode, bool> action) {
			bool cont = true;

			GetNodes(node => {
				if (cont) cont &= action(node);
			});
		}

		/// <summary>
		/// Calls a delegate with all nodes in the graph.
		/// This is the primary way of iterating through all nodes in a graph.
		///
		/// Do not change the graph structure inside the delegate.
		///
		/// <code>
		/// var gg = AstarPath.active.data.gridGraph;
		///
		/// gg.GetNodes(node => {
		///     // Here is a node
		///     Debug.Log("I found a node at position " + (Vector3)node.position);
		/// });
		/// </code>
		///
		/// If you want to store all nodes in a list you can do this
		///
		/// <code>
		/// var gg = AstarPath.active.data.gridGraph;
		///
		/// List<GraphNode> nodes = new List<GraphNode>();
		/// gg.GetNodes((System.Action<GraphNode>)nodes.Add);
		/// </code>
		///
		/// See: <see cref="Pathfinding.AstarData.GetNodes"/>
		/// </summary>
		public abstract void GetNodes (System.Action<GraphNode> action);

		/// <summary>
		/// A matrix for translating/rotating/scaling the graph.
		/// Deprecated: Use the transform field (only available on some graph types) instead
		/// </summary>
		[System.Obsolete("Use the transform field (only available on some graph types) instead", true)]
		public Matrix4x4 matrix = Matrix4x4.identity;

		/// <summary>
		/// Inverse of matrix.
		/// Deprecated: Use the transform field (only available on some graph types) instead
		/// </summary>
		[System.Obsolete("Use the transform field (only available on some graph types) instead", true)]
		public Matrix4x4 inverseMatrix = Matrix4x4.identity;

		/// <summary>
		/// Use to set both matrix and inverseMatrix at the same time.
		/// Deprecated: Use the transform field (only available on some graph types) instead
		/// </summary>
		[System.Obsolete("Use the transform field (only available on some graph types) instead", true)]
		public void SetMatrix (Matrix4x4 m) {
			matrix = m;
			inverseMatrix = m.inverse;
		}

		/// <summary>
		/// Moves nodes in this graph.
		/// Deprecated: Use RelocateNodes(Matrix4x4) instead.
		///  To keep the same behavior you can call RelocateNodes(newMatrix * oldMatrix.inverse).
		/// </summary>
		[System.Obsolete("Use RelocateNodes(Matrix4x4) instead. To keep the same behavior you can call RelocateNodes(newMatrix * oldMatrix.inverse).")]
		public void RelocateNodes (Matrix4x4 oldMatrix, Matrix4x4 newMatrix) {
			RelocateNodes(newMatrix * oldMatrix.inverse);
		}

		/// <summary>
		/// Moves the nodes in this graph.
		/// Multiplies all node positions by deltaMatrix.
		///
		/// For example if you want to move all your nodes in e.g a point graph 10 units along the X axis from the initial position
		/// <code>
		/// var graph = AstarPath.data.pointGraph;
		/// var m = Matrix4x4.TRS (new Vector3(10,0,0), Quaternion.identity, Vector3.one);
		/// graph.RelocateNodes (m);
		/// </code>
		///
		/// Note: For grid graphs, navmesh graphs and recast graphs it is recommended to
		/// use their custom overloads of the RelocateNodes method which take parameters
		/// for e.g center and nodeSize (and additional parameters) instead since
		/// they are both easier to use and are less likely to mess up pathfinding.
		///
		/// Warning: This method is lossy for PointGraphs, so calling it many times may
		/// cause node positions to lose precision. For example if you set the scale
		/// to 0 in one call then all nodes will be scaled/moved to the same point and
		/// you will not be able to recover their original positions. The same thing
		/// happens for other - less extreme - values as well, but to a lesser degree.
		/// </summary>
		public virtual void RelocateNodes (Matrix4x4 deltaMatrix) {
			GetNodes(node => node.position = ((Int3)deltaMatrix.MultiplyPoint((Vector3)node.position)));
		}

		/// <summary>
		/// Returns the nearest node to a position.
		/// See: Pathfinding.NNConstraint.None
		/// </summary>
		/// <param name="position">The position to try to find a close node to</param>
		public NNInfoInternal GetNearest (Vector3 position) {
			return GetNearest(position, NNConstraint.None);
		}

		/// <summary>Returns the nearest node to a position using the specified NNConstraint.</summary>
		/// <param name="position">The position to try to find a close node to</param>
		/// <param name="constraint">Can for example tell the function to try to return a walkable node. If you do not get a good node back, consider calling GetNearestForce.</param>
		public NNInfoInternal GetNearest (Vector3 position, NNConstraint constraint) {
			return GetNearest(position, constraint, null);
		}

		/// <summary>Returns the nearest node to a position using the specified NNConstraint.</summary>
		/// <param name="position">The position to try to find a close node to</param>
		/// <param name="hint">Can be passed to enable some graph generators to find the nearest node faster.</param>
		/// <param name="constraint">Can for example tell the function to try to return a walkable node. If you do not get a good node back, consider calling GetNearestForce.</param>
		public virtual NNInfoInternal GetNearest (Vector3 position, NNConstraint constraint, GraphNode hint) {
			// This is a default implementation and it is pretty slow
			// Graphs usually override this to provide faster and more specialised implementations

			float maxDistSqr = constraint == null || constraint.constrainDistance ? AstarPath.active.maxNearestNodeDistanceSqr : float.PositiveInfinity;

			float minDist = float.PositiveInfinity;
			GraphNode minNode = null;

			float minConstDist = float.PositiveInfinity;
			GraphNode minConstNode = null;

			// Loop through all nodes and find the closest suitable node
			GetNodes(node => {
				float dist = (position-(Vector3)node.position).sqrMagnitude;

				if (dist < minDist) {
					minDist = dist;
					minNode = node;
				}

				if (dist < minConstDist && dist < maxDistSqr && (constraint == null || constraint.Suitable(node))) {
					minConstDist = dist;
					minConstNode = node;
				}
			});

			var nnInfo = new NNInfoInternal(minNode);

			nnInfo.constrainedNode = minConstNode;

			if (minConstNode != null) {
				nnInfo.constClampedPosition = (Vector3)minConstNode.position;
			} else if (minNode != null) {
				nnInfo.constrainedNode = minNode;
				nnInfo.constClampedPosition = (Vector3)minNode.position;
			}

			return nnInfo;
		}

		/// <summary>
		/// Returns the nearest node to a position using the specified \link Pathfinding.NNConstraint constraint \endlink.
		/// Returns: an NNInfo. This method will only return an empty NNInfo if there are no nodes which comply with the specified constraint.
		/// </summary>
		public virtual NNInfoInternal GetNearestForce (Vector3 position, NNConstraint constraint) {
			return GetNearest(position, constraint);
		}

		/// <summary>
		/// Function for cleaning up references.
		/// This will be called on the same time as OnDisable on the gameObject which the AstarPath script is attached to (remember, not in the editor).
		/// Use for any cleanup code such as cleaning up static variables which otherwise might prevent resources from being collected.
		/// Use by creating a function overriding this one in a graph class, but always call base.OnDestroy () in that function.
		/// All nodes should be destroyed in this function otherwise a memory leak will arise.
		/// </summary>
		protected virtual void OnDestroy () {
			DestroyAllNodes();
		}

		/// <summary>
		/// Destroys all nodes in the graph.
		/// Warning: This is an internal method. Unless you have a very good reason, you should probably not call it.
		/// </summary>
		protected virtual void DestroyAllNodes () {
			GetNodes(node => node.Destroy());
		}

		/// <summary>
		/// Scan the graph.
		/// Deprecated: Use AstarPath.Scan() instead
		/// </summary>
		[System.Obsolete("Use AstarPath.Scan instead")]
		public void ScanGraph () {
			Scan();
		}

		/// <summary>
		/// Scan the graph.
		///
		/// Consider using AstarPath.Scan() instead since this function only scans this graph and if you are using multiple graphs
		/// with connections between them, then it is better to scan all graphs at once.
		/// </summary>
		public void Scan () {
			active.Scan(this);
		}

		/// <summary>
		/// Internal method to scan the graph.
		/// Called from AstarPath.ScanAsync.
		/// Override this function to implement custom scanning logic.
		/// Progress objects can be yielded to show progress info in the editor and to split up processing
		/// over several frames when using async scanning.
		/// </summary>
		protected abstract IEnumerable<Progress> ScanInternal ();

		/// <summary>
		/// Serializes graph type specific node data.
		/// This function can be overriden to serialize extra node information (or graph information for that matter)
		/// which cannot be serialized using the standard serialization.
		/// Serialize the data in any way you want and return a byte array.
		/// When loading, the exact same byte array will be passed to the DeserializeExtraInfo function.\n
		/// These functions will only be called if node serialization is enabled.\n
		/// </summary>
		protected virtual void SerializeExtraInfo (GraphSerializationContext ctx) {
		}

		/// <summary>
		/// Deserializes graph type specific node data.
		/// See: SerializeExtraInfo
		/// </summary>
		protected virtual void DeserializeExtraInfo (GraphSerializationContext ctx) {
		}

		/// <summary>
		/// Called after all deserialization has been done for all graphs.
		/// Can be used to set up more graph data which is not serialized
		/// </summary>
		protected virtual void PostDeserialization (GraphSerializationContext ctx) {
		}

		/// <summary>
		/// An old format for serializing settings.
		/// Deprecated: This is deprecated now, but the deserialization code is kept to
		/// avoid loosing data when upgrading from older versions.
		/// </summary>
		protected virtual void DeserializeSettingsCompatibility (GraphSerializationContext ctx) {
			guid = new Guid(ctx.reader.ReadBytes(16));
			initialPenalty = ctx.reader.ReadUInt32();
			open = ctx.reader.ReadBoolean();
			name = ctx.reader.ReadString();
			drawGizmos = ctx.reader.ReadBoolean();
			infoScreenOpen = ctx.reader.ReadBoolean();
		}

		/// <summary>Draw gizmos for the graph</summary>
		public virtual void OnDrawGizmos (RetainedGizmos gizmos, bool drawNodes) {
			if (!drawNodes) {
				return;
			}

			// This is a relatively slow default implementation.
			// subclasses of the base graph class may override
			// this method to draw gizmos in a more optimized way

			var hasher = new RetainedGizmos.Hasher(active);
			GetNodes(node => hasher.HashNode(node));

			// Update the gizmo mesh if necessary
			if (!gizmos.Draw(hasher)) {
				using (var helper = gizmos.GetGizmoHelper(active, hasher)) {
					GetNodes((System.Action<GraphNode>)helper.DrawConnections);
				}
			}

			if (active.showUnwalkableNodes) DrawUnwalkableNodes(active.unwalkableNodeDebugSize);
		}

		protected void DrawUnwalkableNodes (float size) {
			Gizmos.color = AstarColor.UnwalkableNode;
			GetNodes(node => {
				if (!node.Walkable) Gizmos.DrawCube((Vector3)node.position, Vector3.one*size);
			});
		}

		#region IGraphInternals implementation
		string IGraphInternals.SerializedEditorSettings { get { return serializedEditorSettings; } set { serializedEditorSettings = value; } }
		void IGraphInternals.OnDestroy () { OnDestroy(); }
		void IGraphInternals.DestroyAllNodes () { DestroyAllNodes(); }
		IEnumerable<Progress> IGraphInternals.ScanInternal () { return ScanInternal(); }
		void IGraphInternals.SerializeExtraInfo (GraphSerializationContext ctx) { SerializeExtraInfo(ctx); }
		void IGraphInternals.DeserializeExtraInfo (GraphSerializationContext ctx) { DeserializeExtraInfo(ctx); }
		void IGraphInternals.PostDeserialization (GraphSerializationContext ctx) { PostDeserialization(ctx); }
		void IGraphInternals.DeserializeSettingsCompatibility (GraphSerializationContext ctx) { DeserializeSettingsCompatibility(ctx); }

		#endregion
	}


	/// <summary>
	/// Handles collision checking for graphs.
	/// Mostly used by grid based graphs
	/// </summary>
	[System.Serializable]
	public class GraphCollision {
		/// <summary>
		/// Collision shape to use.
		/// See: <see cref="Pathfinding.ColliderType"/>
		/// </summary>
		public ColliderType type = ColliderType.Capsule;

		/// <summary>
		/// Diameter of capsule or sphere when checking for collision.
		/// When checking for collisions the system will check if any colliders
		/// overlap a specific shape at the node's position. The shape is determined
		/// by the <see cref="type"/> field.
		///
		/// A diameter of 1 means that the shape has a diameter equal to the node's width,
		/// or in other words it is equal to \link Pathfinding.GridGraph.nodeSize nodeSize \endlink.
		///
		/// If <see cref="type"/> is set to Ray, this does not affect anything.
		///
		/// [Open online documentation to see images]
		/// </summary>
		public float diameter = 1F;

		/// <summary>
		/// Height of capsule or length of ray when checking for collision.
		/// If <see cref="type"/> is set to Sphere, this does not affect anything.
		///
		/// [Open online documentation to see images]
		/// </summary>
		public float height = 2F;

		/// <summary>
		/// Height above the ground that collision checks should be done.
		/// For example, if the ground was found at y=0, collisionOffset = 2
		/// type = Capsule and height = 3 then the physics system
		/// will be queried to see if there are any colliders in a capsule
		/// for which the bottom sphere that is made up of is centered at y=2
		/// and the top sphere has its center at y=2+3=5.
		///
		/// If type = Sphere then the sphere's center would be at y=2 in this case.
		/// </summary>
		public float collisionOffset;

		/// <summary>
		/// Direction of the ray when checking for collision.
		/// If <see cref="type"/> is not Ray, this does not affect anything
		/// </summary>
		public RayDirection rayDirection = RayDirection.Both;

		/// <summary>Layers to be treated as obstacles.</summary>
		public LayerMask mask;

		/// <summary>Layers to be included in the height check.</summary>
		public LayerMask heightMask = -1;

		/// <summary>
		/// The height to check from when checking height ('ray length' in the inspector).
		///
		/// As the image below visualizes, different ray lengths can make the ray hit different things.
		/// The distance is measured up from the graph plane.
		///
		/// [Open online documentation to see images]
		/// </summary>
		public float fromHeight = 100;

		/// <summary>
		/// Toggles thick raycast.
		/// See: https://docs.unity3d.com/ScriptReference/Physics.SphereCast.html
		/// </summary>
		public bool thickRaycast;

		/// <summary>
		/// Diameter of the thick raycast in nodes.
		/// 1 equals \link Pathfinding.GridGraph.nodeSize nodeSize \endlink
		/// </summary>
		public float thickRaycastDiameter = 1;

		/// <summary>Make nodes unwalkable when no ground was found with the height raycast. If height raycast is turned off, this doesn't affect anything.</summary>
		public bool unwalkableWhenNoGround = true;

		/// <summary>
		/// Use Unity 2D Physics API.
		/// See: http://docs.unity3d.com/ScriptReference/Physics2D.html
		/// </summary>
		public bool use2D;

		/// <summary>Toggle collision check</summary>
		public bool collisionCheck = true;

		/// <summary>Toggle height check. If false, the grid will be flat</summary>
		public bool heightCheck = true;

		/// <summary>
		/// Direction to use as UP.
		/// See: Initialize
		/// </summary>
		public Vector3 up;

		/// <summary>
		/// <see cref="up"/> * <see cref="height"/>.
		/// See: Initialize
		/// </summary>
		private Vector3 upheight;

		/// <summary>
		/// <see cref="diameter"/> * scale * 0.5.
		/// Where scale usually is \link Pathfinding.GridGraph.nodeSize nodeSize \endlink
		/// See: Initialize
		/// </summary>
		private float finalRadius;

		/// <summary>
		/// <see cref="thickRaycastDiameter"/> * scale * 0.5.
		/// Where scale usually is \link Pathfinding.GridGraph.nodeSize nodeSize \endlink See: Initialize
		/// </summary>
		private float finalRaycastRadius;

		/// <summary>Offset to apply after each raycast to make sure we don't hit the same point again in CheckHeightAll</summary>
		public const float RaycastErrorMargin = 0.005F;

		/// <summary>
		/// Sets up several variables using the specified matrix and scale.
		/// See: GraphCollision.up
		/// See: GraphCollision.upheight
		/// See: GraphCollision.finalRadius
		/// See: GraphCollision.finalRaycastRadius
		/// </summary>
		public void Initialize (GraphTransform transform, float scale) {
			up = (transform.Transform(Vector3.up) - transform.Transform(Vector3.zero)).normalized;
			upheight = up*height;
			finalRadius = diameter*scale*0.5F;
			finalRaycastRadius = thickRaycastDiameter*scale*0.5F;
		}

		/// <summary>
		/// Returns if the position is obstructed.
		/// If <see cref="collisionCheck"/> is false, this will always return true.\n
		/// </summary>
		public bool Check (Vector3 position) {
			if (!collisionCheck) {
				return true;
			}

			if (use2D) {
				switch (type) {
				case ColliderType.Capsule:
				case ColliderType.Sphere:
					return Physics2D.OverlapCircle(position, finalRadius, mask) == null;
				default:
					return Physics2D.OverlapPoint(position, mask) == null;
				}
			}

			position += up*collisionOffset;
			switch (type) {
			case ColliderType.Capsule:
				return !Physics.CheckCapsule(position, position+upheight, finalRadius, mask, QueryTriggerInteraction.Ignore);
			case ColliderType.Sphere:
				return !Physics.CheckSphere(position, finalRadius, mask, QueryTriggerInteraction.Ignore);
			default:
				switch (rayDirection) {
				case RayDirection.Both:
					return !Physics.Raycast(position, up, height, mask, QueryTriggerInteraction.Ignore) && !Physics.Raycast(position+upheight, -up, height, mask, QueryTriggerInteraction.Ignore);
				case RayDirection.Up:
					return !Physics.Raycast(position, up, height, mask, QueryTriggerInteraction.Ignore);
				default:
					return !Physics.Raycast(position+upheight, -up, height, mask, QueryTriggerInteraction.Ignore);
				}
			}
		}

		/// <summary>
		/// Returns the position with the correct height.
		/// If <see cref="heightCheck"/> is false, this will return position.
		/// </summary>
		public Vector3 CheckHeight (Vector3 position) {
			RaycastHit hit;
			bool walkable;

			return CheckHeight(position, out hit, out walkable);
		}

		/// <summary>
		/// Returns the position with the correct height.
		/// If <see cref="heightCheck"/> is false, this will return position.\n
		/// walkable will be set to false if nothing was hit.
		/// The ray will check a tiny bit further than to the grids base to avoid floating point errors when the ground is exactly at the base of the grid
		/// </summary>
		public Vector3 CheckHeight (Vector3 position, out RaycastHit hit, out bool walkable) {
			walkable = true;

			if (!heightCheck || use2D) {
				hit = new RaycastHit();
				return position;
			}

			if (thickRaycast) {
				var ray = new Ray(position+up*fromHeight, -up);
				if (Physics.SphereCast(ray, finalRaycastRadius, out hit, fromHeight+0.005F, heightMask, QueryTriggerInteraction.Ignore)) {
					return VectorMath.ClosestPointOnLine(ray.origin, ray.origin+ray.direction, hit.point);
				}

				walkable &= !unwalkableWhenNoGround;
			} else {
				// Cast a ray from above downwards to try to find the ground
				if (Physics.Raycast(position+up*fromHeight, -up, out hit, fromHeight+0.005F, heightMask, QueryTriggerInteraction.Ignore)) {
					return hit.point;
				}

				walkable &= !unwalkableWhenNoGround;
			}
			return position;
		}

		/// <summary>Internal buffer used by <see cref="CheckHeightAll"/></summary>
		RaycastHit[] hitBuffer = new RaycastHit[8];

		/// <summary>
		/// Returns all hits when checking height for position.
		/// Warning: Does not work well with thick raycast, will only return an object a single time
		///
		/// Warning: The returned array is ephermal. It will be invalidated when this method is called again.
		/// If you need persistent results you should copy it.
		///
		/// The returned array may be larger than the actual number of hits, the numHits out parameter indicates how many hits there actually were.
		/// </summary>
		public RaycastHit[] CheckHeightAll (Vector3 position, out int numHits) {
			if (!heightCheck || use2D) {
				hitBuffer[0] = new RaycastHit {
					point = position,
					distance = 0,
				};
				numHits = 1;
				return hitBuffer;
			}

			// Cast a ray from above downwards to try to find the ground
#if UNITY_2017_1_OR_NEWER
			numHits = Physics.RaycastNonAlloc(position+up*fromHeight, -up, hitBuffer, fromHeight+0.005F, heightMask, QueryTriggerInteraction.Ignore);
			if (numHits == hitBuffer.Length) {
				// Try again with a larger buffer
				hitBuffer = new RaycastHit[hitBuffer.Length*2];
				return CheckHeightAll(position, out numHits);
			}
			return hitBuffer;
#else
			var result = Physics.RaycastAll(position+up*fromHeight, -up, fromHeight+0.005F, heightMask, QueryTriggerInteraction.Ignore);
			numHits = result.Length;
			return result;
#endif
		}

		public void DeserializeSettingsCompatibility (GraphSerializationContext ctx) {
			type = (ColliderType)ctx.reader.ReadInt32();
			diameter = ctx.reader.ReadSingle();
			height = ctx.reader.ReadSingle();
			collisionOffset = ctx.reader.ReadSingle();
			rayDirection = (RayDirection)ctx.reader.ReadInt32();
			mask = (LayerMask)ctx.reader.ReadInt32();
			heightMask = (LayerMask)ctx.reader.ReadInt32();
			fromHeight = ctx.reader.ReadSingle();
			thickRaycast = ctx.reader.ReadBoolean();
			thickRaycastDiameter = ctx.reader.ReadSingle();

			unwalkableWhenNoGround = ctx.reader.ReadBoolean();
			use2D = ctx.reader.ReadBoolean();
			collisionCheck = ctx.reader.ReadBoolean();
			heightCheck = ctx.reader.ReadBoolean();
		}
	}


	/// <summary>
	/// Determines collision check shape.
	/// See: <see cref="Pathfinding.GraphCollision"/>
	/// </summary>
	public enum ColliderType {
		/// <summary>Uses a Sphere, Physics.CheckSphere. In 2D this is a circle instead.</summary>
		Sphere,
		/// <summary>Uses a Capsule, Physics.CheckCapsule. This will behave identically to the Sphere mode in 2D.</summary>
		Capsule,
		/// <summary>Uses a Ray, Physics.Linecast. In 2D this is a single point instead.</summary>
		Ray
	}

	/// <summary>Determines collision check ray direction</summary>
	public enum RayDirection {
		Up,     /// <summary>< Casts the ray from the bottom upwards</summary>
		Down,   /// <summary>< Casts the ray from the top downwards</summary>
		Both    /// <summary>< Casts two rays in both directions</summary>
	}
}
