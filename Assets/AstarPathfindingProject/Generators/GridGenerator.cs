//#define ASTAR_NoTagPenalty		//Enables or disables tag penalties. Can give small performance boost
//#define ASTARDEBUG
#define ASTAR_GRID_CUSTOM_CONNECTIONS	
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding.Nodes;
using Pathfinding.Serialization.JsonFx;
using Pathfinding.Serialization;

namespace Pathfinding {
	[JsonOptIn]
	/** Generates a grid of nodes.
The GridGraph does exactly what the name implies, generates nodes in a grid pattern.\n
Grid graphs suit well to when you already have a grid based world.
Features:
 - You can update the graph during runtime (good for e.g Tower Defence or RTS games)
 - Throw any scene at it, with minimal configurations you can get a good graph from it.
 - Supports raycast and the funnel algorithm
 - Predictable pattern
 - Can apply penalty and walkability values from a supplied image
 - Perfect for terrain worlds since it can make areas unwalkable depending on the slope

\shadowimage{gridgraph_graph.png}
\shadowimage{gridgraph_inspector.png}

The <b>The Snap Size</b> button snaps the internal size of the graph to exactly contain the current number of nodes, i.e not contain 100.3 nodes but exactly 100 nodes.\n
This will make the "center" coordinate more accurate.\n

<b>The Graph During Runtime</b>
Any graph which implements the IUpdatableGraph interface can be updated during runtime.\n
For grid graphs this is a great feature since you can update only a small part of the grid without causing any lag like a complete rescan would.\n
If you for example just have instantiated a sphere obstacle in the scene and you want to update the grid where that sphere was instantiated, you can do this:\n
\code AstarPath.active.UpdateGraphs (ob.collider.bounds); \endcode
Where \a ob is the obstacle you just instantiated (a GameObject).\n
As you can see, the UpdateGraphs function takes a Bounds parameter and it will send an update call to all updateable graphs.\n
A grid graph will update that area and a small margin around it equal to \link Pathfinding.GraphCollision.diameter collision testing diameter/2 \endlink
\see graph-updates for more info about updating graphs during runtime


<b>Configure using script</b>
\code
// This holds all graph data
AstarData data = AstarPath.active.astarData;

// This creates a Grid Graph
GridGraph gg = data.AddGraph(typeof(GridGraph)) as GridGraph;

// Setup a grid graph with some values
gg.width = 50;
gg.depth = 50;
gg.nodeSize = 1;
gg.center = new Vector3 (10,0,0);

// Updates internal size from the above values
gg.UpdateSizeFromWidthDepth();

// Scans all graphs, do not call gg.Scan(), that is an internal method
AstarPath.active.Scan();
\endcode

\ingroup graphs
\nosubgrouping
	 */
	public class GridGraph : NavGraph, IUpdatableGraph
	{
		
		/** This function will be called when this graph is destroyed */
		public override void OnDestroy () {
			
			base.OnDestroy ();
			
			//Clean up a reference in a static variable which otherwise should point to this graph forever and stop the GC from collecting it
			RemoveGridGraphFromStatic ();
		}
		
		public void RemoveGridGraphFromStatic () {
			GridNode.SetGridGraph (AstarPath.active.astarData.GetGraphIndex(this),null);
		}
		
		/** This is placed here so generators inheriting from this one can override it and set it to false.
		If it is true, it means that the nodes array's length will always be equal to width*depth
		It is used mainly in the editor to do auto-scanning calls, setting it to false for a non-uniform grid will reduce the number of scans */
		public virtual bool uniformWidthDepthGrid {
			get {
				return true;
			}
		}
		
		public override void GetNodes (GraphNodeDelegateCancelable del) {
			if (nodes == null) return;
			for (int i=0;i<nodes.Length && del (nodes[i]);i++) {}
		}
		
		/** \name Inspector - Settings
		 * \{ */
		
		public int width; /**< Width of the grid in nodes. \see UpdateSizeFromWidthDepth */
		public int depth; /**< Depth (height) of the grid in nodes. \see UpdateSizeFromWidthDepth */
		
		[JsonMember]
		public float aspectRatio = 1F; /**< Scaling of the graph along the X axis. This should be used if you want different scales on the X and Y axis of the grid */

		/** Angle to use for the isometric projection.
		 * If you are making a 2D isometric game, you may want to use this parameter to adjust the layout of the graph to match your game.
		 * This will essentially scale the graph along one of its diagonals to produce something like this:
		 * 
		 * A perspective view of an isometric graph.
		 * \shadowimage{isometric/isometric_perspective.png}
		 * 
		 * A top down view of an isometric graph. Note that the graph is entirely 2D, there is no perspective in this image.
		 * \shadowimage{isometric/isometric_top.png}
		 * 
		 * Usually the angle that you want to use is either 30 degrees (alternatively 90-30 = 60 degrees) or atan(1/sqrt(2)) which is approximately 35.264 degrees (alternatively 90 - 35.264 = 54.736 degrees).
		 * You might also want to rotate the graph plus or minus 45 degrees around the Y axis to get the oritientation required for your game.
		 * 
		 * You can read more about it on the wikipedia page linked below.
		 * 
		 * \see http://en.wikipedia.org/wiki/Isometric_projection
		 * \see rotation
		 */
		[JsonMember]
		public float isometricAngle = 0;

		[JsonMember]
		public Vector3 rotation; /**< Rotation of the grid in degrees */
		
		public Bounds bounds;
		
		[JsonMember]
		public Vector3 center; /**< Center point of the grid */
		
		[JsonMember]
		public Vector2 unclampedSize; 	/**< Size of the grid. Might be negative or smaller than #nodeSize */
		
		[JsonMember]
		public float nodeSize = 1; /**< Size of one node in world units. \see UpdateSizeFromWidthDepth */
		
		/* Collision and stuff */
		
		/** Settings on how to check for walkability and height */
		[JsonMember]
		public GraphCollision collision;
		
		/** The max position difference between two nodes to enable a connection. Set to 0 to ignore the value.*/
		[JsonMember]
		public float maxClimb = 0.4F;
		
		/** The axis to use for #maxClimb. X = 0, Y = 1, Z = 2. */
		[JsonMember]
		public int maxClimbAxis = 1;
		
		/** The max slope in degrees for a node to be walkable. */
		[JsonMember]
		public float maxSlope = 90;
		
		/** Use heigh raycasting normal for max slope calculation.
		 * True if #maxSlope is less than 90 degrees.
		 */
		public bool useRaycastNormal { get { return System.Math.Abs (90-maxSlope) > float.Epsilon; }}
		
		/** Erosion of the graph.
		 * The graph can be eroded after calculation.
		 * This means a margin is put around unwalkable nodes or other unwalkable connections.
		 * It is really good if your graph contains ledges where the nodes without erosion are walkable too close to the edge.
		 * 
		 * Below is an image showing a graph with erode iterations 0, 1 and 2
		 * \shadowimage{erosion.png}
		 * 
		 * \note A high number of erode iterations can seriously slow down graph updates during runtime (GraphUpdateObject)
		 * and should be kept as low as possible.
		 * \see erosionUseTags
		 */
		[JsonMember]
		public int erodeIterations = 0;
		
