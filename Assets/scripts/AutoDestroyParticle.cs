using UnityEngine;
using System.Collections;

public class AutoDestroyParticle : MonoBehaviour {

	private ParticleSystem ps;
	// Use this for initialization
	void Start () {
		ps = GetComponent<ParticleSystem>();
	}
	
	// Update is called once per frame
	void Update () {
		if (ps && ps.time > 0  && !ps.IsAlive()) Destroy (this.gameObject);
	}
}
