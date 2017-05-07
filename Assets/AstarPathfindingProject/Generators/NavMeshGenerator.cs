//#define ASTARDEBUG    //Some debugging
//#define ASTAR_NO_JSON
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding.Serialization.JsonFx;
using Pathfinding.Serialization;
using Pathfinding;


namespace Pathfinding {
	public interface INavmesh {
		
		//TriangleMeshNode[] TriNodes {get;}
		void GetNodes(GraphNodeDelegateCancelable del);
		
		//Int3[] originalVertices {
		//	get;
		//	set;
		//}
	}
	
	[System.Serializable]
	[JsonOptIn]
	/** Generates graphs based on navmeshes.
\ingroup graphs
Navmeshes are meshes where each polygon define a walkable area.
These are great because the AI can get so much more information on how it can walk.
Polygons instead of points mean that the funnel smoother can produce really nice looking paths and the graphs are also really fast to search
and have a low memory footprint because of their smaller size to describe the same area (compared to grid graphs).
\see Pathfinding.RecastGraph

\shadowimage{navmeshgraph_graph.png}
\shadowimage{navmeshgraph_inspector.png}

	 */
	public class NavMeshGraph : NavGraph, INavmesh, IUpdatableGraph, IFunnelGraph, INavmeshHolder
	{
		
		public override void CreateNodes (int number) {
			TriangleMeshNode[] tmp = new TriangleMeshNode[number];
			for (int i=0;i<number;i++) {
				tmp[i] = new TriangleMeshNode (active);
				tmp[i].Penalty = initialPenalty;
			}
		}
		
		[JsonMember]
		public Mesh sourceMesh; /**< Mesh to construct navmesh from */
		
		[JsonMember]
		public Vector3 offset; /**< Offset in world space */
		
		[JsonMember]
		public Vector3 rotation; /**< Rotation in degrees */
		
		[JsonMember]
		public float scale = 1; /**< Scale of the graph */
		
		[JsonMember]
		/** More accurate nearest node queries.
		 * When on, looks for the closest point on every triangle instead of if point is inside the node triangle in XZ space.
		 * This is slower, but a lot better if your mesh contains overlaps (e.g bridges over other areas of the mesh).
		 * Note that for maximum effect the Full Get Nearest Node Search setting should be toggled in A* Inspector Settings.
		 */
		public bool accurateNearestNode = true;
		
		public TriangleMeshNode[] nodes;
		
		public TriangleMeshNode[] TriNodes {
			get { return nodes; }
		}
		
		public override void GetNodes (GraphNodeDelegateCancelable del) {
			if (nodes == null) return;
			for (int i=0;i<nodes.Length && del (nodes[i]);i++) {}
		}
		
		public override void OnDestroy () {
			
			base.OnDestroy ();
			
			// Cleanup
			TriangleMeshNode.SetNavmeshHolder (active.astarData.GetGraphIndex(this), null);
		}
		
		public Int3 GetVertex (int index) {
			return vertices[index];
		}
		
		public int GetVertexArrayIndex (int index) {
			return index;
		}
		
		public void GetTileCoordinates (int tileIndex, out int x, out int z) {
			//Tiles not supported
			x = z = 0;
		}
		
		/** Bounding Box Tree. Enables really fast lookups of nodes. \astarpro */
		BBTree _bbTree;
		public BBTree bbTree {
			get { return _bbTree; }
			set { _bbTree = value;}
		}
		
		[System.NonSerialized]
		Int3[] _vertices;
		
		public Int3[] vertices {
			get {
				return _vertices;
			}
			set {
				_vertices = value;
			}
		}
		
		[System.NonSerialized]
		Vector3[] originalVertices;
		
		/*public Int3[] originalVertices {
			get { 	return _originalVertices; 	}
			set { 	_originalVertices = value;	}
		}*/
		
		[System.NonSerialized]
		public int[] triangles;
		
		public void GenerateMatrix () {
			
			SetMatrix (Matrix4x4.TRS (offset,Quaternion.Euler (rotation),new Vector3 (scale,scale,scale)));
			
		}
		
