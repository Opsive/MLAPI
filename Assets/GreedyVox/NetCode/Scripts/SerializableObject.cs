using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Netcode;

namespace GreedyVox.NetCode
{
    public struct SerializableObject : INetworkSerializable
    {
        public byte[] Value;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Value);
        }
    }
    public static class DeserializerObject
    {
        /// <summary>
        /// Convert a byte array to an Object.
        /// Class|Properties|Fields will need to be tagged with the Serializable attribute to be serialized with this.
        /// </summary>
        private static object ByteArrayToObject(byte[] bytes)
        {
            var binForm = new BinaryFormatter();
            using (var memStream = new MemoryStream())
            {
                memStream.Write(bytes, 0, bytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                return (object)binForm.Deserialize(memStream);
            }
        }
        public static object Deserialize(SerializableObject serializer) =>
            ByteArrayToObject(serializer.Value);
    }
    public static class SerializerObject
    {
        /// <summary>
        /// Convert an object to a byte array.
        /// Class|Properties|Fields will need to be tagged with the Serializable attribute to be serialized with this.
        /// </summary>        
        private static byte[] ObjectToByteArray(object obj)
        {
            if (obj != null)
            {
                var bf = new BinaryFormatter();
                using (var ms = new MemoryStream())
                {
                    bf.Serialize(ms, obj);
                    return ms.ToArray();
                }
            }
            return null;
        }
        public static SerializableObject Serialize(this object value) =>
            new SerializableObject { Value = ObjectToByteArray(value) };
    }
}