using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pathfinding {
	public class NodeLink3Node : PointNode {
		
		public NodeLink3 link;
		public Vector3 portalA;
		public Vector3 portalB;
		
		public NodeLink3Node ( AstarPath active ) : base ( active ) {}
		
		public override bool GetPortal (GraphNode other, List<Vector3> left, List<Vector3> right, bool backwards)
		{
			
			if ( this.connections.Length < 2 ) return false;
			
			if ( this.connections.Length != 2 ) throw new System.Exception ("Invalid NodeLink3Node. Expected 2 connections, found " + this.connections.Length);
			
			//if ( other != connections[0] || other != connections[1] ) return false;
			
			if ( left != null ) {
				//Debug.DrawLine ( portalA, portalB, Color.red);
				left.Add ( portalA );
				right.Add ( portalB );
				/*
				Vector3 normal = link.transform.forward;
				Vector3 tangent = Vector3.Dot (normal, (Vector3)(other.Position - this.Position) ) > 0 ? link.transform.right*0.5f : -link.transform.right*0.5f;
				
				Debug.DrawLine ( link.transform.position -tangent * link.portalWidth, link.transform.position +tangent * link.portalWidth, Color.red);
				
				Debug.DrawRay ( link.transform.position -tangent * link.portalWidth, Vector3.up*5, Color.red);
				Debug.Break ();
				left.Add ( link.transform.position -tangent * link.portalWidth );
				right.Add (link.transform.position +tangent * link.portalWidth );*/
			}
			
			return true;
		}
		
		public GraphNode GetOther ( GraphNode a ) {
			
			if ( this.connections.Length < 2 ) return null;
			if ( this.connections.Length != 2 ) throw new System.Exception ("Invalid NodeLink3Node. Expected 2 connections, found " + this.connections.Length);
			
			return a == connections[0] ? (connections[1] as NodeLink3Node).GetOtherInternal(this) : (connections[0] as NodeLink3Node).GetOtherInternal(this);
		}
		
		GraphNode GetOtherInternal ( GraphNode a ) {
			if ( this.connections.Length < 2 ) return null;
			return a == connections[0] ? connections[1] : connections[0];
		}
	}
	
	[AddComponentMenu("Pathfinding/Link3")]
	public class NodeLink3 : GraphModifier {
		
		protected static Dictionary<GraphNode,NodeLink3> reference = new Dictionary<GraphNode,NodeLink3>();
		public static NodeLink3 GetNodeLink (GraphNode node) {
			NodeLink3 v;
			reference.TryGetValue (node, out v);
			return v;
		}
		
		/** End position of the link */
		public Transform end;
		
		/** The connection will be this times harder/slower to traverse.
		 * Note that values lower than one will not always make the pathfinder choose this path instead of another path even though this one should
		  * lead to a lower total cost unless you also adjust the Heuristic Scale in A* Inspector -> Settings -> Pathfinding or disable the heuristic altogether.
		  */
		public float costFactor = 1.0f;
		
		/** Make a one-way connection */
		public bool oneWay = false;
	
		/* Delete existing connection instead of adding one */
		//public bool deleteConnection = false;
		
		//private bool createHiddenNodes = true;
		
		public Transform StartTransform {
			get { return transform; }
		}
		
		public Transform EndTransform {
			get { return end; }
		}
		
		NodeLink3Node startNode;
		NodeLink3Node endNode;
		MeshNode connectedNode1, connectedNode2;
		Vector3 clamped1, clamped2;
		bool postScanCalled = false;
		
		public GraphNode StartNode {
			get { return startNode; }
		}
		
		public GraphNode EndNode {
			get { return endNode; }
		}
		
		public override void OnPostScan () {
			
			if (AstarPath.active.isScanning) {
				InternalOnPostScan ();
			} else {
				AstarPath.active.AddWorkItem (new AstarPath.AstarWorkItem (delegate (bool force) {
					InternalOnPostScan ();
					return true;
				}));
			}
		}
		
		public void InternalOnPostScan () {
			
			if ( AstarPath.active.astarData.pointGraph == null ) {
				AstarPath.active.astarData.AddGraph ( new PointGraph () );
			}
			
			//Get nearest nodes from the first point graph, assuming both start and end transforms are nodes
			startNode = AstarPath.active.astarData.pointGraph.AddNode ( new NodeLink3Node(AstarPath.active), (Int3)StartTransform.position );//AstarPath.active.astarData.pointGraph.GetNearest(StartTransform.position).node as PointNode;
			startNode.link = this;
			endNode = AstarPath.active.astarData.pointGraph.AddNode ( new NodeLink3Node(AstarPath.active), (Int3)EndTransform.position ); //AstarPath.active.astarData.pointGraph.GetNearest(EndTransform.position).node as PointNode;
			endNode.link = this;
			connectedNode1 = null;
			connectedNode2 = null;
			
			if (startNode == null || endNode == null) {
				startNode = null;
				endNode = null;
				return;
			}
			
			postScanCalled = true;
			reference[startNode] = this;
			reference[endNode] = this;
			Apply( true );
		}
			
		public override void OnGraphsPostUpdate () {
			//if (connectedNode1 != null && connectedNode2 != null) {
			if (!AstarPath.active.isScanning) {
				
				if (connectedNode1 != null && connectedNode1.Destroyed) {
					connectedNode1 = null;
				}
				if (connectedNode2 != null && connectedNode2.Destroyed) {
					connectedNode2 = null;
				}
				
				if (!postScanCalled) {
					OnPostScan();
				} else {
					//OnPostScan will also call this method
					Apply( false );
				}
			}
		}
		
		protected override void OnEnable () {
			base.OnEnable();
			
			if (AstarPath.active != null && AstarPath.active.astarData != null && AstarPath.active.astarData.pointGraph != null) {
				OnGraphsPostUpdate ();
			}
		}
		
		protected override void OnDisable () {
			base.OnDisable();
			
			postScanCalled = false;
			
			if (startNode != null) reference.Remove(startNode);
			if (endNode != null) reference.Remove(endNode);
			
			if (startNode != null && endNode != null) {
				startNode.RemoveConnection (endNode);
				endNode.RemoveConnection (startNode);
				
				if (connectedNode1 != null && connectedNode2 != null) {
					startNode.RemoveConnection (connectedNode1);
					connectedNode1.RemoveConnection (startNode);
					
					endNode.RemoveConnection (connectedNode2);
					connectedNode2.RemoveConnection (endNode);
				}
			}
		}
		
		void RemoveConnections (GraphNode node) {
			//TODO, might be better to replace connection
			node.ClearConnections (true);
		}
		
		[ContextMenu ("Recalculate neighbours")]
		void ContextApplyForce () {
			if (Application.isPlaying) {
				Apply ( true );
				if ( AstarPath.active != null ) {
					AstarPath.active.FloodFill ();
				}
			}
		}
		
		public void Apply ( bool forceNewCheck ) {
			//TODO
			//This function assumes that connections from the n1,n2 nodes never need to be removed in the future (e.g because the nodes move or something)
			NNConstraint nn = NNConstraint.None;
			nn.distanceXZ = true;
			int graph = (int)startNode.GraphIndex;
			
			//Search all graphs but the one which start and end nodes are on
			nn.graphMask = ~(1 << graph);
			
			bool same = true;
			
			if (true) {
				NNInfo n1 = AstarPath.active.GetNearest(StartTransform.position, nn);
				same &= n1.node == connectedNode1 && n1.node != null;
				connectedNode1 = n1.node as MeshNode;
				clamped1 = n1.clampedPosition;
				if ( connectedNode1 != null ) Debug.DrawRay ( (Vector3)connectedNode1.position, Vector3.up*5,Color.red);
			}
			
			if (true) {
				NNInfo n2 = AstarPath.active.GetNearest(EndTransform.position, nn);
				same &= n2.node == connectedNode2 && n2.node != null;
				connectedNode2 = n2.node as MeshNode;
				clamped2 = n2.clampedPosition;
				if ( connectedNode2 != null ) Debug.DrawRay ( (Vector3)connectedNode2.position, Vector3.up*5,Color.cyan);
			}
			
			if (connectedNode2 == null || connectedNode1 == null) return;
			
			startNode.SetPosition ( (Int3)StartTransform.position );
			endNode.SetPosition ( (Int3)EndTransform.position );
			
			if ( same && !forceNewCheck ) return;
			
			RemoveConnections(startNode);
			RemoveConnections(endNode);
			
			uint cost = (uint)Mathf.RoundToInt(((Int3)(StartTransform.position-EndTransform.position)).costMagnitude*costFactor);
			startNode.AddConnection (endNode, cost);
			endNode.AddConnection(startNode, cost);
			
			Int3 dir = connectedNode2.position - connectedNode1.position;
			
			for ( int a=0;a<connectedNode1.GetVertexCount();a++) {
				Int3 va1 = connectedNode1.GetVertex ( a );
				Int3 va2 = connectedNode1.GetVertex ( (a+1) % connectedNode1.GetVertexCount() );
				
				if ( Int3.DotLong ( (va2-va1).Normal2D (), dir ) > 0 ) continue;
				
				for ( int b=0;b<connectedNode2.GetVertexCount();b++) {
					Int3 vb1 = connectedNode2.GetVertex ( b );
					Int3 vb2 = connectedNode2.GetVertex ( (b+1) % connectedNode2.GetVertexCount() );
					
					if ( Int3.DotLong ( (vb2-vb1).Normal2D (), dir ) < 0 ) continue;
					
					
					//Debug.DrawLine ((Vector3)va1, (Vector3)va2, Color.magenta);
					//Debug.DrawLine ((Vector3)vb1, (Vector3)vb2, Color.cyan);
					//Debug.Break ();
					
					if ( Int3.Angle ( (vb2-vb1), (va2-va1) ) > (170.0/360.0f)*Mathf.PI*2 ) {
						
						float t1 = 0;
						float t2 = 1;
						
						t2 = System.Math.Min ( t2, AstarMath.NearestPointFactor ( va1, va2, vb1 ) );
						t1 = System.Math.Max ( t1, AstarMath.NearestPointFactor ( va1, va2, vb2 ) );
						
						if ( t2 < t1 ) {
							Debug.LogError ("Wait wut!? " + t1 + " " + t2 + " " + va1 + " " + va2 + " " + vb1 + " " + vb2+"\nTODO, fix this error" );
						} else {
						
							Vector3 pa = (Vector3)(va2-va1)*t1 + (Vector3)va1;
							Vector3 pb = (Vector3)(va2-va1)*t2 + (Vector3)va1;
							
							startNode.portalA = pa;
							startNode.portalB = pb;
							
							endNode.portalA = pb;
							endNode.portalB = pa;
							
							//Add connections between nodes, or replace old connections if existing
							connectedNode1.AddConnection(startNode, (uint)Mathf.RoundToInt (((Int3)(clamped1 - StartTransform.position)).costMagnitude*costFactor));
							connectedNode2.AddConnection(endNode, (uint)Mathf.RoundToInt (((Int3)(clamped2 - EndTransform.position)).costMagnitude*costFactor));
							
							startNode.AddConnection(connectedNode1, (uint)Mathf.RoundToInt (((Int3)(clamped1 - StartTransform.position)).costMagnitude*costFactor));
							endNode.AddConnection(connectedNode2, (uint)Mathf.RoundToInt (((Int3)(clamped2 - EndTransform.position)).costMagnitude*costFactor));
							
							return;
						}
					}
				}
			}
			
		}
		
		void DrawCircle (Vector3 o, float r, int detail, Color col) {
			Vector3 prev = new Vector3(Mathf.Cos(0)*r,0,Mathf.Sin(0)*r) + o;
			Gizmos.color = col;
			for (int i=0;i<=detail;i++) {
				float t = (i*Mathf.PI*2f)/detail;
				Vector3 c = new Vector3(Mathf.Cos(t)*r,0,Mathf.Sin(t)*r) + o;
				Gizmos.DrawLine(prev,c);
				prev = c;
			}
		}
		
		private readonly static Color GizmosColor = new Color(206.0f/255.0f,136.0f/255.0f,48.0f/255.0f,0.5f);
		private readonly static Color GizmosColorSelected = new Color(235.0f/255.0f,123.0f/255.0f,32.0f/255.0f,1.0f);
		
		void DrawGizmoBezier (Vector3 p1, Vector3 p2) {
			
			Vector3 dir = p2-p1;
			
			if (dir == Vector3.zero) return;
			
			Vector3 normal = Vector3.Cross (Vector3.up,dir);
			Vector3 normalUp = Vector3.Cross (dir,normal);
			
			normalUp = normalUp.normalized;
			normalUp *= dir.magnitude*0.1f;
			
			Vector3 p1c = p1+normalUp;
			Vector3 p2c = p2+normalUp;
			
			Vector3 prev = p1;
			for (int i=1;i<=20;i++) {
				float t = i/20.0f;
				Vector3 p = AstarMath.CubicBezier (p1,p1c,p2c,p2,t);
				Gizmos.DrawLine (prev,p);
				prev = p;
			}
		}
		
		public virtual void OnDrawGizmosSelected () {
			OnDrawGizmos(true);
		}
		
		public void OnDrawGizmos () {
			OnDrawGizmos (false);
		}
		
		public void OnDrawGizmos (bool selected) {
			Color col = selected ? GizmosColorSelected : GizmosColor;
			if (StartTransform != null) {
				DrawCircle(StartTransform.position,0.4f,10,col);
			}
			if (EndTransform != null) {
				DrawCircle(EndTransform.position,0.4f,10,col);
			}
			
			//Gizmos.DrawLine ( transform.position - transform.right*0.5f*portalWidth, transform.position + transform.right*0.5f*portalWidth );
			
			if (StartTransform != null && EndTransform != null) {
				Gizmos.color = col;
				DrawGizmoBezier (StartTransform.position,EndTransform.position);
				if (selected) {
					Vector3 cross = Vector3.Cross (Vector3.up, (EndTransform.position-StartTransform.position)).normalized;
					DrawGizmoBezier (StartTransform.position+cross*0.1f,EndTransform.position+cross*0.1f);
					DrawGizmoBezier (StartTransform.position-cross*0.1f,EndTransform.position-cross*0.1f);
				}
				
			}
		}
	}
}