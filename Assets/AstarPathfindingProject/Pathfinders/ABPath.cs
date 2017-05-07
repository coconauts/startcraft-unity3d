//#define ASTAR_MORE_PATH_IDS
//#define ASTAR_NO_LOGGING //Disables path error logging totally. This also reduces memory allocations because the logging strings will not be allocated. It does not affect normal logging calls, only error calls since they are allocated even though they are not logged
//#define ASTAR_LOCK_FREE_PATH_STATE
//#define ASTARDEBUG

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

namespace Pathfinding {
//Mem - 4+1+4+1+[4]+[4]+1+1+4+4+4+4+4+  12+12+12+12+12+12+4+4+4+4+4+1+1+(4)+4+4+4+4+4+4+4 ? 166 bytes
	
	/** Basic path, finds the shortest path from A to B.
	 * \ingroup paths
	 * This is the most basic path object it will try to find the shortest path from A to B.\n
	 * Many other path types inherit from this type.
	 * \see Seeker.StartPath
	 */
	public class ABPath : Path {
		
		/** Defines if start and end nodes will have their connection costs recalculated for this path.
		 * These connection costs will be more accurate and based on the exact start point and target point,
		 * however it should not be used when connection costs are not the default ones (all build in graph generators currently generate default connection costs).
		 * \see Int3.costMagnitude
		 * \since Added in 3.0.8.3
		 * \bug Does not do anything in 3.2 and up due to incompabilities with multithreading. Will be enabled again in later versions.
		 */
		public bool recalcStartEndCosts = true;
		
		public GraphNode startNode; /**< Start node of the path */
		public GraphNode endNode;   /**< End node of the path */
		
		/** Hints can be set to enable faster Get Nearest Node queries. Only applies to some graph types */
		public GraphNode startHint;
		/** Hints can be set to enable faster Get Nearest Node queries. Only applies to some graph types */
		public GraphNode endHint;
		
		/** Start Point exactly as in the path request */
		public Vector3 originalStartPoint;
		
		/** End Point exactly as in the path request */
		public Vector3 originalEndPoint;
		
		/** Exact start point of the path */
		public Vector3 startPoint;
		
		/** Exact end point of the path */
		public Vector3 endPoint;
		
		/** Determines if a search for an end node should be done.
		 * Set by different path types.
		 * \since Added in 3.0.8.3 */
		protected bool hasEndPoint = true;
		
		public Int3 startIntPoint; /**< Start point in integer coordinates */
		
		/** Calculate partial path if the target node cannot be reached.
		 * If the target node cannot be reached, the node which was closest (given by heuristic) will be chosen as target node
		 * and a partial path will be returned.
		 * This only works if a heuristic is used (which is the default).
		 * If a partial path is found, CompleteState is set to Partial.
		 * \note It is not required by other path types to respect this setting
		 * 
		 */
		public bool calculatePartial = false;
		
		/** Current best target for the partial path.
		 * This is the node with the lowest H score */
		protected PathNode partialBestTarget = null;
		
		/** Saved original costs for the end node. \see ResetCosts */
		protected int[] endNodeCosts;
		
		/** Create a new path with a start and end point.
		 * The delegate will be called when the path has been calculated.
		 * Do not confuse it with the Seeker callback as they are sent at different times.
		 * If you are using a Seeker to start the path you can set \a callbackDelegate to null */
		[System.Obsolete("Use PathPool<T>.GetPath instead")]
		public ABPath (Vector3 start, Vector3 end, OnPathDelegate callbackDelegate) {
			Reset ();
			Setup (start, end, callbackDelegate);
		}
		
		/** @{ @name Constructors */
		
		/** Default constructor.
		 * Do not use this.
		 * Instead use Pathfinding.PathPool<Pathfinding.Path>.GetPath()\n
		 * If you want to use the constructor, use this: 
		 * \code
		 * ABPath p = new ABPath();
		 * p.Reset();
		 * \endcode
		 * And then use a Setup() method.
		 */
		public ABPath () {}
		
		/** Construct a path with a start and end point.
		 * The delegate will be called when the path has been calculated.
		 * Do not confuse it with the Seeker callback as they are sent at different times.
		 * If you are using a Seeker to start the path you can set \a callback to null.
		 * 
		 * \returns The constructed path object
		 */
		public static ABPath Construct (Vector3 start, Vector3 end, OnPathDelegate callback = null) {
			ABPath p = PathPool<ABPath>.GetPath ();
			p.Setup (start, end, callback);
			return p;
		}
		
		protected void Setup (Vector3 start, Vector3 end, OnPathDelegate callbackDelegate) {
			callback = callbackDelegate;
			UpdateStartEnd (start,end);
		}
		
