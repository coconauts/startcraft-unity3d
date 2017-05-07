
using UnityEngine;
using System.Collections;
using Pathfinding;
using System;
using System.Collections.Generic;

namespace Pathfinding {
	
	/** Contains various spline functions.
	 * \ingroup utils
	 */
	class AstarSplines {
		public static Vector3 CatmullRom(Vector3 previous,Vector3 start, Vector3 end, Vector3 next, float elapsedTime) {
			// References used:
			// p.266 GemsV1
			//
			// tension is often set to 0.5 but you can use any reasonable value:
			// http://www.cs.cmu.edu/~462/projects/assn2/assn2/catmullRom.pdf
			//
			// bias and tension controls:
			// http://local.wasp.uwa.edu.au/~pbourke/miscellaneous/interpolation/
		
			float percentComplete = elapsedTime;
			float percentCompleteSquared = percentComplete * percentComplete;
			float percentCompleteCubed = percentCompleteSquared * percentComplete;
			
			/*return previous * (-0.5F*percentCompleteCubed +
							 percentCompleteSquared -
							 tension*percentComplete) +
							 
			start * ((2-tension) *percentCompleteCubed +
					 (tension - 3)*percentCompleteSquared + 1.0F) +
					 
			end * ((tension - 2)*percentCompleteCubed +
					 2.0F *percentCompleteSquared +
					 0.5F*percentComplete) +
					 
			next * (0.5F*percentCompleteCubed -
					tension*percentCompleteSquared);*/
					
			return 
			previous * (-0.5F*percentCompleteCubed +
							 percentCompleteSquared -
							 0.5F*percentComplete) +
							 
			start * 
				(1.5F*percentCompleteCubed +
				-2.5F*percentCompleteSquared + 1.0F) +
				
			end * 
				(-1.5F*percentCompleteCubed +
				2.0F*percentCompleteSquared +
				0.5F*percentComplete) +
				
			next * 
				(0.5F*percentCompleteCubed -
				0.5F*percentCompleteSquared);
		}
		
		public static Vector3 CatmullRomOLD (Vector3 previous,Vector3 start, Vector3 end, Vector3 next, float elapsedTime) {
			// References used:
			// p.266 GemsV1
			//
			// tension is often set to 0.5 but you can use any reasonable value:
			// http://www.cs.cmu.edu/~462/projects/assn2/assn2/catmullRom.pdf
			//
			// bias and tension controls:
			// http://local.wasp.uwa.edu.au/~pbourke/miscellaneous/interpolation/
		
			float percentComplete = elapsedTime;
			float percentCompleteSquared = percentComplete * percentComplete;
			float percentCompleteCubed = percentCompleteSquared * percentComplete;
		
			return previous * (-0.5F*percentCompleteCubed +
							 percentCompleteSquared -
							 0.5F*percentComplete) +
			start * (1.5F*percentCompleteCubed +
					 -2.5F*percentCompleteSquared + 1.0F) +
			end * (-1.5F*percentCompleteCubed +
					 2.0F*percentCompleteSquared +
					 0.5F*percentComplete) +
			next * (0.5F*percentCompleteCubed -
					0.5F*percentCompleteSquared);
		}
	}
	
	/** Utility functions for working with numbers, lines and vectors.
	 * \ingroup utils
	  * \see Polygon */
	public class AstarMath {
		
		/** Returns a hash value for a integer vector.
		 * \author Code got from the internet */
		public static int ComputeVertexHash (int x, int y, int z) {
			uint h1 = 0x8da6b343; // Large multiplicative constants;
			uint h2 = 0xd8163841; // here arbitrarily chosen primes
			uint h3 = 0xcb1ab31f;
			uint n = (uint)(h1 * x + h2 * y + h3 * z);
			
			return (int)(n & ((1<<30)-1));
		}

