//#define ProfileAstar

using UnityEngine;
using System.Collections;
using System.Text;
using Pathfinding;

[AddComponentMenu("Pathfinding/Debugger")]
[ExecuteInEditMode]
/** Debugger for the A* Pathfinding Project.
 * This class can be used to profile different parts of the pathfinding system
 * and the whole game as well to some extent.
 * 
 * Clarification of the labels shown when enabled.
 * All memory related things profiles <b>the whole game</b> not just the A* Pathfinding System.\n
 * - Currently allocated: memory the GC (garbage collector) says the application has allocated right now.
 * - Peak allocated: maximum measured value of the above.
 * - Last collect peak: the last peak of 'currently allocated'.
 * - Allocation rate: how much the 'currently allocated' value increases per second. This value is not as reliable as you can think
 * it is often very random probably depending on how the GC thinks this application is using memory.
 * - Collection frequency: how often the GC is called. Again, the GC might decide it is better with many small collections
 * or with a few large collections. So you cannot really trust this variable much.
 * - Last collect fps: FPS during the last garbage collection, the GC will lower the fps a lot.
 * 
 * - FPS: current FPS (not updated every frame for readability)
 * - Lowest FPS (last x): As the label says, the lowest fps of the last x frames.
 * 
 * - Size: Size of the path pool.
 * - Total created: Number of paths of that type which has been created. Pooled paths are not counted twice.
 * If this value just keeps on growing and growing without an apparent stop, you are are either not pooling any paths
 * or you have missed to pool some path somewhere in your code.
 * 
 * \see pooling
 * 
 * \todo Add field showing how many graph updates are being done right now
 */
public class AstarDebugger : MonoBehaviour {
	
	public int yOffset = 5;
	
	public bool show = true;
	public bool showInEditor = false;
	
	public bool showFPS = false;
	public bool showPathProfile = false;
	public bool showMemProfile = false;
	public bool showGraph = false;
	
	public int graphBufferSize = 200;
	
	/** Font to use.
	 * A monospaced font is the best
	 */
	public Font font = null;
	public int fontSize = 12;
	
	StringBuilder text = new StringBuilder ();
	string cachedText;
	float lastUpdate = -999;
	
	private GraphPoint[] graph;
	
	struct GraphPoint {
		public float fps, memory;
		public bool collectEvent;
	}
	
	private float delayedDeltaTime = 1;
	private float lastCollect = 0;
	private float lastCollectNum = 0;
	private float delta = 0;
	private float lastDeltaTime = 0;
	private int allocRate = 0;
	private int lastAllocMemory = 0;
	private float lastAllocSet = -9999;
	private int allocMem = 0;
	private int collectAlloc = 0;
	private int peakAlloc = 0;
	
	private int fpsDropCounterSize = 200;
	private float[] fpsDrops;
	
	private Rect boxRect;
	
	private GUIStyle style;
	
	private Camera cam;
	private LineRenderer lineRend;
	
	float graphWidth = 100;
	float graphHeight = 100;
	float graphOffset = 50;
	
	public void Start () {
		
		useGUILayout = false;
		
		fpsDrops = new float[fpsDropCounterSize];
		
		if (GetComponent<Camera>() != null) {
			cam = GetComponent<Camera>();
		} else {
			cam = Camera.main;
		}
		
		
		graph = new GraphPoint[graphBufferSize];
		
		for (int i=0;i<fpsDrops.Length;i++) {
			fpsDrops[i] = 1F / Time.deltaTime;
		}
	}
	
	int maxVecPool = 0;
	int maxNodePool = 0;
	
	PathTypeDebug[] debugTypes = new PathTypeDebug[] {
		new PathTypeDebug ("ABPath", PathPool<ABPath>.GetSize, PathPool<ABPath>.GetTotalCreated)
	};
	
	struct PathTypeDebug {
		string name;
		System.Func<int> getSize;
		System.Func<int> getTotalCreated;
		public PathTypeDebug (string name, System.Func<int> getSize, System.Func<int> getTotalCreated) {
			this.name = name;
			this.getSize = getSize;
			this.getTotalCreated = getTotalCreated;
		}
		
		public void Print (StringBuilder text) {
			int totCreated = getTotalCreated ();
			if (totCreated > 0) {
				text.Append("\n").Append (("  " + name).PadRight (25)).Append (getSize()).Append("/").Append(totCreated);
			}
		}
	}
	
