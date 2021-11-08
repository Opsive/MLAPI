using System.Collections.Generic;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Objects;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Small utility methods that interact with PUN.
/// </summary>
namespace GreedyVox.Networked {
    public static class NetworkedUtility {
        private static Dictionary<ulong, ObjectIdentifier> s_SceneIDMap = new Dictionary<ulong, ObjectIdentifier> ();
        private static Dictionary<GameObject, Dictionary<ulong, ObjectIdentifier>> s_IDObjectIDMap = new Dictionary<GameObject, Dictionary<ulong, ObjectIdentifier>> ();
        /// <summary>
        /// Returns the networked friendly ID for the specified GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the ID of.</param>
        /// <param name="itemSlotID">If the object is an item then return the slot ID of the item.</param>
        /// <returns>The ID for the specified GameObject.</returns>
        public static ulong GetID (GameObject gameObject, out int itemSlotID) {
            itemSlotID = -1;
            if (gameObject == null) { return 0; }

            var id = 0UL;
            var hasID = false;
            var obj = gameObject.GetCachedInactiveComponentInParent<NetworkObject> ();
            if (obj != null) {
                id = obj.NetworkObjectId;
                hasID = true;
            } else {
                // Try to get the ObjectIdentifier.
                var objectIdentifier = gameObject.GetCachedComponent<ObjectIdentifier> ();
                if (objectIdentifier != null) {
                    id = objectIdentifier.ID;
                    hasID = true;
                } else {
                    // The object may be an item.
                    var inventory = gameObject.GetCachedParentComponent<InventoryBase> ();
                    if (inventory != null) {
                        for (int i = 0; i < inventory.SlotCount; ++i) {
                            var item = inventory.GetActiveItem (i);
                            if (item == null) { continue; }
                            var visibleObject = item.ActivePerspectiveItem.GetVisibleObject ();
                            if (gameObject == visibleObject) {
                                id = item.ItemIdentifier.ID;
                                itemSlotID = item.SlotID;
                                hasID = true;
                                break;
                            }
                        }
                        // The item may be a holstered item.
                        if (!hasID) {
                            var allItems = inventory.GetAllItems ();
                            for (int i = 0; i < allItems.Count; ++i) {
                                var visibleObject = allItems[i].ActivePerspectiveItem.GetVisibleObject ();
                                if (gameObject == visibleObject) {
                                    id = allItems[i].ItemIdentifier.ID;
                                    itemSlotID = allItems[i].SlotID;
                                    hasID = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            if (!hasID) {
                Debug.LogWarning ($"Error: The object {gameObject.name} does not contain a NetworkObject or ObjectIdentifier. It will not be able to be sent over the network.");
            }
            return id;
        }
        /// <summary>
        /// Retrieves the GameObject with the specified ID.
        /// </summary>
        /// <param name="parent">The parent GameObject to the object with the specified ID.</param>
        /// <param name="id">The ID to search for.</param>
        /// <param name="itemSlotID">If the object is an item then the slot ID will specify which slot the item is from.</param>
        /// <returns>The GameObject with the specified ID. Can be null.</returns>
        public static GameObject RetrieveGameObject (GameObject parent, ulong id, int itemSlotID) {
            if (id == 0) { return null; }
            // The ID can be a PhotonView, ObjectIdentifier, or Item ID. Search for the ObjectIdentifier first and then the PhotonView.
            GameObject gameObject = null;
            if (itemSlotID == -1) {
                Dictionary<ulong, ObjectIdentifier> idObjectIDMap;
                if (parent == null) {
                    idObjectIDMap = s_SceneIDMap;
                } else if (!s_IDObjectIDMap.TryGetValue (parent, out idObjectIDMap)) {
                    idObjectIDMap = new Dictionary<ulong, ObjectIdentifier> ();
                    s_IDObjectIDMap.Add (parent, idObjectIDMap);
                }

                ObjectIdentifier objectIdentifier = null;
                if (!idObjectIDMap.TryGetValue (id, out objectIdentifier)) {
                    // The ID doesn't exist in the cache. Try to find the object.
                    NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue (id, out var hitPhotonView);
                    if (hitPhotonView != null) {
                        gameObject = hitPhotonView.gameObject;
                    } else {
                        // The object isn't a PhotonView. It could be an ObjectIdentifier.
                        var objectIdentifiers = parent == null ? GameObject.FindObjectsOfType<ObjectIdentifier> () :
                            parent.GetComponentsInChildren<ObjectIdentifier> ();
                        if (objectIdentifiers != null) {
                            for (int i = 0; i < objectIdentifiers.Length; ++i) {
                                if (objectIdentifiers[i].ID == id) {
                                    objectIdentifier = objectIdentifiers[i];
                                    break;
                                }
                            }
                        }
                        idObjectIDMap.Add (id, objectIdentifier);
                    }
                }
                if (objectIdentifier != null) { gameObject = objectIdentifier.gameObject; }
            } else { // The ID is an item.
                if (parent == null) {
                    Debug.LogError ("Error: The parent must exist in order to retrieve the item ID.");
                    return null;
                }
                var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier (id);
                if (itemIdentifier == null) {
                    Debug.LogError ($"Error: The ItemIdentifier with id {id} does not exist.");
                    return null;
                }
                var inventory = parent.GetCachedParentComponent<InventoryBase> ();
                if (inventory == null) {
                    Debug.LogError ("Error: The parent does not contain an inventory.");
                    return null;
                }
                var item = inventory.GetItem (itemIdentifier, itemSlotID);
                // The item may not exist if it was removed shortly after it was hit on sending client.
                if (item == null) {
                    return null;
                }
                return item.ActivePerspectiveItem.GetVisibleObject ();
            }
            return gameObject;
        }
    }
}