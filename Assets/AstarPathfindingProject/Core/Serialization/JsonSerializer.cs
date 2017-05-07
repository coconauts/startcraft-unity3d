//#define ASTARDEBUG
//#define ASTAR_FAST_NO_EXCEPTIONS
//#define ASTAR_NO_JSON //Strips out usage of the JsonFx library. Slightly lower build size but reduces backwards and forwards compatibility of saving graph settings. Only works properly with the NavmeshGraph right now. You can remove the Pathfinding.JsonFx.dll file if you enable this.
//#define ASTAR_NO_ZIP
using System;
using Pathfinding;
using Pathfinding.Serialization.JsonFx;

using Pathfinding.Ionic.Zip;

using System.IO;
using UnityEngine;
using System.Collections.Generic;
using Pathfinding.Util;

namespace Pathfinding.Serialization
{
	
	/** Holds information passed to custom graph serializers */
	public class GraphSerializationContext {
		
		private readonly GraphNode[] id2NodeMapping;
		
		public readonly System.IO.BinaryReader reader;
		public readonly System.IO.BinaryWriter writer;
		public readonly int graphIndex;
		
		public GraphSerializationContext (System.IO.BinaryReader reader, GraphNode[] id2NodeMapping, int graphIndex) {
			this.reader = reader;
			this.id2NodeMapping = id2NodeMapping;
			this.graphIndex = graphIndex;
		}
		
		public GraphSerializationContext (System.IO.BinaryWriter writer) {
			this.writer = writer;
		}
		
		public int GetNodeIdentifier (GraphNode node) {
			return node == null ? -1 : node.NodeIndex;
		}
		
		public GraphNode GetNodeFromIdentifier (int id) {
			if (id2NodeMapping == null) throw new System.Exception ("Calling GetNodeFromIdentifier when serializing");
			
			if (id == -1) return null;
			GraphNode node = id2NodeMapping[id];
			if (node == null) throw new System.Exception ("Invalid id");
			return node;
		}
	}
	
	/** Handles low level serialization and deserialization of graph settings and data */
	public class AstarSerializer
	{
		
		private AstarData data;
		public JsonWriterSettings writerSettings;
		public JsonReaderSettings readerSettings;

		private ZipFile zip;
		private MemoryStream str;
		
		private GraphMeta meta;
		
		private SerializeSettings settings;
		
		private NavGraph[] graphs;

		const string binaryExt = ".binary";
		const string jsonExt = ".json";

		private uint checksum = 0xffffffff;

		System.Text.UTF8Encoding encoding=new System.Text.UTF8Encoding();

		private static System.Text.StringBuilder _stringBuilder = new System.Text.StringBuilder();
		
		/** Returns a cached StringBuilder.
		 * This function only has one string builder cached and should
		 * thus only be called from a single thread and should not be called while using an earlier got string builder.
		 */
		private static System.Text.StringBuilder GetStringBuilder () { _stringBuilder.Length = 0; return _stringBuilder; }
		
		public AstarSerializer (AstarData data) {
			this.data = data;
			settings = SerializeSettings.Settings;
		}
		
		public AstarSerializer (AstarData data, SerializeSettings settings) {
			this.data = data;
			this.settings = settings;
		}
		
		public void AddChecksum (byte[] bytes) {
			checksum = Checksum.GetChecksum (bytes,checksum);
		}
		
		public uint GetChecksum () { return checksum; }
		
#region Serialize
		
		public void OpenSerialize () {
			zip = new ZipFile();
			zip.AlternateEncoding = System.Text.Encoding.UTF8;
			zip.AlternateEncodingUsage = ZipOption.Always;

			writerSettings = new JsonWriterSettings();
			writerSettings.AddTypeConverter (new VectorConverter());
			writerSettings.AddTypeConverter (new BoundsConverter());
			writerSettings.AddTypeConverter (new LayerMaskConverter());
			writerSettings.AddTypeConverter (new MatrixConverter());
			writerSettings.AddTypeConverter (new GuidConverter());
			writerSettings.AddTypeConverter (new UnityObjectConverter());
			
			//writerSettings.DebugMode = true;
			writerSettings.PrettyPrint = settings.prettyPrint;
			meta = new GraphMeta();
		}
		
