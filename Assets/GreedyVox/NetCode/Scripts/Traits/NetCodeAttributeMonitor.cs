using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The NetCodeAttributeMonitor will ensure the attribute values are synchronized when a new player joins the room.
/// </summary>
namespace GreedyVox.NetCode.Traits
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AttributeManager))]
    public class NetCodeAttributeMonitor : NetworkBehaviour
    {
        private AttributeManager m_AttributeManager;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake() =>
        m_AttributeManager = gameObject.GetCachedComponent<AttributeManager>();
        /// <summary>
        /// Register for any interested events.
        /// </summary>
        private void Start()
        {
            if (IsOwner)
                EventHandler.RegisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
        }
        /// <summary>
        /// The object has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            EventHandler.UnregisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
        }
        /// <summary>
        /// A player has entered the room. Ensure the joining player is in sync with the current game state.
        /// </summary>
        private void OnPlayerConnected(ulong id, NetworkObjectReference obj)
        {
            var attributes = m_AttributeManager.Attributes;
            if (attributes == null) return;
            for (int i = 0; i < attributes.Length; ++i)
                UpdateAttributeRpc(attributes[i].Name, attributes[i].Value, attributes[i].MinValue, attributes[i].MaxValue, attributes[i].AutoUpdateAmount,
                attributes[i].AutoUpdateInterval, attributes[i].AutoUpdateStartDelay, (int)attributes[i].AutoUpdateValueType);
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
        [Rpc(SendTo.NotMe)]
        private void UpdateAttributeRpc(string name, float value, float minValue, float maxValue, float autoUpdateAmount, float autoUpdateInterval, float autoUpdateStartDelay, int autoUpdateValueType)
        {
            var attribute = m_AttributeManager.GetAttribute(name);
            if (attribute != null)
            {
                attribute.Value = value;
                attribute.MinValue = minValue;
                attribute.MaxValue = maxValue;
                attribute.AutoUpdateAmount = autoUpdateAmount;
                attribute.AutoUpdateInterval = autoUpdateInterval;
                attribute.AutoUpdateStartDelay = autoUpdateStartDelay;
                attribute.AutoUpdateValueType = (Attribute.AutoUpdateValue)autoUpdateValueType;
            }
        }
    }
}