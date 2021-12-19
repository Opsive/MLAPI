using GreedyVox.Networked;
using GreedyVox.Networked.Utilities;
using Opsive.UltimateCharacterController.AddOns.Multiplayer.Character;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Editor.Managers;
using Opsive.UltimateCharacterController.Game;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.ThirdPersonController.Character;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class NetworkedCharacterInspector : EditorWindow {
    private Object m_NetworkCharacter;
    [MenuItem ("Tools/GreedyVox/Networked/Character Inspector")]
    private static NetworkedCharacterInspector Init () {
        return EditorWindow.GetWindowWithRect<NetworkedCharacterInspector> (
            new Rect (Screen.width - 300 / 2, Screen.height - 200 / 2, 300, 200), true, "Network Character");
    }
    private void OnGUI () {
        EditorGUILayout.BeginHorizontal ();
        m_NetworkCharacter = EditorGUILayout.ObjectField (m_NetworkCharacter, typeof (Object), true);
        EditorGUILayout.EndHorizontal ();
        if (GUILayout.Button ("Update Character")) {
            if (m_NetworkCharacter == null) {
                ShowNotification (new GUIContent ("No object selected for updating"), 9);
            } else {
                SetupCharacter ((GameObject) m_NetworkCharacter);
                ShowNotification (new GUIContent ("Finished updating character"), 9);
            }
        }
    }
    /// <summary>
    /// Sets up the character to be able to work with networking.
    /// </summary>
    private void SetupCharacter (GameObject obj) {
        // Remove the single player variants of the necessary components.
        ComponentUtility.TryRemoveComponent<AnimatorMonitor> (obj);
        if (ComponentUtility.TryRemoveComponent<UltimateCharacterLocomotionHandler> (obj)) {
            ComponentUtility.TryAddComponent<NetworkCharacterLocomotionHandler> (obj);
        }
        if (ComponentUtility.TryAddComponent<NetworkObject> (obj, out var net)) {
            net.AutoObjectParentSync = false;
        }
        ComponentUtility.TryAddComponent<NetworkedCharacterTransformMonitor> (obj);
        ComponentUtility.TryAddComponent<NetworkedAnimatorMonitor> (obj);
        ComponentUtility.TryAddComponent<NetworkedLookSource> (obj);
        ComponentUtility.TryAddComponent<NetworkedCharacter> (obj);
        ComponentUtility.TryAddComponent<NetworkedInfo> (obj);
        ComponentUtility.TryAddComponent<NetworkedEvent> (obj);
        // Certain components may be necessary if their single player components is added to the character.

        if (ComponentUtility.HasComponent<AttributeManager> (obj)) {
            ComponentUtility.TryAddComponent<NetworkedAttributeMonitor> (obj);
        }
        if (ComponentUtility.HasComponent<Health> (obj)) {
            ComponentUtility.TryAddComponent<NetworkedHealthMonitor> (obj);
        }
        if (ComponentUtility.HasComponent<Respawner> (obj)) {
            ComponentUtility.TryAddComponent<NetworkedRespawnerMonitor> (obj);
        }
        if (ComponentUtility.HasComponent<Destructible> (obj)) {
            ComponentUtility.TryAddComponent<NetworkedDestructibleMonitor> (obj);
        }

        // The RemotePlayerPerspectiveMonitor will switch out the first person materials if the third person Perspective Monitor doesn't exist.
#if THIRD_PERSON_CONTROLLER
        var addRemotePlayerPerspectiveMonitor = ComponentUtility.HasComponent<PerspectiveMonitor> (obj);
#else
        var addRemotePlayerPerspectiveMonitor = true;
#endif
        var invisibleShadowCastor = ManagerUtility.FindInvisibleShadowCaster (this);
        if (addRemotePlayerPerspectiveMonitor) {
            var remotePlayerPerspectiveMonitor = ComponentUtility.TryAddGetComponent<NetworkedRemotePlayerPerspectiveMonitor> (obj);
            if (remotePlayerPerspectiveMonitor.InvisibleMaterial == null) {
                remotePlayerPerspectiveMonitor.InvisibleMaterial = invisibleShadowCastor;
            }
        }

        if (ComponentUtility.TryAddComponent<NetworkedRuntimePickups> (obj)) {
            // TODO: Add a list of items to be added
        }

        // Any invisible shadow castor materials should be swapped out for a default material.
        var renderers = obj.GetComponentsInChildren<Renderer> (true);
        var updatedMaterialCount = 0;
        var defaultShader = Shader.Find ("Standard");
        for (int i = 0; i < renderers.Length; ++i) {
            var materials = renderers[i].sharedMaterials;
            for (int j = 0; j < materials.Length; ++j) {
                if (materials[j] == invisibleShadowCastor) {
                    materials[j] = new Material (defaultShader);
                    updatedMaterialCount++;
                }
            }
            renderers[i].sharedMaterials = materials;
        }
        if (updatedMaterialCount > 0) {
            Debug.Log ("Updated " + updatedMaterialCount + " invisible shadow castor materials. Ensure the correct material has been assigned before continuing.");
        }

        // Add the ObjectInspector to any character or ragdoll colliders. This will allow the collider GameObjects to be identifiable over the network.
        uint maxID = 0;
        var existingIdentifiers = obj.GetComponentsInChildren<ObjectIdentifier> (true);
        for (int i = 0; i < existingIdentifiers.Length; ++i) {
            var collider = existingIdentifiers[i].GetComponent<Collider> ();
            if (collider != null) {
                // The collider may be used for a ragdoll. Ragdoll colliders should not contribute to the max id.
                if (!collider.isTrigger &&
                    (collider.gameObject.layer == LayerManager.Character ||
                        (collider.gameObject.layer == LayerManager.SubCharacter && collider.GetComponent<Rigidbody> () != null))) {
                    continue;
                }
            }

            if (existingIdentifiers[i].ID > maxID) {
                maxID = existingIdentifiers[i].ID;
            }
        }

        // The max available ID has been determined. Add the ObjectIdentifier.
        var colliders = obj.GetComponentsInChildren<Collider> (true);
        uint IDOffset = 1000000000;
        for (int i = 0; i < colliders.Length; ++i) {
            if (colliders[i].isTrigger ||
                (colliders[i].gameObject.layer != LayerManager.Character &&
                    (colliders[i].gameObject.layer != LayerManager.SubCharacter || colliders[i].GetComponent<Rigidbody> () == null))) {
                continue;
            }

            var objectIdentifier = ComponentUtility.TryAddGetComponent<ObjectIdentifier> (colliders[i].gameObject);
            objectIdentifier.ID = maxID + IDOffset;
            IDOffset++;
        }
    }
}