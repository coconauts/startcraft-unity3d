using UnityEngine;
using System.Collections;
using System.Threading;
using Pathfinding;

namespace Pathfinding {
	public class ThreadControlQueue {
		
		public class QueueTerminationException : System.Exception {
		}
		
		Path head;
		Path tail;
		
		System.Object lockObj = new System.Object();
		
		int numReceivers;
		
		bool blocked = false;
		int blockedReceivers = 0;
		bool starving = false;
		bool terminate = false;
		
		ManualResetEvent block = new ManualResetEvent(true);
		
		/** Create a new queue with the specified number of receivers.
		 * It is important that the number of receivers is fixed.
		 * Properties like AllReceiversBlocked rely on knowing the exact number of receivers using the Pop (or PopNoBlock) methods.
		 */
		public ThreadControlQueue (int numReceivers) {
			this.numReceivers = numReceivers;
		}
		
		/** True if the queue is empty */
		public bool IsEmpty {
			get {
				return head == null;
			}
		}
		
		/** True if TerminateReceivers has been called */
		public bool IsTerminating {
			get {
				return terminate;
			}
		}
		
		/** Block queue, all calls to Pop will block until Unblock is called */
		public void Block () {
			lock (lockObj) {
				blocked = true;
				block.Reset();
			}
		}
		
		/** Unblock queue.
		 * Calls to Pop will not block anymore.
		 * \see Block
		 */
		public void Unblock () {
			lock (lockObj) {
				blocked = false;
				block.Set();
			}
		}
		
		/** Aquires a lock on this queue.
		  * Must be paired with a call to #Unlock */
		public void Lock () {
			Monitor.Enter(lockObj);
		}
		
		/** Releases the lock on this queue */
		public void Unlock () {
			Monitor.Exit(lockObj);
		}
		
		/** True if blocking and all receivers are waiting for unblocking */
		public bool AllReceiversBlocked {
			get {
				return blocked && blockedReceivers == numReceivers;
			}
		}
		
		/** Push a path to the front of the queue */
		public void PushFront (Path p) {
			//If termination is due, why add stuff to a queue which will not be read from anyway
			if (terminate) return;
			
			lock (lockObj) {
				if (tail == null) {// (tail == null) ==> (head == null)
					head = p;
					tail = p;
					
					if (starving && !blocked) {
						starving = false;
						block.Set();
					} else {
						starving = false;
					}
				} else {
					p.next = head;
					head = p;
				}
			}
		}
		
		/** Push a path to the end of the queue */
		public void Push (Path p) {
			//If termination is due, why add stuff to a queue which will not be read from anyway
			if (terminate) return;
			
			lock (lockObj) {
				if (tail == null) {// (tail == null) ==> (head == null)
					head = p;
					tail = p;
					
					if (starving && !blocked) {
						starving = false;
						block.Set();
					} else {
						starving = false;
					}
				} else {
					tail.next = p;
					tail = p;
				}
			}
		}
		
		void Starving () {
			starving = true;
			block.Reset();
		}
		
		/** All calls to Pop and PopNoBlock will now generate exceptions */
		public void TerminateReceivers () {
			terminate = true;
			block.Set();
		}
		
		/** Pops the next item off the queue.
		  * This call will block if there are no items in the queue or if the queue is currently blocked.
		  * 
		  * \returns A Path object, guaranteed to be not null.
		  * \throws QueueTerminationException if #TerminateReceivers has been called.
		  * \throws System.InvalidOperationException if more receivers get blocked than the fixed count sent to the constructor
		  */
		public Path Pop () {
			
			Monitor.Enter(lockObj);
			try {
				if (terminate) {
					blockedReceivers++;
					throw new QueueTerminationException();
				}
				
				if (head == null) {
					Starving ();
				}
				
				while (blocked || starving) {
					blockedReceivers++;
					
					if (terminate) throw new QueueTerminationException();
					
					if (blockedReceivers == numReceivers) {
						//Last alive
						
					} else if (blockedReceivers > numReceivers) {
						throw new System.InvalidOperationException ("More receivers are blocked than specified in constructor ("+blockedReceivers + " > " + numReceivers+")");
					}
					
					Monitor.Exit (lockObj);
					block.WaitOne();
					Monitor.Enter (lockObj);
					blockedReceivers--;
					
					if (head == null) {
						Starving ();
					}
				}
				Path p = head;
				
				if (head.next == null) {
					tail = null;
				}
				head = head.next;
				return p;
			} finally {
				Monitor.Exit(lockObj);
			}
		}
		
		/** Call when a receiver was terminated in other ways than by a QueueTerminationException.
		 * 
		 * After this call, the receiver should be dead and not call anything else in this class.
		 */
		public void ReceiverTerminated () {
			Monitor.Enter(lockObj);
			blockedReceivers++;
			Monitor.Exit (lockObj);
		}
		
		/** Pops the next item off the queue, this call will not block.
		 * To ensure stability, the caller must follow this pattern.
		 * 1. Call PopNoBlock(false), if a null value is returned, wait for a bit (e.g yield return null in a Unity coroutine)
		 * 2. try again with PopNoBlock(true), if still null, wait for a bit
		 * 3. Repeat from step 2.
		 * 
		 * \throws QueueTerminationException if #TerminateReceivers has been called.
		 * \throws System.InvalidOperationException if more receivers get blocked than the fixed count sent to the constructor
		 */
		public Path PopNoBlock (bool blockedBefore) {
			
			Monitor.Enter(lockObj);
			try {
				if (terminate) {
					blockedReceivers++;
					throw new QueueTerminationException();
				}
				
				if (head == null) {
					Starving ();
				}
				if (blocked || starving) {
					if (!blockedBefore) {
						blockedReceivers++;
						
						if (terminate) throw new QueueTerminationException();
						
						if (blockedReceivers == numReceivers) {
							//Last alive
						} else if (blockedReceivers > numReceivers) {
							throw new System.InvalidOperationException ("More receivers are blocked than specified in constructor ("+blockedReceivers + " > " + numReceivers+")");
						}
					}
					return null;
				} else if (blockedBefore) {
					blockedReceivers--;
				}
				
				Path p = head;
				
				if (head.next == null) {
					tail = null;
				}
				head = head.next;
				return p;
			} finally {
				Monitor.Exit (lockObj);
			}
		}
	}
}