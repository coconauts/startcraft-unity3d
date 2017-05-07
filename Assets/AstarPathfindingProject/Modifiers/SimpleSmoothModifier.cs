using UnityEngine;
using System.Collections.Generic;

using Pathfinding;
using Pathfinding.Util;

namespace Pathfinding {
	[AddComponentMenu ("Pathfinding/Modifiers/Simple Smooth")]
	[System.Serializable]
	/** Modifier which smooths the path. This modifier can smooth a path by either moving the points closer together (Simple) or using Bezier curves (Bezier).\n
	 * \ingroup modifiers
	 * Attach this component to the same GameObject as a Seeker component.
	 * \n
	 * This component will hook in to the Seeker's path post-processing system and will post process any paths it searches for.
	 * Take a look at the Modifier Priorities settings on the Seeker component to determine where in the process this modifier should process the path.
	 * \n
	 * \n
	 * Several smoothing types are available, here follows a list of them and a short description of what they do, and how they work.
	 * But the best way is really to experiment with them yourself.\n
	 * 
	 * - <b>Simple</b> Smooths the path by drawing all points close to each other. This results in paths that might cut corners if you are not careful.
	 * It will also subdivide the path to create more more points to smooth as otherwise it would still be quite rough.
	 * \shadowimage{smooth_simple.png}
	 * - <b>Bezier</b> Smooths the path using Bezier curves. This results a smooth path which will always pass through all points in the path, but make sure it doesn't turn too quickly.
	 * \shadowimage{smooth_bezier.png}
	 * - <b>OffsetSimple</b> An alternative to Simple smooth which will offset the path outwards in each step to minimize the corner-cutting.
	 * But be careful, if too high values are used, it will turn into loops and look really ugly.
	 * - <b>Curved Non Uniform</b> \shadowimage{smooth_curved_nonuniform.png}
	 * 
	 * \note Modifies vectorPath array
	 * \todo Make the smooth modifier take the world geometry into account when smoothing
	 * */
	public class SimpleSmoothModifier : MonoModifier {
		
	#if UNITY_EDITOR
		[UnityEditor.MenuItem ("CONTEXT/Seeker/Add Simple Smooth Modifier")]
		public static void AddComp (UnityEditor.MenuCommand command) {
			(command.context as Component).gameObject.AddComponent (typeof(SimpleSmoothModifier));
		}
	#endif
		
		public override ModifierData input {
			get { return ModifierData.All; }
		}
		
		public override ModifierData output {
			get {
				ModifierData result = ModifierData.VectorPath;
				if (iterations == 0 && smoothType == SimpleSmoothModifier.SmoothType.Simple && !uniformLength) {
					result |= ModifierData.StrictVectorPath;
				}
				return result;
			}
		}
		
		/** Type of smoothing to use */
		public SmoothType smoothType = SmoothType.Simple;
		
		/** Number of times to subdivide when not using a uniform length */
		public int subdivisions = 2;
		
		/** Number of times to apply smoothing */
		public int iterations = 2;
		
		/** The strength of the smoothing. A value from 0 to 1 is recommended, but values larger than 1 works too.*/
		public float strength = 0.5F;
		
		/** Toggle to divide all lines in equal length segments.
		  * \see #maxSegmentLength
		  */
		public bool uniformLength = true;
		
		/** The length of the segments in the smoothed path when using #uniformLength.
		 * A high value yields rough paths and low value yields very smooth paths, but is slower */
		public float maxSegmentLength = 2F;
		
		/** Length factor of the bezier curves' tangents' */
		public float bezierTangentLength = 0.4F;
		
		/** Offset to apply in each smoothing iteration when using Offset Simple. \see #smoothType */
		public float offset = 0.2F;
		
		public enum SmoothType {
			Simple,
			Bezier,
			OffsetSimple,
			CurvedNonuniform
		}
		
