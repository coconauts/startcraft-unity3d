using UnityEngine;
using Pathfinding;
using Pathfinding.Serialization;

namespace Pathfinding
{
	public class PointNode : GraphNode {
		
		public GraphNode[] connections;
		public uint[] connectionCosts;
		
		/** Used for internal linked list structure.
		 * \warning Do not modify
		 */
		public PointNode next;

		//public override Int3 Position {get { return position; } }
		
		public void SetPosition (Int3 value) {
			position = value;
		}
		
		public PointNode (AstarPath astar) : base (astar) {
		}
		
		public override void GetConnections (GraphNodeDelegate del) {
			if (connections == null) return;
			for (int i=0;i<connections.Length;i++) del (connections[i]);
		}
		
		public override void ClearConnections (bool alsoReverse) {
			if (alsoReverse && connections != null) {
				for (int i=0;i<connections.Length;i++) {
					connections[i].RemoveConnection (this);
				}
			}
			
			connections = null;
			connectionCosts = null;
		}
		
		public override void UpdateRecursiveG (Path path, PathNode pathNode, PathHandler handler) {
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
		
		public override bool ContainsConnection (GraphNode node) {
			for (int i=0;i<connections.Length;i++) if (connections[i] == node) return true;
			return false;
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
		
		public override void Open (Path path, PathNode pathNode, PathHandler handler) {
			
			if (connections == null) return;
			
			for (int i=0;i<connections.Length;i++) {
				GraphNode other = connections[i];
				
				if (path.CanTraverse (other)) {
					
					PathNode pathOther = handler.GetPathNode (other);
					
					if (pathOther.pathID != handler.PathID) {
						
						pathOther.parent = pathNode;
						pathOther.pathID = handler.PathID;
						
						pathOther.cost = connectionCosts[i];
						
						pathOther.H = path.CalculateHScore (other);
						other.UpdateG (path, pathOther);
						
						handler.PushNode (pathOther);
					} else {
						//If not we can test if the path from this node to the other one is a better one then the one already used
						uint tmpCost = connectionCosts[i];
						
						if (pathNode.G + tmpCost + path.GetTraversalCost(other) < pathOther.G) {
							
							pathOther.cost = tmpCost;
							pathOther.parent = pathNode;
							
							other.UpdateRecursiveG (path, pathOther,handler);
							
							//handler.PushNode (pathOther);
						}
						else if (pathOther.G+tmpCost+path.GetTraversalCost(this) < pathNode.G && other.ContainsConnection (this)) {
							//Or if the path from the other node to this one is better
							
							pathNode.parent = pathOther;
							pathNode.cost = tmpCost;
							
							UpdateRecursiveG (path, pathNode,handler);
							
							//handler.PushNode (pathNode);
						}
					}
				}
			}
		}
		
		public override void SerializeNode (GraphSerializationContext ctx)
		{
			base.SerializeNode (ctx);
			ctx.writer.Write (position.x);
			ctx.writer.Write (position.y);
			ctx.writer.Write (position.z);
		}
		
		public override void DeserializeNode (GraphSerializationContext ctx)
		{
			base.DeserializeNode (ctx);
			position = new Int3 (ctx.reader.ReadInt32(), ctx.reader.ReadInt32(), ctx.reader.ReadInt32());
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

