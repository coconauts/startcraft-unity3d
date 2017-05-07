//#define ASTAR_NoTagPenalty
using UnityEngine;
using Pathfinding;
using Pathfinding.Serialization;

namespace Pathfinding {
	
	public interface INavmeshHolder {
		Int3 GetVertex (int i);
		int GetVertexArrayIndex(int index);
		void GetTileCoordinates (int tileIndex, out int x, out int z);
	}
	
	/** Node represented by a triangle */
	public class TriangleMeshNode : MeshNode {
		
		public TriangleMeshNode (AstarPath astar) : base(astar) {}
		
		public int v0, v1, v2;
		
		protected static INavmeshHolder[] _navmeshHolders = new INavmeshHolder[0];
		public static INavmeshHolder GetNavmeshHolder (uint graphIndex) {
			return _navmeshHolders[(int)graphIndex];
		}
		
		public static void SetNavmeshHolder (int graphIndex, INavmeshHolder graph) {
			if (_navmeshHolders.Length <= graphIndex) {
				INavmeshHolder[] gg = new INavmeshHolder[graphIndex+1];
				for (int i=0;i<_navmeshHolders.Length;i++) gg[i] = _navmeshHolders[i];
				_navmeshHolders = gg;
			}
			_navmeshHolders[graphIndex] = graph;
		}
		
		public void UpdatePositionFromVertices () {
			INavmeshHolder g = GetNavmeshHolder(GraphIndex);
			position = (g.GetVertex(v0) + g.GetVertex(v1) + g.GetVertex(v2)) * 0.333333f;
		}
		
		/** Return a number identifying a vertex.
		 * This number does not necessarily need to be a index in an array but two different vertices (in the same graph) should
		 * not have the same vertex numbers.
		 */
		public int GetVertexIndex (int i) {
			return i == 0 ? v0 : (i == 1 ? v1 : v2);
		}
		
		/** Return a number specifying an index in the source vertex array.
		 * The vertex array can for example be contained in a recast tile, or be a navmesh graph, that is graph dependant.
		 * This is slower than GetVertexIndex, if you only need to compare vertices, use GetVertexIndex.
		 */
		public int GetVertexArrayIndex (int i) {
			return GetNavmeshHolder(GraphIndex).GetVertexArrayIndex (i == 0 ? v0 : (i == 1 ? v1 : v2));
		}
		
		public override Int3 GetVertex (int i) {
			return GetNavmeshHolder(GraphIndex).GetVertex (GetVertexIndex(i));
		}
		
		public override int GetVertexCount () {
			return 3;
		}
		
		public override Vector3 ClosestPointOnNode (Vector3 p) {
			INavmeshHolder g = GetNavmeshHolder(GraphIndex);
			return Pathfinding.Polygon.ClosestPointOnTriangle ((Vector3)g.GetVertex(v0), (Vector3)g.GetVertex(v1), (Vector3)g.GetVertex(v2), p);
		}
		
