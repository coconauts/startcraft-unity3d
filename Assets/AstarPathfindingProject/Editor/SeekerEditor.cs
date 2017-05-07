//#define NoTagPenalty		//Enables or disables tag penalties. Can give small performance boost

#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_3_5 || UNITY_3_4 || UNITY_3_3
#define UNITY_LE_4_3
#endif

#if !UNITY_3_5 && !UNITY_3_4 && !UNITY_3_3
#define UNITY_4
#endif

using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

[CustomEditor(typeof(Seeker))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Seeker))]
public class SeekerEditor : Editor {
	
	public static bool modifiersOpen = false;
	public static bool tagPenaltiesOpen = false;
	
	List<IPathModifier> mods = null;
	public override void OnInspectorGUI () {
		DrawDefaultInspector ();
		
		Seeker script = target as Seeker;

#if !UNITY_LE_4_3
		Undo.RecordObject ( script, "modify settings on Seeker");
#endif

		EditorGUILayoutx.SetTagField (new GUIContent ("Valid Tags"),ref script.traversableTags);

		EditorGUI.indentLevel=0;
		tagPenaltiesOpen = EditorGUILayout.Foldout (tagPenaltiesOpen,new GUIContent ("Tag Penalties","Penalties for each tag"));
		if (tagPenaltiesOpen) {
			EditorGUI.indentLevel=2;
			string[] tagNames = AstarPath.FindTagNames ();
			for (int i=0;i<script.tagPenalties.Length;i++) {
				int tmp = EditorGUILayout.IntField ((i < tagNames.Length ? tagNames[i] : "Tag "+i),(int)script.tagPenalties[i]);
				if (tmp < 0) tmp = 0;
				script.tagPenalties[i] = tmp;
			}
			if (GUILayout.Button ("Edit Tag Names...")) {
				AstarPathEditor.EditTags ();
			}
		}
		EditorGUI.indentLevel=1;
		
		//Do some loading and checking
		if (!AstarPathEditor.stylesLoaded) {
			if (!AstarPathEditor.LoadStyles ()) {
				
				if (AstarPathEditor.upArrow == null) {
					AstarPathEditor.upArrow = GUI.skin.FindStyle ("Button");
					AstarPathEditor.downArrow = AstarPathEditor.upArrow;
				}
			} else {
				AstarPathEditor.stylesLoaded = true;
			}
		}

		GUIStyle helpBox = GUI.skin.GetStyle ("helpBox");


		if (mods == null) {
			mods = new List<IPathModifier>(script.GetComponents<MonoModifier>() as IPathModifier[]);
		} else {
			mods.Clear ();
			mods.AddRange (script.GetComponents<MonoModifier>() as IPathModifier[]);
		}
		
		mods.Add (script.startEndModifier as IPathModifier);
		
		bool changed = true;
		while (changed) {
			changed = false;
			for (int i=0;i<mods.Count-1;i++) {
				if (mods[i].Priority < mods[i+1].Priority) {
					IPathModifier tmp = mods[i+1];
					mods[i+1] = mods[i];
					mods[i] = tmp;
					changed = true;
				}
			}
		}
		
		for (int i=0;i<mods.Count;i++) {
			if (mods.Count-i != mods[i].Priority) {
				mods[i].Priority = mods.Count-i;
				GUI.changed = true;
				EditorUtility.SetDirty (target);
			}
		}
		
		bool modifierErrors = false;
		
		IPathModifier prevMod = mods[0];
		
		//Loops through all modifiers and checks if there are any errors in converting output between modifiers
		for (int i=1;i<mods.Count;i++) {
			MonoModifier monoMod = mods[i] as MonoModifier;
			if ((prevMod as MonoModifier) != null && !(prevMod as MonoModifier).enabled) {
				if (monoMod == null || monoMod.enabled) prevMod = mods[i];
				continue;
			}
			
			if ((monoMod == null || monoMod.enabled) && prevMod != mods[i] && !ModifierConverter.CanConvert (prevMod.output, mods[i].input)) {
				modifierErrors = true;
			}
			
			if (monoMod == null || monoMod.enabled) {
				prevMod = mods[i];
			}
		}
		
		EditorGUI.indentLevel = 0;
		
#if UNITY_LE_4_3
		modifiersOpen = EditorGUILayout.Foldout (modifiersOpen, "Modifiers Priorities"+(modifierErrors ? " - Errors in modifiers!" : ""),EditorStyles.foldout);
#else
		modifiersOpen = EditorGUILayout.Foldout (modifiersOpen, "Modifiers Priorities"+(modifierErrors ? " - Errors in modifiers!" : ""));
#endif

#if UNITY_LE_4_3
		EditorGUI.indentLevel = 1;
#endif

		if (modifiersOpen) {
#if UNITY_LE_4_3
			EditorGUI.indentLevel+= 2;
#endif

			//GUILayout.BeginHorizontal ();
			//GUILayout.Space (28);
			if (GUILayout.Button ("Modifiers attached to this gameObject are listed here.\nModifiers with a higher priority (higher up in the list) will be executed first.\nClick here for more info",helpBox)) {
				Application.OpenURL (AstarPathEditor.GetURL ("modifiers"));
			}


			EditorGUILayout.HelpBox ("Original or All can be converted to anything\n" +
			    "NodePath can be converted to VectorPath\n"+
				"VectorPath can only be used as VectorPath\n"+
			    "Vector takes both VectorPath and StrictVectorPath\n"+
			    "Strict... can be converted to the non-strict variant", MessageType.None );
			//GUILayout.EndHorizontal ();
			
			prevMod = mods[0];
			
			for (int i=0;i<mods.Count;i++) {
				
				//EditorGUILayout.LabelField (mods[i].GetType ().ToString (),mods[i].Priority.ToString ());
				MonoModifier monoMod = mods[i] as MonoModifier;
				
				Color prevCol = GUI.color;
				if (monoMod != null && !monoMod.enabled) {
					GUI.color *= new Color (1,1,1,0.5F);
				}
				
				GUILayout.BeginVertical (GUI.skin.box);
				
				if (i > 0) {
					
					
					if ((prevMod as MonoModifier) != null && !(prevMod as MonoModifier).enabled) {
						prevMod = mods[i];
					} else {
						
						if ((monoMod == null || monoMod.enabled) && !ModifierConverter.CanConvert (prevMod.output, mods[i].input)) {
							//GUILayout.BeginHorizontal ();
							//GUILayout.Space (28);
							GUIUtilityx.SetColor (new Color (0.8F,0,0));
							GUILayout.Label ("Cannot convert "+prevMod.GetType ().Name+"'s output to "+mods[i].GetType ().Name+"'s input\nRearranging the modifiers might help",EditorStyles.whiteMiniLabel);
							GUIUtilityx.ResetColor ();
							//GUILayout.EndHorizontal ();
						}
						
						if (monoMod == null || monoMod.enabled) {
							prevMod = mods[i];
						}
					}
				}
				
				GUILayout.Label ("Input: "+mods[i].input,EditorStyles.wordWrappedMiniLabel);
				int newPrio = EditorGUILayoutx.UpDownArrows (new GUIContent (ObjectNames.NicifyVariableName (mods[i].GetType ().ToString ())),mods[i].Priority, EditorStyles.label, AstarPathEditor.upArrow,AstarPathEditor.downArrow);
				
				GUILayout.Label ("Output: "+mods[i].output,EditorStyles.wordWrappedMiniLabel);
				
				GUILayout.EndVertical ();
				
				int diff = newPrio - mods[i].Priority;
				
				if (i > 0 && diff > 0) {
					mods[i-1].Priority = mods[i].Priority;
				} else if (i < mods.Count-1 && diff < 0) {
					mods[i+1].Priority = mods[i].Priority;
				}
				
				mods[i].Priority = newPrio;
				
				GUI.color = prevCol;
			}

#if UNITY_LE_4_3
			EditorGUI.indentLevel-= 2;
#endif
		}
	}
}