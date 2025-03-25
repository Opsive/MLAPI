using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Contains information about the object on the network.
/// </summary>
namespace GreedyVox.NetCode
{
    [DisallowMultipleComponent]
    public class NetCodeEvent : NetworkBehaviour
    {
        public EventNetworkDespawn NetworkDespawnEvent;
        public EventNetworkSpawn NetworkSpawnEvent;
        public delegate void EventNetworkDespawn();
        public delegate void EventNetworkSpawn();
        private NetworkTransport m_Transport;
        private Coroutine m_Coroutine;
        private ulong m_ServerID;
        private void OnDisable()
        {
            NetworkSpawnEvent = null;
            NetworkDespawnEvent = null;
        }
        /// <summary>
        /// The player connection disconnected.
        /// </summary>
        public override void OnNetworkDespawn() { NetworkDespawnEvent?.Invoke(); base.OnNetworkDespawn(); }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            NetworkSpawnEvent?.Invoke();
            m_ServerID = NetworkManager.ServerClientId;
            m_Transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            if (IsLocalPlayer && m_Transport != null && m_Coroutine == null)
                m_Coroutine = StartCoroutine(NetworkTimer());
            base.OnNetworkSpawn();
        }
        /// <summary>
        /// Send the ping result to be reported.
        /// </summary>
        private IEnumerator NetworkTimer()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (isActiveAndEnabled)
            {
                // Your ping event code here....
                var ping = m_Transport.GetCurrentRtt(m_ServerID);
                yield return wait;
            }
            m_Coroutine = null;
        }
    }
}