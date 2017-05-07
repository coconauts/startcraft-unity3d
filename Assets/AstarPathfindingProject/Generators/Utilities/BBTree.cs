//#define ASTARDEBUG   //"BBTree Debug" If enables, some queries to the tree will show debug lines. Turn off multithreading when using this since DrawLine calls cannot be called from a different thread

//#define ASTAR_OLD_BBTREE // Use class based BBTree implementation instead of struct based. Struct based is better for runtime performance and memory, but class based scans slightly faster.
using System;
using UnityEngine;
using Pathfinding;
using System.Collections.Generic;

namespace Pathfinding
{
	/** Axis Aligned Bounding Box Tree.
	 * Holds a bounding box tree of triangles.\n
	 * \b Performance: Insertion - Practically O(1) - About 0.003 ms
	 * \astarpro
	 */
	public class BBTree
	{

		public void OnDrawGizmos () {}
	}
	
}