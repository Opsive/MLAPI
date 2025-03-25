using GreedyVox.NetCode.Utilities;
using Opsive.Shared.Game;
using Opsive.Shared.Utility;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Items.Actions;
using Opsive.UltimateCharacterController.Networking.Traits;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.Traits;
using Opsive.UltimateCharacterController.Traits.Damage;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode.Traits
{
    /// <summary>
    /// Synchronizes the Health component over the network.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class NetCodeHealthAbstract : NetworkBehaviour, INetworkHealthMonitor
    {
        protected Health m_Health;
        protected InventoryBase m_Inventory;
        protected GameObject m_GamingObject;
        protected NetworkObject m_NetCodeObject;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        protected virtual void Awake()
        {
            m_GamingObject = gameObject;
            m_NetCodeObject = GetComponent<NetworkObject>();
            m_Health = m_GamingObject.GetCachedComponent<Health>();
            m_Inventory = m_GamingObject.GetCachedComponent<InventoryBase>();
        }
        /// <summary>
        /// The object has taken been damaged on the network.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="direction">The direction that the object took damage from.</param>
        /// <param name="forceMagnitude">The magnitude of the force that is applied to the object.</param>
        /// <param name="frames">The number of frames to add the force to.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-explosive force will be used.</param>
        /// <param name="sourceNetworkObjectID">The NetCode ID of the object that did the damage.</param>
        /// <param name="sourceItemIdentifierID">The ID of the source's Item Identifier.</param>
        /// <param name="sourceSlotID">The ID of the source's slot.</param>
        /// <param name="sourceItemActionID">The ID of the source's ItemAction.</param>
        /// <param name="hitColliderID">The NetCode or ObjectIdentifier ID of the Collider that was hit.</param>
        /// <param name="hitItemSlotID">If the hit collider is an item then the slot ID of the item will be specified.</param>
        public virtual void OnDamage(float amount, Vector3 position, Vector3 direction, float forceMagnitude, int frames, float radius, IDamageSource source, Collider hitCollider)
        {
            // A source is not required. If one exists it must have a NetworkObject component attached for identification purposes.
            var sourceSlotID = -1;
            var sourceItemActionID = -1;
            var sourceItemIdentifierID = 0U;
            NetworkObjectReference sourceNetworkObject = default;
            if (source != null)
            {
                // If the originator is an item then more data needs to be sent.
                CharacterItemAction itemAction = null;
                if (source is CharacterItemAction)
                {
                    itemAction = source as CharacterItemAction;
                }
                else if (source is Explosion)
                {
                    itemAction = source.OwnerDamageSource as CharacterItemAction;
                }
                if (itemAction != null)
                {
                    sourceItemActionID = itemAction.ID;
                    sourceSlotID = itemAction.CharacterItem.SlotID;
                    sourceItemIdentifierID = itemAction.CharacterItem.ItemIdentifier.ID;
                }
                if (source.SourceGameObject != null)
                {
                    var originatorNetworkObject = source.SourceGameObject.GetCachedComponent<NetworkObject>();
                    if (originatorNetworkObject == null)
                    {
                        originatorNetworkObject = source.SourceOwner.GetCachedComponent<NetworkObject>();
                        if (originatorNetworkObject == null)
                        {
                            Debug.LogError($"Error: The attacker {source.SourceOwner.name} must have a NetCode component.");
                            return;
                        }
                    }
                    sourceNetworkObject = originatorNetworkObject;
                }
            }
            // A hit collider is not required. If one exists it must have an ObjectIdentifier or NetCode attached for identification purposes.
            (ulong ID, bool) hitColliderPair;
            var hitItemSlotID = -1;
            if (hitCollider != null)
                hitColliderPair = NetCodeUtility.GetID(hitCollider.gameObject, out hitItemSlotID);
            else
                hitColliderPair = (0UL, false);
            DamageRpc(amount, position, direction, forceMagnitude, frames, radius, sourceNetworkObject,
            sourceItemIdentifierID, sourceSlotID, sourceItemActionID, hitColliderPair.ID, hitItemSlotID);
        }
        /// <summary>
        /// The object has taken been damaged on the network.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="direction">The direction that the object took damage from.</param>
        /// <param name="forceMagnitude">The magnitude of the force that is applied to the object.</param>
        /// <param name="frames">The number of frames to add the force to.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-explosive force will be used.</param>
        /// <param name="sourceNetworkObjectID">The NetCode ID of the object that did the damage.</param>
        /// <param name="sourceItemIdentifierID">The ID of the source's Item Identifier.</param>
        /// <param name="sourceSlotID">The ID of the source's slot.</param>
        /// <param name="sourceItemActionID">The ID of the source's ItemAction.</param>
        /// <param name="hitColliderID">The NetCode or ObjectIdentifier ID of the Collider that was hit.</param>
        /// <param name="hitItemSlotID">If the hit collider is an item then the slot ID of the item will be specified.</param>
        /// 
        [Rpc(SendTo.Everyone, Delivery = RpcDelivery.Reliable)]
        protected virtual void DamageRpc(float amount, Vector3 position, Vector3 direction, float forceMagnitude, int frames, float radius,
        NetworkObjectReference sourceNetworkObject, uint sourceItemIdentifierID, int sourceSlotID, int sourceItemActionID, ulong hitColliderID, int hitItemSlotID)
        {
            IDamageSource source = null;
            if (sourceNetworkObject.TryGet(out var net))
            {
                var sourceView = net.gameObject;
                source = sourceView?.GetComponent<IDamageSource>();
                // If the originator is null then it may have come from an item.
                if (source == null)
                {
                    var itemType = ItemIdentifierTracker.GetItemIdentifier(sourceItemIdentifierID);
                    m_Inventory = sourceView.GetComponent<InventoryBase>();
                    if (itemType != null && m_Inventory != null)
                    {
                        var item = m_Inventory.GetCharacterItem(itemType, sourceSlotID);
                        source = item?.GetItemAction(sourceItemActionID) as IDamageSource;
                    }
                }
            }
            var hitCollider = NetCodeUtility.RetrieveGameObject(m_GamingObject, hitColliderID, hitItemSlotID);
            var pooledDamageData = GenericObjectPool.Get<DamageData>();
            pooledDamageData.SetDamage(source, amount, position, direction, forceMagnitude, frames, radius,
            hitCollider?.GetCachedComponent<Collider>());
            m_Health.OnDamage(pooledDamageData);
            GenericObjectPool.Return(pooledDamageData);
        }
        /// <summary>
        /// The object is no longer alive.
        /// </summary>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attacker">The GameObject that killed the character.</param>
        public virtual void Die(Vector3 position, Vector3 force, GameObject attacker)
        {
            // An attacker is not required. If one exists it must have a NetworkObject component attached for identification purposes.
            NetworkObject attackerObject = null;
            if (attacker != null)
            {
                attackerObject = attacker.GetCachedComponent<NetworkObject>();
                if (attackerObject == null)
                {
                    Debug.LogError("Error: The attacker " + attacker.name + " must have a NetworkObject component.");
                    return;
                }
            }
            DieRpc(position, force, attackerObject);
        }
        /// <summary>
        /// The object is no longer alive on the network.
        /// </summary>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attackerID">The NetworkObject ID of the GameObject that killed the object.</param>
        /// 
        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Reliable)]
        protected virtual void DieRpc(Vector3 position, Vector3 force, NetworkObjectReference obj)
        {
            obj.TryGet(out var attacker);
            m_Health.Die(position, force, attacker?.gameObject);
        }
        /// <summary>
        /// Adds amount to health and then to the shield if there is still an amount remaining. Will not go over the maximum health or shield value.
        /// </summary>
        /// <param name="amount">The amount of health or shield to add.</param>
        public virtual void Heal(float amount) => HealRpc(amount);
        /// <summary>
        /// Adds amount to health and then to the shield if there is still an amount remaining on the network.
        /// </summary>
        /// <param name="amount">The amount of health or shield to add.</param>
        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Reliable)]
        protected virtual void HealRpc(float amount) => m_Health.Heal(amount);
    }
}