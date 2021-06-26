using UnityEngine;
using System.Collections.Generic;

namespace Pathfinding {
	using Pathfinding.Util;
	using Pathfinding.Serialization;

	public interface INavmesh {
		void GetNodes (System.Action<GraphNode> del);
	}

	/// <summary>
	/// Generates graphs based on navmeshes.
	/// \ingroup graphs
	/// Navmeshes are meshes where each triangle defines a walkable area.
	/// These are great because the AI can get so much more information on how it can walk.
	/// Polygons instead of points mean that the funnel smoother can produce really nice looking paths and the graphs are also really fast to search
	/// and have a low memory footprint because fewer nodes are usually needed to describe the same area compared to grid graphs.
	///
	/// See: Pathfinding.RecastGraph
	///
	/// [Open online documentation to see images]
	/// [Open online documentation to see images]
	/// </summary>
	[JsonOptIn]
	[Pathfinding.Util.Preserve]
	public class NavMeshGraph : NavmeshBase, IUpdatableGraph {
		/// <summary>Mesh to construct navmesh from</summary>
		[JsonMember]
		public Mesh sourceMesh;

		/// <summary>Offset in world space</summary>
		[JsonMember]
		public Vector3 offset;

		/// <summary>Rotation in degrees</summary>
		[JsonMember]
		public Vector3 rotation;

		/// <summary>Scale of the graph</summary>
		[JsonMember]
		public float scale = 1;

		/// <summary>
		/// Determines how normals are calculated.
		/// Disable for spherical graphs or other complicated surfaces that allow the agents to e.g walk on walls or ceilings.
		///
		/// By default the normals of the mesh will be flipped so that they point as much as possible in the upwards direction.
		/// The normals are important when connecting adjacent nodes. Two adjacent nodes will only be connected if they are oriented the same way.
		/// This is particularly important if you have a navmesh on the walls or even on the ceiling of a room. Or if you are trying to make a spherical navmesh.
		/// If you do one of those things then you should set disable this setting and make sure the normals in your source mesh are properly set.
		///
		/// If you for example take a look at the image below. In the upper case then the nodes on the bottom half of the
		/// mesh haven't been connected with the nodes on the upper half because the normals on the lower half will have been
		/// modified to point inwards (as that is the direction that makes them face upwards the most) while the normals on
		/// the upper half point outwards. This causes the nodes to not connect properly along the seam. When this option
		/// is set to false instead the nodes are connected properly as in the original mesh all normals point outwards.
		/// [Open online documentation to see images]
		///
		/// The default value of this field is true to reduce the risk for errors in the common case. If a mesh is supplied that
		/// has all normals pointing downwards and this option is false, then some methods like <see cref="PointOnNavmesh"/> will not work correctly
		/// as they assume that the normals point upwards. For a more complicated surface like a spherical graph those methods make no sense anyway
		/// as there is no clear definition of what it means to be "inside" a triangle when there is no clear up direction.
		/// </summary>
		[JsonMember]
		public bool recalculateNormals = true;

		/// <summary>
		/// Cached bounding box minimum of <see cref="sourceMesh"/>.
		/// This is important when the graph has been saved to a file and is later loaded again, but the original mesh does not exist anymore (or has been moved).
		/// In that case we still need to be able to find the bounding box since the <see cref="CalculateTransform"/> method uses it.
		/// </summary>
		[JsonMember]
		Vector3 cachedSourceMeshBoundsMin;

		protected override bool RecalculateNormals { get { return recalculateNormals; } }

		public override float TileWorldSizeX {
			get {
				return forcedBoundsSize.x;
			}
		}

		public override float TileWorldSizeZ {
			get {
				return forcedBoundsSize.z;
			}
		}

		protected override float MaxTileConnectionEdgeDistance {
			get {
				// Tiles are not supported, so this is irrelevant
				return 0f;
			}
		}

		public override GraphTransform CalculateTransform () {
			return new GraphTransform(Matrix4x4.TRS(offset, Quaternion.Euler(rotation), Vector3.one) * Matrix4x4.TRS(sourceMesh != null ? sourceMesh.bounds.min * scale : cachedSourceMeshBoundsMin * scale, Quaternion.identity, Vector3.one));
		}

		GraphUpdateThreading IUpdatableGraph.CanUpdateAsync (GraphUpdateObject o) {
			return GraphUpdateThreading.UnityThread;
		}

		void IUpdatableGraph.UpdateAreaInit (GraphUpdateObject o) {}
		void IUpdatableGraph.UpdateAreaPost (GraphUpdateObject o) {}

		void IUpdatableGraph.UpdateArea (GraphUpdateObject o) {
			UpdateArea(o, this);
		}

