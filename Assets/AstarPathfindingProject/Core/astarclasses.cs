using UnityEngine;
using System.Collections.Generic;

// Empty namespace declaration to avoid errors in the free version
// Which does not have any classes in the RVO namespace
namespace Pathfinding.RVO {}

namespace Pathfinding {
	using Pathfinding.Util;

#if UNITY_5_0
	/// <summary>Used in Unity 5.0 since the HelpURLAttribute was first added in Unity 5.1</summary>
	public class HelpURLAttribute : Attribute {
	}
#endif

	[System.Serializable]
	/// <summary>Stores editor colors</summary>
	public class AstarColor {
		public Color _SolidColor;
		public Color _UnwalkableNode;
		public Color _BoundsHandles;

		public Color _ConnectionLowLerp;
		public Color _ConnectionHighLerp;

		public Color _MeshEdgeColor;

		/// <summary>
		/// Holds user set area colors.
		/// Use GetAreaColor to get an area color
		/// </summary>
		public Color[] _AreaColors;

		public static Color SolidColor = new Color(30/255f, 102/255f, 201/255f, 0.9F);
		public static Color UnwalkableNode = new Color(1, 0, 0, 0.5F);
		public static Color BoundsHandles = new Color(0.29F, 0.454F, 0.741F, 0.9F);

		public static Color ConnectionLowLerp = new Color(0, 1, 0, 0.5F);
		public static Color ConnectionHighLerp = new Color(1, 0, 0, 0.5F);

		public static Color MeshEdgeColor = new Color(0, 0, 0, 0.5F);

		private static Color[] AreaColors = new Color[1];

		public static int ColorHash () {
			var hash = SolidColor.GetHashCode() ^ UnwalkableNode.GetHashCode() ^ BoundsHandles.GetHashCode() ^ ConnectionLowLerp.GetHashCode() ^ ConnectionHighLerp.GetHashCode() ^ MeshEdgeColor.GetHashCode();

			for (int i = 0; i < AreaColors.Length; i++) hash = 7*hash ^ AreaColors[i].GetHashCode();
			return hash;
		}

		/// <summary>
		/// Returns an color for an area, uses both user set ones and calculated.
		/// If the user has set a color for the area, it is used, but otherwise the color is calculated using AstarMath.IntToColor
		/// See: <see cref="RemappedAreaColors"/>
		/// </summary>
		public static Color GetAreaColor (uint area) {
			if (area >= AreaColors.Length) return AstarMath.IntToColor((int)area, 1F);
			return AreaColors[(int)area];
		}

		/// <summary>
		/// Returns an color for a tag, uses both user set ones and calculated.
		/// If the user has set a color for the tag, it is used, but otherwise the color is calculated using AstarMath.IntToColor
		/// See: <see cref="AreaColors"/>
		/// </summary>
		public static Color GetTagColor (uint tag) {
			if (tag >= AreaColors.Length) return AstarMath.IntToColor((int)tag, 1F);
			return AreaColors[(int)tag];
		}

		/// <summary>
		/// Pushes all local variables out to static ones.
		/// This is done because that makes it so much easier to access the colors during Gizmo rendering
		/// and it has a positive performance impact as well (gizmo rendering is hot code).
		/// It is a bit ugly though, but oh well.
		/// </summary>
		public void PushToStatic (AstarPath astar) {
			_AreaColors  = _AreaColors ?? new Color[1];

			SolidColor = _SolidColor;
			UnwalkableNode = _UnwalkableNode;
			BoundsHandles = _BoundsHandles;
			ConnectionLowLerp = _ConnectionLowLerp;
			ConnectionHighLerp = _ConnectionHighLerp;
			MeshEdgeColor = _MeshEdgeColor;
			AreaColors = _AreaColors;
		}

		public AstarColor () {
			// Set default colors
			_SolidColor = new Color(30/255f, 102/255f, 201/255f, 0.9F);
			_UnwalkableNode = new Color(1, 0, 0, 0.5F);
			_BoundsHandles = new Color(0.29F, 0.454F, 0.741F, 0.9F);
			_ConnectionLowLerp = new Color(0, 1, 0, 0.5F);
			_ConnectionHighLerp = new Color(1, 0, 0, 0.5F);
			_MeshEdgeColor = new Color(0, 0, 0, 0.5F);
		}
	}


	/// <summary>
	/// Returned by graph ray- or linecasts containing info about the hit.
	/// This is the return value by the <see cref="Pathfinding.IRaycastableGraph.Linecast"/> methods.
	/// Some members will also be initialized even if nothing was hit, see the individual member descriptions for more info.
	///
	/// [Open online documentation to see images]
	/// </summary>
	public struct GraphHitInfo {
		/// <summary>
		/// Start of the line/ray.
		/// Note that the point passed to the Linecast method will be clamped to the closest point on the navmesh.
		/// </summary>
		public Vector3 origin;
		/// <summary>
		/// Hit point.
		/// In case no obstacle was hit then this will be set to the endpoint of the line.
		/// </summary>
		public Vector3 point;
		/// <summary>
		/// Node which contained the edge which was hit.
		/// If the linecast did not hit anything then this will be set to the last node along the line's path (the one which contains the endpoint).
		///
		/// For layered grid graphs the linecast will return true (i.e: no free line of sight) if when walking the graph we ended up at X,Z coordinate for the end node
		/// even if end node was on a different level (e.g the floor below or above in a building). In this case no node edge was really hit so this field will still be null.
		/// </summary>
		public GraphNode node;
		/// <summary>
		/// Where the tangent starts. <see cref="tangentOrigin"/> and <see cref="tangent"/> together actually describes the edge which was hit.
		/// [Open online documentation to see images]
		/// </summary>
		public Vector3 tangentOrigin;
		/// <summary>
		/// Tangent of the edge which was hit.
		/// [Open online documentation to see images]
		/// </summary>
		public Vector3 tangent;