		public byte[] CloseSerialize () {
			byte[] bytes = SerializeMeta ();
			
			AddChecksum (bytes);
			zip.AddEntry("meta"+jsonExt,bytes);
			
			MemoryStream output = new MemoryStream();
    		zip.Save(output);
			bytes = output.ToArray();
			output.Dispose();
			
			
			zip.Dispose();
			
			zip = null;
			return bytes;
		}
		
		public void SerializeGraphs (NavGraph[] _graphs) {
			if (graphs != null) throw new InvalidOperationException ("Cannot serialize graphs multiple times.");
			graphs = _graphs;
			
			if (zip == null) throw new NullReferenceException ("You must not call CloseSerialize before a call to this function");
			
			if (graphs == null) graphs = new NavGraph[0];
			
			for (int i=0;i<graphs.Length;i++) {
				//Ignore graph if null
				if (graphs[i] == null) continue;
				
				byte[] bytes = Serialize(graphs[i]);
				
				AddChecksum (bytes);
				zip.AddEntry ("graph"+i+jsonExt,bytes);
			}
		}
		
		public void SerializeUserConnections (UserConnection[] conns) {
			if (conns == null) conns = new UserConnection[0];
			
			System.Text.StringBuilder output = GetStringBuilder ();//new System.Text.StringBuilder();
			JsonWriter writer = new JsonWriter (output,writerSettings);
			writer.Write (conns);
			
			byte[] bytes = encoding.GetBytes (output.ToString());
			output = null;
			
			//If length is <= 2 that means nothing was serialized (file is "[]")
			if (bytes.Length <= 2) return;
			
			AddChecksum (bytes);
			zip.AddEntry ("connections"+jsonExt,bytes);
		}
		
		/** Serialize metadata about alll graphs */
		private byte[] SerializeMeta () {
			
			meta.version = AstarPath.Version;
			meta.graphs = data.graphs.Length;
			meta.guids = new string[data.graphs.Length];
			meta.typeNames = new string[data.graphs.Length];
			meta.nodeCounts = new int[data.graphs.Length];
			//meta.settings = settings;
			
			for (int i=0;i<data.graphs.Length;i++) {
				if (data.graphs[i] == null) continue;
				
				meta.guids[i] = data.graphs[i].guid.ToString();
				meta.typeNames[i] = data.graphs[i].GetType().FullName;
				
				//meta.nodeCounts[i] = data.graphs[i].nodes==null?0:data.graphs[i].nodes.Length;
			}

			System.Text.StringBuilder output = GetStringBuilder ();//new System.Text.StringBuilder();
			JsonWriter writer = new JsonWriter (output,writerSettings);
			writer.Write (meta);
			
			return encoding.GetBytes (output.ToString());
		}
		
		/** Serializes the graph settings to JSON and returns the data */
		public byte[] Serialize (NavGraph graph) {
			System.Text.StringBuilder output = GetStringBuilder ();//new System.Text.StringBuilder();
			JsonWriter writer = new JsonWriter (output,writerSettings);
			writer.Write (graph);
			
			return encoding.GetBytes (output.ToString());
		}
		
		public void SerializeNodes () {
			if (!settings.nodes) return;
			if (graphs == null) throw new InvalidOperationException ("Cannot serialize nodes with no serialized graphs (call SerializeGraphs first)");
			
			for (int i=0;i<graphs.Length;i++) {
				
				byte[] bytes = SerializeNodes (i);
				
				AddChecksum (bytes);
				zip.AddEntry ("graph"+i+"_nodes"+binaryExt,bytes);
			}
			
			for (int i=0;i<graphs.Length;i++) {
				byte[] bytes = SerializeNodeConnections (i);
				
				AddChecksum (bytes);
				zip.AddEntry ("graph"+i+"_conns"+binaryExt,bytes);
			}
		}
		
