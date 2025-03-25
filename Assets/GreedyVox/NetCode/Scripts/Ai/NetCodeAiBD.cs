#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER_BD_AI
using BehaviorDesigner.Runtime;
#endif
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ai Behavior Designer for running on server only.
/// </summary>
namespace GreedyVox.NetCode.Ai
{
    [DisallowMultipleComponent]
    public class NetCodeAiBD : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER_BD_AI
            if (TryGetComponent<BehaviorTree>(out var tree))
                tree.enabled = IsServer;
#endif
            if (TryGetComponent<NavMeshAgent>(out var agent))
                agent.enabled = IsServer;
            base.OnNetworkSpawn();
        }
    }
}