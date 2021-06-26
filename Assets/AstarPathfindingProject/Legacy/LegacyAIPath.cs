#pragma warning disable 618
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Pathfinding.Legacy {
	using Pathfinding;
	using Pathfinding.RVO;

	/// <summary>
	/// AI for following paths.
	/// This AI is the default movement script which comes with the A* Pathfinding Project.
	/// It is in no way required by the rest of the system, so feel free to write your own. But I hope this script will make it easier
	/// to set up movement for the characters in your game. This script is not written for high performance, so I do not recommend using it for large groups of units.
	/// \n
	/// \n
	/// This script will try to follow a target transform, in regular intervals, the path to that target will be recalculated.
	/// It will on FixedUpdate try to move towards the next point in the path.
	/// However it will only move in the forward direction, but it will rotate around it's Y-axis
	/// to make it reach the target.
	///
	/// \section variables Quick overview of the variables
	/// In the inspector in Unity, you will see a bunch of variables. You can view detailed information further down, but here's a quick overview.\n
	/// The <see cref="repathRate"/> determines how often it will search for new paths, if you have fast moving targets, you might want to set it to a lower value.\n
	/// The <see cref="target"/> variable is where the AI will try to move, it can be a point on the ground where the player has clicked in an RTS for example.
	/// Or it can be the player object in a zombie game.\n
	/// The speed is self-explanatory, so is turningSpeed, however <see cref="slowdownDistance"/> might require some explanation.
	/// It is the approximate distance from the target where the AI will start to slow down. Note that this doesn't only affect the end point of the path
	/// but also any intermediate points, so be sure to set <see cref="forwardLook"/> and <see cref="pickNextWaypointDist"/> to a higher value than this.\n
	/// <see cref="pickNextWaypointDist"/> is simply determines within what range it will switch to target the next waypoint in the path.\n
	/// <see cref="forwardLook"/> will try to calculate an interpolated target point on the current segment in the path so that it has a distance of <see cref="forwardLook"/> from the AI\n
	/// Below is an image illustrating several variables as well as some internal ones, but which are relevant for understanding how it works.
	/// Note that the <see cref="forwardLook"/> range will not match up exactly with the target point practically, even though that's the goal.
	/// [Open online documentation to see images]
	/// This script has many movement fallbacks.
	/// If it finds a NavmeshController, it will use that, otherwise it will look for a character controller, then for a rigidbody and if it hasn't been able to find any
	/// it will use Transform.Translate which is guaranteed to always work.
	///
	/// Deprecated: Use the AIPath class instead. This class only exists for compatibility reasons.
	/// </summary>
	[RequireComponent(typeof(Seeker))]
	[AddComponentMenu("Pathfinding/Legacy/AI/Legacy AIPath (3D)")]
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_legacy_1_1_legacy_a_i_path.php")]
	public class LegacyAIPath : AIPath {
		/// <summary>
		/// Target point is Interpolated on the current segment in the path so that it has a distance of <see cref="forwardLook"/> from the AI.
		/// See the detailed description of AIPath for an illustrative image
		/// </summary>
		public float forwardLook = 1;

		/// <summary>
		/// Do a closest point on path check when receiving path callback.
		/// Usually the AI has moved a bit between requesting the path, and getting it back, and there is usually a small gap between the AI
		/// and the closest node.
		/// If this option is enabled, it will simulate, when the path callback is received, movement between the closest node and the current
		/// AI position. This helps to reduce the moments when the AI just get a new path back, and thinks it ought to move backwards to the start of the new path
		/// even though it really should just proceed forward.
		/// </summary>
		public bool closestOnPathCheck = true;

		protected float minMoveScale = 0.05F;

		/// <summary>Current index in the path which is current target</summary>
		protected int currentWaypointIndex = 0;

		protected Vector3 lastFoundWaypointPosition;
		protected float lastFoundWaypointTime = -9999;

		protected override void Awake () {
			base.Awake();
		}

		/// <summary>
		/// Called when a requested path has finished calculation.
		/// A path is first requested by <see cref="SearchPath"/>, it is then calculated, probably in the same or the next frame.
		/// Finally it is returned to the seeker which forwards it to this function.\n
		/// </summary>
		protected override void OnPathComplete (Path _p) {
			ABPath p = _p as ABPath;

			if (p == null) throw new System.Exception("This function only handles ABPaths, do not use special path types");

			waitingForPathCalculation = false;

			//Claim the new path
			p.Claim(this);

			// Path couldn't be calculated of some reason.
			// More info in p.errorLog (debug string)
			if (p.error) {
				p.Release(this);
				return;
			}

			//Release the previous path
			if (path != null) path.Release(this);

			//Replace the old path
			path = p;

			//Reset some variables
			currentWaypointIndex = 0;
			reachedEndOfPath = false;

			//The next row can be used to find out if the path could be found or not
			//If it couldn't (error == true), then a message has probably been logged to the console
			//however it can also be got using p.errorLog
			//if (p.error)

			if (closestOnPathCheck) {
				// Simulate movement from the point where the path was requested
				// to where we are right now. This reduces the risk that the agent
				// gets confused because the first point in the path is far away
				// from the current position (possibly behind it which could cause
				// the agent to turn around, and that looks pretty bad).
				Vector3 p1 = Time.time - lastFoundWaypointTime < 0.3f ? lastFoundWaypointPosition : p.originalStartPoint;
				Vector3 p2 = GetFeetPosition();
				Vector3 dir = p2-p1;
				float magn = dir.magnitude;
				dir /= magn;
				int steps = (int)(magn/pickNextWaypointDist);

#if ASTARDEBUG
				Debug.DrawLine(p1, p2, Color.red, 1);
#endif

				for (int i = 0; i <= steps; i++) {
					CalculateVelocity(p1);
					p1 += dir;
				}
			}
		}

		protected override void Update () {
			if (!canMove) { return; }

			Vector3 dir = CalculateVelocity(GetFeetPosition());

			//Rotate towards targetDirection (filled in by CalculateVelocity)
			RotateTowards(targetDirection);

			if (controller != null) {
				controller.SimpleMove(dir);
			} else if (rigid != null) {
				rigid.AddForce(dir);
			} else {
				tr.Translate(dir*Time.deltaTime, Space.World);
			}
		}

		/// <summary>
		/// Relative direction to where the AI is heading.
		/// Filled in by <see cref="CalculateVelocity"/>
		/// </summary>
		protected new Vector3 targetDirection;

		protected float XZSqrMagnitude (Vector3 a, Vector3 b) {
			float dx = b.x-a.x;
			float dz = b.z-a.z;

			return dx*dx + dz*dz;
		}

		/// <summary>
		/// Calculates desired velocity.
		/// Finds the target path segment and returns the forward direction, scaled with speed.
		/// A whole bunch of restrictions on the velocity is applied to make sure it doesn't overshoot, does not look too far ahead,
		/// and slows down when close to the target.
		/// /see speed
		/// /see endReachedDistance
		/// /see slowdownDistance
		/// /see CalculateTargetPoint
		/// /see targetPoint
		/// /see targetDirection
		/// /see currentWaypointIndex
		/// </summary>
		protected new Vector3 CalculateVelocity (Vector3 currentPosition) {
			if (path == null || path.vectorPath == null || path.vectorPath.Count == 0) return Vector3.zero;

			List<Vector3> vPath = path.vectorPath;

			if (vPath.Count == 1) {
				vPath.Insert(0, currentPosition);
			}

			if (currentWaypointIndex >= vPath.Count) { currentWaypointIndex = vPath.Count-1; }

			if (currentWaypointIndex <= 1) currentWaypointIndex = 1;

			while (true) {
				if (currentWaypointIndex < vPath.Count-1) {
					//There is a "next path segment"
					float dist = XZSqrMagnitude(vPath[currentWaypointIndex], currentPosition);
					//Mathfx.DistancePointSegmentStrict (vPath[currentWaypointIndex+1],vPath[currentWaypointIndex+2],currentPosition);
					if (dist < pickNextWaypointDist*pickNextWaypointDist) {
						lastFoundWaypointPosition = currentPosition;
						lastFoundWaypointTime = Time.time;
						currentWaypointIndex++;
					} else {
						break;
					}
				} else {
					break;
				}
			}

			Vector3 dir = vPath[currentWaypointIndex] - vPath[currentWaypointIndex-1];
			Vector3 targetPosition = CalculateTargetPoint(currentPosition, vPath[currentWaypointIndex-1], vPath[currentWaypointIndex]);


			dir = targetPosition-currentPosition;
			dir.y = 0;
			float targetDist = dir.magnitude;

			float slowdown = Mathf.Clamp01(targetDist / slowdownDistance);

			this.targetDirection = dir;

			if (currentWaypointIndex == vPath.Count-1 && targetDist <= endReachedDistance) {
				if (!reachedEndOfPath) { reachedEndOfPath = true; OnTargetReached(); }

				//Send a move request, this ensures gravity is applied
				return Vector3.zero;
			}

			Vector3 forward = tr.forward;
			float dot = Vector3.Dot(dir.normalized, forward);
			float sp = maxSpeed * Mathf.Max(dot, minMoveScale) * slowdown;

#if ASTARDEBUG
			Debug.DrawLine(vPath[currentWaypointIndex-1], vPath[currentWaypointIndex], Color.black);
			Debug.DrawLine(GetFeetPosition(), targetPosition, Color.red);
			Debug.DrawRay(targetPosition, Vector3.up, Color.red);
			Debug.DrawRay(GetFeetPosition(), dir, Color.yellow);
			Debug.DrawRay(GetFeetPosition(), forward*sp, Color.cyan);
#endif

			if (Time.deltaTime > 0) {
				sp = Mathf.Clamp(sp, 0, targetDist/(Time.deltaTime*2));
			}

			return forward*sp;
		}

		/// <summary>
		/// Rotates in the specified direction.
		/// Rotates around the Y-axis.
		/// See: turningSpeed
		/// </summary>
		protected void RotateTowards (Vector3 dir) {
			if (dir == Vector3.zero) return;

			Quaternion rot = tr.rotation;
			Quaternion toTarget = Quaternion.LookRotation(dir);

			rot = Quaternion.Slerp(rot, toTarget, turningSpeed*Time.deltaTime);
			Vector3 euler = rot.eulerAngles;
			euler.z = 0;
			euler.x = 0;
			rot = Quaternion.Euler(euler);

			tr.rotation = rot;
		}

		/// <summary>
		/// Calculates target point from the current line segment.
		/// See: <see cref="forwardLook"/>
		/// TODO: This function uses .magnitude quite a lot, can it be optimized?
		/// </summary>
		/// <param name="p">Current position</param>
		/// <param name="a">Line segment start</param>
		/// <param name="b">Line segment end
		/// The returned point will lie somewhere on the line segment.</param>
		protected Vector3 CalculateTargetPoint (Vector3 p, Vector3 a, Vector3 b) {
			a.y = p.y;
			b.y = p.y;

			float magn = (a-b).magnitude;
			if (magn == 0) return a;

			float closest = Mathf.Clamp01(VectorMath.ClosestPointOnLineFactor(a, b, p));
			Vector3 point = (b-a)*closest + a;
			float distance = (point-p).magnitude;

			float lookAhead = Mathf.Clamp(forwardLook - distance, 0.0F, forwardLook);

			float offset = lookAhead / magn;
			offset = Mathf.Clamp(offset+closest, 0.0F, 1.0F);
			return (b-a)*offset + a;
		}
	}
}
