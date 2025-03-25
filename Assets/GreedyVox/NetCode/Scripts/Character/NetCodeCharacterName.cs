using Unity.Netcode;

namespace GreedyVox.NetCode.Character
{
    public class NetCodeCharacterName : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            this.name = $"{(IsOwner ? "Player" : "Client")} ID: [{OwnerClientId}] Object ID: [{NetworkObjectId}]";
            base.OnNetworkSpawn();
        }
    }
}