using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.Networked {
    public class NetworkedSpawnManager : INetworkPrefabInstanceHandler {
        private GameObject m_Prefab;
        private Transform m_Transform;
        public NetworkedSpawnManager (GameObject fab, Transform tran = null) {
            m_Prefab = fab;
            m_Transform = tran;
        }
        public void Destroy (NetworkObject net) {
            ObjectPool.Destroy (net.gameObject);
        }
        public NetworkObject Instantiate (ulong ID, Vector3 pos, Quaternion rot) {
            var go = ObjectPool.Instantiate (m_Prefab, pos, rot, m_Transform);
            go?.GetComponent<CharacterRespawner> ()?.Respawn (pos, rot, true);
            return go?.GetComponent<NetworkObject> ();
        }
    }
}