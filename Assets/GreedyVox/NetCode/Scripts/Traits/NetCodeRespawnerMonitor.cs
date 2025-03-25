using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Traits;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes the Respawner over the network.
/// </summary>
namespace GreedyVox.NetCode.Traits
{
    public class NetCodeRespawnerMonitor : NetworkBehaviour, INetworkRespawnerMonitor
    {
        private Respawner m_Respawner;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake() =>
        m_Respawner = gameObject.GetCachedComponent<Respawner>();
        /// <summary>
        /// Does the respawn by setting the position and rotation to the specified values.
        /// Enable the GameObject and let all of the listening objects know that the object has been respawned.
        /// </summary>
        /// <param name="position">The respawn position.</param>
        /// <param name="rotation">The respawn rotation.</param>
        /// <param name="state">Was the position or rotation changed?</param>
        public void Respawn(Vector3 position, Quaternion rotation, bool state) =>
        RespawnRpc(position, rotation, state);
        /// <summary>
        /// Does the respawn on the network by setting the position and rotation to the specified values.
        /// Enable the GameObject and let all of the listening objects know that the object has been respawned.
        /// </summary>
        /// <param name="position">The respawn position.</param>
        /// <param name="rotation">The respawn rotation.</param>
        /// <param name="state">Was the position or rotation changed?</param>
        [Rpc(SendTo.NotOwner, RequireOwnership = false, Delivery = RpcDelivery.Reliable)]
        private void RespawnRpc(Vector3 position, Quaternion rotation, bool state) =>
        m_Respawner.Respawn(position, rotation, state);
    }
}