		/** Returns the closest point on the line. The line is treated as infinite.
		 * \see NearestPointStrict
		 */
		public static Vector3 NearestPoint(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
	    {
	        Vector3 lineDirection = Vector3.Normalize(lineEnd-lineStart);
	        
	        float closestPoint = Vector3.Dot((point-lineStart),lineDirection); //Vector3.Dot(lineDirection,lineDirection);
	        return lineStart+(closestPoint*lineDirection);
	    }
	 
	 	public static float NearestPointFactor (Vector3 lineStart, Vector3 lineEnd, Vector3 point)
	    {
	    	Vector3 lineDirection = lineEnd-lineStart;
	    	float magn = lineDirection.magnitude;
			lineDirection /= magn;
	        
	        float closestPoint = Vector3.Dot((point-lineStart),lineDirection); //Vector3.Dot(lineDirection,lineDirection);
	        return closestPoint / magn;
	    }
	    
		/** Factor of the nearest point on the segment.
		 * Returned value is in the range [0,1] if the point lies on the segment otherwise it just lies on the line.
		 * The closest point can be got by (end-start)*factor + start;
		 */
		public static float NearestPointFactor (Int3 lineStart, Int3 lineEnd, Int3 point)
	    {
	    	Int3 lineDirection = lineEnd-lineStart;
	    	float magn = lineDirection.sqrMagnitude;
	        
	        float closestPoint = Int3.Dot((point-lineStart),lineDirection) / magn; //Vector3.Dot(lineDirection,lineDirection);
			return closestPoint;
	        //return closestPoint / magn;
	    }
		
		/** Factor of the nearest point on the segment.
		 * Returned value is in the range [0,1] if the point lies on the segment otherwise it just lies on the line.
		 * The closest point can be got by (end-start)*factor + start;
		 */
		public static float NearestPointFactor (Int2 lineStart, Int2 lineEnd, Int2 point)
	    {
	    	Int2 lineDirection = lineEnd-lineStart;
	    	double magn = lineDirection.sqrMagnitudeLong;
	        
	        double closestPoint = Int2.DotLong(point-lineStart,lineDirection) / magn; //Vector3.Dot(lineDirection,lineDirection);
			return (float)closestPoint;
	        //return closestPoint / magn;
	    }
		
		/** Returns the closest point on the line segment. The line is NOT treated as infinite.
		 * \see NearestPoint
		 */
	    public static Vector3 NearestPointStrict(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
	    {
	        Vector3 fullDirection = lineEnd-lineStart;
			float magn = fullDirection.magnitude;
	        Vector3 lineDirection = fullDirection/magn;
	        
	        float closestPoint = Vector3.Dot((point-lineStart),lineDirection); //WASTE OF CPU POWER - This is always ONE -- Vector3.Dot(lineDirection,lineDirection);
	        return lineStart+(Mathf.Clamp(closestPoint,0.0f,magn)*lineDirection);
	    }
	    
		/** Returns the closest point on the line segment on the XZ plane. The line is NOT treated as infinite.
		 * \see NearestPoint
		 */
	    public static Vector3 NearestPointStrictXZ (Vector3 lineStart, Vector3 lineEnd, Vector3 point)
	    {
			lineStart.y = point.y;
			lineEnd.y = point.y;
	        Vector3 fullDirection = lineEnd-lineStart;
			Vector3 fullDirection2 = fullDirection;
			fullDirection2.y = 0;
	        Vector3 lineDirection = Vector3.Normalize(fullDirection2);
	        //lineDirection.y = 0;
			
	        float closestPoint = Vector3.Dot((point-lineStart),lineDirection); //WASTE OF CPU POWER - This is always ONE -- Vector3.Dot(lineDirection,lineDirection);
	        return lineStart+(Mathf.Clamp(closestPoint,0.0f,fullDirection2.magnitude)*lineDirection);
	    }
		
	    /** Returns the approximate shortest squared distance between x,z and the line p-q.
	     * The line is considered infinite.
	     * This function is not entirely exact, but it is about twice as fast as DistancePointSegment2. */
	    public static float DistancePointSegment (int x,int z, int px, int pz, int qx, int qz) {
	    	
	    	float pqx = (float)(qx - px);
			float pqz = (float)(qz - pz);
			float dx = (float)(x - px);
			float dz = (float)(z - pz);
			float d = pqx*pqx + pqz*pqz;
			float t = pqx*dx + pqz*dz;
			if (d > 0)
				t /= d;
			if (t < 0)
				t = 0;
			else if (t > 1)
				t = 1;
			
			dx = px + t*pqx - x;
			dz = pz + t*pqz - z;
			
			return dx*dx + dz*dz;
	    }
	    
		/** Returns the approximate shortest squared distance between x,z and the line p-q.
		 * The line is considered infinite.
		 * This function is not entirely exact, but it is about twice as fast as DistancePointSegment2. */
	    public static float DistancePointSegment (Int3 a, Int3 b, Int3 p) {
	    	
	    	float pqx = (float)(b.x - a.x);
			float pqz = (float)(b.z - a.z);
			float dx = (float)(p.x - a.x);
			float dz = (float)(p.z - a.z);
			float d = pqx*pqx + pqz*pqz;
			float t = pqx*dx + pqz*dz;
			if (d > 0)
				t /= d;
			if (t < 0)
				t = 0;
			else if (t > 1)
				t = 1;
			
			dx = a.x + t*pqx - p.x;
			dz = a.z + t*pqz - p.z;
			
			return dx*dx + dz*dz;
	    }
		
	    /*public static float DistancePointSegment2 (int x,int z, int px, int pz, int qx, int qz) {
	    	
	    	Vector3 p = new Vector3 (x,0,z);
	    	
	    	Vector3 p1 = new Vector3 (px,0,pz);
	    	Vector3 p2 = new Vector3 (qx,0,qz);
	    	
	    	Vector3 nearest = NearestPoint (p1,p2,p);
			
			return (nearest-p).sqrMagnitude;
	    }*/
	    
	    /** Returns the distance between x,z and the line p-q. The line is considered infinite. */
	    public static float DistancePointSegment2 (int x,int z, int px, int pz, int qx, int qz) {
	    	
	    	Vector3 p = new Vector3 (x,0,z);
	    	
	    	Vector3 p1 = new Vector3 (px,0,pz);
	    	Vector3 p2 = new Vector3 (qx,0,qz);
	    	
	    	return DistancePointSegment2 (p1,p2,p);
	    }
	   
	    /** Returns the distance between c and the line a-b. The line is considered infinite. */
	    public static float DistancePointSegment2 (Vector3 a, Vector3 b, Vector3 p) {
	    	
	    	float bax = b.x - a.x;
	    	float baz = b.z - a.z;
	    	
	    	float area = Mathf.Abs (bax * (p.z - a.z) - (p.x - a.x) * baz);
	    	
	    	float d = bax*bax+baz*baz;
	    	
	    	if (d > 0) {
	    		return area / Mathf.Sqrt (d);
	    	}
	    	
	    	return (a-p).magnitude;
	    }
	    
	    /** Returns the squared distance between c and the line a-b. The line is not considered infinite. */
	    public static float DistancePointSegmentStrict (Vector3 a, Vector3 b, Vector3 p) {
	    	
	    	Vector3 nearest = NearestPointStrict (a,b,p);
			return (nearest-p).sqrMagnitude;
	    }
	    
		/** Returns a point on a hermite curve. Slow start and slow end, fast in the middle */
		public static float Hermite(float start, float end, float value) {
			
			return Mathf.Lerp(start, end, value * value * (3.0f - 2.0f * value));
		}
		
		/** Returns a point on a cubic bezier curve. \a t is clamped between 0 and 1 */
		public static Vector3 CubicBezier (Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3, float t) {
			t = Mathf.Clamp01 (t);
			float t2 = 1-t;
			return t2*t2*t2 * p0 + 3 * t2*t2 * t * p1 + 3 * t2 * t*t * p2 + t*t*t * p3;
		}
		
		/** Maps a value between startMin and startMax to be between 0 and 1 */
		public static float MapTo (float startMin,float startMax, float value) {
			value -= startMin;
			value /= (startMax-startMin);
			value = Mathf.Clamp01 (value);
			return value;
		}
		
		/** Maps a value (0...1) to be between targetMin and targetMax */
		public static float MapToRange (float targetMin,float targetMax, float value) {
			value *= (targetMax-targetMin);
			value += targetMin;
			return value;
		}
		
		/** Maps a value between startMin and startMax to be between targetMin and targetMax */
		public static float MapTo (float startMin,float startMax, float targetMin, float targetMax, float value) {
			value -= startMin;
			value /= (startMax-startMin);
			value = Mathf.Clamp01 (value);
			value *= (targetMax-targetMin);
			value += targetMin;
			return value;
		}
		
		/** Returns a nicely formatted string for the number of bytes (kB, MB, GB etc). Uses decimal values, not binary ones
		  * \see FormatBytesBinary */
		public static string FormatBytes (int bytes) {
			double sign = bytes >= 0 ? 1D : -1D;
			bytes = bytes >= 0 ? bytes : -bytes;
			
			if (bytes < 1000) {
				return (bytes*sign).ToString ()+" bytes";
			} else if (bytes < 1000000) {
				return ((bytes/1000D)*sign).ToString ("0.0") + " kb";
			} else if (bytes < 1000000000) {
				return ((bytes/1000000D)*sign).ToString ("0.0") +" mb";
			} else {
				return ((bytes/1000000000D)*sign).ToString ("0.0") +" gb";
			}
		}
		
		/** Returns a nicely formatted string for the number of bytes (KiB, MiB, GiB etc). Uses decimal names (KB, Mb - 1000) but calculates using binary values (KiB, MiB - 1024) */
		public static string FormatBytesBinary (int bytes) {
			double sign = bytes >= 0 ? 1D : -1D;
			bytes = bytes >= 0 ? bytes : -bytes;
			
			if (bytes < 1024) {
				return (bytes*sign).ToString ()+" bytes";
			} else if (bytes < 1024*1024) {
				return ((bytes/1024D)*sign).ToString ("0.0") + " kb";
			} else if (bytes < 1024*1024*1024) {
				return ((bytes/(1024D*1024D))*sign).ToString ("0.0") +" mb";
			} else {
				return ((bytes/(1024D*1024D*1024D))*sign).ToString ("0.0") +" gb";
			}
		}
		
		/** Returns bit number \a b from int \a a. The bit number is zero based. Relevant \a b values are from 0 to 31\n
		  * Equals to (a >> b) & 1 */
		public static int Bit (int a, int b) {
			return (a >> b) & 1;
			//return (a & (1 << b)) >> b; //Original code, one extra shift operation required
		}
		
		/** Returns a nice color from int \a i with alpha \a a. Got code from the open-source Recast project, works really good\n
		  * Seems like there are only 64 possible colors from studying the code*/
		public static Color IntToColor (int i, float a) {
			int	r = Bit(i, 1) + Bit(i, 3) * 2 + 1;
			int	g = Bit(i, 2) + Bit(i, 4) * 2 + 1;
			int	b = Bit(i, 0) + Bit(i, 5) * 2 + 1;
			return new Color (r*0.25F,g*0.25F,b*0.25F,a);
		}
	
		/** Distance between two points on the XZ plane */
		public static float MagnitudeXZ (Vector3 a, Vector3 b) {
			Vector3 delta = a-b;
			return (float)Math.Sqrt (delta.x*delta.x+delta.z*delta.z);
		}
		
		/** Squared distance between two points on the XZ plane */
		public static float SqrMagnitudeXZ (Vector3 a, Vector3 b) {
			Vector3 delta = a-b;
			return delta.x*delta.x+delta.z*delta.z;
		}
		
		public static int Repeat (int i, int n) {
			while (i >= n) {
				i -= n;
			}
			return i;
		}
		
		public static float Abs (float a) {
			if (a < 0) {
				return -a;
			}
			return a;
		}
	
		public static int Abs (int a) {
			if (a < 0) {
				return -a;
			}
			return a;
		}
		
		public static float Min (float a, float b) {
			return a < b ? a : b;
		}
		
		public static int Min (int a, int b) {
			return a < b ? a : b;
		}
		
		public static uint Min (uint a, uint b) {
			return a < b ? a : b;
		}
		
		public static float Max (float a, float b) {
			return a > b ? a : b;
		}
		
		public static int Max (int a, int b) {
			return a > b ? a : b;
		}
		
		public static uint Max (uint a, uint b) {
			return a > b ? a : b;
		}
		
		public static ushort Max (ushort a, ushort b) {
			return a > b ? a : b;
		}
		
		public static float Sign (float a) {
			return a < 0 ? -1F : 1F;
		}
		
		public static int Sign (int a) {
			return a < 0 ? -1 : 1;
		}
		
		public static float Clamp (float a, float b, float c) {
			return a > c ? c : a < b ? b : a;
		}
		
		public static int Clamp (int a, int b, int c) {
			return a > c ? c : a < b ? b : a;
		}
		
		public static float Clamp01 (float a) {
			return a > 1 ? 1 : a < 0 ? 0 : a;
		}
		
		public static int Clamp01 (int a) {
			return a > 1 ? 1 : a < 0 ? 0 : a;
		}
		
		public static float Lerp (float a,float b, float t) {
			return a + (b-a)*(t > 1 ? 1 : t < 0 ? 0 : t);
		}
		
		public static int RoundToInt (float v) {
			return (int)(v+0.5F);
		}
		
		public static int RoundToInt (double v) {
			return (int)(v+0.5D);
		}
		
	}
	
	/** Utility functions for working with polygons, lines, and other vector math.
	 * All functions which accepts Vector3s but work in 2D space uses the XZ space if nothing else is said.
	  * \ingroup utils */
	public class Polygon {
		
		/** Signed area of a triangle in the XZ plane multiplied by 2.
		 * This will be negative for clockwise triangles and positive for counter-clockwise ones */
		public static long TriangleArea2 (Int3 a, Int3 b, Int3 c) {
			return (long)(b.x - a.x) * (long)(c.z - a.z) - (long)(c.x - a.x) * (long)(b.z - a.z);
			//a.x*b.z+b.x*c.z+c.x*a.z-a.x*c.z-c.x*b.z-b.x*a.z;
		}
		
		/** Signed area of a triangle in the XZ plane multiplied by 2.
		 * This will be negative for clockwise triangles and positive for counter-clockwise ones.
		 */
		public static float TriangleArea2 (Vector3 a, Vector3 b, Vector3 c) {
			return (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
			//return a.x*b.z+b.x*c.z+c.x*a.z-a.x*c.z-c.x*b.z-b.x*a.z;
		}
		
		/** Signed area of a triangle in the XZ plane multiplied by 2.
		 * This will be negative for clockwise triangles and positive for counter-clockwise ones.
		 * This method can handle larger numbers than TriangleArea2(Int3)
		 */
		public static long TriangleArea (Int3 a, Int3 b, Int3 c) {
			return (long)(b.x - a.x) * (long)(c.z - a.z) - (long)(c.x - a.x) * (long)(b.z - a.z);
			//a.x*b.z+b.x*c.z+c.x*a.z-a.x*c.z-c.x*b.z-b.x*a.z;
		}
		
		/** Signed area of a triangle in the XZ plane multiplied by 2.
		 * This will be negative for clockwise triangles and positive for counter-clockwise ones.
		 * Idential to TriangleArea2(Vector3), kept for compability.
		 */
		public static float TriangleArea (Vector3 a, Vector3 b, Vector3 c) {
			return (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
			//return a.x*b.z+b.x*c.z+c.x*a.z-a.x*c.z-c.x*b.z-b.x*a.z;
		}
	
		/** Returns if the triangle \a ABC contains the point \a p in XZ space */
		public static bool ContainsPoint (Vector3 a, Vector3 b, Vector3 c, Vector3 p) {
			return Polygon.IsClockwiseMargin (a,b, p) && Polygon.IsClockwiseMargin (b,c, p) && Polygon.IsClockwiseMargin (c,a, p);
		}
		
		/** Returns if the triangle \a ABC contains the point \a p */
		public static bool ContainsPoint (Int2 a, Int2 b, Int2 c, Int2 p) {
			return Polygon.IsClockwiseMargin (a,b, p) && Polygon.IsClockwiseMargin (b,c, p) && Polygon.IsClockwiseMargin (c,a, p);
		}
		
		/** Returns if the triangle \a ABC contains the point \a p */
		public static bool ContainsPoint (Int3 a, Int3 b, Int3 c, Int3 p) {
			return Polygon.IsClockwiseMargin (a,b, p) && Polygon.IsClockwiseMargin (b,c, p) && Polygon.IsClockwiseMargin (c,a, p);
		}
		
		/** Checks if \a p is inside the polygon.
		 * \author http://unifycommunity.com/wiki/index.php?title=PolyContainsPoint (Eric5h5)
		 */
		public static bool ContainsPoint (Vector2[] polyPoints,Vector2 p) { 
		   int j = polyPoints.Length-1; 
		   bool inside = false; 
		   
		   for (int i = 0; i < polyPoints.Length; j = i++) { 
		      if ( ((polyPoints[i].y <= p.y && p.y < polyPoints[j].y) || (polyPoints[j].y <= p.y && p.y < polyPoints[i].y)) && 
		         (p.x < (polyPoints[j].x - polyPoints[i].x) * (p.y - polyPoints[i].y) / (polyPoints[j].y - polyPoints[i].y) + polyPoints[i].x)) 
		         inside = !inside; 
		   } 
		   return inside;
		}
		
		/** Checks if \a p is inside the polygon (XZ space)
		 * \author http://unifycommunity.com/wiki/index.php?title=PolyContainsPoint (Eric5h5)
		 */
		public static bool ContainsPoint (Vector3[] polyPoints,Vector3 p) { 
		   int j = polyPoints.Length-1; 
		   bool inside = false; 
		   
		   for (int i = 0; i < polyPoints.Length; j = i++) { 
		      if ( ((polyPoints[i].z <= p.z && p.z < polyPoints[j].z) || (polyPoints[j].z <= p.z && p.z < polyPoints[i].z)) && 
		         (p.x < (polyPoints[j].x - polyPoints[i].x) * (p.z - polyPoints[i].z) / (polyPoints[j].z - polyPoints[i].z) + polyPoints[i].x)) 
		         inside = !inside; 
		   } 
		   return inside; 
		}
		
		/** Returns if \a p lies on the left side of the line \a a - \a b. Uses XZ space.
		  * Does not return true if the points are colinear. */
		public static bool LeftNotColinear (Vector3 a, Vector3 b, Vector3 p) {
			return (b.x - a.x) * (p.z - a.z) - (p.x - a.x) * (b.z - a.z) < -float.Epsilon;
		}
		
		/** Returns if \a p lies on the left side of the line \a a - \a b. Uses XZ space. Also returns true if the points are colinear */
		public static bool Left (Vector3 a, Vector3 b, Vector3 p) {
			return (b.x - a.x) * (p.z - a.z) - (p.x - a.x) * (b.z - a.z) <= 0;
		}

		/** Returns if \a p lies on the left side of the line \a a - \a b. Also returns true if the points are colinear */
		public static bool Left (Vector2 a, Vector2 b, Vector2 p) {
			return (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y) <= 0;
		}

		/** Returns if \a p lies on the left side of the line \a a - \a b. Uses XZ space. Also returns true if the points are colinear */
		public static bool Left (Int3 a, Int3 b, Int3 c) {
			return (long)(b.x - a.x) * (long)(c.z - a.z) - (long)(c.x - a.x) * (long)(b.z - a.z) <= 0;
		}
		
		/** Returns if \a p lies on the left side of the line \a a - \a b. Uses XZ space. */
		public static bool LeftNotColinear (Int3 a, Int3 b, Int3 c) {
			return (long)(b.x - a.x) * (long)(c.z - a.z) - (long)(c.x - a.x) * (long)(b.z - a.z) < 0;
		}
		
		/** Returns if \a p lies on the left side of the line \a a - \a b. Also returns true if the points are colinear */
		public static bool Left (Int2 a, Int2 b, Int2 c) {
			return (long)(b.x - a.x) * (long)(c.y - a.y) - (long)(c.x - a.x) * (long)(b.y - a.y) <= 0;
		}
		
		/** Returns if the points a in a clockwise order.
		 * Will return true even if the points are colinear or very slightly counter-clockwise
		 * (if the signed area of the triangle formed by the points has an area less than or equals to float.Epsilon) */
		public static bool IsClockwiseMargin (Vector3 a, Vector3 b, Vector3 c) {
			return (b.x-a.x)*(c.z-a.z)-(c.x-a.x)*(b.z-a.z) <= float.Epsilon;
		}
		
		/** Returns if the points a in a clockwise order */
		public static bool IsClockwise (Vector3 a, Vector3 b, Vector3 c) {
			return (b.x-a.x)*(c.z-a.z)-(c.x-a.x)*(b.z-a.z) < 0;
		}
		
		/** Returns if the points a in a clockwise order */
		public static bool IsClockwise (Int3 a, Int3 b, Int3 c) {
			return (long)(b.x - a.x) * (long)(c.z - a.z) - (long)(c.x - a.x) * (long)(b.z - a.z) < 0;
		}
		
		/** Returns true if the points a in a clockwise order or if they are colinear */
		public static bool IsClockwiseMargin (Int3 a, Int3 b, Int3 c) {
			return (long)(b.x - a.x) * (long)(c.z - a.z) - (long)(c.x - a.x) * (long)(b.z - a.z) <= 0;
		}
		
		/** Returns true if the points a in a clockwise order or if they are colinear */
		public static bool IsClockwiseMargin (Int2 a, Int2 b, Int2 c) {
			return (long)(b.x - a.x) * (long)(c.y - a.y) - (long)(c.x - a.x) * (long)(b.y - a.y) <= 0;
		}
		
		/** Returns if the points are colinear (lie on a straight line) */
		public static bool IsColinear (Int3 a, Int3 b, Int3 c) {
			return (long)(b.x - a.x) * (long)(c.z - a.z) - (long)(c.x - a.x) * (long)(b.z - a.z) == 0;
		}
		
		/** Returns if the points are colinear (lie on a straight line) */
		public static bool IsColinearAlmost (Int3 a, Int3 b, Int3 c) {
			long v = (long)(b.x - a.x) * (long)(c.z - a.z) - (long)(c.x - a.x) * (long)(b.z - a.z);
			return v > -1 && v < 1;
		}
		
		/** Returns if the points are colinear (lie on a straight line) */
		public static bool IsColinear (Vector3 a, Vector3 b, Vector3 c) {
			float v = (b.x-a.x)*(c.z-a.z)-(c.x-a.x)*(b.z-a.z);
			//Epsilon not chosen with much though, just that float.Epsilon was a bit too small.
			return v <= 0.0000001f && v >= -0.0000001f;
		}
		
		/** Returns if the line segment \a a2 - \a b2 intersects the infinite line \a a - \a b. a-b is infinite, a2-b2 is not infinite */
		public static bool IntersectsUnclamped (Vector3 a, Vector3 b, Vector3 a2, Vector3 b2) {
			return Left (a,b,a2) != Left (a,b,b2);
		}
		
		/** Returns if the line segment \a a2 - \a b2 intersects the line segment \a a - \a b.
		 * If only the endpoints coincide, the result is undefined (may be true or false).
		 */
		public static bool Intersects (Int2 a, Int2 b, Int2 a2, Int2 b2) {
			return Left (a,b,a2) != Left (a,b,b2) && Left (a2,b2,a) != Left (a2,b2,b);
		}
		
		/** Returns if the line segment \a a2 - \a b2 intersects the line segment \a a - \a b.
		 * If only the endpoints coincide, the result is undefined (may be true or false).
		 * 
		 * \note XZ space
		 */
		public static bool Intersects (Int3 a, Int3 b, Int3 a2, Int3 b2) {
			return Left (a,b,a2) != Left (a,b,b2) && Left (a2,b2,a) != Left (a2,b2,b);
		}
		
		/** Returns if the two line segments intersects. The lines are NOT treated as infinite (just for clarification)
		  * \see IntersectionPoint */
		public static bool Intersects (Vector3 start1, Vector3 end1, Vector3 start2, Vector3 end2) {
			
			Vector3 dir1 = end1-start1;
			Vector3 dir2 = end2-start2;
			
			float den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den == 0) {
				return false;
			}
			
			float nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			float nom2 = dir1.x*(start1.z-start2.z) - dir1.z * (start1.x - start2.x);
			float u = nom/den;
			float u2 = nom2/den;
		
			if (u < 0F || u > 1F || u2 < 0F || u2 > 1F) {
				return false;
			}
			//Debug.DrawLine (start2,end2,Color.magenta);
			//Debug.DrawRay (start1,dir1*5,Color.green);
			return true;
			//Vector2 intersection = start1 + dir1*u;
			//return intersection;
		}
		
		/** Intersection point between two infinite lines.
		 * Lines are treated as infinite. If the lines are parallel 'start1' will be returned. Intersections are calculated on the XZ plane.
		 */
		public static Vector3 IntersectionPointOptimized (Vector3 start1, Vector3 dir1, Vector3 start2, Vector3 dir2) {
			
			float den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den == 0) {
				return start1;
			}
			
			float nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			
			float u = nom/den;
			
			return start1 + dir1*u;
		}
		
		/** Intersection point between two infinite lines.
		 * Lines are treated as infinite. If the lines are parallel 'start1' will be returned. Intersections are calculated on the XZ plane.
		 */
		public static Vector3 IntersectionPointOptimized (Vector3 start1, Vector3 dir1, Vector3 start2, Vector3 dir2, out bool intersects) {
			
			float den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den == 0) {
				intersects = false;
				return start1;
			}
			
			float nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			
			float u = nom/den;
			
			intersects = true;
			return start1 + dir1*u;
		}
		
		/** Returns if the ray (start1, end1) intersects the segment (start2, end2).
		 * false is returned if the lines are parallel.
		 * Only the XZ coordinates are used.
		 * \todo Double check that this actually works
		 */
		public static bool IntersectionFactorRaySegment (Int3 start1, Int3 end1, Int3 start2, Int3 end2) {
			
			Int3 dir1 = end1-start1;
			Int3 dir2 = end2-start2;
			
			//Color rnd = new Color (Random.value,Random.value,Random.value);
			//Debug.DrawRay (start1,dir1,rnd);
			//Debug.DrawRay (start2,dir2,rnd);
			
			long den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den == 0) {
				return false;
			}
			
			long nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			long nom2 = dir1.x*(start1.z-start2.z) - dir1.z * (start1.x - start2.x);
			
			//factor1 < 0
			// If both have the same sign, then nom/den < 0 and thus the segment cuts the ray before the ray starts
			if (!(nom < 0 ^ den < 0)) {
				return false;
			}
			
			//factor2 < 0
			if(!(nom2 < 0 ^ den < 0)) {
				return false;
			}
			
			if ((den >= 0 && nom2 > den) || (den < 0 && nom2 <= den)) {
				return false;
			}
			//factor1 = (float)nom/den;
			//factor2 = (float)nom2/den;
			return true;
			
			//Debug.DrawLine (start2,end2,Color.magenta);
			//Debug.DrawRay (start1,dir1*5,Color.green);
		}
			