		/** @} */
		
		/** Sets the start and end points.
		 * Sets #originalStartPoint, #originalEndPoint, #startPoint, #endPoint, #startIntPoint and #hTarget (to \a end ) */
		protected void UpdateStartEnd (Vector3 start, Vector3 end) {
			originalStartPoint = start;
			originalEndPoint = end;
			
			startPoint = start;
			endPoint = end;
			
			startIntPoint = (Int3)start;
			hTarget = (Int3)end;
		}
		
		public override uint GetConnectionSpecialCost (GraphNode a, GraphNode b, uint currentCost) {
			if (startNode != null && endNode != null) {
				
				if (a == startNode) {
					// hTarget is the endpoint
					return (uint)((startIntPoint - (b == endNode ? hTarget : b.position)).costMagnitude * (currentCost*1.0/(a.position-b.position).costMagnitude));
				} else if (b == startNode) {
					// hTarget is the endpoint
					return (uint)((startIntPoint - (a == endNode ? hTarget : a.position)).costMagnitude * (currentCost*1.0/(a.position-b.position).costMagnitude));
				} else if (a == endNode) {
					return (uint)((hTarget - b.position).costMagnitude * (currentCost*1.0/(a.position-b.position).costMagnitude));
				} else if (b == endNode) {
					return (uint)((hTarget - a.position).costMagnitude * (currentCost*1.0/(a.position-b.position).costMagnitude));
				}
			} else {
				// endNode is null, startNode should never be null for an ABPath
				if (a == startNode) {
					return (uint)((startIntPoint - b.position).costMagnitude * (currentCost*1.0/(a.position-b.position).costMagnitude));
				} else if (b == startNode) {
					return (uint)((startIntPoint - a.position).costMagnitude * (currentCost*1.0/(a.position-b.position).costMagnitude));
				}
			}
			
			return currentCost;
		}
		
		/** Reset all values to their default values.
		 * All inheriting path types must implement this function, resetting ALL their variables to enable recycling of paths.
		 * Call this base function in inheriting types with base.Reset ();
		  */
		public override void Reset () {
			base.Reset ();
			
			startNode = null;
			endNode = null;
			startHint = null;
			endHint = null;
			originalStartPoint = Vector3.zero;
			originalEndPoint = Vector3.zero;
			startPoint = Vector3.zero;
			endPoint = Vector3.zero;
			calculatePartial = false;
			partialBestTarget = null;
			hasEndPoint = true;
			startIntPoint = new Int3();
			hTarget = new Int3();
			
			endNodeCosts = null;
		}
		
		/** Prepares the path. Searches for start and end nodes and does some simple checking if a path is at all possible */
		public override void Prepare () {
			
			AstarProfiler.StartProfile ("Get Nearest");
			
			//Initialize the NNConstraint
			nnConstraint.tags = enabledTags;
			NNInfo startNNInfo 	= AstarPath.active.GetNearest (startPoint,nnConstraint, startHint);
			
			//Tell the NNConstraint which node was found as the start node if it is a PathNNConstraint and not a normal NNConstraint
			PathNNConstraint pathNNConstraint = nnConstraint as PathNNConstraint;
			if (pathNNConstraint != null) {
				pathNNConstraint.SetStart (startNNInfo.node);
			}
			
			startPoint = startNNInfo.clampedPosition;
			
			startIntPoint = (Int3)startPoint;
			startNode = startNNInfo.node;
			
			//If it is declared that this path type has an end point
			//Some path types might want to use most of the ABPath code, but will not have an explicit end point at this stage
			if (hasEndPoint) {
				NNInfo endNNInfo = AstarPath.active.GetNearest (endPoint,nnConstraint, endHint);
				endPoint = endNNInfo.clampedPosition;
				
				// Note, other methods assume hTarget is (Int3)endPoint
				hTarget = (Int3)endPoint;
				endNode = endNNInfo.node;
			}
			
			AstarProfiler.EndProfile ();
			
			
			if (startNode == null && (hasEndPoint && endNode == null)) {
				Error ();
				LogError ("Couldn't find close nodes to the start point or the end point");
				return;
			}
			
			if (startNode == null) {
				Error ();
				LogError ("Couldn't find a close node to the start point");
				return;
			}
			
			if (endNode == null && hasEndPoint) {
				Error ();
				LogError ("Couldn't find a close node to the end point");
				return;
			}
			
			if (!startNode.Walkable) {
				Error ();
				LogError ("The node closest to the start point is not walkable");
				return;
			}
			
			if (hasEndPoint && !endNode.Walkable) {
				Error ();
				LogError ("The node closest to the end point is not walkable");
				return;
			}
			
			if (hasEndPoint && startNode.Area != endNode.Area) {
				Error ();
				LogError ("There is no valid path to the target (start area: "+startNode.Area+", target area: "+endNode.Area+")");
				return;
			}
		}
		
