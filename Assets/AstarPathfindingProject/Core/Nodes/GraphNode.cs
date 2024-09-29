using UnityEngine;
using System.Collections.Generic;
using Pathfinding.Serialization;

namespace Pathfinding {
	using Pathfinding.Util;

	/// <summary>Represents a connection to another node</summary>
	public struct Connection {
		/// <summary>Node which this connection goes to</summary>
		public GraphNode node;

		/// <summary>
		/// Cost of moving along this connection.
		/// A cost of 1000 corresponds approximately to the cost of moving one world unit.
		/// </summary>
		public uint cost;

		/// <summary>
		/// Side of the node shape which this connection uses.
		/// Used for mesh nodes.
		/// A value of 0 corresponds to using the side for vertex 0 and vertex 1 on the node. 1 corresponds to vertex 1 and 2, etc.
		/// A negative value means that this connection does not use any side at all (this is mostly used for off-mesh links).
		///
		/// Note: Due to alignment, the <see cref="node"/> and <see cref="cost"/> fields use 12 bytes which will be padded
		/// to 16 bytes when used in an array even if this field would be removed.
		/// So this field does not contribute to increased memory usage.
		///
		/// See: TriangleMeshNode
		/// See: TriangleMeshNode.AddConnection
		/// </summary>
		public byte shapeEdge;

		public Connection (GraphNode node, uint cost, byte shapeEdge = 0xFF) {
			this.node = node;
			this.cost = cost;
			this.shapeEdge = shapeEdge;
		}

		public override int GetHashCode () {
			return node.GetHashCode() ^ (int)cost;
		}

		public override bool Equals (object obj) {
			if (obj == null) return false;
			var conn = (Connection)obj;
			return conn.node == node && conn.cost == cost && conn.shapeEdge == shapeEdge;
		}
	}

	/// <summary>Base class for all nodes</summary>
	public abstract class GraphNode {
		/// <summary>Internal unique index. Also stores some bitpacked values such as <see cref="TemporaryFlag1"/> and <see cref="TemporaryFlag2"/>.</summary>
		private int nodeIndex;

		/// <summary>
		/// Bitpacked field holding several pieces of data.
		/// See: Walkable
		/// See: Area
		/// See: GraphIndex
		/// See: Tag
		/// </summary>
		protected uint flags;

#if !ASTAR_NO_PENALTY
		/// <summary>
		/// Penalty cost for walking on this node.
		/// This can be used to make it harder/slower to walk over certain nodes.
		///
		/// A penalty of 1000 (Int3.Precision) corresponds to the cost of walking one world unit.
		///
		/// See: graph-updates (view in online documentation for working links)
		/// </summary>
		private uint penalty;
#endif

		/// <summary>
		/// Graph which this node belongs to.
		///
		/// If you know the node belongs to a particular graph type, you can cast it to that type:
		/// <code>
		/// GraphNode node = ...;
		/// GridGraph graph = node.Graph as GridGraph;
		/// </code>
		///
		/// Will return null if the node has been destroyed.
		/// </summary>
		public NavGraph Graph {
			get {
				return Destroyed ? null : AstarData.GetGraph(this);
			}
		}

		/// <summary>Constructor for a graph node.</summary>
		protected GraphNode (AstarPath astar) {
			if (!System.Object.ReferenceEquals(astar, null)) {
				this.nodeIndex = astar.GetNewNodeIndex();
				astar.InitializeNode(this);
			} else {
				throw new System.Exception("No active AstarPath object to bind to");
			}
		}

		/// <summary>
		/// Destroys the node.
		/// Cleans up any temporary pathfinding data used for this node.
		/// The graph is responsible for calling this method on nodes when they are destroyed, including when the whole graph is destoyed.
		/// Otherwise memory leaks might present themselves.
		///
		/// Once called the <see cref="Destroyed"/> property will return true and subsequent calls to this method will not do anything.
		///
		/// Note: Assumes the current active AstarPath instance is the same one that created this node.
		///
		/// Warning: Should only be called by graph classes on their own nodes
		/// </summary>
		public void Destroy () {
			if (Destroyed) return;

			ClearConnections(true);

			if (AstarPath.active != null) {
				AstarPath.active.DestroyNode(this);
			}
			NodeIndex = DestroyedNodeIndex;
		}

