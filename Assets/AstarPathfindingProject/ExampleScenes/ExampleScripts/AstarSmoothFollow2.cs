using UnityEngine;
using System.Collections;

/** Smooth Camera Following.
 * \author http://wiki.unity3d.com/index.php/SmoothFollow2
 */
public class AstarSmoothFollow2 : MonoBehaviour {
	public Transform target;
	public float distance = 3.0f;
	public float height = 3.0f;
	public float damping = 5.0f;
	public bool smoothRotation = true;
	public bool followBehind = true;
	public float rotationDamping = 10.0f;
	public bool staticOffset = false;
	
	void LateUpdate () {
		Vector3 wantedPosition;
		if (staticOffset) {
			wantedPosition = target.position + new Vector3(0,height,distance);
		} else {
			
	       if(followBehind)
		       wantedPosition = target.TransformPoint(0, height, -distance);
	       else
		       wantedPosition = target.TransformPoint(0, height, distance);
		}
	       transform.position = Vector3.Lerp (transform.position, wantedPosition, Time.deltaTime * damping);

	       if (smoothRotation) {
		       Quaternion wantedRotation = Quaternion.LookRotation(target.position - transform.position, target.up);
		       transform.rotation = Quaternion.Slerp (transform.rotation, wantedRotation, Time.deltaTime * rotationDamping);
	       }
	       else transform.LookAt (target, target.up);
	 }
}