		public override void Initialize () {
			
			// Mark nodes to enable special connection costs for start and end nodes
			// See GetConnectionSpecialCost
			if (startNode != null) pathHandler.GetPathNode (startNode).flag2 = true;
			if (endNode != null) pathHandler.GetPathNode (endNode).flag2 = true;
			
			if (hasEndPoint && startNode == endNode) {
				PathNode endNodeR = pathHandler.GetPathNode (endNode);
				endNodeR.node = endNode;
				endNodeR.parent = null;
				endNodeR.H = 0;
				endNodeR.G = 0;
				Trace (endNodeR);
				CompleteState = PathCompleteState.Complete;
				return;
			}
			
			//Adjust the costs for the end node
			/*if (hasEndPoint && recalcStartEndCosts) {
				endNodeCosts = endNode.InitialOpen (open,hTarget,(Int3)endPoint,this,false);
				callback += ResetCosts; /* \todo Might interfere with other paths since other paths might be calculated before #callback is called *
			}*/
			
			PathNode startRNode = pathHandler.GetPathNode (startNode);
			startRNode.node = startNode;
			startRNode.pathID = pathHandler.PathID;
			startRNode.parent = null;
			startRNode.cost = 0;
			startRNode.G = GetTraversalCost (startNode);
			startRNode.H = CalculateHScore (startNode);
			
			/*if (recalcStartEndCosts) {
				startNode.InitialOpen (open,hTarget,startIntPoint,this,true);
			} else {*/
				startNode.Open (this,startRNode,pathHandler);
			//}
			
			searchedNodes++;
			
			partialBestTarget = startRNode;
			
			//any nodes left to search?
			if (pathHandler.HeapEmpty ()) {
				if (calculatePartial) {
					CompleteState = PathCompleteState.Partial;
					Trace (partialBestTarget);
				} else {
					Error ();
					LogError ("No open points, the start node didn't open any nodes");
					return;
				}
			}
			
			currentR = pathHandler.PopNode ();
		}
		
		public override void Cleanup () {
			if (startNode != null) pathHandler.GetPathNode (startNode).flag2 = false;
			if (endNode != null) pathHandler.GetPathNode (endNode).flag2 = false;
		}
		
		/** Calculates the path until completed or until the time has passed \a targetTick.
		 * Usually a check is only done every 500 nodes if the time has passed \a targetTick.
		 * Time/Ticks are got from System.DateTime.UtcNow.Ticks.
		 * 
		 * Basic outline of what the function does for the standard path (Pathfinding.ABPath).
\code
while the end has not been found and no error has ocurred
	check if we have reached the end
		if so, exit and return the path
	
	open the current node, i.e loop through its neighbours, mark them as visited and put them on a heap
	
	check if there are still nodes left to process (or have we searched the whole graph)
		if there are none, flag error and exit
		
	pop the next node of the heap and set it as current
	
	check if the function has exceeded the time limit
		if so, return and wait for the function to get called again
\endcode
		 */
		public override void CalculateStep (long targetTick) {
			
			int counter = 0;
			
			//Continue to search while there hasn't ocurred an error and the end hasn't been found
			while (CompleteState == PathCompleteState.NotCalculated) {
				
				searchedNodes++;
				
				//Close the current node, if the current node is the target node then the path is finished
				if (currentR.node == endNode) {
					CompleteState = PathCompleteState.Complete;
					break;
				}
				
				if (currentR.H < partialBestTarget.H) {
					partialBestTarget = currentR;
				}
				
				AstarProfiler.StartFastProfile (4);
				//Debug.DrawRay ((Vector3)currentR.node.Position, Vector3.up*2,Color.red);
				
				//Loop through all walkable neighbours of the node and add them to the open list.
				currentR.node.Open (this,currentR,pathHandler);
				
				AstarProfiler.EndFastProfile (4);
				
				//any nodes left to search?
				if (pathHandler.HeapEmpty()) {
					Error ();
					LogError ("No open points, whole area searched");
					return;
				}
				
				
				//Select the node with the lowest F score and remove it from the open list
				AstarProfiler.StartFastProfile (7);
				currentR = pathHandler.PopNode ();
				AstarProfiler.EndFastProfile (7);
				
				//Check for time every 500 nodes, roughly every 0.5 ms usually
				if (counter > 500) {
					
					//Have we exceded the maxFrameTime, if so we should wait one frame before continuing the search since we don't want the game to lag
					if (System.DateTime.UtcNow.Ticks >= targetTick) {
						//Return instead of yield'ing, a separate function handles the yield (CalculatePaths)
						return;
					}
					counter = 0;
					
					if (searchedNodes > 1000000) {
						throw new System.Exception ("Probable infinite loop. Over 1,000,000 nodes searched");
					}
				}
				
				counter++;
			}
			
			
			AstarProfiler.StartProfile ("Trace");
			
			if (CompleteState == PathCompleteState.Complete) {
				Trace (currentR);
			} else if (calculatePartial && partialBestTarget != null) {
				CompleteState = PathCompleteState.Partial;
				Trace (partialBestTarget);
			}
			
			AstarProfiler.EndProfile ();
			
		}
		
