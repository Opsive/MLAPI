using MLAPI;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;
using Opsive.Shared.Game;
using Opsive.Shared.StateSystem;
using Opsive.UltimateCharacterController.Game;
using Opsive.UltimateCharacterController.Objects;
using UnityEngine;

/// <summary>
/// Syncronizes the moving platform when a new player joins the room.
/// </summary>
namespace GreedyVox.Networked {
    public class NetworkedMovingPlatform : MovingPlatform {
        private string m_MsgClient;
        private NetworkedInfo m_NetworkInfo;
        private NetworkTransport m_Transport;
        private NetworkedSettingsAbstract m_Settings;
        protected override void Awake () {
            base.Awake ();
            m_NetworkInfo = GetComponent<NetworkedInfo> ();
            m_Settings = NetworkedManager.Instance.NetworkSettings;
            m_Transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
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
            m_MsgClient = $"{m_NetworkInfo.NetworkBehaviourId}MsgClientNetworkedMovingPlatform{m_NetworkInfo.OwnerClientId}";
            if (m_NetworkInfo.IsLocalPlayer)
                CustomMessagingManager.RegisterNamedMessageHandler (m_MsgClient, (senderClientId, stream) => {
                    using (var reader = PooledNetworkReader.Get (stream)) {
                        InitializeMovingPlatformClientRpc (
                            reader.ReadVector3Packed (),
                            reader.ReadRotationPacked (),
                            reader.ReadInt32Packed (),
                            reader.ReadInt32Packed (),
                            reader.ReadInt32Packed (),
                            reader.ReadInt32Packed (),
                            reader.ReadSinglePacked (),
                            reader.ReadSinglePacked (),
                            reader.ReadRotationPacked (),
                            reader.ReadSinglePacked (),
                            reader.ReadVector3Packed (),
                            reader.ReadRotationPacked (),
                            reader.ReadInt32Packed ());
                    }
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
            using (var stream = PooledNetworkBuffer.Get ())
            using (var writer = PooledNetworkWriter.Get (stream)) {
                writer.WriteVector3Packed (m_Transform.position);
                writer.WriteRotationPacked (m_Transform.rotation);
                writer.WriteInt32Packed (activeStates);
                writer.WriteInt32Packed ((int) m_Direction);
                writer.WriteInt32Packed (m_NextWaypoint);
                writer.WriteInt32Packed (m_PreviousWaypoint);
                writer.WriteSinglePacked (m_NextWaypointDistance);
                writer.WriteSinglePacked (nextWaypointEventDelay);
                writer.WriteRotationPacked (m_OriginalRotation);
                writer.WriteSinglePacked (m_MoveTime);
                writer.WriteVector3Packed (m_TargetPosition);
                writer.WriteRotationPacked (m_TargetRotation);
                writer.WriteInt32Packed (m_ActiveCharacterCount);
                CustomMessagingManager.SendNamedMessage (m_MsgClient, id, stream, NetworkChannel.ChannelUnused);
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