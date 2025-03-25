using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode
{
    public class NetCodePingPong : NetworkBehaviour
    {
        private void Start()
        {
            if (IsOwner)
                StartCoroutine(Updating());
        }
        // Here we use RpcParams for incoming purposes - fetching the sender ID the RPC came from
        // That sender ID can be passed in to the PongRpc to send this back to that client and ONLY that client
        [Rpc(SendTo.Server)]
        public void PingRpc(float ping, RpcParams rpc = default) =>
        PongRpc(ping, "PONG!", RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
        // We do not use rpcParams within this method's body, but that is okay!
        // The params passed in are used by the generated code to ensure that this sends only
        // to the one client it should go to
        [Rpc(SendTo.SpecifiedInParams)]
        void PongRpc(float ping, string message, RpcParams rpc) =>
        Debug.Log($"Received pong from server for ping {Mathf.RoundToInt((Time.realtimeSinceStartup - ping) * 1000.0f)} ms and message {message}");
        private IEnumerator Updating()
        {
            var wait = new WaitForSeconds(1.0f);
            while (isActiveAndEnabled)
            {
                yield return wait;
                PingRpc(Time.realtimeSinceStartup);
            }
        }
    }
}