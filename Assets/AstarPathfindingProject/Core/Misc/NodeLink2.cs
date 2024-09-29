using UnityEngine;
using System.Collections.Generic;

namespace Pathfinding {
	using Pathfinding.Util;

	/// <summary>
	/// Connects two nodes via two intermediate point nodes.
	/// In contrast to the NodeLink component, this link type will not connect the nodes directly
	/// instead it will create two point nodes at the start and end position of this link and connect
	/// through those nodes.
	///
	/// If the closest node to this object is called A and the closest node to the end transform is called
	/// D, then it will create one point node at this object's position (call it B) and one point node at
	/// the position of the end transform (call it C), it will then connect A to B, B to C and C to D.
	///
	/// This link type is possible to detect while following since it has these special point nodes in the middle.
	/// The link corresponding to one of those intermediate nodes can be retrieved using the <see cref="GetNodeLink"/> method
	/// which can be of great use if you want to, for example, play a link specific animation when reaching the link.
	///
	/// See: The example scene RecastExample2 contains a few links which you can take a look at to see how they are used.
	/// </summary>
	[AddComponentMenu("Pathfinding/Link2")]
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_node_link2.php")]
	public class NodeLink2 : GraphModifier {
		protected static Dictionary<GraphNode, NodeLink2> reference = new Dictionary<GraphNode, NodeLink2>();
		public static NodeLink2 GetNodeLink (GraphNode node) {
			NodeLink2 v;

			reference.TryGetValue(node, out v);
			return v;
		}

		/// <summary>End position of the link</summary>
		public Transform end;

		/// <summary>
		/// The connection will be this times harder/slower to traverse.
		/// Note that values lower than 1 will not always make the pathfinder choose this path instead of another path even though this one should
		/// lead to a lower total cost unless you also adjust the Heuristic Scale in A* Inspector -> Settings -> Pathfinding or disable the heuristic altogether.
		/// </summary>
		public float costFactor = 1.0f;

		/// <summary>Make a one-way connection</summary>
		public bool oneWay = false;

		public Transform StartTransform {
			get { return transform; }
		}

		public Transform EndTransform {
			get { return end; }
		}

		public PointNode startNode { get; private set; }
		public PointNode endNode { get; private set; }
		GraphNode connectedNode1, connectedNode2;
		Vector3 clamped1, clamped2;
		bool postScanCalled = false;

		[System.Obsolete("Use startNode instead (lowercase s)")]
		public GraphNode StartNode {
			get { return startNode; }
		}

		[System.Obsolete("Use endNode instead (lowercase e)")]
		public GraphNode EndNode {
			get { return endNode; }
		}

		public override void OnPostScan () {
			InternalOnPostScan();
		}

		public void InternalOnPostScan () {
			if (EndTransform == null || StartTransform == null) return;

#if ASTAR_NO_POINT_GRAPH
			throw new System.Exception("Point graph is not included. Check your A* optimization settings.");
#else
			if (AstarPath.active.data.pointGraph == null) {
				var graph = AstarPath.active.data.AddGraph(typeof(PointGraph)) as PointGraph;
				graph.name = "PointGraph (used for node links)";
			}

			if (startNode != null && startNode.Destroyed) {
				reference.Remove(startNode);
				startNode = null;
			}

			if (endNode != null && endNode.Destroyed) {
				reference.Remove(endNode);
				endNode = null;
			}

			// Create new nodes on the point graph
			if (startNode == null) startNode = AstarPath.active.data.pointGraph.AddNode((Int3)StartTransform.position);
			if (endNode == null) endNode = AstarPath.active.data.pointGraph.AddNode((Int3)EndTransform.position);

			connectedNode1 = null;
			connectedNode2 = null;

			if (startNode == null || endNode == null) {
				startNode = null;
				endNode = null;
				return;
			}

			postScanCalled = true;
			reference[startNode] = this;
			reference[endNode] = this;
			Apply(true);
#endif
		}

