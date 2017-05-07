//#define ASTAR_POOL_DEBUG //Enables debugging of path pooling. Will log warnings and info messages about paths not beeing pooled correctly.

using UnityEngine;
using System.Collections;
using Pathfinding;
using System.Collections.Generic;

namespace Pathfinding {
	/** Base class for all path types */
	public abstract class Path {
	
		
		/** Data for the thread calculating this path */
		public PathHandler pathHandler;
		
		/** Callback to call when the path is complete.
		 * This is usually sent to the Seeker component which post processes the path and then calls a callback to the script which requested the path 
		*/
		public OnPathDelegate callback;
		
		
		private PathState state;
		private System.Object stateLock = new object();
		
		/** Current state of the path.
		 * \see #CompleteState
		 */
		private PathCompleteState pathCompleteState;
		
		/** Current state of the path */
		public PathCompleteState CompleteState {
			get { return pathCompleteState; }
			protected set { pathCompleteState = value; }
		}
		
		/** If the path failed, this is true.
		  * \see #errorLog */
		public bool error { get { return CompleteState == PathCompleteState.Error; }}
		
		/** Additional info on what went wrong.
		  * \see #error */
		private string _errorLog = "";
		
		/** Log messages with info about eventual errors. */
		public string errorLog {
			get { return _errorLog; }
		}
		
		private GraphNode[] _path;
		private Vector3[] _vectorPath;
		
		/** Holds the path as a Node array. All nodes the path traverses. This might not be the same as all nodes the smoothed path traverses. */
		public List<GraphNode> path;
		
		/** Holds the (perhaps post processed) path as a Vector3 array */
		public List<Vector3> vectorPath;
		
		/** The max number of milliseconds per iteration (frame, in case of non-multithreading) */
		protected float maxFrameTime;
		
		/** The node currently being processed */
		protected PathNode currentR;
		
		public float duration;			/**< The duration of this path in ms. How long it took to calculate the path */
		
		/**< The number of frames/iterations this path has executed.
		 * This is the number of frames when not using multithreading.
		 * When using multithreading, this value is quite irrelevant
		 */
		public int searchIterations = 0;
		public int searchedNodes;		/**< Number of nodes this path has searched */
		
		/** When the call was made to start the pathfinding for this path */
		public System.DateTime callTime;
		
		/* True if the path has been calculated (even if it had an error).
		 * Used by the multithreaded pathfinder to signal that this path object is safe to return. */
		//public bool processed = false;
		
		/** True if the path is currently recycled (i.e in the path pool).
		 * Do not set this value. Only read. It is used internally.
		 */
		public bool recycled = false;
		
		/** True if the Reset function has been called.
		 * Used to allert users when they are doing it wrong.
		 */
		protected bool hasBeenReset = false;
		
		/** Constraint for how to search for nodes */
		public NNConstraint nnConstraint = PathNNConstraint.Default;
		
		/** The next path to be searched.
		 * Linked list implementation.
		 * \warning You should never change this if you do not know what you are doing
		 */
		public Path next;
		
		//These are variables which different scripts and custom graphs can use to get a bit more info about What is searching
		//Not all are used in the standard graph types
		//These variables are put here because it is a lot faster to access fields than, for example make use of a lookup table (e.g dictionary)
		//Note: These variables needs to be filled in by an external script to be usable
		/** Radius for the unit searching for the path.
		 * \note Not used by any built-in pathfinders.
		 * These common name variables are put here because it is a lot faster to access fields than, for example make use of a lookup table (e.g dictionary).
		 * Or having to cast to another path type for acess.
		 */
		public int radius;
		
		/** A mask for defining what type of ground a unit can traverse, not used in any default standard graph. \see #enabledTags
		  * \note Not used by any built-in pathfinders.
		 * These variables are put here because it is a lot faster to access fields than, for example make use of a lookup table (e.g dictionary)
		 */
		public int walkabilityMask = -1;
		
		/** Height of the character. Not used currently */
		public int height;
		
		/** Turning radius of the character. Not used currently */
		public int turnRadius;
		
		/** Speed of the character. Not used currently */
		public int speed;
		
