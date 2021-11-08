using UnityEngine;

namespace GreedyVox.Networked.Data {
    public interface IPayload {
        /// <summary>
        /// Returns the initialization data that is required when the object spawns.
        /// This allows the remote players to initialize the object correctly.
        /// </summary>
        void Load ();
        /// <summary>
        /// Callback after the object has been spawned.
        /// </summary>
        void Unload<T> (T val, GameObject go) where T : unmanaged;
    }
}