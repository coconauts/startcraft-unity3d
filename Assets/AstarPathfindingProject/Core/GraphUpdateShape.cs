using UnityEngine;
using System.Collections;
using Pathfinding;

namespace Pathfinding {
	
	/** Defines a shape for a Pathfinding.GraphUpdateObject.
	 * The shape consists of a number of points which it can either calculate the convex hull of (XZ space) or use as a polygon directly.
	 * \see Pathfinding.GraphUpdateObject.shape
	 */
	public class GraphUpdateShape  {
	
		Vector3[] _points;
		Vector3[] _convexPoints;
		bool _convex;
		
		/** Gets or sets the points of the polygon in the shape.
		 * Will automatically calculate the convex hull if #convex is set to true */
		public Vector3[] points {
			get {
				return _points;
			}
			set {
				_points = value;
				if (convex) CalculateConvexHull ();
			}
		}
		
		/** Sets if the convex hull of the points should be calculated.
		 * Convex hulls are faster but non-convex hulls can be used to specify the shape more exactly
		 */
		public bool convex {
			get {
				return _convex;
			}
			set {
				if (_convex	!= value && value) {
					_convex = value;
					CalculateConvexHull ();
				} else {
					_convex = value;
				}
			}
		}
		
		private void CalculateConvexHull () {
			if (points == null) { _convexPoints = null; return; }
			
			_convexPoints = Polygon.ConvexHull (points);
			for (int i=0;i<_convexPoints.Length;i++) {
				Debug.DrawLine (_convexPoints[i],_convexPoints[(i+1) % _convexPoints.Length],Color.green);
			}
		}
		
		public Bounds GetBounds () {
			if (points == null || points.Length == 0) return new Bounds();
			Vector3 min = points[0];
			Vector3 max = points[0];
			for (int i=0;i<points.Length;i++) {
				min = Vector3.Min (min,points[i]);
				max = Vector3.Max (max,points[i]);
			}
			return new Bounds ((min+max)*0.5F,max-min);
		}
		
		public bool Contains (GraphNode node) {
			
			Vector3 point = (Vector3)node.position;
			
			//Debug.DrawRay (node.position,-Vector3.up*2,Color.magenta);
			
			if (convex) {
				if (_convexPoints == null) return false;
				
				for (int i=0,j=_convexPoints.Length-1;i<_convexPoints.Length;j=i,i++) {
					if (Polygon.Left (_convexPoints[i],_convexPoints[j],point)) return false;
				}
			} else {
				if (_points	== null) return false;
				
				return Polygon.ContainsPoint (_points,point);
			}
			
			//Debug.DrawRay (node.position,Vector3.up*2,Color.blue);
			
			return true;
		}
		
		public bool Contains (Vector3 point) {
			if (convex) {
				if (_convexPoints == null) return false;
				
				for (int i=0,j=_convexPoints.Length-1;i<_convexPoints.Length;j=i,i++) {
					if (Polygon.Left (_convexPoints[i],_convexPoints[j],point)) return false;
				}
			} else {
				if (_points	== null) return false;
				
				return Polygon.ContainsPoint (_points,point);
			}
			
			return true;
		}
	}
}