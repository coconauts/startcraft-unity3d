//#define ASTARDEBUG
#define TUPLE
#pragma warning disable 162
#pragma warning disable 429
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

namespace Pathfinding {
//Binary Heap
	
	/** Binary heap implementation. Binary heaps are really fast for ordering nodes in a way that makes it possible to get the node with the lowest F score. Also known as a priority queue.
	 * \see http://en.wikipedia.org/wiki/Binary_heap
	 */
	public class BinaryHeapM { 

		public int numberOfItems; 
		
		public float growthFactor = 2;

		public const int D = 4;

		private PathNode[] binaryHeap; 

		public BinaryHeapM ( int numberOfElements ) { 
			binaryHeap = new PathNode[numberOfElements]; 
			numberOfItems = 0;
		}
		
		public void Clear () {
			numberOfItems = 0;
		}
		
		public PathNode GetNode (int i) {
			return binaryHeap[i];
		}

		/** Adds a node to the heap */
		public void Add(PathNode node) {
			
			if (node == null) throw new System.ArgumentNullException ("Sending null node to BinaryHeap");

			if (numberOfItems == binaryHeap.Length) {
				int newSize = System.Math.Max(binaryHeap.Length+4,(int)System.Math.Round(binaryHeap.Length*growthFactor));
				if (newSize > 1<<18) {
					throw new System.Exception ("Binary Heap Size really large (2^18). A heap size this large is probably the cause of pathfinding running in an infinite loop. " +
						"\nRemove this check (in BinaryHeap.cs) if you are sure that it is not caused by a bug");
				}

				PathNode[] tmp = new PathNode[newSize];

				for (int i=0;i<binaryHeap.Length;i++) {
					tmp[i] = binaryHeap[i];
				}
				binaryHeap = tmp;
				
				//Debug.Log ("Forced to discard nodes because of binary heap size limit, please consider increasing the size ("+numberOfItems +" "+binaryHeap.Length+")");
				//numberOfItems--;
			}

			PathNode obj = node;
			binaryHeap[numberOfItems] = obj;

			//node.heapIndex = numberOfItems;//Heap index

			int bubbleIndex = numberOfItems;
			uint nodeF = node.F;
			//Debug.Log ( "Adding node with " + nodeF + " to index " + numberOfItems);
			
			while (bubbleIndex != 0 ) {
				int parentIndex = (bubbleIndex-1) / D;

				//Debug.Log ("Testing " + nodeF + " < " + binaryHeap[parentIndex].F);

				if (nodeF < binaryHeap[parentIndex].F) {

				   	
					//binaryHeap[bubbleIndex].f <= binaryHeap[parentIndex].f) { /* \todo Wouldn't it be more efficient with '<' instead of '<=' ? * /
					//Node tmpValue = binaryHeap[parentIndex];
					
					//tmpValue.heapIndex = bubbleIndex;//HeapIndex
					
					binaryHeap[bubbleIndex] = binaryHeap[parentIndex];
					binaryHeap[parentIndex] = obj;
					
					//binaryHeap[bubbleIndex].heapIndex = bubbleIndex; //Heap index
					//binaryHeap[parentIndex].heapIndex = parentIndex; //Heap index
					
					bubbleIndex = parentIndex;
				} else {
					break;
				}
			}

			numberOfItems++;

			//Validate();
		}
		
