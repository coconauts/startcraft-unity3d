#pragma warning disable 414
using System.Collections.Generic;
using UnityEngine;

namespace Pathfinding {
	public enum HeuristicOptimizationMode {
		None,
		Random,
		RandomSpreadOut,
		Custom
	}

	/// <summary>
	/// Implements heuristic optimizations.
	///
	/// See: heuristic-opt
	/// See: Game AI Pro - Pathfinding Architecture Optimizations by Steve Rabin and Nathan R. Sturtevant
	/// </summary>
	[System.Serializable]
	public class EuclideanEmbedding {
		/// <summary>
		/// If heuristic optimization should be used and how to place the pivot points.
		/// See: heuristic-opt
		/// See: Game AI Pro - Pathfinding Architecture Optimizations by Steve Rabin and Nathan R. Sturtevant
		/// </summary>
		public HeuristicOptimizationMode mode;

		public int seed;

		/// <summary>All children of this transform will be used as pivot points</summary>
		public Transform pivotPointRoot;

		public int spreadOutCount = 1;

		[System.NonSerialized]
		public bool dirty;


		void EnsureCapacity (int index) {
		}

		public uint GetHeuristic (int nodeIndex1, int nodeIndex2) {
			return 0;
		}


		public void RecalculatePivots () {
		}

		public void RecalculateCosts () {
			dirty = false;
		}


		public void OnDrawGizmos () {
		}
	}
}
