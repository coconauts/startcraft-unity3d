using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class Utils {

	public static Vector3 screen2world(Vector3 position){

		Vector3 screen = new Vector3(Screen.width - (Screen.width - position.x), Screen.height - (Screen.height - position.y), Camera.main.transform.position.y );
		Vector3 world =  Camera.main.ScreenToWorldPoint(screen);
		//Debug.Log ("Screen " + position + " to world "+ world);
		return world;
	}

	public static Vector3 world2screen(Vector3 position){
		
		Vector3 screen = Camera.main.WorldToScreenPoint(position);
		screen.y = Screen.height - screen.y;
		return screen;
	}

	//TODO Use Vector3 for position
	public static void drawCircle(LineRenderer lineRenderer, Vector3 p, int radius){
		
		int vertex = 10;
		lineRenderer.material = new Material (Shader.Find ("Particles/Additive"));
		lineRenderer.SetVertexCount (vertex+1); 
		
		for (int i = 0 ; i < vertex +1 ; i ++){
			float difX = Mathf.Cos(i * Mathf.PI / (2 *vertex / 4 ));
			float difZ = Mathf.Sin (i * Mathf.PI / (2 *vertex / 4 ));
			Vector3 pos = new Vector3(p.x + radius * difX,
			                          p.y, 
			                          p.z + radius * difZ);
			lineRenderer.SetPosition(i, pos);
		}
	}

	public static Vector3 cameraRaycast(Vector3 pos) {
		//Terrain terrain = GameObject.Find("Terrain").GetComponent<Terrain>();
		Camera camera = Camera.main;
		Ray ray = camera.ScreenPointToRay (pos);
		RaycastHit hit;
		Physics.Raycast (ray, out hit);
		pos.y = hit.point.y;
		return pos;
	}

	public static Vector3 terrainHeight(Vector3 pos) {
		Terrain terrain = GameObject.Find("Terrain").GetComponent<Terrain>();
		//float h = terrain.terrainData.GetHeight ((int)pos.x, (int)pos.z);
		float h = terrain.SampleHeight(pos);
		//Debug.Log ("Terrain h in pos " + pos + ": " + h);
		return new Vector3 (pos.x, h, pos.z);
	}

	
	public static void drawDebugRectangle(Vector3 leftUp, Vector3 rightDown){
		
		Vector3 rightUp = new Vector3(leftUp.x, leftUp.y, rightDown.z);
		Vector3 leftDown = new Vector3(rightDown.x, rightDown.y, leftUp.z);

		Debug.DrawLine(leftUp,  rightUp);
		Debug.DrawLine(rightUp, rightDown);
		Debug.DrawLine(rightDown, leftDown);
		Debug.DrawLine(leftDown, leftUp);
	}

	public static void debugText(string text){
		Text t = GameObject.Find ("DebugText").GetComponent<Text>();
		t.text = text;
	}
	public static void drawSquare(LineRenderer lineRenderer, Vector3 leftUp, Vector3 rightDown){
		
		//lineRenderer.material = new Material (Shader.Find ("Particles/Additive"));
		//lineRenderer.material.color = Color.black;
		lineRenderer.SetVertexCount (5);
		lineRenderer.SetPosition(0, leftUp);
		lineRenderer.SetPosition(1, new Vector3(leftUp.x, leftUp.y, rightDown.z));
		lineRenderer.SetPosition(2, rightDown);
		lineRenderer.SetPosition(3, new Vector3(rightDown.x, rightDown.y, leftUp.z));
		lineRenderer.SetPosition(4, leftUp); //close square
	}

	public static void drawLine(LineRenderer lineRenderer, Vector3 from, Vector3 to){
		
		lineRenderer.material = new Material (Shader.Find ("Particles/Additive"));
		lineRenderer.SetVertexCount (2);
		lineRenderer.SetPosition(0, from);
		lineRenderer.SetPosition(1, to);	
	}
	public static Texture2D MakeTex( int width, int height, Color col )
	{
		Color[] pix = new Color[width * height];
		for( int i = 0; i < pix.Length; ++i )
		{
			pix[ i ] = col;
		}
		Texture2D result = new Texture2D( width, height );
		result.SetPixels( pix );
		result.Apply();
		return result;
	}
	public static void clearLineRenderer(LineRenderer lineRenderer){
		lineRenderer.SetVertexCount (0);
	}

	/**
	 * This method doesn't work as expected
	 */
	public static T CopyComponent<T>(T original, GameObject destination) where T : Component
	{
		System.Type type = original.GetType();
		Component copy = destination.AddComponent(type);
		System.Reflection.FieldInfo[] fields = type.GetFields();
		foreach (System.Reflection.FieldInfo field in fields)
		{
			field.SetValue(copy, field.GetValue(original));
		}
		return copy as T;
	}
	
	public static Rect terrain(){
    	//TODO Get terrain dimmensions (as a Rect)
		Terrain terrain = GameObject.Find ("Terrain").GetComponent<Terrain> ();
		Vector3 size = terrain.terrainData.size;
		return new Rect (0, 0, size.x, size.z);
	}
		
	public static T closest<T>(string name, string tag, Vector3 pos) where T : UnityEngine.Component{
		GameObject[] gos = GameObject.FindGameObjectsWithTag(tag);
		GameObject closest = null;
		foreach (GameObject go in gos){
			if (go.name.Contains(name)){ //a clone object will contain (Clone) in the name
				float distance = (go.transform.position - pos).magnitude;
				if (closest == null || distance < (closest.transform.position - pos).magnitude){
					closest = go;
				}
			}
		}
		if (closest != null) return (T) closest.GetComponent<T>();
		else return null;
	}

	public static T random<T>(T[] list) {
		int r = Random.Range(0,list.Length);
		return list[r];
	}

	private static Texture2D progressTexture;
	private static Texture2D hudBox;

	public static void drawProgress(Vector3 pos, float percent){
		//Load once
		if (progressTexture == null) {
			progressTexture = new Texture2D(1,1);
			progressTexture.SetPixel(0,0,Color.green);
			progressTexture.Apply();

			hudBox = Resources.Load<Texture2D>("hudbox");
		}

		GUI.DrawTexture (new Rect (pos.x,pos.y, 50*(percent/100),15), progressTexture);
		//Draw hudbox after texture
		GUI.DrawTexture (new Rect (pos.x,pos.y, 50,15), hudBox);
		
		//int p = (int) percent;
		//GUI.Box (new Rect(pos.x,pos.y,50,15), p+"%");	
	}
	
	public static void PlayAudioOnCamera(AudioClip clip){
		AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position );
	}

	public static void SetGarbageCollector(GameObject go) {
		go.transform.parent = Gameplay.garbageCollector;
	}
	
	Component CopyComponent(Component original, GameObject destination) {
		System.Type type = original.GetType();
		Component copy = destination.AddComponent(type);
		// Copied fields can be restricted with BindingFlags
		System.Reflection.FieldInfo[] fields = type.GetFields(); 
		foreach (System.Reflection.FieldInfo field in fields)
		{
			field.SetValue(copy, field.GetValue(original));
		}
		return copy;
	}
}
