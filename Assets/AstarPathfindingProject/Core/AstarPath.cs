using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

#if NETFX_CORE
using Thread = Pathfinding.WindowsStore.Thread;
#else
using Thread = System.Threading.Thread;
#endif

[ExecuteInEditMode]
[AddComponentMenu("Pathfinding/Pathfinder")]
/// <summary>
/// Core component for the A* %Pathfinding System.
/// This class handles all of the pathfinding system, calculates all paths and stores the info.\n
/// This class is a singleton class, meaning there should only exist at most one active instance of it in the scene.\n
/// It might be a bit hard to use directly, usually interfacing with the pathfinding system is done through the <see cref="Pathfinding.Seeker"/> class.
///
/// \nosubgrouping
/// \ingroup relevant
/// </summary>
[HelpURL("http://arongranberg.com/astar/docs/class_astar_path.php")]
public class AstarPath : VersionedMonoBehaviour {
	/// <summary>The version number for the A* %Pathfinding Project</summary>
	public static readonly System.Version Version = new System.Version(4, 2, 15);

	/// <summary>Information about where the package was downloaded</summary>
	public enum AstarDistribution { WebsiteDownload, AssetStore, PackageManager };

	/// <summary>Used by the editor to guide the user to the correct place to download updates</summary>
	public static readonly AstarDistribution Distribution = AstarDistribution.WebsiteDownload;

	/// <summary>
	/// Which branch of the A* %Pathfinding Project is this release.
	/// Used when checking for updates so that
	/// users of the development versions can get notifications of development
	/// updates.
	/// </summary>
	public static readonly string Branch = "master";

	/// <summary>
	/// See Pathfinding.AstarData
	/// Deprecated:
	/// </summary>
	[System.Obsolete]
	public System.Type[] graphTypes {
		get {
			return data.graphTypes;
		}
	}

	/// <summary>Holds all graph data</summary>
	[UnityEngine.Serialization.FormerlySerializedAs("astarData")]
	public AstarData data;

	/// <summary>
	/// Holds all graph data.
	/// Deprecated: The 'astarData' field has been renamed to 'data'
	/// </summary>
	[System.Obsolete("The 'astarData' field has been renamed to 'data'")]
	public AstarData astarData { get { return data; } }

	/// <summary>
	/// Returns the active AstarPath object in the scene.
	/// Note: This is only set if the AstarPath object has been initialized (which happens in Awake).
	/// </summary>
#if UNITY_4_6 || UNITY_4_3
	public static new AstarPath active;
#else
	public static AstarPath active;
#endif

	/// <summary>Shortcut to Pathfinding.AstarData.graphs</summary>
	public NavGraph[] graphs {
		get {
			if (data == null)
				data = new AstarData();
			return data.graphs;
		}
	}

	#region InspectorDebug
	/// <summary>
	/// @name Inspector - Debug
	/// @{
	/// </summary>

	/// <summary>
	/// Visualize graphs in the scene view (editor only).
	/// [Open online documentation to see images]
	/// </summary>
	public bool showNavGraphs = true;

	/// <summary>
	/// Toggle to show unwalkable nodes.
	///
	/// Note: Only relevant in the editor
	///
	/// See: <see cref="unwalkableNodeDebugSize"/>
	/// </summary>
	public bool showUnwalkableNodes = true;

	/// <summary>
	/// The mode to use for drawing nodes in the sceneview.
	///
	/// Note: Only relevant in the editor
	///
	/// See: Pathfinding.GraphDebugMode
	/// </summary>
	public GraphDebugMode debugMode;

	/// <summary>
	/// Low value to use for certain <see cref="debugMode"/> modes.
	/// For example if <see cref="debugMode"/> is set to G, this value will determine when the node will be completely red.
	///
	/// Note: Only relevant in the editor
	///
	/// See: <see cref="debugRoof"/>
	/// See: <see cref="debugMode"/>
	/// </summary>
	public float debugFloor = 0;

	/// <summary>
	/// High value to use for certain <see cref="debugMode"/> modes.
	/// For example if <see cref="debugMode"/> is set to G, this value will determine when the node will be completely green.
	///
	/// For the penalty debug mode, the nodes will be colored green when they have a penalty less than <see cref="debugFloor"/> and red
	/// when their penalty is greater or equal to this value and something between red and green otherwise.
	///
	/// Note: Only relevant in the editor
	///
	/// See: <see cref="debugFloor"/>
	/// See: <see cref="debugMode"/>
	/// </summary>
	public float debugRoof = 20000;

	/// <summary>
	/// If set, the <see cref="debugFloor"/> and <see cref="debugRoof"/> values will not be automatically recalculated.
	///
	/// Note: Only relevant in the editor
	/// </summary>
	public bool manualDebugFloorRoof = false;


	/// <summary>
	/// If enabled, nodes will draw a line to their 'parent'.
	/// This will show the search tree for the latest path.
	///
	/// Note: Only relevant in the editor
	///
	/// TODO: Add a showOnlyLastPath flag to indicate whether to draw every node or only the ones visited by the latest path.
	/// </summary>
	public bool showSearchTree = false;

	/// <summary>
	/// Size of the red cubes shown in place of unwalkable nodes.
	///
	/// Note: Only relevant in the editor. Does not apply to grid graphs.
	/// See: <see cref="showUnwalkableNodes"/>
	/// </summary>
	public float unwalkableNodeDebugSize = 0.3F;

	/// <summary>
	/// The amount of debugging messages.
	/// Use less debugging to improve performance (a bit) or just to get rid of the Console spamming.
	/// Use more debugging (heavy) if you want more information about what the pathfinding scripts are doing.
	/// The InGame option will display the latest path log using in-game GUI.
	///
	/// [Open online documentation to see images]
	/// </summary>
	public PathLog logPathResults = PathLog.Normal;

	/// <summary>@}</summary>
	#endregion

	#region InspectorSettings
	/// <summary>
	/// @name Inspector - Settings
	/// @{
	/// </summary>

	/// <summary>
	/// Maximum distance to search for nodes.
	/// When searching for the nearest node to a point, this is the limit (in world units) for how far away it is allowed to be.
	///
	/// This is relevant if you try to request a path to a point that cannot be reached and it thus has to search for
	/// the closest node to that point which can be reached (which might be far away). If it cannot find a node within this distance
	/// then the path will fail.
	///
	/// [Open online documentation to see images]
	///
	/// See: Pathfinding.NNConstraint.constrainDistance
	/// </summary>
	public float maxNearestNodeDistance = 100;

	/// <summary>
	/// Max Nearest Node Distance Squared.
	/// See: <see cref="maxNearestNodeDistance"/>
	/// </summary>
	public float maxNearestNodeDistanceSqr {
		get { return maxNearestNodeDistance*maxNearestNodeDistance; }
	}

	/// <summary>
	/// If true, all graphs will be scanned during Awake.
	/// This does not include loading from the cache.
	/// If you disable this, you will have to call \link Scan AstarPath.active.Scan() \endlink yourself to enable pathfinding.
	/// Alternatively you could load a saved graph from a file.
	///
	/// See: <see cref="Scan"/>
	/// See: <see cref="ScanAsync"/>
	/// </summary>
	public bool scanOnStartup = true;

	/// <summary>
	/// Do a full GetNearest search for all graphs.
	/// Additional searches will normally only be done on the graph which in the first fast search seemed to have the closest node.
	/// With this setting on, additional searches will be done on all graphs since the first check is not always completely accurate.\n
	/// More technically: GetNearestForce on all graphs will be called if true, otherwise only on the one graph which's GetNearest search returned the best node.\n
	/// Usually faster when disabled, but higher quality searches when enabled.
	/// When using a a navmesh or recast graph, for best quality, this setting should be combined with the Pathfinding.NavMeshGraph.accurateNearestNode setting set to true.
	/// Note: For the PointGraph this setting doesn't matter much as it has only one search mode.
	/// </summary>
	public bool fullGetNearestSearch = false;

	/// <summary>
	/// Prioritize graphs.
	/// Graphs will be prioritized based on their order in the inspector.
	/// The first graph which has a node closer than <see cref="prioritizeGraphsLimit"/> will be chosen instead of searching all graphs.
	/// </summary>
	public bool prioritizeGraphs = false;

	/// <summary>
	/// Distance limit for <see cref="prioritizeGraphs"/>.
	/// See: <see cref="prioritizeGraphs"/>
	/// </summary>
	public float prioritizeGraphsLimit = 1F;

	/// <summary>
	/// Reference to the color settings for this AstarPath object.
	/// Color settings include for example which color the nodes should be in, in the sceneview.
	/// </summary>
	public AstarColor colorSettings;

	/// <summary>
	/// Stored tag names.
	/// See: AstarPath.FindTagNames
	/// See: AstarPath.GetTagNames
	/// </summary>
	[SerializeField]
	protected string[] tagNames = null;

	/// <summary>
	/// The distance function to use as a heuristic.
	/// The heuristic, often referred to as just 'H' is the estimated cost from a node to the target.
	/// Different heuristics affect how the path picks which one to follow from multiple possible with the same length
	/// See: <see cref="Pathfinding.Heuristic"/> for more details and descriptions of the different modes.
	/// See: <a href="https://en.wikipedia.org/wiki/Admissible_heuristic">Wikipedia: Admissible heuristic</a>
	/// See: <a href="https://en.wikipedia.org/wiki/A*_search_algorithm">Wikipedia: A* search algorithm</a>
	/// See: <a href="https://en.wikipedia.org/wiki/Dijkstra%27s_algorithm">Wikipedia: Dijkstra's Algorithm</a>
	/// </summary>
	public Heuristic heuristic = Heuristic.Euclidean;

