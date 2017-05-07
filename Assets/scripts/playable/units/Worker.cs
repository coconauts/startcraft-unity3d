using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;

public class Worker : Unit {
	
	private int MAX_RESOURCE = 8;
	private int collectResourceAmount = 0;
	private int DROP_TIME = 1;
	public Resource resource;

	private Building build;

	private Canvas buildCanvas; 
	private bool droping = false;

	public void Start(){
		base.Start();

		buildCanvas = transform.Find("Canvas").GetComponent<Canvas>();			
	}
	// ------------------------------------
	// UPDATE
	// ------------------------------------
	
	protected override void Update(){
		base.Update ();

		if(selected) buildCanvas.enabled = true;
		else buildCanvas.enabled = false;

		collect();
		
		dropResource ();

		building();

	}

	public void Collect(Resource r){
		CancelActions();
		goTo (r);
		resource = r;
	}

	private void collect(){
		//TODO Return to base if resource has been consumed completely
		
		if (resource != null) {
			if (collectResourceAmount < MAX_RESOURCE && 
			    GoCloser(resource) &&
			    doFreq (Action.Collect, resource.SPEED_COLLECTION)) {
				//Debug.Log("Collecting resource " + resource.name);
				if (resource.consume (1))resource=null;
				collectResourceAmount++;
				anim.SetBool("Working", true);
				Effects.Bluesparks(resource.transform.position);
				
			} else if(collectResourceAmount >= MAX_RESOURCE){ //GO back to base to drop resources
				Building building = Utils.closest<Building>("Base", tag, transform.position);
				if (building != null && GoCloser(building) && !droping) AddResources (); 
			}
		} else anim.SetBool("Working", false);
	}
	private void dropResource(){
		//Manually drop resource if you move the unit close to a building
		if (collectResourceAmount > 0){
			Building building = Utils.closest<Building>("Base", tag, transform.position);
			if 	(building != null && 
			     CloseEnough(building)) StartCoroutine (AddResources ());

		}
	}
	private void building(){
		if (build != null && GoCloser(build)){
			anim.SetBool("Working", true);
			build = build.Construct(); //Return null when building finish
		} else anim.SetBool("Working", false);
	}

	public void Build(Building b){

		CancelActions();
		doAction();
		build = b;
	}

	public override void CancelActions(){
		base.CancelActions();
		resource = null;
		build = null;
	}

	IEnumerator AddResources(){
		droping = true;
		yield return new WaitForSeconds(DROP_TIME);
		try {

			Gameplay.getPlayer(tag).resources[resource.type]+= collectResourceAmount;
			collectResourceAmount=0;
			droping = false;
		} catch (Exception e){
			Debug.LogWarning("Got exception "+ e);
		}


	}

	override protected void Ai(){
		//Do nothing
	}
}
