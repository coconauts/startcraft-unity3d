//Define optimizations is a A* Pathfinding Project Pro only feature
//#define ProfileAstar	//Enables profiling of the pathfinding process
//#define ASTARDEBUG			//Enables more debugging messages, enable if this script is behaving weird (crashing or throwing NullReference exceptions or something)
//#define NoGUI			//Disables the use of the OnGUI function, can possibly improve performance by a tiny bit (disables the InGame option for path debugging)

//#define ASTAR_FAST_NO_EXCEPTIONS

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Pathfinding;

using Thread = System.Threading.Thread;
using ParameterizedThreadStart = System.Threading.ParameterizedThreadStart;

// Note sure why anyone would want to use this...
//#define ASTAR_MORE_PATH_IDS //Increases the number of pathIDs from 2^16 to 2^32. Uses more memory

[AddComponentMenu ("Pathfinding/Pathfinder")]
/** Main Pathfinding System.
 * This class handles all the pathfinding system, calculates all paths and stores the info.\n
 * This class is a singleton class, meaning it should only exist at most one active instance of it in the scene.\n
 * It might be a bit hard to use directly, usually interfacing with the pathfinding system is done through the Seeker class.
 * 
 * \nosubgrouping
 * \ingroup relevant */
public class AstarPath : MonoBehaviour {
	
	/** The version number for the A* %Pathfinding Project
	 */
	public static System.Version Version {
		get {
			return new System.Version (3,5,2);
		}
	}
	
	public enum AstarDistribution { WebsiteDownload, AssetStore };
	
	/** Used by the editor to guide the user to the correct place to download updates */
	public static readonly AstarDistribution Distribution = AstarDistribution.WebsiteDownload;

	/** Which branch of the A* Pathfinding Project is this release.
	 * Used when checking for updates so that
	 * users of the development versions can get notifications of development
	 * updates.
	 */
	public static readonly string Branch = "isometric_Free";

	/** Used by the editor to show some Pro specific stuff.
	 * Note that setting this to true will not grant you any additional features */
	public static readonly bool HasPro = false;
	
	/** See Pathfinding.AstarData */
	public System.Type[] graphTypes {
		get {
			return astarData.graphTypes;
		}
	}
	
	/** Reference to the Pathfinding.AstarData object for this graph. The AstarData object stores information about all graphs. */
	public AstarData astarData;
	
	/** Returns the active AstarPath object in the scene.*/
	public new static AstarPath active;
	
	/** Shortcut to Pathfinding.AstarData.graphs */
	public NavGraph[] graphs {
		get {
			if (astarData == null)
				astarData = new AstarData ();
			return astarData.graphs;
		}
		set {
			if (astarData == null)
				astarData = new AstarData ();
			astarData.graphs = value;
		}
	}
	
#region InspectorDebug
	/** @name Inspector - Debug
	 * @{ */
	
	/** Toggle for showing the gizmo debugging for the graphs in the scene view (editor only). */
	public bool showNavGraphs = true;
	
	/** Toggle to show unwalkable nodes.
	  * \see unwalkableNodeDebugSize */
	public bool showUnwalkableNodes = true;
	
	/** The mode to use for drawing nodes in the sceneview.
	 * \see Pathfinding.GraphDebugMode
	 */
	public GraphDebugMode debugMode;
	
	/** Low value to use for certain #debugMode modes.
	 * For example if #debugMode is set to G, this value will determine when the node will be totally red.
	 * \see #debugRoof
	 */
	public float debugFloor = 0;
	
	/** High value to use for certain #debugMode modes.
	 * For example if #debugMode is set to G, this value will determine when the node will be totally green.
	 * \see #debugFloor
	 */
	public float debugRoof = 20000;
	
	/** If enabled, nodes will draw a line to their 'parent'.
	 * This will show the search tree for the latest path. This is editor only.
	 * \todo Add a showOnlyLastPath flag to indicate whether to draw every node or only the ones visited by the latest path.
	 */
	public bool	showSearchTree = false;
	
	/** Size of the red cubes shown in place of unwalkable nodes.
	  * \see showUnwalkableNodes */
	public float unwalkableNodeDebugSize = 0.3F;
	
	/** The amount of debugging messages.
	 * Use less debugging to improve performance (a bit) or just to get rid of the Console spamming.\n
	 * Use more debugging (heavy) if you want more information about what the pathfinding is doing.\n
	 * InGame will display the latest path log using in game GUI. */
	public PathLog logPathResults = PathLog.Normal;
	
	/** @} */
#endregion
	
#region InspectorSettings
	/** @name Inspector - Settings
	 * @{ */
	
	/** Max Nearest Node Distance.
	 * When searching for a nearest node, this is the limit (world units) for how far away it is allowed to be.
	 * \see Pathfinding.NNConstraint.constrainDistance
	 */
	public float maxNearestNodeDistance = 100;
	
	/** Max Nearest Node Distance Squared.
	 * \see #maxNearestNodeDistance */
	public float maxNearestNodeDistanceSqr {
		get { return maxNearestNodeDistance*maxNearestNodeDistance; }
	}
	
	/** If true, all graphs will be scanned in Awake.
	 * This does not include loading from the cache.
	 * If you disable this, you will have to call \link Scan AstarPath.active.Scan () \endlink yourself to enable pathfinding,
	 * alternatively you could load a saved graph from a file.
	 */
	public bool scanOnStartup = true;
	
	/** Do a full GetNearest search for all graphs.
	 * Additinal searches will normally only be done on the graph which in the first, fast searches proved to have the closest node.
	 * With this setting on, additional searches will be done on all graphs.\n
	 * More technically: GetNearestForce on all graphs will be called if true, otherwise only on the one graph which's GetNearest search returned the best node.\n
	 * Usually faster when disabled, but higher quality searches when enabled.
	 * When using a a navmesh or recast graph, for best quality, this setting should be combined with the Pathfinding.NavMeshGraph.accurateNearestNode setting set to true.
	 * \note For the PointGraph this setting doesn't matter much as it has only one search mode.
	 */
	public bool fullGetNearestSearch = false;
	
	/** Prioritize graphs.
	 * Graphs will be prioritized based on their order in the inspector.
	 * The first graph which has a node closer than #prioritizeGraphsLimit will be chosen instead of searching all graphs.
	 */
	public bool prioritizeGraphs = false;
	
	/** Distance limit for #prioritizeGraphs.
	 * \see #prioritizeGraphs
	 */
	public float prioritizeGraphsLimit = 1F;
	
	/** Reference to the color settings for this AstarPath object.
	 * Color settings include for example which color the nodes should be in, in the sceneview. */
	public AstarColor colorSettings;
	
	/** Stored tag names.
	 * \see AstarPath.FindTagNames
	 * \see AstarPath.GetTagNames
	 */
	[SerializeField]
	protected string[] tagNames = null;
	
	/** The heuristic to use.
	 * The heuristic, often referred to as 'H' is the estimated cost from a node to the target.
	 * Different heuristics affect how the path picks which one to follow from multiple possible with the same length
	 * \see Pathfinding.Heuristic
	 */
	public Heuristic heuristic = Heuristic.Euclidean;
	
	/** The scale of the heuristic. If a smaller value than 1 is used, the pathfinder will search more nodes (slower).
	 * If 0 is used, the pathfinding will be equal to dijkstra's algorithm.
	 * If a value larger than 1 is used the pathfinding will (usually) be faster because it expands fewer nodes, but the paths might not longer be optimal
	 */
	public float heuristicScale = 1F;

	/** Number of pathfinding threads to use.
	 * Multithreading puts pathfinding in another thread, this is great for performance on 2+ core computers since the framerate will barely be affected by the pathfinding at all.
	 * But this can cause strange errors and pathfinding stopping to work if you are not carefull (that is, if you are modifying the pathfinding scripts). For basic usage (not modding the pathfinding core) it should be safe.\n
	 * - None indicates that the pathfinding is run in the Unity thread as a coroutine
	 * - Automatic will try to adjust the number of threads to the number of cores and memory on the computer.
	 * 	Less than 512mb of memory or a single core computer will make it revert to using no multithreading.
	 * \see CalculateThreadCount
	 * \astarpro
	 */
	public ThreadCount threadCount = ThreadCount.None;
	
	/** Max number of milliseconds to spend each frame for pathfinding.
	 * At least 500 nodes will be searched each frame (if there are that many to search).
	 * When using multithreading this value is quite irrelevant,
	 * but do not set it too low since that could add upp to some overhead, 10ms will work good for multithreading */
	public float maxFrameTime = 1F;
	
	/** Defines the minimum amount of nodes in an area.
	 * If an area has less than this amount of nodes, the area will be flood filled again with the area ID 254,
	 * it shouldn't affect pathfinding in any significant way.\n
	 * If you want to be able to separate areas from one another for some reason (for example to do a fast check to see if a path is at all possible)
	 * you should set this variable to 0.\n
	  * Can be found in A* Inspector-->Settings-->Min Area Size
	  */
	public int minAreaSize = 10;
	
	/** Limit graph updates. If toggled, graph updates will be executed less often (specified by #maxGraphUpdateFreq).*/
	public bool limitGraphUpdates = true;
	
	/** How often should graphs be updated. If #limitGraphUpdates is true, this defines the minimum amount of seconds between each graph update.*/
	public float maxGraphUpdateFreq = 0.2F;
	
	/** @} */
#endregion
	
#region DebugVariables
	/** @name Debug Members
	 * @{ */
	
	/** How many paths has been computed this run. From application start.\n
	 * Debugging variable */
	public static int PathsCompleted = 0;
	
	public static System.Int64 				TotalSearchedNodes = 0;
	public static System.Int64			 	TotalSearchTime = 0;
	
	/** The time it took for the last call to Scan() to complete.
	 * Used to prevent automatically rescanning the graphs too often (editor only) */
	public float lastScanTime = 0F;
	
	/** The path to debug using gizmos.
	 * This is equal to the last path which was calculated,
	 * it is used in the editor to draw debug information using gizmos.*/
	public Path debugPath;
	
	/** NodeRunData from #debugPath.
	 * Returns null if #debugPath is null
	 */
	public PathHandler debugPathData {
		get {
			if (debugPath == null) return null;
			return debugPath.pathHandler;
		}
	}
	
	/** This is the debug string from the last completed path.
	 * Will be updated if #logPathResults == PathLog.InGame */
	public string inGameDebugPath;
	
	/* @} */
#endregion
	
#region StatusVariables
	
	/** Set when scanning is being done. It will be true up until the FloodFill is done.
	 * Used to better support Graph Update Objects called for example in OnPostScan */
	public bool isScanning = false;
	
	/** Number of parallel pathfinders.
	 * Returns the number of concurrent processes which can calculate paths at once.
	 * When using multithreading, this will be the number of threads, if not using multithreading it is always 1 (since only 1 coroutine is used).
	 * \see threadInfos
	 * \see IsUsingMultithreading
	 */
	public static int NumParallelThreads {
		get {
			return threadInfos != null ? threadInfos.Length : 0;
		}
	}
	
