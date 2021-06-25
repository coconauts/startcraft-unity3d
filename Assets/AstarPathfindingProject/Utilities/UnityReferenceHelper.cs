using UnityEngine;

namespace Pathfinding {
	[ExecuteInEditMode]
	/// <summary>
	/// Helper class to keep track of references to GameObjects.
	/// Does nothing more than to hold a GUID value.
	/// </summary>
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_unity_reference_helper.php")]
	public class UnityReferenceHelper : MonoBehaviour {
		[HideInInspector]
		[SerializeField]
		private string guid;

		public string GetGUID () {
			return guid;
		}

		public void Awake () {
			Reset();
		}

		public void Reset () {
			if (string.IsNullOrEmpty(guid)) {
				guid = Pathfinding.Util.Guid.NewGuid().ToString();
				Debug.Log("Created new GUID - "+guid);
			} else {
				foreach (UnityReferenceHelper urh in FindObjectsOfType(typeof(UnityReferenceHelper)) as UnityReferenceHelper[]) {
					if (urh != this && guid == urh.guid) {
						guid = Pathfinding.Util.Guid.NewGuid().ToString();
						Debug.Log("Created new GUID - "+guid);
						return;
					}
				}
			}
		}
	}
}
