#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_3_5 || UNITY_3_4 || UNITY_3_3
#define UNITY_LE_4_3
#endif

using UnityEngine;
using UnityEditor;
using System.Collections;
using Pathfinding;

namespace Pathfinding {
	[CustomGraphEditor (typeof(NavMeshGraph),"NavMeshGraph")]
	public class NavMeshGraphEditor : GraphEditor {
		
		//public GameObject meshRenderer;
		
		public override void OnInspectorGUI (NavGraph target) {
			NavMeshGraph graph = target as NavMeshGraph;
	/*
	#if UNITY_3_3
			graph.sourceMesh = EditorGUILayout.ObjectField ("Source Mesh",graph.sourceMesh,typeof(Mesh)) as Mesh;
	#else
			graph.sourceMesh = EditorGUILayout.ObjectField ("Source Mesh",graph.sourceMesh,typeof(Mesh), true) as Mesh;
	#endif
	*/
			graph.sourceMesh = ObjectField ("Source Mesh", graph.sourceMesh, typeof(Mesh), false) as Mesh;
	
	#if UNITY_LE_4_3
			EditorGUIUtility.LookLikeControls ();
			EditorGUILayoutx.BeginIndent ();
	#endif
			graph.offset = EditorGUILayout.Vector3Field ("Offset",graph.offset);
	
	#if UNITY_LE_4_3
			EditorGUILayoutx.EndIndent ();
			
			EditorGUILayoutx.BeginIndent ();
	#endif
			graph.rotation = EditorGUILayout.Vector3Field ("Rotation",graph.rotation);
	
	#if UNITY_LE_4_3
			EditorGUILayoutx.EndIndent ();
			EditorGUIUtility.LookLikeInspector ();
	#endif
	
			graph.scale = EditorGUILayout.FloatField (new GUIContent ("Scale","Scale of the mesh"),graph.scale);
			graph.scale = (graph.scale < 0.01F && graph.scale > -0.01F) ? (graph.scale >= 0 ? 0.01F : -0.01F) : graph.scale;
			
			graph.accurateNearestNode = EditorGUILayout.Toggle (new GUIContent ("Accurate Nearest Node Queries","More accurate nearest node queries. See docs for more info"),graph.accurateNearestNode);
		}
		
		public override void OnSceneGUI (NavGraph target) {
			
			//NavMeshGraph graph = target as NavMeshGraph;
			
			/*if (meshRenderer == null) {
				Debug.Log ("IsNull");
				meshRenderer = new GameObject ("NavmeshRenderer");
				meshRenderer.hideFlags = HideFlags.HideAndDontSave;
				
				Renderer renderer = meshRenderer.AddComponent (typeof(MeshRenderer)) as Renderer;
				MeshFilter filter = meshRenderer.AddComponent (typeof(MeshFilter)) as MeshFilter;
				
				Mesh mesh = new Mesh ();
				mesh.vertices = graph.vertices;
				mesh.triangles = graph.triangles;
				
				mesh.RecalculateBounds ();
				mesh.RecalculateNormals ();
				
				filter.mesh = mesh;
				
				renderer.material = new Material (Shader.Find ("Transparent/Diffuse"));
				renderer.material.color = AstarColor.MeshColor;
			} else {
				Debug.Log ("Not Null "+meshRenderer.renderer.enabled+" "+meshRenderer.hideFlags);
				//meshRenderer.transform.position = new Vector3 (0,5,0);//meshRenderer.transform.position+Vector3.up*0.5F;
				meshRenderer.active = false;
				meshRenderer.active = true;
				
				
			}*/
			
			//DrawAALine (Vector3.zero,Vector3.one*20);
		}
	}
}