		/** Relocates the nodes to match the newMatrix.
		 * The "oldMatrix" variable can be left out in this function call (only for this graph generator) since it is not used */
		public override void RelocateNodes (Matrix4x4 oldMatrix, Matrix4x4 newMatrix) {
			//base.RelocateNodes (oldMatrix,newMatrix);
			
			if (vertices == null || vertices.Length == 0 || originalVertices == null || originalVertices.Length != vertices.Length) {
				return;
			}
			
			for (int i=0;i<_vertices.Length;i++) {
				//Vector3 tmp = inv.MultiplyPoint3x4 (vertices[i]);
				//vertices[i] = (Int3)newMatrix.MultiplyPoint3x4 (tmp);
				_vertices[i] = (Int3)newMatrix.MultiplyPoint3x4 ((Vector3)originalVertices[i]);
			}
			
			for (int i=0;i<nodes.Length;i++) {
				TriangleMeshNode node = (TriangleMeshNode)nodes[i];
				node.UpdatePositionFromVertices();
				
				if (node.connections != null) {
					for (int q=0;q<node.connections.Length;q++) {
						node.connectionCosts[q] = (uint)(node.position-node.connections[q].position).costMagnitude;
					}
				}
			}
			
			SetMatrix (newMatrix);
			
		}
	
		public static NNInfo GetNearest (NavMeshGraph graph, GraphNode[] nodes, Vector3 position, NNConstraint constraint, bool accurateNearestNode) {
			if (nodes == null || nodes.Length == 0) {
				Debug.LogError ("NavGraph hasn't been generated yet or does not contain any nodes");
				return new NNInfo ();
			}
			
			if (constraint == null) constraint = NNConstraint.None;
			
			
			return GetNearestForceBoth (graph,graph, position, NNConstraint.None, accurateNearestNode);
		}
		
		public override NNInfo GetNearest (Vector3 position, NNConstraint constraint, GraphNode hint) {
			return GetNearest (this, nodes,position, constraint, accurateNearestNode);
		}
		
		/** This performs a linear search through all polygons returning the closest one.
		 * This is usually only called in the Free version of the A* Pathfinding Project since the Pro one supports BBTrees and will do another query
		 */
		public override NNInfo GetNearestForce (Vector3 position, NNConstraint constraint) {
			
			return GetNearestForce (this, this,position,constraint, accurateNearestNode);
			//Debug.LogWarning ("This function shouldn't be called since constrained nodes are sent back in the GetNearest call");
			
			//return new NNInfo ();
		}
		
		/** This performs a linear search through all polygons returning the closest one */
		public static NNInfo GetNearestForce (NavGraph graph, INavmeshHolder navmesh, Vector3 position, NNConstraint constraint, bool accurateNearestNode) {
			NNInfo nn = GetNearestForceBoth (graph, navmesh,position,constraint,accurateNearestNode);
			nn.node = nn.constrainedNode;
			nn.clampedPosition = nn.constClampedPosition;
			return nn;
		}
		
