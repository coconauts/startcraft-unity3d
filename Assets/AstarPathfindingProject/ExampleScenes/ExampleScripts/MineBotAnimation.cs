using UnityEngine;

namespace Pathfinding.Examples {
	/// <summary>
	/// Animation helper specifically made for the spider robot in the example scenes.
	/// The spider robot (or mine-bot) which has been copied from the Unity Example Project
	/// can have this script attached to be able to pathfind around with animations working properly.\n
	/// This script should be attached to a parent GameObject however since the original bot has Z+ as up.
	/// This component requires Z+ to be forward and Y+ to be up.\n
	///
	/// A movement script (e.g AIPath) must also be attached to the same GameObject to actually move the unit.
	///
	/// Animation is handled by this component. The Animator component refered to in <see cref="anim"/> should have a single parameter called NormalizedSpeed.
	/// When the end of path is reached, if the <see cref="endOfPathEffect"/> is not null, it will be instantiated at the current position. However a check will be
	/// done so that it won't spawn effects too close to the previous spawn-point.
	/// [Open online documentation to see images]
	/// </summary>
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_examples_1_1_mine_bot_animation.php")]
	public class MineBotAnimation : VersionedMonoBehaviour {
		/// <summary>
		/// Animation component.
		/// Should hold animations "awake" and "forward"
		/// </summary>
		public Animator anim;

		/// <summary>
		/// Effect which will be instantiated when end of path is reached.
		/// See: <see cref="OnTargetReached"/>
		/// </summary>
		public GameObject endOfPathEffect;

		bool isAtDestination;

		IAstarAI ai;
		Transform tr;

		protected override void Awake () {
			base.Awake();
			ai = GetComponent<IAstarAI>();
			tr = GetComponent<Transform>();
		}

		/// <summary>Point for the last spawn of <see cref="endOfPathEffect"/></summary>
		protected Vector3 lastTarget;

		/// <summary>
		/// Called when the end of path has been reached.
		/// An effect (<see cref="endOfPathEffect)"/> is spawned when this function is called
		/// However, since paths are recalculated quite often, we only spawn the effect
		/// when the current position is some distance away from the previous spawn-point
		/// </summary>
		void OnTargetReached () {
			if (endOfPathEffect != null && Vector3.Distance(tr.position, lastTarget) > 1) {
				GameObject.Instantiate(endOfPathEffect, tr.position, tr.rotation);
				lastTarget = tr.position;
			}
		}

		protected void Update () {
			if (ai.reachedEndOfPath) {
				if (!isAtDestination) OnTargetReached();
				isAtDestination = true;
			} else isAtDestination = false;

			// Calculate the velocity relative to this transform's orientation
			Vector3 relVelocity = tr.InverseTransformDirection(ai.velocity);
			relVelocity.y = 0;

			// Speed relative to the character size
			anim.SetFloat("NormalizedSpeed", relVelocity.magnitude / anim.transform.lossyScale.x);
		}
	}
}
