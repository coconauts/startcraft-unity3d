using UnityEngine;
using System.Collections;

namespace Pathfinding {
	/// <summary>
	/// Moves a grid graph to follow a target.
	///
	/// Attach this to some object in the scene and assign the target to e.g the player.
	/// Then the graph will follow that object around as it moves.
	///
	/// This is useful if pathfinding is only necessary in a small region around an object (for example the player).
	/// It makes it possible to have vast open worlds (maybe procedurally generated) and still be able to use pathfinding on them.
	///
	/// When the graph is moved you may notice an fps drop.
	/// If this grows too large you can try a few things:
	/// - Reduce the <see cref="updateDistance"/>. This will make the updates smaller but more frequent.
	///   This only works to some degree however since an update has an inherent overhead.
	/// - Reduce the grid size.
	/// - Turn on multithreading (A* Inspector -> Settings)
	/// - Disable the <see cref="floodFill"/> field. However note the restrictions on when this can be done.
	/// - Disable Height Testing or Collision Testing in the grid graph. This can give a performance boost
	///   since fewer calls to the physics engine need to be done.
	/// - Avoid using any erosion in the grid graph settings. This is relatively slow.
	///
	/// Make sure you have 'Show Graphs' disabled in the A* inspector since gizmos in the scene view can take some
	/// time to update when the graph moves and thus make it seem like this script is slower than it actually is.
	///
	/// See: Take a look at the example scene called "Procedural" for an example of how to use this script
	///
	/// Note: This class does not support the erosion setting on grid graphs. You can instead try to
	///  increase the 'diameter' setting under the Grid Graph Settings -> Collision Testing header to achieve a similar effect.
	///  However even if it did support erosion you would most likely not want to use it with this script
	///  since erosion would increase the number of nodes that had to be updated when the graph moved by a large amount.
	///
	/// Version: Since 3.6.8 this class can handle graph rotation other options such as isometric angle and aspect ratio.
	/// Version: After 3.6.8 this class can also handle layered grid graphs.
	/// </summary>
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_procedural_grid_mover.php")]
	public class ProceduralGridMover : VersionedMonoBehaviour {
		/// <summary>
		/// Graph will be updated if the target is more than this number of nodes from the graph center.
		/// Note that this is in nodes, not world units.
		///
		/// Version: The unit was changed to nodes instead of world units in 3.6.8.
		/// </summary>
		public float updateDistance = 10;

		/// <summary>Graph will be moved to follow this target</summary>
		public Transform target;

		/// <summary>Grid graph to update</summary>
		GridGraph graph;

		/// <summary>Temporary buffer</summary>
		GridNodeBase[] buffer;

		/// <summary>True while the graph is being updated by this script</summary>
		public bool updatingGraph { get; private set; }

		void Start () {
			if (AstarPath.active == null) throw new System.Exception("There is no AstarPath object in the scene");

			graph = AstarPath.active.data.FindGraphWhichInheritsFrom(typeof(GridGraph)) as GridGraph;

			if (graph == null) throw new System.Exception("The AstarPath object has no GridGraph or LayeredGridGraph");
			UpdateGraph();
		}

		/// <summary>Update is called once per frame</summary>
		void Update () {
			if (graph == null) return;

			// Calculate where the graph center and the target position is in graph space
			var graphCenterInGraphSpace = PointToGraphSpace(graph.center);
			var targetPositionInGraphSpace = PointToGraphSpace(target.position);

			// Check the distance in graph space
			// We only care about the X and Z axes since the Y axis is the "height" coordinate of the nodes (in graph space)
			// We only care about the plane that the nodes are placed in
			if (VectorMath.SqrDistanceXZ(graphCenterInGraphSpace, targetPositionInGraphSpace) > updateDistance*updateDistance) {
				UpdateGraph();
			}
		}

		/// <summary>
		/// Transforms a point from world space to graph space.
		/// In graph space, (0,0,0) is bottom left corner of the graph
		/// and one unit along the X and Z axes equals distance between two nodes
		/// the Y axis still uses world units
		/// </summary>
		Vector3 PointToGraphSpace (Vector3 p) {
			// Multiply with the inverse matrix of the graph
			// to get the point in graph space
			return graph.transform.InverseTransform(p);
		}