		/** Use tags instead of walkability for erosion.
		 * Tags will be used for erosion instead of marking nodes as unwalkable. The nodes will be marked with tags in an increasing order starting with the tag #erosionFirstTag.
		 * Debug with the Tags mode to see the effect. With this enabled you can in effect set how close different AIs are allowed to get to walls using the Valid Tags field on the Seeker component. 
		 * \shadowimage{erosionTags.png}
		 * \shadowimage{erosionTags2.png}
		 * \see erosionFirstTag
		 */
		[JsonMember]
		public bool erosionUseTags = false;
		
		/** Tag to start from when using tags for erosion.
		 * \see #erosionUseTags
		 * \see #erodeIterations
		 */
		[JsonMember]
		public int erosionFirstTag = 1;
		
		/** Auto link the graph's edge nodes together with other GridGraphs in the scene on Scan. \see #autoLinkDistLimit */
		[JsonMember]
		public bool autoLinkGrids = false;
		
		/** Distance limit for grid graphs to be auto linked. \see #autoLinkGrids */
		[JsonMember]
		public float autoLinkDistLimit = 10F;
		
		/** Number of neighbours for each node. Either four directional or eight directional */
		[JsonMember]
		public NumNeighbours neighbours = NumNeighbours.Eight;
		
		/** If disabled, will not cut corners on obstacles. If \link #neighbours connections \endlink is Eight, obstacle corners might be cut by a connection, setting this to false disables that. \image html images/cutCorners.png */
		[JsonMember]
		public bool cutCorners = true;
		
		/** Offset for the position when calculating penalty.
		  * \see penaltyPosition */
		[JsonMember]
		public float penaltyPositionOffset = 0;

		/** Use position (y-coordinate) to calculate penalty */
		[JsonMember]

		public bool penaltyPosition = false;
		/** Scale factor for penalty when calculating from position.
		 * \see penaltyPosition
		 */
		[JsonMember]
		public float penaltyPositionFactor = 1F;
		
		[JsonMember]
		public bool penaltyAngle = false;

		[JsonMember]
		public float penaltyAngleFactor = 100F;
		
		
		/** \} */
		
		public Vector2 size;			/**< Size of the grid. Will always be positive and larger than #nodeSize. \see #GenerateMatrix */
		
		/* End collision and stuff */
		
		/** Index offset to get neighbour nodes. Added to a node's index to get a neighbour node index */
		[System.NonSerialized]
		public readonly int[] neighbourOffsets = new int[8];
		
		/** Costs to neighbour nodes */
		[System.NonSerialized]
		public readonly uint[] neighbourCosts = new uint[8];
		
		/** Offsets in the X direction for neighbour nodes. Only 1, 0 or -1 */
		[System.NonSerialized]
		public readonly int[] neighbourXOffsets = new int[8];
		
		/** Offsets in the Z direction for neighbour nodes. Only 1, 0 or -1 */
		[System.NonSerialized]
		public readonly int[] neighbourZOffsets = new int[8];
		
		/* Same as #nodes, but already in the correct type (GridNode instead of Node) */
		//public GridNode[] graphNodes;
		
		/* If a walkable node wasn't found, then it will search (max) in a square with the side of 2*getNearestForceLimit+1 for a close walkable node */
		//public int getNearestForceLimit = 40;
		/** In GetNearestForce, determines how far to search after a valid node has been found */
		public const int getNearestForceOverlap = 2;
		
		public Matrix4x4 boundsMatrix;
		public Matrix4x4 boundsMatrix2;
		
		public int scans = 0;
		
		/** All nodes in this graph */
		public GridNode[] nodes;
		
		
		public GridGraph () {
			unclampedSize = new Vector2 (10,10);
			nodeSize = 1F;
			collision = new GraphCollision ();
		}
		
		public Int3 GetNodePosition (int index, int yOffset) {
			//int z = Math.DivRem (index,Width, out x);
			int z = index/Width;
			int x = index - z*Width;
			return (Int3)matrix.MultiplyPoint3x4(new Vector3(x+0.5f,yOffset*Int3.PrecisionFactor,z+0.5f));//return Int3.zero;}
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
			if (!node.GetConnectionInternal(dir)) return null;
			else if (!node.EdgeNode) {
				return nodes[node.NodeInGridIndex + neighbourOffsets[dir]];
			} else {
				int index = node.NodeInGridIndex;
				//int z = Math.DivRem (index,Width, out x);
				int z = index/Width;
				int x = index - z*Width;
				
				return GetNodeConnection (index, x, z, dir);
			}
		}
		
		public bool HasNodeConnection (GridNode node, int dir) {
			if (!node.GetConnectionInternal(dir)) return false;
			else if (!node.EdgeNode) {
				return true;
			} else {
				int index = node.NodeInGridIndex;
				//int z = Math.DivRem (index,Width, out x);
				int z = index/Width;
				int x = index - z*Width;
				
				return HasNodeConnection (index, x, z, dir);
			}
		}
		
		public void SetNodeConnection (GridNode node, int dir, bool value) {
			int index = node.NodeInGridIndex;
			//int z = Math.DivRem (index,Width, out x);
			int z = index/Width;
			int x = index - z*Width;
			
			SetNodeConnection (index, x, z, dir, value);
		}
		
		/** Get the connecting node from the node at (x,z) in the specified direction.
		 * \returns A GridNode if the node has a connection to that node. Null if no connection in that direction exists
		 * 
		 * \see GridNode
		 */
		private GridNode GetNodeConnection (int index, int x, int z, int dir) {
			if (!nodes[index].GetConnectionInternal (dir)) return null;
			
			/** \todo Mark edge nodes and only do bounds checking for them */
			int nx = x + neighbourXOffsets[dir];
			if (nx < 0 || nx >= Width) return null; /** \todo Modify to get adjacent grid graph here */
			int nz = z + neighbourZOffsets[dir];
			if (nz < 0 || nz >= Depth) return null;
			int nindex = index + neighbourOffsets[dir];
			
			//if (dir < 8) return nodes[index].GetConnectionInternal (dir) ? nodes[nindex] : null;
			return nodes[nindex];
		}
		
		/** Set if connection in the specified direction should be enabled.
		 * Note that bounds checking will still be done when getting the connection value again,
		 * so it is not necessarily true that HasNodeConnection will return true just because you used
		 * SetNodeConnection on a node to set a connection to true.
		 * 
		 * \param index Index of the node
		 * \param x X coordinate of the node
		 * \param z Z coordinate of the node
		 * \param value Enable or disable the connection
		 * 
		 * \note This is identical to Pathfinding.Node.SetConnectionInternal
		 * 
		 * \deprecated
		 */
		public void SetNodeConnection (int index, int x, int z, int dir, bool value) {
			nodes[index].SetConnectionInternal (dir, value);
			
			/*
			/* \todo Mark edge nodes and only do bounds checking for them /
			int nx = x + neighbourXOffsets[dir];
			if (nx < 0 || nx >= Width) return; /** \todo Modify to get adjacent grid graph here 
			int nz = z + neighbourZOffsets[dir];
			if (nz < 0 || nz >= Depth) return;
			int nindex = index + neighbourOffsets[dir];
			
			if (dir < 8) nodes[index].SetConnectionInternal (dir, value);
			else nodes[nindex].SetConnectionInternal (dir, value);*/
		}
		
