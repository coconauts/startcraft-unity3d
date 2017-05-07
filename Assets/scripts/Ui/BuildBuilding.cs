using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class BuildBuilding : MonoBehaviour {

	private Worker worker;
	public Building building;
	
	private Button button;
	// Use this for initialization
	void Start () {
	    button = gameObject.GetComponent<Button>();
		button.onClick.AddListener(delegate(){OnClick();});
		
		//TODO Make getParents util function (by name)
		worker = transform.parent.transform.parent.GetComponent<Worker>();

		AddCostText();
	}

	void AddCostText(){
		GameObject child = (GameObject)Instantiate(Resources.Load("CostText")); ;
		child.transform.parent = this.transform;
		child.transform.localScale = new Vector3(1,1,1);
		child.transform.localPosition = Vector3.zero;
		child.transform.localRotation = Quaternion.identity;
		
		Text text = child.GetComponent<Text>();
		text.text = building.cost[0] + "\n" +building.cost[1];
	}
	
	void OnClick(){
		Debug.Log("Button click");
		
		if(Gameplay.player1.consumeResources(building.cost)) {
			Gui.buildingSelected =  Building.BuildPlace(building);
		} else {
			Utils.PlayAudioOnCamera(Gameplay.st.notEnoughResource1);
			Debug.Log("Not enough minerals to build " + building.name);
		}
			//else audioNotMinerals.Play();
	}
	void Update(){
		//Debug.Log ("Update ");
		button.interactable = building.CanBeDeveloped();
	}
}