		/* To store additional data. Note: this is SLOW. About 10-100 times slower than using the fields above.
		 * \since Removed in 3.0.8
		  * Currently not used */
		//public Dictionary<string,int> customData = null;//new Dictionary<string,int>();
		
		/** Determines which heuristic to use */
		public Heuristic heuristic;
		
		/** Scale of the heuristic values */
		public float heuristicScale = 1F;
		
		/** ID of this path. Used to distinguish between different paths */
		public ushort pathID;
		
		protected Int3 hTarget; /**< Target to use for H score calculations. \see Pathfinding.Node.H */
		
		/** Which graph tags are traversable.
		 * This is a bitmask so -1 = all bits set = all tags traversable.
		 * For example, to set bit 5 to true, you would do
		 * \code myPath.enabledTags |= 1 << 5; \endcode
		 * To set it to false, you would do
		 * \code myPath.enabledTags &= ~(1 << 5); \endcode
		 * 
		 * The Seeker has a popup field where you can set which tags to use.
		 * \note If you are using a Seeker. The Seeker will set this value to what is set in the inspector field on StartPath.
		 * So you need to change the Seeker value via script, not set this value if you want to change it via script.
		 * 
		 * \see CanTraverse
		 */
		public int enabledTags = -1;
		
		/** Penalties for each tag.
		 */
		protected int[] _tagPenalties = new int[0];
		
		/** Penalties for each tag.
		 * Tag 0 which is the default tag, will have added a penalty of tagPenalties[0].
		 * These should only be positive values since the A* algorithm cannot handle negative penalties.
		 * \note This array will never be null. If you try to set it to null or with a lenght which is not 32. It will be set to "new int[0]".
		 * 
		 * \note If you are using a Seeker. The Seeker will set this value to what is set in the inspector field on StartPath.
		 * So you need to change the Seeker value via script, not set this value if you want to change it via script.
		 * 
		 * \see Seeker.tagPenalties
		 */
		public int[] tagPenalties {
			get {
				return _tagPenalties;
			}
			set {
				if (value == null || value.Length != 32) _tagPenalties = new int[0];
				else _tagPenalties = value;
			}
		}
		
		/** Total Length of the path.
		 * Calculates the total length of the #vectorPath.
		 * Cache this rather than call this function every time since it will calculate the length every time, not just return a cached value.
		 * \returns Total length of #vectorPath, if #vectorPath is null positive infinity is returned.
		 */
		public float GetTotalLength () {
			if (vectorPath == null) return float.PositiveInfinity;
			float tot = 0;
			for (int i=0;i<vectorPath.Count-1;i++) tot += Vector3.Distance (vectorPath[i],vectorPath[i+1]);
			return tot;
		}
		
		/** Waits until this path has been calculated and returned.
		 * Allows for very easy scripting.
\code
//In an IEnumerator function

Path p = Seeker.StartPath (transform.position, transform.position + Vector3.forward * 10);
yield return StartCoroutine (p.WaitForPath ());

//The path is calculated at this stage
\endcode
		 * \note Do not confuse this with AstarPath.WaitForPath. This one will wait using yield until it has been calculated
		 * while AstarPath.WaitForPath will halt all operations until the path has been calculated.
		 * 
		 * \throws System.InvalidOperationException if the path is not started. Send the path to Seeker.StartPath or AstarPath.StartPath before calling this function.
		 * 
		 * \see AstarPath.WaitForPath
		 */
		public IEnumerator WaitForPath () {
			if (GetState () == PathState.Created) throw new System.InvalidOperationException ("This path has not been started yet");
			
			while (GetState () != PathState.Returned) yield return null;
		}
		
		public uint CalculateHScore (GraphNode node) {
			switch (heuristic) {
			case Heuristic.Euclidean:
				return (uint)(((GetHTarget () - node.position).costMagnitude)*heuristicScale);
			case Heuristic.Manhattan:
				Int3 p2 = node.position;
				return (uint)((System.Math.Abs (hTarget.x-p2.x) + System.Math.Abs (hTarget.y-p2.y) + System.Math.Abs (hTarget.z-p2.z))*heuristicScale);
			case Heuristic.DiagonalManhattan:
				Int3 p = GetHTarget () - node.position;
				p.x = System.Math.Abs (p.x);
				p.y = System.Math.Abs (p.y);
				p.z = System.Math.Abs (p.z);
				int diag = System.Math.Min (p.x,p.z);
				int diag2 = System.Math.Max (p.x,p.z);
				return (uint)((((14*diag)/10) + (diag2-diag) + p.y) * heuristicScale);
			}
			return 0U;
		}
		
