using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

public class ProceduralWorld : MonoBehaviour {

	public Transform target;

	public ProceduralPrefab[] prefabs;

	public int range = 0;

	public float tileSize = 100;
	public int subTiles = 20;

	[System.Serializable]
	public class ProceduralPrefab {
		/** Prefab to use */
		public GameObject prefab;

		/** Number of objects per square world unit */
		public float density = 0;

		/** Multiply by [perlin noise].
		 * Value from 0 to 1 indicating weight.
		 */
		public float perlin = 0;

		/** Perlin will be raised to this power.
		 * A higher value gives more distinct edges
		 */
		public float perlinPower = 1;

		/** Some offset to avoid identical density maps */
		public Vector2 perlinOffset = Vector2.zero;

		/** Perlin noise scale.
		 * A higher value spreads out the maximums and minimums of the density.
		 */
		public float perlinScale = 1;

		/** Multiply by [random].
		 * Value from 0 to 1 indicating weight.
		 */
		public float random = 1;

		/** If checked, a single object will be created in the center of each tile */
		public bool singleFixed = false;
	}

	/** All tiles */
	Dictionary<Int2, ProceduralTile> tiles = new Dictionary<Int2, ProceduralTile>();

	// Use this for initialization
	void Start () {
		// Calculate the closest tiles
		// and then recalculate the graph
		Update ();
		AstarPath.active.Scan ();
	}
	
	// Update is called once per frame
	void Update () {

		// Calculate the tile the target is standing on
		Int2 p = new Int2 ( Mathf.RoundToInt ((target.position.x - tileSize*0.5f) / tileSize), Mathf.RoundToInt ((target.position.z - tileSize*0.5f) / tileSize) );

		// Clamp range
		range = range < 1 ? 1 : range;

		// Remove tiles which are out of range
		bool changed = true;
		while ( changed ) {
			changed = false;
			foreach (KeyValuePair<Int2,ProceduralTile> pair in tiles ) {
				if ( Mathf.Abs (pair.Key.x-p.x) > range || Mathf.Abs (pair.Key.y-p.y) > range ) {
					pair.Value.Destroy ();
					tiles.Remove ( pair.Key );
					changed = true;
					break;
				}
			}
		}

		// Add tiles which have come in range
		// and start calculating them
		for ( int x = p.x-range; x <= p.x+range; x++ ) {
			for ( int z = p.y-range; z <= p.y+range; z++ ) {
				if ( !tiles.ContainsKey ( new Int2(x,z) ) ) {
					ProceduralTile tile = new ProceduralTile ( this, x, z );
					StartCoroutine ( tile.Generate () );
					tiles.Add ( new Int2(x,z), tile );
				}
			}
		}

		// The ones directly adjacent to the current one
		// should always be completely calculated
		// make sure they are
		for ( int x = p.x-1; x <= p.x+1; x++ ) {
			for ( int z = p.y-1; z <= p.y+1; z++ ) {
				tiles[new Int2(x,z)].ForceFinish();
			}
		}

	}

	class ProceduralTile {
		
		int x, z;
		System.Random rnd;

		ProceduralWorld world;

		public ProceduralTile ( ProceduralWorld world, int x, int z ) {
			this.x = x;
			this.z = z;
			this.world = world;
			rnd = new System.Random ( (x * 10007) ^ (z*36007));
		}

		Transform root;
		IEnumerator ie;

		public IEnumerator Generate () {
			ie = InternalGenerate ();
			GameObject rt = new GameObject ("Tile " + x + " " + z );
			root = rt.transform;
			while ( ie != null && root != null && ie.MoveNext ()) yield return ie.Current;
			ie = null;
		}

		public void ForceFinish () {
			while ( ie != null && root != null && ie.MoveNext ()) {}
			ie = null;
		}

		Vector3 RandomInside () {
			Vector3 v = new Vector3();
			v.x = (x + (float)rnd.NextDouble())*world.tileSize;
			v.z = (z + (float)rnd.NextDouble())*world.tileSize;
			return v;
		}
	
