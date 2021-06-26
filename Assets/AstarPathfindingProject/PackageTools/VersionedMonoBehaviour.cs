using UnityEngine;

namespace Pathfinding {
	/// <summary>Exposes internal methods from <see cref="Pathfinding.VersionedMonoBehaviour"/></summary>
	public interface IVersionedMonoBehaviourInternal {
		void UpgradeFromUnityThread ();
	}

	/// <summary>Base class for all components in the package</summary>
	public abstract class VersionedMonoBehaviour : MonoBehaviour, ISerializationCallbackReceiver, IVersionedMonoBehaviourInternal {
		/// <summary>Version of the serialized data. Used for script upgrades.</summary>
		[SerializeField]
		[HideInInspector]
		int version = 0;

		protected virtual void Awake () {
			// Make sure the version field is up to date for components created during runtime.
			// Reset is not called when in play mode.
			// If the data had to be upgraded then OnAfterDeserialize would have been called earlier.
			if (Application.isPlaying) version = OnUpgradeSerializedData(int.MaxValue, true);
		}

		/// <summary>Handle serialization backwards compatibility</summary>
		protected virtual void Reset () {
			// Set initial version when adding the component for the first time
			version = OnUpgradeSerializedData(int.MaxValue, true);
		}

		/// <summary>Handle serialization backwards compatibility</summary>
		void ISerializationCallbackReceiver.OnBeforeSerialize () {
		}

		/// <summary>Handle serialization backwards compatibility</summary>
		void ISerializationCallbackReceiver.OnAfterDeserialize () {
			var r = OnUpgradeSerializedData(version, false);

			// Negative values (-1) indicate that the version number should not be updated
			if (r >= 0) version = r;
		}

		/// <summary>Handle serialization backwards compatibility</summary>
		protected virtual int OnUpgradeSerializedData (int version, bool unityThread) {
			return 1;
		}

		void IVersionedMonoBehaviourInternal.UpgradeFromUnityThread () {
			var r = OnUpgradeSerializedData(version, true);

			if (r < 0) throw new System.Exception();
			version = r;
		}
	}
}