		/// <summary>Distance from <see cref="origin"/> to <see cref="point"/></summary>
		public float distance {
			get {
				return (point-origin).magnitude;
			}
		}

		public GraphHitInfo (Vector3 point) {
			tangentOrigin  = Vector3.zero;
			origin = Vector3.zero;
			this.point = point;
			node = null;
			tangent = Vector3.zero;
		}
	}

	/// <summary>Nearest node constraint. Constrains which nodes will be returned by the <see cref="AstarPath.GetNearest"/> function</summary>
	public class NNConstraint {
		/// <summary>
		/// Graphs treated as valid to search on.
		/// This is a bitmask meaning that bit 0 specifies whether or not the first graph in the graphs list should be able to be included in the search,
		/// bit 1 specifies whether or not the second graph should be included and so on.
		/// <code>
		/// // Enables the first and third graphs to be included, but not the rest
		/// myNNConstraint.graphMask = (1 << 0) | (1 << 2);
		/// </code>
		/// <code>
		/// GraphMask mask1 = GraphMask.FromGraphName("My Grid Graph");
		/// GraphMask mask2 = GraphMask.FromGraphName("My Other Grid Graph");
		///
		/// NNConstraint nn = NNConstraint.Default;
		///
		/// nn.graphMask = mask1 | mask2;
		///
		/// // Find the node closest to somePoint which is either in 'My Grid Graph' OR in 'My Other Grid Graph'
		/// var info = AstarPath.active.GetNearest(somePoint, nn);
		/// </code>
		///
		/// Note: This does only affect which nodes are returned from a <see cref="AstarPath.GetNearest"/> call, if a valid graph is connected to an invalid graph using a node link then it might be searched anyway.
		///
		/// See: <see cref="AstarPath.GetNearest"/>
		/// See: <see cref="SuitableGraph"/>
		/// See: bitmasks (view in online documentation for working links)
		/// </summary>
		public GraphMask graphMask = -1;

		/// <summary>Only treat nodes in the area <see cref="area"/> as suitable. Does not affect anything if <see cref="area"/> is less than 0 (zero)</summary>
		public bool constrainArea;

		/// <summary>Area ID to constrain to. Will not affect anything if less than 0 (zero) or if <see cref="constrainArea"/> is false</summary>
		public int area = -1;

		/// <summary>Constrain the search to only walkable or unwalkable nodes depending on <see cref="walkable"/>.</summary>
		public bool constrainWalkability = true;

		/// <summary>
		/// Only search for walkable or unwalkable nodes if <see cref="constrainWalkability"/> is enabled.
		/// If true, only walkable nodes will be searched for, otherwise only unwalkable nodes will be searched for.
		/// Does not affect anything if <see cref="constrainWalkability"/> if false.
		/// </summary>
		public bool walkable = true;

		/// <summary>
		/// if available, do an XZ check instead of checking on all axes.
		/// The navmesh/recast graph supports this.
		///
		/// This can be important on sloped surfaces. See the image below in which the closest point for each blue point is queried for:
		/// [Open online documentation to see images]
		///
		/// The navmesh/recast graphs also contain a global option for this: <see cref="Pathfinding.NavmeshBase.nearestSearchOnlyXZ"/>.
		/// </summary>
		public bool distanceXZ;

		/// <summary>
		/// Sets if tags should be constrained.
		/// See: <see cref="tags"/>
		/// </summary>
		public bool constrainTags = true;

		/// <summary>
		/// Nodes which have any of these tags set are suitable.
		/// This is a bitmask, i.e bit 0 indicates that tag 0 is good, bit 3 indicates tag 3 is good etc.
		/// See: <see cref="constrainTags"/>
		/// See: <see cref="graphMask"/>
		/// See: bitmasks (view in online documentation for working links)
		/// </summary>
		public int tags = -1;

		/// <summary>
		/// Constrain distance to node.
		/// Uses distance from <see cref="AstarPath.maxNearestNodeDistance"/>.
		/// If this is false, it will completely ignore the distance limit.
		///
		/// If there are no suitable nodes within the distance limit then the search will terminate with a null node as a result.
		/// Note: This value is not used in this class, it is used by the AstarPath.GetNearest function.
		/// </summary>
		public bool constrainDistance = true;

		/// <summary>
		/// Returns whether or not the graph conforms to this NNConstraint's rules.
		/// Note that only the first 31 graphs are considered using this function.
		/// If the <see cref="graphMask"/> has bit 31 set (i.e the last graph possible to fit in the mask), all graphs
		/// above index 31 will also be considered suitable.
		/// </summary>
		public virtual bool SuitableGraph (int graphIndex, NavGraph graph) {
			return graphMask.Contains(graphIndex);
		}

		/// <summary>Returns whether or not the node conforms to this NNConstraint's rules</summary>
		public virtual bool Suitable (GraphNode node) {
			if (constrainWalkability && node.Walkable != walkable) return false;

			if (constrainArea && area >= 0 && node.Area != area) return false;

			if (constrainTags && ((tags >> (int)node.Tag) & 0x1) == 0) return false;

			return true;
		}

		/// <summary>
		/// The default NNConstraint.
		/// Equivalent to new NNConstraint ().
		/// This NNConstraint has settings which works for most, it only finds walkable nodes
		/// and it constrains distance set by A* Inspector -> Settings -> Max Nearest Node Distance
		/// </summary>
		public static NNConstraint Default {
			get {
				return new NNConstraint();
			}
		}

