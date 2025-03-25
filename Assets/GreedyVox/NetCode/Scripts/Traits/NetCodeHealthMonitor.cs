using GreedyVox.NetCode.Game;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Game;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode.Traits
{
    /// <summary>
    /// Synchronizes the Health component over the network.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetCodeHealthMonitor : NetCodeHealthAbstract
    {
        [Tooltip("Spawn objects on death over the network.")]
        [SerializeField] protected GameObject[] m_SpawnObjectsOnDeath;
        public override void Die(Vector3 position, Vector3 force, GameObject attacker)
        {
            base.Die(position, force, attacker);
            if (IsServer)
            {
                SpawnObjectsOnDeath(position, force);
                if (TryGetComponent<Respawner>(out var com)
                && (com.ScheduleRespawnOnDeath || com.ScheduleRespawnOnDisable))
                    return;
                if (m_NetCodeObject == null)
                    ObjectPool.Destroy(m_GamingObject);
                else NetCodeObjectPool.Destroy(m_GamingObject);
            }
        }
        /// <summary>
        /// Spawn objects on death over the network.
        /// <param name="position">The position of the damage.</param>
        /// <param name="direction">The direction that the object took damage from.</param>
        /// </summary>
        protected virtual void SpawnObjectsOnDeath(Vector3 position, Vector3 force)
        {
            // Spawn any objects on death, such as an explosion if the object is an explosive barrel.
            if (m_SpawnObjectsOnDeath != null)
            {
                for (int n = 0; n < m_SpawnObjectsOnDeath.Length; n++)
                {
                    var go = m_SpawnObjectsOnDeath[n];
                    var obj = ObjectPool.Instantiate(go, transform.position, transform.rotation);
                    if (obj == null || obj.GetComponent<NetworkObject>() == null)
                    {
                        Debug.LogError($"Spawning Obect {obj} over network requires having the NetCodeObject component.");
                        continue;
                    }
                    NetworkObjectPool.NetworkSpawn(go, obj, true);
                    if (obj.TryGetComponent<Explosion>(out var exp))
                        exp.Explode(gameObject);
                    var rigs = obj.GetComponentsInChildren<Rigidbody>();
                    for (int i = 0; i < rigs.Length; i++)
                        rigs[i].AddForceAtPosition(force, position);
                }
            }
        }
    }
}