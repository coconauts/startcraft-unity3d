using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding.WindowsStore;
#if UNITY_WINRT && !UNITY_EDITOR
//using MarkerMetro.Unity.WinLegacy.IO;
//using MarkerMetro.Unity.WinLegacy.Reflection;
#endif

namespace Pathfinding {
	[System.Serializable]
	/// <summary>
	/// Stores the navigation graphs for the A* Pathfinding System.
	/// \ingroup relevant
	///
	/// An instance of this class is assigned to AstarPath.data, from it you can access all graphs loaded through the <see cref="graphs"/> variable.\n
	/// This class also handles a lot of the high level serialization.
	/// </summary>
	public class AstarData {
		/// <summary>Shortcut to AstarPath.active</summary>
		public static AstarPath active {
			get {
				return AstarPath.active;
			}
		}

		#region Fields
		/// <summary>
		/// Shortcut to the first NavMeshGraph.
		/// Updated at scanning time
		/// </summary>
		public NavMeshGraph navmesh { get; private set; }

#if !ASTAR_NO_GRID_GRAPH
		/// <summary>
		/// Shortcut to the first GridGraph.
		/// Updated at scanning time
		/// </summary>
		public GridGraph gridGraph { get; private set; }
#endif

#if !ASTAR_NO_POINT_GRAPH
		/// <summary>
		/// Shortcut to the first PointGraph.
		/// Updated at scanning time
		/// </summary>
		public PointGraph pointGraph { get; private set; }
#endif


		/// <summary>
		/// All supported graph types.
		/// Populated through reflection search
		/// </summary>
		public System.Type[] graphTypes { get; private set; }

#if ASTAR_FAST_NO_EXCEPTIONS || UNITY_WINRT || UNITY_WEBGL
		/// <summary>
		/// Graph types to use when building with Fast But No Exceptions for iPhone.
		/// If you add any custom graph types, you need to add them to this hard-coded list.
		/// </summary>
		public static readonly System.Type[] DefaultGraphTypes = new System.Type[] {
#if !ASTAR_NO_GRID_GRAPH
			typeof(GridGraph),
#endif
#if !ASTAR_NO_POINT_GRAPH
			typeof(PointGraph),
#endif
			typeof(NavMeshGraph),
		};
#endif

		/// <summary>
		/// All graphs this instance holds.
		/// This will be filled only after deserialization has completed.
		/// May contain null entries if graph have been removed.
		/// </summary>
		[System.NonSerialized]
		public NavGraph[] graphs = new NavGraph[0];

		//Serialization Settings

		/// <summary>
		/// Serialized data for all graphs and settings.
		/// Stored as a base64 encoded string because otherwise Unity's Undo system would sometimes corrupt the byte data (because it only stores deltas).
		///
		/// This can be accessed as a byte array from the <see cref="data"/> property.
		///
		/// \since 3.6.1
		/// </summary>
		[SerializeField]
		string dataString;

		/// <summary>
		/// Data from versions from before 3.6.1.
		/// Used for handling upgrades
		/// \since 3.6.1
		/// </summary>
		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("data")]
		private byte[] upgradeData;

		/// <summary>Serialized data for all graphs and settings</summary>
		private byte[] data {
			get {
				// Handle upgrading from earlier versions than 3.6.1
				if (upgradeData != null && upgradeData.Length > 0) {
					data = upgradeData;
					upgradeData = null;
				}
				return dataString != null? System.Convert.FromBase64String (dataString) : null;
			}
			set {
				dataString = value != null? System.Convert.ToBase64String (value) : null;
			}
		}

		/// <summary>
		/// Serialized data for cached startup.
		/// If set, on start the graphs will be deserialized from this file.
		/// </summary>
		public TextAsset file_cachedStartup;

		/// <summary>
		/// Serialized data for cached startup.
		///
		/// Deprecated: Deprecated since 3.6, AstarData.file_cachedStartup is now used instead
		/// </summary>
		public byte[] data_cachedStartup;

