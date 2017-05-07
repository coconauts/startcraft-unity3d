#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_4 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || UNITY_4_8 || UNITY_4_9
#define UNITY_4
#endif

#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_3_5 || UNITY_3_4 || UNITY_3_3
#define UNITY_LE_4_3
#endif

using UnityEngine;
using UnityEditor;
using System.Collections;
using Pathfinding;

namespace Pathfinding {
	[CustomGraphEditor (typeof(PointGraph),"PointGraph")]
	public class PointGraphEditor : GraphEditor {
	
		public override void OnInspectorGUI (NavGraph target) {
			PointGraph graph = target as PointGraph;
	
	/*
	#if UNITY_3_3
			graph.root = (Transform)EditorGUILayout.ObjectField (new GUIContent ("Root","All childs of this object will be used as nodes, if it is not set, a tag search will be used instead (see below)"),graph.root,typeof(Transform));
	#else
			graph.root = (Transform)EditorGUILayout.ObjectField (new GUIContent ("Root","All childs of this object will be used as nodes, if it is not set, a tag search will be used instead (see below)"),graph.root,typeof(Transform),true);
	#endif
	*/
			//Debug.Log (EditorGUI.indentLevel);
			
			graph.root = ObjectField (new GUIContent ("Root","All childs of this object will be used as nodes, if it is not set, a tag search will be used instead (see below)"),graph.root,typeof(Transform),true) as Transform;
			
			graph.recursive = EditorGUILayout.Toggle (new GUIContent ("Recursive","Should childs of the childs in the root GameObject be searched"),graph.recursive);
			graph.searchTag = EditorGUILayout.TagField (new GUIContent ("Tag","If root is not set, all objects with this tag will be used as nodes"),graph.searchTag);
	
	#if UNITY_4
			if (graph.root != null) {
				EditorGUILayout.HelpBox ("All childs "+(graph.recursive ? "and sub-childs ":"") +"of 'root' will be used as nodes\nSet root to null to use a tag search instead", MessageType.None);
			} else {
				EditorGUILayout.HelpBox ("All object with the tag '"+graph.searchTag+"' will be used as nodes"+(graph.searchTag == "Untagged" ? "\nNote: the tag 'Untagged' cannot be used" : ""), MessageType.None);
			}
	#else
			if (graph.root != null) {
				GUILayout.Label ("All childs "+(graph.recursive ? "and sub-childs ":"") +"of 'root' will be used as nodes\nSet root to null to use a tag search instead",AstarPathEditor.helpBox);
			} else {
				GUILayout.Label ("All object with the tag '"+graph.searchTag+"' will be used as nodes"+(graph.searchTag == "Untagged" ? "\nNote: the tag 'Untagged' cannot be used" : ""),AstarPathEditor.helpBox);
			}
	#endif
	
			graph.maxDistance = EditorGUILayout.FloatField (new GUIContent ("Max Distance","The max distance in world space for a connection to be valid. A zero counts as infinity"),graph.maxDistance);
	
	#if UNITY_LE_4_3
			EditorGUIUtility.LookLikeControls ();
	#endif
	#if UNITY_4
			graph.limits = EditorGUILayout.Vector3Field ("Max Distance (axis aligned)",graph.limits);
	#else
			EditorGUILayoutx.BeginIndent ();
			graph.limits = EditorGUILayout.Vector3Field ("Max Distance (axis aligned)",graph.limits);
			EditorGUILayoutx.EndIndent ();
	#endif
	#if UNITY_LE_4_3
			EditorGUIUtility.LookLikeInspector ();
	#endif
	
			graph.raycast = EditorGUILayout.Toggle (new GUIContent ("Raycast","Use raycasting to check if connections are valid between each pair of nodes"),graph.raycast);
			
			//EditorGUILayoutx.FadeArea fade = editor.GUILayoutx.BeginFadeArea (graph.raycast,"raycast");
			//if ( fade.Show () ) {
			if ( graph.raycast ) {
				EditorGUI.indentLevel++;
				
			 	graph.thickRaycast = EditorGUILayout.Toggle (new GUIContent ("Thick Raycast","A thick raycast checks along a thick line with radius instead of just along a line"),graph.thickRaycast);
			 	
			 	//editor.GUILayoutx.BeginFadeArea (graph.thickRaycast,"thickRaycast");
				if ( graph.thickRaycast ) {
			 		graph.thickRaycastRadius = EditorGUILayout.FloatField (new GUIContent ("Raycast Radius","The radius in world units for the thick raycast"),graph.thickRaycastRadius);
				}
			 	//editor.GUILayoutx.EndFadeArea ();
			 	
				//graph.mask = 1 << EditorGUILayout.LayerField ("Mask",(int)Mathf.Log (graph.mask,2));
				graph.mask = EditorGUILayoutx.LayerMaskField (/*new GUIContent (*/"Mask"/*,"Used to mask which layers should be checked")*/,graph.mask);
				EditorGUI.indentLevel--;
			}
	
			//editor.GUILayoutx.EndFadeArea ();

		}
		
		public override void OnDrawGizmos () {
				
			PointGraph graph = target as PointGraph;
			
			//Debug.Log ("Gizmos "+(graph == null)+" "+target);
			if (graph == null || !graph.active.showNavGraphs) {
				return;
			}
			
			//Handles.color = new Color (0.161F,0.341F,1F,0.5F);
			Gizmos.color = new Color (0.161F,0.341F,1F,0.5F);
			//for (int i=0;i<graph.nodes.Length;i++) {
			
			if (graph.root != null) {
				DrawChildren (graph, graph.root);
			} else {
				
				GameObject[] gos = GameObject.FindGameObjectsWithTag (graph.searchTag);
				for (int i=0;i<gos.Length;i++) {
					Gizmos.DrawCube (gos[i].transform.position,Vector3.one*HandleUtility.GetHandleSize(gos[i].transform.position)*0.1F);
				}
			}
		}
		
		public void DrawChildren (PointGraph graph, Transform tr) {
			foreach (Transform child in tr) {
				Gizmos.DrawCube (child.position,Vector3.one*HandleUtility.GetHandleSize(child.position)*0.1F);
				//Handles.CubeCap (-1,graph.nodes[i].position,Quaternion.identity,HandleUtility.GetHandleSize(graph.nodes[i].position)*0.1F);
				//Gizmos.DrawCube (nodes[i].position,Vector3.one);
				if (graph.recursive) DrawChildren (graph, child);
			}
		}
	}
}