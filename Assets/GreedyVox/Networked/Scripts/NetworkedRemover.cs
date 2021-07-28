using MLAPI;
using Opsive.Shared.Game;
using UnityEngine;

namespace GreedyVox.Networked {
    public class NetworkedRemover : NetworkBehaviour {
        [Tooltip ("The number of seconds until the object should be placed back in the pool.")]
        [SerializeField] protected float m_Lifetime = 5;
        private GameObject m_GameObject;
        private NetworkObject m_NetworkedObject;
        private ScheduledEventBase m_RemoveEvent;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake () {
            m_GameObject = gameObject;
            m_NetworkedObject = GetComponent<NetworkObject> ();
        }
        /// <summary>
        /// Schedule the object for removal.
        /// </summary>
        private void OnEnable () {
            m_RemoveEvent = Scheduler.Schedule (m_Lifetime, Remove);
        }
        /// <summary>
        /// Cancels the remove event.
        /// </summary>
        public void CancelRemoveEvent () {
            if (m_RemoveEvent != null) {
                Scheduler.Cancel (m_RemoveEvent);
                m_RemoveEvent = null;
            }
        }
        /// <summary>
        /// The object has been destroyed - no need for removal if it hasn't already been removed.
        /// </summary>
        private void OnDisable () {
            CancelRemoveEvent ();
        }
        /// <summary>
        /// Remove the object.
        /// </summary>
        private void Remove () {
            if (m_NetworkedObject == null) {
                ObjectPool.Destroy (m_GameObject);
            } else if (IsServer) {
                NetworkedObjectPool.Destroy (m_GameObject);
            }
            m_RemoveEvent = null;
        }
    }
}