		public bool Destroyed {
			get {
				return NodeIndex == DestroyedNodeIndex;
			}
		}

		// If anyone creates more than about 200 million nodes then things will not go so well, however at that point one will certainly have more pressing problems, such as having run out of RAM
		const int NodeIndexMask = 0xFFFFFFF;
		const int DestroyedNodeIndex = NodeIndexMask - 1;
		const int TemporaryFlag1Mask = 0x10000000;
		const int TemporaryFlag2Mask = 0x20000000;

		/// <summary>
		/// Internal unique index.
		/// Every node will get a unique index.
		/// This index is not necessarily correlated with e.g the position of the node in the graph.
		/// </summary>
		public int NodeIndex { get { return nodeIndex & NodeIndexMask; } private set { nodeIndex = (nodeIndex & ~NodeIndexMask) | value; } }

		/// <summary>
		/// Temporary flag for internal purposes.
		/// May only be used in the Unity thread. Must be reset to false after every use.
		/// </summary>
		internal bool TemporaryFlag1 { get { return (nodeIndex & TemporaryFlag1Mask) != 0; } set { nodeIndex = (nodeIndex & ~TemporaryFlag1Mask) | (value ? TemporaryFlag1Mask : 0); } }

		/// <summary>
		/// Temporary flag for internal purposes.
		/// May only be used in the Unity thread. Must be reset to false after every use.
		/// </summary>
		internal bool TemporaryFlag2 { get { return (nodeIndex & TemporaryFlag2Mask) != 0; } set { nodeIndex = (nodeIndex & ~TemporaryFlag2Mask) | (value ? TemporaryFlag2Mask : 0); } }

		/// <summary>
		/// Position of the node in world space.
		/// Note: The position is stored as an Int3, not a Vector3.
		/// You can convert an Int3 to a Vector3 using an explicit conversion.
		/// <code> var v3 = (Vector3)node.position; </code>
		/// </summary>
		public Int3 position;

		#region Constants
		/// <summary>Position of the walkable bit. See: <see cref="Walkable"/></summary>
		const int FlagsWalkableOffset = 0;
		/// <summary>Mask of the walkable bit. See: <see cref="Walkable"/></summary>
		const uint FlagsWalkableMask = 1 << FlagsWalkableOffset;

		/// <summary>Start of hierarchical node index bits. See: <see cref="HierarchicalNodeIndex"/></summary>
		const int FlagsHierarchicalIndexOffset = 1;
		/// <summary>Mask of hierarchical node index bits. See: <see cref="HierarchicalNodeIndex"/></summary>
		const uint HierarchicalIndexMask = (131072-1) << FlagsHierarchicalIndexOffset;

		/// <summary>Start of <see cref="IsHierarchicalNodeDirty"/> bits. See: <see cref="IsHierarchicalNodeDirty"/></summary>
		const int HierarchicalDirtyOffset = 18;

		/// <summary>Mask of the <see cref="IsHierarchicalNodeDirty"/> bit. See: <see cref="IsHierarchicalNodeDirty"/></summary>
		const uint HierarchicalDirtyMask = 1 << HierarchicalDirtyOffset;

		/// <summary>Start of graph index bits. See: <see cref="GraphIndex"/></summary>
		const int FlagsGraphOffset = 24;
		/// <summary>Mask of graph index bits. See: <see cref="GraphIndex"/></summary>
		const uint FlagsGraphMask = (256u-1) << FlagsGraphOffset;

		public const uint MaxHierarchicalNodeIndex = HierarchicalIndexMask >> FlagsHierarchicalIndexOffset;

