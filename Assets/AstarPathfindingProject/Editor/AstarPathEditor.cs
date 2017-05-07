//#define ASTARDEBUG   //"Enables some debugging messages"
//#define ProfileAstar //"Enables profiling of the pathfinder. Use the context menu to get log results"

#if UNITY_3_5 || UNITY_3_4 || UNITY_3_3
#define UNITY_BEFORE_4
#endif

#if UNITY_4_2 || UNITY_4_1 || UNITY_4_0 || UNITY_3_5 || UNITY_3_4 || UNITY_3_3
	#define UNITY_LE_4_3
#endif

using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Pathfinding;

[CustomEditor (typeof(AstarPath))]
public class AstarPathEditor : Editor {
	
	/** List of all graph editors available (e.g GridGraphEditor) */
	public static Dictionary<string,CustomGraphEditor> graphEditorTypes = new Dictionary<string,CustomGraphEditor> ();

	public static Dictionary<NavGraph,KeyValuePair<float,KeyValuePair<int,int> > > graphNodeCounts;


	/** List of all graph editors for graphs attached */
	public GraphEditor[] graphEditors;
	
	public System.Type[] graphTypes {
		get {
			return script.astarData.graphTypes;
		}
	}
	
#if !UNITY_LE_4_3
	static int lastUndoGroup = -1000;
#endif
	static int ignoreUndoHash = 0;

	/** Path to the editor assets folder for the A* Pathfinding Project. If this path turns out to be incorrect, the script will try to find the correct path
	  * \see LoadStyles */
	public static string editorAssets = "Assets/AstarPathfindingProject/Editor/EditorAssets";
	
	public static string scriptsFolder = "Assets/AstarPathfindingProject";
	
	/** Alternative path to the editor assets folder for the A* Pathfinding Project. \deprecated */
	[System.ObsoleteAttribute ("This is not used anymore, the folder is searched for if it cannot be found int the default location")]
	public static string alternativeEditorAssets = "Assets/AstarPathfindingEditor/Editor/EditorAssets";
	
	/** URL to the version file containing the latest version number. \deprecated */
	public static string updateURL = "http://www.arongranberg.com/astar/version.php";
	
	/** URL to the documentation of the A* Pathfinding Project. \deprecated */
	[System.ObsoleteAttribute ("Use GetURL () instead")]
	public static string documentationURL = "http://arongranberg.com/astar/docs/";

	/** URL to more info about the A* Pathfinding Project. \deprecated */
	[System.ObsoleteAttribute ("Use GetURL () instead")]
	public static string astarProInfoURL = "http://arongranberg.com/unity/a-pathfinding/astarpro/";
	
	/** URL to a page with more info on modifiers. \deprecated */
	[System.ObsoleteAttribute ("Use GetURL () instead")]
	public static string ModifierHelpURL = "http://www.arongranberg.com/astar/docs/modifiers.php";
	
	/** False if #astarServerData has not yet been updated with latest data.
	 * The data is got from the server on system update checks, it is cached in editor prefs.
	 */
	public static bool refreshedServerData = false;
	
	/** Holds various URLs and text for the editor.
	 * This info can be updated when a check for new versions is done to ensure that there are no invalid links. */
	public static Dictionary<string,string> astarServerData = new Dictionary<string, string> {
		{"URL:modifiers","http://www.arongranberg.com/astar/docs/modifiers.php"},
		{"URL:astarpro","http://arongranberg.com/unity/a-pathfinding/astarpro/"},
		{"URL:documentation","http://arongranberg.com/astar/docs/"},
		{"URL:findoutmore","http://arongranberg.com/astar"},
		{"URL:download","http://arongranberg.com/unity/a-pathfinding/download"},
		{"URL:cRecastHelp","http://www.arongranberg.com/astar/docs/class_pathfinding_1_1_recast_graph.php#a2d3655da3ed281674cf5125205e2a246"},
		{"URL:changelog","http://arongranberg.com/astar/docs/changelog.php"},
		{"URL:tags","http://arongranberg.com/astar/docs/tags.php"},
		{"URL:homepage","http://arongranberg.com/astar/"}
	};
	
	public static string GetURL (string tag) {
		if (!refreshedServerData) RefreshServerMessage ();
		string url = "";
		astarServerData.TryGetValue ("URL:"+tag,out url);
		return url;
	}
	
	public static string GetURL (string tag, string defaultURL) {
		if (!refreshedServerData) RefreshServerMessage ();
		string url = "";
		astarServerData.TryGetValue ("URL:"+tag,out url);
		if (string.IsNullOrEmpty (url)) return defaultURL;
		
		return url;
	}

	public static readonly string AstarProTooltip = "A* Pathfinding Project Pro only feature\nThe Pro version can be bought on the A* Pathfinding Project homepage,";
	public static readonly string AstarProButton  = "A* Pathfinding Project Pro only feature\nThe Pro version can be bought on the A* Pathfinding Project homepage, click here for info";
	public static WWW updateCheckObject;
	
	public static double updateCheckRate = 1F;//1.0F;	/** Number of days between update checks */
	public static System.Version latestAstarVersion; /**< Latest version of the A* Pathfinding Project */
	public static string latestAstarVersionDesc;/**< Description of the latest update of the A* Pathfinding Project */
	
	/** Toggle to use a darker skin which matches the Unity Pro dark skin */
	public static bool useDarkSkin = false;
	public static bool askForDarkSkin = false; /**< If the dark skin is detected, show a popup asking if the dark skin should be used */
	public static bool hasForcedNoDarkSkin = false; /**< True if the user answered No on #askForDarkSkin */
	
	public static bool firstRun = true;
	
#region SectionFlags
	
	bool showAddGraphMenu = false; /**< Is the 'Add New Graph' menu open */
	
	static bool showSettings = false;
	
	//static bool debugSettings = false;
	
	static bool colorSettings = false;
	static bool editorSettings = false;
	static bool linkSettings = false;
	static bool editLinks = false;
	static bool aboutArea = false;
	static bool optimizationSettings = false;
	static bool customAreaColorsOpen = false;
	static bool editTags = false;
	
	public static bool showSerializationSettings = false;
	
#endregion
	
	public static Pathfinding.Serialization.SerializeSettings serializationSettings = Pathfinding.Serialization.SerializeSettings.All;
	
	public AstarPath script;
	public EditorGUILayoutx GUILayoutx;
	
	//Styles
	
#region Styles
	
	public static bool stylesLoaded = false;
	
	public static GUISkin astarSkin;
	public static GUIStyle graphBoxStyle;
	public static GUIStyle topBoxHeaderStyle;
	public static GUIStyle graphDeleteButtonStyle;
	public static GUIStyle graphInfoButtonStyle;
	public static GUIStyle graphGizmoButtonStyle;
	public static GUIStyle helpBox;
	public static GUIStyle thinHelpBox;
	public static GUIStyle upArrow;
	public static GUIStyle downArrow;

#endregion
	//End Styles
	
	//Settings
	
	//End Settings
	
	//Misc
	
	private static System.DateTime _lastUpdateCheck;
	private static bool _lastUpdateCheckRead = false;

	public static System.DateTime lastUpdateCheck {
		get {
			try {
				// Reading from EditorPrefs is relatively slow, avoid it
				if ( _lastUpdateCheckRead ) return _lastUpdateCheck;

				_lastUpdateCheck = System.DateTime.Parse (EditorPrefs.GetString ("AstarLastUpdateCheck","1/1/1971 00:00:01"), System.Globalization.CultureInfo.InvariantCulture);
				_lastUpdateCheckRead = true;
			}
			catch (System.FormatException) {
				lastUpdateCheck = System.DateTime.UtcNow;
				Debug.LogWarning ("Invalid DateTime string encountered when loading from preferences");
			}
			return _lastUpdateCheck;
		}
		set {
			_lastUpdateCheck = value;
			EditorPrefs.SetString ("AstarLastUpdateCheck", _lastUpdateCheck.ToString ( System.Globalization.CultureInfo.InvariantCulture ));
		}
	}
	
	
	//End Misc
	
	
	/** Enables editor stuff. Loads graphs, reads settings and sets everything up */
	public void OnEnable () {
		
		script = target as AstarPath;
		GUILayoutx = new EditorGUILayoutx ();
		EditorGUILayoutx.editor = this;
		
		//Enables the editor to get a callback on OnDrawGizmos to enable graph editors to draw gizmos
		script.OnDrawGizmosCallback = OnDrawGizmos;
		
		// Make sure all references are set up to avoid NullReferenceExceptions
		script.SetUpReferences ();
		
		//Search the assembly for graph types and graph editors
		if ( graphEditorTypes == null || graphEditorTypes.Count == 0 )
			FindGraphTypes ();

		try {
			GetAstarEditorSettings ();
		} catch (System.Exception e) {
			Debug.LogException ( e );
		}

		LoadStyles ();
		
		//Load graphs only when not playing, or in extreme cases, when astarData.graphs is null
		if ((!Application.isPlaying && (script.astarData == null || script.astarData.graphs == null || script.astarData.graphs.Length == 0)) || script.astarData.graphs == null) {
			LoadGraphs ();
		}
	}

	/** Cleans up editor stuff */
	public void OnDisable () {
		
		if (target == null) {
			return;
		}
		
		SetAstarEditorSettings ();
		CheckGraphEditors ();
		
		//This doesn't get saved by Unity anyway for some reason
		//SerializeGraphs (new AstarSerializer (script));
		
		for (int i=0;i<graphEditors.Length;i++) {
			if (graphEditors[i] != null) graphEditors[i].OnDisable ();
			//graphEditors[i].OnDisableUndo ();
		}
		
		SaveGraphsAndUndo ();
	}
	
	public void OnDestroy () {
		if (graphEditors != null) {
			for (int i=0;i<graphEditors.Length;i++) {
				if (graphEditors[i] != null) graphEditors[i].OnDestroy ();
			}
		}
	}
	