		/** This performs a linear search through all polygons returning the closest one.
		  * This will fill the NNInfo with .node for the closest node not necessarily complying with the NNConstraint, and .constrainedNode with the closest node
		  * complying with the NNConstraint.
		  * \see GetNearestForce(Node[],Int3[],Vector3,NNConstraint,bool)
		  */
		public static NNInfo GetNearestForceBoth (NavGraph graph, INavmeshHolder navmesh, Vector3 position, NNConstraint constraint, bool accurateNearestNode) {
			Int3 pos = (Int3)position;
			
			float minDist = -1;
			GraphNode minNode = null;
			
			float minConstDist = -1;
			GraphNode minConstNode = null;
			
			float maxDistSqr = constraint.constrainDistance ? AstarPath.active.maxNearestNodeDistanceSqr : float.PositiveInfinity;
			
			GraphNodeDelegateCancelable del = delegate (GraphNode _node) {
				TriangleMeshNode node = _node as TriangleMeshNode;
				
				if (accurateNearestNode) {
					
					Vector3 closest = node.ClosestPointOnNode (position);
					float dist = ((Vector3)pos-closest).sqrMagnitude;
					
					if (minNode == null || dist < minDist) {
						minDist = dist;
						minNode = node;
					}
					
					if (dist < maxDistSqr && constraint.Suitable (node)) {
						if (minConstNode == null || dist < minConstDist) {
							minConstDist = dist;
							minConstNode = node;
						}
					}
					
				} else {
					
					if (!node.ContainsPoint ((Int3)position)) {
						
						float dist = (node.position-pos).sqrMagnitude;
						if (minNode == null || dist < minDist) {
							minDist = dist;
							minNode = node;
						}
						
						if (dist < maxDistSqr && constraint.Suitable (node)) {
							if (minConstNode == null || dist < minConstDist) {
								minConstDist = dist;
								minConstNode = node;
							}
						}
						
					} else {
					
						
						int dist = AstarMath.Abs (node.position.y-pos.y);
						
						if (minNode == null || dist < minDist) {
							minDist = dist;
							minNode = node;
						}
						
						if (dist < maxDistSqr && constraint.Suitable (node)) {
							if (minConstNode == null || dist < minConstDist) {
								minConstDist = dist;
								minConstNode = node;
							}
						}
					}
				}
				return true;
			};
			
			graph.GetNodes (del);
			
			NNInfo nninfo = new NNInfo (minNode);
			
			//Find the point closest to the nearest triangle
				
			if (nninfo.node != null) {
				TriangleMeshNode node = nninfo.node as TriangleMeshNode;//minNode2 as MeshNode;
				
				Vector3 clP = node.ClosestPointOnNode (position);
				
				nninfo.clampedPosition = clP;
			}
			
			nninfo.constrainedNode = minConstNode;
			if (nninfo.constrainedNode != null) {
				TriangleMeshNode node = nninfo.constrainedNode as TriangleMeshNode;//minNode2 as MeshNode;
				
				Vector3 clP = node.ClosestPointOnNode (position);
				
				nninfo.constClampedPosition = clP;
			}
			
			return nninfo;
		}
		
		public void BuildFunnelCorridor (List<GraphNode> path, int startIndex, int endIndex, List<Vector3> left, List<Vector3> right) {
			BuildFunnelCorridor (this,path,startIndex,endIndex,left,right);
		}
		
		public static void BuildFunnelCorridor (INavmesh graph, List<GraphNode> path, int startIndex, int endIndex, List<Vector3> left, List<Vector3> right) {
			
			if (graph == null) {
				Debug.LogError ("Couldn't cast graph to the appropriate type (graph isn't a Navmesh type graph, it doesn't implement the INavmesh interface)");
				return;
			}
			
			for (int i=startIndex;i<endIndex;i++) {
				//Find the connection between the nodes
				
				TriangleMeshNode n1 = path[i] as TriangleMeshNode;
				TriangleMeshNode n2 = path[i+1] as TriangleMeshNode;
				
				int a;
				bool search = true;
				for (a=0;a<3;a++) {
					for (int b=0;b<3;b++) {
						if (n1.GetVertexIndex(a) == n2.GetVertexIndex((b+1)%3) && n1.GetVertexIndex((a+1)%3) == n2.GetVertexIndex(b)) {
							search = false;
							break;
						}
					}
					if (!search) break;
				}
				
				if (a == 3) {
					left.Add ((Vector3)n1.position);
					right.Add ((Vector3)n1.position);
					left.Add ((Vector3)n2.position);
					right.Add ((Vector3)n2.position);
				} else {
					left.Add ((Vector3)n1.GetVertex(a));
					right.Add ((Vector3)n1.GetVertex((a+1)%3));
				}
			}
		}
		
		public void AddPortal (GraphNode n1, GraphNode n2, List<Vector3> left, List<Vector3> right) {
		}
		
		
		public GraphUpdateThreading CanUpdateAsync (GraphUpdateObject o) {
			return GraphUpdateThreading.UnityThread;
		}
		
		public void UpdateAreaInit (GraphUpdateObject o) {}
		
