using UnityEngine;
using System.Collections.Generic;
using System;


public abstract class Unit : Playable {
	 
	public override void faceDirection(Vector3 destiny){
		//Instant rotation
		//transform.LookAt(destiny);
		
		//Smooth rotation
		Quaternion rotation = Quaternion.LookRotation(destiny - transform.position);
		transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * speed);
	}
}