		/** Returns penalty for the given tag.
		 * \param tag A value between 0 (inclusive) and 31 (inclusive).
		 */
		public uint GetTagPenalty (int tag) {
			return tag < _tagPenalties.Length ? (uint)_tagPenalties[tag] : 0;
		}
		
		public Int3 GetHTarget () {
			return hTarget;
		}
		
		/** Returns if the node can be traversed.
		  * This per default equals to if the node is walkable and if the node's tag is included in #enabledTags */
		public bool CanTraverse (GraphNode node) {
			unchecked { return node.Walkable && (enabledTags >> (int)node.Tag & 0x1) != 0; }
		}
		
		public uint GetTraversalCost (GraphNode node) {
			unchecked { return GetTagPenalty ((int)node.Tag ) + node.Penalty; }
		}
		
		/** May be called by graph nodes to get a special cost for some connections.
		 * Nodes may call it when PathNode.flag2 is set to true, for example mesh nodes, which have 
		 * a very large area can be marked on the start and end nodes, this method will be called
		 * to get the actual cost for moving from the start position to its neighbours instead
		 * of as would otherwise be the case, from the start node's position to its neighbours.
		 * The position of a node and the actual start point on the node can vary quite a lot.
		 * 
		 * The default behaviour of this method is to return the previous cost of the connection,
		 * essentiall making no change at all.
		 * 
		 * This method should return the same regardless of the order of a and b.
		 * That is f(a,b) == f(b,a) should hold.
		 * 
		 * \param a Moving from this node
		 * \param b Moving to this node
		 * \param currentCost The cost of moving between the nodes. Return this value if there is no meaningful special cost to return.
		 */
		public virtual uint GetConnectionSpecialCost (GraphNode a, GraphNode b, uint currentCost) {
			return currentCost;
		}
		
		/** Returns if this path is done calculating.
		 * \returns If CompleteState is not PathCompleteState.NotCalculated.
		 * 
		 * \note The path might not have been returned yet.
		 * 
		 * \since Added in 3.0.8
		 * 
		 * \see Seeker.IsDone
		 */
		public bool IsDone () {
			return CompleteState != PathCompleteState.NotCalculated;
		}
		
		/** Threadsafe increment of the state */
		public void AdvanceState (PathState s) {

			lock (stateLock) {
				state = (PathState)System.Math.Max ((int)state, (int)s);
			}
		}
		
		/** Returns the state of the path in the pathfinding pipeline */
		public PathState GetState () {
			return (PathState)state;
		}
		
		/** Appends \a msg to #errorLog and logs \a msg to the console.
		 * Debug.Log call is only made if AstarPath.logPathResults is not equal to None and not equal to InGame.
		 * Consider calling Error() along with this call.
		 */
// Ugly Code Inc. wrote the below code :D
// What it does is that it disables the LogError function if ASTAR_NO_LOGGING is enabled
// since the DISABLED define will never be enabled
// Ugly way of writing Conditional("!ASTAR_NO_LOGGING")
		public void LogError (string msg) {
			// Optimize for release builds
			if (!(!AstarPath.isEditor && AstarPath.active.logPathResults == PathLog.None)) {
				_errorLog += msg;
			}
			
			if (AstarPath.active.logPathResults != PathLog.None && AstarPath.active.logPathResults != PathLog.InGame) {
				Debug.LogWarning (msg);
			}
		}
		
		/** Logs an error and calls Error() to true.
		 * This is called only if something is very wrong or the user is doing something he/she really should not be doing.
		 */
		public void ForceLogError (string msg) {
			Error();
			
			_errorLog += msg;
			
			Debug.LogError (msg);
		}
		
