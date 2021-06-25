using UnityEngine;
using System.Collections.Generic;

namespace Pathfinding {
	[System.Serializable]
	/// <summary>
	/// Adjusts start and end points of a path.
	///
	/// This modifier is included in the <see cref="Pathfinding.Seeker"/> component and is always used if you are using a Seeker.
	/// When a path is calculated the resulting path will only be the positions of the nodes it passes through.
	/// However often you may not want to navigate to the center of a specific node but instead to a point on the surface of a node.
	/// This modifier will adjust the endpoints of the path.
	///
	/// [Open online documentation to see images]
	///
	/// \ingroup modifiers
	/// </summary>
	public class StartEndModifier : PathModifier {
		public override int Order { get { return 0; } }

		/// <summary>
		/// Add points to the path instead of replacing them.
		/// If for example <see cref="exactEndPoint"/> is set to ClosestOnNode then the path will be modified so that
		/// the path goes first to the center of the last node in the path and then goes to the closest point
		/// on the node to the end point in the path request.
		///
		/// If this is false however then the relevant points in the path will simply be replaced.
		/// In the above example the path would go directly to the closest point on the node without passing
		/// through the center of the node.
		/// </summary>
		public bool addPoints;

		/// <summary>
		/// How the start point of the path will be determined.
		/// See: <see cref="Exactness"/>
		/// </summary>
		public Exactness exactStartPoint = Exactness.ClosestOnNode;

		/// <summary>
		/// How the end point of the path will be determined.
		/// See: <see cref="Exactness"/>
		/// </summary>
		public Exactness exactEndPoint = Exactness.ClosestOnNode;

		/// <summary>
		/// Will be called when a path is processed.
		/// The value which is returned will be used as the start point of the path
		/// and potentially clamped depending on the value of the <see cref="exactStartPoint"/> field.
		/// Only used for the Original, Interpolate and NodeConnection modes.
		/// </summary>
		public System.Func<Vector3> adjustStartPoint;

		/// <summary>
		/// Sets where the start and end points of a path should be placed.
		///
		/// Here is a legend showing what the different items in the above images represent.
		/// The images above show a path coming in from the top left corner and ending at a node next to an obstacle as well as 2 different possible end points of the path and how they would be modified.
		/// [Open online documentation to see images]
		/// </summary>
		public enum Exactness {
			/// <summary>
			/// The point is snapped to the position of the first/last node in the path.
			/// Use this if your game is very tile based and you want your agents to stop precisely at the center of the nodes.
			/// If you recalculate the path while the agent is moving you may want the start point snapping to be ClosestOnNode and the end point snapping to be SnapToNode however
			/// as while an agent is moving it will likely not be right at the center of a node.
			///
			/// [Open online documentation to see images]
			/// </summary>
			SnapToNode,
			/// <summary>
			/// The point is set to the exact point which was passed when creating the path request.
			/// Note that if a path was for example requested to a point inside an obstacle, then the last point of the path will be inside that obstacle, which is usually not what you want.
			/// Consider using the <see cref="ClosestOnNode"/> option instead.
			///
			/// [Open online documentation to see images]
			/// </summary>
			Original,
			/// <summary>
			/// The point is set to the closest point on the line between either the two first points or the two last points.
			/// Usually you will want to use the NodeConnection mode instead since that is usually the behaviour that you really want.
			/// This mode exists mostly for compatibility reasons.
			/// [Open online documentation to see images]
			/// Deprecated: Use NodeConnection instead.
			/// </summary>
			Interpolate,
			/// <summary>
			/// The point is set to the closest point on the surface of the node. Note that some node types (point nodes) do not have a surface, so the "closest point" is simply the node's position which makes this identical to <see cref="Exactness.SnapToNode"/>.
			/// This is the mode that you almost always want to use in a free movement 3D world.
			/// [Open online documentation to see images]
			/// </summary>
			ClosestOnNode,
			/// <summary>
			/// The point is set to the closest point on one of the connections from the start/end node.
			/// This mode may be useful in a grid based or point graph based world when using the AILerp script.
			///
			/// Note: If you are using this mode with a <see cref="Pathfinding.PointGraph"/> you probably also want to use the <see cref="Pathfinding.PointGraph.NodeDistanceMode Connection"/> for <see cref="Pathfinding.PointGraph.nearestNodeDistanceMode"/>.
			///
			/// [Open online documentation to see images]
			/// </summary>
			NodeConnection,
		}

		/// <summary>
		/// Do a straight line check from the node's center to the point determined by the <see cref="Exactness"/>.
		/// There are very few cases where you will want to use this. It is mostly here for
		/// backwards compatibility reasons.
		///
		/// Version: Since 4.1 this field only has an effect for the <see cref="Exactness"/> mode Original because that's the only one where it makes sense.
		/// </summary>
		public bool useRaycasting;
		public LayerMask mask = -1;

		/// <summary>
		/// Do a straight line check from the node's center to the point determined by the <see cref="Exactness"/>.
		/// See: <see cref="useRaycasting"/>
		///
		/// Version: Since 4.1 this field only has an effect for the <see cref="Exactness"/> mode Original because that's the only one where it makes sense.
		/// </summary>
		public bool useGraphRaycasting;

		List<GraphNode> connectionBuffer;
		System.Action<GraphNode> connectionBufferAddDelegate;

