using MLAPI;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;
using UnityEngine;

namespace GreedyVox.Networked {
    public class NetworkedPlayerDropItem : NetworkBehaviour {
        [SerializeField] private GameObject m_Spawn;
        private NetworkedSettingsAbstract m_Settings;
        private void Awake () {
            m_Settings = NetworkedManager.Instance.NetworkSettings;
        }
        private void Start () {
            if (IsClient) {
                // Sending
                using (var stream = PooledNetworkBuffer.Get ())
                using (var writer = PooledNetworkWriter.Get (stream)) {
                    writer.WriteVector3Packed (transform.position);
                    writer.WriteRotationPacked (transform.rotation);
                    CustomMessagingManager.SendNamedMessage (
                        "MsgPlayerDropItem",
                        NetworkManager.Singleton.ServerClientId,
                        stream,
                        NetworkChannel.ChannelUnused);
                }
            }
            GameObject.Destroy (gameObject);
        }
    }
}