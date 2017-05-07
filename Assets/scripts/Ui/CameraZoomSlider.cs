using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class CameraZoomSlider : MonoBehaviour {
		
	private Slider slider; 

	private GameObject camera;

	void Start(){
		slider = GetComponent<Slider> ();
		slider.onValueChanged.AddListener(ValueChanged);
		camera = GameObject.Find ("Main Camera");
	}

	void ValueChanged(float value){
		Debug.Log ("Value " + value);
		//Camera.main.fieldOfView = value;
		Vector3 position = camera.transform.position;
		position.y = value;
		camera.transform.position = position;
	}
	
}