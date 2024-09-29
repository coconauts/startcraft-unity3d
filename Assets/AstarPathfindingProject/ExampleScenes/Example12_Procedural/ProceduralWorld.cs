using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Pathfinding.Examples {
	/// <summary>Example script for generating an infinite procedural world</summary>
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_examples_1_1_procedural_world.php")]
	public class ProceduralWorld : MonoBehaviour {
		public Transform target;

		public ProceduralPrefab[] prefabs;

		/// <summary>How far away to generate tiles</summary>
		public int range = 1;

		public int disableAsyncLoadWithinRange = 1;

		/// <summary>World size of tiles</summary>
		public float tileSize = 100;
		public int subTiles = 20;

		/// <summary>
		/// Enable static batching on generated tiles.
		/// Will improve overall FPS, but might cause FPS drops on
		/// some frames when static batching is done
		/// </summary>
		public bool staticBatching = false;

		Queue<IEnumerator> tileGenerationQueue = new Queue<IEnumerator>();

		public enum RotationRandomness {
			AllAxes,
			Y
		}

		[System.Serializable]
		public class ProceduralPrefab {
			/// <summary>Prefab to use</summary>
			public GameObject prefab;

			/// <summary>Number of objects per square world unit</summary>
			public float density = 0;

			/// <summary>
			/// Multiply by [perlin noise].
			/// Value from 0 to 1 indicating weight.
			/// </summary>
			public float perlin = 0;

			/// <summary>
			/// Perlin will be raised to this power.
			/// A higher value gives more distinct edges
			/// </summary>
			public float perlinPower = 1;

			/// <summary>Some offset to avoid identical density maps</summary>
			public Vector2 perlinOffset = Vector2.zero;

			/// <summary>
			/// Perlin noise scale.
			/// A higher value spreads out the maximums and minimums of the density.
			/// </summary>
			public float perlinScale = 1;

			/// <summary>
			/// Multiply by [random].
			/// Value from 0 to 1 indicating weight.
			/// </summary>
			public float random = 1;

			public RotationRandomness randomRotation = RotationRandomness.AllAxes;

			/// <summary>If checked, a single object will be created in the center of each tile</summary>
			public bool singleFixed = false;
		}

		/// <summary>All tiles</summary>
		Dictionary<Int2, ProceduralTile> tiles = new Dictionary<Int2, ProceduralTile>();

		// Use this for initialization
		void Start () {
			// Calculate the closest tiles
			// and then recalculate the graph
			Update();
			AstarPath.active.Scan();

			StartCoroutine(GenerateTiles());
		}

		// Update is called once per frame
		void Update () {
			// Calculate the tile the target is standing on
			Int2 p = new Int2(Mathf.RoundToInt((target.position.x - tileSize*0.5f) / tileSize), Mathf.RoundToInt((target.position.z - tileSize*0.5f) / tileSize));

			// Clamp range
			range = range < 1 ? 1 : range;

			// Remove tiles which are out of range
			bool changed = true;
			while (changed) {
				changed = false;
				foreach (KeyValuePair<Int2, ProceduralTile> pair in tiles) {
					if (Mathf.Abs(pair.Key.x-p.x) > range || Mathf.Abs(pair.Key.y-p.y) > range) {
						pair.Value.Destroy();
						tiles.Remove(pair.Key);
						changed = true;
						break;
					}
				}
			}

			// Add tiles which have come in range
			// and start calculating them
			for (int x = p.x-range; x <= p.x+range; x++) {
				for (int z = p.y-range; z <= p.y+range; z++) {
					if (!tiles.ContainsKey(new Int2(x, z))) {
						ProceduralTile tile = new ProceduralTile(this, x, z);
						var generator = tile.Generate();
						// Tick it one step forward
						generator.MoveNext();
						// Calculate the rest later
						tileGenerationQueue.Enqueue(generator);
						tiles.Add(new Int2(x, z), tile);
					}
				}
			}

			// The ones directly adjacent to the current one
			// should always be completely calculated
			// make sure they are
			for (int x = p.x-disableAsyncLoadWithinRange; x <= p.x+disableAsyncLoadWithinRange; x++) {
				for (int z = p.y-disableAsyncLoadWithinRange; z <= p.y+disableAsyncLoadWithinRange; z++) {
					tiles[new Int2(x, z)].ForceFinish();
				}
			}
		}

		IEnumerator GenerateTiles () {
			while (true) {
				if (tileGenerationQueue.Count > 0) {
					var generator = tileGenerationQueue.Dequeue();
					yield return StartCoroutine(generator);
				}
				yield return null;
			}
		}

		class ProceduralTile {
			int x, z;
			System.Random rnd;

			ProceduralWorld world;

			public bool destroyed { get; private set; }

			public ProceduralTile (ProceduralWorld world, int x, int z) {
				this.x = x;
				this.z = z;
				this.world = world;
				rnd = new System.Random((x * 10007) ^ (z*36007));
			}

			Transform root;
			IEnumerator ie;

			public IEnumerator Generate () {
				ie = InternalGenerate();
				GameObject rt = new GameObject("Tile " + x + " " + z);
				root = rt.transform;
				while (ie != null && root != null && ie.MoveNext()) yield return ie.Current;
				ie = null;
			}

			public void ForceFinish () {
				while (ie != null && root != null && ie.MoveNext()) {}
				ie = null;
			}

			Vector3 RandomInside () {
				Vector3 v = new Vector3();

				v.x = (x + (float)rnd.NextDouble())*world.tileSize;
				v.z = (z + (float)rnd.NextDouble())*world.tileSize;
				return v;
			}

			Vector3 RandomInside (float px, float pz) {
				Vector3 v = new Vector3();

				v.x = (px + (float)rnd.NextDouble()/world.subTiles)*world.tileSize;
				v.z = (pz + (float)rnd.NextDouble()/world.subTiles)*world.tileSize;
				return v;
			}

			Quaternion RandomYRot (ProceduralPrefab prefab) {
				return prefab.randomRotation == RotationRandomness.AllAxes ? Quaternion.Euler(360*(float)rnd.NextDouble(), 360*(float)rnd.NextDouble(), 360*(float)rnd.NextDouble()) : Quaternion.Euler(0, 360 * (float)rnd.NextDouble(), 0);
			}

			IEnumerator InternalGenerate () {
				Debug.Log("Generating tile " + x + ", " + z);
				int counter = 0;

				float[, ] ditherMap = new float[world.subTiles+2, world.subTiles+2];

				//List<GameObject> objs = new List<GameObject>();

				for (int i = 0; i < world.prefabs.Length; i++) {
					ProceduralPrefab pref = world.prefabs[i];

					if (pref.singleFixed) {
						Vector3 p = new Vector3((x+0.5f) * world.tileSize, 0, (z+0.5f) * world.tileSize);
						GameObject ob = GameObject.Instantiate(pref.prefab, p, Quaternion.identity) as GameObject;
						ob.transform.parent = root;
					} else {
						float subSize = world.tileSize/world.subTiles;

						for (int sx = 0; sx < world.subTiles; sx++) {
							for (int sz = 0; sz < world.subTiles; sz++) {
								ditherMap[sx+1, sz+1] = 0;
							}
						}

						for (int sx = 0; sx < world.subTiles; sx++) {
							for (int sz = 0; sz < world.subTiles; sz++) {
								float px = x + sx/(float)world.subTiles;//sx / world.tileSize;
								float pz = z + sz/(float)world.subTiles;//sz / world.tileSize;

								float perl = Mathf.Pow(Mathf.PerlinNoise((px + pref.perlinOffset.x)*pref.perlinScale, (pz + pref.perlinOffset.y)*pref.perlinScale), pref.perlinPower);

								float density = pref.density * Mathf.Lerp(1, perl, pref.perlin) * Mathf.Lerp(1, (float)rnd.NextDouble(), pref.random);
								float fcount = subSize*subSize*density + ditherMap[sx+1, sz+1];
								int count = Mathf.RoundToInt(fcount);

								// Apply dithering
								// See http://en.wikipedia.org/wiki/Floyd%E2%80%93Steinberg_dithering
								ditherMap[sx+1+1, sz+1+0] += (7f/16f) * (fcount - count);
								ditherMap[sx+1-1, sz+1+1] += (3f/16f) * (fcount - count);
								ditherMap[sx+1+0, sz+1+1] += (5f/16f) * (fcount - count);
								ditherMap[sx+1+1, sz+1+1] += (1f/16f) * (fcount - count);

								// Create a number of objects
								for (int j = 0; j < count; j++) {
									// Find a random position inside the current sub-tile
									Vector3 p = RandomInside(px, pz);
									GameObject ob = GameObject.Instantiate(pref.prefab, p, RandomYRot(pref)) as GameObject;
									ob.transform.parent = root;
									//ob.SetActive ( false );
									//objs.Add ( ob );
									counter++;
									if (counter % 2 == 0)
										yield return null;
								}
							}
						}
					}
				}

				ditherMap = null;

				yield return null;
				yield return null;

				//Batch everything for improved performance
				if (Application.HasProLicense() && world.staticBatching) {
					StaticBatchingUtility.Combine(root.gameObject);
				}
			}

			public void Destroy () {
				if (root != null) {
					Debug.Log("Destroying tile " + x + ", " + z);
					GameObject.Destroy(root.gameObject);
					root = null;
				}

				// Make sure the tile generator coroutine is destroyed
				ie = null;
			}
		}
	}
}
