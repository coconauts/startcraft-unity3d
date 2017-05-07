//#define ASTAR_NO_JSON

using System;
using UnityEngine;
using Pathfinding.Serialization.JsonFx;

using System.Collections.Generic;


namespace Pathfinding.Serialization
{
	
	public class UnityObjectConverter : JsonConverter {

		public override bool CanConvert (Type type) {
			return typeof(UnityEngine.Object).IsAssignableFrom (type);
		}
		
		public override object ReadJson ( Type objectType, Dictionary<string,object> values) {
			
			if (values == null) return null;
			
			
			//int instanceID = (int)values["InstanceID"];
			
			string name = (string)values["Name"];
			
			string typename = (string)values["Type"];
			Type type = Type.GetType (typename);
			
			if (System.Type.Equals (type, null)) {
				Debug.LogError ("Could not find type '"+typename+"'. Cannot deserialize Unity reference");
				return null;
			}
			
			if (values.ContainsKey ("GUID")) {
				string guid = (string)values["GUID"];
				
				UnityReferenceHelper[] helpers = UnityEngine.Object.FindObjectsOfType(typeof(UnityReferenceHelper)) as UnityReferenceHelper[];
				
				for (int i=0;i<helpers.Length;i++) {
					if (helpers[i].GetGUID () == guid) {
						if (System.Type.Equals ( type, typeof(GameObject) )) {
							return helpers[i].gameObject;
						} else {
							return helpers[i].GetComponent (type);
						}
					}
				}
				
			}
			
			//Try to load from resources
			UnityEngine.Object[] objs = Resources.LoadAll (name,type);
			
			for (int i=0;i<objs.Length;i++) {
				if (objs[i].name == name || objs.Length == 1) {
					return objs[i];
				}
			}
			
			return null;
		}
		
		public override Dictionary<string,object> WriteJson (Type type, object value) {
			UnityEngine.Object obj = (UnityEngine.Object)value;
			
			
			Dictionary<string, object> dict = new Dictionary<string, object>();
			
			dict.Add ("InstanceID",obj.GetInstanceID());
			dict.Add ("Name",obj.name);
			dict.Add ("Type",obj.GetType().AssemblyQualifiedName);
			
			//Write scene path if the object is a Component or GameObject
			Component component = value as Component;
			GameObject go = value as GameObject;
			
			if (component != null || go != null) {
				if (component != null && go == null) {
					go = component.gameObject;
				}
				
				UnityReferenceHelper helper = go.GetComponent<UnityReferenceHelper>();
				
				if (helper == null) {
					Debug.Log ("Adding UnityReferenceHelper to Unity Reference '"+obj.name+"'");
					helper = go.AddComponent<UnityReferenceHelper>();
				}
				
				//Make sure it has a unique GUID
				helper.Reset ();
				
				dict.Add ("GUID",helper.GetGUID ());
			}
			return dict;
		}
	}
	
	public class GuidConverter : JsonConverter {
		public override bool CanConvert (Type type) {
			return System.Type.Equals ( type, typeof(Pathfinding.Util.Guid) );
		}
		
		public override object ReadJson ( Type objectType, Dictionary<string,object> values) {
			
			string s = (string)values["value"];
			return new Pathfinding.Util.Guid(s);
		}
		
		public override Dictionary<string,object> WriteJson (Type type, object value) {
			Pathfinding.Util.Guid m = (Pathfinding.Util.Guid)value;
			return new Dictionary<string, object>() {{"value",m.ToString()}};
		}
	}
	
	public class MatrixConverter : JsonConverter {
		public override bool CanConvert (Type type) {
			return System.Type.Equals ( type, typeof(Matrix4x4) );
		}
		
		public override object ReadJson ( Type objectType, Dictionary<string,object> values) {
			Matrix4x4 m = new Matrix4x4();
			
			Array arr = (Array)values["values"];
			if (arr.Length != 16) {
				Debug.LogError ("Number of elements in matrix was not 16 (got "+arr.Length+")");
				return m;
			}

			for (int i=0;i<16;i++) m[i] = System.Convert.ToSingle (arr.GetValue(new int[] {i}));
			
			return m;
		}
		
		/** Just a temporary array of 16 floats.
		 * Stores the elements of the matrices temporarily */
		float[] values = new float[16];
		
		public override Dictionary<string,object> WriteJson (Type type, object value) {
			Matrix4x4 m = (Matrix4x4)value;
			for (int i=0;i<values.Length;i++) values[i] = m[i];
			
			return new Dictionary<string, object>() {
				{"values",values}
			};
		}
	}
	
