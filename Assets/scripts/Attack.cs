using UnityEngine;
using System.Collections;

//System.serializable will make it appear nice in the unity3d inspector
[System.Serializable]
public class Attack {
	
	public int damage = 0; //0 means no attack
	public float speed = 0; //in seconds
	public int range = 0;
	
	public AudioClip audio;
}