		public override void Apply (Path p, ModifierData source) {
			
			//This should never trigger unless some other modifier has messed stuff up
			if (p.vectorPath == null) {
				Debug.LogWarning ("Can't process NULL path (has another modifier logged an error?)");
				return;
			}
			
			List<Vector3> path = null;
			
			switch (smoothType) {
				case SmoothType.Simple:
					path = SmoothSimple (p.vectorPath); break;
				case SmoothType.Bezier:
					path = SmoothBezier (p.vectorPath); break;
				case SmoothType.OffsetSimple:
					path = SmoothOffsetSimple (p.vectorPath); break;
				case SmoothType.CurvedNonuniform:
					path = CurvedNonuniform (p.vectorPath); break;
			}
			
			if (path != p.vectorPath) {
				ListPool<Vector3>.Release (p.vectorPath);
				p.vectorPath = path;
			}
			//.vectorPath.Clear ();
			//p.vectorPath.AddRange (path);
		}
		
		public float factor = 0.1F;
		
		public List<Vector3> CurvedNonuniform (List<Vector3> path) {
			
			if (maxSegmentLength <= 0) {
				Debug.LogWarning ("Max Segment Length is <= 0 which would cause DivByZero-exception or other nasty errors (avoid this)");
				return path;
			}
			
			int pointCounter = 0;
			for (int i=0;i<path.Count-1;i++) {
				//pointCounter += Mathf.FloorToInt ((path[i]-path[i+1]).magnitude / maxSegmentLength)+1;
				
				float dist = (path[i]-path[i+1]).magnitude;
				//In order to avoid floating point errors as much as possible, and in lack of a better solution
				//loop through it EXACTLY as the other code further down will
				for (float t=0;t<=dist;t+=maxSegmentLength) {
					pointCounter++;
				}
			}
			
			List<Vector3> subdivided = ListPool<Vector3>.Claim (pointCounter);
			
			//Set first velocity
			Vector3 preEndVel = (path[1]-path[0]).normalized;
			
			for (int i=0;i<path.Count-1;i++) {
				//subdivided[counter] = path[i];
				//counter++;
				
				float dist = (path[i]-path[i+1]).magnitude;
				
				Vector3 startVel1 = preEndVel;
				Vector3 endVel1 = i < path.Count-2 ? ((path[i+2]-path[i+1]).normalized - (path[i]-path[i+1]).normalized).normalized : (path[i+1]-path[i]).normalized;
				
				Vector3 startVel = startVel1 * dist * factor;
				Vector3 endVel = endVel1 * dist * factor;
				
				
				               
				Vector3 start = path[i];
				Vector3 end = path[i+1];
				
				//Vector3 p1 = start + startVel;
				//Vector3 p2 = end - endVel;
				           
				float onedivdist = 1F / dist;
				
				for (float t=0;t<=dist;t+=maxSegmentLength) {
					
					float t2 = t * onedivdist;
					
					subdivided.Add (GetPointOnCubic(start,end,startVel,endVel,t2));
					//counter++;
				}
				
				preEndVel = endVel1;
				
			}
			
			subdivided[subdivided.Count-1] = path[path.Count-1];
			
			return subdivided;
		}
		
		public static Vector3 GetPointOnCubic (Vector3 a, Vector3 b, Vector3 tan1, Vector3 tan2, float t) {
			float t2 = t*t, t3 = t2*t;
			
			//float s = (float)t / (float)steps;  // scale s to go from 0 to 1
			float h1 =  2*t3 - 3*t2 + 1;       	  // calculate basis function 1
			float h2 = -2*t3 + 3*t2;              // calculate basis function 2
			float h3 =   t3 -  2*t2 + t;       	  // calculate basis function 3
			float h4 =   t3 -  t2;            	  // calculate basis function 4
			return		h1*a +                   	 // multiply and sum all funtions
						h2*b +                   	 // together to build the interpolated
						h3*tan1 +                	 // point along the curve.
						h4*tan2;
			
			//return //(b*(3*t*t-2*t*t*t)+c*(t-2*t*t+t*t*t)+d*(-t*t+t*t*t)+a*(1-3*t*t+2*t*t*t));
				
			//float mt = 1-t;
			//return start*mt*mt*mt + (start+startVel)*3*t*mt*mt + 3*mt*t*t*(end-endVel) + t*t*t*end;
			//return start*t*t*t + (start+startVel)*t*t + (end-endVel)*t + end;
			//return start*t*t*t + start*t*t + startVel*t*t + end*t + endVel*t + end;
		}
		
