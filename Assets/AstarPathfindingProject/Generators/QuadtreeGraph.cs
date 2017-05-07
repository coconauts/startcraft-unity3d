//@AstarDev
using UnityEngine;
using System.Collections;
using Pathfinding;
using Pathfinding.Nodes;
using System.Collections.Generic;
using Pathfinding.Serialization;

namespace Pathfinding {
	public class QuadtreeGraph : NavGraph {
		
		public int editorWidthLog2 = 6;
		public int editorHeightLog2 = 6;
		
		public int Width { get; protected set;}
		public int Height { get; protected set;}
		
		public LayerMask layerMask = -1;
		public float nodeSize = 1;
		public int minDepth = 3;
		
		QuadtreeNodeHolder root;
		public Vector3 center;
		
		private BitArray map;
		
		public override void GetNodes (GraphNodeDelegateCancelable del) {
			if (root == null) return;
			root.GetNodes (del);
		}
		
		public bool CheckCollision (int x, int y) {
			Vector3 pos = LocalToWorldPosition (x,y,1);
			bool walkable = !Physics.CheckSphere (pos, nodeSize*1.4142f, layerMask);
			return walkable;
		}
		
		public int CheckNode (int xs, int ys, int width) {
			Debug.Log ("Checking Node " + xs + " " + ys + " width: " + width);
			bool val = map[xs + ys*Width];
			for (int x = xs;x < xs+width; x++) {
				for (int y = ys; y < ys+width; y++) {
					//Values differ, node should be split
					if (map[x + y*Width] != val) return -1;
				}
			}
			return val ? 1 : 0;
		}
		
		public override void ScanInternal (OnScanStatus statusCallback)
		{
			Width = 1 << editorWidthLog2;
			Height = 1 << editorHeightLog2;
			/** \todo Check if can clear */
			map = new BitArray(Width*Height);
			
			for (int x=0;x<Width;x++) {
				for (int y=0;y<Height;y++) {
					map.Set(x+y*Width, CheckCollision (x,y));
				}
			}
			
			QuadtreeNodeHolder h = new QuadtreeNodeHolder();
			
			CreateNodeRec (h, 0, 0,0);
			root = h;
			
			RecalculateConnectionsRec (root, 0,0,0);
		}
		
		public void RecalculateConnectionsRec (QuadtreeNodeHolder holder, int depth, int x, int y) {
			if (holder.node != null) {
				RecalculateConnections (holder, depth, x,y);
			} else {
				int width = 1 << (System.Math.Min (editorHeightLog2,editorWidthLog2)-depth);
				
				RecalculateConnectionsRec (holder.c0,depth+1,x          , y          );
				RecalculateConnectionsRec (holder.c1,depth+1,x + width/2, y          );
				RecalculateConnectionsRec (holder.c2,depth+1,x + width/2, y + width/2);
				RecalculateConnectionsRec (holder.c3,depth+1,x          , y + width/2);
			}
		}
		
		public Vector3 LocalToWorldPosition (int x, int y, int width) {
			/** \bug Change to XY plane, matrix should handle rotation */
			return new Vector3((x+width*0.5f)*nodeSize, 0, (y+width*0.5f)*nodeSize);
		}
		
		public void CreateNodeRec (QuadtreeNodeHolder holder, int depth, int x, int y) {
			
			int width = 1 << (System.Math.Min (editorHeightLog2,editorWidthLog2)-depth);
			int walkable;
			if (depth < minDepth) {
				walkable = -1;
			} else {
				walkable = CheckNode (x,y, width);
			}
			
			if (walkable == 1 || walkable == 0 || width == 1) {
				QuadtreeNode node = new QuadtreeNode(active);
				node.SetPosition ((Int3)LocalToWorldPosition(x,y,width));
				node.Walkable = walkable == 1;
				holder.node = node;
				
				
			} else { //walkable = -1 //Undefined
				holder.c0 = new QuadtreeNodeHolder ();
				holder.c1 = new QuadtreeNodeHolder ();
				holder.c2 = new QuadtreeNodeHolder ();
				holder.c3 = new QuadtreeNodeHolder ();
				
				CreateNodeRec (holder.c0,depth+1,x          , y          );
				CreateNodeRec (holder.c1,depth+1,x + width/2, y          );
				CreateNodeRec (holder.c2,depth+1,x + width/2, y + width/2);
				CreateNodeRec (holder.c3,depth+1,x          , y + width/2);
			}
		}
		
		public void RecalculateConnections (QuadtreeNodeHolder holder, int depth, int x, int y) {
			
			if (root == null) throw new System.InvalidOperationException ("Graph contains no nodes");
				
			if (holder.node == null) throw new System.ArgumentException ("No leaf node specified. Holder has no node.");
			
			int width = 1 << (System.Math.Min (editorHeightLog2,editorWidthLog2)-depth);
			
			List<QuadtreeNode> ls = new List<QuadtreeNode>();
			AddNeighboursRec (ls, root, 0,0,0, new IntRect (x,y,x+width,y+width).Expand(0), holder.node);
			holder.node.connections = ls.ToArray();
			holder.node.connectionCosts = new uint[ls.Count];
			
			for (int i=0;i<ls.Count;i++) {
				uint d = (uint)(ls[i].position - holder.node.position).costMagnitude;
				holder.node.connectionCosts[i] = d;
			}
		}
		