		public bool HasNodeConnection (int index, int x, int z, int dir) {
			if (!nodes[index].GetConnectionInternal (dir)) return false;
			
			/** \todo Mark edge nodes and only do bounds checking for them */
			int nx = x + neighbourXOffsets[dir];
			if (nx < 0 || nx >= Width) return false; /** \todo Modify to get adjacent grid graph here */
			int nz = z + neighbourZOffsets[dir];
			if (nz < 0 || nz >= Depth) return false;
			//int nindex = index + neighbourOffsets[dir];
			
			return true;
		}
		
		/** Updates #size from #width, #depth and #nodeSize values. Also \link GenerateMatrix generates a new matrix \endlink.
		 * \note This does not rescan the graph, that must be done with Scan */
		public void UpdateSizeFromWidthDepth () {
			unclampedSize = new Vector2 (width,depth)*nodeSize;
			GenerateMatrix ();
		}
		
		/** Generates the matrix used for translating nodes from grid coordinates to world coordintes. */
		public void GenerateMatrix () {
			
			size = unclampedSize;
			
			size.x *= Mathf.Sign (size.x);
			//size.y *= Mathf.Sign (size.y);
			size.y *= Mathf.Sign (size.y);
			
			//Clamp the nodeSize at 0.1
			nodeSize = Mathf.Clamp (nodeSize,size.x/1024F,Mathf.Infinity);//nodeSize < 0.1F ? 0.1F : nodeSize;
			nodeSize = Mathf.Clamp (nodeSize,size.y/1024F,Mathf.Infinity);
			
			size.x = size.x < nodeSize ? nodeSize : size.x;
			//size.y = size.y < 0.1F ? 0.1F : size.y;
			size.y = size.y < nodeSize ? nodeSize : size.y;
			
			var isometricMatrix = Matrix4x4.TRS (Vector3.zero, Quaternion.Euler(0,45,0), Vector3.one);
			isometricMatrix = Matrix4x4.Scale ( new Vector3 ( Mathf.Cos(Mathf.Deg2Rad*isometricAngle), 1, 1 ) ) * isometricMatrix;
			isometricMatrix = Matrix4x4.TRS (Vector3.zero, Quaternion.Euler(0,-45,0), Vector3.one) * isometricMatrix;
			boundsMatrix = Matrix4x4.TRS (center,Quaternion.Euler (rotation),new Vector3 (aspectRatio,1,1)) * isometricMatrix;
			
			//bounds.center = boundsMatrix.MultiplyPoint (Vector3.up*height*0.5F);
			//bounds.size = new Vector3 (width*nodeSize,height,depth*nodeSize);
			
			width = Mathf.FloorToInt (size.x / nodeSize);
			depth = Mathf.FloorToInt (size.y / nodeSize);
			
			if (Mathf.Approximately (size.x / nodeSize,Mathf.CeilToInt (size.x / nodeSize))) {
				width = Mathf.CeilToInt (size.x / nodeSize);
			}
			
			if (Mathf.Approximately (size.y / nodeSize,Mathf.CeilToInt (size.y / nodeSize))) {
				depth = Mathf.CeilToInt (size.y / nodeSize);
			}
			
			//height = size.y;


			var m = Matrix4x4.TRS (boundsMatrix.MultiplyPoint3x4 (-new Vector3 (size.x,0,size.y)*0.5F),Quaternion.Euler(rotation), new Vector3 (nodeSize*aspectRatio,1,nodeSize)) * isometricMatrix;

			SetMatrix (m);
		}
		
		//public void GenerateBounds () {
			//bounds.center = offset+new Vector3 (0,height*0.5F,0);
			//bounds.size = new Vector3 (width*scale,height,depth*scale);
		//}
		
		/** \todo Set clamped position for Grid Graph */
		public override NNInfo GetNearest (Vector3 position, NNConstraint constraint, GraphNode hint) {
			
			if (nodes == null || depth*width != nodes.Length) {
				return new NNInfo ();
			}
			
			position = inverseMatrix.MultiplyPoint3x4 (position);
			
			float xf = position.x-0.5F;
			float zf = position.z-0.5f;
			int x = Mathf.Clamp (Mathf.RoundToInt (xf)  , 0, width-1);
			int z = Mathf.Clamp (Mathf.RoundToInt (zf)  , 0, depth-1);
			
			NNInfo nn = new NNInfo(nodes[z*width+x]);
			
			float y = inverseMatrix.MultiplyPoint3x4((Vector3)nodes[z*width+x].position).y;
			nn.clampedPosition = matrix.MultiplyPoint3x4 (new Vector3(Mathf.Clamp(xf,x-0.5f,x+0.5f)+0.5f,y,Mathf.Clamp(zf,z-0.5f,z+0.5f)+0.5f));
			
			//Set clamped position
			//nn.clampedPosition = new Vector3(Mathf.Clamp (xf,x-0.5f,x+0.5f),position.y,Mathf.Clamp (zf,z-0.5f,z+0.5f));
			//nn.clampedPosition = matrix.MultiplyPoint3x4 (nn.clampedPosition);
			
			return nn;
		}
		