		public List<Vector3> SmoothOffsetSimple (List<Vector3> path) {
			
			if (path.Count <= 2 || iterations <= 0) {
				return path;
			}
			
			if (iterations > 12) {
				Debug.LogWarning ("A very high iteration count was passed, won't let this one through");
				return path;
			}
			
			int maxLength = (path.Count-2)*(int)Mathf.Pow(2,iterations)+2;
			
			List<Vector3> subdivided = ListPool<Vector3>.Claim (maxLength);
			//new Vector3[(path.Length-2)*(int)Mathf.Pow(2,iterations)+2];
			List<Vector3> subdivided2 = ListPool<Vector3>.Claim (maxLength);
			//new Vector3[(path.Length-2)*(int)Mathf.Pow(2,iterations)+2];
			
			for (int i=0;i<maxLength;i++) { subdivided.Add (Vector3.zero); subdivided2.Add (Vector3.zero); }
			
			for (int i=0;i<path.Count;i++) {
				subdivided[i] = path[i];
			}
			
			for (int iteration=0;iteration < iterations; iteration++) {
				int currentPathLength = (path.Count-2)*(int)Mathf.Pow(2,iteration)+2;
				
				//Switch the arrays
				List<Vector3> tmp = subdivided;
				subdivided = subdivided2;
				subdivided2 = tmp;
				
				float nextMultiplier = 1F;
				
				for (int i=0;i<currentPathLength-1;i++) {
					Vector3 current = subdivided2[i];
					Vector3 next = subdivided2[i+1];
					
					Vector3 normal = Vector3.Cross (next-current,Vector3.up);
					normal = normal.normalized;
					
					//This didn't work very well, made the path jaggy
					/*Vector3 dir = next-current;
					dir *= strength*0.5F;
					current += dir;
					next -= dir;*/
					
					bool firstRight = false;
					bool secondRight = false;
					bool setFirst = false;
					bool setSecond = false;
					if (i != 0 && !Polygon.IsColinear (current,next, subdivided2[i-1])) {
						setFirst = true;
						firstRight = Polygon.Left (current,next, subdivided2[i-1]);
					}
					if (i < currentPathLength-1 && !Polygon.IsColinear (current,next, subdivided2[i+2])) {
						setSecond = true;
						secondRight = Polygon.Left (current,next,subdivided2[i+2]);
					}
					
					if (setFirst) {
						subdivided[i*2] = current + (firstRight ? normal*offset*nextMultiplier : -normal*offset*nextMultiplier);
					} else {
						subdivided[i*2] = current;
					}
					
					//Didn't work very well
					/*if (setFirst && setSecond) {
						if (firstRight != secondRight) {
							nextMultiplier = 0.5F;
						} else {
							nextMultiplier = 1F;
						}
					}*/
					
					if (setSecond) {
						subdivided[i*2+1] = next  + (secondRight ? normal*offset*nextMultiplier : -normal*offset*nextMultiplier);
					} else {
						subdivided[i*2+1] = next;
					}
				}
				
				subdivided[(path.Count-2)*(int)Mathf.Pow(2,iteration+1)+2-1] = subdivided2[currentPathLength-1];
			}
			
			
			ListPool<Vector3>.Release (subdivided2);
			
			return subdivided;
		}
		