		public override Vector3 ClosestPointOnNodeXZ (Vector3 _p) {
			INavmeshHolder g = GetNavmeshHolder(GraphIndex);
			Int3 tp1 = g.GetVertex(v0);
			Int3 tp2 = g.GetVertex(v1);
			Int3 tp3 = g.GetVertex(v2);
			
			Int3 p = (Int3)_p;
			int oy = p.y;
			
			// Assumes the triangle vertices are laid out in (counter?)clockwise order
			
			tp1.y = 0;
			tp2.y = 0;
			tp3.y = 0;
			p.y = 0;
			
			if ((long)(tp2.x - tp1.x) * (long)(p.z - tp1.z) - (long)(p.x - tp1.x) * (long)(tp2.z - tp1.z) > 0) {
				float f = Mathf.Clamp01 (AstarMath.NearestPointFactor (tp1, tp2, p));
				return new Vector3(tp1.x + (tp2.x-tp1.x)*f, oy, tp1.z + (tp2.z-tp1.z)*f)*Int3.PrecisionFactor;
			} else if ((long)(tp3.x - tp2.x) * (long)(p.z - tp2.z) - (long)(p.x - tp2.x) * (long)(tp3.z - tp2.z) > 0) {
				float f = Mathf.Clamp01 (AstarMath.NearestPointFactor (tp2, tp3, p));
				return new Vector3(tp2.x + (tp3.x-tp2.x)*f, oy, tp2.z + (tp3.z-tp2.z)*f)*Int3.PrecisionFactor;
			} else if ((long)(tp1.x - tp3.x) * (long)(p.z - tp3.z) - (long)(p.x - tp3.x) * (long)(tp1.z - tp3.z) > 0) {
				float f = Mathf.Clamp01 (AstarMath.NearestPointFactor (tp3, tp1, p));
				return new Vector3(tp3.x + (tp1.x-tp3.x)*f, oy, tp3.z + (tp1.z-tp3.z)*f)*Int3.PrecisionFactor;
			} else {
				return _p;
			}
			
			/*
			 * Equivalent to the above, but the above uses manual inlining
			if (!Polygon.Left (tp1, tp2, p)) {
				float f = Mathf.Clamp01 (AstarMath.NearestPointFactor (tp1, tp2, p));
				return new Vector3(tp1.x + (tp2.x-tp1.x)*f, oy, tp1.z + (tp2.z-tp1.z)*f)*Int3.PrecisionFactor;
			} else if (!Polygon.Left (tp2, tp3, p)) {
				float f = Mathf.Clamp01 (AstarMath.NearestPointFactor (tp2, tp3, p));
				return new Vector3(tp2.x + (tp3.x-tp2.x)*f, oy, tp2.z + (tp3.z-tp2.z)*f)*Int3.PrecisionFactor;
			} else if (!Polygon.Left (tp3, tp1, p)) {
				float f = Mathf.Clamp01 (AstarMath.NearestPointFactor (tp3, tp1, p));
				return new Vector3(tp3.x + (tp1.x-tp3.x)*f, oy, tp3.z + (tp1.z-tp3.z)*f)*Int3.PrecisionFactor;
			} else {
				return _p;
			}*/

			/* Almost equivalent to the above, but this is slower
			Vector3 tp1 = (Vector3)g.GetVertex(v0);
			Vector3 tp2 = (Vector3)g.GetVertex(v1);
			Vector3 tp3 = (Vector3)g.GetVertex(v2);
			tp1.y = 0;
			tp2.y = 0;
			tp3.y = 0;
			_p.y = 0;
			return Pathfinding.Polygon.ClosestPointOnTriangle (tp1,tp2,tp3,_p);*/
		}
		
		public override bool ContainsPoint (Int3 p) {
			INavmeshHolder g = GetNavmeshHolder(GraphIndex);
			
			Int3 a = g.GetVertex(v0);
			Int3 b = g.GetVertex(v1);
			Int3 c = g.GetVertex(v2);
			
			if ((long)(b.x - a.x) * (long)(p.z - a.z) - (long)(p.x - a.x) * (long)(b.z - a.z) > 0) return false;
			
			if ((long)(c.x - b.x) * (long)(p.z - b.z) - (long)(p.x - b.x) * (long)(c.z - b.z) > 0) return false;
			
			if ((long)(a.x - c.x) * (long)(p.z - c.z) - (long)(p.x - c.x) * (long)(a.z - c.z) > 0) return false;
			
			return true;
			//return Polygon.IsClockwiseMargin (a,b, p) && Polygon.IsClockwiseMargin (b,c, p) && Polygon.IsClockwiseMargin (c,a, p);
			
			//return Polygon.ContainsPoint(g.GetVertex(v0),g.GetVertex(v1),g.GetVertex(v2),p);
		}
		
		public override void UpdateRecursiveG (Path path, PathNode pathNode, PathHandler handler) {
			UpdateG (path,pathNode);
			
			handler.PushNode (pathNode);
			
			if (connections == null) return;
			
			for (int i=0;i<connections.Length;i++) {
				GraphNode other = connections[i];
				PathNode otherPN = handler.GetPathNode (other);
				if (otherPN.parent == pathNode && otherPN.pathID == handler.PathID) other.UpdateRecursiveG (path, otherPN,handler);
			}
		}
		
