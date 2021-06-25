using UnityEngine;
using Pathfinding.Util;

namespace Pathfinding {
	// Obsolete methods in AIPath
	public partial class AIPath {
		/// <summary>
		/// True if the end of the path has been reached.
		/// Deprecated: When unifying the interfaces for different movement scripts, this property has been renamed to <see cref="reachedEndOfPath"/>
		/// </summary>
		[System.Obsolete("When unifying the interfaces for different movement scripts, this property has been renamed to reachedEndOfPath.  [AstarUpgradable: 'TargetReached' -> 'reachedEndOfPath']")]
		public bool TargetReached { get { return reachedEndOfPath; } }

		/// <summary>
		/// Rotation speed.
		/// Deprecated: This field has been renamed to <see cref="rotationSpeed"/> and is now in degrees per second instead of a damping factor.
		/// </summary>
		[System.Obsolete("This field has been renamed to #rotationSpeed and is now in degrees per second instead of a damping factor")]
		public float turningSpeed { get { return rotationSpeed/90; } set { rotationSpeed = value*90; } }

		/// <summary>
		/// Maximum speed in world units per second.
		/// Deprecated: Use <see cref="maxSpeed"/> instead
		/// </summary>
		[System.Obsolete("This member has been deprecated. Use 'maxSpeed' instead. [AstarUpgradable: 'speed' -> 'maxSpeed']")]
		public float speed { get { return maxSpeed; } set { maxSpeed = value; } }

		/// <summary>
		/// Direction that the agent wants to move in (excluding physics and local avoidance).
		/// Deprecated: Only exists for compatibility reasons. Use <see cref="desiredVelocity"/> or <see cref="steeringTarget"/> instead instead.
		/// </summary>
		[System.Obsolete("Only exists for compatibility reasons. Use desiredVelocity or steeringTarget instead.")]
		public Vector3 targetDirection {
			get {
				return (steeringTarget - tr.position).normalized;
			}
		}

		/// <summary>
		/// Current desired velocity of the agent (excluding physics and local avoidance but it includes gravity).
		/// Deprecated: This method no longer calculates the velocity. Use the <see cref="desiredVelocity"/> property instead.
		/// </summary>
		[System.Obsolete("This method no longer calculates the velocity. Use the desiredVelocity property instead")]
		public Vector3 CalculateVelocity (Vector3 position) {
			return desiredVelocity;
		}
	}
}
