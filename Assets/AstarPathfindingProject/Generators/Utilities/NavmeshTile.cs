namespace Pathfinding {
	using Pathfinding.Util;
	using UnityEngine;

	public class NavmeshTile : INavmeshHolder {
		/// <summary>Tile triangles</summary>
		public int[] tris;

		/// <summary>Tile vertices</summary>
		public Int3[] verts;

		/// <summary>Tile vertices in graph space</summary>
		public Int3[] vertsInGraphSpace;

		/// <summary>Tile X Coordinate</summary>
		public int x;

		/// <summary>Tile Z Coordinate</summary>
		public int z;

		/// <summary>
		/// Width, in tile coordinates.
		/// Warning: Widths other than 1 are not supported. This is mainly here for possible future features.
		/// </summary>
		public int w;

		/// <summary>
		/// Depth, in tile coordinates.
		/// Warning: Depths other than 1 are not supported. This is mainly here for possible future features.
		/// </summary>
		public int d;

		/// <summary>All nodes in the tile</summary>
		public TriangleMeshNode[] nodes;

		/// <summary>Bounding Box Tree for node lookups</summary>
		public BBTree bbTree;

		/// <summary>Temporary flag used for batching</summary>
		public bool flag;

		public NavmeshBase graph;

		#region INavmeshHolder implementation

		public void GetTileCoordinates (int tileIndex, out int x, out int z) {
			x = this.x;
			z = this.z;
		}

		public int GetVertexArrayIndex (int index) {
			return index & NavmeshBase.VertexIndexMask;
		}

		/// <summary>Get a specific vertex in the tile</summary>
		public Int3 GetVertex (int index) {
			int idx = index & NavmeshBase.VertexIndexMask;

			return verts[idx];
		}

		public Int3 GetVertexInGraphSpace (int index) {
			return vertsInGraphSpace[index & NavmeshBase.VertexIndexMask];
		}

		/// <summary>Transforms coordinates from graph space to world space</summary>
		public GraphTransform transform { get { return graph.transform; } }

		#endregion

		public void GetNodes (System.Action<GraphNode> action) {
			if (nodes == null) return;
			for (int i = 0; i < nodes.Length; i++) action(nodes[i]);
		}
	}
}
