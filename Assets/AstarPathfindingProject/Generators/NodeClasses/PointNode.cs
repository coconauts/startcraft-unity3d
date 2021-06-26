using UnityEngine;
using Pathfinding.Serialization;

namespace Pathfinding {
	/// <summary>
	/// Node used for the PointGraph.
	/// This is just a simple point with a list of connections (and associated costs) to other nodes.
	/// It does not have any concept of a surface like many other node types.
	///
	/// See: PointGraph
	/// </summary>
	public class PointNode : GraphNode {
		/// <summary>
		/// All connections from this node.
		/// See: <see cref="AddConnection"/>
		/// See: <see cref="RemoveConnection"/>
		///
		/// Note: If you modify this array or the contents of it you must call <see cref="SetConnectivityDirty"/>.
		///
		/// Note: If you modify this array or the contents of it you must call <see cref="PointGraph.RegisterConnectionLength"/> with the length of the new connections.
		/// </summary>
		public Connection[] connections;

		/// <summary>
		/// GameObject this node was created from (if any).
		/// Warning: When loading a graph from a saved file or from cache, this field will be null.
		///
		/// <code>
		/// var node = AstarPath.active.GetNearest(transform.position).node;
		/// var pointNode = node as PointNode;
		///
		/// if (pointNode != null) {
		///     Debug.Log("That node was created from the GameObject named " + pointNode.gameObject.name);
		/// } else {
		///     Debug.Log("That node is not a PointNode");
		/// }
		/// </code>
		/// </summary>
		public GameObject gameObject;

		public void SetPosition (Int3 value) {
			position = value;
		}

		public PointNode (AstarPath astar) : base(astar) {
		}

		public override void GetConnections (System.Action<GraphNode> action) {
			if (connections == null) return;
			for (int i = 0; i < connections.Length; i++) action(connections[i].node);
		}

		public override void ClearConnections (bool alsoReverse) {
			if (alsoReverse && connections != null) {
				for (int i = 0; i < connections.Length; i++) {
					connections[i].node.RemoveConnection(this);
				}
			}

			connections = null;
			AstarPath.active.hierarchicalGraph.AddDirtyNode(this);
		}

		public override void UpdateRecursiveG (Path path, PathNode pathNode, PathHandler handler) {
			pathNode.UpdateG(path);

			handler.heap.Add(pathNode);

			for (int i = 0; i < connections.Length; i++) {
				GraphNode other = connections[i].node;
				PathNode otherPN = handler.GetPathNode(other);
				if (otherPN.parent == pathNode && otherPN.pathID == handler.PathID) {
					other.UpdateRecursiveG(path, otherPN, handler);
				}
			}
		}

		public override bool ContainsConnection (GraphNode node) {
			if (connections == null) return false;
			for (int i = 0; i < connections.Length; i++) if (connections[i].node == node) return true;
			return false;
		}

		/// <summary>
		/// Add a connection from this node to the specified node.
		/// If the connection already exists, the cost will simply be updated and
		/// no extra connection added.
		///
		/// Note: Only adds a one-way connection. Consider calling the same function on the other node
		/// to get a two-way connection.
		///
		/// <code>
		/// AstarPath.active.AddWorkItem(new AstarWorkItem(ctx => {
		///     // Connect two nodes
		///     var node1 = AstarPath.active.GetNearest(transform.position, NNConstraint.None).node;
		///     var node2 = AstarPath.active.GetNearest(transform.position + Vector3.right, NNConstraint.None).node;
		///     var cost = (uint)(node2.position - node1.position).costMagnitude;
		///     node1.AddConnection(node2, cost);
		///     node2.AddConnection(node1, cost);
		///
		///     node1.ContainsConnection(node2); // True
		///
		///     node1.RemoveConnection(node2);
		///     node2.RemoveConnection(node1);
		/// }));
		/// </code>
		/// </summary>
		public override void AddConnection (GraphNode node, uint cost) {
			if (node == null) throw new System.ArgumentNullException();

			if (connections != null) {
				for (int i = 0; i < connections.Length; i++) {
					if (connections[i].node == node) {
						connections[i].cost = cost;
						return;
					}
				}
			}

			int connLength = connections != null ? connections.Length : 0;

			var newconns = new Connection[connLength+1];
			for (int i = 0; i < connLength; i++) {
				newconns[i] = connections[i];
			}

			newconns[connLength] = new Connection(node, cost);

			connections = newconns;
			AstarPath.active.hierarchicalGraph.AddDirtyNode(this);

			// Make sure the graph knows that there exists a connection with this length
			(this.Graph as PointGraph).RegisterConnectionLength((node.position - position).sqrMagnitudeLong);
		}

