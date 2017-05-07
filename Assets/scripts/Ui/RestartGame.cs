using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;

public class RestartGame : MonoBehaviour {
	
	// Use this for initialization
	void Start () {
		Button button = gameObject.GetComponent<Button>();
		button.onClick.AddListener(delegate(){OnClick();});
		
	}
		
	void OnClick(){
		Application.LoadLevel(Application.loadedLevel);
		
	}
}
