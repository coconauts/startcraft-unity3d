using UnityEngine;

namespace Pathfinding {
	/// <summary>
	/// Attach this script to any obstacle with a collider to enable dynamic updates of the graphs around it.
	/// When the object has moved a certain distance (or actually when it's bounding box has changed by a certain amount) defined by <see cref="updateError"/>
	/// it will call AstarPath.UpdateGraphs and update the graph around it.
	///
	/// Make sure that any children colliders do not extend beyond the bounds of the collider attached to the
	/// GameObject that the DynamicGridObstacle component is attached to since this script only updates the graph
	/// around the bounds of the collider on the same GameObject.
	///
	/// This script works with both 2D colliders and normal 3D colliders.
	///
	/// Note: This script only works with GridGraph, PointGraph and LayerGridGraph
	///
	/// See: AstarPath.UpdateGraphs
	/// See: graph-updates
	/// </summary>
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_dynamic_grid_obstacle.php")]
	public class DynamicGridObstacle : GraphModifier {
		/// <summary>Collider to get bounds information from</summary>
		Collider coll;

		/// <summary>2D Collider to get bounds information from</summary>
		Collider2D coll2D;

		/// <summary>Cached transform component</summary>
		Transform tr;

		/// <summary>The minimum change in world units along one of the axis of the bounding box of the collider to trigger a graph update</summary>
		public float updateError = 1;

		/// <summary>
		/// Time in seconds between bounding box checks.
		/// If AstarPath.batchGraphUpdates is enabled, it is not beneficial to have a checkTime much lower
		/// than AstarPath.graphUpdateBatchingInterval because that will just add extra unnecessary graph updates.
		///
		/// In real time seconds (based on Time.realtimeSinceStartup).
		/// </summary>
		public float checkTime = 0.2F;

		/// <summary>Bounds of the collider the last time the graphs were updated</summary>
		Bounds prevBounds;

		/// <summary>Rotation of the collider the last time the graphs were updated</summary>
		Quaternion prevRotation;

		/// <summary>True if the collider was enabled last time the graphs were updated</summary>
		bool prevEnabled;

		float lastCheckTime = -9999;

		Bounds bounds {
			get {
				if (coll != null) {
					return coll.bounds;
				} else {
					var b = coll2D.bounds;
					// Make sure the bounding box stretches close to infinitely along the Z axis (which is the axis perpendicular to the 2D plane).
					// We don't want any change along the Z axis to make a difference.
					b.extents += new Vector3(0, 0, 10000);
					return b;
				}
			}
		}

		bool colliderEnabled {
			get {
				return coll != null ? coll.enabled : coll2D.enabled;
			}
		}

		protected override void Awake () {
			base.Awake();

			coll = GetComponent<Collider>();
			coll2D = GetComponent<Collider2D>();
			tr = transform;
			if (coll == null && coll2D == null && Application.isPlaying) {
				throw new System.Exception("A collider or 2D collider must be attached to the GameObject(" + gameObject.name + ") for the DynamicGridObstacle to work");
			}

			prevBounds = bounds;
			prevRotation = tr.rotation;
			// Make sure we update the graph as soon as we find that the collider is enabled
			prevEnabled = false;
		}

		public override void OnPostScan () {
			// Make sure we find the collider
			// AstarPath.Awake may run before Awake on this component
			if (coll == null) Awake();

			// In case the object was in the scene from the start and the graphs
			// were scanned then we ignore the first update since it is unnecessary.
			if (coll != null) prevEnabled = colliderEnabled;
		}

		void Update () {
			if (!Application.isPlaying) return;

			if (coll == null && coll2D == null) {
				Debug.LogError("Removed collider from DynamicGridObstacle", this);
				enabled = false;
				return;
			}

			if (AstarPath.active == null || AstarPath.active.isScanning || Time.realtimeSinceStartup - lastCheckTime < checkTime || !Application.isPlaying) {
				return;
			}

			lastCheckTime = Time.realtimeSinceStartup;
			if (colliderEnabled) {
				// The current bounds of the collider
				Bounds newBounds = bounds;
				var newRotation = tr.rotation;

				Vector3 minDiff = prevBounds.min - newBounds.min;
				Vector3 maxDiff = prevBounds.max - newBounds.max;

				var extents = newBounds.extents.magnitude;
				// This is the distance that a point furthest out on the bounding box
				// would have moved due to the changed rotation of the object
				var errorFromRotation = extents*Quaternion.Angle(prevRotation, newRotation)*Mathf.Deg2Rad;

				// If the difference between the previous bounds and the new bounds is greater than some value, update the graphs
				if (minDiff.sqrMagnitude > updateError*updateError || maxDiff.sqrMagnitude > updateError*updateError ||
					errorFromRotation > updateError || !prevEnabled) {
					// Update the graphs as soon as possible
					DoUpdateGraphs();
				}
			} else {
				// Collider has just been disabled
				if (prevEnabled) {
					DoUpdateGraphs();
				}
			}
		}

		/// <summary>
		/// Revert graphs when disabled.
		/// When the DynamicObstacle is disabled or destroyed, a last graph update should be done to revert nodes to their original state
		/// </summary>
		protected override void OnDisable () {
			base.OnDisable();
			if (AstarPath.active != null && Application.isPlaying) {
				var guo = new GraphUpdateObject(prevBounds);
				AstarPath.active.UpdateGraphs(guo);
				prevEnabled = false;
			}
		}

		/// <summary>
		/// Update the graphs around this object.
		/// Note: The graphs will not be updated immediately since the pathfinding threads need to be paused first.
		/// If you want to guarantee that the graphs have been updated then call AstarPath.active.FlushGraphUpdates()
		/// after the call to this method.
		/// </summary>
		public void DoUpdateGraphs () {
			if (coll == null && coll2D == null) return;

			if (!colliderEnabled) {
				// If the collider is not enabled, then col.bounds will empty
				// so just update prevBounds
				AstarPath.active.UpdateGraphs(prevBounds);
			} else {
				Bounds newBounds = bounds;

				Bounds merged = newBounds;
				merged.Encapsulate(prevBounds);

				// Check what seems to be fastest, to update the union of prevBounds and newBounds in a single request
				// or to update them separately, the smallest volume is usually the fastest
				if (BoundsVolume(merged) < BoundsVolume(newBounds) + BoundsVolume(prevBounds)) {
					// Send an update request to update the nodes inside the 'merged' volume
					AstarPath.active.UpdateGraphs(merged);
				} else {
					// Send two update request to update the nodes inside the 'prevBounds' and 'newBounds' volumes
					AstarPath.active.UpdateGraphs(prevBounds);
					AstarPath.active.UpdateGraphs(newBounds);
				}

#if ASTARDEBUG
				Debug.DrawLine(prevBounds.min, prevBounds.max, Color.yellow);
				Debug.DrawLine(newBounds.min, newBounds.max, Color.red);
#endif
				prevBounds = newBounds;
			}

			prevEnabled = colliderEnabled;
			prevRotation = tr.rotation;

			// Set this here as well since the DoUpdateGraphs method can be called from other scripts
			lastCheckTime = Time.realtimeSinceStartup;
		}

		/// <summary>Volume of a Bounds object. X*Y*Z</summary>
		static float BoundsVolume (Bounds b) {
			return System.Math.Abs(b.size.x * b.size.y * b.size.z);
		}
	}
}
