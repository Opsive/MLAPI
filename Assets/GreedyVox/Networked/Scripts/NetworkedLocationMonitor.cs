using System.Collections.Generic;
using MLAPI;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Game;
using UnityEngine;

/// <summary>
/// Synchronizes the object's GameObject, Transform or Rigidbody values over the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    [RequireComponent (typeof (NetworkedSyncRate))]
    public class NetworkedLocationMonitor : NetworkBehaviour {
        [Tooltip ("Should the GameObject's active state be syncornized?")]
        [SerializeField] protected bool m_SynchronizeActiveState = true;
        [Tooltip ("Should the transform's position be synchronized?")]
        [SerializeField] protected bool m_SynchronizePosition = true;
        [Tooltip ("Should the transform's rotation be synchronized?")]
        [SerializeField] protected bool m_SynchronizeRotation = true;
        [Tooltip ("Should the transform's scale be synchronized?")]
        [SerializeField] protected bool m_SynchronizeScale;
        private byte m_Flag;
        private Rigidbody m_Rigidbody;
        private Transform m_Transform;
        private string m_MsgTransform;
        private GameObject m_GameObject;
        private bool m_InitialSync = true;
        private Quaternion m_NetworkRotation;
        private NetworkedSyncRate m_NetworkSync;
        private NetworkedSettingsAbstract m_Settings;
        private float m_NetworkedTime, m_Angle, m_Distance = 0.0f;
        private Vector3 m_NetworkRigidbodyAngularVelocity, m_NetworkRigidbodyVelocity, m_NetworkPosition, m_NetworkScale;
        /// <summary>
        /// Specifies which transform objects are dirty.
        /// </summary>
        private enum TransformDirtyFlags : byte {
            Position = 1, // The position has changed.
            RigidbodyVelocity = 2, // The Rigidbody velocity has changed.
            Rotation = 4, // The rotation has changed.
            RigidbodyAngularVelocity = 8, // The Rigidbody angular velocity has changed.
            Scale = 16 // The scale has changed.
        }
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake () {
            m_Transform = transform;
            m_GameObject = gameObject;
            m_Rigidbody = GetComponent<Rigidbody> ();
            m_NetworkPosition = m_Transform.position;
            m_NetworkRotation = m_Transform.rotation;
            m_Settings = NetworkedManager.Instance.NetworkSettings;
            m_NetworkSync = gameObject.GetCachedComponent<NetworkedSyncRate> ();
        }
        /// <summary>
        /// The object has been deactivated.
        /// </summary>
        private void OnDisable () {
            m_NetworkSync.NetworkSyncEvent -= OnNetworkSyncEvent;
            m_Settings.NetworkSyncUpdateEvent -= OnNetworkSyncUpdateEvent;
            m_Settings.NetworkSyncFixedUpdateEvent -= OnNetworkSyncFixedUpdateEvent;
        }
        /// <summary>
        /// The object has been enabled.
        /// </summary>
        private void OnEnable () {
            m_InitialSync = true;
            // If the object is pooled then the network object pool will manage the active state.
            if (m_SynchronizeActiveState && NetworkedObjectPool.SpawnedWithPool (m_GameObject)) {
                m_SynchronizeActiveState = false;
            }
            if (m_SynchronizeActiveState && IsOwner) {
                if (IsServer) {
                    SetActiveClientRpc (m_GameObject.activeSelf);
                } else {
                    SetActiveServerRpc (m_GameObject.activeSelf);
                }
            }
        }
        /// <summary>
        /// A player has entered the world. Ensure the joining player is in sync with the current game state.
        /// </summary>
        /// <param name="player">The Player that entered the world.</param>
        /// <param name="character">The character that the player controls.</param>
        public override void NetworkStart () {
            m_NetworkSync.NetworkSyncEvent += OnNetworkSyncEvent;
            m_MsgTransform = $"{NetworkObjectId}MsgClientLocationMonitor{OwnerClientId}";

            if (!IsServer) {
                if (m_Rigidbody == null) { m_Settings.NetworkSyncUpdateEvent += OnNetworkSyncUpdateEvent; } else {
                    m_Settings.NetworkSyncFixedUpdateEvent += OnNetworkSyncFixedUpdateEvent;
                }
                CustomMessagingManager.RegisterNamedMessageHandler (m_MsgTransform, (sender, stream) => {
                    using (var reader = PooledNetworkReader.Get (stream)) {
                        Serialize (reader);
                    }
                });
            }
            if (m_SynchronizeActiveState && !NetworkObjectPool.SpawnedWithPool (m_GameObject)) {
                if (IsServer) { SetActiveClientRpc (m_GameObject.activeSelf); } else {
                    SetActiveServerRpc (m_GameObject.activeSelf);
                }
            }
        }
        /// <summary>
        /// Network broadcast event called from the NetworkedSyncRate component
        /// </summary>
        private void OnNetworkSyncEvent (List<ulong> clients) {
            using (var stream = PooledNetworkBuffer.Get ())
            using (var writer = PooledNetworkWriter.Get (stream)) {
                Serialize (writer);
                CustomMessagingManager.SendNamedMessage (m_MsgTransform, clients, stream, NetworkChannel.ChannelUnused);
            }
        }

        /// <summary>
        /// Activates or deactivates the GameObject on the network.
        /// </summary>
        /// <param name="active">Should the GameObject be activated?</param>
        [ServerRpc]
        private void SetActiveServerRpc (bool active) {
            SetActiveClientRpc (active);
        }

        [ClientRpc]
        private void SetActiveClientRpc (bool active) {
            m_GameObject?.SetActive (active);
        }
        /// <summary>
        /// Updates the remote object's transform values.
        /// </summary>
        private void OnNetworkSyncUpdateEvent () {
            Synchronize ();
        }
        /// <summary>
        /// Fixed updates the remote object's transform values.
        /// </summary>
        private void OnNetworkSyncFixedUpdateEvent () {
            Synchronize ();
        }
        /// <summary>
        /// Synchronizes the transform.
        /// </summary>
        private void Synchronize () {
            // The position and rotation should be applied immediately if it is the first sync.
            if (m_InitialSync) {
                if (m_SynchronizePosition) { m_Transform.position = m_NetworkPosition; }
                if (m_SynchronizeRotation) { m_Transform.rotation = m_NetworkRotation; }
                m_InitialSync = false;
            } else {
                if (m_SynchronizePosition) {
                    m_Transform.position = Vector3.MoveTowards (transform.position, m_NetworkPosition, m_Distance * (1.0f / m_Settings.SyncRateClient));
                }
                if (m_SynchronizeRotation) {
                    m_Transform.rotation = Quaternion.RotateTowards (transform.rotation, m_NetworkRotation, m_Angle * (1.0f / m_Settings.SyncRateClient));
                }
            }
        }
        /// <summary>
        /// Called several times per second, so that your script can read synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being read from.</param>
        public void Serialize (PooledNetworkReader stream) {
            // Receive the GameObject and Transform values.
            // The position and rotation will then be used within the Update method to actually move the character.
            m_Flag = (byte) stream.ReadByte ();
            if (m_SynchronizePosition) {
                if ((m_Flag & (byte) TransformDirtyFlags.Position) != 0) {
                    m_NetworkPosition = stream.ReadVector3Packed ();
                    var velocity = stream.ReadVector3Packed ();
                    if (!m_InitialSync) {
                        // Compensate for the lag.
                        var lag = Mathf.Abs (NetworkManager.Singleton.NetworkTime - m_NetworkedTime);
                        m_NetworkPosition += velocity * lag;
                    }
                    m_Distance = Vector3.Distance (m_Transform.position, m_NetworkPosition);
                }
                if ((m_Flag & (byte) TransformDirtyFlags.RigidbodyVelocity) != 0 && m_Rigidbody != null) {
                    m_Rigidbody.velocity = stream.ReadVector3Packed ();
                }
            }
            if (m_SynchronizeRotation) {
                if ((m_Flag & (byte) TransformDirtyFlags.Rotation) != 0) {
                    m_NetworkRotation = Quaternion.Euler (stream.ReadVector3Packed ());
                    m_Angle = Quaternion.Angle (m_Transform.rotation, m_NetworkRotation);
                }
                if ((m_Flag & (byte) TransformDirtyFlags.RigidbodyAngularVelocity) != 0 && m_Rigidbody != null) {
                    m_Rigidbody.angularVelocity = stream.ReadVector3Packed ();
                }
            }
            if (m_SynchronizeScale) {
                if ((m_Flag & (byte) TransformDirtyFlags.Scale) != 0) {
                    m_Transform.localScale = stream.ReadVector3Packed ();
                }
            }
            m_NetworkedTime = NetworkManager.Singleton.NetworkTime;
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public bool Serialize (PooledNetworkWriter stream) {
            // Determine the dirty objects before sending the value.
            m_Flag = 0;
            if (m_SynchronizePosition) {
                if (m_NetworkPosition != m_Transform.position) {
                    m_Flag |= (byte) TransformDirtyFlags.Position;
                    m_NetworkPosition = m_Transform.position;
                }
                if (m_Rigidbody != null && m_NetworkRigidbodyVelocity != m_Rigidbody.velocity) {
                    m_Flag |= (byte) TransformDirtyFlags.RigidbodyVelocity;
                    m_NetworkRigidbodyVelocity = m_Rigidbody.velocity;
                }
            }
            if (m_SynchronizeRotation) {
                if (m_NetworkRotation != m_Transform.rotation) {
                    m_Flag |= (byte) TransformDirtyFlags.Rotation;
                    m_NetworkRotation = m_Transform.rotation;
                }
                if (m_Rigidbody != null && m_NetworkRigidbodyAngularVelocity != m_Rigidbody.angularVelocity) {
                    m_Flag |= (byte) TransformDirtyFlags.RigidbodyAngularVelocity;
                    m_NetworkRigidbodyAngularVelocity = m_Rigidbody.angularVelocity;
                }
            }
            if (m_SynchronizeScale) {
                if (m_NetworkScale != m_Transform.localScale) {
                    m_Flag |= (byte) TransformDirtyFlags.Scale;
                    m_NetworkScale = m_Transform.localScale;
                }
            }
            if (m_Flag != 0) {
                Serialize (stream, ref m_Flag);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public void Serialize (PooledNetworkWriter stream, ref byte flag) {
            // Send the current GameObject and Transform values to all remote players.        
            stream.WriteByte (flag);
            if (m_SynchronizePosition) {
                if ((flag & (byte) TransformDirtyFlags.Position) != 0) {
                    stream.WriteVector3Packed (m_Transform.position);
                    stream.WriteVector3Packed (m_Transform.position - m_NetworkPosition);
                    m_NetworkPosition = m_Transform.position;
                }
                if ((flag & (byte) TransformDirtyFlags.RigidbodyVelocity) != 0 && m_Rigidbody != null) {
                    stream.WriteVector3Packed (m_Rigidbody.velocity);
                }
            }
            if (m_SynchronizeRotation) {
                if ((flag & (byte) TransformDirtyFlags.Rotation) != 0) {
                    stream.WriteVector3Packed (m_Transform.eulerAngles);
                }
                if ((flag & (byte) TransformDirtyFlags.RigidbodyAngularVelocity) != 0 && m_Rigidbody != null) {
                    stream.WriteVector3Packed (m_Rigidbody.angularVelocity);
                }
            }
            if (m_SynchronizeScale) {
                if ((flag & (byte) TransformDirtyFlags.Scale) != 0) {
                    stream.WriteVector3Packed (m_Transform.localScale);
                }
            }
        }
    }
}