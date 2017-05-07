#define ASTAR_MORE_AREAS // Increases the number of areas to 65535 but reduces the maximum number of graphs to 4. Disabling gives a max number of areas of 1023 and 32 graphs.
//#define ASTAR_NO_PENALTY // Enabling this disables the use of penalties. Reduces memory usage.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using Pathfinding.Nodes;
using Pathfinding.Serialization;

namespace Pathfinding.Nodes {
	//class A{}
}

namespace Pathfinding {
	
	public delegate void GraphNodeDelegate (GraphNode node);
	public delegate bool GraphNodeDelegateCancelable (GraphNode node);
	
	[System.Obsolete("This class has been replaced with GraphNode, it may be removed in future versions",true)]
	public class Node {
	}
	
	public abstract class GraphNode {
		
		private int nodeIndex;
		protected uint flags;
		
		/** Penlty cost for walking on this node. This can be used to make it harder/slower to walk over certain areas. */
		private uint penalty;
		
		// Some fallback properties
		
		[System.Obsolete ("This attribute is deprecated. Please use .position (not a capital P)")]
		public Int3 Position { get { return position; } }
		
		[System.Obsolete ("This attribute is deprecated. Please use .Walkable (with a capital W)")]
		public bool walkable { get { return Walkable; } set { Walkable = value; } }
		
		[System.Obsolete ("This attribute is deprecated. Please use .Tag (with a capital T)")]
		public uint tags { get { return Tag; } set { Tag = value; } }
		
		[System.Obsolete ("This attribute is deprecated. Please use .GraphIndex (with a capital G)")]
		public uint graphIndex { get { return GraphIndex; } set { GraphIndex = value; } }
		
		// End of fallback
		
		/** Constructor for a graph node. */
		public GraphNode (AstarPath astar) {
			//this.nodeIndex = NextNodeIndex++;
			if (astar != null) {
				this.nodeIndex = astar.GetNewNodeIndex();
				astar.InitializeNode (this);
			} else {
				throw new System.Exception ("No active AstarPath object to bind to");
			}
		}
		
		/** Destroys the node.
		 * Cleans up any temporary pathfinding data used for this node.
		 * The graph is responsible for calling this method on nodes when they are destroyed, including when the whole graph is destoyed.
		 * Otherwise memory leaks might present themselves.
		 * 
		 * Once called, subsequent calls to this method will not do anything.
		 * 
		 * \note Assumes the current active AstarPath instance is the same one that created this node.
		 */
		public void Destroy () {
			//Already destroyed
			if (nodeIndex == -1) return;
			
			ClearConnections(true);
			
			if (AstarPath.active != null) {
				AstarPath.active.DestroyNode(this);
			}
			nodeIndex = -1;
			//System.Console.WriteLine ("~");
		}
		
		public bool Destroyed {
			get {
				return nodeIndex == -1;
			}
		}
		
		//~GraphNode () {
			//Debug.Log ("Destroyed GraphNode " + nodeIndex);
		//}
		
		public int NodeIndex { get {return nodeIndex;}}
		
		public Int3 position;
		
#region Constants
		/** Position of the walkable bit. \see Walkable */
		const int FlagsWalkableOffset = 0;
		/** Mask of the walkable bit. \see Walkable */
		const uint FlagsWalkableMask = 1 << FlagsWalkableOffset;
		
		/** Start of region bits. \see Area */
		const int FlagsAreaOffset = 1;
		/** Mask of region bits. \see Area */
		const uint FlagsAreaMask = (1024-1) << FlagsAreaOffset;
		
		/** Start of graph index bits. \see GraphIndex */
		const int FlagsGraphOffset = 11;
		/** Mask of graph index bits. \see GraphIndex */
		const uint FlagsGraphMask = (32-1) << FlagsGraphOffset;
		public const uint MaxRegionCount = FlagsAreaMask >> FlagsAreaOffset;
		/** Max number of graphs */
		public const uint MaxGraphCount = FlagsGraphMask >> FlagsGraphOffset;
		