		private byte[] SerializeNodes (int index) {
			
			return new byte[0];
			/*
			NavGraph graph = graphs[index];
			MemoryStream str = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(str);
			
			GraphNode[] nodes = graph.nodes;
			
			if (nodes == null) nodes = new GraphNode[0];
			
			//Write basic node data.
			//Divide in to different chunks to possibly yield better compression rates with zip
			//The integers above each chunk is a tag to identify each chunk to be able to load them correctly
			
			writer.Write(1);
			for (int i=0;i<nodes.Length;i++) {
				GraphNode node = nodes[i];
				if (node == null) {
					writer.Write(0);
					writer.Write(0);
					writer.Write(0);
				} else {
					writer.Write (node.Position.x);
					writer.Write (node.Position.y);
					writer.Write (node.Position.z);
				}
			}
			
			writer.Write(2);
			for (int i=0;i<nodes.Length;i++) {
				if (nodes[i] == null)	writer.Write (0);
				else					writer.Write (nodes[i].Penalty);
			}
			
			writer.Write(3);
			for (int i=0;i<nodes.Length;i++) {
				if (nodes[i] == null)	writer.Write (0);
				else 					writer.Write (nodes[i].Flags);
			}
			
			writer.Close();
			return str.ToArray();*/
		}
		
		public void SerializeExtraInfo () {
			if (!settings.nodes) return;
			
			int totCount = 0;
			for (int i=0;i<graphs.Length;i++) {
				if (graphs[i] == null) continue;
				graphs[i].GetNodes (delegate (GraphNode node) {
					
					totCount = System.Math.Max (node.NodeIndex, totCount);
					if (node.NodeIndex == -1) {
						Debug.LogError ("Graph contains destroyed nodes. This is a bug.");
					}
					return true;
				});
			}
			
			{
				MemoryStream stream = new MemoryStream ();
				BinaryWriter wr = new BinaryWriter (stream);
				
				wr.Write (totCount);
				
				int c = 0;
				for (int i=0;i<graphs.Length;i++) {
					if (graphs[i] == null) continue;
					graphs[i].GetNodes (delegate (GraphNode node) {
						c = System.Math.Max (node.NodeIndex, c);
						wr.Write (node.NodeIndex);
						return true;
					});
				}
				
				if (c != totCount) throw new System.Exception ("Some graphs are not consistent in their GetNodes calls, sequential calls give different results.");

				byte[] bytes = stream.ToArray ();
				wr.Close ();

				
				AddChecksum (bytes);
				zip.AddEntry ("graph_references"+binaryExt,bytes);
			}
					
			for (int i=0;i<graphs.Length;i++) {
				if (graphs[i] == null) continue;
				
				MemoryStream stream = new MemoryStream ();
				BinaryWriter wr = new BinaryWriter (stream);
				GraphSerializationContext ctx = new GraphSerializationContext(wr);
				
				graphs[i].SerializeExtraInfo (ctx);
				byte[] bytes = stream.ToArray ();

				wr.Close ();
				
				AddChecksum (bytes);
				zip.AddEntry ("graph"+i+"_extra"+binaryExt,bytes);
				
				
				stream = new MemoryStream ();
				wr = new BinaryWriter (stream);
				ctx = new GraphSerializationContext(wr);
				graphs[i].GetNodes (delegate (GraphNode node) {
					node.SerializeReferences (ctx);
					return true;
				});

				wr.Close ();
				
				bytes = stream.ToArray ();
				
				AddChecksum (bytes);
				zip.AddEntry ("graph"+i+"_references"+binaryExt,bytes);
			}
		}
		
