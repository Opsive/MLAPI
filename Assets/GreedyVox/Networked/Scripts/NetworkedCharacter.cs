using GreedyVox.Networked.Utilities;
using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Camera;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.Items;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Items;
using Opsive.UltimateCharacterController.Items.Actions;
using Opsive.UltimateCharacterController.Items.Actions.PerspectiveProperties;
using Opsive.UltimateCharacterController.Networking.Character;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The Networked Character component manages the RPCs and state of the character on the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedCharacter : NetworkBehaviour, INetworkCharacter {
        private GameObject m_GameObject;
        private UltimateCharacterLocomotion m_CharacterLocomotion;
        private InventoryBase m_Inventory;
        private bool m_ItemsPickedUp;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake () {
            m_GameObject = gameObject;
            m_Inventory = m_GameObject.GetCachedComponent<InventoryBase> ();
            m_CharacterLocomotion = m_GameObject.GetCachedComponent<UltimateCharacterLocomotion> ();
        }
        /// <summary>
        /// Registers for any interested events.
        /// </summary>
        private void Start () {
            if (IsOwner) {
                EventHandler.RegisterEvent<Ability, bool> (m_GameObject, "OnCharacterAbilityActive", AbilityActive);
                EventHandler.RegisterEvent<ItemAbility, bool> (m_GameObject, "OnCharacterItemAbilityActive", ItemAbilityActive);
            } else {
                PickupItems ();
            }
        }
        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        public override void OnDestroy () {
            base.OnDestroy ();
            EventHandler.UnregisterEvent<Ability, bool> (m_GameObject, "OnCharacterAbilityActive", AbilityActive);
            EventHandler.UnregisterEvent<ItemAbility, bool> (m_GameObject, "OnCharacterItemAbilityActive", ItemAbilityActive);
        }
        /// <summary>
        /// The object has been despawned.
        /// </summary>
        public override void OnNetworkDespawn () {
            EventHandler.UnregisterEvent<ulong> ("OnPlayerConnected", OnPlayerConnected);
            EventHandler.UnregisterEvent<ulong> ("OnPlayerDisconnected", OnPlayerDisconnected);
        }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup.
        /// </summary>
        public override void OnNetworkSpawn () {
            if (IsServer) {
                EventHandler.RegisterEvent<ulong> ("OnPlayerConnected", OnPlayerConnected);
                EventHandler.RegisterEvent<ulong> ("OnPlayerDisconnected", OnPlayerDisconnected);
            }
        }
        /// <summary>
        /// A player has disconnected. Perform any cleanup.
        /// </summary>
        /// <param name="player">The Player networking ID that disconnected.</param>
        private void OnPlayerDisconnected (ulong ID) {
            if (OwnerClientId == ID && m_CharacterLocomotion.LookSource != null &&
                m_CharacterLocomotion.LookSource.GameObject != null) {
                // The local character has disconnected. The character no longer has a look source.
                var cameraController = m_CharacterLocomotion.LookSource.GameObject.GetComponent<CameraController> ();
                if (cameraController != null) {
                    cameraController.Character = null;
                }
                EventHandler.ExecuteEvent<ILookSource> (m_GameObject, "OnCharacterAttachLookSource", null);
            }
        }
        /// <summary>
        /// A player has joined. Ensure the joining player is in sync with the current game state.
        /// </summary>
        /// <param name="id">The Player networking ID that connected.</param>
        private void OnPlayerConnected (ulong ID) {
            // Notify the joining player of the ItemIdentifiers that the player has within their inventory.
            if (m_Inventory != null) {
                var items = m_Inventory.GetAllItems ();
                for (int i = 0; i < items.Count; i++) {
                    var item = items[i];
                    PickupItemIdentifierClientRpc (item.ItemIdentifier.ID, m_Inventory.GetItemIdentifierAmount (item.ItemIdentifier));
                    // Usable Items have a separate ItemIdentifiers amount.
                    if (item.DropPrefab != null) {
                        var itemActions = item.ItemActions;
                        for (int j = 0; j < itemActions.Length; j++) {
                            var usableItem = itemActions[j] as IUsableItem;
                            if (usableItem != null) {
                                var consumableItemIdentifierAmount = usableItem.GetConsumableItemIdentifierAmount ();
                                if (consumableItemIdentifierAmount > 0 || consumableItemIdentifierAmount == -1) { // -1 is used by the grenade to indicate that there is only one item.
                                    PickupUsableItemActionClientRpc (item.ItemIdentifier.ID, item.SlotID, itemActions[j].ID,
                                        m_Inventory.GetItemIdentifierAmount (usableItem.GetConsumableItemIdentifier ()), consumableItemIdentifierAmount);
                                }
                            }
                        }
                    }
                }
                // Ensure the correct item is equipped in each slot.
                for (int i = 0; i < m_Inventory.SlotCount; i++) {
                    var item = m_Inventory.GetActiveItem (i);
                    if (item != null) {
                        EquipUnequipItemClientRpc (item.ItemIdentifier.ID, i, true);
                    }
                }
            }
            // ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER will be defined, but it is required here to allow the add-on to be compiled for the first time.
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            // The remote character should have the same abilities active.
            for (int i = 0; i < m_CharacterLocomotion.ActiveAbilityCount; i++) {
                var activeAbility = m_CharacterLocomotion.ActiveAbilities[i];
                var dat = activeAbility?.GetNetworkStartData ();
                if (dat != null) {
                    StartAbilityClientRpc (activeAbility.Index, SerializerObjectArray.Serialize (dat));
                }
            }
            for (int i = 0; i < m_CharacterLocomotion.ActiveItemAbilityCount; i++) {
                var activeItemAbility = m_CharacterLocomotion.ActiveItemAbilities[i];
                var abilities = activeItemAbility.GetNetworkStartData ();
                if (abilities != null) {
                    StartItemAbilityClientRpc (activeItemAbility.Index, SerializerObjectArray.Serialize (abilities));
                }
            }
#endif
        }
        /// <summary>
        /// Pickup isn't called on unequipped items. Ensure pickup is called before the item is equipped.
        /// </summary>
        private void PickupItems () {
            if (!m_ItemsPickedUp) {
                m_ItemsPickedUp = true;
                var items = m_GameObject.GetComponentsInChildren<Item> (true);
                for (int i = 0; i < items.Length; ++i) {
                    items[i].Pickup ();
                }
            }
        }
        /// <summary>
        /// Loads the inventory's default loadout.
        /// </summary>
        public void LoadDefaultLoadout () {
            if (IsServer) {
                LoadoutDefaultClientRpc ();
            } else {
                LoadoutDefaultServerRpc ();
            }
        }
        /// <summary>
        /// Loads the inventory's default loadout on the network.
        /// </summary>
        private void LoadoutDefaultRpc () {
            m_Inventory.LoadDefaultLoadout ();
            EventHandler.ExecuteEvent (m_GameObject, "OnCharacterSnapAnimator");
        }

        [ServerRpc]
        private void LoadoutDefaultServerRpc () {
            if (!IsClient) { LoadoutDefaultRpc (); }
            LoadoutDefaultClientRpc ();
        }

        [ClientRpc]
        private void LoadoutDefaultClientRpc () {
            if (!IsOwner) { LoadoutDefaultRpc (); }
        }
        /// <summary>
        /// The character's ability has been started or stopped.
        /// </summary>
        /// <param name="ability">The ability which was started or stopped.</param>
        /// <param name="active">True if the ability was started, false if it was stopped.</param>
        private void AbilityActive (Ability ability, bool active) {
            if (IsServer) {
                AbilityActiveClientRpc (ability.Index, active);
            } else {
                AbilityActiveServerRpc (ability.Index, active);
            }
        }
        /// <summary>
        /// Activates or deactivates the ability on the network at the specified index.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        /// <param name="active">Should the ability be activated?</param>
        private void AbilityActiveRpc (int abilityIndex, bool active) {
            if (active) {
                m_CharacterLocomotion.TryStartAbility (m_CharacterLocomotion.Abilities[abilityIndex]);
            } else {
                m_CharacterLocomotion.TryStopAbility (m_CharacterLocomotion.Abilities[abilityIndex], true);
            }
        }

        [ServerRpc]
        private void AbilityActiveServerRpc (int abilityIndex, bool active) {
            if (!IsClient) { AbilityActiveRpc (abilityIndex, active); }
            AbilityActiveClientRpc (abilityIndex, active);
        }

        [ClientRpc]
        private void AbilityActiveClientRpc (int abilityIndex, bool active) {
            if (!IsOwner) { AbilityActiveRpc (abilityIndex, active); }
        }
        /// <summary>
        /// Starts the ability on the remote player.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        /// <param name="startData">Any data associated with the ability start.</param>
        private void StartAbilityRpc (int abilityIndex, SerializableObjectArray startData) {
            var ability = m_CharacterLocomotion.Abilities[abilityIndex];
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            if (startData != null) {
                ability.SetNetworkStartData (DeserializerObjectArray.Deserialize (startData));
            }
#endif
            m_CharacterLocomotion.TryStartAbility (ability, true, true);
        }

        [ServerRpc]
        private void StartAbilityServerRpc (int abilityIndex, SerializableObjectArray startData) {
            if (!IsClient) { StartAbilityRpc (abilityIndex, startData); }
            StartAbilityClientRpc (abilityIndex, startData);
        }

        [ClientRpc]
        private void StartAbilityClientRpc (int abilityIndex, SerializableObjectArray startData) {
            if (!IsOwner) { StartAbilityRpc (abilityIndex, startData); }
        }
        /// <summary>
        /// Starts the item ability on the remote player.
        /// </summary>
        /// <param name="itemAbilityIndex">The index of the item ability.</param>
        /// <param name="startData">Any data associated with the item ability start.</param>
        private void StartItemAbilityRpc (int itemAbilityIndex, SerializableObjectArray startData) {
            var itemAbility = m_CharacterLocomotion.ItemAbilities[itemAbilityIndex];
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            if (startData != null) {
                itemAbility.SetNetworkStartData (DeserializerObjectArray.Deserialize (startData));
            }
#endif
            m_CharacterLocomotion.TryStartAbility (itemAbility, true, true);
        }

        [ServerRpc]
        private void StartItemAbilityServerRpc (int itemAbilityIndex, SerializableObjectArray startData) {
            if (!IsClient) { StartItemAbilityRpc (itemAbilityIndex, startData); }
            StartItemAbilityClientRpc (itemAbilityIndex, startData);
        }

        [ClientRpc]
        private void StartItemAbilityClientRpc (int itemAbilityIndex, SerializableObjectArray startData) {
            if (!IsOwner) { StartItemAbilityRpc (itemAbilityIndex, startData); }
        }
        /// <summary>
        /// The character's item ability has been started or stopped.
        /// </summary>
        /// <param name="itemAbility">The item ability which was started or stopped.</param>
        /// <param name="active">True if the ability was started, false if it was stopped.</param>
        private void ItemAbilityActive (ItemAbility itemAbility, bool active) {
            if (IsServer) {
                ItemAbilityActiveClientRpc (itemAbility.Index, active);
            } else {
                ItemAbilityActiveServerRpc (itemAbility.Index, active);
            }
        }
        /// <summary>
        /// Activates or deactivates the item ability on the network at the specified index.
        /// </summary>
        /// <param name="itemAbilityIndex">The index of the item ability.</param>
        /// <param name="active">Should the ability be activated?</param>
        private void ItemAbilityActiveRpc (int itemAbilityIndex, bool active) {
            if (active) {
                m_CharacterLocomotion.TryStartAbility (m_CharacterLocomotion.ItemAbilities[itemAbilityIndex]);
            } else {
                m_CharacterLocomotion.TryStopAbility (m_CharacterLocomotion.ItemAbilities[itemAbilityIndex], true);
            }
        }

        [ServerRpc]
        private void ItemAbilityActiveServerRpc (int itemAbilityIndex, bool active) {
            if (!IsClient) { ItemAbilityActiveRpc (itemAbilityIndex, active); }
            ItemAbilityActiveClientRpc (itemAbilityIndex, active);
        }

        [ClientRpc]
        private void ItemAbilityActiveClientRpc (int itemAbilityIndex, bool active) {
            if (!IsOwner) { ItemAbilityActiveRpc (itemAbilityIndex, active); }
        }
        /// <summary>
        /// Picks up the ItemIdentifier on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifiers that should be equipped.</param>
        /// <param name="amount">The number of ItemIdnetifiers to pickup.</param>
        private void PickupItemIdentifierRpc (uint itemIdentifierID, int amount) {
            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier (itemIdentifierID);
            if (itemIdentifier != null) {
                m_Inventory.Pickup (itemIdentifier, amount, -1, false, false, false);
            }
        }

        [ServerRpc]
        private void PickupItemIdentifierServerRpc (uint itemIdentifierID, int amount) {
            if (!IsClient) { PickupItemIdentifierRpc (itemIdentifierID, amount); }
            PickupItemIdentifierClientRpc (itemIdentifierID, amount);
        }

        [ClientRpc]
        private void PickupItemIdentifierClientRpc (uint itemIdentifierID, int amount) {
            if (!IsOwner) { PickupItemIdentifierRpc (itemIdentifierID, amount); }
        }
        /// <summary>
        /// Picks up the IUsableItem ItemIdentifier on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that should be equipped.</param>
        /// <param name="slotID">The slot of the item being picked up.</param>
        /// <param name="itemActionID">The ID of the IUsableItem being picked up.</param>
        /// <param name="itemActionAmount">The IUsableItem amount within the inventory.</param>
        /// <param name="consumableItemIdentifierAmount">The ConsumableItemIdentifier amount loaded within the IUsableItem.</param>
        private void PickupUsableItemActionRpc (uint itemIdentifierID, int slotID, int itemActionID, int itemActionAmount, int consumableItemIdentifierAmount) {
            var itemType = ItemIdentifierTracker.GetItemIdentifier (itemIdentifierID);
            if (itemType != null) {
                var item = m_Inventory.GetItem (itemType, slotID);
                if (item != null) {
                    var usableItemAction = item.GetItemAction (itemActionID) as IUsableItem;
                    if (usableItemAction != null) {
                        // The IUsableItem has two counts: the first count is from the inventory, and the second count is set on the actual ItemAction.
                        m_Inventory.Pickup (usableItemAction.GetConsumableItemIdentifier (), itemActionAmount, -1, false, false, false);
                        usableItemAction.SetConsumableItemIdentifierAmount (consumableItemIdentifierAmount);
                    }
                }
            }
        }

        [ServerRpc]
        private void PickupUsableItemActionServerRpc (uint itemIdentifierID, int slotID, int itemActionID, int itemActionAmount, int consumableItemIdentifierAmount) {
            if (!IsClient) { PickupUsableItemActionRpc (itemIdentifierID, slotID, itemActionID, itemActionAmount, consumableItemIdentifierAmount); }
            PickupUsableItemActionClientRpc (itemIdentifierID, slotID, itemActionID, itemActionAmount, consumableItemIdentifierAmount);
        }

        [ClientRpc]
        private void PickupUsableItemActionClientRpc (uint itemIdentifierID, int slotID, int itemActionID, int itemActionAmount, int consumableItemIdentifierAmount) {
            if (!IsOwner) { PickupUsableItemActionRpc (itemIdentifierID, slotID, itemActionID, itemActionAmount, consumableItemIdentifierAmount); }
        }
        /// <summary>
        /// Equips or unequips the item with the specified ItemIdentifier and slot.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that should be equipped.</param>
        /// <param name="slotID">The slot of the item that should be equipped.</param>
        /// <param name="equip">Should the item be equipped? If false it will be unequipped.</param>
        public void EquipUnequipItem (uint itemIdentifierID, int slotID, bool equip) {
            if (IsServer) {
                EquipUnequipItemClientRpc (itemIdentifierID, slotID, equip);
            } else {
                EquipUnequipItemServerRpc (itemIdentifierID, slotID, equip);
            }
        }
        /// <summary>
        /// Equips or unequips the item on the network with the specified ItemIdentifier and slot.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that should be equipped.</param>
        /// <param name="slotID">The slot of the item that should be equipped.</param>
        /// <param name="equip">Should the item be equipped? If false it will be unequipped.</param>
        private void EquipUnequipItemRpc (uint itemIdentifierID, int slotID, bool equip) {
            // The character has to be alive to equip.
            if (m_CharacterLocomotion.Alive) {
                // Ensure pickup is called before the item is equipped.
                if (equip) { PickupItems (); }
                var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier (itemIdentifierID);
                if (itemIdentifier != null) {
                    var item = m_Inventory.GetItem (itemIdentifier, slotID);
                    if (item != null) {
                        if (equip) { m_Inventory.EquipItem (itemIdentifier, slotID, true); } else {
                            m_Inventory.UnequipItem (itemIdentifier, slotID);
                        }
                    }
                }
            }
        }

        [ServerRpc]
        private void EquipUnequipItemServerRpc (uint itemIdentifierID, int slotID, bool equip) {
            if (!IsClient) { EquipUnequipItemRpc (itemIdentifierID, slotID, equip); }
            EquipUnequipItemClientRpc (itemIdentifierID, slotID, equip);
        }

        [ClientRpc]
        private void EquipUnequipItemClientRpc (uint itemIdentifierID, int slotID, bool equip) {
            if (!IsOwner) { EquipUnequipItemRpc (itemIdentifierID, slotID, equip); }
        }
        /// <summary>
        /// The ItemIdentifier has been picked up.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that was picked up.</param>
        /// <param name="amount">The number of ItemIdentifier picked up.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="immediatePickup">Was the item be picked up immediately?</param>
        /// <param name="forceEquip">Should the item be force equipped?</param>
        public void ItemIdentifierPickup (uint itemIdentifierID, int amount, int slotID, bool immediatePickup, bool forceEquip) {
            if (IsServer) {
                ItemIdentifierPickupClientRpc (itemIdentifierID, amount, slotID, immediatePickup, forceEquip);
            } else {
                ItemIdentifierPickupServerRpc (itemIdentifierID, amount, slotID, immediatePickup, forceEquip);
            }
        }
        /// <summary>
        /// The ItemIdentifier has been picked up on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that was picked up.</param>
        /// <param name="amount">The number of ItemIdentifier picked up.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="immediatePickup">Was the item be picked up immediately?</param>
        /// <param name="forceEquip">Should the item be force equipped?</param>
        private void ItemIdentifierPickupRpc (uint itemIdentifierID, int amount, int slotID, bool immediatePickup, bool forceEquip) {
            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier (itemIdentifierID);
            if (itemIdentifier != null) {
                m_Inventory.Pickup (itemIdentifier, amount, slotID, immediatePickup, forceEquip);
            }
        }

        [ServerRpc]
        private void ItemIdentifierPickupServerRpc (uint itemIdentifierID, int amount, int slotID, bool immediatePickup, bool forceEquip) {
            if (!IsClient) { ItemIdentifierPickupRpc (itemIdentifierID, amount, slotID, immediatePickup, forceEquip); }
            ItemIdentifierPickupClientRpc (itemIdentifierID, amount, slotID, immediatePickup, forceEquip);
        }

        [ClientRpc]
        private void ItemIdentifierPickupClientRpc (uint itemIdentifierID, int amount, int slotID, bool immediatePickup, bool forceEquip) {
            if (!IsOwner) { ItemIdentifierPickupRpc (itemIdentifierID, amount, slotID, immediatePickup, forceEquip); }
        }
        /// <summary>
        /// Removes all of the items from the inventory.
        /// </summary>
        public void RemoveAllItems () {
            if (IsServer) {
                RemoveAllItemsClientRpc ();
            } else {
                RemoveAllItemsServerRpc ();
            }
        }
        /// <summary>
        /// Removes all of the items from the inventory on the network.
        /// </summary>
        private void RemoveAllItemsRpc () {
            m_Inventory.RemoveAllItems (true);
        }

        [ServerRpc]
        private void RemoveAllItemsServerRpc () {
            if (!IsClient) { RemoveAllItemsRpc (); }
            RemoveAllItemsClientRpc ();
        }

        [ClientRpc]
        private void RemoveAllItemsClientRpc () {
            if (!IsOwner) { RemoveAllItemsRpc (); }
        }
        /// <summary>
        /// Returns the ItemAction with the specified slot and ID.
        /// </summary>
        /// <param name="slotID">The slot that the ItemAction belongs to.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <returns>The ItemAction with the specified slot and ID</returns>
        private ItemAction GetItemAction (int slotID, int actionID) {
            var item = m_Inventory.GetActiveItem (slotID);
            if (item != null) { return item.GetItemAction (actionID); }
            return null;
        }

#if ULTIMATE_CHARACTER_CONTROLLER_SHOOTER
        /// <summary>
        /// Fires the weapon.
        /// </summary>
        /// <param name="itemAction">The ItemAction that is being fired.</param>
        /// <param name="strength">(0 - 1) value indicating the amount of strength to apply to the shot.</param>
        public void Fire (ItemAction itemAction, float strength) {
            if (IsServer) {
                FireClientRpc (itemAction.Item.SlotID, itemAction.ID, strength);
            } else {
                FireServerRpc (itemAction.Item.SlotID, itemAction.ID, strength);
            }
        }
        /// <summary>
        /// Fires the weapon on the network.
        /// </summary>
        /// <param name="slotID">The slot of the ShootableWeapon being fired.</param>
        /// <param name="actionID">The ID of the ShootableWeapon being fired.</param>
        /// <param name="strength">(0 - 1) value indicating the amount of strength to apply to the shot.</param>
        private void FireRpc (int slotID, int actionID, float strength) {
            var itemAction = GetItemAction (slotID, actionID) as ShootableWeapon;
            if (itemAction != null) {
                itemAction.Fire (strength);
            }
        }

        [ServerRpc]
        private void FireServerRpc (int slotID, int actionID, float strength) {
            if (!IsClient) { FireRpc (slotID, actionID, strength); }
            FireClientRpc (slotID, actionID, strength);
        }

        [ClientRpc]
        private void FireClientRpc (int slotID, int actionID, float strength) {
            if (!IsOwner) { FireRpc (slotID, actionID, strength); }
        }
        /// <summary>
        /// Starts to reload the item.
        /// </summary>
        /// <param name="itemAction">The ItemAction that is being reloaded.</param>
        public void StartItemReload (ItemAction itemAction) {
            if (IsServer) {
                StartItemReloadClientRpc (itemAction.Item.SlotID, itemAction.ID);
            } else {
                StartItemReloadServerRpc (itemAction.Item.SlotID, itemAction.ID);
            }
        }
        /// <summary>
        /// Starts to reload the item on the network.
        /// </summary>
        /// <param name="slotID">The slot of the ShootableWeapon being reloaded.</param>
        /// <param name="actionID">The ID of the ShootableWeapon being reloaded.</param>
        private void StartItemReloadRpc (int slotID, int actionID) {
            var itemAction = GetItemAction (slotID, actionID);
            if (itemAction != null) {
                (itemAction as ShootableWeapon).StartItemReload ();
            }
        }

        [ServerRpc]
        private void StartItemReloadServerRpc (int slotID, int actionID) {
            if (!IsClient) { StartItemReloadRpc (slotID, actionID); }
            StartItemReloadClientRpc (slotID, actionID);
        }

        [ClientRpc]
        private void StartItemReloadClientRpc (int slotID, int actionID) {
            if (!IsOwner) { StartItemReloadRpc (slotID, actionID); }
        }
        /// <summary>
        /// Reloads the item.
        /// </summary>
        /// <param name="itemAction">The ItemAction that is being reloaded.</param>
        /// <param name="fullClip">Should the full clip be force reloaded?</param>
        public void ReloadItem (ItemAction itemAction, bool fullClip) {
            if (IsServer) {
                ReloadItemClientRpc (itemAction.Item.SlotID, itemAction.ID, fullClip);
            } else {
                ReloadItemServerRpc (itemAction.Item.SlotID, itemAction.ID, fullClip);
            }
        }
        /// <summary>
        /// Reloads the item on the network.
        /// </summary>
        /// <param name="slotID">The slot of the ShootableWeapon being reloaded.</param>
        /// <param name="actionID">The ID of the ShootableWeapon being reloaded.</param>
        /// <param name="fullClip">Should the full clip be force reloaded?</param>
        private void ReloadItemRpc (int slotID, int actionID, bool fullClip) {
            var itemAction = GetItemAction (slotID, actionID) as ShootableWeapon;
            if (itemAction != null) {
                itemAction.ReloadItem (fullClip);
            }
        }

        [ServerRpc]
        private void ReloadItemServerRpc (int slotID, int actionID, bool fullClip) {
            if (!IsClient) { ReloadItemRpc (slotID, actionID, fullClip); }
            ReloadItemClientRpc (slotID, actionID, fullClip);
        }

        [ClientRpc]
        private void ReloadItemClientRpc (int slotID, int actionID, bool fullClip) {
            if (!IsOwner) { ReloadItemRpc (slotID, actionID, fullClip); }
        }
        /// <summary>
        /// The item has finished reloading.
        /// </summary>
        /// <param name="itemAction">The ItemAction that is being reloaded.</param>
        /// <param name="success">Was the item reloaded successfully?</param>
        /// <param name="immediateReload">Should the item be reloaded immediately?</param>
        public void ItemReloadComplete (ItemAction itemAction, bool success, bool immediateReload) {
            if (IsServer) {
                ItemReloadCompleteClientRpc (itemAction.Item.SlotID, itemAction.ID, success, immediateReload);
            } else {
                ItemReloadCompleteServerRpc (itemAction.Item.SlotID, itemAction.ID, success, immediateReload);
            }
        }
        /// <summary>
        /// The item has finished reloading on the network.
        /// </summary>
        /// <param name="slotID">The slot of the ShootableWeapon being reloaded.</param>
        /// <param name="actionID">The ID of the ShootableWeapon being reloaded.</param>
        /// <param name="success">Was the item reloaded successfully?</param>
        /// <param name="immediateReload">Should the item be reloaded immediately?</param>
        private void ItemReloadCompleteRpc (int slotID, int actionID, bool success, bool immediateReload) {
            var itemAction = GetItemAction (slotID, actionID) as ShootableWeapon;
            if (itemAction != null) {
                itemAction.ItemReloadComplete (success, immediateReload);
            }
        }

        [ServerRpc]
        private void ItemReloadCompleteServerRpc (int slotID, int actionID, bool success, bool immediateReload) {
            if (!IsClient) { ItemReloadCompleteClientRpc (slotID, actionID, success, immediateReload); }
            ItemReloadCompleteClientRpc (slotID, actionID, success, immediateReload);
        }

        [ClientRpc]
        private void ItemReloadCompleteClientRpc (int slotID, int actionID, bool success, bool immediateReload) {
            if (!IsOwner) { ItemReloadCompleteRpc (slotID, actionID, success, immediateReload); }
        }
#endif

        // #if ULTIMATE_CHARACTER_CONTROLLER_SHOOTER
        //         /// <summary>
        //         /// The shootable weapon hitscan caused damage.
        //         /// </summary>
        //         /// <param name="itemAction">The ItemAction that caused the damage.</param>
        //         /// <param name="hitGameObject">The GameObject that was damaged.</param>
        //         /// <param name="damageAmount">The amount of damage taken.</param>
        //         /// <param name="position">The position of the hitscan.</param>
        //         /// <param name="direction">The direction of the hitscan.</param>
        //         /// <param name="strength">The stength of the fire.</param>
        //         public void ShootableHitscanDamage (ItemAction itemAction, GameObject hitGameObject, float damageAmount, Vector3 position, Vector3 direction, float strength, Collider hitCollider) {
        //             var hitGameObjectID = NetworkedUtility.GetID (hitGameObject, out var slotID);
        //             var hitItemSlotID = -1;
        //             ulong hitColliderID = 0;
        //             if (hitCollider != null) {
        //                 hitColliderID = NetworkedUtility.GetID (hitCollider.gameObject, out hitItemSlotID);
        //             }
        //             Debug.Log ("hitscandamage " + hitGameObject + " " + hitGameObjectID);

        //             if (IsServer) {
        //                 ShootableHitscanDamageClientRpc (itemAction.Item.SlotID, itemAction.ID, hitGameObjectID,
        //                     slotID, damageAmount, position, direction, strength, hitColliderID, hitItemSlotID);
        //             } else {
        //                 ShootableHitscanDamageServerRpc (itemAction.Item.SlotID, itemAction.ID, hitGameObjectID,
        //                     slotID, damageAmount, position, direction, strength, hitColliderID, hitItemSlotID);
        //             }
        //             ((ShootableWeapon) itemAction) (hitGameObject.GetCachedComponent<Health> (), damageAmount, position, direction, strength, hitCollider);
        //         }

        //         /// <summary>
        //         /// The melee weapon hit a collider on the network.
        //         /// </summary>
        //         /// <param name="slotID">The slot of the ShootableWeapon that caused the damage.</param>
        //         /// <param name="actionID">The ID of the ShootableWeapon that caused the damage.</param>
        //         /// <param name="hitGameObjectID">The ID of the GameObject that was hit.</param>
        //         /// <param name="hitSlotID">If the hit GameObject is an item then the slot ID of the item will be specified.</param>
        //         /// <param name="damageAmount">The amount of damage taken.</param>
        //         /// <param name="position">The position of the hitscan.</param>
        //         /// <param name="direction">The direction of the hitscan.</param>
        //         /// <param name="strength">The stength of the fire.</param>
        //         /// <param name="hitColliderID">The NetworkObject or ObjectIdentifier ID of the Collider that was hit.</param>
        //         /// <param name="hitItemSlotID">If the hit collider is an item then the slot ID of the item will be specified.</param>
        //         private void ShootableHitscanDamageRpc (int slotID, int actionID, ulong hitGameObjectID, int hitSlotID, float damageAmount, Vector3 position, Vector3 direction, float strength, ulong hitColliderID, int hitItemSlotID) {
        //             var shootableWeapon = GetItemAction (slotID, actionID) as ShootableWeapon;
        //             if (shootableWeapon == null) {
        //                 return;
        //             }

        //             var hitGameObject = NetworkedUtility.RetrieveGameObject (null, hitGameObjectID, hitSlotID);
        //             if (hitGameObject == null) {
        //                 return;
        //             }

        //             var hitCollider = NetworkedUtility.RetrieveGameObject (hitGameObject, hitColliderID, hitItemSlotID);
        //             shootableWeapon.HitscanDamage (hitGameObject.GetCachedComponent<Health> (), damageAmount, position, direction, strength, hitCollider != null ? hitCollider.GetCachedComponent<Collider> () : null);
        //         }

        //         [ServerRpc]
        //         private void ShootableHitscanDamageServerRpc (int slotID, int actionID, ulong hitGameObjectID, int hitSlotID, float damageAmount, Vector3 position, Vector3 direction, float strength, ulong hitColliderID, int hitItemSlotID) {
        //             if (!IsClient) { ShootableHitscanDamageClientRpc (slotID, actionID, hitGameObjectID, hitSlotID, damageAmount, position, direction, strength, hitColliderID, hitItemSlotID); }
        //             ShootableHitscanDamageClientRpc (slotID, actionID, hitGameObjectID, hitSlotID, damageAmount, position, direction, strength, hitColliderID, hitItemSlotID);
        //         }

        //         [ClientRpc]
        //         private void ShootableHitscanDamageClientRpc (int slotID, int actionID, ulong hitGameObjectID, int hitSlotID, float damageAmount, Vector3 position, Vector3 direction, float strength, ulong hitColliderID, int hitItemSlotID) {
        //             if (!IsOwner) { ShootableHitscanDamageRpc (slotID, actionID, hitGameObjectID, hitSlotID, damageAmount, position, direction, strength, hitColliderID, hitItemSlotID); }
        //         }
        // #endif

#if ULTIMATE_CHARACTER_CONTROLLER_MELEE
        /// <summary>
        /// The melee weapon hit a collider.
        /// </summary>
        /// <param name="itemAction">The ItemAction that caused the collision.</param>
        /// <param name="hitboxIndex">The index of the hitbox that caused the collision.</param>
        /// <param name="raycastHit">The raycast that caused the collision.</param>
        /// <param name="hitGameObject">The GameObject that was hit.</param>
        /// <param name="hitCharacterLocomotion">The hit Ultimate Character Locomotion component.</param>
        public void MeleeHitCollider (ItemAction itemAction, int hitboxIndex, RaycastHit raycastHit, GameObject hitGameObject, UltimateCharacterLocomotion hitCharacterLocomotion) {
            var slotID = -1;
            var hitGameObjectID = NetworkedUtility.GetID (hitGameObject, out slotID);
            var hitCharacterLocomotionViewID = -1L;
            if (hitCharacterLocomotion != null) {
                var hitCharacterLocomotionView = hitCharacterLocomotion.gameObject.GetCachedComponent<NetworkObject> ();
                if (hitCharacterLocomotionView == null) {
                    Debug.LogError ($"Error: The character {hitCharacterLocomotion.gameObject} must have a NetworkObject component added.");
                    return;
                }
                hitCharacterLocomotionViewID = (long) hitCharacterLocomotionView.NetworkObjectId;
            }
            if (IsServer) {
                MeleeHitColliderClientRpc (itemAction.Item.SlotID, itemAction.ID, hitboxIndex, raycastHit.point,
                    raycastHit.normal, hitCharacterLocomotionViewID, hitGameObjectID, slotID);
            } else {
                MeleeHitColliderServerRpc (itemAction.Item.SlotID, itemAction.ID, hitboxIndex, raycastHit.point,
                    raycastHit.normal, hitCharacterLocomotionViewID, hitGameObjectID, slotID);
            }
        }
        /// <summary>
        /// The melee weapon hit a collider on the network.
        /// </summary>
        /// <param name="slotID">The slot of the MeleeWeapon that caused the collision.</param>
        /// <param name="actionID">The ID of the MeleeWeapon that caused the collision.</param>
        /// <param name="hitboxIndex">The index of the hitbox that caused the collision.</param>
        /// <param name="hitCharacterLocomotionViewID">The NetworkObject ID of the hit Ultimate Character Locomotion component.</param>
        /// <param name="hitGameObjectID">The ID of the GameObject that was hit.</param>
        /// <param name="hitSlotID">If the hit GameObject is an item then the slot ID of the item will be specified.</param>
        private void MeleeHitColliderRpc (int slotID, int actionID, int hitboxIndex, Vector3 raycastHitPoint,
            Vector3 raycastHitNormal, long hitCharacterLocomotionViewID, ulong hitGameObjectID, int hitSlotID) {
            var meleeWeapon = GetItemAction (slotID, actionID) as MeleeWeapon;
            if (meleeWeapon != null) {
                // Retrieve the hit character before getting the hit GameObject so RetrieveGameObject will know the parent GameObject (if it exists).
                UltimateCharacterLocomotion characterLocomotion = null;
                if (hitCharacterLocomotionViewID != -1) {
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue ((ulong) hitCharacterLocomotionViewID, out var obj)) {
                        characterLocomotion = obj.gameObject.GetCachedComponent<UltimateCharacterLocomotion> ();
                    }
                }
                var hitGameObject = NetworkedUtility.RetrieveGameObject ((characterLocomotion != null ? characterLocomotion.gameObject : null), hitGameObjectID, hitSlotID);
                if (hitGameObject != null) {
                    var hitCollider = hitGameObject.GetCachedComponent<Collider> ();
                    if (hitCollider != null) {
                        // A RaycastHit cannot be sent over the network. Try to recreate it locally based on the position and normal values.
                        var ray = new Ray (raycastHitPoint + raycastHitNormal * 1f, -raycastHitNormal);
                        if (!hitCollider.Raycast (ray, out var hit, 2f)) {
                            // The object has moved. Do a larger cast to try to find the object.
                            if (!Physics.SphereCast (ray, 1f, out hit, 2f, 1 << hitGameObject.layer, QueryTriggerInteraction.Ignore)) {
                                // The object can't be found. Return.
                                return;
                            }
                        }
                        var hitHealth = hitGameObject.GetCachedParentComponent<Health> ();
                        var hitbox = (meleeWeapon.ActivePerspectiveProperties as IMeleeWeaponPerspectiveProperties).Hitboxes[hitboxIndex];
                        meleeWeapon.HitCollider (hitbox, hit, hitGameObject, hitCollider, hitHealth);
                    }
                }
            }
        }

        [ServerRpc]
        private void MeleeHitColliderServerRpc (int slotID, int actionID, int hitboxIndex, Vector3 raycastHitPoint,
            Vector3 raycastHitNormal, long hitCharacterLocomotionViewID, ulong hitGameObjectID, int hitSlotID) {
            if (!IsClient) {
                MeleeHitColliderRpc (slotID, actionID, hitboxIndex, raycastHitPoint, raycastHitNormal,
                    hitCharacterLocomotionViewID, hitGameObjectID, hitSlotID);
            }
            MeleeHitColliderClientRpc (slotID, actionID, hitboxIndex, raycastHitPoint, raycastHitNormal,
                hitCharacterLocomotionViewID, hitGameObjectID, hitSlotID);
        }

        [ClientRpc]
        private void MeleeHitColliderClientRpc (int slotID, int actionID, int hitboxIndex, Vector3 raycastHitPoint,
            Vector3 raycastHitNormal, long hitCharacterLocomotionViewID, ulong hitGameObjectID, int hitSlotID) {
            if (!IsOwner) {
                MeleeHitColliderRpc (slotID, actionID, hitboxIndex, raycastHitPoint, raycastHitNormal,
                    hitCharacterLocomotionViewID, hitGameObjectID, hitSlotID);
            }
        }
