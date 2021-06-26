using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding.Util;
using Pathfinding.WindowsStore;

#if ASTAR_NO_ZIP
using Pathfinding.Serialization.Zip;
#elif NETFX_CORE
// For Universal Windows Platform
using ZipEntry = System.IO.Compression.ZipArchiveEntry;
using ZipFile = System.IO.Compression.ZipArchive;
#else
using Pathfinding.Ionic.Zip;
#endif

namespace Pathfinding.Serialization {
	/// <summary>Holds information passed to custom graph serializers</summary>
	public class GraphSerializationContext {
		private readonly GraphNode[] id2NodeMapping;

		/// <summary>
		/// Deserialization stream.
		/// Will only be set when deserializing
		/// </summary>
		public readonly BinaryReader reader;

		/// <summary>
		/// Serialization stream.
		/// Will only be set when serializing
		/// </summary>
		public readonly BinaryWriter writer;

		/// <summary>
		/// Index of the graph which is currently being processed.
		/// Version: uint instead of int after 3.7.5
		/// </summary>
		public readonly uint graphIndex;

		/// <summary>Metadata about graphs being deserialized</summary>
		public readonly GraphMeta meta;

		public GraphSerializationContext (BinaryReader reader, GraphNode[] id2NodeMapping, uint graphIndex, GraphMeta meta) {
			this.reader = reader;
			this.id2NodeMapping = id2NodeMapping;
			this.graphIndex = graphIndex;
			this.meta = meta;
		}

		public GraphSerializationContext (BinaryWriter writer) {
			this.writer = writer;
		}

		public void SerializeNodeReference (GraphNode node) {
			writer.Write(node == null ? -1 : node.NodeIndex);
		}

		public GraphNode DeserializeNodeReference () {
			var id = reader.ReadInt32();

			if (id2NodeMapping == null) throw new Exception("Calling DeserializeNodeReference when not deserializing node references");

			if (id == -1) return null;
			GraphNode node = id2NodeMapping[id];
			if (node == null) throw new Exception("Invalid id ("+id+")");
			return node;
		}

		/// <summary>Write a Vector3</summary>
		public void SerializeVector3 (Vector3 v) {
			writer.Write(v.x);
			writer.Write(v.y);
			writer.Write(v.z);
		}

		/// <summary>Read a Vector3</summary>
		public Vector3 DeserializeVector3 () {
			return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
		}

		/// <summary>Write an Int3</summary>
		public void SerializeInt3 (Int3 v) {
			writer.Write(v.x);
			writer.Write(v.y);
			writer.Write(v.z);
		}

		/// <summary>Read an Int3</summary>
		public Int3 DeserializeInt3 () {
			return new Int3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
		}

		public int DeserializeInt (int defaultValue) {
			if (reader.BaseStream.Position <= reader.BaseStream.Length-4) {
				return reader.ReadInt32();
			} else {
				return defaultValue;
			}
		}

		public float DeserializeFloat (float defaultValue) {
			if (reader.BaseStream.Position <= reader.BaseStream.Length-4) {
				return reader.ReadSingle();
			} else {
				return defaultValue;
			}
		}

		/// <summary>Read a UnityEngine.Object</summary>
		public UnityEngine.Object DeserializeUnityObject ( ) {
			int inst = reader.ReadInt32();

			if (inst == int.MaxValue) {
				return null;
			}

			string name = reader.ReadString();
			string typename = reader.ReadString();
			string guid = reader.ReadString();

			System.Type type = System.Type.GetType(typename);

			if (type == null) {
				Debug.LogError("Could not find type '"+typename+"'. Cannot deserialize Unity reference");
				return null;
			}

			if (!string.IsNullOrEmpty(guid)) {
				UnityReferenceHelper[] helpers = UnityEngine.Object.FindObjectsOfType(typeof(UnityReferenceHelper)) as UnityReferenceHelper[];

				for (int i = 0; i < helpers.Length; i++) {
					if (helpers[i].GetGUID() == guid) {
						if (type == typeof(GameObject)) {
							return helpers[i].gameObject;
						} else {
							return helpers[i].GetComponent(type);
						}
					}
				}
			}

			//Try to load from resources
			UnityEngine.Object[] objs = Resources.LoadAll(name, type);

			for (int i = 0; i < objs.Length; i++) {
				if (objs[i].name == name || objs.Length == 1) {
					return objs[i];
				}
			}

			return null;
		}
	}

