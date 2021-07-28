using GreedyVox.Networked.Events;
using GreedyVox.ProjectManagers;
using GreedyVox.ProjectManagers.Events;
using MLAPI;
using UnityEngine;

namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedManager : AbstractSingletonBehaviour<NetworkedManager> {
        [SerializeField] private NetworkedSettingsAbstract m_NetworkSettings = null;
        [SerializeField] private GameEventUlong m_ClientConnectEvent = default;
        [SerializeField] private GameEventUlong m_ClientDisconnectEvent = default;
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
                if (NetworkManager.Singleton.IsHost) { m_ClientConnectEvent?.Raise (0); }
                EventManager.Instance.Raise (new ServerStartedEvent ());
            };

            Connection.OnClientDisconnectCallback += ID => {
                m_ClientDisconnectEvent?.Raise (ID);
                m_NetworkSettings.PlayDisconnect (m_AudioSource);
                Debug.LogFormat ("<color=white>Server Client Disconnected ID: [<b><color=red>{0}</color></b>]</color>", ID);
            };

            Connection.OnClientConnectedCallback += ID => {
                m_NetworkSettings.PlayConnect (m_AudioSource);
                var client = Connection.ConnectedClients[ID];
                EventManager.Instance.Raise (new PlayerConnectedEvent (ID));
                m_ClientConnectEvent?.Raise (ID);
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