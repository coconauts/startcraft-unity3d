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

[CustomEditor(typeof(SimpleSmoothModifier))]
public class SmoothModifierEditor : Editor {

	public override void OnInspectorGUI () {

#if UNITY_LE_4_3
		EditorGUI.indentLevel = 1;
#else
		EditorGUI.indentLevel = 0;
#endif

		SimpleSmoothModifier ob = target as SimpleSmoothModifier;
		
		ob.smoothType = (SimpleSmoothModifier.SmoothType)EditorGUILayout.EnumPopup (new GUIContent ("Smooth Type"),ob.smoothType);

#if UNITY_LE_4_3
		EditorGUIUtility.LookLikeInspector ();
#else
		Undo.RecordObject (ob, "changed settings on Simple Smooth Modifier");
#endif

		if (ob.smoothType == SimpleSmoothModifier.SmoothType.Simple) {
			
			ob.uniformLength = EditorGUILayout.Toggle (new GUIContent ("Uniform Segment Length","Toggle to divide all lines in equal length segments"),ob.uniformLength);
			
			if (ob.uniformLength) {
				ob.maxSegmentLength = EditorGUILayout.FloatField (new GUIContent ("Max Segment Length","The length of each segment in the smoothed path. A high value yields rough paths and low value yields very smooth paths, but is slower"),ob.maxSegmentLength);
				ob.maxSegmentLength = ob.maxSegmentLength < 0 ? 0 : ob.maxSegmentLength;
			} else {
				ob.subdivisions = EditorGUILayout.IntField (new GUIContent ("Subdivisions","The number of times to subdivide (divide in half) the path segments. [0...inf] (recommended [1...10])"),ob.subdivisions);
				if (ob.subdivisions < 0) ob.subdivisions = 0;
			}
			
			ob.iterations = EditorGUILayout.IntField (new GUIContent ("Iterations","Number of times to apply smoothing"),ob.iterations);
			ob.iterations = ob.iterations < 0 ? 0 : ob.iterations;
			
			ob.strength = EditorGUILayout.Slider (new GUIContent ("Strength","Determines how much smoothing to apply in each smooth iteration. 0.5 usually produces the nicest looking curves"),ob.strength,0.0F,1.0F);
			
		} else if (ob.smoothType == SimpleSmoothModifier.SmoothType.OffsetSimple) {
			
			ob.iterations = EditorGUILayout.IntField (new GUIContent ("Iterations","Number of times to apply smoothing"),ob.iterations);
			ob.iterations = ob.iterations < 0 ? 0 : ob.iterations;
			ob.iterations = ob.iterations > 12 ? 12 : ob.iterations;
			
			ob.offset = EditorGUILayout.FloatField (new GUIContent ("Offset","Offset to apply in each smoothing iteration"),ob.offset);
			if (ob.offset < 0) ob.offset = 0;
			
		} else if (ob.smoothType == SimpleSmoothModifier.SmoothType.Bezier) {
			
			ob.subdivisions = EditorGUILayout.IntField (new GUIContent ("Subdivisions","The number of times to subdivide (divide in half) the path segments. [0...inf] (recommended [1...10])"),ob.subdivisions);
			if (ob.subdivisions < 0) ob.subdivisions = 0;
			
			ob.bezierTangentLength = EditorGUILayout.FloatField (new GUIContent ("Tangent Length","Tangent length factor"),ob.bezierTangentLength);
			
		} else if (ob.smoothType == SimpleSmoothModifier.SmoothType.CurvedNonuniform) {
			ob.maxSegmentLength = EditorGUILayout.FloatField (new GUIContent ("Max Segment Length","The length of each segment in the smoothed path. A high value yields rough paths and low value yields very smooth paths, but is slower"),ob.maxSegmentLength);
			ob.maxSegmentLength = ob.maxSegmentLength < 0 ? 0 : ob.maxSegmentLength;
		} else {
			DrawDefaultInspector ();
		}
		
		//GUILayout.Space (5);
		
		Color preCol = GUI.color;
		GUI.color *= new Color (1,1,1,0.5F);
		ob.Priority = EditorGUILayout.IntField (new GUIContent ("Priority","Higher priority modifiers are executed first\nAdjust this in Seeker-->Modifier Priorities"),ob.Priority);
		GUI.color = preCol;

		if ( ob.gameObject.GetComponent<Seeker> () == null ) {
			EditorGUILayout.HelpBox ("No seeker found, modifiers are usually used together with a Seeker component", MessageType.Warning );
		}
	}
}