using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace Pathfinding {
#if NETFX_CORE
	using Thread = Pathfinding.WindowsStore.Thread;
#else
	using Thread = System.Threading.Thread;
#endif

	public class PathProcessor {
		public event System.Action<Path> OnPathPreSearch;
		public event System.Action<Path> OnPathPostSearch;
		public event System.Action OnQueueUnblocked;

		internal readonly ThreadControlQueue queue;
		readonly AstarPath astar;
		readonly PathReturnQueue returnQueue;

		readonly PathHandler[] pathHandlers;

		/// <summary>References to each of the pathfinding threads</summary>
		readonly Thread[] threads;

		/// <summary>
		/// When no multithreading is used, the IEnumerator is stored here.
		/// When no multithreading is used, a coroutine is used instead. It is not directly called with StartCoroutine
		/// but a separate function has just a while loop which increments the main IEnumerator.
		/// This is done so other functions can step the thread forward at any time, without having to wait for Unity to update it.
		/// See: CalculatePaths
		/// See: CalculatePathsHandler
		/// </summary>
		IEnumerator threadCoroutine;

		/// <summary>
		/// Holds the next node index which has not been used by any previous node.
		/// See: nodeIndexPool
		/// </summary>
		int nextNodeIndex = 1;

		/// <summary>
		/// Holds indices for nodes that have been destroyed.
		/// To avoid trashing a lot of memory structures when nodes are
		/// frequently deleted and created, node indices are reused.
		/// </summary>
		readonly Stack<int> nodeIndexPool = new Stack<int>();

		readonly List<int> locks = new List<int>();
		int nextLockID = 0;

#if UNITY_2017_3_OR_NEWER
		CustomSampler profilingSampler;
#endif

		/// <summary>
		/// Number of parallel pathfinders.
		/// Returns the number of concurrent processes which can calculate paths at once.
		/// When using multithreading, this will be the number of threads, if not using multithreading it is always 1 (since only 1 coroutine is used).
		/// See: threadInfos
		/// See: IsUsingMultithreading
		/// </summary>
		public int NumThreads {
			get {
				return pathHandlers.Length;
			}
		}

		/// <summary>Returns whether or not multithreading is used</summary>
		public bool IsUsingMultithreading {
			get {
				return threads != null;
			}
		}

		internal PathProcessor (AstarPath astar, PathReturnQueue returnQueue, int processors, bool multithreaded) {
			this.astar = astar;
			this.returnQueue = returnQueue;

			if (processors < 0) {
				throw new System.ArgumentOutOfRangeException("processors");
			}

			if (!multithreaded && processors != 1) {
				throw new System.Exception("Only a single non-multithreaded processor is allowed");
			}

			// Set up path queue with the specified number of receivers
			queue = new ThreadControlQueue(processors);
			pathHandlers = new PathHandler[processors];

			for (int i = 0; i < processors; i++) {
				pathHandlers[i] = new PathHandler(i, processors);
			}

			if (multithreaded) {
#if UNITY_2017_3_OR_NEWER
				profilingSampler = CustomSampler.Create("Calculating Path");
#endif

				threads = new Thread[processors];

				// Start lots of threads
				for (int i = 0; i < processors; i++) {
					var pathHandler = pathHandlers[i];
					threads[i] = new Thread(() => CalculatePathsThreaded(pathHandler));
#if !UNITY_SWITCH || UNITY_EDITOR
					// Note: Setting the thread name seems to crash when deploying for Switch: https://forum.arongranberg.com/t/path-processor-crashing-nintendo-switch-build/6584
					threads[i].Name = "Pathfinding Thread " + i;
#endif
					threads[i].IsBackground = true;
					threads[i].Start();
				}
			} else {
				// Start coroutine if not using multithreading
				threadCoroutine = CalculatePaths(pathHandlers[0]);
			}
		}

		/// <summary>Prevents pathfinding from running while held</summary>
		public struct GraphUpdateLock {
			PathProcessor pathProcessor;
			int id;

			public GraphUpdateLock (PathProcessor pathProcessor, bool block) {
				this.pathProcessor = pathProcessor;
				id = pathProcessor.Lock(block);
			}

			/// <summary>
			/// True while this lock is preventing the pathfinding threads from processing more paths.
			/// Note that the pathfinding threads may not be paused yet (if this lock was obtained using PausePathfinding(false)).
			/// </summary>
			public bool Held {
				get {
					return pathProcessor != null && pathProcessor.locks.Contains(id);
				}
			}

			/// <summary>Allow pathfinding to start running again if no other locks are still held</summary>
			public void Release () {
				pathProcessor.Unlock(id);
			}
		}

		int Lock (bool block) {
			queue.Block();

			if (block) {
				while (!queue.AllReceiversBlocked) {
					if (IsUsingMultithreading) {
						Thread.Sleep(1);
					} else {
						TickNonMultithreaded();
					}
				}
			}

			nextLockID++;
			locks.Add(nextLockID);
			return nextLockID;
		}

		void Unlock (int id) {
			if (!locks.Remove(id)) {
				throw new System.ArgumentException("This lock has already been released");
			}

			// Check if there are no remaining active locks
			if (locks.Count == 0) {
				if (OnQueueUnblocked != null) OnQueueUnblocked();

				queue.Unblock();
			}
		}

		/// <summary>
		/// Prevents pathfinding threads from starting to calculate any new paths.
		///
		/// Returns: A lock object. You need to call Unlock on that object to allow pathfinding to resume.
		///
		/// Note: In most cases this should not be called from user code.
		/// </summary>
		/// <param name="block">If true, this call will block until all pathfinding threads are paused.
		/// otherwise the threads will be paused as soon as they are done with what they are currently doing.</param>
		public GraphUpdateLock PausePathfinding (bool block) {
			return new GraphUpdateLock(this, block);
		}

		public void TickNonMultithreaded () {
			// Process paths
			if (threadCoroutine != null) {
				try {
					threadCoroutine.MoveNext();
				} catch (System.Exception e) {
					//This will kill pathfinding
					threadCoroutine = null;

					// Queue termination exceptions should be ignored, they are supposed to kill the thread
					if (!(e is ThreadControlQueue.QueueTerminationException)) {
						Debug.LogException(e);
						Debug.LogError("Unhandled exception during pathfinding. Terminating.");
						queue.TerminateReceivers();

						// This will throw an exception supposed to kill the thread
						try {
							queue.PopNoBlock(false);
						} catch {}
					}
				}
			}
		}

		/// <summary>Calls 'Join' on each of the threads to block until they have completed</summary>
		public void JoinThreads () {
			if (threads != null) {
				for (int i = 0; i < threads.Length; i++) {
					if (!threads[i].Join(200)) {
						Debug.LogError("Could not terminate pathfinding thread["+i+"] in 200ms, trying Thread.Abort");
						threads[i].Abort();
					}
				}
			}
		}

		/// <summary>Calls 'Abort' on each of the threads</summary>
		public void AbortThreads () {
			if (threads == null) return;
			for (int i = 0; i < threads.Length; i++) {
				if (threads[i] != null && threads[i].IsAlive) threads[i].Abort();
			}
		}

		/// <summary>
		/// Returns a new global node index.
		/// Warning: This method should not be called directly. It is used by the GraphNode constructor.
		/// </summary>
		public int GetNewNodeIndex () {
			return nodeIndexPool.Count > 0 ? nodeIndexPool.Pop() : nextNodeIndex++;
		}

		/// <summary>
		/// Initializes temporary path data for a node.
		/// Warning: This method should not be called directly. It is used by the GraphNode constructor.
		/// </summary>
		public void InitializeNode (GraphNode node) {
			if (!queue.AllReceiversBlocked) {
				throw new System.Exception("Trying to initialize a node when it is not safe to initialize any nodes. Must be done during a graph update. See http://arongranberg.com/astar/docs/graph-updates.php#direct");
			}

			for (int i = 0; i < pathHandlers.Length; i++) {
				pathHandlers[i].InitializeNode(node);
			}

			astar.hierarchicalGraph.OnCreatedNode(node);
		}

		/// <summary>
		/// Destroyes the given node.
		/// This is to be called after the node has been disconnected from the graph so that it cannot be reached from any other nodes.
		/// It should only be called during graph updates, that is when the pathfinding threads are either not running or paused.
		///
		/// Warning: This method should not be called by user code. It is used internally by the system.
		/// </summary>
		public void DestroyNode (GraphNode node) {
			if (node.NodeIndex == -1) return;

			nodeIndexPool.Push(node.NodeIndex);

			for (int i = 0; i < pathHandlers.Length; i++) {
				pathHandlers[i].DestroyNode(node);
			}

			astar.hierarchicalGraph.AddDirtyNode(node);
		}

		/// <summary>
		/// Main pathfinding method (multithreaded).
		/// This method will calculate the paths in the pathfinding queue when multithreading is enabled.
		///
		/// See: CalculatePaths
		/// See: StartPath
		/// </summary>
		void CalculatePathsThreaded (PathHandler pathHandler) {
#if UNITY_2017_3_OR_NEWER
			UnityEngine.Profiling.Profiler.BeginThreadProfiling("Pathfinding", "Pathfinding thread #" + (pathHandler.threadID+1));
#endif

#if !ASTAR_FAST_BUT_NO_EXCEPTIONS
			try {
#endif

			// Max number of ticks we are allowed to continue working in one run.
			// One tick is 1/10000 of a millisecond.
			// We need to check once in a while if the thread should be stopped.
			long maxTicks = (long)(10*10000);
			long targetTick = System.DateTime.UtcNow.Ticks + maxTicks;
			while (true) {
				// The path we are currently calculating
				Path path = queue.Pop();
#if UNITY_2017_3_OR_NEWER
				profilingSampler.Begin();
#endif
				// Access the internal implementation methods
				IPathInternals ipath = (IPathInternals)path;

				// Trying to prevent simple modding to allow more than one thread
				if (pathHandler.threadID > 0) {
					throw new System.Exception("Thread Error");
				}

				AstarProfiler.StartFastProfile(0);
				ipath.PrepareBase(pathHandler);

				// Now processing the path
				// Will advance to Processing
				ipath.AdvanceState(PathState.Processing);

				// Call some callbacks
				if (OnPathPreSearch != null) {
					OnPathPreSearch(path);
				}

				// Tick for when the path started, used for calculating how long time the calculation took
				long startTicks = System.DateTime.UtcNow.Ticks;

				// Prepare the path
				ipath.Prepare();

				AstarProfiler.EndFastProfile(0);

				if (path.CompleteState == PathCompleteState.NotCalculated) {
					// For visualization purposes, we set the last computed path to p, so we can view debug info on it in the editor (scene view).
					astar.debugPathData = ipath.PathHandler;
					astar.debugPathID = path.pathID;

					AstarProfiler.StartFastProfile(1);

					// Initialize the path, now ready to begin search
					ipath.Initialize();

					AstarProfiler.EndFastProfile(1);

					// Loop while the path has not been fully calculated
					while (path.CompleteState == PathCompleteState.NotCalculated) {
						// Do some work on the path calculation.
						// The function will return when it has taken too much time
						// or when it has finished calculation
						AstarProfiler.StartFastProfile(2);
						ipath.CalculateStep(targetTick);
						AstarProfiler.EndFastProfile(2);

						targetTick = System.DateTime.UtcNow.Ticks + maxTicks;

						// Cancel function (and thus the thread) if no more paths should be accepted.
						// This is done when the A* object is about to be destroyed
						// The path is returned and then this function will be terminated
						if (queue.IsTerminating) {
							path.FailWithError("AstarPath object destroyed");
						}
					}

					path.duration = (System.DateTime.UtcNow.Ticks - startTicks)*0.0001F;

#if ProfileAstar
					System.Threading.Interlocked.Increment(ref AstarPath.PathsCompleted);
					System.Threading.Interlocked.Add(ref AstarPath.TotalSearchTime, System.DateTime.UtcNow.Ticks - startTicks);
#endif
				}

				// Cleans up node tagging and other things
				ipath.Cleanup();

				AstarProfiler.StartFastProfile(9);

				if (path.immediateCallback != null) path.immediateCallback(path);

				if (OnPathPostSearch != null) {
					OnPathPostSearch(path);
				}

				// Push the path onto the return stack
				// It will be detected by the main Unity thread and returned as fast as possible (the next late update hopefully)
				returnQueue.Enqueue(path);

				// Will advance to ReturnQueue
				ipath.AdvanceState(PathState.ReturnQueue);

				AstarProfiler.EndFastProfile(9);
#if UNITY_2017_3_OR_NEWER
				profilingSampler.End();
#endif
			}
#if !ASTAR_FAST_BUT_NO_EXCEPTIONS
		}
		catch (System.Exception e) {
#if !NETFX_CORE
			if (e is ThreadAbortException || e is ThreadControlQueue.QueueTerminationException)
#else
			if (e is ThreadControlQueue.QueueTerminationException)
#endif
			{
				if (astar.logPathResults == PathLog.Heavy)
					Debug.LogWarning("Shutting down pathfinding thread #" + pathHandler.threadID);
				return;
			}
			Debug.LogException(e);
			Debug.LogError("Unhandled exception during pathfinding. Terminating.");
			// Unhandled exception, kill pathfinding
			queue.TerminateReceivers();
		} finally {
#if UNITY_2017_3_OR_NEWER
			UnityEngine.Profiling.Profiler.EndThreadProfiling();
#endif
		}
#endif

			Debug.LogError("Error : This part should never be reached.");
			queue.ReceiverTerminated();
		}

		/// <summary>
		/// Main pathfinding method.
		/// This method will calculate the paths in the pathfinding queue.
		///
		/// See: CalculatePathsThreaded
		/// See: StartPath
		/// </summary>
		IEnumerator CalculatePaths (PathHandler pathHandler) {
			// Max number of ticks before yielding/sleeping
			long maxTicks = (long)(astar.maxFrameTime*10000);
			long targetTick = System.DateTime.UtcNow.Ticks + maxTicks;

			while (true) {
				// The path we are currently calculating
				Path p = null;

				AstarProfiler.StartProfile("Path Queue");

				// Try to get the next path to be calculated
				bool blockedBefore = false;
				while (p == null) {
					try {
						p = queue.PopNoBlock(blockedBefore);
						blockedBefore |= p == null;
					} catch (ThreadControlQueue.QueueTerminationException) {
						yield break;
					}

					if (p == null) {
						AstarProfiler.EndProfile();
						yield return null;
						AstarProfiler.StartProfile("Path Queue");
					}
				}

				AstarProfiler.EndProfile();

				AstarProfiler.StartProfile("Path Calc");

				IPathInternals ip = (IPathInternals)p;

				// Max number of ticks we are allowed to continue working in one run
				// One tick is 1/10000 of a millisecond
				maxTicks = (long)(astar.maxFrameTime*10000);

				ip.PrepareBase(pathHandler);

				// Now processing the path
				// Will advance to Processing
				ip.AdvanceState(PathState.Processing);

				// Call some callbacks
				// It needs to be stored in a local variable to avoid race conditions
				var tmpOnPathPreSearch = OnPathPreSearch;
				if (tmpOnPathPreSearch != null) tmpOnPathPreSearch(p);

				// Tick for when the path started, used for calculating how long time the calculation took
				long startTicks = System.DateTime.UtcNow.Ticks;
				long totalTicks = 0;

				AstarProfiler.StartFastProfile(8);

				AstarProfiler.StartFastProfile(0);
				//Prepare the path
				AstarProfiler.StartProfile("Path Prepare");
				ip.Prepare();
				AstarProfiler.EndProfile("Path Prepare");
				AstarProfiler.EndFastProfile(0);

				// Check if the Prepare call caused the path to complete
				// If this happens the path usually failed
				if (p.CompleteState == PathCompleteState.NotCalculated) {
					// For debug uses, we set the last computed path to p, so we can view debug info on it in the editor (scene view).
					astar.debugPathData = ip.PathHandler;
					astar.debugPathID = p.pathID;

					// Initialize the path, now ready to begin search
					AstarProfiler.StartProfile("Path Initialize");
					ip.Initialize();
					AstarProfiler.EndProfile();

					// The error can turn up in the Init function
					while (p.CompleteState == PathCompleteState.NotCalculated) {
						// Do some work on the path calculation.
						// The function will return when it has taken too much time
						// or when it has finished calculation
						AstarProfiler.StartFastProfile(2);

						AstarProfiler.StartProfile("Path Calc Step");
						ip.CalculateStep(targetTick);
						AstarProfiler.EndFastProfile(2);

						AstarProfiler.EndProfile();

						// If the path has finished calculation, we can break here directly instead of sleeping
						// Improves latency
						if (p.CompleteState != PathCompleteState.NotCalculated) break;

						AstarProfiler.EndFastProfile(8);
						totalTicks += System.DateTime.UtcNow.Ticks-startTicks;
						// Yield/sleep so other threads can work

						AstarProfiler.EndProfile();
						yield return null;
						AstarProfiler.StartProfile("Path Calc");

						startTicks = System.DateTime.UtcNow.Ticks;
						AstarProfiler.StartFastProfile(8);

						// Cancel function (and thus the thread) if no more paths should be accepted.
						// This is done when the A* object is about to be destroyed
						// The path is returned and then this function will be terminated (see similar IF statement higher up in the function)
						if (queue.IsTerminating) {
							p.FailWithError("AstarPath object destroyed");
						}

						targetTick = System.DateTime.UtcNow.Ticks + maxTicks;
					}

					totalTicks += System.DateTime.UtcNow.Ticks-startTicks;
					p.duration = totalTicks*0.0001F;

#if ProfileAstar
					System.Threading.Interlocked.Increment(ref AstarPath.PathsCompleted);
#endif
				}

				// Cleans up node tagging and other things
				ip.Cleanup();

				AstarProfiler.EndFastProfile(8);

				// Call the immediate callback
				// It needs to be stored in a local variable to avoid race conditions
				var tmpImmediateCallback = p.immediateCallback;
				if (tmpImmediateCallback != null) tmpImmediateCallback(p);

				AstarProfiler.StartFastProfile(13);

				// It needs to be stored in a local variable to avoid race conditions
				var tmpOnPathPostSearch = OnPathPostSearch;
				if (tmpOnPathPostSearch != null) tmpOnPathPostSearch(p);

				AstarProfiler.EndFastProfile(13);

				// Push the path onto the return stack
				// It will be detected by the main Unity thread and returned as fast as possible (the next late update)
				returnQueue.Enqueue(p);

				ip.AdvanceState(PathState.ReturnQueue);

				AstarProfiler.EndProfile();

				// Wait a bit if we have calculated a lot of paths
				if (System.DateTime.UtcNow.Ticks > targetTick) {
					yield return null;
					targetTick = System.DateTime.UtcNow.Ticks + maxTicks;
				}
			}
		}
	}
}