		/// <summary>Returns a constraint which does not filter the results</summary>
		public static NNConstraint None {
			get {
				return new NNConstraint {
						   constrainWalkability = false,
						   constrainArea = false,
						   constrainTags = false,
						   constrainDistance = false,
						   graphMask = -1,
				};
			}
		}

		/// <summary>Default constructor. Equals to the property <see cref="Default"/></summary>
		public NNConstraint () {
		}
	}

	/// <summary>
	/// A special NNConstraint which can use different logic for the start node and end node in a path.
	/// A PathNNConstraint can be assigned to the Path.nnConstraint field, the path will first search for the start node, then it will call <see cref="SetStart"/> and proceed with searching for the end node (nodes in the case of a MultiTargetPath).\n
	/// The default PathNNConstraint will constrain the end point to lie inside the same area as the start point.
	/// </summary>
	public class PathNNConstraint : NNConstraint {
		public static new PathNNConstraint Default {
			get {
				return new PathNNConstraint {
						   constrainArea = true
				};
			}
		}

		/// <summary>Called after the start node has been found. This is used to get different search logic for the start and end nodes in a path</summary>
		public virtual void SetStart (GraphNode node) {
			if (node != null) {
				area = (int)node.Area;
			} else {
				constrainArea = false;
			}
		}
	}

	/// <summary>
	/// Internal result of a nearest node query.
	/// See: NNInfo
	/// </summary>
	public struct NNInfoInternal {
		/// <summary>
		/// Closest node found.
		/// This node is not necessarily accepted by any NNConstraint passed.
		/// See: constrainedNode
		/// </summary>
		public GraphNode node;

		/// <summary>
		/// Optional to be filled in.
		/// If the search will be able to find the constrained node without any extra effort it can fill it in.
		/// </summary>
		public GraphNode constrainedNode;

		/// <summary>The position clamped to the closest point on the <see cref="node"/>.</summary>
		public Vector3 clampedPosition;

		/// <summary>Clamped position for the optional constrainedNode</summary>
		public Vector3 constClampedPosition;

		public NNInfoInternal (GraphNode node) {
			this.node = node;
			constrainedNode = null;
			clampedPosition = Vector3.zero;
			constClampedPosition = Vector3.zero;

			UpdateInfo();
		}

		/// <summary>Updates <see cref="clampedPosition"/> and <see cref="constClampedPosition"/> from node positions</summary>
		public void UpdateInfo () {
			clampedPosition = node != null ? (Vector3)node.position : Vector3.zero;
			constClampedPosition = constrainedNode != null ? (Vector3)constrainedNode.position : Vector3.zero;
		}
	}

	/// <summary>Result of a nearest node query</summary>
	public struct NNInfo {
		/// <summary>Closest node</summary>
		public readonly GraphNode node;

		/// <summary>
		/// Closest point on the navmesh.
		/// This is the query position clamped to the closest point on the <see cref="node"/>.
		/// </summary>
		public readonly Vector3 position;

		/// <summary>
		/// Closest point on the navmesh.
		/// Deprecated: This field has been renamed to <see cref="position"/>
		/// </summary>
		[System.Obsolete("This field has been renamed to 'position'")]
		public Vector3 clampedPosition {
			get {
				return position;
			}
		}

		public NNInfo (NNInfoInternal internalInfo) {
			node = internalInfo.node;
			position = internalInfo.clampedPosition;
		}

		public static explicit operator Vector3(NNInfo ob) {
			return ob.position;
		}

		public static explicit operator GraphNode(NNInfo ob) {
			return ob.node;
		}
	}

	/// <summary>
	/// Progress info for e.g a progressbar.
	/// Used by the scan functions in the project
	/// See: <see cref="AstarPath.ScanAsync"/>
	/// </summary>
	public struct Progress {
		/// <summary>Current progress as a value between 0 and 1</summary>
		public readonly float progress;
		/// <summary>Description of what is currently being done</summary>
		public readonly string description;

		public Progress (float progress, string description) {
			this.progress = progress;
			this.description = description;
		}

		public Progress MapTo (float min, float max, string prefix = null) {
			return new Progress(Mathf.Lerp(min, max, progress), prefix + description);
		}

		public override string ToString () {
			return progress.ToString("0.0") + " " + description;
		}
	}

	/// <summary>Graphs which can be updated during runtime</summary>
	public interface IUpdatableGraph {
		/// <summary>
		/// Updates an area using the specified <see cref="GraphUpdateObject"/>.
		///
		/// Notes to implementators.
		/// This function should (in order):
		/// -# Call o.WillUpdateNode on the GUO for every node it will update, it is important that this is called BEFORE any changes are made to the nodes.
		/// -# Update walkabilty using special settings such as the usePhysics flag used with the GridGraph.
		/// -# Call Apply on the GUO for every node which should be updated with the GUO.
		/// -# Update connectivity info if appropriate (GridGraphs updates connectivity, but most other graphs don't since then the connectivity cannot be recovered later).
		/// </summary>
		void UpdateArea (GraphUpdateObject o);

		/// <summary>
		/// May be called on the Unity thread before starting the update.
		/// See: CanUpdateAsync
		/// </summary>
		void UpdateAreaInit (GraphUpdateObject o);

		/// <summary>
		/// May be called on the Unity thread after executing the update.
		/// See: CanUpdateAsync
		/// </summary>
		void UpdateAreaPost (GraphUpdateObject o);