		/// <summary>Max number of graphs-1</summary>
		public const uint MaxGraphIndex = FlagsGraphMask >> FlagsGraphOffset;

		/// <summary>Start of tag bits. See: <see cref="Tag"/></summary>
		const int FlagsTagOffset = 19;
		/// <summary>Mask of tag bits. See: <see cref="Tag"/></summary>
		const uint FlagsTagMask = (32-1) << FlagsTagOffset;

		#endregion

		#region Properties

		/// <summary>
		/// Holds various bitpacked variables.
		///
		/// Bit 0: <see cref="Walkable"/>
		/// Bits 1 through 17: <see cref="HierarchicalNodeIndex"/>
		/// Bit 18: <see cref="IsHierarchicalNodeDirty"/>
		/// Bits 19 through 23: <see cref="Tag"/>
		/// Bits 24 through 31: <see cref="GraphIndex"/>
		///
		/// Warning: You should pretty much never modify this property directly. Use the other properties instead.
		/// </summary>
		public uint Flags {
			get {
				return flags;
			}
			set {
				flags = value;
			}
		}

		/// <summary>
		/// Penalty cost for walking on this node.
		/// This can be used to make it harder/slower to walk over certain nodes.
		/// A cost of 1000 (<see cref="Pathfinding.Int3.Precision"/>) corresponds to the cost of moving 1 world unit.
		///
		/// See: graph-updates (view in online documentation for working links)
		/// </summary>
		public uint Penalty {
#if !ASTAR_NO_PENALTY
			get {
				return penalty;
			}
			set {
				if (value > 0xFFFFFF)
					Debug.LogWarning("Very high penalty applied. Are you sure negative values haven't underflowed?\n" +
						"Penalty values this high could with long paths cause overflows and in some cases infinity loops because of that.\n" +
						"Penalty value applied: "+value);
				penalty = value;
			}
#else
			get { return 0U; }
			set {}
#endif
		}

		/// <summary>
		/// True if the node is traversable.
		///
		/// See: graph-updates (view in online documentation for working links)
		/// </summary>
		public bool Walkable {
			get {
				return (flags & FlagsWalkableMask) != 0;
			}
			set {
				flags = flags & ~FlagsWalkableMask | (value ? 1U : 0U) << FlagsWalkableOffset;
				AstarPath.active.hierarchicalGraph.AddDirtyNode(this);
			}
		}

		/// <summary>
		/// Hierarchical Node that contains this node.
		/// The graph is divided into clusters of small hierarchical nodes in which there is a path from every node to every other node.
		/// This structure is used to speed up connected component calculations which is used to quickly determine if a node is reachable from another node.
		///
		/// See: <see cref="Pathfinding.HierarchicalGraph"/>
		///
		/// Warning: This is an internal property and you should most likely not change it.
		/// </summary>
		internal int HierarchicalNodeIndex {
			get {
				return (int)((flags & HierarchicalIndexMask) >> FlagsHierarchicalIndexOffset);
			}
			set {
				flags = (flags & ~HierarchicalIndexMask) | (uint)(value << FlagsHierarchicalIndexOffset);
			}
		}

		/// <summary>Some internal bookkeeping</summary>
		internal bool IsHierarchicalNodeDirty {
			get {
				return (flags & HierarchicalDirtyMask) != 0;
			}
			set {
				flags = flags & ~HierarchicalDirtyMask | (value ? 1U : 0U) << HierarchicalDirtyOffset;
			}
		}

		/// <summary>
		/// Connected component that contains the node.
		/// This is visualized in the scene view as differently colored nodes (if the graph coloring mode is set to 'Areas').
		/// Each area represents a set of nodes such that there is no valid path between nodes of different colors.
		///
		/// See: https://en.wikipedia.org/wiki/Connected_component_(graph_theory)
		/// See: <see cref="Pathfinding.HierarchicalGraph"/>
		/// </summary>
		public uint Area {
			get {
				return AstarPath.active.hierarchicalGraph.GetConnectedComponent(HierarchicalNodeIndex);
			}
		}

