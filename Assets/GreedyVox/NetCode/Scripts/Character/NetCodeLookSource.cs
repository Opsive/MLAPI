using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Syncronizes the ILookSource over the network.
/// </summary>
namespace GreedyVox.NetCode.Character
{
    [DisallowMultipleComponent]
    public class NetCodeLookSource : NetworkBehaviour, ILookSource
    {
        [Tooltip("A multiplier to apply to the networked values for remote players.")]
        [SerializeField] protected float m_RemoteInterpolationMultiplayer = 1.2f;
        private byte m_Flag;
        private ulong m_ServerID;
        private int m_MaxBufferSize;
        private Transform m_Transform;
        private GameObject m_GameObject;
        private ILookSource m_LookSource;
        private NetCodeManager m_NetworkManager;
        private FastBufferWriter m_FastBufferWriter;
        private string m_MsgNameClient, m_MsgNameServer;
        private float m_NetworkLookDirectionDistance = 1;
        private float m_NetworkTargetPitch, m_NetworkPitch;
        private float m_NetworkTargetLookDirectionDistance = 1;
        private CustomMessagingManager m_CustomMessagingManager;
        private UltimateCharacterLocomotion m_CharacterLocomotion;
        private Vector3 m_NetworkLookPosition, m_NetworkLookDirection;
        private Vector3 m_NetworkTargetLookPosition, m_NetworkTargetLookDirection;
        public GameObject GameObject { get { return m_GameObject; } }
        public Transform Transform { get { return m_Transform; } }
        public float LookDirectionDistance { get { return m_NetworkLookDirectionDistance; } }
        public float Pitch { get { return m_NetworkPitch; } }
        /// <summary>
        /// Specifies which look source objects are dirty.
        /// </summary>
        private enum TransformDirtyFlags : byte
        {
            LookDirectionDistance = 1, // The Look Direction Distance has changed.
            Pitch = 2, // The Pitch has changed.
            LookPosition = 4, // The Look Position has changed.
            LookDirection = 8, // The Look Direction has changed.
        }
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_GameObject = gameObject;
            m_MaxBufferSize = MaxBufferSize();
            m_NetworkManager = NetCodeManager.Instance;
            m_NetworkLookPosition = m_NetworkTargetLookPosition = m_Transform.position;
            m_NetworkLookDirection = m_NetworkTargetLookDirection = m_Transform.forward;
            m_CharacterLocomotion = m_GameObject.GetCachedComponent<UltimateCharacterLocomotion>();
            EventHandler.RegisterEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
        }
        /// <summary>
        /// Register for any interested events.
        /// </summary>
        private void Start()
        {
            // Remote characters will not have a local look source. The current component should act as the look source.
            if (!IsOwner)
            {
                EventHandler.UnregisterEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
                EventHandler.ExecuteEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", this);
            }
        }
        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            EventHandler.UnregisterEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
            base.OnDestroy();
        }
        /// <summary>
        /// The object has been despawned.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            m_CustomMessagingManager?.UnregisterNamedMessageHandler(m_MsgNameServer);
            m_CustomMessagingManager?.UnregisterNamedMessageHandler(m_MsgNameClient);
            m_NetworkManager.NetworkSettings.NetworkSyncServerEvent -= OnNetworkSyncServerEvent;
            m_NetworkManager.NetworkSettings.NetworkSyncClientEvent -= OnNetworkSyncClientEvent;
            m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent -= OnNetworkSyncUpdateEvent;
            base.OnNetworkDespawn();
        }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            m_ServerID = NetworkManager.ServerClientId;
            m_MsgNameClient = $"{OwnerClientId}MsgClientLookSource{NetworkObjectId}";
            m_MsgNameServer = $"{OwnerClientId}MsgServerLookSource{NetworkObjectId}";
            m_CustomMessagingManager = NetworkManager.Singleton.CustomMessagingManager;

            if (IsServer)
                m_NetworkManager.NetworkSettings.NetworkSyncServerEvent += OnNetworkSyncServerEvent;
            else if (IsOwner)
                m_NetworkManager.NetworkSettings.NetworkSyncClientEvent += OnNetworkSyncClientEvent;
            else OnNetworkSyncEventRpc();

            if (!IsOwner)
            {
                m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent += OnNetworkSyncUpdateEvent;
                m_CustomMessagingManager?.RegisterNamedMessageHandler(IsServer ? m_MsgNameServer : m_MsgNameClient, (sender, reader) =>
                { SerializeView(ref reader); });
            }
            base.OnNetworkSpawn();
        }
        /// <summary>
        /// Returns the maximus size for the fast buffer writer
        /// </summary>               
        private int MaxBufferSize() => sizeof(byte) + sizeof(float) * 2 + sizeof(float) * 3 * 3;
        /// <summary>
        /// A player connected syncing event sent.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void OnNetworkSyncEventRpc(RpcParams rpc = default)
        {
            SetNetworkSyncEventRpc(m_NetworkLookPosition, m_NetworkLookDirection, m_NetworkLookDirectionDistance,
            m_NetworkPitch, RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
        }
        /// <summary>
        /// Sync all players already connected to the server.
        /// </summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void SetNetworkSyncEventRpc(Vector3 position, Vector3 direction, float distance, float pitch, RpcParams rpc)
        {
            m_NetworkLookDirectionDistance = distance;
            m_NetworkPitch = pitch;
            m_NetworkLookPosition = position;
            m_NetworkLookDirection = direction;
        }
        /// <summary>
        /// Network sync event called from the NetworkInfo component
        /// </summary>
        private void OnNetworkSyncClientEvent()
        {
            // Error handling if this function still executing after despawning event
            if (NetworkManager.Singleton.IsClient)
                using (m_FastBufferWriter = new FastBufferWriter(FastBufferWriter.GetWriteSize(m_Flag), Allocator.Temp, m_MaxBufferSize))
                    if (SerializeView())
                        m_CustomMessagingManager?.SendNamedMessage(m_MsgNameServer, m_ServerID, m_FastBufferWriter, NetworkDelivery.UnreliableSequenced);
        }
        /// <summary>
        /// Network broadcast event called from the NetworkInfo component
        /// </summary>
        private void OnNetworkSyncServerEvent()
        {
            // Error handling if this function still executing after despawning event
            if (NetworkManager.Singleton.IsServer)
            {
                using (m_FastBufferWriter = new FastBufferWriter(FastBufferWriter.GetWriteSize(m_Flag), Allocator.Temp, m_MaxBufferSize))
                    if (IsOwner)
                    {
                        if (SerializeView())
                            m_CustomMessagingManager?.SendNamedMessageToAll(m_MsgNameClient, m_FastBufferWriter, NetworkDelivery.UnreliableSequenced);
                    }
                    else if (SerializeView(ref m_Flag))
                        m_CustomMessagingManager?.SendNamedMessageToAll(m_MsgNameClient, m_FastBufferWriter, NetworkDelivery.UnreliableSequenced);
                m_Flag = 0;
            }
        }
        /// <summary>
        /// Updates the remote character's transform values.
        /// </summary>
        private void OnNetworkSyncUpdateEvent()
        {
            var serializationRate = m_NetworkManager.NetworkSettings.SyncRateClient * m_RemoteInterpolationMultiplayer;
            m_NetworkLookDirectionDistance = Mathf.MoveTowards(m_NetworkLookDirectionDistance, m_NetworkTargetLookDirectionDistance,
                Mathf.Abs(m_NetworkTargetLookDirectionDistance - m_NetworkLookDirectionDistance) * serializationRate);
            m_NetworkPitch = Mathf.MoveTowards(m_NetworkPitch, m_NetworkTargetPitch, Mathf.Abs(m_NetworkTargetPitch - m_NetworkPitch) * serializationRate);
            m_NetworkLookPosition = Vector3.MoveTowards(m_NetworkLookPosition, m_NetworkTargetLookPosition, (m_NetworkTargetLookPosition - m_NetworkLookPosition).magnitude * serializationRate);
            m_NetworkLookDirection = Vector3.MoveTowards(m_NetworkLookDirection, m_NetworkTargetLookDirection, (m_NetworkTargetLookDirection - m_NetworkLookDirection).magnitude * serializationRate);
        }
        /// <summary>
        /// Called several times per second, so that your script can read synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being read from.</param>
        public void SerializeView(ref FastBufferReader reader)
        {
            ByteUnpacker.ReadValuePacked(reader, out m_Flag);
            if ((m_Flag & (byte)TransformDirtyFlags.LookDirectionDistance) != 0)
                ByteUnpacker.ReadValuePacked(reader, out m_NetworkTargetLookDirectionDistance);
            if ((m_Flag & (byte)TransformDirtyFlags.Pitch) != 0)
                ByteUnpacker.ReadValuePacked(reader, out m_NetworkTargetPitch);
            if ((m_Flag & (byte)TransformDirtyFlags.LookPosition) != 0)
                ByteUnpacker.ReadValuePacked(reader, out m_NetworkTargetLookPosition);
            if ((m_Flag & (byte)TransformDirtyFlags.LookDirection) != 0)
                ByteUnpacker.ReadValuePacked(reader, out m_NetworkTargetLookDirection);
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public bool SerializeView(ref byte flag)
        {
            // Send the changes.
            BytePacker.WriteValuePacked(m_FastBufferWriter, flag);
            if ((flag & (byte)TransformDirtyFlags.LookDirectionDistance) != 0)
                BytePacker.WriteValuePacked(m_FastBufferWriter, m_NetworkLookDirectionDistance);
            if ((flag & (byte)TransformDirtyFlags.Pitch) != 0)
                BytePacker.WriteValuePacked(m_FastBufferWriter, m_NetworkPitch);
            if ((flag & (byte)TransformDirtyFlags.LookPosition) != 0)
                BytePacker.WriteValuePacked(m_FastBufferWriter, m_NetworkLookPosition);
            if ((flag & (byte)TransformDirtyFlags.LookDirection) != 0)
                BytePacker.WriteValuePacked(m_FastBufferWriter, m_NetworkLookDirection);
            return flag > 0;
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written to.</param>
        public bool SerializeView()
        {
            // Determine the objects that have changed.
            m_Flag = 0;
            if (m_LookSource != null)
            {
                if (m_NetworkLookDirectionDistance != m_LookSource.LookDirectionDistance)
                {
                    m_Flag |= (byte)TransformDirtyFlags.LookDirectionDistance;
                    m_NetworkLookDirectionDistance = m_LookSource.LookDirectionDistance;
                }
                if (m_NetworkPitch != m_LookSource.Pitch)
                {
                    m_Flag |= (byte)TransformDirtyFlags.Pitch;
                    m_NetworkPitch = m_LookSource.Pitch;
                }
                var lookPosition = m_LookSource.LookPosition(true);
                if (m_NetworkLookPosition != lookPosition)
                {
                    m_Flag |= (byte)TransformDirtyFlags.LookPosition;
                    m_NetworkLookPosition = lookPosition;
                }
                var lookDirection = m_LookSource.LookDirection(false);
                if (m_NetworkLookDirection != lookDirection)
                {
                    m_Flag |= (byte)TransformDirtyFlags.LookDirection;
                    m_NetworkLookDirection = lookDirection;
                }
            }
            return SerializeView(ref m_Flag);
        }
        /// <summary>
        /// A new ILookSource object has been attached to the character.
        /// </summary>
        /// <param name="lookSource">The ILookSource object attached to the character.</param>
        private void OnAttachLookSource(ILookSource lookSource) => m_LookSource = lookSource;
        /// <summary>
        /// Returns the position of the look source.
        /// </summary>
        /// <param name="characterLookPosition">Is the character look position being retrieved?</param>
        /// <returns>The position of the look source.</returns>
        public Vector3 LookPosition(bool characterLookPosition) => m_NetworkLookPosition;
        /// <summary>
        /// Returns the direction that the character is looking.
        /// </summary>
        /// <param name="characterLookDirection">Is the character look direction being retrieved?</param>
        /// <returns>The direction that the character is looking.</returns>
        public Vector3 LookDirection(bool characterLookDirection)
        {
            if (characterLookDirection)
                return m_Transform.forward;
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
        public Vector3 LookDirection(Vector3 lookPosition, bool characterLookDirection, int layerMask, bool includeRecoil, bool includeMovementSpread)
        {
            var collisionLayerEnabled = m_CharacterLocomotion.CollisionLayerEnabled;
            m_CharacterLocomotion.EnableColliderCollisionLayer(false);
            // Cast a ray from the look source point in the forward direction. The look direction is then the vector from the look position to the hit point.
            var direction = m_NetworkLookDirection;
            if (Physics.Raycast(m_NetworkLookPosition, m_NetworkLookDirection, out RaycastHit hit, m_NetworkLookDirectionDistance, layerMask, QueryTriggerInteraction.Ignore))
                direction = (hit.point - lookPosition).normalized;
            m_CharacterLocomotion.EnableColliderCollisionLayer(collisionLayerEnabled);
            return direction;
        }
    }
}