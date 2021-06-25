#define DECREASE_KEY
using System.Collections.Generic;

namespace Pathfinding {
	/// <summary>
	/// Stores temporary node data for a single pathfinding request.
	/// Every node has one PathNode per thread used.
	/// It stores e.g G score, H score and other temporary variables needed
	/// for path calculation, but which are not part of the graph structure.
	///
	/// See: Pathfinding.PathHandler
	/// See: https://en.wikipedia.org/wiki/A*_search_algorithm
	/// </summary>
	public class PathNode {
		/// <summary>Reference to the actual graph node</summary>
		public GraphNode node;

		/// <summary>Parent node in the search tree</summary>
		public PathNode parent;

		/// <summary>The path request (in this thread, if multithreading is used) which last used this node</summary>
		public ushort pathID;

#if DECREASE_KEY
		/// <summary>
		/// Index of the node in the binary heap.
		/// The open list in the A* algorithm is backed by a binary heap.
		/// To support fast 'decrease key' operations, the index of the node
		/// is saved here.
		/// </summary>
		public ushort heapIndex = BinaryHeap.NotInHeap;
#endif

		/// <summary>Bitpacked variable which stores several fields</summary>
		private uint flags;

		/// <summary>Cost uses the first 28 bits</summary>
		private const uint CostMask = (1U << 28) - 1U;

		/// <summary>Flag 1 is at bit 28</summary>
		private const int Flag1Offset = 28;
		private const uint Flag1Mask = (uint)(1 << Flag1Offset);

		/// <summary>Flag 2 is at bit 29</summary>
		private const int Flag2Offset = 29;
		private const uint Flag2Mask = (uint)(1 << Flag2Offset);

		public uint cost {
			get {
				return flags & CostMask;
			}
			set {
				flags = (flags & ~CostMask) | value;
			}
		}

		/// <summary>
		/// Use as temporary flag during pathfinding.
		/// Pathfinders (only) can use this during pathfinding to mark
		/// nodes. When done, this flag should be reverted to its default state (false) to
		/// avoid messing up other pathfinding requests.
		/// </summary>
		public bool flag1 {
			get {
				return (flags & Flag1Mask) != 0;
			}
			set {
				flags = (flags & ~Flag1Mask) | (value ? Flag1Mask : 0U);
			}
		}

		/// <summary>
		/// Use as temporary flag during pathfinding.
		/// Pathfinders (only) can use this during pathfinding to mark
		/// nodes. When done, this flag should be reverted to its default state (false) to
		/// avoid messing up other pathfinding requests.
		/// </summary>
		public bool flag2 {
			get {
				return (flags & Flag2Mask) != 0;
			}
			set {
				flags = (flags & ~Flag2Mask) | (value ? Flag2Mask : 0U);
			}
		}

		/// <summary>Backing field for the G score</summary>
		private uint g;

		/// <summary>Backing field for the H score</summary>
		private uint h;

		/// <summary>G score, cost to get to this node</summary>
		public uint G { get { return g; } set { g = value; } }

		/// <summary>H score, estimated cost to get to to the target</summary>
		public uint H { get { return h; } set { h = value; } }

		/// <summary>F score. H score + G score</summary>
		public uint F { get { return g+h; } }

		public void UpdateG (Path path) {
#if ASTAR_NO_TRAVERSAL_COST
			g = parent.g + cost;
#else
			g = parent.g + cost + path.GetTraversalCost(node);
#endif
		}
	}

	/// <summary>Handles thread specific path data.</summary>
	public class PathHandler {
		/// <summary>
		/// Current PathID.
		/// See: <see cref="PathID"/>
		/// </summary>
		private ushort pathID;

		public readonly int threadID;
		public readonly int totalThreadCount;

		/// <summary>
		/// Binary heap to keep track of nodes on the "Open list".
		/// See: https://en.wikipedia.org/wiki/A*_search_algorithm
		/// </summary>
		public readonly BinaryHeap heap = new BinaryHeap(128);

		/// <summary>ID for the path currently being calculated or last path that was calculated</summary>
		public ushort PathID { get { return pathID; } }

		/// <summary>Array of all PathNodes</summary>
		public PathNode[] nodes = new PathNode[0];

		/// <summary>
		/// StringBuilder that paths can use to build debug strings.
		/// Better for performance and memory usage to use a single StringBuilder instead of each path creating its own
		/// </summary>
		public readonly System.Text.StringBuilder DebugStringBuilder = new System.Text.StringBuilder();

		public PathHandler (int threadID, int totalThreadCount) {
			this.threadID = threadID;
			this.totalThreadCount = totalThreadCount;
		}

		public void InitializeForPath (Path p) {
			pathID = p.pathID;
			heap.Clear();
		}

		/// <summary>Internal method to clean up node data</summary>
		public void DestroyNode (GraphNode node) {
			PathNode pn = GetPathNode(node);

			// Clean up references to help the GC
			pn.node = null;
			pn.parent = null;
			// This is not required for pathfinding, but not clearing it may confuse gizmo drawing for a fraction of a second.
			// Especially when 'Show Search Tree' is enabled
			pn.pathID = 0;
			pn.G = 0;
			pn.H = 0;
		}

		/// <summary>Internal method to initialize node data</summary>
		public void InitializeNode (GraphNode node) {
			//Get the index of the node
			int ind = node.NodeIndex;

			if (ind >= nodes.Length) {
				// Grow by a factor of 2
				PathNode[] newNodes = new PathNode[System.Math.Max(128, nodes.Length*2)];
				nodes.CopyTo(newNodes, 0);
				// Initialize all PathNode instances at once
				// It is important that we do this here and don't for example leave the entries as NULL and initialize
				// them lazily. By allocating them all at once we are much more likely to allocate the PathNodes close
				// to each other in memory (most systems use some kind of bumb-allocator) and this improves cache locality
				// and reduces false sharing (which would happen if we allocated PathNodes for the different threads close
				// to each other). This has been profiled to give around a 4% difference in overall pathfinding performance.
				for (int i = nodes.Length; i < newNodes.Length; i++) newNodes[i] = new PathNode();
				nodes = newNodes;
			}

			nodes[ind].node = node;
		}

		public PathNode GetPathNode (int nodeIndex) {
			return nodes[nodeIndex];
		}

		/// <summary>
		/// Returns the PathNode corresponding to the specified node.
		/// The PathNode is specific to this PathHandler since multiple PathHandlers
		/// are used at the same time if multithreading is enabled.
		/// </summary>
		public PathNode GetPathNode (GraphNode node) {
			return nodes[node.NodeIndex];
		}

		/// <summary>
		/// Set all nodes' pathIDs to 0.
		/// See: Pathfinding.PathNode.pathID
		/// </summary>
		public void ClearPathIDs () {
			for (int i = 0; i < nodes.Length; i++) {
				if (nodes[i] != null) nodes[i].pathID = 0;
			}
		}
	}
}
