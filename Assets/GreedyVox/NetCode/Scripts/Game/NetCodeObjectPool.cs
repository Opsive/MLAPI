using System.Collections.Generic;
using GreedyVox.NetCode.Interfaces;
using GreedyVox.NetCode.Utilities;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Game;
using Unity.Netcode;
using UnityEngine;
using static Opsive.Shared.Game.ObjectPoolBase;

namespace GreedyVox.NetCode.Game
{
    /// <summary>
    /// Manages synchronization of pooled objects over the network.
    /// </summary>
    public class NetCodeObjectPool : NetworkObjectPool
    {
        [Tooltip("An array of objects that can be spawned over the network. These objects will require manually custom pooling.")]
        [SerializeField] protected ObjectPoolDataAbstract[] m_InjectObjectPoolData;
        protected HashSet<GameObject> m_SpawnableGameObjects = new();
        protected HashSet<GameObject> m_SpawnedGameObjects = new();
        protected HashSet<GameObject> m_ActiveGameObjects = new();
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        protected virtual void Start()
        {
            SetupSpawnManager(FindObjectOfType<ObjectPool>()?.PreloadedPrefabs);
            foreach (var pool in m_InjectObjectPoolData)
                pool.InjectGameObject(m_SpawnableGameObjects);
        }
        /// <summary>
        /// Injects a GameObject into the pool manager for networked spawning.
        /// </summary>
        /// <param name="go">The original GameObject to be injected into the pool manager.</param>
        /// <param name="pool">Specifies whether to use the pool manager for this object.</param>
        public virtual void SetupSpawnManager(GameObject go, bool pool = true)
        {
            if (ComponentUtility.HasComponent<NetworkObject>(go))
                InjectSpawnManager(new NetCodeSpawnInstance(go, pool), go);
        }
        /// <summary>
        /// Injects multiple GameObjects into the pool manager for networked spawning.
        /// </summary>
        /// <param name="list">The array of prefabs to be injected into the pool manager.</param>
        /// <param name="pool">Specifies whether to use the pool manager for these objects.</param>
        public virtual void SetupSpawnManager(PreloadedPrefab[] list, bool pool = true)
        {
            if (list == null) return;
            foreach (var obj in list)
                SetupSpawnManager(obj.Prefab, pool);
        }
        /// <summary>
        /// Injects a GameObject into the pool manager for networked spawning.
        /// </summary>
        /// <param name="go">The original GameObject to be injected into the pool manager.</param>
        /// <param name="inject">The handler responsible for managing the instantiation and handling of networked prefabs.</param>
        /// <param name="pool">Specifies whether to use the pool manager for this object.</param>
        public virtual void InjectSpawnManager(INetworkPrefabInstanceHandler inject, GameObject go)
        {
            m_SpawnableGameObjects.Add(go);
            NetworkManager.Singleton.PrefabHandler.AddHandler(go, inject);
        }
        /// <summary>
        /// Spawns an object over the network without instantiating a new object on the local client.
        /// </summary>
        /// <param name="original">The original object the instance was created from.</param>
        /// <param name="instanceObject">The instance object created from the original object.</param>
        /// <param name="sceneObject">Indicates if the object is owned by the scene. If false, it will be owned by the character.</param>
        protected override void NetworkSpawnInternal(GameObject original, GameObject instanceObject, bool sceneObject)
        {
            if (m_SpawnableGameObjects.Contains(original))
            {
                if (!m_SpawnedGameObjects.Contains(instanceObject))
                    m_SpawnedGameObjects.Add(instanceObject);
                if (!m_ActiveGameObjects.Contains(instanceObject))
                    m_ActiveGameObjects.Add(instanceObject);
                if (NetworkManager.Singleton.IsServer)
                    instanceObject.GetCachedComponent<NetworkObject>()?.Spawn(sceneObject);
                else if (ComponentUtility.TryGet<IPayload>(instanceObject, out var dat))
                    NetCodeMessenger.Instance.ClientSpawnObject(original, dat);
                return;
            }
            Debug.LogError($"Error: Unable to spawn {original.name} on the network. Ensure the object has been added to the NetworkObjectPool.");
        }
        /// <summary>
        /// Destroys an object instance on the network.
        /// </summary>
        /// <param name="obj">The object to be destroyed.</param>
        protected override void DestroyInternal(GameObject obj)
        {
            if (ObjectPool.InstantiatedWithPool(obj))
                DestroyInternalExtended(obj);
            else if (NetworkManager.Singleton.IsServer
            && obj.TryGetComponent<NetworkObject>(out var net))
                net.Despawn();
            else GameObject.Destroy(obj);
        }
        /// <summary>
        /// Extends the destruction of the object.
        /// </summary>
        /// <param name="obj">The object to be destroyed.</param>
        protected virtual void DestroyInternalExtended(GameObject obj)
        {
            if (obj.TryGetComponent<NetworkObject>(out var net) && net.IsSpawned)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    net.Despawn();
                    m_SpawnedGameObjects.Remove(obj);
                }
                else if (NetworkManager.Singleton.IsClient)
                    NetCodeMessenger.Instance.ClientDespawnObject(net.NetworkObjectId);
            }
            else { ObjectPool.Destroy(obj); }
            m_ActiveGameObjects.Remove(obj);
        }
        /// <summary>
        /// Determines if the specified object was spawned using the network object pool.
        /// </summary>
        /// <param name="obj">The object instance to check.</param>
        /// <returns>True if the object was spawned using the network object pool, otherwise false.</returns>
        protected override bool SpawnedWithPoolInternal(GameObject obj) => m_SpawnedGameObjects.Contains(obj);
    }
}
