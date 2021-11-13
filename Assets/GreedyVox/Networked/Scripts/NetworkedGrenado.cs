using GreedyVox.Networked.Data;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Objects;
using UnityEngine;

/// <summary>
/// Initializes the grenade over the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedGrenado : Grenade, IPayload {
        private NetworkedItemDrop m_ItemDrop;
        protected override void Awake () {
            base.Awake ();
            m_ItemDrop = gameObject.GetCachedComponent<NetworkedItemDrop> ();
        }
        /// <summary>
        /// Initialization data that is required when the object spawns. This allows the remote players to initialize the object correctly.
        /// </summary>
        public void Load () {
            m_ItemDrop?.NetworkedVariable<PayloadGrenado> (new PayloadGrenado () {
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
            });
        }
        /// <summary>
        /// The object has been spawned. Initialize the grenade.
        /// </summary>
        public void Unload<T> (T val, GameObject go) where T : unmanaged {
            PayloadGrenado? dat = val as PayloadGrenado?;
            if (dat != null) {
                Initialize (dat.Value.Velocity,
                    dat.Value.Torque,
                    m_DamageProcessor,
                    dat.Value.DamageAmount,
                    dat.Value.ImpactForce,
                    dat.Value.ImpactFrames,
                    dat.Value.ImpactLayers,
                    dat.Value.ImpactStateName,
                    dat.Value.ImpactStateDisableTimer,
                    null, go, false);
                // The grenade should start cooking.
                var deactivationTime = dat.Value.ScheduledDeactivation;
                if (deactivationTime > 0) {
                    m_ScheduledDeactivation = Scheduler.Schedule (deactivationTime, Deactivate);
                }
            }
        }
    }
}