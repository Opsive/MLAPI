using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode.Data
{
    public struct PayloadProjectile : INetworkSerializable
    {
        public long OwnerID;
        public uint ProjectileID;
        public Vector3 Velocity;
        public Vector3 Torque;
        public float DamageAmount;
        public float ImpactForce;
        public int ImpactFrames;
        public int ImpactLayers;
        public float ImpactStateDisableTimer;
        public string ImpactStateName;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
        {
            serializer.SerializeValue(ref OwnerID);
            serializer.SerializeValue(ref ProjectileID);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref Torque);
            serializer.SerializeValue(ref DamageAmount);
            serializer.SerializeValue(ref ImpactForce);
            serializer.SerializeValue(ref ImpactFrames);
            serializer.SerializeValue(ref ImpactLayers);
            serializer.SerializeValue(ref ImpactStateDisableTimer);
            serializer.SerializeValue(ref ImpactStateName);
        }
    }
}