		public static void UpdateArea (GraphUpdateObject o, INavmeshHolder graph) {
			Bounds bounds = graph.transform.InverseTransform(o.bounds);

			// Bounding rectangle with integer coordinates
			var irect = new IntRect(
				Mathf.FloorToInt(bounds.min.x*Int3.Precision),
				Mathf.FloorToInt(bounds.min.z*Int3.Precision),
				Mathf.CeilToInt(bounds.max.x*Int3.Precision),
				Mathf.CeilToInt(bounds.max.z*Int3.Precision)
				);

			// Corners of the bounding rectangle
			var a = new Int3(irect.xmin, 0, irect.ymin);
			var b = new Int3(irect.xmin, 0, irect.ymax);
			var c = new Int3(irect.xmax, 0, irect.ymin);
			var d = new Int3(irect.xmax, 0, irect.ymax);

			var ymin = ((Int3)bounds.min).y;
			var ymax = ((Int3)bounds.max).y;

			// Loop through all nodes and check if they intersect the bounding box
			graph.GetNodes(_node => {
				var node = _node as TriangleMeshNode;

				bool inside = false;

				int allLeft = 0;
				int allRight = 0;
				int allTop = 0;
				int allBottom = 0;

				// Check bounding box rect in XZ plane
				for (int v = 0; v < 3; v++) {
					Int3 p = node.GetVertexInGraphSpace(v);

					if (irect.Contains(p.x, p.z)) {
						inside = true;
						break;
					}

					if (p.x < irect.xmin) allLeft++;
					if (p.x > irect.xmax) allRight++;
					if (p.z < irect.ymin) allTop++;
					if (p.z > irect.ymax) allBottom++;
				}

				if (!inside && (allLeft == 3 || allRight == 3 || allTop == 3 || allBottom == 3)) {
					return;
				}

				// Check if the polygon edges intersect the bounding rect
				for (int v = 0; v < 3; v++) {
					int v2 = v > 1 ? 0 : v+1;

					Int3 vert1 = node.GetVertexInGraphSpace(v);
					Int3 vert2 = node.GetVertexInGraphSpace(v2);

					if (VectorMath.SegmentsIntersectXZ(a, b, vert1, vert2)) { inside = true; break; }
					if (VectorMath.SegmentsIntersectXZ(a, c, vert1, vert2)) { inside = true; break; }
					if (VectorMath.SegmentsIntersectXZ(c, d, vert1, vert2)) { inside = true; break; }
					if (VectorMath.SegmentsIntersectXZ(d, b, vert1, vert2)) { inside = true; break; }
				}

				// Check if the node contains any corner of the bounding rect
				if (inside || node.ContainsPointInGraphSpace(a) || node.ContainsPointInGraphSpace(b) || node.ContainsPointInGraphSpace(c) || node.ContainsPointInGraphSpace(d)) {
					inside = true;
				}

				if (!inside) {
					return;
				}

				int allAbove = 0;
				int allBelow = 0;

				// Check y coordinate
				for (int v = 0; v < 3; v++) {
					Int3 p = node.GetVertexInGraphSpace(v);
					if (p.y < ymin) allBelow++;
					if (p.y > ymax) allAbove++;
				}

				// Polygon is either completely above the bounding box or completely below it
				if (allBelow == 3 || allAbove == 3) return;

				// Triangle is inside the bounding box!
				// Update it!
				o.WillUpdateNode(node);
				o.Apply(node);
			});
		}

		/// <summary>Scans the graph using the path to an .obj mesh</summary>
		[System.Obsolete("Set the mesh to ObjImporter.ImportFile(...) and scan the graph the normal way instead")]
		public void ScanInternal (string objMeshPath) {
			Mesh mesh = ObjImporter.ImportFile(objMeshPath);

			if (mesh == null) {
				Debug.LogError("Couldn't read .obj file at '"+objMeshPath+"'");
				return;
			}

			sourceMesh = mesh;

			var scan = ScanInternal().GetEnumerator();
			while (scan.MoveNext()) {}
		}

		protected override IEnumerable<Progress> ScanInternal () {
			cachedSourceMeshBoundsMin = sourceMesh != null ? sourceMesh.bounds.min : Vector3.zero;
			transform = CalculateTransform();
			tileZCount = tileXCount = 1;
			tiles = new NavmeshTile[tileZCount*tileXCount];
			TriangleMeshNode.SetNavmeshHolder(AstarPath.active.data.GetGraphIndex(this), this);

			if (sourceMesh == null) {
				FillWithEmptyTiles();
				yield break;
			}

			yield return new Progress(0.0f, "Transforming Vertices");

			forcedBoundsSize = sourceMesh.bounds.size * scale;
			Vector3[] vectorVertices = sourceMesh.vertices;
			var intVertices = ListPool<Int3>.Claim (vectorVertices.Length);
			var matrix = Matrix4x4.TRS(-sourceMesh.bounds.min * scale, Quaternion.identity, Vector3.one * scale);
			// Convert the vertices to integer coordinates and also position them in graph space
			// so that the minimum of the bounding box of the mesh is at the origin
			// (the vertices will later be transformed to world space)
			for (int i = 0; i < vectorVertices.Length; i++) {
				intVertices.Add((Int3)matrix.MultiplyPoint3x4(vectorVertices[i]));
			}

			yield return new Progress(0.1f, "Compressing Vertices");

			// Remove duplicate vertices
			Int3[] compressedVertices = null;
			int[] compressedTriangles = null;
			Polygon.CompressMesh(intVertices, new List<int>(sourceMesh.triangles), out compressedVertices, out compressedTriangles);
			ListPool<Int3>.Release (ref intVertices);

			yield return new Progress(0.2f, "Building Nodes");

			ReplaceTile(0, 0, compressedVertices, compressedTriangles);

			// Signal that tiles have been recalculated to the navmesh cutting system.
			navmeshUpdateData.OnRecalculatedTiles(tiles);
			if (OnRecalculatedTiles != null) OnRecalculatedTiles(tiles.Clone() as NavmeshTile[]);
		}

		protected override void DeserializeSettingsCompatibility (GraphSerializationContext ctx) {
			base.DeserializeSettingsCompatibility(ctx);

			sourceMesh = ctx.DeserializeUnityObject() as Mesh;
			offset = ctx.DeserializeVector3();
			rotation = ctx.DeserializeVector3();
			scale = ctx.reader.ReadSingle();
			nearestSearchOnlyXZ = !ctx.reader.ReadBoolean();
		}
	}
}