		public List<Vector3> SmoothSimple (List<Vector3> path) {
			
			if (path.Count < 2) {
				return path;
			}
			
			if (uniformLength) {
				int numSegments = 0;
				maxSegmentLength = maxSegmentLength < 0.005F ? 0.005F : maxSegmentLength;
				
				for (int i=0;i<path.Count-1;i++) {
					float length = Vector3.Distance (path[i],path[i+1]);
					
					numSegments += Mathf.FloorToInt (length / maxSegmentLength);
				}
				
				List<Vector3> subdivided = ListPool<Vector3>.Claim (numSegments+1);
				
				int c = 0;
				
				float carry = 0;
				
				for (int i=0;i<path.Count-1;i++) {
					
					float length = Vector3.Distance (path[i],path[i+1]);
					
					int numSegmentsForSegment = Mathf.FloorToInt ((length + carry) / maxSegmentLength);
					
					float carryOffset = carry/length;
					//float t = 1F / numSegmentsForSegment;
					
					Vector3 dir = path[i+1] - path[i];
					
					for (int q=0;q<numSegmentsForSegment;q++) {
						//Debug.Log (q+" "+c+" "+numSegments+" "+length+" "+numSegmentsForSegment);
						subdivided.Add (dir*(System.Math.Max (0, (float)q/numSegmentsForSegment - carryOffset)) + path[i]);
						c++;
					}
					
					carry = (length + carry) % maxSegmentLength;
				}
				
				subdivided.Add (path[path.Count-1]);
				
				if (strength != 0) {
					for (int it = 0; it < iterations; it++) {
						Vector3 prev = subdivided[0];
						
						for (int i=1;i<subdivided.Count-1;i++) {
							
							Vector3 tmp = subdivided[i];
							
							subdivided[i] = Vector3.Lerp (tmp, (prev+subdivided[i+1])/2F,strength);
							
							prev = tmp;
						}
					}
				}
				
				return subdivided;
			} else {
				List<Vector3> subdivided = ListPool<Vector3>.Claim ();
				//Polygon.Subdivide (path,subdivisions);
				if (subdivisions < 0) subdivisions = 0;
				
				int steps = 1 << subdivisions;
				
				for (int i=0;i<path.Count-1;i++)
					for (int j=0;j<steps;j++)
						subdivided.Add (Vector3.Lerp (path[i],path[i+1],(float)j / steps));
				
				for (int it = 0; it < iterations; it++) {
					Vector3 prev = subdivided[0];
					
					for (int i=1;i<subdivided.Count-1;i++) {
						
						Vector3 tmp = subdivided[i];
						
						subdivided[i] = Vector3.Lerp (tmp, (prev+subdivided[i+1])/2F,strength);
						
						prev = tmp;
					}
				}
				return subdivided;
			}
		}
		
		public List<Vector3> SmoothBezier (List<Vector3> path) {
			if (subdivisions < 0) subdivisions = 0;
			
			int subMult = 1 << subdivisions;
			List<Vector3> subdivided = ListPool<Vector3>.Claim ();
			//new Vector3[(path.Length-1)*(int)subMult+1];
			
			for (int i=0;i<path.Count-1;i++) {
				
				Vector3 tangent1 = Vector3.zero;
				Vector3 tangent2 = Vector3.zero;
				if (i == 0) {
					tangent1 = path[i+1]-path[i];
				} else {
					tangent1 = path[i+1]-path[i-1];
				}
				
				if (i == path.Count-2) {
					tangent2 = path[i]-path[i+1];
				} else {
					tangent2 = path[i]-path[i+2];
				}
				
				tangent1 *= bezierTangentLength;
				tangent2 *= bezierTangentLength;
				
				Vector3 v1 = path[i];
				Vector3 v2 = v1+tangent1;
				Vector3 v4 = path[i+1];
				Vector3 v3 = v4+tangent2;
				
				
				for (int j=0;j<subMult;j++) {
					subdivided.Add (AstarMath.CubicBezier (v1,v2,v3,v4, (float)j/subMult));
				}
			}
			
			//Assign the last point
			subdivided.Add (path[path.Count-1]);
			
			return subdivided;
		}
		
	}
}