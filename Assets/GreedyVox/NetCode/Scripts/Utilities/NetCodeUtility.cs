using System.Collections.Generic;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Objects;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Small utility methods that interact with NetCode.
/// </summary>
namespace GreedyVox.NetCode.Utilities
{
    public static class NetCodeUtility
    {
        private static Dictionary<ulong, ObjectIdentifier> s_SceneIDMap = new();
        private static Dictionary<GameObject, Dictionary<ulong, ObjectIdentifier>> s_IDObjectIDMap = new();
        /// <summary>
        /// Unregisters the Object Identifier within the scene ID map.
        /// </summary>
        /// <param name="sceneObjectIdentifier">The Object Identifier that should be unregistered.</param>
        public static void UnregisterSceneObjectIdentifier(ObjectIdentifier sceneObjectIdentifier)
        => s_SceneIDMap.Remove(sceneObjectIdentifier.ID);
        /// <summary>
        /// Registers the Object Identifier within the scene ID map.
        /// </summary>
        /// <param name="sceneObjectIdentifier">The Object Identifier that should be registered.</param>
        public static void RegisterSceneObjectIdentifier(ObjectIdentifier sceneObjectIdentifier)
        {
            if (s_SceneIDMap.ContainsKey(sceneObjectIdentifier.ID))
            {
                Debug.LogError($"Error: The scene object ID {sceneObjectIdentifier.ID} already exists. This can be corrected by running Scene Setup again on this scene.", sceneObjectIdentifier);
                return;
            }
            s_SceneIDMap.Add(sceneObjectIdentifier.ID, sceneObjectIdentifier);
        }
        /// <summary>
        /// Returns the Network ID for the specified GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the ID of.</param>
        /// <param name="itemSlotID">If the object is an item then return the slot ID of the item.</param>
        /// <returns>The ID and HasID pair for the specified GameObject.</returns>
        public static (ulong ID, bool HasID) GetID(GameObject gameObject, out int itemSlotID)
        {
            itemSlotID = -1;
            if (gameObject == null)
                return (0, false);
            var id = 0UL;
            var hasID = false;
            var net = gameObject.GetCachedComponent<NetworkObject>();
            if (net != null)
            {
                id = net.NetworkObjectId;
                hasID = true;
            }
            else
            {
                // Try to get the ObjectIdentifier.
                var objectIdentifier = gameObject.GetCachedComponent<ObjectIdentifier>();
                if (objectIdentifier != null)
                {
                    id = objectIdentifier.ID;
                    hasID = true;
                }
                else
                {
                    // The object may be an item.
                    var inventory = gameObject.GetCachedParentComponent<InventoryBase>();
                    if (inventory != null)
                    {
                        for (int i = 0; i < inventory.SlotCount; ++i)
                        {
                            var item = inventory.GetActiveCharacterItem(i);
                            if (item == null) continue;
                            if (gameObject == item.gameObject)
                            {
                                id = item.ItemIdentifier.ID;
                                itemSlotID = item.SlotID;
                                hasID = true;
                                break;
                            }
                        }
                        // The item may be a holstered item.
                        if (!hasID)
                        {
                            var allItems = inventory.GetAllCharacterItems();
                            for (int i = 0; i < allItems.Count; ++i)
                            {
                                if (gameObject == allItems[i].gameObject)
                                {
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
            if (!hasID)
            {
                Debug.LogWarning($"Error: The object {gameObject.name} does not contain a NetCode or ObjectIdentifier. It will not be able to be sent over the network.");
                return (0, false);
            }
            return (id, true);
        }
        /// <summary>
        /// Retrieves the GameObject with the specified ID.
        /// </summary>
        /// <param name="parent">The parent GameObject to the object with the specified ID.</param>
        /// <param name="id">The ID to search for.</param>
        /// <param name="itemSlotID">If the object is an item then the slot ID will specify which slot the item is from.</param>
        /// <returns>The GameObject with the specified ID. Can be null.</returns>
        public static GameObject RetrieveGameObject(GameObject parent, ulong id, int itemSlotID)
        {
            if (id == 0) return null;
            // The ID can be a NetCode, ObjectIdentifier, or Item ID. Search for the ObjectIdentifier first and then the NetCode.
            GameObject gameObject = null;
            if (itemSlotID == -1)
            {
                Dictionary<ulong, ObjectIdentifier> idObjectIDMap;
                if (parent == null)
                {
                    idObjectIDMap = s_SceneIDMap;
                }
                else if (!s_IDObjectIDMap.TryGetValue(parent, out idObjectIDMap))
                {
                    idObjectIDMap = new Dictionary<ulong, ObjectIdentifier>();
                    s_IDObjectIDMap.Add(parent, idObjectIDMap);
                }
                if (!idObjectIDMap.TryGetValue(id, out var objectIdentifier)
                || (objectIdentifier == null && idObjectIDMap.ContainsKey(id)))
                {
                    // The ID doesn't exist in the cache. Try to find the object.
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var hitPhotonView))
                        gameObject = hitPhotonView?.gameObject;
                    else
                    {
                        // The object isn't a NetCode. It could be an ObjectIdentifier.
                        var objectIdentifiers = parent == null
                            ? GameObject.FindObjectsOfType<ObjectIdentifier>()
                            : parent.GetComponentsInChildren<ObjectIdentifier>();
                        if (objectIdentifiers != null)
                        {
                            for (int i = 0; i < objectIdentifiers.Length; i++)
                            {
                                if (objectIdentifiers[i].ID == id)
                                {
                                    objectIdentifier = objectIdentifiers[i];
                                    break;
                                }
                            }
                        }
                        if (idObjectIDMap.ContainsKey(id))
                            idObjectIDMap[id] = objectIdentifier;
                        else
                            idObjectIDMap.Add(id, objectIdentifier);
                    }
                }
                if (objectIdentifier == null)
                {
                    // The object isn't identified with an ObjectIdentifier. Search the PhotonViews.
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject net))
                        gameObject = net?.gameObject;
                }
                else { gameObject = objectIdentifier.gameObject; }
            }
            else
            { // The ID is an item.
                if (parent == null)
                {
                    Debug.LogError("Error: The parent must exist in order to retrieve the item ID.");
                    return null;
                }
                var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier(id);
                if (itemIdentifier == null)
                {
                    Debug.LogError($"Error: The ItemIdentifier with id {id} does not exist.");
                    return null;
                }
                var inventory = parent.GetCachedParentComponent<InventoryBase>();
                if (inventory == null)
                {
                    Debug.LogError("Error: The parent does not contain an inventory.");
                    return null;
                }
                // The item may not exist if it was removed shortly after it was hit on sending client.
                var item = inventory.GetCharacterItem(itemIdentifier, itemSlotID);
                if (item == null) return null;
                return item.gameObject;
            }
            return gameObject;
        }
    }
}