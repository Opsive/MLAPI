using Opsive.Shared.Game;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode.Game
{
    public class NetCodeSpawnInstance : INetworkPrefabInstanceHandler
    {
        private bool m_IsPooled;
        private GameObject m_Prefab;
        public NetCodeSpawnInstance(GameObject fab, bool pool = true)
        { m_Prefab = fab; m_IsPooled = pool; }
        public NetworkObject Instantiate(ulong ID, Vector3 pos, Quaternion rot)
        {
            var go = ObjectPoolBase.Instantiate(m_Prefab, pos, rot);
            return go?.GetComponent<NetworkObject>();
        }
        public void Destroy(NetworkObject net)
        {
            var go = net?.gameObject;
            if (m_IsPooled) ObjectPool.Destroy(go);
            else go?.SetActive(false);
        }
    }
}