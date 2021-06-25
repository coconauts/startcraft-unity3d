//#define ASTAR_NO_POOLING // Disable pooling for some reason. Maybe for debugging or just for measuring the difference.
using System;
using System.Collections.Generic;

namespace Pathfinding {
	/// <summary>Pools path objects to reduce load on the garbage collector</summary>
	public static class PathPool {
		static readonly Dictionary<Type, Stack<Path> > pool = new Dictionary<Type, Stack<Path> >();
		static readonly Dictionary<Type, int> totalCreated = new Dictionary<Type, int>();

		/// <summary>
		/// Adds a path to the pool.
		/// This function should not be used directly. Instead use the Path.Claim and Path.Release functions.
		/// </summary>
		public static void Pool (Path path) {
#if !ASTAR_NO_POOLING
			lock (pool) {
				if (((IPathInternals)path).Pooled) {
					throw new System.ArgumentException("The path is already pooled.");
				}

				Stack<Path> poolStack;
				if (!pool.TryGetValue(path.GetType(), out poolStack)) {
					poolStack = new Stack<Path>();
					pool[path.GetType()] = poolStack;
				}

				((IPathInternals)path).Pooled = true;
				((IPathInternals)path).OnEnterPool();
				poolStack.Push(path);
			}
#endif
		}

		/// <summary>Total created instances of paths of the specified type</summary>
		public static int GetTotalCreated (Type type) {
			int created;

			if (totalCreated.TryGetValue(type, out created)) {
				return created;
			} else {
				return 0;
			}
		}

		/// <summary>Number of pooled instances of a path of the specified type</summary>
		public static int GetSize (Type type) {
			Stack<Path> poolStack;

			if (pool.TryGetValue(type, out poolStack)) {
				return poolStack.Count;
			} else {
				return 0;
			}
		}

		/// <summary>Get a path from the pool or create a new one if the pool is empty</summary>
		public static T GetPath<T>() where T : Path, new() {
#if ASTAR_NO_POOLING
			T result = new T();
			((IPathInternals)result).Reset();
			return result;
#else
			lock (pool) {
				T result;
				Stack<Path> poolStack;
				if (pool.TryGetValue(typeof(T), out poolStack) && poolStack.Count > 0) {
					// Guaranteed to have the correct type
					result = poolStack.Pop() as T;
				} else {
					result = new T();

					// Make sure an entry for the path type exists
					if (!totalCreated.ContainsKey(typeof(T))) {
						totalCreated[typeof(T)] = 0;
					}

					totalCreated[typeof(T)]++;
				}

				((IPathInternals)result).Pooled = false;
				((IPathInternals)result).Reset();
				return result;
			}
#endif
		}
	}
}
