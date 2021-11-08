using Unity.Netcode;

namespace GreedyVox.Networked {
    public class SerializableObjectArray : INetworkSerializable {
        public SerializableObject[] Value;
        public void NetworkSerialize<T> (BufferSerializer<T> serializer) where T : IReaderWriter {
            var length = 0;
            if (serializer.IsWriter) {
                length = Value.Length;
            }
            serializer.SerializeValue (ref length);
            if (serializer.IsReader) {
                Value = new SerializableObject[length];
            }
            for (int n = 0; n < length; ++n) {
                serializer.SerializeValue (ref Value[n]);
            }
        }
    }
    public static class DeserializerObjectArray {
        public static object[] Deserializer (SerializableObjectArray serializer) {
            var length = serializer.Value.Length;
            var value = new object[length];
            for (int n = 0; n < length; n++) {
                value[n] = serializer.Value[n];
            }
            return value;
        }
    }
    public static class SerializerObjectArray {
        public static SerializableObjectArray Serializer (this object[] value) {
            var length = value.Length;
            var serializer = new SerializableObjectArray { Value = new SerializableObject[length] };
            for (int n = 0; n < length; n++) {
                serializer.Value[n] = SerializerObject.Serializer (value[n]);
            }
            return serializer;
        }
    }
}