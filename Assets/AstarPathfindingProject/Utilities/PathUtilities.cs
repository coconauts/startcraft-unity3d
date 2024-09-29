using Pathfinding.Util;
using System.Collections.Generic;
using UnityEngine;

namespace Pathfinding {
	/// <summary>
	/// Contains useful functions for working with paths and nodes.
	/// This class works a lot with the <see cref="Pathfinding.GraphNode"/> class, a useful function to get nodes is AstarPath.GetNearest.
	/// See: <see cref="AstarPath.GetNearest"/>
	/// See: <see cref="Pathfinding.GraphUpdateUtilities"/>
	/// See: <see cref="Pathfinding.GraphUtilities"/>
	/// \ingroup utils
	/// </summary>
	public static class PathUtilities {
		/// <summary>
		/// Returns if there is a walkable path from node1 to node2.
		/// This method is extremely fast because it only uses precalculated information.
		///
		/// <code>
		/// GraphNode node1 = AstarPath.active.GetNearest(point1, NNConstraint.Default).node;
		/// GraphNode node2 = AstarPath.active.GetNearest(point2, NNConstraint.Default).node;
		///
		/// if (PathUtilities.IsPathPossible(node1, node2)) {
		///     // Yay, there is a path between those two nodes
		/// }
		/// </code>
		///
		/// See: graph-updates (view in online documentation for working links)
		/// See: <see cref="AstarPath.GetNearest"/>
		/// </summary>
		public static bool IsPathPossible (GraphNode node1, GraphNode node2) {
			return node1.Walkable && node2.Walkable && node1.Area == node2.Area;
		}

		/// <summary>
		/// Returns if there are walkable paths between all nodes.
		///
		/// See: graph-updates (view in online documentation for working links)
		///
		/// Returns true for empty lists.
		///
		/// See: <see cref="AstarPath.GetNearest"/>
		/// </summary>
		public static bool IsPathPossible (List<GraphNode> nodes) {
			if (nodes.Count == 0) return true;

			uint area = nodes[0].Area;
			for (int i = 0; i < nodes.Count; i++) if (!nodes[i].Walkable || nodes[i].Area != area) return false;
			return true;
		}

		/// <summary>
		/// Returns if there are walkable paths between all nodes.
		/// See: graph-updates (view in online documentation for working links)
		///
		/// This method will actually only check if the first node can reach all other nodes. However this is
		/// equivalent in 99% of the cases since almost always the graph connections are bidirectional.
		/// If you are not aware of any cases where you explicitly create unidirectional connections
		/// this method can be used without worries.
		///
		/// Returns true for empty lists
		///
		/// Warning: This method is significantly slower than the IsPathPossible method which does not take a tagMask
		///
		/// See: <see cref="AstarPath.GetNearest"/>
		/// </summary>
		public static bool IsPathPossible (List<GraphNode> nodes, int tagMask) {
			if (nodes.Count == 0) return true;

			// Make sure that the first node has a valid tag
			if (((tagMask >> (int)nodes[0].Tag) & 1) == 0) return false;

			// Fast check first
			if (!IsPathPossible(nodes)) return false;

			// Make sure that the first node can reach all other nodes
			var reachable = GetReachableNodes(nodes[0], tagMask);
			bool result = true;

			// Make sure that the first node can reach all other nodes
			for (int i = 1; i < nodes.Count; i++) {
				if (!reachable.Contains(nodes[i])) {
					result = false;
					break;
				}
			}

			// Pool the temporary list
			ListPool<GraphNode>.Release (ref reachable);

			return result;
		}