		public void UpdateArea (GraphUpdateObject o) {
			
		}
		
		
		public static void UpdateArea (GraphUpdateObject o, INavmesh graph) {
			
			//System.DateTime startTime = System.DateTime.UtcNow;
				
			Bounds bounds = o.bounds;
			
			Rect r = Rect.MinMaxRect (bounds.min.x,bounds.min.z,bounds.max.x,bounds.max.z);
			
			IntRect r2 = new IntRect(
				Mathf.FloorToInt(bounds.min.x*Int3.Precision),
				Mathf.FloorToInt(bounds.min.z*Int3.Precision),
				Mathf.FloorToInt(bounds.max.x*Int3.Precision),
				Mathf.FloorToInt(bounds.max.z*Int3.Precision)
			);
			
			/*Vector3 a = new Vector3 (r.xMin,0,r.yMin);//	-1 	-1
			Vector3 b = new Vector3 (r.xMin,0,r.yMax);//	-1	 1 
			Vector3 c = new Vector3 (r.xMax,0,r.yMin);//	 1 	-1
			Vector3 d = new Vector3 (r.xMax,0,r.yMax);//	 1 	 1
			*/
			Int3 a = new Int3(r2.xmin,0,r2.ymin);
			Int3 b = new Int3(r2.xmin,0,r2.ymax);
			Int3 c = new Int3(r2.xmax,0,r2.ymin);
			Int3 d = new Int3(r2.xmax,0,r2.ymax);
			
			Int3 ia = (Int3)a;
			Int3 ib = (Int3)b;
			Int3 ic = (Int3)c;
			Int3 id = (Int3)d;
			
			
			//for (int i=0;i<nodes.Length;i++) {
			graph.GetNodes (delegate (GraphNode _node) {
				TriangleMeshNode node = _node as TriangleMeshNode;
				
				bool inside = false;
				
				int allLeft = 0;
				int allRight = 0;
				int allTop = 0;
				int allBottom = 0;
				
				for (int v=0;v<3;v++) {
					
					Int3 p = node.GetVertex(v);
					Vector3 vert = (Vector3)p;
					//Vector2 vert2D = new Vector2 (vert.x,vert.z);
					
					if (r2.Contains (p.x,p.z)) {
						//Debug.DrawRay (vert,Vector3.up*10,Color.yellow);
						inside = true;
						break;
					}
					
					if (vert.x < r.xMin) allLeft++;
					if (vert.x > r.xMax) allRight++;
					if (vert.z < r.yMin) allTop++;
					if (vert.z > r.yMax) allBottom++;
					
					//if (!bounds.Contains (node[v]) {
					//	inside = false;
					//	break;
					//}
				}
				if (!inside) {
					if (allLeft == 3 || allRight == 3 || allTop == 3 || allBottom == 3) {
						return true;
					}
				}
				
				//Debug.DrawLine ((Vector3)node.GetVertex(0),(Vector3)node.GetVertex(1),Color.yellow);
				//Debug.DrawLine ((Vector3)node.GetVertex(1),(Vector3)node.GetVertex(2),Color.yellow);
				//Debug.DrawLine ((Vector3)node.GetVertex(2),(Vector3)node.GetVertex(0),Color.yellow);
				
				for (int v=0;v<3;v++) {
					int v2 = v > 1 ? 0 : v+1;
					
					Int3 vert1 = node.GetVertex(v);
					Int3 vert2 = node.GetVertex(v2);
					
					if (Polygon.Intersects (a,b,vert1,vert2)) { inside = true; break; }
					if (Polygon.Intersects (a,c,vert1,vert2)) { inside = true; break; }
					if (Polygon.Intersects (c,d,vert1,vert2)) { inside = true; break; }
					if (Polygon.Intersects (d,b,vert1,vert2)) { inside = true; break; }
				}
				
				
				
				if (node.ContainsPoint (ia) || node.ContainsPoint (ib) || node.ContainsPoint (ic) || node.ContainsPoint (id)) {
					inside = true;
				}
				
				if (!inside) {
					return true;
				}
				
				o.WillUpdateNode(node);
				o.Apply (node);
				/*Debug.DrawLine ((Vector3)node.GetVertex(0),(Vector3)node.GetVertex(1),Color.blue);
				Debug.DrawLine ((Vector3)node.GetVertex(1),(Vector3)node.GetVertex(2),Color.blue);
				Debug.DrawLine ((Vector3)node.GetVertex(2),(Vector3)node.GetVertex(0),Color.blue);
				Debug.Break ();*/
				return true;
			});
			
			//System.DateTime endTime = System.DateTime.UtcNow;
			//float theTime = (endTime-startTime).Ticks*0.0001F;
			//Debug.Log ("Intersecting bounds with navmesh took "+theTime.ToString ("0.000")+" ms");
		
		}
		