		/** \todo Set clamped position for Grid Graph */
		public override NNInfo GetNearestForce (Vector3 position, NNConstraint constraint) {
			
			if (nodes == null || depth*width != nodes.Length) {
				return new NNInfo ();
			}
			
			Vector3 globalPosition = position;
			
			position = inverseMatrix.MultiplyPoint3x4 (position);
			
			float xf = position.x-0.5F;
			float zf = position.z-0.5f;
			int x = Mathf.Clamp (Mathf.RoundToInt (xf)  , 0, width-1);
			int z = Mathf.Clamp (Mathf.RoundToInt (zf)  , 0, depth-1);
			
			GridNode node = nodes[x+z*width];
			
			GridNode minNode = null;
			float minDist = float.PositiveInfinity;
			int overlap = getNearestForceOverlap;
			
			Vector3 clampedPosition = Vector3.zero;
			NNInfo nn = new NNInfo(null);
			
			if (constraint.Suitable (node)) {
				minNode = node;
				minDist = ((Vector3)minNode.position-globalPosition).sqrMagnitude;
				float y = inverseMatrix.MultiplyPoint3x4((Vector3)node.position).y;
				clampedPosition = matrix.MultiplyPoint3x4 (new Vector3(Mathf.Clamp(xf,x-0.5f,x+0.5f)+0.5f, y, Mathf.Clamp(zf,z-0.5f,z+0.5f)+0.5f));
			}
			
			if (minNode != null) {
				nn.node = minNode;
				nn.clampedPosition = clampedPosition;
				
				if (overlap == 0) return nn;
				else overlap--;
			}
			
			
			//int counter = 0;
			
			
			float maxDist = constraint.constrainDistance ? AstarPath.active.maxNearestNodeDistance : float.PositiveInfinity;
			float maxDistSqr = maxDist*maxDist;
			
			
			//for (int w = 1; w < getNearestForceLimit;w++) {
			for (int w = 1;;w++) {
				
				//Check if the nodes are within distance limit
				if (nodeSize*w > maxDist) {
					nn.node = minNode;
					nn.clampedPosition = clampedPosition;
					return nn;
				}
				
				bool anyInside = false;
				
				int nx = x;
				int nz = z+w;
				
				int nz2 = nz*width;
				
				for (nx = x-w;nx <= x+w;nx++) {
					if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
					anyInside = true;
					if (constraint.Suitable (nodes[nx+nz2])) {
						float dist = ((Vector3)nodes[nx+nz2].position-globalPosition).sqrMagnitude;
						//Debug.DrawRay (nodes[nx+nz2].position,Vector3.up*dist,Color.cyan);counter++;
						if (dist < minDist && dist < maxDistSqr) {
							minDist = dist;
							minNode = nodes[nx+nz2];
							clampedPosition = matrix.MultiplyPoint3x4 (new Vector3 (Mathf.Clamp(xf,nx-0.5f,nx+0.5f)+0.5f, inverseMatrix.MultiplyPoint3x4((Vector3)minNode.position).y, Mathf.Clamp(zf,nz-0.5f,nz+0.5f)+0.5f));
						}
					}
				}
				
				nz = z-w;
				nz2 = nz*width;
				
				for (nx = x-w;nx <= x+w;nx++) {
					if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
					anyInside = true;
					if (constraint.Suitable (nodes[nx+nz2])) {
						float dist = ((Vector3)nodes[nx+nz2].position-globalPosition).sqrMagnitude;
						//Debug.DrawRay (nodes[nx+nz2].position,Vector3.up*dist,Color.cyan);counter++;
						if (dist < minDist && dist < maxDistSqr) {
							minDist = dist;
							minNode = nodes[nx+nz2];
							clampedPosition = matrix.MultiplyPoint3x4 (new Vector3 (Mathf.Clamp(xf,nx-0.5f,nx+0.5f)+0.5f, inverseMatrix.MultiplyPoint3x4((Vector3)minNode.position).y, Mathf.Clamp(zf,nz-0.5f,nz+0.5f)+0.5f));
						}
					}
				}
				
				nx = x-w;
				nz = z-w+1;
				
				for (nz = z-w+1;nz <= z+w-1; nz++) {
					if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
					anyInside = true;
					if (constraint.Suitable (nodes[nx+nz*width])) {
						float dist = ((Vector3)nodes[nx+nz*width].position-globalPosition).sqrMagnitude;
						//Debug.DrawRay (nodes[nx+nz*width].position,Vector3.up*dist,Color.cyan);counter++;
						if (dist < minDist && dist < maxDistSqr) {
							minDist = dist;
							minNode = nodes[nx+nz*width];
							clampedPosition = matrix.MultiplyPoint3x4 (new Vector3 (Mathf.Clamp(xf,nx-0.5f,nx+0.5f)+0.5f, inverseMatrix.MultiplyPoint3x4((Vector3)minNode.position).y, Mathf.Clamp(zf,nz-0.5f,nz+0.5f)+0.5f));
						}
					}
				}
				
				nx = x+w;
				
				for (nz = z-w+1;nz <= z+w-1; nz++) {
					if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
					anyInside = true;
					if (constraint.Suitable (nodes[nx+nz*width])) {
						float dist = ((Vector3)nodes[nx+nz*width].position-globalPosition).sqrMagnitude;
						//Debug.DrawRay (nodes[nx+nz*width].position,Vector3.up*dist,Color.cyan);counter++;
						if (dist < minDist && dist < maxDistSqr) {
							minDist = dist;
							minNode = nodes[nx+nz*width];
							clampedPosition = matrix.MultiplyPoint3x4 (new Vector3 (Mathf.Clamp(xf,nx-0.5f,nx+0.5f)+0.5f, inverseMatrix.MultiplyPoint3x4((Vector3)minNode.position).y, Mathf.Clamp(zf,nz-0.5f,nz+0.5f)+0.5f));
						}
					}
				}
				
				if (minNode != null) {
					if (overlap == 0) {
						nn.node = minNode;
						nn.clampedPosition = clampedPosition;
						return nn;
					}
					else overlap--;
				}
				
				//No nodes were inside grid bounds
				if (!anyInside) {
					nn.node = minNode;
					nn.clampedPosition = clampedPosition;
					return nn;
				}
			}
			//return new NNInfo ();
		}
		
		/** Sets up #neighbourOffsets with the current settings. #neighbourOffsets, #neighbourCosts, #neighbourXOffsets and #neighbourZOffsets are set up.\n
		 * The cost for a non-diagonal movement between two adjacent nodes is RoundToInt (#nodeSize * Int3.Precision)\n
		 * The cost for a diagonal movement between two adjacent nodes is RoundToInt (#nodeSize * Sqrt (2) * Int3.Precision) */
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
			
			uint straightCost = (uint)Mathf.RoundToInt (nodeSize*Int3.Precision);
			uint diagonalCost = (uint)Mathf.RoundToInt (nodeSize*Mathf.Sqrt (2F)*Int3.Precision);
			
			//Diagonals cost more (sqrt(2) times more)
			neighbourCosts[0] = straightCost;
			neighbourCosts[1] = straightCost;
			neighbourCosts[2] = straightCost;
			neighbourCosts[3] = straightCost;
			neighbourCosts[4] = diagonalCost;
			neighbourCosts[5] = diagonalCost;
			neighbourCosts[6] = diagonalCost;
			neighbourCosts[7] = diagonalCost;
			
			
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
		
		public override void ScanInternal (OnScanStatus statusCallback) {
			
			AstarPath.OnPostScan += new OnScanDelegate (OnPostScan);
			
			scans++;
			
			if (nodeSize <= 0) {
				return;
			}
			
			GenerateMatrix ();
			
			if (width > 1024 || depth > 1024) {
				Debug.LogError ("One of the grid's sides is longer than 1024 nodes");
				return;
			}
			
			//GenerateBounds ();
			
			/*neighbourOffsets = new int[8] {
				-width-1,-width,-width+1,
				-1,1,
				width-1,width,width+1
			}*/
			
			SetUpOffsetsAndCosts ();
			
			//GridNode.RemoveGridGraph (this);
			
			int graphIndex = AstarPath.active.astarData.GetGraphIndex(this);
			GridNode.SetGridGraph (graphIndex,this);
			
			//graphNodes = new GridNode[width*depth];
			
			//nodes = CreateNodes (width*depth);
			nodes = new GridNode[width*depth];
			for (int i=0;i<nodes.Length;i++) {
				nodes[i] = new GridNode(active);
				nodes[i].GraphIndex = (uint)graphIndex;
			}
			
			if (collision == null) {
				collision = new GraphCollision ();
			}
			collision.Initialize (matrix,nodeSize);
			
			
			//Max slope in cosinus
			//float cosAngle = Mathf.Cos (maxSlope*Mathf.Deg2Rad);
			
			for (int z = 0; z < depth; z ++) {
				for (int x = 0; x < width; x++) {
					
					GridNode node = nodes[z*width+x];//new GridNode ();
					
					node.NodeInGridIndex = z*width+x;
					
					UpdateNodePositionCollision (node,x,z);
					
					
					/*node.position = matrix.MultiplyPoint3x4 (new Vector3 (x+0.5F,0,z+0.5F));
					
					RaycastHit hit;
					
					bool walkable = true;
					
					node.position = collision.CheckHeight (node.position, out hit, out walkable);
					
					node.penalty = 0;//Mathf.RoundToInt (Random.value*100);
					
					//Check if the node is on a slope steeper than permitted
					if (walkable && useRaycastNormal && collision.heightCheck) {
						
						if (hit.normal != Vector3.zero) {
							//Take the dot product to find out the cosinus of the angle it has (faster than Vector3.Angle)
							float angle = Vector3.Dot (hit.normal.normalized,Vector3.up);
							
							if (angle < cosAngle) {
								walkable = false;
							}
						}
					}
					
					//If the walkable flag has already been set to false, there is no point in checking for it again
					if (walkable) {
						node.walkable = collision.Check (node.position);
					} else {
						node.walkable = walkable;
					}*/
					
				}
			}
			
			
			for (int z = 0; z < depth; z ++) {
				for (int x = 0; x < width; x++) {
				
					GridNode node = nodes[z*width+x];
						
					CalculateConnections (nodes,x,z,node);
					
				}
			}
			
			
			ErodeWalkableArea ();
			//Assign the nodes to the main storage
			
			//startIndex = AstarPath.active.AssignNodes (graphNodes);
			//endIndex = startIndex+graphNodes.Length;
		}
		
