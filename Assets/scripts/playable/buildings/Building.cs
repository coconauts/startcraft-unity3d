using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class Building : Playable {

	// ------------------------------------
	// Defined in scene
	// ------------------------------------
	public StrategyObject[] developable;
	
	// ------------------------------------
	// Class variables
	// ------------------------------------
	//
	
	public List<StrategyObject> trainingQueue;
	private float trainingStart;
	protected List<GameObject> collissions;
	public bool moving = false;
	private float buildingProgress;
	public bool isBuilding = false;
	public Canvas canvas;
	public Image canvasProgress;
	public bool canBuild = false;
	public GameObject deployObject;
	public Vector3 deploy; 

	
	public List<CancelTrainUnit> cancelButtons;

	// ------------------------------------
	// CREATION AND MODEL MODIFICATIONS
	// ------------------------------------
	
	private new void Start () {
		base.Start();

		Debug.Log("Start building " +name);
		collissions = new List<GameObject>();
		trainingQueue = new List<StrategyObject>();
		cancelButtons = new List<CancelTrainUnit>();

		initDeploy();

		Transform canvasObject = transform.Find("Canvas");//Utils.getChildren(transform,"Canvas");
		if (canvasObject != null){
			canvas = canvasObject.GetComponent<Canvas>();
			canvasProgress =  canvas.transform.Find ("Progress").GetComponent<Image>();
			canvas.enabled = false;
		}
		
	}

	public void initDeploy(){
		
		deploy =  transform.position + new Vector3(0,1,-5);
		//deploy = this.transform.position;
		//Debug.Log("transform.position "+transform.position);
		//deploy.x -= 5;
/*
		deployObject = new GameObject();
		deployObject.name = "deploy";
		deployObject.transform.parent = this.transform;
		deployObject.transform.position = deploy;

		Debug.Log("Position on "+deploy);*/
		
	}
	// ------------------------------------
	// CONTROLS
	// ------------------------------------
	
	//http://answers.unity3d.com/questions/18875/right-mouse-detection.html
	protected override void OnMouseOver () {
		if (!moving) base.OnMouseOver();
	
		//Restart building
		if ( Controller.RightClickOrTouch() &&
		    tag != Gameplay.player1.PLAYER_TAG &&
		    isBuilding) {
			Worker w = Selected<Worker>()[0];
			w.Build(this);
		}
	}
	// ------------------------------------
	// BUILDING PLACEMENT
	// ------------------------------------

	//Collission detection
	void OnTriggerEnter(Collider other){
		
		if (moving /*&& other.name != "Terrain"*/) {
			collissions.Add(other.gameObject);
			Debug.Log ("OnTriggerEnter with " +other.name+ ": "+collissions.Count);
			canBuild = false;
		}
	}
	void OnTriggerExit(Collider other){
		
		if (moving /*&& other.name != "Terrain"*/) {
			
			collissions.Remove(other.gameObject);
			Debug.Log ("OnTriggerExit with " +other.name+ ": "+ collissions.Count);
			if (collissions.Count == 0) canBuild = true; 
			
		}
	}

	/**
	* Building is finish
	*/
	public void BuildFinish(){
		tag = Gameplay.player1.PLAYER_TAG;
		isBuilding = false;
		LayerModel(0); //default layer
		UpdatePath();
		InitBuilding();

		//Job finished
		Debug.Log ("Building finish " + name);
	}
	/**
	* Inmediately creates a building, do not use in game
	*/
	public static Building create(Building building, Vector3 deploy, string tag){

		Building go = (Building) Unit.create( building, deploy, tag);

		go.InitBuilding();
		go.UpdatePath();
		return go;
	}

	/**
	* Start building by a worker
	*/
	public void BuildStart(){
		
		Debug.Log ("Building " + this.name+ " init");
		//TODO assign player tag at the begginign (so enemy can attack the building)
		colorModel(Player.getColor("player1"));
		GetComponent<Collider>().isTrigger = false;
		transform.position = Utils.terrainHeight(transform.position);
		moving  = false;
		UpdatePath();

		Worker w = Selected<Worker>()[0];
		w.Build(this);
		isBuilding = true;
	}
	//TODO Destroy path on Die
	public void UpdatePath(){
		AstarPath.active.UpdateGraphs(this.GetComponent<Collider>().bounds);
	}
	public virtual void InitBuilding(){
		//Do something on building creation (eg: SUpply depots)
	}	

	public static Building BuildPlace(Building building){
		
		Building b = (Building) Unit.create( building, Vector3.zero, Gameplay.player1.PLAYER_TAG);
		b.colorModel(Color.green);
		b.GetComponent<Collider>().isTrigger = true;
		b.moving  = true;

		Color c = Color.green;
		c.a = 0.5f;
		b.colorModel(c );

		b.LayerModel(8); //wireframe
		return b;

	}

	public Building Construct(){
		buildingProgress += Time.deltaTime ;
		if (buildingProgress > trainingTime) {
			BuildFinish();
			return null;
		}
		return this;
	}

	// ------------------------------------
	// DRAW
	// ------------------------------------
	
	public override void OnGUI() {
		base.OnGUI();
    	//Other methods to draw images
		//GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height), image);
		//GUI.Label(new Rect(0, 0, 128, 128), image);
		/*Vector3 rect = Utils.world2screen( this.transform.position);

		drawTrainingQueue(rect);
		
		if (selected) drawDevelopable(rect);
*/
		if (isBuilding)	{
			float fillAmout = buildingProgress * 100 / trainingTime;
			Utils.drawProgress(Utils.world2screen(transform.position), fillAmout);
			//canvasProgress.fillAmount = fillAmout;
		}
	}

	public void Update(){

		base.Update ();

		initDeploy(); //Restart deploy so it works on drag and drop from unity interface

		if (canvas != null) canvas.enabled = selected && !isBuilding;

		//Update training progress
		if (canvasProgress != null){
			if (trainingQueue.Count > 0 ){
				canvasProgress.enabled = true;
				StrategyObject u = trainingQueue[0];	
				float percent = ( Time.time - trainingStart) * 100 / u.trainingTime;
				canvasProgress.fillAmount = percent / 100;

				if (percent >= 100) finishTraining();
			} else canvasProgress.enabled = false;
		}

		if (moving) {
			Vector3 terrainBase = Utils.terrainHeight(transform.position);
			terrainBase.y += 0.5f;
			terrainBase.x = Mathf.Round(transform.position.x);
			terrainBase.z = Mathf.Round(transform.position.z);
			
			transform.position = terrainBase;
			
			if(canBuild) colorModel( Color.green);	
			else colorModel(Color.red);
		}
		
	}
	
	public void CancelTraining(int i) {

		StrategyObject u = trainingQueue[i];
		trainingQueue.Remove(u);
		if (i==0) {
			Gameplay.getPlayer(tag).addSupply(u.cost);
			trainingStart = Time.time;
			if (trainingQueue.Count > 0) startDevelopment(trainingQueue[0]);
		}
		u.CancelDevelopment();
		Gameplay.getPlayer(tag).addResources(u.cost);

		RemoveCancelButton(i);
		
	}

	// ------------------------------------
	// TRAINING QUEUE AND ACTIONS
	// ------------------------------------
	
	public void train(StrategyObject t){
		
		Debug.Log ("Adding " + t +" to " + trainingQueue);
		if (trainingQueue.Count == 0) startDevelopment( t);
		trainingQueue.Add (t);
		t.StartDevelopment();
		Gameplay.getPlayer(tag).consumeResources(t.cost);
	}
	public void startDevelopment(StrategyObject u){

		//TODO check if has enough supplies
		Gameplay.getPlayer(tag).consumeSupply(u.cost);

		if (u is Research) player ().research.StartDevelopment((Research)u);
		
		trainingStart = Time.time;
	}
	public void finishTraining(){
		StrategyObject u = trainingQueue[0];
		//Cannot put this inside finishdevelopment because it requires a object, tag and position
		if (u is Playable) Playable.create((Playable)u, deploy, tag);
		if (u is Research) player ().research.FinishDevelopment((Research)u);

		trainingQueue.RemoveAt(0);

		RemoveCancelButton(0);
		
		u.FinishDevelopment();
		if (trainingQueue.Count > 0) startDevelopment(trainingQueue[0]);
	}

	private void RemoveCancelButton(int p){
		Debug.Log("REmoving cancel button " +p + " of " +cancelButtons.Count );
		
		Destroy(cancelButtons[p].gameObject);

		cancelButtons.RemoveAt(p);
		
		for(int i=0; i < cancelButtons.Count; i++) {
			CancelTrainUnit cancel = cancelButtons[i];
			cancel.UpdatePosition(i);
		}
	}

}
