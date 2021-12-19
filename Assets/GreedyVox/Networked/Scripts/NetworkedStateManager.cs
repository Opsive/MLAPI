/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace GreedyVox.Networked {
    using System.Collections.Generic;
    using GreedVox.Networked.Collections;
    using Opsive.Shared.Events;
    using Opsive.Shared.Game;
    using Opsive.Shared.StateSystem;
    using Unity.Netcode;
    using UnityEngine;
    /// <summary>
    /// Ensures the states are synchronized when a new player joins the room.
    /// StateManager.SendStateChangeEvent must be enabled for this component to work.
    /// </summary>
    public class NetworkedStateManager : NetworkBehaviour {
        private Dictionary<ulong, HashSet<string>> m_ActiveCharacterStates;
        private NetworkedDictionary<ulong, NetworkObjectReference> m_Players;
        private NetworkedManager m_NetworkManager;
        private object[] m_EventData = new object[3];
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake () {
            m_NetworkManager = NetworkedManager.Instance;
            m_ActiveCharacterStates = new Dictionary<ulong, HashSet<string>> ();
            m_Players = new NetworkedDictionary<ulong, NetworkObjectReference> (128);
        }
        /// <summary>
        /// Registering events.
        /// </summary>
        private void OnEnable () {
            EventHandler.RegisterEvent<ulong> ("OnPlayerConnected", OnPlayerConnected);
            EventHandler.RegisterEvent<ulong> ("OnPlayerDisconnected", OnPlayerDisconnected);
            EventHandler.RegisterEvent<GameObject, string, bool> ("OnStateChange", OnStateChange);
        }
        /// <summary>
        /// Removing events.
        /// </summary>
        private void OnDisable () {
            EventHandler.UnregisterEvent<ulong> ("OnPlayerConnected", OnPlayerConnected);
            EventHandler.UnregisterEvent<ulong> ("OnPlayerDisconnected", OnPlayerDisconnected);
            EventHandler.UnregisterEvent<GameObject, string, bool> ("OnStateChange", OnStateChange);
        }
        /// <summary>
        /// Ensure StateManager.SendStateChangeEvent is true.
        /// </summary>
        private void Start () {
            var stateManager = GameObject.FindObjectOfType<StateManager> ();
            stateManager.SendStateChangeEvent = true;
            m_Players.OnDictionaryChanged += e => {
                if (NetworkedDictionaryEvent<ulong, NetworkObjectReference>.EventType.Add == e.Type) {
                    m_ActiveCharacterStates[e.Key] = new HashSet<string> ();
                } else if (NetworkedDictionaryEvent<ulong, NetworkObjectReference>.EventType.Remove == e.Type) {
                    m_ActiveCharacterStates.Remove (e.Key);
                }
            };
        }
        /// <summary>
        /// A player has disconnected. Perform any cleanup.
        /// </summary>
        /// <param name="player">The Player networking ID that disconnected.</param>
        private void OnPlayerDisconnected (ulong id) {
            m_ActiveCharacterStates.Remove (id);
            if (IsServer) m_Players.Remove (id);
        }
        /// <summary>
        /// A player has connected. Ensure the joining player is in sync with the current game state.
        /// </summary>
        /// <param name="id">The Player networking ID that connected.</param>
        private void OnPlayerConnected (ulong id) {
            var net = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject (id);
            if (IsServer) {
                m_Players.Add (id, net);
            } else {
                // Keep track of the character states for as long as the character is connected.
                foreach (var key in m_Players.Keys) {
                    m_ActiveCharacterStates.Add (key, new HashSet<string> ());
                }
            }
            // Ensure the new player has received all of the active events.
            m_EventData[2] = true;
            foreach (var activeStates in m_ActiveCharacterStates) {
                m_EventData[0] = activeStates.Key;
                foreach (var activestate in activeStates.Value) {
                    m_EventData[1] = activestate;
                    StateEventClientRpc (SerializerObjectArray.Serialize (m_EventData));
                }
            }
        }
        /// <summary>
        /// A state has changed. 
        /// </summary>
        /// <param name="character">The character that had the state change.</param>
        /// <param name="stateName">The name of the state that was changed.</param>
        /// <param name="active">Is the state active?</param>
        private void OnStateChange (GameObject character, string state, bool active) {
            HashSet<string> activeStates;
            var net = character.GetCachedComponent<NetworkObject> ();
            if (net != null && m_ActiveCharacterStates.TryGetValue (net.OwnerClientId, out activeStates)) {
                // Store the active states in a HashSet. This will be stored for all characters.
                if (active) { activeStates.Add (state); } else {
                    activeStates.Remove (state);
                }
                if (net.IsOwner) {
                    // Notify remote players of the state change for the local character.
                    m_EventData[0] = net.OwnerClientId;
                    m_EventData[1] = state;
                    m_EventData[2] = active;
                    if (IsServer) {
                        StateEventClientRpc (SerializerObjectArray.Serialize (m_EventData));
                    } else {
                        StateEventServerRpc (SerializerObjectArray.Serialize (m_EventData));
                    }
                }
            }
        }
        /// <summary>
        /// A event from state manager has been sent.
        /// </summary>
        /// <param name="SerializableObjectArray">The state event.</param>
        private void StateEventRpc (SerializableObjectArray dat) {
            var data = DeserializerObjectArray.Deserialize (dat);
            if (m_Players.TryGetValue ((ulong) data[0], out var go)) {
                if (go.TryGet (out var net, NetworkManager) && !net.IsOwner) {
                    StateManager.SetState (net.gameObject, (string) data[1], (bool) data[2]);
                }
            }
        }

        [ServerRpc (RequireOwnership = false)]
        private void StateEventServerRpc (SerializableObjectArray dat) {
            if (!IsClient) { StateEventRpc (dat); }
            StateEventClientRpc (dat);
        }

        [ClientRpc]
        private void StateEventClientRpc (SerializableObjectArray dat) {
            StateEventRpc (dat);
        }
    }
}