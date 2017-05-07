using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class CancelTrainUnit : MonoBehaviour {

	private Building building;
	public StrategyObject unit;
	private static float buttonWidth = 2.64f;	
	
	private int index; 

	public static CancelTrainUnit AddCancelButton(StrategyObject unit, Building building, int count){
		Debug.Log ("Adding unit " + unit.name + " on building " + building.name + ":" + count);
		GameObject cancelButton = (GameObject) Instantiate (Resources.Load ("Cancel"));

		Image image = cancelButton.GetComponent<Image>();
		image.sprite = unit.sprite;
		//if(count > 0) image.transform.localScale = Vector3.one * 0.5f;

		cancelButton.transform.parent = building.canvas.transform;
		RectTransform buttonRect = (RectTransform)  cancelButton.GetComponent<RectTransform>();
		 RectTransform canvasRect = (RectTransform) building.canvas.GetComponent<RectTransform>();

		cancelButton.transform.position = building.canvas.transform.position;
		cancelButton.transform.rotation = building.canvas.transform.rotation;		

		CancelTrainUnit script = cancelButton.GetComponent<CancelTrainUnit>();
		script.unit = unit;
		script.building = building;
		
		return (CancelTrainUnit) cancelButton.GetComponent<CancelTrainUnit>();
	}

	void Start () {
		Button button = gameObject.GetComponent<Button>();
		button.onClick.AddListener(delegate(){OnClick();});

		//TODO Make getParents util function (by name)
		building = transform.parent.transform.parent.GetComponent<Building>();

		index = building.trainingQueue.Count -1;
		name += index;

		UpdatePosition(index);
	}
	
	void OnClick(){
		Debug.Log("Button click "+index);

		building.CancelTraining(index);
		Destroy(this.gameObject);
	}

	public void UpdatePosition(int i){
		index = i;
		Debug.Log ("Update position " +index  +" to " +i);
		Vector3 position = transform.localPosition;

		//float scaleWidth = buttonWidth;
		//if (i > 0) scaleWidth = buttonWidth / 2;

		position.x = index * buttonWidth;
		Debug.Log ("Update position " +transform.localPosition  +" to " +position);
		transform.localPosition = position;
		
	}

}
