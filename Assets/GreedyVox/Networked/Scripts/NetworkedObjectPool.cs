using System.Collections.Generic;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Game;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Provides a way to synchronize pooled objects over the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedObjectPool : NetworkObjectPool {
        [SerializeField] private NetworkedMessenger m_NetworkedMessenger = default;
        [Tooltip ("An array of objects that can be spawned over the network. Any object that can be spawned on the network must be within this list.")]
        [SerializeField] private HashSet<GameObject> m_SpawnableGameObjects = new HashSet<GameObject> ();
        private NetworkObject m_NetworkObject;
        private HashSet<GameObject> m_ActiveGameObjects = new HashSet<GameObject> ();
        private HashSet<GameObject> m_SpawnedGameObjects = new HashSet<GameObject> ();
        private Dictionary<string, GameObject> m_ResourceCache = new Dictionary<string, GameObject> ();
        /// Initialize the default values.
        /// </summary>
        private void Start () {
            GameObject go;
            var pool = FindObjectOfType<ObjectPool> ()?.PreloadedPrefabs;
            for (int i = 0; i < pool?.Length; i++) {
                go = pool[i].Prefab;
                if (go != null && go.GetComponent<NetworkObject> () != null) {
                    m_SpawnableGameObjects.Add (go);
                    NetworkManager.Singleton.PrefabHandler.AddHandler (go,
                        new NetworkedSpawnManager (go, transform));
                }
            }
        }
        /// <summary>
        /// Internal method which spawns the object over the network. This does not instantiate a new object on the local client.
        /// </summary>
        /// <param name="original">The object that the object was instantiated from.</param>
        /// <param name="instanceObject">The object that was instantiated from the original object.</param>
        /// <param name="sceneObject">Is the object owned by the scene? If fales it will be owned by the character.</param>
        protected override void NetworkSpawnInternal (GameObject original, GameObject instanceObject, bool sceneObject) {
            if (!m_SpawnableGameObjects.Contains (original)) {
                Debug.LogError ($"Error: Unable to spawn {original.name} on the network. Ensure the object has been added to the NetworkObjectPool.");
            } else {
                if (!m_SpawnedGameObjects.Contains (instanceObject)) {
                    m_SpawnedGameObjects.Add (instanceObject);
                }
                if (!m_ActiveGameObjects.Contains (instanceObject)) {
                    m_ActiveGameObjects.Add (instanceObject);
                }
                if (NetworkManager.Singleton.IsServer) {
                    instanceObject.GetCachedComponent<NetworkObject> ()?.Spawn ();
                }
            }
        }
        /// <summary>
        /// Internal method which destroys the object instance on the network.
        /// </summary>
        /// <param name="obj">The object to destroy.</param>
        protected override void DestroyInternal (GameObject obj) {
            if (ObjectPool.InstantiatedWithPool (obj)) { DestroyInternalExtended (obj); } else {
                GameObject.Destroy (obj);
            }
        }
        /// <summary>
        /// Destroys the object.
        /// </summary>
        /// <param name="obj">The object that should be destroyed.</param>
        protected virtual void DestroyInternalExtended (GameObject obj) {
            if ((m_NetworkObject = obj.GetComponent<NetworkObject> ()) != null && m_NetworkObject.IsSpawned) {
                if (NetworkManager.Singleton.IsServer) {
                    m_NetworkObject.Despawn ();
                } else {
                    m_NetworkedMessenger?.ClientDespawnObject (m_NetworkObject.NetworkObjectId);
                }
            }
            if (m_NetworkObject == null || !m_NetworkObject.IsSpawned) {
                m_ActiveGameObjects.Remove (obj);
                ObjectPool.Destroy (obj);
            }
        }
        /// <summary>
        /// Called to get an instance of a prefab. Must return valid, disabled GameObject with PhotonView. Required by IPunPrefabPool.
        /// </summary>
        /// <param name="prefabId">The id of this prefab.</param>
        /// <param name="position">The position for the instance.</param>
        /// <param name="rotation">The rotation for the instance.</param>
        /// <returns>A disabled instance to use by Networking or null if the prefabId is unknown.</returns>
        public GameObject Instantiate (string prefabId, Vector3 position, Quaternion rotation) {
            if (!m_ResourceCache.TryGetValue (prefabId, out var value)) {
                value = (GameObject) Resources.Load (prefabId, typeof (GameObject));
                m_ResourceCache.Add (prefabId, value);
            }
            // Networking requires the instantiated object to be deactivated.
            var obj = ObjectPool.Instantiate (value, position, rotation);
            obj?.SetActive (true);
            return obj;
        }
        /// Internal method which returns if the specified object was spawned with the network object pool.
        /// </summary>
        /// <param name="obj">The object instance to determine if was spawned with the object pool.</param>
        /// <returns>True if the object was spawned with the network object pool.</returns>
        protected override bool SpawnedWithPoolInternal (GameObject obj) {
            return m_SpawnedGameObjects.Contains (obj);
        }
    }
}