using UnityEngine;
using System.Collections.Generic;

namespace Pathfinding {
	using Pathfinding.Util;

	/// <summary>
	/// Contains utility methods for getting useful information out of graph.
	/// This class works a lot with the <see cref="Pathfinding.GraphNode"/> class, a useful function to get nodes is <see cref="AstarPath.GetNearest"/>.
	///
	/// See: <see cref="AstarPath.GetNearest"/>
	/// See: <see cref="Pathfinding.GraphUpdateUtilities"/>
	/// See: <see cref="Pathfinding.PathUtilities"/>
	///
	/// \ingroup utils
	/// </summary>
	public static class GraphUtilities {
		/// <summary>
		/// Convenience method to get a list of all segments of the contours of a graph.
		/// Returns: A list of segments. Every 2 elements form a line segment. The first segment is (result[0], result[1]), the second one is (result[2], result[3]) etc.
		/// The line segments are oriented so that the navmesh is on the right side of the segments when seen from above.
		///
		/// This method works for navmesh, recast, grid graphs and layered grid graphs. For other graph types it will return an empty list.
		///
		/// If you need more information about how the contours are connected you can take a look at the other variants of this method.
		///
		/// <code>
		/// // Get the first graph
		/// var navmesh = AstarPath.active.graphs[0];
		///
		/// // Get all contours of the graph (works for grid, navmesh and recast graphs)
		/// var segments = GraphUtilities.GetContours(navmesh);
		///
		/// // Every 2 elements form a line segment. The first segment is (segments[0], segments[1]), the second one is (segments[2], segments[3]) etc.
		/// // The line segments are oriented so that the navmesh is on the right side of the segments when seen from above.
		/// for (int i = 0; i < segments.Count; i += 2) {
		///     var start = segments[i];
		///     var end = segments[i+1];
		///     Debug.DrawLine(start, end, Color.red, 3);
		/// }
		/// </code>
		///
		/// [Open online documentation to see images]
		/// [Open online documentation to see images]
		/// </summary>
		public static List<Vector3> GetContours (NavGraph graph) {
			List<Vector3> result = ListPool<Vector3>.Claim ();

			if (graph is INavmesh) {
				GetContours(graph as INavmesh, (vertices, cycle) => {
					for (int j = cycle ? vertices.Count - 1 : 0, i = 0; i < vertices.Count; j = i, i++) {
						result.Add((Vector3)vertices[j]);
						result.Add((Vector3)vertices[i]);
					}
				});
#if !ASTAR_NO_GRID_GRAPH
			} else if (graph is GridGraph) {
				GetContours(graph as GridGraph, vertices => {
					for (int j = vertices.Length - 1, i = 0; i < vertices.Length; j = i, i++) {
						result.Add((Vector3)vertices[j]);
						result.Add((Vector3)vertices[i]);
					}
				}, 0);
#endif
			}
			return result;
		}

