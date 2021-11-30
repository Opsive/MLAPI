using System.Runtime.InteropServices;
using Unity.Netcode;

namespace GreedyVox.Networked {
    public struct SerializableObject : INetworkSerializable {
        // Infite loop needs to be replaced, this works to bypass the complilier.
        // May need to replace the whole struct to find a working solution with the networking buffers.
        // The type 'SerializableObject' must be a non-nullable value type, along with all fields at any level of nesting, 
        // in order to use it as parameter 'T' in the generic type or method 'BufferSerializer<T>.SerializeValue<T>(ref T)'
        public object Value { get => Value ?? new object (); set => Value = value; }
        public void NetworkSerialize<T> (BufferSerializer<T> serializer) where T : IReaderWriter {
            var size = serializer.IsReader ? 0 :
                Marshal.SizeOf (Value);
            serializer.SerializeValue (ref size);
            var bytes = serializer.IsReader ? new byte[size] :
                ObjectToByteArray (Value, size);
            for (int n = 0; n < bytes.Length; n++) {
                serializer.SerializeValue (ref bytes[n]);
            }
            if (serializer.IsReader) { Value = ByteArrayToObject (bytes, size); }
        }
        /// <summary>
        /// Convert a byte array to an Object.
        /// Properties/Fields without implementing serialization and without [Serializable] attribute.
        /// </summary>
        private object ByteArrayToObject (byte[] bytes, int size) {
            var ptr = Marshal.AllocHGlobal (size);
            Marshal.Copy (bytes, 0, ptr, size);
            var obj = (object) Marshal.PtrToStructure (ptr, typeof (object));
            Marshal.FreeHGlobal (ptr);
            return obj;
        }
        /// <summary>
        /// Convert an object to a byte array.
        /// Properties/Fields without implementing serialization and without [Serializable] attribute.
        /// </summary>        
        private byte[] ObjectToByteArray (object obj, int size) {
            if (obj != null) {
                // Both managed and unmanaged buffers required.
                var bytes = new byte[size];
                var ptr = Marshal.AllocHGlobal (size);
                // Copy object byte-to-byte to unmanaged memory.
                Marshal.StructureToPtr (obj, ptr, false);
                // Copy data from unmanaged memory to managed buffer.
                Marshal.Copy (ptr, bytes, 0, size);
                // Release unmanaged memory.
                Marshal.FreeHGlobal (ptr);
                return bytes;
            }
            return null;
        }
        // /// <summary>
        // /// Convert a byte array to an Object.
        // /// Properties/Fields will need to be tagged with the Serializable attribute to be serialized with this.
        // /// </summary>
        // private object ByteArrayToObjectSafe (byte[] bytes) {
        //     var binForm = new BinaryFormatter ();
        //     using (var memStream = new MemoryStream ()) {
        //         memStream.Write (bytes, 0, bytes.Length);
        //         memStream.Seek (0, SeekOrigin.Begin);
        //         return (object) binForm.Deserialize (memStream);
        //     }
        // }
        // /// <summary>
        // /// Convert an object to a byte array.
        // /// Properties/Fields will need to be tagged with the Serializable attribute to be serialized with this.
        // /// </summary>        
        // private byte[] ObjectToByteArraySafe (object obj) {
        //     if (obj != null) {
        //         var bf = new BinaryFormatter ();
        //         using (var ms = new MemoryStream ()) {
        //             bf.Serialize (ms, obj);
        //             return ms.ToArray ();
        //         }
        //     }
        //     return null;
        // }
    }
    public static class SerializerObject {
        public static SerializableObject Serializer (this object value) =>
            new SerializableObject { Value = value };
    }
}