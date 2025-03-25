using System;
using GreedyVox.NetCode.Data;
using GreedyVox.NetCode.Interfaces;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Items.Actions.Impact;
using Opsive.UltimateCharacterController.Objects;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode.Objects
{
    /// <summary>
    /// Initializes the grenade over the network.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetCodeInfo))]
    public class NetCodeGrenade : Grenade, IPayload
    {
        private const string m_StateName = "Grenade Network Impact";
        private ImpactDamageData m_DamageData = new();
        private PayloadGrenado m_Data;
        public int NetworkID { get; set; }
        /// <summary>
        /// Initializes the object. This will be called from an object creating the projectile (such as a weapon).
        /// </summary>
        /// <param name="id">The id used to differentiate this projectile from others.</param>
        /// <param name="owner">The object that instantiated the trajectory object.</param>
        public void Initialize(uint id, GameObject own)
        {
            InitializeComponentReferences();
            Initialize(id, Vector3.zero, Vector3.zero, own, m_DamageData);
        }
        /// <summary>
        /// Initialize the payload data values.
        /// </summary>
        private PayloadGrenado PayLoad()
        {
            return new PayloadGrenado()
            {
                OwnerID = m_ID,
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = m_Velocity,
                Torque = m_Torque,
                ImpactFrames = m_ImpactDamageData.ImpactForceFrames,
                ImpactLayers = m_ImpactLayers.value,
                ImpactForce = m_ImpactDamageData.ImpactForce,
                DamageAmount = m_ImpactDamageData.DamageAmount,
                ImpactStateDisableTimer = m_ImpactDamageData.ImpactStateDisableTimer,
                ScheduledDeactivation = m_ScheduledDeactivation != null ?
                (m_ScheduledDeactivation.EndTime - Time.time) : -1,
                NetCodeObject = m_Owner?.GetCachedComponent<NetworkObject>()
            };
        }
        /// <summary>
        /// The object has been spawned, write the payload data.
        /// </summary>
        public bool PayLoad(ref int idx, out FastBufferWriter writer)
        {
            try
            {
                using (writer = new FastBufferWriter(MaxBufferSize(), Allocator.Temp))
                {
                    writer.WriteValueSafe(idx);
                    writer.WriteValueSafe(PayLoad());
                }
                return true;
            }
            catch (Exception e)
            {
                NetworkLog.LogErrorServer($"{e.Message} [Length={writer.Length}/{writer.MaxCapacity}]");
                return false;
            }
        }
        /// <summary>
        /// The object has been spawned, read the payload data.
        /// </summary>
        public void PayLoad(in FastBufferReader reader, GameObject own = default)
        {
            reader.ReadValueSafe(out m_Data);
            if (m_Data.NetCodeObject.TryGet(out var net))
                own = net.gameObject;
            transform.position = m_Data.Position;
            transform.rotation = m_Data.Rotation;
            m_DamageData ??= new ImpactDamageData();
            m_DamageData.DamageAmount = m_Data.DamageAmount;
            m_DamageData.ImpactForce = m_Data.ImpactForce;
            m_DamageData.ImpactForceFrames = m_Data.ImpactFrames;
            m_ImpactLayers = m_Data.ImpactLayers;
            m_DamageData.ImpactStateName = m_StateName;
            m_DamageData.ImpactStateDisableTimer = m_Data.ImpactStateDisableTimer;
            Initialize(m_Data.OwnerID, m_Data.Velocity, m_Data.Torque, own, m_DamageData);
            // The grenade should start cooking.
            var deactivationTime = m_Data.ScheduledDeactivation;
            if (deactivationTime > 0)
                m_ScheduledDeactivation = Scheduler.Schedule(deactivationTime, Deactivate);
        }
        /// <summary>
        /// Returns the maximus size for the fast buffer writer
        /// </summary>
        public int MaxBufferSize()
        {
            return
            FastBufferWriter.GetWriteSize<int>() +
            FastBufferWriter.GetWriteSize(NetworkID) +
            FastBufferWriter.GetWriteSize(m_Data.OwnerID) +
            FastBufferWriter.GetWriteSize(m_Data.Position) +
            FastBufferWriter.GetWriteSize(m_Data.Rotation) +
            FastBufferWriter.GetWriteSize(m_Data.Velocity) +
            FastBufferWriter.GetWriteSize(m_Data.Torque) +
            FastBufferWriter.GetWriteSize(m_Data.ImpactFrames) +
            FastBufferWriter.GetWriteSize(m_Data.ImpactLayers) +
            FastBufferWriter.GetWriteSize(m_Data.ImpactForce) +
            FastBufferWriter.GetWriteSize(m_Data.DamageAmount) +
            FastBufferWriter.GetWriteSize(m_Data.ImpactStateDisableTimer) +
            FastBufferWriter.GetWriteSize(m_Data.ScheduledDeactivation) +
            FastBufferWriter.GetWriteSize(m_Data.NetCodeObject);
        }
    }
}