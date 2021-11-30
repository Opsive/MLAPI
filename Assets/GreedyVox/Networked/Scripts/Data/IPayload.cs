using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.Networked.Data {
    public interface IPayload {
        /// <summary>
        /// The object has been spawned, write the payload data.
        /// </summary>
        bool Load (out FastBufferWriter writer);
        /// <summary>
        /// The object has been spawned, read the payload data.
        /// </summary>
        void Unload (ref FastBufferReader reader, GameObject go);
    }
}