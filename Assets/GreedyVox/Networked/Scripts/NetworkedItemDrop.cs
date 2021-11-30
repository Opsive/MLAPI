using GreedyVox.Networked.Data;
using Opsive.Shared.Events;
using Unity.Netcode;

namespace GreedyVox.Networked {
    public class NetworkedItemDrop : NetworkBehaviour {
        private IPayload m_Payload;
        private CustomMessagingManager m_CustomMessagingManager;
        private const string MsgNameClient = "MsgNetworkedItemDropClient";
        private void Awake () {
            m_Payload = GetComponent<IPayload> ();
        }
        private void OnEnable () {
            EventHandler.ExecuteEvent (gameObject, "OnWillRespawn");
        }
        public override void OnNetworkDespawn () {
            m_CustomMessagingManager?.UnregisterNamedMessageHandler (MsgNameClient);
        }
        public override void OnNetworkSpawn () {
            EventHandler.ExecuteEvent (gameObject, "OnRespawn");
            m_CustomMessagingManager = NetworkManager.Singleton.CustomMessagingManager;
            if (IsServer) {
                if (m_Payload.Load (out var writer)) {
                    m_CustomMessagingManager?.SendNamedMessage (MsgNameClient, NetworkManager.Singleton.ConnectedClientsIds, writer);
                }
            } else {
                m_CustomMessagingManager?.RegisterNamedMessageHandler (MsgNameClient, (sender, reader) => {
                    m_Payload?.Unload (ref reader, gameObject);
                });
            }
        }
    }
}