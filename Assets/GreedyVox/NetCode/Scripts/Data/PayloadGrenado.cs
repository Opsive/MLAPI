using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.NetCode.Data
{
    public struct PayloadGrenado : INetworkSerializable
    {
        public uint OwnerID;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Torque;
        public Vector3 Velocity;
        public int ImpactFrames;
        public int ImpactLayers;
        public float ImpactForce;
        public float DamageAmount;
        public float ScheduledDeactivation;
        public float ImpactStateDisableTimer;
        public NetworkObjectReference NetCodeObject;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
        {
            serializer.SerializeValue(ref OwnerID);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Torque);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref ImpactLayers);
            serializer.SerializeValue(ref ImpactFrames);
            serializer.SerializeValue(ref ImpactForce);
            serializer.SerializeValue(ref DamageAmount);
            serializer.SerializeValue(ref ScheduledDeactivation);
            serializer.SerializeValue(ref ImpactStateDisableTimer);
            serializer.SerializeValue(ref NetCodeObject);
        }
    }
}