		GraphUpdateThreading CanUpdateAsync (GraphUpdateObject o);
	}

	/// <summary>
	/// Represents a collection of settings used to update nodes in a specific region of a graph.
	/// See: AstarPath.UpdateGraphs
	/// See: graph-updates (view in online documentation for working links)
	/// </summary>
	public class GraphUpdateObject {
		/// <summary>
		/// The bounds to update nodes within.
		/// Defined in world space.
		/// </summary>
		public Bounds bounds;

		/// <summary>
		/// Controlls if a flood fill will be carried out after this GUO has been applied.
		/// Disabling this can be used to gain a performance boost, but use with care.
		/// If you are sure that a GUO will not modify walkability or connections. You can set this to false.
		/// For example when only updating penalty values it can save processing power when setting this to false. Especially on large graphs.
		/// Note: If you set this to false, even though it does change e.g walkability, it can lead to paths returning that they failed even though there is a path,
		/// or the try to search the whole graph for a path even though there is none, and will in the processes use wast amounts of processing power.
		///
		/// If using the basic GraphUpdateObject (not a derived class), a quick way to check if it is going to need a flood fill is to check if <see cref="modifyWalkability"/> is true or <see cref="updatePhysics"/> is true.
		///
		/// Deprecated: Not necessary anymore
		/// </summary>
		[System.Obsolete("Not necessary anymore")]
		public bool requiresFloodFill { set {} }

		/// <summary>
		/// Use physics checks to update nodes.
		/// When updating a grid graph and this is true, the nodes' position and walkability will be updated using physics checks
		/// with settings from "Collision Testing" and "Height Testing".
		///
		/// When updating a PointGraph, setting this to true will make it re-evaluate all connections in the graph which passes through the <see cref="bounds"/>.
		/// This has no effect when updating GridGraphs if <see cref="modifyWalkability"/> is turned on.
		///
		/// On RecastGraphs, having this enabled will trigger a complete recalculation of all tiles intersecting the bounds.
		/// This is quite slow (but powerful). If you only want to update e.g penalty on existing nodes, leave it disabled.
		/// </summary>
		public bool updatePhysics = true;

		/// <summary>
		/// Reset penalties to their initial values when updating grid graphs and <see cref="updatePhysics"/> is true.
		/// If you want to keep old penalties even when you update the graph you may want to disable this option.
		///
		/// The images below shows two overlapping graph update objects, the right one happened to be applied before the left one. They both have updatePhysics = true and are
		/// set to increase the penalty of the nodes by some amount.
		///
		/// The first image shows the result when resetPenaltyOnPhysics is false. Both penalties are added correctly.
		/// [Open online documentation to see images]
		///
		/// This second image shows when resetPenaltyOnPhysics is set to true. The first GUO is applied correctly, but then the second one (the left one) is applied
		/// and during its updating, it resets the penalties first and then adds penalty to the nodes. The result is that the penalties from both GUOs are not added together.
		/// The green patch in at the border is there because physics recalculation (recalculation of the position of the node, checking for obstacles etc.) affects a slightly larger
		/// area than the original GUO bounds because of the Grid Graph -> Collision Testing -> Diameter setting (it is enlarged by that value). So some extra nodes have their penalties reset.
		///
		/// [Open online documentation to see images]
		/// </summary>
		public bool resetPenaltyOnPhysics = true;

		/// <summary>
		/// Update Erosion for GridGraphs.
		/// When enabled, erosion will be recalculated for grid graphs
		/// after the GUO has been applied.
		///
		/// In the below image you can see the different effects you can get with the different values.\n
		/// The first image shows the graph when no GUO has been applied. The blue box is not identified as an obstacle by the graph, the reason
		/// there are unwalkable nodes around it is because there is a height difference (nodes are placed on top of the box) so erosion will be applied (an erosion value of 2 is used in this graph).
		/// The orange box is identified as an obstacle, so the area of unwalkable nodes around it is a bit larger since both erosion and collision has made
		/// nodes unwalkable.\n
		/// The GUO used simply sets walkability to true, i.e making all nodes walkable.
		///
		/// [Open online documentation to see images]
		///
		/// When updateErosion=True, the reason the blue box still has unwalkable nodes around it is because there is still a height difference
		/// so erosion will still be applied. The orange box on the other hand has no height difference and all nodes are set to walkable.\n
		/// \n
		/// When updateErosion=False, all nodes walkability are simply set to be walkable in this example.
		///
		/// See: Pathfinding.GridGraph
		/// </summary>
		public bool updateErosion = true;

		/// <summary>
		/// NNConstraint to use.
		/// The Pathfinding.NNConstraint.SuitableGraph function will be called on the NNConstraint to enable filtering of which graphs to update.\n
		/// Note: As the Pathfinding.NNConstraint.SuitableGraph function is A* Pathfinding Project Pro only, this variable doesn't really affect anything in the free version.
		/// </summary>
		public NNConstraint nnConstraint = NNConstraint.None;

		/// <summary>
		/// Penalty to add to the nodes.
		/// A penalty of 1000 is equivalent to the cost of moving 1 world unit.
		/// </summary>
		public int addPenalty;

		/// <summary>If true, all nodes' walkable variable will be set to <see cref="setWalkability"/></summary>
		public bool modifyWalkability;

		/// <summary>If <see cref="modifyWalkability"/> is true, the nodes' walkable variable will be set to this value</summary>
		public bool setWalkability;

		/// <summary>If true, all nodes' tag will be set to <see cref="setTag"/></summary>
		public bool modifyTag;

		/// <summary>If <see cref="modifyTag"/> is true, all nodes' tag will be set to this value</summary>
		public int setTag;