	public void Update () {
		if (!show || (!Application.isPlaying && !showInEditor)) return;
		
		int collCount = System.GC.CollectionCount (0);
		
		if (lastCollectNum != collCount) {
			lastCollectNum = collCount;
			delta = Time.realtimeSinceStartup-lastCollect;
			lastCollect = Time.realtimeSinceStartup;
			lastDeltaTime = Time.deltaTime;
			collectAlloc = allocMem;
		}
		
		allocMem = (int)System.GC.GetTotalMemory (false);
		
		bool collectEvent = allocMem < peakAlloc;
		peakAlloc = !collectEvent ? allocMem : peakAlloc;
		
		if (Time.realtimeSinceStartup - lastAllocSet > 0.3F || !Application.isPlaying) {
			int diff = allocMem - lastAllocMemory;
			lastAllocMemory = allocMem;
			lastAllocSet = Time.realtimeSinceStartup;
			delayedDeltaTime = Time.deltaTime;
			
			if (diff >= 0) {
				allocRate = diff;
			}
		}
		
		if (Application.isPlaying) {
			fpsDrops[Time.frameCount % fpsDrops.Length] = Time.deltaTime != 0 ? 1F / Time.deltaTime : float.PositiveInfinity;
			int graphIndex = Time.frameCount % graph.Length;
			graph[graphIndex].fps = Time.deltaTime < Mathf.Epsilon ? 0 : 1F / Time.deltaTime;
			graph[graphIndex].collectEvent = collectEvent;
			graph[graphIndex].memory = allocMem;
		}
		
		if (Application.isPlaying && cam != null && showGraph) {
			
			graphWidth = cam.pixelWidth*0.8f;
			
				
			float minMem = float.PositiveInfinity, maxMem = 0, minFPS = float.PositiveInfinity, maxFPS = 0;
			for (int i=0;i<graph.Length;i++) {
				minMem = Mathf.Min (graph[i].memory, minMem);
				maxMem = Mathf.Max (graph[i].memory, maxMem);
				minFPS = Mathf.Min (graph[i].fps, minFPS);
				maxFPS = Mathf.Max (graph[i].fps, maxFPS);
			}
			
			int currentGraphIndex = Time.frameCount % graph.Length;
			
			Matrix4x4 m = Matrix4x4.TRS (new Vector3 ((cam.pixelWidth - graphWidth)/2f, graphOffset,1), Quaternion.identity, new Vector3 (graphWidth, graphHeight, 1));
			
			for (int i=0;i<graph.Length-1;i++) {
				if (i == currentGraphIndex) continue;
				
				//Debug.DrawLine (m.MultiplyPoint (new Vector3 (i/(float)graph.Length, Mathfx.MapTo (minMem, maxMem, graph[i].memory), -1)),
				//	m.MultiplyPoint (new Vector3 ((i+1)/(float)graph.Length, Mathfx.MapTo (minMem, maxMem, graph[i+1].memory), -1)), Color.blue);
				
				//Debug.DrawLine (m.MultiplyPoint (Vector3.zero), m.MultiplyPoint (-Vector3.one), Color.red);
				//Debug.Log (Mathfx.MapTo (minMem, maxMem, graph[i].memory)  + " " + graph[i].memory);
				DrawGraphLine (i, m, i/(float)graph.Length, (i+1)/(float)graph.Length, AstarMath.MapTo (minMem, maxMem, graph[i].memory), AstarMath.MapTo (minMem, maxMem, graph[i+1].memory), Color.blue);
				
				DrawGraphLine (i, m, i/(float)graph.Length, (i+1)/(float)graph.Length, AstarMath.MapTo (minFPS, maxFPS, graph[i].fps), AstarMath.MapTo (minFPS, maxFPS, graph[i+1].fps), Color.green);
				
				//Debug.DrawLine (m.MultiplyPoint (new Vector3 (i/(float)graph.Length, Mathfx.MapTo (minFPS, maxFPS, graph[i].fps), -1)),
				//	m.MultiplyPoint (new Vector3 ((i+1)/(float)graph.Length, Mathfx.MapTo (minFPS, maxFPS, graph[i+1].fps), -1)), Color.green);
			}
			
			
			
			/*Cross (new Vector3(0,0,1));
			Cross (new Vector3(1,1,1));
			Cross (new Vector3(0,1,1));
			Cross (new Vector3(1,0,1));
			Cross (new Vector3(-1,0,1));
			Debug.DrawLine (m.MultiplyPoint(Vector3.zero), m.MultiplyPoint(new Vector3(0,0,-5)),Color.blue);*/
		}
	}
	
	public void DrawGraphLine (int index, Matrix4x4 m, float x1, float x2, float y1, float y2, Color col) {
		Debug.DrawLine (cam.ScreenToWorldPoint (m.MultiplyPoint3x4 (new Vector3 (x1,y1))), cam.ScreenToWorldPoint (m.MultiplyPoint3x4 (new Vector3 (x2,y2))), col);
	}
	