#endif

        /// <summary>
        /// Throws the throwable object.
        /// </summary>
        /// <param name="itemAction">The ThrowableItem that is performing the throw.</param>
        public void ThrowItem (ItemAction itemAction) {
            if (IsServer) {
                ThrowItemClientRpc (itemAction.Item.SlotID, itemAction.ID);
            } else {
                ThrowItemServerRpc (itemAction.Item.SlotID, itemAction.ID);
            }
        }
        /// <summary>
        /// Throws the throwable object on the network.
        /// </summary>
        /// <param name="slotID">The slot of the ThrowableItem that is performing the throw.</param>
        /// <param name="actionID">The ID of the ThrowableItem that is performing the throw.</param>
        private void ThrowItemRpc (int slotID, int actionID) {
            var itemAction = GetItemAction (slotID, actionID) as ThrowableItem;
            if (itemAction != null) {
                itemAction.ThrowItem ();
            }
        }

        [ServerRpc]
        private void ThrowItemServerRpc (int slotID, int actionID) {
            if (!IsClient) { ThrowItemRpc (slotID, actionID); }
            ThrowItemClientRpc (slotID, actionID);
        }

        [ClientRpc]
        private void ThrowItemClientRpc (int slotID, int actionID) {
            if (!IsOwner) { ThrowItemRpc (slotID, actionID); }
        }
        /// <summary>
        /// Enables the object mesh renderers for the ThrowableItem.
        /// </summary>
        /// <param name="itemAction">The ThrowableItem that is having the renderers enabled.</param>
        public void EnableThrowableObjectMeshRenderers (ItemAction itemAction) {
            if (IsServer) {
                EnableThrowableObjectMeshRenderersClientRpc (itemAction.Item.SlotID, itemAction.ID);
            } else {
                EnableThrowableObjectMeshRenderersServerRpc (itemAction.Item.SlotID, itemAction.ID);
            }
        }
        /// <summary>
        /// Enables the object mesh renderers for the ThrowableItem on the network.
        /// </summary>
        /// <param name="slotID">The slot of the ThrowableItem that is having the renderers enabled.</param>
        /// <param name="actionID">The ID of the ThrowableItem that is having the renderers enabled.</param>
        private void EnableThrowableObjectMeshRenderersRpc (int slotID, int actionID) {
            var itemAction = GetItemAction (slotID, actionID) as ThrowableItem;
            if (itemAction != null) {
                itemAction.EnableObjectMeshRenderers (true);
            }
        }

        [ServerRpc]
        private void EnableThrowableObjectMeshRenderersServerRpc (int slotID, int actionID) {
            if (!IsClient) { EnableThrowableObjectMeshRenderersRpc (slotID, actionID); }
            EnableThrowableObjectMeshRenderersClientRpc (slotID, actionID);
        }

        [ClientRpc]
        private void EnableThrowableObjectMeshRenderersClientRpc (int slotID, int actionID) {
            if (!IsOwner) { EnableThrowableObjectMeshRenderersRpc (slotID, actionID); }
        }
        /// <summary>
        /// Starts or stops the begin or end actions.
        /// </summary>
        /// <param name="itemAction">The MagicItem that is starting or stopping the actions.</param>
        /// <param name="beginActions">Should the begin actions be started?</param>
        /// <param name="start">Should the actions be started?</param>
        public void StartStopBeginEndMagicActions (ItemAction itemAction, bool beginActions, bool start) {
            if (IsServer) {
                StartStopBeginEndMagicActionsClientRpc (itemAction.Item.SlotID, itemAction.ID, beginActions, start);
            } else {
                StartStopBeginEndMagicActionsServerRpc (itemAction.Item.SlotID, itemAction.ID, beginActions, start);
            }
        }
        /// <summary>
        /// Starts or stops the begin or end actions on the network.
        /// </summary>
        /// <param name="slotID">The slot of the MagicItem that is starting or stopping the action.</param>
        /// <param name="actionID">The ID of the MagicItem that is starting or stopping the action.</param>
        /// <param name="beginActions">Should the begin actions be started?</param>
        /// <param name="start">Should the actions be started?</param>
        public void StartStopBeginEndMagicActionsRpc (int slotID, int actionID, bool beginActions, bool start) {
            var itemAction = GetItemAction (slotID, actionID) as MagicItem;
            if (itemAction != null) {
                itemAction.StartStopBeginEndActions (beginActions, start, false);
            }
        }

        [ServerRpc]
        public void StartStopBeginEndMagicActionsServerRpc (int slotID, int actionID, bool beginActions, bool start) {
            if (!IsClient) { StartStopBeginEndMagicActionsRpc (slotID, actionID, beginActions, start); }
            StartStopBeginEndMagicActionsClientRpc (slotID, actionID, beginActions, start);
        }

        [ClientRpc]
        public void StartStopBeginEndMagicActionsClientRpc (int slotID, int actionID, bool beginActions, bool start) {
            if (!IsOwner) { StartStopBeginEndMagicActionsRpc (slotID, actionID, beginActions, start); }
        }
        /// <summary>
        /// Casts a magic CastAction.
        /// </summary>
        /// <param name="itemAction">The MagicItem that is performing the cast.</param>
        /// <param name="index">The index of the CastAction.</param>
        /// <param name="castID">The ID of the cast.</param>
        /// <param name="direction">The direction of the cast.</param>
        /// <param name="targetPosition">The target position of the cast.</param>
        public void MagicCast (ItemAction itemAction, int index, uint castID, Vector3 direction, Vector3 targetPosition) {
            if (IsServer) {
                MagicCastClientRpc (itemAction.Item.SlotID, itemAction.ID, index, castID, direction, targetPosition);
            } else {
                MagicCastServerRpc (itemAction.Item.SlotID, itemAction.ID, index, castID, direction, targetPosition);
            }
        }
        /// <summary>
        /// Casts a magic CastAction on the network.
        /// </summary>
        /// <param name="slotID">The slot of the MagicItem that is performing the cast.</param>
        /// <param name="actionID">The ID of the MagicItem that is performing the cast.</param>
        /// <param name="index">The index of the CastAction.</param>
        /// <param name="castID">The ID of the cast.</param>
        /// <param name="direction">The direction of the cast.</param>
        /// <param name="targetPosition">The target position of the cast.</param>
        private void MagicCastRpc (int slotID, int actionID, int index, uint castID, Vector3 direction, Vector3 targetPosition) {
            var itemAction = GetItemAction (slotID, actionID) as MagicItem;
            if (itemAction != null) {
                var castAction = itemAction.CastActions[index];
                if (castAction != null) {
                    castAction.CastID = castID;
                    castAction.Cast (itemAction.MagicItemPerspectiveProperties.OriginLocation, direction, targetPosition);
                }
            }
        }

        [ServerRpc]
        private void MagicCastServerRpc (int slotID, int actionID, int index, uint castID, Vector3 direction, Vector3 targetPosition) {
            if (!IsClient) { MagicCastRpc (slotID, actionID, index, castID, direction, targetPosition); }
            MagicCastClientRpc (slotID, actionID, index, castID, direction, targetPosition);
        }

        [ClientRpc]
        private void MagicCastClientRpc (int slotID, int actionID, int index, uint castID, Vector3 direction, Vector3 targetPosition) {
            if (!IsOwner) { MagicCastRpc (slotID, actionID, index, castID, direction, targetPosition); }
        }
        // public void MagicImpact (ItemAction itemAction, uint castID, GameObject source, GameObject target, Vector3 position, Vector3 normal) { }
        /// <summary>
        /// Performs the magic impact.
        /// </summary>
        /// <param name="itemAction">The MagicItem that is performing the impact.</param>
        /// <param name="castID">The ID of the cast.</param>
        /// <param name="source">The object that originated the impact.</param>
        /// <param name="target">The object that received the impact.</param>
        /// <param name="position">The position of the impact.</param>
        /// <param name="normal">The impact normal direction.</param>
        public void MagicImpact (ItemAction itemAction, uint castID, GameObject source, GameObject target, Vector3 position, Vector3 normal) {
            var sourceID = NetworkedUtility.GetID (source, out var slotID);
            if (sourceID == 0) {
                Debug.LogError ($"Error: Unable to retrieve the ID of the {source.name} GameObject. Ensure a NetworkObject has been added.");
            } else {
                var targetID = NetworkedUtility.GetID (target, out slotID);
                if (targetID == 0) {
                    Debug.LogError ($"Error: Unable to retrieve the ID of the {target.name} GameObject. Ensure a NetworkObject has been added.");
                } else {
                    if (IsServer) {
                        MagicImpactClientRpc (itemAction.Item.SlotID, itemAction.ID, castID, sourceID, targetID, slotID, position, normal);
                    } else {
                        MagicImpactServerRpc (itemAction.Item.SlotID, itemAction.ID, castID, sourceID, targetID, slotID, position, normal);
                    }
                }
            }
        }
        /// <summary>
        /// Performs the magic impact on the network.
        /// </summary>
        /// <param name="slotID">The slot of the MagicItem that is performing the impact.</param>
        /// <param name="actionID">The ID of the MagicItem that is performing the impact.</param>
        /// <param name="castID">The ID of the cast.</param>
        /// <param name="sourceID">The ID of the object that originated the impact.</param>
        /// <param name="targetID">The ID of the object that received the impact.</param>
        /// <param name="targetSlotID">If the target  GameObject is an item then the slot ID of the item will be specified.</param>
        /// <param name="position">The position of the impact.</param>
        /// <param name="normal">The impact normal direction.</param>
        private void MagicImpactRpc (int slotID, int actionID, uint castID, ulong sourceID, ulong targetID, int targetSlotID, Vector3 position, Vector3 normal) {
            var itemAction = GetItemAction (slotID, actionID) as MagicItem;
            if (itemAction != null) {
                var source = NetworkedUtility.RetrieveGameObject (null, sourceID, -1);
                if (source == null) {
                    Debug.LogError ($"Error: Unable to find the NetworkObject with ID {sourceID}.");
                } else {
                    var target = NetworkedUtility.RetrieveGameObject (null, targetID, targetSlotID);
                    if (target == null) {
                        Debug.LogError ($"Error: Unable to find the NetworkObject with ID {targetID}.");
                    } else {
                        var targetCollider = target.GetCachedComponent<Collider> ();
                        if (targetCollider != null) {
                            // A RaycastHit cannot be sent over the network. Try to recreate it locally based on the position and normal values.
                            var ray = new Ray (position + normal * 1f, -normal);
                            if (!targetCollider.Raycast (ray, out var hit, 2f)) {
                                // The object has moved. Do a larger cast to try to find the object.
                                if (!Physics.SphereCast (ray, 1f, out hit, 2f, 1 << targetCollider.gameObject.layer, QueryTriggerInteraction.Ignore)) {
                                    // The object can't be found. Return.
                                    return;
                                }
                            }
                            itemAction.PerformImpact (castID, source, target, hit);
                        }
                    }
                }
            }
        }

        [ServerRpc]
        private void MagicImpactServerRpc (int slotID, int actionID, uint castID, ulong sourceID, ulong targetID, int targetSlotID, Vector3 position, Vector3 normal) {
            if (!IsClient) { MagicImpactRpc (slotID, actionID, castID, sourceID, targetID, targetSlotID, position, normal); }
            MagicImpactClientRpc (slotID, actionID, castID, sourceID, targetID, targetSlotID, position, normal);
        }

        [ClientRpc]
        private void MagicImpactClientRpc (int slotID, int actionID, uint castID, ulong sourceID, ulong targetID, int targetSlotID, Vector3 position, Vector3 normal) {
            if (!IsOwner) { MagicImpactRpc (slotID, actionID, castID, sourceID, targetID, targetSlotID, position, normal); }
        }
        /// <summary>
        /// Stops the magic CastAction.
        /// </summary>
        /// <param name="itemAction">The MagicItem that is stopping the cast.</param>
        /// <param name="index">The index of the CastAction.</param>
        /// <param name="castID">The ID of the cast.</param>
        public void StopMagicCast (ItemAction itemAction, int index, uint castID) {
            if (IsServer) {
                MagicCastClientRpc (itemAction.Item.SlotID, itemAction.ID, index, castID);
            } else {
                StopMagicCastServerRpc (itemAction.Item.SlotID, itemAction.ID, index, castID);
            }
        }

        /// <summary>
        /// Stops the magic CastAction on the network.
        /// </summary>
        /// <param name="slotID">The slot of the MagicItem that is stopping the cast.</param>
        /// <param name="actionID">The ID of the MagicItem that is stopping the cast.</param>
        /// <param name="index">The index of the CastAction.</param>
        /// <param name="castID">The ID of the cast.</param>
        private void MagicCastRpc (int slotID, int actionID, int index, uint castID) {
            var itemAction = GetItemAction (slotID, actionID) as MagicItem;
            if (itemAction != null) {
                var castAction = itemAction.CastActions[index];
                if (castAction != null) {
                    castAction.Stop (castID);
                }
            }
        }

        [ServerRpc]
        private void StopMagicCastServerRpc (int slotID, int actionID, int index, uint castID) {
            if (!IsClient) { MagicCastRpc (slotID, actionID, index, castID); }
            MagicCastClientRpc (slotID, actionID, index, castID);
        }

        [ClientRpc]
        private void MagicCastClientRpc (int slotID, int actionID, int index, uint castID) {
            if (!IsOwner) { MagicCastRpc (slotID, actionID, index, castID); }
        }
        /// <summary>
        /// Activates or deactives the flashlight.
        /// </summary>
        /// <param name="active">Should the flashlight be activated?</param>
        public void ToggleFlashlight (ItemAction itemAction, bool active) {
            if (IsServer) {
                ToggleFlashlightClientRpc (itemAction.Item.SlotID, itemAction.ID, active);
            } else {
                ToggleFlashlightServerRpc (itemAction.Item.SlotID, itemAction.ID, active);
            }
        }
        /// <summary>
        /// Activates or deactives the flashlight on the network.
        /// </summary>
        /// <param name="slotID">The slot of the Flashlight that is being toggled.</param>
        /// <param name="actionID">The ID of the Flashlight that is being toggled.</param>
        /// <param name="active">Should the flashlight be activated?</param>
        private void ToggleFlashlightRpc (int slotID, int actionID, bool active) {
            var itemAction = GetItemAction (slotID, actionID) as Flashlight;
            if (itemAction != null) {
                itemAction.ToggleFlashlight (active);
            }
        }

        [ServerRpc]
        private void ToggleFlashlightServerRpc (int slotID, int actionID, bool active) {
            if (!IsClient) { ToggleFlashlightRpc (slotID, actionID, active); }
            ToggleFlashlightClientRpc (slotID, actionID, active);
        }

        [ClientRpc]
        private void ToggleFlashlightClientRpc (int slotID, int actionID, bool active) {
            if (!IsOwner) { ToggleFlashlightRpc (slotID, actionID, active); }
        }
        /// <summary>
        /// Pushes the target Rigidbody in the specified direction.
        /// </summary>
        /// <param name="targetRigidbody">The Rigidbody to push.</param>
        /// <param name="force">The amount of force to apply.</param>
        /// <param name="point">The point at which to apply the push force.</param>
        public void PushRigidbody (Rigidbody targetRigidbody, Vector3 force, Vector3 point) {
            var target = targetRigidbody.gameObject.GetCachedComponent<NetworkObject> ();
            if (target == null) {
                Debug.LogError ($"Error: The object {targetRigidbody.gameObject} must have a NetworkObject component added.");
            } else {
                if (IsOwner) {
                    PushRigidbodyServerRpc (target.NetworkObjectId, force, point);
                }
            }
        }
        /// <summary>
        /// Pushes the target Rigidbody in the specified direction on the network.
        /// </summary>
        /// <param name="targetRigidbody">The Rigidbody to push.</param>
        /// <param name="force">The amount of force to apply.</param>
        /// <param name="point">The point at which to apply the push force.</param>
        private void PushRigidbodyRpc (ulong rigidbodyNetworkObjectId, Vector3 force, Vector3 point) {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue ((ulong) rigidbodyNetworkObjectId, out var obj)) {
                var targetRigidbody = obj.gameObject.GetComponent<Rigidbody> ();
                if (targetRigidbody != null) {
                    targetRigidbody.AddForceAtPosition (force, point, ForceMode.VelocityChange);
                }
            }
        }

        [ServerRpc]
        private void PushRigidbodyServerRpc (ulong rigidbodyNetworkObjectId, Vector3 force, Vector3 point) {
            if (!IsClient) { PushRigidbodyRpc (rigidbodyNetworkObjectId, force, point); }
            PushRigidbodyClientRpc (rigidbodyNetworkObjectId, force, point);
        }

        [ClientRpc]
        private void PushRigidbodyClientRpc (ulong rigidbodyNetworkObjectId, Vector3 force, Vector3 point) {
            if (!IsOwner) { PushRigidbodyRpc (rigidbodyNetworkObjectId, force, point); }
        }
        /// <summary>
        /// Sets the rotation of the character.
        /// </summary>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetRotation (Quaternion rotation, bool snapAnimator) {
            if (IsServer) {
                SetRotationClientRpc (rotation, snapAnimator);
            } else {
                SetRotationServerRpc (rotation, snapAnimator);
            }
        }
        /// <summary>
        /// Sets the rotation of the character.
        /// </summary>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetRotationRpc (Quaternion rotation, bool snapAnimator) {
            m_CharacterLocomotion.SetRotation (rotation, snapAnimator);
        }

        [ServerRpc]
        public void SetRotationServerRpc (Quaternion rotation, bool snapAnimator) {
            if (!IsClient) { SetRotationRpc (rotation, snapAnimator); }
            SetRotationClientRpc (rotation, snapAnimator);
        }

        [ClientRpc]
        public void SetRotationClientRpc (Quaternion rotation, bool snapAnimator) {
            if (!IsOwner) { SetRotationRpc (rotation, snapAnimator); }
        }
        /// <summary>
        /// Sets the position of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetPosition (Vector3 position, bool snapAnimator) {
            if (IsServer) {
                SetPositionClientRpc (position, snapAnimator);
            } else {
                SetPositionServerRpc (position, snapAnimator);
            }
        }
        /// <summary>
        /// Sets the position of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetPositionRpc (Vector3 position, bool snapAnimator) {
            m_CharacterLocomotion.SetPosition (position, snapAnimator);
        }

        [ServerRpc]
        public void SetPositionServerRpc (Vector3 position, bool snapAnimator) {
            if (!IsClient) { SetPositionRpc (position, snapAnimator); }
            SetPositionClientRpc (position, snapAnimator);
        }

        [ClientRpc]
        public void SetPositionClientRpc (Vector3 position, bool snapAnimator) {
            if (!IsOwner) { SetPositionRpc (position, snapAnimator); }
        }
        /// <summary>
        /// Resets the rotation and position to their default values.
        /// </summary>
        public void ResetRotationPosition () {
            if (IsServer) {
                ResetRotationPositionClientRpc ();
            } else {
                ResetRotationPositionServerRpc ();
            }
        }
        /// <summary>
        /// Resets the rotation and position to their default values on the network.
        /// </summary>
        public void ResetRotationPositionRpc () {
            m_CharacterLocomotion.ResetRotationPosition ();
        }

        [ServerRpc]
        public void ResetRotationPositionServerRpc () {
            if (!IsClient) { ResetRotationPositionRpc (); }
            ResetRotationPositionClientRpc ();
        }

        [ClientRpc]
        public void ResetRotationPositionClientRpc () {
            if (!IsOwner) { ResetRotationPositionRpc (); }
        }
        /// <summary>
        /// Sets the position and rotation of the character on the network.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetPositionAndRotation (Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities) {
            if (IsServer) {
                SetPositionAndRotationClientRpc (position, rotation, snapAnimator, stopAllAbilities);
            } else {
                SetPositionAndRotationServerRpc (position, rotation, snapAnimator, stopAllAbilities);
            }
        }
        /// <summary>
        /// Sets the position and rotation of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetPositionAndRotationRpc (Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities) {
            m_CharacterLocomotion.SetPositionAndRotation (position, rotation, snapAnimator, stopAllAbilities);
        }

        [ServerRpc]
        public void SetPositionAndRotationServerRpc (Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities) {
            if (!IsClient) { SetPositionAndRotationRpc (position, rotation, stopAllAbilities, snapAnimator); }
            SetPositionAndRotationClientRpc (position, rotation, stopAllAbilities, snapAnimator);
        }

        [ClientRpc]
        public void SetPositionAndRotationClientRpc (Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities) {
            if (!IsOwner) { SetPositionAndRotationRpc (position, rotation, stopAllAbilities, snapAnimator); }
        }
        /// <summary>
        /// Activates or deactivates the character.
        /// </summary>
        /// <param name="active">Is the character active?</param>
        /// <param name="uiEvent">Should the OnShowUI event be executed?</param>
        public void SetActive (bool active, bool uiEvent) {
            if (IsServer) {
                SetActiveClientRpc (active, uiEvent);
            } else {
                SetActiveServerRpc (active, uiEvent);
            }
        }
        /// <summary>
        /// Activates or deactivates the character on the network.
        /// </summary>
        /// <param name="active">Is the character active?</param>
        /// <param name="uiEvent">Should the OnShowUI event be executed?</param>
        private void SetActiveRpc (bool active, bool uiEvent) {
            // m_CharacterLocomotion.SetActive (active, uiEvent);
            m_GameObject.SetActive (active);
        }

        [ServerRpc]
        private void SetActiveServerRpc (bool active, bool uiEvent) {
            if (!IsClient) { SetActiveRpc (active, uiEvent); }
            SetActiveClientRpc (active, uiEvent);
        }

        [ClientRpc]
        private void SetActiveClientRpc (bool active, bool uiEvent) {
            if (!IsOwner) { SetActiveRpc (active, uiEvent); }
        }
    }
}