		/** Start of tag bits. \see Tag */
		const int FlagsTagOffset = 19;
		/** Mask of tag bits. \see Tag */
		const uint FlagsTagMask = (32-1) << FlagsTagOffset;
		
#endregion
		
		
#region Properties
		
		/** Holds various bitpacked variables.
		 */
		public uint Flags {
			get {
				return flags;
			}
			set {
				flags = value;
			}
		}
		
		/** Penalty cost for walking on this node. This can be used to make it harder/slower to walk over certain areas. */
		public uint Penalty {
			get {
				return penalty;
			}
			set {
				if (value > 0xFFFFF)
					Debug.LogWarning ("Very high penalty applied. Are you sure negative values haven't underflowed?\n" +
						"Penalty values this high could with long paths cause overflows and in some cases infinity loops because of that.\n" +
						"Penalty value applied: "+value);
				penalty = value;
			}
		}
		
		/** True if the node is traversable */
		public bool Walkable {
			get {
				return (flags & FlagsWalkableMask) != 0;
			}
			set {
				flags = flags & ~FlagsWalkableMask | (value ? 1U : 0U) << FlagsWalkableOffset;
			}
		}
		
		public uint Area {
			get {
				return (flags & FlagsAreaMask) >> FlagsAreaOffset;
			}
			set {
				//Awesome! No parentheses
				flags = flags & ~FlagsAreaMask | value << FlagsAreaOffset;
			}
		}
		
		public uint GraphIndex {
			get {
				return (flags & FlagsGraphMask) >> FlagsGraphOffset;
			}
			set {
				flags = flags & ~FlagsGraphMask | value << FlagsGraphOffset;
			}
		}
		
		public uint Tag {
			get {
				return (flags & FlagsTagMask) >> FlagsTagOffset;
			}
			set {
				flags = flags & ~FlagsTagMask | value << FlagsTagOffset;
			}
		}
		
#endregion
		
		public void UpdateG (Path path, PathNode pathNode) {
			pathNode.G = pathNode.parent.G + pathNode.cost + path.GetTraversalCost(this);
		}
		
		public virtual void UpdateRecursiveG (Path path, PathNode pathNode, PathHandler handler) {
			//Simple but slow default implementation
			UpdateG (path,pathNode);
			
			handler.PushNode (pathNode);
			
			GetConnections (delegate (GraphNode other) {
				PathNode otherPN = handler.GetPathNode (other);
				if (otherPN.parent == pathNode && otherPN.pathID == handler.PathID) other.UpdateRecursiveG (path, otherPN,handler);
			});
		}
		
		public virtual void FloodFill (Stack<GraphNode> stack, uint region) {
			//Simple but slow default implementation
			
			GetConnections (delegate (GraphNode other) {
				if (other.Area != region) {
					other.Area = region;
					stack.Push (other);
				}
			});
		}
		
		/** Calls the delegate with all connections from this node */
		public abstract void GetConnections (GraphNodeDelegate del);

		public abstract void AddConnection (GraphNode node, uint cost);
		public abstract void RemoveConnection (GraphNode node);
		
		/** Remove all connections from this node.
		  * \param alsoReverse if true, neighbours will be requested to remove connections to this node.
		  */
		public abstract void ClearConnections (bool alsoReverse);
		
		/** Checks if this node has a connection to the specified node */
		public virtual bool ContainsConnection (GraphNode node) {
			//Simple but slow default implementation
			bool contains = false;
			GetConnections (delegate (GraphNode n) {
				if (n == node) contains = true;
			});
			return contains;
		}
		
		/** Recalculates all connection costs from this node.
		 * Depending on the node type, this may or may not be supported.
		 * Nothing will be done if the operation is not supported
		 * \todo Use interface?
		 */
		public virtual void RecalculateConnectionCosts () {
		}
		
