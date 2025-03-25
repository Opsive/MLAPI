using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode.Objects
{
    /// <summary>
    /// Syncronizes the animated interactable when a new player joins the room.
    /// </summary>
    public class NetCodeAnimatedInteractable : AnimatedInteractable
    {
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            var net = gameObject.GetCachedComponent<NetworkObject>();
            if (net == null)
            {
                Debug.LogError("Error: A NetCode must be added to " + gameObject.name + ".");
                enabled = false;
            }
        }
        /// <summary>
        /// Registering events.
        /// </summary>
        private void OnEnable() =>
        EventHandler.RegisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
        /// <summary>
        /// Removing events.
        /// </summary>
        private void OnDisable() =>
        EventHandler.UnregisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
        /// <summary>
        /// A event from Photon has been sent.
        /// </summary>
        /// <param name="id">The Client networking id that connected.</param>
        /// <param name="obj">The Player NetworkObject that connected.</param>
        private void OnPlayerConnected(ulong id, NetworkObjectReference obj)
        {
            // If isn't the Server/Host then we should early return here!
            if (!NetworkManager.Singleton.IsServer || !m_HasInteracted) return;
            InteractedRpc();
        }
        /// <summary>
        /// Indicates that the GameObject has been interacted with.
        /// </summary>
        [Rpc(SendTo.NotMe)]
        private void InteractedRpc() => m_HasInteracted = true;
    }
}