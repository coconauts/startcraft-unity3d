using UnityEngine;
using System.Collections.Generic;
using System;

public abstract class StrategyObject: MonoBehaviour {

	// ------------------------------------
	// Defined in scene
	// ------------------------------------
	public Sprite sprite;
	public int[] cost = {1,0,0};
	public float trainingTime = 5;
	public Playable[] dependencies;
	
	public virtual void StartDevelopment(){}
	public virtual void FinishDevelopment(){}
	public virtual void CancelDevelopment(){}
	
	public virtual bool CanBeDeveloped(){
		return dependenciesSatisfied(Gameplay.player1.PLAYER_TAG);
	}

	private bool dependenciesSatisfied(string tag){
		GameObject[] gos = GameObject.FindGameObjectsWithTag(tag);
		List<Playable> satisfied = new List<Playable>();
		
		foreach (Playable dep in dependencies) {
			foreach (GameObject go in gos){
				if (go.name == dep.gameObject.name+"(Clone)") {
					Building b = go.GetComponent<Building>();
					if (b!= null) {
						Debug.Log ("is moving " + b.isBuilding + " " + b.moving);
						if (!b.isBuilding && !b.moving) satisfied.Add(dep);
						else Debug.Log ("Dependency building go.name satisfied "+go.name);
					} else {
						satisfied.Add(dep);
					}

					break;
				} 
			}
		}
		return satisfied.Count == dependencies.Length;
	}

	protected Player player(){
		return Gameplay.getPlayer(this.tag);
	}
	
}
