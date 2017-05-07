using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;

public class AiRush : Player{
	private GameObject[] deploys;
	
	private const int TIME_RUSH = 60*5;
	private const int AMOUNT_RUSH = 5;
	
	private int timeToRush = TIME_RUSH;
	private int enemiesInRush = AMOUNT_RUSH;
	
	private Text rushText ; 
	private string separator = ":";

	private bool rushInProgress = false;
	private int level = 0;

	private List<Unit> enemies;
	// Use this for initialization
	public void Start(){
		enemies = new List<Unit>();
		deploys = GameObject.FindGameObjectsWithTag("RushDeploy");
		
		rushText  = GameObject.Find("RushText").GetComponent<Text>();
		printTime();

		InvokeRepeating("PrintRushStatus", 0, 1);
		InvokeRepeating("RefreshAttack", 0, 5);
	}

	private void RefreshAttack(){
		foreach(Unit u in enemies)
			u.Attack(Gameplay.getPlayer("player1").mainBase);	
	}	
	public void Update(){
		if (timeToRush <= 0) {
			if( !rushInProgress) {
				StartCoroutine(CreateEnemies(enemiesInRush , 0.1f));
				rushInProgress = true;
			} else {
				for(int i = 0; i < enemies.Count; i++) {
					Unit enemy = enemies[i];
					//Debug.Log ("Enemy "+enemy);
					if (enemy == null) enemies.RemoveAt(i);
				}
				//Restart rush
				if (enemies.Count == 0){
					enemiesInRush += AMOUNT_RUSH;
					timeToRush = TIME_RUSH;
					rushInProgress = false;
					level++;
				}
			}
		}
	}
	private void PrintRushStatus (){
		if (rushInProgress) rushText.text = "Defeat "+enemies.Count;
		else {
			if (separator == ":") separator = " ";
			else separator = ":";

			printTime();
			timeToRush--;
		}
	}

	private void printTime(){
		string minutes = "0";
		if (timeToRush >= 60) minutes = (timeToRush / 60).ToString();
		string seconds = (timeToRush % 60).ToString();
		
		rushText.text =  fillWithZeros(minutes) + separator  + fillWithZeros(seconds);
	}

	private string fillWithZeros(string str){
		if (str.Length==1) return "0" + str;
		else return str;
	}

	//Coruitine
	private IEnumerator CreateEnemies(int amount, float delay){
		string[] enemies = enemiesOnLevel(level);
		
		float created = 0;
		while (created < amount) {
			string enemy = Utils.random(enemies);
			switch(enemy){
				case "Zergling": created += 0.5f; break;
				default: created += 1f; break;
			}
			createEnemy(enemy);
			yield return new WaitForSeconds(delay);
		}
	}

	private string[] enemiesOnLevel(int level){
		if (level < 2) return new string[]{"Zergling"}; 
		else if (level < 5) return new string[]{"Zergling", "Hydralisk"}; 
		else return new string[]{"Zergling", "Mutalisk", "Hydralisk"}; 
	}

	private void createEnemy(string enemyName){
		Debug.Log ("Creating zerg in rush: "+ enemyName);
		GameObject deploy = Utils.random(deploys);
		
		GameObject enemy = GameObject.Find(enemyName);

		//GameObject e = (GameObject) GameObject.Instantiate(enemy, deploy.transform.position, Quaternion.identity);
		Unit unit = enemy.GetComponent<Unit>();
		Unit newUnit = (Unit) Unit.create(unit, deploy.transform.position, PLAYER_TAG);

		enemies.Add(newUnit);
		newUnit.Attack(Gameplay.getPlayer("player1").mainBase);
	}
}
