using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Pathfinding {
	/** GraphModifier for modifying graphs or processing graph data based on events.
	 * This class is a simple container for a number of events.
	 * 
	 * \warning Some events will be called both in play mode <b>and in editor mode</b> (at least the scan events).
	 * So make sure your code handles both cases well. You may choose to ignore editor events.
	 * \see Application.IsPlaying
	 */
	public abstract class GraphModifier : MonoBehaviour {
		
		/** All active graph modifiers */
		private static GraphModifier root;
		
		private GraphModifier prev;
		private GraphModifier next;
		
		public static void FindAllModifiers () {
			GraphModifier[] arr = FindObjectsOfType(typeof(GraphModifier)) as GraphModifier[];
			for (int i=0;i<arr.Length;i++) {
				arr[i].OnEnable();
			}
		}
		
		/** GraphModifier event type.
		 * \see GraphModifier */
		public enum EventType {
			PostScan = 1 << 0,
			PreScan = 1 << 1,
			LatePostScan = 1 << 2,
			PreUpdate = 1 << 3,
			PostUpdate = 1 << 4,
			PostCacheLoad = 1 << 5
		}
		
		/** Triggers an event for all active graph modifiers */
		public static void TriggerEvent (GraphModifier.EventType type) {
			
			if (!Application.isPlaying) {
				FindAllModifiers ();
			}
			
			GraphModifier c = root;
			switch (type){
			case EventType.PreScan:
					while (c != null) { c.OnPreScan(); c = c.next; }
					break;
			case EventType.PostScan:
					while (c != null) { c.OnPostScan(); c = c.next; }
					break;
			case EventType.LatePostScan:
					while (c != null) { c.OnLatePostScan(); c = c.next; }
					break;
			case EventType.PreUpdate:
					while (c != null) { c.OnGraphsPreUpdate(); c = c.next; }
					break;
			case EventType.PostUpdate:
					while (c != null) { c.OnGraphsPostUpdate(); c = c.next; }
					break;
			case EventType.PostCacheLoad:
					while (c != null) { c.OnPostCacheLoad(); c = c.next; }
					break;
			}
		}
		
		/** Adds this modifier to list of active modifiers.
		 */
		protected virtual void OnEnable () {
			OnDisable();

			if (root == null) {
				root = this;
			} else {
				this.next = root;
				root.prev = this;
				root = this;
			}
		}
		
		/** Removes this modifier from list of active modifiers */
		protected virtual void OnDisable () {
			if (root == this) {
				root = this.next;
				if (root != null) root.prev = null;
			} else {
				if (prev != null) prev.next = next;
				if (next != null) next.prev = prev;
			}
			prev = null;
			next = null;
		}
		
		/* Called just before a graph is scanned.
		  * Note that some other graphs might already be scanned */
		//public virtual void OnGraphPreScan (NavGraph graph) {}
		
		/* Called just after a graph has been scanned.
		  * Note that some other graphs might not have been scanned at this point. */
		//public virtual void OnGraphPostScan (NavGraph graph) {}
		
		/** Called right after all graphs have been scanned.
		 * FloodFill and other post processing has not been done.
		 * 
		 * \warning Since OnEnable and Awake are called roughly in the same time, the only way
		 * to ensure that these scripts get this call when scanning in Awake is to
		 * set the Script Execution Order for AstarPath to some time later than default time
		 * (see Edit -> Project Settings -> Script Execution Order).
		 * 
		 * \see OnLatePostScan
		 */
		public virtual void OnPostScan () {}
		
		/** Called right before graphs are going to be scanned.
		 * 
		 * \warning Since OnEnable and Awake are called roughly in the same time, the only way
		 * to ensure that these scripts get this call when scanning in Awake is to
		 * set the Script Execution Order for AstarPath to some time later than default time
		 * (see Edit -> Project Settings -> Script Execution Order).
		 * 
		 * \see OnLatePostScan
		 * */
		public virtual void OnPreScan () {}
		
		/** Called at the end of the scanning procedure.
		 * This is the absolute last thing done by Scan.
		 * 
		 * \note This event will be called even if Script Execution Order has messed things up
		 * (see the other two scan events).
		 */
		public virtual void OnLatePostScan () {}
		
		/** Called after cached graphs have been loaded.
		 * When using cached startup, this event is analogous to OnLatePostScan and implementing scripts
		 * should do roughly the same thing for both events.
		 * 
		 * \note This event will be called even if Script Execution Order has messed things up
		 * (see the other two scan events).
		 */
		public virtual void OnPostCacheLoad () {}
		
		/** Called before graphs are updated using GraphUpdateObjects */
		public virtual void OnGraphsPreUpdate () {}
		
		/** Called after graphs have been updated using GraphUpdateObjects.
		  * Eventual flood filling has been done */
		public virtual void OnGraphsPostUpdate () {}
	}
}