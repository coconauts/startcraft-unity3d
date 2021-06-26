using UnityEngine;
using System.Collections.Generic;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace Pathfinding {
	class PathReturnQueue {
		/// <summary>
		/// Holds all paths which are waiting to be flagged as completed.
		/// See: <see cref="ReturnPaths"/>
		/// </summary>
		Queue<Path> pathReturnQueue = new Queue<Path>();

		/// <summary>
		/// Paths are claimed silently by some object to prevent them from being recycled while still in use.
		/// This will be set to the AstarPath object.
		/// </summary>
		System.Object pathsClaimedSilentlyBy;

		public PathReturnQueue (System.Object pathsClaimedSilentlyBy) {
			this.pathsClaimedSilentlyBy = pathsClaimedSilentlyBy;
		}

		public void Enqueue (Path path) {
			lock (pathReturnQueue) {
				pathReturnQueue.Enqueue(path);
			}
		}

		/// <summary>
		/// Returns all paths in the return stack.
		/// Paths which have been processed are put in the return stack.
		/// This function will pop all items from the stack and return them to e.g the Seeker requesting them.
		/// </summary>
		/// <param name="timeSlice">Do not return all paths at once if it takes a long time, instead return some and wait until the next call.</param>
		public void ReturnPaths (bool timeSlice) {
			Profiler.BeginSample("Calling Path Callbacks");

			// Hard coded limit on 1.0 ms
			long targetTick = timeSlice ? System.DateTime.UtcNow.Ticks + 1 * 10000 : 0;

			int counter = 0;
			// Loop through the linked list and return all paths
			while (true) {
				// Move to the next path
				Path path;
				lock (pathReturnQueue) {
					if (pathReturnQueue.Count == 0) break;
					path = pathReturnQueue.Dequeue();
				}

				// Return the path
				((IPathInternals)path).ReturnPath();

				// Will increment path state to Returned
				((IPathInternals)path).AdvanceState(PathState.Returned);

				path.Release(pathsClaimedSilentlyBy, true);

				counter++;
				// At least 5 paths will be returned, even if timeSlice is enabled
				if (counter > 5 && timeSlice) {
					counter = 0;
					if (System.DateTime.UtcNow.Ticks >= targetTick) {
						break;
					}
				}
			}
			Profiler.EndSample();
		}
	}
}
