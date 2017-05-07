using UnityEngine;
using System.Collections.Generic;
using System;
using Pathfinding;

public enum UnitType{
	Ground,
	Air
}
public enum Action{
	Attack,
	Collect,
	Drop
}

public abstract class Playable : StrategyObject {
	
	// ------------------------------------
	// Defined in scene
	// ------------------------------------
	public int life = 5 ;
	public int armor = 0;
	public float speed = 5;
	public int sight = 5;

	public Attack ground ; //Default values in Attack class
	public Attack air;
	
	public UnitType type = UnitType.Ground;
	public bool immobile = true; //Because static and fixed are reserved words

	public AudioClip audioDie;
	public AudioClip[] audioSelects;
	public AudioClip audioTrained;
	public AudioClip[] audioActions;
	// ------------------------------------
	// Class variables
	// ------------------------------------
	
	//Children
	private GameObject selection; 

	protected bool selected = false;
	
	private int maxLife ;

	public Playable target;

	private Path path;
	private int currentWaypoint;
	protected Seeker seeker;

	protected Animator anim;
	
	Dictionary<Action,float> lastAction = new Dictionary<Action,float>(); 
	
	// ------------------------------------
	// CREATION AND MODEL MODIFICATIONS
	// ------------------------------------
	public void Start () {
		
		Debug.Log ("Start " + name);
		
		Color c = Player.getColor(tag);
		colorModel (c);

		maxLife = life;

		addSelection();

		seeker = (Seeker) GetComponent<Seeker>();

		//Get animator from model
		Transform model = transform.Find ("model");
		anim = model.GetComponent<Animator>();		

		//Init action map
		foreach(Action a in (Action[])Enum.GetValues(typeof(Action))){
			lastAction.Add(a,0);		
		}

		GetComponent<AudioSource>().PlayOneShot(audioTrained);
		
		transform.position = Utils.terrainHeight (transform.position);		
	}
	
	/*
	* Buildings and units start at the beginning, and then we instantiate new buildings, 
	* This method prevents to childrend duplications (like selection)
	*/
	private void addSelection(){
		Transform s = transform.Find ("_Selection");
		if (s == null){
			GameObject selectionTemplate = GameObject.Find("Selection");
			
			selection = (GameObject) GameObject.Instantiate(selectionTemplate);
			selection.transform.parent = this.transform;
			selection.name = "_Selection";
			selection.transform.localPosition = new Vector3(0,0.1f,0);

			//Scale selection size
			Vector3 size = this.GetComponent<BoxCollider>().bounds.size;

			size.x = Math.Max (size.x, size.z);
			size.z = Math.Max(size.x, size.z);
			size *= 1.5f;
			size.y = 0;
			selection.transform.localScale = size;

			//Debug.Log ("object " + name + " size " + size);
		} else selection = s.gameObject;
	}

	public static Playable create(Playable u, Vector3 deploy,string tag){
		//Vector3 inTerrain = Utils.terrainRaycast (deploy);
		Playable go = (Playable)Instantiate (u, deploy, Quaternion.identity);
		go.gameObject.tag = tag;
		go.gameObject.transform.parent = GameObject.Find(tag).transform;
		return go;
	}

	public void colorModel(Color c){
		Transform model =  transform.Find ("model");
		if (model.GetComponent<Renderer>() != null)  model.GetComponent<Renderer>().material.color = c;
		foreach (Transform t in model) if ( t.GetComponent<Renderer>() != null) t.GetComponent<Renderer>().material.color = c;
	}	

	public void alphaModel(float a) {
		Transform model =  transform.Find ("model");
		if (model.GetComponent<Renderer> () != null) {
			Color color = model.GetComponent<Renderer> ().material.color ;
			color.a = a;
			model.GetComponent<Renderer> ().material.color  = color;
		}
		foreach (Transform t in model) {
				if (t.GetComponent<Renderer> () != null) {
					Color color = t.GetComponent<Renderer> ().material.color;
					color.a = a;
					t.GetComponent<Renderer> ().material.color = color;
				}
		}
	}

	/**
	* Used for building, move object to different layer (wireframe)
	*/ 
	public void LayerModel(int layer){
		
		Transform model =  transform.Find ("model");
		model.gameObject.layer = layer;
		foreach (Transform t in model) t.gameObject.layer = layer;
	}	
	// ------------------------------------
	// CONTROLS
	// ------------------------------------
	
