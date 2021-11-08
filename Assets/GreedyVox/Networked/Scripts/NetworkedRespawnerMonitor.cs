using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Traits;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes the Respawner over the network.
/// </summary>
namespace GreedyVox.Networked {
    public class NetworkedRespawnerMonitor : NetworkBehaviour, INetworkRespawnerMonitor {
        private Respawner m_Respawner;
        private NetworkedSettingsAbstract m_Settings;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake () {
            m_Settings = NetworkedManager.Instance.NetworkSettings;
            m_Respawner = gameObject.GetCachedComponent<Respawner> ();
        }
        /// <summary>
        /// Does the respawn by setting the position and rotation to the specified values.
        /// Enable the GameObject and let all of the listening objects know that the object has been respawned.
        /// </summary>
        /// <param name="position">The respawn position.</param>
        /// <param name="rotation">The respawn rotation.</param>
        /// <param name="transformChange">Was the position or rotation changed?</param>
        public void Respawn (Vector3 position, Quaternion rotation, bool transformChange) {
            if (IsServer) {
                RespawnClientRpc (position, rotation, transformChange);
            } else {
                RespawnServerRpc (position, rotation, transformChange);
            }
        }
        /// <summary>
        /// Does the respawn on the network by setting the position and rotation to the specified values.
        /// Enable the GameObject and let all of the listening objects know that the object has been respawned.
        /// </summary>
        /// <param name="position">The respawn position.</param>
        /// <param name="rotation">The respawn rotation.</param>
        /// <param name="transformChange">Was the position or rotation changed?</param>
        private void RespawnRpc (Vector3 position, Quaternion rotation, bool transformChange) {
            m_Respawner.Respawn (position, rotation, transformChange);
        }

        [ServerRpc]
        private void RespawnServerRpc (Vector3 position, Quaternion rotation, bool transformChange) {
            if (!IsClient) { RespawnRpc (position, rotation, transformChange); }
            RespawnClientRpc (position, rotation, transformChange);
        }

        [ClientRpc]
        private void RespawnClientRpc (Vector3 position, Quaternion rotation, bool transformChange) {
            if (!IsOwner) { RespawnRpc (position, rotation, transformChange); }
        }
    }
}