using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Gameplay : MonoBehaviour {

	public static Player player1;
	public static Player player2;

	public Building baseBuilding;

	public GameObject defeatMenu;

	// Use this for initialization
	public AudioClip notEnoughResource1;
	public AudioClip notEnoughResource2;
	public AudioClip cantBuildHere;
	public AudioClip notEnoughSupply;

	public static Transform garbageCollector;

	public AudioClip[] music;

	public static Gameplay st;

	private bool gameEnded = false;

	private float startGame;

	void Awake () {
		Debug.Log ("Initializing amplitude metrics");
		Amplitude amplitude = Amplitude.Instance;
		amplitude.logging = true;
		amplitude.init("cc640845a5016ce9f116ba3d90c95ae7");
	}

	void Start () {

		startGame = Time.time;
		//adjustMinimap ();
		defeatMenu.SetActive(false);
		
		garbageCollector = GameObject.Find ("GarbageCollector").transform;
		//player1 = new Human("player1");
		//player2 = new AiRush("player2");
		//player2 = new Human("player2");
		player1 = GameObject.Find("player1").GetComponent<Player>();
		player2 = GameObject.Find("player2").GetComponent<Player>();
		
		Debug.Log ("player1 " + player1);
		Debug.Log ("player2 " + player2);
		
		//addPlayerObject("player1");
		//addPlayerObject("player2");

		createBases();
		createUnits();

		//createUnits ();
		Gameplay.st = this;
	
		//Set main camera to player1base
		//CameraControl.moveCamera(player1.mainBase.transform.position - new Vector3(0,0,5) );

		GetComponent<AudioSource>().PlayOneShot(music[0]);

		
		//DEBUG
		Vector3 position = new Vector3(80,0,30);
		//CameraControl.moveCamera(position);
		
		//createAllUnits(position, "player1"); 
		position.z+=20;
		//createAllBuildings(position, "player1"); 
		position.z+=20;
		//createAllUnits(position, "player2"); 
	}

	private void adjustMinimap (){
		float width = 558;
		float ratio = 0.2f;


		float newRatio = (Screen.width * ratio) / width;
		newRatio = 0.2f - newRatio;
		//Debug.Log ("Screen width " + Screen.width + " new ratio " + newRatio);
		Camera camera = GameObject.Find ("Minimap").GetComponent<Camera> ();

		//Debug.Log ("Camera size " + camera.rect.size);
		
		Rect r = camera.rect;
		r.width = newRatio;
		camera.rect = r;
	}

	private void createAllUnits(Vector3 position, string tag){

		GameObject units = GameObject.Find("Units");
		foreach (Transform race in units.transform) {
			Debug.Log ("Creating race unit " + race.name);
			foreach (Transform t in race.transform){
				Debug.Log ("Creating debug unit " + t.name+ " with tag " + tag);
				Unit u = t.GetComponent<Unit>();
				Unit.create(u, position,tag);
				position.x += 2f;
			}
		}
	}
	private void createAllBuildings(Vector3 position, string tag){
		
		GameObject buildings = GameObject.Find("Buildings");
		foreach (Transform t in buildings.transform) {
				Debug.Log ("Creating debug building " + t.name+ " with tag " + tag);
				Building u = t.GetComponent<Building>();
				Building.create(u, position,tag);
				position.x += 5f;
		}
	}
	
	private void createUnits(){
		//TODO Get proper gameobject per race
		Unit baseUnit = GameObject.Find("Worker").GetComponent<Unit>();
	
		player1.mainBase.initDeploy();
		Debug.Log ("Main base deploy " + player1.mainBase.deploy);
		
		for (int i = 0; i < 4; i++){
			Vector3 desp = new Vector3(i*2, 0,0);
			Unit.create (baseUnit, player1.mainBase.deploy+desp , player1.PLAYER_TAG);
			//Unit.create (baseUnit, player2.mainBase.deploy.transform.position+desp, player2.PLAYER_TAG);
		}		
	}
	private void createBases(){
		GameObject[] larray = GameObject.FindGameObjectsWithTag("BaseLocation");
		List<GameObject> locations = new List<GameObject> ();
		locations.AddRange(larray);
		
		//TODO: Proper random location
		
		GameObject l1 = Utils.random(larray);
		locations.Remove(l1);
		GameObject l2 = Utils.random (locations.ToArray());
		
		player1.mainBase = Building.create(baseBuilding, l1.transform.position, "player1");
		Debug.Log ("Main base " + player1.mainBase.transform.position);
		//player2.mainBase = Building.create(baseBuilding, l2.transform.position, "player2");
		
	}
	
	void Update(){
		//player2.update();
		//adjustMinimap ();
		if (isGameEnd() && !gameEnded) gameEnd();
	}


	private bool isGameEnd(){
		return player1.mainBase == null;
	}

	private void gameEnd(){
		gameEnded = true;
		defeatMenu.SetActive(true);

		//Send amplitude stats
		float timeAlive = Time.time ;
		Dictionary<string, object> demoOptions = new Dictionary<string, object>() {
			{"time" , Time.time }
		};
		Amplitude.Instance.logEvent("game-end", demoOptions);

	}

	public static Player getPlayer(string tag){
		switch (tag){
			case "player1": return player1;
			case "player2": return player2;
			default: return null;
		}
	}
	
	//TODO make it scalable (more players)
	public static string enemies(string tag){
		switch (tag) {
		case "player1": return "player2"; break;
		case "player2": return "player1"; break;
		}
		return null;
	}
}