	/// <summary>
	/// The scale of the heuristic.
	/// If a value lower than 1 is used, the pathfinder will search more nodes (slower).
	/// If 0 is used, the pathfinding algorithm will be reduced to dijkstra's algorithm. This is equivalent to setting <see cref="heuristic"/> to None.
	/// If a value larger than 1 is used the pathfinding will (usually) be faster because it expands fewer nodes, but the paths may no longer be the optimal (i.e the shortest possible paths).
	///
	/// Usually you should leave this to the default value of 1.
	///
	/// See: https://en.wikipedia.org/wiki/Admissible_heuristic
	/// See: https://en.wikipedia.org/wiki/A*_search_algorithm
	/// See: https://en.wikipedia.org/wiki/Dijkstra%27s_algorithm
	/// </summary>
	public float heuristicScale = 1F;

	/// <summary>
	/// Number of pathfinding threads to use.
	/// Multithreading puts pathfinding in another thread, this is great for performance on 2+ core computers since the framerate will barely be affected by the pathfinding at all.
	/// - None indicates that the pathfinding is run in the Unity thread as a coroutine
	/// - Automatic will try to adjust the number of threads to the number of cores and memory on the computer.
	///  Less than 512mb of memory or a single core computer will make it revert to using no multithreading.
	///
	/// It is recommended that you use one of the "Auto" settings that are available.
	/// The reason is that even if your computer might be beefy and have 8 cores.
	/// Other computers might only be quad core or dual core in which case they will not benefit from more than
	/// 1 or 3 threads respectively (you usually want to leave one core for the unity thread).
	/// If you use more threads than the number of cores on the computer it is mostly just wasting memory, it will not run any faster.
	/// The extra memory usage is not trivially small. Each thread needs to keep a small amount of data for each node in all the graphs.
	/// It is not the full graph data but it is proportional to the number of nodes.
	/// The automatic settings will inspect the machine it is running on and use that to determine the number of threads so that no memory is wasted.
	///
	/// The exception is if you only have one (or maybe two characters) active at time. Then you should probably just go with one thread always since it is very unlikely
	/// that you will need the extra throughput given by more threads. Keep in mind that more threads primarily increases throughput by calculating different paths on different
	/// threads, it will not calculate individual paths any faster.
	///
	/// Note that if you are modifying the pathfinding core scripts or if you are directly modifying graph data without using any of the
	/// safe wrappers (like <see cref="AddWorkItem)"/> multithreading can cause strange errors and pathfinding stopping to work if you are not careful.
	/// For basic usage (not modding the pathfinding core) it should be safe.
	///
	/// Note: WebGL does not support threads at all (since javascript is single-threaded) so no threads will be used on that platform.
	///
	/// See: CalculateThreadCount
	/// </summary>
	public ThreadCount threadCount = ThreadCount.One;

	/// <summary>
	/// Max number of milliseconds to spend each frame for pathfinding.
	/// At least 500 nodes will be searched each frame (if there are that many to search).
	/// When using multithreading this value is irrelevant.
	/// </summary>
	public float maxFrameTime = 1F;

	/// <summary>
	/// Throttle graph updates and batch them to improve performance.
	/// If toggled, graph updates will batched and executed less often (specified by <see cref="graphUpdateBatchingInterval)"/>.
	///
	/// This can have a positive impact on pathfinding throughput since the pathfinding threads do not need
	/// to be stopped as often, and it reduces the overhead per graph update.
	/// All graph updates are still applied however, they are just batched together so that more of them are
	/// applied at the same time.
	///
	/// However do not use this if you want minimal latency between a graph update being requested
	/// and it being applied.
	///
	/// This only applies to graph updates requested using the <see cref="UpdateGraphs"/> method. Not those requested
	/// using <see cref="RegisterSafeUpdate"/> or <see cref="AddWorkItem"/>.
	///
	/// If you want to apply graph updates immediately at some point, you can call <see cref="FlushGraphUpdates"/>.
	///
	/// See: graph-updates (view in online documentation for working links)
	/// </summary>
	public bool batchGraphUpdates = false;

	/// <summary>
	/// Minimum number of seconds between each batch of graph updates.
	/// If <see cref="batchGraphUpdates"/> is true, this defines the minimum number of seconds between each batch of graph updates.
	///
	/// This can have a positive impact on pathfinding throughput since the pathfinding threads do not need
	/// to be stopped as often, and it reduces the overhead per graph update.
	/// All graph updates are still applied however, they are just batched together so that more of them are
	/// applied at the same time.
	///
	/// Do not use this if you want minimal latency between a graph update being requested
	/// and it being applied.
	///
	/// This only applies to graph updates requested using the <see cref="UpdateGraphs"/> method. Not those requested
	/// using <see cref="RegisterSafeUpdate"/> or <see cref="AddWorkItem"/>.
	///
	/// See: graph-updates (view in online documentation for working links)
	/// </summary>
	public float graphUpdateBatchingInterval = 0.2F;

	/// <summary>
	/// Batch graph updates.
	/// Deprecated: This field has been renamed to <see cref="batchGraphUpdates"/>.
	/// </summary>
	[System.Obsolete("This field has been renamed to 'batchGraphUpdates'")]
	public bool limitGraphUpdates { get { return batchGraphUpdates; } set { batchGraphUpdates = value; } }

	/// <summary>
	/// Limit for how often should graphs be updated.
	/// Deprecated: This field has been renamed to <see cref="graphUpdateBatchingInterval"/>.
	/// </summary>
	[System.Obsolete("This field has been renamed to 'graphUpdateBatchingInterval'")]
	public float maxGraphUpdateFreq { get { return graphUpdateBatchingInterval; } set { graphUpdateBatchingInterval = value; } }

	/// <summary>@}</summary>
	#endregion

	#region DebugVariables
	/// <summary>
	/// @name Debug Members
	/// @{
	/// </summary>

#if ProfileAstar
	/// <summary>
	/// How many paths has been computed this run. From application start.\n
	/// Debugging variable
	/// </summary>
	public static int PathsCompleted = 0;

	public static System.Int64 TotalSearchedNodes = 0;
	public static System.Int64 TotalSearchTime = 0;
#endif

	/// <summary>
	/// The time it took for the last call to Scan() to complete.
	/// Used to prevent automatically rescanning the graphs too often (editor only)
	/// </summary>
	public float lastScanTime { get; private set; }

	/// <summary>
	/// The path to debug using gizmos.
	/// This is the path handler used to calculate the last path.
	/// It is used in the editor to draw debug information using gizmos.
	/// </summary>
	[System.NonSerialized]
	public PathHandler debugPathData;

	/// <summary>The path ID to debug using gizmos</summary>
	[System.NonSerialized]
	public ushort debugPathID;

	/// <summary>
	/// Debug string from the last completed path.
	/// Will be updated if <see cref="logPathResults"/> == PathLog.InGame
	/// </summary>
	string inGameDebugPath;

	/* @} */
	#endregion

	#region StatusVariables

	/// <summary>
	/// Backing field for <see cref="isScanning"/>.
	/// Cannot use an auto-property because they cannot be marked with System.NonSerialized.
	/// </summary>
	[System.NonSerialized]
	bool isScanningBacking;

	/// <summary>
	/// Set while any graphs are being scanned.
	/// It will be true up until the FloodFill is done.
	///
	/// Note: Not to be confused with graph updates.
	///
	/// Used to better support Graph Update Objects called for example in OnPostScan
	///
	/// See: IsAnyGraphUpdateQueued
	/// See: IsAnyGraphUpdateInProgress
	/// </summary>
	public bool isScanning { get { return isScanningBacking; } private set { isScanningBacking = value; } }

	/// <summary>
	/// Number of parallel pathfinders.
	/// Returns the number of concurrent processes which can calculate paths at once.
	/// When using multithreading, this will be the number of threads, if not using multithreading it is always 1 (since only 1 coroutine is used).
	/// See: IsUsingMultithreading
	/// </summary>
	public int NumParallelThreads {
		get {
			return pathProcessor.NumThreads;
		}
	}

	/// <summary>
	/// Returns whether or not multithreading is used.
	/// \exception System.Exception Is thrown when it could not be decided if multithreading was used or not.
	/// This should not happen if pathfinding is set up correctly.
	/// Note: This uses info about if threads are running right now, it does not use info from the settings on the A* object.
	/// </summary>
	public bool IsUsingMultithreading {
		get {
			return pathProcessor.IsUsingMultithreading;
		}
	}

	/// <summary>
	/// Returns if any graph updates are waiting to be applied.
	/// Deprecated: Use IsAnyGraphUpdateQueued instead
	/// </summary>
	[System.Obsolete("Fixed grammar, use IsAnyGraphUpdateQueued instead")]
	public bool IsAnyGraphUpdatesQueued { get { return IsAnyGraphUpdateQueued; } }

	/// <summary>
	/// Returns if any graph updates are waiting to be applied.
	/// Note: This is false while the updates are being performed.
	/// Note: This does *not* includes other types of work items such as navmesh cutting or anything added by <see cref="RegisterSafeUpdate"/> or <see cref="AddWorkItem"/>.
	/// </summary>
	public bool IsAnyGraphUpdateQueued { get { return graphUpdates.IsAnyGraphUpdateQueued; } }

	/// <summary>
	/// Returns if any graph updates are being calculated right now.
	/// Note: This does *not* includes other types of work items such as navmesh cutting or anything added by <see cref="RegisterSafeUpdate"/> or <see cref="AddWorkItem"/>.
	///
	/// See: IsAnyWorkItemInProgress
	/// </summary>
	public bool IsAnyGraphUpdateInProgress { get { return graphUpdates.IsAnyGraphUpdateInProgress; } }

	/// <summary>
	/// Returns if any work items are in progress right now.
	/// Note: This includes pretty much all types of graph updates.
	/// Such as normal graph updates, navmesh cutting and anything added by <see cref="RegisterSafeUpdate"/> or <see cref="AddWorkItem"/>.
	/// </summary>
	public bool IsAnyWorkItemInProgress { get { return workItems.workItemsInProgress; } }

	/// <summary>
	/// Returns if this code is currently being exectuted inside a work item.
	/// Note: This includes pretty much all types of graph updates.
	/// Such as normal graph updates, navmesh cutting and anything added by <see cref="RegisterSafeUpdate"/> or <see cref="AddWorkItem"/>.
	///
	/// In contrast to <see cref="IsAnyWorkItemInProgress"/> this is only true when work item code is being executed, it is not
	/// true in-between the updates to a work item that takes several frames to complete.
	/// </summary>
	internal bool IsInsideWorkItem { get { return workItems.workItemsInProgressRightNow; } }

