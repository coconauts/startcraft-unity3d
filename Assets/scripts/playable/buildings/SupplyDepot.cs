using UnityEngine;
using System.Collections.Generic;

public class SupplyDepot : Building {

	public int addSupply = 5;	

	public override void InitBuilding () {
		base.InitBuilding();
		Gameplay.getPlayer(tag).maxSupply+=addSupply;
		Debug.Log ("Incrementing " + tag +" supply " + Gameplay.getPlayer(tag).maxSupply);
	}

	private void Die() {
		Gameplay.getPlayer(tag).maxSupply-=addSupply;
		base.Die();
	}
	
}
