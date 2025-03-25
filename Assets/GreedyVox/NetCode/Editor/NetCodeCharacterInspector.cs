using GreedyVox.NetCode.Character;
using GreedyVox.NetCode.Traits;
using GreedyVox.NetCode.Utilities;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Editor.Managers;
using Opsive.UltimateCharacterController.Game;
using Opsive.UltimateCharacterController.Networking.Character;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.ThirdPersonController.Character;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace GreedyVox.NetCode.Editors
{
    public class NetCodeCharacterInspector : EditorWindow
    {
        [MenuItem("Tools/GreedyVox/NetCode/Characters/Player Inspector")]
        private static NetCodeCharacterInspector Init() =>
        EditorWindow.GetWindowWithRect<NetCodeCharacterInspector>(
        new Rect(Screen.width - 300 / 2, Screen.height - 200 / 2, 300, 200), true, "Network Character");
        private const string IconErrorPath = "d_console.erroricon.sml";
        private const string IconIfoPath = "d_console.infoicon.sml";
        private Object m_NetworkCharacter;
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            m_NetworkCharacter = EditorGUILayout.ObjectField(m_NetworkCharacter, typeof(Object), true);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Update Character"))
            {
                if (m_NetworkCharacter == null)
                {
                    ShowNotification(new GUIContent(" No object selected for updating",
                                         EditorGUIUtility.IconContent(IconErrorPath).image), 15);
                }
                else
                {
                    SetupCharacter((GameObject)m_NetworkCharacter);
                    ShowNotification(new GUIContent(" Finished updating character",
                                         EditorGUIUtility.IconContent(IconIfoPath).image), 15);
                }
            }
        }
        /// <summary>
        /// Sets up the character to be able to work with networking.
        /// </summary>
        private void SetupCharacter(GameObject go)
        {
            if (go == null) return;
            // Remove the single player variants of the necessary components.
            ComponentUtility.TryAddComponent<RemotePlayerPerspectiveMonitor>(go);
            ComponentUtility.TryReplaceComponent<UltimateCharacterLocomotionHandler, NetworkCharacterLocomotionHandler>(go);
            if (!ComponentUtility.TryReplaceComponentsInChildren<AnimatorMonitor, NetCodeAnimatorMonitor>(go))
                Debug.LogError($"Error replacing all the {typeof(AnimatorMonitor)} with {typeof(NetCodeAnimatorMonitor)}");
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
            ComponentUtility.TryAddComponent<NetCodeCharacterName>(go);
            ComponentUtility.TryAddComponent<NetCodeCharacter>(go);
            ComponentUtility.TryAddComponent<NetCodeCharacterAnimatorMonitor>(go);
            ComponentUtility.TryAddComponent<NetCodeCharacterTransformMonitor>(go);
            ComponentUtility.TryAddComponent<NetCodeLookSource>(go);
            // Certain components may be necessary if their single player components is added to the character.
            if (ComponentUtility.HasComponent<AttributeManager>(go))
                ComponentUtility.TryAddComponent<NetCodeAttributeMonitor>(go);
            if (ComponentUtility.HasComponent<Health>(go))
                ComponentUtility.TryAddComponent<NetCodeHealthMonitor>(go);
            if (ComponentUtility.HasComponent<Respawner>(go))
                ComponentUtility.TryAddComponent<NetCodeRespawnerMonitor>(go);
            // The RemotePlayerPerspectiveMonitor will switch out the first person materials if the third person Perspective Monitor doesn't exist.
#if THIRD_PERSON_CONTROLLER
            var addRemotePlayerPerspectiveMonitor = ComponentUtility.HasComponent<PerspectiveMonitor>(go);
#else
        var addRemotePlayerPerspectiveMonitor = true;
#endif
            var invisibleShadowCastor = ManagerUtility.FindInvisibleShadowCaster(this);
            if (addRemotePlayerPerspectiveMonitor)
            {
                var remotePlayerPerspectiveMonitor = ComponentUtility.TryAddGetComponent<NetCodeRemotePlayerPerspectiveMonitor>(go);
                if (remotePlayerPerspectiveMonitor.InvisibleMaterial == null)
                    remotePlayerPerspectiveMonitor.InvisibleMaterial = invisibleShadowCastor;
            }
            // Any invisible shadow castor materials should be swapped out for a default material.
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var updatedMaterialCount = 0;
            var defaultShader = Shader.Find("Standard");
            for (int i = 0; i < renderers.Length; ++i)
            {
                var materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; ++j)
                    if (materials[j] == invisibleShadowCastor)
                    {
                        materials[j] = new Material(defaultShader);
                        updatedMaterialCount++;
                    }
                renderers[i].sharedMaterials = materials;
            }
            if (updatedMaterialCount > 0)
                Debug.Log("Updated " + updatedMaterialCount + " invisible shadow castor materials. Ensure the correct material has been assigned before continuing.");

            // Add the ObjectInspector to any character or ragdoll colliders. This will allow the collider GameObjects to be identifiable over the network.
            uint maxID = 0;
            var existingIdentifiers = go.GetComponentsInChildren<ObjectIdentifier>(true);
            for (int i = 0; i < existingIdentifiers.Length; ++i)
            {
                // The collider may be used for a ragdoll. Ragdoll colliders should not contribute to the max id.
                var collider = existingIdentifiers[i].GetComponent<Collider>();
                if (collider != null)
                    if (!collider.isTrigger &&
                        (collider.gameObject.layer == LayerManager.Character ||
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
                    (colliders[i].gameObject.layer != LayerManager.Character &&
                        (colliders[i].gameObject.layer != LayerManager.SubCharacter || colliders[i].GetComponent<Rigidbody>() == null)))
                    continue;
                var objectIdentifier = ComponentUtility.TryAddGetComponent<ObjectIdentifier>(colliders[i].gameObject);
                objectIdentifier.ID = maxID + IDOffset++;
            }
        }
    }
}