		public override void Apply (Path _p) {
			var p = _p as ABPath;

			// This modifier only supports ABPaths (doesn't make much sense for other paths anyway)
			if (p == null || p.vectorPath.Count == 0) return;

			bool singleNode = false;

			if (p.vectorPath.Count == 1 && !addPoints) {
				// Duplicate first point
				p.vectorPath.Add(p.vectorPath[0]);
				singleNode = true;
			}

			// Add instead of replacing points
			bool forceAddStartPoint, forceAddEndPoint;
			// Which connection the start/end point was on (only used for the Connection mode)
			int closestStartConnection, closestEndConnection;

			Vector3 pStart = Snap(p, exactStartPoint, true, out forceAddStartPoint, out closestStartConnection);
			Vector3 pEnd = Snap(p, exactEndPoint, false, out forceAddEndPoint, out closestEndConnection);

			// This is a special case when the path is only a single node and the Connection mode is used.
			// (forceAddStartPoint/forceAddEndPoint is only used for the Connection mode)
			// In this case the start and end points lie on the connections of the node.
			// There are two cases:
			// 1. If the start and end points lie on the same connection we do *not* want
			// the path to pass through the node center but instead go directly from point to point.
			// This is the case of closestStartConnection == closestEndConnection.
			// 2. If the start and end points lie on different connections we *want*
			// the path to pass through the node center as it goes from one connection to another one.
			// However in any case we only want the node center to be added once to the path
			// so we set forceAddStartPoint to false anyway.
			if (singleNode) {
				if (closestStartConnection == closestEndConnection) {
					forceAddStartPoint = false;
					forceAddEndPoint = false;
				} else {
					forceAddStartPoint = false;
				}
			}

			// Add or replace the start point
			// Disable adding of points if the mode is SnapToNode since then
			// the first item in vectorPath will very likely be the same as the
			// position of the first node
			if ((forceAddStartPoint || addPoints) && exactStartPoint != Exactness.SnapToNode) {
				p.vectorPath.Insert(0, pStart);
			} else {
				p.vectorPath[0] = pStart;
			}

			if ((forceAddEndPoint || addPoints) && exactEndPoint != Exactness.SnapToNode) {
				p.vectorPath.Add(pEnd);
			} else {
				p.vectorPath[p.vectorPath.Count-1] = pEnd;
			}
		}

		Vector3 Snap (ABPath path, Exactness mode, bool start, out bool forceAddPoint, out int closestConnectionIndex) {
			var index = start ? 0 : path.path.Count - 1;
			var node = path.path[index];
			var nodePos = (Vector3)node.position;

			closestConnectionIndex = 0;

			forceAddPoint = false;

			switch (mode) {
			case Exactness.ClosestOnNode:
				return start ? path.startPoint : path.endPoint;
			case Exactness.SnapToNode:
				return nodePos;
			case Exactness.Original:
			case Exactness.Interpolate:
			case Exactness.NodeConnection:
				Vector3 relevantPoint;
				if (start) {
					relevantPoint = adjustStartPoint != null? adjustStartPoint () : path.originalStartPoint;
				} else {
					relevantPoint = path.originalEndPoint;
				}

				switch (mode) {
				case Exactness.Original:
					return GetClampedPoint(nodePos, relevantPoint, node);
				case Exactness.Interpolate:
					// Adjacent node to either the start node or the end node in the path
					var adjacentNode = path.path[Mathf.Clamp(index + (start ? 1 : -1), 0, path.path.Count-1)];
					return VectorMath.ClosestPointOnSegment(nodePos, (Vector3)adjacentNode.position, relevantPoint);
				case Exactness.NodeConnection:
					// This code uses some tricks to avoid allocations
					// even though it uses delegates heavily
					// The connectionBufferAddDelegate delegate simply adds whatever node
					// it is called with to the connectionBuffer
					connectionBuffer = connectionBuffer ?? new List<GraphNode>();
					connectionBufferAddDelegate = connectionBufferAddDelegate ?? (System.Action<GraphNode>)connectionBuffer.Add;

					// Adjacent node to either the start node or the end node in the path
					adjacentNode = path.path[Mathf.Clamp(index + (start ? 1 : -1), 0, path.path.Count-1)];

					// Add all neighbours of #node to the connectionBuffer
					node.GetConnections(connectionBufferAddDelegate);
					var bestPos = nodePos;
					var bestDist = float.PositiveInfinity;

					// Loop through all neighbours
					// Do it in reverse order because the length of the connectionBuffer
					// will change during iteration
					for (int i = connectionBuffer.Count - 1; i >= 0; i--) {
						var neighbour = connectionBuffer[i];

						// Find the closest point on the connection between the nodes
						// and check if the distance to that point is lower than the previous best
						var closest = VectorMath.ClosestPointOnSegment(nodePos, (Vector3)neighbour.position, relevantPoint);

						var dist = (closest - relevantPoint).sqrMagnitude;
						if (dist < bestDist) {
							bestPos = closest;
							bestDist = dist;
							closestConnectionIndex = i;

							// If this node is not the adjacent node
							// then the path should go through the start node as well
							forceAddPoint = neighbour != adjacentNode;
						}
					}

					connectionBuffer.Clear();
					return bestPos;
				default:
					throw new System.ArgumentException("Cannot reach this point, but the compiler is not smart enough to realize that.");
				}
			default:
				throw new System.ArgumentException("Invalid mode");
			}
		}

		protected Vector3 GetClampedPoint (Vector3 from, Vector3 to, GraphNode hint) {
			Vector3 point = to;
			RaycastHit hit;

			if (useRaycasting && Physics.Linecast(from, to, out hit, mask)) {
				point = hit.point;
			}

			if (useGraphRaycasting && hint != null) {
				var rayGraph = AstarData.GetGraph(hint) as IRaycastableGraph;

				if (rayGraph != null) {
					GraphHitInfo graphHit;
					if (rayGraph.Linecast(from, point, hint, out graphHit)) {
						point = graphHit.point;
					}
				}
			}

			return point;
		}
	}
}
