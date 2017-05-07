using UnityEngine;
using System.Collections;
using Pathfinding.RVO;

namespace Pathfinding {
	public class TargetMover : MonoBehaviour {
		
		/** Mask for the raycast placement */
		public LayerMask mask;
		
		public Transform target;
		AIPath[] ais2;

		/** Determines if the target position should be updated every frame or only on double-click */
		public bool onlyOnDoubleClick;
		
		Camera cam;
		
		public void Start () {
			//Cache the Main Camera
			cam = Camera.main;
			ais2 = FindObjectsOfType(typeof(AIPath)) as AIPath[];
		}
		
		public void OnGUI () {
			
			if (onlyOnDoubleClick && cam != null && Event.current.type == EventType.MouseDown && Event.current.clickCount == 2) {
				UpdateTargetPosition ();
			}
		}
		
		// Update is called once per frame
		void Update () {
			
			if (!onlyOnDoubleClick && cam != null) {
				UpdateTargetPosition ();
			}
			
		}
		
		public void UpdateTargetPosition () {
			//Fire a ray through the scene at the mouse position and place the target where it hits
			RaycastHit hit;
			if (Physics.Raycast	(cam.ScreenPointToRay (Input.mousePosition), out hit, Mathf.Infinity, mask) && hit.point != target.position) {
				target.position = hit.point;
				
				if (ais2 != null && onlyOnDoubleClick) {
					for (int i=0;i<ais2.Length;i++) {
						if (ais2[i] != null) ais2[i].SearchPath ();
					}
				}
			}
		}
		
	}
}