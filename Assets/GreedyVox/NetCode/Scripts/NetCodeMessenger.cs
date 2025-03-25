using GreedyVox.NetCode.Game;
using GreedyVox.NetCode.Interfaces;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Game;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode
{
    /// <summary>
    /// Ensure that the NetworkManager is set up in a previous scene for the server and client.
    /// This setup acts as a bootstrapper, initializing the NetworkManager.Singleton beforehand.
    /// </summary>
    public class NetCodeMessenger : NetworkBehaviour
    {
        private static NetCodeMessenger _Instance;
        public static NetCodeMessenger Instance { get { return _Instance; } }
        private const string MsgServerNameDespawn = "MsgServerDespawnObject";
        private const string MsgServerNameSpawn = "MsgServerSpawnObject";
        private ObjectPoolBase.PreloadedPrefab[] m_PreloadedPrefabs;
        private CustomMessagingManager m_CustomMessagingManager;
        /// <summary>
        /// The object has awaken.
        /// </summary>
        private void Awake()
        {
            if (_Instance != null && _Instance != this)
                Destroy(this.gameObject);
            else _Instance = this;
            m_PreloadedPrefabs ??= FindObjectOfType<ObjectPool>()?.PreloadedPrefabs;
        }
        private void OnEnable()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientStarted += NetworkStarting;
            NetworkManager.Singleton.OnServerStarted += NetworkStarting;
        }
        private void OnDisable()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientStarted -= NetworkStarting;
            NetworkManager.Singleton.OnServerStarted -= NetworkStarting;
            m_CustomMessagingManager?.UnregisterNamedMessageHandler(MsgServerNameDespawn);
        }
        private void NetworkStarting()
        {
            if (NetworkManager.Singleton == null) return;
            m_PreloadedPrefabs ??= FindObjectOfType<ObjectPool>()?.PreloadedPrefabs;
            m_CustomMessagingManager ??= NetworkManager.Singleton.CustomMessagingManager;
            if (NetworkManager.Singleton.IsServer)
            {
                // Listening for client side network pooling calls, then forwards message to despawn the object.
                m_CustomMessagingManager?.RegisterNamedMessageHandler(MsgServerNameDespawn, (sender, reader) =>
                {
                    ByteUnpacker.ReadValuePacked(reader, out ulong id);
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var net)
                     && NetworkObjectPool.IsNetworkActive())
                        NetworkObjectPool.Destroy(net.gameObject);
                });
                // Listening for client side network pooling calls, then forwards message to spawn the object.
                m_CustomMessagingManager?.RegisterNamedMessageHandler(MsgServerNameSpawn, (sender, reader) =>
                {
                    reader.ReadValueSafe(out int idx);
                    if (TryGetNetworkPoolObject(idx, out var go))
                    {
                        var spawn = ObjectPoolBase.Instantiate(go);
                        spawn?.GetComponent<IPayload>()?.PayLoad(reader);
                        NetCodeObjectPool.NetworkSpawn(go, spawn, true);
                    }
                });
            }
        }
        /// <summary>
        /// Listening for client side network pooling calls, then forwards message to spawn the object.
        /// </summary>
        public void ClientSpawnObject(GameObject go, IPayload dat)
        {
            // Client sending custom message to the server using the NetCode Messagenger.
            if (TryGetNetworkPoolObjectIndex(go, out var idx) && dat.PayLoad(ref idx, out var writer))
                m_CustomMessagingManager?.SendNamedMessage(MsgServerNameSpawn,
                NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        }
        /// <summary>
        /// Listening for client side network pooling calls, then forwards message to despawn the object.
        /// </summary>
        public void ClientDespawnObject(ulong id)
        {
            // Client sending custom message to the server using the NetCode Messagenger.
            using var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(id), Allocator.Temp);
            BytePacker.WriteValuePacked(writer, id);
            m_CustomMessagingManager?.SendNamedMessage(
                MsgServerNameDespawn, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        }
        /// <summary>
        /// Find the index of the GameObject inside the pooling list
        /// </summary>
        public bool TryGetNetworkPoolObjectIndex(GameObject go, out int idx)
        {
            for (idx = 0; idx < m_PreloadedPrefabs?.Length; idx++)
                if (m_PreloadedPrefabs[idx].Prefab == go)
                    return true;
            idx = default;
            return false;
        }
        /// <summary>
        /// Find the GameObject index inside the pooling list
        /// </summary>
        public bool TryGetNetworkPoolObject(int idx, out GameObject go)
        {
            if (idx > -1 && idx < m_PreloadedPrefabs?.Length)
            {
                go = m_PreloadedPrefabs[idx].Prefab;
                return true;
            }
            go = default;
            return false;
        }
    }
}