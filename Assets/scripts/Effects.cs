using UnityEngine;
using System.Collections;

public class Effects: MonoBehaviour {

	// Use this for initialization
	public static void Sparks (Vector3 position) {
		GameObject effect = (GameObject) Instantiate (Resources.Load ("Sparks"));
		effect.transform.position = position;
		Utils.SetGarbageCollector(effect);
	}

	public static void Bluesparks (Vector3 position) {
		GameObject effect = (GameObject) Instantiate (Resources.Load ("BlueSparks"));
		effect.transform.position = position;
		Utils.SetGarbageCollector(effect);
	}
	
}
