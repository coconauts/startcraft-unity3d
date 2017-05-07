using Pathfinding;
using System.Collections.Generic;

namespace Pathfinding
{
	/** Stores temporary node data for a single pathfinding request.
	 * Every node has one PathNode per thread used.
	 * It stores e.g G score, H score and other temporary variables needed
	 * for path calculation, but which are not part of the graph structure.
	 * 
	 * \see Pathfinding.PathHandler
	 */
	public class PathNode {
		/** Reference to the actual graph node */
		public GraphNode node;
		
		/** Parent node in the search tree */
		public PathNode parent;
		
		/** The path request (in this thread, if multithreading is used) which last used this node */
		public ushort pathID;
		
		/** Bitpacked variable which stores several fields */
		private uint flags;
		
		/** Cost uses the first 28 bits */
		private const uint CostMask = (1U << 28) - 1U;
		
		/** Flag 1 is at bit 28 */
		private const int Flag1Offset = 28;
		private const uint Flag1Mask = (uint)(1 << Flag1Offset);
		
		/** Flag 2 is at bit 29 */
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
		
		/** Use as temporary flag during pathfinding.
		 * Pathfinders (only) can use this during pathfinding to mark
		 * nodes. When done, this flag should be reverted to its default state (false) to
		 * avoid messing up other pathfinding requests.
		 */
		public bool flag1 {
			get {
				return (flags & Flag1Mask) != 0;
			}
			set {
				flags = (flags & ~Flag1Mask) | (value ? Flag1Mask : 0U);
			}
		}
		
		/** Use as temporary flag during pathfinding.
		 * Pathfinders (only) can use this during pathfinding to mark
		 * nodes. When done, this flag should be reverted to its default state (false) to
		 * avoid messing up other pathfinding requests.
		 */
		public bool flag2 {
			get {
				return (flags & Flag2Mask) != 0;
			}
			set {
				flags = (flags & ~Flag2Mask) | (value ? Flag2Mask : 0U);
			}
		}
		
		/** Backing field for the G score */
		private uint g;
		
		/** Backing field for the H score */
		private uint h;
		
		/** G score, cost to get to this node */
		public uint G {get { return g;} set{g = value;}}
		
		/** H score, estimated cost to get to to the target */
		public uint H {get { return h;} set{h = value;}}
		
		/** F score. H score + G score */
		public uint F {get { return g+h;}}
	}
	
	/** Handles thread specific path data.
	 */
	public class PathHandler {
		
		/** Current PathID.
		  * \see #PathID
		  */
		private ushort pathID;
		
		/** Binary heap to keep nodes on the "Open list" */
		private BinaryHeapM heap = new BinaryHeapM(128);
		
		/** ID for the path currently being calculated or last path that was calculated */
		public ushort PathID {get { return pathID; }}
		
		/** Push a node to the heap */
		public void PushNode (PathNode node) {
			heap.Add (node);
		}
		
		/** Pop the node with the lowest F score off the heap */
		public PathNode PopNode () {
			return heap.Remove ();
		}
		
		/** Get the internal heap.
		 * \note Most things can be accomplished with the methods on this class instead.
		 */
		public BinaryHeapM GetHeap () {
			return heap;
		}
		
		/** Rebuild the heap to account for changed node values.
		 * Some path types change the target for the H score in the middle of the path calculation,
		 * that requires rebuilding the heap to keep it correctly sorted
		 */
		public void RebuildHeap () {
			heap.Rebuild ();
		}
		
		/** True if the heap is empty */
		public bool HeapEmpty () {
			return heap.numberOfItems <= 0;
		}
		
		/** Log2 size of buckets.
		 * So 10 yields a real bucket size of 1024.
		 * Be careful with large values.
		 */
		const int BucketSizeLog2 = 10;
		
		/** Real bucket size */
		const int BucketSize = 1 << BucketSizeLog2;
		const int BucketIndexMask = (1 << BucketSizeLog2)-1;
		
		/** Array of buckets containing PathNodes */
		public PathNode[][] nodes = new PathNode[0][];
		private bool[] bucketNew = new bool[0];
		private bool[] bucketCreated = new bool[0];
		
		private Stack<PathNode[]> bucketCache = new Stack<PathNode[]> ();
		
		private int filledBuckets = 0;
		
		/** StringBuilder that paths can use to build debug strings.
		 * Better to use a single StringBuilder instead of each path creating its own
		 */
		public readonly System.Text.StringBuilder DebugStringBuilder = new System.Text.StringBuilder();
		
		public PathHandler () {
		}
		
		public void InitializeForPath (Path p) {
			pathID = p.pathID;
			heap.Clear ();
		}
		
		/** Internal method to clean up node data */
		public void DestroyNode (GraphNode node) {
			PathNode pn = GetPathNode (node);
			
			//Clean up reference to help GC
			pn.node = null;
			pn.parent = null;
		}
		
		/** Internal method to initialize node data */
		public void InitializeNode (GraphNode node) {
			
			//Get the index of the node
			int ind = node.NodeIndex;

			int bucketNumber = ind >> BucketSizeLog2;
			int bucketIndex = ind & BucketIndexMask;
			
			if (bucketNumber >= nodes.Length) {
				//At least increase the size to:
				//Current size * 1.5
				//Current size + 2 or
				//bucketNumber
				
				PathNode[][] newNodes = new PathNode[System.Math.Max (System.Math.Max (nodes.Length*3 / 2,bucketNumber+1), nodes.Length+2)][];
				for (int i=0;i<nodes.Length;i++) newNodes[i] = nodes[i];
				//Debug.Log ("Resizing Bucket List from " + nodes.Length + " to " + newNodes.Length + " (bucketNumber="+bucketNumber+")");
				
				bool[] newBucketNew = new bool[newNodes.Length];
				for (int i=0;i<nodes.Length;i++) newBucketNew[i] = bucketNew[i];
				
				
				bool[] newBucketCreated = new bool[newNodes.Length];
				for (int i=0;i<nodes.Length;i++) newBucketCreated[i] = bucketCreated[i];
				
				nodes = newNodes;
				bucketNew = newBucketNew;
				bucketCreated = newBucketCreated;
			}
			
			if (nodes[bucketNumber] == null) {
				PathNode[] ns;
				
				if (bucketCache.Count > 0) {
					ns = bucketCache.Pop();
				} else {
					ns = new PathNode[BucketSize];
					for (int i=0;i<BucketSize;i++) ns[i] = new PathNode ();
				}
				nodes[bucketNumber] = ns;
				
				if (!bucketCreated[bucketNumber]) {
					bucketNew[bucketNumber] = true;
					bucketCreated[bucketNumber] = true;
				}
				filledBuckets++;
			}
			
			PathNode pn = nodes[bucketNumber][bucketIndex];
			pn.node = node;
		}
			
		public PathNode GetPathNode (GraphNode node) {
			
			
			//Get the index of the node
			int ind = node.NodeIndex;
			
			return nodes[ind >> BucketSizeLog2][ind & BucketIndexMask];	
		}
		
		/** Set all node's pathIDs to 0.
		 * \see Pathfinding.PathNode.pathID
		 */
		public void ClearPathIDs () {
			
			for (int i=0;i<nodes.Length;i++) {
				PathNode[] ns = nodes[i];
				if (nodes[i] != null) for (int j=0;j<BucketSize;j++) ns[j].pathID = 0;
			}
		}
	}
}

