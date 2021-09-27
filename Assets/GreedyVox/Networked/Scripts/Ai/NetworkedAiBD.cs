using BehaviorDesigner.Runtime;
using MLAPI;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;

/// <summary>
/// Ai Behavior Designer for running on server only.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedAiBD : NetworkBehaviour {
        private BehaviorTree m_BehaviorTree;
        private UltimateCharacterLocomotion m_Locomotion;
        private void Awake () {
            m_BehaviorTree = GetComponent<BehaviorTree> ();
            m_Locomotion = GetComponent<UltimateCharacterLocomotion> ();
        }
        public override void NetworkStart () {
            if (m_Locomotion != null) { m_Locomotion.enabled = IsServer; }
            if (m_BehaviorTree != null) { m_BehaviorTree.enabled = IsServer; }
        }
    }
}