		/// <summary>
		/// Returns all nodes reachable from the seed node.
		/// This function performs a DFS (depth-first-search) or flood fill of the graph and returns all nodes which can be reached from
		/// the seed node. In almost all cases this will be identical to returning all nodes which have the same area as the seed node.
		/// In the editor areas are displayed as different colors of the nodes.
		/// The only case where it will not be so is when there is a one way path from some part of the area to the seed node
		/// but no path from the seed node to that part of the graph.
		///
		/// The returned list is not sorted in any particular way.
		///
		/// Depending on the number of reachable nodes, this function can take quite some time to calculate
		/// so don't use it too often or it might affect the framerate of your game.
		///
		/// See: bitmasks (view in online documentation for working links).
		///
		/// Returns: A List<Node> containing all nodes reachable from the seed node.
		/// For better memory management the returned list should be pooled, see Pathfinding.Util.ListPool.
		/// </summary>
		/// <param name="seed">The node to start the search from.</param>
		/// <param name="tagMask">Optional mask for tags. This is a bitmask.</param>
		/// <param name="filter">Optional filter for which nodes to search. You can combine this with tagMask = -1 to make the filter determine everything.
		///      Only walkable nodes are searched regardless of the filter. If the filter function returns false the node will be treated as unwalkable.</param>
		public static List<GraphNode> GetReachableNodes (GraphNode seed, int tagMask = -1, System.Func<GraphNode, bool> filter = null) {
			Stack<GraphNode> dfsStack = StackPool<GraphNode>.Claim ();
			List<GraphNode> reachable = ListPool<GraphNode>.Claim ();

			/// <summary>TODO: Pool</summary>
			var map = new HashSet<GraphNode>();

			System.Action<GraphNode> callback;
			// Check if we can use the fast path
			if (tagMask == -1 && filter == null) {
				callback = (GraphNode node) => {
					if (node.Walkable && map.Add(node)) {
						reachable.Add(node);
						dfsStack.Push(node);
					}
				};
			} else {
				callback = (GraphNode node) => {
					if (node.Walkable && ((tagMask >> (int)node.Tag) & 0x1) != 0 && map.Add(node)) {
						if (filter != null && !filter(node)) return;

						reachable.Add(node);
						dfsStack.Push(node);
					}
				};
			}

			callback(seed);

			while (dfsStack.Count > 0) {
				dfsStack.Pop().GetConnections(callback);
			}

			StackPool<GraphNode>.Release (dfsStack);
			return reachable;
		}

		static Queue<GraphNode> BFSQueue;
		static Dictionary<GraphNode, int> BFSMap;

		/// <summary>
		/// Returns all nodes up to a given node-distance from the seed node.
		/// This function performs a BFS (<a href="https://en.wikipedia.org/wiki/Breadth-first_search">breadth-first search</a>) or flood fill of the graph and returns all nodes within a specified node distance which can be reached from
		/// the seed node. In almost all cases when depth is large enough this will be identical to returning all nodes which have the same area as the seed node.
		/// In the editor areas are displayed as different colors of the nodes.
		/// The only case where it will not be so is when there is a one way path from some part of the area to the seed node
		/// but no path from the seed node to that part of the graph.
		///
		/// The returned list is sorted by node distance from the seed node
		/// i.e distance is measured in the number of nodes the shortest path from seed to that node would pass through.
		/// Note that the distance measurement does not take heuristics, penalties or tag penalties.
		///
		/// Depending on the number of nodes, this function can take quite some time to calculate
		/// so don't use it too often or it might affect the framerate of your game.
		///
		/// Returns: A List<GraphNode> containing all nodes reachable up to a specified node distance from the seed node.
		/// For better memory management the returned list should be pooled, see Pathfinding.Util.ListPool
		///
		/// Warning: This method is not thread safe. Only use it from the Unity thread (i.e normal game code).
		///
		/// The video below shows the BFS result with varying values of depth. Points are sampled on the nodes using <see cref="GetPointsOnNodes"/>.
		/// [Open online documentation to see videos]
		/// </summary>
		/// <param name="seed">The node to start the search from.</param>
		/// <param name="depth">The maximum node-distance from the seed node.</param>
		/// <param name="tagMask">Optional mask for tags. This is a bitmask.</param>
		/// <param name="filter">Optional filter for which nodes to search. You can combine this with depth = int.MaxValue and tagMask = -1 to make the filter determine everything.
		///      Only walkable nodes are searched regardless of the filter. If the filter function returns false the node will be treated as unwalkable.</param>
		public static List<GraphNode> BFS (GraphNode seed, int depth, int tagMask = -1, System.Func<GraphNode, bool> filter = null) {
#if ASTAR_PROFILE
			System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
			watch.Start();
#endif

			BFSQueue = BFSQueue ?? new Queue<GraphNode>();
			var que = BFSQueue;

			BFSMap = BFSMap ?? new Dictionary<GraphNode, int>();
			var map = BFSMap;

			// Even though we clear at the end of this function, it is good to
			// do it here as well in case the previous invocation of the method
			// threw an exception for some reason
			// and didn't clear the que and map
			que.Clear();
			map.Clear();

			List<GraphNode> result = ListPool<GraphNode>.Claim ();

			int currentDist = -1;
			System.Action<GraphNode> callback;
			if (tagMask == -1) {
				callback = node => {
					if (node.Walkable && !map.ContainsKey(node)) {
						if (filter != null && !filter(node)) return;

						map.Add(node, currentDist+1);
						result.Add(node);
						que.Enqueue(node);
					}
				};
			} else {
				callback = node => {
					if (node.Walkable && ((tagMask >> (int)node.Tag) & 0x1) != 0 && !map.ContainsKey(node)) {
						if (filter != null && !filter(node)) return;

						map.Add(node, currentDist+1);
						result.Add(node);
						que.Enqueue(node);
					}
				};
			}

			callback(seed);

			while (que.Count > 0) {
				GraphNode n = que.Dequeue();
				currentDist = map[n];

				if (currentDist >= depth) break;

				n.GetConnections(callback);
			}

			que.Clear();
			map.Clear();

#if ASTAR_PROFILE
			watch.Stop();
			Debug.Log((1000*watch.Elapsed.TotalSeconds).ToString("0.0 ms"));
#endif
			return result;
		}