	/** Returns whether or not multithreading is used.
	 * \exception System.Exception Is thrown when it could not be decided if multithreading was used or not.
	 * This should not happen if pathfinding is set up correctly.
	 * \note This uses info about if threads are running right now, it does not use info from the settings on the A* object.
	 */
	public static bool IsUsingMultithreading {
		get {
			if (threads != null && threads.Length > 0)
				return true;
			else if (threads != null && threads.Length == 0 && threadEnumerator != null)
				return false;
			else if (Application.isPlaying)
				throw new System.Exception ("Not 'using threading' and not 'not using threading'... Are you sure pathfinding is set up correctly?\nIf scripts are reloaded in unity editor during play this could happen.\n"+
					(threads != null ? ""+threads.Length : "NULL") + " " + (threadEnumerator != null));
			else 
				return false;
		}
	}
	
	/** Returns if any graph updates are waiting to be applied */
	public bool IsAnyGraphUpdatesQueued { get { return graphUpdateQueue != null && graphUpdateQueue.Count > 0; }}
	
	private bool graphUpdateRoutineRunning = false;
	
	private bool isRegisteredForUpdate = false;
	
#endregion
	
#region Callbacks
	/** @name Callbacks */
	 /* Callbacks to pathfinding events.
	 * These allow you to hook in to the pathfinding process.\n
	 * Callbacks can be used like this:
	 * \code
	 * public void Start () {
	 * 	AstarPath.OnPostScan += SomeFunction;
	 * }
	 * 
	 * public void SomeFunction (AstarPath active) {
	 * 	//This will be called every time the graphs are scanned
	 * }
	 * \endcode
	*/
	 /** @{ */
	
	/** Called on Awake before anything else is done.
	  * This is called at the start of the Awake call, right after #active has been set, but this is the only thing that has been done.\n
	  * Use this when you want to set up default settings for an AstarPath component created during runtime since some settings can only be changed in Awake
	  * (such as multithreading related stuff)
	  * \code
	  * //Create a new AstarPath object on Start and apply some default settings
	  * public void Start () {
	  * 	AstarPath.OnAwakeSettings += ApplySettings;
	  * 	AstarPath astar = AddComponent<AstarPath>();
	  * }
	  * 
	  * public void ApplySettings () {
	  * 	//Unregister from the delegate
	  * 	AstarPath.OnAwakeSettings -= ApplySettings;
	  * 	
	  * 	//For example useMultithreading should not be changed after the Awake call
	  * 	//so here's the only place to set it if you create the component during runtime
	  * 	AstarPath.active.useMultithreading = true;
	  * }
	  * \endcode
	  */
	public static OnVoidDelegate OnAwakeSettings;
	
	public static OnGraphDelegate OnGraphPreScan; /**< Called for each graph before they are scanned */
	
	public static OnGraphDelegate OnGraphPostScan; /**< Called for each graph after they have been scanned. All other graphs might not have been scanned yet. */
	
	public static OnPathDelegate OnPathPreSearch; /**< Called for each path before searching. Be carefull when using multithreading since this will be called from a different thread. */
	public static OnPathDelegate OnPathPostSearch; /**< Called for each path after searching. Be carefull when using multithreading since this will be called from a different thread. */
	
	public static OnScanDelegate OnPreScan; /**< Called before starting the scanning */
	public static OnScanDelegate OnPostScan; /**< Called after scanning. This is called before applying links, flood-filling the graphs and other post processing. */
	public static OnScanDelegate OnLatePostScan; /**< Called after scanning has completed fully. This is called as the last thing in the Scan function. */
	
	public static OnScanDelegate OnGraphsUpdated; /**< Called when any graphs are updated. Register to for example recalculate the path whenever a graph changes. */
	
	/** Called when \a pathID overflows 65536.
	 * The Pathfinding.CleanupPath65K will be added to the queue, and directly after, this callback will be called.
	 * \note This callback will be cleared every timed it is called, so if you want to register to it repeatedly, register to it directly on receiving the callback as well. 
	 */
	public static OnVoidDelegate On65KOverflow;
	
	/** Will send a callback when it is safe to update the nodes. Register to this with RegisterSafeNodeUpdate
	 * When it is safe is defined as between the path searches.
	 * This callback will only be sent once and is nulled directly after the callback is sent.
	 * \warning Note that these callbacks are not thread safe when using multithreading, DO NOT call any part of the Unity API from these callbacks except for Debug.Log
	 */
	private static OnVoidDelegate OnSafeCallback;
	
	/** Will send a callback when it is safe to update the nodes. Register to this with RegisterThreadSafeNodeUpdate
	 * When it is safe is defined as between the path searches.
	 * This callback will only be sent once and is nulled directly after the callback is sent.
	 * \see OnSafeCallback
	 */
	private static OnVoidDelegate OnThreadSafeCallback;
	
	/** Used to enable gizmos in editor scripts.
	  * Used internally by the editor, do not use this in game code */
	public OnVoidDelegate OnDrawGizmosCallback;
	
	[System.ObsoleteAttribute]
	public OnVoidDelegate OnGraphsWillBeUpdated;
	[System.ObsoleteAttribute]
	public OnVoidDelegate OnGraphsWillBeUpdated2;
	
	/* @} */
#endregion
	
#region MemoryStructures
	
	/** Stack containing all waiting graph update queries. Add to this stack by using \link UpdateGraphs \endlink
	 * \see UpdateGraphs
	 */
	[System.NonSerialized]
	public Queue<GraphUpdateObject> graphUpdateQueue;
	
	/** Stack used for flood-filling the graph. It is saved to minimize memory allocations. */
	[System.NonSerialized]
	public Stack<GraphNode> floodStack;
	
	ThreadControlQueue pathQueue = new ThreadControlQueue(0);
	
	private static Thread[] threads;
	
	/** Holds info about each thread.
	 * The first item will hold information about the pathfinding coroutine when not using multithreading.
	 */
	private static PathThreadInfo[] threadInfos = new PathThreadInfo[0];
	
	/** When no multithreading is used, the IEnumerator is stored here.
	 * When no multithreading is used, a coroutine is used instead. It is not directly called with StartCoroutine
	 * but a separate function has just a while loop which increments the main IEnumerator.
	 * This is done so other functions can step the thread forward at any time, without having to wait for Unity to update it.
	 * \see CalculatePaths
	 * \see CalculatePathsHandler
	 */
	private static IEnumerator threadEnumerator;
	private static Pathfinding.Util.LockFreeStack pathReturnStack = new Pathfinding.Util.LockFreeStack();
	
#endregion
	
	/** Shows or hides graph inspectors.
	 * Used internally by the editor */
	public bool showGraphs = false;
	
	public static bool isEditor = true;
	
	/** The last area index which was used.
	 * Used for the \link FloodFill(Node node) FloodFill \endlink function to start flood filling with an unused area.
	 * \see FloodFill(Node node)
	 */
	public uint lastUniqueAreaIndex = 0;
	
#region ThreadingMembers
	
	private static readonly System.Object safeUpdateLock = new object();
	
#endregion
	
	/** Time the last graph update was done.
	 * Used to group together frequent graph updates to batches */
	private float lastGraphUpdate = -9999F;
	
	/** The next unused Path ID.
	 * Incremented for every call to GetFromPathPool */
	private ushort nextFreePathID = 1;
	
	/** Returns tag names.
	 * Makes sure that the tag names array is not null and of length 32.
	 * If it is null or not of length 32, it creates a new array and fills it with 0,1,2,3,4 etc...
	 * \see AstarPath.FindTagNames
	 */
	public string[] GetTagNames () {
		
		if (tagNames == null || tagNames.Length	!= 32) {
			tagNames = new string[32];
			for (int i=0;i<tagNames.Length;i++) {
				tagNames[i] = ""+i;
			}
			tagNames[0] = "Basic Ground";
		}
		return tagNames;
	}
	
	/** Tries to find an AstarPath object and return tag names.
	 * If an AstarPath object cannot be found, it returns an array of length 1 with an error message.
	 * \see AstarPath.GetTagNames
	 */
	public static string[] FindTagNames () {
		if (active != null) return active.GetTagNames ();
		else {
			AstarPath a = GameObject.FindObjectOfType (typeof (AstarPath)) as AstarPath;
			if (a != null) { active = a; return a.GetTagNames (); }
			else {
				return new string[1] {"There is no AstarPath component in the scene"};
			}
		}
	}
	
	/** Returns the next free path ID. If the next free path ID overflows 65535, a cleanup operation is queued
	  * \see Pathfinding.CleanupPath65K */
	public ushort GetNextPathID ()
	{
		if (nextFreePathID == 0) {
			nextFreePathID++;
			
			//Queue a cleanup operation to zero all path IDs
			//StartPath (new CleanupPath65K ());
			Debug.Log ("65K cleanup");
			
			//ushort toBeReturned = nextFreePathID;
			
			if (On65KOverflow != null) {
				OnVoidDelegate tmp = On65KOverflow;
				On65KOverflow = null;
				tmp ();
			}
			
			//return nextFreePathID++;
		}
		return nextFreePathID++;
	}
	
	
	/** Calls OnDrawGizmos on graph generators and also #OnDrawGizmosCallback */
	private void OnDrawGizmos () {
		AstarProfiler.StartProfile ("OnDrawGizmos");
		
		if (active == null) {
			active = this;
		} else if (active != this) {
			return;
		}
		
		if (graphs == null) return;
		
		//If updating graphs, graph info might be corrupt right now
		if (pathQueue != null && pathQueue.AllReceiversBlocked && workItems.Count > 0) return;
		
		for (int i=0;i<graphs.Length;i++) {
			if (graphs[i] == null) continue;
			
			if (graphs[i].drawGizmos)
				graphs[i].OnDrawGizmos (showNavGraphs);
		}
		
		if (showUnwalkableNodes && showNavGraphs) {
			Gizmos.color = AstarColor.UnwalkableNode;
			
			GraphNodeDelegateCancelable del = DrawUnwalkableNode;
			
			for (int i=0;i<graphs.Length;i++) {
				if (graphs[i] != null) graphs[i].GetNodes (del);
			}
		}
		
		if (OnDrawGizmosCallback != null) {
			OnDrawGizmosCallback ();
		}
		
		AstarProfiler.EndProfile ("OnDrawGizmos");
	}
	
	/** Draws a cube at the node's position if unwalkable.
	 * Used for gizmo drawing
	 */
	private bool DrawUnwalkableNode (GraphNode node) {
		if (!node.Walkable) {
			Gizmos.DrawCube ((Vector3)node.position, Vector3.one*unwalkableNodeDebugSize);
		}
		return true;
	}

	/** Draws the InGame debugging (if enabled), also shows the fps if 'L' is pressed down.
	 * \see #logPathResults PathLog
	 */
	private void OnGUI () {
		
		if (logPathResults == PathLog.InGame) {
			
			if (inGameDebugPath != "") {
						
				GUI.Label (new Rect (5,5,400,600),inGameDebugPath);
			}
		}
		
		/*if (GUI.Button (new Rect (Screen.width-100,5,100,20),"Load New Level")) {
			Application.LoadLevel (0);
		}*/
		
	}
	
#line hidden
	/** Logs a string while taking into account #logPathResults */
	private static void AstarLog (string s) {
		if (System.Object.ReferenceEquals(active,null)) {
			Debug.Log ("No AstarPath object was found : "+s);
			return;
		}
		
		if (active.logPathResults != PathLog.None && active.logPathResults != PathLog.OnlyErrors) {
			Debug.Log (s);
		}
	}
	