		/// <summary>
		/// Graph which contains this node.
		/// See: <see cref="Pathfinding.AstarData.graphs"/>
		/// See: <see cref="Graph"/>
		/// </summary>
		public uint GraphIndex {
			get {
				return (flags & FlagsGraphMask) >> FlagsGraphOffset;
			}
			set {
				flags = flags & ~FlagsGraphMask | value << FlagsGraphOffset;
			}
		}

		/// <summary>
		/// Node tag.
		/// See: tags (view in online documentation for working links)
		/// See: graph-updates (view in online documentation for working links)
		/// </summary>
		public uint Tag {
			get {
				return (flags & FlagsTagMask) >> FlagsTagOffset;
			}
			set {
				flags = flags & ~FlagsTagMask | ((value << FlagsTagOffset) & FlagsTagMask);
			}
		}

		#endregion

		/// <summary>
		/// Inform the system that the node's connectivity has changed.
		/// This is used for recalculating the connected components of the graph.
		///
		/// See: <see cref="Pathfinding.HierarchicalGraph"/>
		///
		/// You must call this method if you change the connectivity or walkability of the node without going through the high level methods
		/// such as the <see cref="Walkable"/> property or the <see cref="AddConnection"/> method. For example if your manually change the <see cref="Pathfinding.MeshNode.connections"/> array you need to call this method.
		/// </summary>
		public void SetConnectivityDirty () {
			AstarPath.active.hierarchicalGraph.AddDirtyNode(this);
		}

		/// <summary>
		/// Recalculates a node's connection costs.
		/// Deprecated: This method is deprecated because it never did anything, you can safely remove any calls to this method.
		/// </summary>
		[System.Obsolete("This method is deprecated because it never did anything, you can safely remove any calls to this method")]
		public void RecalculateConnectionCosts () {
		}

		public virtual void UpdateRecursiveG (Path path, PathNode pathNode, PathHandler handler) {
			//Simple but slow default implementation
			pathNode.UpdateG(path);

			handler.heap.Add(pathNode);

			GetConnections((GraphNode other) => {
				PathNode otherPN = handler.GetPathNode(other);
				if (otherPN.parent == pathNode && otherPN.pathID == handler.PathID) other.UpdateRecursiveG(path, otherPN, handler);
			});
		}

		/// <summary>
		/// Calls the delegate with all connections from this node.
		/// <code>
		/// node.GetConnections(connectedTo => {
		///     Debug.DrawLine((Vector3)node.position, (Vector3)connectedTo.position, Color.red);
		/// });
		/// </code>
		///
		/// You can add all connected nodes to a list like this
		/// <code>
		/// var connections = new List<GraphNode>();
		/// node.GetConnections(connections.Add);
		/// </code>
		/// </summary>
		public abstract void GetConnections (System.Action<GraphNode> action);

		/// <summary>
		/// Add a connection from this node to the specified node.
		/// If the connection already exists, the cost will simply be updated and
		/// no extra connection added.
		///
		/// Note: Only adds a one-way connection. Consider calling the same function on the other node
		/// to get a two-way connection.
		///
		/// <code>
		/// AstarPath.active.AddWorkItem(new AstarWorkItem(ctx => {
		///     // Connect two nodes
		///     var node1 = AstarPath.active.GetNearest(transform.position, NNConstraint.None).node;
		///     var node2 = AstarPath.active.GetNearest(transform.position + Vector3.right, NNConstraint.None).node;
		///     var cost = (uint)(node2.position - node1.position).costMagnitude;
		///     node1.AddConnection(node2, cost);
		///     node2.AddConnection(node1, cost);
		///
		///     node1.ContainsConnection(node2); // True
		///
		///     node1.RemoveConnection(node2);
		///     node2.RemoveConnection(node1);
		/// }));
		/// </code>
		/// </summary>
		public abstract void AddConnection (GraphNode node, uint cost);

