using UnityEditor;
using UnityEngine;

namespace Pathfinding {
	[CustomEditor(typeof(AIBase), true)]
	[CanEditMultipleObjects]
	public class BaseAIEditor : EditorBase {
		float lastSeenCustomGravity = float.NegativeInfinity;

		protected override void Inspector () {
			var isAIPath = typeof(AIPath).IsAssignableFrom(target.GetType());

			Section("Shape");
			FloatField("radius", min: 0.01f);
			FloatField("height", min: 0.01f);

			Section("Pathfinding");
			if (PropertyField("canSearch")) {
				EditorGUI.indentLevel++;
				FloatField("repathRate", min: 0f);
				EditorGUI.indentLevel--;
			}

			Section("Movement");

			PropertyField("canMove");
			FloatField("maxSpeed", min: 0f);

			if (isAIPath) {
				EditorGUI.BeginChangeCheck();
				var acceleration = FindProperty("maxAcceleration");
				int acc = acceleration.hasMultipleDifferentValues ? -1 : (acceleration.floatValue >= 0 ? 1 : 0);
				var nacc = EditorGUILayout.Popup("Max Acceleration", acc, new [] { "Default", "Custom" });
				if (EditorGUI.EndChangeCheck()) {
					if (nacc == 0) acceleration.floatValue = -2.5f;
					else if (acceleration.floatValue < 0) acceleration.floatValue = 10;
				}

				if (!acceleration.hasMultipleDifferentValues && nacc == 1) {
					EditorGUI.indentLevel++;
					PropertyField(acceleration.propertyPath);
					EditorGUI.indentLevel--;
					acceleration.floatValue = Mathf.Max(acceleration.floatValue, 0.01f);
				}

				Popup("orientation", new [] { new GUIContent("ZAxisForward (for 3D games)"), new GUIContent("YAxisForward (for 2D games)") });
			} else {
				FloatField("acceleration", min: 0f);

				// The RichAI script doesn't really support any orientation other than Z axis forward, so don't expose it in the inspector
				FindProperty("orientation").enumValueIndex = (int)OrientationMode.ZAxisForward;
			}

			if (PropertyField("enableRotation")) {
				EditorGUI.indentLevel++;
				FloatField("rotationSpeed", min: 0f);
				PropertyField("slowWhenNotFacingTarget");
				EditorGUI.indentLevel--;
			}

			if (isAIPath) {
				FloatField("pickNextWaypointDist", min: 0f);
				FloatField("slowdownDistance", min: 0f);
			} else {
				FloatField("slowdownTime", min: 0f);
				FloatField("wallForce", min: 0f);
				FloatField("wallDist", min: 0f);
				PropertyField("funnelSimplification");
			}

			FloatField("endReachedDistance", min: 0f);

			if (isAIPath) {
				PropertyField("alwaysDrawGizmos");
				PropertyField("whenCloseToDestination");
				PropertyField("constrainInsideGraph");
			}

			var mono = target as MonoBehaviour;
			var rigid = mono.GetComponent<Rigidbody>();
			var rigid2D = mono.GetComponent<Rigidbody2D>();
			var controller = mono.GetComponent<CharacterController>();
			var canUseGravity = (controller != null && controller.enabled) || ((rigid == null || rigid.isKinematic) && (rigid2D == null || rigid2D.isKinematic));

			var gravity = FindProperty("gravity");
			var groundMask = FindProperty("groundMask");

			if (canUseGravity) {
				EditorGUI.BeginChangeCheck();
				int grav = gravity.hasMultipleDifferentValues ? -1 : (gravity.vector3Value == Vector3.zero ? 0 : (float.IsNaN(gravity.vector3Value.x) ? 1 : 2));
				var ngrav = EditorGUILayout.Popup("Gravity", grav, new [] { "None", "Use Project Settings", "Custom" });
				if (EditorGUI.EndChangeCheck()) {
					if (ngrav == 0) gravity.vector3Value = Vector3.zero;
					else if (ngrav == 1) gravity.vector3Value = new Vector3(float.NaN, float.NaN, float.NaN);
					else if (float.IsNaN(gravity.vector3Value.x) || gravity.vector3Value == Vector3.zero) gravity.vector3Value = Physics.gravity;
					lastSeenCustomGravity = float.NegativeInfinity;
				}

				if (!gravity.hasMultipleDifferentValues) {
					// A sort of delayed Vector3 field (to prevent the field from dissappearing if you happen to enter zeroes into x, y and z for a short time)
					// Note: cannot use != in this case because that will not give the correct result in case of NaNs
					if (!(gravity.vector3Value == Vector3.zero)) lastSeenCustomGravity = Time.realtimeSinceStartup;
					if (Time.realtimeSinceStartup - lastSeenCustomGravity < 2f) {
						EditorGUI.indentLevel++;
						if (!float.IsNaN(gravity.vector3Value.x)) {
							PropertyField(gravity.propertyPath);
						}

						if (controller == null || !controller.enabled) {
							PropertyField(groundMask.propertyPath, "Raycast Ground Mask");
						}

						EditorGUI.indentLevel--;
					}
				}
			} else {
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.Popup(new GUIContent(gravity.displayName, "Disabled because a non-kinematic rigidbody is attached"), 0, new [] { new GUIContent("Handled by Rigidbody") });
				EditorGUI.EndDisabledGroup();
			}

			if ((rigid != null || rigid2D != null) && (controller != null && controller.enabled)) {
				EditorGUILayout.HelpBox("You are using both a Rigidbody and a Character Controller. Those components are not really designed for that. Please use only one of them.", MessageType.Warning);
			}
		}
	}
}
