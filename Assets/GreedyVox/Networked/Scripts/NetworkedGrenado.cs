using System.IO;
using MLAPI.Serialization.Pooled;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Objects;
using UnityEngine;

/// <summary>
/// Initializes the grenade over the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedGrenado : Grenade, ISpawnDataObject {
        /// <summary>
        /// Returns the initialization data that is required when the object spawns. This allows the remote players to initialize the object correctly.
        /// </summary>
        /// <returns>The initialization data that is required when the object spawns.</returns>
        public void SpawnData (PooledNetworkWriter writer) {
            writer.WriteVector3Packed (m_Velocity);
            writer.WriteVector3Packed (m_Torque);
            writer.WriteSinglePacked (m_DamageAmount);
            writer.WriteSinglePacked (m_ImpactForce);
            writer.WriteInt32Packed (m_ImpactForceFrames);
            writer.WriteInt32Packed (m_ImpactLayers.value);
            writer.WriteStringPacked (m_ImpactStateName);
            writer.WriteSinglePacked (m_ImpactStateDisableTimer);
            writer.WriteSinglePacked (m_ScheduledDeactivation != null ? (m_ScheduledDeactivation.EndTime - Time.time) : -1);
        }
        /// <summary>
        /// The object has been spawned. Initialize the grenade.
        /// </summary>
        public void ObjectSpawned (Stream stream, GameObject go) {
            // Initialize the grenade from the data within the InitializationData field.
            using (PooledNetworkReader reader = PooledNetworkReader.Get (stream)) {
                Initialize (
                    reader.ReadVector3Packed (),
                    reader.ReadVector3Packed (),
                    reader.ReadSinglePacked (),
                    reader.ReadSinglePacked (),
                    reader.ReadInt32Packed (),
                    reader.ReadInt32Packed (),
                    reader.ReadStringPacked ().ToString (),
                    reader.ReadSinglePacked (),
                    null, go, false);

                // The grenade should start cooking.
                var deactivationTime = reader.ReadSinglePacked ();
                if (deactivationTime > 0) {
                    m_ScheduledDeactivation = Scheduler.Schedule (deactivationTime, Deactivate);
                }
            }
        }
    }
}