	//http://answers.unity3d.com/questions/18875/right-mouse-detection.html
	protected virtual void OnMouseOver () {

		if (Input.GetMouseButtonDown(0) &&
		    tag == Gameplay.player1.PLAYER_TAG) {
			unselectAll();
			select ();
		}

		if ( Controller.RightClickOrTouch()  &&
		    tag != Gameplay.player1.PLAYER_TAG) {
			foreach (Playable u in Selected<Playable>()) u.Attack (this);
		}
	}
	private void randomAudio(AudioClip[] audios){
		if (audios.Length > 0 ) GetComponent<AudioSource>().PlayOneShot(Utils.random (audios));
		else Debug.LogWarning("Missing audios " + name);		
	}
	// ------------------------------------
	// UPDATE METHODS
	// ------------------------------------
	
	protected virtual void Update () {
		if (life <= 0) Die ();
		
		attacking();
		if (!immobile) moving();

		if (selected) UpdateSelectLife ();

		Ai();
	}

	private void UpdateSelectLife(){
		//selection.GetComponent<Renderer>().Color = true;

		float percent = ((float) life) / maxLife;

		Color c = new Color(1 -percent,  percent, 0);
		selection.GetComponent<Renderer> ().material.color = c;
	}
	protected void Die() {
		Gameplay.getPlayer(tag).addSupply(this.cost);
		
		if (audioDie != null) AudioSource.PlayClipAtPoint(audioDie, transform.position);
		Destroy (this.gameObject);
	}
	private void attacking(){
		if (target != null){
			if (target.type == UnitType.Air) attacking (air);
			else attacking (ground); //GROUND
		} else if (anim != null) anim.SetBool ("Attack", false);
	}
	private void attacking(Attack attack){
		if (goTo(target, attack.range) &&
		    attack.damage != 0 && 
			doFreq (Action.Attack, attack.speed) ) {
				performAttack(target, attack);
		} else anim.SetBool ("Attack", false);
	}
	private void moving(){
		if (path != null) {
			
			anim.SetBool("Moving", true);
			
			Vector3 dir = (path.vectorPath[currentWaypoint]-transform.position).normalized;
			dir *= speed* Time.deltaTime;
			
			if (currentWaypoint!= 0) faceDirection(path.vectorPath[currentWaypoint]);		
			
			Vector3 move = transform.position + dir;
			transform.position = move;
			
			if ( (transform.position-path.vectorPath[currentWaypoint]).sqrMagnitude < 1) {
				currentWaypoint++;
			}
			if (currentWaypoint >= path.vectorPath.Count) path = null; //end of path reached
		} else anim.SetBool("Moving", false);	
	}
	/**
	* Return true every freq seconds 
	*/
	public bool doFreq(Action action, float freq ){
		float lastFreq = lastAction[action];
		if ( lastFreq + freq < Time.time){
			lastAction[action] = Time.time;
			return true;
		} else return false;
	}

	// ------------------------------------
	// DRAW METHODS
	// ------------------------------------

	public virtual void OnGUI() {
		/*if (selected){
			Vector3 pos = Utils.world2screen(transform.position);
			int percent = life *100 / maxLife;
			Utils.drawProgress(pos, percent);
		}*/
	}
	
	// ------------------------------------
	// STATIC METHODS
	// ------------------------------------
	
	public static T[] allUnits<T>(string tag) where T: Playable {
		if (tag == null)return new T[0];
		//TODO add selected to list of elements (to avoid using Find)
		GameObject[] gos = GameObject.FindGameObjectsWithTag(tag);
		List<T> units = new List<T>();
		foreach (GameObject go in gos){
			T comp = go.GetComponent<T>(); 
			if (comp != null ) units.Add(comp); 
		}
		return units.ToArray();
	}

	public static T[] Selected<T>() where T: Playable {
		T[] all = allUnits<T>(Gameplay.player1.PLAYER_TAG);
		List<T> units = new List<T>();
		foreach (T t in all) if (t.selected) units.Add(t); 
		return units.ToArray();
	}
	public static bool anyIsSelected<T>() where T: Playable{
		return Selected<T>().Length > 0;
	}

	public static void moveSelected(Vector3 position){
		
		foreach (Playable comp in Playable.Selected<Playable>()){
			comp.CancelActions();
			comp.goTo(position);
			comp.doAction();		
		}
	}
	//Non static
	public T Closest<T>(string player) where T: Playable{
		GameObject[] gos = GameObject.FindGameObjectsWithTag(player);
		T closest = null;
		foreach (GameObject go in gos){
			T t = go.GetComponent<T>();
			if (t != null){
				float distance = (go.transform.position - transform.position).magnitude;
				if (closest == null || distance < (t.transform.position - transform.position).magnitude){
					closest = t;
				}
			}
		}
		return closest;
	}
	// ------------------------------------
	// PLAYABLE ACTIONS
	// ------------------------------------

