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

            Connection.OnServerStarted += () => {
                if (NetworkManager.Singleton.IsHost) {
                    Debug.Log ("<color=white>Server Started</color>");
                }
            };

            Connection.OnClientDisconnectCallback += ID => {
                m_NetworkSettings.PlayDisconnect (m_AudioSource);
                NetworkLog.LogInfoServer ("<color=white>Client Disconnected</color>");
                Debug.LogFormat ("<color=white>Server Client Disconnected ID: [<b><color=red>{0}</color></b>]</color>", ID);
            };

            Connection.OnClientConnectedCallback += ID => {
                m_NetworkSettings.PlayConnect (m_AudioSource);
                var client = Connection.ConnectedClients[ID];
                NetworkLog.LogInfoServer ("<color=white>Client Connected</color>");
                Debug.LogFormat ("<color=white>Server Client Connected {0} ID: [<b><color=red>{1}</color></b>]</color>", client, ID);
            };
        }
        private void Start () {
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