		/** Updates position, walkability and penalty for the node.
		 * Assumes that collision.Initialize (...) has been called before this function */
		public virtual void UpdateNodePositionCollision (GridNode node, int x, int z, bool resetPenalty = true) {
			
			node.position = GetNodePosition ( node.NodeInGridIndex, 0 );//0;// = (Int3)matrix.MultiplyPoint3x4 (new Vector3 (x+0.5F,0,z+0.5F));
			
			RaycastHit hit;
			
			bool walkable = true;

			Vector3 position = collision.CheckHeight ((Vector3)node.position, out hit, out walkable);
			//if (walkable)
				node.position = (Int3)position;//GetNodePosition ( node.NodeInGridIndex, (int)((collision.fromHeight - hit.distance)*Int3.Precision) );
			
			if (resetPenalty)
				node.Penalty = initialPenalty;//Mathf.RoundToInt (Random.value*100);
			
			if (penaltyPosition && resetPenalty) {
				node.Penalty += (uint)Mathf.RoundToInt ((node.position.y-penaltyPositionOffset)*penaltyPositionFactor);
			}
			
			/*if (textureData && textureSourceData != null && x < textureSource.width && z < textureSource.height) {
				for (int i=0;i<3;i++) {
					//node.penalty = textureSourceData
				}
			}*/
			//Check if the node is on a slope steeper than permitted
			if (walkable && useRaycastNormal && collision.heightCheck) {
				
				if (hit.normal != Vector3.zero) {
					//Take the dot product to find out the cosinus of the angle it has (faster than Vector3.Angle)
					float angle = Vector3.Dot (hit.normal.normalized,collision.up);
					
					//Add penalty based on normal
					if (penaltyAngle && resetPenalty) {
						node.Penalty += (uint)Mathf.RoundToInt ((1F-angle)*penaltyAngleFactor);
					}
					
					//Max slope in cosinus
					float cosAngle = Mathf.Cos (maxSlope*Mathf.Deg2Rad);
					
					//Check if the slope is flat enough to stand on
					if (angle < cosAngle) {
						walkable = false;
					}
				}
			}
			
			//If the walkable flag has already been set to false, there is no point in checking for it again
			if (walkable)
				node.Walkable = collision.Check ((Vector3)node.position);
			else
				node.Walkable = walkable;
			
			node.WalkableErosion = node.Walkable;
			//Equal to (node as GridNode).WalkableErosion = node.walkable, but this is faster
			
		}
		
		/** Erodes the walkable area. \see #erodeIterations */
		public virtual void ErodeWalkableArea () {
			ErodeWalkableArea (0,0,Width,Depth);
		}
		
		/** Erodes the walkable area.
		 * 
		 * xmin, zmin (inclusive)\n
		 * xmax, zmax (exclusive)
		 * 
		 * \see #erodeIterations */
		public virtual void ErodeWalkableArea (int xmin, int zmin, int xmax, int zmax) {
			//Clamp values to grid
			xmin = xmin < 0 ? 0 : (xmin > Width ? Width : xmin);
			xmax = xmax < 0 ? 0 : (xmax > Width ? Width : xmax);
			zmin = zmin < 0 ? 0 : (zmin > Depth ? Depth : zmin);
			zmax = zmax < 0 ? 0 : (zmax > Depth ? Depth : zmax);
			
			if (!erosionUseTags) {
				for (int it=0;it < erodeIterations;it++) {
					for (int z = zmin; z < zmax; z ++) {
						for (int x = xmin; x < xmax; x++) {
							GridNode node = nodes[z*Width+x];
							
							if (!node.Walkable) {
								
								/*int index = node.GetIndex ();
								
								for (int i=0;i<4;i++) {
									if (node.GetConnection (i)) {
										nodes[index+neighbourOffsets[i]].walkable = false;
									}
								}*/
							} else {
								bool anyFalseConnections = false;
							
								for (int i=0;i<4;i++) {
									if (!this.HasNodeConnection (node,i)) {
										anyFalseConnections = true;
										break;
									}
								}
								
								if (anyFalseConnections) {
									node.Walkable = false;
								}
							}
						}
					}
					
					//Recalculate connections
					for (int z = zmin; z < zmax; z ++) {
						for (int x = xmin; x < xmax; x++) {
							GridNode node = nodes[z*Width+x];
							CalculateConnections (nodes,x,z,node);
						}
					}
				}
			} else {
				if (erodeIterations+erosionFirstTag > 31) {
					Debug.LogError ("Too few tags available for "+erodeIterations+" erode iterations and starting with tag " + erosionFirstTag + " (erodeIterations+erosionFirstTag > 31)");
					return;
				}
				if (erosionFirstTag <= 0) {
					Debug.LogError ("First erosion tag must be greater or equal to 1");
					return;
				}
				
				for (int it=0;it < erodeIterations;it++) {
					for (int z = zmin; z < zmax; z ++) {
						for (int x = xmin; x < xmax; x++) {
							GridNode node = nodes[z*width+x] as GridNode;
							
							if (node.Walkable && node.Tag >= erosionFirstTag && node.Tag < erosionFirstTag + it) {
								
								for (int i=0;i<4;i++) {
									GridNode other = GetNodeConnection (node,i);
									if (other != null) {
										uint tag = other.Tag;
										if (tag > erosionFirstTag + it || tag < erosionFirstTag) {
											other.Tag = (uint)(erosionFirstTag+it);
										}
									}
								}
							} else if (node.Walkable && it == 0) {
								bool anyFalseConnections = false;
								
								for (int i=0;i<4;i++) {
									if (!HasNodeConnection (node, i)) {
										anyFalseConnections = true;
										break;
									}
								}
								
								if (anyFalseConnections) {
									node.Tag = (uint)(erosionFirstTag+it);
								}
							}
						}
					}
				}
			}
		}
		
		/** Returns true if a connection between the adjacent nodes \a n1 and \a n2 is valid. Also takes into account if the nodes are walkable */
		public virtual bool IsValidConnection (GridNode n1, GridNode n2) {
			if (!n1.Walkable || !n2.Walkable) {
				return false;
			}
			
			if (maxClimb != 0 && Mathf.Abs (n1.position[maxClimbAxis] - n2.position[maxClimbAxis]) > maxClimb*Int3.Precision) {
				return false;
			}
			
			return true;
		}
		
		/** To reduce memory allocations this array is reused.
		 * Used in the CalculateConnections function
		 * \see CalculateConnections */
		[System.NonSerialized]
		protected int[] corners;
		
