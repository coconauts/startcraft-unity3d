using UnityEngine;
using System.Collections.Generic;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace Pathfinding {
	/// <summary>
	/// Handles path calls for a single unit.
	/// \ingroup relevant
	/// This is a component which is meant to be attached to a single unit (AI, Robot, Player, whatever) to handle its pathfinding calls.
	/// It also handles post-processing of paths using modifiers.
	///
	/// [Open online documentation to see images]
	///
	/// See: calling-pathfinding (view in online documentation for working links)
	/// See: modifiers (view in online documentation for working links)
	/// </summary>
	[AddComponentMenu("Pathfinding/Seeker")]
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_seeker.php")]
	public class Seeker : VersionedMonoBehaviour {
		/// <summary>
		/// Enables drawing of the last calculated path using Gizmos.
		/// The path will show up in green.
		///
		/// See: OnDrawGizmos
		/// </summary>
		public bool drawGizmos = true;

		/// <summary>
		/// Enables drawing of the non-postprocessed path using Gizmos.
		/// The path will show up in orange.
		///
		/// Requires that <see cref="drawGizmos"/> is true.
		///
		/// This will show the path before any post processing such as smoothing is applied.
		///
		/// See: drawGizmos
		/// See: OnDrawGizmos
		/// </summary>
		public bool detailedGizmos;

		/// <summary>Path modifier which tweaks the start and end points of a path</summary>
		[HideInInspector]
		public StartEndModifier startEndModifier = new StartEndModifier();

		/// <summary>
		/// The tags which the Seeker can traverse.
		///
		/// Note: This field is a bitmask.
		/// See: bitmasks (view in online documentation for working links)
		/// </summary>
		[HideInInspector]
		public int traversableTags = -1;

		/// <summary>
		/// Penalties for each tag.
		/// Tag 0 which is the default tag, will have added a penalty of tagPenalties[0].
		/// These should only be positive values since the A* algorithm cannot handle negative penalties.
		///
		/// Note: This array should always have a length of 32 otherwise the system will ignore it.
		///
		/// See: Pathfinding.Path.tagPenalties
		/// </summary>
		[HideInInspector]
		public int[] tagPenalties = new int[32];

		/// <summary>
		/// Graphs that this Seeker can use.
		/// This field determines which graphs will be considered when searching for the start and end nodes of a path.
		/// It is useful in numerous situations, for example if you want to make one graph for small units and one graph for large units.
		///
		/// This is a bitmask so if you for example want to make the agent only use graph index 3 then you can set this to:
		/// <code> seeker.graphMask = 1 << 3; </code>
		///
		/// See: bitmasks (view in online documentation for working links)
		///
		/// Note that this field only stores which graph indices that are allowed. This means that if the graphs change their ordering
		/// then this mask may no longer be correct.
		///
		/// If you know the name of the graph you can use the <see cref="Pathfinding.GraphMask.FromGraphName"/> method:
		/// <code>
		/// GraphMask mask1 = GraphMask.FromGraphName("My Grid Graph");
		/// GraphMask mask2 = GraphMask.FromGraphName("My Other Grid Graph");
		///
		/// NNConstraint nn = NNConstraint.Default;
		///
		/// nn.graphMask = mask1 | mask2;
		///
		/// // Find the node closest to somePoint which is either in 'My Grid Graph' OR in 'My Other Grid Graph'
		/// var info = AstarPath.active.GetNearest(somePoint, nn);
		/// </code>
		///
		/// Some overloads of the <see cref="StartPath"/> methods take a graphMask parameter. If those overloads are used then they
		/// will override the graph mask for that path request.
		///
		/// [Open online documentation to see images]
		///
		/// See: multiple-agent-types (view in online documentation for working links)
		/// </summary>
		[HideInInspector]
		public GraphMask graphMask = GraphMask.everything;

		/// <summary>Used for serialization backwards compatibility</summary>
		[UnityEngine.Serialization.FormerlySerializedAs("graphMask")]
		int graphMaskCompatibility = -1;

		/// <summary>
		/// Callback for when a path is completed.
		/// Movement scripts should register to this delegate.\n
		/// A temporary callback can also be set when calling StartPath, but that delegate will only be called for that path
		/// </summary>
		public OnPathDelegate pathCallback;

		/// <summary>Called before pathfinding is started</summary>
		public OnPathDelegate preProcessPath;

		/// <summary>Called after a path has been calculated, right before modifiers are executed.</summary>
		public OnPathDelegate postProcessPath;

		/// <summary>Used for drawing gizmos</summary>
		[System.NonSerialized]
		List<Vector3> lastCompletedVectorPath;

		/// <summary>Used for drawing gizmos</summary>
		[System.NonSerialized]
		List<GraphNode> lastCompletedNodePath;

		/// <summary>The current path</summary>
		[System.NonSerialized]
		protected Path path;

		/// <summary>Previous path. Used to draw gizmos</summary>
		[System.NonSerialized]
		private Path prevPath;

		/// <summary>Cached delegate to avoid allocating one every time a path is started</summary>
		private readonly OnPathDelegate onPathDelegate;

		/// <summary>Temporary callback only called for the current path. This value is set by the StartPath functions</summary>
		private OnPathDelegate tmpPathCallback;

		/// <summary>The path ID of the last path queried</summary>
		protected uint lastPathID;

		/// <summary>Internal list of all modifiers</summary>
		readonly List<IPathModifier> modifiers = new List<IPathModifier>();

		public enum ModifierPass {
			PreProcess,
			// An obsolete item occupied index 1 previously
			PostProcess = 2,
		}

		public Seeker () {
			onPathDelegate = OnPathComplete;
		}

		/// <summary>Initializes a few variables</summary>
		protected override void Awake () {
			base.Awake();
			startEndModifier.Awake(this);
		}

		/// <summary>
		/// Path that is currently being calculated or was last calculated.
		/// You should rarely have to use this. Instead get the path when the path callback is called.
		///
		/// See: pathCallback
		/// </summary>
		public Path GetCurrentPath () {
			return path;
		}

		/// <summary>
		/// Stop calculating the current path request.
		/// If this Seeker is currently calculating a path it will be canceled.
		/// The callback (usually to a method named OnPathComplete) will soon be called
		/// with a path that has the 'error' field set to true.
		///
		/// This does not stop the character from moving, it just aborts
		/// the path calculation.
		/// </summary>
		/// <param name="pool">If true then the path will be pooled when the pathfinding system is done with it.</param>
		public void CancelCurrentPathRequest (bool pool = true) {
			if (!IsDone()) {
				path.FailWithError("Canceled by script (Seeker.CancelCurrentPathRequest)");
				if (pool) {
					// Make sure the path has had its reference count incremented and decremented once.
					// If this is not done the system will think no pooling is used at all and will not pool the path.
					// The particular object that is used as the parameter (in this case 'path') doesn't matter at all
					// it just has to be *some* object.
					path.Claim(path);
					path.Release(path);
				}
			}
		}

		/// <summary>
		/// Cleans up some variables.
		/// Releases any eventually claimed paths.
		/// Calls OnDestroy on the <see cref="startEndModifier"/>.
		///
		/// See: <see cref="ReleaseClaimedPath"/>
		/// See: <see cref="startEndModifier"/>
		/// </summary>
		public void OnDestroy () {
			ReleaseClaimedPath();
			startEndModifier.OnDestroy(this);
		}

		/// <summary>
		/// Releases the path used for gizmos (if any).
		/// The seeker keeps the latest path claimed so it can draw gizmos.
		/// In some cases this might not be desireable and you want it released.
		/// In that case, you can call this method to release it (not that path gizmos will then not be drawn).
		///
		/// If you didn't understand anything from the description above, you probably don't need to use this method.
		///
		/// See: pooling (view in online documentation for working links)
		/// </summary>
		void ReleaseClaimedPath () {
			if (prevPath != null) {
				prevPath.Release(this, true);
				prevPath = null;
			}
		}

		/// <summary>Called by modifiers to register themselves</summary>
		public void RegisterModifier (IPathModifier modifier) {
			modifiers.Add(modifier);

			// Sort the modifiers based on their specified order
			modifiers.Sort((a, b) => a.Order.CompareTo(b.Order));
		}

		/// <summary>Called by modifiers when they are disabled or destroyed</summary>
		public void DeregisterModifier (IPathModifier modifier) {
			modifiers.Remove(modifier);
		}

		/// <summary>
		/// Post Processes the path.
		/// This will run any modifiers attached to this GameObject on the path.
		/// This is identical to calling RunModifiers(ModifierPass.PostProcess, path)
		/// See: RunModifiers
		/// \since Added in 3.2
		/// </summary>
		public void PostProcess (Path path) {
			RunModifiers(ModifierPass.PostProcess, path);
		}

		/// <summary>Runs modifiers on a path</summary>
		public void RunModifiers (ModifierPass pass, Path path) {
			if (pass == ModifierPass.PreProcess) {
				if (preProcessPath != null) preProcessPath(path);

				for (int i = 0; i < modifiers.Count; i++) modifiers[i].PreProcess(path);
			} else if (pass == ModifierPass.PostProcess) {
				Profiler.BeginSample("Running Path Modifiers");
				// Call delegates if they exist
				if (postProcessPath != null) postProcessPath(path);

				// Loop through all modifiers and apply post processing
				for (int i = 0; i < modifiers.Count; i++) modifiers[i].Apply(path);
				Profiler.EndSample();
			}
		}

		/// <summary>
		/// Is the current path done calculating.
		/// Returns true if the current <see cref="path"/> has been returned or if the <see cref="path"/> is null.
		///
		/// Note: Do not confuse this with Pathfinding.Path.IsDone. They usually return the same value, but not always
		/// since the path might be completely calculated, but it has not yet been processed by the Seeker.
		///
		/// \since Added in 3.0.8
		/// Version: Behaviour changed in 3.2
		/// </summary>
		public bool IsDone () {
			return path == null || path.PipelineState >= PathState.Returned;
		}

		/// <summary>
		/// Called when a path has completed.
		/// This should have been implemented as optional parameter values, but that didn't seem to work very well with delegates (the values weren't the default ones)
		/// See: OnPathComplete(Path,bool,bool)
		/// </summary>
		void OnPathComplete (Path path) {
			OnPathComplete(path, true, true);
		}

		/// <summary>
		/// Called when a path has completed.
		/// Will post process it and return it by calling <see cref="tmpPathCallback"/> and <see cref="pathCallback"/>
		/// </summary>
		void OnPathComplete (Path p, bool runModifiers, bool sendCallbacks) {
			if (p != null && p != path && sendCallbacks) {
				return;
			}

			if (this == null || p == null || p != path)
				return;

			if (!path.error && runModifiers) {
				// This will send the path for post processing to modifiers attached to this Seeker
				RunModifiers(ModifierPass.PostProcess, path);
			}

			if (sendCallbacks) {
				p.Claim(this);

				lastCompletedNodePath = p.path;
				lastCompletedVectorPath = p.vectorPath;

				// This will send the path to the callback (if any) specified when calling StartPath
				if (tmpPathCallback != null) {
					tmpPathCallback(p);
				}

				// This will send the path to any script which has registered to the callback
				if (pathCallback != null) {
					pathCallback(p);
				}

				// Note: it is important that #prevPath is kept alive (i.e. not pooled)
				// if we are drawing gizmos.
				// It is also important that #path is kept alive since it can be returned
				// from the GetCurrentPath method.
				// Since #path will be copied to #prevPath it is sufficient that #prevPath
				// is kept alive until it is replaced.

				// Recycle the previous path to reduce the load on the GC
				if (prevPath != null) {
					prevPath.Release(this, true);
				}

				prevPath = p;
			}
		}


		/// <summary>
		/// Returns a new path instance.
		/// The path will be taken from the path pool if path recycling is turned on.\n
		/// This path can be sent to <see cref="StartPath(Path,OnPathDelegate,int)"/> with no change, but if no change is required <see cref="StartPath(Vector3,Vector3,OnPathDelegate)"/> does just that.
		/// <code>
		/// var seeker = GetComponent<Seeker>();
		/// Path p = seeker.GetNewPath (transform.position, transform.position+transform.forward*100);
		/// // Disable heuristics on just this path for example
		/// p.heuristic = Heuristic.None;
		/// seeker.StartPath (p, OnPathComplete);
		/// </code>
		/// Deprecated: Use ABPath.Construct(start, end, null) instead.
		/// </summary>
		[System.Obsolete("Use ABPath.Construct(start, end, null) instead")]
		public ABPath GetNewPath (Vector3 start, Vector3 end) {
			// Construct a path with start and end points
			return ABPath.Construct(start, end, null);
		}

		/// <summary>
		/// Call this function to start calculating a path.
		/// Since this method does not take a callback parameter, you should set the <see cref="pathCallback"/> field before calling this method.
		/// </summary>
		/// <param name="start">The start point of the path</param>
		/// <param name="end">The end point of the path</param>
		public Path StartPath (Vector3 start, Vector3 end) {
			return StartPath(start, end, null);
		}

		/// <summary>
		/// Call this function to start calculating a path.
		///
		/// callback will be called when the path has completed.
		/// Callback will not be called if the path is canceled (e.g when a new path is requested before the previous one has completed)
		/// </summary>
		/// <param name="start">The start point of the path</param>
		/// <param name="end">The end point of the path</param>
		/// <param name="callback">The function to call when the path has been calculated</param>
		public Path StartPath (Vector3 start, Vector3 end, OnPathDelegate callback) {
			return StartPath(ABPath.Construct(start, end, null), callback);
		}

		/// <summary>
		/// Call this function to start calculating a path.
		///
		/// callback will be called when the path has completed.
		/// Callback will not be called if the path is canceled (e.g when a new path is requested before the previous one has completed)
		/// </summary>
		/// <param name="start">The start point of the path</param>
		/// <param name="end">The end point of the path</param>
		/// <param name="callback">The function to call when the path has been calculated</param>
		/// <param name="graphMask">Mask used to specify which graphs should be searched for close nodes. See #Pathfinding.NNConstraint.graphMask. This will override #graphMask for this path request.</param>
		public Path StartPath (Vector3 start, Vector3 end, OnPathDelegate callback, GraphMask graphMask) {
			return StartPath(ABPath.Construct(start, end, null), callback, graphMask);
		}

		/// <summary>
		/// Call this function to start calculating a path.
		///
		/// The callback will be called when the path has been calculated (which may be several frames into the future).
		/// The callback will not be called if a new path request is started before this path request has been calculated.
		///
		/// Version: Since 3.8.3 this method works properly if a MultiTargetPath is used.
		/// It now behaves identically to the StartMultiTargetPath(MultiTargetPath) method.
		///
		/// Version: Since 4.1.x this method will no longer overwrite the graphMask on the path unless it is explicitly passed as a parameter (see other overloads of this method).
		/// </summary>
		/// <param name="p">The path to start calculating</param>
		/// <param name="callback">The function to call when the path has been calculated</param>
		public Path StartPath (Path p, OnPathDelegate callback = null) {
			// Set the graph mask only if the user has not changed it from the default value.
			// This is not perfect as the user may have wanted it to be precisely -1
			// however it is the best detection that I can do.
			// The non-default check is primarily for compatibility reasons to avoid breaking peoples existing code.
			// The StartPath overloads with an explicit graphMask field should be used instead to set the graphMask.
			if (p.nnConstraint.graphMask == -1) p.nnConstraint.graphMask = graphMask;
			StartPathInternal(p, callback);
			return p;
		}

		/// <summary>
		/// Call this function to start calculating a path.
		///
		/// The callback will be called when the path has been calculated (which may be several frames into the future).
		/// The callback will not be called if a new path request is started before this path request has been calculated.
		///
		/// Version: Since 3.8.3 this method works properly if a MultiTargetPath is used.
		/// It now behaves identically to the StartMultiTargetPath(MultiTargetPath) method.
		/// </summary>
		/// <param name="p">The path to start calculating</param>
		/// <param name="callback">The function to call when the path has been calculated</param>
		/// <param name="graphMask">Mask used to specify which graphs should be searched for close nodes. See #Pathfinding.GraphMask. This will override #graphMask for this path request.</param>
		public Path StartPath (Path p, OnPathDelegate callback, GraphMask graphMask) {
			p.nnConstraint.graphMask = graphMask;
			StartPathInternal(p, callback);
			return p;
		}

		/// <summary>Internal method to start a path and mark it as the currently active path</summary>
		void StartPathInternal (Path p, OnPathDelegate callback) {
			p.callback += onPathDelegate;

			p.enabledTags = traversableTags;
			p.tagPenalties = tagPenalties;

			// Cancel a previously requested path is it has not been processed yet and also make sure that it has not been recycled and used somewhere else
			if (path != null && path.PipelineState <= PathState.Processing && path.CompleteState != PathCompleteState.Error && lastPathID == path.pathID) {
				path.FailWithError("Canceled path because a new one was requested.\n"+
					"This happens when a new path is requested from the seeker when one was already being calculated.\n" +
					"For example if a unit got a new order, you might request a new path directly instead of waiting for the now" +
					" invalid path to be calculated. Which is probably what you want.\n" +
					"If you are getting this a lot, you might want to consider how you are scheduling path requests.");
				// No callback will be sent for the canceled path
			}

			// Set p as the active path
			path = p;
			tmpPathCallback = callback;

			// Save the path id so we can make sure that if we cancel a path (see above) it should not have been recycled yet.
			lastPathID = path.pathID;

			// Pre process the path
			RunModifiers(ModifierPass.PreProcess, path);

			// Send the request to the pathfinder
			AstarPath.StartPath(path);
		}


		/// <summary>Draws gizmos for the Seeker</summary>
		public void OnDrawGizmos () {
			if (lastCompletedNodePath == null || !drawGizmos) {
				return;
			}

			if (detailedGizmos) {
				Gizmos.color = new Color(0.7F, 0.5F, 0.1F, 0.5F);

				if (lastCompletedNodePath != null) {
					for (int i = 0; i < lastCompletedNodePath.Count-1; i++) {
						Gizmos.DrawLine((Vector3)lastCompletedNodePath[i].position, (Vector3)lastCompletedNodePath[i+1].position);
					}
				}
			}

			Gizmos.color = new Color(0, 1F, 0, 1F);

			if (lastCompletedVectorPath != null) {
				for (int i = 0; i < lastCompletedVectorPath.Count-1; i++) {
					Gizmos.DrawLine(lastCompletedVectorPath[i], lastCompletedVectorPath[i+1]);
				}
			}
		}

		protected override int OnUpgradeSerializedData (int version, bool unityThread) {
			if (graphMaskCompatibility != -1) {
				Debug.Log("Loaded " + graphMaskCompatibility + " " + graphMask.value);
				graphMask = graphMaskCompatibility;
				graphMaskCompatibility = -1;
			}
			return base.OnUpgradeSerializedData(version, unityThread);
		}
	}
}
