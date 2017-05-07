#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_3_5 || UNITY_3_4 || UNITY_3_3
#define UNITY_LE_4_3
#endif

#if !UNITY_3_5 && !UNITY_3_4 && !UNITY_3_3
#define UNITY_4
#endif

using UnityEngine;
using UnityEditor;
using System.Collections;
using Pathfinding;

[CustomEditor(typeof(RaycastModifier))]
public class RaycastModifierEditor : Editor {

	public override void OnInspectorGUI () {
		DrawDefaultInspector ();
		RaycastModifier ob = target as RaycastModifier;

#if UNITY_LE_4_3
		EditorGUI.indentLevel = 1;
#else
		EditorGUI.indentLevel = 0;
		Undo.RecordObject (ob, "modify settings on Raycast Modifier");
#endif
	
		if ( ob.iterations < 0 ) ob.iterations = 0;

		ob.useRaycasting = EditorGUILayout.Toggle (new GUIContent ("Use Physics Raycasting"),ob.useRaycasting);
		
		if (ob.useRaycasting) {
			EditorGUI.indentLevel++;
			ob.thickRaycast = EditorGUILayout.Toggle (new GUIContent ("Use Thick Raycast", "Checks around the line between two points, not just the exact line.\n" +
				"Make sure the ground is either too far below or is not inside the mask since otherwise the raycast might always hit the ground"), ob.thickRaycast);
			if ( ob.thickRaycast ) {
				EditorGUI.indentLevel++;
				ob.thickRaycastRadius = EditorGUILayout.FloatField (new GUIContent ("Thick Raycast Radius"), ob.thickRaycastRadius);
				if ( ob.thickRaycastRadius < 0 ) ob.thickRaycastRadius = 0;
				EditorGUI.indentLevel--;
			}
			
#if UNITY_LE_4_3
			ob.raycastOffset = EditorGUILayout.Vector3Field ("Raycast Offset", ob.raycastOffset);
#else
			ob.raycastOffset = EditorGUILayout.Vector3Field (new GUIContent ("Raycast Offset", "Offset from the original positions to perform the raycast.\n" +
				"Can be useful to avoid the raycast intersecting the ground or similar things you do not want to it intersect."), ob.raycastOffset);
#endif
			EditorGUILayout.PropertyField ( serializedObject.FindProperty("mask") );

			EditorGUI.indentLevel--;
		}

		ob.useGraphRaycasting = EditorGUILayout.Toggle (new GUIContent ("Use Graph Raycasting", "Raycasts on the graph to see if it hits any unwalkable nodes"), ob.useGraphRaycasting );

		ob.subdivideEveryIter = EditorGUILayout.Toggle (new GUIContent ("Subdivide Every Iteration", "Subdivides the path every iteration to be able to find shorter paths"), ob.subdivideEveryIter );

		Color preCol = GUI.color;
		GUI.color *= new Color (1,1,1,0.5F);
		ob.Priority = EditorGUILayout.IntField (new GUIContent ("Priority","Higher priority modifiers are executed first\nAdjust this in Seeker-->Modifier Priorities"),ob.Priority);
		GUI.color = preCol;

		if ( ob.gameObject.GetComponent<Seeker> () == null ) {
			EditorGUILayout.HelpBox ("No seeker found, modifiers are usually used together with a Seeker component", MessageType.Warning );
		}

	}
}