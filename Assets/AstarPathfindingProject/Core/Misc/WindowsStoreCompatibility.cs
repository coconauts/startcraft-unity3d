using UnityEngine;
using System.Collections;
using System;
using TP = System.Type;

namespace Pathfinding.WindowsStore {
	public class WindowsStoreCompatibility {
		public static System.Type GetTypeFromInfo ( TP type ) {
			return type;
		}

		public static TP GetTypeInfo ( System.Type type ) {
			return type;
		}
	}
	
}