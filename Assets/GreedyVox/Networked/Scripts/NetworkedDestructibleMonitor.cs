using Opsive.UltimateCharacterController.Networking.Objects;
using Opsive.UltimateCharacterController.Objects;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Destroys a Destructible over the network.
/// </summary>
// [RequireComponent (typeof (NetworkedInfo))]
namespace GreedyVox.Networked {
    public class NetworkedDestructibleMonitor : NetworkBehaviour, IDestructibleMonitor {
        private Destructible m_Destructible;
        private NetworkedSettingsAbstract m_Settings;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake () {
            m_Destructible = GetComponent<Destructible> ();
            m_Settings = NetworkedManager.Instance.NetworkSettings;
        }
        /// <summary>
        /// Destroys the object.
        /// </summary>
        /// <param name="hitPosition">The position of the destruction.</param>
        /// <param name="hitNormal">The normal direction of the destruction.</param>
        public void Destruct (Vector3 hitPosition, Vector3 hitNormal) {
            if (IsServer) {
                DestructClientRpc (hitPosition, hitNormal);
            } else {
                DestructServerRpc (hitPosition, hitNormal);
            }
        }
        /// <summary>
        /// Destroys the object over the network.
        /// </summary>
        /// <param name="hitPosition">The position of the destruction.</param>
        /// <param name="hitNormal">The normal direction of the destruction.</param>
        private void DestructRpc (Vector3 hitPosition, Vector3 hitNormal) {
            m_Destructible.Destruct (hitPosition, hitNormal);
        }

        [ServerRpc]
        private void DestructServerRpc (Vector3 hitPosition, Vector3 hitNormal) {
            if (!IsClient) { DestructRpc (hitPosition, hitNormal); }
            DestructClientRpc (hitPosition, hitNormal);
        }

        [ClientRpc]
        private void DestructClientRpc (Vector3 hitPosition, Vector3 hitNormal) {
            if (!IsOwner) { DestructRpc (hitPosition, hitNormal); }
        }
    }
}