		public override void Open (Path path, PathNode pathNode, PathHandler handler) {
			if (connections == null) return;
			
			bool flag2 = pathNode.flag2;
			
			for (int i=connections.Length-1;i >= 0;i--) {
				GraphNode other = connections[i];
				
				if (path.CanTraverse (other)) {
					
					PathNode pathOther = handler.GetPathNode (other);
					
					//Fast path out, worth it for triangle mesh nodes since they usually have degree 2 or 3
					if (pathOther == pathNode.parent) {
						continue;
					}

					uint cost = connectionCosts[i];

					if (flag2 || pathOther.flag2) {
						cost = path.GetConnectionSpecialCost (this,other,cost);
						
					}
					
					if (pathOther.pathID != handler.PathID) {
						//Might not be assigned
						pathOther.node = other;
						
						pathOther.parent = pathNode;
						pathOther.pathID = handler.PathID;
						
						pathOther.cost = cost;
						
						pathOther.H = path.CalculateHScore (other);
						other.UpdateG (path, pathOther);
						
						handler.PushNode (pathOther);
					} else {

						//If not we can test if the path from this node to the other one is a better one than the one already used
						if (pathNode.G + cost + path.GetTraversalCost(other) < pathOther.G) {
							
							pathOther.cost = cost;
							pathOther.parent = pathNode;
							
							other.UpdateRecursiveG (path, pathOther,handler);
							//handler.PushNode (pathOther);
							
						}
						else if (pathOther.G+cost+path.GetTraversalCost(this) < pathNode.G && other.ContainsConnection (this)) {
							//Or if the path from the other node to this one is better
							
							pathNode.parent = pathOther;
							pathNode.cost = cost;
							
							UpdateRecursiveG (path, pathNode,handler);
							
							//handler.PushNode (pathNode);
						}
					}
				}
			}
		}
		
		/** Returns the edge which is shared with \a other.
		 * If no edge is shared, -1 is returned.
		 * The edge is GetVertex(result) - GetVertex((result+1) % GetVertexCount()).
		 * See GetPortal for the exact segment shared.
		 * \note Might return that an edge is shared when the two nodes are in different tiles and adjacent on the XZ plane, but on the Y-axis.
		 * Therefore it is recommended that you only test for neighbours of this node or do additional checking afterwards.
		 */
		public int SharedEdge (GraphNode other) {
			//Debug.Log ("SHARED");
			int a, b;
			GetPortal(other, null, null, false, out a, out b);
			//Debug.Log ("/SHARED");
			return a;
		}
		
		public override bool GetPortal (GraphNode _other, System.Collections.Generic.List<Vector3> left, System.Collections.Generic.List<Vector3> right, bool backwards) {
			int aIndex, bIndex;
			return GetPortal (_other,left,right,backwards, out aIndex, out bIndex);
		}
		
