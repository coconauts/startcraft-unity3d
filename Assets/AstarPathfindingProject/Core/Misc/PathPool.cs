//#define ASTAR_NO_POOLING //Disable pooling for some reason. Could be debugging or just for measuring the difference.
using System;
using System.Collections.Generic;

namespace Pathfinding
{
	public static class PathPool<T> where T : Path, new()
	{
		private static Stack<T> pool;
		
		private static int totalCreated;
		
		static PathPool () {
			pool = new Stack<T>();
		}
		
		/** Recycles a path and puts in the pool.
		 * This function should not be used directly. Instead use the Path.Claim and Path.Release functions.
		 */
		public static void Recycle (T path) {
			lock (pool) {
#if UNITY_EDITOR
				// I am trusting the developer that it at least 1 time tests the game in the editor
				// Increases performance in builds
				if (!System.Type.Equals (path.GetType (), typeof(T))) {
					throw new ArgumentException ("Cannot recycle path of type '"+path.GetType().Name+"' in a pool for path type '"+typeof(T).Name+"'.\n" +
						"Most likely the path type does not have support for recycling. Please do not call Recycle () on that path");
				}
#endif
				path.recycled = true;
				
				path.OnEnterPool ();
				pool.Push (path);
			}
		}
		
		/** Warms up path, node list and vector list pools.
		 * Makes sure there is at least \a count paths, each with a minimum capacity for paths with length \a length in the pool.
		 * The capacity means that paths shorter or equal to the capacity can be calculated without any large allocations taking place.
		 */
		public static void Warmup (int count, int length) {
			Pathfinding.Util.ListPool<GraphNode>.Warmup (count, length);
			Pathfinding.Util.ListPool<UnityEngine.Vector3>.Warmup (count, length);
			
			Path[] tmp = new Path[count];
			for (int i=0;i<count;i++)	{ tmp[i] = GetPath (); tmp[i].Claim (tmp); }
			for (int i=0;i<count;i++) 	tmp[i].Release (tmp);
		}
		
		public static int GetTotalCreated () {
			return totalCreated;
		}
		
		public static int GetSize () {
			return pool.Count;
		}
		
		public static T GetPath () {
			lock (pool) {
				T result;
				if (pool.Count > 0) {
					result = pool.Pop ();
				} else {
					result = new T ();
					totalCreated++;
				}
				result.recycled = false;
				result.Reset();
				
				return result;
			}
			
		}
	}
}

