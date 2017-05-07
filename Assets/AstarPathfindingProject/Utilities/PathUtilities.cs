//#define ASTAR_PROFILE

using Pathfinding;
using Pathfinding.Util;
using System.Collections.Generic;
using UnityEngine;

namespace Pathfinding
{
	/** Contains useful functions for working with paths and nodes.
	 * This class works a lot with the Node class, a useful function to get nodes is AstarPath.GetNearest.
	  * \see AstarPath.GetNearest
	  * \see Pathfinding.Utils.GraphUpdateUtilities
	  * \since Added in version 3.2
	  * \ingroup utils
	  * 
	  */
	public static class PathUtilities {
		/** Returns if there is a walkable path from \a n1 to \a n2.
		 * If you are making changes to the graph, areas must first be recaculated using FloodFill()
		 * \note This might return true for small areas even if there is no possible path if AstarPath.minAreaSize is greater than zero (0).
		 * So when using this, it is recommended to set AstarPath.minAreaSize to 0. (A* Inspector -> Settings -> Pathfinding)
		 * \see AstarPath.GetNearest
		 */
		public static bool IsPathPossible (GraphNode n1, GraphNode n2) {
			return n1.Walkable && n2.Walkable && n1.Area == n2.Area;
		}
		
		/** Returns if there are walkable paths between all nodes.
		 * If you are making changes to the graph, areas must first be recaculated using FloodFill()
		 * \note This might return true for small areas even if there is no possible path if AstarPath.minAreaSize is greater than zero (0).
		 * So when using this, it is recommended to set AstarPath.minAreaSize to 0. (A* Inspector -> Settings -> Pathfinding)
		 * \see AstarPath.GetNearest
		 */
		public static bool IsPathPossible (List<GraphNode> nodes) {
			uint area = nodes[0].Area;
			for (int i=0;i<nodes.Count;i++) if (!nodes[i].Walkable || nodes[i].Area != area) return false;
			return true;
		}
		
		/** Returns all nodes reachable from the seed node.
		 * This function performs a BFS (breadth-first-search) or flood fill of the graph and returns all nodes which can be reached from
		 * the seed node. In almost all cases this will be identical to returning all nodes which have the same area as the seed node.
		 * In the editor areas are displayed as different colors of the nodes.
		 * The only case where it will not be so is when there is a one way path from some part of the area to the seed node
		 * but no path from the seed node to that part of the graph.
		 * 
		 * The returned list is sorted by node distance from the seed node
		 * i.e distance is measured in the number of nodes the shortest path from \a seed to that node would pass through.
		 * Note that the distance measurement does not take heuristics, penalties or tag penalties.
		 * 
		 * Depending on the number of reachable nodes, this function can take quite some time to calculate
		 * so don't use it too often or it might affect the framerate of your game.
		 * 
		 * \param seed The node to start the search from
		 * \param tagMask Optional mask for tags. This is a bitmask.
		 * 
		 * \returns A List<Node> containing all nodes reachable from the seed node.
		 * For better memory management the returned list should be pooled, see Pathfinding.Util.ListPool
		 */
		public static List<GraphNode> GetReachableNodes (GraphNode seed, int tagMask = -1) {
			Stack<GraphNode> stack = Pathfinding.Util.StackPool<GraphNode>.Claim ();
			List<GraphNode> list = Pathfinding.Util.ListPool<GraphNode>.Claim ();
			
			
			HashSet<GraphNode> map = new HashSet<GraphNode>();
			
			GraphNodeDelegate callback;
			if (tagMask == -1) {
				callback = delegate (GraphNode node) {
					if (node.Walkable && map.Add (node)) {
						list.Add (node);
						stack.Push (node);
					}
				};
			} else {
				callback = delegate (GraphNode node) {
					if (node.Walkable && ((tagMask >> (int)node.Tag) & 0x1) != 0 && map.Add (node)) {
						list.Add (node);
						stack.Push (node);
					}
				};
			}
			
			callback (seed);
			
			while (stack.Count > 0) {
				stack.Pop ().GetConnections (callback);
			}
			
			Pathfinding.Util.StackPool<GraphNode>.Release (stack);
			
			return list;
		}
		
