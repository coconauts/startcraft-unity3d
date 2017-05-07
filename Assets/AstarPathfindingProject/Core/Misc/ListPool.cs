//#define ASTAR_NO_POOLING //Disable pooling for some reason. Could be debugging or just for measuring the difference.
//#define ASTAR_OPTIMIZE_POOLING //Skip some error checking for pooling. Optimizes Release calls to O(1) instead of O(n) where n is the number of lists in the pool. Recommended for release. Make sure you are pooling everything correctly.

using System;
using System.Collections.Generic;

namespace Pathfinding.Util
{
	/** Lightweight List Pool.
	 * Handy class for pooling lists of type T.
	 * 
	 * Usage:
	 * - Claim a new list using \code List<SomeClass> foo = ListPool<SomeClass>.Claim (); \endcode
	 * - Use it and do stuff with it
	 * - Release it with \code ListPool<SomeClass>.Release (foo); \endcode
	 * 
	 * You do not need to clear the list before releasing it.
	 * After you have released a list, you should never use it again, if you do use it, you will
	 * mess things up quite badly in the worst case.
	 * 
	 * // \warning This class is not thread safe
	 * 
	 * \since Version 3.2
	 * \see Pathfinding.Util.StackPool
	 */
	public static class ListPool<T>
	{
		/** Internal pool */
		static List<List<T>> pool;
		
		/** When requesting a list with a specified capacity, search max this many lists in the pool before giving up.
		 * Must be greater or equal to one.
		 */
		const int MaxCapacitySearchLength = 8;
		
		/** Static constructor */
		static ListPool ()
		{
			pool = new List<List<T>> ();
		}
		
		/** Claim a list.
		 * Returns a pooled list if any are in the pool.
		 * Otherwise it creates a new one.
		 * After usage, this list should be released using the Release function (though not strictly necessary).
		 */
		public static List<T> Claim () {
			lock (pool) {
				if (pool.Count > 0) {
					List<T> ls = pool[pool.Count-1];
					pool.RemoveAt(pool.Count-1);
					return ls;
				} else {
					return new List<T>();
				}
			}
		}
		
		/** Claim a list with minimum capacity
		 * Returns a pooled list if any are in the pool.
		 * Otherwise it creates a new one.
		 * After usage, this list should be released using the Release function (though not strictly necessary).
		 * This list returned will have at least the capacity specified.
		 */
		public static List<T> Claim (int capacity) {
			lock (pool) {
				if (pool.Count > 0) {
					List<T> list = null;
					int i = 0;
					for (;i<pool.Count && i < MaxCapacitySearchLength;i++) {
						list = pool[pool.Count-1-i];
						
						if (list.Capacity >= capacity) {
							pool.RemoveAt(pool.Count-1-i);
							return list;
						}
					}
					
					if (list == null) list = new List<T>(capacity);
					else {
						list.Capacity = capacity;
						//Note, pool.Count-1-i should not be written since i is incremented at end of above loop.
						//Swap current item and last item to enable more efficient remove
						pool[pool.Count-i] = pool[pool.Count-1];
						pool.RemoveAt(pool.Count-1);
					}
					return list;
				} else {
					return new List<T>(capacity);
				}
			}
		}
		
		/** Makes sure the pool contains at least \a count pooled items with capacity \a size.
		 * This is good if you want to do all allocations at start.
		 */
		public static void Warmup (int count, int size) {
			lock (pool) {
				List<T>[] tmp = new List<T>[count];
				for (int i=0;i<count;i++) tmp[i] = Claim (size);
				for (int i=0;i<count;i++) Release (tmp[i]);
			}
		}
		
		/** Releases a list.
		 * After the list has been released it should not be used anymore.
		 * 
		 * \throws System.InvalidOperationException
		 * Releasing a list when it has already been released will cause an exception to be thrown.
		 * 
		 * \see Claim
		 */
		public static void Release (List<T> list) {
			
			list.Clear ();
			
			lock (pool) {
				for (int i=0;i<pool.Count;i++)
					if (pool[i] == list)
						throw new System.InvalidOperationException ("The List is released even though it is in the pool");
			
				pool.Add (list);
			}
		}
		
		/** Clears the pool for lists of this type.
		 * This is an O(n) operation, where n is the number of pooled lists.
		 */
		public static void Clear () {
			lock (pool) {
				pool.Clear ();
			}
		}
		
		/** Number of lists of this type in the pool */
		public static int GetSize () {
			//No lock required since int writes are atomic
			return pool.Count;
		}
	}
}

