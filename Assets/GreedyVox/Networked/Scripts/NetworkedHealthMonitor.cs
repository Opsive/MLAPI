using System.Collections.Generic;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Game;
using Opsive.UltimateCharacterController.Networking.Traits;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes the Health component over the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedHealthMonitor : NetworkBehaviour, INetworkHealthMonitor {
        [Tooltip ("Spawn objects on death over the network.")]
        [SerializeField] private GameObject[] m_SpawnObjectsOnDeath;
        private Health m_Health;
        private GameObject m_GamingObject;
        private NetworkedSettingsAbstract m_Settings;
        // private Dictionary<ulong, NetworkedClient> m_NetworkObjects;
        private Dictionary<ulong, NetworkObject> m_NetworkObjects;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake () {
            m_GamingObject = gameObject;
            m_NetworkObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
            m_Settings = NetworkedManager.Instance.NetworkSettings;
            m_Health = m_GamingObject.GetCachedComponent<Health> ();
        }
        /// <summary>
        /// Spawn objects on death over the network.
        /// <param name="position">The position of the damage.</param>
        /// <param name="direction">The direction that the object took damage from.</param>
        /// </summary>
        private void SpawnObjectsOnDeath (Vector3 position, Vector3 force) {
            // Spawn any objects on death, such as an explosion if the object is an explosive barrel.
            if (m_SpawnObjectsOnDeath != null) {
                Explosion exp;
                for (int n = 0; n < m_SpawnObjectsOnDeath.Length; n++) {
                    var go = m_SpawnObjectsOnDeath[n];
                    var obj = ObjectPool.Instantiate (go, transform.position, transform.rotation);
                    if (obj == null || obj.GetComponent<NetworkObject> () == null) {
                        Debug.LogError ($"Spawning Obect {obj} over network requires having the NetworkedObject component.");
                        continue;
                    }

                    NetworkObjectPool.NetworkSpawn (go, obj, true);
                    if ((exp = obj.GetCachedComponent<Explosion> ()) != null) {
                        exp.Explode (gameObject);
                    }

                    var rigs = obj.GetComponentsInChildren<Rigidbody> ();
                    for (int i = 0; i < rigs.Length; i++) {
                        rigs[i].AddForceAtPosition (force, position);
                    }
                }
            }
        }
        /// <summary>
        /// The object has taken been damaged.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="direction">The direction that the object took damage from.</param>
        /// <param name="magnitude">The magnitude of the force that is applied to the object.</param>
        /// <param name="frames">The number of frames to add the force to.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-explosive force will be used.</param>
        /// <param name="attacker">The GameObject that did the damage.</param>
        /// <param name="collider">The Collider that was hit.</param>
        public void OnDamage (float amount, Vector3 position, Vector3 direction, float magnitude, int frames, float radius, GameObject attacker, Collider collider) {
            // An attacker is not required. If one exists it must have a NetworkObject component attached for identification purposes.
            var attackerID = -1L;
            if (attacker != null) {
                var attackerObject = attacker.GetCachedComponent<NetworkObject> ();
                if (attackerObject == null) {
                    Debug.LogError ("Error: The attacker " + attacker.name + " must have a NetworkObject component.");
                    return;
                }
                attackerID = (long) attackerObject.NetworkObjectId;
            }
            // A hit collider is not required. If one exists it must have an ObjectIdentifier or NetworkObject attached for identification purposes.
            var hitSlotID = -1;
            var colliderID = 0UL;
            if (collider != null) {
                colliderID = NetworkedUtility.GetID (collider.gameObject, out hitSlotID);
            }
            if (IsServer) {
                DamageClientRpc (amount, position, direction, magnitude, frames, radius, attackerID, (long) colliderID, hitSlotID);
            } else {
                DamageServerRpc (amount, position, direction, magnitude, frames, radius, attackerID, (long) colliderID, hitSlotID);
            }
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
        /// <param name="attackerViewID">The NetworkObject ID of the GameObject that did the damage.</param>
        /// <param name="hitID">The NetworkObject or ObjectIdentifier ID of the Collider that was hit.</param>
        /// <param name="hitSlotID">If the hit collider is an item then the slot ID of the item will be specified.</param>
        private void DamageRpc (float amount, Vector3 position, Vector3 direction, float magnitude, int frames, float radius, long attackerID, long hitID, int hitSlotID) {
            GameObject attacker = null;
            if (attackerID != -1) {
                if (m_NetworkObjects.TryGetValue ((ulong) attackerID, out var obj)) {
                    attacker = obj.gameObject;
                }
            }
            var collider = NetworkedUtility.RetrieveGameObject (m_GamingObject, (ulong) hitID, hitSlotID);
            m_Health.OnDamage (amount, position, direction, magnitude, frames, radius,
                attacker != null ? attacker.gameObject : null, null,
                collider != null ? collider.GetCachedComponent<Collider> () : null);
        }

        [ServerRpc (RequireOwnership = false)]
        private void DamageServerRpc (float amount, Vector3 position, Vector3 direction, float magnitude, int frames, float radius, long attackerID, long hitID, int hitSlotID) {
            if (!IsClient) { DamageRpc (amount, position, direction, magnitude, frames, radius, attackerID, hitID, hitSlotID); }
            DamageClientRpc (amount, position, direction, magnitude, frames, radius, attackerID, hitID, hitSlotID);
        }

        [ClientRpc]
        private void DamageClientRpc (float amount, Vector3 position, Vector3 direction, float magnitude, int frames, float radius, long attackerID, long hitID, int hitSlotID) {
            DamageRpc (amount, position, direction, magnitude, frames, radius, attackerID, hitID, hitSlotID);
        }
        /// <summary>
        /// The object is no longer alive.
        /// </summary>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attacker">The GameObject that killed the character.</param>
        public void Die (Vector3 position, Vector3 force, GameObject attacker) {
            // An attacker is not required. If one exists it must have a NetworkObject component attached for identification purposes.
            var attackerID = -1L;
            if (attacker != null) {
                var attackerObject = attacker.GetCachedComponent<NetworkObject> ();
                if (attackerObject == null) {
                    Debug.LogError ("Error: The attacker " + attacker.name + " must have a NetworkObject component.");
                    return;
                }
                attackerID = (long) attackerObject.NetworkObjectId;
            }
            if (IsServer) {
                SpawnObjectsOnDeath (position, force);
                NetworkObjectPool.Destroy (gameObject);
                DieClientRpc (position, force, attackerID);
            } else {
                DieServerRpc (position, force, attackerID);
            }
        }
        /// <summary>
        /// The object is no longer alive on the network.
        /// </summary>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attackerID">The NetworkObject ID of the GameObject that killed the object.</param>
        private void DieRpc (Vector3 position, Vector3 force, long attackerID) {
            GameObject attacker = null;
            if (attackerID != -1) {
                if (m_NetworkObjects.TryGetValue ((ulong) attackerID, out var obj)) {
                    attacker = obj.gameObject;
                }
            }
            m_Health.Die (position, force, attacker != null ? attacker.gameObject : null);
        }

        [ServerRpc]
        private void DieServerRpc (Vector3 position, Vector3 force, long attackerID) {
            SpawnObjectsOnDeath (position, force);
            NetworkObjectPool.Destroy (gameObject);
            if (!IsClient) { DieRpc (position, force, attackerID); }
            DieClientRpc (position, force, attackerID);
        }

        [ClientRpc]
        private void DieClientRpc (Vector3 position, Vector3 force, long attackerID) {
            if (!IsOwner) { DieRpc (position, force, attackerID); }
        }
        /// <summary>
        /// Adds amount to health and then to the shield if there is still an amount remaining. Will not go over the maximum health or shield value.
        /// </summary>
        /// <param name="amount">The amount of health or shield to add.</param>
        public void Heal (float amount) {
            if (IsServer) {
                HealClientRpc (amount);
            } else {
                HealServerRpc (amount);
            }
        }
        /// <summary>
        /// Adds amount to health and then to the shield if there is still an amount remaining on the network.
        /// </summary>
        /// <param name="amount">The amount of health or shield to add.</param>
        private void HealRpc (float amount) {
            m_Health.Heal (amount);
        }

        [ServerRpc]
        private void HealServerRpc (float amount) {
            if (!IsClient) { HealRpc (amount); }
            HealClientRpc (amount);
        }

        [ClientRpc]
        private void HealClientRpc (float amount) {
            if (!IsOwner) { HealRpc (amount); }
        }
    }
}