using System.Collections.Generic;
using MLAPI;
using MLAPI.Serialization.Pooled;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Game;
using UnityEngine;

/// <summary>
/// Provides a way to synchronize pooled objects over the network.
/// </summary>
namespace GreedyVox.Networked {
    [DisallowMultipleComponent]
    public class NetworkedObjectPool : NetworkObjectPool {
        [SerializeField] private NetworkedMessenger m_NetworkedMessenger = default;
        [Tooltip ("An array of objects that can be spawned over the network. Any object that can be spawned on the network must be within this list.")]
        private NetworkObject m_NetworkObject;
        private NetworkedSettingsAbstract m_Settings;
        private HashSet<GameObject> m_SpawnedGameObjects = new HashSet<GameObject> ();
        private Dictionary<string, GameObject> m_ResourceCache = new Dictionary<string, GameObject> ();
        private Dictionary<GameObject, int> m_SpawnableGameObjectIDMap = new Dictionary<GameObject, int> ();
        private Dictionary<GameObject, ISpawnDataObject> m_ActiveGameObjects = new Dictionary<GameObject, ISpawnDataObject> ();
        /// Initialize the default values.
        /// </summary>
        private void Start () {
            m_Settings = NetworkedManager.Instance.NetworkSettings;
            var objs = NetworkManager.Singleton.NetworkConfig.NetworkPrefabs;
            for (int i = 0; i < objs.Count; i++) {
                if (objs[i] != null) {
                    m_SpawnableGameObjectIDMap.Add (objs[i].Prefab, i);
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
            if (!m_SpawnableGameObjectIDMap.TryGetValue (original, out var index)) {
                Debug.LogError ($"Error: Unable to spawn {original.name} on the network. Ensure the object has been added to the NetworkObjectPool.");
            } else {
                if (!m_SpawnedGameObjects.Contains (instanceObject)) {
                    m_SpawnedGameObjects.Add (instanceObject);
                }
                if (!m_ActiveGameObjects.ContainsKey (instanceObject)) {
                    m_ActiveGameObjects.Add (instanceObject, instanceObject.GetCachedComponent<ISpawnDataObject> ());
                }

                if (NetworkManager.Singleton.IsServer &&
                    (m_NetworkObject = instanceObject.GetCachedComponent<NetworkObject> ()) != null) {
                    // The pooled object can optionally provide initialization spawn data.
                    var data = instanceObject.GetCachedComponent<ISpawnDataObject> ();
                    if (data == null) {
                        m_NetworkObject.Spawn ();
                    } else {
                        // TODO: cause the object maybe already spawned, a pooled object will need to be handle                    
                        using (var stream = PooledNetworkBuffer.Get ())
                        using (var writer = PooledNetworkWriter.Get (stream)) {
                            data.SpawnData (writer);
                            m_NetworkObject.Spawn (stream);
                        }
                    }
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
            GameObject value;
            if (!m_ResourceCache.TryGetValue (prefabId, out value)) {
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