	#endregion

	#region Callbacks
	/// <summary>@name Callbacks</summary>
	/* Callbacks to pathfinding events.
	 * These allow you to hook in to the pathfinding process.\n
	 * Callbacks can be used like this:
	 * \snippet MiscSnippets.cs AstarPath.Callbacks
	 */
	/// <summary>@{</summary>

	/// <summary>
	/// Called on Awake before anything else is done.
	/// This is called at the start of the Awake call, right after <see cref="active"/> has been set, but this is the only thing that has been done.\n
	/// Use this when you want to set up default settings for an AstarPath component created during runtime since some settings can only be changed in Awake
	/// (such as multithreading related stuff)
	/// <code>
	/// // Create a new AstarPath object on Start and apply some default settings
	/// public void Start () {
	///     AstarPath.OnAwakeSettings += ApplySettings;
	///     AstarPath astar = gameObject.AddComponent<AstarPath>();
	/// }
	///
	/// public void ApplySettings () {
	///     // Unregister from the delegate
	///     AstarPath.OnAwakeSettings -= ApplySettings;
	///     // For example threadCount should not be changed after the Awake call
	///     // so here's the only place to set it if you create the component during runtime
	///     AstarPath.active.threadCount = ThreadCount.One;
	/// }
	/// </code>
	/// </summary>
	public static System.Action OnAwakeSettings;

	/// <summary>Called for each graph before they are scanned</summary>
	public static OnGraphDelegate OnGraphPreScan;

	/// <summary>Called for each graph after they have been scanned. All other graphs might not have been scanned yet.</summary>
	public static OnGraphDelegate OnGraphPostScan;

	/// <summary>Called for each path before searching. Be careful when using multithreading since this will be called from a different thread.</summary>
	public static OnPathDelegate OnPathPreSearch;

	/// <summary>Called for each path after searching. Be careful when using multithreading since this will be called from a different thread.</summary>
	public static OnPathDelegate OnPathPostSearch;

	/// <summary>Called before starting the scanning</summary>
	public static OnScanDelegate OnPreScan;

	/// <summary>Called after scanning. This is called before applying links, flood-filling the graphs and other post processing.</summary>
	public static OnScanDelegate OnPostScan;

	/// <summary>Called after scanning has completed fully. This is called as the last thing in the Scan function.</summary>
	public static OnScanDelegate OnLatePostScan;

	/// <summary>Called when any graphs are updated. Register to for example recalculate the path whenever a graph changes.</summary>
	public static OnScanDelegate OnGraphsUpdated;

	/// <summary>
	/// Called when pathID overflows 65536 and resets back to zero.
	/// Note: This callback will be cleared every time it is called, so if you want to register to it repeatedly, register to it directly on receiving the callback as well.
	/// </summary>
	public static System.Action On65KOverflow;

	/// <summary>Deprecated:</summary>
	[System.ObsoleteAttribute]
	public System.Action OnGraphsWillBeUpdated;

	/// <summary>Deprecated:</summary>
	[System.ObsoleteAttribute]
	public System.Action OnGraphsWillBeUpdated2;

	/* @} */
	#endregion

	#region MemoryStructures

	/// <summary>Processes graph updates</summary>
	readonly GraphUpdateProcessor graphUpdates;

	/// <summary>Holds a hierarchical graph to speed up some queries like if there is a path between two nodes</summary>
	internal readonly HierarchicalGraph hierarchicalGraph = new HierarchicalGraph();

	/// <summary>
	/// Handles navmesh cuts.
	/// See: <see cref="Pathfinding.NavmeshCut"/>
	/// </summary>
	public readonly NavmeshUpdates navmeshUpdates = new NavmeshUpdates();

	/// <summary>Processes work items</summary>
	readonly WorkItemProcessor workItems;

	/// <summary>Holds all paths waiting to be calculated and calculates them</summary>
	PathProcessor pathProcessor;

	bool graphUpdateRoutineRunning = false;

	/// <summary>Makes sure QueueGraphUpdates will not queue multiple graph update orders</summary>
	bool graphUpdatesWorkItemAdded = false;

	/// <summary>
	/// Time the last graph update was done.
	/// Used to group together frequent graph updates to batches
	/// </summary>
	float lastGraphUpdate = -9999F;

	/// <summary>Held if any work items are currently queued</summary>
	PathProcessor.GraphUpdateLock workItemLock;

	/// <summary>Holds all completed paths waiting to be returned to where they were requested</summary>
	internal readonly PathReturnQueue pathReturnQueue;

	/// <summary>
	/// Holds settings for heuristic optimization.
	/// See: heuristic-opt (view in online documentation for working links)
	/// </summary>
	public EuclideanEmbedding euclideanEmbedding = new EuclideanEmbedding();

	#endregion

	/// <summary>
	/// Shows or hides graph inspectors.
	/// Used internally by the editor
	/// </summary>
	public bool showGraphs = false;

	/// <summary>
	/// The next unused Path ID.
	/// Incremented for every call to GetNextPathID
	/// </summary>
	private ushort nextFreePathID = 1;

	private AstarPath () {
		pathReturnQueue = new PathReturnQueue(this);

		// Make sure that the pathProcessor is never null
		pathProcessor = new PathProcessor(this, pathReturnQueue, 1, false);

		workItems = new WorkItemProcessor(this);
		graphUpdates = new GraphUpdateProcessor(this);

		// Forward graphUpdates.OnGraphsUpdated to AstarPath.OnGraphsUpdated
		graphUpdates.OnGraphsUpdated += () => {
			if (OnGraphsUpdated != null) {
				OnGraphsUpdated(this);
			}
		};
	}

	/// <summary>
	/// Returns tag names.
	/// Makes sure that the tag names array is not null and of length 32.
	/// If it is null or not of length 32, it creates a new array and fills it with 0,1,2,3,4 etc...
	/// See: AstarPath.FindTagNames
	/// </summary>
	public string[] GetTagNames () {
		if (tagNames == null || tagNames.Length != 32) {
			tagNames = new string[32];
			for (int i = 0; i < tagNames.Length; i++) {
				tagNames[i] = ""+i;
			}
			tagNames[0] = "Basic Ground";
		}
		return tagNames;
	}

	/// <summary>
	/// Used outside of play mode to initialize the AstarPath object even if it has not been selected in the inspector yet.
	/// This will set the <see cref="active"/> property and deserialize all graphs.
	///
	/// This is useful if you want to do changes to the graphs in the editor outside of play mode, but cannot be sure that the graphs have been deserialized yet.
	/// In play mode this method does nothing.
	/// </summary>
	public static void FindAstarPath () {
		if (Application.isPlaying) return;
		if (active == null) active = GameObject.FindObjectOfType<AstarPath>();
		if (active != null && (active.data.graphs == null || active.data.graphs.Length == 0)) active.data.DeserializeGraphs();
	}

	/// <summary>
	/// Tries to find an AstarPath object and return tag names.
	/// If an AstarPath object cannot be found, it returns an array of length 1 with an error message.
	/// See: AstarPath.GetTagNames
	/// </summary>
	public static string[] FindTagNames () {
		FindAstarPath();
		return active != null? active.GetTagNames () : new string[1] { "There is no AstarPath component in the scene" };
	}

	/// <summary>Returns the next free path ID</summary>
	internal ushort GetNextPathID () {
		if (nextFreePathID == 0) {
			nextFreePathID++;

			if (On65KOverflow != null) {
				System.Action tmp = On65KOverflow;
				On65KOverflow = null;
				tmp();
			}
		}
		return nextFreePathID++;
	}

	void RecalculateDebugLimits () {
		debugFloor = float.PositiveInfinity;
		debugRoof = float.NegativeInfinity;

		bool ignoreSearchTree = !showSearchTree || debugPathData == null;
		for (int i = 0; i < graphs.Length; i++) {
			if (graphs[i] != null && graphs[i].drawGizmos) {
				graphs[i].GetNodes(node => {
					if (node.Walkable && (ignoreSearchTree || Pathfinding.Util.GraphGizmoHelper.InSearchTree(node, debugPathData, debugPathID))) {
						if (debugMode == GraphDebugMode.Penalty) {
							debugFloor = Mathf.Min(debugFloor, node.Penalty);
							debugRoof = Mathf.Max(debugRoof, node.Penalty);
						} else if (debugPathData != null) {
							var rnode = debugPathData.GetPathNode(node);
							switch (debugMode) {
							case GraphDebugMode.F:
								debugFloor = Mathf.Min(debugFloor, rnode.F);
								debugRoof = Mathf.Max(debugRoof, rnode.F);
								break;
							case GraphDebugMode.G:
								debugFloor = Mathf.Min(debugFloor, rnode.G);
								debugRoof = Mathf.Max(debugRoof, rnode.G);
								break;
							case GraphDebugMode.H:
								debugFloor = Mathf.Min(debugFloor, rnode.H);
								debugRoof = Mathf.Max(debugRoof, rnode.H);
								break;
							}
						}
					}
				});
			}
		}

		if (float.IsInfinity(debugFloor)) {
			debugFloor = 0;
			debugRoof = 1;
		}

		// Make sure they are not identical, that will cause the color interpolation to fail
		if (debugRoof-debugFloor < 1) debugRoof += 1;
	}

	Pathfinding.Util.RetainedGizmos gizmos = new Pathfinding.Util.RetainedGizmos();