	/// <summary>
	/// Handles low level serialization and deserialization of graph settings and data.
	/// Mostly for internal use. You can use the methods in the AstarData class for
	/// higher level serialization and deserialization.
	///
	/// See: AstarData
	/// </summary>
	public class AstarSerializer {
		private AstarData data;

		/// <summary>Zip which the data is loaded from</summary>
		private ZipFile zip;

		/// <summary>Memory stream with the zip data</summary>
		private MemoryStream zipStream;

		/// <summary>Graph metadata</summary>
		private GraphMeta meta;

		/// <summary>Settings for serialization</summary>
		private SerializeSettings settings;

		/// <summary>Graphs that are being serialized or deserialized</summary>
		private NavGraph[] graphs;

		/// <summary>
		/// Index used for the graph in the file.
		/// If some graphs were null in the file then graphIndexInZip[graphs[i]] may not equal i.
		/// Used for deserialization.
		/// </summary>
		private Dictionary<NavGraph, int> graphIndexInZip;

		private int graphIndexOffset;

		/// <summary>Extension to use for binary files</summary>
		const string binaryExt = ".binary";

		/// <summary>Extension to use for json files</summary>
		const string jsonExt = ".json";

		/// <summary>
		/// Checksum for the serialized data.
		/// Used to provide a quick equality check in editor code
		/// </summary>
		private uint checksum = 0xffffffff;

		System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();

		/// <summary>Cached StringBuilder to avoid excessive allocations</summary>
		static System.Text.StringBuilder _stringBuilder = new System.Text.StringBuilder();

		/// <summary>
		/// Returns a cached StringBuilder.
		/// This function only has one string builder cached and should
		/// thus only be called from a single thread and should not be called while using an earlier got string builder.
		/// </summary>
		static System.Text.StringBuilder GetStringBuilder () { _stringBuilder.Length = 0; return _stringBuilder; }

		/// <summary>Cached version object for 3.8.3</summary>
		public static readonly System.Version V3_8_3 = new System.Version(3, 8, 3);

		/// <summary>Cached version object for 3.9.0</summary>
		public static readonly System.Version V3_9_0 = new System.Version(3, 9, 0);

		/// <summary>Cached version object for 4.1.0</summary>
		public static readonly System.Version V4_1_0 = new System.Version(4, 1, 0);

		public AstarSerializer (AstarData data) {
			this.data = data;
			settings = SerializeSettings.Settings;
		}

		public AstarSerializer (AstarData data, SerializeSettings settings) {
			this.data = data;
			this.settings = settings;
		}

		public void SetGraphIndexOffset (int offset) {
			graphIndexOffset = offset;
		}

		void AddChecksum (byte[] bytes) {
			checksum = Checksum.GetChecksum(bytes, checksum);
		}

		void AddEntry (string name, byte[] bytes) {
#if NETFX_CORE
			var entry = zip.CreateEntry(name);
			using (var stream = entry.Open()) {
				stream.Write(bytes, 0, bytes.Length);
			}
#else
			zip.AddEntry(name, bytes);
#endif
		}

		public uint GetChecksum () { return checksum; }

		#region Serialize

		public void OpenSerialize () {
			// Create a new zip file, here we will store all the data
			zipStream = new MemoryStream();
#if NETFX_CORE
			zip = new ZipFile(zipStream, System.IO.Compression.ZipArchiveMode.Create);
#else
			zip = new ZipFile();
			zip.AlternateEncoding = System.Text.Encoding.UTF8;
			zip.AlternateEncodingUsage = ZipOption.Always;
#endif
			meta = new GraphMeta();
		}

		public byte[] CloseSerialize () {
			// As the last step, serialize metadata
			byte[] bytes = SerializeMeta();
			AddChecksum(bytes);
			AddEntry("meta"+jsonExt, bytes);

#if !ASTAR_NO_ZIP && !NETFX_CORE
			// Set dummy dates on every file to prevent the binary data to change
			// for identical settings and graphs.
			// Prevents the scene from being marked as dirty in the editor
			// If ASTAR_NO_ZIP is defined this is not relevant since the replacement zip
			// implementation does not even store dates
			var dummy = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			foreach (var entry in zip.Entries) {
				entry.AccessedTime = dummy;
				entry.CreationTime = dummy;
				entry.LastModified = dummy;
				entry.ModifiedTime = dummy;
			}
#endif

			// Save all entries to a single byte array
#if !NETFX_CORE
			zip.Save(zipStream);
#endif
			zip.Dispose();
			bytes = zipStream.ToArray();

			zip = null;
			zipStream = null;
			return bytes;
		}