	public void Cross (Vector3 p) {
		
		p = cam.cameraToWorldMatrix.MultiplyPoint (p);
		Debug.DrawLine (p-Vector3.up*0.2f, p+Vector3.up*0.2f,Color.red);
		Debug.DrawLine (p-Vector3.right*0.2f, p+Vector3.right*0.2f,Color.red);
	}
	
	
	public void OnGUI () {
		if (!show || (!Application.isPlaying && !showInEditor)) return;
		
		if (style == null) {
			style = new GUIStyle();
			style.normal.textColor = Color.white;
			style.padding = new RectOffset (5,5,5,5);
		}
		
		if (Time.realtimeSinceStartup - lastUpdate > 0.5f || cachedText == null || !Application.isPlaying) {
			lastUpdate = Time.realtimeSinceStartup;
			
			boxRect = new Rect (5,yOffset,310,40);
			
			text.Length = 0;
			text.AppendLine ("A* Pathfinding Project Debugger");
			text.Append ("A* Version: ").Append (AstarPath.Version.ToString ());
			
			if (showMemProfile) {
				boxRect.height += 200;
				
				text.AppendLine();
				text.AppendLine();
				text.Append ("Currently allocated".PadRight (25));
				text.Append ((allocMem/1000000F).ToString ("0.0 MB"));
				text.AppendLine ();
				
				text.Append ("Peak allocated".PadRight (25));
				text.Append ((peakAlloc/1000000F).ToString ("0.0 MB")).AppendLine();

				text.Append ("Last collect peak".PadRight(25));
				text.Append ((collectAlloc/1000000F).ToString ("0.0 MB")).AppendLine();
				
				
				text.Append ("Allocation rate".PadRight (25));
				text.Append ((allocRate/1000000F).ToString ("0.0 MB")).AppendLine();
				
				text.Append ("Collection frequency".PadRight (25));
				text.Append (delta.ToString ("0.00"));
				text.Append ("s\n");
				
				text.Append ("Last collect fps".PadRight (25));
				text.Append ((1F/lastDeltaTime).ToString ("0.0 fps"));
				text.Append (" (");
				text.Append (lastDeltaTime.ToString ("0.000 s"));
				text.Append (")");
			}
			
			if (showFPS) {
				text.AppendLine ();
				text.AppendLine();
				text.Append ("FPS".PadRight (25)).Append((1F/delayedDeltaTime).ToString ("0.0 fps"));
				
				
				float minFps = Mathf.Infinity;
				
				for (int i=0;i<fpsDrops.Length;i++) if (fpsDrops[i] < minFps) minFps = fpsDrops[i];
				
				text.AppendLine();
				text.Append (("Lowest fps (last " + fpsDrops.Length + ")").PadRight(25)).Append(minFps.ToString ("0.0"));
			}
			
			if (showPathProfile) {
				AstarPath astar = AstarPath.active;
				
				text.AppendLine ();
				
				if (astar == null) {
					text.Append ("\nNo AstarPath Object In The Scene");
				} else {
					
					if (Pathfinding.Util.ListPool<Vector3>.GetSize() > maxVecPool) maxVecPool = Pathfinding.Util.ListPool<Vector3>.GetSize();
					if (Pathfinding.Util.ListPool<Pathfinding.GraphNode>.GetSize() > maxNodePool) maxNodePool = Pathfinding.Util.ListPool<Pathfinding.GraphNode>.GetSize();
					
					text.Append ("\nPool Sizes (size/total created)");
					
					for (int i=0;i<debugTypes.Length;i++) {
						debugTypes[i].Print (text);
					}
				}
			}
			
			cachedText = text.ToString();
		}
		
		
		if (font != null) {
			style.font = font;
			style.fontSize = fontSize;
		}
		
		boxRect.height = style.CalcHeight (new GUIContent (cachedText),boxRect.width);
		
		GUI.Box (boxRect,"");
		GUI.Label (boxRect,cachedText,style);
		
		if (showGraph) {
			
			
			float minMem = float.PositiveInfinity, maxMem = 0, minFPS = float.PositiveInfinity, maxFPS = 0;
			for (int i=0;i<graph.Length;i++) {
				minMem = Mathf.Min (graph[i].memory, minMem);
				maxMem = Mathf.Max (graph[i].memory, maxMem);
				minFPS = Mathf.Min (graph[i].fps, minFPS);
				maxFPS = Mathf.Max (graph[i].fps, maxFPS);
			}
			
			float line;
			GUI.color = Color.blue;
			//Round to nearest x.x MB
			line = Mathf.RoundToInt (maxMem/(100.0f*1000)); // *1000*100
			GUI.Label (new Rect (5, Screen.height - AstarMath.MapTo (minMem, maxMem, 0 + graphOffset, graphHeight + graphOffset, line*1000*100) - 10, 100,20), (line/10.0f).ToString("0.0 MB"));
			
			
			
			line = Mathf.Round (minMem/(100.0f*1000)); // *1000*100
			GUI.Label (new Rect (5, Screen.height - AstarMath.MapTo (minMem, maxMem, 0 + graphOffset, graphHeight + graphOffset, line*1000*100) - 10, 100,20), (line/10.0f).ToString("0.0 MB"));
			
			
			GUI.color = Color.green;
			//Round to nearest x.x MB
			line = Mathf.Round (maxFPS); // *1000*100
			GUI.Label (new Rect (55, Screen.height - AstarMath.MapTo (minFPS, maxFPS, 0 + graphOffset, graphHeight + graphOffset, line) - 10, 100,20), (line).ToString("0 FPS"));
			
			
			
			line = Mathf.Round (minFPS); // *1000*100
			GUI.Label (new Rect (55, Screen.height - AstarMath.MapTo (minFPS, maxFPS, 0 + graphOffset, graphHeight + graphOffset, line) - 10, 100,20), (line).ToString("0 FPS"));
		}
	}
}

