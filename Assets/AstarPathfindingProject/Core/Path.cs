//#define ASTAR_POOL_DEBUG //@SHOWINEDITOR Enables debugging of path pooling. Will log warnings and info messages about paths not beeing pooled correctly.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Pathfinding {
	/// <summary>
	/// Provides additional traversal information to a path request.
	/// See: turnbased (view in online documentation for working links)
	/// </summary>
	public interface ITraversalProvider {
		bool CanTraverse (Path path, GraphNode node);
		uint GetTraversalCost (Path path, GraphNode node);
	}

	/// <summary>Convenience class to access the default implementation of the ITraversalProvider</summary>
	public static class DefaultITraversalProvider {
		public static bool CanTraverse (Path path, GraphNode node) {
			return node.Walkable && (path.enabledTags >> (int)node.Tag & 0x1) != 0;
		}

		public static uint GetTraversalCost (Path path, GraphNode node) {
			return path.GetTagPenalty((int)node.Tag) + node.Penalty;
		}
	}

	/// <summary>Base class for all path types</summary>
	public abstract class Path : IPathInternals {
#if ASTAR_POOL_DEBUG
		private string pathTraceInfo = "";
		private List<string> claimInfo = new List<string>();
		~Path() {
			Debug.Log("Destroying " + GetType().Name + " instance");
			if (claimed.Count > 0) {
				Debug.LogWarning("Pool Is Leaking. See list of claims:\n" +
					"Each message below will list what objects are currently claiming the path." +
					" These objects have removed their reference to the path object but has not called .Release on it (which is bad).\n" + pathTraceInfo+"\n");
				for (int i = 0; i < claimed.Count; i++) {
					Debug.LogWarning("- Claim "+ (i+1) + " is by a " + claimed[i].GetType().Name + "\n"+claimInfo[i]);
				}
			} else {
				Debug.Log("Some scripts are not using pooling.\n" + pathTraceInfo + "\n");
			}
		}
#endif

		/// <summary>Data for the thread calculating this path</summary>
		protected PathHandler pathHandler;

		/// <summary>
		/// Callback to call when the path is complete.
		/// This is usually sent to the Seeker component which post processes the path and then calls a callback to the script which requested the path
		/// </summary>
		public OnPathDelegate callback;

		/// <summary>
		/// Immediate callback to call when the path is complete.
		/// Warning: This may be called from a separate thread. Usually you do not want to use this one.
		///
		/// See: callback
		/// </summary>
		public OnPathDelegate immediateCallback;

		/// <summary>Returns the state of the path in the pathfinding pipeline</summary>
		public PathState PipelineState { get; private set; }
		System.Object stateLock = new object();

		/// <summary>
		/// Provides additional traversal information to a path request.
		/// See: turnbased (view in online documentation for working links)
		/// </summary>
		public ITraversalProvider traversalProvider;


		/// <summary>Backing field for <see cref="CompleteState"/></summary>
		protected PathCompleteState completeState;

		/// <summary>
		/// Current state of the path.
		/// \bug This may currently be set to Complete before the path has actually been fully calculated. In particular the vectorPath and path lists may not have been fully constructed.
		/// This can lead to race conditions when using multithreading. Try to avoid using this method to check for if the path is calculated right now, use <see cref="IsDone"/> instead.
		/// </summary>
		public PathCompleteState CompleteState {
			get { return completeState; }
			protected set {
				// Locking is used to avoid multithreading race conditions
				// in which the error state is set on the main thread to cancel the path and then a pathfinding thread marks the path as
				// completed which would replace the error state (if a lock and check would not have been used).
				lock (stateLock) {
					// Once the path is put in the error state, it cannot be set to any other state
					if (completeState != PathCompleteState.Error) completeState = value;
				}
			}
		}

		/// <summary>
		/// If the path failed, this is true.
		/// See: <see cref="errorLog"/>
		/// See: This is equivalent to checking path.CompleteState == PathCompleteState.Error
		/// </summary>
		public bool error { get { return CompleteState == PathCompleteState.Error; } }

		/// <summary>
		/// Additional info on why a path failed.
		/// See: <see cref="AstarPath.logPathResults"/>
		/// </summary>
		public string errorLog { get; private set; }

		/// <summary>
		/// Holds the path as a Node array. All nodes the path traverses.
		/// This may not be the same nodes as the post processed path traverses.
		/// </summary>
		public List<GraphNode> path;

		/// <summary>Holds the (possibly post processed) path as a Vector3 list</summary>
		public List<Vector3> vectorPath;

		/// <summary>The node currently being processed</summary>
		protected PathNode currentR;

		/// <summary>How long it took to calculate this path in milliseconds</summary>
		public float duration;

		/// <summary>Number of nodes this path has searched</summary>
		internal int searchedNodes;

		/// <summary>
		/// True if the path is currently pooled.
		/// Do not set this value. Only read. It is used internally.
		///
		/// See: PathPool
		/// Version: Was named 'recycled' in 3.7.5 and earlier.
		/// </summary>
		bool IPathInternals.Pooled { get; set; }

		/// <summary>
		/// True if the path is currently recycled (i.e in the path pool).
		/// Do not set this value. Only read. It is used internally.
		///
		/// Deprecated: Has been renamed to 'pooled' to use more widely underestood terminology
		/// </summary>
		[System.Obsolete("Has been renamed to 'Pooled' to use more widely underestood terminology", true)]
		internal bool recycled { get { return false; } }

		/// <summary>
		/// True if the Reset function has been called.
		/// Used to alert users when they are doing something wrong.
		/// </summary>
		protected bool hasBeenReset;

		/// <summary>Constraint for how to search for nodes</summary>
		public NNConstraint nnConstraint = PathNNConstraint.Default;

		/// <summary>
		/// Internal linked list implementation.
		/// Warning: This is used internally by the system. You should never change this.
		/// </summary>
		internal Path next;

		/// <summary>Determines which heuristic to use</summary>
		public Heuristic heuristic;

		/// <summary>
		/// Scale of the heuristic values.
		/// See: AstarPath.heuristicScale
		/// </summary>
		public float heuristicScale = 1F;

		/// <summary>ID of this path. Used to distinguish between different paths</summary>
		public ushort pathID { get; private set; }

		/// <summary>Target to use for H score calculation. Used alongside <see cref="hTarget"/>.</summary>
		protected GraphNode hTargetNode;

		/// <summary>Target to use for H score calculations. See: Pathfinding.Node.H</summary>
		protected Int3 hTarget;

		/// <summary>
		/// Which graph tags are traversable.
		/// This is a bitmask so -1 = all bits set = all tags traversable.
		/// For example, to set bit 5 to true, you would do
		/// <code> myPath.enabledTags |= 1 << 5; </code>
		/// To set it to false, you would do
		/// <code> myPath.enabledTags &= ~(1 << 5); </code>
		///
		/// The Seeker has a popup field where you can set which tags to use.
		/// Note: If you are using a Seeker. The Seeker will set this value to what is set in the inspector field on StartPath.
		/// So you need to change the Seeker value via script, not set this value if you want to change it via script.
		///
		/// See: CanTraverse
		/// </summary>
		public int enabledTags = -1;

		/// <summary>List of zeroes to use as default tag penalties</summary>
		static readonly int[] ZeroTagPenalties = new int[32];

		/// <summary>
		/// The tag penalties that are actually used.
		/// If manualTagPenalties is null, this will be ZeroTagPenalties
		/// See: tagPenalties
		/// </summary>
		protected int[] internalTagPenalties;

		/// <summary>
		/// Tag penalties set by other scripts
		/// See: tagPenalties
		/// </summary>
		protected int[] manualTagPenalties;

		/// <summary>
		/// Penalties for each tag.
		/// Tag 0 which is the default tag, will have added a penalty of tagPenalties[0].
		/// These should only be positive values since the A* algorithm cannot handle negative penalties.
		///
		/// When assigning an array to this property it must have a length of 32.
		///
		/// Note: Setting this to null, or trying to assign an array which does not have a length of 32, will make all tag penalties be treated as if they are zero.
		///
		/// Note: If you are using a Seeker. The Seeker will set this value to what is set in the inspector field when you call seeker.StartPath.
		/// So you need to change the Seeker's value via script, not set this value.
		///
		/// See: Seeker.tagPenalties
		/// </summary>
		public int[] tagPenalties {
			get {
				return manualTagPenalties;
			}
			set {
				if (value == null || value.Length != 32) {
					manualTagPenalties = null;
					internalTagPenalties = ZeroTagPenalties;
				} else {
					manualTagPenalties = value;
					internalTagPenalties = value;
				}
			}
		}

		/// <summary>
		/// True for paths that want to search all nodes and not jump over nodes as optimizations.
		/// This disables Jump Point Search when that is enabled to prevent e.g ConstantPath and FloodPath
		/// to become completely useless.
		/// </summary>
		internal virtual bool FloodingPath {
			get {
				return false;
			}
		}

		/// <summary>
		/// Total Length of the path.
		/// Calculates the total length of the <see cref="vectorPath"/>.
		/// Cache this rather than call this function every time since it will calculate the length every time, not just return a cached value.
		/// Returns: Total length of <see cref="vectorPath"/>, if <see cref="vectorPath"/> is null positive infinity is returned.
		/// </summary>
		public float GetTotalLength () {
			if (vectorPath == null) return float.PositiveInfinity;
			float tot = 0;
			for (int i = 0; i < vectorPath.Count-1; i++) tot += Vector3.Distance(vectorPath[i], vectorPath[i+1]);
			return tot;
		}

		/// <summary>
		/// Waits until this path has been calculated and returned.
		/// Allows for very easy scripting.
		/// <code>
		/// IEnumerator Start () {
		///  var path = seeker.StartPath(transform.position, transform.position + transform.forward*10, null);
		///  yield return StartCoroutine(path.WaitForPath());
		///  // The path is calculated now
		/// }
		/// </code>
		///
		/// Note: Do not confuse this with AstarPath.WaitForPath. This one will wait using yield until it has been calculated
		/// while AstarPath.WaitForPath will halt all operations until the path has been calculated.
		///
		/// \throws System.InvalidOperationException if the path is not started. Send the path to Seeker.StartPath or AstarPath.StartPath before calling this function.
		///
		/// See: <see cref="BlockUntilCalculated"/>
		/// See: https://docs.unity3d.com/Manual/Coroutines.html
		/// </summary>
		public IEnumerator WaitForPath () {
			if (PipelineState == PathState.Created) throw new System.InvalidOperationException("This path has not been started yet");

			while (PipelineState != PathState.Returned) yield return null;
		}

		/// <summary>
		/// Blocks until this path has been calculated and returned.
		/// Normally it takes a few frames for a path to be calculated and returned.
		/// This function will ensure that the path will be calculated when this function returns
		/// and that the callback for that path has been called.
		///
		/// Use this function only if you really need to.
		/// There is a point to spreading path calculations out over several frames.
		/// It smoothes out the framerate and makes sure requesting a large
		/// number of paths at the same time does not cause lag.
		///
		/// Note: Graph updates and other callbacks might get called during the execution of this function.
		///
		/// <code>
		/// Path p = seeker.StartPath (transform.position, transform.position + Vector3.forward * 10);
		/// p.BlockUntilCalculated();
		/// // The path is calculated now
		/// </code>
		///
		/// See: This is equivalent to calling AstarPath.BlockUntilCalculated(Path)
		/// See: WaitForPath
		/// </summary>
		public void BlockUntilCalculated () {
			AstarPath.BlockUntilCalculated(this);
		}

		/// <summary>
		/// Estimated cost from the specified node to the target.
		/// See: https://en.wikipedia.org/wiki/A*_search_algorithm
		/// </summary>
		internal uint CalculateHScore (GraphNode node) {
			uint h;

			switch (heuristic) {
			case Heuristic.Euclidean:
				h = (uint)(((GetHTarget() - node.position).costMagnitude)*heuristicScale);
				return h;
			case Heuristic.Manhattan:
				Int3 p2 = node.position;
				h = (uint)((System.Math.Abs(hTarget.x-p2.x) + System.Math.Abs(hTarget.y-p2.y) + System.Math.Abs(hTarget.z-p2.z))*heuristicScale);
				return h;
			case Heuristic.DiagonalManhattan:
				Int3 p = GetHTarget() - node.position;
				p.x = System.Math.Abs(p.x);
				p.y = System.Math.Abs(p.y);
				p.z = System.Math.Abs(p.z);
				int diag = System.Math.Min(p.x, p.z);
				int diag2 = System.Math.Max(p.x, p.z);
				h = (uint)((((14*diag)/10) + (diag2-diag) + p.y) * heuristicScale);
				return h;
			}
			return 0U;
		}

		/// <summary>Returns penalty for the given tag.</summary>
		/// <param name="tag">A value between 0 (inclusive) and 32 (exclusive).</param>
		public uint GetTagPenalty (int tag) {
			return (uint)internalTagPenalties[tag];
		}

		internal Int3 GetHTarget () {
			return hTarget;
		}

		/// <summary>
		/// Returns if the node can be traversed.
		/// This per default equals to if the node is walkable and if the node's tag is included in <see cref="enabledTags"/>
		/// </summary>
		internal bool CanTraverse (GraphNode node) {
			// Use traversal provider if set, otherwise fall back on default behaviour
			// This method is hot, but this branch is extremely well predicted so it
			// doesn't affect performance much (profiling indicates it is just above
			// the noise level, somewhere around 0%-0.3%)
			if (traversalProvider != null)
				return traversalProvider.CanTraverse(this, node);

			unchecked { return node.Walkable && (enabledTags >> (int)node.Tag & 0x1) != 0; }
		}

		internal uint GetTraversalCost (GraphNode node) {
#if ASTAR_NO_TRAVERSAL_COST
			return 0;
#else
			// Use traversal provider if set, otherwise fall back on default behaviour
			if (traversalProvider != null)
				return traversalProvider.GetTraversalCost(this, node);

			unchecked { return GetTagPenalty((int)node.Tag) + node.Penalty; }
#endif
		}

		/// <summary>
		/// May be called by graph nodes to get a special cost for some connections.
		/// Nodes may call it when PathNode.flag2 is set to true, for example mesh nodes, which have
		/// a very large area can be marked on the start and end nodes, this method will be called
		/// to get the actual cost for moving from the start position to its neighbours instead
		/// of as would otherwise be the case, from the start node's position to its neighbours.
		/// The position of a node and the actual start point on the node can vary quite a lot.
		///
		/// The default behaviour of this method is to return the previous cost of the connection,
		/// essentiall making no change at all.
		///
		/// This method should return the same regardless of the order of a and b.
		/// That is f(a,b) == f(b,a) should hold.
		/// </summary>
		/// <param name="a">Moving from this node</param>
		/// <param name="b">Moving to this node</param>
		/// <param name="currentCost">The cost of moving between the nodes. Return this value if there is no meaningful special cost to return.</param>
		internal virtual uint GetConnectionSpecialCost (GraphNode a, GraphNode b, uint currentCost) {
			return currentCost;
		}

		/// <summary>
		/// Returns if this path is done calculating.
		///
		/// Note: The callback for the path might not have been called yet.
		///
		/// \since Added in 3.0.8
		///
		/// See: <see cref="Seeker.IsDone"/> which also takes into account if the %path %callback has been called and had modifiers applied.
		/// </summary>
		public bool IsDone () {
			return PipelineState > PathState.Processing;
		}

		/// <summary>Threadsafe increment of the state</summary>
		void IPathInternals.AdvanceState (PathState s) {
			lock (stateLock) {
				PipelineState = (PathState)System.Math.Max((int)PipelineState, (int)s);
			}
		}

		/// <summary>
		/// Returns the state of the path in the pathfinding pipeline.
		/// Deprecated: Use the <see cref="Pathfinding.Path.PipelineState"/> property instead
		/// </summary>
		[System.Obsolete("Use the 'PipelineState' property instead")]
		public PathState GetState () {
			return PipelineState;
		}

		/// <summary>Causes the path to fail and sets <see cref="errorLog"/> to msg</summary>
		internal void FailWithError (string msg) {
			Error();
			if (errorLog != "") errorLog += "\n" + msg;
			else errorLog = msg;
		}

		/// <summary>
		/// Logs an error.
		/// Deprecated: Use <see cref="FailWithError"/> instead
		/// </summary>
		[System.Obsolete("Use FailWithError instead")]
		internal void LogError (string msg) {
			Log(msg);
		}

		/// <summary>
		/// Appends a message to the <see cref="errorLog"/>.
		/// Nothing is logged to the console.
		///
		/// Note: If AstarPath.logPathResults is PathLog.None and this is a standalone player, nothing will be logged as an optimization.
		///
		/// Deprecated: Use <see cref="FailWithError"/> instead
		/// </summary>
		[System.Obsolete("Use FailWithError instead")]
		internal void Log (string msg) {
			errorLog += msg;
		}

		/// <summary>
		/// Aborts the path because of an error.
		/// Sets <see cref="error"/> to true.
		/// This function is called when an error has occurred (e.g a valid path could not be found).
		/// See: <see cref="FailWithError"/>
		/// </summary>
		public void Error () {
			CompleteState = PathCompleteState.Error;
		}

		/// <summary>
		/// Performs some error checking.
		/// Makes sure the user isn't using old code paths and that no major errors have been made.
		///
		/// Causes the path to fail if any errors are found.
		/// </summary>
		private void ErrorCheck () {
			if (!hasBeenReset) FailWithError("Please use the static Construct function for creating paths, do not use the normal constructors.");
			if (((IPathInternals)this).Pooled) FailWithError("The path is currently in a path pool. Are you sending the path for calculation twice?");
			if (pathHandler == null) FailWithError("Field pathHandler is not set. Please report this bug.");
			if (PipelineState > PathState.Processing) FailWithError("This path has already been processed. Do not request a path with the same path object twice.");
		}

		/// <summary>
		/// Called when the path enters the pool.
		/// This method should release e.g pooled lists and other pooled resources
		/// The base version of this method releases vectorPath and path lists.
		/// Reset() will be called after this function, not before.
		/// Warning: Do not call this function manually.
		/// </summary>
		protected virtual void OnEnterPool () {
			if (vectorPath != null) Pathfinding.Util.ListPool<Vector3>.Release (ref vectorPath);
			if (path != null) Pathfinding.Util.ListPool<GraphNode>.Release (ref path);
			// Clear the callback to remove a potential memory leak
			// while the path is in the pool (which it could be for a long time).
			callback = null;
			immediateCallback = null;
			traversalProvider = null;
		}

		/// <summary>
		/// Reset all values to their default values.
		///
		/// Note: All inheriting path types (e.g ConstantPath, RandomPath, etc.) which declare their own variables need to
		/// override this function, resetting ALL their variables to enable pooling of paths.
		/// If this is not done, trying to use that path type for pooling could result in weird behaviour.
		/// The best way is to reset to default values the variables declared in the extended path type and then
		/// call the base function in inheriting types with base.Reset().
		/// </summary>
		protected virtual void Reset () {
#if ASTAR_POOL_DEBUG
			pathTraceInfo = "This path was got from the pool or created from here (stacktrace):\n";
			pathTraceInfo += System.Environment.StackTrace;
#endif

			if (System.Object.ReferenceEquals(AstarPath.active, null))
				throw new System.NullReferenceException("No AstarPath object found in the scene. " +
					"Make sure there is one or do not create paths in Awake");

			hasBeenReset = true;
			PipelineState = (int)PathState.Created;
			releasedNotSilent = false;

			pathHandler = null;
			callback = null;
			immediateCallback = null;
			errorLog = "";
			completeState = PathCompleteState.NotCalculated;

			path = Pathfinding.Util.ListPool<GraphNode>.Claim ();
			vectorPath = Pathfinding.Util.ListPool<Vector3>.Claim ();

			currentR = null;

			duration = 0;
			searchedNodes = 0;

			nnConstraint = PathNNConstraint.Default;
			next = null;

			heuristic = AstarPath.active.heuristic;
			heuristicScale = AstarPath.active.heuristicScale;

			enabledTags = -1;
			tagPenalties = null;

			pathID = AstarPath.active.GetNextPathID();

			hTarget = Int3.zero;
			hTargetNode = null;

			traversalProvider = null;
		}

		/// <summary>List of claims on this path with reference objects</summary>
		private List<System.Object> claimed = new List<System.Object>();

		/// <summary>
		/// True if the path has been released with a non-silent call yet.
		///
		/// See: Release
		/// See: Claim
		/// </summary>
		private bool releasedNotSilent;

		/// <summary>
		/// Claim this path (pooling).
		/// A claim on a path will ensure that it is not pooled.
		/// If you are using a path, you will want to claim it when you first get it and then release it when you will not
		/// use it anymore. When there are no claims on the path, it will be reset and put in a pool.
		///
		/// This is essentially just reference counting.
		///
		/// The object passed to this method is merely used as a way to more easily detect when pooling is not done correctly.
		/// It can be any object, when used from a movement script you can just pass "this". This class will throw an exception
		/// if you try to call Claim on the same path twice with the same object (which is usually not what you want) or
		/// if you try to call Release with an object that has not been used in a Claim call for that path.
		/// The object passed to the Claim method needs to be the same as the one you pass to this method.
		///
		/// See: Release
		/// See: Pool
		/// See: pooling (view in online documentation for working links)
		/// See: https://en.wikipedia.org/wiki/Reference_counting
		/// </summary>
		public void Claim (System.Object o) {
			if (System.Object.ReferenceEquals(o, null)) throw new System.ArgumentNullException("o");

			for (int i = 0; i < claimed.Count; i++) {
				// Need to use ReferenceEquals because it might be called from another thread
				if (System.Object.ReferenceEquals(claimed[i], o))
					throw new System.ArgumentException("You have already claimed the path with that object ("+o+"). Are you claiming the path with the same object twice?");
			}

			claimed.Add(o);
#if ASTAR_POOL_DEBUG
			claimInfo.Add(o.ToString() + "\n\nClaimed from:\n" + System.Environment.StackTrace);
#endif
		}

		/// <summary>
		/// Releases the path silently (pooling).
		/// Deprecated: Use Release(o, true) instead
		/// </summary>
		[System.Obsolete("Use Release(o, true) instead")]
		internal void ReleaseSilent (System.Object o) {
			Release(o, true);
		}

		/// <summary>
		/// Releases a path claim (pooling).
		/// Removes the claim of the path by the specified object.
		/// When the claim count reaches zero, the path will be pooled, all variables will be cleared and the path will be put in a pool to be used again.
		/// This is great for performance since fewer allocations are made.
		///
		/// If the silent parameter is true, this method will remove the claim by the specified object
		/// but the path will not be pooled if the claim count reches zero unless a Release call (not silent) has been made earlier.
		/// This is used by the internal pathfinding components such as Seeker and AstarPath so that they will not cause paths to be pooled.
		/// This enables users to skip the claim/release calls if they want without the path being pooled by the Seeker or AstarPath and
		/// thus causing strange bugs.
		///
		/// See: Claim
		/// See: PathPool
		/// </summary>
		public void Release (System.Object o, bool silent = false) {
			if (o == null) throw new System.ArgumentNullException("o");

			for (int i = 0; i < claimed.Count; i++) {
				// Need to use ReferenceEquals because it might be called from another thread
				if (System.Object.ReferenceEquals(claimed[i], o)) {
					claimed.RemoveAt(i);
#if ASTAR_POOL_DEBUG
					claimInfo.RemoveAt(i);
#endif
					if (!silent) {
						releasedNotSilent = true;
					}

					if (claimed.Count == 0 && releasedNotSilent) {
						PathPool.Pool(this);
					}
					return;
				}
			}
			if (claimed.Count == 0) {
				throw new System.ArgumentException("You are releasing a path which is not claimed at all (most likely it has been pooled already). " +
					"Are you releasing the path with the same object ("+o+") twice?" +
					"\nCheck out the documentation on path pooling for help.");
			}
			throw new System.ArgumentException("You are releasing a path which has not been claimed with this object ("+o+"). " +
				"Are you releasing the path with the same object twice?\n" +
				"Check out the documentation on path pooling for help.");
		}

		/// <summary>
		/// Traces the calculated path from the end node to the start.
		/// This will build an array (<see cref="path)"/> of the nodes this path will pass through and also set the <see cref="vectorPath"/> array to the <see cref="path"/> arrays positions.
		/// Assumes the <see cref="vectorPath"/> and <see cref="path"/> are empty and not null (which will be the case for a correctly initialized path).
		/// </summary>
		protected virtual void Trace (PathNode from) {
			// Current node we are processing
			PathNode c = from;
			int count = 0;

			while (c != null) {
				c = c.parent;
				count++;
				if (count > 2048) {
					Debug.LogWarning("Infinite loop? >2048 node path. Remove this message if you really have that long paths (Path.cs, Trace method)");
					break;
				}
			}

			// Ensure capacities for lists
			AstarProfiler.StartProfile("Check List Capacities");

			if (path.Capacity < count) path.Capacity = count;
			if (vectorPath.Capacity < count) vectorPath.Capacity = count;

			AstarProfiler.EndProfile();

			c = from;

			for (int i = 0; i < count; i++) {
				path.Add(c.node);
				c = c.parent;
			}

			// Reverse
			int half = count/2;
			for (int i = 0; i < half; i++) {
				var tmp = path[i];
				path[i] = path[count-i-1];
				path[count - i - 1] = tmp;
			}

			for (int i = 0; i < count; i++) {
				vectorPath.Add((Vector3)path[i].position);
			}
		}

		/// <summary>
		/// Writes text shared for all overrides of DebugString to the string builder.
		/// See: DebugString
		/// </summary>
		protected void DebugStringPrefix (PathLog logMode, System.Text.StringBuilder text) {
			text.Append(error ? "Path Failed : " : "Path Completed : ");
			text.Append("Computation Time ");
			text.Append(duration.ToString(logMode == PathLog.Heavy ? "0.000 ms " : "0.00 ms "));

			text.Append("Searched Nodes ").Append(searchedNodes);

			if (!error) {
				text.Append(" Path Length ");
				text.Append(path == null ? "Null" : path.Count.ToString());
			}
		}

		/// <summary>
		/// Writes text shared for all overrides of DebugString to the string builder.
		/// See: DebugString
		/// </summary>
		protected void DebugStringSuffix (PathLog logMode, System.Text.StringBuilder text) {
			if (error) {
				text.Append("\nError: ").Append(errorLog);
			}

			// Can only print this from the Unity thread
			// since otherwise an exception might be thrown
			if (logMode == PathLog.Heavy && !AstarPath.active.IsUsingMultithreading) {
				text.Append("\nCallback references ");
				if (callback != null) text.Append(callback.Target.GetType().FullName).AppendLine();
				else text.AppendLine("NULL");
			}

			text.Append("\nPath Number ").Append(pathID).Append(" (unique id)");
		}

		/// <summary>
		/// Returns a string with information about it.
		/// More information is emitted when logMode == Heavy.
		/// An empty string is returned if logMode == None
		/// or logMode == OnlyErrors and this path did not fail.
		/// </summary>
		internal virtual string DebugString (PathLog logMode) {
			if (logMode == PathLog.None || (!error && logMode == PathLog.OnlyErrors)) {
				return "";
			}

			// Get a cached string builder for this thread
			System.Text.StringBuilder text = pathHandler.DebugStringBuilder;
			text.Length = 0;

			DebugStringPrefix(logMode, text);
			DebugStringSuffix(logMode, text);

			return text.ToString();
		}

		/// <summary>Calls callback to return the calculated path. See: <see cref="callback"/></summary>
		protected virtual void ReturnPath () {
			if (callback != null) {
				callback(this);
			}
		}

		/// <summary>
		/// Prepares low level path variables for calculation.
		/// Called before a path search will take place.
		/// Always called before the Prepare, Initialize and CalculateStep functions
		/// </summary>
		protected void PrepareBase (PathHandler pathHandler) {
			//Path IDs have overflowed 65K, cleanup is needed
			//Since pathIDs are handed out sequentially, we can do this
			if (pathHandler.PathID > pathID) {
				pathHandler.ClearPathIDs();
			}

			//Make sure the path has a reference to the pathHandler
			this.pathHandler = pathHandler;
			//Assign relevant path data to the pathHandler
			pathHandler.InitializeForPath(this);

			// Make sure that internalTagPenalties is an array which has the length 32
			if (internalTagPenalties == null || internalTagPenalties.Length != 32)
				internalTagPenalties = ZeroTagPenalties;

			try {
				ErrorCheck();
			} catch (System.Exception e) {
				FailWithError(e.Message);
			}
		}

		/// <summary>
		/// Called before the path is started.
		/// Called right before Initialize
		/// </summary>
		protected abstract void Prepare ();

		/// <summary>
		/// Always called after the path has been calculated.
		/// Guaranteed to be called before other paths have been calculated on
		/// the same thread.
		/// Use for cleaning up things like node tagging and similar.
		/// </summary>
		protected virtual void Cleanup () {}

		/// <summary>
		/// Initializes the path.
		/// Sets up the open list and adds the first node to it
		/// </summary>
		protected abstract void Initialize ();

		/// <summary>Calculates the until it is complete or the time has progressed past targetTick</summary>
		protected abstract void CalculateStep (long targetTick);

		PathHandler IPathInternals.PathHandler { get { return pathHandler; } }
		void IPathInternals.OnEnterPool () { OnEnterPool(); }
		void IPathInternals.Reset () { Reset(); }
		void IPathInternals.ReturnPath () { ReturnPath(); }
		void IPathInternals.PrepareBase (PathHandler handler) { PrepareBase(handler); }
		void IPathInternals.Prepare () { Prepare(); }
		void IPathInternals.Cleanup () { Cleanup(); }
		void IPathInternals.Initialize () { Initialize(); }
		void IPathInternals.CalculateStep (long targetTick) { CalculateStep(targetTick); }
	}

	/// <summary>Used for hiding internal methods of the Path class</summary>
	internal interface IPathInternals {
		PathHandler PathHandler { get; }
		bool Pooled { get; set; }
		void AdvanceState (PathState s);
		void OnEnterPool ();
		void Reset ();
		void ReturnPath ();
		void PrepareBase (PathHandler handler);
		void Prepare ();
		void Initialize ();
		void Cleanup ();
		void CalculateStep (long targetTick);
	}
}
