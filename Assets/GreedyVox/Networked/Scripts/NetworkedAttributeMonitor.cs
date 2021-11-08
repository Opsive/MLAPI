using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The NetworkedAttributeMonitor will ensure the attribute values are synchronized when a new player joins the room.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    [RequireComponent (typeof (AttributeManager))]
    public class NetworkedAttributeMonitor : NetworkBehaviour {
        private AttributeManager m_AttributeManager;
        private NetworkedSettingsAbstract m_Settings;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake () {
            m_Settings = NetworkedManager.Instance.NetworkSettings;
            m_AttributeManager = gameObject.GetCachedComponent<AttributeManager> ();
        }
        /// <summary>
        /// A player has entered the room. Ensure the joining player is in sync with the current game state.
        /// </summary>
        public override void OnNetworkSpawn () {
            var attributes = m_AttributeManager.Attributes;
            if (attributes != null) {
                for (int i = 0; i < attributes.Length; ++i) {
                    if (IsServer) {
                        UpdateAttributeClientRpc (attributes[i].Name, attributes[i].Value, attributes[i].MinValue, attributes[i].MaxValue,
                            attributes[i].AutoUpdateAmount, attributes[i].AutoUpdateInterval, attributes[i].AutoUpdateStartDelay, (int) attributes[i].AutoUpdateValueType);
                    } else {
                        UpdateAttributeServerRpc (attributes[i].Name, attributes[i].Value, attributes[i].MinValue, attributes[i].MaxValue,
                            attributes[i].AutoUpdateAmount, attributes[i].AutoUpdateInterval, attributes[i].AutoUpdateStartDelay, (int) attributes[i].AutoUpdateValueType);
                    }
                }
            }
        }
        /// <summary>
        /// Updates the attribute values for the specified attribute.
        /// </summary>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="value">The value of the attribute.</param>
        /// <param name="minValue">The min value of the attribute.</param>
        /// <param name="maxValue">The max value of the attribute.</param>
        /// <param name="autoUpdateAmount">The amount to change the value with each auto update.</param>
        /// <param name="autoUpdateInterval">The amount of time to wait in between auto update loops.</param>
        /// <param name="autoUpdateStartDelay">The amount of time between a value change and when the auto updater should start.</param>
        /// <param name="autoUpdateValueType">Describes how the attribute should update the value</param>
        private void UpdateAttributeRpc (string name, float value, float minValue, float maxValue, float autoUpdateAmount, float autoUpdateInterval, float autoUpdateStartDelay, int autoUpdateValueType) {
            var attribute = m_AttributeManager.GetAttribute (name);
            if (attribute != null) {
                attribute.Value = value;
                attribute.MinValue = minValue;
                attribute.MaxValue = maxValue;
                attribute.AutoUpdateAmount = autoUpdateAmount;
                attribute.AutoUpdateInterval = autoUpdateInterval;
                attribute.AutoUpdateStartDelay = autoUpdateStartDelay;
                attribute.AutoUpdateValueType = (Attribute.AutoUpdateValue) autoUpdateValueType;
            }
        }

        [ServerRpc]
        private void UpdateAttributeServerRpc (string name, float value, float minValue, float maxValue, float autoUpdateAmount, float autoUpdateInterval, float autoUpdateStartDelay, int autoUpdateValueType) {
            if (!IsClient) { UpdateAttributeRpc (name, value, minValue, maxValue, autoUpdateAmount, autoUpdateInterval, autoUpdateStartDelay, autoUpdateValueType); }
            UpdateAttributeClientRpc (name, value, minValue, maxValue, autoUpdateAmount, autoUpdateInterval, autoUpdateStartDelay, autoUpdateValueType);
        }

        [ClientRpc]
        private void UpdateAttributeClientRpc (string name, float value, float minValue, float maxValue, float autoUpdateAmount, float autoUpdateInterval, float autoUpdateStartDelay, int autoUpdateValueType) {
            if (!IsOwner) { UpdateAttributeRpc (name, value, minValue, maxValue, autoUpdateAmount, autoUpdateInterval, autoUpdateStartDelay, autoUpdateValueType); }
        }
    }
}