		/** Appends a message to the #errorLog.
		  * Nothing is logged to the console.
		  * 
		  * \note If AstarPath.logPathResults is PathLog.None and this is a standalone player, nothing will be logged as an optimization.
		  */
		public void Log (string msg) {
			// Optimize for release builds
			if (!(!AstarPath.isEditor && AstarPath.active.logPathResults == PathLog.None)) {
				_errorLog += msg;
			}
		}
		
		/** Aborts the path because of an error.
		 * Sets #error to true.
		 * This function is called when an error has ocurred (e.g a valid path could not be found).
		 * \see LogError
		 */
		public void Error () {
			CompleteState = PathCompleteState.Error;
		}
		
		/** Does some error checking.
		 * Makes sure the user isn't using old code paths and that no major errors have been done.
		 * 
		 * \throws An exception if any errors are found
		 */
		private void ErrorCheck () {
			if (!hasBeenReset) throw new System.Exception ("The path has never been reset. Use pooling API or call Reset() after creating the path with the default constructor.");
			if (recycled) throw new System.Exception ("The path is currently in a path pool. Are you sending the path for calculation twice?");
			if (pathHandler == null) throw new System.Exception ("Field pathHandler is not set. Please report this bug.");
			if (GetState() > PathState.Processing) throw new System.Exception ("This path has already been processed. Do not request a path with the same path object twice.");
		}
		
		/** Called when the path enters the pool.
		 * This method should release e.g pooled lists and other pooled resources
		 * The base version of this method releases vectorPath and path lists.
		 * Reset() will be called after this function, not before.
		 * \warning Do not call this function manually.
		 */
		public virtual void OnEnterPool () {
			if (vectorPath != null) Pathfinding.Util.ListPool<Vector3>.Release (vectorPath);
			if (path != null) Pathfinding.Util.ListPool<GraphNode>.Release (path);
			vectorPath = null;
			path = null;
		}
		
		/** Reset all values to their default values.
		 * 
		 * \note All inheriting path types (e.g ConstantPath, RandomPath, etc.) which declare their own variables need to
		 * override this function, resetting ALL their variables to enable recycling of paths.
		 * If this is not done, trying to use that path type for pooling might result in weird behaviour.
		 * The best way is to reset to default values the variables declared in the extended path type and then
		 * call this base function in inheriting types with base.Reset ().
		 * 
		 * \warning This function should not be called manually.
		  */
		public virtual void Reset () {
				
			if (AstarPath.active == null)
				throw new System.NullReferenceException ("No AstarPath object found in the scene. " +
					"Make sure there is one or do not create paths in Awake");
			
			hasBeenReset = true;
			state = (int)PathState.Created;
			releasedNotSilent = false;
			
			pathHandler = null;
			callback = null;
			_errorLog = "";
			pathCompleteState = PathCompleteState.NotCalculated;
			
			path = Pathfinding.Util.ListPool<GraphNode>.Claim();
			vectorPath = Pathfinding.Util.ListPool<Vector3>.Claim();
			
			currentR = null;
			
			duration = 0;
			searchIterations = 0;
			searchedNodes = 0;
			//calltime
			
			nnConstraint = PathNNConstraint.Default;
			next = null;
			
			radius = 0;
			walkabilityMask = -1;
			height = 0;
			turnRadius = 0;
			speed = 0;
			
			//heuristic = (Heuristic)0;
			//heuristicScale = 1F;
			heuristic = AstarPath.active.heuristic;
			heuristicScale = AstarPath.active.heuristicScale;
			
			pathID = 0;
			enabledTags = -1;
			tagPenalties = null;
			
			callTime = System.DateTime.UtcNow;
			pathID = AstarPath.active.GetNextPathID ();
			
			hTarget = Int3.zero;
		}
		
		protected bool HasExceededTime (int searchedNodes, long targetTime) {
			return System.DateTime.UtcNow.Ticks >= targetTime;
		}
		
		/** Recycle the path.
		 * Calling this means that the path and any variables on it are not needed anymore and the path can be pooled.
		 * All path data will be reset.
		 * Implement this in inheriting path types to support recycling of paths.
\code
public override void Recycle () {
	//Recycle the Path (<Path> should be replaced by the path type it is implemented in)
	PathPool<Path>.Recycle (this);
}
\endcode
		 * 
		 * \warning Do not call this function directly, instead use the #Claim and #Release functions.
		 * \see Pathfinding.PathPool
		 * \see Reset
		 * \see Claim
		 * \see Release
		 */
		protected abstract void Recycle ();// {
		//	PathPool<Path>.Recycle (this);
		//}
		