		/// <summary>
		/// Updates the graph asynchronously.
		/// This will move the graph so that the target's position is the center of the graph.
		/// If the graph is already being updated, the call will be ignored.
		///
		/// The image below shows which nodes will be updated when the graph moves.
		/// The whole graph is not recalculated each time it is moved, but only those
		/// nodes that have to be updated, the rest will keep their old values.
		/// The image is a bit simplified but it shows the main idea.
		/// [Open online documentation to see images]
		///
		/// If you want to move the graph synchronously then call
		/// <code> AstarPath.active.FlushWorkItems(); </code>
		/// Immediately after you have called this method.
		/// </summary>
		public void UpdateGraph () {
			if (updatingGraph) {
				// We are already updating the graph
				// so ignore this call
				return;
			}

			updatingGraph = true;

			// Start a work item for updating the graph
			// This will pause the pathfinding threads
			// so that it is safe to update the graph
			// and then do it over several frames
			// (hence the IEnumerator coroutine)
			// to avoid too large FPS drops
			IEnumerator ie = UpdateGraphCoroutine();
			AstarPath.active.AddWorkItem(new AstarWorkItem(
				(context, force) => {
				// If force is true we need to calculate all steps at once
				if (force) while (ie.MoveNext()) {}

				// Calculate one step. False will be returned when there are no more steps
				bool done;
				try {
					done = !ie.MoveNext();
				} catch (System.Exception e) {
				    // The code MAY throw an exception in rare circumstances if for example the user
				    // changes the width of the graph in the inspector while an update is being performed
				    // at the same time. So lets just fail in that case and retry later.
					Debug.LogException(e, this);
					done = true;
				}

				if (done) {
					updatingGraph = false;
				}
				return done;
			}));
		}

