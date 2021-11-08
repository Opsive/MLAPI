using System.Linq;
using GreedyVox.Networked.Data;
using Opsive.Shared.Game;
using Opsive.Shared.Utility;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Initializes the item pickup over the network.
/// </summary>
namespace GreedyVox.Networked {
    public class NetworkedItemPickup : ItemPickup, IPayload {
        private NetworkedItemDrop m_ItemDrop;
        private NetworkedInfo m_NetworkInfo;
        private TrajectoryObject m_TrajectoryObject;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake () {
            base.Awake ();
            m_NetworkInfo = gameObject.GetCachedComponent<NetworkedInfo> ();
            m_ItemDrop = gameObject.GetCachedComponent<NetworkedItemDrop> ();
            m_TrajectoryObject = gameObject.GetCachedComponent<TrajectoryObject> ();
        }
        /// <summary>
        /// Returns the initialization data that is required when the object spawns.
        /// This allows the remote players to initialize the object correctly.
        /// </summary>
        /// <returns>The initialization data that is required when the object spawns.</returns>
        public void Load () {
            var net = m_TrajectoryObject?.Originator.GetCachedComponent<NetworkObject> ();
            m_ItemDrop?.NetworkedVariable<PayloadItemPickup> (new PayloadItemPickup () {
                ItemCount = m_ItemDefinitionAmounts.Length * 2 + (m_TrajectoryObject != null ? 2 : 0),
                    ItemID = m_ItemDefinitionAmounts.Select (items => (items.ItemIdentifier as ItemType).ID).ToArray (),
                    ItemAmounts = m_ItemDefinitionAmounts.Select (items => items.Amount).ToArray (),
                    OwnerID = net == null ? -1L : (long) net.OwnerClientId,
                    Velocity = m_TrajectoryObject.Velocity,
                    Torque = m_TrajectoryObject.Torque,
            });
        }
        /// <summary>
        /// The object has been spawned. Initialize the item pickup.
        /// </summary>
        public void Unload<T> (T val, GameObject go) where T : unmanaged {
            PayloadItemPickup? dat = val as PayloadItemPickup?;
            // Return the old.
            for (int i = 0; i < m_ItemDefinitionAmounts.Length; i++) {
                GenericObjectPool.Return (m_ItemDefinitionAmounts[i]);
            }
            if (dat != null) {
                // Setup the item counts.
                var itemDefinitionAmountLength = (dat.Value.ItemCount - (m_TrajectoryObject != null ? 2 : 0)) / 2;
                if (m_ItemDefinitionAmounts.Length != itemDefinitionAmountLength) {
                    m_ItemDefinitionAmounts = new ItemDefinitionAmount[itemDefinitionAmountLength];
                }
                for (int n = 0; n < itemDefinitionAmountLength; n++) {
                    m_ItemDefinitionAmounts[n] = new ItemDefinitionAmount (ItemIdentifierTracker.GetItemIdentifier (
                        dat.Value.ItemID[n]).GetItemDefinition (), dat.Value.ItemAmounts[n]);
                }
                Initialize (true);
                // Setup the trajectory object.
                if (m_TrajectoryObject != null) {
                    var velocity = dat.Value.Velocity;
                    var torque = dat.Value.Torque;
                    GameObject originator = null;
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue ((ulong) dat.Value.OwnerID, out var obj)) {
                        originator = obj.gameObject;
                    }
                    m_TrajectoryObject.Initialize (velocity, torque, originator);
                }
            }
        }
    }
}