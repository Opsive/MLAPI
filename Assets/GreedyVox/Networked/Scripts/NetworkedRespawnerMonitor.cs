using Opsive.UltimateCharacterController.Networking.Traits;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes the Respawner over the network.
/// </summary>
namespace GreedyVox.Networked {
    public class NetworkedRespawnerMonitor : NetworkBehaviour, INetworkRespawnerMonitor {
        /// <summary>
        /// Does the respawn by setting the position and rotation to the specified values.
        /// Enable the GameObject and let all of the listening objects know that the object has been respawned.
        /// </summary>
        /// <param name="position">The respawn position.</param>
        /// <param name="rotation">The respawn rotation.</param>
        /// <param name="transformChange">Was the position or rotation changed?</param>
        public void Respawn (Vector3 position, Quaternion rotation, bool state) {
            if (NetworkManager.Singleton.IsServer) {
                var net = gameObject.GetComponent<NetworkObject> ();
                if (net != null && !net.IsSpawned) { net.Spawn (); }
            }
        }
    }
}