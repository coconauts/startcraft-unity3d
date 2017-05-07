using AmplitudeNS.MiniJSON;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_IPHONE
using System.Runtime.InteropServices;
#endif

public class Amplitude {

#if UNITY_ANDROID
	private static readonly string androidPluginName = "com.amplitude.unity.plugins.AmplitudePlugin";
	private AndroidJavaClass pluginClass;
#endif

	private static Amplitude instance;
	public bool logging = false;

#if UNITY_IPHONE
	[DllImport ("__Internal")]
	private static extern void _Amplitude_init(string apiKey, string userId);
	[DllImport ("__Internal")]
	private static extern void _Amplitude_logEvent(string evt, string propertiesJson);
	[DllImport ("__Internal")]
	private static extern void _Amplitude_logOutOfSessionEvent(string evt, string propertiesJson);
	[DllImport ("__Internal")]
	private static extern void _Amplitude_setUserId(string userId);
	[DllImport ("__Internal")]
	private static extern void _Amplitude_setUserProperties(string propertiesJson);
	[DllImport ("__Internal")]
	private static extern void _Amplitude_setOptOut(bool enabled);
	[DllImport ("__Internal")]
	private static extern void _Amplitude_logRevenueAmount(double amount);
	[DllImport ("__Internal")]
	private static extern void _Amplitude_logRevenue(string productIdentifier, int quantity, double price);
	[DllImport ("__Internal")]
	private static extern void _Amplitude_logRevenueWithReceipt(string productIdentifier, int quantity, double price, string receipt);
	[DllImport ("__Internal")]
	private static extern string _Amplitude_getDeviceId();
#endif

	public static Amplitude Instance {
		get
		{
			if(instance == null) {
				instance = new Amplitude();
			}

			return instance;
		}
	}

	public Amplitude() : base() {
#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			Debug.Log ("construct instance");
			pluginClass = new AndroidJavaClass(androidPluginName);
		}
#endif
	}

	protected void Log(string message) {
		if(!logging) return;

		Debug.Log(message);
	}

	public void init(string apiKey) {
		Log (string.Format("C# init {0}", apiKey));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_init(apiKey, null);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			using(AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) {
				using(AndroidJavaObject unityActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity")) {
					using(AndroidJavaObject unityApplication = unityActivity.Call<AndroidJavaObject>("getApplication")) {
						pluginClass.CallStatic("init", unityActivity, apiKey);
						pluginClass.CallStatic("enableForegroundTracking", unityApplication);
					}
				}
			}
		}
#endif
	}

	public void init(string apiKey, string userId) {
		Log (string.Format("C# init {0} with userId {1}", apiKey, userId));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_init(apiKey, userId);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			using(AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) {
				using(AndroidJavaObject unityActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity")) {
					using (AndroidJavaObject unityApplication = unityActivity.Call<AndroidJavaObject>("getApplication")) {
						pluginClass.CallStatic("init", unityActivity, apiKey, userId);
						pluginClass.CallStatic("enableForegroundTracking", unityApplication);
					}
				}
			}
		}
#endif
	}

	public void logEvent(string evt) {
		Log (string.Format("C# sendEvent {0}", evt));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_logEvent(evt, null);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			pluginClass.CallStatic("logEvent", evt);
		}
#endif
	}

	public void logEvent(string evt, IDictionary<string, object> properties) {
		string propertiesJson;
		if (properties != null) {
			propertiesJson = Json.Serialize(properties);
		} else {
			propertiesJson = Json.Serialize(new Dictionary<string, object>());
		}

		Log(string.Format("C# sendEvent {0} with properties {1}", evt, propertiesJson));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_logEvent(evt, propertiesJson);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			pluginClass.CallStatic("logEvent", evt, propertiesJson);
		}
#endif
	}

	public void logEvent(string evt, IDictionary<string, object> properties, bool outOfSession) {
		string propertiesJson;
		if (properties != null) {
			propertiesJson = Json.Serialize(properties);
		} else {
			propertiesJson = Json.Serialize(new Dictionary<string, object>());
		}

		Log(string.Format("C# sendEvent {0} with properties {1} and outOfSession {2}", evt, propertiesJson, outOfSession));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			if (outOfSession) {
				_Amplitude_logOutOfSessionEvent(evt, propertiesJson);
			} else {
				_Amplitude_logEvent(evt, propertiesJson);
			}
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			pluginClass.CallStatic("logEvent", evt, propertiesJson, outOfSession);
		}
#endif
	}

	public void setUserId(string userId) {
		Log (string.Format("C# setUserId {0}", userId));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_setUserId(userId);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			pluginClass.CallStatic("setUserId", userId);
		}
#endif
	}

	public void setUserProperties(IDictionary<string, object> properties) {
		string propertiesJson;
		if (properties != null) {
			propertiesJson = Json.Serialize(properties);
		} else {
			propertiesJson = Json.Serialize(new Dictionary<string, object>());
		}

		Log (string.Format("C# setUserProperties {0}", propertiesJson));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_setUserProperties(propertiesJson);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			pluginClass.CallStatic("setUserProperties", propertiesJson);
		}
#endif
	}

	public void setOptOut(bool enabled) {
		Log (string.Format("C# setOptOut {0}", enabled));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_setOptOut(enabled);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			pluginClass.CallStatic("setOptOut", enabled);
		}
#endif
	}

	[System.Obsolete("Please call setUserProperties instead", false)]
	public void setGlobalUserProperties(IDictionary<string, object> properties) {
		setUserProperties(properties);
	}

	public void logRevenue(double amount) {
		Log (string.Format("C# logRevenue {0}", amount));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_logRevenueAmount(amount);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			pluginClass.CallStatic("logRevenue", amount);
		}
#endif
	}

	public void logRevenue(string productId, int quantity, double price) {
		Log (string.Format("C# logRevenue {0}, {1}, {2}", productId, quantity, price));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_logRevenue(productId, quantity, price);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			pluginClass.CallStatic("logRevenue", productId, quantity, price);
		}
#endif
	}

	public void logRevenue(string productId, int quantity, double price, string receipt, string receiptSignature) {
		Log (string.Format("C# logRevenue {0}, {1}, {2} (with receipt)", productId, quantity, price));
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			_Amplitude_logRevenueWithReceipt(productId, quantity, price, receipt);
		}
#endif

#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			pluginClass.CallStatic("logRevenue", productId, quantity, price, receipt, receiptSignature);
		}
#endif
	}

	public string getDeviceId() {
		#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			return _Amplitude_getDeviceId();
		}
		#endif

		#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			return pluginClass.CallStatic<string>("getDeviceId");
		}
		#endif
		return null;
	}

	// This method is deprecated
	public void startSession() { return; }

	// This method is deprecated
	public void endSession() { return; }
}