		/// <summary>
		/// Track which nodes are changed and save backup data.
		/// Used internally to revert changes if needed.
		/// </summary>
		public bool trackChangedNodes;

		/// <summary>
		/// Nodes which were updated by this GraphUpdateObject.
		/// Will only be filled if <see cref="trackChangedNodes"/> is true.
		/// Note: It might take a few frames for graph update objects to be applied.
		/// If you need this info immediately, use <see cref="AstarPath.FlushGraphUpdates"/>.
		/// </summary>
		public List<GraphNode> changedNodes;
		private List<uint> backupData;
		private List<Int3> backupPositionData;

		/// <summary>
		/// A shape can be specified if a bounds object does not give enough precision.
		/// Note that if you set this, you should set the bounds so that it encloses the shape
		/// because the bounds will be used as an initial fast check for which nodes that should
		/// be updated.
		/// </summary>
		public GraphUpdateShape shape;

		/// <summary>
		/// Should be called on every node which is updated with this GUO before it is updated.
		/// See: <see cref="trackChangedNodes"/>
		/// </summary>
		/// <param name="node">The node to save fields for. If null, nothing will be done</param>
		public virtual void WillUpdateNode (GraphNode node) {
			if (trackChangedNodes && node != null) {
				if (changedNodes == null) { changedNodes = ListPool<GraphNode>.Claim (); backupData = ListPool<uint>.Claim (); backupPositionData = ListPool<Int3>.Claim (); }
				changedNodes.Add(node);
				backupPositionData.Add(node.position);
				backupData.Add(node.Penalty);
				backupData.Add(node.Flags);
#if !ASTAR_NO_GRID_GRAPH
				var gridNode = node as GridNode;
				if (gridNode != null) backupData.Add(gridNode.InternalGridFlags);
#endif
			}
		}

		/// <summary>
		/// Reverts penalties and flags (which includes walkability) on every node which was updated using this GUO.
		/// Data for reversion is only saved if <see cref="trackChangedNodes"/> is true.
		///
		/// Note: Not all data is saved. The saved data includes: penalties, walkability, tags, area, position and for grid graphs (not layered) it also includes connection data.
		///
		/// This method modifies the graph. So it must be called inside while it is safe to modify the graph, for example inside a work item as shown in the example below.
		///
		/// \miscsnippets MiscSnippets.cs GraphUpdateObject.RevertFromBackup
		///
		/// See: blocking (view in online documentation for working links)
		/// See: <see cref="Pathfinding.PathUtilities.UpdateGraphsNoBlock"/>
		/// </summary>
		public virtual void RevertFromBackup () {
			if (trackChangedNodes) {
				if (changedNodes == null) return;

				int counter = 0;
				for (int i = 0; i < changedNodes.Count; i++) {
					changedNodes[i].Penalty = backupData[counter];
					counter++;
					// Restore the flags, but not the HierarchicalNodeIndex as that could screw up some internal datastructures
					var tmp = changedNodes[i].HierarchicalNodeIndex;
					changedNodes[i].Flags = backupData[counter];
					changedNodes[i].HierarchicalNodeIndex = tmp;
					counter++;
#if !ASTAR_NO_GRID_GRAPH
					var gridNode = changedNodes[i] as GridNode;
					if (gridNode != null) {
						gridNode.InternalGridFlags = (ushort)backupData[counter];
						counter++;
					}
#endif
					changedNodes[i].position = backupPositionData[i];
					changedNodes[i].SetConnectivityDirty();
				}

				ListPool<GraphNode>.Release (ref changedNodes);
				ListPool<uint>.Release (ref backupData);
				ListPool<Int3>.Release (ref backupPositionData);
			} else {
				throw new System.InvalidOperationException("Changed nodes have not been tracked, cannot revert from backup. Please set trackChangedNodes to true before applying the update.");
			}
		}

		/// <summary>Updates the specified node using this GUO's settings</summary>
		public virtual void Apply (GraphNode node) {
			if (shape == null || shape.Contains(node)) {
				//Update penalty and walkability
				node.Penalty = (uint)(node.Penalty+addPenalty);
				if (modifyWalkability) {
					node.Walkable = setWalkability;
				}

				//Update tags
				if (modifyTag) node.Tag = (uint)setTag;
			}
		}

		public GraphUpdateObject () {
		}

		/// <summary>Creates a new GUO with the specified bounds</summary>
		public GraphUpdateObject (Bounds b) {
			bounds = b;
		}
	}

	/// <summary>Graph which has a well defined transformation from graph space to world space</summary>
	public interface ITransformedGraph {
		GraphTransform transform { get; }
	}

	/// <summary>Graph which supports the Linecast method</summary>
	public interface IRaycastableGraph {
		bool Linecast (Vector3 start, Vector3 end);
		bool Linecast (Vector3 start, Vector3 end, GraphNode hint);
		bool Linecast (Vector3 start, Vector3 end, GraphNode hint, out GraphHitInfo hit);
		bool Linecast (Vector3 start, Vector3 end, GraphNode hint, out GraphHitInfo hit, List<GraphNode> trace);
	}

	/// <summary>
	/// Integer Rectangle.
	/// Works almost like UnityEngine.Rect but with integer coordinates
	/// </summary>
	[System.Serializable]
	public struct IntRect {
		public int xmin, ymin, xmax, ymax;

		public IntRect (int xmin, int ymin, int xmax, int ymax) {
			this.xmin = xmin;
			this.xmax = xmax;
			this.ymin = ymin;
			this.ymax = ymax;
		}

