using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GasExtractor : Building {
	
	void OnTriggerEnter(Collider other){
		
		if (moving /*&& other.name != "Terrain"*/) {
			collissions.Add(other.gameObject);
			Debug.Log ("OnTriggerEnter with " +other.name+ ": "+collissions.Count);
			canBuild = other.name == "geyser";
		}
	}
	
	void OnTriggerExit(Collider other){
		
		if (moving /*&& other.name != "Terrain"*/) {
			
			collissions.Remove(other.gameObject);
			Debug.Log ("OnTriggerExit with " +other.name+ ": "+ collissions.Count);
			if (collissions.Count == 0) canBuild = false; 
		}
	}


	public override void InitBuilding(){
		foreach(GameObject o in collissions){
			if (o.name == "geyser") {
				Resource gas = o.GetComponent<Resource>();

				Utils.CopyComponent( o.GetComponent<Resource>(), this.gameObject);
				/*Resource r = gameObject.AddComponent<Resource>();
				Debug.Log("New gas extractor " + gas.quantity);

				r.quantity = gas.quantity;
				r.type = gas.type;
				r.description = gas.description;
				r.SPEED_COLLECTION = gas.SPEED_COLLECTION;
*/
				Destroy (o);
			}
		}	
	}
}