		/** Calculates the grid connections for a single node. Convenience function, it's faster to use CalculateConnections(GridNode[],int,int,node) but that will only show when calculating for a large number of nodes
		  * \todo Test this function, should work ok, but you never know */
		public static void CalculateConnections (GridNode node) {
			GridGraph gg = AstarData.GetGraph (node) as GridGraph;
			
			if (gg != null) {
				int index = node.NodeInGridIndex;
				int x = index % gg.width;
				int z = index / gg.width;
				gg.CalculateConnections (gg.nodes,x,z,node);
			}
		}
		
		/** Calculates the grid connections for a single node */
		public virtual void CalculateConnections (GridNode[] nodes, int x, int z, GridNode node) {
			
			//Reset all connections
			//node.flags = node.lags & -256;
			
			node.ResetConnectionsInternal ();
			
			//All connections are disabled if the node is not walkable
			if (!node.Walkable) {
				return;
			}
			
			int index = node.NodeInGridIndex;
			
			if (corners == null) {
				corners = new int[4];
			} else {
				for (int i = 0;i<4;i++) {
					corners[i] = 0;
				}
			}
			
			for (int i=0, j = 3; i<4; j = i, i++) {
				
				int nx = x + neighbourXOffsets[i];
				int nz = z + neighbourZOffsets[i];
				
				if (nx < 0 || nz < 0 || nx >= width || nz >= depth) {
					continue;
				}
				
				GridNode other = nodes[index+neighbourOffsets[i]] as GridNode;
				
				if (IsValidConnection (node, other)) {
					node.SetConnectionInternal (i, true);
					//SetNodeConnection (node, i, true);
					corners[i]++;
					corners[j]++;
				} else {
					node.SetConnectionInternal (i, false);
					//SetNodeConnection (node, i, false);
				}
			}
			
			if (neighbours == NumNeighbours.Eight) {
				if (cutCorners) {
					for (int i=0; i<4; i++) {
						
						if (corners[i] >= 1) {
							int nx = x + neighbourXOffsets[i+4];
							int nz = z + neighbourZOffsets[i+4];
						
							if (nx < 0 || nz < 0 || nx >= width || nz >= depth) {
								continue;
							}
					
							GridNode other = nodes[index+neighbourOffsets[i+4]];
							
							node.SetConnectionInternal (i+4, IsValidConnection (node,other));
							//SetNodeConnection (node, i+4, IsValidConnection (node,other));
						}
					}
				} else {
					for (int i=0; i<4; i++) {
						
						//We don't need to check if it is out of bounds because if both of the other neighbours are inside the bounds this one must be too
						if (corners[i] == 2) {
							GridNode other = nodes[index+neighbourOffsets[i+4]];
							
							node.SetConnectionInternal (i+4, IsValidConnection (node,other));
							//SetNodeConnection (node, i+4, IsValidConnection (node,other));
						}
					}
				}
			}
			
		}
		
		/** Auto links grid graphs together. Called after all graphs have been scanned.
		 * \see autoLinkGrids
		 */
		public void OnPostScan (AstarPath script) {
			
			AstarPath.OnPostScan -= new OnScanDelegate (OnPostScan);
			
			if (!autoLinkGrids || autoLinkDistLimit <= 0) {
				return;
			}
			
			//Link to other grids
			
			throw new System.NotSupportedException ();
			
	
		}
		
		public override void OnDrawGizmos (bool drawNodes) {
			
			//GenerateMatrix ();
			
			Gizmos.matrix = boundsMatrix;
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube (Vector3.zero, new Vector3 (size.x,0,size.y));
			
			Gizmos.matrix = Matrix4x4.identity;
			
			if (!drawNodes) {
				return;
			}
			
			if (nodes == null || depth*width != nodes.Length) {
				//Scan (AstarPath.active.GetGraphIndex (this));
				return;
			}
			
			//base.OnDrawGizmos (drawNodes);
			
			PathHandler debugData = AstarPath.active.debugPathData;
			
			GridNode node = null;
			
			/*GraphNodeDelegate del = delegate (GraphNode other) {
				Gizmos.DrawLine ((Vector3)node.Position, (Vector3)other.Position);
			};*/
			
			for (int z = 0; z < depth; z ++) {
				for (int x = 0; x < width; x++) {
					node = nodes[z*width+x] as GridNode;
					
					if (!node.Walkable) {// || node.activePath != AstarPath.active.debugPath)  
						continue;
					}
					//Gizmos.color = node.walkable ? Color.green : Color.red;
					//Gizmos.DrawSphere (node.position,0.2F);
					
					Gizmos.color = NodeColor (node,AstarPath.active.debugPathData);
					
					//if (true) {
					//	Gizmos.DrawCube (node.position,Vector3.one*nodeSize);
					//}
					//else 
					if (AstarPath.active.showSearchTree && AstarPath.active.debugPathData != null) {
						if (InSearchTree(node,AstarPath.active.debugPath)) {
							PathNode nodeR = debugData.GetPathNode (node);
							if (nodeR != null && nodeR.parent != null) {
								Gizmos.DrawLine ((Vector3)node.position, (Vector3)nodeR.parent.node.position);
							}
						}
					} else {
						for (int i=0;i<8;i++) {
							GridNode other = GetNodeConnection (node, i);
							if (other != null) {
								Gizmos.DrawLine ((Vector3)node.position, (Vector3)other.position);
							}
						}
					}
					
				}
			}
		}
		
		/** Calculates minimum and maximum points for bounds \a b when multiplied with the matrix */
		public void GetBoundsMinMax (Bounds b, Matrix4x4 matrix, out Vector3 min, out Vector3 max) {
			Vector3[] p = new Vector3[8];
			
			p[0] = matrix.MultiplyPoint3x4 (b.center + new Vector3 ( b.extents.x, b.extents.y, b.extents.z));
			p[1] = matrix.MultiplyPoint3x4 (b.center + new Vector3 ( b.extents.x, b.extents.y,-b.extents.z));
			p[2] = matrix.MultiplyPoint3x4 (b.center + new Vector3 ( b.extents.x,-b.extents.y, b.extents.z));
			p[3] = matrix.MultiplyPoint3x4 (b.center + new Vector3 ( b.extents.x,-b.extents.y,-b.extents.z));
			p[4] = matrix.MultiplyPoint3x4 (b.center + new Vector3 (-b.extents.x, b.extents.y, b.extents.z));
			p[5] = matrix.MultiplyPoint3x4 (b.center + new Vector3 (-b.extents.x, b.extents.y,-b.extents.z));
			p[6] = matrix.MultiplyPoint3x4 (b.center + new Vector3 (-b.extents.x,-b.extents.y, b.extents.z));
			p[7] = matrix.MultiplyPoint3x4 (b.center + new Vector3 (-b.extents.x,-b.extents.y,-b.extents.z));
			
			min = p[0];
			max = p[0];
			for (int i=1;i<8;i++) {
				min = Vector3.Min (min,p[i]);
				max = Vector3.Max (max,p[i]);
			}
		}
		
		/** All nodes inside the bounding box.
		  * \note Be nice to the garbage collector and release the list when you have used it (optional)
		  * \see Pathfinding.Util.ListPool
		  * 
		  * \see GetNodesInArea(GraphUpdateShape)
		  */
		public List<GraphNode> GetNodesInArea (Bounds b) {
			return GetNodesInArea (b, null);
		}
		
