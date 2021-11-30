using GreedyVox.Networked;
using GreedyVox.Networked.Utilities;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class NetworkedItemPickupInspector : EditorWindow {
    private Object m_NetworkItem;
    [MenuItem ("Tools/GreedyVox/Networked/Item Pickup Inspector")]
    private static NetworkedItemPickupInspector Init () {
        return EditorWindow.GetWindowWithRect<NetworkedItemPickupInspector> (
            new Rect (Screen.width - 300 / 2, Screen.height - 200 / 2, 300, 200), true, "Network Item Pickup");
    }
    private void OnGUI () {
        EditorGUILayout.BeginHorizontal ();
        m_NetworkItem = EditorGUILayout.ObjectField (m_NetworkItem, typeof (Object), true);
        EditorGUILayout.EndHorizontal ();
        if (GUILayout.Button ("Update Item")) {
            if (m_NetworkItem == null) {
                ShowNotification (new GUIContent ("No object selected for updating"), 9);
            } else {
                SetupItem ((GameObject) m_NetworkItem);
                ShowNotification (new GUIContent ("Finished updating pickup item"), 9);
            }
        }
    }
    /// <summary>
    /// Sets up the item to be able to work with networking.
    /// </summary>
    private void SetupItem (GameObject obj) {
        // Remove the single player variants of the necessary components.
        if (ComponentUtility.TryAddComponent<NetworkObject> (obj, out var net)) {
            net.AutoObjectParentSync = false;
        }
        // Certain components may be necessary if their single player components is added.
        if (ComponentUtility.TryRemoveComponent<Remover> (obj)) {
            ComponentUtility.TryAddComponent<NetworkedRemover> (obj);
        }
        if (ComponentUtility.HasComponent<Respawner> (obj)) {
            ComponentUtility.TryAddComponent<NetworkedRespawnerMonitor> (obj);
        }
        if (obj.TryGetComponent<ItemPickup> (out var com1) &&
            ComponentUtility.TryAddComponent<NetworkedItemPickup> (obj, out var com2)) {
            ComponentUtility.RemoveCopyValues (com1, com2);
        }
        ComponentUtility.TryAddComponent<NetworkedLocationMonitor> (obj);
        ComponentUtility.TryAddComponent<NetworkedSyncRate> (obj);
    }
    private void CopyValues (ItemPickup from, NetworkedItemPickup to) {
        var json = JsonUtility.ToJson (from);
        JsonUtility.FromJsonOverwrite (json, to);
    }
}