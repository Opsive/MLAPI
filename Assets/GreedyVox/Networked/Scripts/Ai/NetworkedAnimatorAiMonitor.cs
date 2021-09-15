using System.Collections.Generic;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Networking;
using UnityEngine;

/// <summary>
/// Synchronizes the Ultimate Character Controller animator across the network.
/// </summary>
namespace GreedyVox.Networked.Ai {
    [DisallowMultipleComponent]
    [RequireComponent (typeof (NetworkedSyncRate))]
    public class NetworkedAnimatorAiMonitor : AnimatorMonitor {
        /// <summary>
        /// Specifies which parameters are dirty.
        /// </summary>
        private enum ParameterDirtyFlags : short {
            HorizontalMovement = 1, // The Horizontal Movement parameter has changed.
            ForwardMovement = 2, // The Forward Movement parameter has changed.
            Pitch = 4, // The Pitch parameter has changed.
            Yaw = 8, // The Yaw parameter has changed.
            Speed = 16, // The Speed parameter has changed.
            Height = 32, // The Height parameter has changed.
            Moving = 64, // The Moving parameter has changed.
            Aiming = 128, // The Aiming parameter has changed.
            MovementSetID = 256, // The Movement Set ID parameter has changed.
            AbilityIndex = 512, // The Ability Index parameter has changed.
            AbilityIntData = 1024, // The Ability Int Data parameter has changed.
            AbilityFloatData = 2048 // The Ability Float Data parameter has changed.
        }
        private short m_DirtyFlag;
        private byte m_ItemDirtySlot;
        private string m_MsgAnimator;
        private INetworkInfo m_NetworkInfo;
        private NetworkedEvent m_NetworkEvent;
        private NetworkedSyncRate m_NetworkSync;
        private NetworkedManager m_NetworkManager;
        private int m_SnappedAbilityIndex = -1;
        private float m_NetworkYaw, m_NetworkPitch, m_NetworkSpeed;
        private float m_NetworkHorizontalMovement, m_NetworkForwardMovement, m_NetworkAbilityFloatData;
        protected override void Awake () {
            base.Awake ();
            m_NetworkManager = NetworkedManager.Instance;
            m_NetworkInfo = GetComponent<INetworkInfo> ();
            m_NetworkEvent = GetComponent<NetworkedEvent> ();
            m_NetworkSync = gameObject.GetCachedComponent<NetworkedSyncRate> ();

            m_NetworkEvent.NetworkStartEvent += OnNetworkStartEvent;
        }
        /// <summary>
        /// Verify the update mode of the animator.
        /// </summary>
        protected override void Start () {
            base.Start ();
            if (!m_NetworkEvent.IsOwner) {
                // Remote players do not move within the FixedUpdate loop.
                var animators = GetComponentsInChildren<Animator> (true);
                for (int i = 0; i < animators.Length; i++) {
                    animators[i].updateMode = AnimatorUpdateMode.Normal;
                }
            }
        }
        private void OnDisable () {
            m_NetworkSync.NetworkSyncEvent -= OnNetworkSyncEvent;
            m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent -= OnNetworkSyncUpdateEvent;
        }
        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        private void OnDestroy () {
            CustomMessagingManager.UnregisterNamedMessageHandler (m_MsgAnimator);
        }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup. Provides a Payload if it was provided
        /// </summary>
        private void OnNetworkStartEvent () {
            m_NetworkSync.NetworkSyncEvent += OnNetworkSyncEvent;
            m_MsgAnimator = $"{m_NetworkEvent.NetworkObjectId}MsgClientAnima{ m_NetworkEvent.OwnerClientId}";

            if (!m_NetworkInfo.IsServer ()) {
                m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent += OnNetworkSyncUpdateEvent;
                CustomMessagingManager.RegisterNamedMessageHandler (m_MsgAnimator, (sender, stream) => {
                    using (var reader = PooledNetworkReader.Get (stream)) {
                        SynchronizeParameters (reader);
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
                if (SynchronizeParameters (writer)) {
                    CustomMessagingManager.SendNamedMessage (m_MsgAnimator, clients, stream, NetworkChannel.ChannelUnused);
                }
            }
        }

        /// <summary>
        /// Reads/writes the continuous animator parameters.
        /// </summary>
        private void OnNetworkSyncUpdateEvent () {
            SetHorizontalMovementParameter (m_NetworkHorizontalMovement, 1);
            SetForwardMovementParameter (m_NetworkForwardMovement, 1);
            SetPitchParameter (m_NetworkPitch, 1);
            SetYawParameter (m_NetworkYaw, 1);
            SetSpeedParameter (m_NetworkSpeed, 1);
            SetAbilityFloatDataParameter (m_NetworkAbilityFloatData, 1);
        }
        /// <summary>
        /// Snaps the animator to the default values.
        /// </summary>
        protected override void SnapAnimator () {
            base.SnapAnimator ();
            m_SnappedAbilityIndex = AbilityIndex;
        }
        /// <summary>
        /// Sets the initial item parameter values.
        /// </summary>
        private void InitializeItemParameters (PooledNetworkWriter stream, int idx) {
            stream.WriteInt32Packed (idx);
            stream.WriteInt32Packed (ItemSlotID[idx]);
            stream.WriteInt32Packed (ItemSlotStateIndex[idx]);
            stream.WriteInt32Packed (ItemSlotSubstateIndex[idx]);
        }
        /// <summary>
        /// Gets the initial item parameter values.
        /// </summary>
        private void InitializeItemParameters (PooledNetworkReader stream) {
            var idx = stream.ReadInt32Packed ();
            SetItemIDParameter (idx, stream.ReadInt32Packed ());
            SetItemStateIndexParameter (idx, stream.ReadInt32Packed ());
            SetItemSubstateIndexParameter (idx, stream.ReadInt32Packed ());
            SnapAnimator ();
        }
        /// <summary>
        /// Sets the initial parameter values.
        /// </summary>
        private void InitializeParameters (PooledNetworkWriter stream) {
            stream.WriteSinglePacked (HorizontalMovement);
            stream.WriteSinglePacked (ForwardMovement);
            stream.WriteSinglePacked (Pitch);
            stream.WriteSinglePacked (Yaw);
            stream.WriteSinglePacked (Speed);
            stream.WriteInt32Packed (Height);
            stream.WriteBool (Moving);
            stream.WriteBool (Aiming);
            stream.WriteInt32Packed (MovementSetID);
            stream.WriteInt32Packed (AbilityIndex);
            stream.WriteInt32Packed (AbilityIntData);
            stream.WriteSinglePacked (AbilityFloatData);
        }
        /// <summary>
        /// Gets the initial parameter values.
        /// </summary>
        private void InitializeParameters (PooledNetworkReader stream) {
            SetHorizontalMovementParameter (stream.ReadSinglePacked (), 1);
            SetForwardMovementParameter (stream.ReadSinglePacked (), 1);
            SetPitchParameter (stream.ReadSinglePacked (), 1);
            SetYawParameter (stream.ReadSinglePacked (), 1);
            SetSpeedParameter (stream.ReadSinglePacked (), 1);
            SetHeightParameter (stream.ReadInt32Packed ());
            SetMovingParameter (stream.ReadBool ());
            SetAimingParameter (stream.ReadBool ());
            SetMovementSetIDParameter (stream.ReadInt32Packed ());
            SetAbilityIndexParameter (stream.ReadInt32Packed ());
            SetAbilityIntDataParameter (stream.ReadInt32Packed ());
            SetAbilityFloatDataParameter (stream.ReadSinglePacked (), 1);
            SnapAnimator ();
        }
        /// <summary>
        /// Called several times per second, so that your script can read synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being read from.</param>
        private void SynchronizeParameters (PooledNetworkReader stream) {
            var flag = stream.ReadInt16Packed ();
            if ((flag & (short) ParameterDirtyFlags.HorizontalMovement) != 0)
                m_NetworkHorizontalMovement = stream.ReadSinglePacked ();
            if ((flag & (short) ParameterDirtyFlags.ForwardMovement) != 0)
                m_NetworkForwardMovement = stream.ReadSinglePacked ();
            if ((flag & (short) ParameterDirtyFlags.Pitch) != 0)
                m_NetworkPitch = stream.ReadSinglePacked ();
            if ((flag & (short) ParameterDirtyFlags.Yaw) != 0)
                m_NetworkYaw = stream.ReadSinglePacked ();
            if ((flag & (short) ParameterDirtyFlags.Speed) != 0)
                m_NetworkSpeed = stream.ReadSinglePacked ();
            if ((flag & (short) ParameterDirtyFlags.Height) != 0)
                SetHeightParameter (stream.ReadInt32Packed ());
            if ((flag & (short) ParameterDirtyFlags.Moving) != 0)
                SetMovingParameter (stream.ReadBool ());
            if ((flag & (short) ParameterDirtyFlags.Aiming) != 0)
                SetAimingParameter (stream.ReadBool ());
            if ((flag & (short) ParameterDirtyFlags.MovementSetID) != 0)
                SetMovementSetIDParameter (stream.ReadInt32Packed ());
            if ((flag & (short) ParameterDirtyFlags.AbilityIndex) != 0) {
                var abilityIndex = stream.ReadInt32Packed ();
                // When the animator is snapped the ability index will be reset. 
                // It may take some time for that value to propagate across the network.
                // Wait to set the ability index until it is the correct reset value.
                if (m_SnappedAbilityIndex == -1 || abilityIndex == m_SnappedAbilityIndex) {
                    SetAbilityIndexParameter (abilityIndex);
                    m_SnappedAbilityIndex = -1;
                }
            }
            if ((flag & (short) ParameterDirtyFlags.AbilityIntData) != 0)
                SetAbilityIntDataParameter (stream.ReadInt32Packed ());
            if ((flag & (short) ParameterDirtyFlags.AbilityFloatData) != 0)
                m_NetworkAbilityFloatData = stream.ReadSinglePacked ();
            if (HasItemParameters) {
                var slot = (byte) stream.ReadInt16Packed ();
                for (int i = 0; i < ParameterSlotCount; ++i) {
                    if ((slot & (i + 1)) != 0) {
                        SetItemIDParameter (i, stream.ReadInt32Packed ());
                        SetItemStateIndexParameter (i, stream.ReadInt32Packed ());
                        SetItemSubstateIndexParameter (i, stream.ReadInt32Packed ());
                    }
                }
            }
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written.</param>
        private bool SynchronizeParameters (PooledNetworkWriter stream) {
            bool results = m_DirtyFlag > 0;
            stream.WriteInt16Packed (m_DirtyFlag);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.HorizontalMovement) != 0)
                stream.WriteSinglePacked (HorizontalMovement);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.ForwardMovement) != 0)
                stream.WriteSinglePacked (ForwardMovement);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Pitch) != 0)
                stream.WriteSinglePacked (Pitch);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Yaw) != 0)
                stream.WriteSinglePacked (Yaw);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Speed) != 0)
                stream.WriteSinglePacked (Speed);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Height) != 0)
                stream.WriteInt32Packed (Height);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Moving) != 0)
                stream.WriteBool (Moving);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Aiming) != 0)
                stream.WriteBool (Aiming);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.MovementSetID) != 0)
                stream.WriteInt32Packed (MovementSetID);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.AbilityIndex) != 0)
                stream.WriteInt32Packed (AbilityIndex);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.AbilityIntData) != 0)
                stream.WriteInt32Packed (AbilityIntData);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.AbilityFloatData) != 0)
                stream.WriteSinglePacked (AbilityFloatData);
            if (HasItemParameters) {
                stream.WriteInt16Packed (m_ItemDirtySlot);
                for (int i = 0; i < ParameterSlotCount; ++i) {
                    if ((m_ItemDirtySlot & (i + 1)) != 0) {
                        stream.WriteInt32Packed (ItemSlotID[i]);
                        stream.WriteInt32Packed (ItemSlotStateIndex[i]);
                        stream.WriteInt32Packed (ItemSlotSubstateIndex[i]);
                    }
                }
            }
            m_DirtyFlag = 0;
            m_ItemDirtySlot = 0;
            return results;
        }
        /// <summary>
        /// Sets the Horizontal Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetHorizontalMovementParameter (float value, float timeScale, float dampingTime) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetHorizontalMovementParameter (value, timeScale, dampingTime)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.HorizontalMovement;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Forward Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetForwardMovementParameter (float value, float timeScale, float dampingTime) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetForwardMovementParameter (value, timeScale, dampingTime)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.ForwardMovement;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Pitch parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetPitchParameter (float value, float timeScale, float dampingTime) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetPitchParameter (value, timeScale, dampingTime)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.Pitch;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Yaw parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetYawParameter (float value, float timeScale, float dampingTime) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetYawParameter (value, timeScale, dampingTime)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.Yaw;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Speed parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetSpeedParameter (float value, float timeScale, float dampingTime) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetSpeedParameter (value, timeScale, dampingTime)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.Speed;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Height parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetHeightParameter (int value) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetHeightParameter (value)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.Height;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Moving parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetMovingParameter (bool value) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetMovingParameter (value)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.Moving;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Aiming parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAimingParameter (bool value) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetAimingParameter (value)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.Aiming;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Movement Set ID parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetMovementSetIDParameter (int value) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetMovementSetIDParameter (value)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.MovementSetID;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Ability Index parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAbilityIndexParameter (int value) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetAbilityIndexParameter (value)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.AbilityIndex;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Int Data parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAbilityIntDataParameter (int value) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetAbilityIntDataParameter (value)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.AbilityIntData;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Ability Float parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAbilityFloatDataParameter (float value, float timeScale, float dampingTime) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetAbilityFloatDataParameter (value, timeScale, dampingTime)) {
                m_DirtyFlag |= (short) ParameterDirtyFlags.AbilityFloatData;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Item ID parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        public override bool SetItemIDParameter (int slotID, int value) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetItemIDParameter (slotID, value)) {
                m_ItemDirtySlot |= (byte) (slotID + 1);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Primary Item State Index parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetItemStateIndexParameter (int slotID, int value) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetItemStateIndexParameter (slotID, value)) {
                m_ItemDirtySlot |= (byte) (slotID + 1);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Item Substate Index parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetItemSubstateIndexParameter (int slotID, int value) {
            // The animator may not be enabled. Return silently.
            if (m_Animator != null && !m_Animator.isActiveAndEnabled) {
                return false;
            }
            if (base.SetItemSubstateIndexParameter (slotID, value)) {
                m_ItemDirtySlot |= (byte) (slotID + 1);
                return true;
            }
            return false;
        }
    }
}