		/** Serialize node connections for given graph index.
Connections structure is as follows. Bracket structure has nothing to do with data, just how it is structured:\n
\code
for every node {
	Int32 NodeIndex
	Int16 ConnectionCount
	for every connection of the node {
		Int32 OtherNodeIndex
		Int32 ConnectionCost
	}
}
\endcode
		*/
		private byte[] SerializeNodeConnections (int index) {
			
			return new byte[0];
			/*
			NavGraph graph = graphs[index];
			MemoryStream str = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(str);
			
			
			if (graph.nodes == null) return new byte[0];
			
			GraphNode[] nodes = graph.nodes;
			
			for (int i=0;i<nodes.Length;i++) {
				GraphNode node = nodes[i];
#if FALSE
				if (node.connections == null) { writer.Write((ushort)0); continue; }
				
				if (node.connections.Length	!= node.connectionCosts.Length)
					throw new IndexOutOfRangeException ("Node.connections.Length != Node.connectionCosts.Length. In node "+i+" in graph "+index);
				
				//writer.Write(node.GetNodeIndex());
				writer.Write ((ushort)node.connections.Length);
				
				for (int j=0;j<node.connections.Length;j++) {
					writer.Write(node.connections[j].GetNodeIndex());
					writer.Write(node.connectionCosts[j]);
				}
#endif
			}
			
			writer.Close();
			return str.ToArray();*/
		}
		
//#if UNITY_EDITOR
		public void SerializeEditorSettings (GraphEditorBase[] editors) {
			if (editors == null || !settings.editorSettings) return;

			for (int i=0;i<editors.Length;i++) {
				if (editors[i] == null) return;
				
				System.Text.StringBuilder output = GetStringBuilder ();//new System.Text.StringBuilder();
				JsonWriter writer = new JsonWriter (output,writerSettings);
				writer.Write (editors[i]);
				
				byte[] bytes = encoding.GetBytes (output.ToString());
				
				//Less or equal to 2 bytes means that nothing was saved (file is "{}")
				if (bytes.Length <= 2)
					continue;
				
				AddChecksum(bytes);
				zip.AddEntry ("graph"+i+"_editor"+jsonExt,bytes);
			}
		}
//#endif
		
#endregion
		
#region Deserialize
		
		public bool OpenDeserialize (byte[] bytes) {
			readerSettings = new JsonReaderSettings();
			readerSettings.AddTypeConverter (new VectorConverter());
			readerSettings.AddTypeConverter (new BoundsConverter());
			readerSettings.AddTypeConverter (new LayerMaskConverter());
			readerSettings.AddTypeConverter (new MatrixConverter());
			readerSettings.AddTypeConverter (new GuidConverter());
			readerSettings.AddTypeConverter (new UnityObjectConverter());

			str = new MemoryStream();
			str.Write(bytes,0,bytes.Length);
			str.Position = 0;
			try {
				zip = ZipFile.Read(str);
			} catch (System.Exception e) {
				//Catches exceptions when an invalid zip file is found
				Debug.LogWarning ("Caught exception when loading from zip\n"+e);

				str.Dispose ();
				return false;
			}
			meta = DeserializeMeta (zip["meta"+jsonExt]);
			
			if (meta.version > AstarPath.Version) {
				Debug.LogWarning ("Trying to load data from a newer version of the A* Pathfinding Project\nCurrent version: "+AstarPath.Version+" Data version: "+meta.version);
			} else if (meta.version < AstarPath.Version) {
				Debug.LogWarning ("Trying to load data from an older version of the A* Pathfinding Project\nCurrent version: "+AstarPath.Version+" Data version: "+meta.version
					+ "\nThis is usually fine, it just means you have upgraded to a new version.\nHowever node data (not settings) can get corrupted between versions, so it is recommended" +
						"to recalculate any caches (those for faster startup) and resave any files. Even if it seems to load fine, it might cause subtle bugs.\n");
			}
			return true;
		}
		
		public void CloseDeserialize () {
			str.Dispose();
			zip.Dispose();
			zip = null;
			str = null;
		}
		
