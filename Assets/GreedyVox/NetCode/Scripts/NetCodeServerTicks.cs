using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetCodeServerTicks : NetworkBehaviour
    {
        [SerializeField] private bool m_IsDebugLogging = false;
        public override void OnNetworkSpawn() =>
        NetworkManager.NetworkTickSystem.Tick += Tick;
        public override void OnNetworkDespawn() =>
        NetworkManager.NetworkTickSystem.Tick -= Tick;
        private void Tick()
        {
            if (m_IsDebugLogging)
                Debug.LogFormat("<color=white>Tick: <color=black><b>[{0}] Fixed: [{1}]</b></color></color>",
                NetworkManager.LocalTime.Tick, NetworkManager.LocalTime.FixedTime);
        }
    }
}