	/// <summary>Calls OnDrawGizmos on graph generators</summary>
	private void OnDrawGizmos () {
		// Make sure the singleton pattern holds
		// Might not hold if the Awake method
		// has not been called yet
		if (active == null) active = this;

		if (active != this || graphs == null) {
			return;
		}

		// In Unity one can select objects in the scene view by simply clicking on them with the mouse.
		// Graph gizmos interfere with this however. If we would draw a mesh here the user would
		// not be able to select whatever was behind it because the gizmos would block them.
		// (presumably Unity cannot associate the gizmos with the AstarPath component because we are using
		// Graphics.DrawMeshNow to draw most gizmos). It turns out that when scene picking happens
		// then Event.current.type will be 'mouseUp'. We will therefore ignore all events which are
		// not repaint events to make sure that the gizmos do not interfere with any kind of scene picking.
		// This will not have any visual impact as only repaint events will result in any changes on the screen.
		// From testing it seems the only events that can happen during OnDrawGizmos are the mouseUp and repaint events.
		if (Event.current.type != EventType.Repaint) return;

		colorSettings.PushToStatic(this);

		AstarProfiler.StartProfile("OnDrawGizmos");

		if (workItems.workItemsInProgress || isScanning) {
			// If updating graphs, graph info might not be valid right now
			// so just draw the same thing as last frame.
			// Also if the scene has multiple cameras (or in the editor if we have a scene view and a game view) we
			// just calculate the mesh once and then redraw the existing one for the other cameras.
			// This improves performance quite a bit.
			gizmos.DrawExisting();
		} else {
			if (showNavGraphs && !manualDebugFloorRoof) {
				RecalculateDebugLimits();
			}

			Profiler.BeginSample("Graph.OnDrawGizmos");
			// Loop through all graphs and draw their gizmos
			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] != null && graphs[i].drawGizmos)
					graphs[i].OnDrawGizmos(gizmos, showNavGraphs);
			}
			Profiler.EndSample();

			if (showNavGraphs) {
				euclideanEmbedding.OnDrawGizmos();
				if (debugMode == GraphDebugMode.HierarchicalNode) hierarchicalGraph.OnDrawGizmos(gizmos);
			}
		}

		gizmos.FinalizeDraw();

		AstarProfiler.EndProfile("OnDrawGizmos");
	}

#if !ASTAR_NO_GUI
	/// <summary>
	/// Draws the InGame debugging (if enabled), also shows the fps if 'L' is pressed down.
	/// See: <see cref="logPathResults"/> PathLog
	/// </summary>
	private void OnGUI () {
		if (logPathResults == PathLog.InGame && inGameDebugPath != "") {
			GUI.Label(new Rect(5, 5, 400, 600), inGameDebugPath);
		}
	}
