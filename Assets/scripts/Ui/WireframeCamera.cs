using UnityEngine;
using System.Collections;

public class WireframeCamera : MonoBehaviour {

	void OnPreRender(){
		GL.wireframe = true;
	}
	
	void OnPostRender(){
		GL.wireframe = false;
	}
}
