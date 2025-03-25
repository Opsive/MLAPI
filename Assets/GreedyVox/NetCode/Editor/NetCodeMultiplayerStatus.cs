using GreedyVox.NetCode.Utilities;
using Opsive.UltimateCharacterController.Networking;
using UnityEditor;
using UnityEngine;

namespace GreedyVox.NetCode.Editors
{
    public class NetCodeMultiplayerStatus : EditorWindow
    {
        [MenuItem("Tools/GreedyVox/NetCode/Setup/Multiplayer Settings")]
        private static void Init()
        {
            NetCodeMultiplayerStatus window = (NetCodeMultiplayerStatus)EditorWindow.GetWindow(typeof(NetCodeMultiplayerStatus));
            window.titleContent = new GUIContent("Multiplayer Settings");
            window.minSize = new Vector2(500, 300);
            window.Show();
        }
        private bool m_ToggleButtonBD, m_ToggleButtonStateBD, m_ToggleButtonState = true;
        // private const string UccAiBD = "ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER_BD_AI";
        private const string FilePath = "Assets/Settings/MultiplayerStatus.asset";
        private const string IconPath = "d_console.infoicon.sml";
        private MultiplayerStatus m_MultiplayerStatus;
        // Load the MultiplayerStatus asset when the window first loads
        private void OnEnable()
        {
            m_ToggleButtonState = TryGetMultiplayerStatus(out m_MultiplayerStatus);
            m_ToggleButtonStateBD = m_ToggleButtonBD = UnitySymbolUtility.HasSymbol(UnitySymbolUtility.UccAiBD);
        }
        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical();
            GUILayout.Box("Ultimate Character Controller", GUILayout.ExpandWidth(true));
            m_MultiplayerStatus = (MultiplayerStatus)EditorGUILayout.ObjectField("Multiplayer Asset", m_MultiplayerStatus, typeof(MultiplayerStatus), true);
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(m_ToggleButtonState);
            if (GUILayout.Button("Create Multiplayer Asset") && TryCreateMultiplayerStatus(out m_MultiplayerStatus))
            {
                m_MultiplayerStatus.SupportsMultiplayer = true;
                AssetDatabase.SaveAssets();
                DisplayNotification("Multiplayer Status asset created successfully");
                Debug.Log("Multiplayer Status asset created at: " + FilePath);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical();
            GUILayout.Box("Networking Behaviour Designer For AI", GUILayout.ExpandWidth(true));
            GUILayout.Label("Using Behaviour Designer", EditorStyles.boldLabel);
            m_ToggleButtonStateBD = EditorGUILayout.Toggle("Enable Networking For Ai", m_ToggleButtonBD);
            EditorGUILayout.Space();
            GUILayout.Label("Ensure that Behavior Designer is imported.", EditorStyles.miniLabel);
            GUILayout.Label("Enabling this option will ensure that your project compiles without errors.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical();
            if (m_ToggleButtonBD != m_ToggleButtonStateBD)
            {
                m_ToggleButtonBD = m_ToggleButtonStateBD;

                if (m_ToggleButtonBD)
                {
                    DisplayNotification($"Added symbol: {UnitySymbolUtility.UccAiBD}");
                    UnitySymbolUtility.AddSymbol(UnitySymbolUtility.UccAiBD);
                }
                else
                {
                    DisplayNotification($"Removed symbol: {UnitySymbolUtility.UccAiBD}");
                    UnitySymbolUtility.RemoveSymbol(UnitySymbolUtility.UccAiBD);
                }
            }
            EditorGUILayout.EndVertical();
        }
        private void DisplayNotification(string msg) =>
        ShowNotification(new GUIContent($" {msg}", EditorGUIUtility.IconContent(IconPath).image), 15);
        private bool TryGetMultiplayerStatus(out MultiplayerStatus val)
        {
            val = AssetDatabase.LoadAssetAtPath<MultiplayerStatus>(FilePath);
            return val != null;
        }
        private bool TryCreateMultiplayerStatus(out MultiplayerStatus val)
        {
            if (!TryGetMultiplayerStatus(out val))
            {
                val = ScriptableObject.CreateInstance<MultiplayerStatus>();
                AssetDatabase.CreateAsset(val, FilePath);
                return true;
            }
            return false;
        }
    }
}