		/** Returns the closest point of the node */
		public static Vector3 ClosestPointOnNode (TriangleMeshNode node, Int3[] vertices, Vector3 pos) {
			return Polygon.ClosestPointOnTriangle ((Vector3)vertices[node.v0],(Vector3)vertices[node.v1],(Vector3)vertices[node.v2],pos);
		}
		
		/** Returns if the point is inside the node in XZ space */
		public bool ContainsPoint (TriangleMeshNode node, Vector3 pos) {
			if (	Polygon.IsClockwise ((Vector3)vertices[node.v0],(Vector3)vertices[node.v1], pos)
			    && 	Polygon.IsClockwise ((Vector3)vertices[node.v1],(Vector3)vertices[node.v2], pos)
			    && 	Polygon.IsClockwise ((Vector3)vertices[node.v2],(Vector3)vertices[node.v0], pos)) {
				return true;
			}
			return false;
		}
		
		/** Returns if the point is inside the node in XZ space */
		public static bool ContainsPoint (TriangleMeshNode node, Vector3 pos, Int3[] vertices) {
			if (!Polygon.IsClockwiseMargin ((Vector3)vertices[node.v0],(Vector3)vertices[node.v1], (Vector3)vertices[node.v2])) {
				Debug.LogError ("Noes!");
			}
			
			if ( 	Polygon.IsClockwiseMargin ((Vector3)vertices[node.v0],(Vector3)vertices[node.v1], pos)
			    && 	Polygon.IsClockwiseMargin ((Vector3)vertices[node.v1],(Vector3)vertices[node.v2], pos)
			    && 	Polygon.IsClockwiseMargin ((Vector3)vertices[node.v2],(Vector3)vertices[node.v0], pos)) {
				return true;
			}
			return false;
		}
		
		/** Scans the graph using the path to an .obj mesh */
		public void ScanInternal (string objMeshPath) {
			
			Mesh mesh = ObjImporter.ImportFile (objMeshPath);
			
			if (mesh == null) {
				Debug.LogError ("Couldn't read .obj file at '"+objMeshPath+"'");
				return;
			}
			
			sourceMesh = mesh;
			ScanInternal ();
		}
		
		public override void ScanInternal (OnScanStatus statusCallback) {
			
			if (sourceMesh == null) {
				return;
			}
			
			GenerateMatrix ();
			
			//float startTime = 0;//Time.realtimeSinceStartup;
			
			Vector3[] vectorVertices = sourceMesh.vertices;
			
			triangles = sourceMesh.triangles;
			
			TriangleMeshNode.SetNavmeshHolder (active.astarData.GetGraphIndex(this),this);
			GenerateNodes (vectorVertices,triangles, out originalVertices, out _vertices);
		
		}
		