		/** Add a portal from this node to the specified node.
		 * This function should add a portal to the left and right lists which is connecting the two nodes (\a this and \a other).
		 * 
		 * \param other The node which is on the other side of the portal (strictly speaking it does not actually have to be on the other side of the portal though).
		 * \param left List of portal points on the left side of the funnel
		 * \param right List of portal points on the right side of the funnel
		 * \param backwards If this is true, the call was made on a node with the \a other node as the node before this one in the path.
		 * In this case you may choose to do nothing since a similar call will be made to the \a other node with this node referenced as \a other (but then with backwards = true).
		 * You do not have to care about switching the left and right lists, that is done for you already.
		 * 
		 * \returns True if the call was deemed successful. False if some unknown case was encountered and no portal could be added.
		 * If both calls to node1.GetPortal (node2,...) and node2.GetPortal (node1,...) return false, the funnel modifier will fall back to adding to the path
		 * the positions of the node.
		 * 
		 * The default implementation simply returns false.
		 * 
		 * This function may add more than one portal if necessary.
		 * 
		 * \see http://digestingduck.blogspot.se/2010/03/simple-stupid-funnel-algorithm.html
		 */
		public virtual bool GetPortal (GraphNode other, List<Vector3> left, List<Vector3> right, bool backwards) {
			return false;
		}
		
		/** Open the node */
		public abstract void Open (Path path, PathNode pathNode, PathHandler handler);
		
		
		
		public virtual void SerializeNode (GraphSerializationContext ctx) {
			//Write basic node data.
			ctx.writer.Write (Penalty);
			ctx.writer.Write (Flags);
		}
		
		public virtual void DeserializeNode (GraphSerializationContext ctx) {
			Penalty = ctx.reader.ReadUInt32();
			Flags = ctx.reader.ReadUInt32();
		}
		
		/** Used to serialize references to other nodes e.g connections.
		 * Use the GraphSerializationContext.GetNodeIdentifier and
		 * GraphSerializationContext.GetNodeFromIdentifier methods
		 * for serialization and deserialization respectively.
		 * 
		 * Nodes must override this method and serialize their connections.
		 * Graph generators do not need to call this method, it will be called automatically on all
		 * nodes at the correct time by the serializer.
		 */
		public virtual void SerializeReferences (GraphSerializationContext ctx) {
		}
		
		/** Used to deserialize references to other nodes e.g connections.
		 * Use the GraphSerializationContext.GetNodeIdentifier and
		 * GraphSerializationContext.GetNodeFromIdentifier methods
		 * for serialization and deserialization respectively.
		 * 
		 * Nodes must override this method and serialize their connections.
		 * Graph generators do not need to call this method, it will be called automatically on all
		 * nodes at the correct time by the serializer.
		 */
		public virtual void DeserializeReferences (GraphSerializationContext ctx) {
		}
	}
	
	public abstract class MeshNode : GraphNode {
		
		public MeshNode (AstarPath astar) : base (astar) {
		}
		
		public GraphNode[] connections;
		public uint[] connectionCosts;
		
		public abstract Int3 GetVertex (int i);
		public abstract int GetVertexCount ();
		public abstract Vector3 ClosestPointOnNode (Vector3 p);
		public abstract Vector3 ClosestPointOnNodeXZ (Vector3 p);
		
		public override void ClearConnections (bool alsoReverse) {
			if (alsoReverse && connections != null) {
				for (int i=0;i<connections.Length;i++) {
					connections[i].RemoveConnection (this);
				}
			}
			
			connections = null;
			connectionCosts = null;
		}
		
		public override void GetConnections (GraphNodeDelegate del) {
			if (connections == null) return;
			for (int i=0;i<connections.Length;i++) del (connections[i]);
		}
		
		public override void FloodFill (Stack<GraphNode> stack, uint region) {
			//Faster, more specialized implementation to override the slow default implementation
			if (connections == null) return;
			
			for (int i=0;i<connections.Length;i++) {
				GraphNode other = connections[i];
				if (other.Area != region) {
					other.Area = region;
					stack.Push (other);
				}
			}
		}
		
		public override bool ContainsConnection (GraphNode node) {
			for (int i=0;i<connections.Length;i++) if (connections[i] == node) return true;
			return false;
		}
		