		/// <summary>
		/// Returns points in a spiral centered around the origin with a minimum clearance from other points.
		/// The points are laid out on the involute of a circle
		/// See: http://en.wikipedia.org/wiki/Involute
		/// Which has some nice properties.
		/// All points are separated by clearance world units.
		/// This method is O(n), yes if you read the code you will see a binary search, but that binary search
		/// has an upper bound on the number of steps, so it does not yield a log factor.
		///
		/// Note: Consider recycling the list after usage to reduce allocations.
		/// See: Pathfinding.Util.ListPool
		/// </summary>
		public static List<Vector3> GetSpiralPoints (int count, float clearance) {
			List<Vector3> pts = ListPool<Vector3>.Claim (count);

			// The radius of the smaller circle used for generating the involute of a circle
			// Calculated from the separation distance between the turns
			float a = clearance/(2*Mathf.PI);
			float t = 0;


			pts.Add(InvoluteOfCircle(a, t));

			for (int i = 0; i < count; i++) {
				Vector3 prev = pts[pts.Count-1];

				// d = -t0/2 + sqrt( t0^2/4 + 2d/a )
				// Minimum angle (radians) which would create an arc distance greater than clearance
				float d = -t/2 + Mathf.Sqrt(t*t/4 + 2*clearance/a);

				// Binary search for separating this point and the previous one
				float mn = t + d;
				float mx = t + 2*d;
				while (mx - mn > 0.01f) {
					float mid = (mn + mx)/2;
					Vector3 p = InvoluteOfCircle(a, mid);
					if ((p - prev).sqrMagnitude < clearance*clearance) {
						mn = mid;
					} else {
						mx = mid;
					}
				}

				pts.Add(InvoluteOfCircle(a, mx));
				t = mx;
			}

			return pts;
		}

		/// <summary>
		/// Returns the XZ coordinate of the involute of circle.
		/// See: http://en.wikipedia.org/wiki/Involute
		/// </summary>
		private static Vector3 InvoluteOfCircle (float a, float t) {
			return new Vector3(a*(Mathf.Cos(t) + t*Mathf.Sin(t)), 0, a*(Mathf.Sin(t) - t*Mathf.Cos(t)));
		}