		/// <summary>
		/// Traces the contour of a navmesh.
		///
		/// [Open online documentation to see images]
		///
		/// This image is just used to illustrate the difference between chains and cycles. That it shows a grid graph is not relevant.
		/// [Open online documentation to see images]
		///
		/// See: <see cref="GetContours(NavGraph)"/>
		/// </summary>
		/// <param name="navmesh">The navmesh-like object to trace. This can be a recast or navmesh graph or it could be a single tile in one such graph.</param>
		/// <param name="results">Will be called once for each contour with the contour as a parameter as well as a boolean indicating if the contour is a cycle or a chain (see second image).</param>
		public static void GetContours (INavmesh navmesh, System.Action<List<Int3>, bool> results) {
			// Assume 3 vertices per node
			var uses = new bool[3];

			var outline = new Dictionary<int, int>();
			var vertexPositions = new Dictionary<int, Int3>();
			var hasInEdge = new HashSet<int>();

			navmesh.GetNodes(_node => {
				var node = _node as TriangleMeshNode;

				uses[0] = uses[1] = uses[2] = false;

				if (node != null) {
				    // Find out which edges are shared with other nodes
					for (int j = 0; j < node.connections.Length; j++) {
						var other = node.connections[j].node as TriangleMeshNode;

				        // Not necessarily a TriangleMeshNode
						if (other != null) {
							int a = node.SharedEdge(other);
							if (a != -1) uses[a] = true;
						}
					}

				    // Loop through all edges on the node
					for (int j = 0; j < 3; j++) {
				        // The edge is not shared with any other node
				        // I.e it is an exterior edge on the mesh
						if (!uses[j]) {
							var i1 = j;
							var i2 = (j+1) % node.GetVertexCount();

							outline[node.GetVertexIndex(i1)] = node.GetVertexIndex(i2);
							hasInEdge.Add(node.GetVertexIndex(i2));
							vertexPositions[node.GetVertexIndex(i1)] = node.GetVertex(i1);
							vertexPositions[node.GetVertexIndex(i2)] = node.GetVertex(i2);
						}
					}
				}
			});

			Polygon.TraceContours(outline, hasInEdge, (chain, cycle) => {
				List<Int3> vertices = ListPool<Int3>.Claim();
				for (int i = 0; i < chain.Count; i++) vertices.Add(vertexPositions[chain[i]]);
				results(vertices, cycle);
			});
		}

#if !ASTAR_NO_GRID_GRAPH
		/// <summary>
		/// Finds all contours of a collection of nodes in a grid graph.
		///
		/// <code>
		/// var grid = AstarPath.active.data.gridGraph;
		///
		/// // Find all contours in the graph and draw them using debug lines
		/// GraphUtilities.GetContours(grid, vertices => {
		///     for (int i = 0; i < vertices.Length; i++) {
		///         Debug.DrawLine(vertices[i], vertices[(i+1)%vertices.Length], Color.red, 4);
		///     }
		/// }, 0);
		/// </code>
		///
		/// In the image below you can see the contour of a graph.
		/// [Open online documentation to see images]
		///
		/// In the image below you can see the contour of just a part of a grid graph (when the nodes parameter is supplied)
		/// [Open online documentation to see images]
		///
		/// Contour of a hexagon graph
		/// [Open online documentation to see images]
		///
		/// See: <see cref="GetContours(NavGraph)"/>
		/// </summary>
		/// <param name="grid">The grid to find the contours of</param>
		/// <param name="callback">The callback will be called once for every contour that is found with the vertices of the contour. The contour always forms a cycle.</param>
		/// <param name="yMergeThreshold">Contours will be simplified if the y coordinates for adjacent vertices differ by no more than this value.</param>
		/// <param name="nodes">Only these nodes will be searched. If this parameter is null then all nodes in the grid graph will be searched.</param>
		public static void GetContours (GridGraph grid, System.Action<Vector3[]> callback, float yMergeThreshold, GridNodeBase[] nodes = null) {
			// Set of all allowed nodes or null if all nodes are allowed
			HashSet<GridNodeBase> nodeSet = nodes != null ? new HashSet<GridNodeBase>(nodes) : null;

			// Use all nodes if the nodes parameter is null
			nodes = nodes ?? grid.nodes;
			int[] neighbourXOffsets = grid.neighbourXOffsets;
			int[] neighbourZOffsets = grid.neighbourZOffsets;
			var neighbourIndices = grid.neighbours == NumNeighbours.Six ? GridGraph.hexagonNeighbourIndices : new [] { 0, 1, 2, 3 };
			var offsetMultiplier = grid.neighbours == NumNeighbours.Six ? 1/3f : 0.5f;

			if (nodes != null) {
				var trace = ListPool<Vector3>.Claim ();
				var seenStates = new HashSet<int>();

				for (int i = 0; i < nodes.Length; i++) {
					var startNode = nodes[i];
					// The third check is a fast check for if the node has connections in all grid directions, if it has then we can skip processing it (unless the nodes parameter was used in which case we have to handle the edge cases)
					if (startNode != null && startNode.Walkable && (!startNode.HasConnectionsToAllEightNeighbours || nodeSet != null)) {
						for (int startDir = 0; startDir < neighbourIndices.Length; startDir++) {
							int startState = (startNode.NodeIndex << 4) | startDir;

							// Check if there is an obstacle in that direction
							var startNeighbour = startNode.GetNeighbourAlongDirection(neighbourIndices[startDir]);
							if ((startNeighbour == null || (nodeSet != null && !nodeSet.Contains(startNeighbour))) && !seenStates.Contains(startState)) {
								// Start tracing a contour here
								trace.ClearFast();
								int dir = startDir;
								GridNodeBase node = startNode;

								while (true) {
									int state = (node.NodeIndex << 4) | dir;
									if (state == startState && trace.Count > 0) {
										break;
									}

									seenStates.Add(state);

									var neighbour = node.GetNeighbourAlongDirection(neighbourIndices[dir]);
									if (neighbour == null || (nodeSet != null && !nodeSet.Contains(neighbour))) {
										// Draw edge
										var d0 = neighbourIndices[dir];
										dir = (dir + 1) % neighbourIndices.Length;
										var d1 = neighbourIndices[dir];

										// Position in graph space of the vertex
										Vector3 graphSpacePos = new Vector3(node.XCoordinateInGrid + 0.5f, 0, node.ZCoordinateInGrid + 0.5f);
										// Offset along diagonal to get the correct XZ coordinates
										graphSpacePos.x += (neighbourXOffsets[d0] + neighbourXOffsets[d1]) * offsetMultiplier;
										graphSpacePos.z += (neighbourZOffsets[d0] + neighbourZOffsets[d1]) * offsetMultiplier;
										graphSpacePos.y = grid.transform.InverseTransform((Vector3)node.position).y;

										if (trace.Count >= 2) {
											var v0 = trace[trace.Count-2];
											var v1 = trace[trace.Count-1];
											var v1d = v1 - v0;
											var v2d = graphSpacePos - v0;
											// Replace the previous point if it is colinear with the point just before it and just after it (the current point), because that point wouldn't add much information, but it would add CPU overhead
											if (((Mathf.Abs(v1d.x) > 0.01f || Mathf.Abs(v2d.x) > 0.01f) && (Mathf.Abs(v1d.z) > 0.01f || Mathf.Abs(v2d.z) > 0.01f)) || (Mathf.Abs(v1d.y) > yMergeThreshold || Mathf.Abs(v2d.y) > yMergeThreshold)) {
												trace.Add(graphSpacePos);
											} else {
												trace[trace.Count-1] = graphSpacePos;
											}
										} else {
											trace.Add(graphSpacePos);
										}
									} else {
										// Move
										node = neighbour;
										dir = (dir + neighbourIndices.Length/2 + 1) % neighbourIndices.Length;
									}
								}

								// Simplify the contour a bit around the start point.
								// Otherwise we might return a cycle which was not as simplified as possible and the number of vertices
								// would depend on where in the cycle the algorithm started to traverse the contour.
								if (trace.Count >= 3) {
									var v0 = trace[trace.Count-2];
									var v1 = trace[trace.Count-1];
									var v1d = v1 - v0;
									var v2d = trace[0] - v0;
									// Replace the previous point if it is colinear with the point just before it and just after it (the current point), because that point wouldn't add much information, but it would add CPU overhead
									if (!(((Mathf.Abs(v1d.x) > 0.01f || Mathf.Abs(v2d.x) > 0.01f) && (Mathf.Abs(v1d.z) > 0.01f || Mathf.Abs(v2d.z) > 0.01f)) || (Mathf.Abs(v1d.y) > yMergeThreshold || Mathf.Abs(v2d.y) > yMergeThreshold))) {
										trace.RemoveAt(trace.Count - 1);
									}
								}

								if (trace.Count >= 3) {
									var v0 = trace[trace.Count-1];
									var v1 = trace[0];
									var v1d = v1 - v0;
									var v2d = trace[1] - v0;
									// Replace the previous point if it is colinear with the point just before it and just after it (the current point), because that point wouldn't add much information, but it would add CPU overhead
									if (!(((Mathf.Abs(v1d.x) > 0.01f || Mathf.Abs(v2d.x) > 0.01f) && (Mathf.Abs(v1d.z) > 0.01f || Mathf.Abs(v2d.z) > 0.01f)) || (Mathf.Abs(v1d.y) > yMergeThreshold || Mathf.Abs(v2d.y) > yMergeThreshold))) {
										trace.RemoveAt(0);
									}
								}
								var result = trace.ToArray();
								grid.transform.Transform(result);
								callback(result);
							}
						}
					}
				}

				ListPool<Vector3>.Release (ref trace);
			}
		}
#endif
	}
}
