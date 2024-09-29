using UnityEngine;
using System.Linq;

namespace Pathfinding {
	/// <summary>
	/// Moves the target in example scenes.
	/// This is a simple script which has the sole purpose
	/// of moving the target point of agents in the example
	/// scenes for the A* Pathfinding Project.
	///
	/// It is not meant to be pretty, but it does the job.
	/// </summary>
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_target_mover.php")]
	public class TargetMover : MonoBehaviour {
		/// <summary>Mask for the raycast placement</summary>
		public LayerMask mask;

		public Transform target;
		IAstarAI[] ais;

		/// <summary>Determines if the target position should be updated every frame or only on double-click</summary>
		public bool onlyOnDoubleClick;
		public bool use2D;

		Camera cam;

		public void Start () {
			//Cache the Main Camera
			cam = Camera.main;
			// Slightly inefficient way of finding all AIs, but this is just an example script, so it doesn't matter much.
			// FindObjectsOfType does not support interfaces unfortunately.
			ais = FindObjectsOfType<MonoBehaviour>().OfType<IAstarAI>().ToArray();
			useGUILayout = false;
		}

		public void OnGUI () {
			if (onlyOnDoubleClick && cam != null && Event.current.type == EventType.MouseDown && Event.current.clickCount == 2) {
				UpdateTargetPosition();
			}
		}

		/// <summary>Update is called once per frame</summary>
		void Update () {
			if (!onlyOnDoubleClick && cam != null) {
				UpdateTargetPosition();
			}
		}

		public void UpdateTargetPosition () {
			Vector3 newPosition = Vector3.zero;
			bool positionFound = false;

			if (use2D) {
				newPosition = cam.ScreenToWorldPoint(Input.mousePosition);
				newPosition.z = 0;
				positionFound = true;
			} else {
				// Fire a ray through the scene at the mouse position and place the target where it hits
				RaycastHit hit;
				if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit, Mathf.Infinity, mask)) {
					newPosition = hit.point;
					positionFound = true;
				}
			}

			if (positionFound && newPosition != target.position) {
				target.position = newPosition;

				if (onlyOnDoubleClick) {
					for (int i = 0; i < ais.Length; i++) {
						if (ais[i] != null) ais[i].SearchPath();
					}
				}
			}
		}
	}
}