		public void SerializeGraphs (NavGraph[] _graphs) {
			if (graphs != null) throw new InvalidOperationException("Cannot serialize graphs multiple times.");
			graphs = _graphs;

			if (zip == null) throw new NullReferenceException("You must not call CloseSerialize before a call to this function");

			if (graphs == null) graphs = new NavGraph[0];

			for (int i = 0; i < graphs.Length; i++) {
				//Ignore graph if null
				if (graphs[i] == null) continue;

				// Serialize the graph to a byte array
				byte[] bytes = Serialize(graphs[i]);

				AddChecksum(bytes);
				AddEntry("graph"+i+jsonExt, bytes);
			}
		}

		/// <summary>Serialize metadata about all graphs</summary>
		byte[] SerializeMeta () {
			if (graphs == null) throw new System.Exception("No call to SerializeGraphs has been done");

			meta.version = AstarPath.Version;
			meta.graphs = graphs.Length;
			meta.guids = new List<string>();
			meta.typeNames = new List<string>();

			// For each graph, save the guid
			// of the graph and the type of it
			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] != null) {
					meta.guids.Add(graphs[i].guid.ToString());
					meta.typeNames.Add(graphs[i].GetType().FullName);
				} else {
					meta.guids.Add(null);
					meta.typeNames.Add(null);
				}
			}

			// Grab a cached string builder to avoid allocations
			var output = GetStringBuilder();
			TinyJsonSerializer.Serialize(meta, output);
			return encoding.GetBytes(output.ToString());
		}

		/// <summary>Serializes the graph settings to JSON and returns the data</summary>
		public byte[] Serialize (NavGraph graph) {
			// Grab a cached string builder to avoid allocations
			var output = GetStringBuilder();

			TinyJsonSerializer.Serialize(graph, output);
			return encoding.GetBytes(output.ToString());
		}

		/// <summary>
		/// Deprecated method to serialize node data.
		/// Deprecated: Not used anymore
		/// </summary>
		[System.Obsolete("Not used anymore. You can safely remove the call to this function.")]
		public void SerializeNodes () {
		}

		static int GetMaxNodeIndexInAllGraphs (NavGraph[] graphs) {
			int maxIndex = 0;

			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] == null) continue;
				graphs[i].GetNodes(node => {
					maxIndex = Math.Max(node.NodeIndex, maxIndex);
					if (node.NodeIndex == -1) {
						Debug.LogError("Graph contains destroyed nodes. This is a bug.");
					}
				});
			}
			return maxIndex;
		}

		static byte[] SerializeNodeIndices (NavGraph[] graphs) {
			var stream = new MemoryStream();
			var writer = new BinaryWriter(stream);

			int maxNodeIndex = GetMaxNodeIndexInAllGraphs(graphs);

			writer.Write(maxNodeIndex);

			// While writing node indices, verify that the max node index is the same
			// (user written graphs might have gotten it wrong)
			int maxNodeIndex2 = 0;
			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] == null) continue;
				graphs[i].GetNodes(node => {
					maxNodeIndex2 = Math.Max(node.NodeIndex, maxNodeIndex2);
					writer.Write(node.NodeIndex);
				});
			}

			// Nice to verify if users are writing their own graph types
			if (maxNodeIndex2 != maxNodeIndex) throw new Exception("Some graphs are not consistent in their GetNodes calls, sequential calls give different results.");

			byte[] bytes = stream.ToArray();
			writer.Close();

			return bytes;
		}

		/// <summary>Serializes info returned by NavGraph.SerializeExtraInfo</summary>
		static byte[] SerializeGraphExtraInfo (NavGraph graph) {
			var stream = new MemoryStream();
			var writer = new BinaryWriter(stream);
			var ctx = new GraphSerializationContext(writer);

			((IGraphInternals)graph).SerializeExtraInfo(ctx);
			byte[] bytes = stream.ToArray();
			writer.Close();

			return bytes;
		}

		/// <summary>
		/// Used to serialize references to other nodes e.g connections.
		/// Nodes use the GraphSerializationContext.GetNodeIdentifier and
		/// GraphSerializationContext.GetNodeFromIdentifier methods
		/// for serialization and deserialization respectively.
		/// </summary>
		static byte[] SerializeGraphNodeReferences (NavGraph graph) {
			var stream = new MemoryStream();
			var writer = new BinaryWriter(stream);
			var ctx = new GraphSerializationContext(writer);

			graph.GetNodes(node => node.SerializeReferences(ctx));
			writer.Close();

			var bytes = stream.ToArray();
			return bytes;
		}

		public void SerializeExtraInfo () {
			if (!settings.nodes) return;
			if (graphs == null) throw new InvalidOperationException("Cannot serialize extra info with no serialized graphs (call SerializeGraphs first)");

			var bytes = SerializeNodeIndices(graphs);
			AddChecksum(bytes);
			AddEntry("graph_references"+binaryExt, bytes);

			for (int i = 0; i < graphs.Length; i++) {
				if (graphs[i] == null) continue;

				bytes = SerializeGraphExtraInfo(graphs[i]);
				AddChecksum(bytes);
				AddEntry("graph"+i+"_extra"+binaryExt, bytes);

				bytes = SerializeGraphNodeReferences(graphs[i]);
				AddChecksum(bytes);
				AddEntry("graph"+i+"_references"+binaryExt, bytes);
			}

			bytes = SerializeNodeLinks();
			AddChecksum(bytes);
			AddEntry("node_link2" + binaryExt, bytes);
		}

		byte[] SerializeNodeLinks () {
			var stream = new MemoryStream();

#if !ASTAR_NO_LINKS
			var writer = new BinaryWriter(stream);
			var ctx = new GraphSerializationContext(writer);
			NodeLink2.SerializeReferences(ctx);
#endif
			return stream.ToArray();
		}

		#endregion

		#region Deserialize

		ZipEntry GetEntry (string name) {
#if NETFX_CORE
			return zip.GetEntry(name);
#else
			return zip[name];
#endif
		}

		bool ContainsEntry (string name) {
			return GetEntry(name) != null;
		}

		public bool OpenDeserialize (byte[] bytes) {
			// Copy the bytes to a stream
			zipStream = new MemoryStream();
			zipStream.Write(bytes, 0, bytes.Length);
			zipStream.Position = 0;
			try {
#if NETFX_CORE
				zip = new ZipFile(zipStream);
#else
				zip = ZipFile.Read(zipStream);
#endif
			} catch (Exception e) {
				// Catches exceptions when an invalid zip file is found
				Debug.LogError("Caught exception when loading from zip\n"+e);

				zipStream.Dispose();
				return false;
			}

			if (ContainsEntry("meta" + jsonExt)) {
				meta = DeserializeMeta(GetEntry("meta" + jsonExt));
			} else if (ContainsEntry("meta" + binaryExt)) {
				meta = DeserializeBinaryMeta(GetEntry("meta" + binaryExt));
			} else {
				throw new Exception("No metadata found in serialized data.");
			}

			if (FullyDefinedVersion(meta.version) > FullyDefinedVersion(AstarPath.Version)) {
				Debug.LogWarning("Trying to load data from a newer version of the A* Pathfinding Project\nCurrent version: "+AstarPath.Version+" Data version: "+meta.version +
					"\nThis is usually fine as the stored data is usually backwards and forwards compatible." +
					"\nHowever node data (not settings) can get corrupted between versions (even though I try my best to keep compatibility), so it is recommended " +
					"to recalculate any caches (those for faster startup) and resave any files. Even if it seems to load fine, it might cause subtle bugs.\n");
			} else if (FullyDefinedVersion(meta.version) < FullyDefinedVersion(AstarPath.Version)) {
				Debug.LogWarning("Upgrading serialized pathfinding data from version " + meta.version + " to " + AstarPath.Version +
					"\nThis is usually fine, it just means you have upgraded to a new version." +
					"\nHowever node data (not settings) can get corrupted between versions (even though I try my best to keep compatibility), so it is recommended " +
					"to recalculate any caches (those for faster startup) and resave any files. Even if it seems to load fine, it might cause subtle bugs.\n");
			}
			return true;
		}

		/// <summary>
		/// Returns a version with all fields fully defined.
		/// This is used because by default new Version(3,0,0) > new Version(3,0).
		/// This is not the desired behaviour so we make sure that all fields are defined here
		/// </summary>
		static System.Version FullyDefinedVersion (System.Version v) {
			return new System.Version(Mathf.Max(v.Major, 0), Mathf.Max(v.Minor, 0), Mathf.Max(v.Build, 0), Mathf.Max(v.Revision, 0));
		}

		public void CloseDeserialize () {
			zipStream.Dispose();
			zip.Dispose();
			zip = null;
			zipStream = null;
		}

		NavGraph DeserializeGraph (int zipIndex, int graphIndex, System.Type[] availableGraphTypes) {
			// Get the graph type from the metadata we deserialized earlier
			var graphType = meta.GetGraphType(zipIndex, availableGraphTypes);

			// Graph was null when saving, ignore
			if (System.Type.Equals(graphType, null)) return null;

			// Create a new graph of the right type
			NavGraph graph = data.CreateGraph(graphType);
			graph.graphIndex = (uint)(graphIndex);

			var jsonName = "graph" + zipIndex + jsonExt;
			var binName = "graph" + zipIndex + binaryExt;

			if (ContainsEntry(jsonName)) {
				// Read the graph settings
				TinyJsonDeserializer.Deserialize(GetString(GetEntry(jsonName)), graphType, graph);
			} else if (ContainsEntry(binName)) {
				var reader = GetBinaryReader(GetEntry(binName));
				var ctx = new GraphSerializationContext(reader, null, graph.graphIndex, meta);
				((IGraphInternals)graph).DeserializeSettingsCompatibility(ctx);
			} else {
				throw new FileNotFoundException("Could not find data for graph " + zipIndex + " in zip. Entry 'graph" + zipIndex + jsonExt + "' does not exist");
			}

			if (graph.guid.ToString() != meta.guids[zipIndex])
				throw new Exception("Guid in graph file not equal to guid defined in meta file. Have you edited the data manually?\n"+graph.guid+" != "+meta.guids[zipIndex]);

			return graph;
		}

		/// <summary>
		/// Deserializes graph settings.
		/// Note: Stored in files named "graph<see cref=".json"/>" where # is the graph number.
		/// </summary>
		public NavGraph[] DeserializeGraphs (System.Type[] availableGraphTypes) {
			// Allocate a list of graphs to be deserialized
			var graphList = new List<NavGraph>();

			graphIndexInZip = new Dictionary<NavGraph, int>();

			for (int i = 0; i < meta.graphs; i++) {
				var newIndex = graphList.Count + graphIndexOffset;
				var graph = DeserializeGraph(i, newIndex, availableGraphTypes);
				if (graph != null) {
					graphList.Add(graph);
					graphIndexInZip[graph] = i;
				}
			}

			graphs = graphList.ToArray();
			return graphs;
		}

		bool DeserializeExtraInfo (NavGraph graph) {
			var zipIndex = graphIndexInZip[graph];
			var entry = GetEntry("graph"+zipIndex+"_extra"+binaryExt);

			if (entry == null)
				return false;

			var reader = GetBinaryReader(entry);

			var ctx = new GraphSerializationContext(reader, null, graph.graphIndex, meta);

			// Call the graph to process the data
			((IGraphInternals)graph).DeserializeExtraInfo(ctx);
			return true;
		}

		bool AnyDestroyedNodesInGraphs () {
			bool result = false;

			for (int i = 0; i < graphs.Length; i++) {
				graphs[i].GetNodes(node => {
					if (node.Destroyed) {
						result = true;
					}
				});
			}
			return result;
		}

		GraphNode[] DeserializeNodeReferenceMap () {
			// Get the file containing the list of all node indices
			// This is correlated with the new indices of the nodes and a mapping from old to new
			// is done so that references can be resolved
			var entry = GetEntry("graph_references"+binaryExt);

			if (entry == null) throw new Exception("Node references not found in the data. Was this loaded from an older version of the A* Pathfinding Project?");

			var reader = GetBinaryReader(entry);
			int maxNodeIndex = reader.ReadInt32();
			var int2Node = new GraphNode[maxNodeIndex+1];

			try {
				for (int i = 0; i < graphs.Length; i++) {
					graphs[i].GetNodes(node => {
						var index = reader.ReadInt32();
						int2Node[index] = node;
					});
				}
			} catch (Exception e) {
				throw new Exception("Some graph(s) has thrown an exception during GetNodes, or some graph(s) have deserialized more or fewer nodes than were serialized", e);
			}

#if !NETFX_CORE
			// For Windows Store apps the BaseStream.Position property is not supported
			// so we have to disable this error check on that platform
			if (reader.BaseStream.Position != reader.BaseStream.Length) {
				throw new Exception((reader.BaseStream.Length / 4) + " nodes were serialized, but only data for " + (reader.BaseStream.Position / 4) + " nodes was found. The data looks corrupt.");
			}
#endif

			reader.Close();
			return int2Node;
		}

		void DeserializeNodeReferences (NavGraph graph, GraphNode[] int2Node) {
			var zipIndex = graphIndexInZip[graph];
			var entry = GetEntry("graph"+zipIndex+"_references"+binaryExt);

			if (entry == null) throw new Exception("Node references for graph " + zipIndex + " not found in the data. Was this loaded from an older version of the A* Pathfinding Project?");

			var reader = GetBinaryReader(entry);
			var ctx = new GraphSerializationContext(reader, int2Node, graph.graphIndex, meta);

			graph.GetNodes(node => node.DeserializeReferences(ctx));
		}

		/// <summary>
		/// Deserializes extra graph info.
		/// Extra graph info is specified by the graph types.
		/// See: Pathfinding.NavGraph.DeserializeExtraInfo
		/// Note: Stored in files named "graph<see cref="_extra.binary"/>" where # is the graph number.
		/// </summary>
		public void DeserializeExtraInfo () {
			bool anyDeserialized = false;

			// Loop through all graphs and deserialize the extra info
			// if there is any such info in the zip file
			for (int i = 0; i < graphs.Length; i++) {
				anyDeserialized |= DeserializeExtraInfo(graphs[i]);
			}

			if (!anyDeserialized) {
				return;
			}

			// Sanity check
			// Make sure the graphs don't contain destroyed nodes
			if (AnyDestroyedNodesInGraphs()) {
				Debug.LogError("Graph contains destroyed nodes. This is a bug.");
			}

			// Deserialize map from old node indices to new nodes
			var int2Node = DeserializeNodeReferenceMap();

			// Deserialize node references
			for (int i = 0; i < graphs.Length; i++) {
				DeserializeNodeReferences(graphs[i], int2Node);
			}

			DeserializeNodeLinks(int2Node);
		}

		void DeserializeNodeLinks (GraphNode[] int2Node) {
#if !ASTAR_NO_LINKS
			var entry = GetEntry("node_link2"+binaryExt);

			if (entry == null)
				return;

			var reader = GetBinaryReader(entry);
			var ctx = new GraphSerializationContext(reader, int2Node, 0, meta);
			NodeLink2.DeserializeReferences(ctx);
#endif
		}

		/// <summary>Calls PostDeserialization on all loaded graphs</summary>
		public void PostDeserialization () {
			for (int i = 0; i < graphs.Length; i++) {
				var ctx = new GraphSerializationContext(null, null, 0, meta);
				((IGraphInternals)graphs[i]).PostDeserialization(ctx);
			}
		}

		/// <summary>
		/// Deserializes graph editor settings.
		/// For future compatibility this method does not assume that the graphEditors array matches the <see cref="graphs"/> array in order and/or count.
		/// It searches for a matching graph (matching if graphEditor.target == graph) for every graph editor.
		/// Multiple graph editors should not refer to the same graph.\n
		/// Note: Stored in files named "graph<see cref="_editor.json"/>" where # is the graph number.
		///
		/// Note: This method is only used for compatibility, newer versions store everything in the graph.serializedEditorSettings field which is already serialized.
		/// </summary>
		public void DeserializeEditorSettingsCompatibility () {
			for (int i = 0; i < graphs.Length; i++) {
				var zipIndex = graphIndexInZip[graphs[i]];
				ZipEntry entry = GetEntry("graph"+zipIndex+"_editor"+jsonExt);
				if (entry == null) continue;

				(graphs[i] as IGraphInternals).SerializedEditorSettings = GetString(entry);
			}
		}

		/// <summary>Returns a binary reader for the data in the zip entry</summary>
		private static BinaryReader GetBinaryReader (ZipEntry entry) {
#if NETFX_CORE
			return new BinaryReader(entry.Open());
#else
			var stream = new System.IO.MemoryStream();

			entry.Extract(stream);
			stream.Position = 0;
			return new System.IO.BinaryReader(stream);
#endif
		}

		/// <summary>Returns the data in the zip entry as a string</summary>
		private static string GetString (ZipEntry entry) {
#if NETFX_CORE
			var reader = new StreamReader(entry.Open());
#else
			var buffer = new MemoryStream();

			entry.Extract(buffer);
			buffer.Position = 0;
			var reader = new StreamReader(buffer);
#endif
			string s = reader.ReadToEnd();
			reader.Dispose();
			return s;
		}

		private GraphMeta DeserializeMeta (ZipEntry entry) {
			return TinyJsonDeserializer.Deserialize(GetString(entry), typeof(GraphMeta)) as GraphMeta;
		}

		private GraphMeta DeserializeBinaryMeta (ZipEntry entry) {
			var meta = new GraphMeta();

			var reader = GetBinaryReader(entry);

			if (reader.ReadString() != "A*") throw new System.Exception("Invalid magic number in saved data");
			int major = reader.ReadInt32();
			int minor = reader.ReadInt32();
			int build = reader.ReadInt32();
			int revision = reader.ReadInt32();

			// Required because when saving a version with a field not set, it will save it as -1
			// and then the Version constructor will throw an exception (which we do not want)
			if (major < 0) meta.version = new Version(0, 0);
			else if (minor < 0) meta.version = new Version(major, 0);
			else if (build < 0) meta.version = new Version(major, minor);
			else if (revision < 0) meta.version = new Version(major, minor, build);
			else meta.version = new Version(major, minor, build, revision);

			meta.graphs = reader.ReadInt32();

			meta.guids = new List<string>();
			int count = reader.ReadInt32();
			for (int i = 0; i < count; i++) meta.guids.Add(reader.ReadString());

			meta.typeNames = new List<string>();
			count = reader.ReadInt32();
			for (int i = 0; i < count; i++) meta.typeNames.Add(reader.ReadString());

			return meta;
		}


		#endregion

		#region Utils

		/// <summary>Save the specified data at the specified path</summary>
		public static void SaveToFile (string path, byte[] data) {
#if NETFX_CORE
			throw new System.NotSupportedException("Cannot save to file on this platform");
#else
			using (var stream = new FileStream(path, FileMode.Create)) {
				stream.Write(data, 0, data.Length);
			}
#endif
		}

		/// <summary>Load the specified data from the specified path</summary>
		public static byte[] LoadFromFile (string path) {
#if NETFX_CORE
			throw new System.NotSupportedException("Cannot load from file on this platform");
#else
			using (var stream = new FileStream(path, FileMode.Open)) {
				var bytes = new byte[(int)stream.Length];
				stream.Read(bytes, 0, (int)stream.Length);
				return bytes;
			}
#endif
		}

		#endregion
	}

	/// <summary>Metadata for all graphs included in serialization</summary>
	public class GraphMeta {
		/// <summary>Project version it was saved with</summary>
		public Version version;

		/// <summary>Number of graphs serialized</summary>
		public int graphs;

		/// <summary>Guids for all graphs</summary>
		public List<string> guids;

		/// <summary>Type names for all graphs</summary>
		public List<string> typeNames;

		/// <summary>Returns the Type of graph number index</summary>
		public Type GetGraphType (int index, System.Type[] availableGraphTypes) {
			// The graph was null when saving. Ignore it
			if (String.IsNullOrEmpty(typeNames[index])) return null;

			for (int j = 0; j < availableGraphTypes.Length; j++) {
				if (availableGraphTypes[j].FullName == typeNames[index]) return availableGraphTypes[j];
			}

			throw new Exception("No graph of type '" + typeNames[index] + "' could be created, type does not exist");
		}
	}

	/// <summary>Holds settings for how graphs should be serialized</summary>
	public class SerializeSettings {
		/// <summary>
		/// Enable to include node data.
		/// If false, only settings will be saved
		/// </summary>
		public bool nodes = true;

		/// <summary>
		/// Use pretty printing for the json data.
		/// Good if you want to open up the saved data and edit it manually
		/// </summary>
		[System.Obsolete("There is no support for pretty printing the json anymore")]
		public bool prettyPrint;

		/// <summary>
		/// Save editor settings.
		/// Warning: Only applicable when saving from the editor using the AstarPathEditor methods
		/// </summary>
		public bool editorSettings;

		/// <summary>Serialization settings for only saving graph settings</summary>
		public static SerializeSettings Settings {
			get {
				return new SerializeSettings {
						   nodes = false
				};
			}
		}
	}
}