		/// <summary>
		/// Should graph-data be cached.
		/// Caching the startup means saving the whole graphs - not only the settings - to a file (<see cref="file_cachedStartup)"/> which can
		/// be loaded when the game starts. This is usually much faster than scanning the graphs when the game starts. This is configured from the editor under the "Save & Load" tab.
		///
		/// See: save-load-graphs (view in online documentation for working links)
		/// </summary>
		[SerializeField]
		public bool cacheStartup;

		//End Serialization Settings

		List<bool> graphStructureLocked = new List<bool>();

		#endregion

		public byte[] GetData () {
			return data;
		}

		public void SetData (byte[] data) {
			this.data = data;
		}

		/// <summary>Loads the graphs from memory, will load cached graphs if any exists</summary>
		public void Awake () {
			graphs = new NavGraph[0];

			if (cacheStartup && file_cachedStartup != null) {
				LoadFromCache();
			} else {
				DeserializeGraphs();
			}
		}

		/// <summary>
		/// Prevent the graph structure from changing during the time this lock is held.
		/// This prevents graphs from being added or removed and also prevents graphs from being serialized or deserialized.
		/// This is used when e.g an async scan is happening to ensure that for example a graph that is being scanned is not destroyed.
		///
		/// Each call to this method *must* be paired with exactly one call to <see cref="UnlockGraphStructure"/>.
		/// The calls may be nested.
		/// </summary>
		internal void LockGraphStructure (bool allowAddingGraphs = false) {
			graphStructureLocked.Add(allowAddingGraphs);
		}

		/// <summary>
		/// Allows the graph structure to change again.
		/// See: <see cref="LockGraphStructure"/>
		/// </summary>
		internal void UnlockGraphStructure () {
			if (graphStructureLocked.Count == 0) throw new System.InvalidOperationException();
			graphStructureLocked.RemoveAt(graphStructureLocked.Count - 1);
		}

		PathProcessor.GraphUpdateLock AssertSafe (bool onlyAddingGraph = false) {
			if (graphStructureLocked.Count > 0) {
				bool allowAdding = true;
				for (int i = 0; i < graphStructureLocked.Count; i++) allowAdding &= graphStructureLocked[i];
				if (!(onlyAddingGraph && allowAdding)) throw new System.InvalidOperationException("Graphs cannot be added, removed or serialized while the graph structure is locked. This is the case when a graph is currently being scanned and when executing graph updates and work items.\nHowever as a special case, graphs can be added inside work items.");
			}

			// Pause the pathfinding threads
			var graphLock = active.PausePathfinding();
			if (!active.IsInsideWorkItem) {
				// Make sure all graph updates and other callbacks are done
				// Only do this if this code is not being called from a work item itself as that would cause a recursive wait that could never complete.
				// There are some valid cases when this can happen. For example it may be necessary to add a new graph inside a work item.
				active.FlushWorkItems();

				// Paths that are already calculated and waiting to be returned to the Seeker component need to be
				// processed immediately as their results usually depend on graphs that currently exist. If this was
				// not done then after destroying a graph one could get a path result with destroyed nodes in it.
				active.pathReturnQueue.ReturnPaths(false);
			}
			return graphLock;
		}