		/** Returns all nodes within a given node-distance from the seed node.
		 * This function performs a BFS (breadth-first-search) or flood fill of the graph and returns all nodes within a specified node distance which can be reached from
		 * the seed node. In almost all cases when \a depth is large enough this will be identical to returning all nodes which have the same area as the seed node.
		 * In the editor areas are displayed as different colors of the nodes.
		 * The only case where it will not be so is when there is a one way path from some part of the area to the seed node
		 * but no path from the seed node to that part of the graph.
		 * 
		 * The returned list is sorted by node distance from the seed node
		 * i.e distance is measured in the number of nodes the shortest path from \a seed to that node would pass through.
		 * Note that the distance measurement does not take heuristics, penalties or tag penalties.
		 * 
		 * Depending on the number of nodes, this function can take quite some time to calculate
		 * so don't use it too often or it might affect the framerate of your game.
		 * 
		 * \param seed The node to start the search from.
		 * \param depth The maximum node-distance from the seed node.
		 * \param tagMask Optional mask for tags. This is a bitmask.
		 *
		 * \returns A List<Node> containing all nodes reachable within a specified node distance from the seed node.
		 * For better memory management the returned list should be pooled, see Pathfinding.Util.ListPool
		 */
		public static List<GraphNode> BFS (GraphNode seed, int depth, int tagMask = -1) {
			List<GraphNode> que = Pathfinding.Util.ListPool<GraphNode>.Claim ();
			List<GraphNode> list = Pathfinding.Util.ListPool<GraphNode>.Claim ();

			/** \todo Pool */
			Dictionary<GraphNode,int> map = new Dictionary<GraphNode,int>();

			int currentDist = 0;
			GraphNodeDelegate callback;
			if (tagMask == -1) {
				callback = delegate (GraphNode node) {
					if (node.Walkable && !map.ContainsKey (node)) {
						map.Add (node, currentDist+1);
						list.Add (node);
						que.Add (node);
					}
				};
			} else {
				callback = delegate (GraphNode node) {
					if (node.Walkable && ((tagMask >> (int)node.Tag) & 0x1) != 0 && !map.ContainsKey (node)) {
						map.Add (node, currentDist+1);
						list.Add (node);
						que.Add (node);
					}
				};
			}

			map[seed] = currentDist;
			callback (seed);


			while (que.Count > 0 && currentDist < depth ) {
				GraphNode n = que[que.Count-1];
				currentDist = map[n];
				que.RemoveAt ( que.Count-1 );
				n.GetConnections (callback);
			}
			
			Pathfinding.Util.ListPool<GraphNode>.Release (que);
			
			return list;
		}

		/** Returns points in a spiral centered around the origin with a minimum clearance from other points.
		 * The points are laid out on the involute of a circle
		 * \see http://en.wikipedia.org/wiki/Involute
		 * Which has some nice properties.
		 * All points are separated by \a clearance world units.
		 * This method is O(n), yes if you read the code you will see a binary search, but that binary search
		 * has an upper bound on the number of steps, so it does not yield a log factor.
		 * 
		 * \note Consider recycling the list after usage to reduce allocations.
		 * \see Pathfinding.Util.ListPool
		 */
		public static List<Vector3> GetSpiralPoints (int count, float clearance) {
			
			List<Vector3> pts = Pathfinding.Util.ListPool<Vector3>.Claim(count);
			
			// The radius of the smaller circle used for generating the involute of a circle
			// Calculated from the separation distance between the turns
			float a = clearance/(2*Mathf.PI);
			float t = 0;
			
			
			pts.Add (InvoluteOfCircle(a, t));
			
			for (int i=0;i<count;i++) {
				Vector3 prev = pts[pts.Count-1];
				
				// d = -t0/2 + sqrt( t0^2/4 + 2d/a )
				// Minimum angle (radians) which would create an arc distance greater than clearance
				float d = -t/2 + Mathf.Sqrt (t*t/4 + 2*clearance/a);
				
				// Binary search for separating this point and the previous one
				float mn = t + d;
				float mx = t + 2*d;
				while (mx - mn > 0.01f) {
					float mid = (mn + mx)/2;
					Vector3 p = InvoluteOfCircle (a, mid);
					if ((p - prev).sqrMagnitude < clearance*clearance) {
						mn = mid;
					} else {
						mx = mid;
					}
				}
				
				pts.Add ( InvoluteOfCircle (a, mx) );
				t = mx;
			}
			
			return pts;
		}
		
