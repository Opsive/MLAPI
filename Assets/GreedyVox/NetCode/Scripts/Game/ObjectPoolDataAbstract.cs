using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static Opsive.Shared.Game.ObjectPoolBase;

namespace GreedyVox.NetCode.Game
{
    public abstract class ObjectPoolDataAbstract : ScriptableObject
    {
        public abstract INetworkPrefabInstanceHandler GetNetworkPrefabInstanceHandler(GameObject go, bool pool = true);
        [field: SerializeField] public PreloadedPrefab[] SpawnablePrefabs { get; protected set; }
        protected virtual bool TryGetGameObject(PreloadedPrefab fab, out GameObject go) => (go = fab.Prefab) != null;
        /// <summary>
        /// Injects a GameObject into the pool manager for networked spawning.
        /// </summary>
        /// <param name="go">The original GameObject to be injected into the pool manager.</param>
        /// <param name="inject">The handler responsible for managing the instantiation and handling of networked prefabs.</param>
        protected virtual void InjectSpawnManager(INetworkPrefabInstanceHandler inject, GameObject go) =>
        NetworkManager.Singleton.PrefabHandler.AddHandler(go, inject);
        public virtual void InjectGameObject(HashSet<GameObject> dat)
        {
            foreach (var spawn in SpawnablePrefabs)
            {
                if (TryGetGameObject(spawn, out var go))
                {
                    InjectSpawnManager(GetNetworkPrefabInstanceHandler(go), go);
                    dat.Add(go);
                }
            }
        }
    }
}