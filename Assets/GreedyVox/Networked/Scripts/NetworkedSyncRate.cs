using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Contains information about the object on the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedSyncRate : NetworkBehaviour {
        public EventNetworkSync NetworkSyncEvent;
        public delegate void EventNetworkSync (List<ulong> clients);
        [SerializeField] private AnimationCurve m_DistanceSendCurve;
        [SerializeField] private float m_DistanceSendRange = 50.0f;
        [SerializeField][Range (1, 100)] private int m_FixedSendsPerSecond = 20;
        [SerializeField] private bool m_DispalyDebugLog = false;
        private Transform m_Transform;
        private float m_DistanceSendrate;
        private List<ulong> m_Clients = new List<ulong> ();
        private readonly Dictionary<ulong, float> m_ClientInfo = new Dictionary<ulong, float> ();
        private void OnDisable () {
            NetworkSyncEvent = null;
        }
        private void Awake () {
            m_Transform = transform;
        }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup. Provides a Payload if it was provided
        /// </summary>
        public override void OnNetworkSpawn () {
            m_DistanceSendrate = 1.0f / m_FixedSendsPerSecond;
            if (IsServer) {
                StartCoroutine (NetworkTimerServer ());
            }
        }
        public void SetDefaultDistanceCurve () {
            m_DistanceSendCurve = new AnimationCurve (new Keyframe[2] {
                new Keyframe (0.0f, 0.0f), new Keyframe (1.0f, 1.0f)
            });
        }
        private float GetTimeForLerp (Vector3 pos) {
            return Mathf.Max (m_DistanceSendCurve.Evaluate (Vector3.Distance (m_Transform.position, pos) / m_DistanceSendRange), m_DistanceSendrate);
        }
        /// <summary>
        /// Sync rates for Ai calculated from distance to players.
        /// </summary>
        private IEnumerator NetworkTimerServer () {
            while (NetworkManager.Singleton.IsServer) {
                if (NetworkSyncEvent != null) {
                    m_Clients.Clear ();
                    foreach (var client in NetworkManager.Singleton.ConnectedClients) {
                        if (client.Key == NetworkManager.Singleton.ServerClientId) continue;
                        float value;
                        if (m_ClientInfo.TryGetValue (client.Key, out value)) {
                            value += Time.deltaTime;
                            var timer = GetTimeForLerp (client.Value.PlayerObject.transform.position);
                            if (value > timer && timer < 1.0f) {
                                value = 0.0f;
                                m_Clients.Add (client.Key);
                                if (m_DispalyDebugLog) {
                                    Debug.LogFormat ("<color=green>ID: [<color=white>{0}</color>] Distance: [<color=white>{1}</color>] Rate: [<color=white>{2}</color>]</color>",
                                        client.Key,
                                        Vector3.Distance (m_Transform.position, client.Value.PlayerObject.transform.position),
                                        GetTimeForLerp (client.Value.PlayerObject.transform.position));
                                }
                            }
                        }
                        m_ClientInfo[client.Key] = value;
                    }
                    if (m_Clients.Count > 0) NetworkSyncEvent (m_Clients);
                }
                yield return null;
            }
        }
    }
}