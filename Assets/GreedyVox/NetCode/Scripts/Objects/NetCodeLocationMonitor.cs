using System.Collections.Generic;
using GreedyVox.NetCode.Game;
using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Game;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes the object's GameObject, Transform or Rigidbody values over the network.
/// </summary>
namespace GreedyVox.NetCode.Objects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetCodeSyncRate))]
    public class NetCodeLocationMonitor : NetworkBehaviour
    {
        [Tooltip("Should the GameObject's active state be syncornized?")]
        [SerializeField] protected bool m_SynchronizeActiveState = true;
        [Tooltip("Should the transform's position be synchronized?")]
        [SerializeField] protected bool m_SynchronizePosition = true;
        [Tooltip("Should the transform's rotation be synchronized?")]
        [SerializeField] protected bool m_SynchronizeRotation = true;
        [Tooltip("Should the transform's scale be synchronized?")]
        [SerializeField] protected bool m_SynchronizeScale;
        private byte m_Flag;
        private ulong m_ServerID;
        private int m_MaxBufferSize;
        private Rigidbody m_Rigidbody;
        private Transform m_Transform;
        private GameObject m_GameObject;
        private bool m_InitialSync = true;
        private Quaternion m_NetworkRotation;
        private NetCodeSyncRate m_NetworkSync;
        private FastBufferWriter m_FastBufferWriter;
        private string m_MsgNameClient, m_MsgNameServer;
        private NetCodeSettingsAbstract m_NetworkSettings;
        private CustomMessagingManager m_CustomMessagingManager;
        private float m_NetCodeTime, m_Angle, m_Distance = 0.0f;
        private Vector3 m_NetworkRigidbodyAngularVelocity, m_NetworkRigidbodyVelocity, m_NetworkPosition, m_NetworkScale;
        /// <summary>
        /// Specifies which transform objects are dirty.
        /// </summary>
        private enum TransformDirtyFlags : byte
        {
            Position = 1, // The position has changed.
            RigidbodyVelocity = 2, // The Rigidbody velocity has changed.
            Rotation = 4, // The rotation has changed.
            RigidbodyAngularVelocity = 8, // The Rigidbody angular velocity has changed.
            Scale = 16 // The scale has changed.
        }
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_GameObject = gameObject;
            m_MaxBufferSize = MaxBufferSize();
            m_Rigidbody = GetComponent<Rigidbody>();
            m_NetworkPosition = m_Transform.position;
            m_NetworkRotation = m_Transform.rotation;
            m_NetworkSettings = NetCodeManager.Instance.NetworkSettings;
            if (m_NetworkSettings == null)
                enabled = false;
            m_NetworkSync = gameObject.GetCachedComponent<NetCodeSyncRate>();
        }
        /// <summary>
        /// Removing events.
        /// </summary>
        private void OnDisable() =>
        EventHandler.UnregisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
        /// <summary>
        /// The object has been enabled.
        /// </summary>
        private void OnEnable()
        {
            m_InitialSync = true;
            // If the object is pooled then the network object pool will manage the active state.
            if (m_SynchronizeActiveState && NetCodeObjectPool.SpawnedWithPool(m_GameObject))
                m_SynchronizeActiveState = false;
            EventHandler.RegisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
        }
        /// <summary>
        /// The object has been despawned.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            m_NetworkSync.NetworkSyncEvent -= OnNetworkSyncServerEvent;
            m_NetworkSettings.NetworkSyncClientEvent -= OnNetworkSyncClientEvent;
            m_NetworkSettings.NetworkSyncUpdateEvent -= OnNetworkSyncUpdateEvent;
            m_NetworkSettings.NetworkSyncFixedUpdateEvent -= OnNetworkSyncFixedUpdateEvent;
            m_CustomMessagingManager?.UnregisterNamedMessageHandler(m_MsgNameClient);
            m_CustomMessagingManager?.UnregisterNamedMessageHandler(m_MsgNameServer);
        }
        /// <summary>
        /// The object has been spawned.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            m_ServerID = NetworkManager.ServerClientId;
            m_MsgNameClient = $"{OwnerClientId}MsgClientLocationMonitor{NetworkObjectId}";
            m_MsgNameServer = $"{OwnerClientId}MsgServerLocationMonitor{NetworkObjectId}";
            m_CustomMessagingManager = NetworkManager.Singleton.CustomMessagingManager;

            if (IsServer)
                m_NetworkSync.NetworkSyncEvent += OnNetworkSyncServerEvent;
            else
                m_NetworkSettings.NetworkSyncClientEvent += OnNetworkSyncClientEvent;

            if (!IsOwner)
            {
                if (m_Rigidbody == null)
                    m_NetworkSettings.NetworkSyncUpdateEvent += OnNetworkSyncUpdateEvent;
                else
                    m_NetworkSettings.NetworkSyncFixedUpdateEvent += OnNetworkSyncFixedUpdateEvent;
            }
            m_CustomMessagingManager?.RegisterNamedMessageHandler(IsServer ? m_MsgNameServer : m_MsgNameClient, (sender, reader) =>
            { Serialize(ref reader); });
            if (m_SynchronizeActiveState && !NetworkObjectPool.SpawnedWithPool(m_GameObject))
                SetActiveRpc(m_GameObject.activeSelf);
        }
        /// <summary>
        /// A event from Photon has been sent.
        /// </summary>
        /// <param name="id">The Client networking id that connected.</param>
        /// <param name="obj">The Player NetworkObject that connected.</param>
        private void OnPlayerConnected(ulong id, NetworkObjectReference obj)
        {
            if (IsServer)
                SetPositionRotationRpc(m_Transform.rotation, m_Transform.position);
        }
        [Rpc(SendTo.NotMe)]
        private void SetPositionRotationRpc(Quaternion rotation, Vector3 position)
        {
            m_Transform.rotation = rotation;
            m_Transform.position = position;
        }
        /// <summary>
        /// Network broadcast event called from the NetCodeSyncRate component
        /// </summary>
        private void OnNetworkSyncClientEvent()
        {
            // Error handling if this function still executing after despawning event
            if (IsClient)
                using (m_FastBufferWriter = new FastBufferWriter(FastBufferWriter.GetWriteSize(m_Flag), Allocator.Temp, m_MaxBufferSize))
                    if (Serialize())
                        m_CustomMessagingManager?.SendNamedMessage(m_MsgNameServer, m_ServerID, m_FastBufferWriter, NetworkDelivery.UnreliableSequenced);
        }
        /// <summary>
        /// Network broadcast event called from the NetworkInfo component
        /// </summary>
        private void OnNetworkSyncServerEvent(List<ulong> clients)
        {
            // Error handling if this function still executing after despawning event
            if (IsServer)
            {
                using (m_FastBufferWriter = new FastBufferWriter(FastBufferWriter.GetWriteSize(m_Flag), Allocator.Temp, m_MaxBufferSize))
                {
                    if (Serialize())
                        m_CustomMessagingManager?.SendNamedMessage(m_MsgNameClient, clients, m_FastBufferWriter, NetworkDelivery.UnreliableSequenced);
                }
            }
        }
        /// <summary>
        /// Activates or deactivates the GameObject on the network.
        /// </summary>
        /// <param name="active">Should the GameObject be activated?</param>
        [Rpc(SendTo.NotMe)]
        private void SetActiveRpc(bool active) => m_GameObject?.SetActive(active);
        /// <summary>
        /// Updates the remote object's transform values.
        /// </summary>
        private void OnNetworkSyncUpdateEvent() => Synchronize();
        /// <summary>
        /// Fixed updates the remote object's transform values.
        /// </summary>
        private void OnNetworkSyncFixedUpdateEvent() => Synchronize();
        /// <summary>
        /// Returns the maximus size for the fast buffer writer
        /// </summary>               
        private int MaxBufferSize() => sizeof(byte) + sizeof(float) * 3 * 6;
        /// <summary>
        /// Synchronizes the transform.
        /// </summary>
        private void Synchronize()
        {
            // The position and rotation should be applied immediately if it is the first sync.
            if (m_InitialSync)
            {
                if (m_SynchronizePosition) { m_Transform.position = m_NetworkPosition; }
                if (m_SynchronizeRotation) { m_Transform.rotation = m_NetworkRotation; }
                m_InitialSync = false;
            }
            else
            {
                if (m_SynchronizePosition)
                    m_Transform.position = Vector3.MoveTowards(transform.position, m_NetworkPosition, m_Distance * (1.0f / m_NetworkSettings.SyncRateClient));
                if (m_SynchronizeRotation)
                    m_Transform.rotation = Quaternion.RotateTowards(transform.rotation, m_NetworkRotation, m_Angle * (1.0f / m_NetworkSettings.SyncRateClient));
            }
        }
        /// <summary>
        /// Called several times per second, so that your script can read synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being read from.</param>
        public void Serialize(ref FastBufferReader reader)
        {
            // Receive the GameObject and Transform values.
            // The position and rotation will then be used within the Update method to actually move the character.
            ByteUnpacker.ReadValuePacked(reader, out m_Flag);
            if (m_SynchronizePosition)
            {
                if ((m_Flag & (byte)TransformDirtyFlags.Position) != 0)
                {
                    ByteUnpacker.ReadValuePacked(reader, out m_NetworkPosition);
                    ByteUnpacker.ReadValuePacked(reader, out Vector3 position);
                    if (!m_InitialSync)
                    {
                        // Compensate for the lag.
                        var lag = Mathf.Abs(NetworkManager.Singleton.LocalTime.TimeAsFloat - m_NetCodeTime);
                        m_NetworkPosition += position * lag;
                    }
                    m_Distance = Vector3.Distance(m_Transform.position, m_NetworkPosition);
                }
                if ((m_Flag & (byte)TransformDirtyFlags.RigidbodyVelocity) != 0 && m_Rigidbody != null)
                {
                    ByteUnpacker.ReadValuePacked(reader, out Vector3 velocity);
                    m_Rigidbody.velocity = velocity;
                }
            }
            if (m_SynchronizeRotation)
            {
                if ((m_Flag & (byte)TransformDirtyFlags.Rotation) != 0)
                {
                    ByteUnpacker.ReadValuePacked(reader, out Vector3 angle);
                    m_NetworkRotation = Quaternion.Euler(angle);
                    m_Angle = Quaternion.Angle(m_Transform.rotation, m_NetworkRotation);
                }
                if ((m_Flag & (byte)TransformDirtyFlags.RigidbodyAngularVelocity) != 0 && m_Rigidbody != null)
                {
                    ByteUnpacker.ReadValuePacked(reader, out Vector3 angle);
                    m_Rigidbody.angularVelocity = angle;
                }
            }
            if (m_SynchronizeScale)
            {
                if ((m_Flag & (byte)TransformDirtyFlags.Scale) != 0)
                {
                    ByteUnpacker.ReadValuePacked(reader, out Vector3 scale);
                    m_Transform.localScale = scale;
                }
            }
            m_NetCodeTime = NetworkManager.Singleton.LocalTime.TimeAsFloat;
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public bool Serialize()
        {
            // Determine the dirty objects before sending the value.
            m_Flag = 0;
            if (m_SynchronizePosition)
            {
                if (m_NetworkPosition != m_Transform.position)
                {
                    m_Flag |= (byte)TransformDirtyFlags.Position;
                    m_NetworkPosition = m_Transform.position;
                }
                if (m_Rigidbody != null && m_NetworkRigidbodyVelocity != m_Rigidbody.velocity)
                {
                    m_Flag |= (byte)TransformDirtyFlags.RigidbodyVelocity;
                    m_NetworkRigidbodyVelocity = m_Rigidbody.velocity;
                }
            }
            if (m_SynchronizeRotation)
            {
                if (m_NetworkRotation != m_Transform.rotation)
                {
                    m_Flag |= (byte)TransformDirtyFlags.Rotation;
                    m_NetworkRotation = m_Transform.rotation;
                }
                if (m_Rigidbody != null && m_NetworkRigidbodyAngularVelocity != m_Rigidbody.angularVelocity)
                {
                    m_Flag |= (byte)TransformDirtyFlags.RigidbodyAngularVelocity;
                    m_NetworkRigidbodyAngularVelocity = m_Rigidbody.angularVelocity;
                }
            }
            if (m_SynchronizeScale)
            {
                if (m_NetworkScale != m_Transform.localScale)
                {
                    m_Flag |= (byte)TransformDirtyFlags.Scale;
                    m_NetworkScale = m_Transform.localScale;
                }
            }
            if (m_Flag != 0)
            {
                Serialize(ref m_Flag);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public void Serialize(ref byte flag)
        {
            // Send the current GameObject and Transform values to all remote players.        
            BytePacker.WriteValuePacked(m_FastBufferWriter, flag);
            if (m_SynchronizePosition)
            {
                if ((flag & (byte)TransformDirtyFlags.Position) != 0)
                {
                    BytePacker.WriteValuePacked(m_FastBufferWriter, m_Transform.position);
                    BytePacker.WriteValuePacked(m_FastBufferWriter, m_Transform.position - m_NetworkPosition);
                    m_NetworkPosition = m_Transform.position;
                }
                if ((flag & (byte)TransformDirtyFlags.RigidbodyVelocity) != 0 && m_Rigidbody != null)
                    BytePacker.WriteValuePacked(m_FastBufferWriter, m_Rigidbody.velocity);
            }
            if (m_SynchronizeRotation)
            {
                if ((flag & (byte)TransformDirtyFlags.Rotation) != 0)
                    BytePacker.WriteValuePacked(m_FastBufferWriter, m_Transform.eulerAngles);
                if ((flag & (byte)TransformDirtyFlags.RigidbodyAngularVelocity) != 0 && m_Rigidbody != null)
                    BytePacker.WriteValuePacked(m_FastBufferWriter, m_Rigidbody.angularVelocity);
            }
            if (m_SynchronizeScale)
                if ((flag & (byte)TransformDirtyFlags.Scale) != 0)
                    BytePacker.WriteValuePacked(m_FastBufferWriter, m_Transform.localScale);
        }
    }
}