		public bool Contains (int x, int y) {
			return !(x < xmin || y < ymin || x > xmax || y > ymax);
		}

		public int Width {
			get {
				return xmax-xmin+1;
			}
		}

		public int Height {
			get {
				return ymax-ymin+1;
			}
		}

		/// <summary>
		/// Returns if this rectangle is valid.
		/// An invalid rect could have e.g xmin > xmax.
		/// Rectamgles with a zero area area invalid.
		/// </summary>
		public bool IsValid () {
			return xmin <= xmax && ymin <= ymax;
		}

		public static bool operator == (IntRect a, IntRect b) {
			return a.xmin == b.xmin && a.xmax == b.xmax && a.ymin == b.ymin && a.ymax == b.ymax;
		}

		public static bool operator != (IntRect a, IntRect b) {
			return a.xmin != b.xmin || a.xmax != b.xmax || a.ymin != b.ymin || a.ymax != b.ymax;
		}

		public override bool Equals (System.Object obj) {
			var rect = (IntRect)obj;

			return xmin == rect.xmin && xmax == rect.xmax && ymin == rect.ymin && ymax == rect.ymax;
		}

		public override int GetHashCode () {
			return xmin*131071 ^ xmax*3571 ^ ymin*3109 ^ ymax*7;
		}

		/// <summary>
		/// Returns the intersection rect between the two rects.
		/// The intersection rect is the area which is inside both rects.
		/// If the rects do not have an intersection, an invalid rect is returned.
		/// See: IsValid
		/// </summary>
		public static IntRect Intersection (IntRect a, IntRect b) {
			return new IntRect(
				System.Math.Max(a.xmin, b.xmin),
				System.Math.Max(a.ymin, b.ymin),
				System.Math.Min(a.xmax, b.xmax),
				System.Math.Min(a.ymax, b.ymax)
				);
		}

		/// <summary>Returns if the two rectangles intersect each other</summary>
		public static bool Intersects (IntRect a, IntRect b) {
			return !(a.xmin > b.xmax || a.ymin > b.ymax || a.xmax < b.xmin || a.ymax < b.ymin);
		}

		/// <summary>
		/// Returns a new rect which contains both input rects.
		/// This rectangle may contain areas outside both input rects as well in some cases.
		/// </summary>
		public static IntRect Union (IntRect a, IntRect b) {
			return new IntRect(
				System.Math.Min(a.xmin, b.xmin),
				System.Math.Min(a.ymin, b.ymin),
				System.Math.Max(a.xmax, b.xmax),
				System.Math.Max(a.ymax, b.ymax)
				);
		}

		/// <summary>Returns a new IntRect which is expanded to contain the point</summary>
		public IntRect ExpandToContain (int x, int y) {
			return new IntRect(
				System.Math.Min(xmin, x),
				System.Math.Min(ymin, y),
				System.Math.Max(xmax, x),
				System.Math.Max(ymax, y)
				);
		}

		/// <summary>Returns a new rect which is expanded by range in all directions.</summary>
		/// <param name="range">How far to expand. Negative values are permitted.</param>
		public IntRect Expand (int range) {
			return new IntRect(xmin-range,
				ymin-range,
				xmax+range,
				ymax+range
				);
		}

		public override string ToString () {
			return "[x: "+xmin+"..."+xmax+", y: " + ymin +"..."+ymax+"]";
		}

		/// <summary>Draws some debug lines representing the rect</summary>
		public void DebugDraw (GraphTransform transform, Color color) {
			Vector3 p1 = transform.Transform(new Vector3(xmin, 0, ymin));
			Vector3 p2 = transform.Transform(new Vector3(xmin, 0, ymax));
			Vector3 p3 = transform.Transform(new Vector3(xmax, 0, ymax));
			Vector3 p4 = transform.Transform(new Vector3(xmax, 0, ymin));

			Debug.DrawLine(p1, p2, color);
			Debug.DrawLine(p2, p3, color);
			Debug.DrawLine(p3, p4, color);
			Debug.DrawLine(p4, p1, color);
		}
	}

	/// <summary>
	/// Holds a bitmask of graphs.
	/// This bitmask can hold up to 32 graphs.
	///
	/// The bitmask can be converted to and from integers implicitly.
	///
	/// <code>
	/// GraphMask mask1 = GraphMask.FromGraphName("My Grid Graph");
	/// GraphMask mask2 = GraphMask.FromGraphName("My Other Grid Graph");
	///
	/// NNConstraint nn = NNConstraint.Default;
	///
	/// nn.graphMask = mask1 | mask2;
	///
	/// // Find the node closest to somePoint which is either in 'My Grid Graph' OR in 'My Other Grid Graph'
	/// var info = AstarPath.active.GetNearest(somePoint, nn);
	/// </code>
	///
	/// See: bitmasks (view in online documentation for working links)
	/// </summary>
	[System.Serializable]
	public struct GraphMask {
		/// <summary>Bitmask representing the mask</summary>
		public int value;

		/// <summary>A mask containing every graph</summary>
		public static GraphMask everything { get { return new GraphMask(-1); } }

		public GraphMask (int value) {
			this.value = value;
		}

		public static implicit operator int(GraphMask mask) {
			return mask.value;
		}

		public static implicit operator GraphMask (int mask) {
			return new GraphMask(mask);
		}

		/// <summary>Combines two masks to form the intersection between them</summary>
		public static GraphMask operator & (GraphMask lhs, GraphMask rhs) {
			return new GraphMask(lhs.value & rhs.value);
		}

		/// <summary>Combines two masks to form the union of them</summary>
		public static GraphMask operator | (GraphMask lhs, GraphMask rhs) {
			return new GraphMask(lhs.value | rhs.value);
		}