		/** Generates a navmesh. Based on the supplied vertices and triangles. Memory usage is about O(n) */
		public void GenerateNodes (Vector3[] vectorVertices, int[] triangles, out Vector3[] originalVertices, out Int3[] vertices) {
			
			Profiler.BeginSample ("Init");

			if (vectorVertices.Length == 0 || triangles.Length == 0) {
				originalVertices = vectorVertices;
				vertices = new Int3[0];
				//graph.CreateNodes (0);
				nodes = new TriangleMeshNode[0];
				return;
			}
			
			vertices = new Int3[vectorVertices.Length];
			
			//Backup the original vertices
			//for (int i=0;i<vectorVertices.Length;i++) {
			//	vectorVertices[i] = graph.matrix.MultiplyPoint (vectorVertices[i]);
			//}
			
			int c = 0;
			
			for (int i=0;i<vertices.Length;i++) {
				vertices[i] = (Int3)matrix.MultiplyPoint3x4 (vectorVertices[i]);
			}
			
			Dictionary<Int3,int> hashedVerts = new Dictionary<Int3,int> ();
			
			int[] newVertices = new int[vertices.Length];
				
			Profiler.EndSample ();
			Profiler.BeginSample ("Hashing");

			for (int i=0;i<vertices.Length;i++) {
				if (!hashedVerts.ContainsKey (vertices[i])) {
					newVertices[c] = i;
					hashedVerts.Add (vertices[i], c);
					c++;
				}// else {
					//Debug.Log ("Hash Duplicate "+hash+" "+vertices[i].ToString ());
				//}
			}
			
			/*newVertices[c] = vertices.Length-1;

			if (!hashedVerts.ContainsKey (vertices[newVertices[c]])) {
				
				hashedVerts.Add (vertices[newVertices[c]], c);
				c++;
			}*/
			
			for (int x=0;x<triangles.Length;x++) {
				Int3 vertex = vertices[triangles[x]];

				triangles[x] = hashedVerts[vertex];
			}
			
			/*for (int i=0;i<triangles.Length;i += 3) {
				
				Vector3 offset = Vector3.forward*i*0.01F;
				Debug.DrawLine (newVertices[triangles[i]]+offset,newVertices[triangles[i+1]]+offset,Color.blue);
				Debug.DrawLine (newVertices[triangles[i+1]]+offset,newVertices[triangles[i+2]]+offset,Color.blue);
				Debug.DrawLine (newVertices[triangles[i+2]]+offset,newVertices[triangles[i]]+offset,Color.blue);
			}*/
			
			Int3[] totalIntVertices = vertices;
			vertices = new Int3[c];
			originalVertices = new Vector3[c];
			for (int i=0;i<c;i++) {
				
				vertices[i] = totalIntVertices[newVertices[i]];//(Int3)graph.matrix.MultiplyPoint (vectorVertices[i]);
				originalVertices[i] = (Vector3)vectorVertices[newVertices[i]];//vectorVertices[newVertices[i]];
			}

			Profiler.EndSample ();
			Profiler.BeginSample ("Constructing Nodes");

			//graph.CreateNodes (triangles.Length/3);//new Node[triangles.Length/3];
			nodes = new TriangleMeshNode[triangles.Length/3];
			
			for (int i=0;i<nodes.Length;i++) {
				
				nodes[i] = new TriangleMeshNode(active);
				TriangleMeshNode node = nodes[i];//new MeshNode ();
				
				node.Penalty = initialPenalty;
				node.Walkable = true;
				
				
				node.v0 = triangles[i*3];
				node.v1 = triangles[i*3+1];
				node.v2 = triangles[i*3+2];
				
				if (!Polygon.IsClockwise (vertices[node.v0],vertices[node.v1],vertices[node.v2])) {
					//Debug.DrawLine (vertices[node.v0],vertices[node.v1],Color.red);
					//Debug.DrawLine (vertices[node.v1],vertices[node.v2],Color.red);
					//Debug.DrawLine (vertices[node.v2],vertices[node.v0],Color.red);
					
					int tmp = node.v0;
					node.v0 = node.v2;
					node.v2 = tmp;
				}
				
				if (Polygon.IsColinear (vertices[node.v0],vertices[node.v1],vertices[node.v2])) {
					Debug.DrawLine ((Vector3)vertices[node.v0],(Vector3)vertices[node.v1],Color.red);
					Debug.DrawLine ((Vector3)vertices[node.v1],(Vector3)vertices[node.v2],Color.red);
					Debug.DrawLine ((Vector3)vertices[node.v2],(Vector3)vertices[node.v0],Color.red);
				}
				
				// Make sure position is correctly set
				node.UpdatePositionFromVertices();
			}

			Profiler.EndSample ();

			Dictionary<Int2,TriangleMeshNode> sides = new Dictionary<Int2, TriangleMeshNode>();
			
			for (int i=0, j=0;i<triangles.Length; j+=1, i+=3) {
				sides[new Int2(triangles[i+0],triangles[i+1])] = nodes[j];
				sides[new Int2(triangles[i+1],triangles[i+2])] = nodes[j];
				sides[new Int2(triangles[i+2],triangles[i+0])] = nodes[j];
			}

			Profiler.BeginSample ("Connecting Nodes");

			List<MeshNode> connections = new List<MeshNode> ();
			List<uint> connectionCosts = new List<uint> ();
			
			int identicalError = 0;

			for (int i=0, j=0;i<triangles.Length; j+=1, i+=3) {
				connections.Clear ();
				connectionCosts.Clear ();
				
				//Int3 indices = new Int3(triangles[i],triangles[i+1],triangles[i+2]);
				
				TriangleMeshNode node = nodes[j];

				for ( int q = 0; q < 3; q++ ) {
					TriangleMeshNode other;
					if (sides.TryGetValue ( new Int2 (triangles[i+((q+1)%3)], triangles[i+q]), out other ) ) {
						connections.Add (other);
						connectionCosts.Add ((uint)(node.position-other.position).costMagnitude);
					}
				}

				node.connections = connections.ToArray ();
				node.connectionCosts = connectionCosts.ToArray ();
			}
			
			if (identicalError > 0) {
				Debug.LogError ("One or more triangles are identical to other triangles, this is not a good thing to have in a navmesh\nIncreasing the scale of the mesh might help\nNumber of triangles with error: "+identicalError+"\n");
			}

			Profiler.EndSample ();
			Profiler.BeginSample ("Rebuilding BBTree");

			RebuildBBTree (this);

			Profiler.EndSample ();

			//Debug.Log ("Graph Generation - NavMesh - Time to compute graph "+((Time.realtimeSinceStartup-startTime)*1000F).ToString ("0")+"ms");
		}
		
