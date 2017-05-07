using UnityEngine;
using System.Collections;

public class Resource : MonoBehaviour {

	public int type = 0;
	public string description = "Minerals";
	public int quantity = 1500;

	public float SPEED_COLLECTION = 0.25f; //in seconds

	void OnMouseOver () {

		if (Controller.RightClickOrTouch()) {
			foreach (Unit u in Unit.Selected<Unit>()) {
				Debug.Log ("Unit " + u.name+ " will collect "+this);
				if (u is Worker) ((Worker) u).Collect(this);
			}
		}
	}
	
	public bool consume(int amount ){
		quantity-= amount;
		if (quantity <= 0) {
			
			Destroy (this.gameObject);
			return true;
		}
		return false;
	}
}
