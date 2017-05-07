using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Gui : MonoBehaviour {

	public Building[] buildings;
	
	public static Building buildingSelected; 

	private Text gasTotal;
	private Text supplyTotal;
	private Text crystalTotal;

	public void Start(){
		
		GameObject[] bo = GameObject.FindGameObjectsWithTag("Building");
		gasTotal = GameObject.Find("GasTotal").GetComponent<Text>();
		crystalTotal = GameObject.Find("CrystalTotal").GetComponent<Text>();
		supplyTotal = GameObject.Find("SupplyTotal").GetComponent<Text>();
		
		buildings = new Building[bo.Length];
		
		for (int i = 0; i < bo.Length; i++){
			buildings[i] = bo[i].GetComponent<Building>();
		}

		InvokeRepeating("UpdateResources", 0, 1);
	}

	void UpdateResources()  {
	
		crystalTotal.text = Gameplay.player1.resources[0].ToString();
		gasTotal.text = Gameplay.player1.resources[1].ToString();
		supplyTotal.text = Gameplay.player1.totalSupply +"/"+ Gameplay.player1.maxSupply;
		
	}
	void Update(){
		if (buildingSelected != null) drawWireframeBuilding();
	}

	void drawWireframeBuilding(){
		Vector3 position = Utils.screen2world(Input.mousePosition);
		position.y = Terrain.activeTerrain.SampleHeight(position) + 0.1f;
		buildingSelected.transform.position = position ;
		if (Input.GetMouseButtonDown(0)){
			if (!buildingSelected.canBuild){
				Utils.PlayAudioOnCamera(Gameplay.st.cantBuildHere);
			} else {
				buildingSelected.BuildStart ();
				buildingSelected = null;
			}	
		}
		if (Input.GetMouseButtonDown(1)){
			Gameplay.player1.addResources(buildingSelected.cost);
			Destroy(buildingSelected.gameObject);
			buildingSelected = null;
		}
	}

}
