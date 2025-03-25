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
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class NetCodeProjectile : Projectile, IPayload
    {
        private ImpactDamageData m_DamageData;
        private PayloadProjectile m_Data;
        public int NetworkID { get; set; }
        /// <summary>
        /// Returns the initialization data that is required when the object spawns. This allows the remote players to initialize the object correctly.
        /// </summary>
        /// <returns>The initialization data that is required when the object spawns.</returns>
        private void Start()
        {
            var net = m_Owner.GetCachedComponent<NetworkObject>();
            m_Data = new PayloadProjectile()
            {
                OwnerID = net == null ? -1L : (long)net.OwnerClientId,
                ProjectileID = m_ID,
                Velocity = m_Velocity,
                Torque = m_Torque,
                DamageAmount = m_ImpactDamageData.DamageAmount,
                ImpactForce = m_ImpactDamageData.ImpactForce,
                ImpactFrames = m_ImpactDamageData.ImpactForceFrames,
                ImpactLayers = m_ImpactLayers.value,
                ImpactStateDisableTimer = m_ImpactDamageData.ImpactStateDisableTimer,
                ImpactStateName = m_ImpactDamageData.ImpactStateName
            };
        }
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
        /// The object has been spawned, write the payload data.
        /// </summary>
        public bool PayLoad(ref int idx, out FastBufferWriter writer)
        {
            try
            {
                using (writer = new FastBufferWriter(MaxBufferSize(), Allocator.Temp))
                {
                    writer.WriteValueSafe(idx);
                    writer.WriteValueSafe(m_Data);
                }
                return true;
            }
            catch (Exception e)
            {
                NetworkLog.LogErrorServer(e.Message);
                return false;
            }
        }
        /// <summary>
        /// The object has been spawned. Initialize the projectile.
        /// </summary>
        public void PayLoad(in FastBufferReader reader, GameObject go = default)
        {
            if (go == null) return;
            reader.ReadValueSafe(out m_Data);
            m_DamageData ??= new ImpactDamageData();
            m_DamageData.DamageAmount = m_Data.DamageAmount;
            m_DamageData.ImpactForce = m_Data.ImpactForce;
            m_DamageData.ImpactForceFrames = m_Data.ImpactFrames;
            m_ImpactLayers = m_Data.ImpactLayers;
            m_DamageData.ImpactStateName = m_Data.ImpactStateName;
            m_DamageData.ImpactStateDisableTimer = m_Data.ImpactStateDisableTimer;
            Initialize(m_Data.ProjectileID, m_Data.Velocity, m_Data.Torque, go, m_DamageData);
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
            FastBufferWriter.GetWriteSize(m_Data.ProjectileID) +
            FastBufferWriter.GetWriteSize(m_Data.Velocity) +
            FastBufferWriter.GetWriteSize(m_Data.Torque) +
            FastBufferWriter.GetWriteSize(m_Data.DamageAmount) +
            FastBufferWriter.GetWriteSize(m_Data.ImpactForce) +
            FastBufferWriter.GetWriteSize(m_Data.ImpactFrames) +
            FastBufferWriter.GetWriteSize(m_Data.ImpactLayers) +
            FastBufferWriter.GetWriteSize(m_Data.ImpactStateDisableTimer) +
            FastBufferWriter.GetWriteSize(m_Data.ImpactStateName);
        }
    }
}