	/** Logs an error string while taking into account #logPathResults */
	private static void AstarLogError (string s) {
		if (active == null) {
			Debug.Log ("No AstarPath object was found : "+s);
			return;
		}
		
		if (active.logPathResults != PathLog.None) {
			Debug.LogError (s);
		}
	}
#line default

	/** Prints path results to the log. What it prints can be controled using #logPathResults.
	 * \see #logPathResults
	 * \see PathLog
	 * \see Pathfinding.Path.DebugString
	 */
	private void LogPathResults (Path p) {
		
		if (logPathResults == PathLog.None || (logPathResults == PathLog.OnlyErrors && !p.error)) {
			return;
		}
		
		string debug = p.DebugString (logPathResults);
		
		if (logPathResults == PathLog.InGame) {
			inGameDebugPath = debug;
		} else {
			//Debug.Log (debug);
		}
	}
	
	
	public struct AstarWorkItem {
		/** Init function.
		 * May be null if no initialization is needed.
		 * Will be called once, right before the first call to #update.
		 */
		public OnVoidDelegate init;
		
		/** Update function, called once per frame when the work item executes.
		 * Takes a param \a force. If that is true, the work item should try to complete the whole item in one go instead
		 * of spreading it out over multiple frames.
		 * \returns True when the work item is completed.
		 */
		public System.Func<bool, bool> update;
		
		public AstarWorkItem (System.Func<bool, bool> update) {
			init = null;
			this.update = update;
		}
		
		public AstarWorkItem (OnVoidDelegate init, System.Func<bool, bool> update) {
			this.init = init;
			this.update = update;
		}
	}
	
	private Queue<AstarWorkItem> workItems = new Queue<AstarWorkItem>();
	
	/* Checks if the OnThreadSafeCallback callback needs to be (and can) be called and if so, does it.
	 * Unpauses pathfinding threads after that.
	 * \see CallThreadSafeCallbacks
	 */
	private void Update () {
		PerformBlockingActions();
		
		//Process paths
		if (threadEnumerator != null) {
			try {
				threadEnumerator.MoveNext ();
				
			} catch (System.Exception e) {
				//This will kill pathfinding
				threadEnumerator = null;
				
				// Queue termination exceptions should be ignored, they are supposed to kill the thread
				if (!(e is ThreadControlQueue.QueueTerminationException)) {
			
					Debug.LogException (e);
					Debug.LogError ("Unhandled exception during pathfinding. Terminating.");
					pathQueue.TerminateReceivers();
					
					//This will throw an exception supposed to kill the thread
					try {
						pathQueue.PopNoBlock(false);
					} catch {}
				}
			}
		}
		
		//Return calculated paths
		ReturnPaths(true);
	}
	
	private void PerformBlockingActions (bool force = false, bool unblockOnComplete = true) {
		if (pathQueue.AllReceiversBlocked) {
			// Return all paths before starting blocking actions (these might change the graph and make returned paths invalid (at least the nodes))
			ReturnPaths (false);
			
			//This must be called before since otherwise ProcessWorkItems might start pathfinding again
			//if no work items are left to be processed resulting in thread safe callbacks never being called
			if (OnThreadSafeCallback != null) {
				OnVoidDelegate tmp = OnThreadSafeCallback;
				OnThreadSafeCallback = null;
				tmp ();
			}
			
			if (ProcessWorkItems (force) == 2) {
				//At this stage there are no more work items, restart pathfinding threads
				workItemsQueued = false;
				if (unblockOnComplete) {
					pathQueue.Unblock();
				}
			}
		}
		
	}
	
	private bool workItemsQueued = false;
	private bool queuedWorkItemFloodFill = false;
	
	/** Call during work items to queue a flood fill.
	 * An instant flood fill can be done via FloodFill()
	 * but this method can be used to batch several updates into one
	 * which can often save performance.
	 * WorkItems which require a valid Flood Fill in their execution can call EnsureValidFloodFill directly.
	 * If queued a flood fill will be done after all WorkItems have been executed
	 */
	public void QueueWorkItemFloodFill () {
		//Not a perfect check for if it is called inside or outside a workitem, but good enough
		if (!pathQueue.AllReceiversBlocked) {
			throw new System.Exception ("You are calling QueueWorkItemFloodFill from outside a WorkItem. This might cause unexpected behaviour.");
		}
		
		queuedWorkItemFloodFill = true;
	}
	
	/** If a WorkItem needs to have a valid flood fill during execution, call this method to ensure there are no pending flood fills.
	 */
	public void EnsureValidFloodFill () {
		if (queuedWorkItemFloodFill) {
			FloodFill();
		}
	}
	
	/** Add a work item to be processed when pathfinding is paused.
	 * 
	 * \see ProcessWorkItems
	 */
	public void AddWorkItem (AstarWorkItem itm) {
		
		workItems.Enqueue (itm);
		
		//Make sure pathfinding is stopped and work items are processed
		if (!workItemsQueued) {
			workItemsQueued = true;
			if (!isScanning) {
				InterruptPathfinding ();
			}
		}

#if UNITY_EDITOR
		//If not playing, execute instantly
		if (!Application.isPlaying) {
			FlushWorkItems();
		}
#endif
	}
	
	bool processingWorkItems = false;
	
	/** Process graph updating work items.
	 * Process all queued work items, e.g graph updates and the likes.
	 * 
	 * \returns 
	 * - 0 if there were no work items to be processed or if nothing can be done
	 * 	because pathfinding is not paused.
	 * - 1 if there are still items to be processed.
	 * - 2 if the last work items was processed and pathfinding threads are ready to be resumed.
	 * 
	 * \see AddWorkItem
	 * \see threadSafeUpdateState
	 * \see Update
	 */
	private int ProcessWorkItems (bool force) {
		if (pathQueue.AllReceiversBlocked) {
			if (processingWorkItems) throw new System.Exception ("Processing work items recursively. Please do not wait for other work items to be completed inside work items. " +
				"If you think this is not caused by any of your scripts, this might be a bug.");
			
			processingWorkItems = true;
			while (workItems.Count > 0) {
				AstarWorkItem itm = workItems.Peek ();
				
				//Call init the first time the item is seen
				if (itm.init != null) {
					itm.init ();
					itm.init = null;
				}
				
				bool status;
				try {
					status = itm.update == null ? true : itm.update (force);
				} catch {
					workItems.Dequeue ();
					processingWorkItems = false;
					throw;
				}
				
				if (!status) {
					// Still work items to process
					processingWorkItems = false;
					return 1;
				}
				else workItems.Dequeue ();
			}
			
			EnsureValidFloodFill ();
			
			processingWorkItems = false;
			return 2;
		}
		return 0;
	}
	
#region GraphUpdateMethods
	
	/** Will apply queued graph updates as soon as possible, regardless of #limitGraphUpdates.
	 * Calling this multiple times will not create multiple callbacks.
	 * Makes sure DoUpdateGraphs is called as soon as possible.\n
	 * This function is useful if you are limiting graph updates, but you want a specific graph update to be applied as soon as possible regardless of the time limit.
	 * \see FlushGraphUpdates
	 */
	public void QueueGraphUpdates () {
		if (!isRegisteredForUpdate) {
			isRegisteredForUpdate = true;
			AstarWorkItem itm = new AstarWorkItem();
			itm.init = QueueGraphUpdatesInternal;
			itm.update = ProcessGraphUpdates;
			AddWorkItem (itm);
		}
	}
	
	/** Waits a moment with updating graphs.
	 * If limitGraphUpdates is set, we want to keep some space between them to let pathfinding threads running and then calculate all queued calls at once
	 */
	private IEnumerator DelayedGraphUpdate () {
		graphUpdateRoutineRunning = true;
		
		yield return new WaitForSeconds (maxGraphUpdateFreq-(Time.time-lastGraphUpdate));
		QueueGraphUpdates ();
		graphUpdateRoutineRunning = false;
	}
	
	/** Update all graphs within \a bounds after \a t seconds.
	 * This function will add a GraphUpdateObject to the #graphUpdateQueue.
	 * The graphs will be updated as soon as possible.
	 */
	public void UpdateGraphs (Bounds bounds, float t) {
		UpdateGraphs (new GraphUpdateObject (bounds),t);
	}
	
	/** Update all graphs using the GraphUpdateObject after \a t seconds.
	 * This can be used to, e.g make all nodes in an area unwalkable, or set them to a higher penalty.
	*/
	public void UpdateGraphs (GraphUpdateObject ob, float t) {
		StartCoroutine (UpdateGraphsInteral (ob,t));
	}
	
	/** Update all graphs using the GraphUpdateObject after \a t seconds */
	private IEnumerator UpdateGraphsInteral (GraphUpdateObject ob, float t) {
		yield return new WaitForSeconds (t);
		UpdateGraphs (ob);
	}
	
	/** Update all graphs within \a bounds.
	 * This function will add a GraphUpdateObject to the #graphUpdateQueue.
	 * The graphs will be updated as soon as possible.
	 * 
	 * This is equivalent to\n
	 * UpdateGraphs (new GraphUpdateObject (bounds))
	 * 
	 * \see FlushGraphUpdates
	 */
	public void UpdateGraphs (Bounds bounds) {
		UpdateGraphs (new GraphUpdateObject (bounds));
	}
	
	/** Update all graphs using the GraphUpdateObject.
	 * This can be used to, e.g make all nodes in an area unwalkable, or set them to a higher penalty.
	 * The graphs will be updated as soon as possible (with respect to #limitGraphUpdates)
	 * 
	 * \see FlushGraphUpdates
	*/
	public void UpdateGraphs (GraphUpdateObject ob) {
		
		//Create a new queue if no queue exists yet
		if (graphUpdateQueue == null) {
			graphUpdateQueue = new Queue<GraphUpdateObject> ();
		}
		
		//Put the GUO in the queue
		graphUpdateQueue.Enqueue (ob);
		
		//If we should limit graph updates, start a coroutine which waits until we should update graphs
		if (limitGraphUpdates && Time.time-lastGraphUpdate < maxGraphUpdateFreq) {
			if (!graphUpdateRoutineRunning) {
				StartCoroutine (DelayedGraphUpdate ());
			}
		} else {
			//Otherwise, graph updates should be carried out as soon as possible
			QueueGraphUpdates ();
		}
		
	}
	
	/** Forces graph updates to run.
	 * This will force all graph updates to run immidiately. Or more correctly, it will block the Unity main thread until graph updates can be performed and then issue them.
	 * This will force the pathfinding threads to finish calculate the path they are currently calculating (if any) and then pause.
	 * When all threads have paused, graph updates will be performed.
	 * \warning Using this very often (many times per second) can reduce your fps due to a lot of threads waiting for one another.
	 * But you probably wont have to worry about that.
	 * 
	 * \note This is almost identical to FlushThreadSafeCallbacks, but added for more descriptive name.
	 * This function will also override any time limit delays for graph updates.
	 * This is because graph updates are implemented using thread safe callbacks.
	 * So calling this function will also call other thread safe callbacks (if any are waiting).
	 * 
	 * Will not do anything if there are no graph updates queued (not even call other callbacks).
	 */
	public void FlushGraphUpdates () {
		if (IsAnyGraphUpdatesQueued) {
			QueueGraphUpdates ();
			FlushWorkItems ();
		}
	}
	
