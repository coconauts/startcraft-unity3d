using UnityEngine;

namespace Pathfinding {
	/// <summary>
	/// Blocks single nodes in a graph.
	///
	/// This is useful in turn based games where you want
	/// units to avoid all other units while pathfinding
	/// but not be blocked by itself.
	///
	/// Note: This cannot be used together with any movement script
	/// as the nodes are not blocked in the normal way.
	/// See: TurnBasedAI for example usage
	///
	/// See: BlockManager
	/// See: turnbased (view in online documentation for working links)
	/// </summary>
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_single_node_blocker.php")]
	public class SingleNodeBlocker : VersionedMonoBehaviour {
		public GraphNode lastBlocked { get; private set; }
		public BlockManager manager;

		/// <summary>
		/// Block node closest to the position of this object.
		///
		/// Will unblock the last node that was reserved (if any)
		/// </summary>
		public void BlockAtCurrentPosition () {
			BlockAt(transform.position);
		}

		/// <summary>
		/// Block node closest to the specified position.
		///
		/// Will unblock the last node that was reserved (if any)
		/// </summary>
		public void BlockAt (Vector3 position) {
			Unblock();
			var node = AstarPath.active.GetNearest(position, NNConstraint.None).node;
			if (node != null) {
				Block(node);
			}
		}

		/// <summary>
		/// Block specified node.
		///
		/// Will unblock the last node that was reserved (if any)
		/// </summary>
		public void Block (GraphNode node) {
			if (node == null)
				throw new System.ArgumentNullException("node");

			manager.InternalBlock(node, this);
			lastBlocked = node;
		}

		/// <summary>Unblock the last node that was blocked (if any)</summary>
		public void Unblock () {
			if (lastBlocked == null || lastBlocked.Destroyed) {
				lastBlocked = null;
				return;
			}

			manager.InternalUnblock(lastBlocked, this);
			lastBlocked = null;
		}
	}
}
