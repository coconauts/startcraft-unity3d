using System.Threading;

namespace Pathfinding {
	/// <summary>Queue of paths to be processed by the system</summary>
	class ThreadControlQueue {
		public class QueueTerminationException : System.Exception {
		}

		Path head;
		Path tail;

		readonly System.Object lockObj = new System.Object();

		readonly int numReceivers;

		bool blocked;

		/// <summary>
		/// Number of receiver threads that are currently blocked.
		/// This is only modified while a thread has a lock on lockObj
		/// </summary>
		int blockedReceivers;

		/// <summary>
		/// True while head == null.
		/// This is only modified while a thread has a lock on lockObj
		/// </summary>
		bool starving;

		/// <summary>
		/// True after TerminateReceivers has been called.
		/// All receivers will be terminated when they next call Pop.
		/// </summary>
		bool terminate;

		ManualResetEvent block = new ManualResetEvent(true);

		/// <summary>
		/// Create a new queue with the specified number of receivers.
		/// It is important that the number of receivers is fixed.
		/// Properties like AllReceiversBlocked rely on knowing the exact number of receivers using the Pop (or PopNoBlock) methods.
		/// </summary>
		public ThreadControlQueue (int numReceivers) {
			this.numReceivers = numReceivers;
		}

		/// <summary>True if the queue is empty</summary>
		public bool IsEmpty {
			get {
				return head == null;
			}
		}

		/// <summary>True if TerminateReceivers has been called</summary>
		public bool IsTerminating {
			get {
				return terminate;
			}
		}

		/// <summary>Block queue, all calls to Pop will block until Unblock is called</summary>
		public void Block () {
			lock (lockObj) {
				blocked = true;
				block.Reset();
			}
		}

		/// <summary>
		/// Unblock queue.
		/// Calls to Pop will not block anymore.
		/// See: Block
		/// </summary>
		public void Unblock () {
			lock (lockObj) {
				blocked = false;
				block.Set();
			}
		}

		/// <summary>
		/// Aquires a lock on this queue.
		/// Must be paired with a call to <see cref="Unlock"/>
		/// </summary>
		public void Lock () {
			Monitor.Enter(lockObj);
		}

		/// <summary>Releases the lock on this queue</summary>
		public void Unlock () {
			Monitor.Exit(lockObj);
		}

		/// <summary>True if blocking and all receivers are waiting for unblocking</summary>
		public bool AllReceiversBlocked {
			get {
				lock (lockObj) {
					return blocked && blockedReceivers == numReceivers;
				}
			}
		}

		/// <summary>Push a path to the front of the queue</summary>
		public void PushFront (Path path) {
			lock (lockObj) {
				// If termination is due, why add stuff to a queue which will not be read from anyway
				if (terminate) return;

				if (tail == null) {// (tail == null) ==> (head == null)
					head = path;
					tail = path;

					if (starving && !blocked) {
						starving = false;
						block.Set();
					} else {
						starving = false;
					}
				} else {
					path.next = head;
					head = path;
				}
			}
		}

		/// <summary>Push a path to the end of the queue</summary>
		public void Push (Path path) {
			lock (lockObj) {
				// If termination is due, why add stuff to a queue which will not be read from anyway
				if (terminate) return;

				if (tail == null) {// (tail == null) ==> (head == null)
					head = path;
					tail = path;

					if (starving && !blocked) {
						starving = false;
						block.Set();
					} else {
						starving = false;
					}
				} else {
					tail.next = path;
					tail = path;
				}
			}
		}

		void Starving () {
			starving = true;
			block.Reset();
		}

		/// <summary>All calls to Pop and PopNoBlock will now generate exceptions</summary>
		public void TerminateReceivers () {
			lock (lockObj) {
				terminate = true;
				block.Set();
			}
		}

		/// <summary>
		/// Pops the next item off the queue.
		/// This call will block if there are no items in the queue or if the queue is currently blocked.
		///
		/// Returns: A Path object, guaranteed to be not null.
		/// \throws QueueTerminationException if <see cref="TerminateReceivers"/> has been called.
		/// \throws System.InvalidOperationException if more receivers get blocked than the fixed count sent to the constructor
		/// </summary>
		public Path Pop () {
			Monitor.Enter(lockObj);
			try {
				if (terminate) {
					blockedReceivers++;
					throw new QueueTerminationException();
				}

				if (head == null) {
					Starving();
				}

				while (blocked || starving) {
					blockedReceivers++;

					if (blockedReceivers > numReceivers) {
						throw new System.InvalidOperationException("More receivers are blocked than specified in constructor ("+blockedReceivers + " > " + numReceivers+")");
					}

					Monitor.Exit(lockObj);

					block.WaitOne();

					Monitor.Enter(lockObj);

					if (terminate) {
						throw new QueueTerminationException();
					}

					blockedReceivers--;

					if (head == null) {
						Starving();
					}
				}
				Path p = head;

				var newHead = head.next;
				if (newHead == null) {
					tail = null;
				}
				head.next = null;
				head = newHead;
				return p;
			} finally {
				Monitor.Exit(lockObj);
			}
		}

		/// <summary>
		/// Call when a receiver was terminated in other ways than by a QueueTerminationException.
		///
		/// After this call, the receiver should be dead and not call anything else in this class.
		/// </summary>
		public void ReceiverTerminated () {
			Monitor.Enter(lockObj);
			blockedReceivers++;
			Monitor.Exit(lockObj);
		}

		/// <summary>
		/// Pops the next item off the queue, this call will not block.
		/// To ensure stability, the caller must follow this pattern.
		/// 1. Call PopNoBlock(false), if a null value is returned, wait for a bit (e.g yield return null in a Unity coroutine)
		/// 2. try again with PopNoBlock(true), if still null, wait for a bit
		/// 3. Repeat from step 2.
		///
		/// \throws QueueTerminationException if <see cref="TerminateReceivers"/> has been called.
		/// \throws System.InvalidOperationException if more receivers get blocked than the fixed count sent to the constructor
		/// </summary>
		public Path PopNoBlock (bool blockedBefore) {
			Monitor.Enter(lockObj);
			try {
				if (terminate) {
					blockedReceivers++;
					throw new QueueTerminationException();
				}

				if (head == null) {
					Starving();
				}
				if (blocked || starving) {
					if (!blockedBefore) {
						blockedReceivers++;

						if (terminate) throw new QueueTerminationException();

						if (blockedReceivers == numReceivers) {
							//Last alive
						} else if (blockedReceivers > numReceivers) {
							throw new System.InvalidOperationException("More receivers are blocked than specified in constructor ("+blockedReceivers + " > " + numReceivers+")");
						}
					}
					return null;
				}
				if (blockedBefore) {
					blockedReceivers--;
				}

				Path p = head;

				var newHead = head.next;
				if (newHead == null) {
					tail = null;
				}
				head.next = null;
				head = newHead;
				return p;
			} finally {
				Monitor.Exit(lockObj);
			}
		}
	}
}
