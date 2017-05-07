using UnityEngine;
using System.Collections.Generic;

public abstract class Player: MonoBehaviour  {

	public string PLAYER_TAG ;
	
	private int MAX_SUPPLY = 250;
	
	public int[] resources = {200,200};

	public int maxSupply = 10;
	public int totalSupply = 0;

	public Building mainBase;

	public ResearchManager research;

	public void Start(){
		research = new ResearchManager();
	}

	//cost: {mineral, gas, supplies}
	public bool hasResources(int[] cost){
		for(int i=0;i<resources.Length;i++) if(resources[i] < cost[i]) return false;
		return true;
	}
	public bool hasSupply(int[] cost){
		if (totalSupply + cost[cost.Length-1] > maxSupply) return false;
		return true;
	}

	/** 
	* Return true if has enough resources
	* cost: {mineral, gas, supplies}
	*/
	public bool consumeResources(int[] cost){
		if (hasResources (cost) ) {
			for(int i=0;i<resources.Length;i++) resources[i] -= cost[i];
			return true;
		} 
		return false;
	}
	public bool consumeSupply(int[] cost){
		if (hasSupply (cost) ) {
			totalSupply += cost[cost.Length-1];
			return true;
		}
		return false;
	}
	public void addResources(int[] cost){
		for(int i=0;i<resources.Length;i++) resources[i] += cost[i];
	}
	public void addSupply(int[] cost){
		totalSupply -= cost[cost.Length-1];
	}
	public void incrSupply(int[] cost){
		maxSupply = Mathf.Min(MAX_SUPPLY, maxSupply+cost[cost.Length-1]);
	}
	

	public static Color getColor(string tag){
		/*switch (tag) { 
			case "player1": return Color.yellow;  
			case "player2": return Color.blue; 
			case "player3": return Color.red; 
			default: return Color.green; 
		}*/
		return Color.white;
	}
}
