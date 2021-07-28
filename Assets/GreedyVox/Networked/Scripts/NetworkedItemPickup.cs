using System.IO;
using MLAPI;
using MLAPI.Serialization.Pooled;
using MLAPI.Spawning;
using Opsive.Shared.Game;
using Opsive.Shared.Utility;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using UnityEngine;

/// <summary>
/// Initializes the item pickup over the network.
/// </summary>
namespace GreedyVox.Networked {
    public class NetworkedItemPickup : ItemPickup, ISpawnDataObject {
        private NetworkedInfo m_NetworkInfo;
        private TrajectoryObject m_TrajectoryObject;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake () {
            base.Awake ();
            m_NetworkInfo = gameObject.GetCachedComponent<NetworkedInfo> ();
            m_TrajectoryObject = gameObject.GetCachedComponent<TrajectoryObject> ();
        }
        /// <summary>
        /// Returns the initialization data that is required when the object spawns. This allows the remote players to initialize the object correctly.
        /// </summary>
        /// <returns>The initialization data that is required when the object spawns.</returns>
        public void SpawnData (PooledNetworkWriter stream) {
            stream.WriteInt32Packed (m_ItemDefinitionAmounts.Length * 2 + (m_TrajectoryObject != null ? 2 : 0));
            for (int i = 0; i < m_ItemDefinitionAmounts.Length; i++) {
                stream.WriteUInt32Packed ((m_ItemDefinitionAmounts[i].ItemIdentifier as ItemType).ID);
                stream.WriteInt32Packed (m_ItemDefinitionAmounts[i].Amount);
            }

            if (m_TrajectoryObject != null) {
                stream.WriteVector3Packed (m_TrajectoryObject.Velocity);
                stream.WriteVector3Packed (m_TrajectoryObject.Torque);
                var net = m_TrajectoryObject?.Originator.GetCachedComponent<NetworkObject> ();
                stream.WriteInt64Packed (net == null ? -1L : (long) net.OwnerClientId);
            }
        }
        /// <summary>
        /// The object has been spawned. Initialize the item pickup.
        /// </summary>
        public void ObjectSpawned (Stream stream, GameObject go) {
            // Return the old.
            for (int i = 0; i < m_ItemDefinitionAmounts.Length; i++) {
                GenericObjectPool.Return (m_ItemDefinitionAmounts[i]);
            }
            if (stream != null) {
                using (PooledNetworkReader reader = PooledNetworkReader.Get (stream)) {
                    // Setup the item counts.
                    var itemDefinitionAmountLength = (reader.ReadInt32Packed () - (m_TrajectoryObject != null ? 2 : 0)) / 2;
                    if (m_ItemDefinitionAmounts.Length != itemDefinitionAmountLength) {
                        m_ItemDefinitionAmounts = new ItemDefinitionAmount[itemDefinitionAmountLength];
                    }
                    for (int i = 0; i < itemDefinitionAmountLength; i++) {
                        m_ItemDefinitionAmounts[i] = new ItemDefinitionAmount (ItemIdentifierTracker.GetItemIdentifier (
                            reader.ReadUInt32Packed ()).GetItemDefinition (), reader.ReadInt32Packed ());
                    }
                    Initialize (true);
                    // Setup the trajectory object.
                    if (m_TrajectoryObject != null) {
                        var velocity = reader.ReadVector3Packed ();
                        var torque = reader.ReadVector3Packed ();
                        GameObject originator = null;
                        if (NetworkSpawnManager.SpawnedObjects.TryGetValue ((ulong) reader.ReadInt64Packed (), out var obj)) {
                            originator = obj.gameObject;
                        }
                        m_TrajectoryObject.Initialize (velocity, torque, originator);
                    }
                }
            }
        }
    }
}