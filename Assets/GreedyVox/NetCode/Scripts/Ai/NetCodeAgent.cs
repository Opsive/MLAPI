using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities.AI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace GreedyVox.NetCode.Ai
{
    /// <summary>
    /// Basic testing class for NavMeshAgent and UCC
    /// </summary>
    public class NetCodeAgent : NetworkBehaviour
    {
        private NavMeshAgent m_NavMeshAgent;
        private NavMeshAgentMovement m_NavMeshAgentMovement;
        /// <summary>
        /// Initailizes the default values.
        /// </summary>
        private void Start()
        {
            m_NavMeshAgent = GetComponent<NavMeshAgent>();
            var UCC = gameObject.GetCachedComponent<UltimateCharacterLocomotion>();
            m_NavMeshAgentMovement = UCC?.GetAbility<NavMeshAgentMovement>();
            if (IsServer)
                SetNextDestination();
            else enabled = false;
        }
        /// <summary>
        /// Traverses the random waypoints.
        /// </summary>
        private void Update()
        {
            if (m_NavMeshAgent.isStopped)
                SetNextDestination();
            if (UnityEngine.Input.GetMouseButtonDown(0))
                SetDestinationToMousePosition();
        }
        /// <summary>
        /// Sets the next NavMeshAgent destination.
        /// </summary>
        private void SetNextDestination()
        {
            var des = UnityEngine.Random.insideUnitCircle * 10.0f;
            Debug.LogFormat("Nav Mesh Agent Success? {0}", m_NavMeshAgentMovement.SetDestination(new Vector3(des.x, 0.0f, des.y)));
        }
        private void SetDestinationToMousePosition()
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition), out var hit))
                Debug.LogFormat("Nav Mesh Agent Success? {0}", m_NavMeshAgentMovement.SetDestination(hit.point));
        }
    }
}
