using GreedyVox.Networked.Data;
using Opsive.Shared.Events;
using Unity.Netcode;

namespace GreedyVox.Networked {
    public class NetworkedItemDrop : NetworkBehaviour {
        private IPayload m_Payload;
        private void Awake () {
            m_Payload = GetComponent<IPayload> ();
        }
        private void Start () {
            m_Payload?.Load ();
            EventHandler.ExecuteEvent (gameObject, "OnWillRespawn");
        }
        public override void OnNetworkSpawn () {
            EventHandler.ExecuteEvent (gameObject, "OnRespawn");
        }
        public void NetworkedVariable<T> (T value) where T : unmanaged {
            new NetworkVariable<T> (value).OnValueChanged += (T o, T n) => {
                m_Payload?.Unload (n, gameObject);
            };
        }
    }
}