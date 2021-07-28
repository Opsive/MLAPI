using MLAPI;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;
using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;

/// <summary>
/// Syncronizes the ILookSource over the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedLookSource : NetworkBehaviour, ILookSource {
        /// <summary>
        /// Specifies which look source objects are dirty.
        /// </summary>
        private enum TransformDirtyFlags : byte {
            LookDirectionDistance = 1, // The Look Direction Distance has changed.
            Pitch = 2, // The Pitch has changed.
            LookPosition = 4, // The Look Position has changed.
            LookDirection = 8, // The Look Direction has changed.
        }

        [Tooltip ("A multiplier to apply to the networked values for remote players.")]
        [SerializeField] protected float m_RemoteInterpolationMultiplayer = 1.2f;
        private byte m_Flag;
        private ulong m_ServerID;
        private Transform m_Transform;
        private GameObject m_GameObject;
        private ILookSource m_LookSource;
        private NetworkedManager m_NetworkManager;
        private UltimateCharacterLocomotion m_CharacterLocomotion;
        public GameObject GameObject { get { return m_GameObject; } }
        public Transform Transform { get { return m_Transform; } }
        public float LookDirectionDistance { get { return m_NetworkLookDirectionDistance; } }
        public float Pitch { get { return m_NetworkPitch; } }
        private float m_NetworkLookDirectionDistance = 1;
        private float m_NetworkTargetLookDirectionDistance = 1;
        private float m_NetworkTargetPitch, m_NetworkPitch;
        private string m_MsgClientLookSource, m_MsgServerLookSource;
        private Vector3 m_NetworkLookPosition, m_NetworkLookDirection;
        private Vector3 m_NetworkTargetLookPosition, m_NetworkTargetLookDirection;
        private bool m_InitialSync = true;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake () {
            m_GameObject = gameObject;
            m_Transform = transform;
            m_NetworkManager = NetworkedManager.Instance;
            m_NetworkLookPosition = m_NetworkTargetLookPosition = m_Transform.position;
            m_NetworkLookDirection = m_NetworkTargetLookDirection = m_Transform.forward;
            m_CharacterLocomotion = m_GameObject.GetCachedComponent<UltimateCharacterLocomotion> ();

            EventHandler.RegisterEvent<ILookSource> (m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
        }
        /// <summary>
        /// Register for any interested events.
        /// </summary>
        private void Start () {
            // Remote characters will not have a local look source. The current component should act as the look source.
            if (!IsOwner) {
                EventHandler.UnregisterEvent<ILookSource> (m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
                EventHandler.ExecuteEvent<ILookSource> (m_GameObject, "OnCharacterAttachLookSource", this);
            }
        }
        private void OnDisable () {
            m_NetworkManager.NetworkSettings.NetworkSyncServerEvent -= OnNetworkSyncServerEvent;
            m_NetworkManager.NetworkSettings.NetworkSyncClientEvent -= OnNetworkSyncClientEvent;
            m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent -= OnNetworkSyncUpdateEvent;
        }
        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        private void OnDestroy () {
            CustomMessagingManager.UnregisterNamedMessageHandler (m_MsgServerLookSource);
            CustomMessagingManager.UnregisterNamedMessageHandler (m_MsgClientLookSource);
            EventHandler.UnregisterEvent<ILookSource> (m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
        }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup. Provides a Payload if it was provided
        /// </summary>
        public override void NetworkStart () {
            m_ServerID = NetworkManager.Singleton.ServerClientId;
            m_MsgClientLookSource = $"{NetworkObjectId}MsgClientLookSource{OwnerClientId}";
            m_MsgServerLookSource = $"{NetworkObjectId}MsgServerLookSource{OwnerClientId}";

            if (IsServer) {
                m_NetworkManager.NetworkSettings.NetworkSyncServerEvent += OnNetworkSyncServerEvent;
            } else if (IsOwner) {
                m_NetworkManager.NetworkSettings.NetworkSyncClientEvent += OnNetworkSyncClientEvent;
            }

            if (!IsOwner) {
                m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent += OnNetworkSyncUpdateEvent;
                if (IsServer) {
                    CustomMessagingManager.RegisterNamedMessageHandler (m_MsgServerLookSource, (sender, stream) => {
                        using (PooledNetworkReader reader = PooledNetworkReader.Get (stream)) {
                            SerializeView (reader);
                        }
                    });
                } else {
                    CustomMessagingManager.RegisterNamedMessageHandler (m_MsgClientLookSource, (sender, stream) => {
                        using (PooledNetworkReader reader = PooledNetworkReader.Get (stream)) {
                            SerializeView (reader);
                        }
                    });
                }
            }
        }
        /// <summary>
        /// Network sync event called from the NetworkInfo component
        /// </summary>
        private void OnNetworkSyncClientEvent () {
            using (var stream = PooledNetworkBuffer.Get ())
            using (var writer = PooledNetworkWriter.Get (stream)) {
                SerializeView (writer);
                CustomMessagingManager.SendNamedMessage (m_MsgServerLookSource, m_ServerID, stream, NetworkChannel.ChannelUnused);
            }
        }
        /// <summary>
        /// Network broadcast event called from the NetworkInfo component
        /// </summary>
        private void OnNetworkSyncServerEvent () {
            using (var stream = PooledNetworkBuffer.Get ())
            using (var writer = PooledNetworkWriter.Get (stream)) {
                if (IsOwner) {
                    SerializeView (writer);
                } else {
                    SerializeView (writer, ref m_Flag);
                }
                CustomMessagingManager.SendNamedMessage (m_MsgClientLookSource, null, stream, NetworkChannel.ChannelUnused); // [MLAPI] 1024 buffers have been created. Did you forget to dispose?
            }
        }
        /// <summary>
        /// Updates the remote character's transform values.
        /// </summary>
        private void OnNetworkSyncUpdateEvent () {
            var serializationRate = m_NetworkManager.NetworkSettings.SyncRateClient * m_RemoteInterpolationMultiplayer;
            m_NetworkLookDirectionDistance = Mathf.MoveTowards (m_NetworkLookDirectionDistance, m_NetworkTargetLookDirectionDistance,
                Mathf.Abs (m_NetworkTargetLookDirectionDistance - m_NetworkLookDirectionDistance) * serializationRate);
            m_NetworkPitch = Mathf.MoveTowards (m_NetworkPitch, m_NetworkTargetPitch, Mathf.Abs (m_NetworkTargetPitch - m_NetworkPitch) * serializationRate);
            m_NetworkLookPosition = Vector3.MoveTowards (m_NetworkLookPosition, m_NetworkTargetLookPosition, (m_NetworkTargetLookPosition - m_NetworkLookPosition).magnitude * serializationRate);
            m_NetworkLookDirection = Vector3.MoveTowards (m_NetworkLookDirection, m_NetworkTargetLookDirection, (m_NetworkTargetLookDirection - m_NetworkLookDirection).magnitude * serializationRate);
        }
        /// <summary>
        /// Called several times per second, so that your script can read synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being read from.</param>
        public void SerializeView (PooledNetworkReader stream) {
            m_Flag = (byte) stream.ReadByte ();
            if (m_Flag != (byte) TransformDirtyFlags.LookDirectionDistance)
                m_NetworkTargetLookDirectionDistance = stream.ReadSinglePacked ();
            if (m_Flag != (byte) TransformDirtyFlags.Pitch)
                m_NetworkTargetPitch = stream.ReadSinglePacked ();
            if (m_Flag != (byte) TransformDirtyFlags.LookPosition)
                m_NetworkTargetLookPosition = stream.ReadVector3Packed ();
            if (m_Flag != (byte) TransformDirtyFlags.LookDirection)
                m_NetworkTargetLookDirection = stream.ReadVector3Packed ();
            if (m_InitialSync) {
                m_NetworkLookDirectionDistance = m_NetworkTargetLookDirectionDistance;
                m_NetworkPitch = m_NetworkTargetPitch;
                m_NetworkLookPosition = m_NetworkTargetLookPosition;
                m_NetworkLookDirection = m_NetworkTargetLookDirection;
                m_InitialSync = false;
            }
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public void SerializeView (PooledNetworkWriter stream, ref byte flag) {
            // Send the changes.
            stream.WriteByte (flag);
            if (flag != (byte) TransformDirtyFlags.LookDirectionDistance)
                stream.WriteSinglePacked (m_NetworkLookDirectionDistance);
            if (flag != (byte) TransformDirtyFlags.Pitch)
                stream.WriteSinglePacked (m_NetworkPitch);
            if (flag != (byte) TransformDirtyFlags.LookPosition)
                stream.WriteVector3Packed (m_NetworkLookPosition);
            if (flag != (byte) TransformDirtyFlags.LookDirection)
                stream.WriteVector3Packed (m_NetworkLookDirection);
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public void SerializeView (PooledNetworkWriter stream) {
            // Determine the objects that have changed.
            m_Flag = 0;
            if (m_LookSource != null) {
                if (m_NetworkLookDirectionDistance != m_LookSource.LookDirectionDistance) {
                    m_Flag |= (byte) TransformDirtyFlags.LookDirectionDistance;
                    m_NetworkLookDirectionDistance = m_LookSource.LookDirectionDistance;
                }
                if (m_NetworkPitch != m_LookSource.Pitch) {
                    m_Flag |= (byte) TransformDirtyFlags.Pitch;
                    m_NetworkPitch = m_LookSource.Pitch;
                }
                var lookPosition = m_LookSource.LookPosition ();
                if (m_NetworkLookPosition != lookPosition) {
                    m_Flag |= (byte) TransformDirtyFlags.LookPosition;
                    m_NetworkLookPosition = lookPosition;
                }
                var lookDirection = m_LookSource.LookDirection (false);
                if (m_NetworkLookDirection != lookDirection) {
                    m_Flag |= (byte) TransformDirtyFlags.LookDirection;
                    m_NetworkLookDirection = lookDirection;
                }
                // Send the changes.
                stream.WriteByte (m_Flag);
                if (m_Flag != (byte) TransformDirtyFlags.LookDirectionDistance)
                    stream.WriteSinglePacked (m_NetworkLookDirectionDistance);
                if (m_Flag != (byte) TransformDirtyFlags.Pitch)
                    stream.WriteSinglePacked (m_NetworkPitch);
                if (m_Flag != (byte) TransformDirtyFlags.LookPosition)
                    stream.WriteVector3Packed (m_NetworkLookPosition);
                if (m_Flag != (byte) TransformDirtyFlags.LookDirection)
                    stream.WriteVector3Packed (m_NetworkLookDirection);
            }
        }
        /// <summary>
        /// A new ILookSource object has been attached to the character.
        /// </summary>
        /// <param name="lookSource">The ILookSource object attached to the character.</param>
        private void OnAttachLookSource (ILookSource lookSource) {
            m_LookSource = lookSource;
        }
        /// <summary>
        /// Returns the position of the look source.
        /// </summary>
        /// <returns>The position of the look source.</returns>
        public Vector3 LookPosition () {
            return m_NetworkLookPosition;
        }
        /// <summary>
        /// Returns the direction that the character is looking.
        /// </summary>
        /// <param name="characterLookDirection">Is the character look direction being retrieved?</param>
        /// <returns>The direction that the character is looking.</returns>
        public Vector3 LookDirection (bool characterLookDirection) {
            if (characterLookDirection) {
                return m_Transform.forward;
            }
            return m_NetworkLookDirection;
        }
        /// <summary>
        /// Returns the direction that the character is looking.
        /// </summary>
        /// <param name="lookPosition">The position that the character is looking from.</param>
        /// <param name="characterLookDirection">Is the character look direction being retrieved?</param>
        /// <param name="layerMask">The LayerMask value of the objects that the look direction can hit.</param>
        /// <param name="useRecoil">Should recoil be included in the look direction?</param>
        /// <param name="includeMovementSpread">Should the movement spread be included in the look direction?</param>
        /// <returns>The direction that the character is looking.</returns>
        public Vector3 LookDirection (Vector3 lookPosition, bool characterLookDirection, int layerMask, bool includeRecoil, bool includeMovementSpread) {
            var collisionLayerEnabled = m_CharacterLocomotion.CollisionLayerEnabled;
            m_CharacterLocomotion.EnableColliderCollisionLayer (false);
            // Cast a ray from the look source point in the forward direction. The look direction is then the vector from the look position to the hit point.
            RaycastHit hit;
            Vector3 direction;
            if (Physics.Raycast (m_NetworkLookPosition, m_NetworkLookDirection, out hit, m_NetworkLookDirectionDistance, layerMask, QueryTriggerInteraction.Ignore)) {
                direction = (hit.point - lookPosition).normalized;
            } else {
                direction = m_NetworkLookDirection;
            }
            m_CharacterLocomotion.EnableColliderCollisionLayer (collisionLayerEnabled);
            return direction;
        }
    }
}