using MLAPI;
using Opsive.UltimateCharacterController.Networking;
using UnityEngine;

/// <summary>
/// Contains information about the object on the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    [RequireComponent (typeof (NetworkObject))]
    public class NetworkedInfo : NetworkBehaviour, INetworkInfo {
        /// <summary>
        /// Is the networking implementation server or host? Cheat code for moving platforms
        /// </summary>
        /// <returns>True if the network transform is server or host.</returns>        
        public bool IsServerHost () {
            return IsServer || IsHost;
        }
        /// <summary>
        /// Is the networking implementation server authoritative?
        /// </summary>
        /// <returns>True if the network transform is server authoritative.</returns>        
        public bool IsServerAuthoritative () {
            return IsServer && !IsClient;
        }
        /// <summary>
        /// Is the game instance on the server?
        /// </summary>
        /// <returns>True if the game instance is on the server.</returns>
        bool INetworkInfo.IsServer () {
            return IsServer;
        }
        /// <summary>
        /// Is the character the local player?
        /// </summary>
        /// <returns>True if the character is the local player.</returns>
        bool INetworkInfo.IsLocalPlayer () {
            return IsOwner;
        }
        public bool IsPlayer () {
            return IsClient;
        }
    }
}