	/** Reads settings frome EditorPrefs */
	public void GetAstarEditorSettings () {
		EditorGUILayoutx.fancyEffects = EditorPrefs.GetBool ("EditorGUILayoutx.fancyEffects",true);
		
		try {
			latestAstarVersion = new System.Version (EditorPrefs.GetString ("AstarLatestVersion",AstarPath.Version.ToString ()));
		}
		catch (System.Exception) {
			Debug.LogWarning ("Invalid last version number encountered when loading from preferences");
			latestAstarVersion = AstarPath.Version;
		}
		
		//latestAstarVersionURL = EditorPrefs.GetString ("AstarLatestVersionURL","http://arongranberg.com/unity/a-pathfinding/");
		latestAstarVersionDesc = EditorPrefs.GetString ("AstarLatestVersionDesc");
		useDarkSkin = EditorPrefs.GetBool ("AstarUseDarkSkin",false);
		hasForcedNoDarkSkin = EditorPrefs.GetBool ("AstarForcedNoDarkSkin",false);
		useDarkSkin = hasForcedNoDarkSkin ? useDarkSkin : EditorGUIUtility.isProSkin;
		askForDarkSkin = true;//EditorPrefs.GetInt ("UseDarkSkin",0) == 1;//Set by Unity
		
		editorAssets = EditorPrefs.GetString ("AstarEditorAssets",editorAssets);
		
		//Debug.Log ("Dark Skin : "+EditorPrefs.GetInt ("UseDarkSkin",0));
		
		//Check if this is the first run of the A* Pathfinding Project in this project
		string runBeforeProjects = EditorPrefs.GetString ("AstarUsedProjects","");
		
		string projectName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Application.dataPath));
		firstRun = !runBeforeProjects.Contains (projectName);
		
		RefreshServerMessage ();
	}
	
	public static void RefreshServerMessage () {
		string serverMessage = EditorPrefs.GetString ("AstarServerMessage");
		ParseServerMessage (serverMessage);
	}
	
	public void SetAstarEditorSettings () {
		EditorPrefs.SetBool ("EditorGUILayoutx.fancyEffects",EditorGUILayoutx.fancyEffects);;
		EditorPrefs.SetBool ("AstarUseDarkSkin",useDarkSkin);
		EditorPrefs.SetBool ("AstarForcedNoDarkSkin",hasForcedNoDarkSkin);
		EditorPrefs.SetString ("AstarEditorAssets",editorAssets);
		
		//EditorPrefs.SetInt ("UseDarkSkin",useDarkSkin ? 1 : 0);
	}
	
	/** Checks if JS support is enabled. This is done by checking if the directory 'Assets/AstarPathfindingEditor/Editor' exists */
	public static bool IsJsEnabled () {
		return System.IO.Directory.Exists (Application.dataPath+"/AstarPathfindingEditor/Editor");
	}
	
	/** Enables JS support. This is done by restructuring folders in the project */
	public void EnableJs () {
		
		//Path to the project folder (not with /Assets at the end)
		string projectPath = Application.dataPath;
		if (projectPath.EndsWith ("/Assets")) {
			projectPath = projectPath.Remove (projectPath.Length-("Assets".Length));
		}
		
		if (!System.IO.Directory.Exists (projectPath + scriptsFolder)) {
			string error = "Could not enable Js support. AstarPathfindingProject folder did not exist in the default location.\n" +
				"If you get this message and the AstarPathfindingProject is not at the root of your Assets folder (i.e at Assets/AstarPathfindingProject)" +
				" then you should move it to the root";
			
			Debug.LogError (error);
			EditorUtility.DisplayDialog ("Could not enable Js support",error,"ok");
			return;
		}
		
		if (!System.IO.Directory.Exists (Application.dataPath+"/AstarPathfindingEditor")) {
			System.IO.Directory.CreateDirectory (Application.dataPath+"/AstarPathfindingEditor");
			AssetDatabase.Refresh ();
		}
		if (!System.IO.Directory.Exists (Application.dataPath+"/Plugins")) {
			System.IO.Directory.CreateDirectory (Application.dataPath+"/Plugins");
			AssetDatabase.Refresh ();
		}
		
		
		AssetDatabase.MoveAsset (scriptsFolder + "/Editor","Assets/AstarPathfindingEditor/Editor");
		AssetDatabase.MoveAsset (scriptsFolder,"Assets/Plugins/AstarPathfindingProject");
		AssetDatabase.Refresh ();
	}
	
	/** Disables JS support if it was enabled. This is done by restructuring folders in the project */
	public void DisableJs () {
		
		if (System.IO.Directory.Exists (Application.dataPath+"/Plugins/AstarPathfindingProject")) {
			string error = AssetDatabase.MoveAsset ("Assets/Plugins/AstarPathfindingProject",scriptsFolder);
			if (error != "") {
				Debug.LogError ("Couldn't disable Js - "+error);
			} else {
				try {
					System.IO.Directory.Delete (Application.dataPath+"/Plugins");
				} catch (System.Exception) {}
			}
		} else {
			Debug.LogWarning ("Could not disable JS - Could not find directory '"+Application.dataPath+"/Plugins/AstarPathfindingProject'");
		}
		
		if (System.IO.Directory.Exists (Application.dataPath+"/AstarPathfindingEditor/Editor")) {
			string error = AssetDatabase.MoveAsset ("Assets/AstarPathfindingEditor/Editor",scriptsFolder + "/Editor");
			if (error != "") {
				Debug.LogError ("Couldn't disable Js - "+error);
			} else {
				try {
					System.IO.Directory.Delete (Application.dataPath+"/AstarPathfindingEditor");
				} catch (System.Exception) {}
			}
				
		} else {
			Debug.LogWarning ("Could not disable JS - Could not find directory '"+Application.dataPath+"/AstarPathfindingEditor/Editor'");
		}
		
		AssetDatabase.Refresh ();
	}
	
	/** Discards the first run window.
	 * It will not be shown for this project again */
	public static void DiscardFirstRun () {
		string runBeforeProjects = EditorPrefs.GetString ("AstarUsedProjects","");

		string projectName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Application.dataPath));
		if (!runBeforeProjects.Contains (projectName)) {
			runBeforeProjects += "|"+projectName;
		}
		EditorPrefs.SetString ("AstarUsedProjects",runBeforeProjects);

		firstRun = false;
	}
	
	/** Repaints Scene View.
	 * \warning Uses Undocumented Unity Calls (should be safe for Unity 3.x though) */
	public void RepaintSceneView () {
		if (!Application.isPlaying || EditorApplication.isPaused) SceneView.RepaintAll();
	}

#if !UNITY_LE_4_3
	/** Tell Unity that we want to use the whole inspector width */
	public override bool UseDefaultMargins ()
	{
		return false;
	}