		/** Rebuilds the BBTree on a NavGraph.
		 * \astarpro
		 * \see NavMeshGraph.bbTree */
		public static void RebuildBBTree (NavMeshGraph graph) {
			//BBTrees is a A* Pathfinding Project Pro only feature - The Pro version can be bought in the Unity Asset Store or on arongranberg.com
		}
		
		public void PostProcess () {
		}
		
		public void Sort (Vector3[] a) {
			
			bool changed = true;
		
			while (changed) {
				changed = false;
				for (int i=0;i<a.Length-1;i++) {
					if (a[i].x > a[i+1].x || (a[i].x == a[i+1].x && (a[i].y > a[i+1].y || (a[i].y == a[i+1].y && a[i].z > a[i+1].z)))) {
						Vector3 tmp = a[i];
						a[i] = a[i+1];
						a[i+1] = tmp;
						changed = true;
					}
				}
			}
		}
		
		public override void OnDrawGizmos (bool drawNodes) {
			
			if (!drawNodes) {
				return;
			}

			Matrix4x4 preMatrix = matrix;
			
			GenerateMatrix ();
			
			if (nodes == null) {
				//Scan ();
			}
			
			if (nodes == null) {
				return;
			}

			if ( bbTree != null ) {
				bbTree.OnDrawGizmos ();
			}

			if (preMatrix != matrix) {
				//Debug.Log ("Relocating Nodes");
				RelocateNodes (preMatrix, matrix);
			}
			
			PathHandler debugData = AstarPath.active.debugPathData;
			for (int i=0;i<nodes.Length;i++) {
				
				
				TriangleMeshNode node = (TriangleMeshNode)nodes[i];
				
				Gizmos.color = NodeColor (node,AstarPath.active.debugPathData);
				
				if (node.Walkable ) {
					
					if (AstarPath.active.showSearchTree && debugData != null && debugData.GetPathNode(node).parent != null) {
						Gizmos.DrawLine ((Vector3)node.position,(Vector3)debugData.GetPathNode(node).parent.node.position);
					} else {
						for (int q=0;q<node.connections.Length;q++) {
							Gizmos.DrawLine ((Vector3)node.position,Vector3.Lerp ((Vector3)node.position, (Vector3)node.connections[q].position, 0.45f));
						}
					}
				
					Gizmos.color = AstarColor.MeshEdgeColor;
				} else {
					Gizmos.color = Color.red;
				}
				Gizmos.DrawLine ((Vector3)vertices[node.v0],(Vector3)vertices[node.v1]);
				Gizmos.DrawLine ((Vector3)vertices[node.v1],(Vector3)vertices[node.v2]);
				Gizmos.DrawLine ((Vector3)vertices[node.v2],(Vector3)vertices[node.v0]);
				
			}
			
		}
		
