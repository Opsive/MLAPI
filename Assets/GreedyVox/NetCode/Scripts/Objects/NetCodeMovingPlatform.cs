using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.Shared.StateSystem;
using Opsive.UltimateCharacterController.Objects;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Syncronizes the moving platform when a new player joins the room.
/// </summary>
namespace GreedyVox.NetCode
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetCodeInfo), typeof(NetCodeEvent))]
    public class NetCodeMovingPlatform : MovingPlatform
    {
        private string m_MsgName;
        private int m_MaxBufferSize;
        private NetCodeInfo m_NetworkInfo;
        private NetCodeEvent m_NetworkEvent;
        private CustomMessagingManager m_CustomMessagingManager;
        protected override void Awake()
        {
            base.Awake();
            m_MaxBufferSize = MaxBufferSize();
            m_NetworkInfo = GetComponent<NetCodeInfo>();
            m_NetworkEvent = GetComponent<NetCodeEvent>();
            if (m_NetworkEvent != null)
            {
                m_NetworkEvent.NetworkSpawnEvent += OnNetworkSpawn;
                m_NetworkEvent.NetworkDespawnEvent += OnNetworkDespawn;
            }
        }
        /// <summary>
        /// The object has been despawned.
        /// </summary>
        private void OnNetworkDespawn() =>
        EventHandler.UnregisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
        /// <summary>
        /// The object has been spawned.
        /// </summary>
        private void OnNetworkSpawn()
        {
            m_CustomMessagingManager = NetworkManager.Singleton.CustomMessagingManager;
            m_MsgName = $"{m_NetworkInfo.NetworkObjectId}MsgClientNetCodeMovingPlatform{m_NetworkInfo.OwnerClientId}";
            if (m_NetworkInfo != null)
            {
                if (m_NetworkInfo.IsServerHost())
                    EventHandler.RegisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
                else
                    m_CustomMessagingManager?.RegisterNamedMessageHandler(m_MsgName, (senderClientId, reader) =>
                    {
                        ByteUnpacker.ReadValuePacked(reader, out float time);
                        ByteUnpacker.ReadValuePacked(reader, out float delay);
                        ByteUnpacker.ReadValuePacked(reader, out float distance);
                        ByteUnpacker.ReadValuePacked(reader, out int state);
                        ByteUnpacker.ReadValuePacked(reader, out int point);
                        ByteUnpacker.ReadValuePacked(reader, out int direction);
                        ByteUnpacker.ReadValuePacked(reader, out int previous);
                        ByteUnpacker.ReadValuePacked(reader, out int count);
                        ByteUnpacker.ReadValuePacked(reader, out Vector3 target);
                        ByteUnpacker.ReadValuePacked(reader, out Vector3 position);
                        ByteUnpacker.ReadValuePacked(reader, out Quaternion facing);
                        ByteUnpacker.ReadValuePacked(reader, out Quaternion original);
                        ByteUnpacker.ReadValuePacked(reader, out Quaternion rotation);
                        InitializeMovingPlatformRpc(position, rotation, state, direction,
                        point, previous, distance, delay, original, time, target, facing, count);
                    });
            }
        }
        /// <summary>
        /// Returns the maximus size for the fast buffer writer.
        /// Added extra vector bytes, because of overflow with rotation.
        /// </summary>               
        private int MaxBufferSize() =>
        sizeof(float) * 3 + sizeof(int) * 5 + sizeof(float) * 3 * 3 + sizeof(float) * 4 * 3;
        /// <summary>
        /// A event from the server has been sent.
        /// </summary>
        /// <param name="id">The Client networking id that connected.</param>
        /// <param name="obj">The Player NetworkObject that connected.</param>
        public void OnPlayerConnected(ulong id, NetworkObjectReference obj)
        {
            var nextWaypointEventDelay = -1.0f;
            if (m_NextWaypointEvent != null)
                nextWaypointEventDelay = m_NextWaypointEvent.EndTime - Time.time;
            var activeStates = 0;
            for (int i = 0; i < States.Length - 1; i++)
                if (States[i].Active)
                    activeStates |= (int)Mathf.Pow(i + 1, 2);
            using (var writer = new FastBufferWriter(
                FastBufferWriter.GetWriteSize(m_MoveTime), Allocator.Temp, m_MaxBufferSize))
            {
                BytePacker.WriteValuePacked(writer, m_MoveTime);
                BytePacker.WriteValuePacked(writer, nextWaypointEventDelay);
                BytePacker.WriteValuePacked(writer, m_NextWaypointDistance);
                BytePacker.WriteValuePacked(writer, activeStates);
                BytePacker.WriteValuePacked(writer, m_NextWaypoint);
                BytePacker.WriteValuePacked(writer, (int)m_Direction);
                BytePacker.WriteValuePacked(writer, m_PreviousWaypoint);
                BytePacker.WriteValuePacked(writer, m_ActiveCharacterCount);
                BytePacker.WriteValuePacked(writer, m_TargetPosition);
                BytePacker.WriteValuePacked(writer, m_Transform.position);
                BytePacker.WriteValuePacked(writer, m_TargetRotation);
                BytePacker.WriteValuePacked(writer, m_OriginalRotation);
                BytePacker.WriteValuePacked(writer, m_Transform.rotation);
                m_CustomMessagingManager?.SendNamedMessage(m_MsgName, id, writer);
            }
        }
        /// <summary>
        /// Initialize the moving platform to the same parameters as the server.
        /// </summary>
        [Rpc(SendTo.NotMe)]
        private void InitializeMovingPlatformRpc(Vector3 position, Quaternion rotation, int activeStates, int pathDirection, int nextWaypoint, int previousWaypoint, float nextWaypointDistance,
            float nextWaypointEventDelay, Quaternion originalRotation, float moveTime, Vector3 targetPosition, Quaternion targetRotation, int activeCharacterCount)
        {
            m_Transform.position = position;
            m_Transform.rotation = rotation;
            m_Direction = (PathDirection)pathDirection;
            m_NextWaypoint = nextWaypoint;
            m_PreviousWaypoint = previousWaypoint;
            m_NextWaypointDistance = nextWaypointDistance;
            m_OriginalRotation = originalRotation;
            m_MoveTime = moveTime;
            m_TargetPosition = targetPosition;
            m_TargetRotation = targetRotation;
            m_ActiveCharacterCount = activeCharacterCount;
            if (m_NextWaypointEvent != null)
            {
                Scheduler.Cancel(m_NextWaypointEvent);
                m_NextWaypointEvent = null;
            }
            if (nextWaypointEventDelay != -1)
                m_NextWaypointEvent = Scheduler.ScheduleFixed(nextWaypointEventDelay, UpdateWaypoint);
            // The states should match the master client.
            for (int i = 0; i < States.Length - 1; i++)
                if (((int)Mathf.Pow(i + 1, 2) & activeStates) != 0)
                    StateManager.SetState(m_GameObject, States[i].Name, true);
            // There will be a small amount of lag between the time that the RPC was sent on the server and the time that it was received on the client.
            // Make up for this difference by simulating the movement for the lag difference.
            var lag = Mathf.Abs(NetworkManager.Singleton.ServerTime.TimeAsFloat - NetworkManager.Singleton.LocalTime.TimeAsFloat);
            var startTime = Time.time;
            var elapsedTime = 0f;
            while (elapsedTime < lag)
            {
                // The next waypoint event has to be simulated.
                if (m_NextWaypointEvent != null
                 && startTime + elapsedTime > m_NextWaypointEvent.EndTime)
                    UpdateWaypoint();
                Move();
                elapsedTime += Time.fixedDeltaTime;
            }
        }
    }
}