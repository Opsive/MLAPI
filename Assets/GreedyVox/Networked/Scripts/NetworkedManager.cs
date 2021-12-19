using Opsive.Shared.Events;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedManager : AbstractSingletonBehaviour<NetworkedManager> {
        [SerializeField] private NetworkedSettingsAbstract m_NetworkSettings = null;
        public NetworkedSettingsAbstract NetworkSettings { get { return m_NetworkSettings; } }
        private AudioSource m_AudioSource;
        private NetworkManager _Connection;
        public NetworkManager Connection {
            get {
                if (_Connection == null)
                    _Connection = NetworkManager.Singleton;
                return _Connection;
            }
        }
        protected override void Awake () {
            Persist = false;
            base.Awake ();
            m_AudioSource = GetComponent<AudioSource> ();
            if (m_AudioSource == null) {
                m_AudioSource = gameObject.AddComponent<AudioSource> ();
            }
        }
        private void Start () {
            Connection.OnServerStarted += () => {
                if (NetworkManager.Singleton.IsHost) {
                    Debug.Log ("<color=white>Server Started</color>");
                }
            };
            Connection.OnClientDisconnectCallback += ID => {
                m_NetworkSettings?.PlayDisconnect (m_AudioSource);
                EventHandler.ExecuteEvent ("OnPlayerDisconnected", ID);

                Debug.LogFormat ("<color=white>Server Client Disconnected ID: [<b><color=red><b>{0}</b></color></b>]</color>", ID);
            };
            Connection.OnClientConnectedCallback += ID => {
                m_NetworkSettings?.PlayConnect (m_AudioSource);
                var net = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject (ID);
                net.gameObject.name = $"[{ID}]{net.gameObject.name}[{net.NetworkObjectId}]";
                EventHandler.ExecuteEvent ("OnPlayerConnected", ID);

                NetworkLog.LogInfoServer ($"<color=white>Server Client Connected {net.gameObject.name} ID: [<b><color=blue><b>{ID}</b></color></b>]</color>");
            };
            if (m_NetworkSettings == null) {
                Debug.LogErrorFormat ("NullReferenceException: There is no network settings manager\n{0}", typeof (NetworkedSettingsAbstract));
                Quit ();
            } else {
                StartCoroutine (m_NetworkSettings.NetworkSyncUpdate ());
                StartCoroutine (m_NetworkSettings.NetworkSyncClient ());
                StartCoroutine (m_NetworkSettings.NetworkSyncServer ());
            }
        }
        private void Quit () {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit ();
#endif
        }
    }
}