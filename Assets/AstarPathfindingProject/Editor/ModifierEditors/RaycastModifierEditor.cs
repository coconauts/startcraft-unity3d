using UnityEditor;

namespace Pathfinding {
	[CustomEditor(typeof(RaycastModifier))]
	[CanEditMultipleObjects]
	public class RaycastModifierEditor : EditorBase {
		protected override void Inspector () {
			PropertyField("quality");

			if (PropertyField("useRaycasting", "Use Physics Raycasting")) {
				EditorGUI.indentLevel++;

				PropertyField("use2DPhysics");
				if (PropertyField("thickRaycast")) {
					EditorGUI.indentLevel++;
					FloatField("thickRaycastRadius", min: 0f);
					EditorGUI.indentLevel--;
				}

				PropertyField("raycastOffset");
				PropertyField("mask", "Layer Mask");
				EditorGUI.indentLevel--;
			}

			PropertyField("useGraphRaycasting");
			if (FindProperty("useGraphRaycasting").boolValue) {
				EditorGUILayout.HelpBox("Graph raycasting is only available in the pro version for the built-in graphs.", MessageType.Info);
			}
			if (!FindProperty("useGraphRaycasting").boolValue && !FindProperty("useRaycasting").boolValue) {
				EditorGUILayout.HelpBox("You should use either raycasting, graph raycasting or both, otherwise this modifier will not do anything", MessageType.Warning);
			}
		}
	}
}
