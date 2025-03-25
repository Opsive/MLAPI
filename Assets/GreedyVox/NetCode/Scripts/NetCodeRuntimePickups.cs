// using Opsive.Shared.Game;
// using Opsive.UltimateCharacterController.Inventory;
// using Opsive.UltimateCharacterController.Items;
// using Unity.Netcode;
// using UnityEngine;

// namespace GreedyVox.NetCode {
//     /// <summary>
//     /// A player has entered the room. Ensure the joining player is in sync with the current game state.
//     /// </summary>
//     /// <param name="player">The Photon Player that entered the room.</param>
//     /// <param name="gameObject">The gameObject that the player controls.</param>
//     public class NetCodeRuntimePickups : NetworkBehaviour {
//         [Tooltip ("An array of items that can be picked up at runtime. Any runtime pickup item must be specified within this array.")]
//         [SerializeField] protected Item[] m_RuntimeItems;
//         public override void OnNetworkSpawn () {
//             var inventory = gameObject.GetCachedComponent<InventoryBase> ();
//             if (inventory != null) {
//                 var itemPlacement = gameObject.GetComponentInChildren<ItemPlacement> (true);
//                 if (itemPlacement != null) {
//                     if (m_RuntimeItems != null && m_RuntimeItems.Length > 0) {
//                         // The gameObject needs to be enabled for the item to be initialized.
//                         var activeCharacter = gameObject.activeSelf;
//                         if (!activeCharacter) {
//                             gameObject.SetActive (true);
//                         }
//                         // Add all runtime pickups to the gameObject as soon as the player joins. This ensures the joining gameObject can equip any already picked up items.
//                         for (int i = 0; i < m_RuntimeItems.Length; ++i) {
//                             if (m_RuntimeItems[i] != null && !inventory.HasItem (m_RuntimeItems[i])) {
//                                 var itemGameObject = ObjectPool.Instantiate (m_RuntimeItems[i], Vector3.zero, Quaternion.identity, itemPlacement.transform);
//                                 itemGameObject.name = m_RuntimeItems[i].name;
//                                 itemGameObject.transform.localPosition = Vector3.zero;
//                                 itemGameObject.transform.localRotation = Quaternion.identity;
//                                 inventory.AddItem (itemGameObject.GetComponent<Item> (), false, true);
//                             }
//                         }
//                         if (!activeCharacter) {
//                             gameObject.SetActive (false);
//                         }
//                     }
//                 }
//             }
//         }
//     }
// }