		Vector3 RandomInside ( float px, float pz) {
			Vector3 v = new Vector3();
			v.x = (px + (float)rnd.NextDouble()/world.subTiles)*world.tileSize;
			v.z = (pz + (float)rnd.NextDouble()/world.subTiles)*world.tileSize;
			return v;
		}

		Quaternion RandomYRot () {
			return Quaternion.Euler ( 360*(float)rnd.NextDouble(),0, 360*(float)rnd.NextDouble());
		}

		IEnumerator InternalGenerate () {
			
			Debug.Log ( "Generating tile " + x + ", " + z );
			int counter = 0;

			float[,] ditherMap = new float[world.subTiles+2,world.subTiles+2];

			//List<GameObject> objs = new List<GameObject>();

			for ( int i=0;i<world.prefabs.Length;i++ ) {

				ProceduralPrefab pref = world.prefabs[i];

				if ( pref.singleFixed ) {
					Vector3 p = new Vector3 ( (x+0.5f) * world.tileSize, 0, (z+0.5f) * world.tileSize );
					GameObject ob = GameObject.Instantiate ( pref.prefab, p, Quaternion.identity ) as GameObject;
					ob.transform.parent = root;
				} else {
					float subSize = world.tileSize/world.subTiles;

					for ( int sx=0; sx < world.subTiles; sx++ ) {
						for ( int sz=0; sz < world.subTiles; sz++ ) {
							ditherMap[sx+1,sz+1] = 0;
						}
					}

					for ( int sx=0; sx < world.subTiles; sx++ ) {
						for ( int sz=0; sz < world.subTiles; sz++ ) {

							float px = x + sx/(float)world.subTiles;//sx / world.tileSize;
							float pz = z + sz/(float)world.subTiles;//sz / world.tileSize;

							float perl = Mathf.Pow (Mathf.PerlinNoise ( (px + pref.perlinOffset.x)*pref.perlinScale, (pz + pref.perlinOffset.y)*pref.perlinScale ), pref.perlinPower );
							
							float density = pref.density * Mathf.Lerp ( 1, perl, pref.perlin) * Mathf.Lerp ( 1, (float)rnd.NextDouble (), pref.random );
							float fcount = subSize*subSize*density + ditherMap[sx+1,sz+1];
							int count = Mathf.RoundToInt(fcount);

							// Apply dithering
							// See http://en.wikipedia.org/wiki/Floyd%E2%80%93Steinberg_dithering
							ditherMap[sx+1+1,sz+1+0] += (7f/16f) * (fcount - count);
							ditherMap[sx+1-1,sz+1+1] += (3f/16f) * (fcount - count);
							ditherMap[sx+1+0,sz+1+1] += (5f/16f) * (fcount - count);
							ditherMap[sx+1+1,sz+1+1] += (1f/16f) * (fcount - count);

							// Create a number of objects
							for ( int j=0;j<count;j++) {
								// Find a random position inside the current sub-tile
								Vector3 p = RandomInside (px, pz);
								GameObject ob = GameObject.Instantiate ( pref.prefab, p, RandomYRot () ) as GameObject;
								ob.transform.parent = root;
								//ob.SetActive ( false );
								//objs.Add ( ob );
								counter++;
								if ( counter % 2 == 0 )
									yield return null;
							}
						}
					}
				}
			}

			ditherMap = null;

			// Wait a bit so that less tiles are being generated right now to avoid larger FPS spikes
			yield return new WaitForSeconds(0.5f);

			//for ( int i=0;i<objs.Count;i++) {
			//	objs[i].SetActive ( true );
				//if ( i % 4 == 0 ) yield return 0;
			//}

			//Batch everything for improved performance
			if (Application.HasProLicense ()) {
				StaticBatchingUtility.Combine (root.gameObject);
			}
		}

		public void Destroy () {
			Debug.Log ("Destroying tile "+x + ", " + z);
			GameObject.Destroy ( root.gameObject );
			root = null;
		}
	}
}
