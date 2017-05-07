using UnityEngine;
using System.Collections;

public class CameraControl : MonoBehaviour {

	private LineRenderer lr ;

	private float speed = 50.0f;
	private const float ZOOM_SPEED = 200.0f;

	private const float MAX_ZOOM = 30;
	private const float MIN_ZOOM = 10;
	
	private static Bounds terrainBounds;

	private static GameObject staticCamera;

	void Start(){

		terrainBounds = GameObject.Find ("CameraMovement").GetComponent<Collider>().bounds;//.GetComponent<Terrain>();
		Debug.Log ("Terrain dimensions " + terrainBounds);
		staticCamera = this.gameObject;

		//Initialize camera on base 1
		CameraControl.moveCamera(Gameplay.player1.mainBase.transform.position - new Vector3(0,0,5) );
		
	}

	void Update()
	{
		MoveCameraArrows ();
		Zoom ();
	}

	private void MoveCameraArrows(){
		float moveSpeed = speed * Time.deltaTime;
		Vector3 move = Vector3.zero;
		
		if(Input.GetKey(KeyCode.RightArrow)) move = Vector3.right * moveSpeed;
		if(Input.GetKey(KeyCode.LeftArrow)) move = Vector3.left * moveSpeed;
		if(Input.GetKey(KeyCode.DownArrow)) move = Vector3.back * moveSpeed;
		if(Input.GetKey(KeyCode.UpArrow)) move = Vector3.forward * moveSpeed;
		
		UpdateCamera(move);
	}

	public static Vector3 minimapMousePosition(){
		Camera minimap = GameObject.Find("Minimap").GetComponent<Camera>();
		
		Ray ray = minimap.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit ;
		Physics.Raycast(ray, out hit);
		return hit.point;
	}
	
	public void Zoom(){		
		float scroll = Input.GetAxis ("Mouse ScrollWheel") * ZOOM_SPEED * Time.deltaTime;

		float newY = transform.position.y - scroll;
		if (newY < MAX_ZOOM && newY > MIN_ZOOM) transform.position += Vector3.down * scroll;
	}
	public static void moveCamera(Vector3 position){

		Vector3 oldPosition = staticCamera.transform.position; //keep y
		Debug.Log("moving camera from " + oldPosition + " to " + position );
		
		oldPosition.x = position.x;
		oldPosition.z= position.z;
		staticCamera.transform.position = oldPosition;
	}

	public static void UpdateCamera(Vector3 move){
		if(InsideTerrainBoundaries(staticCamera.transform.position + move)) {
			move.y = 0;
			staticCamera.transform.position+= move;
		}
	}
	private static bool InsideTerrainBoundaries(Vector3 newPosition){
		newPosition.y = 0;
		return terrainBounds.Contains (newPosition);
	}
	
}
