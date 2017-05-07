//#define ASTARDEBUG //Enable for some debug lines showing the DynamicGridObstacle activity

using UnityEngine;
using System.Collections;
using Pathfinding;

/** Attach this script to any obstacle with a collider to enable dynamic updates of the graphs around it.
 * When the object has moved a certain distance (or actually when it's bounding box has changed by a certain amount) defined by #updateError
 * it will call AstarPath.UpdateGraphs and update the graph around it.
 * 
 * \note This script does only work with GridGraph, PointGraph and LayerGridGraph
 * 
 * \see AstarPath.UpdateGraphs
 */
public class DynamicGridObstacle : MonoBehaviour {
	
	Collider col;
	public float updateError = 1; /**< The minimum change along one of the axis of the bounding box of collider to trigger a graph update */
	public float checkTime = 0.2F; /**< Time in seconds between bounding box checks */
	
	/** Use this for initialization */
	void Start () {
		col = GetComponent<Collider>();
		if (GetComponent<Collider>() == null) {
			Debug.LogError ("A collider must be attached to the GameObject for DynamicGridObstacle to work");
		}
		StartCoroutine (UpdateGraphs ());
	}
	
	Bounds prevBounds;
	bool isWaitingForUpdate = false;
	
	/** Coroutine which checks for changes in the collider's bounding box */
	IEnumerator UpdateGraphs () {
		
		if (col == null || AstarPath.active == null) {
			Debug.LogWarning ("No collider is attached to the GameObject. Canceling check");
			yield break;
		}
		
		//Perform update checks while there is a collider attached to the GameObject
		while (col) {
			
			while (isWaitingForUpdate) {
				yield return new WaitForSeconds (checkTime);
			}
			
			//The current bounds
			Bounds newBounds = col.bounds;
			
			//The combined bounds of the previous bounds and the new bounds
			Bounds merged = newBounds;
			merged.Encapsulate (prevBounds);
			
			Vector3 minDiff = merged.min - newBounds.min;
			Vector3 maxDiff = merged.max - newBounds.max;
			
			//If the difference between the previous bounds and the new bounds is greater than some value, update the graphs
			if (Mathf.Abs (minDiff.x) > updateError || Mathf.Abs (minDiff.y) > updateError || Mathf.Abs (minDiff.z) > updateError ||
			   	Mathf.Abs (maxDiff.x) > updateError || Mathf.Abs (maxDiff.y) > updateError || Mathf.Abs (maxDiff.z) > updateError) {
				
				//Update the graphs as soon as possible
				//DoUpdateGraphs and DoUpdateGraphs2 will be called (in order) when there is an opportunity to update graphs
				isWaitingForUpdate = true;
				/** \bug Fix Update Graph Passes */
				DoUpdateGraphs ();
				//AstarPath.active.RegisterCanUpdateGraphs (DoUpdateGraphs, DoUpdateGraphs2);
				
			}
			
			yield return new WaitForSeconds (checkTime);
		}
		
		//The collider object has been removed from the GameObject, pretend the object has been destroyed
		OnDestroy ();
	}
	
	/** Revert graphs when destroyed.
	 * When the DynamicObstacle is destroyed, a last graph update should be done to revert nodes to their original state */
	public void OnDestroy () {
		if (AstarPath.active != null) {
			GraphUpdateObject guo = new GraphUpdateObject (prevBounds);
			AstarPath.active.UpdateGraphs (guo);
		}
	}
	
	public void DoUpdateGraphs () {
		if (col == null) { return; }
		
		isWaitingForUpdate = false;
		Bounds newBounds = col.bounds;
		
		//if (!simple) {
			Bounds merged = newBounds;
			merged.Encapsulate (prevBounds);
			
			if (BoundsVolume (merged) < BoundsVolume (newBounds)+BoundsVolume(prevBounds)) {
				AstarPath.active.UpdateGraphs (merged);
			} else {
				AstarPath.active.UpdateGraphs (prevBounds);
				AstarPath.active.UpdateGraphs (newBounds);
			}
		/*} else {
			GraphUpdateObject guo = new GraphUpdateObject (prevBounds);
			guo.updatePhysics = false;
			guo.modifyWalkability = true;
			guo.setWalkability = true;
			
			AstarPath.active.UpdateGraphs (guo);
		}*/
		
		
		prevBounds = newBounds;
	}
	
	/*public void DoUpdateGraphs2 () {
		if (col == null) { return; }
		
		if (simple) {
			GraphUpdateObject guo = new GraphUpdateObject (col.bounds);
			guo.updatePhysics = false;
			guo.modifyWalkability = true;
			guo.setWalkability = false;
			AstarPath.active.UpdateGraphs (guo);
		}
	}*/
	
	/* Returns a new Bounds object which contains both \a b1 and \a b2 */
	/*public Rect ExpandToContain (Bounds b1, Bounds b2) {
		Vector3 min = Vector3.Min (b1.min,b2.min);
		Vector3 max = Vector3.Max (b1.max,b2.max);
		
		return new Bounds ((max+min)*0.5F,max-min);
	}*/
	
	/** Returns the volume of a Bounds object. X*Y*Z */
	public float BoundsVolume (Bounds b) {
		return System.Math.Abs (b.size.x * b.size.y * b.size.z);
	}
}
