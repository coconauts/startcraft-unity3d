using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class ResearchManager {
	public List<Research> inProgress = new List<Research>();
	public List<Research> finished = new List<Research>();
	
	public void StartDevelopment (Research r) {
		Debug.Log ("Adding "+r.name+ " to inProgress " +inProgress);
		inProgress.Add(r);
	}
	public void FinishDevelopment(Research r) {
		Debug.Log ("Finished research "+r.name);
		
		inProgress.Remove(r);
		finished.Add(r);
	}
	public void CancelDevelopment (Research r) {
		inProgress.Remove(r);
	}
	
	public bool Researched(Research.Available research) {
		
		foreach(Research r in finished) {
			Debug.Log ("Researches finished " + r);
			if (r.type == research) return true;
		}
		return false;	
	}
}