		public override void DeserializeExtraInfo (GraphSerializationContext ctx)
		{
			
			uint graphIndex = (uint)active.astarData.GetGraphIndex(this);
			TriangleMeshNode.SetNavmeshHolder ((int)graphIndex,this);
			
			int c1 = ctx.reader.ReadInt32();
			int c2 = ctx.reader.ReadInt32();
			
			if (c1 == -1) {
				nodes = new TriangleMeshNode[0];
				_vertices = new Int3[0];
				originalVertices = new Vector3[0];
			}
			
			nodes = new TriangleMeshNode[c1];
			_vertices = new Int3[c2];
			originalVertices = new Vector3[c2];
			
			for (int i=0;i<c2;i++) {
				_vertices[i] = new Int3(ctx.reader.ReadInt32(), ctx.reader.ReadInt32(), ctx.reader.ReadInt32());
				originalVertices[i] = new Vector3(ctx.reader.ReadSingle(), ctx.reader.ReadSingle(), ctx.reader.ReadSingle());
			}
			
			
			for (int i=0;i<c1;i++) {
				nodes[i] = new TriangleMeshNode(active);
				TriangleMeshNode node = nodes[i];
				node.DeserializeNode(ctx);
				node.GraphIndex = graphIndex;
				node.UpdatePositionFromVertices();
			}
		}
		
		public override void SerializeExtraInfo (GraphSerializationContext ctx)
		{
			if (nodes == null || originalVertices == null || _vertices == null || originalVertices.Length != _vertices.Length) {
				ctx.writer.Write (-1);
				ctx.writer.Write (-1);
				return;
			}
			ctx.writer.Write(nodes.Length);
			ctx.writer.Write(_vertices.Length);
			
			for (int i=0;i<_vertices.Length;i++) {
				ctx.writer.Write (_vertices[i].x);
				ctx.writer.Write (_vertices[i].y);
				ctx.writer.Write (_vertices[i].z);
				
				ctx.writer.Write (originalVertices[i].x);
				ctx.writer.Write (originalVertices[i].y);
				ctx.writer.Write (originalVertices[i].z);
			}
			
			for (int i=0;i<nodes.Length;i++) {
				nodes[i].SerializeNode (ctx);
			}
		}
		
		public static void DeserializeMeshNodes (NavMeshGraph graph, GraphNode[] nodes, byte[] bytes) {
			
			System.IO.MemoryStream mem = new System.IO.MemoryStream(bytes);
			System.IO.BinaryReader stream = new System.IO.BinaryReader(mem);
			
			for (int i=0;i<nodes.Length;i++) {
				TriangleMeshNode node = nodes[i] as TriangleMeshNode;
				
				if (node == null) {
					Debug.LogError ("Serialization Error : Couldn't cast the node to the appropriate type - NavMeshGenerator");
					return;
				}
				
				node.v0 = stream.ReadInt32 ();
				node.v1 = stream.ReadInt32 ();
				node.v2 = stream.ReadInt32 ();
				
			}
			
			int numVertices = stream.ReadInt32 ();
			
			graph.vertices = new Int3[numVertices];
			
			for (int i=0;i<numVertices;i++) {
				int x = stream.ReadInt32 ();
				int y = stream.ReadInt32 ();
				int z = stream.ReadInt32 ();
				
				graph.vertices[i] = new Int3 (x,y,z);
			}
			
			RebuildBBTree (graph);
		}

	}
}