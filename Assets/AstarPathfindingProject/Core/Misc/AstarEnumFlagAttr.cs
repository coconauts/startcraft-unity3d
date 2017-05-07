using UnityEngine;

namespace Pathfinding {

	/** \author http://wiki.unity3d.com/index.php/EnumFlagPropertyDrawer */
	public class AstarEnumFlagAttribute : PropertyAttribute
	{
		public string enumName;
		
		public AstarEnumFlagAttribute() {}
		
		public AstarEnumFlagAttribute(string name)
		{
			enumName = name;
		}
	}
}