		/// <summary>Inverts the mask</summary>
		public static GraphMask operator ~ (GraphMask lhs) {
			return new GraphMask(~lhs.value);
		}

		/// <summary>True if this mask contains the graph with the given graph index</summary>
		public bool Contains (int graphIndex) {
			return ((value >> graphIndex) & 1) != 0;
		}

		/// <summary>A bitmask containing the given graph</summary>
		public static GraphMask FromGraph (NavGraph graph) {
			return 1 << (int)graph.graphIndex;
		}

		public override string ToString () {
			return value.ToString();
		}

		/// <summary>
		/// A bitmask containing the first graph with the given name.
		/// <code>
		/// GraphMask mask1 = GraphMask.FromGraphName("My Grid Graph");
		/// GraphMask mask2 = GraphMask.FromGraphName("My Other Grid Graph");
		///
		/// NNConstraint nn = NNConstraint.Default;
		///
		/// nn.graphMask = mask1 | mask2;
		///
		/// // Find the node closest to somePoint which is either in 'My Grid Graph' OR in 'My Other Grid Graph'
		/// var info = AstarPath.active.GetNearest(somePoint, nn);
		/// </code>
		/// </summary>
		public static GraphMask FromGraphName (string graphName) {
			var graph = AstarData.active.data.FindGraph(g => g.name == graphName);

			if (graph == null) throw new System.ArgumentException("Could not find any graph with the name '" + graphName + "'");
			return FromGraph(graph);
		}
	}

	#region Delegates

	/* Delegate with on Path object as parameter.
	 * This is used for callbacks when a path has finished calculation.\n
	 * Example function:
	 * \snippet MiscSnippets.cs OnPathDelegate
	 */
	public delegate void OnPathDelegate (Path p);

	public delegate void OnGraphDelegate (NavGraph graph);

	public delegate void OnScanDelegate (AstarPath script);

	/// <summary>Deprecated:</summary>
	public delegate void OnScanStatus (Progress progress);

	#endregion

	#region Enums

	public enum GraphUpdateThreading {
		/// <summary>
		/// Call UpdateArea in the unity thread.
		/// This is the default value.
		/// Not compatible with SeparateThread.
		/// </summary>
		UnityThread = 0,
		/// <summary>Call UpdateArea in a separate thread. Not compatible with UnityThread.</summary>
		SeparateThread = 1 << 0,
		/// <summary>Calls UpdateAreaInit in the Unity thread before everything else</summary>
		UnityInit = 1 << 1,
		/// <summary>
		/// Calls UpdateAreaPost in the Unity thread after everything else.
		/// This is used together with SeparateThread to apply the result of the multithreaded
		/// calculations to the graph without modifying it at the same time as some other script
		/// might be using it (e.g calling GetNearest).
		/// </summary>
		UnityPost = 1 << 2,
		/// <summary>Combination of SeparateThread and UnityInit</summary>
		SeparateAndUnityInit = SeparateThread | UnityInit
	}

	/// <summary>How path results are logged by the system</summary>
	public enum PathLog {
		/// <summary>Does not log anything. This is recommended for release since logging path results has a performance overhead.</summary>
		None,
		/// <summary>Logs basic info about the paths</summary>
		Normal,
		/// <summary>Includes additional info</summary>
		Heavy,
		/// <summary>Same as heavy, but displays the info in-game using GUI</summary>
		InGame,
		/// <summary>Same as normal, but logs only paths which returned an error</summary>
		OnlyErrors
	}

	/// <summary>
	/// How to estimate the cost of moving to the destination during pathfinding.
	///
	/// The heuristic is the estimated cost from the current node to the target.
	/// The different heuristics have roughly the same performance except not using any heuristic at all (<see cref="None)"/>
	/// which is usually significantly slower.
	///
	/// In the image below you can see a comparison of the different heuristic options for an 8-connected grid and
	/// for a 4-connected grid.
	/// Note that all paths within the green area will all have the same length. The only difference between the heuristics
	/// is which of those paths of the same length that will be chosen.
	/// Note that while the Diagonal Manhattan and Manhattan options seem to behave very differently on an 8-connected grid
	/// they only do it in this case because of very small rounding errors. Usually they behave almost identically on 8-connected grids.
	///
	/// [Open online documentation to see images]
	///
	/// Generally for a 4-connected grid graph the Manhattan option should be used as it is the true distance on a 4-connected grid.
	/// For an 8-connected grid graph the Diagonal Manhattan option is the mathematically most correct option, however the Euclidean option
	/// is often preferred, especially if you are simplifying the path afterwards using modifiers.
	///
	/// For any graph that is not grid based the Euclidean option is the best one to use.
	///
	/// See: <a href="https://en.wikipedia.org/wiki/A*_search_algorithm">Wikipedia: A* search_algorithm</a>
	/// </summary>
	public enum Heuristic {
		/// <summary>Manhattan distance. See: https://en.wikipedia.org/wiki/Taxicab_geometry</summary>
		Manhattan,
		/// <summary>
		/// Manhattan distance, but allowing diagonal movement as well.
		/// Note: This option is currently hard coded for the XZ plane. It will be equivalent to Manhattan distance if you try to use it in the XY plane (i.e for a 2D game).
		/// </summary>
		DiagonalManhattan,
		/// <summary>Ordinary distance. See: https://en.wikipedia.org/wiki/Euclidean_distance</summary>
		Euclidean,
		/// <summary>
		/// Use no heuristic at all.
		/// This reduces the pathfinding algorithm to Dijkstra's algorithm.
		/// This is usually significantly slower compared to using a heuristic, which is why the A* algorithm is usually preferred over Dijkstra's algorithm.
		/// You may have to use this if you have a very non-standard graph. For example a world with a <a href="https://en.wikipedia.org/wiki/Wraparound_(video_games)">wraparound playfield</a> (think Civilization or Asteroids) and you have custom links
		/// with a zero cost from one end of the map to the other end. Usually the A* algorithm wouldn't find the wraparound links because it wouldn't think to look in that direction.
		/// See: https://en.wikipedia.org/wiki/Dijkstra%27s_algorithm
		/// </summary>
		None
	}

