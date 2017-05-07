using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;

public class Ai : Player {

	//TODO add dinamically everytime we create a new unit ???
  private Unit[] allAttackers;
  private Unit[] allWorkers;
  private Building[] allBuildings;

	private bool log = false;
  private List<Building> discoveredBuildings;
	
	public Ai(string tag){
		PLAYER_TAG = tag;
	}

	private const int AI_FREQ = 5;
	private float lastAi = 0;

 /* public override void update(){
		if ( lastAi + AI_FREQ< Time.time) doSomething();
  }*/

  public void doSomething(){
	try{
		allAttackers = Playable.allUnits<Attacker>(PLAYER_TAG);
		allWorkers = Playable.allUnits<Worker>(PLAYER_TAG);
		allBuildings = Playable.allUnits<Building>(PLAYER_TAG);
		if (allAttackers.Length <= 0 || allBuildings.Length <= 0) gameOver();

		if (resources[0] < 15) {
			collect ();
		}
		if (allAttackers.Length >= 5){
			attack ();
		}
		if (resources[0] >=15 && allWorkers.Length< 3){
			createWorker(allBuildings[0]);
		}
		if (resources[0] >=15 && allAttackers.Length< 5){
			createAttacker(allBuildings[0]);
		}
		
	} catch (Exception e) {
			if (log) Debug.LogWarning("Unable to run AI "+e);
	} 
	lastAi= Time.time;

  }

  private void gameOver(){
		if (log) Debug.Log ("Enemy doesn't have more units - GAME OVER");

		//Destroy (this);
	}
  
  void explore(){
  		
  }
  
  void attack(){
		if (log) Debug.Log ("AI - attack");
		string enemyT = Gameplay.enemies(PLAYER_TAG);
		Unit[] enemyUnits = Playable.allUnits<Unit>(enemyT);
		
		foreach(Unit u in allAttackers ) u.Attack (enemyUnits[0]);

  }
  
  void collect(){
		if (log) Debug.Log ("AI - collect");
		foreach (Worker u in  allWorkers) {
			u.resource = Utils.closest<Resource> ("Resource", "Resource", u.transform.position);
		}
  }
  void createWorker(Building b){
		if (log) Debug.Log ("AI - create worker");
		//TODO Get proper worker from race
		Worker u = GameObject.Find ("Worker").GetComponent<Worker>(); //First unit from base must be a worker
		//int random = UnityEngine.Random.Range(0,units.Length);
		//Unit u = units[random];
		if (consumeResources(u.cost)){
			Worker.create(u, b.deploy, PLAYER_TAG);
		}
	}
	void createAttacker(Building b){
		if (log) Debug.Log ("AI - create attacker");
		//TODO Get proper unit from race		
		Unit u = GameObject.Find ("Marine").GetComponent<Unit>();
		if (consumeResources(u.cost)){
			Unit.create(u, b.deploy, PLAYER_TAG);
		}
	}
}
