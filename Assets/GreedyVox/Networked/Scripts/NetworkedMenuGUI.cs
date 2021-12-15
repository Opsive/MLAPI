using System.Text.RegularExpressions;
using Unity.Netcode;
using Unity.Netcode.Transports.UNET;
using UnityEngine;
using static Unity.Netcode.NetworkManager;

[DisallowMultipleComponent]
public class NetworkedMenuGUI : NetworkBehaviour {
    [SerializeField][Range (1, 1000)] private int m_WindowWidth = 200;
    [SerializeField] GUIStyle m_StyleVersion = null;
    private int m_ElementWidth = 0;
    private UNetTransport m_Transport;
    private Texture2D m_Texture = null;
    private bool m_ToggleAddress = false;
    private string m_Address = string.Empty;
    private GUIStyle m_Style = new GUIStyle ();
    private Regex m_IP = new Regex (@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
    private Rect m_WindowAddress, m_WindowConnect, m_WindowDisconnect;
    private void Start () {
        m_ElementWidth = m_WindowWidth - 25;
        m_WindowConnect = new Rect (2, 2, 200, 25);
        m_WindowDisconnect = new Rect (2, 2, 200, 100);
        m_Transport = NetworkManager.Singleton.GetComponent<UNetTransport> ();
    }
    private void OnGUI () {
        if (IsServer || IsClient) {
            if (Event.current.isKey && Event.current.keyCode == KeyCode.F1) {
                Disconnect ();
            } else {
                m_WindowConnect = GUILayout.Window (0, m_WindowConnect, WindowConnect, "Press F1 To Disconnect");
            }
        } else {
            if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape) {
                Quit ();
            } else if (Event.current.isKey && Event.current.keyCode == KeyCode.H) {
                StartClient (true);
            } else if (Event.current.isKey && Event.current.keyCode == KeyCode.C) {
                StartClient (false);
            } else {
                m_WindowDisconnect = GUILayout.Window (2, m_WindowDisconnect, WindowDisconnect, "Press Escape To Quit");
            }
            m_WindowAddress.Set (Screen.width - m_WindowWidth - 2, 0, m_WindowWidth, 50);
            m_WindowAddress = GUILayout.Window (3, m_WindowAddress, WindowAddress, "IP ConnectAddress");
        }
    }
    #region Windows
    private void WindowConnect (int id) {
        if (GUILayout.Button ("Disconnect", GUILayout.Width (m_ElementWidth))) {
            Disconnect ();
        }
    }
    private void WindowTexture (int id) {
        if (m_Texture != null) {
            GUILayout.Box (m_Texture, GUILayout.Width (m_Texture.width), GUILayout.Height (m_Texture.height));
            GUILayout.Label (string.Format ("Version.{0}", Application.version), m_StyleVersion);
        }
    }
    private void WindowDisconnect (int id) {
        if (GUILayout.Button ("Host", GUILayout.Width (m_ElementWidth))) {
            StartClient (true);
            Debug.Log ($"<color=white>Starting Hosting On <b>{m_Transport?.ConnectAddress}</b></color>");
        } else if (GUILayout.Button ("Client", GUILayout.Width (m_ElementWidth))) {
            StartClient (false);
            NetworkLog.LogInfoServer ($"<color=white>Client <b>{OwnerClientId}</b> Joined</color>");
            Debug.Log ($"<color=white>Joining Server On <b>{m_Transport?.ConnectAddress}</b></color>");
        } else if (GUILayout.Button ("Server", GUILayout.Width (m_ElementWidth))) {
            StartServer ();
            Debug.Log ($"<color=white>Starting Server On <b>{m_Transport?.ConnectAddress}</b></color>");
        } else if (GUILayout.Button ("Exit", GUILayout.Width (m_ElementWidth))) {
            Quit ();
        }
    }
    private void WindowAddress (int id) {
        if (m_ToggleAddress = GUILayout.Toggle (m_ToggleAddress,
                "Change ConnectAddress", GUILayout.Width (m_ElementWidth))) {
            m_Address = GUILayout.TextField (m_Address, GUILayout.Width (m_ElementWidth));
            if (m_IP.IsMatch (m_Address)) {
                m_Style.normal.textColor = Color.white;
                UpdateAddress (m_Address);
            } else {
                m_Style.normal.textColor = Color.red;
            }
            if (m_Address.Length > 0) {
                GUILayout.Label (string.Format ("{0}:{1}", m_Address, m_Transport?.ConnectPort),
                    m_Style, GUILayout.Width (m_ElementWidth));
            }
        }
    }
    #endregion
    #region Methods
    private void UpdateAddress (string ip) {
        if (!m_Transport.ConnectAddress.Equals (ip)) {
            m_Transport.ConnectAddress = ip;
        }
    }
    private void ApprovalCheck (byte[] connectionData, ulong clientId, ConnectionApprovedDelegate callback) {
        callback (true, null, true, Vector3.zero, Quaternion.identity);
    }
    private void StartServer () {
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        // yield return new WaitForSeconds (0.1f);
        NetworkManager.Singleton.StartServer ();
    }
    private void StartClient (bool server) {
        if (server) {
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
            // Server host client will bypass the callback, joining clients will be approved.
            NetworkManager.Singleton.StartHost ();
        } else { NetworkManager.Singleton.StartClient (); }
    }
    public void Disconnect () {
        NetworkManager.Singleton.Shutdown ();
    }
    private void Quit () {
        NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit ();
#endif
    }
    #endregion
}