	/// <summary>How to visualize the graphs in the editor</summary>
	public enum GraphDebugMode {
		/// <summary>Draw the graphs with a single solid color</summary>
		SolidColor,
		/// <summary>
		/// Use the G score of the last calculated paths to color the graph.
		/// The G score is the cost from the start node to the given node.
		/// See: https://en.wikipedia.org/wiki/A*_search_algorithm
		/// </summary>
		G,
		/// <summary>
		/// Use the H score (heuristic) of the last calculated paths to color the graph.
		/// The H score is the estimated cost from the current node to the target.
		/// See: https://en.wikipedia.org/wiki/A*_search_algorithm
		/// </summary>
		H,
		/// <summary>
		/// Use the F score of the last calculated paths to color the graph.
		/// The F score is the G score + the H score, or in other words the estimated cost total cost of the path.
		/// See: https://en.wikipedia.org/wiki/A*_search_algorithm
		/// </summary>
		F,
		/// <summary>
		/// Use the penalty of each node to color the graph.
		/// This does not show penalties added by tags.
		/// See: graph-updates (view in online documentation for working links)
		/// See: <see cref="Pathfinding.GraphNode.Penalty"/>
		/// </summary>
		Penalty,
		/// <summary>
		/// Visualize the connected components of the graph.
		/// A node with a given color can reach any other node with the same color.
		///
		/// See: <see cref="Pathfinding.HierarchicalGraph"/>
		/// See: https://en.wikipedia.org/wiki/Connected_component_(graph_theory)
		/// </summary>
		Areas,
		/// <summary>
		/// Use the tag of each node to color the graph.
		/// See: tags (view in online documentation for working links)
		/// See: <see cref="Pathfinding.GraphNode.Tag"/>
		/// </summary>
		Tags,
		/// <summary>
		/// Visualize the hierarchical graph structure of the graph.
		/// This is mostly for internal use.
		/// See: <see cref="Pathfinding.HierarchicalGraph"/>
		/// </summary>
		HierarchicalNode,
	}

	/// <summary>Number of threads to use</summary>
	public enum ThreadCount {
		AutomaticLowLoad = -1,
		AutomaticHighLoad = -2,
		None = 0,
		One = 1,
		Two,
		Three,
		Four,
		Five,
		Six,
		Seven,
		Eight
	}

	/// <summary>Internal state of a path in the pipeline</summary>
	public enum PathState {
		Created = 0,
		PathQueue = 1,
		Processing = 2,
		ReturnQueue = 3,
		Returned = 4
	}

	/// <summary>State of a path request</summary>
	public enum PathCompleteState {
		/// <summary>
		/// The path has not been calculated yet.
		/// See: <see cref="Pathfinding.Path.IsDone()"/>
		/// </summary>
		NotCalculated = 0,
		/// <summary>
		/// The path calculation is done, but it failed.
		/// See: <see cref="Pathfinding.Path.error"/>
		/// </summary>
		Error = 1,
		/// <summary>The path has been successfully calculated</summary>
		Complete = 2,
		/// <summary>
		/// The path has been calculated, but only a partial path could be found.
		/// See: <see cref="Pathfinding.ABPath.calculatePartial"/>
		/// </summary>
		Partial = 3,
	}

	/// <summary>What to do when the character is close to the destination</summary>
	public enum CloseToDestinationMode {
		/// <summary>The character will stop as quickly as possible when within endReachedDistance (field that exist on most movement scripts) units from the destination</summary>
		Stop,
		/// <summary>The character will continue to the exact position of the destination</summary>
		ContinueToExactDestination,
	}

	/// <summary>Indicates the side of a line that a point lies on</summary>
	public enum Side : byte {
		/// <summary>The point lies exactly on the line</summary>
		Colinear = 0,
		/// <summary>The point lies on the left side of the line</summary>
		Left = 1,
		/// <summary>The point lies on the right side of the line</summary>
		Right = 2
	}

	public enum InspectorGridHexagonNodeSize {
		/// <summary>Value is the distance between two opposing sides in the hexagon</summary>
		Width,
		/// <summary>Value is the distance between two opposing vertices in the hexagon</summary>
		Diameter,
		/// <summary>Value is the raw node size of the grid</summary>
		NodeSize
	}

	public enum InspectorGridMode {
		Grid,
		IsometricGrid,
		Hexagonal,
		Advanced
	}

	/// <summary>
	/// Determines which direction the agent moves in.
	/// For 3D games you most likely want the ZAxisIsForward option as that is the convention for 3D games.
	/// For 2D games you most likely want the YAxisIsForward option as that is the convention for 2D games.
	/// </summary>
	public enum OrientationMode {
		ZAxisForward,
		YAxisForward,
	}

	#endregion
}

namespace Pathfinding.Util {
	/// <summary>Prevents code stripping. See: https://docs.unity3d.com/Manual/ManagedCodeStripping.html</summary>
	public class PreserveAttribute : System.Attribute {
	}
}