		/** Returns the intersection factors for line 1 and line 2. The intersection factors is a distance along the line \a start - \a end where the other line intersects it.\n
		 * \code intersectionPoint = start1 + factor1 * (end1-start1) \endcode
		 * \code intersectionPoint2 = start2 + factor2 * (end2-start2) \endcode
		 * Lines are treated as infinite.\n
		 * false is returned if the lines are parallel and true if they are not.
		 * Only the XZ coordinates are used.
		 */
		public static bool IntersectionFactor (Int3 start1, Int3 end1, Int3 start2, Int3 end2, out float factor1, out float factor2) {
			
			Int3 dir1 = end1-start1;
			Int3 dir2 = end2-start2;
			
			//Color rnd = new Color (Random.value,Random.value,Random.value);
			//Debug.DrawRay (start1,dir1,rnd);
			//Debug.DrawRay (start2,dir2,rnd);
			
			long den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den == 0) {
				factor1 = 0;
				factor2 = 0;
				return false;
			}
			
			long nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			long nom2 = dir1.x*(start1.z-start2.z) - dir1.z * (start1.x - start2.x);
			
			factor1 = (float)nom/den;
			factor2 = (float)nom2/den;
			
			//Debug.DrawLine (start2,end2,Color.magenta);
			//Debug.DrawRay (start1,dir1*5,Color.green);
			return true;
		}
		
