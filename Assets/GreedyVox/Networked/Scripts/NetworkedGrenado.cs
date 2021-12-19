using System;
using GreedyVox.Networked.Data;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Objects;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Initializes the grenade over the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedGrenado : Grenade, IPayload {
        private PayloadGrenado m_Data;
        /// <summary>
        /// Initialize the default data values.
        /// </summary>
        public void OnNetworkSpawn () {
            base.OnEnable ();
            m_Data = new PayloadGrenado () {
                ImpactStateName = m_ImpactStateName,
                Velocity = m_Velocity,
                Torque = m_Torque,
                ImpactFrames = m_ImpactForceFrames,
                ImpactLayers = m_ImpactLayers,
                ImpactForce = m_ImpactForce,
                DamageAmount = m_DamageAmount,
                ImpactStateDisableTimer = m_ImpactStateDisableTimer,
                ScheduledDeactivation = m_ScheduledDeactivation != null ?
                (m_ScheduledDeactivation.EndTime - Time.time) : -1
            };
        }
        /// <summary>
        /// Returns the maximus size for the fast buffer writer
        /// </summary>
        public int MaxBufferSize () {
            return FastBufferWriter.GetWriteSize (m_Data.ImpactStateName) +
                FastBufferWriter.GetWriteSize (m_Data.Velocity) +
                FastBufferWriter.GetWriteSize (m_Data.Torque) +
                FastBufferWriter.GetWriteSize (m_Data.ImpactFrames) +
                FastBufferWriter.GetWriteSize (m_Data.ImpactLayers) +
                FastBufferWriter.GetWriteSize (m_Data.ImpactForce) +
                FastBufferWriter.GetWriteSize (m_Data.DamageAmount) +
                FastBufferWriter.GetWriteSize (m_Data.ImpactStateDisableTimer) +
                FastBufferWriter.GetWriteSize (m_Data.ScheduledDeactivation);
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
                NetworkLog.LogErrorServer ($"{e.Message} [Length={writer.Length}/{writer.MaxCapacity}]");
                return false;
            }
        }
        /// <summary>
        /// The object has been spawned, read the payload data.
        /// </summary>
        public void Unload (ref FastBufferReader reader, GameObject go) {
            reader.ReadValueSafe (out m_Data);
            Initialize (m_Data.Velocity,
                m_Data.Torque,
                m_DamageProcessor,
                m_Data.DamageAmount,
                m_Data.ImpactForce,
                m_Data.ImpactFrames,
                m_Data.ImpactLayers,
                m_Data.ImpactStateName,
                m_Data.ImpactStateDisableTimer,
                null, go, false);
            // The grenade should start cooking.
            var deactivationTime = m_Data.ScheduledDeactivation;
            if (deactivationTime > 0) {
                m_ScheduledDeactivation = Scheduler.Schedule (deactivationTime, Deactivate);
            }
        }
    }
}