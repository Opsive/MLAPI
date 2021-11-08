using System.Collections.Generic;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Traits;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes the Interactable component over the network.
/// </summary>
namespace GreedyVox.Networked {
    public class NetworkedInteractableMonitor : NetworkBehaviour, INetworkInteractableMonitor {
        private GameObject m_GameObject;
        private Interactable m_Interactable;
        private NetworkedSettingsAbstract m_Settings;
        private Dictionary<ulong, NetworkObject> m_NetworkObjects;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake () {
            m_GameObject = gameObject;
            m_NetworkObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
            m_Settings = NetworkedManager.Instance.NetworkSettings;
            m_Interactable = m_GameObject.GetCachedComponent<Interactable> ();
        }
        /// <summary>
        /// Performs the interaction.
        /// </summary>
        /// <param name="character">The character that wants to interactact with the target.</param>
        public void Interact (GameObject character) {
            var obj = character.GetCachedComponent<NetworkObject> ();
            if (obj == null) {
                Debug.LogError ("Error: The character " + character.name + " must have a NetworkObject component.");
            } else {
                if (IsServer) {
                    InteractClientRpc (obj.NetworkObjectId);
                } else {
                    InteractServerRpc (obj.NetworkObjectId);
                }
            }
        }
        /// <summary>
        /// Performs the interaction on the network.
        /// </summary>
        /// <param name="characterViewID">The View ID of the character that performed the interaction.</param>
        private void InteractRpc (ulong characterID) {
            if (m_NetworkObjects.TryGetValue (characterID, out var obj)) {
                m_Interactable.Interact (obj.gameObject);
            }
        }

        [ServerRpc]
        private void InteractServerRpc (ulong characterID) {
            if (!IsClient) { InteractRpc (characterID); }
            InteractClientRpc (characterID);
        }

        [ClientRpc]
        private void InteractClientRpc (ulong characterID) {
            if (!IsOwner) { InteractRpc (characterID); }
        }
    }
}