		/** Returns the XZ coordinate of the involute of circle.
		 * \see http://en.wikipedia.org/wiki/Involute
		 */
		private static Vector3 InvoluteOfCircle (float a, float t) {
			return new Vector3(a*(Mathf.Cos(t) + t*Mathf.Sin(t)), 0, a*(Mathf.Sin(t) - t*Mathf.Cos(t)));
		}
		
		/** Will calculate a number of points around \a p which are on the graph and are separated by \a clearance from each other.
		 * This is like GetPointsAroundPoint except that \a previousPoints are treated as being in world space.
		 * The average of the points will be found and then that will be treated as the group center.
		 */
		public static void GetPointsAroundPointWorld (Vector3 p, IRaycastableGraph g, List<Vector3> previousPoints, float radius, float clearanceRadius) {
			if ( previousPoints.Count == 0 ) return;

			Vector3 avg = Vector3.zero;
			for ( int i = 0; i < previousPoints.Count; i++ ) avg += previousPoints[i];
			avg /= previousPoints.Count;

			for ( int i = 0; i < previousPoints.Count; i++ ) previousPoints[i] -= avg;

			GetPointsAroundPoint ( p, g, previousPoints, radius, clearanceRadius );
		}

		/** Will calculate a number of points around \a p which are on the graph and are separated by \a clearance from each other.
		 * The maximum distance from \a p to any point will be \a radius.
		 * Points will first be tried to be laid out as \a previousPoints and if that fails, random points will be selected.
		 * This is great if you want to pick a number of target points for group movement. If you pass all current agent points from e.g the group's average position
		 * this method will return target points so that the units move very little within the group, this is often aesthetically pleasing and reduces jitter if using
		 * some kind of local avoidance.
		 * 
		 * \param g The graph to use for linecasting. If you are only using one graph, you can get this by AstarPath.active.graphs[0] as IRaycastableGraph.
		 * Note that not all graphs are raycastable, recast, navmesh and grid graphs are raycastable. On recast and navmesh it works the best.
		 * \param previousPoints The points to use for reference. Note that these should not be in world space. They are treated as relative to \a p.
		 */
		public static void GetPointsAroundPoint (Vector3 p, IRaycastableGraph g, List<Vector3> previousPoints, float radius, float clearanceRadius) {
			
			if (g == null) throw new System.ArgumentNullException ("g");
			
			NavGraph graph = g as NavGraph;
			
			if (graph == null) throw new System.ArgumentException ("g is not a NavGraph");
			
			NNInfo nn = graph.GetNearestForce (p, NNConstraint.Default);
			p = nn.clampedPosition;
			
			if (nn.node == null) {
				// No valid point to start from
				return;
			}
			
			
			// Make sure the enclosing circle has a radius which can pack circles with packing density 0.5
			radius = Mathf.Max (radius, 1.4142f*clearanceRadius*Mathf.Sqrt(previousPoints.Count));//Mathf.Sqrt(previousPoints.Count*clearanceRadius*2));
			clearanceRadius *= clearanceRadius;
			
			for (int i=0;i<previousPoints.Count;i++) {
				
				Vector3 dir = previousPoints[i];
				float magn = dir.magnitude;
				
				if (magn > 0) dir /= magn;
			
				float newMagn = radius;//magn > radius ? radius : magn;
				dir *= newMagn;
				
				bool worked = false;
				
				GraphHitInfo hit;
				
				int tests = 0;
				do {
					
					Vector3 pt = p + dir;

					if (g.Linecast (p, pt, nn.node, out hit)) {
						pt = hit.point;
					}
					
					for (float q = 0.1f; q <= 1.0f; q+= 0.05f) {
						Vector3 qt = (pt - p)*q + p;
						worked = true;
						for (int j=0;j<i;j++) {
							if ((previousPoints[j] - qt).sqrMagnitude < clearanceRadius) {
								worked = false;
								break;
							}
						}
						
						if (worked) {
							previousPoints[i] = qt;
							break;
						}
					}
					
					if (!worked) {

						// Abort after 5 tries
						if (tests > 8) {
							worked = true;
						} else {
							clearanceRadius *= 0.9f;
							// This will pick points in 2D closer to the edge of the circle with a higher probability
							dir = Random.onUnitSphere * Mathf.Lerp (newMagn, radius, tests / 5);
							dir.y = 0;
							tests++;
						}
					}
				} while (!worked);
			}
			
		}
		
