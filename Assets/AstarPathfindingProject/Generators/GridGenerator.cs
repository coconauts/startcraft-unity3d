using System.Collections.Generic;
using Math = System.Math;
using UnityEngine;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace Pathfinding {
	using Pathfinding.Serialization;
	using Pathfinding.Util;

	/// <summary>
	/// Generates a grid of nodes.
	/// The GridGraph does exactly what the name implies, generates nodes in a grid pattern.\n
	/// Grid graphs suit well to when you already have a grid based world.
	/// Features:
	/// - You can update the graph during runtime (good for e.g Tower Defence or RTS games)
	/// - Throw any scene at it, with minimal configurations you can get a good graph from it.
	/// - Supports raycast and the funnel algorithm
	/// - Predictable pattern
	/// - Can apply penalty and walkability values from a supplied image
	/// - Perfect for terrain worlds since it can make areas unwalkable depending on the slope
	///
	/// [Open online documentation to see images]
	/// [Open online documentation to see images]
	///
	/// The <b>The Snap Size</b> button snaps the internal size of the graph to exactly contain the current number of nodes, i.e not contain 100.3 nodes but exactly 100 nodes.\n
	/// This will make the "center" coordinate more accurate.\n
	///
	/// <b>Updating the graph during runtime</b>\n
	/// Any graph which implements the IUpdatableGraph interface can be updated during runtime.\n
	/// For grid graphs this is a great feature since you can update only a small part of the grid without causing any lag like a complete rescan would.\n
	/// If you for example just have instantiated a sphere obstacle in the scene and you want to update the grid where that sphere was instantiated, you can do this:\n
	/// <code> AstarPath.active.UpdateGraphs (ob.collider.bounds); </code>
	/// Where ob is the obstacle you just instantiated (a GameObject).\n
	/// As you can see, the UpdateGraphs function takes a Bounds parameter and it will send an update call to all updateable graphs.\n
	/// A grid graph will update that area and a small margin around it equal to \link Pathfinding.GraphCollision.diameter collision testing diameter/2 \endlink
	/// See: graph-updates (view in online documentation for working links) for more info about updating graphs during runtime
	///
	/// <b>Hexagon graphs</b>\n
	/// The graph can be configured to work like a hexagon graph with some simple settings. Since 4.1.x the grid graph has a 'Shape' dropdown.
	/// If you set it to 'Hexagonal' the graph will behave as a hexagon graph.
	/// Often you may want to rotate the graph +45 or -45 degrees.
	/// [Open online documentation to see images]
	///
	/// Note however that the snapping to the closest node is not exactly as you would expect in a real hexagon graph,
	/// but it is close enough that you will likely not notice.
	///
	/// <b>Configure using code</b>\n
	/// <code>
	/// // This holds all graph data
	/// AstarData data = AstarPath.active.data;
	///
	/// // This creates a Grid Graph
	/// GridGraph gg = data.AddGraph(typeof(GridGraph)) as GridGraph;
	///
	/// // Setup a grid graph with some values
	/// int width = 50;
	/// int depth = 50;
	/// float nodeSize = 1;
	///
	/// gg.center = new Vector3(10, 0, 0);
	///
	/// // Updates internal size from the above values
	/// gg.SetDimensions(width, depth, nodeSize);
	///
	/// // Scans all graphs
	/// AstarPath.active.Scan();
	/// </code>
	///
	/// \ingroup graphs
	/// \nosubgrouping
	///
	/// <b>Tree colliders</b>\n
	/// It seems that Unity will only generate tree colliders at runtime when the game is started.
	/// For this reason, the grid graph will not pick up tree colliders when outside of play mode
	/// but it will pick them up once the game starts. If it still does not pick them up
	/// make sure that the trees actually have colliders attached to them and that the tree prefabs are
	/// in the correct layer (the layer should be included in the 'Collision Testing' mask).
	///
	/// See: <see cref="Pathfinding.GraphCollision"/> for documentation on the 'Height Testing' and 'Collision Testing' sections
	/// of the grid graph settings.
	/// </summary>
	[JsonOptIn]
	[Pathfinding.Util.Preserve]
	public class GridGraph : NavGraph, IUpdatableGraph, ITransformedGraph {
		/// <summary>This function will be called when this graph is destroyed</summary>
		protected override void OnDestroy () {
			base.OnDestroy();

			// Clean up a reference in a static variable which otherwise should point to this graph forever and stop the GC from collecting it
			RemoveGridGraphFromStatic();
		}

		protected override void DestroyAllNodes () {
			GetNodes(node => {
				// If the grid data happens to be invalid (e.g we had to abort a graph update while it was running) using 'false' as
				// the parameter will prevent the Destroy method from potentially throwing IndexOutOfRange exceptions due to trying
				// to access nodes outside the graph. It is safe to do this because we are destroying all nodes in the graph anyway.
				// We do however need to clear custom connections in both directions
				(node as GridNodeBase).ClearCustomConnections(true);
				node.ClearConnections(false);
				node.Destroy();
			});
		}

		void RemoveGridGraphFromStatic () {
			GridNode.SetGridGraph(AstarPath.active.data.GetGraphIndex(this), null);
		}

		/// <summary>
		/// This is placed here so generators inheriting from this one can override it and set it to false.
		/// If it is true, it means that the nodes array's length will always be equal to width*depth
		/// It is used mainly in the editor to do auto-scanning calls, setting it to false for a non-uniform grid will reduce the number of scans
		/// </summary>
		public virtual bool uniformWidthDepthGrid {
			get {
				return true;
			}
		}

		/// <summary>
		/// Number of layers in the graph.
		/// For grid graphs this is always 1, for layered grid graphs it can be higher.
		/// The nodes array has the size width*depth*layerCount.
		/// </summary>
		public virtual int LayerCount {
			get {
				return 1;
			}
		}

		public override int CountNodes () {
			return nodes != null ? nodes.Length : 0;
		}

		public override void GetNodes (System.Action<GraphNode> action) {
			if (nodes == null) return;
			for (int i = 0; i < nodes.Length; i++) action(nodes[i]);
		}

		/// <summary>
		/// \name Inspector - Settings
		/// \{
		/// </summary>

		/// <summary>
		/// Determines the layout of the grid graph inspector in the Unity Editor.
		/// This field is only used in the editor, it has no effect on the rest of the game whatsoever.
		/// </summary>
		[JsonMember]
		public InspectorGridMode inspectorGridMode = InspectorGridMode.Grid;

		/// <summary>
		/// Determines how the size of each hexagon is set in the inspector.
		/// For hexagons the normal nodeSize field doesn't really correspond to anything specific on the hexagon's geometry, so this enum is used to give the user the opportunity to adjust more concrete dimensions of the hexagons
		/// without having to pull out a calculator to calculate all the square roots and complicated conversion factors.
		///
		/// This field is only used in the graph inspector, the <see cref="nodeSize"/> field will always use the same internal units.
		/// If you want to set the node size through code then you can use <see cref="ConvertHexagonSizeToNodeSize"/>.
		///
		/// [Open online documentation to see images]
		///
		/// See: <see cref="InspectorGridHexagonNodeSize"/>
		/// See: <see cref="ConvertHexagonSizeToNodeSize"/>
		/// See: <see cref="ConvertNodeSizeToHexagonSize"/>
		/// </summary>
		[JsonMember]
		public InspectorGridHexagonNodeSize inspectorHexagonSizeMode = InspectorGridHexagonNodeSize.Width;

		/// <summary>Width of the grid in nodes. See: SetDimensions</summary>
		public int width;

		/// <summary>Depth (height) of the grid in nodes. See: SetDimensions</summary>
		public int depth;

		/// <summary>
		/// Scaling of the graph along the X axis.
		/// This should be used if you want different scales on the X and Y axis of the grid
		/// </summary>
		[JsonMember]
		public float aspectRatio = 1F;

		/// <summary>
		/// Angle to use for the isometric projection.
		/// If you are making a 2D isometric game, you may want to use this parameter to adjust the layout of the graph to match your game.
		/// This will essentially scale the graph along one of its diagonals to produce something like this:
		///
		/// A perspective view of an isometric graph.
		/// [Open online documentation to see images]
		///
		/// A top down view of an isometric graph. Note that the graph is entirely 2D, there is no perspective in this image.
		/// [Open online documentation to see images]
		///
		/// Usually the angle that you want to use is either 30 degrees (alternatively 90-30 = 60 degrees) or atan(1/sqrt(2)) which is approximately 35.264 degrees (alternatively 90 - 35.264 = 54.736 degrees).
		/// You might also want to rotate the graph plus or minus 45 degrees around the Y axis to get the oritientation required for your game.
		///
		/// You can read more about it on the wikipedia page linked below.
		///
		/// See: http://en.wikipedia.org/wiki/Isometric_projection
		/// See: https://en.wikipedia.org/wiki/Isometric_graphics_in_video_games_and_pixel_art
		/// See: rotation
		/// </summary>
		[JsonMember]
		public float isometricAngle;

		/// <summary>
		/// If true, all edge costs will be set to the same value.
		/// If false, diagonals will cost more.
		/// This is useful for a hexagon graph where the diagonals are actually the same length as the
		/// normal edges (since the graph has been skewed)
		/// </summary>
		[JsonMember]
		public bool uniformEdgeCosts;

		/// <summary>Rotation of the grid in degrees</summary>
		[JsonMember]
		public Vector3 rotation;

		/// <summary>Center point of the grid</summary>
		[JsonMember]
		public Vector3 center;

		/// <summary>Size of the grid. Might be negative or smaller than <see cref="nodeSize"/></summary>
		[JsonMember]
		public Vector2 unclampedSize;

		/// <summary>
		/// Size of one node in world units.
		/// See: <see cref="SetDimensions"/>
		/// </summary>
		[JsonMember]
		public float nodeSize = 1;

		/* Collision and stuff */

		/// <summary>Settings on how to check for walkability and height</summary>
		[JsonMember]
		public GraphCollision collision;

		/// <summary>
		/// The max position difference between two nodes to enable a connection.
		/// Set to 0 to ignore the value.
		/// </summary>
		[JsonMember]
		public float maxClimb = 0.4F;

		/// <summary>The max slope in degrees for a node to be walkable.</summary>
		[JsonMember]
		public float maxSlope = 90;

		/// <summary>
		/// Use heigh raycasting normal for max slope calculation.
		/// True if <see cref="maxSlope"/> is less than 90 degrees.
		/// </summary>
		protected bool useRaycastNormal { get { return Math.Abs(90-maxSlope) > float.Epsilon; } }

		/// <summary>
		/// Erosion of the graph.
		/// The graph can be eroded after calculation.
		/// This means a margin is put around unwalkable nodes or other unwalkable connections.
		/// It is really good if your graph contains ledges where the nodes without erosion are walkable too close to the edge.
		///
		/// Below is an image showing a graph with erode iterations 0, 1 and 2
		/// [Open online documentation to see images]
		///
		/// Note: A high number of erode iterations can seriously slow down graph updates during runtime (GraphUpdateObject)
		/// and should be kept as low as possible.
		/// See: erosionUseTags
		/// </summary>
		[JsonMember]
		public int erodeIterations;

		/// <summary>
		/// Use tags instead of walkability for erosion.
		/// Tags will be used for erosion instead of marking nodes as unwalkable. The nodes will be marked with tags in an increasing order starting with the tag <see cref="erosionFirstTag"/>.
		/// Debug with the Tags mode to see the effect. With this enabled you can in effect set how close different AIs are allowed to get to walls using the Valid Tags field on the Seeker component.
		/// [Open online documentation to see images]
		/// [Open online documentation to see images]
		/// See: erosionFirstTag
		/// </summary>
		[JsonMember]
		public bool erosionUseTags;

		/// <summary>
		/// Tag to start from when using tags for erosion.
		/// See: <see cref="erosionUseTags"/>
		/// See: <see cref="erodeIterations"/>
		/// </summary>
		[JsonMember]
		public int erosionFirstTag = 1;


		/// <summary>
		/// Number of neighbours for each node.
		/// Either four, six, eight connections per node.
		///
		/// Six connections is primarily for emulating hexagon graphs.
		/// </summary>
		[JsonMember]
		public NumNeighbours neighbours = NumNeighbours.Eight;

		/// <summary>
		/// If disabled, will not cut corners on obstacles.
		/// If \link <see cref="neighbours"/> connections \endlink is Eight, obstacle corners might be cut by a connection,
		/// setting this to false disables that. \image html images/cutCorners.png
		/// </summary>
		[JsonMember]
		public bool cutCorners = true;

		/// <summary>
		/// Offset for the position when calculating penalty.
		/// See: penaltyPosition
		/// </summary>
		[JsonMember]
		public float penaltyPositionOffset;

		/// <summary>Use position (y-coordinate) to calculate penalty</summary>
		[JsonMember]
		public bool penaltyPosition;

		/// <summary>
		/// Scale factor for penalty when calculating from position.
		/// See: penaltyPosition
		/// </summary>
		[JsonMember]
		public float penaltyPositionFactor = 1F;

		[JsonMember]
		public bool penaltyAngle;

		/// <summary>
		/// How much penalty is applied depending on the slope of the terrain.
		/// At a 90 degree slope (not that exactly 90 degree slopes can occur, but almost 90 degree), this penalty is applied.
		/// At a 45 degree slope, half of this is applied and so on.
		/// Note that you may require very large values, a value of 1000 is equivalent to the cost of moving 1 world unit.
		/// </summary>
		[JsonMember]
		public float penaltyAngleFactor = 100F;

		/// <summary>How much extra to penalize very steep angles</summary>
		[JsonMember]
		public float penaltyAnglePower = 1;


		/// <summary>Show an outline of the grid nodes in the Unity Editor</summary>
		[JsonMember]
		public bool showMeshOutline = true;

		/// <summary>Show the connections between the grid nodes in the Unity Editor</summary>
		[JsonMember]
		public bool showNodeConnections;

		/// <summary>Show the surface of the graph. Each node will be drawn as a square (unless e.g hexagon graph mode has been enabled).</summary>
		[JsonMember]
		public bool showMeshSurface = true;


		/// <summary>\}</summary>

		/// <summary>
		/// Size of the grid. Will always be positive and larger than <see cref="nodeSize"/>.
		/// See: <see cref="UpdateTransform"/>
		/// </summary>
		public Vector2 size { get; protected set; }

		/* End collision and stuff */

		/// <summary>
		/// Index offset to get neighbour nodes. Added to a node's index to get a neighbour node index.
		///
		/// <code>
		///         Z
		///         |
		///         |
		///
		///      6  2  5
		///       \ | /
		/// --  3 - X - 1  ----- X
		///       / | \
		///      7  0  4
		///
		///         |
		///         |
		/// </code>
		/// </summary>
		[System.NonSerialized]
		public readonly int[] neighbourOffsets = new int[8];

		/// <summary>Costs to neighbour nodes</summary>
		[System.NonSerialized]
		public readonly uint[] neighbourCosts = new uint[8];

		/// <summary>Offsets in the X direction for neighbour nodes. Only 1, 0 or -1</summary>
		[System.NonSerialized]
		public readonly int[] neighbourXOffsets = new int[8];

		/// <summary>Offsets in the Z direction for neighbour nodes. Only 1, 0 or -1</summary>
		[System.NonSerialized]
		public readonly int[] neighbourZOffsets = new int[8];

		/// <summary>Which neighbours are going to be used when <see cref="neighbours"/>=6</summary>
		internal static readonly int[] hexagonNeighbourIndices = { 0, 1, 5, 2, 3, 7 };

		/// <summary>In GetNearestForce, determines how far to search after a valid node has been found</summary>
		public const int getNearestForceOverlap = 2;

		/// <summary>
		/// All nodes in this graph.
		/// Nodes are laid out row by row.
		///
		/// The first node has grid coordinates X=0, Z=0, the second one X=1, Z=0\n
		/// the last one has grid coordinates X=width-1, Z=depth-1.
		///
		/// <code>
		/// var gg = AstarPath.active.data.gridGraph;
		/// int x = 5;
		/// int z = 8;
		/// GridNode node = gg.nodes[z*gg.width + x];
		/// </code>
		///
		/// See: <see cref="GetNode"/>
		/// See: <see cref="GetNodes"/>
		/// </summary>
		public GridNode[] nodes;

		/// <summary>
		/// Determines how the graph transforms graph space to world space.
		/// See: <see cref="UpdateTransform"/>
		/// </summary>
		public GraphTransform transform { get; private set; }


		public GridGraph () {
			unclampedSize = new Vector2(10, 10);
			nodeSize = 1F;
			collision = new GraphCollision();
			transform = new GraphTransform(Matrix4x4.identity);
		}

		public override void RelocateNodes (Matrix4x4 deltaMatrix) {
			// It just makes a lot more sense to use the other overload and for that case we don't have to serialize the matrix
			throw new System.Exception("This method cannot be used for Grid Graphs. Please use the other overload of RelocateNodes instead");
		}

		/// <summary>
		/// Relocate the grid graph using new settings.
		/// This will move all nodes in the graph to new positions which matches the new settings.
		/// </summary>
		public void RelocateNodes (Vector3 center, Quaternion rotation, float nodeSize, float aspectRatio = 1, float isometricAngle = 0) {
			var previousTransform = transform;

			this.center = center;
			this.rotation = rotation.eulerAngles;
			this.aspectRatio = aspectRatio;
			this.isometricAngle = isometricAngle;

			SetDimensions(width, depth, nodeSize);

			GetNodes(node => {
				var gnode = node as GridNodeBase;
				var height = previousTransform.InverseTransform((Vector3)node.position).y;
				node.position = GraphPointToWorld(gnode.XCoordinateInGrid, gnode.ZCoordinateInGrid, height);
			});
		}

		/// <summary>
		/// Transform a point in graph space to world space.
		/// This will give you the node position for the node at the given x and z coordinate
		/// if it is at the specified height above the base of the graph.
		/// </summary>
		public Int3 GraphPointToWorld (int x, int z, float height) {
			return (Int3)transform.Transform(new Vector3(x+0.5f, height, z+0.5f));
		}

		public static float ConvertHexagonSizeToNodeSize (InspectorGridHexagonNodeSize mode, float value) {
			if (mode == InspectorGridHexagonNodeSize.Diameter) value *= 1.5f/(float)System.Math.Sqrt(2.0f);
			else if (mode == InspectorGridHexagonNodeSize.Width) value *= (float)System.Math.Sqrt(3.0f/2.0f);
			return value;
		}

		public static float ConvertNodeSizeToHexagonSize (InspectorGridHexagonNodeSize mode, float value) {
			if (mode == InspectorGridHexagonNodeSize.Diameter) value *= (float)System.Math.Sqrt(2.0f)/1.5f;
			else if (mode == InspectorGridHexagonNodeSize.Width) value *= (float)System.Math.Sqrt(2.0f/3.0f);
			return value;
		}

		public int Width {
			get {
				return width;
			}
			set {
				width = value;
			}
		}
		public int Depth {
			get {
				return depth;
			}
			set {
				depth = value;
			}
		}

		public uint GetConnectionCost (int dir) {
			return neighbourCosts[dir];
		}

		public GridNode GetNodeConnection (GridNode node, int dir) {
			if (!node.HasConnectionInDirection(dir)) return null;
			if (!node.EdgeNode) {
				return nodes[node.NodeInGridIndex + neighbourOffsets[dir]];
			} else {
				int index = node.NodeInGridIndex;
				//int z = Math.DivRem (index,Width, out x);
				int z = index/Width;
				int x = index - z*Width;

				return GetNodeConnection(index, x, z, dir);
			}
		}

		public bool HasNodeConnection (GridNode node, int dir) {
			if (!node.HasConnectionInDirection(dir)) return false;
			if (!node.EdgeNode) {
				return true;
			} else {
				int index = node.NodeInGridIndex;
				int z = index/Width;
				int x = index - z*Width;

				return HasNodeConnection(index, x, z, dir);
			}
		}

		public void SetNodeConnection (GridNode node, int dir, bool value) {
			int index = node.NodeInGridIndex;
			int z = index/Width;
			int x = index - z*Width;

			SetNodeConnection(index, x, z, dir, value);
		}

		/// <summary>
		/// Get the connecting node from the node at (x,z) in the specified direction.
		/// Returns: A GridNode if the node has a connection to that node. Null if no connection in that direction exists
		///
		/// See: GridNode
		/// </summary>
		private GridNode GetNodeConnection (int index, int x, int z, int dir) {
			if (!nodes[index].HasConnectionInDirection(dir)) return null;

			/// <summary>TODO: Mark edge nodes and only do bounds checking for them</summary>
			int nx = x + neighbourXOffsets[dir];
			if (nx < 0 || nx >= Width) return null; /// <summary>TODO: Modify to get adjacent grid graph here</summary>
			int nz = z + neighbourZOffsets[dir];
			if (nz < 0 || nz >= Depth) return null;
			int nindex = index + neighbourOffsets[dir];

			return nodes[nindex];
		}

		/// <summary>
		/// Set if connection in the specified direction should be enabled.
		/// Note that bounds checking will still be done when getting the connection value again,
		/// so it is not necessarily true that HasNodeConnection will return true just because you used
		/// SetNodeConnection on a node to set a connection to true.
		///
		/// Note: This is identical to Pathfinding.Node.SetConnectionInternal
		///
		/// Deprecated:
		/// </summary>
		/// <param name="index">Index of the node</param>
		/// <param name="x">X coordinate of the node</param>
		/// <param name="z">Z coordinate of the node</param>
		/// <param name="dir">Direction from 0 up to but excluding 8.</param>
		/// <param name="value">Enable or disable the connection</param>
		public void SetNodeConnection (int index, int x, int z, int dir, bool value) {
			nodes[index].SetConnectionInternal(dir, value);
		}

		public bool HasNodeConnection (int index, int x, int z, int dir) {
			if (!nodes[index].HasConnectionInDirection(dir)) return false;

			/// <summary>TODO: Mark edge nodes and only do bounds checking for them</summary>
			int nx = x + neighbourXOffsets[dir];
			if (nx < 0 || nx >= Width) return false; /// <summary>TODO: Modify to get adjacent grid graph here</summary>
			int nz = z + neighbourZOffsets[dir];
			if (nz < 0 || nz >= Depth) return false;

			return true;
		}

		/// <summary>
		/// Updates <see cref="unclampedSize"/> from <see cref="width"/>, <see cref="depth"/> and <see cref="nodeSize"/> values.
		/// Also \link UpdateTransform generates a new matrix \endlink.
		/// Note: This does not rescan the graph, that must be done with Scan
		///
		/// You should use this method instead of setting the <see cref="width"/> and <see cref="depth"/> fields
		/// as the grid dimensions are not defined by the <see cref="width"/> and <see cref="depth"/> variables but by
		/// the <see cref="unclampedSize"/> and <see cref="center"/> variables.
		///
		/// <code>
		/// var gg = AstarPath.active.data.gridGraph;
		/// var width = 80;
		/// var depth = 60;
		/// var nodeSize = 1.0f;
		///
		/// gg.SetDimensions(width, depth, nodeSize);
		///
		/// // Recalculate the graph
		/// AstarPath.active.Scan();
		/// </code>
		/// </summary>
		public void SetDimensions (int width, int depth, float nodeSize) {
			unclampedSize = new Vector2(width, depth)*nodeSize;
			this.nodeSize = nodeSize;
			UpdateTransform();
		}

		/// <summary>Updates <see cref="unclampedSize"/> from <see cref="width"/>, <see cref="depth"/> and <see cref="nodeSize"/> values. Deprecated: Use <see cref="SetDimensions"/> instead</summary>
		[System.Obsolete("Use SetDimensions instead")]
		public void UpdateSizeFromWidthDepth () {
			SetDimensions(width, depth, nodeSize);
		}

		/// <summary>
		/// Generates the matrix used for translating nodes from grid coordinates to world coordinates.
		/// Deprecated: This method has been renamed to <see cref="UpdateTransform"/>
		/// </summary>
		[System.Obsolete("This method has been renamed to UpdateTransform")]
		public void GenerateMatrix () {
			UpdateTransform();
		}

		/// <summary>
		/// Updates the <see cref="transform"/> field which transforms graph space to world space.
		/// In graph space all nodes are laid out in the XZ plane with the first node having a corner in the origin.
		/// One unit in graph space is one node so the first node in the graph is at (0.5,0) the second one at (1.5,0) etc.
		///
		/// This takes the current values of the parameters such as position and rotation into account.
		/// The transform that was used the last time the graph was scanned is stored in the <see cref="transform"/> field.
		///
		/// The <see cref="transform"/> field is calculated using this method when the graph is scanned.
		/// The width, depth variables are also updated based on the <see cref="unclampedSize"/> field.
		/// </summary>
		public void UpdateTransform () {
			CalculateDimensions(out width, out depth, out nodeSize);
			transform = CalculateTransform();
		}

		/// <summary>
		/// Returns a new transform which transforms graph space to world space.
		/// Does not update the <see cref="transform"/> field.
		/// See: <see cref="UpdateTransform"/>
		/// </summary>
		public GraphTransform CalculateTransform () {
			int newWidth, newDepth;
			float newNodeSize;

			CalculateDimensions(out newWidth, out newDepth, out newNodeSize);

			// Generate a matrix which shrinks the graph along one of the diagonals
			// corresponding to the isometricAngle
			var isometricMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 45, 0), Vector3.one);
			isometricMatrix = Matrix4x4.Scale(new Vector3(Mathf.Cos(Mathf.Deg2Rad*isometricAngle), 1, 1)) * isometricMatrix;
			isometricMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, -45, 0), Vector3.one) * isometricMatrix;

			// Generate a matrix for the bounds of the graph
			// This moves a point to the correct offset in the world and the correct rotation and the aspect ratio and isometric angle is taken into account
			// The unit is still world units however
			var boundsMatrix = Matrix4x4.TRS(center, Quaternion.Euler(rotation), new Vector3(aspectRatio, 1, 1)) * isometricMatrix;

			// Generate a matrix where Vector3.zero is the corner of the graph instead of the center
			// The unit is nodes here (so (0.5,0,0.5) is the position of the first node and (1.5,0,0.5) is the position of the second node)
			// 0.5 is added since this is the node center, not its corner. In graph space a node has a size of 1
			var m = Matrix4x4.TRS(boundsMatrix.MultiplyPoint3x4(-new Vector3(newWidth*newNodeSize, 0, newDepth*newNodeSize)*0.5F), Quaternion.Euler(rotation), new Vector3(newNodeSize*aspectRatio, 1, newNodeSize)) * isometricMatrix;

			// Set the matrix of the graph
			// This will also set inverseMatrix
			return new GraphTransform(m);
		}

		/// <summary>
		/// Calculates the width/depth of the graph from <see cref="unclampedSize"/> and <see cref="nodeSize"/>.
		/// The node size may be changed due to constraints that the width/depth is not
		/// allowed to be larger than 1024 (artificial limit).
		/// </summary>
		void CalculateDimensions (out int width, out int depth, out float nodeSize) {
			var newSize = unclampedSize;

			// Make sure size is positive
			newSize.x *= Mathf.Sign(newSize.x);
			newSize.y *= Mathf.Sign(newSize.y);

			// Clamp the nodeSize so that the graph is never larger than 1024*1024
			nodeSize = Mathf.Max(this.nodeSize, newSize.x/1024F);
			nodeSize = Mathf.Max(this.nodeSize, newSize.y/1024F);

			// Prevent the graph to become smaller than a single node
			newSize.x = newSize.x < nodeSize ? nodeSize : newSize.x;
			newSize.y = newSize.y < nodeSize ? nodeSize : newSize.y;

			size = newSize;

			// Calculate the number of nodes along each side
			width = Mathf.FloorToInt(size.x / nodeSize);
			depth = Mathf.FloorToInt(size.y / nodeSize);

			// Take care of numerical edge cases
			if (Mathf.Approximately(size.x / nodeSize, Mathf.CeilToInt(size.x / nodeSize))) {
				width = Mathf.CeilToInt(size.x / nodeSize);
			}

			if (Mathf.Approximately(size.y / nodeSize, Mathf.CeilToInt(size.y / nodeSize))) {
				depth = Mathf.CeilToInt(size.y / nodeSize);
			}
		}

		public override NNInfoInternal GetNearest (Vector3 position, NNConstraint constraint, GraphNode hint) {
			if (nodes == null || depth*width != nodes.Length) {
				return new NNInfoInternal();
			}

			// Calculate the closest node and the closest point on that node
			position = transform.InverseTransform(position);

			float xf = position.x;
			float zf = position.z;
			int x = Mathf.Clamp((int)xf, 0, width-1);
			int z = Mathf.Clamp((int)zf, 0, depth-1);

			var nn = new NNInfoInternal(nodes[z*width+x]);

			float y = transform.InverseTransform((Vector3)nodes[z*width+x].position).y;
			nn.clampedPosition = transform.Transform(new Vector3(Mathf.Clamp(xf, x, x+1f), y, Mathf.Clamp(zf, z, z+1f)));

			return nn;
		}

		public override NNInfoInternal GetNearestForce (Vector3 position, NNConstraint constraint) {
			if (nodes == null || depth*width != nodes.Length) {
				return new NNInfoInternal();
			}

			// Position in global space
			Vector3 globalPosition = position;

			// Position in graph space
			position = transform.InverseTransform(position);

			// Find the coordinates of the closest node
			float xf = position.x;
			float zf = position.z;
			int x = Mathf.Clamp((int)xf, 0, width-1);
			int z = Mathf.Clamp((int)zf, 0, depth-1);

			// Closest node
			GridNode node = nodes[x+z*width];

			GridNode minNode = null;
			float minDist = float.PositiveInfinity;
			int overlap = getNearestForceOverlap;

			Vector3 clampedPosition = Vector3.zero;
			var nn = new NNInfoInternal(null);

			// If the closest node was suitable
			if (constraint == null || constraint.Suitable(node)) {
				minNode = node;
				minDist = ((Vector3)minNode.position-globalPosition).sqrMagnitude;
				float y = transform.InverseTransform((Vector3)node.position).y;
				clampedPosition = transform.Transform(new Vector3(Mathf.Clamp(xf, x, x+1f), y, Mathf.Clamp(zf, z, z+1f)));
			}

			if (minNode != null) {
				nn.node = minNode;
				nn.clampedPosition = clampedPosition;

				// We have a node, and we don't need to search more, so just return
				if (overlap == 0) return nn;
				overlap--;
			}

			// Search up to this distance
			float maxDist = constraint == null || constraint.constrainDistance ? AstarPath.active.maxNearestNodeDistance : float.PositiveInfinity;
			float maxDistSqr = maxDist*maxDist;

			// Search a square/spiral pattern around the point
			for (int w = 1;; w++) {
				//Check if the nodes are within distance limit
				if (nodeSize*w > maxDist) {
					break;
				}

				bool anyInside = false;

				int nx;
				int nz = z+w;
				int nz2 = nz*width;

				// Side 1 on the square
				for (nx = x-w; nx <= x+w; nx++) {
					if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
					anyInside = true;
					if (constraint == null || constraint.Suitable(nodes[nx+nz2])) {
						float dist = ((Vector3)nodes[nx+nz2].position-globalPosition).sqrMagnitude;
						if (dist < minDist && dist < maxDistSqr) {
							// Minimum distance so far
							minDist = dist;
							minNode = nodes[nx+nz2];

							// Closest point on the node if the node is treated as a square
							clampedPosition = transform.Transform(new Vector3(Mathf.Clamp(xf, nx, nx+1f), transform.InverseTransform((Vector3)minNode.position).y, Mathf.Clamp(zf, nz, nz+1f)));
						}
					}
				}

				nz = z-w;
				nz2 = nz*width;

				// Side 2 on the square
				for (nx = x-w; nx <= x+w; nx++) {
					if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
					anyInside = true;
					if (constraint == null || constraint.Suitable(nodes[nx+nz2])) {
						float dist = ((Vector3)nodes[nx+nz2].position-globalPosition).sqrMagnitude;
						if (dist < minDist && dist < maxDistSqr) {
							minDist = dist;
							minNode = nodes[nx+nz2];
							clampedPosition = transform.Transform(new Vector3(Mathf.Clamp(xf, nx, nx+1f), transform.InverseTransform((Vector3)minNode.position).y, Mathf.Clamp(zf, nz, nz+1f)));
						}
					}
				}

				nx = x-w;

				// Side 3 on the square
				for (nz = z-w+1; nz <= z+w-1; nz++) {
					if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
					anyInside = true;
					if (constraint == null || constraint.Suitable(nodes[nx+nz*width])) {
						float dist = ((Vector3)nodes[nx+nz*width].position-globalPosition).sqrMagnitude;
						if (dist < minDist && dist < maxDistSqr) {
							minDist = dist;
							minNode = nodes[nx+nz*width];
							clampedPosition = transform.Transform(new Vector3(Mathf.Clamp(xf, nx, nx+1f), transform.InverseTransform((Vector3)minNode.position).y, Mathf.Clamp(zf, nz, nz+1f)));
						}
					}
				}

				nx = x+w;

				// Side 4 on the square
				for (nz = z-w+1; nz <= z+w-1; nz++) {
					if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
					anyInside = true;
					if (constraint == null || constraint.Suitable(nodes[nx+nz*width])) {
						float dist = ((Vector3)nodes[nx+nz*width].position-globalPosition).sqrMagnitude;
						if (dist < minDist && dist < maxDistSqr) {
							minDist = dist;
							minNode = nodes[nx+nz*width];
							clampedPosition = transform.Transform(new Vector3(Mathf.Clamp(xf, nx, nx+1f), transform.InverseTransform((Vector3)minNode.position).y, Mathf.Clamp(zf, nz, nz+1f)));
						}
					}
				}

				// We found a suitable node
				if (minNode != null) {
					// If we don't need to search more, just return
					// Otherwise search for 'overlap' iterations more
					if (overlap == 0) {
						break;
					}
					overlap--;
				}

				// No nodes were inside grid bounds
				// We will not be able to find any more valid nodes
				// so just return
				if (!anyInside) {
					break;
				}
			}

			// Copy fields to the NNInfo struct and return
			nn.node = minNode;
			nn.clampedPosition = clampedPosition;
			return nn;
		}

		/// <summary>
		/// Sets up <see cref="neighbourOffsets"/> with the current settings. <see cref="neighbourOffsets"/>, <see cref="neighbourCosts"/>, <see cref="neighbourXOffsets"/> and <see cref="neighbourZOffsets"/> are set up.\n
		/// The cost for a non-diagonal movement between two adjacent nodes is RoundToInt (<see cref="nodeSize"/> * Int3.Precision)\n
		/// The cost for a diagonal movement between two adjacent nodes is RoundToInt (<see cref="nodeSize"/> * Sqrt (2) * Int3.Precision)
		/// </summary>
		public virtual void SetUpOffsetsAndCosts () {
			//First 4 are for the four directly adjacent nodes the last 4 are for the diagonals
			neighbourOffsets[0] = -width;
			neighbourOffsets[1] = 1;
			neighbourOffsets[2] = width;
			neighbourOffsets[3] = -1;
			neighbourOffsets[4] = -width+1;
			neighbourOffsets[5] = width+1;
			neighbourOffsets[6] = width-1;
			neighbourOffsets[7] = -width-1;

			uint straightCost = (uint)Mathf.RoundToInt(nodeSize*Int3.Precision);

			// Diagonals normally cost sqrt(2) (approx 1.41) times more
			uint diagonalCost = uniformEdgeCosts ? straightCost : (uint)Mathf.RoundToInt(nodeSize*Mathf.Sqrt(2F)*Int3.Precision);

			neighbourCosts[0] = straightCost;
			neighbourCosts[1] = straightCost;
			neighbourCosts[2] = straightCost;
			neighbourCosts[3] = straightCost;
			neighbourCosts[4] = diagonalCost;
			neighbourCosts[5] = diagonalCost;
			neighbourCosts[6] = diagonalCost;
			neighbourCosts[7] = diagonalCost;

			/*         Z
			 *         |
			 *         |
			 *
			 *      6  2  5
			 *       \ | /
			 * --  3 - X - 1  ----- X
			 *       / | \
			 *      7  0  4
			 *
			 *         |
			 *         |
			 */

			neighbourXOffsets[0] = 0;
			neighbourXOffsets[1] = 1;
			neighbourXOffsets[2] = 0;
			neighbourXOffsets[3] = -1;
			neighbourXOffsets[4] = 1;
			neighbourXOffsets[5] = 1;
			neighbourXOffsets[6] = -1;
			neighbourXOffsets[7] = -1;

			neighbourZOffsets[0] = -1;
			neighbourZOffsets[1] =  0;
			neighbourZOffsets[2] =  1;
			neighbourZOffsets[3] =  0;
			neighbourZOffsets[4] = -1;
			neighbourZOffsets[5] =  1;
			neighbourZOffsets[6] =  1;
			neighbourZOffsets[7] = -1;
		}

		protected override IEnumerable<Progress> ScanInternal () {
			if (nodeSize <= 0) {
				yield break;
			}

			// Make sure the matrix is up to date
			UpdateTransform();

			if (width > 1024 || depth > 1024) {
				Debug.LogError("One of the grid's sides is longer than 1024 nodes");
				yield break;
			}


			SetUpOffsetsAndCosts();

			// Set a global reference to this graph so that nodes can find it
			GridNode.SetGridGraph((int)graphIndex, this);

			yield return new Progress(0.05f, "Creating nodes");

			// Create all nodes
			nodes = new GridNode[width*depth];
			for (int z = 0; z < depth; z++) {
				for (int x = 0; x < width; x++) {
					var index = z*width+x;
					var node = nodes[index] = new GridNode(active);
					node.GraphIndex = graphIndex;
					node.NodeInGridIndex = index;
				}
			}

			// Create and initialize the collision class
			if (collision == null) {
				collision = new GraphCollision();
			}
			collision.Initialize(transform, nodeSize);


			int progressCounter = 0;

			const int YieldEveryNNodes = 1000;

			for (int z = 0; z < depth; z++) {
				// Yield with a progress value at most every N nodes
				if (progressCounter >= YieldEveryNNodes) {
					progressCounter = 0;
					yield return new Progress(Mathf.Lerp(0.1f, 0.7f, z/(float)depth), "Calculating positions");
				}

				progressCounter += width;

				for (int x = 0; x < width; x++) {
					// Updates the position of the node
					// and a bunch of other things
					RecalculateCell(x, z);
				}
			}

			progressCounter = 0;

			for (int z = 0; z < depth; z++) {
				// Yield with a progress value at most every N nodes
				if (progressCounter >= YieldEveryNNodes) {
					progressCounter = 0;
					yield return new Progress(Mathf.Lerp(0.7f, 0.9f, z/(float)depth), "Calculating connections");
				}

				progressCounter += width;

				for (int x = 0; x < width; x++) {
					// Recalculate connections to other nodes
					CalculateConnections(x, z);
				}
			}

			yield return new Progress(0.95f, "Calculating erosion");

			// Apply erosion
			ErodeWalkableArea();
		}

		/// <summary>
		/// Updates position, walkability and penalty for the node.
		/// Assumes that collision.Initialize (...) has been called before this function
		///
		/// Deprecated: Use RecalculateCell instead which works both for grid graphs and layered grid graphs.
		/// </summary>
		[System.Obsolete("Use RecalculateCell instead which works both for grid graphs and layered grid graphs")]
		public virtual void UpdateNodePositionCollision (GridNode node, int x, int z, bool resetPenalty = true) {
			RecalculateCell(x, z, resetPenalty, false);
		}

		/// <summary>
		/// Recalculates single node in the graph.
		///
		/// For a layered grid graph this will recalculate all nodes at a specific (x,z) cell in the grid.
		/// For grid graphs this will simply recalculate the single node at those coordinates.
		///
		/// Note: This must only be called when it is safe to update nodes.
		///  For example when scanning the graph or during a graph update.
		///
		/// Note: This will not recalculate any connections as this method is often run for several adjacent nodes at a time.
		///  After you have recalculated all the nodes you will have to recalculate the connections for the changed nodes
		///  as well as their neighbours.
		///  See: CalculateConnections
		/// </summary>
		/// <param name="x">X coordinate of the cell</param>
		/// <param name="z">Z coordinate of the cell</param>
		/// <param name="resetPenalties">If true, the penalty of the nodes will be reset to the initial value as if the graph had just been scanned
		///      (this excludes texture data however which is only done when scanning the graph).</param>
		/// <param name="resetTags">If true, the tag will be reset to zero (the default tag).</param>
		public virtual void RecalculateCell (int x, int z, bool resetPenalties = true, bool resetTags = true) {
			var node = nodes[z*width + x];

			// Set the node's initial position with a y-offset of zero
			node.position = GraphPointToWorld(x, z, 0);

			RaycastHit hit;

			bool walkable;

			// Calculate the actual position using physics raycasting (if enabled)
			// walkable will be set to false if no ground was found (unless that setting has been disabled)
			Vector3 position = collision.CheckHeight((Vector3)node.position, out hit, out walkable);
			node.position = (Int3)position;

			if (resetPenalties) {
				node.Penalty = initialPenalty;

				// Calculate a penalty based on the y coordinate of the node
				if (penaltyPosition) {
					node.Penalty += (uint)Mathf.RoundToInt((node.position.y-penaltyPositionOffset)*penaltyPositionFactor);
				}
			}

			if (resetTags) {
				node.Tag = 0;
			}

			// Check if the node is on a slope steeper than permitted
			if (walkable && useRaycastNormal && collision.heightCheck) {
				if (hit.normal != Vector3.zero) {
					// Take the dot product to find out the cosinus of the angle it has (faster than Vector3.Angle)
					float angle = Vector3.Dot(hit.normal.normalized, collision.up);

					// Add penalty based on normal
					if (penaltyAngle && resetPenalties) {
						node.Penalty += (uint)Mathf.RoundToInt((1F-Mathf.Pow(angle, penaltyAnglePower))*penaltyAngleFactor);
					}

					// Cosinus of the max slope
					float cosAngle = Mathf.Cos(maxSlope*Mathf.Deg2Rad);

					// Check if the ground is flat enough to stand on
					if (angle < cosAngle) {
						walkable = false;
					}
				}
			}

			// If the walkable flag has already been set to false, there is no point in checking for it again
			// Check for obstacles
			node.Walkable = walkable && collision.Check((Vector3)node.position);

			// Store walkability before erosion is applied. Used for graph updates
			node.WalkableErosion = node.Walkable;
		}

		/// <summary>
		/// True if the node has any blocked connections.
		/// For 4 and 8 neighbours the 4 axis aligned connections will be checked.
		/// For 6 neighbours all 6 neighbours will be checked.
		///
		/// Internal method used for erosion.
		/// </summary>
		protected virtual bool ErosionAnyFalseConnections (GraphNode baseNode) {
			var node = baseNode as GridNode;

			if (neighbours == NumNeighbours.Six) {
				// Check the 6 hexagonal connections
				for (int i = 0; i < 6; i++) {
					if (!HasNodeConnection(node, hexagonNeighbourIndices[i])) {
						return true;
					}
				}
			} else {
				// Check the four axis aligned connections
				for (int i = 0; i < 4; i++) {
					if (!HasNodeConnection(node, i)) {
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>Internal method used for erosion</summary>
		void ErodeNode (GraphNode node) {
			if (node.Walkable && ErosionAnyFalseConnections(node)) {
				node.Walkable = false;
			}
		}

		/// <summary>Internal method used for erosion</summary>
		void ErodeNodeWithTagsInit (GraphNode node) {
			if (node.Walkable && ErosionAnyFalseConnections(node)) {
				node.Tag = (uint)erosionFirstTag;
			} else {
				node.Tag = 0;
			}
		}

		/// <summary>Internal method used for erosion</summary>
		void ErodeNodeWithTags (GraphNode node, int iteration) {
			var gnode = node as GridNodeBase;

			if (gnode.Walkable && gnode.Tag >= erosionFirstTag && gnode.Tag < erosionFirstTag + iteration) {
				if (neighbours == NumNeighbours.Six) {
					// Check the 6 hexagonal connections
					for (int i = 0; i < 6; i++) {
						var other = gnode.GetNeighbourAlongDirection(hexagonNeighbourIndices[i]);
						if (other != null) {
							uint tag = other.Tag;
							if (tag > erosionFirstTag + iteration || tag < erosionFirstTag) {
								other.Tag = (uint)(erosionFirstTag+iteration);
							}
						}
					}
				} else {
					// Check the four axis aligned connections
					for (int i = 0; i < 4; i++) {
						var other = gnode.GetNeighbourAlongDirection(i);
						if (other != null) {
							uint tag = other.Tag;
							if (tag > erosionFirstTag + iteration || tag < erosionFirstTag) {
								other.Tag = (uint)(erosionFirstTag+iteration);
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Erodes the walkable area.
		/// See: <see cref="erodeIterations"/>
		/// </summary>
		public virtual void ErodeWalkableArea () {
			ErodeWalkableArea(0, 0, Width, Depth);
		}

		/// <summary>
		/// Erodes the walkable area.
		///
		/// xmin, zmin (inclusive)\n
		/// xmax, zmax (exclusive)
		///
		/// See: <see cref="erodeIterations"/>
		/// </summary>
		public void ErodeWalkableArea (int xmin, int zmin, int xmax, int zmax) {
			if (erosionUseTags) {
				if (erodeIterations+erosionFirstTag > 31) {
					Debug.LogError("Too few tags available for "+erodeIterations+" erode iterations and starting with tag " + erosionFirstTag + " (erodeIterations+erosionFirstTag > 31)", active);
					return;
				}
				if (erosionFirstTag <= 0) {
					Debug.LogError("First erosion tag must be greater or equal to 1", active);
					return;
				}
			}

			if (erodeIterations == 0) return;

			// Get all nodes inside the rectangle
			var rect = new IntRect(xmin, zmin, xmax - 1, zmax - 1);
			var nodesInRect = GetNodesInRegion(rect);
			int nodeCount = nodesInRect.Count;
			for (int it = 0; it < erodeIterations; it++) {
				if (erosionUseTags) {
					if (it == 0) {
						for (int i = 0; i < nodeCount; i++) {
							ErodeNodeWithTagsInit(nodesInRect[i]);
						}
					} else {
						for (int i = 0; i < nodeCount; i++) {
							ErodeNodeWithTags(nodesInRect[i], it);
						}
					}
				} else {
					// Loop through all nodes and mark as unwalkble the nodes which
					// have at least one blocked connection to another node
					for (int i = 0; i < nodeCount; i++) {
						ErodeNode(nodesInRect[i]);
					}

					for (int i = 0; i < nodeCount; i++) {
						CalculateConnections(nodesInRect[i] as GridNodeBase);
					}
				}
			}

			// Return the list to the pool
			Pathfinding.Util.ListPool<GraphNode>.Release (ref nodesInRect);
		}

		/// <summary>
		/// Returns true if a connection between the adjacent nodes n1 and n2 is valid.
		/// Also takes into account if the nodes are walkable.
		///
		/// This method may be overriden if you want to customize what connections are valid.
		/// It must however hold that IsValidConnection(a,b) == IsValidConnection(b,a).
		///
		/// This is used for calculating the connections when the graph is scanned or updated.
		///
		/// See: CalculateConnections
		/// </summary>
		public virtual bool IsValidConnection (GridNodeBase node1, GridNodeBase node2) {
			if (!node1.Walkable || !node2.Walkable) {
				return false;
			}

			if (maxClimb <= 0 || collision.use2D) return true;

			if (transform.onlyTranslational) {
				// Common case optimization.
				// If the transformation is only translational, that is if the graph is not rotated or transformed
				// in any other way than changing its center. Then we can use this simplified code.
				// This code is hot when scanning so it does have an impact.
				return System.Math.Abs(node1.position.y - node2.position.y) <= maxClimb*Int3.Precision;
			} else {
				var p1 = (Vector3)node1.position;
				var p2 = (Vector3)node2.position;
				var up = transform.WorldUpAtGraphPosition(p1);
				return System.Math.Abs(Vector3.Dot(up, p1) - Vector3.Dot(up, p2)) <= maxClimb;
			}
		}

		/// <summary>
		/// Calculates the grid connections for a cell as well as its neighbours.
		/// This is a useful utility function if you want to modify the walkability of a single node in the graph.
		///
		/// <code>
		/// AstarPath.active.AddWorkItem(ctx => {
		///     var grid = AstarPath.active.data.gridGraph;
		///     int x = 5;
		///     int z = 7;
		///
		///     // Mark a single node as unwalkable
		///     grid.GetNode(x, z).Walkable = false;
		///
		///     // Recalculate the connections for that node as well as its neighbours
		///     grid.CalculateConnectionsForCellAndNeighbours(x, z);
		/// });
		/// </code>
		/// </summary>
		public void CalculateConnectionsForCellAndNeighbours (int x, int z) {
			CalculateConnections(x, z);
			for (int i = 0; i < 8; i++) {
				int nx = x + neighbourXOffsets[i];
				int nz = z + neighbourZOffsets[i];

				// Check if the new position is inside the grid
				// Bitwise AND (&) is measurably faster than &&
				// (not much, but this code is hot)
				if (nx >= 0 & nz >= 0 & nx < width & nz < depth) {
					CalculateConnections(nx, nz);
				}
			}
		}

		/// <summary>
		/// Calculates the grid connections for a single node.
		/// Deprecated: Use the instance function instead
		/// </summary>
		[System.Obsolete("Use the instance function instead")]
		public static void CalculateConnections (GridNode node) {
			(AstarData.GetGraph(node) as GridGraph).CalculateConnections((GridNodeBase)node);
		}

		/// <summary>
		/// Calculates the grid connections for a single node.
		/// Convenience function, it's slightly faster to use CalculateConnections(int,int)
		/// but that will only show when calculating for a large number of nodes.
		/// This function will also work for both grid graphs and layered grid graphs.
		/// </summary>
		public virtual void CalculateConnections (GridNodeBase node) {
			int index = node.NodeInGridIndex;
			int x = index % width;
			int z = index / width;

			CalculateConnections(x, z);
		}

		/// <summary>
		/// Calculates the grid connections for a single node.
		/// Deprecated: Use CalculateConnections(x,z) or CalculateConnections(node) instead
		/// </summary>
		[System.Obsolete("Use CalculateConnections(x,z) or CalculateConnections(node) instead")]
		public virtual void CalculateConnections (int x, int z, GridNode node) {
			CalculateConnections(x, z);
		}

		/// <summary>
		/// Calculates the grid connections for a single node.
		/// Note that to ensure that connections are completely up to date after updating a node you
		/// have to calculate the connections for both the changed node and its neighbours.
		///
		/// In a layered grid graph, this will recalculate the connections for all nodes
		/// in the (x,z) cell (it may have multiple layers of nodes).
		///
		/// See: CalculateConnections(GridNodeBase)
		/// </summary>
		public virtual void CalculateConnections (int x, int z) {
			var node = nodes[z*width + x];

			// All connections are disabled if the node is not walkable
			if (!node.Walkable) {
				// Reset all connections
				// This makes the node have NO connections to any neighbour nodes
				node.ResetConnectionsInternal();
				return;
			}

			// Internal index of where in the graph the node is
			int index = node.NodeInGridIndex;

			if (neighbours == NumNeighbours.Four || neighbours == NumNeighbours.Eight) {
				// Bitpacked connections
				// bit 0 is set if connection 0 is enabled
				// bit 1 is set if connection 1 is enabled etc.
				int conns = 0;

				// Loop through axis aligned neighbours (down, right, up, left) or (-Z, +X, +Z, -X)
				for (int i = 0; i < 4; i++) {
					int nx = x + neighbourXOffsets[i];
					int nz = z + neighbourZOffsets[i];

					// Check if the new position is inside the grid
					// Bitwise AND (&) is measurably faster than &&
					// (not much, but this code is hot)
					if (nx >= 0 & nz >= 0 & nx < width & nz < depth) {
						var other = nodes[index+neighbourOffsets[i]];

						if (IsValidConnection(node, other)) {
							// Enable connection i
							conns |= 1 << i;
						}
					}
				}

				// Bitpacked diagonal connections
				int diagConns = 0;

				// Add in the diagonal connections
				if (neighbours == NumNeighbours.Eight) {
					if (cutCorners) {
						for (int i = 0; i < 4; i++) {
							// If at least one axis aligned connection
							// is adjacent to this diagonal, then we can add a connection.
							// Bitshifting is a lot faster than calling node.HasConnectionInDirection.
							// We need to check if connection i and i+1 are enabled
							// but i+1 may overflow 4 and in that case need to be wrapped around
							// (so 3+1 = 4 goes to 0). We do that by checking both connection i+1
							// and i+1-4 at the same time. Either i+1 or i+1-4 will be in the range
							// from 0 to 4 (exclusive)
							if (((conns >> i | conns >> (i+1) | conns >> (i+1-4)) & 1) != 0) {
								int directionIndex = i+4;

								int nx = x + neighbourXOffsets[directionIndex];
								int nz = z + neighbourZOffsets[directionIndex];

								if (nx >= 0 & nz >= 0 & nx < width & nz < depth) {
									GridNode other = nodes[index+neighbourOffsets[directionIndex]];

									if (IsValidConnection(node, other)) {
										diagConns |= 1 << directionIndex;
									}
								}
							}
						}
					} else {
						for (int i = 0; i < 4; i++) {
							// If exactly 2 axis aligned connections is adjacent to this connection
							// then we can add the connection
							// We don't need to check if it is out of bounds because if both of
							// the other neighbours are inside the bounds this one must be too
							if ((conns >> i & 1) != 0 && ((conns >> (i+1) | conns >> (i+1-4)) & 1) != 0) {
								GridNode other = nodes[index+neighbourOffsets[i+4]];

								if (IsValidConnection(node, other)) {
									diagConns |= 1 << (i+4);
								}
							}
						}
					}
				}

				// Set all connections at the same time
				node.SetAllConnectionInternal(conns | diagConns);
			} else {
				// Hexagon layout

				// Reset all connections
				// This makes the node have NO connections to any neighbour nodes
				node.ResetConnectionsInternal();

				// Loop through all possible neighbours and try to connect to them
				for (int j = 0; j < hexagonNeighbourIndices.Length; j++) {
					var i = hexagonNeighbourIndices[j];

					int nx = x + neighbourXOffsets[i];
					int nz = z + neighbourZOffsets[i];

					if (nx >= 0 & nz >= 0 & nx < width & nz < depth) {
						var other = nodes[index+neighbourOffsets[i]];
						node.SetConnectionInternal(i, IsValidConnection(node, other));
					}
				}
			}
		}


		public override void OnDrawGizmos (RetainedGizmos gizmos, bool drawNodes) {
			using (var helper = gizmos.GetSingleFrameGizmoHelper(active)) {
				// The width and depth fields might not be up to date, so recalculate
				// them from the #unclampedSize field
				int w, d;
				float s;
				CalculateDimensions(out w, out d, out s);
				var bounds = new Bounds();
				bounds.SetMinMax(Vector3.zero, new Vector3(w, 0, d));
				var trans = CalculateTransform();
				helper.builder.DrawWireCube(trans, bounds, Color.white);

				int nodeCount = nodes != null ? nodes.Length : -1;

				if (drawNodes && width*depth*LayerCount != nodeCount) {
					var color = new Color(1, 1, 1, 0.2f);
					for (int z = 0; z < d; z++) {
						helper.builder.DrawLine(trans.Transform(new Vector3(0, 0, z)), trans.Transform(new Vector3(w, 0, z)), color);
					}

					for (int x = 0; x < w; x++) {
						helper.builder.DrawLine(trans.Transform(new Vector3(x, 0, 0)), trans.Transform(new Vector3(x, 0, d)), color);
					}
				}
			}

			if (!drawNodes) {
				return;
			}

			// Loop through chunks of size chunkWidth*chunkWidth and create a gizmo mesh for each of those chunks.
			// This is done because rebuilding the gizmo mesh (such as when using Unity Gizmos) every frame is pretty slow
			// for large graphs. However just checking if any mesh needs to be updated is relatively fast. So we just store
			// a hash together with the mesh and rebuild the mesh when necessary.
			const int chunkWidth = 32;
			GridNodeBase[] allNodes = ArrayPool<GridNodeBase>.Claim (chunkWidth*chunkWidth*LayerCount);
			for (int cx = width/chunkWidth; cx >= 0; cx--) {
				for (int cz = depth/chunkWidth; cz >= 0; cz--) {
					Profiler.BeginSample("Hash");
					var allNodesCount = GetNodesInRegion(new IntRect(cx*chunkWidth, cz*chunkWidth, (cx+1)*chunkWidth - 1, (cz+1)*chunkWidth - 1), allNodes);
					var hasher = new RetainedGizmos.Hasher(active);
					hasher.AddHash(showMeshOutline ? 1 : 0);
					hasher.AddHash(showMeshSurface ? 1 : 0);
					hasher.AddHash(showNodeConnections ? 1 : 0);
					for (int i = 0; i < allNodesCount; i++) {
						hasher.HashNode(allNodes[i]);
					}
					Profiler.EndSample();

					if (!gizmos.Draw(hasher)) {
						Profiler.BeginSample("Rebuild Retained Gizmo Chunk");
						using (var helper = gizmos.GetGizmoHelper(active, hasher)) {
							if (showNodeConnections) {
								for (int i = 0; i < allNodesCount; i++) {
									// Don't bother drawing unwalkable nodes
									if (allNodes[i].Walkable) {
										helper.DrawConnections(allNodes[i]);
									}
								}
							}
							if (showMeshSurface || showMeshOutline) CreateNavmeshSurfaceVisualization(allNodes, allNodesCount, helper);
						}
						Profiler.EndSample();
					}
				}
			}
			ArrayPool<GridNodeBase>.Release (ref allNodes);

			if (active.showUnwalkableNodes) DrawUnwalkableNodes(nodeSize * 0.3f);
		}

		/// <summary>
		/// Draw the surface as well as an outline of the grid graph.
		/// The nodes will be drawn as squares (or hexagons when using <see cref="neighbours"/> = Six).
		/// </summary>
		void CreateNavmeshSurfaceVisualization (GridNodeBase[] nodes, int nodeCount, GraphGizmoHelper helper) {
			// Count the number of nodes that we will render
			int walkable = 0;

			for (int i = 0; i < nodeCount; i++) {
				if (nodes[i].Walkable) walkable++;
			}

			var neighbourIndices = neighbours == NumNeighbours.Six ? hexagonNeighbourIndices : new [] { 0, 1, 2, 3 };
			var offsetMultiplier = neighbours == NumNeighbours.Six ? 0.333333f : 0.5f;

			// 2 for a square-ish grid, 4 for a hexagonal grid.
			var trianglesPerNode = neighbourIndices.Length-2;
			var verticesPerNode = 3*trianglesPerNode;

			// Get arrays that have room for all vertices/colors (the array might be larger)
			var vertices = ArrayPool<Vector3>.Claim (walkable*verticesPerNode);
			var colors = ArrayPool<Color>.Claim (walkable*verticesPerNode);
			int baseIndex = 0;

			for (int i = 0; i < nodeCount; i++) {
				var node = nodes[i];
				if (!node.Walkable) continue;

				var nodeColor = helper.NodeColor(node);
				// Don't bother drawing transparent nodes
				if (nodeColor.a <= 0.001f) continue;

				for (int dIndex = 0; dIndex < neighbourIndices.Length; dIndex++) {
					// For neighbours != Six
					// n2 -- n3
					// |     |
					// n  -- n1
					//
					// n = this node
					var d = neighbourIndices[dIndex];
					var nextD = neighbourIndices[(dIndex + 1) % neighbourIndices.Length];
					GridNodeBase n1, n2, n3 = null;
					n1 = node.GetNeighbourAlongDirection(d);
					if (n1 != null && neighbours != NumNeighbours.Six) {
						n3 = n1.GetNeighbourAlongDirection(nextD);
					}

					n2 = node.GetNeighbourAlongDirection(nextD);
					if (n2 != null && n3 == null && neighbours != NumNeighbours.Six) {
						n3 = n2.GetNeighbourAlongDirection(d);
					}

					// Position in graph space of the vertex
					Vector3 p = new Vector3(node.XCoordinateInGrid + 0.5f, 0, node.ZCoordinateInGrid + 0.5f);
					// Offset along diagonal to get the correct XZ coordinates
					p.x += (neighbourXOffsets[d] + neighbourXOffsets[nextD]) * offsetMultiplier;
					p.z += (neighbourZOffsets[d] + neighbourZOffsets[nextD]) * offsetMultiplier;

					// Interpolate the y coordinate of the vertex so that the mesh will be seamless (except in some very rare edge cases)
					p.y += transform.InverseTransform((Vector3)node.position).y;
					if (n1 != null) p.y += transform.InverseTransform((Vector3)n1.position).y;
					if (n2 != null) p.y += transform.InverseTransform((Vector3)n2.position).y;
					if (n3 != null) p.y += transform.InverseTransform((Vector3)n3.position).y;
					p.y /= (1f + (n1 != null ? 1f : 0f) + (n2 != null ? 1f : 0f) + (n3 != null ? 1f : 0f));

					// Convert the point from graph space to world space
					// This handles things like rotations, scale other transformations
					p = transform.Transform(p);
					vertices[baseIndex + dIndex] = p;
				}

				if (neighbours == NumNeighbours.Six) {
					// Form the two middle triangles
					vertices[baseIndex + 6] = vertices[baseIndex + 0];
					vertices[baseIndex + 7] = vertices[baseIndex + 2];
					vertices[baseIndex + 8] = vertices[baseIndex + 3];

					vertices[baseIndex + 9] = vertices[baseIndex + 0];
					vertices[baseIndex + 10] = vertices[baseIndex + 3];
					vertices[baseIndex + 11] = vertices[baseIndex + 5];
				} else {
					// Form the last triangle
					vertices[baseIndex + 4] = vertices[baseIndex + 0];
					vertices[baseIndex + 5] = vertices[baseIndex + 2];
				}

				// Set all colors for the node
				for (int j = 0; j < verticesPerNode; j++) {
					colors[baseIndex + j] = nodeColor;
				}

				// Draw the outline of the node
				for (int j = 0; j < neighbourIndices.Length; j++) {
					var other = node.GetNeighbourAlongDirection(neighbourIndices[(j+1) % neighbourIndices.Length]);
					// Just a tie breaker to make sure we don't draw the line twice.
					// Using NodeInGridIndex instead of NodeIndex to make the gizmos deterministic for a given grid layout.
					// This is important because if the graph would be re-scanned and only a small part of it would change
					// then most chunks would be cached by the gizmo system, but the node indices may have changed and
					// if NodeIndex was used then we might get incorrect gizmos at the borders between chunks.
					if (other == null || (showMeshOutline && node.NodeInGridIndex < other.NodeInGridIndex)) {
						helper.builder.DrawLine(vertices[baseIndex + j], vertices[baseIndex + (j+1) % neighbourIndices.Length], other == null ? Color.black : nodeColor);
					}
				}

				baseIndex += verticesPerNode;
			}

			if (showMeshSurface) helper.DrawTriangles(vertices, colors, baseIndex*trianglesPerNode/verticesPerNode);

			ArrayPool<Vector3>.Release (ref vertices);
			ArrayPool<Color>.Release (ref colors);
		}

		/// <summary>
		/// A rect with all nodes that the bounds could touch.
		/// This correctly handles rotated graphs and other transformations.
		/// The returned rect is guaranteed to not extend outside the graph bounds.
		/// </summary>
		protected IntRect GetRectFromBounds (Bounds bounds) {
			// Take the bounds and transform it using the matrix
			// Then convert that to a rectangle which contains
			// all nodes that might be inside the bounds

			bounds = transform.InverseTransform(bounds);
			Vector3 min = bounds.min;
			Vector3 max = bounds.max;

			int minX = Mathf.RoundToInt(min.x-0.5F);
			int maxX = Mathf.RoundToInt(max.x-0.5F);

			int minZ = Mathf.RoundToInt(min.z-0.5F);
			int maxZ = Mathf.RoundToInt(max.z-0.5F);

			var originalRect = new IntRect(minX, minZ, maxX, maxZ);

			// Rect which covers the whole grid
			var gridRect = new IntRect(0, 0, width-1, depth-1);

			// Clamp the rect to the grid
			return IntRect.Intersection(originalRect, gridRect);
		}

		/// <summary>Deprecated: This method has been renamed to GetNodesInRegion</summary>
		[System.Obsolete("This method has been renamed to GetNodesInRegion", true)]
		public List<GraphNode> GetNodesInArea (Bounds bounds) {
			return GetNodesInRegion(bounds);
		}

		/// <summary>Deprecated: This method has been renamed to GetNodesInRegion</summary>
		[System.Obsolete("This method has been renamed to GetNodesInRegion", true)]
		public List<GraphNode> GetNodesInArea (GraphUpdateShape shape) {
			return GetNodesInRegion(shape);
		}

		/// <summary>Deprecated: This method has been renamed to GetNodesInRegion</summary>
		[System.Obsolete("This method has been renamed to GetNodesInRegion", true)]
		public List<GraphNode> GetNodesInArea (Bounds bounds, GraphUpdateShape shape) {
			return GetNodesInRegion(bounds, shape);
		}

		/// <summary>
		/// All nodes inside the bounding box.
		/// Note: Be nice to the garbage collector and pool the list when you are done with it (optional)
		/// See: Pathfinding.Util.ListPool
		///
		/// See: GetNodesInRegion(GraphUpdateShape)
		/// </summary>
		public List<GraphNode> GetNodesInRegion (Bounds bounds) {
			return GetNodesInRegion(bounds, null);
		}

		/// <summary>
		/// All nodes inside the shape.
		/// Note: Be nice to the garbage collector and pool the list when you are done with it (optional)
		/// See: Pathfinding.Util.ListPool
		///
		/// See: GetNodesInRegion(Bounds)
		/// </summary>
		public List<GraphNode> GetNodesInRegion (GraphUpdateShape shape) {
			return GetNodesInRegion(shape.GetBounds(), shape);
		}

		/// <summary>
		/// All nodes inside the shape or if null, the bounding box.
		/// If a shape is supplied, it is assumed to be contained inside the bounding box.
		/// See: GraphUpdateShape.GetBounds
		/// </summary>
		protected virtual List<GraphNode> GetNodesInRegion (Bounds bounds, GraphUpdateShape shape) {
			var rect = GetRectFromBounds(bounds);

			if (nodes == null || !rect.IsValid() || nodes.Length != width*depth) {
				return ListPool<GraphNode>.Claim ();
			}

			// Get a buffer we can use
			var inArea = ListPool<GraphNode>.Claim (rect.Width*rect.Height);

			// Loop through all nodes in the rectangle
			for (int x = rect.xmin; x <= rect.xmax; x++) {
				for (int z = rect.ymin; z <= rect.ymax; z++) {
					int index = z*width+x;

					GraphNode node = nodes[index];

					// If it is contained in the bounds (and optionally the shape)
					// then add it to the buffer
					if (bounds.Contains((Vector3)node.position) && (shape == null || shape.Contains((Vector3)node.position))) {
						inArea.Add(node);
					}
				}
			}

			return inArea;
		}

		/// <summary>Get all nodes in a rectangle.</summary>
		/// <param name="rect">Region in which to return nodes. It will be clamped to the grid.</param>
		public virtual List<GraphNode> GetNodesInRegion (IntRect rect) {
			// Clamp the rect to the grid
			// Rect which covers the whole grid
			var gridRect = new IntRect(0, 0, width-1, depth-1);

			rect = IntRect.Intersection(rect, gridRect);

			if (nodes == null || !rect.IsValid() || nodes.Length != width*depth) return ListPool<GraphNode>.Claim (0);

			// Get a buffer we can use
			var inArea = ListPool<GraphNode>.Claim (rect.Width*rect.Height);


			for (int z = rect.ymin; z <= rect.ymax; z++) {
				var zw = z*Width;
				for (int x = rect.xmin; x <= rect.xmax; x++) {
					inArea.Add(nodes[zw + x]);
				}
			}

			return inArea;
		}

		/// <summary>
		/// Get all nodes in a rectangle.
		/// Returns: The number of nodes written to the buffer.
		///
		/// Note: This method is much faster than GetNodesInRegion(IntRect) which returns a list because this method can make use of the highly optimized
		///  System.Array.Copy method.
		/// </summary>
		/// <param name="rect">Region in which to return nodes. It will be clamped to the grid.</param>
		/// <param name="buffer">Buffer in which the nodes will be stored. Should be at least as large as the number of nodes that can exist in that region.</param>
		public virtual int GetNodesInRegion (IntRect rect, GridNodeBase[] buffer) {
			// Clamp the rect to the grid
			// Rect which covers the whole grid
			var gridRect = new IntRect(0, 0, width-1, depth-1);

			rect = IntRect.Intersection(rect, gridRect);

			if (nodes == null || !rect.IsValid() || nodes.Length != width*depth) return 0;

			if (buffer.Length < rect.Width*rect.Height) throw new System.ArgumentException("Buffer is too small");

			int counter = 0;
			for (int z = rect.ymin; z <= rect.ymax; z++, counter += rect.Width) {
				System.Array.Copy(nodes, z*Width + rect.xmin, buffer, counter, rect.Width);
			}

			return counter;
		}

		/// <summary>
		/// Node in the specified cell.
		/// Returns null if the coordinate is outside the grid.
		///
		/// <code>
		/// var gg = AstarPath.active.data.gridGraph;
		/// int x = 5;
		/// int z = 8;
		/// GridNodeBase node = gg.GetNode(x, z);
		/// </code>
		///
		/// If you know the coordinate is inside the grid and you are looking to maximize performance then you
		/// can look up the node in the internal array directly which is slightly faster.
		/// See: <see cref="nodes"/>
		/// </summary>
		public virtual GridNodeBase GetNode (int x, int z) {
			if (x < 0 || z < 0 || x >= width || z >= depth) return null;
			return nodes[x + z*width];
		}

		GraphUpdateThreading IUpdatableGraph.CanUpdateAsync (GraphUpdateObject o) {
			return GraphUpdateThreading.UnityThread;
		}

		void IUpdatableGraph.UpdateAreaInit (GraphUpdateObject o) {}
		void IUpdatableGraph.UpdateAreaPost (GraphUpdateObject o) {}

		protected void CalculateAffectedRegions (GraphUpdateObject o, out IntRect originalRect, out IntRect affectRect, out IntRect physicsRect, out bool willChangeWalkability, out int erosion) {
			// Take the bounds and transform it using the matrix
			// Then convert that to a rectangle which contains
			// all nodes that might be inside the bounds
			var bounds = transform.InverseTransform(o.bounds);
			Vector3 min = bounds.min;
			Vector3 max = bounds.max;

			int minX = Mathf.RoundToInt(min.x-0.5F);
			int maxX = Mathf.RoundToInt(max.x-0.5F);

			int minZ = Mathf.RoundToInt(min.z-0.5F);
			int maxZ = Mathf.RoundToInt(max.z-0.5F);

			//We now have coordinates in local space (i.e 1 unit = 1 node)
			originalRect = new IntRect(minX, minZ, maxX, maxZ);
			affectRect = originalRect;

			physicsRect = originalRect;

			erosion = o.updateErosion ? erodeIterations : 0;

			willChangeWalkability = o.updatePhysics || o.modifyWalkability;

			//Calculate the largest bounding box which might be affected

			if (o.updatePhysics && !o.modifyWalkability) {
				// Add the collision.diameter margin for physics calls
				if (collision.collisionCheck) {
					Vector3 margin = new Vector3(collision.diameter, 0, collision.diameter)*0.5F;

					min -= margin*1.02F;//0.02 safety margin, physics is rarely very accurate
					max += margin*1.02F;

					physicsRect = new IntRect(
						Mathf.RoundToInt(min.x-0.5F),
						Mathf.RoundToInt(min.z-0.5F),
						Mathf.RoundToInt(max.x-0.5F),
						Mathf.RoundToInt(max.z-0.5F)
						);

					affectRect = IntRect.Union(physicsRect, affectRect);
				}
			}

			if (willChangeWalkability || erosion > 0) {
				// Add affect radius for erosion. +1 for updating connectivity info at the border
				affectRect = affectRect.Expand(erosion + 1);
			}
		}

		/// <summary>Internal function to update an area of the graph</summary>
		void IUpdatableGraph.UpdateArea (GraphUpdateObject o) {
			if (nodes == null || nodes.Length != width*depth) {
				Debug.LogWarning("The Grid Graph is not scanned, cannot update area");
				//Not scanned
				return;
			}

			IntRect originalRect, affectRect, physicsRect;
			bool willChangeWalkability;
			int erosion;
			CalculateAffectedRegions(o, out originalRect, out affectRect, out physicsRect, out willChangeWalkability, out erosion);

#if ASTARDEBUG
			var debugMatrix = transform * Matrix4x4.TRS(new Vector3(0.5f, 0, 0.5f), Quaternion.identity, Vector3.one);

			originalRect.DebugDraw(debugMatrix, Color.red);
#endif

			// Rect which covers the whole grid
			var gridRect = new IntRect(0, 0, width-1, depth-1);

			// Clamp the rect to the grid bounds
			IntRect clampedRect = IntRect.Intersection(affectRect, gridRect);

			// Mark nodes that might be changed
			for (int z = clampedRect.ymin; z <= clampedRect.ymax; z++) {
				for (int x = clampedRect.xmin; x <= clampedRect.xmax; x++) {
					o.WillUpdateNode(nodes[z*width+x]);
				}
			}

			// Update Physics
			if (o.updatePhysics && !o.modifyWalkability) {
				collision.Initialize(transform, nodeSize);

				clampedRect = IntRect.Intersection(physicsRect, gridRect);

				for (int z = clampedRect.ymin; z <= clampedRect.ymax; z++) {
					for (int x = clampedRect.xmin; x <= clampedRect.xmax; x++) {
						RecalculateCell(x, z, o.resetPenaltyOnPhysics, false);
					}
				}
			}

			//Apply GUO

			clampedRect = IntRect.Intersection(originalRect, gridRect);
			for (int z = clampedRect.ymin; z <= clampedRect.ymax; z++) {
				for (int x = clampedRect.xmin; x <= clampedRect.xmax; x++) {
					int index = z*width+x;

					GridNode node = nodes[index];

					if (o.bounds.Contains((Vector3)node.position)) {
						if (willChangeWalkability) {
							node.Walkable = node.WalkableErosion;
							o.Apply(node);
							node.WalkableErosion = node.Walkable;
						} else {
							o.Apply(node);
						}
					}
				}
			}

#if ASTARDEBUG
			physicsRect.DebugDraw(debugMatrix, Color.blue);
			affectRect.DebugDraw(debugMatrix, Color.black);
#endif

			// Recalculate connections
			if (willChangeWalkability && erosion == 0) {
				clampedRect = IntRect.Intersection(affectRect, gridRect);
				for (int x = clampedRect.xmin; x <= clampedRect.xmax; x++) {
					for (int z = clampedRect.ymin; z <= clampedRect.ymax; z++) {
						CalculateConnections(x, z);
					}
				}
			} else if (willChangeWalkability && erosion > 0) {
				clampedRect = IntRect.Union(originalRect, physicsRect);

				IntRect erosionRect1 = clampedRect.Expand(erosion);
				IntRect erosionRect2 = erosionRect1.Expand(erosion);

				erosionRect1 = IntRect.Intersection(erosionRect1, gridRect);
				erosionRect2 = IntRect.Intersection(erosionRect2, gridRect);

#if ASTARDEBUG
				erosionRect1.DebugDraw(debugMatrix, Color.magenta);
				erosionRect2.DebugDraw(debugMatrix, Color.cyan);
#endif


				// * all nodes inside clampedRect might have had their walkability changed
				// * all nodes inside erosionRect1 might get affected by erosion from clampedRect and erosionRect2
				// * all nodes inside erosionRect2 (but outside erosionRect1) will be reset to previous walkability
				//     after calculation since their erosion might not be correctly calculated (nodes outside erosionRect2 might have an effect)

				for (int x = erosionRect2.xmin; x <= erosionRect2.xmax; x++) {
					for (int z = erosionRect2.ymin; z <= erosionRect2.ymax; z++) {
						int index = z*width+x;

						GridNode node = nodes[index];

						bool tmp = node.Walkable;
						node.Walkable = node.WalkableErosion;

						if (!erosionRect1.Contains(x, z)) {
							//Save the border's walkabilty data (will be reset later)
							node.TmpWalkable = tmp;
						}
					}
				}

				for (int x = erosionRect2.xmin; x <= erosionRect2.xmax; x++) {
					for (int z = erosionRect2.ymin; z <= erosionRect2.ymax; z++) {
						CalculateConnections(x, z);
					}
				}

				// Erode the walkable area
				ErodeWalkableArea(erosionRect2.xmin, erosionRect2.ymin, erosionRect2.xmax+1, erosionRect2.ymax+1);

				for (int x = erosionRect2.xmin; x <= erosionRect2.xmax; x++) {
					for (int z = erosionRect2.ymin; z <= erosionRect2.ymax; z++) {
						if (erosionRect1.Contains(x, z)) continue;

						int index = z*width+x;

						GridNode node = nodes[index];

						//Restore temporarily stored data
						node.Walkable = node.TmpWalkable;
					}
				}

				// Recalculate connections of all affected nodes
				for (int x = erosionRect2.xmin; x <= erosionRect2.xmax; x++) {
					for (int z = erosionRect2.ymin; z <= erosionRect2.ymax; z++) {
						CalculateConnections(x, z);
					}
				}
			}
		}


		/// <summary>
		/// Returns if node is connected to it's neighbour in the specified direction.
		/// This will also return true if <see cref="neighbours"/> = NumNeighbours.Four, the direction is diagonal and one can move through one of the adjacent nodes
		/// to the targeted node.
		///
		/// See: neighbourOffsets
		/// </summary>
		public bool CheckConnection (GridNode node, int dir) {
			if (neighbours == NumNeighbours.Eight || neighbours == NumNeighbours.Six || dir < 4) {
				return HasNodeConnection(node, dir);
			} else {
				int dir1 = (dir-4-1) & 0x3;
				int dir2 = (dir-4+1) & 0x3;

				if (!HasNodeConnection(node, dir1) || !HasNodeConnection(node, dir2)) {
					return false;
				} else {
					GridNode n1 = nodes[node.NodeInGridIndex+neighbourOffsets[dir1]];
					GridNode n2 = nodes[node.NodeInGridIndex+neighbourOffsets[dir2]];

					if (!n1.Walkable || !n2.Walkable) {
						return false;
					}

					if (!HasNodeConnection(n2, dir1) || !HasNodeConnection(n1, dir2)) {
						return false;
					}
				}
				return true;
			}
		}

		protected override void SerializeExtraInfo (GraphSerializationContext ctx) {
			if (nodes == null) {
				ctx.writer.Write(-1);
				return;
			}

			ctx.writer.Write(nodes.Length);

			for (int i = 0; i < nodes.Length; i++) {
				nodes[i].SerializeNode(ctx);
			}
		}

		protected override void DeserializeExtraInfo (GraphSerializationContext ctx) {
			int count = ctx.reader.ReadInt32();

			if (count == -1) {
				nodes = null;
				return;
			}

			nodes = new GridNode[count];

			for (int i = 0; i < nodes.Length; i++) {
				nodes[i] = new GridNode(active);
				nodes[i].DeserializeNode(ctx);
			}
		}

		protected override void DeserializeSettingsCompatibility (GraphSerializationContext ctx) {
			base.DeserializeSettingsCompatibility(ctx);

			aspectRatio = ctx.reader.ReadSingle();
			rotation = ctx.DeserializeVector3();
			center = ctx.DeserializeVector3();
			unclampedSize = (Vector2)ctx.DeserializeVector3();
			nodeSize = ctx.reader.ReadSingle();
			collision.DeserializeSettingsCompatibility(ctx);
			maxClimb = ctx.reader.ReadSingle();
			ctx.reader.ReadInt32();
			maxSlope = ctx.reader.ReadSingle();
			erodeIterations = ctx.reader.ReadInt32();
			erosionUseTags = ctx.reader.ReadBoolean();
			erosionFirstTag = ctx.reader.ReadInt32();
			ctx.reader.ReadBoolean(); // Old field
			neighbours = (NumNeighbours)ctx.reader.ReadInt32();
			cutCorners = ctx.reader.ReadBoolean();
			penaltyPosition = ctx.reader.ReadBoolean();
			penaltyPositionFactor = ctx.reader.ReadSingle();
			penaltyAngle = ctx.reader.ReadBoolean();
			penaltyAngleFactor = ctx.reader.ReadSingle();
			penaltyAnglePower = ctx.reader.ReadSingle();
			isometricAngle = ctx.reader.ReadSingle();
			uniformEdgeCosts = ctx.reader.ReadBoolean();
		}

		protected override void PostDeserialization (GraphSerializationContext ctx) {
			UpdateTransform();
			SetUpOffsetsAndCosts();
			GridNode.SetGridGraph((int)graphIndex, this);

			if (nodes == null || nodes.Length == 0) return;

			if (width*depth != nodes.Length) {
				Debug.LogError("Node data did not match with bounds data. Probably a change to the bounds/width/depth data was made after scanning the graph just prior to saving it. Nodes will be discarded");
				nodes = new GridNode[0];
				return;
			}

			for (int z = 0; z < depth; z++) {
				for (int x = 0; x < width; x++) {
					var node = nodes[z*width+x];

					if (node == null) {
						Debug.LogError("Deserialization Error : Couldn't cast the node to the appropriate type - GridGenerator");
						return;
					}

					node.NodeInGridIndex = z*width+x;
				}
			}
		}
	}

	/// <summary>
	/// Number of neighbours for a single grid node.
	/// \since The 'Six' item was added in 3.6.1
	/// </summary>
	public enum NumNeighbours {
		Four,
		Eight,
		Six
	}
}
