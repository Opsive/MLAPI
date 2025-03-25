using Opsive.Shared.Events;
using Opsive.Shared.Input.VirtualControls;
using Unity.Netcode;

namespace GreedyVox.NetCode.Inputs
{
    public class NetCodeVirtualControlsManager : VirtualControlsManager
    {
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (m_Character == null)
                EventHandler.RegisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
        }
        /// <summary>
        /// The object has been destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventHandler.UnregisterEvent<ulong, NetworkObjectReference>("OnPlayerConnected", OnPlayerConnected);
        }
        /// <summary>
        /// A player has entered the room. Connect the virtual controls to the joining local character.
        /// </summary>
        /// <param name="id">The Client networking id that entered the room.</param>
        /// <param name="obj">The NetworkObject Player that entered the room.</param>
        protected virtual void OnPlayerConnected(ulong id, NetworkObjectReference obj)
        {
            if (obj.TryGet(out NetworkObject net) && net.IsLocalPlayer)
                Character = net.gameObject;
        }
    }
}