	public class BoundsConverter : JsonConverter {
		public override bool CanConvert (Type type) {
			return System.Type.Equals ( type, typeof(Bounds) );
		}
		
		public override object ReadJson ( Type objectType, Dictionary<string,object> values) {
			Bounds b = new Bounds();
			b.center = new Vector3(	CastFloat(values["cx"]),CastFloat(values["cy"]),CastFloat(values["cz"]));
			b.extents = new Vector3(CastFloat(values["ex"]),CastFloat(values["ey"]),CastFloat(values["ez"]));
			return b;
		}
		
		public override Dictionary<string,object> WriteJson (Type type, object value) {
			Bounds b = (Bounds)value;
			return new Dictionary<string, object>() {
				{"cx",b.center.x},
				{"cy",b.center.y},
				{"cz",b.center.z},
				{"ex",b.extents.x},
				{"ey",b.extents.y},
				{"ez",b.extents.z}
			};
		}
	}
	
	public class LayerMaskConverter : JsonConverter {
		public override bool CanConvert (Type type) {
			return System.Type.Equals ( type, typeof(LayerMask) );
		}
		
		public override object ReadJson (Type type, Dictionary<string,object> values) {
			return (LayerMask)(int)values["value"];
		}
		
		public override Dictionary<string,object> WriteJson (Type type, object value) {
			return new Dictionary<string, object>() {{"value",((LayerMask)value).value}};
		}
	}
	
	public class VectorConverter : JsonConverter
	{
		public override bool CanConvert (Type type) {
			return System.Type.Equals ( type, typeof(Vector2) ) || System.Type.Equals ( type, typeof(Vector3) )||System.Type.Equals ( type, typeof(Vector4) );
		}
		
		public override object ReadJson (Type type, Dictionary<string,object> values) {
			if (System.Type.Equals ( type, typeof(Vector2) )) {
				return new Vector2(CastFloat(values["x"]),CastFloat(values["y"]));
			} else if (System.Type.Equals ( type, typeof(Vector3) )) {
				return new Vector3(CastFloat(values["x"]),CastFloat(values["y"]),CastFloat(values["z"]));
			} else if (System.Type.Equals ( type, typeof(Vector4) )) {
				return new Vector4(CastFloat(values["x"]),CastFloat(values["y"]),CastFloat(values["z"]),CastFloat(values["w"]));
			} else {
				throw new System.NotImplementedException ("Can only read Vector2,3,4. Not objects of type "+type);
			}
		}
		
		public override Dictionary<string,object> WriteJson (Type type, object value) {
			if (System.Type.Equals ( type, typeof(Vector2) )) {
				Vector2 v = (Vector2)value;
				return new Dictionary<string, object>() {
					{"x",v.x},
					{"y",v.y}
				};
			} else if (System.Type.Equals ( type, typeof(Vector3) )) {
				Vector3 v = (Vector3)value;
				return new Dictionary<string, object>() {
					{"x",v.x},
					{"y",v.y},
					{"z",v.z}
				};
			} else if (System.Type.Equals ( type, typeof(Vector4) )) {
				Vector4 v = (Vector4)value;
				return new Dictionary<string, object>() {
					{"x",v.x},
					{"y",v.y},
					{"z",v.z},
					{"w",v.w}
				};
			}
			throw new System.NotImplementedException ("Can only write Vector2,3,4. Not objects of type "+type);
		}
	}
	
	/** Enables json serialization of dictionaries with integer keys.
	 */
	public class IntKeyDictionaryConverter : JsonConverter {
        public override bool CanConvert (Type type) {
            return ( System.Type.Equals (type, typeof(Dictionary<int,int>)) || System.Type.Equals (type, typeof(SortedDictionary<int,int>)) );
        }
        
        public override object ReadJson (Type type, Dictionary<string,object> values) {
            Dictionary<int, int> holder = new Dictionary<int, int>();
            
            foreach ( KeyValuePair<string, object> val in values )
            {
                holder.Add( System.Convert.ToInt32(val.Key), System.Convert.ToInt32(val.Value) );
            }
            return holder;
        }
        
        public override Dictionary<string,object> WriteJson (Type type, object value ) {
            Dictionary<string, object> holder = new Dictionary<string, object>();
            Dictionary<int, int> d = (Dictionary<int,int>)value;
            
            foreach ( KeyValuePair<int, int> val in d )
            {
                holder.Add( val.Key.ToString(), val.Value );
            }
            return holder;
        }
    }
}
