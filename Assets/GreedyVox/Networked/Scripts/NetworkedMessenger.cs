using MLAPI;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using MLAPI.Spawning;
using MLAPI.Transports;
using Opsive.UltimateCharacterController.Networking.Game;

namespace GreedyVox.Networked {
    public class NetworkedMessenger : NetworkBehaviour {
        private const string MsgServerDespawnObject = "MsgServerDespawnObject";
        private void OnDestroy () {
            CustomMessagingManager.UnregisterNamedMessageHandler (MsgServerDespawnObject);
        }
        public override void NetworkStart () {
            if (IsServer) {
                // Listening for client side network pooling calls, then forwards message to despawn the object.
                CustomMessagingManager.RegisterNamedMessageHandler (MsgServerDespawnObject, (sender, stream) => {
                    using (PooledNetworkReader reader = PooledNetworkReader.Get (stream)) {
                        if (NetworkSpawnManager.SpawnedObjects.TryGetValue (reader.ReadUInt64Packed (), out var net) &&
                            NetworkObjectPool.IsNetworkActive ()) {
                            NetworkObjectPool.Destroy (net.gameObject);
                        }
                    }
                });
            }
        }
        public void ClientDespawnObject (ulong id) {
            using (var stream = PooledNetworkBuffer.Get ()) {
                using (var writer = PooledNetworkWriter.Get (stream)) {
                    writer.WriteUInt64Packed (id);
                    // Client sending custom message to the server using the Networked Messagenger.
                    CustomMessagingManager.SendNamedMessage (
                        MsgServerDespawnObject,
                        NetworkManager.Singleton.ServerClientId,
                        stream,
                        NetworkChannel.DefaultMessage
                    );
                }
            }
        }
    }
}