		/** Returns the intersection factors for line 1 and line 2. The intersection factors is a distance along the line \a start - \a end where the other line intersects it.\n
		 * \code intersectionPoint = start1 + factor1 * (end1-start1) \endcode
		 * \code intersectionPoint2 = start2 + factor2 * (end2-start2) \endcode
		 * Lines are treated as infinite.\n
		 * false is returned if the lines are parallel and true if they are not.
		 * Only the XZ coordinates are used.
		 */
		public static bool IntersectionFactor (Vector3 start1, Vector3 end1, Vector3 start2, Vector3 end2, out float factor1, out float factor2) {
			
			Vector3 dir1 = end1-start1;
			Vector3 dir2 = end2-start2;
			
			//Color rnd = new Color (Random.value,Random.value,Random.value);
			//Debug.DrawRay (start1,dir1,rnd);
			//Debug.DrawRay (start2,dir2,rnd);
			
			float den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den <= 0.00001f && den >= -0.00001f) {
				factor1 = 0;
				factor2 = 0;
				return false;
			}
			
			float nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			float nom2 = dir1.x*(start1.z-start2.z) - dir1.z * (start1.x - start2.x);
			
			float u = nom/den;
			float u2 = nom2/den;
			
			factor1 = u;
			factor2 = u2;
			