		/** Returns the node with the lowest F score from the heap */
		public PathNode Remove() {
			numberOfItems--;
			PathNode returnItem = binaryHeap[0];

		 	//returnItem.heapIndex = 0;//Heap index
			
			binaryHeap[0] = binaryHeap[numberOfItems];
			//binaryHeap[1].heapIndex = 1;//Heap index
			
			int swapItem = 0, parent = 0;
			
			do {

				if (D == 0) {
					parent = swapItem;
					int p2 = parent * D;
					if (p2 + 1 <= numberOfItems) {
						// Both children exist
						if (binaryHeap[parent].F > binaryHeap[p2].F) {
							swapItem = p2;//2 * parent;
						}
						if (binaryHeap[swapItem].F > binaryHeap[p2 + 1].F) {
							swapItem = p2 + 1;
						}
					} else if ((p2) <= numberOfItems) {
						// Only one child exists
						if (binaryHeap[parent].F > binaryHeap[p2].F) {
							swapItem = p2;
						}
					}
				} else {
					parent = swapItem;
					uint swapF = binaryHeap[swapItem].F;
					int pd = parent * D + 1;
					
					if (D >= 1 && pd+0 <= numberOfItems && binaryHeap[pd+0].F < swapF ) {
						swapF = binaryHeap[pd+0].F;
						swapItem = pd+0;
					}
					
					if (D >= 2 && pd+1 <= numberOfItems && binaryHeap[pd+1].F < swapF ) {
						swapF = binaryHeap[pd+1].F;
						swapItem = pd+1;
					}
					
					if (D >= 3 && pd+2 <= numberOfItems && binaryHeap[pd+2].F < swapF ) {
						swapF = binaryHeap[pd+2].F;
						swapItem = pd+2;
					}
					
					if (D >= 4 && pd+3 <= numberOfItems && binaryHeap[pd+3].F < swapF ) {
						swapF = binaryHeap[pd+3].F;
						swapItem = pd+3;
					}
					
					if (D >= 5 && pd+4 <= numberOfItems && binaryHeap[pd+4].F < swapF ) {
						swapF = binaryHeap[pd+4].F;
						swapItem = pd+4;
					}
					
					if (D >= 6 && pd+5 <= numberOfItems && binaryHeap[pd+5].F < swapF ) {
						swapF = binaryHeap[pd+5].F;
						swapItem = pd+5;
					}
					
					if (D >= 7 && pd+6 <= numberOfItems && binaryHeap[pd+6].F < swapF ) {
						swapF = binaryHeap[pd+6].F;
						swapItem = pd+6;
					}
					
					if (D >= 8 && pd+7 <= numberOfItems && binaryHeap[pd+7].F < swapF ) {
						swapF = binaryHeap[pd+7].F;
						swapItem = pd+7;
					}
					
					if (D >= 9 && pd+8 <= numberOfItems && binaryHeap[pd+8].F < swapF ) {
						swapF = binaryHeap[pd+8].F;
						swapItem = pd+8;
					}
				}
				
				// One if the parent's children are smaller or equal, swap them
				if (parent != swapItem) {
					var tmpIndex = binaryHeap[parent];
					//tmpIndex.heapIndex = swapItem;//Heap index
					
					binaryHeap[parent] = binaryHeap[swapItem];
					binaryHeap[swapItem] = tmpIndex;
					
					//binaryHeap[parent].heapIndex = parent;//Heap index
				} else {
					break;
				}
			} while (true);//parent != swapItem);

			//Validate ();

			return returnItem;
		}

		void Validate () {
			for ( int i = 1; i < numberOfItems; i++ ) {
				int parentIndex = (i-1)/D;
				if ( binaryHeap[parentIndex].F > binaryHeap[i].F ) {
					throw new System.Exception ("Invalid state at " + i + ":" +  parentIndex + " ( " + binaryHeap[parentIndex].F + " > " + binaryHeap[i].F + " ) " );
				}
			}
		}

		/** Rebuilds the heap by trickeling down all items.
		 * Usually called after the hTarget on a path has been changed */
		public void Rebuild () {
			
			for (int i=2;i<numberOfItems;i++) {
				int bubbleIndex = i;
				var node = binaryHeap[i];
				uint nodeF = node.F;
				while (bubbleIndex != 1) {
					int parentIndex = bubbleIndex / D;
					
					if (nodeF < binaryHeap[parentIndex].F) {
						//Node tmpValue = binaryHeap[parentIndex];
						binaryHeap[bubbleIndex] = binaryHeap[parentIndex];
						binaryHeap[parentIndex] = node;
						bubbleIndex = parentIndex;
					} else {
						break;
					}
				}
				
			}
			
			
		}
	}
}