using Opsive.UltimateCharacterController.Character;

namespace GreedyVox.NetCode.Character
{
    /// <summary>
    /// Subclasses the AnimatorMonitor for NetCode synchronization.
    /// </summary>
    public class NetCodeAnimatorMonitor : AnimatorMonitor
    {
        private NetCodeCharacterAnimatorMonitor m_CharacterAnimatorMonitor;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_CharacterAnimatorMonitor = GetComponentInParent<NetCodeCharacterAnimatorMonitor>(true);
        }
        /// <summary>
        /// Snaps the animator to the default values.
        /// </summary>
        /// <param name="executeEvent">Should the animator snapped event be executed?</param>
        protected override void SnapAnimator(bool executeEvent)
        {
            base.SnapAnimator(executeEvent);
            m_CharacterAnimatorMonitor.AnimatorSnapped();
        }
        /// <summary>
        /// Sets the Horizontal Movement parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="timeScale">The time scale of the character.</param>
        /// <param name="dampingTime">The time allowed for the parameter to reach the value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetHorizontalMovementParameter(float value, float timeScale, float dampingTime)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetHorizontalMovementParameter(value, timeScale, dampingTime))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.HorizontalMovement;
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
        public override bool SetForwardMovementParameter(float value, float timeScale, float dampingTime)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetForwardMovementParameter(value, timeScale, dampingTime))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.ForwardMovement;
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
        public override bool SetPitchParameter(float value, float timeScale, float dampingTime)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetPitchParameter(value, timeScale, dampingTime))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.Pitch;
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
        public override bool SetYawParameter(float value, float timeScale, float dampingTime)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetYawParameter(value, timeScale, dampingTime))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.Yaw;
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
        public override bool SetSpeedParameter(float value, float timeScale, float dampingTime)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetSpeedParameter(value, timeScale, dampingTime))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.Speed;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Height parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetHeightParameter(float value)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetHeightParameter(value))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.Height;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Moving parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetMovingParameter(bool value)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetMovingParameter(value))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.Moving;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Aiming parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAimingParameter(bool value)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetAimingParameter(value))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.Aiming;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Movement Set ID parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetMovementSetIDParameter(int value)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetMovementSetIDParameter(value))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.MovementSetID;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Ability Index parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAbilityIndexParameter(int value)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetAbilityIndexParameter(value))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.AbilityIndex;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Int Data parameter to the specified value.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetAbilityIntDataParameter(int value)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetAbilityIntDataParameter(value))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.AbilityIntData;
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
        public override bool SetAbilityFloatDataParameter(float value, float timeScale, float dampingTime)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetAbilityFloatDataParameter(value, timeScale, dampingTime))
            {
                m_CharacterAnimatorMonitor.DirtyFlag |= (short)NetCodeCharacterAnimatorMonitor.ParameterDirtyFlags.AbilityFloatData;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Item ID parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        public override bool SetItemIDParameter(int slotID, int value)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetItemIDParameter(slotID, value))
            {
                m_CharacterAnimatorMonitor.ItemDirtySlot |= (byte)(slotID + 1);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Primary Item State Index parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        /// <param name="forceChange">Force the change the new value?</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetItemStateIndexParameter(int slotID, int value, bool forceChange)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetItemStateIndexParameter(slotID, value, forceChange))
            {
                m_CharacterAnimatorMonitor.ItemDirtySlot |= (byte)(slotID + 1);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sets the Item Substate Index parameter with the indicated slot to the specified value.
        /// </summary>
        /// <param name="slotID">The slot that the item occupies.</param>
        /// <param name="value">The new value.</param>
        /// <param name="forceChange">Force the change the new value?</param>
        /// <returns>True if the parameter was changed.</returns>
        public override bool SetItemSubstateIndexParameter(int slotID, int value, bool forceChange)
        {
            // The animator may not be enabled. Return silently.
            if (m_Animator.isActiveAndEnabled
            && base.SetItemSubstateIndexParameter(slotID, value, forceChange))
            {
                m_CharacterAnimatorMonitor.ItemDirtySlot |= (byte)(slotID + 1);
                return true;
            }
            return false;
        }
    }
}