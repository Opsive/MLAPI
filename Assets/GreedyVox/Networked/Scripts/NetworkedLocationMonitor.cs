using MLAPI;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;
using UnityEngine;

/// <summary>
/// Synchronizes the object's GameObject, Transform or Rigidbody values over the network.
/// </summary>
namespace GreedyVox.Networked {
    public class NetworkedLocationMonitor : NetworkBehaviour {
        public bool SynchronizeActiveState { get { return m_SynchronizeActiveState; } set { m_SynchronizeActiveState = value; } }
        public bool SynchronizePosition { get { return m_SynchronizePosition; } set { m_SynchronizePosition = value; } }
        public bool SynchronizeRotation { get { return m_SynchronizeRotation; } set { m_SynchronizeRotation = value; } }
        public bool SynchronizeScale { get { return m_SynchronizeScale; } set { m_SynchronizeScale = value; } }

        [Tooltip ("Should the GameObject's active state be syncornized?")]
        [SerializeField] protected bool m_SynchronizeActiveState = true;
        [Tooltip ("Should the transform's position be synchronized?")]
        [SerializeField] protected bool m_SynchronizePosition = true;
        [Tooltip ("Should the transform's rotation be synchronized?")]
        [SerializeField] protected bool m_SynchronizeRotation = true;
        [Tooltip ("Should the transform's scale be synchronized?")]
        [SerializeField] protected bool m_SynchronizeScale;
        private byte m_Flag;
        private ulong m_ServerID;
        private Rigidbody m_Rigidbody;
        private Transform m_Transform;
        private GameObject m_GameObject;
        private bool m_InitialSync = true;
        private Quaternion m_NetworkRotation;
        private NetworkedSettingsAbstract m_Settings;
        private float m_NetworkedTime, m_Angle, m_Distance = 0.0f;
        private string m_MsgClientTransform, m_MsgServerTransform;
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
        }
        /// <summary>
        /// The object has been deactivated.
        /// </summary>
        private void OnDisable () {
            m_Settings.NetworkSyncServerEvent -= OnNetworkSyncServerEvent;
            m_Settings.NetworkSyncClientEvent -= OnNetworkSyncClientEvent;
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
            m_ServerID = NetworkManager.Singleton.ServerClientId;
            m_MsgClientTransform = $"{NetworkBehaviourId}MsgClientLocationMonitor{OwnerClientId}";
            m_MsgServerTransform = $"{NetworkBehaviourId}MsgServerLocationMonitor{OwnerClientId}";

            if (IsServer) {
                m_Settings.NetworkSyncServerEvent += OnNetworkSyncServerEvent;
            } else if (IsOwner) {
                m_Settings.NetworkSyncClientEvent += OnNetworkSyncClientEvent;
            }

            if (!IsOwner) {
                if (m_Rigidbody == null) { m_Settings.NetworkSyncUpdateEvent += OnNetworkSyncUpdateEvent; } else {
                    m_Settings.NetworkSyncFixedUpdateEvent += OnNetworkSyncFixedUpdateEvent;
                }
                if (IsServer) {
                    CustomMessagingManager.RegisterNamedMessageHandler (m_MsgServerTransform, (sender, stream) => {
                        using (PooledNetworkReader reader = PooledNetworkReader.Get (stream)) {
                            Serialize (reader);
                        }
                    });
                } else {
                    CustomMessagingManager.RegisterNamedMessageHandler (m_MsgClientTransform, (sender, stream) => {
                        using (PooledNetworkReader reader = PooledNetworkReader.Get (stream)) {
                            Serialize (reader);
                        }
                    });
                }
            }

            if (m_SynchronizeActiveState && !NetworkedObjectPool.SpawnedWithPool (m_GameObject)) {
                if (IsServer) {
                    SetActiveClientRpc (m_GameObject.activeSelf);
                } else {
                    SetActiveServerRpc (m_GameObject.activeSelf);
                }
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
        /// Network sync event called from the component
        /// </summary>
        private void OnNetworkSyncClientEvent () {
            using (var stream = PooledNetworkBuffer.Get ()) {
                using (var writer = PooledNetworkWriter.Get (stream)) {
                    Serialize (writer);
                    CustomMessagingManager.SendNamedMessage (m_MsgServerTransform, m_ServerID, stream, NetworkChannel.ChannelUnused);
                }
            }
        }
        /// <summary>
        /// Network broadcast event called from the component
        /// </summary>
        private void OnNetworkSyncServerEvent () {
            using (var stream = PooledNetworkBuffer.Get ()) {
                using (var writer = PooledNetworkWriter.Get (stream)) {
                    if (IsOwner) { Serialize (writer); } else {
                        Serialize (writer, ref m_Flag);
                    }
                    CustomMessagingManager.SendNamedMessage (m_MsgClientTransform, null, stream, NetworkChannel.ChannelUnused);
                }
            }
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
            if (!float.IsNaN (m_NetworkPosition.x) && !float.IsNaN (m_NetworkPosition.y) && !float.IsNaN (m_NetworkPosition.z)) {
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
        }
        /// <summary>
        /// Called several times per second, so that your script can read synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being read from.</param>
        public void Serialize (PooledNetworkReader stream) {
            // Receive the GameObject and Transform values.
            // The position and rotation will then be used within the Update method to actually move the character.
            var flag = m_Flag;
            var pos = m_NetworkPosition;

            m_Flag = (byte) stream.ReadByte ();
            if (m_SynchronizePosition) {
                if ((m_Flag & (byte) TransformDirtyFlags.Position) != 0) {
                    m_NetworkPosition = stream.ReadVector3Packed ();

                    Debug.LogFormat ("<color=green>Flag: [<b>{0}</b>] Before: [{0}-{1}] & After: [{2}-{3}]</color>", flag, pos, m_Flag, m_NetworkPosition);

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
        public void Serialize (PooledNetworkWriter stream) {
            // Determine the dirty objects before sending the value.
            byte flag = 0;
            if (m_SynchronizePosition) {
                if (m_NetworkPosition != m_Transform.position) {
                    flag |= (byte) TransformDirtyFlags.Position;
                    m_NetworkPosition = m_Transform.position;
                }
                if (m_Rigidbody != null && m_NetworkRigidbodyVelocity != m_Rigidbody.velocity) {
                    flag |= (byte) TransformDirtyFlags.RigidbodyVelocity;
                    m_NetworkRigidbodyVelocity = m_Rigidbody.velocity;
                }
            }
            if (m_SynchronizeRotation) {
                if (m_NetworkRotation != m_Transform.rotation) {
                    flag |= (byte) TransformDirtyFlags.Rotation;
                    m_NetworkRotation = m_Transform.rotation;
                }
                if (m_Rigidbody != null && m_NetworkRigidbodyAngularVelocity != m_Rigidbody.angularVelocity) {
                    flag |= (byte) TransformDirtyFlags.RigidbodyAngularVelocity;
                    m_NetworkRigidbodyAngularVelocity = m_Rigidbody.angularVelocity;
                }
            }
            if (m_SynchronizeScale) {
                if (m_NetworkScale != m_Transform.localScale) {
                    flag |= (byte) TransformDirtyFlags.Scale;
                    m_NetworkScale = m_Transform.localScale;
                }
            }
            Serialize (stream, ref flag);
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public void Serialize (PooledNetworkWriter stream, ref byte flag) {
            // Send the current GameObject and Transform values to all remote players.        
            if (flag != 0) {
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
}