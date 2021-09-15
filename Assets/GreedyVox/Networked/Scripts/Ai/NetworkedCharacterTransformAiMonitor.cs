using System.Collections.Generic;
using MLAPI;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using MLAPI.Spawning;
using MLAPI.Transports;
using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Utility;
using UnityEngine;

/// <summary>
/// Synchronizes the character's transform values over the network.
/// </summary>
namespace GreedyVox.Networked.Ai {
    [DisallowMultipleComponent]
    [RequireComponent (typeof (NetworkedSyncRate))]
    public class NetworkedCharacterTransformAiMonitor : NetworkBehaviour {
        /// <summary>
        /// Specifies which transform objects are dirty.
        /// </summary>
        private enum TransformDirtyFlags : byte {
            Position = 1, // The position has changed.
            Rotation = 2, // The rotation has changed.
            Platform = 4, // The platform has changed.
            Scale = 8 // The scale has changed.
        }

        [Tooltip ("Should the transform's scale be synchronized?")]
        [SerializeField] protected bool m_SynchronizeScale;
        [Tooltip ("A multiplier to apply to the interpolation destination for remote players.")]
        [SerializeField] protected float m_RemoteInterpolationMultiplayer = 1.2f;
        private byte m_Flag;
        private ulong m_PlatformID;
        private bool m_InitialSync = true;
        private NetworkObject m_Platform;
        private string m_MsgClientTransform;
        private NetworkedSyncRate m_NetworkSync;
        private NetworkedManager m_NetworkManager;
        private Transform m_NetworkPlatform, m_Transform;
        private float m_NetworkedTime, m_Distance, m_Angle;
        private UltimateCharacterLocomotion m_CharacterLocomotion;
        private Quaternion m_NetworkPlatformPrevRotationOffset, m_NetworkPlatformRotationOffset, m_NetworkRotation;
        private Vector3 m_NetworkPlatformRelativePosition, m_NetworkPlatformPrevRelativePosition, m_NetworkPosition, m_NetworkScale;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake () {
            m_Transform = transform;
            m_NetworkScale = m_Transform.localScale;
            m_NetworkPosition = m_Transform.position;
            m_NetworkRotation = m_Transform.rotation;
            m_NetworkManager = NetworkedManager.Instance;
            m_NetworkSync = gameObject.GetCachedComponent<NetworkedSyncRate> ();
            m_CharacterLocomotion = gameObject.GetCachedComponent<UltimateCharacterLocomotion> ();

            EventHandler.RegisterEvent (gameObject, "OnRespawn", OnRespawn);
            EventHandler.RegisterEvent<bool> (gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);
        }
        private void OnDisable () {
            m_NetworkSync.NetworkSyncEvent -= OnNetworkSyncEvent;
            m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent -= OnNetworkSyncUpdateEvent;
        }
        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        private void OnDestroy () {
            CustomMessagingManager.UnregisterNamedMessageHandler (m_MsgClientTransform);
            EventHandler.UnregisterEvent (gameObject, "OnRespawn", OnRespawn);
            EventHandler.UnregisterEvent<bool> (gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);
        }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup. Provides a Payload if it was provided
        /// </summary>
        public override void NetworkStart () {
            m_NetworkSync.NetworkSyncEvent += OnNetworkSyncEvent;
            m_MsgClientTransform = $"{NetworkObjectId}MsgClientTransformAi{OwnerClientId}";

            if (!IsServer) {
                if (m_NetworkManager != null) { m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent += OnNetworkSyncUpdateEvent; }
                CustomMessagingManager.RegisterNamedMessageHandler (m_MsgClientTransform, (sender, stream) => {
                    using (var reader = PooledNetworkReader.Get (stream)) {
                        Serialize (reader);
                    }
                });
            }
        }
        /// <summary>
        /// Network broadcast event called from the NetworkedSyncRate component
        /// </summary>
        private void OnNetworkSyncEvent (List<ulong> clients) {
            using (var stream = PooledNetworkBuffer.Get ())
            using (var writer = PooledNetworkWriter.Get (stream)) {
                if (Serialize (writer)) {
                    CustomMessagingManager.SendNamedMessage (m_MsgClientTransform, clients, stream, NetworkChannel.ChannelUnused);
                }
            }
        }
        /// <summary>
        /// Updates the remote character's transform values.
        /// </summary>
        private void OnNetworkSyncUpdateEvent () {
            // When the character is on a moving platform the position and rotation is relative to that platform.
            // This allows the character to stay on the platform even though the platform will not be in the exact same location between any two instances.
            var serializationRate = m_NetworkManager.NetworkSettings.SyncRateClient * m_RemoteInterpolationMultiplayer;
            if (m_NetworkPlatform != null) {
                m_NetworkPlatformPrevRelativePosition = Vector3.MoveTowards (m_NetworkPlatformPrevRelativePosition,
                    m_NetworkPlatformRelativePosition, m_Distance * serializationRate);
                m_CharacterLocomotion.SetPosition (m_NetworkPlatform.TransformPoint (m_NetworkPlatformPrevRelativePosition), false);
                m_NetworkPlatformPrevRotationOffset = Quaternion.RotateTowards (m_NetworkPlatformPrevRotationOffset,
                    m_NetworkPlatformRotationOffset, m_Angle * serializationRate);
                m_CharacterLocomotion.SetRotation (MathUtility.TransformQuaternion (m_NetworkPlatform.rotation, m_NetworkPlatformPrevRotationOffset), false);
            } else if (m_Transform != null) {
                m_Transform.position = Vector3.MoveTowards (m_Transform.position, m_NetworkPosition, m_Distance * serializationRate);
                m_Transform.rotation = Quaternion.RotateTowards (m_Transform.rotation, m_NetworkRotation, m_Angle * serializationRate);
            }
        }
        /// <summary>
        /// Called several times per second, so that your script can read synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being read from.</param>
        public void Serialize (PooledNetworkReader stream) {
            m_Flag = (byte) stream.ReadByte ();
            if ((m_Flag & (byte) TransformDirtyFlags.Platform) != 0) {
                var platformID = stream.ReadUInt64Packed ();
                // When the character is on a platform the position and rotation is relative to that platform.
                if ((m_Flag & (byte) TransformDirtyFlags.Position) != 0)
                    m_NetworkPlatformRelativePosition = stream.ReadVector3Packed ();
                if ((m_Flag & (byte) TransformDirtyFlags.Rotation) != 0)
                    m_NetworkPlatformRotationOffset = Quaternion.Euler (stream.ReadVector3Packed ());
                // Do not do any sort of interpolation when the platform has changed.
                if (platformID != m_PlatformID && NetworkSpawnManager.SpawnedObjects.TryGetValue (platformID, out m_Platform)) {
                    m_NetworkPlatform = m_Platform.transform;
                    m_NetworkPlatformRelativePosition = m_NetworkPlatformPrevRelativePosition =
                        m_NetworkPlatform.InverseTransformPoint (m_Transform.position);
                    m_NetworkPlatformRotationOffset = m_NetworkPlatformPrevRotationOffset =
                        MathUtility.InverseTransformQuaternion (m_NetworkPlatform.rotation, m_Transform.rotation);
                }
                m_Distance = Vector3.Distance (m_NetworkPlatformPrevRelativePosition, m_NetworkPlatformRelativePosition);
                m_Angle = Quaternion.Angle (m_NetworkPlatformPrevRotationOffset, m_NetworkPlatformRotationOffset);
                m_PlatformID = platformID;
            } else {
                if ((m_Flag & (byte) TransformDirtyFlags.Position) != 0) {
                    m_NetworkPosition = stream.ReadVector3Packed ();
                    var velocity = stream.ReadVector3Packed ();
                    // Account for the lag.
                    if (!m_InitialSync) {
                        var lag = Mathf.Abs (NetworkManager.Singleton.NetworkTime - m_NetworkedTime);
                        m_NetworkPosition += velocity * lag;
                    };
                    m_InitialSync = false;
                }
                if ((m_Flag & (byte) TransformDirtyFlags.Rotation) != 0)
                    m_NetworkRotation = Quaternion.Euler (stream.ReadVector3Packed ());
                m_Distance = Vector3.Distance (m_Transform.position, m_NetworkPosition);
                m_Angle = Quaternion.Angle (m_Transform.rotation, m_NetworkRotation);
            }
            if ((m_Flag & (byte) TransformDirtyFlags.Scale) != 0)
                m_Transform.localScale = stream.ReadVector3Packed ();
            m_NetworkedTime = NetworkManager.Singleton.NetworkTime;
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public bool Serialize (PooledNetworkWriter stream) {
            m_Flag = 0;
            if (m_SynchronizeScale && m_Transform != null && m_Transform.localScale != m_NetworkScale)
                m_Flag |= (byte) TransformDirtyFlags.Scale;
            // When the character is on a platform the position and rotation is relative to that platform.
            if (m_CharacterLocomotion.Platform != null) {
                var platform = m_CharacterLocomotion.Platform.gameObject.GetCachedComponent<NetworkObject> ();
                if (platform == null) {
                    Debug.LogError ("Error: The platform " + m_CharacterLocomotion.Platform + " must have a NetworkedObject.");
                } else {
                    // Determine the changed objects before sending them.
                    m_Flag |= (byte) TransformDirtyFlags.Platform;
                    var position = m_CharacterLocomotion.Platform.InverseTransformPoint (m_Transform.position);
                    var rotation = MathUtility.InverseTransformQuaternion (m_CharacterLocomotion.Platform.rotation, m_Transform.rotation);
                    if (position != m_NetworkPosition) {
                        m_Flag |= (byte) TransformDirtyFlags.Position;
                        m_NetworkPosition = position;
                    }
                    if (rotation != m_NetworkRotation) {
                        m_Flag |= (byte) TransformDirtyFlags.Rotation;
                        m_NetworkRotation = rotation;
                    }
                    // Write m_Flag as dirty
                    stream.WriteByte (m_Flag);
                    stream.WriteUInt64Packed (platform.OwnerClientId);
                    if ((m_Flag & (byte) TransformDirtyFlags.Position) != 0)
                        stream.WriteVector3Packed (position);
                    if ((m_Flag & (byte) TransformDirtyFlags.Rotation) != 0)
                        stream.WriteVector3Packed (rotation.eulerAngles);
                }
            } else if (m_Transform != null) {
                // Determine the changed objects before sending them.
                if (m_Transform.position != m_NetworkPosition)
                    m_Flag |= (byte) TransformDirtyFlags.Position;

                if (m_Transform.rotation != m_NetworkRotation)
                    m_Flag |= (byte) TransformDirtyFlags.Rotation;
                // Write m_Flag as dirty
                stream.WriteByte (m_Flag);
                if ((m_Flag & (byte) TransformDirtyFlags.Position) != 0) {
                    stream.WriteVector3Packed (m_Transform.position);
                    stream.WriteVector3Packed (m_Transform.position - m_NetworkPosition);
                    m_NetworkPosition = m_Transform.position;
                }
                if ((m_Flag & (byte) TransformDirtyFlags.Rotation) != 0) {
                    stream.WriteVector3Packed (m_Transform.eulerAngles);
                    m_NetworkRotation = m_Transform.rotation;
                }
            }
            if ((m_Flag & (byte) TransformDirtyFlags.Scale) != 0) {
                stream.WriteVector3Packed (m_Transform.localScale);
                m_NetworkScale = m_Transform.localScale;
            }
            return m_Flag > 0;
        }
        /// <summary>
        /// The character has respawned.
        /// </summary>
        private void OnRespawn () {
            m_NetworkPosition = m_Transform.position;
            m_NetworkRotation = m_Transform.rotation;
        }
        /// <summary>
        /// The character's position or rotation has been teleported.
        /// </summary>
        /// <param name="snapAnimator">Should the animator be snapped?</param>
        private void OnImmediateTransformChange (bool snapAnimator) {
            m_NetworkPosition = m_Transform.position;
            m_NetworkRotation = m_Transform.rotation;
        }
    }
}