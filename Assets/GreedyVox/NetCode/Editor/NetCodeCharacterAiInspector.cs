using System.Linq;
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER_BD_AI
using BehaviorDesigner.Runtime;
using GreedyVox.NetCode.Ai;
#endif
using GreedyVox.NetCode.Character;
using GreedyVox.NetCode.Traits;
using GreedyVox.NetCode.Utilities;
using Opsive.Shared.Input;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.AI;
using Opsive.UltimateCharacterController.Game;
using Opsive.UltimateCharacterController.Networking.Character;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace GreedyVox.NetCode.Editors
{
    public class NetCodeCharacterAiInspector : EditorWindow
    {
        [MenuItem("Tools/GreedyVox/NetCode/Characters/Ai Inspector")]
        private static NetCodeCharacterAiInspector Init() =>
        EditorWindow.GetWindowWithRect<NetCodeCharacterAiInspector>(
        new Rect(Screen.width - 300 / 2, Screen.height - 200 / 2, 300, 200), true, "Network Character Ai");
        private GUIStyle m_Style = new();
        private Object m_NetworkCharacter;
        private bool m_RemoverFound = false;
        private const string IconWarningPath = "d_console.warnicon.sml";
        private const string IconErrorPath = "d_console.erroricon.sml";
        private const string IconIfoPath = "d_console.infoicon.sml";
        private void OnEnable()
        {
            m_Style.wordWrap = true;
            m_Style.fontSize = 15;
            m_Style.normal.textColor = Color.yellow;
            m_Style.alignment = TextAnchor.LowerCenter;
        }
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            m_NetworkCharacter = EditorGUILayout.ObjectField(m_NetworkCharacter, typeof(Object), true);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Update Character Ai"))
            {
                if (m_NetworkCharacter == null)
                {
                    ShowNotification(new GUIContent("No object selected for updating",
                                         EditorGUIUtility.IconContent(IconErrorPath).image), 15);
                }
                else
                {
                    SetupCharacter((GameObject)m_NetworkCharacter);
                    ShowNotification(new GUIContent("Finished updating character",
                                         EditorGUIUtility.IconContent(IconIfoPath).image), 15);
                }
            }
            if (m_RemoverFound)
            {
                ShowNotification(new GUIContent("Remover script has been replaced", EditorGUIUtility.IconContent(IconWarningPath).image), 15);
                GUILayout.Label("Please ensure that all events previously utilizing the Remover script are updated to use the NetCodeRemover script.", m_Style);
            }
        }
        /// <summary>
        /// Sets up the character to be able to work with networking.
        /// </summary>
        private void SetupCharacter(GameObject go)
        {
            if (go == null) return;
            go.tag = "Ai";
            if (ComponentUtility.TryAddGetComponent<UltimateCharacterLocomotion>(go, out var com))
            {
                var ability = com.GetAbility(typeof(NavMeshAgentMovement));
                if (ability == null)
                {
                    var abilities = com.Abilities.ToList();
                    ability = new NavMeshAgentMovement();
                    var index = abilities.IndexOf(com.GetAbility(typeof(SpeedChange)));
                    if (index != -1) abilities.Insert(index, ability);
                    else abilities.Add(ability);
                    com.Abilities = abilities.ToArray();
                }
                com.enabled = true;
            }
            // Remove the single player variants of the necessary components.
            ComponentUtility.TryRemoveComponent<ItemHandler>(go);
            ComponentUtility.TryRemoveComponent<PlayerInputProxy>(go);
            ComponentUtility.TryRemoveComponentInChildren<UnityInput>(go);
            ComponentUtility.TryRemoveComponent<RemotePlayerPerspectiveMonitor>(go);
            ComponentUtility.TryRemoveComponent<UltimateCharacterLocomotionHandler>(go);
            ComponentUtility.TryReplaceComponentInChildren<AnimatorMonitor, NetCodeAnimatorMonitor>(go);
            if (!ComponentUtility.HasComponent<NavMeshAgent>(go))
                ComponentUtility.TryAddComponent<NavMeshAgent>(go);
            if (!ComponentUtility.HasComponent<LocalLookSource>(go))
                ComponentUtility.TryAddComponent<LocalLookSource>(go);
            if (ComponentUtility.TryAddComponent<NetworkObject>(go, out var net))
            {
                net.SpawnWithObservers = true;
                net.SynchronizeTransform = true;
                net.AlwaysReplicateAsRoot = false;
                net.ActiveSceneSynchronization = false;
                net.SceneMigrationSynchronization = false;
                net.DontDestroyWithOwner = false;
                net.AutoObjectParentSync = false;
            }
            ComponentUtility.TryAddComponent<NetCodeEvent>(go);
            ComponentUtility.TryAddComponent<NetCodeInfo>(go);
            ComponentUtility.TryAddComponent<NetCodeCharacter>(go);
            ComponentUtility.TryAddComponent<NetCodeCharacterAnimatorMonitor>(go);
            ComponentUtility.TryAddComponent<NetCodeCharacterTransformMonitor>(go);
            ComponentUtility.TryAddComponent<NetCodeLookSource>(go);
            // Certain components may be necessary if their single player components is added to the character.
            if (ComponentUtility.HasComponent<AttributeManager>(go))
                ComponentUtility.TryAddComponent<NetCodeAttributeMonitor>(go);
            if (ComponentUtility.HasComponent<Health>(go))
                ComponentUtility.TryAddComponent<NetCodeHealthMonitor>(go);
            if (m_RemoverFound = ComponentUtility.TryRemoveComponent<Remover>(go))
                ComponentUtility.TryAddComponent<NetCodeRemover>(go);
            if (ComponentUtility.HasComponent<Respawner>(go))
                ComponentUtility.TryAddComponent<NetCodeRespawnerMonitor>(go);
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER_BD_AI
            if (ComponentUtility.TryAddGetComponent<BehaviorTree>(go))
                ComponentUtility.TryAddComponent<NetCodeAiBD>(go);
#endif
            // Add the ObjectInspector to any character or ragdoll colliders. This will allow the collider GameObjects to be identifiable over the network.
            uint maxID = 0;
            var existingIdentifiers = go.GetComponentsInChildren<ObjectIdentifier>(true);
            for (int i = 0; i < existingIdentifiers.Length; ++i)
            {
                // The collider may be used for a ragdoll. Ragdoll colliders should not contribute to the max id.
                var collider = existingIdentifiers[i].GetComponent<Collider>();
                if (collider != null)
                    if (!collider.isTrigger &&
                        (collider.gameObject.layer == LayerManager.Enemy ||
                            (collider.gameObject.layer == LayerManager.SubCharacter && collider.GetComponent<Rigidbody>() != null)))
                        continue;
                if (existingIdentifiers[i].ID > maxID)
                    maxID = existingIdentifiers[i].ID;
            }
            // The max available ID has been determined. Add the ObjectIdentifier.
            var colliders = go.GetComponentsInChildren<Collider>(true);
            uint IDOffset = 1000000000;
            for (int i = 0; i < colliders.Length; ++i)
            {
                if (colliders[i].isTrigger ||
                    (colliders[i].gameObject.layer != LayerManager.Enemy &&
                        (colliders[i].gameObject.layer != LayerManager.SubCharacter || colliders[i].GetComponent<Rigidbody>() == null)))
                    continue;
                var objectIdentifier = ComponentUtility.TryAddGetComponent<ObjectIdentifier>(colliders[i].gameObject);
                objectIdentifier.ID = maxID + IDOffset++;
            }
        }
    }
}