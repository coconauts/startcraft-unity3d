using UnityEngine;
using System.Collections;
using Pathfinding;

namespace Pathfinding {
	[System.Serializable]
	/** Adjusts start and end points of a path.
	 * \ingroup modifiers
	 */
	public class StartEndModifier : PathModifier {
		
		public override ModifierData input {
			get { return ModifierData.Vector; }
		}
		
		public override ModifierData output {
			get { return (addPoints ? ModifierData.None : ModifierData.StrictVectorPath) | ModifierData.VectorPath; }
		}
		
		/** Add points to the path instead of replacing. */
		public bool addPoints = false;
		public Exactness exactStartPoint = Exactness.ClosestOnNode;
		public Exactness exactEndPoint = Exactness.ClosestOnNode;
		
		/** Sets where the start and end points of a path should be placed */
		public enum Exactness {
			SnapToNode,		/**< The point is snapped to the first/last node in the path*/
			Original,		/**< The point is set to the exact point which was passed when calling the pathfinding */
			Interpolate,	/**< The point is set to the closest point on the line between either the two first points or the two last points */
			ClosestOnNode	/**< The point is set to the closest point on the node. Note that for some node types (point nodes) the "closest point" is the node's position which makes this identical to Exactness.SnapToNode */
		}
		
		public bool useRaycasting = false;
		public LayerMask mask = -1;
		
		public bool useGraphRaycasting = false;
		
		/*public override void ApplyOriginal (Path p) {
			
			if (exactStartPoint) {
				pStart = GetClampedPoint (p.path[0].position, p.originalStartPoint, p.path[0]);
				
				if (!addPoints) {
					p.startPoint = pStart;
				}
			}
			
			if (exactEndPoint) {
				pEnd = GetClampedPoint (p.path[p.path.Length-1].position, p.originalEndPoint, p.path[p.path.Length-1]);
				
				if (!addPoints) {
					p.endPoint = pEnd;
				}
			}
		}*/
		
		public override void Apply (Path _p, ModifierData source) {
			
			ABPath p = _p as ABPath;
			
			//Only for ABPaths
			if (p == null) return;
			
			if (p.vectorPath.Count == 0) {
				return;
			} else if (p.vectorPath.Count < 2 && !addPoints) {
				//Vector3[] arr = new Vector3[2];
				//arr[0] = p.vectorPath[0];
				//arr[1] = p.vectorPath[0];
				//p.vectorPath = arr;
				p.vectorPath.Add (p.vectorPath[0]);
			}
			
			//Debug.DrawRay (p.originalEndPoint,Vector3.up,Color.red);
			//Debug.DrawRay (p.startPoint,Vector3.up,Color.red);
			//Debug.DrawRay (p.endPoint,Vector3.up,Color.green);
			
			Vector3 pStart = Vector3.zero,
			pEnd = Vector3.zero;
			
			if (exactStartPoint == Exactness.Original) {
				pStart = GetClampedPoint ((Vector3)p.path[0].position, p.originalStartPoint, p.path[0]);
			} else if (exactStartPoint == Exactness.ClosestOnNode) {
				pStart = GetClampedPoint ((Vector3)p.path[0].position, p.startPoint, p.path[0]);
			} else if (exactStartPoint == Exactness.Interpolate) {
				pStart = GetClampedPoint ((Vector3)p.path[0].position, p.originalStartPoint, p.path[0]);
				pStart = AstarMath.NearestPointStrict ((Vector3)p.path[0].position,(Vector3)p.path[1>=p.path.Count?0:1].position,pStart);
			} else {
				pStart = (Vector3)p.path[0].position;
			}
			
			if (exactEndPoint == Exactness.Original) {
				pEnd   = GetClampedPoint ((Vector3)p.path[p.path.Count-1].position, p.originalEndPoint, p.path[p.path.Count-1]);
			} else if (exactEndPoint == Exactness.ClosestOnNode) {
				pEnd = GetClampedPoint ((Vector3)p.path[p.path.Count-1].position, p.endPoint, p.path[p.path.Count-1]);
			} else if (exactEndPoint == Exactness.Interpolate) {
				pEnd   = GetClampedPoint ((Vector3)p.path[p.path.Count-1].position, p.originalEndPoint, p.path[p.path.Count-1]);
				
				pEnd = AstarMath.NearestPointStrict ((Vector3)p.path[p.path.Count-1].position,(Vector3)p.path[p.path.Count-2<0?0:p.path.Count-2].position,pEnd);
			} else {
				pEnd = (Vector3)p.path[p.path.Count-1].position;
			}
			
			if (!addPoints) {
				//p.vectorPath[0] = p.startPoint;
				//p.vectorPath[p.vectorPath.Length-1] = p.endPoint;
				//Debug.DrawLine (p.vectorPath[0],pStart,Color.green);
				//Debug.DrawLine (p.vectorPath[p.vectorPath.Length-1],pEnd,Color.green);
				p.vectorPath[0] = pStart;
				p.vectorPath[p.vectorPath.Count-1] = pEnd;
				
				
			} else {
				
				//Vector3[] newPath = new Vector3[p.vectorPath.Length+(exactStartPoint != Exactness.SnapToNode ? 1 : 0) + (exactEndPoint  != Exactness.SnapToNode ? 1 : 0)];
				
				if (exactStartPoint != Exactness.SnapToNode) {
					//newPath[0] = pStart;
					p.vectorPath.Insert (0,pStart);
				}
				
				if (exactEndPoint != Exactness.SnapToNode) {
					//newPath[newPath.Length-1] = pEnd;
					p.vectorPath.Add (pEnd);
				}
				
				/*int offset = exactStartPoint != Exactness.SnapToNode ? 1 : 0;
				for (int i=0;i<p.vectorPath.Length;i++) {
					newPath[i+offset] = p.vectorPath[i];
				}
				p.vectorPath = newPath;*/
			}
			
		}
		
		public Vector3 GetClampedPoint (Vector3 from, Vector3 to, GraphNode hint) {
			
			//float minDistance = Mathf.Infinity;
			Vector3 minPoint = to;
			
			if (useRaycasting) {
				RaycastHit hit;
				if (Physics.Linecast (from,to,out hit,mask)) {
					minPoint = hit.point;
					//minDistance = hit.distance;
				}
			}
			
			if (useGraphRaycasting && hint != null) {
				
				NavGraph graph = AstarData.GetGraph (hint);
				
				if (graph != null) {
					IRaycastableGraph rayGraph = graph as IRaycastableGraph;
					
					if (rayGraph != null) {
						GraphHitInfo hit;
						
						if (rayGraph.Linecast (from,minPoint, hint, out hit)) {
							
							//if ((hit.point-from).magnitude < minDistance) {
								minPoint = hit.point;
							//}
						}
					}
				}
			}
			
			return minPoint;
		}
		
	}
}