using UnityEngine;
using System.Collections;
using Pathfinding;

public class DoorController : MonoBehaviour {
	
	private bool open = false;
	
	public int opentag = 1;
	public int closedtag = 1;
	public bool updateGraphsWithGUO = true;
	public float yOffset = 5;
	
	Bounds bounds;
	
	public void Start () {
		bounds = GetComponent<Collider>().bounds;
		SetState (open);
	}
	
	// Use this for initialization
	void OnGUI () {
		
		if (GUI.Button (new Rect (5,yOffset,100,22), "Toggle Door")) {
			SetState (!open);
		}
	}
	
	public void SetState (bool open) {
		this.open = open;
		
		if (updateGraphsWithGUO) {
			GraphUpdateObject guo = new GraphUpdateObject(bounds);
			int tag = open ? opentag : closedtag;
			if (tag > 31) { Debug.LogError ("tag > 31"); return; }
			guo.modifyTag = true;
			guo.setTag = tag;
			guo.updatePhysics = false;
			
			AstarPath.active.UpdateGraphs (guo);
		}
		
		if (open) {
			GetComponent<Animation>().Play ("Open");
		} else {
			GetComponent<Animation>().Play ("Close");
		}
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