	public void FlushWorkItems () {
		
		//FlushThreadSafeCallbacks();
		BlockUntilPathQueueBlocked();
		//Run tasks
		PerformBlockingActions(true);
	}
	
#endregion
	
	private void QueueGraphUpdatesInternal () {
		
		isRegisteredForUpdate = false;
		
		bool anyRequiresFloodFill = false;
		
		while (graphUpdateQueue.Count > 0) {
			GraphUpdateObject ob = graphUpdateQueue.Dequeue ();
			
			if (ob.requiresFloodFill) anyRequiresFloodFill = true;
		
			foreach (IUpdatableGraph g in astarData.GetUpdateableGraphs ()) {
				NavGraph gr = g as NavGraph;
				if (ob.nnConstraint == null || ob.nnConstraint.SuitableGraph (active.astarData.GetGraphIndex (gr),gr)) {
					GUOSingle aguo = new GUOSingle ();
					aguo.order = GraphUpdateOrder.GraphUpdate;
					aguo.obj = ob;
					aguo.graph = g;
					graphUpdateQueueRegular.Enqueue (aguo);
				}
			}
		}
		
		if (anyRequiresFloodFill) {
			GUOSingle guo = new GUOSingle();
			guo.order = GraphUpdateOrder.FloodFill;
			graphUpdateQueueRegular.Enqueue (guo);
		}
		
		//Nodes might have been invalidated and then the debugPathData could lead to nodes being used which have been invalidated
		//This could in the long run lead to exceptions
		debugPath = null;
		
		GraphModifier.TriggerEvent (GraphModifier.EventType.PreUpdate);
	}
	
	/** Updates graphs.
	 * Will do some graph updates, eventually signal another thread to do them.
	 * Will only process graph updates added by QueueGraphUpdatesInternal
	 * 
	 * \param force If true, all graph updates will be processed before this function returns. The return value
	 * will be True.
	 * 
	 * \returns True if all graph updates have been done and pathfinding (or other tasks) may resume.
	 * False if there are still graph updates being done or waiting in the queue.
	 * 
	 * 
	 */
	private bool ProcessGraphUpdates (bool force) {
		
		if (force) {
			processingGraphUpdatesAsync.WaitOne ();
		} else {
			if (!processingGraphUpdatesAsync.WaitOne (0)) {
				return false;
			}
		}
		
		if (graphUpdateQueueAsync.Count != 0) throw new System.Exception ("Queue should be empty at this stage");
		
		while (graphUpdateQueueRegular.Count > 0) {
			
			GUOSingle s = graphUpdateQueueRegular.Peek ();
			
			GraphUpdateThreading threading = s.order == GraphUpdateOrder.FloodFill ? GraphUpdateThreading.SeparateThread : s.graph.CanUpdateAsync(s.obj);
			
			if (!force && (threading == GraphUpdateThreading.SeparateAndUnityInit)) {
				if (graphUpdateQueueAsync.Count > 0) {
					//Process async graph updates first.
					
					//Next call to this function will process this object so it is not dequeued now	
					processingGraphUpdatesAsync.Reset ();
					graphUpdateAsyncEvent.Set ();
					
					return false;
				}
				
				s.graph.UpdateAreaInit (s.obj);
				
				//Move GUO to async queue to be updated by another thread
				graphUpdateQueueRegular.Dequeue ();
				graphUpdateQueueAsync.Enqueue (s);
				
				//Next call to this function will process this object so it is not dequeued now	
				processingGraphUpdatesAsync.Reset ();
				graphUpdateAsyncEvent.Set ();
				
				return false;
			} else if (!force && (threading == GraphUpdateThreading.SeparateThread)) {
				//Move GUO to async queue to be updated by another thread
				graphUpdateQueueRegular.Dequeue ();
				graphUpdateQueueAsync.Enqueue (s);
			} else {
				//Everything should be done in the unity thread
				
				if (graphUpdateQueueAsync.Count > 0) {
					//Process async graph updates first.
					
					if (force) throw new System.Exception ("This should not happen");
					
					//Next call to this function will process this object so it is not dequeued now	
					processingGraphUpdatesAsync.Reset ();
					graphUpdateAsyncEvent.Set ();
					
					return false;
				}
				
				graphUpdateQueueRegular.Dequeue ();
				
				if (s.order == GraphUpdateOrder.FloodFill) {
					FloodFill ();
				} else {
					if (threading == GraphUpdateThreading.SeparateAndUnityInit) {
						try {
							s.graph.UpdateAreaInit (s.obj);
						} catch (System.Exception e) {
							Debug.LogError ("Error while initializing GraphUpdates\n" + e);
						}
					}
					try {
						s.graph.UpdateArea (s.obj);
					} catch (System.Exception e) {
						Debug.LogError ("Error while updating graphs\n"+e);
					}
				}
			}
		}
		
		if (graphUpdateQueueAsync.Count > 0) {
			
			//Next call to this function will process this object so it is not dequeued now	
			processingGraphUpdatesAsync.Reset ();
			graphUpdateAsyncEvent.Set ();
			
			return false;
		}
		
		GraphModifier.TriggerEvent (GraphModifier.EventType.PostUpdate);
		if (OnGraphsUpdated != null) OnGraphsUpdated(this);
		
		return true;
	}
	
	/** \todo Should be signaled in OnDestroy */
	private System.Threading.AutoResetEvent graphUpdateAsyncEvent = new System.Threading.AutoResetEvent(false);
	private System.Threading.ManualResetEvent processingGraphUpdatesAsync = new System.Threading.ManualResetEvent(true);
	private Queue<GUOSingle> graphUpdateQueueAsync = new Queue<GUOSingle>();
	private Queue<GUOSingle> graphUpdateQueueRegular = new Queue<GUOSingle>();

	enum GraphUpdateOrder {
		GraphUpdate,
		FloodFill
	}
	
	struct GUOSingle {
		public GraphUpdateOrder order;
		public IUpdatableGraph graph;
		public GraphUpdateObject obj;
	}
	
	private 
	void ProcessGraphUpdatesAsync (System.Object _astar) {
		AstarPath astar = _astar as AstarPath;
		if (System.Object.ReferenceEquals (astar, null)) {
			Debug.LogError ("ProcessGraphUpdatesAsync started with invalid parameter _astar (was no AstarPath object)");
			return;
		}
		
		while (!astar.pathQueue.IsTerminating) {
			graphUpdateAsyncEvent.WaitOne ();
			
			//Abort thread and clear queue
			if (astar.pathQueue.IsTerminating) {
				graphUpdateQueueAsync.Clear ();
				processingGraphUpdatesAsync.Set ();
				return;
			}
			
			while (graphUpdateQueueAsync.Count > 0) {
				GUOSingle aguo = graphUpdateQueueAsync.Dequeue ();
				
				try {
					if (aguo.order == GraphUpdateOrder.GraphUpdate) {
						aguo.graph.UpdateArea (aguo.obj);
					} else if (aguo.order == GraphUpdateOrder.FloodFill) {
						astar.FloodFill ();
					} else {
						throw new System.NotSupportedException ("" + aguo.order);
					}
				} catch (System.Exception e) {
					Debug.LogError ("Exception while updating graphs:\n"+e);
				}
			}
			
			processingGraphUpdatesAsync.Set ();
		}
	}
	
	/** Forces thread safe callbacks to run.
	 * This will force all thread safe callbacks to run immidiately. Or rather, it will block the Unity main thread until callbacks can be called and then issue them.
	 * This will force the pathfinding threads to finish calculate the path they are currently calculating (if any) and then pause.
	 * When all threads have paused, thread safe callbacks will be called (which can be e.g graph updates).
	 * 
	 * \warning Using this very often (many times per second) can reduce your fps due to a lot of threads waiting for one another.
	 * But you probably wont have to worry about that
	 * 
	 * \note This is almost (note almost) identical to FlushGraphUpdates, but added for more appropriate name.
	 */
	public void FlushThreadSafeCallbacks () {
		
		//No callbacks? why wait?
		if (OnThreadSafeCallback == null) {
			return;
		}
		
		BlockUntilPathQueueBlocked();
		PerformBlockingActions();
		
		
	}
	
	
	/** Calculates number of threads to use.
	 * If \a count is not Automatic, simply returns \a count casted to an int.
	 * \returns An int specifying how many threads to use, 0 means a coroutine should be used for pathfinding instead of a separate thread.
	 * 
	 * If \a count is set to Automatic it will return a value based on the number of processors and memory for the current system.
	 * If memory is <= 512MB or logical cores are <= 1, it will return 0. If memory is <= 1024 it will clamp threads to max 2.
	 * Otherwise it will return the number of logical cores clamped to 6.
	 */
	public static int CalculateThreadCount (ThreadCount count) {
		if (count == ThreadCount.AutomaticLowLoad || count == ThreadCount.AutomaticHighLoad) {
			int logicalCores = Mathf.Max (1,SystemInfo.processorCount);
			int memory = SystemInfo.systemMemorySize;
			
			if ( memory <= 0 ) {
				Debug.LogError ("Machine reporting that is has <= 0 bytes of RAM. This is definitely not true, assuming 1 GiB");
				memory = 1024;
			}
			
			if ( logicalCores <= 1) return 0;
			if ( memory <= 512) return 0;
			
			return 1;
		} else {
			return (int)count > 0 ? 1 : 0;
		}
	}
	
	/** Sets up all needed variables and scans the graphs.
	 * Calls Initialize, starts the ReturnPaths coroutine and scans all graphs.
	 * Also starts threads if using multithreading
	 * \see #OnAwakeSettings */
	public void Awake () {
		//Very important to set this. Ensures the singleton pattern holds
		active = this;
		
		if (FindObjectsOfType (typeof(AstarPath)).Length > 1) {
			Debug.LogError ("You should NOT have more than one AstarPath component in the scene at any time.\n" +
				"This can cause serious errors since the AstarPath component builds around a singleton pattern.");
		}
		
		//Disable GUILayout to gain some performance, it is not used in the OnGUI call
		useGUILayout = false;
		
		isEditor = Application.isEditor;
		
		if (OnAwakeSettings != null) {
			OnAwakeSettings ();
		}
		
		//To make sure all graph modifiers have been enabled before scan (to avoid script run order issues)
		GraphModifier.FindAllModifiers ();
		RelevantGraphSurface.FindAllGraphSurfaces ();
		
		int numThreads = CalculateThreadCount (threadCount);
		
		// Trying to prevent simple modding to add support for more than one thread
		if ( numThreads > 1 ) {
			threadCount = ThreadCount.One;
			numThreads = 1;
		}
		
		threads = new Thread[numThreads];
		//Thread info, will contain at least one item since the coroutine "thread" is thought of as a real thread in this case
		threadInfos = new PathThreadInfo[System.Math.Max(numThreads,1)];
		
		//Set up path queue with the specified number of receivers
		pathQueue = new ThreadControlQueue(threadInfos.Length);
		
		for (int i=0;i<threadInfos.Length;i++) {
			threadInfos[i] = new PathThreadInfo(i,this,new PathHandler());
		}
		for (int i=0;i<threads.Length;i++) {
			threads[i] = new Thread (new ParameterizedThreadStart (CalculatePathsThreaded));
			threads[i].Name = "Pathfinding Thread " + i;
			threads[i].IsBackground = true;
		}
		
		
		//Start coroutine if not using multithreading
		if (numThreads == 0) {
			threadEnumerator = CalculatePaths (threadInfos[0]);
		} else {
			threadEnumerator = null;
		}
		
		//Start pathfinding threads
		for (int i=0;i<threads.Length;i++) {
			if (logPathResults == PathLog.Heavy)
				Debug.Log ("Starting pathfinding thread "+i);
			threads[i].Start (threadInfos[i]);
		}

		Thread graphUpdateThread = new Thread (new ParameterizedThreadStart(ProcessGraphUpdatesAsync));
		graphUpdateThread.IsBackground = true;
		graphUpdateThread.Start (this);
		
		Initialize ();
		
		
		// Flush work items, possibly added in initialize to load graph data
		FlushWorkItems();
		
		if (scanOnStartup) {
			if (!astarData.cacheStartup || astarData.data_cachedStartup == null) {
				Scan ();
			}
		}
		
	}
	