		public override void OnGraphsPostUpdate () {
			// Don't bother running it now since OnPostScan will be called later anyway
			if (AstarPath.active.isScanning)
				return;

			if (connectedNode1 != null && connectedNode1.Destroyed) {
				connectedNode1 = null;
			}
			if (connectedNode2 != null && connectedNode2.Destroyed) {
				connectedNode2 = null;
			}

			if (!postScanCalled) {
				OnPostScan();
			} else {
				Apply(false);
			}
		}

		protected override void OnEnable () {
			base.OnEnable();

#if !ASTAR_NO_POINT_GRAPH
			if (Application.isPlaying && AstarPath.active != null && AstarPath.active.data != null && AstarPath.active.data.pointGraph != null && !AstarPath.active.isScanning) {
				// Call OnGraphsPostUpdate as soon as possible when it is safe to update the graphs
				AstarPath.active.AddWorkItem(OnGraphsPostUpdate);
			}
#endif
		}

		protected override void OnDisable () {
			base.OnDisable();

			postScanCalled = false;

			if (startNode != null) reference.Remove(startNode);
			if (endNode != null) reference.Remove(endNode);

			if (startNode != null && endNode != null) {
				startNode.RemoveConnection(endNode);
				endNode.RemoveConnection(startNode);

				if (connectedNode1 != null && connectedNode2 != null) {
					startNode.RemoveConnection(connectedNode1);
					connectedNode1.RemoveConnection(startNode);

					endNode.RemoveConnection(connectedNode2);
					connectedNode2.RemoveConnection(endNode);
				}
			}
		}

		void RemoveConnections (GraphNode node) {
			//TODO, might be better to replace connection
			node.ClearConnections(true);
		}

		[ContextMenu("Recalculate neighbours")]
		void ContextApplyForce () {
			if (Application.isPlaying) {
				Apply(true);
			}
		}

		public void Apply (bool forceNewCheck) {
			//TODO
			//This function assumes that connections from the n1,n2 nodes never need to be removed in the future (e.g because the nodes move or something)
			NNConstraint nn = NNConstraint.None;
			int graph = (int)startNode.GraphIndex;

			//Search all graphs but the one which start and end nodes are on
			nn.graphMask = ~(1 << graph);

			startNode.SetPosition((Int3)StartTransform.position);
			endNode.SetPosition((Int3)EndTransform.position);

			RemoveConnections(startNode);
			RemoveConnections(endNode);

			uint cost = (uint)Mathf.RoundToInt(((Int3)(StartTransform.position-EndTransform.position)).costMagnitude*costFactor);
			startNode.AddConnection(endNode, cost);
			endNode.AddConnection(startNode, cost);

			if (connectedNode1 == null || forceNewCheck) {
				var info = AstarPath.active.GetNearest(StartTransform.position, nn);
				connectedNode1 = info.node;
				clamped1 = info.position;
			}

			if (connectedNode2 == null || forceNewCheck) {
				var info = AstarPath.active.GetNearest(EndTransform.position, nn);
				connectedNode2 = info.node;
				clamped2 = info.position;
			}

			if (connectedNode2 == null || connectedNode1 == null) return;

			//Add connections between nodes, or replace old connections if existing
			connectedNode1.AddConnection(startNode, (uint)Mathf.RoundToInt(((Int3)(clamped1 - StartTransform.position)).costMagnitude*costFactor));
			if (!oneWay) connectedNode2.AddConnection(endNode, (uint)Mathf.RoundToInt(((Int3)(clamped2 - EndTransform.position)).costMagnitude*costFactor));

			if (!oneWay) startNode.AddConnection(connectedNode1, (uint)Mathf.RoundToInt(((Int3)(clamped1 - StartTransform.position)).costMagnitude*costFactor));
			endNode.AddConnection(connectedNode2, (uint)Mathf.RoundToInt(((Int3)(clamped2 - EndTransform.position)).costMagnitude*costFactor));
		}