		/** Deserializes graph settings.
		 * \note Stored in files named "graph#.json" where # is the graph number.
		 */
		public NavGraph[] DeserializeGraphs () {
			
			//for (int j=0;j<1;j++) {
			//System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
			//watch.Start();
			
			graphs = new NavGraph[meta.graphs];

			int nonNull = 0;

			for (int i=0;i<meta.graphs;i++) {
				Type tp = meta.GetGraphType(i);
				
				//Graph was null when saving, ignore
				if (System.Type.Equals (tp, null)) continue;

				nonNull++;

				ZipEntry entry = zip["graph"+i+jsonExt];
				
				if (entry == null)
					throw new FileNotFoundException ("Could not find data for graph "+i+" in zip. Entry 'graph+"+i+jsonExt+"' does not exist");


				NavGraph tmp = data.CreateGraph(tp);//(NavGraph)System.Activator.CreateInstance(tp);

				String entryText = GetString(entry);
					
				JsonReader reader = new JsonReader(entryText,readerSettings);
				
				//NavGraph graph = tmp.Deserialize(reader);//reader.Deserialize<NavGraph>();
				reader.PopulateObject (ref tmp);
				

				graphs[i] = tmp;
				if (graphs[i].guid.ToString () != meta.guids[i])
					throw new System.Exception ("Guid in graph file not equal to guid defined in meta file. Have you edited the data manually?\n"+graphs[i].guid.ToString()+" != "+meta.guids[i]);
				
				//NavGraph graph = (NavGraph)JsonConvert.DeserializeObject (entryText,tp,settings);
			}

			NavGraph[] compressed = new NavGraph[nonNull];
			nonNull = 0;
			for ( int i=0;i<graphs.Length;i++) {
				if ( graphs[i] != null ) {
					compressed[nonNull] = graphs[i];
					nonNull++;
				}
			}

			graphs = compressed;

			return graphs;
			
			//watch.Stop();
			//Debug.Log ((watch.ElapsedTicks*0.0001).ToString ("0.00"));
			//}
		}
		
		/** Deserializes manually created connections.
		 * Connections are created in the A* inspector.
		 * \note Stored in a file named "connections.json".
		 */
		public UserConnection[] DeserializeUserConnections () {
			ZipEntry entry = zip["connections"+jsonExt];
			
			if (entry == null) return new UserConnection[0];
			
			string entryText = GetString (entry);
			JsonReader reader = new JsonReader(entryText,readerSettings);
			UserConnection[] conns = (UserConnection[])reader.Deserialize(typeof(UserConnection[]));
			return conns;
		}
		
		/** Deserializes nodes.
		 * Nodes can be saved to enable loading a full scanned graph from memory/file without scanning the graph first.
		 * \note Node info is stored in files named "graph#_nodes.binary" where # is the graph number.
		 * \note Connectivity info is stored in files named "graph#_conns.binary" where # is the graph number.
		 */
		public void DeserializeNodes () {
			for (int i=0;i<graphs.Length;i++) {
				if (graphs[i] == null) continue;
				
				if (zip.ContainsEntry("graph"+i+"_nodes"+binaryExt)) {
					//Create nodes
					//graphs[i].nodes = graphs[i].CreateNodes (meta.nodeCounts[i]);
					//throw new System.NotSupportedException ();
				} else {
					//graphs[i].nodes = graphs[i].CreateNodes (0);
				}
			}
			
			for (int i=0;i<graphs.Length;i++) {
				if (graphs[i] == null) continue;
				
				ZipEntry entry = zip["graph"+i+"_nodes"+binaryExt];
				if (entry == null) continue;
				
				MemoryStream str = new MemoryStream();
				
				
				entry.Extract (str);
				str.Position = 0;
				BinaryReader reader = new BinaryReader(str);
				
				DeserializeNodes (i, reader);
			}
			
			for (int i=0;i<graphs.Length;i++) {
				if (graphs[i] == null) continue;
				
				ZipEntry entry = zip["graph"+i+"_conns"+binaryExt];
				if (entry == null) continue;
				
				MemoryStream str = new MemoryStream();
				
				entry.Extract (str);
				str.Position = 0;
				BinaryReader reader = new BinaryReader(str);
				
				DeserializeNodeConnections (i, reader);
			}
		}
		
