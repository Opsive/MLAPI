using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode.Game
{
    [CreateAssetMenu(fileName = "NewPoolingData", menuName = "GreedyVox/Data/Pooling/PoolingData")]
    public class ObjectPoolDataStandard : ObjectPoolDataAbstract
    {
        public override INetworkPrefabInstanceHandler GetNetworkPrefabInstanceHandler(GameObject go, bool pool = true)
        => new NetCodeSpawnInstance(go, pool);
    }
}