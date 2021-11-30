using Opsive.Shared.Game;
using Opsive.Shared.StateSystem;
using Opsive.UltimateCharacterController.Game;
using Opsive.UltimateCharacterController.Objects;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Syncronizes the moving platform when a new player joins the room.
/// </summary>
namespace GreedyVox.Networked {
    public class NetworkedMovingPlatform : MovingPlatform {
        private string m_MsgName;
        private NetworkedInfo m_NetworkInfo;
        private NetworkTransport m_Transport;
        private NetworkedSettingsAbstract m_Settings;
        private CustomMessagingManager m_CustomMessagingManager;
        protected override void Awake () {
            base.Awake ();
            m_NetworkInfo = GetComponent<NetworkedInfo> ();
            m_Settings = NetworkedManager.Instance.NetworkSettings;
            m_Transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            m_CustomMessagingManager = NetworkManager.Singleton.CustomMessagingManager;
        }
        /// <summary>
        /// The object has been enabled.
        /// </summary>
        protected override void OnEnable () {
            base.OnEnable ();
            if (m_NetworkInfo != null && m_NetworkInfo.IsServerHost ()) {
                NetworkManager.Singleton.OnClientConnectedCallback += ID => { OnEvent (ID); };
            }
        }
        private void Start () {
            m_MsgName = $"{m_NetworkInfo.NetworkBehaviourId}MsgClientNetworkedMovingPlatform{m_NetworkInfo.OwnerClientId}";
            if (m_NetworkInfo.IsLocalPlayer)
                m_CustomMessagingManager?.RegisterNamedMessageHandler (m_MsgName, (senderClientId, reader) => {
                    ByteUnpacker.ReadValuePacked (reader, out float time);
                    ByteUnpacker.ReadValuePacked (reader, out float delay);
                    ByteUnpacker.ReadValuePacked (reader, out float distance);
                    ByteUnpacker.ReadValuePacked (reader, out int state);
                    ByteUnpacker.ReadValuePacked (reader, out int point);
                    ByteUnpacker.ReadValuePacked (reader, out int direction);
                    ByteUnpacker.ReadValuePacked (reader, out int previous);
                    ByteUnpacker.ReadValuePacked (reader, out int count);
                    ByteUnpacker.ReadValuePacked (reader, out Vector3 target);
                    ByteUnpacker.ReadValuePacked (reader, out Vector3 position);
                    ByteUnpacker.ReadValuePacked (reader, out Quaternion facing);
                    ByteUnpacker.ReadValuePacked (reader, out Quaternion original);
                    ByteUnpacker.ReadValuePacked (reader, out Quaternion rotation);
                    InitializeMovingPlatformClientRpc (position, rotation, state, direction,
                        point, previous, distance, delay, original, time, target, facing, count);
                });
        }
        /// <summary>
        /// A event from the server has been sent.
        /// </summary>
        /// <param name="photonEvent">The server event.</param>
        public void OnEvent (ulong id) {
            var nextWaypointEventDelay = -1.0f;
            if (m_NextWaypointEvent != null) {
                nextWaypointEventDelay = m_NextWaypointEvent.EndTime - Time.time;
            }
            var activeStates = 0;
            for (int i = 0; i < States.Length - 1; i++) {
                if (States[i].Active) { activeStates |= (int) Mathf.Pow (i + 1, 2); }
            }

            using (var writer = new FastBufferWriter (
                FastBufferWriter.GetWriteSize (m_MoveTime) +
                FastBufferWriter.GetWriteSize (nextWaypointEventDelay) +
                FastBufferWriter.GetWriteSize (m_NextWaypointDistance) +
                FastBufferWriter.GetWriteSize (activeStates) +
                FastBufferWriter.GetWriteSize (m_NextWaypoint) +
                FastBufferWriter.GetWriteSize ((int) m_Direction) +
                FastBufferWriter.GetWriteSize (m_PreviousWaypoint) +
                FastBufferWriter.GetWriteSize (m_ActiveCharacterCount) +
                FastBufferWriter.GetWriteSize (m_TargetPosition) +
                FastBufferWriter.GetWriteSize (m_Transform.position) +
                FastBufferWriter.GetWriteSize (m_TargetRotation) +
                FastBufferWriter.GetWriteSize (m_OriginalRotation) +
                FastBufferWriter.GetWriteSize (m_Transform.rotation),
                Allocator.Temp)) {
                BytePacker.WriteValuePacked (writer, m_MoveTime);
                BytePacker.WriteValuePacked (writer, nextWaypointEventDelay);
                BytePacker.WriteValuePacked (writer, m_NextWaypointDistance);
                BytePacker.WriteValuePacked (writer, activeStates);
                BytePacker.WriteValuePacked (writer, m_NextWaypoint);
                BytePacker.WriteValuePacked (writer, (int) m_Direction);
                BytePacker.WriteValuePacked (writer, m_PreviousWaypoint);
                BytePacker.WriteValuePacked (writer, m_ActiveCharacterCount);
                BytePacker.WriteValuePacked (writer, m_TargetPosition);
                BytePacker.WriteValuePacked (writer, m_Transform.position);
                BytePacker.WriteValuePacked (writer, m_TargetRotation);
                BytePacker.WriteValuePacked (writer, m_OriginalRotation);
                BytePacker.WriteValuePacked (writer, m_Transform.rotation);
                m_CustomMessagingManager?.SendNamedMessage (m_MsgName, id, writer);
            }
        }
        /// <summary>
        /// Initialize the moving platform to the same parameters as the server.
        /// </summary>
        [ClientRpc]
        private void InitializeMovingPlatformClientRpc (Vector3 position, Quaternion rotation, int activeStates, int pathDirection, int nextWaypoint, int previousWaypoint, float nextWaypointDistance,
            float nextWaypointEventDelay, Quaternion originalRotation, float moveTime, Vector3 targetPosition, Quaternion targetRotation, int activeCharacterCount) {
            m_Transform.position = position;
            m_Transform.rotation = rotation;
            KinematicObjectManager.SetKinematicObjectPosition (KinematicObjectIndex, position);
            KinematicObjectManager.SetKinematicObjectRotation (KinematicObjectIndex, rotation);
            m_Direction = (PathDirection) pathDirection;
            m_NextWaypoint = nextWaypoint;
            m_PreviousWaypoint = previousWaypoint;
            m_NextWaypointDistance = nextWaypointDistance;
            m_OriginalRotation = originalRotation;
            m_MoveTime = moveTime;
            m_TargetPosition = targetPosition;
            m_TargetRotation = targetRotation;
            m_ActiveCharacterCount = activeCharacterCount;
            if (m_NextWaypointEvent != null) {
                Scheduler.Cancel (m_NextWaypointEvent);
                m_NextWaypointEvent = null;
            }
            if (nextWaypointEventDelay != -1) {
                m_NextWaypointEvent = Scheduler.ScheduleFixed (nextWaypointEventDelay, UpdateWaypoint);
            }
            // The states should match the master client.
            for (int i = 0; i < States.Length - 1; i++) {
                if (((int) Mathf.Pow (i + 1, 2) & activeStates) != 0) {
                    StateManager.SetState (m_GameObject, States[i].Name, true);
                }
            }

            // There will be a small amount of lag between the time that the RPC was sent on the server and the time that it was received on the client.
            // Make up for this difference by simulating the movement for the lag difference.
            var lag = Mathf.Abs (m_Transport.GetCurrentRtt (NetworkManager.Singleton.ServerClientId));
            var startTime = Time.time;

            var elapsedTime = 0f;
            while (elapsedTime < lag) {
                // The next waypoint event has to be simulated.
                if (m_NextWaypointEvent != null) {
                    if (startTime + elapsedTime > m_NextWaypointEvent.EndTime) {
                        UpdateWaypoint ();
                    }
                }
                Move ();
                elapsedTime += Time.fixedDeltaTime;
            }
            KinematicObjectManager.SetKinematicObjectPosition (KinematicObjectIndex, m_Transform.position);
            KinematicObjectManager.SetKinematicObjectRotation (KinematicObjectIndex, m_Transform.rotation);
        }
    }
}