	/** Does simple error checking.
	 */
	public void VerifyIntegrity () {
		
		if (active != this) {
			throw new System.Exception ("Singleton pattern broken. Make sure you only have one AstarPath object in the scene");
		}
		
		if (astarData == null) {
			throw new System.NullReferenceException ("AstarData is null... Astar not set up correctly?");
		}
		
		if (astarData.graphs == null) {
			astarData.graphs = new NavGraph[0];
		}
		
		if (pathQueue == null && !Application.isPlaying) {
			pathQueue = new ThreadControlQueue(0);
		}
		if (threadInfos == null && !Application.isPlaying) {
			threadInfos = new PathThreadInfo[0];
		}
		
		//Dummy if, the getter does some error checking
		if (IsUsingMultithreading) {
		}
	}
	
	/** Makes sure #active is set to this object and that #astarData is not null.
	 * Also calls OnEnable for the #colorSettings and initializes astarData.userConnections if it wasn't initialized before */
	public void SetUpReferences () {
		active = this;
		if (astarData == null) {
			astarData = new AstarData ();
		}
		
		if (astarData.userConnections == null) {
			astarData.userConnections = new UserConnection[0];
		}
		
		if (colorSettings == null) {
			colorSettings = new AstarColor ();
		}
			
		colorSettings.OnEnable ();
	}
	
	/** Initializes various variables.
	 * \link SetUpReferences Sets up references \endlink, 
	 * Searches for graph types, calls Awake on #astarData and on all graphs
	 * 
	 * \see AstarData.FindGraphTypes 
	 * \see SetUpReferences
	 */
	private void Initialize () {
		
		AstarProfiler.InitializeFastProfile (new string [14] {
			"Prepare", 			//0
			"Initialize",		//1
			"CalculateStep",	//2
			"Trace",			//3
			"Open",				//4
			"UpdateAllG",		//5
			"Add",				//6
			"Remove",			//7
			"PreProcessing",	//8
			"Callback",			//9
			"Overhead",			//10
			"Log",				//11
			"ReturnPaths",		//12
			"PostPathCallback"	//13
		});
		
		SetUpReferences ();
		
		astarData.FindGraphTypes ();
		
		astarData.Awake ();
		
		astarData.UpdateShortcuts ();
		
		//Initialize all graphs by calling their Awake functions
		for (int i=0;i<astarData.graphs.Length;i++) {			
			if (astarData.graphs[i] != null) astarData.graphs[i].Awake ();
		}
	}
	
	/** Clears up variables and other stuff, destroys graphs.
	 * Note that when destroying an AstarPath object, all static variables such as callbacks will be cleared.
	 */
	public void OnDestroy () {
		
		if (logPathResults == PathLog.Heavy)
			Debug.Log ("+++ AstarPath Component Destroyed - Cleaning Up Pathfinding Data +++");
		
		if ( active != this ) return;
		
		
		//Don't accept any more path calls to this AstarPath instance.
		//This will cause all eventual multithreading threads to exit
		pathQueue.TerminateReceivers();

		BlockUntilPathQueueBlocked();
		FlushWorkItems ();

		if (logPathResults == PathLog.Heavy)
			Debug.Log ("Processing Eventual Work Items");
		
		// Process work items until done 
		// Nope, don't do this
		//PerformBlockingActions (true);
		
		//Resume graph update thread, will cause it to terminate
		graphUpdateAsyncEvent.Set();
		
		//Try to join pathfinding threads
		if (threads != null) {
			for (int i=0;i<threads.Length;i++) {
#if UNITY_WEBPLAYER
				if (!threads[i].Join(200)) {
					Debug.LogError ("Could not terminate pathfinding thread["+i+"] in 200ms." +
						"Not good.\nUnity webplayer does not support Thread.Abort\nHoping that it will be terminated by Unity WebPlayer");
				}
#else
				if (!threads[i].Join (50)) {
					Debug.LogError ("Could not terminate pathfinding thread["+i+"] in 50ms, trying Thread.Abort");
					threads[i].Abort ();
				}
#endif
			}
		}
		
		if (logPathResults == PathLog.Heavy)
			Debug.Log ("Returning Paths");
		
		
		//Return all paths
		ReturnPaths (false);
		//Just in case someone happened to request a path in ReturnPath() (even though they should get canceled)
		pathReturnStack.PopAll ();
		
		if (logPathResults == PathLog.Heavy)
			Debug.Log ("Destroying Graphs");

		
		//Clean graphs up
		astarData.OnDestroy ();
		
		if (logPathResults == PathLog.Heavy)
			Debug.Log ("Cleaning up variables");
		
		//Clear variables up, static variables are good to clean up, otherwise the next scene might get weird data
		floodStack = null;
		graphUpdateQueue = null;
		
		//Clear all callbacks
		OnDrawGizmosCallback	= null;
		OnAwakeSettings			= null;
		OnGraphPreScan			= null;
		OnGraphPostScan			= null;
		OnPathPreSearch			= null;
		OnPathPostSearch		= null;
		OnPreScan				= null;
		OnPostScan				= null;
		OnLatePostScan			= null;
		On65KOverflow			= null;
		OnGraphsUpdated			= null;
		OnSafeCallback			= null;
		OnThreadSafeCallback	= null;
		
		threads = null;
		threadInfos = null;
		
		PathsCompleted = 0;
		
		active = null;
		
	}
	
#region ScanMethods
	
	/** Floodfills starting from the specified node */
	public void FloodFill (GraphNode seed) {
		FloodFill (seed, lastUniqueAreaIndex+1);
		lastUniqueAreaIndex++;
	}
	
	/** Floodfills starting from 'seed' using the specified area */
	public void FloodFill (GraphNode seed, uint area) {
		
		if (area > GraphNode.MaxRegionCount) {
			Debug.LogError ("Too high area index - The maximum area index is " + GraphNode.MaxRegionCount);
			return;
		}
		
		if (area < 0) {
			Debug.LogError ("Too low area index - The minimum area index is 0");
			return;
		}
					
		if (floodStack == null) {
			floodStack = new Stack<GraphNode> (1024);
		}
		
		Stack<GraphNode> stack = floodStack;
					
		stack.Clear ();
		
		stack.Push (seed);
		seed.Area = (uint)area;
		
		while (stack.Count > 0) {
			stack.Pop ().FloodFill (stack,(uint)area);
		}
				
	}
	
	/** Floodfills all graphs and updates areas for every node.
	  * \see Pathfinding.Node.area */
	public void FloodFill () {
		queuedWorkItemFloodFill = false;
		
		
		if (astarData.graphs == null) {
			return;
		}
		
		uint area = 0;
		
		lastUniqueAreaIndex = 0;
		
		if (floodStack == null) {
			floodStack = new Stack<GraphNode> (1024);
		}
		
		Stack<GraphNode> stack = floodStack;
		
		for (int i=0;i<graphs.Length;i++) {
			NavGraph graph = graphs[i];
			
			if (graph != null) {
				graph.GetNodes (delegate (GraphNode node) {	
					node.Area = 0;
					return true;
				});
			}
		}
		
		int smallAreasDetected = 0;
		
		bool warnAboutAreas = false;
		
		List<GraphNode> smallAreaList = Pathfinding.Util.ListPool<GraphNode>.Claim();//new List<GraphNode>();
		
		for (int i=0;i<graphs.Length;i++) {
			
			NavGraph graph = graphs[i];
			
			if (graph == null) continue;
			
			//for (int j=0;j<graph.nodes.Length;j++)
			GraphNodeDelegateCancelable del = delegate (GraphNode node) {
				if (node.Walkable && node.Area == 0) {
					
					area++;
					
					uint thisArea = area;
					
					if (area > GraphNode.MaxRegionCount) {
						if ( smallAreaList.Count > 0 ) {
							GraphNode smallOne = smallAreaList[smallAreaList.Count-1];
							thisArea = smallOne.Area;
							smallAreaList.RemoveAt (smallAreaList.Count-1);
							
							//Flood fill the area again with area ID 254, this identifies a small area
							stack.Clear ();
							
							stack.Push (smallOne);
							smallOne.Area = GraphNode.MaxRegionCount;
						
							while (stack.Count > 0) {
								stack.Pop ().FloodFill (stack,GraphNode.MaxRegionCount);
							}
						
							smallAreasDetected++;
						} else {
							// Forced to consider this a small area
							area--;
							thisArea = area;
							warnAboutAreas = true;
						}
					}
					
					stack.Clear ();
					
					stack.Push (node);
					
					int counter = 1;
					
					node.Area = thisArea;
					
					while (stack.Count > 0) {
						counter++;
						stack.Pop ().FloodFill (stack,thisArea);
					}
					
					if (counter < minAreaSize) {
						smallAreaList.Add ( node );
					}
				}
				return true;
			};
			
			graph.GetNodes (del);
		}
		
		lastUniqueAreaIndex = area;
		
		if (warnAboutAreas) {
			Debug.LogError ("Too many areas - The maximum number of areas is " + GraphNode.MaxRegionCount +". Try raising the A* Inspector -> Settings -> Min Area Size value. Enable the optimization ASTAR_MORE_AREAS under the Optimizations tab.");
		}
		
		if (smallAreasDetected > 0) {
			AstarLog (smallAreasDetected +" small areas were detected (fewer than "+minAreaSize+" nodes)," +
				"these might have the same IDs as other areas, but it shouldn't affect pathfinding in any significant way (you might get All Nodes Searched as a reason for path failure)." +
				"\nWhich areas are defined as 'small' is controlled by the 'Min Area Size' variable, it can be changed in the A* inspector-->Settings-->Min Area Size" +
				"\nThe small areas will use the area id 254");
		}
		
		Pathfinding.Util.ListPool<GraphNode>.Release ( smallAreaList );
		
	}
	
	private int nextNodeIndex = 1;
	Stack<int> nodeIndexPool = new Stack<int>();
	
	/** Returns a new global node index.
	 * \note This method should not be called directly. It is used by the GraphNode constructor.
	 */
	public int GetNewNodeIndex () {
		if (nodeIndexPool.Count > 0) return nodeIndexPool.Pop();
		return nextNodeIndex++;
	}
	