		public bool GetPortal (GraphNode _other, System.Collections.Generic.List<Vector3> left, System.Collections.Generic.List<Vector3> right, bool backwards, out int aIndex, out int bIndex)
		{
			aIndex = -1;
			bIndex = -1;
			
			//If the nodes are in different graphs, this function has no idea on how to find a shared edge.
			if (_other.GraphIndex != GraphIndex) return false;
			
			TriangleMeshNode other = _other as TriangleMeshNode;
			
			if (!backwards) {
				
				int first = -1;
				int second = -1;
				
				int av = GetVertexCount ();
				int bv = other.GetVertexCount ();
				
				/** \todo Maybe optimize with pa=av-1 instead of modulus... */
				for (int a=0;a<av;a++) {
					int va = GetVertexIndex(a);
					for (int b=0;b<bv;b++) {
						if (va == other.GetVertexIndex((b+1)%bv) && GetVertexIndex((a+1) % av) == other.GetVertexIndex(b)) {
							first = a;
							second = b;
							a = av;
							break;
						}
						
					}
				}
				
				aIndex = first;
				bIndex = second;
				
				if (first != -1) {
					
					if (left != null) {
						//All triangles should be clockwise so second is the rightmost vertex (seen from this node)
						left.Add ((Vector3)GetVertex(first));
						right.Add ((Vector3)GetVertex((first+1)%av));
					}
				} else {
					for ( int i=0;i<connections.Length;i++) {
						
						if ( connections[i].GraphIndex != GraphIndex ) {
							NodeLink3Node mid = connections[i] as NodeLink3Node;
							if ( mid != null && mid.GetOther (this) == other ) {
								// We have found a node which is connected through a NodeLink3Node
								
								if ( left != null ) {
									mid.GetPortal ( other, left, right, false );
									return true;
								}
							}
						}
					}
					return false;
				}
			}
			
			return true;
		}
		
		public override void SerializeNode (GraphSerializationContext ctx)
		{
			base.SerializeNode (ctx);
			ctx.writer.Write(v0);
			ctx.writer.Write(v1);
			ctx.writer.Write(v2);
		}
		
		public override void DeserializeNode (GraphSerializationContext ctx)
		{
			base.DeserializeNode (ctx);
			v0 = ctx.reader.ReadInt32();
			v1 = ctx.reader.ReadInt32();
			v2 = ctx.reader.ReadInt32();
		}
	}
	
	public class ConvexMeshNode : MeshNode {
		
		public ConvexMeshNode (AstarPath astar) : base(astar) {
			indices = new int[0];//\todo Set indices to some reasonable value
		}
		
		private int[] indices;
		//private new Int3 position;
		
		static ConvexMeshNode () {
			
			//Should register to a delegate to receive updates whenever graph lists are changed
		}
		
		protected static INavmeshHolder[] navmeshHolders = new INavmeshHolder[0];
		protected static INavmeshHolder GetNavmeshHolder (uint graphIndex) { return navmeshHolders[(int)graphIndex]; }
		
		/*public override Int3 Position {
			get {
				return position;
			}
		}*/
		
		public void SetPosition (Int3 p) {
			position = p;
		}
		
		public int GetVertexIndex (int i) {
			return indices[i];
		}
		
		public override Int3 GetVertex (int i) {
			return GetNavmeshHolder(GraphIndex).GetVertex (GetVertexIndex(i));
		}
		
		public override int GetVertexCount () {
			return indices.Length;
		}
		
		public override Vector3 ClosestPointOnNode (Vector3 p)
		{
			throw new System.NotImplementedException ();
		}
		
		public override Vector3 ClosestPointOnNodeXZ (Vector3 p)
		{
			throw new System.NotImplementedException ();
		}
		
		public override void GetConnections (GraphNodeDelegate del) {
			if (connections == null) return;
			for (int i=0;i<connections.Length;i++) del (connections[i]);
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
	}
	
	/*public class ConvexMeshNode : GraphNode {
		//Vertices
		public int v1;
		public int v2;
		public int v3;
		
		public int GetVertexIndex (int i) {
			if (i == 0) {
				return v1;
			} else if (i == 1) {
				return v2;
			} else if (i == 2) {
				return v3;
			} else {
				throw new System.ArgumentOutOfRangeException ("A MeshNode only contains 3 vertices");
			}
		}
		
		public int this[int i]
	    {
	        get
	        {
	            if (i == 0) {
					return v1;
				} else if (i == 1) {
					return v2;
				} else if (i == 2) {
					return v3;
				} else {
					throw new System.ArgumentOutOfRangeException ("A MeshNode only contains 3 vertices");
				}
	        }
	    }
	    
	    public Vector3 ClosestPoint (Vector3 p, Int3[] vertices) {
	    	return Polygon.ClosestPointOnTriangle ((Vector3)vertices[v1],(Vector3)vertices[v2],(Vector3)vertices[v3],p);
	    }
	}*/
}