		/// <summary>
		/// Removes any connection from this node to the specified node.
		/// If no such connection exists, nothing will be done.
		///
		/// Note: This only removes the connection from this node to the other node.
		/// You may want to call the same function on the other node to remove its possible connection
		/// to this node.
		///
		/// <code>
		/// AstarPath.active.AddWorkItem(new AstarWorkItem(ctx => {
		///     // Connect two nodes
		///     var node1 = AstarPath.active.GetNearest(transform.position, NNConstraint.None).node;
		///     var node2 = AstarPath.active.GetNearest(transform.position + Vector3.right, NNConstraint.None).node;
		///     var cost = (uint)(node2.position - node1.position).costMagnitude;
		///     node1.AddConnection(node2, cost);
		///     node2.AddConnection(node1, cost);
		///
		///     node1.ContainsConnection(node2); // True
		///
		///     node1.RemoveConnection(node2);
		///     node2.RemoveConnection(node1);
		/// }));
		/// </code>
		/// </summary>
		public override void RemoveConnection (GraphNode node) {
			if (connections == null) return;

			for (int i = 0; i < connections.Length; i++) {
				if (connections[i].node == node) {
					int connLength = connections.Length;

					var newconns = new Connection[connLength-1];
					for (int j = 0; j < i; j++) {
						newconns[j] = connections[j];
					}
					for (int j = i+1; j < connLength; j++) {
						newconns[j-1] = connections[j];
					}

					connections = newconns;
					AstarPath.active.hierarchicalGraph.AddDirtyNode(this);
					return;
				}
			}
		}

		public override void Open (Path path, PathNode pathNode, PathHandler handler) {
			if (connections == null) return;

			for (int i = 0; i < connections.Length; i++) {
				GraphNode other = connections[i].node;

				if (path.CanTraverse(other)) {
					PathNode pathOther = handler.GetPathNode(other);

					if (pathOther.pathID != handler.PathID) {
						pathOther.parent = pathNode;
						pathOther.pathID = handler.PathID;

						pathOther.cost = connections[i].cost;

						pathOther.H = path.CalculateHScore(other);
						pathOther.UpdateG(path);

						handler.heap.Add(pathOther);
					} else {
						//If not we can test if the path from this node to the other one is a better one then the one already used
						uint tmpCost = connections[i].cost;

						if (pathNode.G + tmpCost + path.GetTraversalCost(other) < pathOther.G) {
							pathOther.cost = tmpCost;
							pathOther.parent = pathNode;

							other.UpdateRecursiveG(path, pathOther, handler);
						}
					}
				}
			}
		}

		public override int GetGizmoHashCode () {
			var hash = base.GetGizmoHashCode();

			if (connections != null) {
				for (int i = 0; i < connections.Length; i++) {
					hash ^= 17 * connections[i].GetHashCode();
				}
			}
			return hash;
		}

		public override void SerializeNode (GraphSerializationContext ctx) {
			base.SerializeNode(ctx);
			ctx.SerializeInt3(position);
		}

		public override void DeserializeNode (GraphSerializationContext ctx) {
			base.DeserializeNode(ctx);
			position = ctx.DeserializeInt3();
		}

		public override void SerializeReferences (GraphSerializationContext ctx) {
			if (connections == null) {
				ctx.writer.Write(-1);
			} else {
				ctx.writer.Write(connections.Length);
				for (int i = 0; i < connections.Length; i++) {
					ctx.SerializeNodeReference(connections[i].node);
					ctx.writer.Write(connections[i].cost);
				}
			}
		}

		public override void DeserializeReferences (GraphSerializationContext ctx) {
			int count = ctx.reader.ReadInt32();

			if (count == -1) {
				connections = null;
			} else {
				connections = new Connection[count];

				for (int i = 0; i < count; i++) {
					connections[i] = new Connection(ctx.DeserializeNodeReference(), ctx.reader.ReadUInt32());
				}
			}
		}
	}
}