	/** Initializes temporary path data for a node.
	 * \note This method should not be called directly. It is used by the GraphNode constructor.
	 */
	public void InitializeNode (GraphNode node) {
		if (!pathQueue.AllReceiversBlocked) throw new System.Exception ("Trying to initialize a node when it is not safe to initialize any nodes. Must be done during a graph update");
		
		if (threadInfos == null) threadInfos = new PathThreadInfo[0];
		
		for (int i=0;i<threadInfos.Length;i++) {
			threadInfos[i].runData.InitializeNode (node);
		}
	}
	
	/** Destroyes the given node.
	 * This is to be called after the node has been disconnected from the graph so that it cannot be reached from any other nodes.
	 * It should only be called during graph updates, that is when the pathfinding threads are either not running or paused.
	 */
	public void DestroyNode (GraphNode node) {
		if (node.NodeIndex == -1) return;
		
		nodeIndexPool.Push(node.NodeIndex);
		
		if (threadInfos == null) threadInfos = new PathThreadInfo[0];
		
		for (int i=0;i<threadInfos.Length;i++) {
			threadInfos[i].runData.DestroyNode (node);
		}
	}
	
	/** Blocks until all pathfinding threads are paused and blocked.
	 * A call to pathQueue.Unblock is required to resume pathfinding calculations. However in
	 * most cases you should never unblock the path queue, instead let the pathfinding scripts do that in the next update.
	 * Unblocking the queue when other tasks (e.g graph updates) are running can interfere and cause invalid graphs.
	 * 
	 */
	public void BlockUntilPathQueueBlocked () {
		if (pathQueue == null) return;
		
		pathQueue.Block();
		
#if UNITY_EDITOR
		if (!Application.isPlaying) {
			if (!pathQueue.AllReceiversBlocked) throw new System.Exception ("Pathfinding running while not playing?");
			return;
		}
#endif
		
		while (!pathQueue.AllReceiversBlocked) {
			if (IsUsingMultithreading) {
				Thread.Sleep(1);
			} else {
				threadEnumerator.MoveNext ();
			}
		}
	}
	
	/** Scans all graphs */
	public void Scan () {
		
		ScanLoop (null);
	}
	
	/** Scans all graphs. This is a IEnumerable, you can loop through it to get the progress
	  * \code foreach (Progress progress in AstarPath.active.ScanLoop ()) {
	*	 Debug.Log ("Scanning... " + progress.description + " - " + (progress.progress*100).ToString ("0") + "%");
	  * } \endcode
	  * \see Scan
	  */
	public void ScanLoop (OnScanStatus statusCallback) {
		
		if (graphs == null) {
			return;
		}
		
		isScanning = true;
		
		VerifyIntegrity ();

		BlockUntilPathQueueBlocked ();

		if (!Application.isPlaying) {
			GraphModifier.FindAllModifiers ();
			RelevantGraphSurface.FindAllGraphSurfaces ();
		}
		
		RelevantGraphSurface.UpdateAllPositions ();
		
		astarData.UpdateShortcuts ();
		
		//statusCallback (new Progress (0.02F,"Updating graph shortcuts"));
		
		if (statusCallback != null) statusCallback (new Progress (0.05F,"Pre processing graphs"));
		
		if (OnPreScan != null) {
			
			OnPreScan (this);
		}
		
		GraphModifier.TriggerEvent (GraphModifier.EventType.PreScan);
		
		//float startTime = Time.realtimeSinceStartup;
		System.DateTime startTime = System.DateTime.UtcNow;
		
		// Destroy previous nodes
		for (int i=0;i<graphs.Length;i++) {
			if (graphs[i] != null) {
				graphs[i].GetNodes (delegate (GraphNode node) {
					node.Destroy ();
					return true;
				});
			}
		}
		
		for (int i=0;i<graphs.Length;i++) {
			
			NavGraph graph = graphs[i];
			
			if (graph == null) {
				if (statusCallback != null) statusCallback (new Progress (AstarMath.MapTo (0.05F,0.7F,(float)(i+0.5F)/(graphs.Length+1)),"Skipping graph "+(i+1)+" of "+graphs.Length+" because it is null"));
				continue;
			}
			
			if (OnGraphPreScan != null) {
				if (statusCallback != null) statusCallback (new Progress (AstarMath.MapToRange (0.1F,0.7F,(float)(i)/(graphs.Length)),"Scanning graph "+(i+1)+" of "+graphs.Length+" - Pre processing"));
				OnGraphPreScan (graph);
			}
			
			
			
			float minp = AstarMath.MapToRange (0.1F,0.7F,(float)(i)/(graphs.Length));
			float maxp = AstarMath.MapToRange (0.1F,0.7F,(float)(i+0.95F)/(graphs.Length));
			
			if (statusCallback != null) statusCallback (new Progress (minp,"Scanning graph "+(i+1)+" of "+graphs.Length));
			
			OnScanStatus info = null;
			if (statusCallback != null) {
				info = delegate (Progress p) {
					p.progress = AstarMath.MapToRange (minp, maxp, p.progress);
					statusCallback (p);
				};
			}
			
			graph.ScanInternal (info);
			
			//statusCallback (new Progress (Mathfx.MapTo (0.05F,0.7F,(float)(i+1.9F)/(graphs.Length+1)),"Scanning graph "+(i+1)+" of "+graphs.Length+" - Assigning graph indices"));
			
			graph.GetNodes (delegate (GraphNode node) {
				node.GraphIndex = (uint)i;
				return true;
			});
			
			if (OnGraphPostScan != null) {
				if (statusCallback != null) statusCallback (new Progress (AstarMath.MapToRange (0.1F,0.7F,(float)(i+0.95F)/(graphs.Length)),"Scanning graph "+(i+1)+" of "+graphs.Length+" - Post processing"));
				OnGraphPostScan (graph);
			}
			
		}
		
		if (statusCallback != null) statusCallback (new Progress (0.8F,"Post processing graphs"));
		
		if (OnPostScan != null) {
			OnPostScan (this);
		}
		GraphModifier.TriggerEvent (GraphModifier.EventType.PostScan);
		
		ApplyLinks ();
		
		//statusCallback (new Progress (0.85F,"Applying links"));
		try {
			FlushWorkItems();
		} catch (System.Exception e) {
			Debug.LogException (e);
		}
		
		isScanning = false;
		
		
		if (statusCallback != null) statusCallback (new Progress (0.90F,"Computing areas"));
		
		FloodFill ();
		
		//statusCallback (new Progress (0.92F,"Updating misc. data"));
		
		
		VerifyIntegrity ();
		
		if (statusCallback != null) statusCallback (new Progress (0.95F,"Late post processing"));
		
		if (OnLatePostScan != null) {
			OnLatePostScan (this);
		}
		GraphModifier.TriggerEvent (GraphModifier.EventType.LatePostScan);
		
		//Perform any blocking actions and unblock (probably, some tasks might take a few frames)
		PerformBlockingActions(true);
		
		lastScanTime = (float)(System.DateTime.UtcNow-startTime).TotalSeconds;//Time.realtimeSinceStartup-startTime;
		
		System.GC.Collect ();
		
		AstarLog ("Scanning - Process took "+(lastScanTime*1000).ToString ("0")+" ms to complete");
		
	}
	
	/** Applies links to the scanned graphs. Called right after #OnPostScan and before #FloodFill(). */
	public void ApplyLinks () {
		// Links are currently not supported by the beta version

		if (astarData.userConnections != null && astarData.userConnections.Length > 0) {
			Debug.LogWarning ("<b>Deleting all links now</b>, but saving graph data in backup variable.\nCreating replacement links using the new system, stored under the <i>Links</i> GameObject.");
			
			GameObject root = new GameObject("Links");
			
			Dictionary<Int3,GameObject> cache = new Dictionary<Int3, GameObject>();
			for (int i=0;i<astarData.userConnections.Length;i++) {
				UserConnection conn = astarData.userConnections[i];
				GameObject a = cache.ContainsKey((Int3)conn.p1) ? cache[(Int3)conn.p1] : new GameObject("Link "+i);
				GameObject b = cache.ContainsKey((Int3)conn.p2) ? cache[(Int3)conn.p2] : new GameObject("Link "+i);
				a.transform.parent = root.transform;
				b.transform.parent = root.transform;
				cache[(Int3)conn.p1] = a;
				cache[(Int3)conn.p2] = b;
				
				a.transform.position = conn.p1;
				b.transform.position = conn.p2;
				
				NodeLink ln = a.AddComponent<NodeLink>();
				ln.end = b.transform;
				ln.deleteConnection = !conn.enable;
			}
			
			astarData.userConnections = null;
			astarData.data_backup = astarData.GetData();

			throw new System.NotSupportedException ("<b>Links have been deprecated</b>. Please use the component <b>NodeLink</b> instead. Create two GameObjects around the points" +
			                                        " you want to link, then press <b>Cmd+Alt+L</b> ( <b>Ctrl+Alt+L</b> on windows) to link them. See <b>Menubar -> Edit -> Pathfinding</b>.");
		}


	}
	
#endregion
	
	private static int waitForPathDepth = 0;
	
	/** Wait for the specified path to be calculated.
	 * Normally it takes a few frames for a path to get calculated and returned.
	 * This function will ensure that the path will be calculated when this function returns
	 * and that the callback for that path has been called.
	 * 
	 * \note Do not confuse this with Pathfinding.Path.WaitForPath. This one will halt all operations until the path has been calculated
	 * while Pathfinding.Path.WaitForPath will wait using yield until it has been calculated.
	 * 
	 * If requesting a lot of paths in one go and waiting for the last one to complete,
	 * it will calculate most of the paths in the queue (only most if using multithreading, all if not using multithreading).
	 * 
	 * Use this function only if you really need to.
	 * There is a point to spreading path calculations out over several frames.
	 * It smoothes out the framerate and makes sure requesting a large
	 * number of paths at the same time does not cause lag.
	 * 
	 * \note Graph updates and other callbacks might get called during the execution of this function.
	 * 
	 * When the pathfinder is shutting down. I.e in OnDestroy, this function will not do anything.
	 * 
	 * \param p The path to wait for. The path must be started, otherwise an exception will be thrown.
	 * 
	 * \throws Exception if pathfinding is not initialized properly for this scene (most likely no AstarPath object exists)
	 * or if the path has not been started yet.
	 * Also throws an exception if critical errors ocurr such as when the pathfinding threads have crashed (which should not happen in normal cases).
	 * This prevents an infinite loop while waiting for the path.
	 * 
	 * \see Pathfinding.Path.WaitForPath
	 */
	public static void WaitForPath (Path p) {
		
		if (active == null)
			throw new System.Exception ("Pathfinding is not correctly initialized in this scene (yet?). " +
				"AstarPath.active is null.\nDo not call this function in Awake");
		
		if (p == null) throw new System.ArgumentNullException ("Path must not be null");
		
		if (active.pathQueue.IsTerminating) return;
		
		if (p.GetState () == PathState.Created){
			throw new System.Exception ("The specified path has not been started yet.");
		}
		
		waitForPathDepth++;
		
		if (waitForPathDepth == 5) {
			Debug.LogError ("You are calling the WaitForPath function recursively (maybe from a path callback). Please don't do this.");
		}
		
		if (p.GetState() < PathState.ReturnQueue) {
			if (IsUsingMultithreading) {
				
				while (p.GetState() < PathState.ReturnQueue) {
					if (active.pathQueue.IsTerminating) {
						waitForPathDepth--;
						throw new System.Exception ("Pathfinding Threads seems to have crashed.");
					}
					
					//Wait for threads to calculate paths
					Thread.Sleep(1);
					active.PerformBlockingActions();
				}
			} else {
				while (p.GetState() < PathState.ReturnQueue) {
					if (active.pathQueue.IsEmpty && p.GetState () != PathState.Processing) {
						waitForPathDepth--;
						throw new System.Exception ("Critical error. Path Queue is empty but the path state is '" + p.GetState() + "'");
					}
					
					//Calculate some paths
					threadEnumerator.MoveNext ();
					active.PerformBlockingActions();
				}
			}
		}
		
		active.ReturnPaths (false);
		
		waitForPathDepth--;
	}
	