		/// <summary>
		/// Calls the callback with every node in all graphs.
		/// This is the easiest way to iterate through every existing node.
		///
		/// <code>
		/// AstarPath.active.data.GetNodes(node => {
		///     Debug.Log("I found a node at position " + (Vector3)node.position);
		/// });
		/// </code>
		///
		/// See: <see cref="Pathfinding.NavGraph.GetNodes"/> for getting the nodes of a single graph instead of all.
		/// See: graph-updates (view in online documentation for working links)
		/// </summary>
		public void GetNodes (System.Action<GraphNode> callback) {
			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] != null) graphs[i].GetNodes(callback);
			}
		}

		/// <summary>
		/// Updates shortcuts to the first graph of different types.
		/// Hard coding references to some graph types is not really a good thing imo. I want to keep it dynamic and flexible.
		/// But these references ease the use of the system, so I decided to keep them.
		/// </summary>
		public void UpdateShortcuts () {
			navmesh = (NavMeshGraph)FindGraphOfType(typeof(NavMeshGraph));

#if !ASTAR_NO_GRID_GRAPH
			gridGraph = (GridGraph)FindGraphOfType(typeof(GridGraph));
#endif

#if !ASTAR_NO_POINT_GRAPH
			pointGraph = (PointGraph)FindGraphOfType(typeof(PointGraph));
#endif
		}

		/// <summary>Load from data from <see cref="file_cachedStartup"/></summary>
		public void LoadFromCache () {
			var graphLock = AssertSafe();

			if (file_cachedStartup != null) {
				var bytes = file_cachedStartup.bytes;
				DeserializeGraphs(bytes);

				GraphModifier.TriggerEvent(GraphModifier.EventType.PostCacheLoad);
			} else {
				Debug.LogError("Can't load from cache since the cache is empty");
			}
			graphLock.Release();
		}

		#region Serialization

		/// <summary>
		/// Serializes all graphs settings to a byte array.
		/// See: DeserializeGraphs(byte[])
		/// </summary>
		public byte[] SerializeGraphs () {
			return SerializeGraphs(Pathfinding.Serialization.SerializeSettings.Settings);
		}

		/// <summary>
		/// Serializes all graphs settings and optionally node data to a byte array.
		/// See: DeserializeGraphs(byte[])
		/// See: Pathfinding.Serialization.SerializeSettings
		/// </summary>
		public byte[] SerializeGraphs (Pathfinding.Serialization.SerializeSettings settings) {
			uint checksum;

			return SerializeGraphs(settings, out checksum);
		}

		/// <summary>
		/// Main serializer function.
		/// Serializes all graphs to a byte array
		/// A similar function exists in the AstarPathEditor.cs script to save additional info
		/// </summary>
		public byte[] SerializeGraphs (Pathfinding.Serialization.SerializeSettings settings, out uint checksum) {
			var graphLock = AssertSafe();
			var sr = new Pathfinding.Serialization.AstarSerializer(this, settings);

			sr.OpenSerialize();
			sr.SerializeGraphs(graphs);
			sr.SerializeExtraInfo();
			byte[] bytes = sr.CloseSerialize();
			checksum = sr.GetChecksum();
#if ASTARDEBUG
			Debug.Log("Got a whole bunch of data, "+bytes.Length+" bytes");
#endif
			graphLock.Release();
			return bytes;
		}

		/// <summary>Deserializes graphs from <see cref="data"/></summary>
		public void DeserializeGraphs () {
			if (data != null) {
				DeserializeGraphs(data);
			}
		}

		/// <summary>Destroys all graphs and sets graphs to null</summary>
		void ClearGraphs () {
			if (graphs == null) return;
			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] != null) {
					((IGraphInternals)graphs[i]).OnDestroy();
					graphs[i].active = null;
				}
			}
			graphs = null;
			UpdateShortcuts();
		}

		public void OnDestroy () {
			ClearGraphs();
		}

		/// <summary>
		/// Deserializes graphs from the specified byte array.
		/// An error will be logged if deserialization fails.
		/// </summary>
		public void DeserializeGraphs (byte[] bytes) {
			var graphLock = AssertSafe();

			ClearGraphs();
			DeserializeGraphsAdditive(bytes);
			graphLock.Release();
		}

		/// <summary>
		/// Deserializes graphs from the specified byte array additively.
		/// An error will be logged if deserialization fails.
		/// This function will add loaded graphs to the current ones.
		/// </summary>
		public void DeserializeGraphsAdditive (byte[] bytes) {
			var graphLock = AssertSafe();

			try {
				if (bytes != null) {
					var sr = new Pathfinding.Serialization.AstarSerializer(this);

					if (sr.OpenDeserialize(bytes)) {
						DeserializeGraphsPartAdditive(sr);
						sr.CloseDeserialize();
					} else {
						Debug.Log("Invalid data file (cannot read zip).\nThe data is either corrupt or it was saved using a 3.0.x or earlier version of the system");
					}
				} else {
					throw new System.ArgumentNullException("bytes");
				}
				active.VerifyIntegrity();
			} catch (System.Exception e) {
				Debug.LogError("Caught exception while deserializing data.\n"+e);
				graphs = new NavGraph[0];
			}

			UpdateShortcuts();
			graphLock.Release();
		}

		/// <summary>Helper function for deserializing graphs</summary>
		void DeserializeGraphsPartAdditive (Pathfinding.Serialization.AstarSerializer sr) {
			if (graphs == null) graphs = new NavGraph[0];

			var gr = new List<NavGraph>(graphs);

			// Set an offset so that the deserializer will load
			// the graphs with the correct graph indexes
			sr.SetGraphIndexOffset(gr.Count);

			if (graphTypes == null) FindGraphTypes();
			gr.AddRange(sr.DeserializeGraphs(graphTypes));
			graphs = gr.ToArray();

			sr.DeserializeEditorSettingsCompatibility();
			sr.DeserializeExtraInfo();

			//Assign correct graph indices.
			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] == null) continue;
				graphs[i].GetNodes(node => node.GraphIndex = (uint)i);
			}

			for (int i = 0; i < graphs.Length; i++) {
				for (int j = i+1; j < graphs.Length; j++) {
					if (graphs[i] != null && graphs[j] != null && graphs[i].guid == graphs[j].guid) {
						Debug.LogWarning("Guid Conflict when importing graphs additively. Imported graph will get a new Guid.\nThis message is (relatively) harmless.");
						graphs[i].guid = Pathfinding.Util.Guid.NewGuid();
						break;
					}
				}
			}

			sr.PostDeserialization();
			active.hierarchicalGraph.RecalculateIfNecessary();
		}

		#endregion

		/// <summary>
		/// Find all graph types supported in this build.
		/// Using reflection, the assembly is searched for types which inherit from NavGraph.
		/// </summary>
		public void FindGraphTypes () {
#if !ASTAR_FAST_NO_EXCEPTIONS && !UNITY_WINRT && !UNITY_WEBGL
			var graphList = new List<System.Type>();
			foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) {
				System.Type[] types = null;
				try {
					types = assembly.GetTypes();
				} catch {
					// Ignore type load exceptions and things like that.
					// We might not be able to read all assemblies for some reason, but hopefully the relevant types exist in the assemblies that we can read
					continue;
				}

				foreach (var type in types) {
#if NETFX_CORE && !UNITY_EDITOR
					System.Type baseType = type.GetTypeInfo().BaseType;
#else
					var baseType = type.BaseType;
#endif
					while (baseType != null) {
						if (System.Type.Equals(baseType, typeof(NavGraph))) {
							graphList.Add(type);

							break;
						}

#if NETFX_CORE && !UNITY_EDITOR
						baseType = baseType.GetTypeInfo().BaseType;
#else
						baseType = baseType.BaseType;
#endif
					}
				}
			}

			graphTypes = graphList.ToArray();

