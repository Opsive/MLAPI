using MLAPI.Serialization;
using UnityEngine;

namespace GreedyVox.Networked {
    public class SerializableObjectArray : INetworkSerializable {
        public object[] Value;
        public void NetworkSerialize (NetworkSerializer sync) {
            if (sync.IsReading) {
                Value = new Object[sync.Reader.ReadInt32Packed ()];
                for (int n = 0; n < Value.Length; n++) {
                    Value[n] = sync.Reader.ReadObjectPacked (typeof (object));
                }
            } else if (Value != null) {
                sync.Writer.WriteInt32Packed (Value.Length);
                for (int n = 0; n < Value.Length; n++) {
                    sync.Writer.WriteObjectPacked (Value[n]);
                }
            }
        }
    }
    public static class SerializerObjectArray {
        public static SerializableObjectArray Create (this object[] value) =>
            new SerializableObjectArray { Value = value };
    }
}