//#define ASTAR_NO_POOLING //Disable pooling for some reason. Could be debugging or just for measuring the difference.
//#define ASTAR_OPTIMIZE_POOLING //Skip some error checking for pooling. Optimizes Release calls to O(1) instead of O(n) where n is the number of objects in the pool.

using System;
using System.Collections.Generic;

namespace Pathfinding.Util
{
	
	public interface IAstarPooledObject {
		void OnEnterPool ();
	}
	
	/** Lightweight object Pool.
	 * Handy class for pooling objects of type T.
	 * 
	 * Usage:
	 * - Claim a new object using \code SomeClass foo = ObjectPool<SomeClass>.Claim (); \endcode
	 * - Use it and do stuff with it
	 * - Release it with \code ObjectPool<SomeClass>.Release (foo); \endcode
	 * 
	 * After you have released a object, you should never use it again.
	 * 
	 * \warning This class is not thread safe
	 * 
	 * \since Version 3.2
	 * \see Pathfinding.Util.ListPool
	 */
	public static class ObjectPool<T> where T : class, IAstarPooledObject, new()
	{
		/** Internal pool */
		static List<T> pool;
		
		/** Static constructor initializing the internal pool list */
		static ObjectPool ()
		{
			pool = new List<T> ();
		}
		
		/** Claim a object.
		 * Returns a pooled object if any are in the pool.
		 * Otherwise it creates a new one.
		 * After usage, this object should be released using the Release function (though not strictly necessary).
		 */
		public static T Claim () {
			if (pool.Count > 0) {
				T ls = pool[pool.Count-1];
				pool.RemoveAt(pool.Count-1);
				return ls;
			} else {
				return new T ();
			}
		}
		
		/** Makes sure the pool contains at least \a count pooled items with capacity \a size.
		 * This is good if you want to do all allocations at start.
		 */
		public static void Warmup (int count) {
			T[] tmp = new T[count];
			for (int i=0;i<count;i++) tmp[i] = Claim ();
			for (int i=0;i<count;i++) Release (tmp[i]);
		}
		
		/** Releases an object.
		 * After the object has been released it should not be used anymore.
		 * 
		 * \throws System.InvalidOperationException
		 * Releasing an object when it has already been released will cause an exception to be thrown.
		 * However enabling ASTAR_OPTIMIZE_POOLING will prevent this check, making this function an O(1) operation instead of O(n).
		 * 
		 * \see Claim
		 */
		public static void Release (T obj) {
			
			for (int i=0;i<pool.Count;i++)
				if (pool[i] == obj)
					throw new System.InvalidOperationException ("The object is released even though it is in the pool. Are you releasing it twice?");
			obj.OnEnterPool();
			pool.Add (obj);
		}
		
		/** Clears the pool for objects of this type.
		 * This is an O(n) operation, where n is the number of pooled objects.
		 */
		public static void Clear () {
			pool.Clear ();
		}
		
		/** Number of objects of this type in the pool */
		public static int GetSize () {
			return pool.Count;
		}
	}
}

