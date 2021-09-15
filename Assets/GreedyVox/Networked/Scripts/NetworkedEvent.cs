using System.Collections;
using MLAPI;
using MLAPI.Transports;
using UnityEngine;

/// <summary>
/// Contains information about the object on the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedEvent : NetworkBehaviour {
        public EventNetworkStart NetworkStartEvent;
        public delegate void EventNetworkStart ();
        private ulong m_ServerID;
        private NetworkTransport m_Transport;
        private Coroutine m_Coroutine;
        /// <summary>
        /// The player connection disconnected.
        /// </summary>
        private void OnDestroy () {
            NetworkStartEvent = null;
        }
        public override void NetworkStart () {
            m_ServerID = NetworkManager.Singleton.ServerClientId;
            if (NetworkStartEvent != null) { NetworkStartEvent (); }
            m_Transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            if (IsLocalPlayer && m_Transport != null && m_Coroutine == null) {
                m_Coroutine = StartCoroutine (NetworkTimer ());
            }
        }
        /// <summary>
        /// Send the ping result to be reported.
        /// </summary>
        private IEnumerator NetworkTimer () {
            var wait = new WaitForSecondsRealtime (0.5f);
            while (isActiveAndEnabled) {
                // Your ping event code here....
                var ping = m_Transport.GetCurrentRtt (m_ServerID);
                yield return wait;
            }
            m_Coroutine = null;
        }
    }
}