#endif

	/// <summary>
	/// Prints path results to the log. What it prints can be controled using <see cref="logPathResults"/>.
	/// See: <see cref="logPathResults"/>
	/// See: PathLog
	/// See: Pathfinding.Path.DebugString
	/// </summary>
	private void LogPathResults (Path path) {
		if (logPathResults != PathLog.None && (path.error || logPathResults != PathLog.OnlyErrors)) {
			string debug = path.DebugString(logPathResults);

			if (logPathResults == PathLog.InGame) {
				inGameDebugPath = debug;
			} else if (path.error) {
				Debug.LogWarning(debug);
			} else {
				Debug.Log(debug);
			}
		}
	}

	/// <summary>
	/// Checks if any work items need to be executed
	/// then runs pathfinding for a while (if not using multithreading because
	/// then the calculation happens in other threads)
	/// and then returns any calculated paths to the
	/// scripts that requested them.
	///
	/// See: PerformBlockingActions
	/// See: PathProcessor.TickNonMultithreaded
	/// See: PathReturnQueue.ReturnPaths
	/// </summary>
	private void Update () {
		// This class uses the [ExecuteInEditMode] attribute
		// So Update is called even when not playing
		// Don't do anything when not in play mode
		if (!Application.isPlaying) return;

		navmeshUpdates.Update();

		// Execute blocking actions such as graph updates
		// when not scanning
		if (!isScanning) {
			PerformBlockingActions();
		}

		// Calculates paths when not using multithreading
		pathProcessor.TickNonMultithreaded();

		// Return calculated paths
		pathReturnQueue.ReturnPaths(true);
	}

	private void PerformBlockingActions (bool force = false) {
		if (workItemLock.Held && pathProcessor.queue.AllReceiversBlocked) {
			// Return all paths before starting blocking actions
			// since these might change the graph and make returned paths invalid (at least the nodes)
			pathReturnQueue.ReturnPaths(false);

			Profiler.BeginSample("Work Items");
			if (workItems.ProcessWorkItems(force)) {
				// At this stage there are no more work items, resume pathfinding threads
				workItemLock.Release();
			}
			Profiler.EndSample();
		}
	}

	/// <summary>
	/// Call during work items to queue a flood fill.
	/// Deprecated: This method has been moved. Use the method on the context object that can be sent with work item delegates instead
	/// <code>
	/// AstarPath.active.AddWorkItem(new AstarWorkItem(() => {
	///     // Safe to update graphs here
	///     var node = AstarPath.active.GetNearest(transform.position).node;
	///     node.Walkable = false;
	/// }));
	/// </code>
	///
	/// See: <see cref="Pathfinding.IWorkItemContext"/>
	/// </summary>
	[System.Obsolete("This method has been moved. Use the method on the context object that can be sent with work item delegates instead")]
	public void QueueWorkItemFloodFill () {
		throw new System.Exception("This method has been moved. Use the method on the context object that can be sent with work item delegates instead");
	}

	/// <summary>
	/// If a WorkItem needs to have a valid flood fill during execution, call this method to ensure there are no pending flood fills.
	/// Deprecated: This method has been moved. Use the method on the context object that can be sent with work item delegates instead
	/// <code>
	/// AstarPath.active.AddWorkItem(new AstarWorkItem(() => {
	///     // Safe to update graphs here
	///     var node = AstarPath.active.GetNearest(transform.position).node;
	///     node.Walkable = false;
	/// }));
	/// </code>
	///
	/// See: <see cref="Pathfinding.IWorkItemContext"/>
	/// </summary>
	[System.Obsolete("This method has been moved. Use the method on the context object that can be sent with work item delegates instead")]
	public void EnsureValidFloodFill () {
		throw new System.Exception("This method has been moved. Use the method on the context object that can be sent with work item delegates instead");
	}

	/// <summary>
	/// Add a work item to be processed when pathfinding is paused.
	/// Convenience method that is equivalent to
	/// <code>
	/// AddWorkItem(new AstarWorkItem(callback));
	/// </code>
	///
	/// See: <see cref="AddWorkItem(AstarWorkItem)"/>
	/// </summary>
	public void AddWorkItem (System.Action callback) {
		AddWorkItem(new AstarWorkItem(callback));
	}

	/// <summary>
	/// Add a work item to be processed when pathfinding is paused.
	/// Convenience method that is equivalent to
	/// <code>
	/// AddWorkItem(new AstarWorkItem(callback));
	/// </code>
	///
	/// See: <see cref="AddWorkItem(AstarWorkItem)"/>
	/// </summary>
	public void AddWorkItem (System.Action<IWorkItemContext> callback) {
		AddWorkItem(new AstarWorkItem(callback));
	}

	/// <summary>
	/// Add a work item to be processed when pathfinding is paused.
	///
	/// The work item will be executed when it is safe to update nodes. This is defined as between the path searches.
	/// When using more threads than one, calling this often might decrease pathfinding performance due to a lot of idling in the threads.
	/// Not performance as in it will use much CPU power, but performance as in the number of paths per second will probably go down
	/// (though your framerate might actually increase a tiny bit).
	///
	/// You should only call this function from the main unity thread (i.e normal game code).
	///
	/// <code>
	/// AstarPath.active.AddWorkItem(new AstarWorkItem(() => {
	///     // Safe to update graphs here
	///     var node = AstarPath.active.GetNearest(transform.position).node;
	///     node.Walkable = false;
	/// }));
	/// </code>
	///
	/// <code>
	/// AstarPath.active.AddWorkItem(() => {
	///     // Safe to update graphs here
	///     var node = AstarPath.active.GetNearest(transform.position).node;
	///     node.position = (Int3)transform.position;
	/// });
	/// </code>
	///
	/// See: <see cref="FlushWorkItems"/>
	/// </summary>
	public void AddWorkItem (AstarWorkItem item) {
		workItems.AddWorkItem(item);

		// Make sure pathfinding is stopped and work items are processed
		if (!workItemLock.Held) {
			workItemLock = PausePathfindingSoon();
		}

#if UNITY_EDITOR
		// If not playing, execute instantly
		if (!Application.isPlaying) {
			FlushWorkItems();
		}
#endif
	}

	#region GraphUpdateMethods

	/// <summary>
	/// Will apply queued graph updates as soon as possible, regardless of <see cref="batchGraphUpdates"/>.
	/// Calling this multiple times will not create multiple callbacks.
	/// This function is useful if you are limiting graph updates, but you want a specific graph update to be applied as soon as possible regardless of the time limit.
	/// Note that this does not block until the updates are done, it merely bypasses the <see cref="batchGraphUpdates"/> time limit.
	///
	/// See: <see cref="FlushGraphUpdates"/>
	/// </summary>
	public void QueueGraphUpdates () {
		if (!graphUpdatesWorkItemAdded) {
			graphUpdatesWorkItemAdded = true;
			var workItem = graphUpdates.GetWorkItem();

			// Add a new work item which first
			// sets the graphUpdatesWorkItemAdded flag to false
			// and then processes the graph updates
			AddWorkItem(new AstarWorkItem(() => {
				graphUpdatesWorkItemAdded = false;
				lastGraphUpdate = Time.realtimeSinceStartup;

				workItem.init();
			}, workItem.update));
		}
	}

	/// <summary>
	/// Waits a moment with updating graphs.
	/// If batchGraphUpdates is set, we want to keep some space between them to let pathfinding threads running and then calculate all queued calls at once
	/// </summary>
	IEnumerator DelayedGraphUpdate () {
		graphUpdateRoutineRunning = true;

		yield return new WaitForSeconds(graphUpdateBatchingInterval-(Time.realtimeSinceStartup-lastGraphUpdate));
		QueueGraphUpdates();
		graphUpdateRoutineRunning = false;
	}

	/// <summary>
	/// Update all graphs within bounds after delay seconds.
	/// The graphs will be updated as soon as possible.
	///
	/// See: FlushGraphUpdates
	/// See: batchGraphUpdates
	/// See: graph-updates (view in online documentation for working links)
	/// </summary>
	public void UpdateGraphs (Bounds bounds, float delay) {
		UpdateGraphs(new GraphUpdateObject(bounds), delay);
	}

	/// <summary>
	/// Update all graphs using the GraphUpdateObject after delay seconds.
	/// This can be used to, e.g make all nodes in a region unwalkable, or set them to a higher penalty.
	///
	/// See: FlushGraphUpdates
	/// See: batchGraphUpdates
	/// See: graph-updates (view in online documentation for working links)
	/// </summary>
	public void UpdateGraphs (GraphUpdateObject ob, float delay) {
		StartCoroutine(UpdateGraphsInternal(ob, delay));
	}

	/// <summary>Update all graphs using the GraphUpdateObject after delay seconds</summary>
	IEnumerator UpdateGraphsInternal (GraphUpdateObject ob, float delay) {
		yield return new WaitForSeconds(delay);
		UpdateGraphs(ob);
	}

	/// <summary>
	/// Update all graphs within bounds.
	/// The graphs will be updated as soon as possible.
	///
	/// This is equivalent to
	/// <code>
	/// UpdateGraphs(new GraphUpdateObject(bounds));
	/// </code>
	///
	/// See: FlushGraphUpdates
	/// See: batchGraphUpdates
	/// See: graph-updates (view in online documentation for working links)
	/// </summary>
	public void UpdateGraphs (Bounds bounds) {
		UpdateGraphs(new GraphUpdateObject(bounds));
	}

	/// <summary>
	/// Update all graphs using the GraphUpdateObject.
	/// This can be used to, e.g make all nodes in a region unwalkable, or set them to a higher penalty.
	/// The graphs will be updated as soon as possible (with respect to <see cref="batchGraphUpdates)"/>
	///
	/// See: FlushGraphUpdates
	/// See: batchGraphUpdates
	/// See: graph-updates (view in online documentation for working links)
	/// </summary>
	public void UpdateGraphs (GraphUpdateObject ob) {
		graphUpdates.AddToQueue(ob);

		// If we should limit graph updates, start a coroutine which waits until we should update graphs
		if (batchGraphUpdates && Time.realtimeSinceStartup-lastGraphUpdate < graphUpdateBatchingInterval) {
			if (!graphUpdateRoutineRunning) {
				StartCoroutine(DelayedGraphUpdate());
			}
		} else {
			// Otherwise, graph updates should be carried out as soon as possible
			QueueGraphUpdates();
		}
	}

	/// <summary>
	/// Forces graph updates to complete in a single frame.
	/// This will force the pathfinding threads to finish calculating the path they are currently calculating (if any) and then pause.
	/// When all threads have paused, graph updates will be performed.
	/// Warning: Using this very often (many times per second) can reduce your fps due to a lot of threads waiting for one another.
	/// But you probably wont have to worry about that.
	///
	/// Note: This is almost identical to <see cref="FlushWorkItems"/>, but added for more descriptive name.
	/// This function will also override any time limit delays for graph updates.
	/// This is because graph updates are implemented using work items.
	/// So calling this function will also execute any other work items (if any are queued).
	///
	/// Will not do anything if there are no graph updates queued (not even execute other work items).
	/// </summary>
	public void FlushGraphUpdates () {
		if (IsAnyGraphUpdateQueued) {
			QueueGraphUpdates();
			FlushWorkItems();
		}
	}

	#endregion

	/// <summary>
	/// Forces work items to complete in a single frame.
	/// This will force all work items to run immidiately.
	/// This will force the pathfinding threads to finish calculating the path they are currently calculating (if any) and then pause.
	/// When all threads have paused, work items will be executed (which can be e.g graph updates).
	///
	/// Warning: Using this very often (many times per second) can reduce your fps due to a lot of threads waiting for one another.
	/// But you probably wont have to worry about that
	///
	/// Note: This is almost (note almost) identical to <see cref="FlushGraphUpdates"/>, but added for more descriptive name.
	///
	/// Will not do anything if there are no queued work items waiting to run.
	/// </summary>
	public void FlushWorkItems () {
		if (workItems.anyQueued) {
			var graphLock = PausePathfinding();
			PerformBlockingActions(true);
			graphLock.Release();
		}
	}

	/// <summary>
	/// Make sure work items are executed.
	///
	/// See: AddWorkItem
	///
	/// Deprecated: Use <see cref="FlushWorkItems()"/> instead.
	/// </summary>
	/// <param name="unblockOnComplete">If true, pathfinding will be allowed to start running immediately after completing all work items.</param>
	/// <param name="block">If true, work items that usually take more than one frame to complete will be forced to complete during this call.
	///              If false, then after this call there might still be work left to do.</param>
	[System.Obsolete("Use FlushWorkItems() instead")]
	public void FlushWorkItems (bool unblockOnComplete, bool block) {
		var graphLock = PausePathfinding();

		// Run tasks
		PerformBlockingActions(block);
		graphLock.Release();
	}

	/// <summary>
	/// Forces thread safe callbacks to run.
	/// Deprecated: Use <see cref="FlushWorkItems"/> instead
	/// </summary>
	[System.Obsolete("Use FlushWorkItems instead")]
	public void FlushThreadSafeCallbacks () {
		FlushWorkItems();
	}

	/// <summary>
	/// Calculates number of threads to use.
	/// If count is not Automatic, simply returns count casted to an int.
	/// Returns: An int specifying how many threads to use, 0 means a coroutine should be used for pathfinding instead of a separate thread.
	///
	/// If count is set to Automatic it will return a value based on the number of processors and memory for the current system.
	/// If memory is <= 512MB or logical cores are <= 1, it will return 0. If memory is <= 1024 it will clamp threads to max 2.
	/// Otherwise it will return the number of logical cores clamped to 6.
	///
	/// When running on WebGL this method always returns 0
	/// </summary>
	public static int CalculateThreadCount (ThreadCount count) {
#if UNITY_WEBGL
		return 0;
#else
		if (count == ThreadCount.AutomaticLowLoad || count == ThreadCount.AutomaticHighLoad) {
			int logicalCores = Mathf.Max(1, SystemInfo.processorCount);
			int memory = SystemInfo.systemMemorySize;

			if (memory <= 0) {
				Debug.LogError("Machine reporting that is has <= 0 bytes of RAM. This is definitely not true, assuming 1 GiB");
				memory = 1024;
			}

			if (logicalCores <= 1) return 0;
			if (memory <= 512) return 0;

			return 1;
		} else {
			return (int)count > 0 ? 1 : 0;
		}
#endif
	}

	/// <summary>
	/// Sets up all needed variables and scans the graphs.
	/// Calls Initialize, starts the ReturnPaths coroutine and scans all graphs.
	/// Also starts threads if using multithreading
	/// See: <see cref="OnAwakeSettings"/>
	/// </summary>
	protected override void Awake () {
		base.Awake();
		// Very important to set this. Ensures the singleton pattern holds
		active = this;

		if (FindObjectsOfType(typeof(AstarPath)).Length > 1) {
			Debug.LogError("You should NOT have more than one AstarPath component in the scene at any time.\n" +
				"This can cause serious errors since the AstarPath component builds around a singleton pattern.");
		}

		// Disable GUILayout to gain some performance, it is not used in the OnGUI call
		useGUILayout = false;

		// This class uses the [ExecuteInEditMode] attribute
		// So Awake is called even when not playing
		// Don't do anything when not in play mode
		if (!Application.isPlaying) return;

		if (OnAwakeSettings != null) {
			OnAwakeSettings();
		}

		// To make sure all graph modifiers have been enabled before scan (to avoid script execution order issues)
		GraphModifier.FindAllModifiers();
		RelevantGraphSurface.FindAllGraphSurfaces();

		InitializePathProcessor();
		InitializeProfiler();
		ConfigureReferencesInternal();
		InitializeAstarData();

		// Flush work items, possibly added in InitializeAstarData to load graph data
		FlushWorkItems();

		euclideanEmbedding.dirty = true;

		navmeshUpdates.OnEnable();

		if (scanOnStartup && (!data.cacheStartup || data.file_cachedStartup == null)) {
			Scan();
		}
	}

	/// <summary>Initializes the <see cref="pathProcessor"/> field</summary>
	void InitializePathProcessor () {
		int numThreads = CalculateThreadCount(threadCount);

		// Outside of play mode everything is synchronous, so no threads are used.
		if (!Application.isPlaying) numThreads = 0;

		// Trying to prevent simple modding to add support for more than one thread
		if (numThreads > 1) {
			threadCount = ThreadCount.One;
			numThreads = 1;
		}

		int numProcessors = Mathf.Max(numThreads, 1);
		bool multithreaded = numThreads > 0;
		pathProcessor = new PathProcessor(this, pathReturnQueue, numProcessors, multithreaded);

		pathProcessor.OnPathPreSearch += path => {
			var tmp = OnPathPreSearch;
			if (tmp != null) tmp(path);
		};

		pathProcessor.OnPathPostSearch += path => {
			LogPathResults(path);
			var tmp = OnPathPostSearch;
			if (tmp != null) tmp(path);
		};

		// Sent every time the path queue is unblocked
		pathProcessor.OnQueueUnblocked += () => {
			if (euclideanEmbedding.dirty) {
				euclideanEmbedding.RecalculateCosts();
			}
		};

		if (multithreaded) {
			graphUpdates.EnableMultithreading();
		}
	}

	/// <summary>Does simple error checking</summary>
	internal void VerifyIntegrity () {
		if (active != this) {
			throw new System.Exception("Singleton pattern broken. Make sure you only have one AstarPath object in the scene");
		}

		if (data == null) {
			throw new System.NullReferenceException("data is null... A* not set up correctly?");
		}

		if (data.graphs == null) {
			data.graphs = new NavGraph[0];
			data.UpdateShortcuts();
		}
	}

	/// <summary>\cond internal</summary>
	/// <summary>
	/// Internal method to make sure <see cref="active"/> is set to this object and that <see cref="data"/> is not null.
	/// Also calls OnEnable for the <see cref="colorSettings"/> and initializes data.userConnections if it wasn't initialized before
	///
	/// Warning: This is mostly for use internally by the system.
	/// </summary>
	public void ConfigureReferencesInternal () {
		active = this;
		data = data ?? new AstarData();
		colorSettings = colorSettings ?? new AstarColor();
		colorSettings.PushToStatic(this);
	}
	/// <summary>\endcond</summary>

	/// <summary>Calls AstarProfiler.InitializeFastProfile</summary>
	void InitializeProfiler () {
		AstarProfiler.InitializeFastProfile(new string[14] {
			"Prepare",          //0
			"Initialize",       //1
			"CalculateStep",    //2
			"Trace",            //3
			"Open",             //4
			"UpdateAllG",       //5
			"Add",              //6
			"Remove",           //7
			"PreProcessing",    //8
			"Callback",         //9
			"Overhead",         //10
			"Log",              //11
			"ReturnPaths",      //12
			"PostPathCallback"  //13
		});
	}

	/// <summary>
	/// Initializes the AstarData class.
	/// Searches for graph types, calls Awake on <see cref="data"/> and on all graphs
	///
	/// See: AstarData.FindGraphTypes
	/// </summary>
	void InitializeAstarData () {
		data.FindGraphTypes();
		data.Awake();
		data.UpdateShortcuts();
	}

	/// <summary>Cleans up meshes to avoid memory leaks</summary>
	void OnDisable () {
		gizmos.ClearCache();
	}

	/// <summary>
	/// Clears up variables and other stuff, destroys graphs.
	/// Note that when destroying an AstarPath object, all static variables such as callbacks will be cleared.
	/// </summary>
	void OnDestroy () {
		// This class uses the [ExecuteInEditMode] attribute
		// So OnDestroy is called even when not playing
		// Don't do anything when not in play mode
		if (!Application.isPlaying) return;

		if (logPathResults == PathLog.Heavy)
			Debug.Log("+++ AstarPath Component Destroyed - Cleaning Up Pathfinding Data +++");

		if (active != this) return;

		// Block until the pathfinding threads have
		// completed their current path calculation
		PausePathfinding();

		navmeshUpdates.OnDisable();

		euclideanEmbedding.dirty = false;
		FlushWorkItems();

		// Don't accept any more path calls to this AstarPath instance.
		// This will cause all pathfinding threads to exit (if any exist)
		pathProcessor.queue.TerminateReceivers();

		if (logPathResults == PathLog.Heavy)
			Debug.Log("Processing Possible Work Items");

		// Stop the graph update thread (if it is running)
		graphUpdates.DisableMultithreading();

		// Try to join pathfinding threads
		pathProcessor.JoinThreads();

		if (logPathResults == PathLog.Heavy)
			Debug.Log("Returning Paths");


		// Return all paths
		pathReturnQueue.ReturnPaths(false);

		if (logPathResults == PathLog.Heavy)
			Debug.Log("Destroying Graphs");


		// Clean up graph data
		data.OnDestroy();

		if (logPathResults == PathLog.Heavy)
			Debug.Log("Cleaning up variables");

		// Clear variables up, static variables are good to clean up, otherwise the next scene might get weird data

		// Clear all callbacks
		OnAwakeSettings         = null;
		OnGraphPreScan          = null;
		OnGraphPostScan         = null;
		OnPathPreSearch         = null;
		OnPathPostSearch        = null;
		OnPreScan               = null;
		OnPostScan              = null;
		OnLatePostScan          = null;
		On65KOverflow           = null;
		OnGraphsUpdated         = null;

		active = null;
	}

	#region ScanMethods

	/// <summary>
	/// Floodfills starting from the specified node.
	///
	/// Deprecated: Deprecated: Not meaningful anymore. The HierarchicalGraph takes care of things automatically behind the scenes
	/// </summary>
	[System.Obsolete("Not meaningful anymore. The HierarchicalGraph takes care of things automatically behind the scenes")]
	public void FloodFill (GraphNode seed) {
	}

	/// <summary>
	/// Floodfills starting from 'seed' using the specified area.
	///
	/// Deprecated: Not meaningful anymore. The HierarchicalGraph takes care of things automatically behind the scenes
	/// </summary>
	[System.Obsolete("Not meaningful anymore. The HierarchicalGraph takes care of things automatically behind the scenes")]
	public void FloodFill (GraphNode seed, uint area) {
	}

	/// <summary>
	/// Floodfills all graphs and updates areas for every node.
	/// The different colored areas that you see in the scene view when looking at graphs
	/// are called just 'areas', this method calculates which nodes are in what areas.
	/// See: Pathfinding.Node.area
	///
	/// Deprecated: Avoid using. This will force a full recalculation of the connected components. In most cases the HierarchicalGraph class takes care of things automatically behind the scenes now.
	/// </summary>
	[ContextMenu("Flood Fill Graphs")]
	[System.Obsolete("Avoid using. This will force a full recalculation of the connected components. In most cases the HierarchicalGraph class takes care of things automatically behind the scenes now.")]
	public void FloodFill () {
		hierarchicalGraph.RecalculateAll();
		workItems.OnFloodFill();
	}

	/// <summary>
	/// Returns a new global node index.
	/// Warning: This method should not be called directly. It is used by the GraphNode constructor.
	/// </summary>
	internal int GetNewNodeIndex () {
		return pathProcessor.GetNewNodeIndex();
	}

	/// <summary>
	/// Initializes temporary path data for a node.
	/// Warning: This method should not be called directly. It is used by the GraphNode constructor.
	/// </summary>
	internal void InitializeNode (GraphNode node) {
		pathProcessor.InitializeNode(node);
	}

	/// <summary>
	/// Internal method to destroy a given node.
	/// This is to be called after the node has been disconnected from the graph so that it cannot be reached from any other nodes.
	/// It should only be called during graph updates, that is when the pathfinding threads are either not running or paused.
	///
	/// Warning: This method should not be called by user code. It is used internally by the system.
	/// </summary>
	internal void DestroyNode (GraphNode node) {
		pathProcessor.DestroyNode(node);
	}

	/// <summary>
	/// Blocks until all pathfinding threads are paused and blocked.
	/// Deprecated: Use <see cref="PausePathfinding"/> instead. Make sure to call Release on the returned lock.
	/// </summary>
	[System.Obsolete("Use PausePathfinding instead. Make sure to call Release on the returned lock.", true)]
	public void BlockUntilPathQueueBlocked () {
	}

	/// <summary>
	/// Blocks until all pathfinding threads are paused and blocked.
	///
	/// <code>
	/// var graphLock = AstarPath.active.PausePathfinding();
	/// // Here we can modify the graphs safely. For example by adding a new node to a point graph
	/// var node = AstarPath.active.data.pointGraph.AddNode((Int3) new Vector3(3, 1, 4));
	///
	/// // Allow pathfinding to resume
	/// graphLock.Release();
	/// </code>
	///
	/// Returns: A lock object. You need to call <see cref="Pathfinding.PathProcessor.GraphUpdateLock.Release"/> on that object to allow pathfinding to resume.
	/// Note: In most cases this should not be called from user code. Use the <see cref="AddWorkItem"/> method instead.
	///
	/// See: <see cref="AddWorkItem"/>
	/// </summary>
	public PathProcessor.GraphUpdateLock PausePathfinding () {
		return pathProcessor.PausePathfinding(true);
	}

	/// <summary>Blocks the path queue so that e.g work items can be performed</summary>
	PathProcessor.GraphUpdateLock PausePathfindingSoon () {
		return pathProcessor.PausePathfinding(false);
	}

	/// <summary>
	/// Scans a particular graph.
	/// Calling this method will recalculate the specified graph.
	/// This method is pretty slow (depending on graph type and graph complexity of course), so it is advisable to use
	/// smaller graph updates whenever possible.
	///
	/// <code>
	/// // Recalculate all graphs
	/// AstarPath.active.Scan();
	///
	/// // Recalculate only the first grid graph
	/// var graphToScan = AstarPath.active.data.gridGraph;
	/// AstarPath.active.Scan(graphToScan);
	///
	/// // Recalculate only the first and third graphs
	/// var graphsToScan = new [] { AstarPath.active.data.graphs[0], AstarPath.active.data.graphs[2] };
	/// AstarPath.active.Scan(graphsToScan);
	/// </code>
	///
	/// See: graph-updates (view in online documentation for working links)
	/// See: ScanAsync
	/// </summary>
	public void Scan (NavGraph graphToScan) {
		if (graphToScan == null) throw new System.ArgumentNullException();
		Scan(new NavGraph[] { graphToScan });
	}

	/// <summary>
	/// Scans all specified graphs.
	///
	/// Calling this method will recalculate all specified graphs or all graphs if the graphsToScan parameter is null.
	/// This method is pretty slow (depending on graph type and graph complexity of course), so it is advisable to use
	/// smaller graph updates whenever possible.
	///
	/// <code>
	/// // Recalculate all graphs
	/// AstarPath.active.Scan();
	///
	/// // Recalculate only the first grid graph
	/// var graphToScan = AstarPath.active.data.gridGraph;
	/// AstarPath.active.Scan(graphToScan);
	///
	/// // Recalculate only the first and third graphs
	/// var graphsToScan = new [] { AstarPath.active.data.graphs[0], AstarPath.active.data.graphs[2] };
	/// AstarPath.active.Scan(graphsToScan);
	/// </code>
	///
	/// See: graph-updates (view in online documentation for working links)
	/// See: ScanAsync
	/// </summary>
	/// <param name="graphsToScan">The graphs to scan. If this parameter is null then all graphs will be scanned</param>
	public void Scan (NavGraph[] graphsToScan = null) {
		var prevProgress = new Progress();

		Profiler.BeginSample("Scan");
		Profiler.BeginSample("Init");
		foreach (var p in ScanAsync(graphsToScan)) {
			if (prevProgress.description != p.description) {
#if !NETFX_CORE && UNITY_EDITOR
				Profiler.EndSample();
				Profiler.BeginSample(p.description);
				// Log progress to the console
				System.Console.WriteLine(p.description);
				prevProgress = p;
#endif
			}
		}
		Profiler.EndSample();
		Profiler.EndSample();
	}

	/// <summary>
	/// Scans a particular graph asynchronously. This is a IEnumerable, you can loop through it to get the progress
	/// <code>
	/// foreach (Progress progress in AstarPath.active.ScanAsync()) {
	///     Debug.Log("Scanning... " + progress.description + " - " + (progress.progress*100).ToString("0") + "%");
	/// }
	/// </code>
	/// You can scan graphs asyncronously by yielding when you loop through the progress.
	/// Note that this does not guarantee a good framerate, but it will allow you
	/// to at least show a progress bar during scanning.
	/// <code>
	/// IEnumerator Start () {
	///     foreach (Progress progress in AstarPath.active.ScanAsync()) {
	///         Debug.Log("Scanning... " + progress.description + " - " + (progress.progress*100).ToString("0") + "%");
	///         yield return null;
	///     }
	/// }
	/// </code>
	///
	/// See: Scan
	/// </summary>
	public IEnumerable<Progress> ScanAsync (NavGraph graphToScan) {
		if (graphToScan == null) throw new System.ArgumentNullException();
		return ScanAsync(new NavGraph[] { graphToScan });
	}

	/// <summary>
	/// Scans all specified graphs asynchronously. This is a IEnumerable, you can loop through it to get the progress
	///
	/// <code>
	/// foreach (Progress progress in AstarPath.active.ScanAsync()) {
	///     Debug.Log("Scanning... " + progress.description + " - " + (progress.progress*100).ToString("0") + "%");
	/// }
	/// </code>
	/// You can scan graphs asyncronously by yielding when you loop through the progress.
	/// Note that this does not guarantee a good framerate, but it will allow you
	/// to at least show a progress bar during scanning.
	/// <code>
	/// IEnumerator Start () {
	///     foreach (Progress progress in AstarPath.active.ScanAsync()) {
	///         Debug.Log("Scanning... " + progress.description + " - " + (progress.progress*100).ToString("0") + "%");
	///         yield return null;
	///     }
	/// }
	/// </code>
	///
	/// See: Scan
	/// </summary>
	/// <param name="graphsToScan">The graphs to scan. If this parameter is null then all graphs will be scanned</param>
	public IEnumerable<Progress> ScanAsync (NavGraph[] graphsToScan = null) {
		if (graphsToScan == null) graphsToScan = graphs;

		if (graphsToScan == null) {
			yield break;
		}

		if (isScanning) throw new System.InvalidOperationException("Another async scan is already running");

		isScanning = true;

		VerifyIntegrity();

		var graphUpdateLock = PausePathfinding();

		// Make sure all paths that are in the queue to be returned
		// are returned immediately
		// Some modifiers (e.g the funnel modifier) rely on
		// the nodes being valid when the path is returned
		pathReturnQueue.ReturnPaths(false);

		if (!Application.isPlaying) {
			data.FindGraphTypes();
			GraphModifier.FindAllModifiers();
		}

		int startFrame = Time.frameCount;

		yield return new Progress(0.05F, "Pre processing graphs");

		// Yes, this constraint is trivial to circumvent
		// the code is the same because it is annoying
		// to have to have separate code for the free
		// and the pro version that does essentially the same thing.
		// I would appreciate if you purchased the pro version of the A* Pathfinding Project
		// if you need async scanning.
		if (Time.frameCount != startFrame) {
			throw new System.Exception("Async scanning can only be done in the pro version of the A* Pathfinding Project");
		}

		if (OnPreScan != null) {
			OnPreScan(this);
		}

		GraphModifier.TriggerEvent(GraphModifier.EventType.PreScan);

		data.LockGraphStructure();

		var watch = System.Diagnostics.Stopwatch.StartNew();

		// Destroy previous nodes
		for (int i = 0; i < graphsToScan.Length; i++) {
			if (graphsToScan[i] != null) {
				((IGraphInternals)graphsToScan[i]).DestroyAllNodes();
			}
		}

		// Loop through all graphs and scan them one by one
		for (int i = 0; i < graphsToScan.Length; i++) {
			// Skip null graphs
			if (graphsToScan[i] == null) continue;

			// Just used for progress information
			// This graph will advance the progress bar from minp to maxp
			float minp = Mathf.Lerp(0.1F, 0.8F, (float)(i)/(graphsToScan.Length));
			float maxp = Mathf.Lerp(0.1F, 0.8F, (float)(i+0.95F)/(graphsToScan.Length));

			var progressDescriptionPrefix = "Scanning graph " + (i+1) + " of " + graphsToScan.Length + " - ";

			// Like a foreach loop but it gets a little complicated because of the exception
			// handling (it is not possible to yield inside try-except clause).
			var coroutine = ScanGraph(graphsToScan[i]).GetEnumerator();
			while (true) {
				try {
					if (!coroutine.MoveNext()) break;
				} catch {
					isScanning = false;
					data.UnlockGraphStructure();
					graphUpdateLock.Release();
					throw;
				}
				yield return coroutine.Current.MapTo(minp, maxp, progressDescriptionPrefix);
			}
		}

		data.UnlockGraphStructure();
		yield return new Progress(0.8F, "Post processing graphs");

		if (OnPostScan != null) {
			OnPostScan(this);
		}
		GraphModifier.TriggerEvent(GraphModifier.EventType.PostScan);

		FlushWorkItems();

		yield return new Progress(0.9F, "Computing areas");

		hierarchicalGraph.RecalculateIfNecessary();

		yield return new Progress(0.95F, "Late post processing");

		// Signal that we have stopped scanning here
		// Note that no yields can happen after this point
		// since then other parts of the system can start to interfere
		isScanning = false;

		if (OnLatePostScan != null) {
			OnLatePostScan(this);
		}
		GraphModifier.TriggerEvent(GraphModifier.EventType.LatePostScan);

		euclideanEmbedding.dirty = true;
		euclideanEmbedding.RecalculatePivots();

		// Perform any blocking actions
		FlushWorkItems();
		// Resume pathfinding threads
		graphUpdateLock.Release();

		watch.Stop();
		lastScanTime = (float)watch.Elapsed.TotalSeconds;

		System.GC.Collect();

		if (logPathResults != PathLog.None && logPathResults != PathLog.OnlyErrors) {
			Debug.Log("Scanning - Process took "+(lastScanTime*1000).ToString("0")+" ms to complete");
		}
	}

	IEnumerable<Progress> ScanGraph (NavGraph graph) {
		if (OnGraphPreScan != null) {
			yield return new Progress(0, "Pre processing");
			OnGraphPreScan(graph);
		}

		yield return new Progress(0, "");

		foreach (var p in ((IGraphInternals)graph).ScanInternal()) {
			yield return p.MapTo(0, 0.95f);
		}

		yield return new Progress(0.95f, "Assigning graph indices");

		// Assign the graph index to every node in the graph
		graph.GetNodes(node => node.GraphIndex = (uint)graph.graphIndex);

		if (OnGraphPostScan != null) {
			yield return new Progress(0.99f, "Post processing");
			OnGraphPostScan(graph);
		}
	}

	#endregion

	private static int waitForPathDepth = 0;

	/// <summary>
	/// Wait for the specified path to be calculated.
	/// Normally it takes a few frames for a path to get calculated and returned.
	///
	/// Deprecated: This method has been renamed to <see cref="BlockUntilCalculated"/>.
	/// </summary>
	[System.Obsolete("This method has been renamed to BlockUntilCalculated")]
	public static void WaitForPath (Path path) {
		BlockUntilCalculated(path);
	}

	/// <summary>
	/// Blocks until the path has been calculated.
	///
	/// Normally it takes a few frames for a path to be calculated and returned.
	/// This function will ensure that the path will be calculated when this function returns
	/// and that the callback for that path has been called.
	///
	/// If requesting a lot of paths in one go and waiting for the last one to complete,
	/// it will calculate most of the paths in the queue (only most if using multithreading, all if not using multithreading).
	///
	/// Use this function only if you really need to.
	/// There is a point to spreading path calculations out over several frames.
	/// It smoothes out the framerate and makes sure requesting a large
	/// number of paths at the same time does not cause lag.
	///
	/// Note: Graph updates and other callbacks might get called during the execution of this function.
	///
	/// When the pathfinder is shutting down. I.e in OnDestroy, this function will not do anything.
	///
	/// \throws Exception if pathfinding is not initialized properly for this scene (most likely no AstarPath object exists)
	/// or if the path has not been started yet.
	/// Also throws an exception if critical errors occur such as when the pathfinding threads have crashed (which should not happen in normal cases).
	/// This prevents an infinite loop while waiting for the path.
	///
	/// See: Pathfinding.Path.WaitForPath
	/// See: Pathfinding.Path.BlockUntilCalculated
	/// </summary>
	/// <param name="path">The path to wait for. The path must be started, otherwise an exception will be thrown.</param>
	public static void BlockUntilCalculated (Path path) {
		if (active == null)
			throw new System.Exception("Pathfinding is not correctly initialized in this scene (yet?). " +
				"AstarPath.active is null.\nDo not call this function in Awake");

		if (path == null) throw new System.ArgumentNullException("Path must not be null");

		if (active.pathProcessor.queue.IsTerminating) return;

		if (path.PipelineState == PathState.Created) {
			throw new System.Exception("The specified path has not been started yet.");
		}

		waitForPathDepth++;

		if (waitForPathDepth == 5) {
			Debug.LogError("You are calling the BlockUntilCalculated function recursively (maybe from a path callback). Please don't do this.");
		}

		if (path.PipelineState < PathState.ReturnQueue) {
			if (active.IsUsingMultithreading) {
				while (path.PipelineState < PathState.ReturnQueue) {
					if (active.pathProcessor.queue.IsTerminating) {
						waitForPathDepth--;
						throw new System.Exception("Pathfinding Threads seem to have crashed.");
					}

					// Wait for threads to calculate paths
					Thread.Sleep(1);
					active.PerformBlockingActions(true);
				}
			} else {
				while (path.PipelineState < PathState.ReturnQueue) {
					if (active.pathProcessor.queue.IsEmpty && path.PipelineState != PathState.Processing) {
						waitForPathDepth--;
						throw new System.Exception("Critical error. Path Queue is empty but the path state is '" + path.PipelineState + "'");
					}

					// Calculate some paths
					active.pathProcessor.TickNonMultithreaded();
					active.PerformBlockingActions(true);
				}
			}
		}

		active.pathReturnQueue.ReturnPaths(false);
		waitForPathDepth--;
	}

	/// <summary>
	/// Will send a callback when it is safe to update nodes. This is defined as between the path searches.
	/// This callback will only be sent once and is nulled directly after the callback has been sent.
	/// When using more threads than one, calling this often might decrease pathfinding performance due to a lot of idling in the threads.
	/// Not performance as in it will use much CPU power,
	/// but performance as in the number of paths per second will probably go down (though your framerate might actually increase a tiny bit)
	///
	/// You should only call this function from the main unity thread (i.e normal game code).
	///
	/// Version: Since version 4.0 this is equivalent to AddWorkItem(new AstarWorkItem(callback)). Previously the
	/// callbacks added using this method would not be ordered with respect to other work items, so they could be
	/// executed before other work items or after them.
	///
	/// Deprecated: Use <see cref="AddWorkItem(System.Action)"/> instead. Note the slight change in behavior (mentioned above).
	/// </summary>
	[System.Obsolete("Use AddWorkItem(System.Action) instead. Note the slight change in behavior (mentioned in the documentation).")]
	public static void RegisterSafeUpdate (System.Action callback) {
		active.AddWorkItem(new AstarWorkItem(callback));
	}

	/// <summary>
	/// Adds the path to a queue so that it will be calculated as soon as possible.
	/// The callback specified when constructing the path will be called when the path has been calculated.
	/// Usually you should use the Seeker component instead of calling this function directly.
	/// </summary>
	/// <param name="path">The path that should be enqueued.</param>
	/// <param name="pushToFront">If true, the path will be pushed to the front of the queue, bypassing all waiting paths and making it the next path to be calculated.
	/// This can be useful if you have a path which you want to prioritize over all others. Be careful to not overuse it though.
	/// If too many paths are put in the front of the queue often, this can lead to normal paths having to wait a very long time before being calculated.</param>
	public static void StartPath (Path path, bool pushToFront = false) {
		// Copy to local variable to avoid multithreading issues
		var astar = active;

		if (System.Object.ReferenceEquals(astar, null)) {
			Debug.LogError("There is no AstarPath object in the scene or it has not been initialized yet");
			return;
		}

		if (path.PipelineState != PathState.Created) {
			throw new System.Exception("The path has an invalid state. Expected " + PathState.Created + " found " + path.PipelineState + "\n" +
				"Make sure you are not requesting the same path twice");
		}

		if (astar.pathProcessor.queue.IsTerminating) {
			path.FailWithError("No new paths are accepted");
			return;
		}

		if (astar.graphs == null || astar.graphs.Length == 0) {
			Debug.LogError("There are no graphs in the scene");
			path.FailWithError("There are no graphs in the scene");
			Debug.LogError(path.errorLog);
			return;
		}

		path.Claim(astar);

		// Will increment p.state to PathState.PathQueue
		((IPathInternals)path).AdvanceState(PathState.PathQueue);
		if (pushToFront) {
			astar.pathProcessor.queue.PushFront(path);
		} else {
			astar.pathProcessor.queue.Push(path);
		}

		// Outside of play mode, all path requests are synchronous
		if (!Application.isPlaying) {
			BlockUntilCalculated(path);
		}
	}

	/// <summary>
	/// Cached NNConstraint.None to avoid unnecessary allocations.
	/// This should ideally be fixed by making NNConstraint an immutable class/struct.
	/// </summary>
	static readonly NNConstraint NNConstraintNone = NNConstraint.None;

	/// <summary>
	/// Returns the nearest node to a position using the specified NNConstraint.
	/// Searches through all graphs for their nearest nodes to the specified position and picks the closest one.\n
	/// Using the NNConstraint.None constraint.
	///
	/// <code>
	/// // Find the closest node to this GameObject's position
	/// GraphNode node = AstarPath.active.GetNearest(transform.position).node;
	///
	/// if (node.Walkable) {
	///     // Yay, the node is walkable, we can place a tower here or something
	/// }
	/// </code>
	///
	/// See: Pathfinding.NNConstraint
	/// </summary>
	public NNInfo GetNearest (Vector3 position) {
		return GetNearest(position, NNConstraintNone);
	}

	/// <summary>
	/// Returns the nearest node to a position using the specified NNConstraint.
	/// Searches through all graphs for their nearest nodes to the specified position and picks the closest one.
	/// The NNConstraint can be used to specify constraints on which nodes can be chosen such as only picking walkable nodes.
	///
	/// <code>
	/// GraphNode node = AstarPath.active.GetNearest(transform.position, NNConstraint.Default).node;
	/// </code>
	///
	/// <code>
	/// var constraint = NNConstraint.None;
	///
	/// // Constrain the search to walkable nodes only
	/// constraint.constrainWalkability = true;
	/// constraint.walkable = true;
	///
	/// // Constrain the search to only nodes with tag 3 or tag 5
	/// // The 'tags' field is a bitmask
	/// constraint.constrainTags = true;
	/// constraint.tags = (1 << 3) | (1 << 5);
	///
	/// var info = AstarPath.active.GetNearest(transform.position, constraint);
	/// var node = info.node;
	/// var closestPoint = info.position;
	/// </code>
	///
	/// See: Pathfinding.NNConstraint
	/// </summary>
	public NNInfo GetNearest (Vector3 position, NNConstraint constraint) {
		return GetNearest(position, constraint, null);
	}

	/// <summary>
	/// Returns the nearest node to a position using the specified NNConstraint.
	/// Searches through all graphs for their nearest nodes to the specified position and picks the closest one.
	/// The NNConstraint can be used to specify constraints on which nodes can be chosen such as only picking walkable nodes.
	/// See: Pathfinding.NNConstraint
	/// </summary>
	public NNInfo GetNearest (Vector3 position, NNConstraint constraint, GraphNode hint) {
		// Cache property lookup
		var graphs = this.graphs;

		float minDist = float.PositiveInfinity;
		NNInfoInternal nearestNode = new NNInfoInternal();
		int nearestGraph = -1;

		if (graphs != null) {
			for (int i = 0; i < graphs.Length; i++) {
				NavGraph graph = graphs[i];

				// Check if this graph should be searched
				if (graph == null || !constraint.SuitableGraph(i, graph)) {
					continue;
				}

				NNInfoInternal nnInfo;
				if (fullGetNearestSearch) {
					// Slower nearest node search
					// this will try to find a node which is suitable according to the constraint
					nnInfo = graph.GetNearestForce(position, constraint);
				} else {
					// Fast nearest node search
					// just find a node close to the position without using the constraint that much
					// (unless that comes essentially 'for free')
					nnInfo = graph.GetNearest(position, constraint);
				}

				GraphNode node = nnInfo.node;

				// No node found in this graph
				if (node == null) {
					continue;
				}

				// Distance to the closest point on the node from the requested position
				float dist = ((Vector3)nnInfo.clampedPosition-position).magnitude;

				if (prioritizeGraphs && dist < prioritizeGraphsLimit) {
					// The node is close enough, choose this graph and discard all others
					minDist = dist;
					nearestNode = nnInfo;
					nearestGraph = i;
					break;
				} else {
					// Choose the best node found so far
					if (dist < minDist) {
						minDist = dist;
						nearestNode = nnInfo;
						nearestGraph = i;
					}
				}
			}
		}

		// No matches found
		if (nearestGraph == -1) {
			return new NNInfo();
		}

		// Check if a constrained node has already been set
		if (nearestNode.constrainedNode != null) {
			nearestNode.node = nearestNode.constrainedNode;
			nearestNode.clampedPosition = nearestNode.constClampedPosition;
		}

		if (!fullGetNearestSearch && nearestNode.node != null && !constraint.Suitable(nearestNode.node)) {
			// Otherwise, perform a check to force the graphs to check for a suitable node
			NNInfoInternal nnInfo = graphs[nearestGraph].GetNearestForce(position, constraint);

			if (nnInfo.node != null) {
				nearestNode = nnInfo;
			}
		}

		if (!constraint.Suitable(nearestNode.node) || (constraint.constrainDistance && (nearestNode.clampedPosition - position).sqrMagnitude > maxNearestNodeDistanceSqr)) {
			return new NNInfo();
		}

		// Convert to NNInfo which doesn't have all the internal fields
		return new NNInfo(nearestNode);
	}

	/// <summary>
	/// Returns the node closest to the ray (slow).
	/// Warning: This function is brute-force and very slow, use with caution
	/// </summary>
	public GraphNode GetNearest (Ray ray) {
		if (graphs == null) return null;

		float minDist = Mathf.Infinity;
		GraphNode nearestNode = null;

		Vector3 lineDirection = ray.direction;
		Vector3 lineOrigin = ray.origin;

		for (int i = 0; i < graphs.Length; i++) {
			NavGraph graph = graphs[i];

			graph.GetNodes(node => {
				Vector3 pos = (Vector3)node.position;
				Vector3 p = lineOrigin+(Vector3.Dot(pos-lineOrigin, lineDirection)*lineDirection);

				float tmp = Mathf.Abs(p.x-pos.x);
				tmp *= tmp;
				if (tmp > minDist) return;

				tmp = Mathf.Abs(p.z-pos.z);
				tmp *= tmp;
				if (tmp > minDist) return;

				float dist = (p-pos).sqrMagnitude;

				if (dist < minDist) {
					minDist = dist;
					nearestNode = node;
				}
				return;
			});
		}

		return nearestNode;
	}
}