	public void select(){
		
		Debug.Log ("Selecting " + name);
		randomAudio(audioSelects);
		selected = true;
		selection.GetComponent<Renderer>().enabled = true;
	}
	public static void unselectAll(){
		foreach(Playable u in Selected<Playable>()) u.unselect();
	}

	public void unselect(){
		selected = false;
		selection.GetComponent<Renderer>().enabled = false;
	}
	public void doAction(){
		randomAudio(audioActions);
	}
	public virtual void CancelActions(){
		target = null;
		path = null;
	}
	
	private void performAttack(Playable target, Attack attack){
		anim.SetBool("Attack", true);
		GetComponent<AudioSource>().PlayOneShot(attack.audio);
		//TODO create attack effect: hit, slash or projectile
		//audioHit.Play ();
		int damage = attack.damage;
		if (player().research.Researched(Research.Available.weapon_level1)) damage += 1;

		target.damage(damage);

		Effects.Sparks(target.transform.position);
	}
	/**
	* Select object as target
	*/
	public void Attack(Playable target){
		CancelActions();
		goTo (target);
		this.target = target;
	}

	public float distance(MonoBehaviour p){
		Vector3 pos = this.transform.position;
		Vector3 closestPoint = p.GetComponent<Collider>().ClosestPointOnBounds(pos);
		float distance =  Vector3.Distance(closestPoint,  pos);
		return distance;
	}
	public bool CloseEnough(MonoBehaviour m){
		return distance(m) < 1f;
	}
	/*
	* Receives damage
	*/
	protected void damage(int dmg){
		
		if (player().research.Researched(Research.Available.armor_level1)) dmg -= 1;
		
		life -= dmg;
		showDmg(dmg);
	}
	private void showDmg(int dmg){
		if (Prefs.showDamage ){
			GameObject health_dmg = GameObject.Find("Health_dmg");
			
			GameObject dmgObject = (GameObject)Instantiate(health_dmg);
			dmgObject.transform.parent = this.transform;
			dmgObject.transform.localPosition = new Vector3(0,2,0);
			
			TextMesh dmgText = dmgObject.GetComponent<TextMesh> ();
			dmgText.text = "-"+dmg;
			Destroy(dmgObject,2);
		}
	}	

	// ------------------------------------
	// MOVEMENT AND STARPATH
	// ------------------------------------
	
	public void goTo(MonoBehaviour m){
		Vector3 pos = this.transform.position;
		Vector3 closestPoint = m.GetComponent<Collider>().ClosestPointOnBounds(pos);
		goTo (closestPoint);
	}
	public void goTo(Vector3 destination){
		//Debug.Log ("Go from " + transform.position + " to "  +destination);
		if (!immobile && seeker != null && seeker.IsDone()) {
			seeker.StartPath (transform.position, destination, OnPathComplete);
		}
	}
	public bool GoCloser(MonoBehaviour m){
		return goTo (m, 1f);
	}

	/**
	* GO to destination, return true when arrive
	*/
	public bool goTo(MonoBehaviour mono, float range){
		//faceDirection (mono.transform.position);
		//if ((transform.position - mono.transform.position).magnitude > range) { // resource is too far
		if (distance (mono) > range){
			if (path == null) goTo(mono); 
			return false;
		}  else  {
			path = null; //close enough
			return true;
		}
	}
	public void OnPathComplete (Path p) {
				p.Claim (this);
		if (!p.error) {
			if (path != null) path.Release (this);
			path = p;
			//Debug.Log ("path complete " + p);
			//Reset the waypoint counter
			currentWaypoint = 0;
		} else {
			p.Release (this);
			Debug.Log ("Oh noes, the target was not reachable: "+p.errorLog);
		}
		//seeker.StartPath (transform.position,targetPosition, OnPathComplete);
	}
	public virtual void faceDirection(Vector3 destiny){}
	// ------------------------------------
	// ARTIFICIAL INTELIGENCE
	// ------------------------------------
	
	//Ai behaviour for all the units (not computer Ai)
	//Heavy method
	virtual protected void Ai(){
		string enemyT = Gameplay.enemies(tag);
		Unit[] enemyUnits = allUnits<Unit>(enemyT);
	    foreach (Unit enemy in enemyUnits){
	      float distance = (enemy.transform.position - transform.position).magnitude;
	      if (distance < sight) target = enemy;
	      //else path = null; //if we went too far, we need to go back to the original position
	    }
	}
}
