using Unity.Netcode;
using UnityEngine;

namespace GreedyVox.Networked.Data {
    public struct PayloadGrenado : INetworkSerializable {
        public StringValue ImpactStateName;
        public Vector3 Torque;
        public Vector3 Velocity;
        public int ImpactFrames;
        public int ImpactLayers;
        public float ImpactForce;
        public float DamageAmount;
        public float ScheduledDeactivation;
        public float ImpactStateDisableTimer;
        public void NetworkSerialize<T> (BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue (ref ImpactStateName);
            serializer.SerializeValue (ref Torque);
            serializer.SerializeValue (ref Velocity);
            serializer.SerializeValue (ref ImpactLayers);
            serializer.SerializeValue (ref ImpactFrames);
            serializer.SerializeValue (ref ImpactForce);
            serializer.SerializeValue (ref DamageAmount);
            serializer.SerializeValue (ref ScheduledDeactivation);
            serializer.SerializeValue (ref ImpactStateDisableTimer);
        }
    }
}