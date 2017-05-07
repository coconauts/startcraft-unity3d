using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;

public class Train : MonoBehaviour {
	
	private Building building;
	public StrategyObject strategyObject;
	
	//private string unitName;
	private int counter = 0;
	
	private int MAX_QUEUE = 5;
	
	// Use this for initialization
	void Start () {
		Button button = gameObject.GetComponent<Button>();
		button.onClick.AddListener(delegate(){OnClick();});
		
		//TODO Make getParents util function (by name)
		building = transform.parent.transform.parent.transform.parent.GetComponent<Building>();
		
		//unitName = strategyObject.name;
		
		AddCostText();
	}
	
	void AddCostText(){
		GameObject child = (GameObject)Instantiate(Resources.Load("CostText")); ;
		child.transform.parent = this.transform;
		child.transform.localScale = new Vector3(1,1,1) * 0.04f;
		child.transform.localPosition = Vector3.zero;
		child.transform.localRotation = Quaternion.identity;
		
		Text text = child.GetComponent<Text>();
		text.text = building.cost[0] + "\n" +building.cost[1];
	}
	
	void OnClick(){
		
		if (!Gameplay.player1.hasSupply(strategyObject.cost)){
			Utils.PlayAudioOnCamera(Gameplay.st.notEnoughSupply);
		} else if(!Gameplay.player1.hasResources(strategyObject.cost)) {
			Utils.PlayAudioOnCamera(Gameplay.st.notEnoughResource1);
		} else {	
			int queueSize = building.trainingQueue.Count;
			
			if (queueSize < MAX_QUEUE){

				Debug.Log("Researching" + strategyObject.name);
				
				//strategyObject.name = unitName + counter++;
				building.train (strategyObject);
				building.cancelButtons.Add (CancelTrainUnit.AddCancelButton(strategyObject, building, queueSize));
			}
		}
		
	}
}
