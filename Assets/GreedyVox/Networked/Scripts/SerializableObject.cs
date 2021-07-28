using MLAPI.Serialization;

namespace GreedyVox.Networked {
    public struct SerializableObject : INetworkSerializable {
        public object Value;
        public void NetworkSerialize (NetworkSerializer sync) {
            if (sync.IsReading) {
                Value = sync.Reader.ReadObjectPacked (typeof (object));
            } else if (Value != null) {
                sync.Writer.WriteObjectPacked (Value);
            }
        }
    }
    public static class SerializerObject {
        public static SerializableObject Create (this object value) =>
            new SerializableObject { Value = value };
    }
}