		/** Resets End Node Costs. Costs are updated on the end node at the start of the search to better reflect the end point passed to the path, the previous ones are saved in #endNodeCosts and are reset in this function which is called after the path search is complete */
		public void ResetCosts (Path p) {
		}
		
		/* String builder used for all debug logging */
		//public static System.Text.StringBuilder debugStringBuilder = new System.Text.StringBuilder ();
		
		/** Returns a debug string for this path.
		 */
		public override string DebugString (PathLog logMode) {
			
			if (logMode == PathLog.None || (!error && logMode == PathLog.OnlyErrors)) {
				return "";
			}
			
			//debugStringBuilder.Length = 0;
			
			System.Text.StringBuilder text = new System.Text.StringBuilder ();
			
			text.Append (error ? "Path Failed : " : "Path Completed : ");
			text.Append ("Computation Time ");
			
			text.Append ((duration).ToString (logMode == PathLog.Heavy ? "0.000" : "0.00"));
			text.Append (" ms Searched Nodes ");
			text.Append (searchedNodes);
			
			if (!error) {
				text.Append (" Path Length ");
				text.Append (path == null ? "Null" : path.Count.ToString ());
			
				if (logMode == PathLog.Heavy) {
					text.Append ("\nSearch Iterations "+searchIterations);
					
					if (hasEndPoint && endNode != null) {
						PathNode nodeR = pathHandler.GetPathNode(endNode);
						text.Append ("\nEnd Node\n	G: ");
						text.Append (nodeR.G);
						text.Append ("\n	H: ");
						text.Append (nodeR.H);
						text.Append ("\n	F: ");
						text.Append (nodeR.F);
						text.Append ("\n	Point: ");
						text.Append (((Vector3)endPoint).ToString ());
						text.Append ("\n	Graph: ");
						text.Append (endNode.GraphIndex);
					}
					
					text.Append ("\nStart Node");
					text.Append ("\n	Point: ");
					text.Append (((Vector3)startPoint).ToString ());
					text.Append ("\n	Graph: ");
					if (startNode != null) text.Append (startNode.GraphIndex);
					else text.Append ("< null startNode >");
					//text.Append ("\nBinary Heap size at completion: ");
					//text.Append (pathHandler.open == null ? "Null" : (pathHandler.open.numberOfItems-2).ToString ());// -2 because numberOfItems includes the next item to be added and item zero is not used
				}
				
				/*"\nEnd node\n	G = "+p.endNode.g+"\n	H = "+p.endNode.H+"\n	F = "+p.endNode.f+"\n	Point	"+p.endPoint
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
		
		protected override void Recycle () {
			PathPool<ABPath>.Recycle (this);
		}
		
		//Movement stuff
		
		/** Returns in which direction to move from a point on the path.
		 * A simple and quite slow (well, compared to more optimized algorithms) algorithm first finds the closest path segment (from #vectorPath) and then returns
		 * the direction to the next point from there. The direction is not normalized.
		 * \returns Direction to move from a \a point, returns Vector3.zero if #vectorPath is null or has a length of 0 */
		public Vector3 GetMovementVector (Vector3 point) {
			
			if (vectorPath == null || vectorPath.Count == 0) {
				return Vector3.zero;
			}
			
			if (vectorPath.Count == 1) {
				return vectorPath[0]-point;
			}
			
			float minDist = float.PositiveInfinity;//Mathf.Infinity;
			int minSegment = 0;
			
			for (int i=0;i<vectorPath.Count-1;i++) {
				
				Vector3 closest = AstarMath.NearestPointStrict (vectorPath[i],vectorPath[i+1],point);
				float dist = (closest-point).sqrMagnitude;
				if (dist < minDist) {
					minDist = dist;
					minSegment = i;
				}
			}
			
			return vectorPath[minSegment+1]-point;
		}
		
	}
}