#endif

	public override void OnInspectorGUI () {
		
		AstarProfiler.StartProfile ("OnInspectorGUI");

		//Do some loading and checking
		if (!stylesLoaded) {
			if (!LoadStyles ()) {
				GUILayout.Label ("The GUISkin 'AstarEditorSkin.guiskin' in the folder "+editorAssets+"/ was not found or some custom styles in it does not exist.\nThis file is required for the A* Pathfinding Project editor.\n\nIf you are trying to add A* to a new project, please do not copy the files outside Unity, export them as a UnityPackage and import them to this project or download the package from the Asset Store or the 'scripts only' package from the A* Pathfinding Project website.\n\n\nSkin loading is done in AstarPathEditor.cs --> LoadStyles function", "HelpBox");
				return;
			} else {
				stylesLoaded = true;
			}
		}

		bool preChanged = GUI.changed;
		GUI.changed = false;
		
		EditorGUILayoutx.editor = this;
		GUILayoutx.ClearStack ();
		
#if !UNITY_LE_4_3
		Undo.RecordObject (script,"A* inspector");
#endif
		
		AstarProfiler.StartProfile ("Check Updates and Editors");
		CheckForUpdates ();
		CheckGraphEditors ();
		AstarProfiler.EndProfile ("Check Updates and Editors");
		
		//End loading and checking
		
		AstarProfiler.StartProfile ("DrawMainArea");
		
		EditorGUI.indentLevel = 1;

#if UNITY_LE_4_3
		EditorGUIUtility.LookLikeInspector ();
#endif

		EventType eT = Event.current.type;
		
		DrawMainArea ();
		
		GUILayout.Space (5);
		
		if (GUILayout.Button (new GUIContent ("Scan", "Recaculate all graphs. Shortcut cmd+alt+s ( ctrl+alt+s on windows )"))) {
			MenuScan ();
		}
		
		
		AstarProfiler.EndProfile ("DrawMainArea");
		
		
		//bool reverted = HandleUndo ();
		
		//if (GUI.changed && !reverted) {
		//	SaveGraphs ();
		//}
		
		AstarProfiler.StartProfile ("Undo");

		// Handle undo at all times except during undo operations
		if ( Event.current.commandName != "UndoRedoPerformed" )
			SaveGraphsAndUndo (eT);
		
		AstarProfiler.EndProfile ("Undo");
		
		AstarProfiler.StartProfile ("Repaint");
		GUI.changed = preChanged || GUI.changed;

		if (GUI.changed) {
			RepaintSceneView ();
			EditorUtility.SetDirty ( script );
		}
		
		AstarProfiler.EndProfile ("Repaint");
		
		AstarProfiler.EndProfile ("OnInspectorGUI");

#if UNITY_LE_4_3
		EditorGUIUtility.LookLikeInspector ();
#endif
		//m_object.ApplyModifiedProperties ();
	}
	
	/** Loads GUISkin and sets up styles. \see #editorAssets
	  * \returns True if all styles were found, false if there was an error somewhere */
	public static bool LoadStyles () {
		
		
		//Correct paths if necessary
		
		string projectPath = Application.dataPath;
		if (projectPath.EndsWith ("/Assets")) {
			projectPath = projectPath.Remove (projectPath.Length-("Assets".Length));
		}
		
		if (!System.IO.File.Exists (projectPath + editorAssets + "/AstarEditorSkinLight.guiskin") && !System.IO.File.Exists (projectPath + editorAssets + "/AstarEditorSkin.guiskin")) {
			//Initiate search
			
			System.IO.DirectoryInfo sdir = new System.IO.DirectoryInfo (Application.dataPath);
			
			Queue<System.IO.DirectoryInfo> dirQueue = new Queue<System.IO.DirectoryInfo>();
			dirQueue.Enqueue (sdir);
			
			bool found = false;
			while (dirQueue.Count > 0) {
				System.IO.DirectoryInfo dir = dirQueue.Dequeue ();
				if (System.IO.File.Exists (dir.FullName + "/AstarEditorSkinLight.guiskin") || System.IO.File.Exists (dir.FullName + "/AstarEditorSkin.guiskin")) {
					string path = dir.FullName.Replace ('\\','/');
					found = true;
					//Remove data path from string to make it relative
					path = path.Replace (projectPath,"");
					
					if (path.StartsWith ("/")) {
						path = path.Remove (0,1);
					}
					
					editorAssets = path;
					Debug.Log ("Located editor assets folder to '"+editorAssets+"'");
					break;
				}
				System.IO.DirectoryInfo[] dirs = dir.GetDirectories ();
				for (int i=0;i<dirs.Length;i++) {
					dirQueue.Enqueue (dirs[i]);
				}
			}
			
			if (!found) {
				Debug.LogWarning ("Could not locate editor assets folder\nA* Pathfinding Project");
				return false;
			}
		}
		
		//End checks
		
		
		if (useDarkSkin) {
			astarSkin = AssetDatabase.LoadAssetAtPath (editorAssets + "/AstarEditorSkinDark.guiskin",typeof(GUISkin)) as GUISkin;
		} else {
			astarSkin = AssetDatabase.LoadAssetAtPath (editorAssets + "/AstarEditorSkinLight.guiskin",typeof(GUISkin)) as GUISkin;
		}
		
		/*if (astarSkin == null) {
			if (useDarkSkin) {
				astarSkin = AssetDatabase.LoadAssetAtPath (alternativeEditorAssets + "/AstarEditorSkinDark.guiskin",typeof(GUISkin)) as GUISkin;
			} else {
				astarSkin = AssetDatabase.LoadAssetAtPath (alternativeEditorAssets + "/AstarEditorSkinLight.guiskin",typeof(GUISkin)) as GUISkin;
			}
			
			if (astarSkin != null) {
				editorAssets = alternativeEditorAssets;
			}
		}*/
		
		GUISkin inspectorSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
		
		if (astarSkin != null) {
			astarSkin.button = inspectorSkin.button;
			//GUI.skin = astarSkin;
		} else {
			//Load skin at old path
			astarSkin = AssetDatabase.LoadAssetAtPath (editorAssets + "/AstarEditorSkin.guiskin",typeof(GUISkin)) as GUISkin;
			if (astarSkin != null) {
				AssetDatabase.RenameAsset (editorAssets + "/AstarEditorSkin.guiskin","AstarEditorSkinLight.guiskin");
			} else {
				return false;
			}
			//Error is shown in the inspector instead
			//Debug.LogWarning ("Couldn't find 'AstarEditorSkin' at '"+editorAssets + "/AstarEditorSkin.guiskin"+"'");
			
		}
		
		EditorGUILayoutx.defaultAreaStyle = astarSkin.FindStyle ("PixelBox");
		
		if (EditorGUILayoutx.defaultAreaStyle == null) {
			return false;
		}
		
		EditorGUILayoutx.defaultLabelStyle = astarSkin.FindStyle ("BoxHeader");
		topBoxHeaderStyle = astarSkin.FindStyle ("TopBoxHeader");

		graphBoxStyle = astarSkin.FindStyle ("PixelBox3");
		graphDeleteButtonStyle = astarSkin.FindStyle ("PixelButton");
		graphInfoButtonStyle = astarSkin.FindStyle ("InfoButton");
		graphGizmoButtonStyle = astarSkin.FindStyle ("GizmoButton");
		
		upArrow = astarSkin.FindStyle ("UpArrow");
		downArrow = astarSkin.FindStyle ("DownArrow");
	
		helpBox = inspectorSkin.FindStyle ("HelpBox") ?? inspectorSkin.FindStyle ("Box");
		
		thinHelpBox = new GUIStyle (helpBox);
		thinHelpBox.contentOffset = new Vector2 (0,-2);
		thinHelpBox.stretchWidth = false;
		thinHelpBox.clipping = TextClipping.Overflow;
		thinHelpBox.overflow.top += 1;
		
		return true;
	}
	
	/** Checks for updates if there was some time since last check.
	 * Usually called from OnInspectorGUI.
	 * It must be called repeatedly to ensure that the result is processed.
	 * \returns True if an update check is progressing (WWW request)
	 */
	public static bool CheckForUpdates () {

		if (updateCheckObject != null && updateCheckObject.isDone) {
			
			if (!string.IsNullOrEmpty (updateCheckObject.error)) {
				Debug.LogWarning ("There was an error checking for updates for the A* Pathfinding Project\n" +
				"The error might dissapear if you switch build target from Webplayer to Standalone because of the webplayer security emulation\nError: " +
				updateCheckObject.error);
				updateCheckObject = null;
				return false;
			}
			Debug.Log ("Got response\n" +updateCheckObject.text);
			UpdateCheckCompleted (updateCheckObject.text);
			updateCheckObject = null;
		}

		if (System.DateTime.Compare (lastUpdateCheck.AddDays (updateCheckRate),System.DateTime.UtcNow) < 0) {
			Debug.Log ("Checking For Updates... " + System.DateTime.UtcNow.ToString (System.Globalization.CultureInfo.InvariantCulture)+"\nA* Pathfinding Project");

			bool use = AstarPath.active != null || GameObject.FindObjectOfType(typeof(AstarPath)) != null;
			bool mecanim = false;
#if !UNITY_BEFORE_4
			mecanim = GameObject.FindObjectOfType(typeof(Animator)) != null;
#endif
			string query = updateURL+"?v="+AstarPath.Version.ToString()+"&pro="+(AstarPath.HasPro ? "1":"0")+
				"&check="+updateCheckRate+"&distr="+AstarPath.Distribution+
				"&unitypro="+(Application.HasProLicense()?"1":"0")+
				"&inscene="+(use?"1":"0")+
				"&targetplatform="+EditorUserBuildSettings.activeBuildTarget.ToString()+
				"&devplatform="+Application.platform.ToString()+
				"&mecanim="+(mecanim?"1":"0")+
				"&unityversion="+Application.unityVersion+
				"&branch="+AstarPath.Branch;
			updateCheckObject = new WWW (query);
			lastUpdateCheck = System.DateTime.UtcNow;
		}

		return updateCheckObject != null;
	}
	
	/** Handles the data from the update page */
	public static void UpdateCheckCompleted (string result) {
		
		ParseServerMessage (result);
	}
	
	public static void ParseServerMessage (string result) {
		if (string.IsNullOrEmpty (result)) {
			return;
		}
		
		
		string[] splits = result.Split ('|');
		string versionString = splits[0];
		string descriptionString = splits.Length > 1 ? splits[1] : "";
		string url = splits.Length > 2 ? splits[2] : "http://arongranberg.com/astar/";
		/*Debug.Log (updateCheckObject.text);
		if (splits.Length > 3) {
			Debug.Log ("Split 3 "+splits[3]);
		}*/
		
		System.Version newVersion = null;
		
		try {
			newVersion = new System.Version (versionString);
		} catch (System.Exception ex) {
			Debug.LogWarning ("Couldn't parse version string. Version string to parse: "+versionString+", error: "+ex.ToString ());
			updateCheckObject = null;
			return;
		}
		
		if (splits.Length > 3) {
			int numKeys = 0;
			int.TryParse (splits[3],out numKeys);
			
			if (splits.Length >= 4+numKeys*2) {
				for (int i=0;i<numKeys;i++) {
					string key = splits[4+i*2];
					string val = splits[4+i*2+1];
					if (!astarServerData.ContainsKey (key)) {
						astarServerData.Add (key,val);
					} else {
						astarServerData[key] = val;
					}
				}
			}
			
		}
		
		//Debug.Log ("New version exists "+newVersion+ " Desc: "+descriptionString);
		EditorPrefs.SetString ("AstarLatestVersion",newVersion.ToString ());
		EditorPrefs.SetString ("AstarLatestVersionDesc",descriptionString);
		EditorPrefs.SetString ("AstarLatestVersionURL",url);
		EditorPrefs.SetString ("AstarServerMessage",result);
		
		latestAstarVersion = newVersion;
		latestAstarVersionDesc = descriptionString;

		System.DateTime remindDate = System.DateTime.UtcNow;
		try {
			System.Version remindVersion = new System.Version (EditorPrefs.GetString ("AstarRemindUpdateVersion", "0.0.0.0"));
			if (newVersion == remindVersion && System.DateTime.TryParse (EditorPrefs.GetString ("AstarRemindUpdateDate", "1/1/1971 00:00:01"), out remindDate)) {
				if ( System.DateTime.UtcNow < remindDate ) {
					//Debug.Log ("Not reminding about A* PathfindiSaveng Project Update yet. " + System.Math.Round ((remindDate-System.DateTime.UtcNow).TotalDays) + " days left");
					return;
				}
			} else {
				EditorPrefs.DeleteKey ("AstarRemindUpdateDate");
				EditorPrefs.DeleteKey ("AstarRemindUpdateVersion");
			}
		} catch {
			Debug.LogError ("Invalid AstarRemindUpdateVersion or AstarRemindUpdateDate");
		}

		System.Version skipVersion = new System.Version (EditorPrefs.GetString ("AstarSkipUpToVersion", AstarPath.Version.ToString()));

		if ( newVersion != skipVersion && newVersion > AstarPath.Version ) {
			EditorPrefs.DeleteKey ("AstarSkipUpToVersion");
			EditorPrefs.DeleteKey ("AstarRemindUpdateDate");
			EditorPrefs.DeleteKey ("AstarRemindUpdateVersion");

			AstarUpdateWindow window = AstarUpdateWindow.Init ();
			window.version = newVersion;
			window.summary = descriptionString;
			window.downloadURL = url;
			window.raw = result;
		} else {
			//Debug.Log ("Skipping this version. " + newVersion.ToString () + " < " + skipVersion.ToString());
		}
	}
	
	/** Draws the first run dialog.
	 * Asks if the user wants to enable JS support */
	public void DrawFirstRun () {
		if (!firstRun) {
			return;
		}
		
		if (IsJsEnabled ()) {
			DiscardFirstRun ();
			return;
		}
		
		GUILayoutx.BeginFadeArea (true,"Do you want to enable Javascript support?","enableJs");
#if UNITY_BEFORE_4
		GUILayout.Label ("Folders can be restructured to enable pathfinding calls from Js\n" +
			"This setting can be edited later in Settings-->Editor",helpBox);
#else
		EditorGUILayout.HelpBox ("Folders can be restructured to enable pathfinding calls from Js\n" +
		                 "This setting can be edited later in Settings-->Editor", MessageType.Info);
#endif
		GUILayout.BeginHorizontal ();
		if (GUILayout.Button ("Yes")) {
			EnableJs ();
		}
		if (GUILayout.Button ("No")) {
			DiscardFirstRun ();
		}
		GUILayout.EndHorizontal ();
		GUILayoutx.EndFadeArea ();
	}
	
	/** Draws a dialog asking the user if he/she wants to use a dark skin */
	public void DrawDarkSkinDialog () {
#if UNITY_3_4
		if (!askForDarkSkin || hasForcedNoDarkSkin || useDarkSkin) {
			return;
		}
		
		GUILayoutx.BeginFadeArea (true,"Use Dark Skin?","enableDarkSkin");
		GUILayout.Label ("It looks like the rest of Unity uses a dark skin\n" +
			"Do you want to use it for the A* plugin too?\n" +
			"This can be changed later in Settings-->Editor",helpBox);
		GUILayout.BeginHorizontal ();
		
		if (GUILayout.Button ("Yes")) {
			useDarkSkin = true;
			askForDarkSkin = false;
			hasForcedNoDarkSkin = false;
			LoadStyles ();
		}
		if (GUILayout.Button ("No")) {
			useDarkSkin = false;
			askForDarkSkin = false;
			hasForcedNoDarkSkin = true;
			LoadStyles ();
		}
		GUILayout.EndHorizontal ();
		GUILayoutx.EndFadeArea ();
#endif
	}
	
	/** Draws the main area in the inspector */
	public void DrawMainArea () {
		
		AstarProfiler.StartProfile ("Draw Graphs");
		
		DrawFirstRun ();
		DrawDarkSkinDialog ();
		
		//Show the graph inspectors
		CheckGraphEditors ();

		EditorGUILayoutx.FadeArea graphsFadeArea = GUILayoutx.BeginFadeArea (script.showGraphs,"Graphs", "showGraphInspectors", EditorGUILayoutx.defaultAreaStyle, topBoxHeaderStyle);
		script.showGraphs = graphsFadeArea.open;

		if ( graphsFadeArea.Show () ) {
			for (int i=0;i<script.graphs.Length;i++) {
				
				NavGraph graph = script.graphs[i];
				
				if (graph == null) continue;
				
				GraphEditor editor = graphEditors[i];
				
				if (editor == null) continue;
				
				if (DrawGraph (graph, editor)) {
					return;
				}
			}
			
			AstarProfiler.EndProfile ("Draw Graphs");
			
			AstarProfiler.StartProfile ("Draw Add New Graph");
			//Draw the Add Graph buttons
			showAddGraphMenu = GUILayoutx.BeginFadeArea (showAddGraphMenu || script.graphs.Length == 0, "Add New Graph","AddNewGraph",graphBoxStyle);
			if ( graphTypes == null ) script.astarData.FindGraphTypes ();
			for (int i=0;i<graphTypes.Length;i++) {
				if (graphEditorTypes.ContainsKey (graphTypes[i].Name)) {
					if (GUILayout.Button (graphEditorTypes[graphTypes[i].Name].displayName)) {
						showAddGraphMenu = false;
						AddGraph (graphTypes[i]);
						//OnSceneGUI ();
					}
				} else {
					bool preEnabled = GUI.enabled;
					GUI.enabled = false;
					GUILayout.Label (graphTypes[i].Name + " (no editor found)","Button");
					GUI.enabled = preEnabled;
				}
			}
			AstarProfiler.EndProfile ("Draw Add New Graph");
			GUILayoutx.EndFadeArea ();

		
			if (script.astarData.data_backup != null && script.astarData.data_backup.Length != 0) {
				GUILayoutx.BeginFadeArea (true, "Backup data detected","backupData",graphBoxStyle);
	#if UNITY_BEFORE_4
				GUILayout.Label ("Backup data was found, this can have been stored because there was an error during deserialization. Check the log.\n" +
					"If you load again and everything goes well, you can discard the backup data\n" +
					"When trying to load again, the deserializer will ignore version differences (for example 3.0 would try to load 3.0.1 files)\n" +
					"The backup data is stored in AstarData.data_backup",helpBox);
	#else
				EditorGUILayout.HelpBox ("Backup data was found, this can have been stored because there was an error during deserialization. Check the log.\n" +
				                 "If you load again and everything goes well, you can discard the backup data\n" +
				                 "When trying to load again, the deserializer will ignore version differences (for example 3.0 would try to load 3.0.1 files)\n" +
				                 "The backup data is stored in AstarData.data_backup", MessageType.Warning);
	#endif
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Try loading data again")) {
					if (script.astarData.graphs == null || script.astarData.graphs.Length == 0
					    	|| EditorUtility.DisplayDialog ("Do you want to load from backup data?",
					                                           "Are you sure you want to load from backup data?\nThis will delete your current graphs.",
					                                           "Yes",
					                                           "Cancel")) {	
						script.astarData.SetData(script.astarData.data_backup,0);
						LoadGraphs ();
					}
				}
				if (GUILayout.Button ("Discard backup data")) {
					script.astarData.data_backup = null;
				}
				GUILayout.EndHorizontal ();
				GUILayoutx.EndFadeArea ();
			}
		}

		GUILayoutx.EndFadeArea ();

		/*AstarProfiler.StartProfile ("DrawLinkSettings");
		DrawLinkSettings ();
		
		AstarProfiler.EndProfile ("DrawLinkSettings");*/
		
		AstarProfiler.StartProfile ("DrawSettings");
		//Draw the settings area
		DrawSettings ();
		
		AstarProfiler.EndProfile ("DrawSettings");
		
		AstarProfiler.StartProfile ("DrawSerializationSettings");
		DrawSerializationSettings ();
		
		AstarProfiler.EndProfile ("DrawSerializationSettings");
		
		AstarProfiler.StartProfile ("DrawOptimizationSettings");
		DrawOptimizationSettings ();
		
		AstarProfiler.EndProfile ("DrawOptimizationSettings");
		
		AstarProfiler.StartProfile ("DrawAboutArea");
		DrawAboutArea ();
		
		AstarProfiler.EndProfile ("DrawAboutArea");
		
		AstarProfiler.StartProfile ("Show Graphs");
		bool showNavGraphs = EditorGUILayout.Toggle ("Show Graphs",script.showNavGraphs);
		if (script.showNavGraphs != showNavGraphs) {
			script.showNavGraphs = showNavGraphs;
			RepaintSceneView ();
		}
		AstarProfiler.EndProfile ("Show Graphs");
	}
	
	/** Draws optimizations settings.
	 * \astarpro */
	public void DrawOptimizationSettings () {
		EditorGUILayoutx.FadeArea fadeArea = GUILayoutx.BeginFadeArea (optimizationSettings,"Optimization","optimization", EditorGUILayoutx.defaultAreaStyle, topBoxHeaderStyle);
		optimizationSettings = fadeArea.open;

		if ( fadeArea.Show () ) {
			

			GUIUtilityx.SetColor (Color.Lerp (Color.yellow,Color.white,0.5F));
			if (GUILayout.Button ("Optimizations is an "+AstarProButton,helpBox)) {
				Application.OpenURL (GetURL ("astarpro"));//astarProInfoURL);
			}
			GUIUtilityx.ResetColor ();
		}
		
		GUILayoutx.EndFadeArea ();
		
	}
	
	public void DrawAboutArea () {
		
		Color tmp1 = GUI.color;
		EditorGUILayoutx.FadeArea fadeArea = GUILayoutx.BeginFadeArea (aboutArea,"aboutArea", 20,EditorGUILayoutx.defaultAreaStyle);
		Color tmp2 = GUI.color;
		GUI.color = tmp1;
		
		GUILayout.BeginHorizontal ();
		
		if (GUILayout.Button ("About", topBoxHeaderStyle )) {
			aboutArea = !aboutArea;
			GUI.changed = true;
		}
		
		if (latestAstarVersion > AstarPath.Version) {
			tmp1 *= Color.green;
			GUI.color = tmp1;
			if (GUILayout.Button ("New Version Available! "+latestAstarVersion.ToString (),thinHelpBox,GUILayout.Height (15))) {
				Application.OpenURL (GetURL ("findoutmore"));
			}
			GUILayout.Space (20);
		}
		
		GUILayout.EndHorizontal ();
		
		GUI.color = tmp2;
		
		if (fadeArea.Show ()) {
			GUILayout.Label ("The A* Pathfinding Project was made by Aron Granberg\nYour current version is "+AstarPath.Version.ToString ());
			
			if (latestAstarVersion > AstarPath.Version) {
#if UNITY_BEFORE_4
				GUILayout.Label ("A new version of the A* Pathfinding Project is available, the new version is "+latestAstarVersion.ToString ()+(latestAstarVersionDesc != null && latestAstarVersionDesc != "" ? "\n"+latestAstarVersionDesc : ""),helpBox);
#else
				EditorGUILayout.HelpBox ("A new version of the A* Pathfinding Project is available, the new version is "+latestAstarVersion.ToString ()+(latestAstarVersionDesc != null && latestAstarVersionDesc != "" ? "\n"+latestAstarVersionDesc : ""), MessageType.Info);
#endif
				
				if (GUILayout.Button ("What's new?")) {
					Application.OpenURL (GetURL ("changelog"));
				}
				
				if (GUILayout.Button ("Click here to find out more")) {
					Application.OpenURL (GetURL ("findoutmore"));
				}
				
				Color tmp3 = GUI.color;
				tmp2 *= new Color (0.3F,0.9F,0.3F);
				GUI.color = tmp2;
				
				if (GUILayout.Button ("Download new version")) {
					Application.OpenURL (GetURL ("download"));
				}
				
				GUI.color = tmp3;
			}
			
			if (GUILayout.Button (new GUIContent ("Documentation","Open the documentation for the A* Pathfinding Project"))) {
				Application.OpenURL (GetURL ("documentation"));
			}

			if (GUILayout.Button (new GUIContent ("Project Homepage","Open the homepage for the A* Pathfinding Project"))) {
				Application.OpenURL (GetURL ("homepage"));
			}
		}
		
		GUILayoutx.EndFadeArea ();
	}
	
	public void DrawLinkSettings () {
		//linkSettings = GUILayoutx.BeginFadeArea (linkSettings,"Links", );
		
		Color tmp1 = GUI.color;
		
		EditorGUILayoutx.FadeArea fadeArea = GUILayoutx.BeginFadeArea (linkSettings,"Links", "linkSettings", EditorGUILayoutx.defaultAreaStyle, topBoxHeaderStyle);

		if ( linkSettings != fadeArea.open ) {
			linkSettings = fadeArea.open;
			RepaintSceneView ();
		}

		if ( fadeArea.Show () ) {
			Color tmp2 = GUI.color;
			GUI.color = tmp1;

			/*if (GUILayout.Button ("Links",EditorGUILayoutx.defaultLabelStyle)) {
				linkSettings = !linkSettings;

			}*/
			
			GUI.color = tmp2;
			
			GUILayout.Label ("Links connect two nodes and makes a direct path between them possible.",helpBox);
			
			editLinks = GUILayout.Toggle (editLinks,"Edit Links","Button");
			
			//EditorGUIUtility.LookLikeControls ();
			
			GUILayout.Label ("You can edit links in the scene view either by clicking the Add Connection button or by holding shift and clicking on two nodes.",helpBox);
		}

		GUILayoutx.EndFadeArea ();
	}
	
	public bool DrawGraph (NavGraph graph, GraphEditor graphEditor) {
		
		Color tmp1 = GUI.color;
		EditorGUILayoutx.FadeArea topFadeArea = GUILayoutx.BeginFadeArea (graph.open,"","graph_"+graph.guid,graphBoxStyle);


		Color tmp2 = GUI.color;
		GUI.color = tmp1;
		
		GUILayout.BeginHorizontal ();
		string graphNameControl = "graph_"+graph.guid+"_name";
		if (graph.name == null) graph.name = graphEditorTypes[graph.GetType ().Name].displayName;
		
		GUI.SetNextControlName (graphNameControl);
		graph.name = GUILayout.TextField (graph.name, EditorGUILayoutx.defaultLabelStyle, GUILayout.ExpandWidth(false),GUILayout.ExpandHeight(false));
		
		if (graph.name == "" && Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != graphNameControl) {
			graph.name = graphEditorTypes[graph.GetType ().Name].displayName;
		}
		
		if (GUILayout.Button ("",EditorGUILayoutx.defaultLabelStyle)) {
			graph.open = !graph.open;
			if (!graph.open) {
				graph.infoScreenOpen = false;
			}
			RepaintSceneView ();
			return true;
		}
		
		if (script.prioritizeGraphs) {
			if (GUILayout.Button (new GUIContent ("Up","Increase the graph priority"),GUILayout.Width (40))) {
				int index = script.astarData.GetGraphIndex (graph);
				
				//Find the next non null graph
				int next = index-1;
				for (;next >= 0;next--) if (script.graphs[next] != null) break;
				
				if (next >= 0) {
					NavGraph tmp = script.graphs[next];
					script.graphs[next] = graph;
					script.graphs[index] = tmp;
					
					GraphEditor tmpEditor = graphEditors[next];
					graphEditors[next] = graphEditors[index];
					graphEditors[index] = tmpEditor;
				}
				CheckGraphEditors ();
				Repaint ();
			}
			if (GUILayout.Button (new GUIContent ("Down","Decrease the graph priority"),GUILayout.Width (40))) {
				int index = script.astarData.GetGraphIndex (graph);
				
				//Find the next non null graph
				int next = index+1;
				for (;next<script.graphs.Length;next++) if (script.graphs[next] != null) break;
				
				if (next < script.graphs.Length) {
					NavGraph tmp = script.graphs[next];
					script.graphs[next] = graph;
					script.graphs[index] = tmp;
					
					GraphEditor tmpEditor = graphEditors[next];
					graphEditors[next] = graphEditors[index];
					graphEditors[index] = tmpEditor;
				}
				CheckGraphEditors ();
				Repaint ();
			}
		}
		
		bool drawGizmos = GUILayout.Toggle (graph.drawGizmos,"Draw Gizmos",graphGizmoButtonStyle);
		if (drawGizmos != graph.drawGizmos) {
			graph.drawGizmos = drawGizmos;
			RepaintSceneView ();
		}
		
		if (GUILayout.Toggle (graph.infoScreenOpen,"Info",graphInfoButtonStyle)) {
			if (!graph.infoScreenOpen) {
				graph.infoScreenOpen = true;
				graph.open = true;
			}
		} else {
			graph.infoScreenOpen = false;
		}
		
		if (GUILayout.Button ("Delete",graphDeleteButtonStyle)) {
			RemoveGraph (graph);
			return true;
		}
		GUILayout.EndHorizontal ();


		if (topFadeArea.Show () ) {
			EditorGUILayoutx.FadeArea fadeArea = GUILayoutx.BeginFadeArea (graph.infoScreenOpen,"graph_info_"+graph.guid,0);
			if (fadeArea.Show ()) {
				
				bool nodenull = false;
				int total = 0;
				int numWalkable = 0;

				KeyValuePair<float,KeyValuePair<int,int>> pair;
				if ( graphNodeCounts == null ) graphNodeCounts = new Dictionary<NavGraph, KeyValuePair<float, KeyValuePair<int, int>>>();

				if ( !graphNodeCounts.TryGetValue ( graph, out pair ) || (Time.realtimeSinceStartup-pair.Key) > 2 ) {
					GraphNodeDelegateCancelable counter = delegate (GraphNode node) {
						if (node == null) {
							nodenull = true;
							return true;
						}
						total++;
						if (node.Walkable) numWalkable++;
						return true;
					};
					graph.GetNodes (counter);
					pair = new KeyValuePair<float, KeyValuePair<int, int>> (Time.realtimeSinceStartup, new KeyValuePair<int,int>( total, numWalkable ) );
					graphNodeCounts[graph] = pair;
				}

				total = pair.Value.Key;
				numWalkable = pair.Value.Value;

			
				EditorGUI.indentLevel++;
				
				if (nodenull) {
					//EditorGUILayout.HelpBox ("Some nodes in the graph are null. Please report this error.", MessageType.Info);
					Debug.LogError ("Some nodes in the graph are null. Please report this error.");
				}
				
				EditorGUILayout.LabelField ("Nodes",total.ToString());
				EditorGUILayout.LabelField ("Walkable",numWalkable.ToString ());
				EditorGUILayout.LabelField ("Unwalkable",(total-numWalkable).ToString ());
				if (total == 0) EditorGUILayout.HelpBox ("The number of nodes in the graph is zero. The graph might not be scanned",MessageType.Info);
				
				EditorGUI.indentLevel--;
			}
			GUILayoutx.EndFadeArea ();
			
			GUI.color = tmp2;
			
			graphEditor.OnInspectorGUI (graph);
			graphEditor.OnBaseInspectorGUI (graph);
		}

		GUILayoutx.EndFadeArea ();
		
		return false;
	}
	
	public void OnSceneGUI () {
		
		//AstarProfiler.StartProfile ("OnSceneGUI");
		
		bool preChanged = GUI.changed;
		GUI.changed = false;
		
		script = target as AstarPath;
		
		AstarPath.active = script;
		
		if (!stylesLoaded) {
			LoadStyles ();
			return;
		}
		
		//Some GUI controls might change this to Used, so we need to grab it here
		EventType et = Event.current.type;
		
		CheckGraphEditors ();
		for (int i=0;i<script.graphs.Length;i++) {
			
			NavGraph graph = script.graphs[i];
			
			if (graph == null || graphEditors.Length <= i) {
				continue;
			}
			
			graphEditors[i].OnSceneGUI (graph);
		}
		
		
		DrawUserConnections ();
		
		SaveGraphsAndUndo (et);
		
		if (GUI.changed) {
			EditorUtility.SetDirty (target);
		} else {
			GUI.changed = preChanged;
		}
		
		//AstarProfiler.EndProfile ("OnSceneGUI");
		
	}
	
	
	public int selectedUserConnection = -1;
	
	public GraphNode firstShiftNode;
	
	public void DrawUserConnections () {
		
		
		UserConnection[] conns = script.astarData.userConnections;
		
		if (conns == null) {
			conns = new UserConnection[0];
		}
		
		Rect r = new Rect(Screen.width - 180, Screen.height - 300, 168,252);
		
		if (editLinks) {
			//Add a small border around the window + add space for the header
			Rect r2 = r;
			r2.yMin -= 30;
			r2.xMin -= 10;
			r2.xMax += 10;
			r2.yMax += 10;
			Vector2 mouse = Event.current.mousePosition;
			
			if (r2.Contains (mouse) && Event.current.type == EventType.Layout) {
				int controlID = GUIUtility.GetControlID(1024,FocusType.Passive);
				HandleUtility.AddControl (controlID,0F);
			}
			
			if (Event.current.shift) {
				
				Ray ray = HandleUtility.GUIPointToWorldRay (Event.current.mousePosition);
				
				GraphNode node = script.GetNearest (ray);
				
				if (firstShiftNode != null) {
					Handles.color = Color.yellow;
					Handles.SphereCap (GUIUtility.GetControlID (FocusType.Passive),(Vector3)firstShiftNode.position,Quaternion.identity,HandleUtility.GetHandleSize ((Vector3)node.position)*0.12F);
				}
				
				if (node != null) {
					Handles.color = Color.yellow;
					Handles.SphereCap (GUIUtility.GetControlID (FocusType.Passive),(Vector3)node.position,Quaternion.identity,HandleUtility.GetHandleSize ((Vector3)node.position)*0.13F);
					
					if (firstShiftNode != null) {
						Handles.DrawLine ((Vector3)firstShiftNode.position,(Vector3)node.position);
					}
					
					if (Event.current.type == EventType.MouseDown && Event.current.button == 0) {
						if (firstShiftNode == null) {
							firstShiftNode = node;
						} else {
							
							//Add a connection between 'node' and firstShiftNode
							selectedUserConnection = CreateNewUserConnection ((Vector3)firstShiftNode.position,(Vector3)node.position);
							
							firstShiftNode = null;
							Event.current.Use ();
						}
					}
				} else {
					Handles.BeginGUI ();
					GUI.Box (new Rect (Event.current.mousePosition.x+20,Event.current.mousePosition.y-2,115,18),"No graphs are scanned",helpBox);
					//GUI.Label (new Rect (Event.current.mousePosition.x+20,Event.current.mousePosition.y-5,150,30),);
					Handles.EndGUI ();
				}
				GUI.changed = true;
			} else {
				firstShiftNode = null;
			}
		}
		
		for (int i=0;i<conns.Length;i++) {
			UserConnection conn = conns[i];
			
			int controlID = GUIUtility.GetControlID(i, FocusType.Native);
			
			if (Event.current.type == EventType.Layout && editLinks) {
				if (conn.type == ConnectionType.Connection) {
					HandleUtility.AddControl (controlID,HandleUtility.DistanceToLine (conn.p1,conn.p2)*0.5F);
				} else {
					HandleUtility.AddControl (controlID,HandleUtility.DistanceToLine (conn.p1,conn.p1)*0.5F);
				}
			}
			
			if (selectedUserConnection == i && editLinks) {
				
				conn.p1 = Handles.PositionHandle (conn.p1,Quaternion.identity);
				
				if (conn.type == ConnectionType.Connection) {
					conn.p2 = Handles.PositionHandle (conn.p2,Quaternion.identity);
				}
				
				Handles.color = new Color (0.603F,0.95F,0.28F,0.8F);
				
			} else {
				
				Handles.color = new Color (0.290F, 0.454F, 0.741F, 0.800F);
			}
			
			if (Event.current.type == EventType.MouseDown && editLinks && firstShiftNode == null) {
				if (HandleUtility.nearestControl == controlID) {
					selectedUserConnection = i;
					Event.current.Use ();
					HandleUtility.Repaint ();
				}
			}
			
			if (conn.type == ConnectionType.Connection) {
				if (conn.oneWay) {
					//Because of nothing... Why not?
					Vector3 goldenRatio = (conn.p2-conn.p1)*0.618F;
					
					Handles.ConeCap (controlID,conn.p1+goldenRatio,Quaternion.LookRotation (goldenRatio),goldenRatio.magnitude*0.07F);
					Handles.ConeCap (controlID,conn.p2-goldenRatio,Quaternion.LookRotation (goldenRatio),goldenRatio.magnitude*0.07F);
					Handles.DrawLine (conn.p1,conn.p2);
				} else {
					Handles.DrawLine (conn.p1,conn.p2);
				}
			
				if (conn.enable) {
					//Nice Blue Color
					Handles.color = new Color (0.290F, 0.454F, 0.741F, 0.600F);
				} else {
					//Nice Red Color
					Handles.color = new Color (0.651F, 0.125F, 0F, 0.6F);
				}
				
				Handles.SphereCap (controlID,conn.p1,Quaternion.identity,HandleUtility.GetHandleSize (conn.p1)*0.1F);
				Handles.SphereCap (controlID,conn.p2,Quaternion.identity,HandleUtility.GetHandleSize (conn.p2)*0.1F);
			} else {
				if (conn.enable) {
					//Nice Blue Color
					Handles.color = new Color (0.290F, 0.454F, 0.741F, 0.600F);
				} else {
					//Nice Red Color
					Handles.color = new Color (0.651F, 0.125F, 0F, 0.6F);
				}
				
				Handles.SphereCap (controlID,conn.p1,Quaternion.identity,HandleUtility.GetHandleSize (conn.p1)*0.15F);
			}
		}
		
		if (Event.current.type == EventType.Layout && linkSettings) {
			int cID = GUIUtility.GetControlID(654, FocusType.Passive);
			HandleUtility.AddDefaultControl (cID);
		}
		
		if (editLinks) {
			Handles.BeginGUI();
			//GUILayout.Window (0,r,DrawUserConnectionsWindow,"Connection");
			GUILayout.BeginArea (r,"Connection","Window");
			DrawUserConnectionsWindow ();
			GUILayout.EndArea ();
			if (GUI.Button (new Rect (r.x,r.y,16,16),"",astarSkin.FindStyle ("CloseButton"))) {
				editLinks = false;
				Repaint ();
			}
			
			Handles.EndGUI();
		} else {
			
			Handles.BeginGUI();
			
			if (GUI.Button (new Rect (Screen.width-40,Screen.height-75,30,30),"Show Links",astarSkin.FindStyle ("LinkButton"))) {
				editLinks = true;
				Repaint ();
				GUI.changed = true;
			}
			
			Handles.EndGUI();
		}
		
		//Debug.Log("id: " + GUIUtility.hotControl+" "+GUIUtility.keyboardControl+ " "+Event.current.GetTypeForControl (GUIUtility.hotControl) +" Name : "+GUI.GetNameOfFocusedControl ());
		
		/*if (Event.current.type == EventType.MouseDown) {
			if (HandleUtility.nearestControl > 0 && HandleUtility.nearestControl <= conns.Length && selectedUserConnection != HandleUtility.nearestControl-1) {
				selectedUserConnection = HandleUtility.nearestControl-1;
				HandleUtility.nearestControl = 0;
				HandleUtility.Repaint ();
				//return;
			}
		}*/
	}
	
	public void DrawUserConnectionsWindow () {
		
		UserConnection[] conns = script.astarData.userConnections;

		if ( conns == null ) return;

		if (selectedUserConnection >= 0 && selectedUserConnection < conns.Length && Event.current.type != EventType.Used) {
			
			UserConnection conn = conns[selectedUserConnection];
			
			if (Event.current.keyCode == KeyCode.Backspace && Event.current.type == EventType.KeyDown) {
				RemoveConnection (conn);
				return;
			}
			
			conn.type = (ConnectionType)EditorGUILayout.Popup ((int)conn.type,new string[2] {"Connection","Modify Node"});
			if (conn.type == ConnectionType.Connection) {
				conn.p1 = EditorGUILayout.Vector3Field (conn.oneWay ? "Start" : "Point 1",conn.p1);
				conn.p2 = EditorGUILayout.Vector3Field (conn.oneWay ? "End" : "Point 2",conn.p2);
				
				
				conn.enable = EditorGUILayout.Toggle (new GUIContent ("Enable/Disable","Should the connection between the nodes be enabled or disabled"),conn.enable);
				conn.oneWay = EditorGUILayout.Toggle (new GUIContent ("One Way","Should this connection take affect in both ways or only in one direction"),conn.oneWay);
				
				EditorGUIUtility.LookLikeControls (70,120);
				
				GUILayout.BeginHorizontal ();
				
				
				if (!conn.doOverrideCost) {
					
					GUI.enabled = false;
					EditorGUILayout.IntField (new GUIContent ("Cost","Cost of the Connection\nDefault cost is "+Int3.Precision+" per world unit"),((Int3)(conn.p1-conn.p2)).costMagnitude,GUILayout.MaxWidth (138));
				} else {
					
					conn.overrideCost = EditorGUILayout.IntField (new GUIContent ("Cost","Cost of the Connection\nDefault cost is "+Int3.Precision+" per world unit"),conn.overrideCost,GUILayout.MaxWidth (138));
				}
				GUI.enabled = true;
				
				GUILayout.FlexibleSpace ();
				conn.doOverrideCost = GUILayout.Toggle (conn.doOverrideCost,new GUIContent ("","Override the connection cost"));
				GUILayout.EndHorizontal ();
			} else {
				
				conn.p1 = EditorGUILayout.Vector3Field ("Point",conn.p1);
				
				EditorGUIUtility.LookLikeControls (70,120);
				
				int walkability = EditorGUILayout.Popup ("Walkability",!conn.doOverrideWalkability ? 0 : (conn.enable ? 2 : 1),new string[3] {"Dont change","Unwalkable","Walkable"});
				if (walkability == 0) {
					conn.doOverrideWalkability = false;
					conn.enable = true;
				} else {
					conn.doOverrideWalkability = true;
					conn.enable = walkability == 2;
				}
				
				GUILayout.BeginHorizontal ();
				
				
				if (!conn.doOverridePenalty) {
					
					GUI.enabled = false;
					EditorGUILayout.IntField (new GUIContent ("Penalty","Penalty of the node"),0,GUILayout.MaxWidth (138));
				} else {
					conn.overridePenalty = (uint)EditorGUILayout.IntField (new GUIContent ("Penalty","Penalty of the node"),(int)conn.overridePenalty,GUILayout.MaxWidth (138));
				}
				GUI.enabled = true;
				
				GUILayout.FlexibleSpace ();
				conn.doOverridePenalty = GUILayout.Toggle (conn.doOverridePenalty,new GUIContent ("","Change node penalty"));
				GUILayout.EndHorizontal ();
				
				//conn.enable = EditorGUILayout.Toggle (new GUIContent ("Walkability","Should the node be set to walkable or unwalkable"),conn.enable);
			}
			
			if(GUILayout.Button("Snap to closest nodes")) {
				GraphNode node1 = script.GetNearest (conn.p1).node;
				GraphNode node2 = script.GetNearest (conn.p2).node;
				
				if (node1 != null) conn.p1 = (Vector3)node1.position;
				if (node2 != null) conn.p2 = (Vector3)node2.position;
				
			}
			
			if (GUILayout.Button ("Remove")) {
				RemoveConnection (conn);
				return;
			}

#if UNITY_LE_4_3
			EditorGUIUtility.LookLikeInspector ();
#endif
		}
		
		GUILayout.FlexibleSpace ();
		
		if(GUILayout.Button("Add Connection")) {
			selectedUserConnection = CreateNewUserConnection (Vector3.zero, Vector3.one);
		}
	}
	
	/** Removes user connection \a conn from the script.astarData.userConnections array */
	void RemoveConnection (UserConnection conn) {
		UserConnection[] conns = script.astarData.userConnections;
		List<UserConnection> connList = new List<UserConnection>(conns);
		connList.Remove (conn);
		script.astarData.userConnections = connList.ToArray ();
		selectedUserConnection = -1;
		HandleUtility.Repaint ();
		RepaintSceneView ();
		//Use the Event to avoid the annoying system beep (indicating a key which can't be used here, so we inform the system that it can... it prevents the beep anyway).
		Event.current.Use ();
		GUI.changed = true;
	}
	
	public void DrawSerializationSettings () {
		
		AstarProfiler.StartProfile ("Serialization step 1");
		
		Color tmp1 = GUI.color;
		EditorGUILayoutx.FadeArea fadeArea = GUILayoutx.BeginFadeArea (showSerializationSettings, "serializationSettings", 20, EditorGUILayoutx.defaultAreaStyle);
		showSerializationSettings = fadeArea.open;

		Color tmp2 = GUI.color;
		GUI.color = tmp1;

		GUILayout.BeginHorizontal ();

		if (GUILayout.Button ("Save & Load", topBoxHeaderStyle )) {
			showSerializationSettings = !showSerializationSettings;
			GUI.changed = true;
		}

		if (script.astarData.cacheStartup && script.astarData.data_cachedStartup != null && script.astarData.data_cachedStartup.Length > 0) {
			tmp1 *= Color.yellow;
			GUI.color = tmp1;
			
			GUILayout.Label ("Startup cached",thinHelpBox,GUILayout.Height (15));
			
			GUILayout.Space (20);
			
		}
		
		GUI.color = tmp2;
		
		GUILayout.EndHorizontal ();

		if ( fadeArea.Show () ) {
			
			AstarProfiler.EndProfile ("Serialization step 1");
			
			AstarProfiler.StartProfile ("SerializationSettings.OnGUI");
			/* This displays the values of the serialization settings */
			serializationSettings.nodes =  UnityEditor.EditorGUILayout.Toggle ("Save Node Data", serializationSettings.nodes);
			serializationSettings.prettyPrint = UnityEditor.EditorGUILayout.Toggle (new GUIContent ("Pretty Print","Format Json data for readability. Yields slightly smaller files when turned off"),serializationSettings.prettyPrint);
			
			AstarProfiler.EndProfile ("SerializationSettings.OnGUI");
			
			AstarProfiler.StartProfile ("Cache Startup");
			GUILayout.Space (5);
			
			bool preEnabled = GUI.enabled;
			
			script.astarData.cacheStartup = EditorGUILayout.Toggle (new GUIContent ("Cache startup","If enabled, will cache the graphs so they don't have to be scanned at startup"),script.astarData.cacheStartup);
			
			tmp1 = GUI.color;
			if (script.astarData.cacheStartup && (script.astarData.data_cachedStartup == null || script.astarData.data_cachedStartup.Length == 0)) {
				GUI.color = Color.red;
			}
			
			EditorGUILayout.LabelField ("Cache size",(script.astarData.data_cachedStartup != null ? EditorUtility.FormatBytes (script.astarData.data_cachedStartup.Length) : "null"));
			
			GUI.color = tmp1;
			
			GUILayout.BeginHorizontal ();
			
			if (GUILayout.Button ("Generate cache")) {
				if (EditorUtility.DisplayDialog ("Scan before generating cache?","Do you want to scan the graphs before saving the cache","Scan","Don't scan")) {
					MenuScan ();
				}
				script.astarData.SaveCacheData (serializationSettings);
			}
			
			GUI.enabled = script.astarData.data_cachedStartup != null; 
			if (GUILayout.Button ("Load from cache")) {
				if (EditorUtility.DisplayDialog ("Are you sure you want to load from cache?","Are you sure you want to load graphs from the cache, this will replace your current graphs?","Yes","Cancel")) {
					script.astarData.LoadFromCache ();
				}
			}
			
			GUILayout.EndHorizontal ();
			
			AstarProfiler.EndProfile ("Cache Startup");
			
			AstarProfiler.StartProfile ("Clear Cache");
			if (GUILayout.Button ("Clear Cache", GUILayout.MaxWidth (120))) {
				script.astarData.data_cachedStartup = null;
				script.astarData.cacheStartup = false;
			}
			
			GUI.enabled = preEnabled;

	#if UNITY_BEFORE_4
			GUILayout.Label ("When using 'cache startup', the 'Nodes' toggle should always be enabled otherwise the graphs' nodes won't be saved and the caching is quite useless",helpBox);
	#else
			EditorGUILayout.HelpBox ("When using 'cache startup', the 'Nodes' toggle should always be enabled otherwise the graphs' nodes won't be saved and the caching is quite useless", MessageType.Info);
	#endif

			/*GUI.enabled = false;
			script.astarData.compress = EditorGUILayout.Toggle ("Compress",false);//script.astarData.compress);
			GUI.enabled = preEnabled;*/
			
			GUILayout.Space (5);
			
			AstarProfiler.EndProfile ("Clear Cache");
			
			AstarProfiler.StartProfile ("SaveToFile");
			
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Save to file")) {
				string path = EditorUtility.SaveFilePanel ("Save Graphs","","myGraph.zip","zip");
				
				if (path != "") {
					if (EditorUtility.DisplayDialog ("Scan before saving?","Do you want to scan the graphs before saving" +
						"\nNot scanning can cause node data to be omitted from the file if Save Node Data is enabled","Scan","Don't scan")) {
						MenuScan ();
					}
					
					uint checksum;
					byte[] bytes = SerializeGraphs (serializationSettings, out checksum);
					Pathfinding.Serialization.AstarSerializer.SaveToFile (path,bytes);
					
					EditorUtility.DisplayDialog ("Done Saving","Done saving graph data.","Ok");
				}
			}
			
			AstarProfiler.EndProfile ("SaveToFile");
			AstarProfiler.StartProfile ("LoadFromFile");
			if (GUILayout.Button ("Load from file")) {
				string path = EditorUtility.OpenFilePanel ("Load Graphs","","zip");
				
				if (path != "") {
					byte[] bytes;
					try {
						bytes = Pathfinding.Serialization.AstarSerializer.LoadFromFile (path);
					} catch (System.Exception e) {
						Debug.LogError ("Could not load from file at '"+path+"'\n"+e);
						bytes = null;
					}
					
					if (bytes != null) DeserializeGraphs (bytes);
				}
				
			}
			
			GUILayout.EndHorizontal ();
			
			AstarProfiler.EndProfile ("LoadFromFile");
			
			AstarProfiler.StartProfile ("!AstarRelease");
			AstarProfiler.EndProfile ("!AstarRelease");
		
		}

		AstarProfiler.StartProfile ("SerializationEndFadeArea");
		GUILayoutx.EndFadeArea ();
		AstarProfiler.EndProfile ("SerializationEndFadeArea");
	}
	
	public void DrawSettings () {
		EditorGUILayoutx.FadeArea fadeArea = GUILayoutx.BeginFadeArea (showSettings,"Settings","settings", EditorGUILayoutx.defaultAreaStyle, topBoxHeaderStyle);
		showSettings = fadeArea.open;

		//if (GUILayoutx.DrawID ("settings")) {
		if ( fadeArea.Show () ) {
			GUILayoutx.BeginFadeArea (true,"Pathfinding","alwaysShow",graphBoxStyle);
			
			if (Application.isPlaying) {
				GUI.enabled = false;
			}
			//script.useMultithreading = EditorGUILayout.Toggle ("Multithreading",script.useMultithreading);
			script.threadCount = (ThreadCount)EditorGUILayout.EnumPopup (new GUIContent ("Thread Count","Number of threads to run the pathfinding in (if any). More threads" +
				"can boost performance on multi core systems.\n" +
				"Use None for debugging or if you dont use pathfinding that much.\n" +
	                                                                "See docs for more info"),script.threadCount);
		
			GUI.enabled = true;
			int threads = AstarPath.CalculateThreadCount(script.threadCount);
			if (threads > 0) EditorGUILayout.HelpBox ("Using " + threads +" thread(s)" + (script.threadCount < 0 ? " on your machine" : "") + ".\n" +
				"The free version of the A* Pathfinding Project is limited to at most one thread.", MessageType.None);
			else EditorGUILayout.HelpBox ("Using a single coroutine (no threads)" + (script.threadCount < 0 ? " on your machine" : ""), MessageType.None);
			
		/*
			GUI.enabled = false;
			EditorGUILayout.EnumPopup (new GUIContent ("Thread Count","Number of threads to run the pathfinding in (if any). More threads " +
				"can boost performance on multi core systems.\n" +
				"Use None for debugging or if you dont use pathfinding that much.\n" +
		                                                                "See docs for more info\nThis is an A* Pathfinding Project Pro feature"),script.threadCount);
			GUI.enabled = true;
		*/
			script.maxFrameTime = EditorGUILayout.FloatField ("Max Frame Time",script.maxFrameTime);
			
			script.minAreaSize = EditorGUILayout.IntField (new GUIContent ("Min Area Size","The minimum number of nodes an area must have to be granted an unique area id. Only 256 area ids are available (8 bits). This merges small areas to use the same area id and helps keeping the area count under 256. [default = 10]"),script.minAreaSize);
			
			script.heuristic = (Heuristic)EditorGUILayout.EnumPopup ("Heuristic",script.heuristic);
			
			GUILayoutx.BeginFadeArea (script.heuristic == Heuristic.Manhattan || script.heuristic == Heuristic.Euclidean || script.heuristic == Heuristic.DiagonalManhattan,"hScale");
			if (GUILayoutx.DrawID ("hScale")) {
				EditorGUI.indentLevel++;
				script.heuristicScale = EditorGUILayout.FloatField ("Heuristic Scale",script.heuristicScale);
				EditorGUI.indentLevel--;
			}
			GUILayoutx.EndFadeArea ();
			
			//script.binaryHeapSize = EditorGUILayout.IntField (new GUIContent ("Binary Heap Size","The max size of the open list during a pathfinding request. If you get errors saying the heap is too small, increase it here. A good value is about 30-50% of the number of nodes in the graphs. But it depends a lot on how the graph is structured"),script.binaryHeapSize);
			
			
			script.limitGraphUpdates = EditorGUILayout.Toggle (new GUIContent ("Limit Graph Updates","Limit graph updates to only run every x seconds. Can have positive impact on performance if many graph updates are done"),script.limitGraphUpdates);
			
			GUILayoutx.BeginFadeArea (script.limitGraphUpdates,"graphUpdateFreq");
			if (GUILayoutx.DrawID ("graphUpdateFreq")) {
				EditorGUI.indentLevel++;
				script.maxGraphUpdateFreq = EditorGUILayout.FloatField ("Max Update Frequency (s)",script.maxGraphUpdateFreq);
				EditorGUI.indentLevel--;
			}
			GUILayoutx.EndFadeArea ();
			
			script.prioritizeGraphs = EditorGUILayout.Toggle (new GUIContent ("Prioritize Graphs","Normally, the system will search for the closest node in all graphs and choose the closest one" +
				"but if Prioritize Graphs is enabled, the first graph which has a node closer than Priority Limit will be chosen and additional search (e.g for the closest WALKABLE node) will be carried out on that graph only"),
				                                                       script.prioritizeGraphs);
			GUILayoutx.BeginFadeArea (script.prioritizeGraphs,"prioritizeGraphs");
			if (GUILayoutx.DrawID ("prioritizeGraphs")) {
				EditorGUI.indentLevel++;
				script.prioritizeGraphsLimit = EditorGUILayout.FloatField ("Priority Limit",script.prioritizeGraphsLimit);
				EditorGUI.indentLevel--;
			}
			GUILayoutx.EndFadeArea ();
			
			script.maxNearestNodeDistance = EditorGUILayout.FloatField (new GUIContent ("Max Nearest Node Distance",
		                                                                            "Normally, if the nearest node to e.g the start point of a path was not walkable" +
		                                                                            " a search will be done for the nearest node which is walkble. This is the maximum distance (world units) which it will serarch"),
		                                                            script.maxNearestNodeDistance);
		
			script.fullGetNearestSearch = EditorGUILayout.Toggle (new GUIContent ("Full Get Nearest Node Search","Forces more accurate searches on all graphs. " +
				"Normally only the closest graph in the initial fast check will perform additional searches, " +
				"if this is toggled, all graphs will do additional searches. Slower, but more accurate"),script.fullGetNearestSearch);
			script.scanOnStartup = EditorGUILayout.Toggle (new GUIContent ("Scan on Awake","Scan all graphs on Awake. If this is false, you must call AstarPath.active.Scan () yourself. Useful if you want to make changes to the graphs with code."),script.scanOnStartup);
		
			GUILayoutx.EndFadeArea ();
			
			DrawDebugSettings ();
			DrawColorSettings ();
			DrawTagSettings ();
			DrawEditorSettings ();
		}
		
				
		GUILayoutx.EndFadeArea ();
	}
	
	public static void EditTags () {
		AstarPath a = AstarPath.active;
		if (a == null) a = GameObject.FindObjectOfType (typeof(AstarPath)) as AstarPath;
		if (a != null) {
			editTags = true;
			showSettings = true;
			Selection.activeGameObject = a.gameObject;
		} else {
			Debug.LogWarning ("No AstarPath component in the scene");
		}
	}
	
	public void DrawTagSettings () {
		editTags = GUILayoutx.BeginFadeArea (editTags,"Tag Names","tags",graphBoxStyle);
		
		if (GUILayoutx.DrawID ("tags")) {
			
			string[] tagNames = script.GetTagNames ();
			
			for (int i=0;i<tagNames.Length;i++) {
				tagNames[i] = EditorGUILayout.TextField (new GUIContent ("Tag "+i,"Name for tag "+i),tagNames[i]);
				if (tagNames[i] == "") tagNames[i] = ""+i;
			}
		}
		
		GUILayoutx.EndFadeArea ();
	}
	
	public void DrawEditorSettings () {
		
		editorSettings = GUILayoutx.BeginFadeArea (editorSettings,"Editor","editorSettings",graphBoxStyle);
		
		if (GUILayoutx.DrawID ("editorSettings")) {
			EditorGUILayoutx.fancyEffects = EditorGUILayout.Toggle ("Fancy fading effects",EditorGUILayoutx.fancyEffects);
			
			bool preVal = useDarkSkin;
			int val = useDarkSkin ? 2 : 1;
			if (!hasForcedNoDarkSkin) val = 0;
			
			val = EditorGUILayout.Popup ("Use Dark Skin",val,new string[] {"Auto","Force Light","Force Dark"});
			
			if (val == 0) {
				useDarkSkin = EditorGUIUtility.isProSkin;
				hasForcedNoDarkSkin = false;
			} else {
				hasForcedNoDarkSkin = true;
				useDarkSkin = val == 2;
			}
			
			if (useDarkSkin != preVal) {
				LoadStyles ();
			}
			
			if (IsJsEnabled ()) {
				if (GUILayout.Button (new GUIContent ("Disable Js Support","Revert to only enable pathfinding calls from C#"))) {
					DisableJs ();
				}
			} else {
				if (GUILayout.Button (new GUIContent ("Enable Js Support","Folders can be restructured to enable pathfinding calls from Js instead of just from C#"))) {
					EnableJs ();
				}
			}
		}
		
		GUILayoutx.EndFadeArea ();
	}
	
	public void DrawDebugSettings () {
		GUILayoutx.BeginFadeArea (true,"Debug","debugSettings",graphBoxStyle);
		
		if (GUILayoutx.DrawID ("debugSettings")) {
			
			script.logPathResults = (PathLog)EditorGUILayout.EnumPopup ("Path Log Mode",script.logPathResults);
			script.debugMode = (GraphDebugMode)EditorGUILayout.EnumPopup ("Path Debug Mode",script.debugMode);
			
			bool show = script.debugMode == GraphDebugMode.G || script.debugMode == GraphDebugMode.H || script.debugMode == GraphDebugMode.F || script.debugMode == GraphDebugMode.Penalty;
			GUILayoutx.BeginFadeArea (show,"debugRoof");
			
			if (GUILayoutx.DrawID ("debugRoof")) {
				EditorGUI.indentLevel++;
				script.debugRoof = EditorGUILayout.FloatField ("Gradient Max (red)",script.debugRoof);
				EditorGUI.indentLevel--;
			}
			
			GUILayoutx.EndFadeArea ();
			
			script.showSearchTree = EditorGUILayout.Toggle ("Show Search Tree",script.showSearchTree);
			
			script.showUnwalkableNodes = EditorGUILayout.Toggle ("Show Unwalkable Nodes", script.showUnwalkableNodes);
			
			if (script.showUnwalkableNodes) {
				EditorGUI.indentLevel++;
				script.unwalkableNodeDebugSize = EditorGUILayout.FloatField ("Size", script.unwalkableNodeDebugSize);
				EditorGUI.indentLevel--;
			}
			
		}
		
		GUILayoutx.EndFadeArea ();
	}
	
	public void DrawColorSettings () {
		
		colorSettings = GUILayoutx.BeginFadeArea (colorSettings,"Colors","colorSettings",graphBoxStyle);
		
		if (GUILayoutx.DrawID ("colorSettings")) {
			if (script.colorSettings == null) {
				script.colorSettings = new AstarColor ();
			}
			
			AstarColor colors = script.colorSettings;
			
			//EditorGUI.indentLevel++;
			
			colors._NodeConnection = EditorGUILayout.ColorField ("Node Connection", colors._NodeConnection);
			colors._UnwalkableNode = EditorGUILayout.ColorField ("Unwalkable Node", colors._UnwalkableNode);
			colors._BoundsHandles = EditorGUILayout.ColorField ("Bounds Handles", colors._BoundsHandles);
			
			colors._ConnectionLowLerp = EditorGUILayout.ColorField ("Connection Gradient (low)", colors._ConnectionLowLerp);
			colors._ConnectionHighLerp = EditorGUILayout.ColorField ("Connection Gradient (high)", colors._ConnectionHighLerp);
			
			colors._MeshEdgeColor = EditorGUILayout.ColorField ("Mesh Edge", colors._MeshEdgeColor);
			colors._MeshColor = EditorGUILayout.ColorField ("Mesh Color", colors._MeshColor);
			
			if (colors._AreaColors == null) {
				colors._AreaColors = new Color[0];
			}
			
			//Custom Area Colors
			customAreaColorsOpen = EditorGUILayout.Foldout (customAreaColorsOpen,"Custom Area Colors");
			if (customAreaColorsOpen) {
				EditorGUI.indentLevel+=2;
				
				for (int i=0;i<colors._AreaColors.Length;i++) {
					GUILayout.BeginHorizontal ();
					colors._AreaColors[i] = EditorGUILayout.ColorField ("Area "+i+(i == 0 ? " (not used usually)":""),colors._AreaColors[i]);
					if (GUILayout.Button (new GUIContent ("","Reset to the default color"),astarSkin.FindStyle ("SmallReset"),GUILayout.Width (20))) {
						colors._AreaColors[i] = AstarMath.IntToColor (i,1F);
					}
					GUILayout.EndHorizontal ();
				}
				
				GUILayout.BeginHorizontal ();
				
				if (colors._AreaColors.Length > 255) {
					GUI.enabled = false;
				}
				
				if (GUILayout.Button ("Add New")) {
					Color[] newcols = new Color[colors._AreaColors.Length+1];
					for (int i=0;i<colors._AreaColors.Length;i++) {
						newcols[i] = colors._AreaColors[i];
					}
					newcols[newcols.Length-1] = AstarMath.IntToColor (newcols.Length-1,1F);
					colors._AreaColors = newcols;
				}
				
				GUI.enabled = true;
				if (colors._AreaColors.Length == 0) {
					GUI.enabled = false;
				}
				
				if (GUILayout.Button ("Remove last") && colors._AreaColors.Length > 0) {
					Color[] newcols = new Color[colors._AreaColors.Length-1];
					for (int i=0;i<colors._AreaColors.Length-1;i++) {
						newcols[i] = colors._AreaColors[i];
					}
					colors._AreaColors = newcols;
				}
				GUI.enabled = true;
				GUILayout.EndHorizontal ();
				
				EditorGUI.indentLevel-=2;
			}
			
			//EditorGUI.indentLevel--;
			
			if (GUI.changed) {
				colors.OnEnable ();
				//OnSceneGUI ();
				// iHandleUtility.Repaint ();
			}
		}
		
		GUILayoutx.EndFadeArea ();
	}
	
	/** Make sure every graph has a graph editor */
	public void CheckGraphEditors (bool forceRebuild = false) {
		if (forceRebuild || graphEditors == null || script.graphs == null || script.graphs.Length != graphEditors.Length) {
				
			if (script.graphs == null) {
				script.graphs = new NavGraph[0];
			}
			
			if (graphEditors != null) {
				for (int i=0;i<graphEditors.Length;i++) {
					if (graphEditors[i] != null) {
						//graphEditors[i].OnDisableUndo ();
						graphEditors[i].OnDisable ();
						graphEditors[i].OnDestroy ();
					}
				}
			}
			
			graphEditors = new GraphEditor[script.graphs.Length];
			
			for (int i=0;i< script.graphs.Length;i++) {
				
				NavGraph graph = script.graphs[i];
				
				if (graph == null) continue;
			
				if (graph.guid == new Pathfinding.Util.Guid ()) {
					Debug.LogWarning ("Zeroed guid detected, creating new randomized guid");
					graph.guid = Pathfinding.Util.Guid.NewGuid();
				}
			
				GraphEditor graphEditor = CreateGraphEditor (graph.GetType ().Name);
				graphEditor.target = graph;
				graphEditor.OnEnable ();
				graphEditors[i] = graphEditor;
			
			
			}
			
		} else {
			for (int i=0;i< script.graphs.Length;i++) {
				
				if (script.graphs[i] == null) continue;
				
				if (graphEditors[i] == null || graphEditorTypes[script.graphs[i].GetType ().Name].editorType != graphEditors[i].GetType ()) {
					CheckGraphEditors (true);
					return;
				}
				
				if (script.graphs[i].guid == new Pathfinding.Util.Guid ()) {
					Debug.LogWarning ("Zeroed guid detected, creating new randomized guid");
					script.graphs[i].guid = Pathfinding.Util.Guid.NewGuid();
				}
				
				graphEditors[i].target = script.graphs[i];
			}
		}
	}
	
	/** Creates a link between start and end. \see \link Pathfinding.AstarData.userConnections AstarData.userConnections \endlink */
	public int CreateNewUserConnection (Vector3 start, Vector3 end) {
		UserConnection[] conns = script.astarData.userConnections;

		if ( conns == null ) conns = new UserConnection[0];

		List<UserConnection> connList = new List<UserConnection>(conns);
		UserConnection conn = new UserConnection ();
		conn.p1 = start;
		conn.p2 = end;
		connList.Add (conn);
		script.astarData.userConnections = connList.ToArray ();
		
		return script.astarData.userConnections.Length-1;
	}
	
	public void RemoveGraph (NavGraph graph) {
		GUILayoutx.RemoveID ("graph_"+graph.guid);
		script.astarData.RemoveGraph (graph);
		CheckGraphEditors ();
		GUI.changed = true;
		Repaint ();
	}
	
	public void AddGraph (System.Type type) {
		script.astarData.AddGraph (type);
		CheckGraphEditors ();
		
		GUI.changed = true;
	}
	
	/** Creates a GraphEditor for a graph */
	public GraphEditor CreateGraphEditor (string graphType) {
		
		if (graphEditorTypes.ContainsKey (graphType)) {
			GraphEditor ge = System.Activator.CreateInstance (graphEditorTypes[graphType].editorType) as GraphEditor;
			ge.editor = this;
			return ge;
		} else {
			Debug.LogError ("Couldn't find an editor for the graph type '"+graphType+"' There are "+graphEditorTypes.Count+" available graph editors");
		}
		
		GraphEditor def = new GraphEditor ();
		def.editor = this;
		return def;
	}
	
	/** Draw Editor Gizmos in graphs. This is called using a delegate OnDrawGizmosCallback in the AstarPath script.*/
	public void OnDrawGizmos () {
		
		AstarProfiler.StartProfile ("OnDrawGizmosEditor");
		
		CheckGraphEditors ();
		
		for (int i=0;i<script.graphs.Length;i++) {
			
			NavGraph graph = script.graphs[i];
			
			
			if (graph == null || graphEditors.Length <= i) {
				continue;
			}
			
			graphEditors[i].OnDrawGizmos ();
		}
		
		AstarProfiler.EndProfile ("OnDrawGizmosEditor");
	}
	
	public bool HandleUndo () {
		//The user has tried to undo something, apply that
#if UNITY_LE_4_3
		if (script.astarData.hasBeenReverted) {
#endif
			if (script.astarData.GetData() == null) {
				script.astarData.SetData (new byte[0], 0);
			} else {

				Debug.Log ("Undo: " + hash (script.astarData.GetData()));
				LoadGraphs ();

				return true;
			}
#if UNITY_LE_4_3
		}
#endif
		return false;
	}

	int hash ( byte[] arr ) {
		if ( arr == null ) return -1;
		int h = -1;
		for ( int i=0;i<arr.Length;i++) {
			h ^= arr[i]*3221;
		}
		return h;
	}
	public void SaveGraphsAndUndo (EventType et = EventType.Used) {
		//Serialize the settings of the graphs

		//Dont process undo events in editor, we don't want to reset graphs
		if ( Application.isPlaying ) {
			return;
		}

		if ( et == EventType.Layout && Event.current.commandName == "UndoRedoPerformed" ) {
			uint checksum;
			byte[] bytes = SerializeGraphs (out checksum);

			//Check if the data is different than the previous data, use checksums
			bool isDifferent = hash(script.astarData.GetData()) != hash(bytes);

			if (isDifferent ) {
				HandleUndo ();
			}

			CheckGraphEditors ();
			// Deserializing a graph does not necessarily yield the same hash as the data loaded from
			// this is (probably) because editor settings are not saved all the time
			// so we explicitly ignore the new hash
			bytes = SerializeGraphs (out checksum);
			ignoreUndoHash = hash(bytes);
			return;
		}

		if (Event.current == null || script.astarData.GetData() == null) {
			uint checksum;
			byte[] bytes = SerializeGraphs (out checksum);
			script.astarData.SetData (bytes,checksum);
			EditorUtility.SetDirty (target);
			return;
		}

#if UNITY_LE_4_3
		if (HandleUndo ()) {
			return;
		}
#endif

#if UNITY_LE_4_3
		Event e = Event.current;
		if ((e.button == 0 && (et == EventType.MouseDown || et == EventType.MouseUp)) || (e.isKey && (e.keyCode == KeyCode.Tab || e.keyCode == KeyCode.Return)) || et == EventType.ExecuteCommand)
#else
		if ( Undo.GetCurrentGroup () != lastUndoGroup )
#endif
		{

			//To serialize settings for a grid graph takes from 0.00 ms to 7.8 ms (usually 0.0, but sometimes jumps up to 7.8 (no values in between)
			
			uint checksum;
			byte[] bytes = SerializeGraphs (out checksum);
				
			int byteHash = hash(bytes);
			//Check if the data is different than the previous data, use checksums
			bool isDifferent = byteHash != ignoreUndoHash && hash(script.astarData.GetData()) != hash(bytes);//script.astarData.dataChecksum != checksum;

			//Only save undo if the data was different from the last saved undo
			if (isDifferent) {
				//This flag is set to true so we can detect if the object has been reverted
#if UNITY_LE_4_3
				script.astarData.hasBeenReverted = true;
				Undo.RegisterUndo (script,"A* inspector");
#else
				Undo.RegisterCompleteObjectUndo (script, "A* Graph Settings");
				//Undo.IncrementCurrentGroup ();
#endif
				
				//Assign the new data
				script.astarData.SetData (bytes, checksum);
				//Debug.Log ("Set: " + hash (script.astarData.GetData()) + " " + Event.current.commandName + " " + et);
#if UNITY_LE_4_3				
				script.astarData.hasBeenReverted = false;
#endif
				//stopWatch.Stop();

				EditorUtility.SetDirty (script);
			}
			
#if !UNITY_LE_4_3
			lastUndoGroup = Undo.GetCurrentGroup ();
#endif
		}
		
		
	}
	
	public void LoadGraphs () {
		//Load graphs from serialized data
		DeserializeGraphs ();

#if UNITY_LE_4_3
		script.astarData.hasBeenReverted = false;
#endif
	}
	
	public byte[] SerializeGraphs () {
		uint checksum;
		return SerializeGraphs (out checksum);
	}
	
	public byte[] SerializeGraphs (out uint checksum) {
		
		Pathfinding.Serialization.SerializeSettings settings = Pathfinding.Serialization.SerializeSettings.Settings;
		settings.editorSettings = true;
		byte[] bytes = SerializeGraphs (settings, out checksum);
		return bytes;
	}
	
	public byte[] SerializeGraphs (Pathfinding.Serialization.SerializeSettings settings, out uint checksum) {
		byte[] bytes = null;
		uint ch = 0;
		AstarPath.active.AddWorkItem (new AstarPath.AstarWorkItem (delegate (bool force) {
			Pathfinding.Serialization.AstarSerializer sr = new Pathfinding.Serialization.AstarSerializer(script.astarData, settings);
			sr.OpenSerialize();
			script.astarData.SerializeGraphsPart (sr);
			sr.SerializeEditorSettings (graphEditors);
			bytes = sr.CloseSerialize();
			ch = sr.GetChecksum ();
			return true;
		}));
		
		//Make sure the above work item is run directly
		AstarPath.active.FlushWorkItems();
		checksum = ch;
		return bytes;
		
		//Forward to runtime serializer
		//return script.astarData.SerializeGraphs(Pathfinding.Serialization.SerializeSettings.Settings, out checksum);
	}
	
	public void DeserializeGraphs () {
		
		if (script.astarData.GetData() == null || script.astarData.GetData().Length == 0) {
			script.astarData.graphs = new NavGraph[0];
			return;
		}
		
		DeserializeGraphs (script.astarData.GetData());
	}
	
	public void DeserializeGraphs (byte[] bytes) {
		
		AstarPath.active.AddWorkItem (new AstarPath.AstarWorkItem (delegate (bool force) {
			Pathfinding.Serialization.AstarSerializer sr = new Pathfinding.Serialization.AstarSerializer(script.astarData);
			if (sr.OpenDeserialize(bytes)) {
				script.astarData.DeserializeGraphsPart (sr);
				
				//Make sure every graph has a graph editor
				CheckGraphEditors ();
				sr.DeserializeEditorSettings (graphEditors);
				
				sr.CloseDeserialize();
			} else {
				Debug.Log ("Invalid data file (cannot read zip).\nThe data is either corrupt or it was saved using a 3.0.x or earlier version of the system");
				//Make sure every graph has a graph editor
				CheckGraphEditors ();
			}
			return true;
		}));
		
		//Make sure the above work item is run directly
		AstarPath.active.FlushWorkItems();
	}
	
	[UnityEditor.MenuItem ("Edit/Pathfinding/Scan All Graphs %&s")]
	public static void MenuScan () {
		
		if (AstarPath.active == null) {
			AstarPath.active = FindObjectOfType(typeof(AstarPath)) as AstarPath;
			if (AstarPath.active == null) {
				return;
			}
		}
		
		if (!Application.isPlaying && (AstarPath.active.astarData.graphs == null || AstarPath.active.astarData.graphTypes == null)) {
			UnityEditor.EditorUtility.DisplayProgressBar ("Scanning","Deserializing",0);
			AstarPath.active.astarData.DeserializeGraphs ();
		}
		
		UnityEditor.EditorUtility.DisplayProgressBar ("Scanning","Scanning...",0);
		
		try {
			OnScanStatus info = delegate (Progress p) {
				UnityEditor.EditorUtility.DisplayProgressBar ("Scanning",p.description,p.progress);
			};
			AstarPath.active.ScanLoop (info);
			
		} catch (System.Exception e) {
			Debug.LogError ("There was an error generating the graphs:\n"+e.ToString ()+"\n\nIf you think this is a bug, please contact me on arongranberg.com (post a comment)\n");
			UnityEditor.EditorUtility.DisplayDialog ("Error Generating Graphs","There was an error when generating graphs, check the console for more info","Ok");
			throw e;
		} finally {
			UnityEditor.EditorUtility.ClearProgressBar();
		}
	}

	/** Searches in the current assembly for GraphEditor and NavGraph types */
	public void FindGraphTypes () {
		
		//Skip if we have already found the graph types
		//if (script.astarData.graphTypes != null && script.astarData.graphTypes.Length != 0) {
		//	return;
		//}
		
		
		graphEditorTypes = new Dictionary<string,CustomGraphEditor> ();
		
		Assembly asm = Assembly.GetAssembly (typeof(AstarPathEditor));
		
		System.Type[] types = asm.GetTypes ();
		
		List<System.Type> graphList = new List<System.Type> ();
		
		
		//Iterate through the assembly for classes which inherit from GraphEditor
		foreach (System.Type type in types) {
			
			System.Type baseType = type.BaseType;
			while (!System.Type.Equals (baseType, null)) {
				if (System.Type.Equals ( baseType, typeof(GraphEditor) )) {
					
					System.Object[] att = type.GetCustomAttributes (false);
					
					//Loop through the attributes for the graph editor class
					foreach (System.Object attribute in att) {
						CustomGraphEditor cge = attribute as CustomGraphEditor;
						
						if (cge != null && !System.Type.Equals (cge.graphType, null)) {
							cge.editorType = type;
							graphList.Add (cge.graphType);
							graphEditorTypes.Add (cge.graphType.Name,cge);
						}
						
					}
					break;
				}
				
				baseType = baseType.BaseType;
			}
		}
		
		
		
		asm = Assembly.GetAssembly (typeof(AstarPath));
		types = asm.GetTypes ();


		//Not really required, but it's so fast so why not make a check and see if any graph types didn't have any editors
		foreach (System.Type type in types) {
			
			System.Type baseType = type.BaseType;
			while (baseType != null) {
				if (System.Type.Equals ( baseType, typeof(NavGraph) )) {
					
					bool alreadyFound = false;
					for (int i=0;i<graphList.Count;i++) {
						if (graphList[i] == type) {
							alreadyFound = true;
							break;
						}
					}
					if (!alreadyFound) {
						graphList.Add (type);
					}
					break;
				}
				
				baseType = baseType.BaseType;
			}
		}
		
		script.astarData.graphTypes = graphList.ToArray ();
		
	}
	
	[InitializeOnLoad]
	/** Checking for updates on startup */
	public static class UpdateChecker {
	    static UpdateChecker()
	    {
			AstarPathEditor.CheckForUpdates ();
			EditorApplication.update += UpdateCheckLoop;
	    }
	}

	/** Checking for updates on startup.
	 * Repeatedly called by #UpdateChecker
	 */
	public static void UpdateCheckLoop () {
		// Go on until the update check has been completed
		if (!CheckForUpdates ()) {
			EditorApplication.update -= UpdateCheckLoop;
		}
	}

}

//[System.AttributeUsageAttribute (System.AttributeUsageAttribute.AllowMultiple = true)]
[System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
/** Added to editors of custom graph types */
public class CustomGraphEditor : System.Attribute {
	/** Graph type which this is an editor for */
	public System.Type graphType;

	/** Name displayed in the inpector */
	public string displayName;
	public System.Type editorType;
	
	public CustomGraphEditor (System.Type t,string displayName) {
		graphType = t;
		this.displayName = displayName;	
	}
}