		/** Deserializes extra graph info.
		 * Extra graph info is specified by the graph types.
		 * \see Pathfinding.NavGraph.DeserializeExtraInfo
		 * \note Stored in files named "graph#_extra.binary" where # is the graph number.
		 */
		public void DeserializeExtraInfo () {
			
			bool anySerialized = false;
			
			for (int i=0;i<graphs.Length;i++) {
				ZipEntry entry = zip["graph"+i+"_extra"+binaryExt];
				if (entry == null) continue;
				
				anySerialized = true;
				
				MemoryStream str = new MemoryStream();
				
				entry.Extract (str);
				str.Seek (0, SeekOrigin.Begin);
				
				BinaryReader reader = new BinaryReader (str);
				//byte[] bytes = str.ToArray();
				
				GraphSerializationContext ctx = new GraphSerializationContext(reader, null, i);
				
				graphs[i].DeserializeExtraInfo (ctx);
			}
			
			if (!anySerialized) {
				return;
			}
			
			int totCount = 0;
			for (int i=0;i<graphs.Length;i++) {
				if (graphs[i] == null) continue;
				graphs[i].GetNodes (delegate (GraphNode node) {
					
					totCount = System.Math.Max (node.NodeIndex, totCount);
					if (node.NodeIndex == -1) {
						Debug.LogError ("Graph contains destroyed nodes. This is a bug.");
					}
					return true;
				});
			}
			
			{
			
				// Get the file containing the list of all node indices
				// This is correlated with the new indices of the nodes and a mapping from old to new
				// is done so that references can be resolved
				ZipEntry entry = zip["graph_references"+binaryExt];
				if (entry == null) throw new System.Exception ("Node references not found in the data. Was this loaded from an older version of the A* Pathfinding Project?");
				
				MemoryStream str = new MemoryStream();
				entry.Extract (str);
				str.Seek (0, SeekOrigin.Begin);
				
				BinaryReader reader = new BinaryReader (str);
				
				int count = reader.ReadInt32();
				GraphNode[] int2Node = new GraphNode[count+1];
				
				try {
					for (int i=0;i<graphs.Length;i++) {
						if (graphs[i] == null) continue;
						graphs[i].GetNodes (delegate (GraphNode node) {
							int2Node[reader.ReadInt32()] = node;
							return true;
						});
					}
				} catch (System.Exception e) {
					throw new System.Exception ("Some graph(s) has thrown an exception during GetNodes, or some graph(s) have deserialized more or fewer nodes than were serialized", e);
				}

				reader.Close ();
				
				// Deserialize node references
				for (int i=0;i<graphs.Length;i++) {
					if (graphs[i] == null) continue;
					
					entry = zip["graph"+i+"_references"+binaryExt];
					if (entry == null) throw new System.Exception ("Node references for graph " +i + " not found in the data. Was this loaded from an older version of the A* Pathfinding Project?");
					
					str = new MemoryStream();
					entry.Extract (str);
					str.Seek (0, SeekOrigin.Begin);
					
					reader = new BinaryReader (str);
					
					
					GraphSerializationContext ctx = new GraphSerializationContext(reader, int2Node, i);
					
					graphs[i].GetNodes (delegate (GraphNode node) {
						node.DeserializeReferences (ctx);
						return true;
					});
				}
			}
		}
		
		public void PostDeserialization () {
			for (int i=0;i<graphs.Length;i++) {
				if (graphs[i] == null) continue;
				
				graphs[i].PostDeserialization();
			}
		}
		
		/** Deserializes nodes for a specified graph */
		private void DeserializeNodes (int index, BinaryReader reader) {
		}
		
