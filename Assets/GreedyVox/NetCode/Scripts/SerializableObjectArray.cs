using Unity.Netcode;

namespace GreedyVox.NetCode
{
    public class SerializableObjectArray : INetworkSerializable
    {
        public SerializableObject[] Value;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
        {
            var length = serializer.IsReader ? 0 : Value.Length;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader)
                Value = new SerializableObject[length];
            for (int n = 0; n < length; n++)
                serializer.SerializeValue(ref Value[n].Value);
        }
    }
    public static class DeserializerObjectArray
    {
        public static object[] Deserialize(SerializableObjectArray serializer)
        {
            var length = serializer.Value.Length;
            var value = new object[length];
            for (int n = 0; n < length; n++)
                value[n] = DeserializerObject.Deserialize(serializer.Value[n]);
            return value;
        }
    }
    public static class SerializerObjectArray
    {
        public static SerializableObjectArray Serialize(this object[] value)
        {
            var length = value.Length;
            var serializer = new SerializableObjectArray { Value = new SerializableObject[length] };
            for (int n = 0; n < length; n++)
                serializer.Value[n] = SerializerObject.Serialize(value[n]);
            return serializer;
        }
    }
}