using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Networking.Traits;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes the Interactable component over the network.
/// </summary>
namespace GreedyVox.NetCode.Traits
{
    public class NetCodeInteractableMonitor : NetworkBehaviour, INetworkInteractableMonitor
    {
        private Interactable m_Interactable;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake() => m_Interactable = gameObject.GetCachedComponent<Interactable>();
        /// <summary>
        /// Performs the interaction.
        /// </summary>
        /// <param name="character">The character that wants to interactact with the target.</param>
        /// <param name="interactAbility">The Interact ability that performed the interaction.</param>
        public void Interact(GameObject character, Interact interactAbility)
        {
            var net = character.GetCachedComponent<NetworkObject>();
            if (net == null)
            {
                Debug.LogError("Error: The character " + character.name + " must have a NetworkObject component.");
                return;
            }
            InteractRpc(net, interactAbility.Index);
        }
        /// <summary>
        /// Performs the interaction on the network.
        /// </summary>
        /// <param name="character">The character that performed the interaction.</param>
        /// <param name="abilityIndex">The index of the Interact ability that performed the interaction.</param>
        [Rpc(SendTo.NotMe)]
        private void InteractRpc(NetworkObjectReference character, int abilityIndex)
        {
            if (!character.TryGet(out var net)) return;
            var go = net.gameObject;
            var characterLocomotion = go.GetCachedComponent<UltimateCharacterLocomotion>();
            if (characterLocomotion != null)
            {
                var interact = characterLocomotion.GetAbility<Interact>(abilityIndex);
                m_Interactable.Interact(go, interact);
            }
        }
    }
}