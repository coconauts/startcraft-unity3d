#if UNITY_4_2 || UNITY_4_1 || UNITY_4_0 || UNITY_3_5 || UNITY_3_4 || UNITY_3_3
	#define UNITY_LE_4_3
#endif


using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

namespace Pathfinding {
	[CustomEditor(typeof(AnimationLink))]
	public class AnimationLinkEditor : Editor {
	
		public override void OnInspectorGUI () {
	#if UNITY_LE_4_3
			EditorGUIUtility.LookLikeInspector ();
	#endif
			DrawDefaultInspector();
			
			AnimationLink script = target as AnimationLink;
			
			EditorGUI.BeginDisabledGroup(script.EndTransform == null);
			if (GUILayout.Button("Autoposition Endpoint")) {
				List<Vector3> buffer = Pathfinding.Util.ListPool<Vector3>.Claim();
				Vector3 endpos;
				script.CalculateOffsets(buffer, out endpos);
				script.EndTransform.position = endpos;
				Pathfinding.Util.ListPool<Vector3>.Release(buffer);
			}
			EditorGUI.EndDisabledGroup();
		}
	}
}