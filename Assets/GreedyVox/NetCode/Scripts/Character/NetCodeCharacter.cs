using GreedyVox.NetCode.Utilities;
using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Camera;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Items.Actions;
using Opsive.UltimateCharacterController.Items.Actions.Impact;
using Opsive.UltimateCharacterController.Items.Actions.Modules;
using Opsive.UltimateCharacterController.Items.Actions.Modules.Melee;
using Opsive.UltimateCharacterController.Items.Actions.Modules.Shootable;
using Opsive.UltimateCharacterController.Networking.Character;
using Unity.Netcode;
using UnityEngine;
using Opsive.Shared.Utility;
using Opsive.UltimateCharacterController.Items.Actions.Modules.Throwable;
using Opsive.UltimateCharacterController.Items;
using Opsive.UltimateCharacterController.Items.Actions.Modules.Magic;

/// <summary>
/// The NetCode Character component manages the RPCs and state of the character on the network.
/// </summary>
namespace GreedyVox.NetCode.Character
{
    [DisallowMultipleComponent]
    public class NetCodeCharacter : NetworkBehaviour, INetworkCharacter
    {
        private UltimateCharacterLocomotion m_CharacterLocomotion;
        private ModelManager m_ModelManager;
        private InventoryBase m_Inventory;
        private GameObject m_GameObject;
        private bool m_ItemsPickedUp;
        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            EventHandler.UnregisterEvent<ulong, NetworkObjectReference>("OnPlayerDisconnected", OnPlayerDisconnected);
            EventHandler.UnregisterEvent<Ability, bool>(m_GameObject, "OnCharacterAbilityActive", OnAbilityActive);
            base.OnDestroy();
        }
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Inventory = m_GameObject.GetCachedComponent<InventoryBase>();
            m_ModelManager = m_GameObject.GetCachedComponent<ModelManager>();
            m_CharacterLocomotion = m_GameObject.GetCachedComponent<UltimateCharacterLocomotion>();
        }
        /// <summary>
        /// Registers for any interested events.
        /// </summary>
        private void Start()
        {
            if (IsOwner)
            {
                EventHandler.RegisterEvent<Ability, bool>(m_GameObject, "OnCharacterAbilityActive", OnAbilityActive);
                EventHandler.RegisterEvent<ulong, NetworkObjectReference>("OnPlayerDisconnected", OnPlayerDisconnected);
            }
            else
            {
                PickupItems();
                EventHandler.ExecuteEvent(m_GameObject, "OnCharacterSnapAnimator", false);
            }
            // AI agents should be disabled on the client.
            if (!NetworkManager.IsServer && m_GameObject.GetCachedComponent<LocalLookSource>() != null)
            {
                m_CharacterLocomotion.enabled = false;
                if (!IsOwner)
                    EventHandler.RegisterEvent<ulong, NetworkObjectReference>("OnPlayerDisconnected", OnPlayerDisconnected);
            }
        }
        /// <summary>
        /// Pickup isn't called on unequipped items. Ensure pickup is called before the item is equipped.
        /// </summary>
        private void PickupItems()
        {
            if (m_ItemsPickedUp) return;
            m_ItemsPickedUp = true;
            var items = m_GameObject.GetComponentsInChildren<CharacterItem>(true);
            for (int i = 0; i < items.Length; i++)
                items[i].Pickup();
        }
        /// <summary>
        /// Loads the inventory's default loadout.
        /// </summary>
        public void LoadDefaultLoadout() => LoadoutDefaultRpc();
        /// <summary>
        /// Loads the inventory's default loadout on the network.
        /// </summary>
        [Rpc(SendTo.NotMe)]
        private void LoadoutDefaultRpc()
        {
            m_Inventory.LoadDefaultLoadout();
            EventHandler.ExecuteEvent(m_GameObject, "OnCharacterSnapAnimator", false);
        }
        /// <summary>
        /// A player has disconnected. Perform any cleanup.
        /// </summary>
        /// <param name="id">The Client networking ID that disconnected.</param>
        /// /// <param name="net">The Player networking Object that connected.</param>
        private void OnPlayerDisconnected(ulong id, NetworkObjectReference obj)
        {
            // The MasterClient is responsible for the AI.
            if (IsHost && m_GameObject.GetCachedComponent<LocalLookSource>() != null)
            {
                m_CharacterLocomotion.enabled = true;
                return;
            }
            if (obj.TryGet(out var net)
            && net.gameObject == m_GameObject
            && m_CharacterLocomotion.LookSource != null
            && m_CharacterLocomotion.LookSource.GameObject != null)
            {
                // The local character has disconnected. The character no longer has a look source.
                var controller = m_CharacterLocomotion.LookSource.GameObject.GetComponent<CameraController>();
                if (controller != null)
                    controller.Character = null;
                EventHandler.ExecuteEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", null);
            }
        }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (!IsServer && !IsOwner)
                OnPlayerConnectedEventRpc();
            base.OnNetworkSpawn();
        }
        /// <summary>
        /// A player connected syncing event sent.
        /// </summary>
        /// <param name="rpc">The client that sent the syncing event.</param>
        [Rpc(SendTo.Server)]
        private void OnPlayerConnectedEventRpc(RpcParams rpc = default)
        {
            // Notify the joining player of the ItemIdentifiers that the player has within their inventory.
            if (m_Inventory != null)
            {
                var items = m_Inventory.GetAllCharacterItems();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    PickupItemIdentifierRpc(item.ItemIdentifier.ID, m_Inventory.GetItemIdentifierAmount(item.ItemIdentifier),
                                            RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
                    // Usable Items have a separate ItemIdentifiers amount.
                    if (item.DropPrefab != null)
                    {
                        var itemActions = item.ItemActions;
                        for (int j = 0; j < itemActions.Length; j++)
                        {
                            var usableAction = itemActions[j] as UsableAction;
                            if (usableAction == null) continue;
                            usableAction.InvokeOnModulesWithType<IModuleItemDefinitionConsumer>(module =>
                            {
                                var amount = module.GetItemDefinitionRemainingCount();
                                if (amount > 0 && module.ItemDefinition != null)
                                {
                                    var moduleItemIdentifier = module.ItemDefinition.CreateItemIdentifier();
                                    PickupUsableItemActionRpc(item.ItemIdentifier.ID, item.SlotID, itemActions[j].ID,
                                                              (module as ActionModule).ModuleGroup.ID,
                                                              (module as ActionModule).ID,
                                                               m_Inventory.GetItemIdentifierAmount(moduleItemIdentifier), amount,
                                                               RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
                                }
                            });
                        }
                    }
                }
                // Ensure the correct item is equipped in each slot.
                for (int i = 0; i < m_Inventory.SlotCount; i++)
                {
                    var item = m_Inventory.GetActiveCharacterItem(i);
                    if (item != null)
                        EquipUnequipItemRpc(item.ItemIdentifier.ID, i, true,
                                            RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
                }
            }
            // The active character model needs to be synced.
            if (m_ModelManager != null && m_ModelManager.ActiveModelIndex != 0)
                ChangeModels(m_ModelManager.ActiveModelIndex);
            // ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER will be defined, but it is required here to allow the add-on to be compiled for the first time.
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            // The remote character should have the same abilities active.
            for (int i = 0; i < m_CharacterLocomotion.ActiveAbilityCount; i++)
            {
                var activeAbility = m_CharacterLocomotion.ActiveAbilities[i];
                var dat = activeAbility?.GetNetworkStartData();
                if (dat != null)
                    StartAbilityRpc(activeAbility.Index, SerializerObjectArray.Serialize(dat),
                                    RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
            }
#endif
        }
        /// <summary>
        /// The character's ability has been started or stopped.
        /// </summary>
        /// <param name="ability">The ability which was started or stopped.</param>
        /// <param name="active">True if the ability was started, false if it was stopped.</param>
        private void OnAbilityActive(Ability ability, bool active) => AbilityActiveRpc(ability.Index, active);
        /// <summary>
        /// Activates or deactivates the ability on the network at the specified index.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        /// <param name="active">Should the ability be activated?</param>
        [Rpc(SendTo.NotMe)]
        private void AbilityActiveRpc(int abilityIndex, bool active)
        {
            if (active)
                m_CharacterLocomotion.TryStartAbility(m_CharacterLocomotion.Abilities[abilityIndex]);
            else
                m_CharacterLocomotion.TryStopAbility(m_CharacterLocomotion.Abilities[abilityIndex], true);
        }
        /// <summary>
        /// Starts the ability on the remote player.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        /// <param name="startData">Any data associated with the ability start.</param>
        [Rpc(SendTo.SpecifiedInParams)]
        private void StartAbilityRpc(int abilityIndex, SerializableObjectArray startData, RpcParams rpc = default)
        {
            var ability = m_CharacterLocomotion.Abilities[abilityIndex];
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            if (startData != null)
                ability.SetNetworkStartData(DeserializerObjectArray.Deserialize(startData));
#endif
            m_CharacterLocomotion.TryStartAbility(ability, true, true);
        }
        /// <summary>
        /// Picks up the ItemIdentifier on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifiers that should be equipped.</param>
        /// <param name="amount">The number of ItemIdnetifiers to pickup.</param>
        [Rpc(SendTo.SpecifiedInParams)]
        private void PickupItemIdentifierRpc(uint itemIdentifierID, int amount, RpcParams rpc = default)
        {
            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemIdentifier != null)
                m_Inventory.PickupItem(itemIdentifier, -1, amount, false, false, false, true);
        }
        /// <summary>
        /// Picks up the IUsableItem ItemIdentifier on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that should be equipped.</param>
        /// <param name="slotID">The slot of the item being picked up.</param>
        /// <param name="itemActionID">The ID of the IUsableItem being picked up.</param>
        /// <param name="moduleGroupID">The ID of the module group containing the ItemIdentifier.</param>
        /// <param name="moduleID">The ID of the module containing the ItemIdentifier.</param>
        /// <param name="moduleAmount">The module amount within the inventory.</param>
        /// <param name="moduleItemIdentifierAmount">The ItemIdentifier amount loaded within the module.</param>
        /// 
        [Rpc(SendTo.SpecifiedInParams)]
        private void PickupUsableItemActionRpc(uint itemIdentifierID, int slotID, int itemActionID, int moduleGroupID,
        int moduleID, int moduleAmount, int moduleItemIdentifierAmount, RpcParams rpc = default)
        {
            var itemType = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemType == null) return;
            var item = m_Inventory.GetCharacterItem(itemType, slotID);
            if (item == null) return;
            var usableItemAction = item.GetItemAction(itemActionID) as UsableAction;
            if (usableItemAction == null) return;
            usableItemAction.InvokeOnModulesWithTypeConditional<IModuleItemDefinitionConsumer>(module =>
            {
                var actionModule = module as ActionModule;
                if (actionModule.ModuleGroup.ID != moduleGroupID || actionModule.ID != moduleID)
                    return false;
                // The UsableAction has two counts: the first count is from the inventory, and the second count is set on the actual ItemAction.
                m_Inventory.PickupItem(module.ItemDefinition.CreateItemIdentifier(), -1, moduleAmount, false, false, false, false);
                module.SetItemDefinitionRemainingCount(moduleItemIdentifierAmount);
                return true;
            }, true, true);
            usableItemAction.InvokeOnModulesWithTypeConditional<ShootableClipModule>(module =>
            {
                if (module.ModuleGroup.ID != moduleGroupID || module.ID != moduleID) return false;
                module.SetClipRemaining(moduleItemIdentifierAmount);
                return true;
            }, true, true);
        }
        /// <summary>
        /// Equips or unequips the item with the specified ItemIdentifier and slot.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that should be equipped.</param>
        /// <param name="slotID">The slot of the item that should be equipped.</param>
        /// <param name="equip">Should the item be equipped? If false it will be unequipped.</param>
        public void EquipUnequipItem(uint itemIdentifierID, int slotID, bool equip) =>
        EquipUnequipItemRpc(itemIdentifierID, slotID, equip, RpcTarget.NotOwner);
        /// <summary>
        /// Equips or unequips the item on the network with the specified ItemIdentifier and slot.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that should be equipped.</param>
        /// <param name="slotID">The slot of the item that should be equipped.</param>
        /// <param name="equip">Should the item be equipped? If false it will be unequipped.</param>
        [Rpc(SendTo.SpecifiedInParams)]
        private void EquipUnequipItemRpc(uint itemIdentifierID, int slotID, bool equip, RpcParams rpc = default)
        {
            if (equip)
            {
                // The character has to be alive to equip.
                if (!m_CharacterLocomotion.Alive) return;
                // Ensure pickup is called before the item is equipped.
                PickupItems();
            }
            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemIdentifier == null) return;
            var item = m_Inventory.GetCharacterItem(itemIdentifier, slotID);
            if (item == null) return;
            if (equip)
            {
                if (m_Inventory.GetActiveCharacterItem(slotID) != item)
                {
                    EventHandler.ExecuteEvent<CharacterItem, int>(m_GameObject, "OnAbilityWillEquipItem", item, slotID);
                    m_Inventory.EquipItem(itemIdentifier, slotID, true);
                }
            }
            else
            {
                EventHandler.ExecuteEvent<CharacterItem, int>(m_GameObject, "OnAbilityUnequipItemComplete", item, slotID);
                m_Inventory.UnequipItem(itemIdentifier, slotID);
            }
        }
        /// <summary>
        /// The ItemIdentifier has been picked up.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that was picked up.</param>
        /// <param name="amount">The number of ItemIdentifier picked up.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="immediatePickup">Was the item be picked up immediately?</param>
        /// <param name="forceEquip">Should the item be force equipped?</param>
        public void ItemIdentifierPickup(uint itemIdentifierID, int slotID, int amount, bool immediatePickup, bool forceEquip) =>
        ItemIdentifierPickupRpc(itemIdentifierID, slotID, amount, immediatePickup, forceEquip);
        /// <summary>
        /// The ItemIdentifier has been picked up on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that was picked up.</param>
        /// <param name="amount">The number of ItemIdentifier picked up.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="immediatePickup">Was the item be picked up immediately?</param>
        /// <param name="forceEquip">Should the item be force equipped?</param>
        [Rpc(SendTo.NotMe)]
        private void ItemIdentifierPickupRpc(uint itemIdentifierID, int slotID, int amount, bool immediatePickup, bool forceEquip)
        {
            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemIdentifier != null)
                m_Inventory?.PickupItem(itemIdentifier, slotID, amount, immediatePickup, forceEquip);
        }
        /// <summary>
        /// Remove an item amount from the inventory.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that was removed.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="amount">The amount of ItemIdentifier to adjust.</param>
        /// <param name="drop">Should the item be dropped?</param>
        /// <param name="removeCharacterItem">Should the character item be removed?</param>
        /// <param name="destroyCharacterItem">Should the character item be destroyed?</param>
        public void RemoveItemIdentifierAmount(uint itemIdentifierID, int slotID, int amount, bool drop, bool removeCharacterItem, bool destroyCharacterItem) =>
        RemoveItemIdentifierAmountRpc(itemIdentifierID, slotID, amount, drop, removeCharacterItem, destroyCharacterItem);
        /// <summary>
        /// Remove an item amount from the inventory on the network.
        /// </summary>
        /// <param name="itemIdentifierID">The ID of the ItemIdentifier that was removed.</param>
        /// <param name="slotID">The ID of the slot which the item belongs to.</param>
        /// <param name="amount">The amount of ItemIdentifier to adjust.</param>
        /// <param name="drop">Should the item be dropped?</param>
        /// <param name="removeCharacterItem">Should the character item be removed?</param>
        /// <param name="destroyCharacterItem">Should the character item be destroyed?</param>
        [Rpc(SendTo.NotMe)]
        private void RemoveItemIdentifierAmountRpc(uint itemIdentifierID, int slotID, int amount, bool drop, bool removeCharacterItem, bool destroyCharacterItem)
        {
            var itemIdentifier = ItemIdentifierTracker.GetItemIdentifier(itemIdentifierID);
            if (itemIdentifier != null)
                m_Inventory?.RemoveItemIdentifierAmount(itemIdentifier, slotID, amount, drop, removeCharacterItem, destroyCharacterItem);
        }
        /// <summary>
        /// Removes all of the items from the inventory.
        /// </summary>
        public void RemoveAllItems() => RemoveAllItemsRpc();
        /// <summary>
        /// Removes all of the items from the inventory on the network.
        /// </summary>
        [Rpc(SendTo.NotMe)]
        private void RemoveAllItemsRpc() => m_Inventory?.RemoveAllItems(true);
        /// <summary>
        /// Returns the ItemAction with the specified slot and ID.
        /// </summary>
        /// <param name="slotID">The slot that the ItemAction belongs to.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <returns>The ItemAction with the specified slot and ID</returns>
        private CharacterItemAction GetItemAction(int slotID, int actionID)
        {
            var item = m_Inventory.GetActiveCharacterItem(slotID);
            return item?.GetItemAction(actionID);
        }
        /// <summary>
        /// Returns the module with the specified IDs.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="slotID">The SlotID of the module that is being retrieved.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <param name="actionID">The ID of the ModuleGroup being retrieved.</param>
        /// <param name="moduleID">The ID of the module being retrieved.</param>
        /// <returns>The module with the specified IDs (can be null).</returns>
        private T GetModule<T>(int slotID, int actionID, int moduleGroupID, int moduleID) where T : ActionModule
        {
            var itemAction = GetItemAction(slotID, actionID);
            if (itemAction == null)
                return null;
            if (!itemAction.ModuleGroupsByID.TryGetValue(moduleGroupID, out var moduleGroup))
                return null;
            if (moduleGroup.GetBaseModuleByID(moduleID) is not T module)
                return null;
            return module;
        }
        /// <summary>
        /// Returns the module group with the specified IDs.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being retrieved.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <param name="actionID">The ID of the ModuleGroup being retrieved.</param>
        /// <returns>The module group with the specified IDs (can be null).</returns>
        private ActionModuleGroupBase GetModuleGroup(int slotID, int actionID, int moduleGroupID)
        {
            var itemAction = GetItemAction(slotID, actionID);
            if (itemAction != null && itemAction.ModuleGroupsByID.TryGetValue(moduleGroupID, out var moduleGroup))
                return moduleGroup;
            return null;
        }
        /// <summary>
        /// Initializes the ImpactCollisionData object.
        /// </summary>
        /// <param name="collisionData">The ImpactCollisionData resulting object.</param>
        /// <param name="sourceID">The ID of the impact.</param>
        /// <param name="sourceCharacterLocomotionViewID">The ID of the CharacterLocomotion component that caused the collision.</param>
        /// <param name="sourceGameObjectID">The ID of the GameObject that caused the collision.</param>
        /// <param name="sourceGameObjectSlotID">The slot ID if an item caused the collision.</param>
        /// <param name="impactGameObjectID">The ID of the GameObject that was impacted.</param>
        /// <param name="impactGameObjectSlotID">The slot ID of the item that was impacted.</param>
        /// <param name="impactColliderID">The ID of the Collider that was impacted.</param>
        /// <param name="impactPosition">The position of impact.</param>
        /// <param name="impactDirection">The direction of impact.</param>
        /// <param name="impactStrength">The strength of the impact.</param>
        /// <returns>True if the data structure was successfully initialized.</returns>
        private bool InitializeImpactCollisionData(ref ImpactCollisionData collisionData, ulong sourceID, int sourceCharacterLocomotionViewID, ulong sourceGameObjectID, int sourceGameObjectSlotID,
                                                   ulong impactGameObjectID, int impactGameObjectSlotID, ulong impactColliderID, Vector3 impactPosition, Vector3 impactDirection, float impactStrength)
        {
            UltimateCharacterLocomotion sourceCharacterLocomotion = null;
            if (sourceCharacterLocomotionViewID != -1)
                if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue((ulong)sourceCharacterLocomotionViewID, out NetworkObject net))
                    sourceCharacterLocomotion = net.gameObject.GetCachedComponent<UltimateCharacterLocomotion>();
            var sourceGameObject = NetCodeUtility.RetrieveGameObject(sourceCharacterLocomotion?.gameObject, sourceGameObjectID, sourceGameObjectSlotID);
            if (sourceGameObject == null) return false;
            var impactGameObject = NetCodeUtility.RetrieveGameObject(null, impactGameObjectID, impactGameObjectSlotID);
            if (impactGameObject == null) return false;
            var impactColliderGameObject = NetCodeUtility.RetrieveGameObject(null, impactColliderID, -1);
            if (impactColliderGameObject == null)
            {
                var impactCollider = impactGameObject.GetCachedComponent<Collider>();
                if (impactCollider == null) return false;
                impactColliderGameObject = impactCollider.gameObject;
            }
            collisionData.ImpactCollider = impactColliderGameObject.GetCachedComponent<Collider>();
            if (collisionData.ImpactCollider == null) return false;
            // A RaycastHit cannot be sent over the network. Try to recreate it locally based on the position and normal values.
            impactDirection.Normalize();
            var ray = new Ray(impactPosition - impactDirection, impactDirection);
            if (!collisionData.ImpactCollider.Raycast(ray, out var hit, 3.0f))
            {
                // The object has moved. Do a direct cast to try to find the object.
                ray.origin = collisionData.ImpactCollider.transform.position - impactDirection;
                // The object can't be found. Return false.
                if (!collisionData.ImpactCollider.Raycast(ray, out hit, 10.0f))
                    return false;
            }
            collisionData.SetRaycast(hit);
            collisionData.SourceID = (uint)sourceID;
            collisionData.SourceCharacterLocomotion = sourceCharacterLocomotion;
            collisionData.SourceGameObject = sourceGameObject;
            collisionData.ImpactGameObject = impactGameObject;
            collisionData.ImpactPosition = impactPosition;
            collisionData.ImpactDirection = impactDirection;
            collisionData.ImpactStrength = impactStrength;
            return true;
        }
        /// <summary>
        /// Invokes the Shootable Action Fire Effect modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeShootableFireEffectModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ShootableUseDataStream data) =>
        InvokeShootableFireEffectModulesRpc(itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask, data.FireData.FirePoint, data.FireData.FireDirection);
        /// <summary>
        /// Invokes the Shootable Action Fire Effect module on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="firePoint">The fire point that is sent to the module.</param>
        /// <param name="fireDirection">The fire direction that is sent to the module.</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeShootableFireEffectModulesRpc(int slotID, int actionID, int moduleGroupID, int invokedBitmask, Vector3 firePoint, Vector3 fireDirection)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<ShootableFireEffectModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) return;
            var data = GenericObjectPool.Get<ShootableUseDataStream>();
            data.FireData ??= new ShootableFireData();
            // The action will be the same across all modules.
            data.ShootableAction = moduleGroup.Modules[0].ShootableAction;
            data.FireData.FirePoint = firePoint;
            data.FireData.FireDirection = fireDirection;
            for (int i = 0; i < moduleGroup.ModuleCount; i++)
            {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0)
                    continue;
                moduleGroup.Modules[i].InvokeEffects(data);
            }
            GenericObjectPool.Return(data);
        }
        /// <summary>
        /// Invokes the Shootable Action Dry Fire Effect modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeShootableDryFireEffectModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ShootableUseDataStream data) =>
        InvokeShootableDryFireEffectModulesRpc(itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask, data.FireData.FirePoint, data.FireData.FireDirection);
        /// <summary>
        /// Invokes the Shootable Action Dry Fire Effect module on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="firePoint">The fire point that is sent to the module.</param>
        /// <param name="fireDirection">The fire direction that is sent to the module.</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeShootableDryFireEffectModulesRpc(int slotID, int actionID, int moduleGroupID, int invokedBitmask, Vector3 firePoint, Vector3 fireDirection)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<ShootableFireEffectModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) return;
            var data = GenericObjectPool.Get<ShootableUseDataStream>();
            data.FireData ??= new ShootableFireData();
            // The action will be the same across all modules.
            data.ShootableAction = moduleGroup.Modules[0].ShootableAction;
            data.FireData.FirePoint = firePoint;
            data.FireData.FireDirection = fireDirection;
            for (int i = 0; i < moduleGroup.ModuleCount; i++)
            {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0)
                    continue;
                moduleGroup.Modules[i].InvokeEffects(data);
            }
            GenericObjectPool.Return(data);
        }
        /// <summary>
        /// Invokes the Shootable Action Impact modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="context">The context being sent to the module.</param>
        public void InvokeShootableImpactModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ShootableImpactCallbackContext context)
        {
            var sourceCharacterLocomotionViewID = -1;
            if (context.ImpactCollisionData.SourceCharacterLocomotion != null)
            {
                var sourceCharacterLocomotionView = context.ImpactCollisionData.SourceCharacterLocomotion.gameObject.GetCachedComponent<NetworkObject>();
                if (sourceCharacterLocomotionView == null)
                {
                    Debug.LogError($"Error: The character {context.ImpactCollisionData.SourceCharacterLocomotion.gameObject} must have a NetworkObject component added.");
                    return;
                }
                sourceCharacterLocomotionViewID = (int)sourceCharacterLocomotionView.NetworkObjectId;
            }
            var sourceGameObject = NetCodeUtility.GetID(context.ImpactCollisionData.SourceGameObject, out var sourceGameObjectSlotID);
            if (!sourceGameObject.HasID) return;
            var impactGameObject = NetCodeUtility.GetID(context.ImpactCollisionData.ImpactGameObject, out var impactGameObjectSlotID);
            if (!impactGameObject.HasID) return;
            var impactCollider = NetCodeUtility.GetID(context.ImpactCollisionData.ImpactCollider.gameObject, out var colliderSlotID);
            if (!impactCollider.HasID) return;
            InvokeShootableImpactModulesRpc(itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask,
            context.ImpactCollisionData.SourceID, sourceCharacterLocomotionViewID, sourceGameObject.ID, sourceGameObjectSlotID, impactGameObject.ID, impactGameObjectSlotID,
            impactCollider.ID, context.ImpactCollisionData.ImpactPosition, context.ImpactCollisionData.ImpactDirection, context.ImpactCollisionData.ImpactStrength);
        }
        /// <summary>
        /// Invokes the Shootable Action Impact modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="sourceID">The ID of the impact.</param>
        /// <param name="sourceCharacterLocomotionViewID">The ID of the CharacterLocomotion component that caused the collision.</param>
        /// <param name="sourceGameObjectID">The ID of the GameObject that caused the collision.</param>
        /// <param name="sourceGameObjectSlotID">The slot ID if an item caused the collision.</param>
        /// <param name="impactGameObjectID">The ID of the GameObject that was impacted.</param>
        /// <param name="impactGameObjectSlotID">The slot ID of the item that was impacted.</param>
        /// <param name="impactColliderID">The ID of the Collider that was impacted.</param>
        /// <param name="impactPosition">The position of impact.</param>
        /// <param name="impactDirection">The direction of impact.</param>
        /// <param name="impactStrength">The strength of the impact.</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeShootableImpactModulesRpc(int slotID, int actionID, int moduleGroupID, int invokedBitmask, uint sourceID, int sourceCharacterLocomotionViewID,
                                                     ulong sourceGameObjectID, int sourceGameObjectSlotID, ulong impactGameObjectID, int impactGameObjectSlotID,
                                                     ulong impactColliderID, Vector3 impactPosition, Vector3 impactDirection, float impactStrength)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<ShootableImpactModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) return;
            var context = GenericObjectPool.Get<ShootableImpactCallbackContext>();
            if (context.ImpactCollisionData == null)
            {
                context.ImpactCollisionData = new ImpactCollisionData();
                context.ImpactDamageData = new ImpactDamageData();
            }
            var collisionData = context.ImpactCollisionData;
            if (!InitializeImpactCollisionData(ref collisionData, sourceID, sourceCharacterLocomotionViewID, sourceGameObjectID, sourceGameObjectSlotID, impactGameObjectID, impactGameObjectSlotID,
                                               impactColliderID, impactPosition, impactDirection, impactStrength))
            {
                GenericObjectPool.Return(context);
                return;
            }
            collisionData.SourceComponent = GetItemAction(slotID, actionID);
            context.ImpactCollisionData = collisionData;
            // The action will be the same across all modules.
            context.ShootableAction = moduleGroup.Modules[0].ShootableAction;
            for (int i = 0; i < moduleGroup.ModuleCount; i++)
            {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0)
                    continue;
                moduleGroup.Modules[i].OnImpact(context);
            }
            GenericObjectPool.Return(context);
        }
        /// <summary>
        /// Starts to reload the module.
        /// </summary>
        /// <param name="module">The module that is being reloaded.</param>
        public void StartItemReload(ShootableReloaderModule module) =>
        StartItemReloadRpc(module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID);
        /// <summary>
        /// Starts to reload the item on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being reloaded.</param>
        /// <param name="actionID">The ID of the ItemAction being reloaded.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being reloaded.</param>
        /// <param name="moduleID">The ID of the module being reloaded.</param>
        [Rpc(SendTo.NotMe)]
        private void StartItemReloadRpc(int slotID, int actionID, int moduleGroupID, int moduleID)
        {
            var module = GetModule<ShootableReloaderModule>(slotID, actionID, moduleGroupID, moduleID);
            module?.StartItemReload();
        }
        /// <summary>
        /// Reloads the item.
        /// </summary>
        /// <param name="module">The module that is being reloaded.</param>
        /// <param name="fullClip">Should the full clip be force reloaded?</param
        public void ReloadItem(ShootableReloaderModule module, bool fullClip) =>
        ReloadItemRpc(module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID, fullClip);
        /// <summary>
        /// Reloads the item on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being reloaded.</param>
        /// <param name="actionID">The ID of the ItemAction being reloaded.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being reloaded.</param>
        /// <param name="moduleID">The ID of the module being reloaded.</param>
        /// <param name="fullClip">Should the full clip be force reloaded?</param>
        [Rpc(SendTo.NotMe)]
        private void ReloadItemRpc(int slotID, int actionID, int moduleGroupID, int moduleID, bool fullClip)
        {
            var module = GetModule<ShootableReloaderModule>(slotID, actionID, moduleGroupID, moduleID);
            module?.ReloadItem(fullClip);
        }
        /// <summary>
        /// The item has finished reloading.
        /// </summary>
        /// <param name="module">The module that is being realoaded.</param>
        /// <param name="success">Was the item reloaded successfully?</param>
        /// <param name="immediateReload">Should the item be reloaded immediately?</param>
        public void ItemReloadComplete(ShootableReloaderModule module, bool success, bool immediateReload) =>
        ItemReloadCompleteRpc(module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID, success, immediateReload);
        /// <summary>
        /// The item has finished reloading on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="moduleID">The ID of the module being invoked.</param>
        /// <param name="success">Was the item reloaded successfully?</param>
        /// <param name="immediateReload">Should the item be reloaded immediately?</param>
        [Rpc(SendTo.NotMe)]
        private void ItemReloadCompleteRpc(int slotID, int actionID, int moduleGroupID, int moduleID, bool success, bool immediateReload)
        {
            var module = GetModule<ShootableReloaderModule>(slotID, actionID, moduleGroupID, moduleID);
            module?.ItemReloadComplete(success, immediateReload);
        }
        /// <summary>
        /// Invokes the Melee Action Attack module.
        /// </summary>
        /// <param name="module">The module that is being invoked.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeMeleeAttackModule(MeleeAttackModule module, MeleeUseDataStream data) =>
        InvokeMeleeAttackModuleRpc(module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID);
        /// <summary>
        /// Invokes the Melee Action Attack modules over the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being retrieved.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being retrieved.</param>
        /// <param name="moduleID">The ID of the module being retrieved.</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeMeleeAttackModuleRpc(int slotID, int actionID, int moduleGroupID, int moduleID)
        {
            var module = GetModule<MeleeAttackModule>(slotID, actionID, moduleGroupID, moduleID);
            if (module == null) return;
            var data = GenericObjectPool.Get<MeleeUseDataStream>();
            data.MeleeAction = module.MeleeAction;
            module.AttackStart(data);
            GenericObjectPool.Return(data);
        }
        /// <summary>
        /// Invokes the Melee Action Attack Effect modules.
        /// </summary>
        /// <param name="module">The module that is being invoked.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeMeleeAttackEffectModule(ActionModule module, MeleeUseDataStream data) =>
        InvokeMeleeAttackEffectModulesRpc(module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID);
        /// <summary>
        /// Invokes the Melee Action Attack Effects modules over the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being retrieved.</param>
        /// <param name="actionID">The ID of the ItemAction being retrieved.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being retrieved.</param>
        /// <param name="moduleID">The bitmask of the invoked modules.</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeMeleeAttackEffectModulesRpc(int slotID, int actionID, int moduleGroupID, int moduleID)
        {
            var module = GetModule<MeleeAttackEffectModule>(slotID, actionID, moduleGroupID, moduleID);
            module?.StartEffects();
        }
        /// <summary>
        /// Invokes the Melee Action Impact modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="context">The context being sent to the module.</param>
        public void InvokeMeleeImpactModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, MeleeImpactCallbackContext context)
        {
            var sourceCharacterLocomotionViewID = -1;
            if (context.ImpactCollisionData.SourceCharacterLocomotion != null)
            {
                var sourceCharacterLocomotionView = context.ImpactCollisionData.SourceCharacterLocomotion.gameObject.GetCachedComponent<NetworkObject>();
                if (sourceCharacterLocomotionView == null)
                {
                    Debug.LogError($"Error: The character {context.ImpactCollisionData.SourceCharacterLocomotion.gameObject} must have a NetworkObject component added.");
                    return;
                }
                sourceCharacterLocomotionViewID = (int)sourceCharacterLocomotionView.NetworkObjectId;
            }
            var sourceGameObject = NetCodeUtility.GetID(context.ImpactCollisionData.SourceGameObject, out var sourceGameObjectSlotID);
            if (!sourceGameObject.HasID) return;
            var impactGameObject = NetCodeUtility.GetID(context.ImpactCollisionData.ImpactGameObject, out var impactGameObjectSlotID);
            if (!impactGameObject.HasID) return;
            var impactCollider = NetCodeUtility.GetID(context.ImpactCollisionData.ImpactCollider.gameObject, out var colliderSlotID);
            if (!impactCollider.HasID) return;
            InvokeMeleeImpactModulesRpc(itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask,
            context.ImpactCollisionData.SourceID, sourceCharacterLocomotionViewID, sourceGameObject.ID, sourceGameObjectSlotID,
            impactGameObject.ID, impactGameObjectSlotID, impactCollider.ID, context.ImpactCollisionData.ImpactPosition,
            context.ImpactCollisionData.ImpactDirection, context.ImpactCollisionData.ImpactStrength);
        }
        /// <summary>
        /// Invokes the Melee Action Impact modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="sourceID">The ID of the impact.</param>
        /// <param name="sourceCharacterLocomotionViewID">The ID of the CharacterLocomotion component that caused the collision.</param>
        /// <param name="sourceGameObjectID">The ID of the GameObject that caused the collision.</param>
        /// <param name="sourceGameObjectSlotID">The slot ID if an item caused the collision.</param>
        /// <param name="impactGameObjectID">The ID of the GameObject that was impacted.</param>
        /// <param name="impactGameObjectSlotID">The slot ID of the item that was impacted.</param>
        /// <param name="impactColliderID">The ID of the Collider that was impacted.</param>
        /// <param name="impactPosition">The position of impact.</param>
        /// <param name="impactDirection">The direction of impact.</param>
        /// <param name="impactStrength">The strength of the impact.</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeMeleeImpactModulesRpc(int slotID, int actionID, int moduleGroupID, int invokedBitmask, uint sourceID, int sourceCharacterLocomotionViewID,
                                                 ulong sourceGameObjectID, int sourceGameObjectSlotID, ulong impactGameObjectID, int impactGameObjectSlotID,
                                                 ulong impactColliderID, Vector3 impactPosition, Vector3 impactDirection, float impactStrength)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<MeleeImpactModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) return;
            var context = GenericObjectPool.Get<MeleeImpactCallbackContext>();
            if (context.ImpactCollisionData == null)
            {
                context.ImpactCollisionData = new ImpactCollisionData();
                context.ImpactDamageData = new ImpactDamageData();
            }
            var collisionData = context.ImpactCollisionData;
            if (!InitializeImpactCollisionData(ref collisionData, sourceID, sourceCharacterLocomotionViewID, sourceGameObjectID, sourceGameObjectSlotID,
                impactGameObjectID, impactGameObjectSlotID, impactColliderID, impactPosition, impactDirection, impactStrength))
            {
                GenericObjectPool.Return(context);
                return;
            }
            collisionData.SourceComponent = GetItemAction(slotID, actionID);
            context.ImpactCollisionData = collisionData;
            // The action will be the same across all modules.
            context.MeleeAction = moduleGroup.Modules[0].MeleeAction;
            for (int i = 0; i < moduleGroup.ModuleCount; i++)
            {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0)
                    continue;
                moduleGroup.Modules[i].OnImpact(context);
            }
            GenericObjectPool.Return(context);
        }
        /// <summary>
        /// Invokes the Throwable Action Effect modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeThrowableEffectModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ThrowableUseDataStream data) =>
        InvokeThrowableEffectModulesRpc(itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask);
        /// <summary>
        /// Invokes the Throwable Action Effect modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>        
        [Rpc(SendTo.NotMe)]
        private void InvokeThrowableEffectModulesRpc(int slotID, int actionID, int moduleGroupID, int invokedBitmask)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<ThrowableThrowEffectModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) return;
            var data = GenericObjectPool.Get<ThrowableUseDataStream>();
            // The action will be the same across all modules.
            data.ThrowableAction = moduleGroup.Modules[0].ThrowableAction;
            for (int i = 0; i < moduleGroup.ModuleCount; i++)
            {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0)
                    continue;
                moduleGroup.Modules[i].InvokeEffect(data);
            }
            GenericObjectPool.Return(data);
        }
        /// <summary>
        /// Enables the object mesh renderers for the Throwable Action.
        /// </summary>
        /// <param name="module">The module that is having the renderers enabled.</param>
        /// <param name="enable">Should the renderers be enabled?</param>
        public void EnableThrowableObjectMeshRenderers(ActionModule module, bool enable) =>
        EnableThrowableObjectMeshRenderersRpc(module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID, enable);
        /// <summary>
        /// Enables the object mesh renderers for the Throwable Action on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="moduleID">The ID of the module being invoked.</param>
        /// <param name="enable">Should the renderers be enabled?</param>
        [Rpc(SendTo.NotMe)]
        private void EnableThrowableObjectMeshRenderersRpc(int slotID, int actionID, int moduleGroupID, int moduleID, bool enable)
        {
            var module = GetModule<Opsive.UltimateCharacterController.Items.Actions.Modules.Throwable.SpawnProjectile>(slotID, actionID, moduleGroupID, moduleID);
            module?.EnableObjectMeshRenderers(enable);
        }
        /// <summary>
        /// Invokes the Magic Action Begin or End modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="start">Should the module be started? If false the module will be stopped.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeMagicBeginEndModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, bool start, MagicUseDataStream data) =>
        InvokeMagicBeginEndModulesRpc(itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask, start);
        /// <summary>
        /// Invokes the Magic Begin or End modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="start">Should the module be started? If false the module will be stopped.</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeMagicBeginEndModulesRpc(int slotID, int actionID, int moduleGroupID, int invokedBitmask, bool start)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<MagicStartStopModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) return;
            var data = GenericObjectPool.Get<MagicUseDataStream>();
            // The action will be the same across all modules.
            data.MagicAction = moduleGroup.Modules[0].MagicAction;
            for (int i = 0; i < moduleGroup.ModuleCount; i++)
            {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0)
                    continue;
                if (start)
                    moduleGroup.Modules[i].Start(data);
                else
                    moduleGroup.Modules[i].Stop(data);
            }
            GenericObjectPool.Return(data);
        }
        /// <summary>
        /// Invokes the Magic Cast Effect modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="state">Specifies the state of the cast.</param>
        /// <param name="data">The data being sent to the module.</param>
        public void InvokeMagicCastEffectsModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, INetworkCharacter.CastEffectState state, MagicUseDataStream data)
        {
            var originTransform = NetCodeUtility.GetID(data.CastData.CastOrigin?.gameObject, out var originTransformSlotID);
            InvokeMagicCastEffectsModulesRpc(itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask, (short)state,
            data.CastData.CastID, data.CastData.StartCastTime, originTransform.ID, originTransformSlotID, data.CastData.CastPosition,
            data.CastData.CastNormal, data.CastData.Direction, data.CastData.CastTargetPosition);
        }
        /// <summary>
        /// Invokes the Magic Cast Effects modules on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="state">Specifies the state of the cast.</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeMagicCastEffectsModulesRpc(int slotID, int actionID, int moduleGroupID, int invokedBitmask, short state, uint castID, float startCastTime,
                                                      ulong originTransformID, int originTransformSlotID, Vector3 castPosition, Vector3 castNormal, Vector3 direction, Vector3 castTargetPosition)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<MagicCastEffectModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) return;
            var data = GenericObjectPool.Get<MagicUseDataStream>();
            data.CastData ??= new MagicCastData();
            // The action will be the same across all modules.
            data.MagicAction = moduleGroup.Modules[0].MagicAction;
            data.CastData.CastID = castID;
            data.CastData.StartCastTime = startCastTime;
            data.CastData.CastPosition = castPosition;
            data.CastData.CastNormal = castNormal;
            data.CastData.Direction = direction;
            data.CastData.CastTargetPosition = castTargetPosition;
            var originGameObject = NetCodeUtility.RetrieveGameObject(null, originTransformID, originTransformSlotID);
            if (originGameObject != null)
                data.CastData.CastOrigin = originGameObject.transform;
            for (int i = 0; i < moduleGroup.ModuleCount; ++i)
            {
                // Not all modules are invoked.
                if ((moduleGroup.Modules[i].ID & invokedBitmask) == 0)
                    continue;
                switch ((INetworkCharacter.CastEffectState)state)
                {
                    case INetworkCharacter.CastEffectState.Start:
                        moduleGroup.Modules[i].StartCast(data);
                        break;
                    case INetworkCharacter.CastEffectState.Update:
                        moduleGroup.Modules[i].OnCastUpdate(data);
                        break;
                    case INetworkCharacter.CastEffectState.End:
                        moduleGroup.Modules[i].StopCast();
                        break;
                }
            }
            GenericObjectPool.Return(data);
        }
        /// <summary>
        /// Invokes the Magic Action Impact modules.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="context">The context being sent to the module.</param>
        public void InvokeMagicImpactModules(CharacterItemAction itemAction, ActionModuleGroupBase moduleGroup, int invokedBitmask, ImpactCallbackContext context)
        {
            var sourceCharacterLocomotionViewID = -1;
            if (context.ImpactCollisionData.SourceCharacterLocomotion != null)
            {
                var sourceCharacterLocomotionView = context.ImpactCollisionData.SourceCharacterLocomotion.gameObject.GetCachedComponent<NetworkObject>();
                if (sourceCharacterLocomotionView == null)
                {
                    Debug.LogError($"Error: The character {context.ImpactCollisionData.SourceCharacterLocomotion.gameObject} must have a NetworkObject component added.");
                    return;
                }
                sourceCharacterLocomotionViewID = (int)sourceCharacterLocomotionView.NetworkObjectId;
            }
            var sourceGameObject = NetCodeUtility.GetID(context.ImpactCollisionData.SourceGameObject, out var sourceGameObjectSlotID);
            if (!sourceGameObject.HasID) return;
            var impactGameObject = NetCodeUtility.GetID(context.ImpactCollisionData.ImpactGameObject, out var impactGameObjectSlotID);
            if (!impactGameObject.HasID) return;
            var impactCollider = NetCodeUtility.GetID(context.ImpactCollisionData.ImpactCollider.gameObject, out var colliderSlotID);
            if (!impactCollider.HasID) return;
            InvokeMagicImpactModulesRpc(itemAction.CharacterItem.SlotID, itemAction.ID, moduleGroup.ID, invokedBitmask, context.ImpactCollisionData.SourceID,
            sourceCharacterLocomotionViewID, sourceGameObject.ID, sourceGameObjectSlotID, impactGameObject.ID, impactGameObjectSlotID, impactCollider.ID,
            context.ImpactCollisionData.ImpactPosition, context.ImpactCollisionData.ImpactDirection, context.ImpactCollisionData.ImpactStrength);
        }
        /// <summary>
        /// Invokes the Magic Action Impact modules on the network.
        /// </summary>
        /// <param name="itemAction">The Item Action that is invoking the modules.</param>
        /// <param name="moduleGroup">The group that the modules belong to.</param>
        /// <param name="invokedBitmask">The bitmask of the invoked modules.</param>
        /// <param name="context">The context being sent to the module.</param>
        /// <param name="sourceID">The ID of the impact.</param>
        /// <param name="sourceCharacterLocomotionViewID">The ID of the CharacterLocomotion component that caused the collision.</param>
        /// <param name="sourceGameObjectID">The ID of the GameObject that caused the collision.</param>
        /// <param name="sourceGameObjectSlotID">The slot ID if an item caused the collision.</param>
        /// <param name="impactGameObjectID">The ID of the GameObject that was impacted.</param>
        /// <param name="impactGameObjectSlotID">The slot ID of the item that was impacted.</param>
        /// <param name="impactColliderID">The ID of the Collider that was impacted.</param>
        /// <param name="impactPosition">The position of impact.</param>
        /// <param name="impactDirection">The direction of impact.</param>
        /// <param name="impactStrength">The strength of the impact.</param>
        [Rpc(SendTo.NotMe)]
        public void InvokeMagicImpactModulesRpc(int slotID, int actionID, int moduleGroupID, int invokedBitmask, uint sourceID, int sourceCharacterLocomotionViewID,
                                                ulong sourceGameObjectID, int sourceGameObjectSlotID, ulong impactGameObjectID, int impactGameObjectSlotID,
                                                ulong impactColliderID, Vector3 impactPosition, Vector3 impactDirection, float impactStrength)
        {
            var moduleGroup = GetModuleGroup(slotID, actionID, moduleGroupID) as ActionModuleGroup<MagicImpactModule>;
            if (moduleGroup == null || moduleGroup.ModuleCount == 0) return;
            var context = GenericObjectPool.Get<ImpactCallbackContext>();
            if (context.ImpactCollisionData == null)
            {
                context.ImpactCollisionData = new ImpactCollisionData();
                context.ImpactDamageData = new ImpactDamageData();
            }
            var collisionData = context.ImpactCollisionData;
            if (!InitializeImpactCollisionData(ref collisionData, sourceID, sourceCharacterLocomotionViewID, sourceGameObjectID, sourceGameObjectSlotID, impactGameObjectID, impactGameObjectSlotID,
                                                impactColliderID, impactPosition, impactDirection, impactStrength))
            {
                GenericObjectPool.Return(context);
                return;
            }
            collisionData.SourceComponent = GetItemAction(slotID, actionID);
            context.ImpactCollisionData = collisionData;
            for (int i = 0; i < moduleGroup.ModuleCount; ++i)
            {
                // Not all modules are invoked.
                if (((1 << moduleGroup.Modules[i].ID) & invokedBitmask) == 0)
                    continue;
                moduleGroup.Modules[i].OnImpact(context);
            }
            GenericObjectPool.Return(context);
        }
        /// <summary>
        /// Invokes the Usable Action Geenric Effect module.
        /// </summary>
        /// <param name="module">The module that should be invoked.</param>
        public void InvokeGenericEffectModule(ActionModule module) =>
        InvokeGenericEffectModuleRpc(module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID);
        /// <summary>
        /// Invokes the Usable Action Geenric Effect module on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="moduleID">The ID of the module being invoked.</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeGenericEffectModuleRpc(int slotID, int actionID, int moduleGroupID, int moduleID)
        {
            var module = GetModule<Opsive.UltimateCharacterController.Items.Actions.Modules.GenericItemEffects>(slotID, actionID, moduleGroupID, moduleID);
            module?.EffectGroup.InvokeEffects();
        }
        /// <summary>
        /// Invokes the Use Attribute Modifier Toggle module.
        /// </summary>
        /// <param name="module">The module that should be invoked.</param>
        /// <param name="on">Should the module be toggled on?</param>
        public void InvokeUseAttributeModifierToggleModule(ActionModule module, bool on) =>
        InvokeUseAttributeModifierToggleModuleRpc(module.CharacterItem.SlotID, module.CharacterItemAction.ID, module.ModuleGroup.ID, module.ID, on);
        /// <summary>
        /// Invokes the Usable Action Geenric Effect module on the network.
        /// </summary>
        /// <param name="slotID">The SlotID of the module that is being invoked.</param>
        /// <param name="actionID">The ID of the ItemAction being invoked.</param>
        /// <param name="moduleGroupID">The ID of the ModuleGroup being invoked.</param>
        /// <param name="moduleID">The ID of the module being invoked.</param>
        /// <param name="on">Should the module be toggled on?</param>
        [Rpc(SendTo.NotMe)]
        private void InvokeUseAttributeModifierToggleModuleRpc(int slotID, int actionID, int moduleGroupID, int moduleID, bool on)
        {
            var module = GetModule<UseAttributeModifierToggle>(slotID, actionID, moduleGroupID, moduleID);
            module?.ToggleGameObjects(on);
        }
        /// <summary>
        /// Pushes the target Rigidbody in the specified direction.
        /// </summary>
        /// <param name="targetRigidbody">The Rigidbody to push.</param>
        /// <param name="force">The amount of force to apply.</param>
        /// <param name="point">The point at which to apply the push force.</param>
        public void PushRigidbody(Rigidbody targetRigidbody, Vector3 force, Vector3 point)
        {
            var target = targetRigidbody.gameObject.GetCachedComponent<NetworkObject>();
            if (target == null)
                Debug.LogError($"Error: The object {targetRigidbody.gameObject} must have a NetworkObject component added.");
            else
                PushRigidbodyRpc(target, force, point);
        }
        /// <summary>
        /// Pushes the target Rigidbody in the specified direction on the network.
        /// </summary>
        /// <param name="targetRigidbody">The Rigidbody to push.</param>
        /// <param name="force">The amount of force to apply.</param>
        /// <param name="point">The point at which to apply the push force.</param>
        [Rpc(SendTo.Server)]
        private void PushRigidbodyRpc(NetworkObjectReference rigidbodyNetworkObject, Vector3 force, Vector3 point)
        {
            if (rigidbodyNetworkObject.TryGet(out var net))
            {
                var targetRigidbody = net.gameObject.GetComponent<Rigidbody>();
                targetRigidbody?.AddForceAtPosition(force, point, ForceMode.VelocityChange);
            }
        }
        /// <summary>
        /// Sets the rotation of the character.
        /// </summary>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetRotation(Quaternion rotation, bool snapAnimator) => SetRotationRpc(rotation, snapAnimator);
        /// <summary>
        /// Sets the rotation of the character.
        /// </summary>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        [Rpc(SendTo.NotMe)]
        public void SetRotationRpc(Quaternion rotation, bool snapAnimator) => m_CharacterLocomotion.SetRotation(rotation, snapAnimator);
        /// <summary>
        /// Sets the position of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetPosition(Vector3 position, bool snapAnimator) => SetPositionRpc(position, snapAnimator);
        /// <summary>
        /// Sets the position of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        [Rpc(SendTo.NotMe)]
        public void SetPositionRpc(Vector3 position, bool snapAnimator) => m_CharacterLocomotion.SetPosition(position, snapAnimator);
        /// <summary>
        /// Resets the rotation and position to their default values.
        /// </summary>
        public void ResetRotationPosition() => ResetRotationPositionRpc();
        /// <summary>
        /// Resets the rotation and position to their default values on the network.
        /// </summary>
        [Rpc(SendTo.NotMe)]
        public void ResetRotationPositionRpc() => m_CharacterLocomotion.ResetRotationPosition();
        /// <summary>
        /// Sets the position and rotation of the character on the network.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities) =>
        SetPositionAndRotationRpc(position, rotation, snapAnimator, stopAllAbilities);
        /// <summary>
        /// Sets the position and rotation of the character.
        /// </summary>
        /// <param name="position">The position to set.</param>
        /// <param name="rotation">The rotation to set.</param>
        /// <param name="snapAnimator">Should the animator be snapped into position?</param>
        [Rpc(SendTo.NotMe)]
        public void SetPositionAndRotationRpc(Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities) =>
        m_CharacterLocomotion.SetPositionAndRotation(position, rotation, snapAnimator, stopAllAbilities);
        /// <summary>
        /// Changes the character model.
        /// </summary>
        /// <param name="modelIndex">The index of the model within the ModelManager.</param>
        public void ChangeModels(int modelIndex) => ChangeModelsRpc(modelIndex);
        /// <summary>
        /// Changes the character model on the network.
        /// </summary>
        /// <param name="modelIndex">The index of the model within the ModelManager.</param>
        [Rpc(SendTo.NotMe)]
        private void ChangeModelsRpc(int modelIndex)
        {
            if (modelIndex < 0 || m_ModelManager.AvailableModels == null || modelIndex >= m_ModelManager.AvailableModels.Length)
                return;
            // ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER will be defined, but it is required here to allow the add-on to be compiled for the first time.
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            m_ModelManager.ChangeModels(m_ModelManager.AvailableModels[modelIndex], true);
#endif
        }
        /// <summary>
        /// Activates or deactivates the character.
        /// </summary>
        /// <param name="active">Is the character active?</param>
        /// <param name="uiEvent">Should the OnShowUI event be executed?</param>
        public void SetActive(bool active, bool uiEvent) => SetActiveRpc(active, uiEvent);
        /// <summary>
        /// Activates or deactivates the character on the network.
        /// </summary>
        /// <param name="active">Is the character active?</param>
        /// <param name="uiEvent">Should the OnShowUI event be executed?</param>
        [Rpc(SendTo.NotMe)]
        private void SetActiveRpc(bool active, bool uiEvent)
        {
            m_GameObject.SetActive(active);
            if (uiEvent)
                EventHandler.ExecuteEvent(m_GameObject, "OnShowUI", active);
        }
        /// <summary>
        /// Executes a bool event.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="value">The bool value.</param>
        public void ExecuteBoolEvent(string eventName, bool value) => ExecuteBoolEventRpc(eventName, value);
        /// <summary>
        /// Executes a bool event on the network.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="value">The bool value.</param>
        [Rpc(SendTo.NotMe)]
        private void ExecuteBoolEventRpc(string eventName, bool value) => EventHandler.ExecuteEvent(m_GameObject, eventName, value);
    }
}