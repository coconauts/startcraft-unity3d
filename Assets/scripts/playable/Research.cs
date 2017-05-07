using UnityEngine;
using System.Collections.Generic;


public class Research : StrategyObject {

	public Available type;

	public enum Available {
		armor_level1,
		weapon_level1
	}

	public override bool CanBeDeveloped(){
		ResearchManager manager = player ().research;
		return base.CanBeDeveloped() && 
			!manager.inProgress.Contains(this) && 
			!manager.finished.Contains(this);
	}
	
}
