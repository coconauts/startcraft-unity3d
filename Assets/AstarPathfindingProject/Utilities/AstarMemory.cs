using System;

namespace Pathfinding.Util {
	/// <summary>Various utilities for handling arrays and memory</summary>
	public static class Memory {
		/// <summary>
		/// Sets all values in an array to a specific value faster than a loop.
		/// Only faster for large arrays. Slower for small ones.
		/// Tests indicate it becomes faster somewhere when the length of the array grows above around 100.
		/// For large arrays this can be magnitudes faster. Up to 40 times faster has been measured.
		///
		/// Note: Only works on primitive value types such as int, long, float, etc.
		///
		/// <code>
		/// //Set all values to 8 in the array
		/// int[] arr = new int[20000];
		/// Pathfinding.Util.Memory.MemSet<int> (arr, 8, sizeof(int));
		/// </code>
		/// See: System.Buffer.BlockCopy
		/// </summary>
		/// <param name="array">the array to fill</param>
		/// <param name="value">the value to fill the array with</param>
		/// <param name="byteSize">size in bytes of every element in the array. e.g 4 bytes for an int, or 8 bytes for a long.
		/// It can be efficiently got using the sizeof built-in function.</param>
		public static void MemSet<T>(T[] array, T value, int byteSize) where T : struct {
			if (array == null) {
				throw new ArgumentNullException("array");
			}

			int block = 32, index = 0;
			int length = Math.Min(block, array.Length);

			//Fill the initial array
			while (index < length) {
				array[index] = value;
				index++;
			}

			length = array.Length;
			while (index < length) {
				Buffer.BlockCopy(array, 0, array, index*byteSize, Math.Min(block, length-index)*byteSize);
				index += block;
				block *= 2;
			}
		}

		/// <summary>
		/// Sets all values in an array to a specific value faster than a loop.
		/// Only faster for large arrays. Slower for small ones.
		/// Tests indicate it becomes faster somewhere when the length of the array grows above around 100.
		/// For large arrays this can be magnitudes faster. Up to 40 times faster has been measured.
		///
		/// Note: Only works on primitive value types such as int, long, float, etc.
		///
		/// It can be efficiently got using the sizeof built-in function.
		///
		/// <code>
		/// //Set all values to 8 in the array
		/// int[] arr = new int[20000];
		/// Pathfinding.Util.Memory.MemSet<int> (arr, 8, sizeof(int));
		/// </code>
		/// See: System.Buffer.BlockCopy
		/// </summary>
		/// <param name="array">the array to fill</param>
		/// <param name="value">the value to fill the array with</param>
		/// <param name="byteSize">size in bytes of every element in the array. e.g 4 bytes for an int, or 8 bytes for a long.</param>
		/// <param name="totalSize">all indices in the range [0, totalSize-1] will be set</param>
		public static void MemSet<T>(T[] array, T value, int totalSize, int byteSize) where T : struct {
			if (array == null) {
				throw new ArgumentNullException("array");
			}

			int block = 32, index = 0;
			int length = Math.Min(block, totalSize);

			//Fill the initial array
			while (index < length) {
				array[index] = value;
				index++;
			}

			length = totalSize;
			while (index < length) {
				Buffer.BlockCopy(array, 0, array, index*byteSize, Math.Min(block, totalSize-index)*byteSize);
				index += block;
				block *= 2;
			}
		}

		/// <summary>
		/// Returns a new array with at most length newLength.
		/// The array will contain a copy of all elements of arr up to but excluding the index newLength.
		/// </summary>
		public static T[] ShrinkArray<T>(T[] arr, int newLength) {
			newLength = Math.Min(newLength, arr.Length);
			var shrunkArr = new T[newLength];
			Array.Copy(arr, shrunkArr, newLength);
			return shrunkArr;
		}

		/// <summary>Swaps the variables a and b</summary>
		public static void Swap<T>(ref T a, ref T b) {
			T tmp = a;

			a = b;
			b = tmp;
		}
	}
}