		/// <summary>
		/// Removes any connection from this node to the specified node.
		/// If no such connection exists, nothing will be done.
		///
		/// Note: This only removes the connection from this node to the other node.
		/// You may want to call the same function on the other node to remove its possible connection
		/// to this node.
		///
		/// <code>
		/// AstarPath.active.AddWorkItem(new AstarWorkItem(ctx => {
		///     // Connect two nodes
		///     var node1 = AstarPath.active.GetNearest(transform.position, NNConstraint.None).node;
		///     var node2 = AstarPath.active.GetNearest(transform.position + Vector3.right, NNConstraint.None).node;
		///     var cost = (uint)(node2.position - node1.position).costMagnitude;
		///     node1.AddConnection(node2, cost);
		///     node2.AddConnection(node1, cost);
		///
		///     node1.ContainsConnection(node2); // True
		///
		///     node1.RemoveConnection(node2);
		///     node2.RemoveConnection(node1);
		/// }));
		/// </code>
		/// </summary>
		public abstract void RemoveConnection (GraphNode node);

		/// <summary>Remove all connections from this node.</summary>
		/// <param name="alsoReverse">if true, neighbours will be requested to remove connections to this node.</param>
		public abstract void ClearConnections (bool alsoReverse);

		/// <summary>
		/// Checks if this node has a connection to the specified node.
		///
		/// <code>
		/// AstarPath.active.AddWorkItem(new AstarWorkItem(ctx => {
		///     // Connect two nodes
		///     var node1 = AstarPath.active.GetNearest(transform.position, NNConstraint.None).node;
		///     var node2 = AstarPath.active.GetNearest(transform.position + Vector3.right, NNConstraint.None).node;
		///     var cost = (uint)(node2.position - node1.position).costMagnitude;
		///     node1.AddConnection(node2, cost);
		///     node2.AddConnection(node1, cost);
		///
		///     node1.ContainsConnection(node2); // True
		///
		///     node1.RemoveConnection(node2);
		///     node2.RemoveConnection(node1);
		/// }));
		/// </code>
		/// </summary>
		public virtual bool ContainsConnection (GraphNode node) {
			// Simple but slow default implementation
			bool contains = false;

			GetConnections(neighbour => {
				contains |= neighbour == node;
			});
			return contains;
		}

		/// <summary>
		/// Add a portal from this node to the specified node.
		/// This function should add a portal to the left and right lists which is connecting the two nodes (this and other).
		///
		/// Returns: True if the call was deemed successful. False if some unknown case was encountered and no portal could be added.
		/// If both calls to node1.GetPortal (node2,...) and node2.GetPortal (node1,...) return false, the funnel modifier will fall back to adding to the path
		/// the positions of the node.
		///
		/// The default implementation simply returns false.
		///
		/// This function may add more than one portal if necessary.
		///
		/// See: http://digestingduck.blogspot.se/2010/03/simple-stupid-funnel-algorithm.html
		/// </summary>
		/// <param name="other">The node which is on the other side of the portal (strictly speaking it does not actually have to be on the other side of the portal though).</param>
		/// <param name="left">List of portal points on the left side of the funnel</param>
		/// <param name="right">List of portal points on the right side of the funnel</param>
		/// <param name="backwards">If this is true, the call was made on a node with the other node as the node before this one in the path.
		/// In this case you may choose to do nothing since a similar call will be made to the other node with this node referenced as other (but then with backwards = true).
		/// You do not have to care about switching the left and right lists, that is done for you already.</param>
		public virtual bool GetPortal (GraphNode other, List<Vector3> left, List<Vector3> right, bool backwards) {
			return false;
		}

		/// <summary>
		/// Open the node.
		/// Used internally for the A* algorithm.
		/// </summary>
		public abstract void Open (Path path, PathNode pathNode, PathHandler handler);