		/** List of claims on this path with reference objects */
		private List<System.Object> claimed = new List<System.Object>();
		
		/** True if the path has been released with a non-silent call yet.
		 * 
		 * \see Release
		 * \see ReleaseSilent
		 * \see Claim
		 */
		private bool releasedNotSilent = false;
		
		/** Claim this path.
		 * A claim on a path will ensure that it is not recycled.
		 * If you are using a path, you will want to claim it when you first get it and then release it when you will not
		 * use it anymore. When there are no claims on the path, it will be recycled and put in a pool.
		 * 
		 * \see Release
		 * \see Recycle
		 */
		public void Claim (System.Object o) {
			if (o == null) throw new System.ArgumentNullException ("o");

			if (claimed.Contains (o)) {
				throw new System.ArgumentException ("You have already claimed the path with that object ("+o.ToString()+"). Are you claiming the path with the same object twice?");
			}
			
			claimed.Add (o);
		}
		
		/** Releases the path silently.
		 * This will remove the claim by the specified object, but the path will not be recycled if the claim count reches zero unless a Release call (not silent) has been made earlier.
		 * This is used by the internal pathfinding components such as Seeker and AstarPath so that they will not recycle paths.
		 * This enables users to skip the claim/release calls if they want without the path being recycled by the Seeker or AstarPath.
		 */
		public void ReleaseSilent (System.Object o) {

			if (o == null) throw new System.ArgumentNullException ("o");
			
			for (int i=0;i<claimed.Count;i++) {
				if (claimed[i] == o) {
					claimed.RemoveAt (i);
					if (releasedNotSilent && claimed.Count == 0) {
						Recycle ();
					}
					return;
				}
			}
			if (claimed.Count == 0) {
				throw new System.ArgumentException ("You are releasing a path which is not claimed at all (most likely it has been pooled already). " +
					"Are you releasing the path with the same object ("+o.ToString()+") twice?");
			} else {
				throw new System.ArgumentException ("You are releasing a path which has not been claimed with this object ("+o.ToString()+"). " +
					"Are you releasing the path with the same object twice?");
			}
		}
		
		/** Releases a path claim.
		 * Removes the claim of the path by the specified object.
		 * When the claim count reaches zero, the path will be recycled, all variables will be cleared and the path will be put in a pool to be used again.
		 * This is great for memory since less allocations are made.
		 * \see Claim
		 */
		public void Release (System.Object o) {
			if (o == null) throw new System.ArgumentNullException ("o");

			for (int i=0;i<claimed.Count;i++) {
				if (claimed[i] == o) {
					claimed.RemoveAt (i);
					releasedNotSilent = true;
					if (claimed.Count == 0) {
						Recycle ();
					}
					return;
				}
			}
			if (claimed.Count == 0) {
				throw new System.ArgumentException ("You are releasing a path which is not claimed at all (most likely it has been pooled already). " +
					"Are you releasing the path with the same object ("+o.ToString()+") twice?");
			} else {
				throw new System.ArgumentException ("You are releasing a path which has not been claimed with this object ("+o.ToString()+"). " +
					"Are you releasing the path with the same object twice?");
			}
		}
		
		/** Traces the calculated path from the end node to the start.
		 * This will build an array (#path) of the nodes this path will pass through and also set the #vectorPath array to the #path arrays positions.
		 * Assumes the #vectorPath and #path are empty and not null (which will be the case for a correctly initialized path).
		 */
		protected virtual void Trace (PathNode from) {
			
			int count = 0;
			
			PathNode c = from;
			while (c != null) {
				c = c.parent;
				count++;
				if (count > 1024) {
					Debug.LogWarning ("Inifinity loop? >1024 node path. Remove this message if you really have that long paths (Path.cs, Trace function)");
					break;
				}
			}
			
			//Ensure capacities for lists
			AstarProfiler.StartProfile ("Check List Capacities");
			
			if (path.Capacity < count) path.Capacity = count;
			if (vectorPath.Capacity < count) vectorPath.Capacity = count;
			
			AstarProfiler.EndProfile ();
			
			c = from;
			
			for (int i = 0;i<count;i++) {
				path.Add (c.node);
				c = c.parent;
			}
			
			int half = count/2;
			for (int i=0;i<half;i++) {
				GraphNode tmp = path[i];
				path[i] = path[count-i-1];
				path[count - i - 1] = tmp;
			}
			
			for (int i=0;i<count;i++) {
				vectorPath.Add ((Vector3)path[i].position);
			}
		}
		