		public override void UpdateRecursiveG (Path path, PathNode pathNode, PathHandler handler)
		{
			UpdateG (path,pathNode);
			
			handler.PushNode (pathNode);
			
			for (int i=0;i<connections.Length;i++) {
				GraphNode other = connections[i];
				PathNode otherPN = handler.GetPathNode (other);
				if (otherPN.parent == pathNode && otherPN.pathID == handler.PathID) {
					other.UpdateRecursiveG (path, otherPN,handler);
				}
			}
		}
		
		/** Add a connection from this node to the specified node.
		 * If the connection already exists, the cost will simply be updated and
		 * no extra connection added.
		 * 
		 * \note Only adds a one-way connection. Consider calling the same function on the other node
		 * to get a two-way connection.
		 */
		public override void AddConnection (GraphNode node, uint cost) {
			
			if (connections != null) { 
				for (int i=0;i<connections.Length;i++) {
					if (connections[i] == node) {
						connectionCosts[i] = cost;
						return;
					}
				}
			}
			
			int connLength = connections != null ? connections.Length : 0;
			
			GraphNode[] newconns = new GraphNode[connLength+1];
			uint[] newconncosts = new uint[connLength+1];
			for (int i=0;i<connLength;i++) {
				newconns[i] = connections[i];
				newconncosts[i] = connectionCosts[i];
			}
			
			newconns[connLength] = node;
			newconncosts[connLength] = cost;
			
			connections = newconns;
			connectionCosts = newconncosts;
		}
		
		/** Removes any connection from this node to the specified node.
		 * If no such connection exists, nothing will be done.
		 * 
		 * \note This only removes the connection from this node to the other node.
		 * You may want to call the same function on the other node to remove its eventual connection
		 * to this node.
		 */
		public override void RemoveConnection (GraphNode node) {
			
			if (connections == null) return;
			
			for (int i=0;i<connections.Length;i++) {
				if (connections[i] == node) {
					
					int connLength = connections.Length;
			
					GraphNode[] newconns = new GraphNode[connLength-1];
					uint[] newconncosts = new uint[connLength-1];
					for (int j=0;j<i;j++) {
						newconns[j] = connections[j];
						newconncosts[j] = connectionCosts[j];
					}
					for (int j=i+1;j<connLength;j++) {
						newconns[j-1] = connections[j];
						newconncosts[j-1] = connectionCosts[j];
					}
					
					connections = newconns;
					connectionCosts = newconncosts;
					return;
				}
			}
		}
		
		/** Checks if \a p is inside the node
		 * 
		 * The default implementation uses XZ space and is in large part got from the website linked below
		 * \author http://unifycommunity.com/wiki/index.php?title=PolyContainsPoint (Eric5h5)
		 */
		public virtual bool ContainsPoint (Int3 p) {
			bool inside = false;
			
			int count = GetVertexCount();
			for (int i = 0, j=count-1; i < count; j = i++) { 
			  if ( ((GetVertex(i).z <= p.z && p.z < GetVertex(j).z) || (GetVertex(j).z <= p.z && p.z < GetVertex(i).z)) && 
			     (p.x < (GetVertex(j).x - GetVertex(i).x) * (p.z - GetVertex(i).z) / (GetVertex(j).z - GetVertex(i).z) + GetVertex(i).x)) 
			     inside = !inside;
			}
			return inside; 
		}
		
		public override void SerializeReferences (GraphSerializationContext ctx)
		{
			if (connections == null) {
				ctx.writer.Write(-1);
			} else {
				ctx.writer.Write (connections.Length);
				for (int i=0;i<connections.Length;i++) {
					ctx.writer.Write (ctx.GetNodeIdentifier (connections[i]));
					ctx.writer.Write (connectionCosts[i]);
				}
			}
		}
		
		public override void DeserializeReferences (GraphSerializationContext ctx)
		{
			int count = ctx.reader.ReadInt32();
			if (count == -1) {
				connections = null;
				connectionCosts = null;
			} else {
				connections = new GraphNode[count];
				connectionCosts = new uint[count];
				
				for (int i=0;i<count;i++) {
					connections[i] = ctx.GetNodeFromIdentifier (ctx.reader.ReadInt32());
					connectionCosts[i] = ctx.reader.ReadUInt32();
				}
			}
		}
	}

}