		/// <summary>The surface area of the node in square world units</summary>
		public virtual float SurfaceArea () {
			return 0;
		}

		/// <summary>
		/// A random point on the surface of the node.
		/// For point nodes and other nodes which do not have a surface, this will always return the position of the node.
		/// </summary>
		public virtual Vector3 RandomPointOnSurface () {
			return (Vector3)position;
		}

		/// <summary>
		/// Hash code used for checking if the gizmos need to be updated.
		/// Will change when the gizmos for the node might change.
		/// </summary>
		public virtual int GetGizmoHashCode () {
			// Some hashing, the constants are just some arbitrary prime numbers. #flags contains the info for #Tag and #Walkable
			return position.GetHashCode() ^ (19 * (int)Penalty) ^ (41 * (int)(flags & ~(HierarchicalIndexMask | HierarchicalDirtyMask)));
		}

		/// <summary>Serialized the node data to a byte array</summary>
		public virtual void SerializeNode (GraphSerializationContext ctx) {
			//Write basic node data.
			ctx.writer.Write(Penalty);
			// Save all flags except the hierarchical node index and the dirty bit
			ctx.writer.Write(Flags & ~(HierarchicalIndexMask | HierarchicalDirtyMask));
		}

		/// <summary>Deserializes the node data from a byte array</summary>
		public virtual void DeserializeNode (GraphSerializationContext ctx) {
			Penalty = ctx.reader.ReadUInt32();
			// Load all flags except the hierarchical node index and the dirty bit (they aren't saved in newer versions and older data should just be cleared)
			// Note that the dirty bit needs to be preserved here because it may already be set (due to the node being created)
			Flags = (ctx.reader.ReadUInt32() & ~(HierarchicalIndexMask | HierarchicalDirtyMask)) | (Flags & (HierarchicalIndexMask | HierarchicalDirtyMask));

			// Set the correct graph index (which might have changed, e.g if loading additively)
			GraphIndex = ctx.graphIndex;
		}

		/// <summary>
		/// Used to serialize references to other nodes e.g connections.
		/// Use the GraphSerializationContext.GetNodeIdentifier and
		/// GraphSerializationContext.GetNodeFromIdentifier methods
		/// for serialization and deserialization respectively.
		///
		/// Nodes must override this method and serialize their connections.
		/// Graph generators do not need to call this method, it will be called automatically on all
		/// nodes at the correct time by the serializer.
		/// </summary>
		public virtual void SerializeReferences (GraphSerializationContext ctx) {
		}

		/// <summary>
		/// Used to deserialize references to other nodes e.g connections.
		/// Use the GraphSerializationContext.GetNodeIdentifier and
		/// GraphSerializationContext.GetNodeFromIdentifier methods
		/// for serialization and deserialization respectively.
		///
		/// Nodes must override this method and serialize their connections.
		/// Graph generators do not need to call this method, it will be called automatically on all
		/// nodes at the correct time by the serializer.
		/// </summary>
		public virtual void DeserializeReferences (GraphSerializationContext ctx) {
		}
	}

	public abstract class MeshNode : GraphNode {
		protected MeshNode (AstarPath astar) : base(astar) {
		}

		/// <summary>
		/// All connections from this node.
		/// See: <see cref="AddConnection"/>
		/// See: <see cref="RemoveConnection"/>
		///
		/// Note: If you modify this array or the contents of it you must call <see cref="SetConnectivityDirty"/>.
		/// </summary>
		public Connection[] connections;

		/// <summary>Get a vertex of this node.</summary>
		/// <param name="i">vertex index. Must be between 0 and #GetVertexCount (exclusive).</param>
		public abstract Int3 GetVertex (int i);

		/// <summary>
		/// Number of corner vertices that this node has.
		/// For example for a triangle node this will return 3.
		/// </summary>
		public abstract int GetVertexCount ();

		/// <summary>Closest point on the surface of this node to the point p</summary>
		public abstract Vector3 ClosestPointOnNode (Vector3 p);

