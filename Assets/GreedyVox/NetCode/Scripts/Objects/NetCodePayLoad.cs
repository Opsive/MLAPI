using Opsive.UltimateCharacterController.Objects;
using Unity.Netcode;

namespace GreedyVox.NetCode.Objects
{
    public class NetCodePayLoad : NetworkBehaviour
    {
        private ProjectileBase m_ProjectileBase;
        private void Awake() =>
        m_ProjectileBase = GetComponent<ProjectileBase>();
        public override void OnNetworkSpawn()
        {
            if (m_ProjectileBase != null)
                m_ProjectileBase.enabled = true;
            base.OnNetworkSpawn();
        }
    }
}