		/** Returns randomly selected points on the specified nodes with each point being separated by \a clearanceRadius from each other.
		 * Selecting points ON the nodes only works for TriangleMeshNode (used by Recast Graph and Navmesh Graph) and GridNode (used by GridGraph).
		 * For other node types, only the positions of the nodes will be used.
		 * 
		 * clearanceRadius will be reduced if no valid points can be found.
		 */
		public static List<Vector3> GetPointsOnNodes (List<GraphNode> nodes, int count, float clearanceRadius = 0) {
			
			if (nodes == null) throw new System.ArgumentNullException ("nodes");
			if (nodes.Count == 0) throw new System.ArgumentException ("no nodes passed");
			
			System.Random rnd = new System.Random();
			
			List<Vector3> pts = Pathfinding.Util.ListPool<Vector3>.Claim(count);
			
			// Square
			clearanceRadius *= clearanceRadius;
			
			if (nodes[0] is TriangleMeshNode || nodes[0] is GridNode) {
				//Assume all nodes are triangle nodes or grid nodes
				
				List<float> accs = Pathfinding.Util.ListPool<float>.Claim(nodes.Count);
					
				float tot = 0;
				
				for (int i=0;i<nodes.Count;i++) {
					TriangleMeshNode tnode = nodes[i] as TriangleMeshNode;
					if (tnode != null) {
						float a = System.Math.Abs(Polygon.TriangleArea(tnode.GetVertex(0), tnode.GetVertex(1), tnode.GetVertex(2)));
						tot += a;
						accs.Add (tot);
					}
					 else {
						GridNode gnode = nodes[i] as GridNode;
						
						if (gnode != null) {
							GridGraph gg = GridNode.GetGridGraph (gnode.GraphIndex);
							float a = gg.nodeSize*gg.nodeSize;
							tot += a;
							accs.Add (tot);
						} else {
							accs.Add(tot);
						}
					}
				}
				
				for (int i=0;i<count;i++) {
					
					//Pick point
					int testCount = 0;
					int testLimit = 10;
					bool worked = false;
					
					while (!worked) {
						worked = true;
						
						//If no valid points can be found, progressively lower the clearance radius until such a point is found
						if (testCount >= testLimit) {
							clearanceRadius *= 0.8f;
							testLimit += 10;
							if (testLimit > 100) clearanceRadius = 0;
						}
					
						float tg = (float)rnd.NextDouble()*tot;
						int v = accs.BinarySearch(tg);
						if (v < 0) v = ~v;
						
						if (v >= nodes.Count) {
							// This shouldn't happen, due to NextDouble being smaller than 1... but I don't trust floating point arithmetic.
							worked = false;
							continue;
						}
						
						TriangleMeshNode node = nodes[v] as TriangleMeshNode;
						
						Vector3 p;
						
						if (node != null) {
							// Find a random point inside the triangle
							float v1;
							float v2;
							do {
								v1 = (float)rnd.NextDouble();
								v2 = (float)rnd.NextDouble();
							} while (v1+v2 > 1);
							
							p = ((Vector3)(node.GetVertex(1)-node.GetVertex(0)))*v1 + ((Vector3)(node.GetVertex(2)-node.GetVertex(0)))*v2 + (Vector3)node.GetVertex(0);
						} else {
							GridNode gnode = nodes[v] as GridNode;
							
							if (gnode != null) {
								GridGraph gg = GridNode.GetGridGraph (gnode.GraphIndex);
								
								float v1 = (float)rnd.NextDouble();
								float v2 = (float)rnd.NextDouble();
								p = (Vector3)gnode.position + new Vector3(v1 - 0.5f, 0, v2 - 0.5f) * gg.nodeSize;
							} else
							{
								//Point nodes have no area, so we break directly instead
								pts.Add ((Vector3)nodes[v].position);
								break;
							}
						}
						
						// Test if it is some distance away from the other points
						if (clearanceRadius > 0) {
							for (int j=0;j<pts.Count;j++) {
								if ((pts[j]-p).sqrMagnitude < clearanceRadius) {
									worked = false;
									break;
								}
							}
						}
						
						if (worked) {
							pts.Add (p);
							break;
						} else {
							testCount++;
						}
					}
				}
				
				Pathfinding.Util.ListPool<float>.Release(accs);
				
			} else {
				for (int i=0;i<count;i++) {
					pts.Add ((Vector3)nodes[rnd.Next (nodes.Count)].position);
				}
			}
			
			return pts;
		}
	}
}

