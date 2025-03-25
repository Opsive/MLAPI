using GreedyVox.NetCode.Game;
using Opsive.Shared.Game;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode
{
    public class NetCodeRemover : NetworkBehaviour
    {
        [Tooltip("The number of seconds until the object should be placed back in the pool.")]
        [SerializeField] protected float m_Lifetime = 5;
        private GameObject m_GameObject;
        private NetworkObject m_NetCodeObject;
        private ScheduledEventBase m_RemoveEvent;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_NetCodeObject = GetComponent<NetworkObject>();
        }
        /// <summary>
        /// Schedule the object for removal.
        /// </summary>
        private void OnEnable() => m_RemoveEvent = Scheduler.Schedule(m_Lifetime, Remove);
        /// <summary>
        /// The object has been destroyed - no need for removal if it hasn't already been removed.
        /// </summary>
        private void OnDisable() => CancelRemoveEvent();
        /// <summary>
        /// Cancels the remove event.
        /// </summary>
        public void CancelRemoveEvent()
        {
            if (m_RemoveEvent != null)
            {
                Scheduler.Cancel(m_RemoveEvent);
                m_RemoveEvent = null;
            }
        }
        /// <summary>
        /// Remove the object.
        /// </summary>
        private void Remove()
        {
            if (m_NetCodeObject == null)
                ObjectPool.Destroy(m_GameObject);
            else if (IsServer)
                NetCodeObjectPool.Destroy(m_GameObject);
            m_RemoveEvent = null;
        }
    }
}