		/** All nodes inside the shape.
		  * \note Be nice to the garbage collector and release the list when you have used it (optional)
		  * \see Pathfinding.Util.ListPool
		  * 
		  * \see GetNodesInArea(Bounds)
		  */
		public List<GraphNode> GetNodesInArea (GraphUpdateShape shape) {
			return GetNodesInArea (shape.GetBounds (), shape);
		}
		
		/** All nodes inside the shape or if null, the bounding box.
		 * If a shape is supplied, it is assumed to be contained inside the bounding box.
		 * \see GraphUpdateShape.GetBounds
		 */
		private List<GraphNode> GetNodesInArea (Bounds b, GraphUpdateShape shape) {
			
			if (nodes == null || width*depth != nodes.Length) {
				return null;
			}
			
			List<GraphNode> inArea = Pathfinding.Util.ListPool<GraphNode>.Claim ();
			
			Vector3 min, max;
			GetBoundsMinMax (b,inverseMatrix,out min, out max);
			
			int minX = Mathf.RoundToInt (min.x-0.5F);
			int maxX = Mathf.RoundToInt (max.x-0.5F);
			
			int minZ = Mathf.RoundToInt (min.z-0.5F);
			int maxZ = Mathf.RoundToInt (max.z-0.5F);
			
			IntRect originalRect = new IntRect(minX,minZ,maxX,maxZ);
			IntRect gridRect = new IntRect(0,0,width-1,depth-1);
			
			IntRect rect = IntRect.Intersection (originalRect, gridRect);
			
			for (int x = rect.xmin; x <= rect.xmax;x++) {
				for (int z = rect.ymin;z <= rect.ymax;z++) {
					
					int index = z*width+x;
					
					GraphNode node = nodes[index];
					
					if (b.Contains ((Vector3)node.position) && (shape == null || shape.Contains ((Vector3)node.position))) {
						inArea.Add (node);
					}
				}
			}
			
			return inArea;
		}
		
		public GraphUpdateThreading CanUpdateAsync (GraphUpdateObject o) {
			return GraphUpdateThreading.UnityThread;
		}
		
		public void UpdateAreaInit (GraphUpdateObject o) {}
		
		/** Internal function to update an area of the graph.
		  */
		public void UpdateArea (GraphUpdateObject o) {
			
			if (nodes == null || nodes.Length != width*depth) {
				Debug.LogWarning ("The Grid Graph is not scanned, cannot update area ");
				//Not scanned
				return;
			}
			
			//Copy the bounds
			Bounds b = o.bounds;
			
			Vector3 min, max;
			GetBoundsMinMax (b,inverseMatrix,out min, out max);
			
			int minX = Mathf.RoundToInt (min.x-0.5F);
			int maxX = Mathf.RoundToInt (max.x-0.5F);
			
			int minZ = Mathf.RoundToInt (min.z-0.5F);
			int maxZ = Mathf.RoundToInt (max.z-0.5F);
			//We now have coordinates in local space (i.e 1 unit = 1 node)
			
			IntRect originalRect = new IntRect(minX,minZ,maxX,maxZ);
			IntRect affectRect = originalRect;
			
			IntRect gridRect = new IntRect(0,0,width-1,depth-1);
			
			IntRect physicsRect = originalRect;
			
			int erosion = o.updateErosion ? erodeIterations : 0;
			
			
			bool willChangeWalkability = o.updatePhysics || o.modifyWalkability;
			
			//Calculate the largest bounding box which might be affected
			
			if (o.updatePhysics && !o.modifyWalkability) {
				//Add the collision.diameter margin for physics calls
				if (collision.collisionCheck) {
					Vector3 margin = new Vector3 (collision.diameter,0,collision.diameter)*0.5F;
					
					min -= margin*1.02F;//0.02 safety margin, physics is rarely very accurate
					max += margin*1.02F;
					
					physicsRect = new IntRect(
					                            Mathf.RoundToInt (min.x-0.5F),
					                            Mathf.RoundToInt (min.z-0.5F),
					                            Mathf.RoundToInt (max.x-0.5F),
					                            Mathf.RoundToInt (max.z-0.5F)
					                            );
					
					affectRect = IntRect.Union (physicsRect, affectRect);
				}
			}
			
			if (willChangeWalkability || erosion > 0) {
				//Add affect radius for erosion. +1 for updating connectivity info at the border
				affectRect = affectRect.Expand (erosion + 1);
			}
			
			IntRect clampedRect = IntRect.Intersection (affectRect,gridRect);
			
			//Mark nodes that might be changed
			for (int x = clampedRect.xmin; x <= clampedRect.xmax;x++) {
				for (int z = clampedRect.ymin;z <= clampedRect.ymax;z++) {
					o.WillUpdateNode (nodes[z*width+x]);
				}
			}
			
			//Update Physics
			if (o.updatePhysics && !o.modifyWalkability) {
				
				collision.Initialize (matrix,nodeSize);
				
				clampedRect = IntRect.Intersection (physicsRect,gridRect);
				
				for (int x = clampedRect.xmin; x <= clampedRect.xmax;x++) {
					for (int z = clampedRect.ymin;z <= clampedRect.ymax;z++) {
						
						int index = z*width+x;
						
						GridNode node = nodes[index];
						
						UpdateNodePositionCollision (node,x,z, o.resetPenaltyOnPhysics);
					}
				}
			}
			
			//Apply GUO
			
			clampedRect = IntRect.Intersection (originalRect, gridRect);
			for (int x = clampedRect.xmin; x <= clampedRect.xmax;x++) {
				for (int z = clampedRect.ymin;z <= clampedRect.ymax;z++) {
					int index = z*width+x;
					
					GridNode node = nodes[index];
					
					if (willChangeWalkability) {
						node.Walkable = node.WalkableErosion;
						if (o.bounds.Contains ((Vector3)node.position)) o.Apply (node);
						node.WalkableErosion = node.Walkable;
					} else {
						if (o.bounds.Contains ((Vector3)node.position)) o.Apply (node);
					}
				}
			}
		
			
			//Recalculate connections
			if (willChangeWalkability && erosion == 0) {
				
				clampedRect = IntRect.Intersection (affectRect, gridRect);
				for (int x = clampedRect.xmin; x <= clampedRect.xmax;x++) {
					for (int z = clampedRect.ymin;z <= clampedRect.ymax;z++) {
						int index = z*width+x;
						
						GridNode node = nodes[index];
						
						CalculateConnections (nodes,x,z,node);
					}
				}
			} else if (willChangeWalkability && erosion > 0) {
				
				
				clampedRect = IntRect.Union (originalRect, physicsRect);
				
				IntRect erosionRect1 = clampedRect.Expand (erosion);
				IntRect erosionRect2 = erosionRect1.Expand (erosion);
				
				erosionRect1 = IntRect.Intersection (erosionRect1,gridRect);
				erosionRect2 = IntRect.Intersection (erosionRect2,gridRect);
				
				
				/*
				all nodes inside clampedRect might have had their walkability changed
				all nodes inside erosionRect1 might get affected by erosion from clampedRect and erosionRect2
				all nodes inside erosionRect2 (but outside erosionRect1) will be reset to previous walkability
				after calculation since their erosion might not be correctly calculated (nodes outside erosionRect2 would maybe have effect)
				*/
				
				for (int x = erosionRect2.xmin; x <= erosionRect2.xmax;x++) {
					for (int z = erosionRect2.ymin;z <= erosionRect2.ymax;z++) {
						
						int index = z*width+x;
						
						GridNode node = nodes[index];
						
						bool tmp = node.Walkable;
						node.Walkable = node.WalkableErosion;
						
						if (!erosionRect1.Contains (x,z)) {
							//Save the border's walkabilty data (will be reset later)
							node.TmpWalkable = tmp;
						}
					}
				}
				
				for (int x = erosionRect2.xmin; x <= erosionRect2.xmax;x++) {
					for (int z = erosionRect2.ymin;z <= erosionRect2.ymax;z++) {
						int index = z*width+x;
						
						GridNode node = nodes[index];
						
						
						CalculateConnections (nodes,x,z,node);
					}
				}
				
				//Erode the walkable area
				ErodeWalkableArea (erosionRect2.xmin,erosionRect2.ymin,erosionRect2.xmax+1,erosionRect2.ymax+1);
				
				for (int x = erosionRect2.xmin; x <= erosionRect2.xmax;x++) {
					for (int z = erosionRect2.ymin;z <= erosionRect2.ymax;z++) {
						if (erosionRect1.Contains (x,z)) continue;
						
						int index = z*width+x;
						
						GridNode node = nodes[index];
						
						//Restore temporarily stored data
						node.Walkable = node.TmpWalkable;
					}
				}
				
				//Recalculate connections of all affected nodes
				for (int x = erosionRect2.xmin; x <= erosionRect2.xmax;x++) {
					for (int z = erosionRect2.ymin;z <= erosionRect2.ymax;z++) {
						int index = z*width+x;
						
						GridNode node = nodes[index];
						CalculateConnections (nodes,x,z,node);
					}
				}
			}
		}
		
