using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Traits;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.Networked {
    public class NetworkedSpawnManager : INetworkPrefabInstanceHandler {
        private bool m_IsPooled;
        private GameObject m_Prefab;
        private Transform m_Transform;
        public NetworkedSpawnManager (GameObject fab, Transform tran = null, bool pool = true) {
            m_Prefab = fab;
            m_Transform = tran;
            m_IsPooled = pool;
        }
        public NetworkObject Instantiate (ulong ID, Vector3 pos, Quaternion rot) {
            var go = ObjectPool.Instantiate (m_Prefab, pos, rot, m_Transform);
            go?.GetComponent<CharacterRespawner> ()?.Respawn (pos, rot, true);
            return go?.GetComponent<NetworkObject> ();
        }
        public void Destroy (NetworkObject net) {
            var go = net.gameObject;
            if (m_IsPooled) {
                ObjectPool.Destroy (go);
            } else if (NetworkManager.Singleton.IsServer) {
                go.SetActive (false);
            } else {
                GameObject.Destroy (go);
            }
        }
    }
}