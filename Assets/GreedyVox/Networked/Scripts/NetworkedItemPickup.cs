using System;
using System.Linq;
using GreedyVox.Networked.Data;
using Opsive.Shared.Game;
using Opsive.Shared.Utility;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Initializes the item pickup over the network.
/// </summary>
namespace GreedyVox.Networked {
    public class NetworkedItemPickup : ItemPickup, IPayload {
        private PayloadItemPickup m_Data;
        private TrajectoryObject m_TrajectoryObject;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake () {
            base.Awake ();
            m_TrajectoryObject = gameObject.GetCachedComponent<TrajectoryObject> ();
        }
        /// <summary>
        /// Initialize the default data values.
        /// </summary>
        public void OnNetworkSpawn () {
            var net = m_TrajectoryObject?.Originator?.GetCachedComponent<NetworkObject> ();
            m_Data = new PayloadItemPickup () {
                ItemCount = m_ItemDefinitionAmounts.Length * 2 + (m_TrajectoryObject != null ? 2 : 0),
                ItemID = m_ItemDefinitionAmounts.Select (items => (items.ItemIdentifier as ItemType).ID).ToArray (),
                ItemAmounts = m_ItemDefinitionAmounts.Select (items => items.Amount).ToArray (),
                OwnerID = net == null ? -1L : (long) net.OwnerClientId,
                Velocity = m_TrajectoryObject == null ? Vector3.zero : m_TrajectoryObject.Velocity,
                Torque = m_TrajectoryObject == null ? Vector3.zero : m_TrajectoryObject.Torque
            };
        }
        /// <summary>
        /// Returns the maximus size for the fast buffer writer
        /// </summary>               
        public int MaxBufferSize () {
            return FastBufferWriter.GetWriteSize (m_Data.ItemCount) +
                FastBufferWriter.GetWriteSize (m_Data.ItemID) +
                FastBufferWriter.GetWriteSize (m_Data.ItemAmounts) +
                FastBufferWriter.GetWriteSize (m_Data.OwnerID) +
                FastBufferWriter.GetWriteSize (m_Data.Velocity) +
                FastBufferWriter.GetWriteSize (m_Data.Torque);
        }
        /// <summary>
        /// The object has been spawned, write the payload data.
        /// </summary>
        public bool Load (out FastBufferWriter writer) {
            try {
                using (writer = new FastBufferWriter (MaxBufferSize (), Allocator.Temp))
                writer.WriteValueSafe (m_Data);
                return true;
            } catch (Exception e) {
                NetworkLog.LogErrorServer (e.Message);
                return false;
            }
        }
        /// <summary>
        /// The object has been spawned, read the payload data.
        /// </summary>
        public void Unload (ref FastBufferReader reader, GameObject go) {
            reader.ReadValueSafe (out m_Data);
            // Return the old.
            for (int i = 0; i < m_ItemDefinitionAmounts.Length; i++) {
                GenericObjectPool.Return (m_ItemDefinitionAmounts[i]);
            }
            // Setup the item counts.
            var itemDefinitionAmountLength = (m_Data.ItemCount - (m_TrajectoryObject != null ? 2 : 0)) / 2;
            if (m_ItemDefinitionAmounts.Length != itemDefinitionAmountLength) {
                m_ItemDefinitionAmounts = new ItemDefinitionAmount[itemDefinitionAmountLength];
            }
            for (int n = 0; n < itemDefinitionAmountLength; n++) {
                m_ItemDefinitionAmounts[n] = new ItemDefinitionAmount (ItemIdentifierTracker.GetItemIdentifier (
                    m_Data.ItemID[n]).GetItemDefinition (), m_Data.ItemAmounts[n]);
            }
            Initialize (true);
            // Setup the trajectory object.
            if (m_TrajectoryObject != null) {
                var velocity = m_Data.Velocity;
                var torque = m_Data.Torque;
                GameObject originator = null;
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue ((ulong) m_Data.OwnerID, out var obj)) {
                    originator = obj.gameObject;
                }
                m_TrajectoryObject.Initialize (velocity, torque, originator);
            }
        }
    }
}