		public void AddNeighboursRec (List<QuadtreeNode> arr, QuadtreeNodeHolder holder, int depth, int x, int y, IntRect bounds, QuadtreeNode dontInclude) {
			int width = 1 << (System.Math.Min (editorHeightLog2,editorWidthLog2)-depth);
			IntRect r = new IntRect(x,y,x+width,y+width);
			if (!IntRect.Intersects (r,bounds)) return;
			
			if (holder.node != null) {
				if (holder.node != dontInclude) {
					arr.Add (holder.node);
				}
			} else {
				AddNeighboursRec (arr, holder.c0, depth+1,x        , y          , bounds, dontInclude);
				AddNeighboursRec (arr, holder.c1, depth+1,x+width/2, y          , bounds, dontInclude);
				AddNeighboursRec (arr, holder.c2, depth+1,x+width/2, y + width/2, bounds, dontInclude);
				AddNeighboursRec (arr, holder.c3, depth+1,x        , y + width/2, bounds, dontInclude);
			}
		}
		
		public QuadtreeNode QueryPoint (int qx, int qy) {
			if (root == null) return null;
			QuadtreeNodeHolder c = root;
			int depth = 0;
			int x = 0;
			int y = 0;
			while (c.node == null) {
				int width = 1 << (System.Math.Min (editorHeightLog2,editorWidthLog2)-depth);
				if (qx >= x + width/2) {
					x = x + width/2;
					if (qy >= y + width/2) {
						y = y + width/2;
						c = c.c2;
					} else {
						c = c.c1;
					}
				} else {
					if (qy >= y + width/2) {
						y = y + width/2;
						c = c.c3;
					} else {
						c = c.c0;
					}
				}
				depth ++;
			}
			return c.node;
		}
		
		public override void OnDrawGizmos (bool drawNodes)
		{
			base.OnDrawGizmos (drawNodes);
			if (!drawNodes) return;
			
			if (root != null)
				DrawRec (root, 0, 0,0, Vector3.zero);
		}
		
		public void DrawRec (QuadtreeNodeHolder h, int depth, int x, int y, Vector3 parentPos) {
			int width = 1 << (System.Math.Min (editorHeightLog2,editorWidthLog2)-depth);
			
			Vector3 pos = LocalToWorldPosition (x,y,width);
			
			Debug.DrawLine (pos, parentPos, Color.red);
			
			if (h.node != null) {
				Debug.DrawRay (pos, Vector3.down, h.node.Walkable ? Color.green : Color.yellow);
			} else {
				DrawRec (h.c0, depth+1,x        , y          , pos);
				DrawRec (h.c1, depth+1,x+width/2, y          , pos);
				DrawRec (h.c2, depth+1,x+width/2, y + width/2, pos);
				DrawRec (h.c3, depth+1,x        , y + width/2, pos);
			}
		}
	}
	
	public class QuadtreeNode : GraphNode {
		
		//new Int3 position;
		
		public GraphNode[] connections;
		public uint[] connectionCosts;
		
		//public override Int3 Position {get { return position; } }
		
		public QuadtreeNode (AstarPath astar) : base (astar) {}
		
		public void SetPosition (Int3 value) {
			position = value;
		}
		
		public override void GetConnections (GraphNodeDelegate del) {
			if (connections == null) return;
			for (int i=0;i<connections.Length;i++) del (connections[i]);
		}
		
		public override void AddConnection (GraphNode node, uint cost) {
			throw new System.NotImplementedException("QuadTree Nodes do not have support for adding manual connections");
		}
		
		public override void RemoveConnection (GraphNode node) {
			throw new System.NotImplementedException("QuadTree Nodes do not have support for adding manual connections");
		}
		
		public override void ClearConnections (bool alsoReverse) {
			if (alsoReverse) {
				for (int i=0;i<connections.Length;i++) {
					connections[i].RemoveConnection (this);
				}
			}
			
			connections = null;
			connectionCosts = null;
		}
		
		public override void Open (Path path, PathNode pathNode, PathHandler handler) {
			if (connections == null) return;
			
			for (int i=0;i<connections.Length;i++) {
				GraphNode other = connections[i];
				
				if (path.CanTraverse (other)) {
					
					PathNode pathOther = handler.GetPathNode (other);
					
					if (pathOther.pathID != handler.PathID) {
						//Might not be assigned
						pathOther.node = other;
						
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
							
							//other.UpdateAllG (pathOther,handler);
							other.UpdateRecursiveG (path,pathOther, handler);
							
							//handler.PushNode (pathOther);
						}
						else if (pathOther.G+tmpCost+path.GetTraversalCost(this) < pathNode.G && other.ContainsConnection (this)) {
							//Or if the path from the other node to this one is better
							
							pathNode.parent = pathOther;
							pathNode.cost = tmpCost;
							
							//UpdateAllG (pathNode,handler);
							
							UpdateRecursiveG(path, pathNode, handler);
							
							//handler.PushNode (pathNode);
						}
					}
				}
			}
		}
	}
		
	public class QuadtreeNodeHolder {
		public QuadtreeNode node;
		public QuadtreeNodeHolder c0;
		public QuadtreeNodeHolder c1;
		public QuadtreeNodeHolder c2;
		public QuadtreeNodeHolder c3;
		
		public void GetNodes (GraphNodeDelegateCancelable del) {
			if (node != null) {
				del (node);
				return;
			}
			
			c0.GetNodes (del);
			c1.GetNodes (del);
			c2.GetNodes (del);
			c3.GetNodes (del);
		}
	}
}
