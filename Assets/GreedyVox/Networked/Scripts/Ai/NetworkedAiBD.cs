using BehaviorDesigner.Runtime;
using Opsive.UltimateCharacterController.Character;
using Unity.Netcode;
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
        public override void OnNetworkSpawn () {
            if (m_Locomotion != null) { m_Locomotion.enabled = IsServer; }
            if (m_BehaviorTree != null) { m_BehaviorTree.enabled = IsServer; }
        }
    }
}