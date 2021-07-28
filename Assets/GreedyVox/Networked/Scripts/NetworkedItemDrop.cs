using System.IO;
using MLAPI;
using Opsive.Shared.Events;

namespace GreedyVox.Networked {
    public class NetworkedItemDrop : NetworkBehaviour {
        private ISpawnDataObject m_SpawnData;
        private void Awake () {
            m_SpawnData = GetComponent<ISpawnDataObject> ();
        }
        private void Start () {
            EventHandler.ExecuteEvent (gameObject, "OnWillRespawn");
        }
        public override void NetworkStart (Stream stream) {
            EventHandler.ExecuteEvent (gameObject, "OnRespawn");
            m_SpawnData?.ObjectSpawned (stream, gameObject);
        }
    }
}