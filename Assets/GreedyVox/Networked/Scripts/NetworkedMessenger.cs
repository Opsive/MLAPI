using Opsive.UltimateCharacterController.Networking.Game;
using Unity.Collections;
using Unity.Netcode;

namespace GreedyVox.Networked {
    public class NetworkedMessenger : NetworkBehaviour {
        private CustomMessagingManager m_CustomMessagingManager;
        private const string MsgServerName = "MsgServerDespawnObject";
        public override void OnNetworkDespawn () {
            m_CustomMessagingManager.UnregisterNamedMessageHandler (MsgServerName);
        }
        public override void OnNetworkSpawn () {
            m_CustomMessagingManager = NetworkManager.Singleton.CustomMessagingManager;
            if (IsServer) {
                // Listening for client side network pooling calls, then forwards message to despawn the object.
                m_CustomMessagingManager.RegisterNamedMessageHandler (MsgServerName, (sender, reader) => {
                    ByteUnpacker.ReadValuePacked (reader, out ulong id);
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue (id, out var net) &&
                        NetworkObjectPool.IsNetworkActive ()) {
                        NetworkObjectPool.Destroy (net.gameObject);
                    }
                });
            }
        }
        public void ClientDespawnObject (ulong id) {
            // Client sending custom message to the server using the Networked Messagenger.
            using (var writer = new FastBufferWriter (FastBufferWriter.GetWriteSize (id), Allocator.Temp)) {
                BytePacker.WriteValuePacked (writer, id);
                m_CustomMessagingManager.SendNamedMessage (MsgServerName, NetworkManager.Singleton.ServerClientId, writer);
            }
        }
    }
}