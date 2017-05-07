using UnityEngine;
using System.Collections;
using Pathfinding;

public class ProceduralGridMover : MonoBehaviour {
	
	public float updateDistance = 5;

	public Transform target;

	public bool floodFill;

	GridGraph graph;

	GridNode[] tmp;

	public void Start () {
		if ( AstarPath.active == null ) throw new System.Exception ("There is no AstarPath object in the scene");

		graph = AstarPath.active.astarData.gridGraph;

		if ( graph == null ) throw new System.Exception ("The AstarPath object has no GridGraph");
		UpdateGraph ();
	}

	// Update is called once per frame
	void Update () {
	
		if ( (target.position - graph.center).sqrMagnitude > updateDistance*updateDistance ) {
			IEnumerator ie = UpdateGraph ();
			AstarPath.active.AddWorkItem (new AstarPath.AstarWorkItem (delegate (bool force) {
				if ( force ) while ( ie.MoveNext () ) {}
				return !ie.MoveNext ();
			}));
		}
	}

	public IEnumerator UpdateGraph () {

		Vector3 dir = target.position - graph.center;

		// Snap to a whole number of nodes
		dir.x = Mathf.Round(dir.x/graph.nodeSize)*graph.nodeSize;
		dir.z = Mathf.Round(dir.z/graph.nodeSize)*graph.nodeSize;
		dir.y = 0;

		if ( dir == Vector3.zero ) yield break;

		Int2 offset = new Int2 ( -Mathf.RoundToInt(dir.x/graph.nodeSize), -Mathf.RoundToInt(dir.z/graph.nodeSize) );

		// Move the center
		graph.center += dir;
		graph.GenerateMatrix ();

		if ( tmp == null || tmp.Length != graph.nodes.Length ) {
			tmp = new GridNode[graph.nodes.Length];
		}

		int width = graph.width;
		int depth = graph.depth;
		GridNode[] nodes = graph.nodes;
		
		if ( Mathf.Abs(offset.x) <= width && Mathf.Abs(offset.y) <= depth ) {
		
			for ( int z=0; z < depth; z++ ) {
				int pz = z*width;
				int tz = ((z+offset.y + depth)%depth)*width;
				for ( int x=0; x < width; x++ ) {
					tmp[tz + ((x+offset.x + width) % width)] = nodes[pz + x];
				}
			}
			
			yield return null;
			
			for ( int z=0; z < depth; z++ ) {
				int pz = z*width;
				for ( int x=0; x < width; x++ ) {
					GridNode node = tmp[pz + x];
					node.NodeInGridIndex = pz + x;
					nodes[pz + x] = node;
				}
			}
	
			IntRect r = new IntRect ( 0, 0, offset.x, offset.y );
			int minz = r.ymax;
			int maxz = depth;
	
			if ( r.xmin > r.xmax ) {
				int tmp2 = r.xmax;
				r.xmax = width + r.xmin;
				r.xmin = width + tmp2;
			}
			if ( r.ymin > r.ymax ) {
				int tmp2 = r.ymax;
				r.ymax = depth + r.ymin;
				r.ymin = depth + tmp2;
	
				minz = 0;
				maxz = r.ymin;
			}
	
			//Debug.Log ( "R1 " + r );
			r = r.Expand ( graph.erodeIterations + 1 );
			r = IntRect.Intersection ( r, new IntRect ( 0, 0, width, depth ) );
	
			//Debug.Log ( "R2 " + r );
	
			yield return null;
	
			for ( int z = r.ymin; z < r.ymax; z++ ) {
				for ( int x = 0; x < width; x++ ) {
					graph.UpdateNodePositionCollision ( nodes[z*width + x], x, z, false );
				}
			}
	
			yield return null;
	
			for ( int z = minz; z < maxz; z++ ) {
				for ( int x = r.xmin; x < r.xmax; x++ ) {
					graph.UpdateNodePositionCollision ( nodes[z*width + x], x, z, false );
				}
			}
	
			yield return null;
	
			for ( int z = r.ymin; z < r.ymax; z++ ) {
				for ( int x = 0; x < width; x++ ) {
					graph.CalculateConnections (nodes, x, z, nodes[z*width+x]);
				}
			}
	
			yield return null;
	
			for ( int z = minz; z < maxz; z++ ) {
				for ( int x = r.xmin; x < r.xmax; x++ ) {
					graph.CalculateConnections (nodes, x, z, nodes[z*width+x]);
				}
			}
	
			yield return null;
	
			for ( int z = 0; z < depth; z++ ) {
				for ( int x = 0; x < width; x++ ) {
					if ( x == 0 || z == 0 || x >= width-1 || z >= depth-1 ) graph.CalculateConnections (nodes, x, z, nodes[z*width+x]);
				}
			}
			
		} else {
			
			for ( int z = 0; z < depth; z++ ) {
				for ( int x = 0; x < width; x++ ) {
					graph.UpdateNodePositionCollision ( nodes[z*width + x], x, z, false );
				}
			}
			
			for ( int z = 0; z < depth; z++ ) {
				for ( int x = 0; x < width; x++ ) {
					graph.CalculateConnections (nodes, x, z, nodes[z*width+x]);
				}
			}
		}
		
		if ( floodFill ) {
			yield return null;
			AstarPath.active.QueueWorkItemFloodFill ();
		}
	}
}
