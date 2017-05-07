using UnityEngine;
using System.Collections;
using System;
using UnityEngine.UI;

public class Controller : MonoBehaviour {
	
	private int LONG_PRESS_TIME = 1; // in seconds
	private int DRAG_SENSITIVITY = 1;

	static private bool drag;
	private bool down;
	private float downTime ;
	private bool longPress;
	private Vector3 downPosition;
	private Vector3 dragPosition;
	GUIStyle selectionStyle ;
	bool minimap;
	bool isMobile;
	static bool pinch;
	private Vector3 pinchPosition1;
	private Vector3 pinchPosition2;

	private Image selection;

	// ------------------------------------
  // MONOBEHAVIOUR METHODS
  // ------------------------------------

	void Start(){
		isMobile = SystemInfo.deviceType == DeviceType.Handheld;

		selection = GameObject.Find ("SelectionUI").GetComponent<Image>();
	}
	void OnMouseDown(){
		down = true;
		downTime = Time.time;
		longPress = false;
		drag = false;
		pinch = false;

		downPosition = Input.mousePosition;
		dragPosition = Input.mousePosition;

		if (GameObject.Find("Minimap") != null) minimap = GameObject.Find("Minimap").GetComponent<Camera>().pixelRect.Contains (downPosition);
	}   

	void OnMouseUp(){

		bool wasDrag = drag;
		bool wasPinch = pinch;

		down = false;
		drag = false;
		pinch = false;
		longPress = false;
		minimap = false;

		if (minimap) {}
		else if (wasDrag) {
			if (!wasPinch && !isMobile) multipleSelect (downPosition, Input.mousePosition);
		} else if (wasPinch) {
			multipleSelect (pinchPosition1, pinchPosition2);
		} else if (isMobile) {
			Vector3 pos = Utils.screen2world( downPosition); 
			Unit.moveSelected (pos);
		}
	}

	void dragCamera(){
		Vector3 from = Utils.screen2world (downPosition);
		Vector3 to = Utils.screen2world (Input.mousePosition);

		Vector3 newVector = from - to;
		CameraControl.UpdateCamera (newVector / 10);
	}

	void multipleSelect(Vector3 fromScreen, Vector3 toScreen){
		//Convert gui rectangle to world rectangle
		Vector3 from = Utils.screen2world (fromScreen);
		Vector3 to = Utils.screen2world (toScreen);

		Rect r = new Rect (Mathf.Min (from.x, to.x), Mathf.Min (from.z, to.z), 
                   Mathf.Abs (to.x - from.x),
                   Mathf.Abs (to.z - from.z));

		int selected = 0;
		//Select all units inside rect
		foreach (Unit u in Unit.allUnits<Unit>(Gameplay.player1.PLAYER_TAG)) {
				Vector2 position = new Vector2 (u.transform.position.x, u.transform.position.z);
				if (r.Contains (position)) {
					if (selected == 0)	Unit.unselectAll ();
					u.select ();
					selected++;
				}
		}
		//Debug
		//GameObject.CreatePrimitive(PrimitiveType.Cube).transform.position = from;
		//GameObject.CreatePrimitive(PrimitiveType.Cube).transform.position = to;

	}

	void OnMouseOver(){
		if (minimap){
			if (Input.GetMouseButtonDown(0) ) {
				Vector3 pos = CameraControl.minimapMousePosition();
				CameraControl.moveCamera(pos);
			} else if (Input.GetMouseButtonDown(1)){ //Only desktop right click
				Vector3 pos = CameraControl.minimapMousePosition();
				Unit.moveSelected (pos);
			}
		}

		if (Input.GetMouseButtonDown(1) && !isMobile) {
			Vector3 pos = Utils.screen2world( Input.mousePosition); 
			Unit.moveSelected (pos);
		}

		if (drag && isMobile) dragCamera();		
	}
	void Update () {
		
		Utils.debugText("Touch " + Input.touchCount + " drag " + drag + " pinch " + pinch + " down " + down);
		//Force touch up update
		if (isMobile && (down || drag || pinch) && Input.touchCount == 0) OnMouseUp();

		UpdateSelectionBox();
		if (down && !pinch && (dragPosition - Input.mousePosition).magnitude > DRAG_SENSITIVITY) Drag ();
		else if (Input.touchCount >= 2) Pinch ();

		else if (!longPress  && down && downTime + LONG_PRESS_TIME< Time.time )  LongPress();
		else if (minimap) {
			pinch = false;
			//Limit camera movement
			Vector3 pos = CameraControl.minimapMousePosition();
			CameraControl.moveCamera(pos);
		}
	}

	void UpdateSelectionBox(){
		if (minimap) {}
		else if (drag) {
			if (!pinch && !isMobile) drawSelection(downPosition, Input.mousePosition);
			
		} else if (pinch) drawSelection (pinchPosition1, pinchPosition2);
		else selection.enabled = false;
	}

	void drawSelection(Vector3 p1, Vector3 p2){
		selection.enabled = true;
		
		Vector2 leftUp = new Vector2(Math.Min (p1.x, p2.x), Math.Min (p1.y, p2.y) );
		Vector2 rightDown = new Vector2 (Math.Max (p1.x, p2.x), Math.Max (p1.y, p2.y));

		Debug.Log ("Drawing rectange from " + leftUp + " to " + rightDown);
		Debug.DrawLine (leftUp, rightDown);
		//Utils.drawDebugRectangle(p1,p2);
			
		selection.rectTransform.position = leftUp;
		
		Vector2 size = rightDown - leftUp;
				
		selection.transform.position = leftUp;
		selection.rectTransform.sizeDelta = size;

	}
	// ------------------------------------
  // HELPER METHODS
  // ------------------------------------
	void Pinch(){
		pinch = true;
		pinchPosition1 = Input.GetTouch(0).position;
		pinchPosition2 = Input.GetTouch(1).position;
	}
	void Drag(){
		drag = true;
		dragPosition = Input.mousePosition;
	}
  void LongPress(){

    longPress = true;
    Debug.Log ("Long press");
  }
  void startStyle() {
    selectionStyle = new GUIStyle( GUI.skin.box );
    selectionStyle.normal.background = Utils.MakeTex( 2, 2, new Color( 0f, 1f, 0f, 0.5f ) );
    selectionStyle.border = new RectOffset(0,0,0,0);
  }

	//Right click on desktop
	//Single touch on mobile
	public static bool RightClickOrTouch(){
		return (Input.GetMouseButtonDown(1) || Input.touchCount > 0);
	}
}
