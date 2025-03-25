/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace GreedyVox.NetCode
{
    using System.Collections.Generic;
    using Opsive.Shared.Events;
    using Opsive.Shared.Game;
    using Opsive.Shared.StateSystem;
    using Unity.Netcode;
    using UnityEngine;
    /// <summary>
    /// Ensures the states are synchronized when a new player joins the room.
    /// StateManager.SendStateChangeEvent must be enabled for this component to work.
    /// </summary>
    public class NetCodeStateManager : NetworkBehaviour
    {
        private Dictionary<NetworkObjectReference, HashSet<string>> m_ActiveCharacterStates;
        // private NetCodeManager m_NetworkManager;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake() =>
        m_ActiveCharacterStates = new Dictionary<NetworkObjectReference, HashSet<string>>();
        /// <summary>
        /// Registering events.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent<GameObject, string, bool>("OnStateChange", OnStateChange);
            EventHandler.RegisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
            EventHandler.RegisterEvent<ulong, NetworkObjectReference>("OnPlayerDisconnected", OnPlayerDisconnected);
        }
        /// <summary>
        /// Removing events.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent<GameObject, string, bool>("OnStateChange", OnStateChange);
            EventHandler.UnregisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
            EventHandler.UnregisterEvent<ulong, NetworkObjectReference>("OnPlayerDisconnected", OnPlayerDisconnected);
        }
        /// <summary>
        /// Ensure StateManager.SendStateChangeEvent is true.
        /// </summary>
        private void Start()
        {
            var stateManager = GameObject.FindObjectOfType<StateManager>();
            stateManager.SendStateChangeEvent = true;
        }
        /// <summary>
        /// A player has disconnected. Perform any cleanup.
        /// </summary>
        /// <param name="id">The Client networking id that entered the room.</param>
        /// <param name="obj">The Player NetworkObject that disconnected.</param>
        private void OnPlayerDisconnected(ulong id, NetworkObjectReference obj)
        {
            if (obj.TryGet(out var net))
                m_ActiveCharacterStates.Remove(net?.gameObject);
        }
        /// <summary>
        /// A player has connected. Ensure the joining player is in sync with the current game state.
        /// </summary>
        /// <param name="id">The Client networking id that connected.</param>
        /// <param name="obj">The Player NetworkObject that connected.</param>
        private void OnPlayerConnected(ulong id, NetworkObjectReference obj)
        {
            // If isn't the Server/Host then we should early return here!
            if (!IsServer) return;
            // var client = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { id } } };
            // Ensure the new player has received all of the active events.
            foreach (var activeStates in m_ActiveCharacterStates)
                foreach (var activestate in activeStates.Value)
                    // StateEventClientRpc(activeStates.Key, activestate, true, client);
                    StateEventRpc(activeStates.Key, activestate, true);
            // Keep track of the character states for as long as the character is within the room.
            if (obj.TryGet(out var net))
                m_ActiveCharacterStates.Add(net, new HashSet<string>());
        }
        /// <summary>
        /// A state has changed. 
        /// </summary>
        /// <param name="character">The character that had the state change.</param>
        /// <param name="stateName">The name of the state that was changed.</param>
        /// <param name="active">Is the state active?</param>        
        private void OnStateChange(GameObject character, string state, bool active)
        {
            var net = character.GetCachedComponent<NetworkObject>();
            if (net == null || !net.IsSpawned) return;
            if (m_ActiveCharacterStates.TryGetValue(net, out HashSet<string> activeStates))
            {
                // Store the active states in a HashSet. This will be stored for all characters.
                if (active) activeStates.Add(state);
                else activeStates.Remove(state);
                // Notify remote players of the state change for the local character.
                StateEventRpc(net, state, active);
            }
        }
        /// <summary>
        /// A event from state manager has been sent.
        /// </summary>
        /// <param name="SerializableObjectArray">The state event.</param>
        [Rpc(SendTo.NotOwner)]
        private void StateEventRpc(NetworkObjectReference obj, string state, bool active)
        {
            if (obj.TryGet(out var net))
                StateManager.SetState(net.gameObject, state, active);
        }
    }
}