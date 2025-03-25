using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode.Interfaces
{
    public interface IPayload
    {
        public int NetworkID { get; set; }
        /// <summary>
        /// The object has been spawned, write the payload data.
        /// </summary>
        bool PayLoad(ref int idx, out FastBufferWriter writer);
        /// <summary>
        /// The object has been spawned, read the payload data.
        /// </summary>
        void PayLoad(in FastBufferReader reader, GameObject go = default);
        /// <summary>
        /// Initializes the object. This will be called from an object creating the projectile (such as a weapon).
        /// </summary>
        /// <param name="id">The id used to differentiate this projectile from others.</param>
        /// <param name="owner">The object that instantiated the trajectory object.</param>
        void Initialize(uint id, GameObject own);
        /// <summary>
        /// Returns the maximus size for the fast buffer writer
        /// </summary>
        int MaxBufferSize();
    }
}