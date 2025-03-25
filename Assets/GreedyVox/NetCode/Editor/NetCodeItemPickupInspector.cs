// using GreedyVox.NetCode;
// using GreedyVox.NetCode.Utilities;
// using Opsive.UltimateCharacterController.Objects.CharacterAssist;
// using Opsive.UltimateCharacterController.Traits;
// using Unity.Netcode;
// using UnityEditor;
// using UnityEngine;

// public class NetCodeItemPickupInspector : EditorWindow {
//     private Object m_NetworkItem;
//     [MenuItem ("Tools/GreedyVox/NetCode/Item Pickup Inspector")]
//     private static NetCodeItemPickupInspector Init () {
//         return EditorWindow.GetWindowWithRect<NetCodeItemPickupInspector> (
//             new Rect (Screen.width - 300 / 2, Screen.height - 200 / 2, 300, 200), true, "Network Item Pickup");
//     }
//     private void OnGUI () {
//         EditorGUILayout.BeginHorizontal ();
//         m_NetworkItem = EditorGUILayout.ObjectField (m_NetworkItem, typeof (Object), true);
//         EditorGUILayout.EndHorizontal ();
//         if (GUILayout.Button ("Update Item")) {
//             if (m_NetworkItem == null) {
//                 ShowNotification (new GUIContent ("No object selected for updating"), 9);
//             } else {
//                 SetupItem ((GameObject) m_NetworkItem);
//                 ShowNotification (new GUIContent ("Finished updating pickup item"), 9);
//             }
//         }
//     }
//     /// <summary>
//     /// Sets up the item to be able to work with networking.
//     /// </summary>
//     private void SetupItem (GameObject obj) {
//         // Remove the single player variants of the necessary components.
//         if (ComponentUtility.TryAddComponent<NetworkObject> (obj, out var net)) {
//             net.AutoObjectParentSync = false;
//         }
//         ComponentUtility.TryAddComponent<NetCodeInfo> (obj);
//         if (ComponentUtility.HasComponent<Respawner> (obj)) {
//             ComponentUtility.TryAddComponent<NetCodeRespawnerMonitor> (obj);
//         }
//         if (obj.TryGetComponent<Remover> (out var rem1) &&
//             ComponentUtility.TryAddComponent<NetCodeRemover> (obj, out var rem2)) {
//             ComponentUtility.RemoveCopyValues (rem1, rem2);
//         }
//         if (obj.TryGetComponent<ItemPickup> (out var com1) &&
//             ComponentUtility.TryAddComponent<NetCodeItemPickup> (obj, out var com2)) {
//             ComponentUtility.RemoveCopyValues (com1, com2);
//         }
//     }
// }