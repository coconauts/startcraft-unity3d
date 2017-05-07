using UnityEngine;
using System.Collections;

public class AttachMainCamera : MonoBehaviour {

	// Use this for initialization
	void Start () {
		Canvas canvas = GetComponent<Canvas>();
		canvas.worldCamera = Camera.main;
	}
}