		/** Returns a debug string for this path.
		 */
		public virtual string DebugString (PathLog logMode) {
			
			if (logMode == PathLog.None || (!error && logMode == PathLog.OnlyErrors)) {
				return "";
			}
			
			//debugStringBuilder.Length = 0;
			
			System.Text.StringBuilder text = pathHandler.DebugStringBuilder;
			text.Length = 0;
			
			text.Append (error ? "Path Failed : " : "Path Completed : ");
			text.Append ("Computation Time ");
			
			text.Append ((duration).ToString (logMode == PathLog.Heavy ? "0.000 ms " : "0.00 ms "));
			text.Append ("Searched Nodes ");
			text.Append (searchedNodes);
			
			if (!error) {
				text.Append (" Path Length ");
				text.Append (path == null ? "Null" : path.Count.ToString ());
				
				if (logMode == PathLog.Heavy) {
					text.Append ("\nSearch Iterations "+searchIterations);
					
					//text.Append ("\nBinary Heap size at complete: ");
					
					// -2 because numberOfItems includes the next item to be added and item zero is not used
					//text.Append (pathHandler.open == null ? "null" : (pathHandler.open.numberOfItems-2).ToString ());
				}
				
				/*"\nEnd node\n	G = "+p.endNode.g+"\n	H = "+p.endNode.h+"\n	F = "+p.endNode.f+"\n	Point	"+p.endPoint
				+"\nStart Point = "+p.startPoint+"\n"+"Start Node graph: "+p.startNode.graphIndex+" End Node graph: "+p.endNode.graphIndex+
				"\nBinary Heap size at completion: "+(p.open == null ? "Null" : p.open.numberOfItems.ToString ())*/
			}
			
			if (error) {
				text.Append ("\nError: ");
				text.Append (errorLog);
			}
			
			if (logMode == PathLog.Heavy && !AstarPath.IsUsingMultithreading ) {
				text.Append ("\nCallback references ");
				if (callback != null) text.Append(callback.Target.GetType().FullName).AppendLine();
				else text.AppendLine ("NULL");
			}
			
			text.Append ("\nPath Number ");
			text.Append (pathID);
			
			return text.ToString ();
		}
		
		/** Calls callback to return the calculated path. \see #callback */
		public virtual void ReturnPath () {
			if (callback != null) {
				callback (this);
			}
		}
		
		/** Prepares low level path variables for calculation.
		  * Called before a path search will take place.
		  * Always called before the Prepare, Initialize and CalculateStep functions
		  */
		public void PrepareBase (PathHandler pathHandler) {
			
			//Path IDs have overflowed 65K, cleanup is needed
			//Since pathIDs are handed out sequentially, we can do this
			if (pathHandler.PathID > pathID) {
				pathHandler.ClearPathIDs ();
			}
			
			//Make sure the path has a reference to the pathHandler
			this.pathHandler = pathHandler;
			//Assign relevant path data to the pathHandler
			pathHandler.InitializeForPath (this);

			try {
				ErrorCheck ();
			} catch (System.Exception e) {
				ForceLogError ("Exception in path "+pathID+"\n"+e.ToString());
			}
		}
		
		public abstract void Prepare ();
		
		/** Always called after the path has been calculated.
		 * Guaranteed to be called before other paths have been calculated on
		 * the same thread.
		 * Use for cleaning up things like node tagging and similar.
		 */
		public virtual void Cleanup () {}
		
		/** Initializes the path.
		 * Sets up the open list and adds the first node to it
		 */
		public abstract void Initialize ();
		
		/** Calculates the path until time has gone past \a targetTick */
		public abstract void CalculateStep (long targetTick);
	}
}