		/// <summary>
		/// Closest point on the surface of this node when seen from above.
		/// This is usually very similar to <see cref="ClosestPointOnNode"/> but when the node is in a slope this can be significantly different.
		/// [Open online documentation to see images]
		/// When the blue point in the above image is used as an argument this method call will return the green point while the <see cref="ClosestPointOnNode"/> method will return the red point.
		/// </summary>
		public abstract Vector3 ClosestPointOnNodeXZ (Vector3 p);

		public override void ClearConnections (bool alsoReverse) {
			// Remove all connections to this node from our neighbours
			if (alsoReverse && connections != null) {
				for (int i = 0; i < connections.Length; i++) {
					// Null check done here because NavmeshTile.Destroy
					// requires it for some optimizations it does
					// Normally connection elements are never null
					if (connections[i].node != null) {
						connections[i].node.RemoveConnection(this);
					}
				}
			}

			ArrayPool<Connection>.Release (ref connections, true);
			AstarPath.active.hierarchicalGraph.AddDirtyNode(this);
		}

		public override void GetConnections (System.Action<GraphNode> action) {
			if (connections == null) return;
			for (int i = 0; i < connections.Length; i++) action(connections[i].node);
		}

		public override bool ContainsConnection (GraphNode node) {
			for (int i = 0; i < connections.Length; i++) if (connections[i].node == node) return true;
			return false;
		}

		public override void UpdateRecursiveG (Path path, PathNode pathNode, PathHandler handler) {
			pathNode.UpdateG(path);

			handler.heap.Add(pathNode);

			for (int i = 0; i < connections.Length; i++) {
				GraphNode other = connections[i].node;
				PathNode otherPN = handler.GetPathNode(other);
				if (otherPN.parent == pathNode && otherPN.pathID == handler.PathID) {
					other.UpdateRecursiveG(path, otherPN, handler);
				}
			}
		}

		/// <summary>
		/// Add a connection from this node to the specified node.
		///
		/// If the connection already exists, the cost will simply be updated and
		/// no extra connection added.
		///
		/// Note: Only adds a one-way connection. Consider calling the same function on the other node
		/// to get a two-way connection.
		/// </summary>
		/// <param name="node">Node to add a connection to</param>
		/// <param name="cost">Cost of traversing the connection. A cost of 1000 corresponds approximately to the cost of moving 1 world unit.</param>
		public override void AddConnection (GraphNode node, uint cost) {
			AddConnection(node, cost, -1);
		}

		/// <summary>
		/// Add a connection from this node to the specified node.
		/// See: Pathfinding.Connection.edge
		///
		/// If the connection already exists, the cost will simply be updated and
		/// no extra connection added.
		///
		/// Note: Only adds a one-way connection. Consider calling the same function on the other node
		/// to get a two-way connection.
		/// </summary>
		/// <param name="node">Node to add a connection to</param>
		/// <param name="cost">Cost of traversing the connection. A cost of 1000 corresponds approximately to the cost of moving 1 world unit.</param>
		/// <param name="shapeEdge">Which edge on the shape of this node to use or -1 if no edge is used.</param>
		public void AddConnection (GraphNode node, uint cost, int shapeEdge) {
			if (node == null) throw new System.ArgumentNullException();

			// Check if we already have a connection to the node
			if (connections != null) {
				for (int i = 0; i < connections.Length; i++) {
					if (connections[i].node == node) {
						// Just update the cost for the existing connection
						connections[i].cost = cost;
						// Update edge only if it was a definite edge, otherwise reuse the existing one
						// This makes it possible to use the AddConnection(node,cost) overload to only update the cost
						// without changing the edge which is required for backwards compatibility.
						connections[i].shapeEdge = shapeEdge >= 0 ? (byte)shapeEdge : connections[i].shapeEdge;
						return;
					}
				}
			}

			// Create new arrays which include the new connection
			int connLength = connections != null ? connections.Length : 0;

			var newconns = ArrayPool<Connection>.ClaimWithExactLength (connLength+1);
			for (int i = 0; i < connLength; i++) {
				newconns[i] = connections[i];
			}

			newconns[connLength] = new Connection(node, cost, (byte)shapeEdge);

			if (connections != null) {
				ArrayPool<Connection>.Release (ref connections, true);
			}

			connections = newconns;
			AstarPath.active.hierarchicalGraph.AddDirtyNode(this);
		}