		/// <summary>
		/// Will calculate a number of points around p which are on the graph and are separated by clearance from each other.
		/// This is like GetPointsAroundPoint except that previousPoints are treated as being in world space.
		/// The average of the points will be found and then that will be treated as the group center.
		/// </summary>
		/// <param name="p">The point to generate points around</param>
		/// <param name="g">The graph to use for linecasting. If you are only using one graph, you can get this by AstarPath.active.graphs[0] as IRaycastableGraph.
		/// Note that not all graphs are raycastable, recast, navmesh and grid graphs are raycastable. On recast and navmesh it works the best.</param>
		/// <param name="previousPoints">The points to use for reference. Note that these are in world space.
		///      The new points will overwrite the existing points in the list. The result will be in world space.</param>
		/// <param name="radius">The final points will be at most this distance from p.</param>
		/// <param name="clearanceRadius">The points will if possible be at least this distance from each other.</param>
		public static void GetPointsAroundPointWorld (Vector3 p, IRaycastableGraph g, List<Vector3> previousPoints, float radius, float clearanceRadius) {
			if (previousPoints.Count == 0) return;

			Vector3 avg = Vector3.zero;
			for (int i = 0; i < previousPoints.Count; i++) avg += previousPoints[i];
			avg /= previousPoints.Count;

			for (int i = 0; i < previousPoints.Count; i++) previousPoints[i] -= avg;

			GetPointsAroundPoint(p, g, previousPoints, radius, clearanceRadius);
		}

		/// <summary>
		/// Will calculate a number of points around center which are on the graph and are separated by clearance from each other.
		/// The maximum distance from center to any point will be radius.
		/// Points will first be tried to be laid out as previousPoints and if that fails, random points will be selected.
		/// This is great if you want to pick a number of target points for group movement. If you pass all current agent points from e.g the group's average position
		/// this method will return target points so that the units move very little within the group, this is often aesthetically pleasing and reduces jitter if using
		/// some kind of local avoidance.
		///
		/// TODO: Write unit tests
		/// </summary>
		/// <param name="center">The point to generate points around</param>
		/// <param name="g">The graph to use for linecasting. If you are only using one graph, you can get this by AstarPath.active.graphs[0] as IRaycastableGraph.
		/// Note that not all graphs are raycastable, recast, navmesh and grid graphs are raycastable. On recast and navmesh it works the best.</param>
		/// <param name="previousPoints">The points to use for reference. Note that these should not be in world space. They are treated as relative to center.
		///      The new points will overwrite the existing points in the list. The result will be in world space, not relative to center.</param>
		/// <param name="radius">The final points will be at most this distance from center.</param>
		/// <param name="clearanceRadius">The points will if possible be at least this distance from each other.</param>
		public static void GetPointsAroundPoint (Vector3 center, IRaycastableGraph g, List<Vector3> previousPoints, float radius, float clearanceRadius) {
			if (g == null) throw new System.ArgumentNullException("g");

			var graph = g as NavGraph;

			if (graph == null) throw new System.ArgumentException("g is not a NavGraph");

			NNInfoInternal nn = graph.GetNearestForce(center, NNConstraint.Default);
			center = nn.clampedPosition;

			if (nn.node == null) {
				// No valid point to start from
				return;
			}


			// Make sure the enclosing circle has a radius which can pack circles with packing density 0.5
			radius = Mathf.Max(radius, 1.4142f*clearanceRadius*Mathf.Sqrt(previousPoints.Count)); //Mathf.Sqrt(previousPoints.Count*clearanceRadius*2));
			clearanceRadius *= clearanceRadius;

			for (int i = 0; i < previousPoints.Count; i++) {
				Vector3 dir = previousPoints[i];
				float magn = dir.magnitude;

				if (magn > 0) dir /= magn;

				float newMagn = radius;//magn > radius ? radius : magn;
				dir *= newMagn;

				GraphHitInfo hit;

				int tests = 0;
				while (true) {
					Vector3 pt = center + dir;

					if (g.Linecast(center, pt, nn.node, out hit)) {
						if (hit.point == Vector3.zero) {
							// Oops, linecast actually failed completely
							// try again unless we have tried lots of times
							// then we just continue anyway
							tests++;
							if (tests > 8) {
								previousPoints[i] = pt;
								break;
							}
						} else {
							pt = hit.point;
						}
					}

					bool worked = false;

					for (float q = 0.1f; q <= 1.0f; q += 0.05f) {
						Vector3 qt = Vector3.Lerp(center, pt, q);
						worked = true;
						for (int j = 0; j < i; j++) {
							if ((previousPoints[j] - qt).sqrMagnitude < clearanceRadius) {
								worked = false;
								break;
							}
						}

						// Abort after 8 tests or when we have found a valid point
						if (worked || tests > 8) {
							worked = true;
							previousPoints[i] = qt;
							break;
						}
					}

					// Break out of nested loop
					if (worked) {
						break;
					}

					// If we could not find a valid point, reduce the clearance radius slightly to improve
					// the chances next time
					clearanceRadius *= 0.9f;
					// This will pick points in 2D closer to the edge of the circle with a higher probability
					dir = Random.onUnitSphere * Mathf.Lerp(newMagn, radius, tests / 5);
					dir.y = 0;
					tests++;
				}
			}
		}

