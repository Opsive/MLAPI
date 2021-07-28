using MLAPI;
using MLAPI.Spawning;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Traits;
using UnityEngine;

namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedSpawnManager : NetworkBehaviour {
        public override void NetworkStart () {
            if (!NetworkManager.Singleton.IsServer) {
                GameObject go;
                foreach (var fab in NetworkManager.Singleton.NetworkConfig.NetworkPrefabs) {
                    if (!fab.PlayerPrefab) {
                        NetworkSpawnManager.RegisterDestroyHandler (NetworkSpawnManager.GetPrefabHashFromGenerator (fab?.Prefab.name), (net) => {
                            NetworkedObjectPool.Destroy (net?.gameObject);
                        });
                        NetworkSpawnManager.RegisterSpawnHandler (NetworkSpawnManager.GetPrefabHashFromGenerator (fab?.Prefab.name), (pos, rot) => {
                            go = ObjectPool.Instantiate (fab?.Prefab, pos, rot, transform);
                            go?.GetComponent<CharacterRespawner> ()?.Respawn (pos, rot, true);
                            return go?.GetComponent<NetworkObject> ();
                        });
                    }
                }
            }
        }
    }
}