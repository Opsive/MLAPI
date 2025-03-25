using System.Text.RegularExpressions;
using GreedyVox.ProjectManagers.Events;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[DisallowMultipleComponent]
public class NetCodeMenuGUI : MonoBehaviour
{
    [SerializeField][Range(1, 1000)] private int m_WindowWidth = 200;
    [SerializeField] GameEvent m_LoadScene;
    [SerializeField] GUIStyle m_StyleVersion = null;
    private int m_ElementWidth = 0;
    private UnityTransport m_Transport;
    private Texture2D m_Texture = null;
    private bool m_ToggleAddress = false;
    private string m_Address = string.Empty;
    private GUIStyle m_Style = new GUIStyle();
    private Rect m_WindowAddress, m_WindowConnect, m_WindowDisconnect;
    private Regex m_IP = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
    // private void Awake() => m_Transport = NetworkManager.GetComponent<UnityTransport>();
    // private void OnEnable() => NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
    // private void OnDisable() => NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
    private void OnDestroy()
    {
        Disconnect();
        Debug.Log("<color=white>NetCode GUI Destroy</color>");
    }
    private void Start()
    {
        m_ElementWidth = m_WindowWidth - 25;
        m_WindowConnect = new Rect(2, 2, 200, 25);
        m_WindowDisconnect = new Rect(2, 2, 200, 100);
        m_Transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    }
    private void OnGUI()
    {
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            if (Event.current.isKey && Event.current.keyCode == KeyCode.F1)
                Disconnect();
            else
                m_WindowConnect = GUILayout.Window(0, m_WindowConnect, WindowConnect, "Press F1 To Disconnect");
        }
        else
        {
            if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape)
                Quit();
            else if (Event.current.isKey && Event.current.keyCode == KeyCode.H)
                StartClient(true);
            else if (Event.current.isKey && Event.current.keyCode == KeyCode.C)
                StartClient(false);
            else
                m_WindowDisconnect = GUILayout.Window(2, m_WindowDisconnect, WindowDisconnect, "Press Escape To Quit");
            m_WindowAddress.Set(Screen.width - m_WindowWidth - 2, 0, m_WindowWidth, 50);
            m_WindowAddress = GUILayout.Window(3, m_WindowAddress, WindowAddress, "IP ConnectAddress");
        }
    }
    // private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    // {
    //     response.Approved = true;
    //     response.Reason = "Testing the declined approval message";
    // }
    // private void OnClientDisconnectCallback(ulong obj)
    // {
    //     if (!NetworkManager.IsServer && NetworkManager.DisconnectReason != string.Empty)
    //         Debug.Log($"Approval Declined Reason: {NetworkManager.DisconnectReason}");
    // }
    #region Windows
    private void WindowConnect(int id)
    {
        if (GUILayout.Button("Disconnect", GUILayout.Width(m_ElementWidth)))
            Disconnect();
    }
    private void WindowTexture(int id)
    {
        if (m_Texture != null)
        {
            GUILayout.Box(m_Texture, GUILayout.Width(m_Texture.width), GUILayout.Height(m_Texture.height));
            GUILayout.Label(string.Format("Version.{0}", Application.version), m_StyleVersion);
        }
    }
    private void WindowDisconnect(int id)
    {
        if (GUILayout.Button("Host", GUILayout.Width(m_ElementWidth)))
        {
            StartClient(true);
            Debug.Log($"<color=white>Starting Hosting On <b>{m_Transport?.ConnectionData.Address}</b></color>");
        }
        else if (GUILayout.Button("Client", GUILayout.Width(m_ElementWidth)))
        {
            StartClient(false);
            // NetworkLog.LogInfoServer($"<color=white>Client <b>{OwnerClientId}</b> Joined</color>");
            Debug.Log($"<color=white>Joining Server On <b>{m_Transport?.ConnectionData.Address}</b></color>");
        }
        else if (GUILayout.Button("Server", GUILayout.Width(m_ElementWidth)))
        {
            StartServer();
            Debug.Log($"<color=white>Starting Server On <b>{m_Transport?.ConnectionData.Address}</b></color>");
        }
        else if (GUILayout.Button("Exit", GUILayout.Width(m_ElementWidth)))
        {
            Quit();
        }
    }
    private void WindowAddress(int id)
    {
        if (m_ToggleAddress = GUILayout.Toggle(m_ToggleAddress,
                "Change ConnectAddress", GUILayout.Width(m_ElementWidth)))
        {
            m_Address = GUILayout.TextField(m_Address, GUILayout.Width(m_ElementWidth));
            if (m_IP.IsMatch(m_Address))
            {
                m_Style.normal.textColor = Color.white;
                UpdateAddress(m_Address);
            }
            else
            {
                m_Style.normal.textColor = Color.red;
            }
            if (m_Address.Length > 0)
                GUILayout.Label(string.Format("{0}:{1}", m_Address, m_Transport?.ConnectionData.Port),
                    m_Style, GUILayout.Width(m_ElementWidth));
        }
    }
    #endregion
    #region Methods
    private void UpdateAddress(string ip)
    {
        if (!m_Transport.ConnectionData.Address.Equals(ip))
            m_Transport.ConnectionData.Address = ip;
    }
    private void StartServer()
    {
        Disconnect();
        // NetworkManager.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.StartServer();
    }
    private void StartClient(bool server)
    {
        m_LoadScene?.Raise();
        Disconnect();
        if (server)
        {
            // NetworkManager.ConnectionApprovalCallback += ApprovalCheck;
            // Server host client will bypass the callback, joining clients will be approved.
            NetworkManager.Singleton.StartHost();
        }
        else { NetworkManager.Singleton.StartClient(); }
    }
    public void Disconnect()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
            // NetworkManager.ConnectionApprovalCallback -= ApprovalCheck;
        }
    }
    private void Quit()
    {
        Disconnect();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit ();
#endif
    }
    #endregion
}