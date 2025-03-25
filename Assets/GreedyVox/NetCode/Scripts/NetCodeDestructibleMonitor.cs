using GreedyVox.NetCode.Interfaces;
using GreedyVox.NetCode.Utilities;
using Opsive.UltimateCharacterController.Networking.Objects;
using Opsive.UltimateCharacterController.Objects;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Destroys a Destructible over the network.
/// </summary>
namespace GreedyVox.NetCode
{
    // [RequireComponent(typeof(NetCodeInfo))]
    public class NetCodeDestructibleMonitor : NetworkBehaviour, IDestructibleMonitor
    {
        private ProjectileBase m_Destructible;
        private IPayload m_PayLoad;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake()
        {
            m_PayLoad = GetComponent<IPayload>();
            m_Destructible = GetComponent<ProjectileBase>();
        }
        public override void OnNetworkSpawn()
        {
            if (IsOwner && ComponentUtility.TryGet<NetworkObject>(m_Destructible?.Owner, out var net))
                InitializeRpc(m_Destructible.ID, net);
            base.OnNetworkSpawn();
        }
        [Rpc(SendTo.NotOwner, RequireOwnership = true, Delivery = RpcDelivery.Reliable)]
        private void InitializeRpc(uint id, NetworkObjectReference obj, RpcParams rpc = default)
        {
            m_Destructible.enabled = true;
            m_PayLoad?.Initialize(id, obj);
        }
        /// <summary>
        /// Destroys the object.
        /// </summary>
        /// <param name="hitPosition">The position of the destruction.</param>
        /// <param name="hitNormal">The normal direction of the destruction.</param>
        public void Destruct(Vector3 hitPosition, Vector3 hitNormal) =>
        DestructRpc(hitPosition, hitNormal);
        /// <summary>
        /// Destroys the object over the network.
        /// </summary>
        /// <param name="hitPosition">The position of the destruction.</param>
        /// <param name="hitNormal">The normal direction of the destruction.</param>
        [Rpc(SendTo.NotOwner, RequireOwnership = true, Delivery = RpcDelivery.Reliable)]
        // private void DestructRpc(Vector3 hitPosition, Vector3 hitNormal) => m_Destructible.Destruct(hitPosition, hitNormal);
        private void DestructRpc(Vector3 hitPosition, Vector3 hitNormal) =>
        m_Destructible.Destruct(hitPosition, hitNormal);
    }
}