		/// <summary>
		/// Returns randomly selected points on the specified nodes with each point being separated by clearanceRadius from each other.
		/// Selecting points ON the nodes only works for TriangleMeshNode (used by Recast Graph and Navmesh Graph) and GridNode (used by GridGraph).
		/// For other node types, only the positions of the nodes will be used.
		///
		/// clearanceRadius will be reduced if no valid points can be found.
		///
		/// Note: This method assumes that the nodes in the list have the same type for some special cases.
		/// More specifically if the first node is not a TriangleMeshNode or a GridNode, it will use a fast path
		/// which assumes that all nodes in the list have the same surface area (which usually is a surface area of zero and the
		/// nodes are all PointNodes).
		/// </summary>
		public static List<Vector3> GetPointsOnNodes (List<GraphNode> nodes, int count, float clearanceRadius = 0) {
			if (nodes == null) throw new System.ArgumentNullException("nodes");
			if (nodes.Count == 0) throw new System.ArgumentException("no nodes passed");

			List<Vector3> pts = ListPool<Vector3>.Claim (count);

			// Square
			clearanceRadius *= clearanceRadius;

			if (clearanceRadius > 0 || nodes[0] is TriangleMeshNode
#if !ASTAR_NO_GRID_GRAPH
				|| nodes[0] is GridNode
#endif
				) {
				// Accumulated area of all nodes
				List<float> accs = ListPool<float>.Claim (nodes.Count);

				// Total area of all nodes so far
				float tot = 0;

				for (int i = 0; i < nodes.Count; i++) {
					var surfaceArea = nodes[i].SurfaceArea();
					// Ensures that even if the nodes have a surface area of 0, a random one will still be picked
					// instead of e.g always picking the first or the last one.
					surfaceArea += 0.001f;
					tot += surfaceArea;
					accs.Add(tot);
				}

				for (int i = 0; i < count; i++) {
					//Pick point
					int testCount = 0;
					int testLimit = 10;
					bool worked = false;

					while (!worked) {
						worked = true;

						// If no valid points could be found, progressively lower the clearance radius until such a point is found
						if (testCount >= testLimit) {
							// Note that clearanceRadius is a squared radius
							clearanceRadius *= 0.9f*0.9f;
							testLimit += 10;
							if (testLimit > 100) clearanceRadius = 0;
						}

						// Pick a random node among the ones in the list weighted by their area
						float tg = Random.value*tot;
						int v = accs.BinarySearch(tg);
						if (v < 0) v = ~v;

						if (v >= nodes.Count) {
							// Cover edge cases
							worked = false;
							continue;
						}

						var node = nodes[v];
						var p = node.RandomPointOnSurface();

						// Test if it is some distance away from the other points
						if (clearanceRadius > 0) {
							for (int j = 0; j < pts.Count; j++) {
								if ((pts[j]-p).sqrMagnitude < clearanceRadius) {
									worked = false;
									break;
								}
							}
						}

						if (worked) {
							pts.Add(p);
							break;
						}
						testCount++;
					}
				}

				ListPool<float>.Release (ref accs);
			} else {
				// Fast path, assumes all nodes have the same area (usually zero)
				for (int i = 0; i < count; i++) {
					pts.Add((Vector3)nodes[Random.Range(0, nodes.Count)].RandomPointOnSurface());
				}
			}

			return pts;
		}
	}
}