			//Debug.DrawLine (start2,end2,Color.magenta);
			//Debug.DrawRay (start1,dir1*5,Color.green);
			return true;
		}

		/** Returns the intersection factor for line 1 with ray 2.
		 * The intersection factors is a factor distance along the line \a start - \a end where the other line intersects it.\n
		 * \code intersectionPoint = start1 + factor * (end1-start1) \endcode
		 * Lines are treated as infinite.\n
		 * 
		 * The second "line" is treated as a ray, meaning only matches on start2 or forwards towards end2 (and beyond) will be returned
		 * If the point lies on the wrong side of the ray start, Nan will be returned.
		 * 
		 * NaN is returned if the lines are parallel. */
		public static float IntersectionFactorRay (Int3 start1, Int3 end1, Int3 start2, Int3 end2) {
			
			Int3 dir1 = end1-start1;
			Int3 dir2 = end2-start2;
			
			int den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den == 0) {
				return float.NaN;
			}
			
			int nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			int nom2 = dir1.x*(start1.z-start2.z) - dir1.z * (start1.x - start2.x);
			
			//if ((nom2 < 0) ^ (den < 0) && nom2 != 0) return float.NaN;
			if ((float)nom2/den < 0) {
				return float.NaN;
			}
			return (float)nom/den;
		}
		
		/** Returns the intersection factor for line 1 with line 2.
		 * The intersection factor is a distance along the line \a start1 - \a end1 where the line \a start2 - \a end2 intersects it.\n
		 * \code intersectionPoint = start1 + intersectionFactor * (end1-start1) \endcode.
		 * Lines are treated as infinite.\n
		 * -1 is returned if the lines are parallel (note that this is a valid return value if they are not parallel too) */
		public static float IntersectionFactor (Vector3 start1, Vector3 end1, Vector3 start2, Vector3 end2) {
			
			Vector3 dir1 = end1-start1;
			Vector3 dir2 = end2-start2;
			
			//Color rnd = new Color (Random.value,Random.value,Random.value);
			//Debug.DrawRay (start1,dir1,rnd);
			//Debug.DrawRay (start2,dir2,rnd);
			
			float den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den == 0) {
				return -1;
			}
			
			float nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			
			float u = nom/den;
			
			//Debug.DrawLine (start2,end2,Color.magenta);
			//Debug.DrawRay (start1,dir1*5,Color.green);
			return u;
		}
		
		/** Returns the intersection point between the two lines. Lines are treated as infinite. \a start1 is returned if the lines are parallel */
		public static Vector3 IntersectionPoint (Vector3 start1, Vector3 end1, Vector3 start2, Vector3 end2) {
			bool s;
			return IntersectionPoint (start1,end1,start2,end2, out s);
		}
		
		/** Returns the intersection point between the two lines. Lines are treated as infinite. \a start1 is returned if the lines are parallel */
		public static Vector3 IntersectionPoint (Vector3 start1, Vector3 end1, Vector3 start2, Vector3 end2, out bool intersects) {
			
			Vector3 dir1 = end1-start1;
			Vector3 dir2 = end2-start2;
			
			//Color rnd = new Color (Random.value,Random.value,Random.value);
			//Debug.DrawRay (start1,dir1,rnd);
			//Debug.DrawRay (start2,dir2,rnd);
			
			float den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den == 0) {
				intersects = false;
				return start1;
			}
			
			float nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			
			float u = nom/den;
			
			//Debug.DrawLine (start2,end2,Color.magenta);
			//Debug.DrawRay (start1,dir1*5,Color.green);
			intersects = true;
			return start1 + dir1*u;
		}
		
		/** Returns the intersection point between the two lines. Lines are treated as infinite. \a start1 is returned if the lines are parallel */
		public static Vector2 IntersectionPoint (Vector2 start1, Vector2 end1, Vector2 start2, Vector2 end2) {
			bool s;
			return IntersectionPoint (start1,end1,start2,end2, out s);
		}
		
		/** Returns the intersection point between the two lines. Lines are treated as infinite. \a start1 is returned if the lines are parallel */
		public static Vector2 IntersectionPoint (Vector2 start1, Vector2 end1, Vector2 start2, Vector2 end2, out bool intersects) {
			
			Vector2 dir1 = end1-start1;
			Vector2 dir2 = end2-start2;
			
			//Color rnd = new Color (Random.value,Random.value,Random.value);
			//Debug.DrawRay (start1,dir1,rnd);
			//Debug.DrawRay (start2,dir2,rnd);
			
			float den = dir2.y*dir1.x - dir2.x * dir1.y;
			
			if (den == 0) {
				intersects = false;
				return start1;
			}
			
			float nom = dir2.x*(start1.y-start2.y)- dir2.y*(start1.x-start2.x);
			
			float u = nom/den;
			
			//Debug.DrawLine (start2,end2,Color.magenta);
			//Debug.DrawRay (start1,dir1*5,Color.green);
			intersects = true;
			return start1 + dir1*u;
		}
		
		/** Returns the intersection point between the two line segments.
		 * Lines are NOT treated as infinite. \a start1 is returned if the line segments do not intersect */
		public static Vector3 SegmentIntersectionPoint (Vector3 start1, Vector3 end1, Vector3 start2, Vector3 end2, out bool intersects) {
			
			Vector3 dir1 = end1-start1;
			Vector3 dir2 = end2-start2;
			
			//Color rnd = new Color (Random.value,Random.value,Random.value);
			//Debug.DrawRay (start1,dir1,rnd);
			//Debug.DrawRay (start2,dir2,rnd);
			
			float den = dir2.z*dir1.x - dir2.x * dir1.z;
			
			if (den == 0) {
				intersects = false;
				return start1;
			}
			
			float nom = dir2.x*(start1.z-start2.z)- dir2.z*(start1.x-start2.x);
			float nom2 = dir1.x*(start1.z-start2.z) - dir1.z * (start1.x - start2.x);
			float u = nom/den;
			float u2 = nom2/den;
		
			if (u < 0F || u > 1F || u2 < 0F || u2 > 1F) {
				intersects = false;
				return start1;
			}
		
			//Debug.Log ("U1 "+u.ToString ("0.00")+" U2 "+u2.ToString ("0.00")+"\nP1: "+(start1 + dir1*u)+"\nP2: "+(start2 + dir2*u2)+"\nStart1: "+start1+"  End1: "+end1);
			//Debug.DrawLine (start2,end2,Color.magenta);
			//Debug.DrawRay (start1,dir1*5,Color.green);
			intersects = true;
			return start1 + dir1*u;
		}
		
		public static List<Vector3> hullCache = new List<Vector3>();
		
		/** Calculates convex hull in XZ space for the points.
		  * Implemented using the very simple Gift Wrapping Algorithm
		  * which has a complexity of O(nh) where \a n is the number of points and \a h is the number of points on the hull,
		  * so it is in the worst case quadratic.
		  */
		public static Vector3[] ConvexHull (Vector3[] points) {
			
			if (points.Length == 0) return new Vector3[0]; 
			
			lock (hullCache) {
				List<Vector3> hull = hullCache;
				hull.Clear ();
				
				
				int pointOnHull = 0;
				for (int i=1;i<points.Length;i++) if (points[i].x < points[pointOnHull].x) pointOnHull = i;
				
				int startpoint = pointOnHull;
				int counter = 0;
				
				do {
					hull.Add (points[pointOnHull]);
					int endpoint = 0;
					for (int i=0;i<points.Length;i++) if (endpoint == pointOnHull || !Left (points[pointOnHull],points[endpoint],points[i])) endpoint = i;
					
					pointOnHull = endpoint;
					
					counter++;
					if (counter > 10000) {
						Debug.LogWarning ("Infinite Loop in Convex Hull Calculation");
						break;
					}
				} while (pointOnHull != startpoint);
				
				return hull.ToArray ();
			}
		}
		
		/** Does the line segment intersect the bounding box.
		 * The line is NOT treated as infinite.
		 * \author Slightly modified code from http://www.3dkingdoms.com/weekly/weekly.php?a=21
		 */
		public static bool LineIntersectsBounds (Bounds bounds, Vector3 a, Vector3 b) {	
			// Put line in box space
			//CMatrix MInv = m_M.InvertSimple();
			//CVec3 LB1 = MInv * L1;
			//CVec3 LB2 = MInv * L2;
			a -= bounds.center;
			b -= bounds.center;
			
			// Get line midpoint and extent
			Vector3 LMid = (a + b) * 0.5F; 
			Vector3 L = (a - LMid);
			Vector3 LExt = new Vector3 ( Math.Abs(L.x), Math.Abs(L.y), Math.Abs(L.z) );
			
			Vector3 extent = bounds.extents;
			
			// Use Separating Axis Test
			// Separation vector from box center to line center is LMid, since the line is in box space
			if ( Math.Abs( LMid.x ) > extent.x + LExt.x ) return false;
			if ( Math.Abs( LMid.y ) > extent.y + LExt.y ) return false;
			if ( Math.Abs( LMid.z ) > extent.z + LExt.z ) return false;
			// Crossproducts of line and each axis
			if ( Math.Abs( LMid.y * L.z - LMid.z * L.y)  >  (extent.y * LExt.z + extent.z * LExt.y) ) return false;
			if ( Math.Abs( LMid.x * L.z - LMid.z * L.x)  >  (extent.x * LExt.z + extent.z * LExt.x) ) return false;
			if ( Math.Abs( LMid.x * L.y - LMid.y * L.x)  >  (extent.x * LExt.y + extent.y * LExt.x) ) return false;
			// No separating axis, the line intersects
			return true;
		}
		
	/*
		public static Vector3[] WeightedSubdivide (Vector3[] path, int subdivisions) {
			
			Vector3[] path2 = new Vector3[(path.Length-1)*(int)Mathf.Pow (2,subdivisions)+1];
			
			int c = 0;
			for (int p=0;p<path.Length-1;p++) {
				float step = 1.0F/Mathf.Pow (2,subdivisions);
				
				for (float i=0;i<1.0F;i+=step) {
					path2[c] = Vector3.Lerp (path[p],path[p+1],Mathfx.Hermite (0F,1F,i));//Mathf.SmoothStep (0,1, i));
					c++;
				}
			}
			
			//Debug.Log (path2.Length +" "+c);
			
			path2[c] = path[path.Length-1];
			return path2;
		}*/
	
		/** Subdivides \a path and returns the new array with interpolated values.
		 * The returned array is \a path subdivided \a subdivisions times, the resulting points are interpolated using Mathf.SmoothStep.\n
		 * If \a subdivisions is less or equal to 0 (zero), the original array will be returned */
		public static Vector3[] Subdivide (Vector3[] path, int subdivisions) {
			
			subdivisions = subdivisions < 0 ? 0 : subdivisions;
			
			if (subdivisions == 0) {
				return path;
			}
			
			Vector3[] path2 = new Vector3[(path.Length-1)*(int)Mathf.Pow (2,subdivisions)+1];
			
			int c = 0;
			for (int p=0;p<path.Length-1;p++) {
				float step = 1.0F/Mathf.Pow (2,subdivisions);
				
				for (float i=0;i<1.0F;i+=step) {
					path2[c] = Vector3.Lerp (path[p],path[p+1],Mathf.SmoothStep (0,1, i));
					c++;
				}
			}
			
			//Debug.Log (path2.Length +" "+c);
			
			path2[c] = path[path.Length-1];
			return path2;
		}
		
		/** Returns the closest point on the triangle. The \a triangle array must have a length of at least 3.
		 * \see ClosesPointOnTriangle(Vector3,Vector3,Vector3,Vector3);
		 */
		public static Vector3 ClosestPointOnTriangle ( Vector3[] triangle, Vector3 point ) {
			return ClosestPointOnTriangle (triangle[0],triangle[1],triangle[2],point);
		}
		
		/** Returns the closest point on the triangle.
		 * 
		 * \author Got code from the internet, changed a bit to work with the Unity API
		 * 
		 */
		public static Vector3 ClosestPointOnTriangle(Vector3 tr0, Vector3 tr1, Vector3 tr2, Vector3 point)
        {
            Vector3 diff = tr0 - point;
            Vector3 edge0 = tr1 - tr0;
            Vector3 edge1 = tr2 - tr0;
            float a00 = edge0.sqrMagnitude;
            float a01 = Vector3.Dot(edge0, edge1);
            float a11 = edge1.sqrMagnitude;
            float b0 = Vector3.Dot(diff, edge0);
            float b1 = Vector3.Dot(diff, edge1);
            //float c = diff.sqrMagnitude;
            float det = a00 * a11 - a01 * a01;

            float s = a01 * b1 - a11 * b0;
            float t = a01 * b0 - a00 * b1;
            //float sqrDistance;

            if (s + t <= det)
            {
                if (s < 0f)
                {
                    if (t < 0f)  // region 4
                    {
                        if (b0 < 0f)
                        {
                            t = 0f;
                            if (-b0 >= a00)
                            {
                                s = 1f;
                                //sqrDistance = a00 + (2f) * b0 + c;
                            }
                            else
                            {
                                s = -b0 / a00;
                                //sqrDistance = b0 * s + c;
                            }
                        }
                        else
                        {
                            s = 0f;
                            if (b1 >= 0f)
                            {
                                t = 0f;
                                //sqrDistance = c;
                            }
                            else if (-b1 >= a11)
                            {
                                t = 1f;
                                //sqrDistance = a11 + (2f) * b1 + c;
                            }
                            else
                            {
                                t = -b1 / a11;
                                //sqrDistance = b1 * t + c;
                            }
                        }
                    }
                    else  // region 3
                    {
                        s = 0f;
                        if (b1 >= 0f)
                        {
                            t = 0f;
                            //sqrDistance = c;
                        }
                        else if (-b1 >= a11)
                        {
                            t = 1f;
                            //sqrDistance = a11 + (2f) * b1 + c;
                        }
                        else
                        {
                            t = -b1 / a11;
                            // sqrDistance = b1 * t + c;
                        }
                    }
                }
                else if (t < 0f)  // region 5
                {
                    t = 0f;
                    if (b0 >= 0f)
                    {
                        s = 0f;
                        // sqrDistance = c;
                    }
                    else if (-b0 >= a00)
                    {
                        s = 1f;
                        //sqrDistance = a00 + (2f) * b0 + c;
                    }
                    else
                    {
                        s = -b0 / a00;
                        //  sqrDistance = b0 * s + c;
                    }
                }
                else  // region 0
                {
                    // minimum at interior point
                    float invDet = 1f / det;
                    s *= invDet;
                    t *= invDet;
                    // sqrDistance = s * (a00 * s + a01 * t + (2f) * b0) + t * (a01 * s + a11 * t + (2f) * b1) + c;
                }
            }
            else
            {
                float tmp0, tmp1, numer, denom;

                if (s < 0f)  // region 2
                {
                    tmp0 = a01 + b0;
                    tmp1 = a11 + b1;
                    if (tmp1 > tmp0)
                    {
                        numer = tmp1 - tmp0;
                        denom = a00 - (2f) * a01 + a11;
                        if (numer >= denom)
                        {
                            s = 1f;
                            t = 0f;
                            // sqrDistance = a00 + (2f) * b0 + c;
                        }
                        else
                        {
                            s = numer / denom;
                            t = 1f - s;
                            // sqrDistance = s * (a00 * s + a01 * t + (2f) * b0) + t * (a01 * s + a11 * t + (2f) * b1) + c;
                        }
                    }
                    else
                    {
                        s = 0f;
                        if (tmp1 <= 0f)
                        {
                            t = 1f;
                            // sqrDistance = a11 + (2f) * b1 + c;
                        }
                        else if (b1 >= 0f)
                        {
                            t = 0f;
                            //  sqrDistance = c;
                        }
                        else
                        {
                            t = -b1 / a11;
                            //  sqrDistance = b1 * t + c;
                        }
                    }
                }
                else if (t < 0f)  // region 6
                {
                    tmp0 = a01 + b1;
                    tmp1 = a00 + b0;
                    if (tmp1 > tmp0)
                    {
                        numer = tmp1 - tmp0;
                        denom = a00 - (2f) * a01 + a11;
                        if (numer >= denom)
                        {
                            t = 1f;
                            s = 0f;
                            // sqrDistance = a11 + (2f) * b1 + c;
                        }
                        else
                        {
                            t = numer / denom;
                            s = 1f - t;
                            // sqrDistance = s * (a00 * s + a01 * t + (2f) * b0) + t * (a01 * s + a11 * t + (2f) * b1) + c;
                        }
                    }
                    else
                    {
                        t = 0f;
                        if (tmp1 <= 0f)
                        {
                            s = 1f;
                            //sqrDistance = a00 + (2f) * b0 + c;
                        }
                        else if (b0 >= 0f)
                        {
                            s = 0f;
                            // sqrDistance = c;
                        }
                        else
                        {
                            s = -b0 / a00;
                            //  sqrDistance = b0 * s + c;
                        }
                    }
                }
                else  // region 1
                {
                    numer = a11 + b1 - a01 - b0;
                    if (numer <= 0f)
                    {
                        s = 0f;
                        t = 1f;
                        //  sqrDistance = a11 + (2f) * b1 + c;
                    }
                    else
                    {
                        denom = a00 - (2f) * a01 + a11;
                        if (numer >= denom)
                        {
                            s = 1f;
                            t = 0f;
                            //  sqrDistance = a00 + (2f) * b0 + c;
                        }
                        else
                        {
                            s = numer / denom;
                            t = 1f - s;
                            // sqrDistance = s * (a00 * s + a01 * t + (2f) * b0) + t * (a01 * s + a11 * t + (2f) * b1) + c;
                        }
                    }
                }
            }

            // Account for numerical round-off error.
            //  if (sqrDistance < 0f)
            //  {
            //       sqrDistance = 0f;
            //    }

            return tr0 + s * edge0 + t * edge1;
        }
		
		/* Returns the closest point on the triangle.
		 * 
		 * \author Got code from the internet, changed a bit to work with the Unity API
		 * 
		 * \todo Uses Dot product to get the sqrMagnitude of a vector, should change to sqrMagnitude for readability and possibly for speed (unlikely though)
		 *
		public static Vector3 ClosestPointOnTriangle (Vector3 tr0, Vector3 tr1, Vector3 tr2, Vector3 point ) {
		    Vector3 edge0 = tr1 - tr0;
		    Vector3 edge1 = tr2 - tr0;
		    Vector3 v0 = tr0 - point;
		
		    float a = Vector3.Dot (edge0,edge0);//edge0.dot( edge0 ); //Equals to sqrMagnitude
		    float b = Vector3.Dot (edge0, edge1 );
		    float c = Vector3.Dot (edge1, edge1 ); //Equals to sqrMagnitude
		    float d = Vector3.Dot (edge0, v0 );
		    float e = Vector3.Dot (edge1, v0 );
		
		    float det = a*c - b*b;
		    float s = b*e - c*d;
		    float t = b*d - a*e;
		
		    if ( s + t < det )
		    {
		        if ( s < 0.0F )
		        {
		            if ( t < 0.0F )
		            {
		                if ( d < 0.0F )
		                {
		                    s = Mathfx.Clamp01 ( -d/a);
		                    t = 0.0F;
		                }
		                else
		                {
		                    s = 0.0F;
		                    t = Mathfx.Clamp01 ( -e/c);
		                }
		            }
		            else
		            {
		                s = 0.0F;
		                t = Mathfx.Clamp01 ( -e/c);
		            }
		        }
		        else if ( t < 0.0F )
		        {
		            s = Mathfx.Clamp01 ( -d/a);
		            t = 0.0F;
		        }
		        else
		        {
		            float invDet = 1.0F / det;
		            s *= invDet;
		            t *= invDet;
		        }
		    }
		    else
		    {
		        if ( s < 0.0F )
		        {
		            float tmp0 = b+d;
		            float tmp1 = c+e;
		            if ( tmp1 > tmp0 )
		            {
		                float numer = tmp1 - tmp0;
		                float denom = a-2*b+c;
		                s = Mathfx.Clamp01 ( numer/denom);
		                t = 1-s;
		            }
		            else
		            {
		                t = Mathfx.Clamp01 ( -e/c);
		                s = 0.0F;
		            }
		        }
		        else if ( t < 0.0F )
		        {
		            if ( a+d > b+e )
		            {
		                float numer = c+e-b-d;
		                float denom = a-2*b+c;
		                s = Mathfx.Clamp01 ( numer/denom);
		                t = 1-s;
		            }
		            else
		            {
		                s = Mathfx.Clamp01 ( -e/c);
		                t = 0.0F;
		            }
		        }
		        else
		        {
		            float numer = c+e-b-d;
		            float denom = a-2*b+c;
		            s = Mathfx.Clamp01 ( numer/denom);
		            t = 1.0F - s;
		        }
		    }
			
			return tr0 + s * edge0 + t * edge1;
		}*/
		
		/** Get the 3D minimum distance between 2 segments
	    * Input:  two 3D line segments S1 and S2
	    * \returns the shortest squared distance between S1 and S2
	    */
		public static float DistanceSegmentSegment3D (Vector3 s1, Vector3 e1, Vector3 s2, Vector3 e2) {
		    Vector3   u = e1 - s1;
		    Vector3   v = e2 - s2;
		    Vector3   w = s1 - s2;
		    float    a = Vector3.Dot(u,u);         // always >= 0
		    float    b = Vector3.Dot(u,v);
		    float    c = Vector3.Dot(v,v);         // always >= 0
		    float    d = Vector3.Dot(u,w);
		    float    e = Vector3.Dot(v,w);
		    float    D = a*c - b*b;        // always >= 0
		    float    sc, sN, sD = D;       // sc = sN / sD, default sD = D >= 0
		    float    tc, tN, tD = D;       // tc = tN / tD, default tD = D >= 0
		
		    // compute the line parameters of the two closest points
		    if (D < 0.000001f) { // the lines are almost parallel
		        sN = 0.0f;         // force using point P0 on segment S1
		        sD = 1.0f;         // to prevent possible division by 0.0 later
		        tN = e;
		        tD = c;
		    }
		    else {                 // get the closest points on the infinite lines
		        sN = (b*e - c*d);
		        tN = (a*e - b*d);
		        if (sN < 0.0f) {        // sc < 0 => the s=0 edge is visible
		            sN = 0.0f;
		            tN = e;
		            tD = c;
		        }
		        else if (sN > sD) {  // sc > 1  => the s=1 edge is visible
		            sN = sD;
		            tN = e + b;
		            tD = c;
		        }
		    }
		
		    if (tN < 0.0f) {            // tc < 0 => the t=0 edge is visible
		        tN = 0.0f;
		        // recompute sc for this edge
		        if (-d < 0.0f)
		            sN = 0.0f;
		        else if (-d > a)
		            sN = sD;
		        else {
		            sN = -d;
		            sD = a;
		        }
		    }
		    else if (tN > tD) {      // tc > 1  => the t=1 edge is visible
		        tN = tD;
		        // recompute sc for this edge
		        if ((-d + b) < 0.0f)
		            sN = 0;
		        else if ((-d + b) > a)
		            sN = sD;
		        else {
		            sN = (-d +  b);
		            sD = a;
		        }
		    }
		    // finally do the division to get sc and tc
		    sc = (System.Math.Abs(sN) < 0.000001f ? 0.0f : sN / sD);
		    tc = (System.Math.Abs(tN) < 0.000001f ? 0.0f : tN / tD);
		
		    // get the difference of the two closest points
		    Vector3 dP = w + (sc * u) - (tc * v);  // =  S1(sc) - S2(tc)
		
		    return dP.sqrMagnitude;   // return the closest distance
		}
		
	}
}
