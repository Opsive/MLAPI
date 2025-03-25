using GreedyVox.NetCode.Interfaces;
using Unity.Netcode;

namespace GreedyVox.NetCode
{
    public class NetCodeItemDrop : NetworkBehaviour
    {
        private IPayload m_Payload;
        private void Awake() => m_Payload = GetComponent<IPayload>();
        public override void OnNetworkSpawn()
        {
            // m_Payload.OnNetworkSpawn();
            UnityEngine.Debug.LogFormat("<color=blue>Position: [{0}] Rotation: [{1}]</color>",
            transform.root.position, transform.root.rotation);
            base.OnNetworkSpawn();
        }
    }
}