		private readonly static Color GizmosColor = new Color(206.0f/255.0f, 136.0f/255.0f, 48.0f/255.0f, 0.5f);
		private readonly static Color GizmosColorSelected = new Color(235.0f/255.0f, 123.0f/255.0f, 32.0f/255.0f, 1.0f);

		public virtual void OnDrawGizmosSelected () {
			OnDrawGizmos(true);
		}

		public void OnDrawGizmos () {
			OnDrawGizmos(false);
		}

		public void OnDrawGizmos (bool selected) {
			Color color = selected ? GizmosColorSelected : GizmosColor;

			if (StartTransform != null) {
				Draw.Gizmos.CircleXZ(StartTransform.position, 0.4f, color);
			}
			if (EndTransform != null) {
				Draw.Gizmos.CircleXZ(EndTransform.position, 0.4f, color);
			}

			if (StartTransform != null && EndTransform != null) {
				Draw.Gizmos.Bezier(StartTransform.position, EndTransform.position, color);
				if (selected) {
					Vector3 cross = Vector3.Cross(Vector3.up, (EndTransform.position-StartTransform.position)).normalized;
					Draw.Gizmos.Bezier(StartTransform.position+cross*0.1f, EndTransform.position+cross*0.1f, color);
					Draw.Gizmos.Bezier(StartTransform.position-cross*0.1f, EndTransform.position-cross*0.1f, color);
				}
			}
		}

		internal static void SerializeReferences (Pathfinding.Serialization.GraphSerializationContext ctx) {
			var links = GetModifiersOfType<NodeLink2>();

			ctx.writer.Write(links.Count);
			foreach (var link in links) {
				ctx.writer.Write(link.uniqueID);
				ctx.SerializeNodeReference(link.startNode);
				ctx.SerializeNodeReference(link.endNode);
				ctx.SerializeNodeReference(link.connectedNode1);
				ctx.SerializeNodeReference(link.connectedNode2);
				ctx.SerializeVector3(link.clamped1);
				ctx.SerializeVector3(link.clamped2);
				ctx.writer.Write(link.postScanCalled);
			}
		}

		internal static void DeserializeReferences (Pathfinding.Serialization.GraphSerializationContext ctx) {
			int count = ctx.reader.ReadInt32();

			for (int i = 0; i < count; i++) {
				var linkID = ctx.reader.ReadUInt64();
				var startNode = ctx.DeserializeNodeReference();
				var endNode = ctx.DeserializeNodeReference();
				var connectedNode1 = ctx.DeserializeNodeReference();
				var connectedNode2 = ctx.DeserializeNodeReference();
				var clamped1 = ctx.DeserializeVector3();
				var clamped2 = ctx.DeserializeVector3();
				var postScanCalled = ctx.reader.ReadBoolean();

				GraphModifier link;
				if (usedIDs.TryGetValue(linkID, out link)) {
					var link2 = link as NodeLink2;
					if (link2 != null) {
						if (startNode != null) reference[startNode] = link2;
						if (endNode != null) reference[endNode] = link2;

						// If any nodes happened to be registered right now
						if (link2.startNode != null) reference.Remove(link2.startNode);
						if (link2.endNode != null) reference.Remove(link2.endNode);

						link2.startNode = startNode as PointNode;
						link2.endNode = endNode as PointNode;
						link2.connectedNode1 = connectedNode1;
						link2.connectedNode2 = connectedNode2;
						link2.postScanCalled = postScanCalled;
						link2.clamped1 = clamped1;
						link2.clamped2 = clamped2;
					} else {
						throw new System.Exception("Tried to deserialize a NodeLink2 reference, but the link was not of the correct type or it has been destroyed.\nIf a NodeLink2 is included in serialized graph data, the same NodeLink2 component must be present in the scene when loading the graph data.");
					}
				} else {
					throw new System.Exception("Tried to deserialize a NodeLink2 reference, but the link could not be found in the scene.\nIf a NodeLink2 is included in serialized graph data, the same NodeLink2 component must be present in the scene when loading the graph data.");
				}
			}
		}
	}
}