		/** Deserializes node connections for a specified graph
		 */
		private void DeserializeNodeConnections (int index, BinaryReader reader) {
		}
		
//#if UNITY_EDITOR
		/** Deserializes graph editor settings.
		 * For future compatibility this method does not assume that the \a graphEditors array matches the #graphs array in order and/or count.
		 * It searches for a matching graph (matching if graphEditor.target == graph) for every graph editor.
		 * Multiple graph editors should not refer to the same graph.\n
		 * \note Stored in files named "graph#_editor.json" where # is the graph number.
		 */
		public void DeserializeEditorSettings (GraphEditorBase[] graphEditors) {
			if (graphEditors == null) return;
			
			for (int i=0;i<graphEditors.Length;i++) {
				if (graphEditors[i] == null) continue;
				for (int j=0;j<graphs.Length;j++) {
					if (graphs[j] == null || graphEditors[i].target != graphs[j]) continue;
					
					ZipEntry entry = zip["graph"+j+"_editor"+jsonExt];
					if (entry == null) continue;
					
					string entryText = GetString (entry);
					
					JsonReader reader = new JsonReader(entryText,readerSettings);
					GraphEditorBase graphEditor = graphEditors[i];
					reader.PopulateObject (ref graphEditor);
					graphEditors[i] = graphEditor;
					break;
				}
			}
		}
//#endif

		private string GetString (ZipEntry entry) {
			MemoryStream buffer = new MemoryStream();
			entry.Extract(buffer);
			buffer.Position = 0;
			StreamReader reader = new StreamReader(buffer);
			string s = reader.ReadToEnd();
			buffer.Position = 0;
			reader.Dispose();
			return s;
		}
		
		private GraphMeta DeserializeMeta (ZipEntry entry) {
			if ( entry == null ) throw new System.Exception ("No metadata found in serialized data.");

			string s = GetString (entry);
			
			JsonReader reader = new JsonReader(s,readerSettings);
			return (GraphMeta)reader.Deserialize(typeof(GraphMeta));
			 //JsonConvert.DeserializeObject<GraphMeta>(s,settings);
		}
		
		
#endregion
		
#region Utils
		
		public static void SaveToFile (string path, byte[] data) {
			using (FileStream stream = new FileStream(path, FileMode.Create)) {
				stream.Write (data,0,data.Length);
			}
		}
		
		public static byte[] LoadFromFile (string path) {
			using (FileStream stream = new FileStream(path, FileMode.Open)) {
				byte[] bytes = new byte[(int)stream.Length];
				stream.Read (bytes,0,(int)stream.Length);
				return bytes;
			}
		}
		
#endregion		
	}
	
	/** Metadata for all graphs included in serialization */
	class GraphMeta {
		/** Project version it was saved with */
		public Version version;
		
		/** Number of graphs serialized */
		public int graphs;
		
		/** Guids for all graphs */
		public string[] guids;
		
		/** Type names for all graphs */
		public string[] typeNames;
		
		/** Number of nodes for every graph. Nodes are not necessarily serialized */
		public int[] nodeCounts;
		
		/** Returns the Type of graph number \a i */
		public Type GetGraphType (int i) {
			
			//The graph was null when saving. Ignore it
			if (typeNames[i] == null) return null;
			
#if ASTAR_FAST_NO_EXCEPTIONS
			System.Type[] types = AstarData.DefaultGraphTypes;
			
			Type type = null;
			for (int j=0;j<types.Length;j++) {
				if (types[j].FullName == typeNames[i]) type = types[j];
			}
#else
			Type type = Type.GetType (typeNames[i]);
#endif
			if (!System.Type.Equals (type, null))
				return type;
			else
				throw new Exception ("No graph of type '" + typeNames [i] + "' could be created, type does not exist");
		}
	}
	
	/** Holds settings for how graphs should be serialized */
	public class SerializeSettings {
		/** Is node data to be included in serialization */
		public bool nodes = true;
		public bool prettyPrint = false;
		
		/** Save editor settings. \warning Only applicable when saving from the editor using the AstarPathEditor methods */
		public bool editorSettings = false;
		
		/** Returns serialization settings for only saving graph settings */
		public static SerializeSettings Settings {
			get {
				SerializeSettings s = new SerializeSettings();
				s.nodes = false;
				return s;
			}
		}
		
		/** Returns serialization settings for saving everything the can be saved.
		 * This included all node data */
		public static SerializeSettings All {
			get {
				SerializeSettings s = new SerializeSettings();
				s.nodes = true;
				return s;
			}
		}
	}
	
}