		/// <summary>
		/// Removes any connection from this node to the specified node.
		/// If no such connection exists, nothing will be done.
		///
		/// Note: This only removes the connection from this node to the other node.
		/// You may want to call the same function on the other node to remove its eventual connection
		/// to this node.
		/// </summary>
		public override void RemoveConnection (GraphNode node) {
			if (connections == null) return;

			// Iterate through all connections and check if there are any to the node
			for (int i = 0; i < connections.Length; i++) {
				if (connections[i].node == node) {
					// Create new arrays which have the specified node removed
					int connLength = connections.Length;

					var newconns = ArrayPool<Connection>.ClaimWithExactLength (connLength-1);
					for (int j = 0; j < i; j++) {
						newconns[j] = connections[j];
					}
					for (int j = i+1; j < connLength; j++) {
						newconns[j-1] = connections[j];
					}

					if (connections != null) {
						ArrayPool<Connection>.Release (ref connections, true);
					}

					connections = newconns;
					AstarPath.active.hierarchicalGraph.AddDirtyNode(this);
					return;
				}
			}
		}

		/// <summary>Checks if point is inside the node when seen from above</summary>
		public virtual bool ContainsPoint (Int3 point) {
			return ContainsPoint((Vector3)point);
		}

		/// <summary>
		/// Checks if point is inside the node when seen from above.
		///
		/// Note that <see cref="ContainsPointInGraphSpace"/> is faster than this method as it avoids
		/// some coordinate transformations. If you are repeatedly calling this method
		/// on many different nodes but with the same point then you should consider
		/// transforming the point first and then calling ContainsPointInGraphSpace.
		/// <code>
		/// Int3 p = (Int3)graph.transform.InverseTransform(point);
		///
		/// node.ContainsPointInGraphSpace(p);
		/// </code>
		/// </summary>
		public abstract bool ContainsPoint (Vector3 point);

		/// <summary>
		/// Checks if point is inside the node in graph space.
		///
		/// In graph space the up direction is always the Y axis so in principle
		/// we project the triangle down on the XZ plane and check if the point is inside the 2D triangle there.
		/// </summary>
		public abstract bool ContainsPointInGraphSpace (Int3 point);

		public override int GetGizmoHashCode () {
			var hash = base.GetGizmoHashCode();

			if (connections != null) {
				for (int i = 0; i < connections.Length; i++) {
					hash ^= 17 * connections[i].GetHashCode();
				}
			}
			return hash;
		}

		public override void SerializeReferences (GraphSerializationContext ctx) {
			if (connections == null) {
				ctx.writer.Write(-1);
			} else {
				ctx.writer.Write(connections.Length);
				for (int i = 0; i < connections.Length; i++) {
					ctx.SerializeNodeReference(connections[i].node);
					ctx.writer.Write(connections[i].cost);
					ctx.writer.Write(connections[i].shapeEdge);
				}
			}
		}

		public override void DeserializeReferences (GraphSerializationContext ctx) {
			int count = ctx.reader.ReadInt32();

			if (count == -1) {
				connections = null;
			} else {
				connections = ArrayPool<Connection>.ClaimWithExactLength (count);

				for (int i = 0; i < count; i++) {
					connections[i] = new Connection(
						ctx.DeserializeNodeReference(),
						ctx.reader.ReadUInt32(),
						ctx.meta.version < AstarSerializer.V4_1_0 ? (byte)0xFF : ctx.reader.ReadByte()
						);
				}
			}
		}
	}
}
