/** \page changelog Changelog

- 3.5.2 (2013-09-01) (tiny bugfix and small feature release)
	- Added isometric angle option for grid graphs to help with isometric 2D games.
	- Fixed a bug with the RVOAgent class which caused the LightweightRVO example scene to not work as intended (no agents were avoiding each other).
	- Fixed some documentation typos.
	- Fixed some compilations errors some people were having with other compilers than Unity's.

- 3.5.1 (2013-06-15)
	- Added avoidance masks to local avoidance.
		Each agent now has a layer and each agent can specify which layers it will avoid.

- 3.5 (2013-06-12)
	- Added back local avoidance!!
		The new system uses a sampling based algorithm instead of a geometric one.
		The API is almost exactly the same so if you used the previous system this will be a drop in replacement.
		As for performance, it is roughly the same, maybe slightly worse in high density situations and slightly better
		in less dense situations. It can handle several thousand agents on an i7 processor.
		Obstacles are not yet supported, but they will be added in a future update.
		
	- Binary heap switched out for a 4-ary heap.
		This improves pathfinding performances by about 5%.
	- Optimized scanning of navmesh graphs (not the recast graphs)
		Large meshes should be much faster to scan now.
	- Optimized BBTree (nearest node lookup for navmesh/recast graphs, pro version only)
		Nearest node queries on navmesh/recast graphs should be slightly faster now.
	- Minor updates to the documentation, esp. to the GraphNode class.
	
- 3.4.0.7
	- Vuforia test build

- 3.4.0.6
	- Fixed an issue where serialization could on some machines sometimes cause an exception to get thrown.
	- Fixed an issue where the recast graph would not rasterize terrains properly near the edges of it.
	- Added PathUtilities.BFS.
	- Added PathUtilities.GetPointsAroundPointWorld.

- 3.4.0.5
	- Added offline documentation (Documentation.zip)
	- Misc fixes for namespace conflicts people have been having. This should improve compatibility with other packages.
		You might need to delete the AstarPathfindingProject folder and reimport the package for everything to work.

- 3.4.0.4
	- Removed RVOSimulatorEditor from the free version, it was causing compiler errors.
	- Made PointGraph.nodes public.

- 3.4.0.3
	- Removed Local Avoidance due to licensing issues.
		Agents will fall back to not avoiding each other.
		I am working to get the local avoidance back as soon as possible.

- 3.4.0.2
	- Unity Asset Store forced me to increase version number.
	
- 3.4.0.1
	- Fixed an ArrayIndexOutOfBounds exception which could be thrown by the ProceduralGridMover script in the Procedural example scene if the target was moved too quickly.
	- The project no longer references assets from the Standard Assets folder (the package on the Unity Asset Store did so by mistake before).

- 3.4
	- Fixed a null reference exception when scanning recast graphs and rasterizing colliders.		
	- Removed duplicate clipper_library.dll which was causing compiler errors.
	- Support for 2D Physics collision testing when using Grid Graphs.
	- Better warnings when using odd settings for Grid Graphs.
	- Minor cleanups.
	- Queued graph updates are no longer being performed when the AstarPath object is destroyed, this just took time.
	- Fixed a bug introduced in 3.3.11 which forced grid graphs to be square in Unity versions earlier than 4.3.
	- Fixed a null reference in BBTree ( used by RecastGraph).
	- Fixed NavmeshGraph not rebuilding BBTree on cached start (causing performance issues on larger graphs).
	
	- Includes all changes from the beta releases below

- Beta 3.3.14 ( available for everyone! )
	- All dlls are now in namespaces (e.g Pathfinding.Ionic.Zip instead of just Ionic.Zip ) to avoid conflicts with other packages.
	- Most scripts are now in namespaces to avoid conflicts with other packages.
	- GridNodes now support custom connections.
	- Cleanups, preparing for release.
	- Reverted to using an Int3 for GraphNode.position instead of an abstract Position property, the tiny memory gains were not worth it.
	
- Beta 3.3.13 ( 4.3 compatible only )
	- Fixed an issue where deleting a NavmeshCut component would not update the underlaying graph.
	- Better update checking.
	
- Beta 3.3.12 ( 4.3 compatible only )
	- Fixed an infinite loop which could happen when scanning graphs during runtime ( not the first scan ).
	- NodeLink component is now working correctly.
	- Added options for optimizations to the PointGraph.
	- Improved TileHandler and navmesh cutting.
	- Fixed rare bug which could mess up navmeshes when using navmesh cutting.
	
- Beta 3.3.11 ( 4.3 compatible only )
	- Fixed update checking. A bug has caused update checking not to run unless you had been running a previous version in which the bug did not exist.
		I am not sure how long this bug has been here, but potentially for a very long time.
	- Added an update notification window which pops up when there is a new version of the A* Pathfinding Project.
	- Lots of UI fixes for Unity 4.3
	- Lots of other UI fixes and imprements.
	- Fixed gravity for RichAI.
	- Fixed Undo for Unity 4.3
	- Added a new example scene showing a procedural environment.

- Beta 3.3.10
	- Removed RecastGraph.includeOutOfBounds.
	- Fixed a few bugs when updating Layered Grid Graphs causing incorrect connections to be created, and valid ones to be left out.
	- Fixed a null reference bug when removing RVO agents.
	- Fixed memory leaks when deserializing graphs or reloading scenes.

- Beta 3.3.9
	- Added new tutorial page about recast graphs.	
	- Recast Graph: Fixed a bug which could cause vertical surfaces to be ignored.
	- Removed support for C++ Recast.
	- Fixed rare bug which could mess up navmeshes when using navmesh cutting.
	- Improved TileHandler and navmesh cutting.
	- GraphModifiers now take O(n) (linear) time to destroy at end of game instead of O(n^2) (quadratic).
	- RecastGraph now has a toggle for using tiles or not.
	- Added RelevantGraphSurface which can be used with RecastGraphs to prune away non-relevant surfaces.
	- Removed RecastGraph.accurateNearestNode since it was not used anymore.
	- Added RecastGraph.nearestSearchOnlyXZ.
	- RecastGraph now has support for removing small areas.
	- Added toggle to show or hide connections between nodes on a recast graph.
	- PointNode has some graph searching methods overloaded specially. This increases performance and reduces alloacations when searching
		point graphs.
	- Reduced allocations when searching on RecastGraph.
	- Reduced allocations in RichAI and RichPath. Everything is pooled now, so for most requests no allocations will be done.
	- Reduced allocations in general by using "yield return null" instead of "yield return 0"
	- Fixed teleport for local avoidance agents. Previously moving an agent from one position to another
		could cause it to interpolate between those two positions for a brief amount of time instead of staying at the second position.
	
- Beta 3.3.8
	- Nicer RichAI gizmo colors.
	- Fixed RichAI not using raycast when no path has been calculated.

- Beta 3.3.7
	- Fixed stack overflow exception in RichPath
	- Fixed RichPath could sometimes generate invalid paths
	- Added gizmos to RichAI
		
- Beta 3.3.6
	- Fixed node positions being off by half a node size. GetNearest node queries on grid graphs would be slightly inexact.
	- Fixed grid graph updating could get messed up when using erosion.	
	- ... among other things, see below
	
- Beta 3.3.5 and 3.3.6
	- Highlights
		- Rewritten graph nodes. Nodes can now be created more easily (less overhead when creating nodes).
		- Graphs may use their custom optimized memory structure for storing nodes.	
		- Performance improvements for scanning recast graphs.
		- Added a whole new AI script. RichAI (and the class RichPath for some things):
			This script is intended for navmesh based graphs and has features such as:
			- Guarantees that the character stays on the navmesh
			- Minor deviations from the path can be fixed without a path recalculation.
			- Very exact stop at endpoint (seriously, precision with something like 7 decimals).
				No more circling around the target point as with AIPath.
			- Does not use path modifiers at all (for good reasons). It has an internal funnel modifier however.
			- Simple wall avoidance to avoid too much wall hugging.
			- Basic support for off-mesh links (see example scene).
		- Improved randomness for RandomPath and FleePath, all nodes considered now have an equal chance of being selected.
		- Recast now has support for tiles. This enabled much larger worlds to be rasterized (without OutOfMemory errors) and allows for dynamic graph updates. Still slow, but much faster than
			a complete recalculation of the graph.
		- Navmesh Cutting can now be done on recast graphs. This is a kind of (relatively) cheap graph updating which punches a hole in the navmesh to make place for obstacles.
			So it only supports removing geometry, not adding it (like bridges). This update is comparitively fast, and it makes real time navmesh updating possible.
			See video: http://youtu.be/qXi5qhhGNIw.
		- Added RecastMeshObj which can be attached to any GameObject to include that object in recast rasterization. It exposes more options and is also
			faster for graph updates with logarithmic lookup complexity instead of linear (good for larger worlds when doing graph updating).
		- Reintroducing special connection costs for start and end nodes.
			Before multithreading was introduced, pathfinding on navmesh graphs could recalculate
			the connection costs for the start and end nodes to take into account that the start point is not actually exactly at the start node's position
			(triangles are usually quite a larger than the player/npc/whatever).
			This didn't work with multithreading however and could mess up pathfinding, so it was removed.
			Now it has been reintroduced, working with multithreading! This means more accurate paths
			on navmeshes.
		- Added several methods to pick random points (e.g for group movement) to Pathfinding.PathUtlitilies.
		- Added RadiusModifier. A new modifier which can offset the path based on the character radius. Intended for navmesh graphs
			which are not shrinked by the character radius at start but can be used for other purposes as well.
		- Improved GraphUpdateScene gizmos. Convex gizmos are now correctly placed. It also shows a bounding box when selected (not showing this has confused a lot of people).
		- AIPath has gotten some cleanups. Among other things it now behaves correctly when disabled and then enabled again
			making it easy to pool and reuse (should that need arise).
		- Funnel modifier on grid graphs will create wider funnels for diagonals which results in nicer paths.
		- If an exception is thrown during pathfinding, the program does no longer hang at quit.
		- Split Automatic thread count into Automatic High Load and Automatic Low Load. The former one using a higher number of thread.
		- Thread count used is now shown in the editor.
		- GridGraph now supports ClosestOnNode (StartEndModifier) properly. SnapToNode gives the previous behaviour on GridGraphs (they were identical before).
		- New example scene Door2 which uses the NavmeshCut component.
	- Fixes
		- Fixed spelling error in GridGraph.uniformWidthDepthGrid.
		- Erosion radius (character radius, recast graphs) could become half of what it really should be in many cases.
		- RecastGraph will not rasterize triggers.
		- Fixed recast not being able to handle multiple terrains.
		- Fixed recast generating an incorrect mesh for terrains in some cases (not the whole terrain was included).
		- Linecast on many graph types had incorrect descriptions saying that the function returns true when the line does not intersect any obstacles,
			it is actually the other way around. Descriptions corrected.
		- The list of nodes returned by a ConstantPath is now guaranteed to have no duplicates.
		- Many recast constants are now proper constants instead of static variables.
		- Fixed bug in GridNode.RemoveGridGraph which caused graphs not being cleaned up correctly. Could cause problems later on.
		- Fixed an ArgumentOutOfRange exception in ListPool class.
		- RelocateNodes on NavMeshGraph now correctly recalculates connection costs and rebuilds the internal query tree (thanks peted on the forums).
		- Much better member documentation for RVOController.
		- Exposed MaxNeighbours from IAgent to RVOController.
		- Fixed AstarData.UpdateShortcuts not being called when caching was enabled. This caused graph shortcuts such as AstarPath.astarData.gridGraph not being set
			when loaded from a cache.
		- RVOCoreSimulator/RVOSimulator now cleans up the worker threads correctly.
		- Tiled recast graphs can now be serialized.
	- Changes
		- Renamed Modifier class to PathModifier to avoid naming conflicts with user scripts and other packages.
		- Cleaned up recast, put inside namespace and split into multiple files.
		- ListPool and friends are now threadsafe.
		- Removed Polygon.Dot since the Vector3 class already contains such a method.
		- The Scan functions now use callbacks for progress info instead of IEnumerators. Graphs can now output progress info as well.
		- Added Pathfinding.NavGraph.CountNodes function.
		- Removed GraphHitInfo.success field since it was not used.
		- GraphUpdateScene will now fall back to collider.bounds or renderer.bounds (depending on what is available) if no points are
			defined for the shape.
		- AstarPath.StartPath now has an option to put the path in the front of the queue to prioritize its calculation over other paths.
		- Time.fixedDeltaTime by Time.deltaTime in AIPath.RotateTowards() to work with both FixedUpdate and Update. (Thanks Pat_AfterMoon)
			You might have to configure the turn speed variable after updating since the actual rotation speed might have changed a bit depending on your settings.
		- Fixed maxNeighbourDistance not being used correctly by the RVOController script. It would stay at the default value. If you
			have had trouble getting local avoidance working on world with a large scale, this could have been the problem. (Thanks to Edgar Sun for providing a reproducible example case)
		- Graphs loaded using DeserializeGraphsAdditive will get their graphIndex variables on the nodes set to the correct values. (thanks peted for noticing the bug).
		- Fixed a null reference exception in MultiTargetPath (thanks Dave for informing me about the bug).
		- GraphUpdateScene.useWorldSpace is now false per default.
		- If no log output is disabled and we are not running in the editor, log output will be discarded as early as possible for performance.
			Even though in theory log output could be enabled between writing to internal log strings and deciding if log output should be written.
		- NavGraph.inverseMatrix is now a field, not a property (for performance). All writes to matrix should be through the SetMatrix method.
		- StartEndModifier now uses ClosestOnNode for both startPoint and endPoint by default.
	- Known bugs
		- Linecasting on graphs is broken at the moment. (working for recast/navmesh graph atm. Except in very special cases)
		- RVONavmesh does not work with tiled recast graphs.

		

- 3.2.5.1
	- Fixes
		- Pooling of paths had been accidentally disabled in AIPath.
	
- 3.2.5
	- Changes
		- Added support for serializing dictionaries with integer keys via a Json Converter.
		- If drawGizmos is disabled on the seeker, paths will be recycled instantly.
			This will show up so that if you had a seeker with drawGizmos=false, and then enable
			drawGizmos, it will not draw gizmos until the next path request is issued.
	- Fixes
		- Fixed UNITY_4_0 preprocesor directives which were indented for UNITY 4 and not only 4.0.
			Now they will be enabled for all 4.x versions of unity instead of only 4.0.
		- Fixed a path pool leak in the Seeker which could cause paths not to be released if a seeker
			was destroyed.
		- When using a non-positive maxDistance for point graphs less processing power will be used.
		- Removed unused 'recyclePaths' variable in the AIPath class.
		- NullReferenceException could ocurr if the Pathfinding.Node.connections array was null.
		- Fixed NullReferenceException which could ocurr sometimes when using a MultiTargetPath (Issue #16)
		- Changed Ctrl to Alt when recalcing path continously in the Path Types example scene to avoid
			clearing the points for the MultiTargetPath at the same time (it was also using Ctrl).
		- Fixed strange looking movement artifacts during the first few frames when using RVO and interpolation was enabled.
		- AlternativePath modifier will no longer cause underflows if penalties have been reset during the time it was active. It will now
			only log a warning message and zero the penalty.
		- Added Pathfinding.GraphUpdateObject.resetPenaltyOnPhysics (and similar in GraphUpdateScene) to force grid graphs not to reset penalties when
			updating graphs.
		- Fixed a bug which could cause pathfinding to crash if using the preprocessor directive ASTAR_NoTagPenalty.
		- Fixed a case where StartEndModifier.exactEndPoint would incorrectly be used instead of exactStartPoint.
		- AlternativePath modifier now correctly resets penalties if it is destroyed.
		
- 3.2.4.1
	- Unity Asset Store guys complained about the wrong key image.
		I had to update the version number to submit again.
	
- 3.2.4
	- Highlights
		- RecastGraph can now rasterize colliders as well!
		- RecastGraph can rasterize colliders added to trees on unity terrains!
		- RecastGraph will use Graphics.DrawMeshNow functions in Unity 4 instead of creating a dummy GameObject.
			This will remove the annoying "cleaning up leaked mesh object" debug message which unity would log sometimes.
			The debug mesh is now also only visible in the Scene View when the A* object is selected as that seemed
			most logical to me (don't like this? post something in the forum saying you want a toggle for it and I will implement
			one).
		- GraphUpdateObject now has a \link Pathfinding.GraphUpdateObject.updateErosion toggle \endlink specifying if erosion (on grid graphs) should be recalculated after applying the guo.
			This enables one to add walkable nodes which should have been made unwalkable by erosion.
		- Made it a bit easier (and added more correct documentation) to add custom graph types when building for iPhone with Fast But No Exceptions (see iPhone page).
	- Changes
		- RecastGraph now only rasterizes enabled MeshRenderers. Previously even disabled ones would be included.
		- Renamed RecastGraph.includeTerrain to RecastGraph.rasterizeTerrain to better match other variable naming.
	- Fixes
		- AIPath now resumes path calculation when the component or GameObject has been disabled and then reenabled.

- 3.2.3 (free version mostly)
	- Fixes
		- A #UNITY_IPHONE directive was not included in the free version. This caused compilation errors when building for iPhone.
	- Changes
		- Some documentation updates

- 3.2.2
	- Changes
		- Max Slope in grid graphs is now relative to the graph's up direction instead of world up (makes more sense I hope)
	- Note
		- Update really too small to be an update by itself, but I was updating the build scripts I use for the project and had to upload a new version because of technical reasons.
		
- 3.2.1
	- Fixes
		- Fixed bug which caused compiler errors on build (player, not in editor).
		- Version number was by mistake set to 3.1 instead of 3.2 in the previous version.
		
- 3.2
	- Highlights
		- A complete Local Avoidance system is now included in the pro version!
		- Almost every allocation can now be pooled. Which means a drastically lower allocation rate (GC get's called less often).
		- Initial node penalty per graph can now be set.
			Custom graph types implementing CreateNodes must update their implementations to properly assign this value.
		- GraphUpdateScene has now many more tools and options which can be used.
		- Added Pathfinding.PathUtilities which contains some usefull functions for working with paths and nodes.
		- Added Pathfinding.Node.GetConnections to enable easy getting of all connections of a node.
			The Node.connections array does not include custom connections which for example grid graphs use.
		- Seeker.PostProcess function was added for easy postprocessing of paths calculated without a seeker.
		- AstarPath.WaitForPath. Wait (block) until a specific path has been calculated.
		- Path.WaitForPath. Wait using a coroutine until a specific path has been calculated.
		- LayeredGridGraph now has support for up to 65535 layers (theoretically, but don't try it as you would probably run out of memory)
		- Recast graph generation is now up to twice as fast!
		- Fixed some UI glitches in Unity 4.
		- Debugger component has more features and a slightly better layout.
	- Fixes
		- Fixed a bug which caused the SimpleSmoothModifier with uniformSegmentLength enabled to skip points sometimes.
		- Fixed a bug where importing graphs additively which had the same GUID as a graph already loaded could cause bugs in the inspector.
		- Fixed a bug where updating a GridGraph loaded from file would throw a NullReferenceException.
		- Fixed a bug which could cause error messages for paths not to be logged
		- Fixed a number of small bugs related to updating grid graphs (especially when using erosion as well).
		- Overflows could occur in some navmesh/polygon math related functions when working with Int3s. This was because the precision of them had recently been increased.
			Further down the line this could cause incorrect answers to GetNearest queries.
			Fixed by casting to long when necessary.
		- Navmesh2.shader defined "Cull Off" twice.
		- Pathfinding threads are now background threads. This will prevent them from blocking the process to terminate if they of some reason are still alive (hopefully at least).
 		- When really high penalties are applied (which could be underflowed negative penalties) a warning message is logged.
 			Really high penalties (close to max uint value) can otherwise cause overflows and in some cases infinity loops because of that.
 		- ClosestPointOnTriangle is now spelled correctly.
 		- MineBotAI now uses Update instead of FixedUpdate.
 		- Use Dark Skin option is now exposed again since it could be incorrectly set sometimes. Now you can force it to light or dark, or set it to auto.
 		- Fixed recast graph bug when using multiple terrains. Previously only one terrain would be used.
 		- Fixed some UI glitches in Unity 4.
	- Changes
		- Removed Pathfinding.NNInfo.priority.
		- Removed Pathfinding.NearestNodePriority.
		- Conversions between NNInfo and Node are now explicit to comply with the rule of "if information might be lost: use explicit casts".
		- NNInfo is now a struct.
		- GraphHitInfo is now a struct.
		- Path.vectorPath and Path.path are now List<Vector3> and List<Node> respectively. This is done to enable pooling of resources more efficiently.
		- Added Pathfinding.Node.RecalculateConnectionCosts.
		- Moved IsPathPossible from GraphUpdateUtilities to PathUtilities.
		- Pathfinding.Path.processed was replaced with Pathfinding.Path.state. The new variable will have much more information about where
			the path is in the pathfinding pipeline.
		- <b>Paths should not be created with constructors anymore, instead use the PathPool class and then call some Setup() method</b>
		- When the AstarPath object is destroyed, calculated paths in the return queue are not returned with errors anymore, but just returned.
		- Removed depracated methods AstarPath.AddToPathPool, RecyclePath, GetFromPathPool.
	- Bugs
		- C++ Version of Recast does not work on Windows.
		- GraphUpdateScene does in some cases not draw correctly positioned gizmos.
		- Starting two webplayers and closing down the first might cause the other one's pathfinding threads to crash (unity bug?) (confirmed on osx)

- 3.1.4 (iOS fixes)
	- Fixes
		- More fixes for the iOS platform.
		- The "JsonFx.Json.dll" file is now correctly named.
	- Changes
		- Removed unused code from DotNetZip which reduced the size of it with about 20 KB.
		
- 3.1.3 (free version only)
	- Fixes
		- Some of the fixes which were said to have been made in 3.1.2 were actually not included in the free version of the project. Sorry about that.
		- Also includes a new JsonFx and Ionic.Zip dll. This should make it possible to build with the .Net 2.0 Subset again see:
			http://www.arongranberg.com/forums/topic/ios-problem/page/1/

- 3.1.2 (small bugfix release)
	- Fixes
		- Fixed a bug which caused builds for iPhone to fail.
		- Fixed a bug which caused runtime errors on the iPhone platform.
		- Fixed a bug which caused huge lag in the editor for some users when using grid graphs.
		- ListGraphs are now correctly loaded as PointGraphs when loading data from older versions of the system.
	- Changes
		- Moved JsonFx into the namespace Pathfinding.Serialization.JsonFx to avoid conflicts with users own JsonFx libraries (if they used JsonFx).
		
	- Known bugs
		- Recast graph does not work when using static batching on any objects included.
		
- 3.1.1 (small bugfix release)
	- Fixes
		- Fixed a bug which would cause Pathfinding.GraphUpdateUtilities.UpdateGraphsNoBlock to throw an exception when using multithreading
		- Fixed a bug which caused an error to be logged and no pathfinding working when not using multithreading in the free version of the project
		- Fixed some example scene bugs due to downgrading the project from Unity 3.5 to Unity 3.4

- 3.1
	- Fixed bug which caused LayerMask fields (GridGraph inspector for example) to behave weirdly for custom layers on Unity 3.5 and up.
	- The color setting "Node Connection" now actually sets the colors of the node connections when no other information should be shown using the connection colors or when no data is available.
	- Put the Int3 class in a separate file.
	- Casting between Int3 and Vector3 is no longer implicit. This follows the rule of "if information might be lost: use explicit casts".
	- Renamed ListGraph to PointGraph. "ListGraph" has previously been used for historical reasons. PointGraph is a more suitable name.
	- Graph can now have names in the editor (just click the name in the graph list)
	- Graph Gizmos can now be selectively shown or hidden per graph (small "eye" icon to the right of the graph's name)
	- Added GraphUpdateUtilities with many useful functions for updating graphs.
	- Erosion for grid graphs can now use tags instead of walkability
	- Fixed a bug where using One Way links could in some cases result in a NullReferenceException being thrown.
	- Vector3 fields in the graph editors now look a bit better in Unity 3.5+. EditorGUILayout.Vector3Field didn't show the XYZ labels in a good way (no idea why)
	- GridGraph.useRaycastNormal is now enabled only if the Max Slope is less than 90 degrees. Previously it was a manual setting.
	- The keyboard shortcut to scan all graphs does now also work even when the graphs are not deserialized yet (which happens a lot in the editor)
	- Added NodeLink script, which can be attached to GameObjects to add manual links. This system will eventually replace the links system in the A* editor.
	- Added keyboard shortcuts for adding and removing links. See Menubar -> Edit -> Pathfinding
	\note Some features are restricted to Unity 3.5 and newer because of technical limitations in earlier versions (especially multi-object editing related features).
	
	
- 3.1 beta (version number 3.0.9.9 in Unity due to technical limitations of the System.Versions class)
	- Multithreading is now enabled in the free version of the A* Pathfinding Project!
	- Better support for graph updates called during e.g OnPostScan.
	- PathID is now used as a short everywhere in the project
	- G,H and penalty is now used as unsigned integers everywhere in the project instead of signed integers.
	- There is now only one tag per node (if not the \#define ConfigureTagsAsMultiple is set).
	- Fixed a bug which could make connections between graphs invalid when loading from file (would also log annoying error messages).
	- Erosion (GridGraph) can now be used even when updating the graph during runtime.
	- Fixed a bug where the GridGraph could return null from it's GetNearestForce calls which ended up later throwing a NullReferenceException.
	- FunnelModifier no longer warns if any graph in the path does not implement the IFunnelGraph interface (i.e have no support for the funnel algorithm)
	and instead falls back to add node positions to the path.
	- Added a new graph type : LayerGridGraph which works like a GridGraph, but has support for multiple layers of nodes (e.g multiple floors in a building).
	- ScanOnStartup is now exposed in the editor.
	- Separated temporary path data and connectivity data.
	- Rewritten multithreading. You can now run any number of threads in parallel.
	- To avoid possible infinite loops, paths are no longer returned with just an error when requested at times they should not (e.g right when destroying the pathfinding object)
	- Cleaned up code in AstarPath.cs, members are now structured and many obsolete members have been removed.
	- Rewritten serialization. Now uses Json for settings along with a small part hardcoded binary data (for performance and memory).
		This is a lot more stable and will be more forwards and backwards compatible.
		Data is now saved as zip files(in memory, but can be saved to file) which means you can actually edit them by hand if you want!
	- Added dependency JsonFx (modified for smaller code size and better compatibility).
	- Added dependency DotNetZip (reduced version and a bit modified) for zip compression.
	- Graph types wanting to serialize members must add the JsonOptIn attribute to the class and JsonMember to any members to serialize (in the JsonFx.Json namespace)
	- Graph types wanting to serialize a bit more data (custom), will have to override some new functions from the NavGraph class to do that instead of the old serialization functions.
	- Changed from using System.Guid to a custom written Guid implementation placed in Pathfinding.Util.Guid. This was done to improve compabitility with iOS and other platforms.
	Previously it could crash when trying to create one because System.Guid was not included in the runtime.
	- Renamed callback AstarPath.OnSafeNodeUpdate to AstarPath.OnSafeCallback (also added AstarPath.OnThreadSafeCallback)
	- MultiTargetPath would throw NullReferenceException if no valid start node was found, fixed now.
	- Binary heaps are now automatically expanded if needed, no annoying warning messages.
	- Fixed a bug where grid graphs would not update the correct area (using GraphUpdateObject) if it was rotated.
	- Node position precision increased from 100 steps per world unit to 1000 steps per world unit (if 1 world unit = 1m, that is mm precision).
		This also means that all costs and penalties in graphs will need to be multiplied by 10 to match the new scale.
		It also means the max range of node positions is reduced a bit... but it is still quite large (about 2 150 000 world units in either direction, that should be enough).
	- If Unity 3.5 is used, the EditorGUIUtility.isProSkin field is used to toggle between light and dark skin.
	- Added LayeredGridGraph which works almost the same as grid graphs, but support multiple layers of nodes.
	- \note Dropped Unity 3.3 support.
	
	 <b>Known Bugs:</b> The C++ version of Recast does not work on Windows
	 
- Documentation Update
	- Changed from FixedUpdate to Update in the Get Started Guide. CharacterController.SimpleMove should not be called more than once per frame,
			so this might have lowered performance when using many agents, sorry about this typo.
- 3.0.9
	- The List Graph's "raycast" variable is now serialized correctly, so it will be saved.
	- List graphs do not generate connections from nodes to themselves anymore (yielding slightly faster searches)
	- List graphs previously calculated cost values for connections which were very low (they should have been 100 times larger),
		this can have caused searches which were not very accurate on small scales since the values were rounded to the nearest integer.
	- Added Pathfinding.Path.recalcStartEndCosts to specify if the start and end nodes connection costs should be recalculated when searching to reflect
		small differences between the node's position and the actual used start point. It is on by default but if you change node connection costs you might want to switch it off to get more accurate paths.
	- Fixed a compile time warning in the free version from referecing obsolete variables in the project.
	- Added AstarPath.threadTimeoutFrames which specifies how long the pathfinding thread will wait for new work to turn up before aborting (due to request). This variable is not exposed in the inspector yet.
	- Fixed typo, either there are eight (8) or four (4) max connections per node in a GridGraph, never six (6).
	- AlternativePath will no longer cause errors when using multithreading!
	- Added Pathfinding.ConstantPath, a path type which finds all nodes in a specific distance (cost) from a start node.
	- Added Pathfinding.FloodPath and Pathfinding.FloodPathTracer as an extreamly fast way to generate paths to a single point in for example TD games.
	- Fixed a bug in MultiTargetPath which could make it extreamly slow to process. It would not use much CPU power, but it could take half a second for it to complete due to excessive yielding
	- Fixed a bug in FleePath, it now returns the correct path. It had previously sometimes returned the last node searched, but which was not necessarily the best end node (though it was often close)
	- Using \#defines, the pathfinder can now be better profiled (see Optimizations tab -> Profile Astar)
	- Added example scene Path Types (mainly useful for A* Pro users, so I have only included it for them)
	- Added many more tooltips in the editor
	- Fixed a bug which would double the Y coordinate of nodes in grid graphs when loading from saved data (or caching startup)
	- Graph saving to file will now work better for users of the Free version, I had forgot to include a segment of code for Grid Graphs (sorry about that)
	- Some other bugfixes
- 3.0.8.2
	- Fixed a critical bug which could render the A* inspector unusable on Windows due to problems with backslashes and forward slashes in paths.
- 3.0.8.1
	- Fixed critical crash bug. When building, a preprocessor-directive had messed up serialization so the game would probably crash from an OutOfMemoryException.
- 3.0.8
	- Graph saving to file is now exposed for users of the Free version
	- Fixed a bug where penalties added using a GraphUpdateObject would be overriden if updatePhysics was turned on in the GraphUpdateObject
	- Fixed a bug where list graphs could ignore some children nodes, especially common if the hierarchy was deep
	- Fixed the case where empty messages would spam the log (instead of spamming somewhat meaningful messages) when path logging was set to Only Errors
	- Changed the NNConstraint used as default when calling NavGraph.GetNearest from NNConstraint.Default to NNConstraint.None, this is now the same as the default for AstarPath.GetNearest.
	- You can now set the size of the red cubes shown in place of unwalkable nodes (Settings-->Show Unwalkable Nodes-->Size)
	- Dynamic search of where the EditorAssets folder is, so now you can place it anywhere in the project.
	- Minor A* inspector enhancements.
	- Fixed a very rare bug which could, when using multithreading cause the pathfinding thread not to start after it has been terminated due to a long delay
	- Modifiers can now be enabled or disabled in the editor
	- Added custom inspector for the Simple Smooth Modifier. Hopefully it will now be easier to use (or at least get the hang on which fields you should change).
	- Added AIFollow.canSearch to disable or enable searching for paths due to popular request.
	- Added AIFollow.canMove to disable or enable moving due to popular request.
	- Changed behaviour of AIFollow.Stop, it will now set AIFollow.ccanSearch and AIFollow.ccanMove to false thus making it completely stop and stop searching for paths.
	- Removed Path.customData since it is a much better solution to create a new path class which inherits from Path.
	- Seeker.StartPath is now implemented with overloads instead of optional parameters to simplify usage for Javascript users
	- Added Curved Nonuniform spline as a smoothing option for the Simple Smooth modifier.
	- Added Pathfinding.WillBlockPath as function for checking if a GraphUpdateObject would block pathfinding between two nodes (useful in TD games).
	- Unity References (GameObject's, Transforms and similar) are now serialized in another way, hopefully this will make it more stable as people have been having problems with the previous one, especially on the iPhone.
	- Added shortcuts to specific types of graphs, AstarData.navmesh, AstarData.gridGraph, AstarData.listGraph
	- <b>Known Bugs:</b> The C++ version of Recast does not work on Windows
- 3.0.7
	- Grid Graphs can now be scaled to allow non-square nodes, good for isometric games.
	- Added more options for custom links. For example individual nodes or connections can be either enabled or disabled. And penalty can be added to individual nodes
	- Placed the Scan keyboard shortcut code in a different place, hopefully it will work more often now
	- Disabled GUILayout in the AstarPath script for a possible small speed boost
	- Some debug variables (such as AstarPath.PathsCompleted) are now only updated if the ProfileAstar define is enabled
	- DynamicGridObstacle will now update nodes correctly when the object is destroyed
	- Unwalkable nodes no longer shows when Show Graphs is not toggled
	- Removed Path.multithreaded since it was not used
	- Removed Path.preCallback since it was obsolate
	- Added Pathfinding.XPath as a more customizable path
	- Added example of how to use MultiTargetPaths to the documentation as it was seriously lacking info on that area
	- The viewing mesh scaling for recast graphs is now correct also for the C# version
	- The StartEndModifier now changes the path length to 2 for correct applying if a path length of 1 was passed.
	- The progressbar is now removed even if an exception was thrown during scanning
	- Two new example scenes have been added, one for list graphs which includes sample links, and another one for recast graphs
	- Reverted back to manually setting the dark skin option, since it didn't work in all cases, however if a dark skin is detected, the user will be asked if he/she wants to enable the dark skin
	- Added gizmos for the AIFollow script which shows the current waypoint and a circle around it illustrating the distance required for it to be considered "reached".
	- The C# version of Recast does now use Character Radius instead of Erosion Radius (world units instead of voxels)
	- Fixed an IndexOutOfRange exception which could ocurr when saving a graph with no nodes to file
	- <b>Known Bugs:</b> The C++ version of Recast does not work on Windows
- 3.0.6
	- Added support for a C++ version of Recast which means faster scanning times and more features (though almost no are available at the moment since I haven't added support for them yet).
	- Removed the overload AstarData.AddGraph (string type, NavGraph graph) since it was obsolete. AstarData.AddGraph (Pathfinding.NavGraph) should be used now.
	- Fixed a few bugs in the FunnelModifier which could cause it to return invalid paths
	- A reference image can now be generated for the Use Texture option for Grid Graphs
	- Fixed an editor bug with graphs which had no editors
	- Graphs with no editors now show up in the Add New Graph list to show that they have been found, but they cannot be used
	- Deleted the \a graphIndex parameter in the Pathfinding.NavGraph.Scan function. If you need to use it in your graph's Scan function, get it using Pathfinding.AstarData.GetGraphIndex
	- Javascript support! At last you can use Js code with the A* Pathfinding Project! Go to A* Inspector-->Settings-->Editor-->Enable Js Support to enable it
	- The Dark Skin is now automatically used if the rest of Unity uses the dark skin(hopefully)
	- Fixed a bug which could cause Unity to crash when using multithreading and creating a new AstarPath object during runtime
- 3.0.5
	- \link Pathfinding.PointGraph List Graphs \endlink now support UpdateGraphs. This means that they for example can be used with the DynamicObstacle script.
	- List Graphs can now gather nodes based on GameObject tags instead of all nodes as childs of a specific GameObject.
	- List Graphs can now search recursively for childs to the 'root' GameObject instead of just searching through the top-level children.
	- Added custom area colors which can be edited in the inspector (A* inspector --> Settings --> Color Settings --> Custom Area Colors)
	- Fixed a NullReference bug which could ocurr when loading a Unity Reference with the AstarSerializer.
	- Fixed some bugs with the FleePath and RandomPath which could cause the StartEndModifier to assign the wrong endpoint to the path.
	- Documentation is now more clear on what is A* Pathfinding Project Pro only features.
	- Pathfinding.NNConstraint now has a variable to constrain which graphs to search (A* Pro only).\n
	  This is also available for Pathfinding.GraphUpdateObject which now have a field for an NNConstraint where it can constrain which graphs to update.
	- StartPath calls on the Seeker can now take a parameter specifying which graphs to search for close nodes on (A* Pro only)
	- Added the delegate AstarPath.OnAwakeSettings which is called as the first thing in the Awake function, can be used to set up settings.
	- Pathfinding.UserConnection.doOverrideCost is now serialized correctly. This represents the toggle to the right of the "Cost" field when editing a link.
	- Fixed some bugs with the RecastGraph when spans were partially out-of-bounds, this could generate seemingly random holes in the mesh
- 3.0.4 (only pro version affected)
	- Added a Dark Skin for Unity Pro users (though it is available to Unity Free users too, even though it doesn't look very good).
	  It can be enabled through A* Inspector --> Settings --> Editor Settings --> Use Dark Skin
	- Added option to include or not include out of bounds voxels (Y axis below the graph only) for Recast graphs.
- 3.0.3 (only pro version affected)
	- Fixed a NullReferenceException caused by Voxelize.cs which could surface if there were MeshFilters with no Renderers on GameObjects (Only Pro version affected)
- 3.0.2
	- Textures can now be used to add penalty, height or change walkability of a Grid Graph (A* Pro only)
	- Slope can now be used to add penalty to nodes
	- Height (Y position) can now be usd to add penalty to nodes
	- Prioritized graphs can be used to enable prioritizing some graphs before others when they are overlapping
	- Several bug fixes
	- Included a new DynamicGridObstacle.cs script which can be attached to any obstacle with a collider and it will update grids around it to account for changed position
- 3.0.1
	- Fixed Unity 3.3 compability
- 3.0
	- Rewrote the system from scratch
	- Funnel modifier
	- Easier to extend the system
	

- x. releases are major rewrites or updates to the system.
- .x releases are quite big feature updates
- ..x releases are the most common updates, fix bugs, add some features etc.
- ...x releases are quickfixes, most common when there was a really bad bug which needed fixing ASAP.

Dates are written according to ISO 8601.

 */