		/// <summary>Async method for moving the graph</summary>
		IEnumerator UpdateGraphCoroutine () {
			// Find the direction that we want to move the graph in.
			// Calcuculate this in graph space (where a distance of one is the size of one node)
			Vector3 dir = PointToGraphSpace(target.position) - PointToGraphSpace(graph.center);

			// Snap to a whole number of nodes
			dir.x = Mathf.Round(dir.x);
			dir.z = Mathf.Round(dir.z);
			dir.y = 0;

			// Nothing do to
			if (dir == Vector3.zero) yield break;

			// Number of nodes to offset in each direction
			Int2 offset = new Int2(-Mathf.RoundToInt(dir.x), -Mathf.RoundToInt(dir.z));

			// Move the center (this is in world units, so we need to convert it back from graph space)
			graph.center += graph.transform.TransformVector(dir);
			graph.UpdateTransform();

			// Cache some variables for easier access
			int width = graph.width;
			int depth = graph.depth;
			GridNodeBase[] nodes;
			// Layers are required when handling LayeredGridGraphs
			int layers = graph.LayerCount;
			nodes = graph.nodes;

			// Create a temporary buffer required for the calculations
			if (buffer == null || buffer.Length != width*depth) {
				buffer = new GridNodeBase[width*depth];
			}

			// Check if we have moved less than a whole graph width all directions
			// If we have moved more than this we can just as well recalculate the whole graph
			if (Mathf.Abs(offset.x) <= width && Mathf.Abs(offset.y) <= depth) {
				IntRect recalculateRect = new IntRect(0, 0, offset.x, offset.y);

				// If offset.x < 0, adjust the rect
				if (recalculateRect.xmin > recalculateRect.xmax) {
					int tmp2 = recalculateRect.xmax;
					recalculateRect.xmax = width + recalculateRect.xmin;
					recalculateRect.xmin = width + tmp2;
				}

				// If offset.y < 0, adjust the rect
				if (recalculateRect.ymin > recalculateRect.ymax) {
					int tmp2 = recalculateRect.ymax;
					recalculateRect.ymax = depth + recalculateRect.ymin;
					recalculateRect.ymin = depth + tmp2;
				}

				// Connections need to be recalculated for the neighbours as well, so we need to expand the rect by 1
				var connectionRect = recalculateRect.Expand(1);

				// Makes sure the rect stays inside the grid
				connectionRect = IntRect.Intersection(connectionRect, new IntRect(0, 0, width, depth));

				// Offset each node by the #offset variable
				// nodes which would end up outside the graph
				// will wrap around to the other side of it
				for (int l = 0; l < layers; l++) {
					int layerOffset = l*width*depth;
					for (int z = 0; z < depth; z++) {
						int pz = z*width;
						int tz = ((z+offset.y + depth)%depth)*width;
						for (int x = 0; x < width; x++) {
							buffer[tz + ((x+offset.x + width) % width)] = nodes[layerOffset + pz + x];
						}
					}

					yield return null;

					// Copy the nodes back to the graph
					// and set the correct indices
					for (int z = 0; z < depth; z++) {
						int pz = z*width;
						for (int x = 0; x < width; x++) {
							int newIndex = pz + x;
							var node = buffer[newIndex];
							if (node != null) node.NodeInGridIndex = newIndex;
							nodes[layerOffset + newIndex] = node;
						}

						// Calculate the limits for the region that has been wrapped
						// to the other side of the graph
						int xmin, xmax;
						if (z >= recalculateRect.ymin && z < recalculateRect.ymax) {
							xmin = 0;
							xmax = depth;
						} else {
							xmin = recalculateRect.xmin;
							xmax = recalculateRect.xmax;
						}

						for (int x = xmin; x < xmax; x++) {
							var node = buffer[pz + x];
							if (node != null) {
								// Clear connections on all nodes that are wrapped and placed on the other side of the graph.
								// This is both to clear any custom connections (which do not really make sense after moving the node)
								// and to prevent possible exceptions when the node will later (possibly) be destroyed because it was
								// not needed anymore (only for layered grid graphs).
								node.ClearConnections(false);
							}
						}
					}

					yield return null;
				}

				// The calculation will only update approximately this number of
				// nodes per frame. This is used to keep CPU load per frame low
				int yieldEvery = 1000;
				// To avoid the update taking too long, make yieldEvery somewhat proportional to the number of nodes that we are going to update
				int approxNumNodesToUpdate = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) * Mathf.Max(width, depth);
				yieldEvery = Mathf.Max(yieldEvery, approxNumNodesToUpdate/10);
				int counter = 0;

				// Recalculate the nodes
				// Take a look at the image in the docs for the UpdateGraph method
				// to see which nodes are being recalculated.
				for (int z = 0; z < depth; z++) {
					int xmin, xmax;
					if (z >= recalculateRect.ymin && z < recalculateRect.ymax) {
						xmin = 0;
						xmax = width;
					} else {
						xmin = recalculateRect.xmin;
						xmax = recalculateRect.xmax;
					}

					for (int x = xmin; x < xmax; x++) {
						graph.RecalculateCell(x, z, false, false);
					}

					counter += (xmax - xmin);

					if (counter > yieldEvery) {
						counter = 0;
						yield return null;
					}
				}

				for (int z = 0; z < depth; z++) {
					int xmin, xmax;
					if (z >= connectionRect.ymin && z < connectionRect.ymax) {
						xmin = 0;
						xmax = width;
					} else {
						xmin = connectionRect.xmin;
						xmax = connectionRect.xmax;
					}

					for (int x = xmin; x < xmax; x++) {
						graph.CalculateConnections(x, z);
					}

					counter += (xmax - xmin);

					if (counter > yieldEvery) {
						counter = 0;
						yield return null;
					}
				}

				yield return null;

				// Calculate all connections for the nodes along the boundary
				// of the graph, these always need to be updated
				/// <summary>TODO: Optimize to not traverse all nodes in the graph, only those at the edges</summary>
				for (int z = 0; z < depth; z++) {
					for (int x = 0; x < width; x++) {
						if (x == 0 || z == 0 || x == width-1 || z == depth-1) graph.CalculateConnections(x, z);
					}
				}
			} else {
				// The calculation will only update approximately this number of
				// nodes per frame. This is used to keep CPU load per frame low
				int yieldEvery = Mathf.Max(depth*width / 20, 1000);
				int counter = 0;
				// Just update all nodes
				for (int z = 0; z < depth; z++) {
					for (int x = 0; x < width; x++) {
						graph.RecalculateCell(x, z);
					}
					counter += width;
					if (counter > yieldEvery) {
						counter = 0;
						yield return null;
					}
				}

				// Recalculate the connections of all nodes
				for (int z = 0; z < depth; z++) {
					for (int x = 0; x < width; x++) {
						graph.CalculateConnections(x, z);
					}
					counter += width;
					if (counter > yieldEvery) {
						counter = 0;
						yield return null;
					}
				}
			}
		}
	}
}
