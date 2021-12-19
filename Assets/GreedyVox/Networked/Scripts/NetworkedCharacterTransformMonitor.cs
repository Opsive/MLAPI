using System.Collections.Generic;
using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes the character's transform values over the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedCharacterTransformMonitor : NetworkBehaviour {
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
        private int m_MaxBufferSize;
        private bool m_InitialSync = true;
        private Transform m_Transform;
        private IReadOnlyList<ulong> m_Clients;
        private ulong m_PlatformID, m_ServerID;
        private NetworkedManager m_NetworkManager;
        private FastBufferWriter m_FastBufferWriter;
        private string m_MsgNameClient, m_MsgNameServer;
        private float m_NetworkedTime, m_Distance, m_Angle;
        private CharacterFootEffects m_CharacterFootEffects;
        private CustomMessagingManager m_CustomMessagingManager;
        private UltimateCharacterLocomotion m_CharacterLocomotion;
        private Quaternion m_NetworkPlatformPrevRotationOffset, m_NetworkPlatformRotationOffset, m_NetworkRotation;
        private Vector3 m_NetworkPlatformRelativePosition, m_NetworkPlatformPrevRelativePosition, m_NetworkPosition, m_NetworkScale;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake () {
            m_Transform = transform;
            m_MaxBufferSize = MaxBufferSize ();
            m_NetworkScale = m_Transform.localScale;
            m_NetworkPosition = m_Transform.position;
            m_NetworkRotation = m_Transform.rotation;
            m_NetworkManager = NetworkedManager.Instance;
            m_CharacterFootEffects = gameObject.GetCachedComponent<CharacterFootEffects> ();
            m_CharacterLocomotion = gameObject.GetCachedComponent<UltimateCharacterLocomotion> ();

            EventHandler.RegisterEvent (gameObject, "OnRespawn", OnRespawn);
            EventHandler.RegisterEvent<bool> (gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);
        }
        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        public override void OnDestroy () {
            base.OnDestroy ();
            EventHandler.UnregisterEvent (gameObject, "OnRespawn", OnRespawn);
            EventHandler.UnregisterEvent<bool> (gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);
        }
        /// <summary>
        /// The object has been despawned.
        /// </summary>
        public override void OnNetworkDespawn () {
            m_CustomMessagingManager?.UnregisterNamedMessageHandler (m_MsgNameServer);
            m_CustomMessagingManager?.UnregisterNamedMessageHandler (m_MsgNameClient);
            m_NetworkManager.NetworkSettings.NetworkSyncServerEvent -= OnNetworkSyncServerEvent;
            m_NetworkManager.NetworkSettings.NetworkSyncClientEvent -= OnNetworkSyncClientEvent;
            m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent -= OnNetworkSyncUpdateEvent;
        }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup.
        /// </summary>
        public override void OnNetworkSpawn () {
            m_ServerID = NetworkManager.Singleton.ServerClientId;
            m_MsgNameClient = $"{NetworkObjectId}MsgClientTransform{OwnerClientId}";
            m_MsgNameServer = $"{NetworkObjectId}MsgServerTransform{OwnerClientId}";
            m_CustomMessagingManager = NetworkManager.Singleton.CustomMessagingManager;

            if (IsServer) {
                m_Clients = NetworkManager.Singleton.ConnectedClientsIds;
                m_NetworkManager.NetworkSettings.NetworkSyncServerEvent += OnNetworkSyncServerEvent;
            } else if (IsOwner) {
                m_NetworkManager.NetworkSettings.NetworkSyncClientEvent += OnNetworkSyncClientEvent;
            }

            if (!IsOwner) {
                m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent += OnNetworkSyncUpdateEvent;
                if (IsServer) {
                    m_CustomMessagingManager?.RegisterNamedMessageHandler (m_MsgNameServer, (sender, reader) => {
                        Serialize (ref reader);
                    });
                } else {
                    m_CustomMessagingManager?.RegisterNamedMessageHandler (m_MsgNameClient, (sender, reader) => {
                        Serialize (ref reader);
                    });
                }
            }
        }
        /// <summary>
        /// Returns the maximus size for the fast buffer writer
        /// </summary>               
        private int MaxBufferSize () {
            return sizeof (byte) + sizeof (long) + sizeof (float) * 3 * 4;
        }
        /// <summary>
        /// Network sync event called from the NetworkInfo component
        /// </summary>
        private void OnNetworkSyncClientEvent () {
            // Error handling if this function still executing after despawning event
            if (NetworkManager.Singleton.IsClient) {
                using (m_FastBufferWriter = new FastBufferWriter (FastBufferWriter.GetWriteSize (m_Flag), Allocator.Temp, m_MaxBufferSize)) {
                    if (Serialize ()) {
                        m_CustomMessagingManager?.SendNamedMessage (m_MsgNameServer, m_ServerID, m_FastBufferWriter, NetworkDelivery.UnreliableSequenced);
                    }
                }
            }
        }
        /// <summary>
        /// Network broadcast event called from the NetworkInfo component
        /// </summary>
        private void OnNetworkSyncServerEvent () {
            // Error handling if this function still executing after despawning event
            if (NetworkManager.Singleton.IsServer) {
                using (m_FastBufferWriter = new FastBufferWriter (FastBufferWriter.GetWriteSize (m_Flag), Allocator.Temp, m_MaxBufferSize)) {
                    if (IsOwner) {
                        if (Serialize ()) {
                            m_CustomMessagingManager?.SendNamedMessage (m_MsgNameClient, m_Clients, m_FastBufferWriter, NetworkDelivery.UnreliableSequenced);
                        }
                    } else if (Serialize (ref m_Flag)) {
                        m_CustomMessagingManager?.SendNamedMessage (m_MsgNameClient, m_Clients, m_FastBufferWriter, NetworkDelivery.UnreliableSequenced);
                    }
                }
                m_Flag = 0;
            }
        }
        /// <summary>
        /// Updates the remote character's transform values.
        /// </summary>
        private void OnNetworkSyncUpdateEvent () {
            // When the character is on a moving platform the position and rotation is relative to that platform.
            // This allows the character to stay on the platform even though the platform will not be in the exact same location between any two instances.
            var serializationRate = m_NetworkManager.NetworkSettings.SyncRateClient * m_RemoteInterpolationMultiplayer;
            if (m_CharacterLocomotion.Platform != null) {
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
                if (m_CharacterFootEffects != null && (m_NetworkPlatformPrevRelativePosition - m_NetworkPlatformRelativePosition).sqrMagnitude > 0.01f) {
                    m_CharacterFootEffects.CanPlaceFootstep = true;
                }
#endif
                m_NetworkPlatformPrevRelativePosition = Vector3.MoveTowards (m_NetworkPlatformPrevRelativePosition, m_NetworkPlatformRelativePosition, m_Distance * serializationRate);
                m_CharacterLocomotion.SetPosition (m_CharacterLocomotion.Platform.TransformPoint (m_NetworkPlatformPrevRelativePosition), false);

                m_NetworkPlatformPrevRotationOffset = Quaternion.RotateTowards (m_NetworkPlatformPrevRotationOffset, m_NetworkPlatformRotationOffset, m_Angle * serializationRate);
                m_CharacterLocomotion.SetRotation (MathUtility.TransformQuaternion (m_CharacterLocomotion.Platform.rotation, m_NetworkPlatformPrevRotationOffset), false);
            } else {
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
                if (m_CharacterFootEffects != null && (m_Transform.position - m_NetworkPosition).sqrMagnitude > 0.01f) {
                    m_CharacterFootEffects.CanPlaceFootstep = true;
                }
#endif
                m_Transform.position = Vector3.MoveTowards (m_Transform.position, m_NetworkPosition, m_Distance * serializationRate);
                m_Transform.rotation = Quaternion.RotateTowards (m_Transform.rotation, m_NetworkRotation, m_Angle * serializationRate);
            }
        }
        /// <summary>
        /// Called several times per second, so that your script can read synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being read from.</param>
        public void Serialize (ref FastBufferReader reader) {
            ByteUnpacker.ReadValuePacked (reader, out m_Flag);
            if ((m_Flag & (byte) TransformDirtyFlags.Platform) != 0) {
                ByteUnpacker.ReadValuePacked (reader, out ulong pid);
                // When the character is on a platform the position and rotation is relative to that platform.
                if ((m_Flag & (byte) TransformDirtyFlags.Position) != 0)
                    ByteUnpacker.ReadValuePacked (reader, out m_NetworkPlatformRelativePosition);
                if ((m_Flag & (byte) TransformDirtyFlags.Rotation) != 0) {
                    ByteUnpacker.ReadValuePacked (reader, out Vector3 angle);
                    m_NetworkPlatformRotationOffset = Quaternion.Euler (angle);
                }
                // Do not do any sort of interpolation when the platform has changed.
                if (pid != m_PlatformID && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue (pid, out var platform)) {
                    m_PlatformID = pid;
                    m_CharacterLocomotion.SetPlatform (platform.transform, true);
                    m_NetworkPlatformRelativePosition = m_NetworkPlatformPrevRelativePosition =
                        platform.transform.InverseTransformPoint (m_Transform.position);
                    m_NetworkPlatformRotationOffset = m_NetworkPlatformPrevRotationOffset =
                        MathUtility.InverseTransformQuaternion (platform.transform.rotation, m_Transform.rotation);
                }
                m_Distance = Vector3.Distance (m_NetworkPlatformPrevRelativePosition, m_NetworkPlatformRelativePosition);
                m_Angle = Quaternion.Angle (m_NetworkPlatformPrevRotationOffset, m_NetworkPlatformRotationOffset);
            } else {
                if (m_PlatformID != 0) {
                    m_PlatformID = 0;
                    m_CharacterLocomotion.SetPlatform (null, true);
                }
                if ((m_Flag & (byte) TransformDirtyFlags.Position) != 0) {
                    ByteUnpacker.ReadValuePacked (reader, out m_NetworkPosition);
                    ByteUnpacker.ReadValuePacked (reader, out Vector3 velocity);
                    // Account for the lag.
                    if (!m_InitialSync) {
                        var lag = Mathf.Abs (NetworkManager.Singleton.LocalTime.TimeAsFloat - m_NetworkedTime);
                        m_NetworkPosition += velocity * lag;
                    };
                    m_InitialSync = false;
                }
                if ((m_Flag & (byte) TransformDirtyFlags.Rotation) != 0) {
                    ByteUnpacker.ReadValuePacked (reader, out Vector3 angle);
                    m_NetworkRotation = Quaternion.Euler (angle);
                }
                m_Distance = Vector3.Distance (m_Transform.position, m_NetworkPosition);
                m_Angle = Quaternion.Angle (m_Transform.rotation, m_NetworkRotation);
            }
            if ((m_Flag & (byte) TransformDirtyFlags.Scale) != 0) {
                ByteUnpacker.ReadValuePacked (reader, out Vector3 scale);
                m_Transform.localScale = scale;
            }
            m_NetworkedTime = NetworkManager.Singleton.LocalTime.TimeAsFloat;
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public bool Serialize (ref byte flag) {
            // When the character is on a platform the position and rotation is relative to that platform.
            if ((m_Flag & (byte) TransformDirtyFlags.Platform) != 0) {
                // Write flag as dirty
                BytePacker.WriteValuePacked (m_FastBufferWriter, flag);
                BytePacker.WriteValuePacked (m_FastBufferWriter, m_PlatformID);
                // Update network position here to insure that local or server calculate the platform inverse transform point.
                if ((flag & (byte) TransformDirtyFlags.Position) != 0) {
                    m_NetworkPosition = m_CharacterLocomotion.Platform.InverseTransformPoint (m_Transform.position);
                    BytePacker.WriteValuePacked (m_FastBufferWriter, m_NetworkPosition);
                }
                // Update network rotation here to insure that local or server calculate the platform inverse transform quaternion.
                if ((flag & (byte) TransformDirtyFlags.Rotation) != 0) {
                    m_NetworkRotation = MathUtility.InverseTransformQuaternion (m_CharacterLocomotion.Platform.rotation, m_Transform.rotation);
                    BytePacker.WriteValuePacked (m_FastBufferWriter, m_NetworkRotation.eulerAngles);
                }
            } else {
                // Write flag as dirty
                BytePacker.WriteValuePacked (m_FastBufferWriter, flag);
                if ((flag & (byte) TransformDirtyFlags.Position) != 0) {
                    BytePacker.WriteValuePacked (m_FastBufferWriter, m_Transform.position);
                    BytePacker.WriteValuePacked (m_FastBufferWriter, m_Transform.position - m_NetworkPosition);
                }
                if ((flag & (byte) TransformDirtyFlags.Rotation) != 0)
                    BytePacker.WriteValuePacked (m_FastBufferWriter, m_Transform.eulerAngles);
            }
            if ((flag & (byte) TransformDirtyFlags.Scale) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, m_Transform.localScale);
            return flag > 0;
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public bool Serialize () {
            m_Flag = 0;
            // When the character is on a platform the position and rotation is relative to that platform.
            if (m_CharacterLocomotion.Platform != null) {
                var platform = m_CharacterLocomotion.Platform.gameObject.GetCachedComponent<NetworkObject> ();
                if (platform == null) {
                    Debug.LogError ("Error: The platform " + m_CharacterLocomotion.Platform + " must have a NetworkObject.");
                } else {
                    // Determine the changed objects before sending them.
                    m_Flag |= (byte) TransformDirtyFlags.Platform;
                    m_PlatformID = platform.NetworkObjectId;
                    if (m_CharacterLocomotion.Platform.InverseTransformPoint (m_Transform.position) != m_NetworkPosition)
                        m_Flag |= (byte) TransformDirtyFlags.Position;
                    if (MathUtility.InverseTransformQuaternion (m_CharacterLocomotion.Platform.rotation, m_Transform.rotation) != m_NetworkRotation)
                        m_Flag |= (byte) TransformDirtyFlags.Rotation;
                }
            } else if (m_Transform != null) {
                // Determine the changed objects before sending them.
                if (m_Transform.position != m_NetworkPosition) {
                    m_Flag |= (byte) TransformDirtyFlags.Position;
                    m_NetworkPosition = m_Transform.position;
                }
                if (m_Transform.rotation != m_NetworkRotation) {
                    m_Flag |= (byte) TransformDirtyFlags.Rotation;
                    m_NetworkRotation = m_Transform.rotation;
                }
            }
            if (m_SynchronizeScale && m_Transform != null && m_Transform.localScale != m_NetworkScale) {
                m_Flag |= (byte) TransformDirtyFlags.Scale;
                m_NetworkScale = m_Transform.localScale;
            }
            return Serialize (ref m_Flag);
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