	/** Will send a callback when it is safe to update nodes. This is defined as between the path searches.
	  * This callback will only be sent once and is nulled directly after the callback has been sent.
	  * When using more threads than one, calling this often might decrease pathfinding performance due to a lot of idling in the threads.
	  * Not performance as in it will use much CPU power,
	  * but performance as in the number of paths per second will probably go down (though your framerate might actually increase a tiny bit)
	  * 
	  * You should only call this function from the main unity thread (i.e normal game code).
	  * 
	  * \warning Note that if you do not set \a threadSafe to true, the callback might not be called from the Unity thread,
	  * DO NOT call any part of the Unity API from those callbacks except for Debug.Log
	  * 
	  * \code
Node node = AstarPath.active.GetNearest (transform.position).node;
AstarPath.RegisterSafeUpdate (delegate () {
	node.walkable = false;
}, false);
\endcode

\code
Node node = AstarPath.active.GetNearest (transform.position).node;
AstarPath.RegisterSafeUpdate (delegate () {
	node.position = (Int3)transform.position;
}, true);
\endcode
	  * Note that the second example uses transform in the callback, and must thus be threadSafe.
	  */
	public static void RegisterSafeUpdate (OnVoidDelegate callback, bool threadSafe) {
		if (callback == null || !Application.isPlaying) {
			return;
		}
		
		if (active.pathQueue.AllReceiversBlocked) {
			// We need to lock here since we cannot be sure that this is the Unity Thread
			// and therefore we cannot be sure that some other thread will not unblock the queue while we are processing the callback
			active.pathQueue.Lock();
			try {
				//Check again
				if (active.pathQueue.AllReceiversBlocked) {
					callback ();
					return;
				}
			} finally {
				active.pathQueue.Unlock();
			}
		}
		
		lock (safeUpdateLock) {			
			if (threadSafe)
				OnThreadSafeCallback += callback;
			else
				OnSafeCallback += callback;
		}
		//Block path queue so that the above callbacks may be called
		active.pathQueue.Block();
		
	}
	
	private static void InterruptPathfinding () {
		active.pathQueue.Block();
	}
	
	/** Puts the Path in queue for calculation.
	  * The callback specified when constructing the path will be called when the path has been calculated.
	  * Usually you should use the Seeker component instead of calling this function directly.
	  * 
	  * \param p The path that should be put in queue for calculation
	  * \param pushToFront If true, the path will be pushed to the front of the queue, bypassing all waiting paths and making it the next path to be calculated.
	  * This can be useful if you have a path which you want to prioritize over all others. Be careful to not overuse it though.
	  * If too many paths are put in the front of the queue often, this can lead to normal paths having to wait a very long time before being calculated.
	  */
	public static void StartPath (Path p, bool pushToFront = false) {
		
		if (active == null) {
			Debug.LogError ("There is no AstarPath object in the scene");
			return;
		}
		
		if (p.GetState() != PathState.Created) {
			throw new System.Exception ("The path has an invalid state. Expected " + PathState.Created + " found " + p.GetState() + "\n" +
				"Make sure you are not requesting the same path twice");
		}
		
		if (active.pathQueue.IsTerminating) {
			p.Error ();
			p.LogError ("No new paths are accepted");
			return;
		}
		
		if (active.graphs == null || active.graphs.Length == 0) {
			Debug.LogError ("There are no graphs in the scene");
			p.Error ();
			p.LogError ("There are no graphs in the scene");
			Debug.LogError (p.errorLog);
			return;
		}
		
		p.Claim (active);
		
		
		//Will increment to PathQueue
		p.AdvanceState (PathState.PathQueue);
		if (pushToFront) {
			active.pathQueue.PushFront (p);
		} else {
			active.pathQueue.Push (p);
		}
	}
	

	/** Terminates eventual pathfinding threads when the application quits.
	 */
	public void OnApplicationQuit () {
		if (logPathResults == PathLog.Heavy) {
			Debug.Log ("+++ Application Quitting - Cleaning Up +++");
		}
		
		OnDestroy ();
		
		
#if !UNITY_WEBPLAYER
		if (threads == null) return;
		//Unity webplayer does not support Abort (even though it supports starting threads). Hope that UnityPlayer aborts the threads
		for (int i=0;i<threads.Length;i++) {
			if ( threads[i] != null && threads[i].IsAlive ) threads[i].Abort ();
		}
#endif
	}
	
#region MainThreads
	
	/** A temporary queue for paths which weren't returned due to large processing time.
	 * When some time limit is exceeded in ReturnPaths, paths are put on this queue until the next frame.
	 * \see ReturnPaths
	 */
	private Path pathReturnPop;
	
	/** Returns all paths in the return stack.
	  * Paths which have been processed are put in the return stack.
	  * This function will pop all items from the stack and return them to e.g the Seeker requesting them.
	  * 
	  * \param timeSlice Do not return all paths at once if it takes a long time, instead return some and wait until the next call.
	  */
	public void ReturnPaths (bool timeSlice) {
		
		//Pop all items from the stack
		Path p = pathReturnStack.PopAll ();
		
		if(pathReturnPop == null) {
			pathReturnPop = p;
		} else {
			Path tail = pathReturnPop;
			while (tail.next != null) tail = tail.next;
			tail.next = p;
		}
		
		//Hard coded limit on 0.5 ms
		long targetTick = timeSlice ? System.DateTime.UtcNow.Ticks + 1 * 10000 : 0;
		
		int counter = 0;
		//Loop through the linked list and return all paths
		while (pathReturnPop != null) {
			
			//Move to the next path
			Path prev = pathReturnPop;
			pathReturnPop = pathReturnPop.next;
			
			/* Remove the reference to prevent possible memory leaks
			If for example the first path computed was stored somewhere,
			it would through the linked list contain references to all comming paths to be computed,
			and thus the nodes those paths searched.
			That adds up to a lot of memory not being released */
			prev.next = null;
			
			//Return the path
			prev.ReturnPath ();
			
			//Will increment to Returned
			//However since multithreading is annoying, it might be set to ReturnQueue for a small time until the pathfinding calculation
			//thread advanced the state as well
			prev.AdvanceState (PathState.Returned);
			
			prev.ReleaseSilent (this);
			
			counter++;
			//At least 5 paths will be returned, even if timeSlice is enabled
			if (counter > 5 && timeSlice) {
				counter = 0;
				if (System.DateTime.UtcNow.Ticks >= targetTick) {
					return;
				}
			}
		}
	}
	
	/** Main pathfinding function (multithreaded). This function will calculate the paths in the pathfinding queue when multithreading is enabled.
	 * \see CalculatePaths
	 * \astarpro 
	 */
	private static 
	void CalculatePathsThreaded (System.Object _threadInfo) {
		
		PathThreadInfo threadInfo;
		
		try {
			threadInfo = (PathThreadInfo)_threadInfo;
		} catch (System.Exception e) {
			Debug.LogError ("Arguments to pathfinding threads must be of type ThreadStartInfo\n"+e);
			throw new System.ArgumentException ("Argument must be of type ThreadStartInfo",e);
		}
		
		AstarPath astar = threadInfo.astar;
		
		try {
			
			//Initialize memory for this thread
			PathHandler runData = threadInfo.runData;
			
			if (runData.nodes == null)
				throw new System.NullReferenceException ("NodeRuns must be assigned to the threadInfo.runData.nodes field before threads are started\nthreadInfo is an argument to the thread functions");
			
			//Max number of ticks before yielding/sleeping
			long maxTicks = (long)(astar.maxFrameTime*10000);
			long targetTick = System.DateTime.UtcNow.Ticks + maxTicks;
			
			while (true) {
				
				//The path we are currently calculating
				Path p = astar.pathQueue.Pop();
				
				//Max number of ticks we are allowed to continue working in one run
				//One tick is 1/10000 of a millisecond
				maxTicks = (long)(astar.maxFrameTime*10000);
				
				//Trying to prevent simple modding to allow more than one thread
				if ( threadInfo.threadIndex > 0 ) {
					throw new System.Exception ("Thread Error");
				}
				
				AstarProfiler.StartFastProfile (0);
				p.PrepareBase (runData);
				
				//Now processing the path
				//Will advance to Processing
				p.AdvanceState (PathState.Processing);
				
				//Call some callbacks
				if (OnPathPreSearch != null) {
					OnPathPreSearch (p);
				}
				
				//Tick for when the path started, used for calculating how long time the calculation took
				long startTicks = System.DateTime.UtcNow.Ticks;
				long totalTicks = 0;
				
				//Prepare the path
				p.Prepare ();
				
				AstarProfiler.EndFastProfile (0);
				
				if (!p.IsDone()) {
					
					//For debug uses, we set the last computed path to p, so we can view debug info on it in the editor (scene view).
					astar.debugPath = p;
					
					AstarProfiler.StartFastProfile (1);
					
					//Initialize the path, now ready to begin search
					p.Initialize ();
					
					AstarProfiler.EndFastProfile (1);
					
					//The error can turn up in the Init function
					while (!p.IsDone ()) {
						//Do some work on the path calculation.
						//The function will return when it has taken too much time
						//or when it has finished calculation
						AstarProfiler.StartFastProfile (2);
						p.CalculateStep (targetTick);
						p.searchIterations++;
						
						AstarProfiler.EndFastProfile (2);
						
						//If the path has finished calculation, we can break here directly instead of sleeping
						if (p.IsDone ()) break;
						
						//Yield/sleep so other threads can work
						totalTicks += System.DateTime.UtcNow.Ticks-startTicks;
						Thread.Sleep(0);
						startTicks = System.DateTime.UtcNow.Ticks;
						
						targetTick = startTicks + maxTicks;
						
						//Cancel function (and thus the thread) if no more paths should be accepted.
						//This is done when the A* object is about to be destroyed
						//The path is returned and then this function will be terminated
						if (astar.pathQueue.IsTerminating) {
							p.Error ();
						}
					}
					
					totalTicks += System.DateTime.UtcNow.Ticks-startTicks;
					p.duration = totalTicks*0.0001F;
					
				}
				
				// Cleans up node tagging and other things
				p.Cleanup ();
				
				AstarProfiler.StartFastProfile (9);
				
				//Log path results
				astar.LogPathResults (p);
				
				if (OnPathPostSearch != null) {
					OnPathPostSearch (p);
				}
				
				//Push the path onto the return stack
				//It will be detected by the main Unity thread and returned as fast as possible (the next late update hopefully)
				pathReturnStack.Push (p);
				
				//Will advance to ReturnQueue
				p.AdvanceState (PathState.ReturnQueue);
				
				AstarProfiler.EndFastProfile (9);
				
				//Wait a bit if we have calculated a lot of paths
				if (System.DateTime.UtcNow.Ticks > targetTick) {
					Thread.Sleep(1);
					targetTick = System.DateTime.UtcNow.Ticks + maxTicks;
				}
			}
		} catch (System.Exception e) {
			if (e is System.Threading.ThreadAbortException || e is ThreadControlQueue.QueueTerminationException) {
				if (astar.logPathResults == PathLog.Heavy)
					Debug.LogWarning ("Shutting down pathfinding thread #"+threadInfo.threadIndex+" with Thread.Abort call");
				return;
			}
			Debug.LogException (e);
			Debug.LogError ("Unhandled exception during pathfinding. Terminating.");
			//Unhandled exception, kill pathfinding
			astar.pathQueue.TerminateReceivers();
		}
		
		Debug.LogError ("Error : This part should never be reached.");
		astar.pathQueue.ReceiverTerminated ();
	}
	