		//IFunnelGraph Implementation
		
		/*
		public void BuildFunnelCorridor (List<GraphNode> path, int sIndex, int eIndex, List<Vector3> left, List<Vector3> right) {
			
			for (int n=sIndex;n<eIndex;n++) {
				
				GridNode n1 = path[n] as GridNode;
				GridNode n2 = path[n+1] as GridNode;
				
				AddPortal (n1,n2,left,right);
			}
		}
		
		public void AddPortal (GraphNode n1, GraphNode n2, List<Vector3> left, List<Vector3> right) {
			//Not implemented
		}
		
		public void AddPortal (GridNode n1, GridNode n2, List<Vector3> left, List<Vector3> right) {
			
			if (n1 == n2) {
				return;
			}
			
			int i1 = n1.NodeInGridIndex;
			int i2 = n2.NodeInGridIndex;
			/** \todo Use System.Math.DivRem *
			int x1 = i1 % width;
			int x2 = i2 % width;
			int z1 = i1 / width;
			int z2 = i2 / width;
			
			Vector3 n1p = (Vector3)n1.Position;
			Vector3 n2p = (Vector3)n2.Position;
			
			int diffx = Mathf.Abs (x1-x2);
			int diffz = Mathf.Abs (z1-z2);
			
			if (diffx > 1 || diffz > 1) {
				//If the nodes are not adjacent to each other
				
				left.Add (n1p);
				right.Add (n1p);
				left.Add (n2p);
				right.Add (n2p);
			} else if ((diffx+diffz) <= 1){
				//If it is not a diagonal move
				
				Vector3 dir = n2p - n1p;
				dir = dir.normalized * nodeSize * 0.5F;
				Vector3 tangent = Vector3.Cross (dir, Vector3.up);
				tangent = tangent.normalized * nodeSize * 0.5F;
				
				left.Add (n1p + dir - tangent);
				right.Add (n1p + dir + tangent);
			} else {
				//Diagonal move
				
				GraphNode t1 = nodes[z1 * width + x2];
				GraphNode t2 = nodes[z2 * width + x1];
				GraphNode target = null;
				
				if (t1.Walkable) {
					target = t1;
				} else if (t2.Walkable) {
					target = t2;
				}
				
				if (target == null) {
					Vector3 avg = (n1p + n2p) * 0.5F;
					
					left.Add (avg);
					right.Add (avg);
				} else {
					AddPortal (n1,(GridNode)target,left,right);
					AddPortal ((GridNode)target,n2,left,right);
				}
			}
		}*/
		
		//END IFunnelGraph Implementation
		
		
		/** Returns if \a node is connected to it's neighbour in the specified direction.
		  * This will also return true if #neighbours = NumNeighbours.Four, the direction is diagonal and one can move through one of the adjacent nodes
		  * to the targeted node.
		  */
		public bool CheckConnection (GridNode node, int dir) {
			if (neighbours == NumNeighbours.Eight) {
				return HasNodeConnection (node, dir);
			} else {
				int dir1 = (dir-4-1) & 0x3;
				int dir2 = (dir-4+1) & 0x3;
				
				if (!HasNodeConnection (node, dir1) || !HasNodeConnection (node, dir2)) {
					return false;
				} else {
					GridNode n1 = nodes[node.NodeInGridIndex+neighbourOffsets[dir1]];
					GridNode n2 = nodes[node.NodeInGridIndex+neighbourOffsets[dir2]];
					
					if (!n1.Walkable || !n2.Walkable) {
						return false;
					}
					
					if (!HasNodeConnection (n2, dir1) || !HasNodeConnection (n1, dir2)) {
						return false;
					}
				}
				return true;
			}
		}
		
		public override void SerializeExtraInfo (GraphSerializationContext ctx)
		{
			if (nodes == null) {
				ctx.writer.Write(-1);
				return;
			}
			
			ctx.writer.Write (nodes.Length);
			
			for (int i=0;i<nodes.Length;i++) {
				nodes[i].SerializeNode(ctx);
			}
		}
		
		public override void DeserializeExtraInfo (GraphSerializationContext ctx)
		{
			
			int count = ctx.reader.ReadInt32();
			if (count == -1) {
				nodes = null;
				return;
			}
			
			nodes = new GridNode[count];
			
			for (int i=0;i<nodes.Length;i++) {
				nodes[i] = new GridNode (active);
				nodes[i].DeserializeNode(ctx);
			}
		}
		
		public override void PostDeserialization () {
			
			
			GenerateMatrix ();
			
			SetUpOffsetsAndCosts ();
			
			if (nodes == null || nodes.Length == 0) return;
			
			if (width*depth != nodes.Length) {
				Debug.LogWarning ("Node data did not match with bounds data. Probably a change to the bounds/width/depth data was made after scanning the graph just prior to saving it. Nodes will be discarded");
				nodes = new GridNode[0];
				return;
			}
			
			//graphNodes = new GridNode[nodes.Length];
			
			GridNode.SetGridGraph (AstarPath.active.astarData.GetGraphIndex(this),this);
			
			for (int z = 0; z < depth; z ++) {
				for (int x = 0; x < width; x++) {
					
					GridNode node = nodes[z*width+x] as GridNode;
					
					if (node == null) {
						Debug.LogError ("Deserialization Error : Couldn't cast the node to the appropriate type - GridGenerator. Check the CreateNodes function");
						return;
					}
					
					node.NodeInGridIndex = z*width+x;
				}
			}
		}
	}
	
	public enum NumNeighbours {
		Four,
		Eight
	}
}