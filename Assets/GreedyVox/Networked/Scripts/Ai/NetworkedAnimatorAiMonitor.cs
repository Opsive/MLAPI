using System.Collections.Generic;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Networking;
using Unity.Collections;
using Unity.Netcode;
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
        private int m_MaxBufferSize;
        private byte m_ItemDirtySlot;
        private string m_MsgName;
        private INetworkInfo m_NetworkInfo;
        private NetworkedEvent m_NetworkEvent;
        private NetworkedSyncRate m_NetworkSync;
        private NetworkedManager m_NetworkManager;
        private FastBufferWriter m_FastBufferWriter;
        private int m_SnappedAbilityIndex = -1;
        private CustomMessagingManager m_CustomMessagingManager;
        private float m_NetworkYaw, m_NetworkPitch, m_NetworkSpeed;
        private float m_NetworkHorizontalMovement, m_NetworkForwardMovement, m_NetworkAbilityFloatData;
        protected override void Awake () {
            base.Awake ();
            m_MaxBufferSize = MaxBufferSize ();
            m_NetworkManager = NetworkedManager.Instance;
            m_NetworkInfo = GetComponent<INetworkInfo> ();
            m_NetworkEvent = GetComponent<NetworkedEvent> ();
            m_NetworkSync = gameObject.GetCachedComponent<NetworkedSyncRate> ();
            m_CustomMessagingManager = NetworkManager.Singleton.CustomMessagingManager;

            m_NetworkEvent.NetworkSpawnEvent += OnNetworkSpawnEvent;
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
        /// <summary>
        /// Gets called when message handlers are ready to be unregistered.
        /// </summary>
        private void OnNetworkDespawnEvent () {
            m_NetworkSync.NetworkSyncEvent -= OnNetworkSyncEvent;
            m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent -= OnNetworkSyncUpdateEvent;
            m_CustomMessagingManager.UnregisterNamedMessageHandler (m_MsgName);
        }
        /// <summary>
        /// Gets called when message handlers are ready to be registered and the networking is setup. Provides a Payload if it was provided
        /// </summary>
        private void OnNetworkSpawnEvent () {
            m_NetworkSync.NetworkSyncEvent += OnNetworkSyncEvent;
            m_MsgName = $"{m_NetworkEvent.NetworkObjectId}MsgClientAnima{ m_NetworkEvent.OwnerClientId}";
            if (!m_NetworkInfo.IsServer ()) {
                m_NetworkManager.NetworkSettings.NetworkSyncUpdateEvent += OnNetworkSyncUpdateEvent;
                m_CustomMessagingManager.RegisterNamedMessageHandler (m_MsgName, (sender, reader) => {
                    SynchronizeParameters (ref reader);
                });
            }
        }
        /// <summary>
        /// Returns the maximus size for the fast buffer writer
        /// </summary>
        private int MaxBufferSize () {
            return sizeof (bool) * 2 + sizeof (short) * 2 + sizeof (int) * 4 +
                sizeof (float) * 6 + sizeof (int) * ParameterSlotCount * 3;
        }
        /// <summary>
        /// Network broadcast event called from the NetworkedSyncRate component
        /// </summary>
        private void OnNetworkSyncEvent (List<ulong> clients) {
            using (m_FastBufferWriter = new FastBufferWriter (FastBufferWriter.GetWriteSize (m_DirtyFlag), Allocator.Temp, m_MaxBufferSize)) {
                if (SynchronizeParameters ()) {
                    m_CustomMessagingManager.SendNamedMessage (m_MsgName, clients, m_FastBufferWriter, NetworkDelivery.ReliableSequenced);
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
        private void InitializeItemParameters (int idx) {
            BytePacker.WriteValuePacked (m_FastBufferWriter, idx);
            BytePacker.WriteValuePacked (m_FastBufferWriter, ItemSlotID[idx]);
            BytePacker.WriteValuePacked (m_FastBufferWriter, ItemSlotStateIndex[idx]);
            BytePacker.WriteValuePacked (m_FastBufferWriter, ItemSlotSubstateIndex[idx]);
        }
        /// <summary>
        /// Gets the initial item parameter values.
        /// </summary>
        private void InitializeItemParameters (ref FastBufferReader reader) {
            ByteUnpacker.ReadValuePacked (reader, out int idx);
            ByteUnpacker.ReadValuePacked (reader, out int id);
            ByteUnpacker.ReadValuePacked (reader, out int state);
            ByteUnpacker.ReadValuePacked (reader, out int index);
            SetItemIDParameter (idx, id);
            SetItemStateIndexParameter (idx, state);
            SetItemSubstateIndexParameter (idx, index);
            SnapAnimator ();
        }
        /// <summary>
        /// Sets the initial parameter values.
        /// </summary>
        private void InitializeParameters () {
            BytePacker.WriteValuePacked (m_FastBufferWriter, HorizontalMovement);
            BytePacker.WriteValuePacked (m_FastBufferWriter, ForwardMovement);
            BytePacker.WriteValuePacked (m_FastBufferWriter, Pitch);
            BytePacker.WriteValuePacked (m_FastBufferWriter, Yaw);
            BytePacker.WriteValuePacked (m_FastBufferWriter, Speed);
            BytePacker.WriteValuePacked (m_FastBufferWriter, Height);
            BytePacker.WriteValuePacked (m_FastBufferWriter, Moving);
            BytePacker.WriteValuePacked (m_FastBufferWriter, Aiming);
            BytePacker.WriteValuePacked (m_FastBufferWriter, MovementSetID);
            BytePacker.WriteValuePacked (m_FastBufferWriter, AbilityIndex);
            BytePacker.WriteValuePacked (m_FastBufferWriter, AbilityIntData);
            BytePacker.WriteValuePacked (m_FastBufferWriter, AbilityFloatData);
        }
        /// <summary>
        /// Gets the initial parameter values.
        /// </summary>
        private void InitializeParameters (ref FastBufferReader reader) {
            ByteUnpacker.ReadValuePacked (reader, out float horizontal);
            ByteUnpacker.ReadValuePacked (reader, out float forward);
            ByteUnpacker.ReadValuePacked (reader, out float pitch);
            ByteUnpacker.ReadValuePacked (reader, out float yaw);
            ByteUnpacker.ReadValuePacked (reader, out float speed);
            ByteUnpacker.ReadValuePacked (reader, out int height);
            ByteUnpacker.ReadValuePacked (reader, out bool moving);
            ByteUnpacker.ReadValuePacked (reader, out bool aiming);
            ByteUnpacker.ReadValuePacked (reader, out int move);
            ByteUnpacker.ReadValuePacked (reader, out int index);
            ByteUnpacker.ReadValuePacked (reader, out int dati);
            ByteUnpacker.ReadValuePacked (reader, out float datf);
            SetHorizontalMovementParameter (horizontal, 1);
            SetForwardMovementParameter (forward, 1);
            SetPitchParameter (pitch, 1);
            SetYawParameter (yaw, 1);
            SetSpeedParameter (speed, 1);
            SetHeightParameter (height);
            SetMovingParameter (moving);
            SetAimingParameter (aiming);
            SetMovementSetIDParameter (move);
            SetAbilityIndexParameter (index);
            SetAbilityIntDataParameter (dati);
            SetAbilityFloatDataParameter (datf, 1);
            SnapAnimator ();
        }
        /// <summary>
        /// Called several times per second, so that your script can read synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being read from.</param>
        private void SynchronizeParameters (ref FastBufferReader reader) {
            ByteUnpacker.ReadValuePacked (reader, out short flag);
            if ((flag & (short) ParameterDirtyFlags.HorizontalMovement) != 0)
                ByteUnpacker.ReadValuePacked (reader, out m_NetworkHorizontalMovement);
            if ((flag & (short) ParameterDirtyFlags.ForwardMovement) != 0)
                ByteUnpacker.ReadValuePacked (reader, out m_NetworkForwardMovement);
            if ((flag & (short) ParameterDirtyFlags.Pitch) != 0)
                ByteUnpacker.ReadValuePacked (reader, out m_NetworkPitch);
            if ((flag & (short) ParameterDirtyFlags.Yaw) != 0)
                ByteUnpacker.ReadValuePacked (reader, out m_NetworkYaw);
            if ((flag & (short) ParameterDirtyFlags.Speed) != 0)
                ByteUnpacker.ReadValuePacked (reader, out m_NetworkSpeed);
            if ((flag & (short) ParameterDirtyFlags.Height) != 0) {
                ByteUnpacker.ReadValuePacked (reader, out int value);
                SetHeightParameter (value);
            }
            if ((flag & (short) ParameterDirtyFlags.Moving) != 0) {
                ByteUnpacker.ReadValuePacked (reader, out bool value);
                SetMovingParameter (value);
            }
            if ((flag & (short) ParameterDirtyFlags.Aiming) != 0) {
                ByteUnpacker.ReadValuePacked (reader, out bool value);
                SetAimingParameter (value);
            }
            if ((flag & (short) ParameterDirtyFlags.MovementSetID) != 0) {
                ByteUnpacker.ReadValuePacked (reader, out int value);
                SetMovementSetIDParameter (value);
            }
            if ((flag & (short) ParameterDirtyFlags.AbilityIndex) != 0) {
                ByteUnpacker.ReadValuePacked (reader, out int abilityIndex);
                // When the animator is snapped the ability index will be reset. 
                // It may take some time for that value to propagate across the network.
                // Wait to set the ability index until it is the correct reset value.
                if (m_SnappedAbilityIndex == -1 || abilityIndex == m_SnappedAbilityIndex) {
                    SetAbilityIndexParameter (abilityIndex);
                    m_SnappedAbilityIndex = -1;
                }
            }
            if ((flag & (short) ParameterDirtyFlags.AbilityIntData) != 0) {
                ByteUnpacker.ReadValuePacked (reader, out int value);
                SetAbilityIntDataParameter (value);
            }
            if ((flag & (short) ParameterDirtyFlags.AbilityFloatData) != 0)
                ByteUnpacker.ReadValuePacked (reader, out m_NetworkAbilityFloatData);
            if (HasItemParameters) {
                int id, state, index;
                ByteUnpacker.ReadValuePacked (reader, out short slot);
                for (int i = 0; i < ParameterSlotCount; i++) {
                    if ((slot & (i + 1)) != 0) {
                        ByteUnpacker.ReadValuePacked (reader, out id);
                        SetItemIDParameter (i, id);
                        ByteUnpacker.ReadValuePacked (reader, out state);
                        SetItemStateIndexParameter (i, state);
                        ByteUnpacker.ReadValuePacked (reader, out index);
                        SetItemSubstateIndexParameter (i, index);
                    }
                }
            }
        }
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written.</param>
        /// <summary>
        /// Called several times per second, so that your script can write synchronization data.
        /// </summary>
        /// <param name="stream">The stream that is being written.</param>
        private bool SynchronizeParameters () {
            bool results = m_DirtyFlag > 0;
            BytePacker.WriteValuePacked (m_FastBufferWriter, m_DirtyFlag);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.HorizontalMovement) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, HorizontalMovement);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.ForwardMovement) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, ForwardMovement);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Pitch) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, Pitch);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Yaw) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, Yaw);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Speed) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, Speed);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Height) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, Height);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Moving) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, Moving);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.Aiming) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, Aiming);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.MovementSetID) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, MovementSetID);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.AbilityIndex) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, AbilityIndex);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.AbilityIntData) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, AbilityIntData);
            if ((m_DirtyFlag & (short) ParameterDirtyFlags.AbilityFloatData) != 0)
                BytePacker.WriteValuePacked (m_FastBufferWriter, AbilityFloatData);
            if (HasItemParameters) {
                BytePacker.WriteValuePacked (m_FastBufferWriter, m_ItemDirtySlot);
                for (int i = 0; i < ParameterSlotCount; i++) {
                    if ((m_ItemDirtySlot & (i + 1)) != 0) {
                        BytePacker.WriteValuePacked (m_FastBufferWriter, ItemSlotID[i]);
                        BytePacker.WriteValuePacked (m_FastBufferWriter, ItemSlotStateIndex[i]);
                        BytePacker.WriteValuePacked (m_FastBufferWriter, ItemSlotSubstateIndex[i]);
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