	/** Main pathfinding function. This function will calculate the paths in the pathfinding queue
	 * \see CalculatePaths
	 */
	private static IEnumerator CalculatePaths (System.Object _threadInfo) {
		
		PathThreadInfo threadInfo;
		try {
			threadInfo = (PathThreadInfo)_threadInfo;
		} catch (System.Exception e) {
			Debug.LogError ("Arguments to pathfinding threads must be of type ThreadStartInfo\n"+e);
			throw new System.ArgumentException ("Argument must be of type ThreadStartInfo",e);
		}
		
		int numPaths = 0;
		
		//Initialize memory for this thread
		PathHandler runData = threadInfo.runData;
		
		AstarPath astar = threadInfo.astar;
		
		if (runData.nodes == null)
			throw new System.NullReferenceException ("NodeRuns must be assigned to the threadInfo.runData.nodes field before threads are started\n" +
				"threadInfo is an argument to the thread functions");
		
		//Max number of ticks before yielding/sleeping
		long maxTicks = (long)(active.maxFrameTime*10000);
		long targetTick = System.DateTime.UtcNow.Ticks + maxTicks;
		
		while (true) {
			
			//The path we are currently calculating
			Path p = null;
			
			AstarProfiler.StartProfile ("Path Queue");
			
			//Try to get the next path to be calculated
			bool blockedBefore = false;
			while (p == null) {
				try {
					p = astar.pathQueue.PopNoBlock(blockedBefore);
					if (p == null) {
						blockedBefore = true;
					}
				} catch (ThreadControlQueue.QueueTerminationException) {
					yield break;
				}
				
				if (p == null) {
					AstarProfiler.EndProfile ();
					yield return null;
					AstarProfiler.StartProfile ("Path Queue");
				}
			}
			
			
			AstarProfiler.EndProfile ();
			
			AstarProfiler.StartProfile ("Path Calc");
			
			//Max number of ticks we are allowed to continue working in one run
			//One tick is 1/10000 of a millisecond
			maxTicks = (long)(active.maxFrameTime*10000);
			
			p.PrepareBase (runData);
			
			//Now processing the path
			//Will advance to Processing
			p.AdvanceState (PathState.Processing);
			
			//Call some callbacks
			if (OnPathPreSearch != null) {
				OnPathPreSearch (p);
			}
			
			numPaths++;
			
			//Tick for when the path started, used for calculating how long time the calculation took
			long startTicks = System.DateTime.UtcNow.Ticks;
			long totalTicks = 0;
			
			AstarProfiler.StartFastProfile(8);
			
			AstarProfiler.StartFastProfile(0);
			//Prepare the path
			AstarProfiler.StartProfile ("Path Prepare");
			p.Prepare ();
			AstarProfiler.EndProfile ("Path Prepare");
			AstarProfiler.EndFastProfile (0);
			
			if (!p.IsDone()) {
				
				//For debug uses, we set the last computed path to p, so we can view debug info on it in the editor (scene view).
				active.debugPath = p;
				
				//Initialize the path, now ready to begin search
				AstarProfiler.StartProfile ("Path Initialize");
				p.Initialize ();
				AstarProfiler.EndProfile ();
				
				//The error can turn up in the Init function
				while (!p.IsDone ()) {
					//Do some work on the path calculation.
					//The function will return when it has taken too much time
					//or when it has finished calculation
					AstarProfiler.StartFastProfile(2);
					
					AstarProfiler.StartProfile ("Path Calc Step");
					p.CalculateStep (targetTick);
					AstarProfiler.EndFastProfile(2);
					p.searchIterations++;
					
					AstarProfiler.EndProfile ();
					
					//If the path has finished calculation, we can break here directly instead of sleeping
					if (p.IsDone ()) break;
					
					AstarProfiler.EndFastProfile(8);
					totalTicks += System.DateTime.UtcNow.Ticks-startTicks;
					//Yield/sleep so other threads can work
						
					AstarProfiler.EndProfile ();
					yield return null;
					AstarProfiler.StartProfile ("Path Calc");
					
					startTicks = System.DateTime.UtcNow.Ticks;
					AstarProfiler.StartFastProfile(8);
					
					//Cancel function (and thus the thread) if no more paths should be accepted.
					//This is done when the A* object is about to be destroyed
					//The path is returned and then this function will be terminated (see similar IF statement higher up in the function)
					if (astar.pathQueue.IsTerminating) {
						p.Error ();
					}
					
					targetTick = System.DateTime.UtcNow.Ticks + maxTicks;
				}
				
				totalTicks += System.DateTime.UtcNow.Ticks-startTicks;
				p.duration = totalTicks*0.0001F;
				
			}
			
			// Cleans up node tagging and other things
			p.Cleanup ();
			
			//Log path results
			AstarProfiler.StartProfile ("Log Path Results");
			active.LogPathResults (p);
			AstarProfiler.EndProfile ();
			
			AstarProfiler.EndFastProfile(8);
			
			AstarProfiler.StartFastProfile(13);
			if (OnPathPostSearch != null) {
				OnPathPostSearch (p);
			}
			AstarProfiler.EndFastProfile(13);
			
			//Push the path onto the return stack
			//It will be detected by the main Unity thread and returned as fast as possible (the next late update)
			pathReturnStack.Push (p);
			
			p.AdvanceState (PathState.ReturnQueue);
			
			AstarProfiler.EndProfile ();
			
			//Wait a bit if we have calculated a lot of paths
			if (System.DateTime.UtcNow.Ticks > targetTick) {
				yield return null;
				targetTick = System.DateTime.UtcNow.Ticks + maxTicks;
				numPaths = 0;
			}
		}
		
		//Debug.LogError ("Error : This part should never be reached");
	}
#endregion
	
	
	/** Returns the nearest node to a position using the specified NNConstraint.
	 Searches through all graphs for their nearest nodes to the specified position and picks the closest one.\n
	 Using the NNConstraint.None constraint.
	 \see Pathfinding.NNConstraint
	 */
	public NNInfo GetNearest (Vector3 position) {
		return GetNearest(position,NNConstraint.None);
	}
	
	/** Returns the nearest node to a position using the specified NNConstraint.
	 Searches through all graphs for their nearest nodes to the specified position and picks the closest one.
	 The NNConstraint can be used to specify constraints on which nodes can be chosen such as only picking walkable nodes.
	 \see Pathfinding.NNConstraint
	 */
	public NNInfo GetNearest (Vector3 position, NNConstraint constraint) {
		return GetNearest(position,constraint,null);
	}
	
	/** Returns the nearest node to a position using the specified NNConstraint.
	 Searches through all graphs for their nearest nodes to the specified position and picks the closest one.
	 The NNConstraint can be used to specify constraints on which nodes can be chosen such as only picking walkable nodes.
	 \see Pathfinding.NNConstraint
	 */
	public NNInfo GetNearest (Vector3 position, NNConstraint constraint, GraphNode hint) {
		
		if (graphs == null) { return new NNInfo(); }
		
		float minDist = float.PositiveInfinity;//Math.Infinity;
		NNInfo nearestNode = new NNInfo ();
		int nearestGraph = -1;
		
		for (int i=0;i<graphs.Length;i++) {
			
			NavGraph graph = graphs[i];
			
			if (graph == null) continue;
			
			//Check if this graph should be searched
			if (!constraint.SuitableGraph (i,graph)) {
				continue;
			}
			
			NNInfo nnInfo;
			if (fullGetNearestSearch) {
				nnInfo = graph.GetNearestForce (position, constraint);
			} else {
				nnInfo = graph.GetNearest (position, constraint);
			}
			
			GraphNode node = nnInfo.node;
			
			if (node == null) {
				continue;
			}
			
			float dist = ((Vector3)nnInfo.clampedPosition-position).magnitude;
			
			if (prioritizeGraphs && dist < prioritizeGraphsLimit) {
				//The node is close enough, choose this graph and discard all others
				minDist = dist;
				nearestNode = nnInfo;
				nearestGraph = i;
				break;
			} else {
				if (dist < minDist) {
					minDist = dist;
					nearestNode = nnInfo;
					nearestGraph = i;
				}
			}
		}
		
		//No matches found
		if (nearestGraph == -1) {
			return nearestNode;
		}
		
		//Check if a constrained node has already been set
		if (nearestNode.constrainedNode != null) {
			nearestNode.node = nearestNode.constrainedNode;
			nearestNode.clampedPosition = nearestNode.constClampedPosition;
		}
		
		if (!fullGetNearestSearch && nearestNode.node != null && !constraint.Suitable (nearestNode.node)) {
			
			//Otherwise, perform a check to force the graphs to check for a suitable node
			NNInfo nnInfo = graphs[nearestGraph].GetNearestForce (position, constraint);
			
			if (nnInfo.node != null) {
				nearestNode = nnInfo;
			}
		}
		
		if (!constraint.Suitable (nearestNode.node) || (constraint.constrainDistance && (nearestNode.clampedPosition - position).sqrMagnitude > maxNearestNodeDistanceSqr)) {
			return new NNInfo();
		}
		
		return nearestNode;
	}
	
	/** Returns the node closest to the ray (slow).
	  * \warning This function is brute-force and very slow, it can barely be used once per frame */
	public GraphNode GetNearest (Ray ray) {
		
		if (graphs == null) { return null; }
		
		float minDist = Mathf.Infinity;
		GraphNode nearestNode = null;
		
		Vector3 lineDirection = ray.direction;
		Vector3 lineOrigin = ray.origin;
		
		for (int i=0;i<graphs.Length;i++) {
			
			NavGraph graph = graphs[i];
			
			graph.GetNodes (delegate (GraphNode node) {
	        	Vector3 pos = (Vector3)node.position;
				Vector3 p = lineOrigin+(Vector3.Dot(pos-lineOrigin,lineDirection)*lineDirection);
				
				float tmp = Mathf.Abs (p.x-pos.x);
				tmp *= tmp;
				if (tmp > minDist) return true;
				
				tmp = Mathf.Abs (p.z-pos.z);
				tmp *= tmp;
				if (tmp > minDist) return true;
				
				float dist = (p-pos).sqrMagnitude;
				
				if (dist < minDist) {
					minDist = dist;
					nearestNode = node;
				}
				return true;
			});
			
		}
		
		return nearestNode;
	}
}
