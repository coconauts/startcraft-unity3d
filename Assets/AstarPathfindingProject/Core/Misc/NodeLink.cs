using UnityEngine;
using System.Collections;
using Pathfinding;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pathfinding {
	[AddComponentMenu("Pathfinding/Link")]
	public class NodeLink : GraphModifier {
		
		/** End position of the link */
		public Transform end;
		
		/** The connection will be this times harder/slower to traverse.
		 * Note that values lower than one will not always make the pathfinder choose this path instead of another path even though this one should
		  * lead to a lower total cost unless you also adjust the Heuristic Scale in A* Inspector -> Settings -> Pathfinding or disable the heuristic altogether.
		  */
		public float costFactor = 1.0f;
		
		/** Make a one-way connection */
		public bool oneWay = false;
		
		/** Delete existing connection instead of adding one */
		public bool deleteConnection = false;
		
		//private bool createHiddenNodes = true;
		
		public Transform Start {
			get { return transform; }
		}
		
		public Transform End {
			get { return end; }
		}
	
		public override void OnPostScan () {
			
			if (AstarPath.active.isScanning) {
				InternalOnPostScan ();
			} else {
				AstarPath.active.AddWorkItem (new AstarPath.AstarWorkItem (delegate (bool force) {
					InternalOnPostScan ();
					return true;
				}));
			}
		}
		
		public void InternalOnPostScan () {
			Apply ();
		}
	
		public override void OnGraphsPostUpdate () {
			//if (connectedNode1 != null && connectedNode2 != null) {
			if (!AstarPath.active.isScanning) {
				AstarPath.active.AddWorkItem (new AstarPath.AstarWorkItem (delegate (bool force) {
					InternalOnPostScan ();
					return true;
				}));
			}
		}
	
		public virtual void Apply () {
	//#if FALSE
			if (Start == null || End == null || AstarPath.active == null) return;
			
			GraphNode startNode = AstarPath.active.GetNearest (Start.position).node;
			GraphNode endNode = AstarPath.active.GetNearest (End.position).node;
			
			if (startNode == null || endNode == null) return;
			
			
			if (deleteConnection) {
				startNode.RemoveConnection (endNode);
				if (!oneWay)
					endNode.RemoveConnection (startNode);
			} else {
				uint cost = (uint)System.Math.Round ((startNode.position-endNode.position).costMagnitude*costFactor);
	
				startNode.AddConnection (endNode,cost);
				if (!oneWay)
					endNode.AddConnection (startNode,cost);
			}
	//#endif
		}
		
		public void OnDrawGizmos () {
			
			if (Start == null || End == null) return;
			
			Vector3 p1 = Start.position;
			Vector3 p2 = End.position;
			
			Gizmos.color = deleteConnection ? Color.red : Color.green;
			DrawGizmoBezier (p1,p2);
		}
		
		private void DrawGizmoBezier (Vector3 p1, Vector3 p2) {
			
			Vector3 dir = p2-p1;
			
			if (dir == Vector3.zero) return;
			
			Vector3 normal = Vector3.Cross (Vector3.up,dir);
			Vector3 normalUp = Vector3.Cross (dir,normal);
			
			normalUp = normalUp.normalized;
			normalUp *= dir.magnitude*0.1f;
			
			Vector3 p1c = p1+normalUp;
			Vector3 p2c = p2+normalUp;
			
			Vector3 prev = p1;
			for (int i=1;i<=20;i++) {
				float t = i/20.0f;
				Vector3 p = AstarMath.CubicBezier (p1,p1c,p2c,p2,t);
				Gizmos.DrawLine (prev,p);
				prev = p;
			}
		}
		
	#if UNITY_EDITOR
		[UnityEditor.MenuItem ("Edit/Pathfinding/Link Pair %&l")]
		public static void LinkObjects () {
			Transform[] tfs = Selection.transforms;
			if (tfs.Length == 2) {
				LinkObjects (tfs[0],tfs[1], false);
			}
			SceneView.RepaintAll ();
		}
		
		[UnityEditor.MenuItem ("Edit/Pathfinding/Unlink Pair %&u")]
		public static void UnlinkObjects () {
			Transform[] tfs = Selection.transforms;
			if (tfs.Length == 2) {
				LinkObjects (tfs[0],tfs[1], true);
			}
			SceneView.RepaintAll ();
		}
		
		[UnityEditor.MenuItem ("Edit/Pathfinding/Delete Links on Selected %&b")]
		public static void DeleteLinks () {
			Transform[] tfs = Selection.transforms;
			for (int i=0;i<tfs.Length;i++) {
				NodeLink[] conns = tfs[i].GetComponents<NodeLink> ();
				for (int j=0;j<conns.Length;j++) DestroyImmediate (conns[j]);
			}
			SceneView.RepaintAll ();
		}
		
		public static void LinkObjects (Transform a, Transform b, bool removeConnection) {
			NodeLink connecting = null;
			NodeLink[] conns = a.GetComponents<NodeLink> ();
			for (int i=0;i<conns.Length;i++) {
				if (conns[i].end == b) {
					connecting = conns[i];
					break;
				}
			}
			
			conns = b.GetComponents<NodeLink> ();
			for (int i=0;i<conns.Length;i++) {
				if (conns[i].end == a) {
					connecting = conns[i];
					break;
				}
			}
			
			if (removeConnection) {
				if (connecting != null) DestroyImmediate (connecting);
			} else {
				if (connecting == null) {
					connecting = a.gameObject.AddComponent<NodeLink> ();
					connecting.end = b;
				} else {
					connecting.deleteConnection = !connecting.deleteConnection;
				}
			}
		}
	#endif
	}
}