#if ASTARDEBUG
			Debug.Log("Found "+graphTypes.Length+" graph types");
#endif
#else
			graphTypes = DefaultGraphTypes;
#endif
		}

		#region GraphCreation
		/// <summary>
		/// Returns: A System.Type which matches the specified type string. If no mathing graph type was found, null is returned
		///
		/// Deprecated:
		/// </summary>
		[System.Obsolete("If really necessary. Use System.Type.GetType instead.")]
		public System.Type GetGraphType (string type) {
			for (int i = 0; i < graphTypes.Length; i++) {
				if (graphTypes[i].Name == type) {
					return graphTypes[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Creates a new instance of a graph of type type. If no matching graph type was found, an error is logged and null is returned
		/// Returns: The created graph
		/// See: <see cref="CreateGraph(System.Type)"/>
		///
		/// Deprecated:
		/// </summary>
		[System.Obsolete("Use CreateGraph(System.Type) instead")]
		public NavGraph CreateGraph (string type) {
			Debug.Log("Creating Graph of type '"+type+"'");

			for (int i = 0; i < graphTypes.Length; i++) {
				if (graphTypes[i].Name == type) {
					return CreateGraph(graphTypes[i]);
				}
			}
			Debug.LogError("Graph type ("+type+") wasn't found");
			return null;
		}

		/// <summary>
		/// Creates a new graph instance of type type
		/// See: <see cref="CreateGraph(string)"/>
		/// </summary>
		internal NavGraph CreateGraph (System.Type type) {
			var graph = System.Activator.CreateInstance(type) as NavGraph;

			graph.active = active;
			return graph;
		}

		/// <summary>
		/// Adds a graph of type type to the <see cref="graphs"/> array
		///
		/// Deprecated:
		/// </summary>
		[System.Obsolete("Use AddGraph(System.Type) instead")]
		public NavGraph AddGraph (string type) {
			NavGraph graph = null;

			for (int i = 0; i < graphTypes.Length; i++) {
				if (graphTypes[i].Name == type) {
					graph = CreateGraph(graphTypes[i]);
				}
			}

			if (graph == null) {
				Debug.LogError("No NavGraph of type '"+type+"' could be found");
				return null;
			}

			AddGraph(graph);

			return graph;
		}

		/// <summary>
		/// Adds a graph of type type to the <see cref="graphs"/> array.
		/// See: runtime-graphs (view in online documentation for working links)
		/// </summary>
		public NavGraph AddGraph (System.Type type) {
			NavGraph graph = null;

			for (int i = 0; i < graphTypes.Length; i++) {
				if (System.Type.Equals(graphTypes[i], type)) {
					graph = CreateGraph(graphTypes[i]);
				}
			}

			if (graph == null) {
				Debug.LogError("No NavGraph of type '"+type+"' could be found, "+graphTypes.Length+" graph types are avaliable");
				return null;
			}

			AddGraph(graph);

			return graph;
		}

		/// <summary>Adds the specified graph to the <see cref="graphs"/> array</summary>
		void AddGraph (NavGraph graph) {
			// Make sure to not interfere with pathfinding
			var graphLock = AssertSafe(true);

			// Try to fill in an empty position
			bool foundEmpty = false;

			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] == null) {
					graphs[i] = graph;
					graph.graphIndex = (uint)i;
					foundEmpty = true;
					break;
				}
			}

			if (!foundEmpty) {
				if (graphs != null && graphs.Length >= GraphNode.MaxGraphIndex) {
					throw new System.Exception("Graph Count Limit Reached. You cannot have more than " + GraphNode.MaxGraphIndex + " graphs.");
				}

				// Add a new entry to the list
				var graphList = new List<NavGraph>(graphs ?? new NavGraph[0]);
				graphList.Add(graph);
				graphs = graphList.ToArray();
				graph.graphIndex = (uint)(graphs.Length-1);
			}

			UpdateShortcuts();
			graph.active = active;
			graphLock.Release();
		}

		/// <summary>
		/// Removes the specified graph from the <see cref="graphs"/> array and Destroys it in a safe manner.
		/// To avoid changing graph indices for the other graphs, the graph is simply nulled in the array instead
		/// of actually removing it from the array.
		/// The empty position will be reused if a new graph is added.
		///
		/// Returns: True if the graph was sucessfully removed (i.e it did exist in the <see cref="graphs"/> array). False otherwise.
		///
		/// Version: Changed in 3.2.5 to call SafeOnDestroy before removing
		/// and nulling it in the array instead of removing the element completely in the <see cref="graphs"/> array.
		/// </summary>
		public bool RemoveGraph (NavGraph graph) {
			// Make sure the pathfinding threads are stopped
			// If we don't wait until pathfinding that is potentially running on
			// this graph right now we could end up with NullReferenceExceptions
			var graphLock = AssertSafe();

			((IGraphInternals)graph).OnDestroy();
			graph.active = null;

			int i = System.Array.IndexOf(graphs, graph);
			if (i != -1) graphs[i] = null;

			UpdateShortcuts();
			graphLock.Release();
			return i != -1;
		}

		#endregion

		#region GraphUtility

		/// <summary>
		/// Returns the graph which contains the specified node.
		/// The graph must be in the <see cref="graphs"/> array.
		///
		/// Returns: Returns the graph which contains the node. Null if the graph wasn't found
		/// </summary>
		public static NavGraph GetGraph (GraphNode node) {
			if (node == null) return null;

			AstarPath script = AstarPath.active;
			if (script == null) return null;

			AstarData data = script.data;
			if (data == null || data.graphs == null) return null;

			uint graphIndex = node.GraphIndex;

			if (graphIndex >= data.graphs.Length) {
				return null;
			}

			return data.graphs[(int)graphIndex];
		}

		/// <summary>Returns the first graph which satisfies the predicate. Returns null if no graph was found.</summary>
		public NavGraph FindGraph (System.Func<NavGraph, bool> predicate) {
			if (graphs != null) {
				for (int i = 0; i < graphs.Length; i++) {
					if (graphs[i] != null && predicate(graphs[i])) {
						return graphs[i];
					}
				}
			}
			return null;
		}

		/// <summary>Returns the first graph of type type found in the <see cref="graphs"/> array. Returns null if no graph was found.</summary>
		public NavGraph FindGraphOfType (System.Type type) {
			return FindGraph(graph => System.Type.Equals(graph.GetType(), type));
		}

		/// <summary>Returns the first graph which inherits from the type type. Returns null if no graph was found.</summary>
		public NavGraph FindGraphWhichInheritsFrom (System.Type type) {
			return FindGraph(graph => WindowsStoreCompatibility.GetTypeInfo(type).IsAssignableFrom(WindowsStoreCompatibility.GetTypeInfo(graph.GetType())));
		}

		/// <summary>
		/// Loop through this function to get all graphs of type 'type'
		/// <code>
		/// foreach (GridGraph graph in AstarPath.data.FindGraphsOfType (typeof(GridGraph))) {
		///     //Do something with the graph
		/// }
		/// </code>
		/// See: AstarPath.RegisterSafeNodeUpdate
		/// </summary>
		public IEnumerable FindGraphsOfType (System.Type type) {
			if (graphs == null) yield break;
			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] != null && System.Type.Equals(graphs[i].GetType(), type)) {
					yield return graphs[i];
				}
			}
		}

		/// <summary>
		/// All graphs which implements the UpdateableGraph interface
		/// <code> foreach (IUpdatableGraph graph in AstarPath.data.GetUpdateableGraphs ()) {
		///  //Do something with the graph
		/// } </code>
		/// See: AstarPath.AddWorkItem
		/// See: Pathfinding.IUpdatableGraph
		/// </summary>
		public IEnumerable GetUpdateableGraphs () {
			if (graphs == null) yield break;
			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] is IUpdatableGraph) {
					yield return graphs[i];
				}
			}
		}

		/// <summary>
		/// All graphs which implements the UpdateableGraph interface
		/// <code> foreach (IRaycastableGraph graph in AstarPath.data.GetRaycastableGraphs ()) {
		///  //Do something with the graph
		/// } </code>
		/// See: Pathfinding.IRaycastableGraph
		/// Deprecated: Deprecated because it is not used by the package internally and the use cases are few. Iterate through the <see cref="graphs"/> array instead.
		/// </summary>
		[System.Obsolete("Obsolete because it is not used by the package internally and the use cases are few. Iterate through the graphs array instead.")]
		public IEnumerable GetRaycastableGraphs () {
			if (graphs == null) yield break;
			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] is IRaycastableGraph) {
					yield return graphs[i];
				}
			}
		}

		/// <summary>Gets the index of the NavGraph in the <see cref="graphs"/> array</summary>
		public int GetGraphIndex (NavGraph graph) {
			if (graph == null) throw new System.ArgumentNullException("graph");

			var index = -1;
			if (graphs != null) {
				index = System.Array.IndexOf(graphs, graph);
				if (index == -1) Debug.LogError("Graph doesn't exist");
			}
			return index;
		}

		#endregion
	}
}
