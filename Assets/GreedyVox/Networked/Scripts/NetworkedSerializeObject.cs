// using System.IO;
// using MLAPI.Serialization;
// using MLAPI.Serialization.Pooled;

// namespace GreedyVox.Networked {
//     public class NetworkedSerializeObject : INetworkSerializable {
//         public object Object;
//         public void NetworkSerialize (NetworkSerializer serializer) {
//             serializer.Writer.WriteObjectPacked (Object);

//             // Tells the MLAPI how to serialize and deserialize object in the future.
//             SerializationManager.RegisterSerializationHandlers<object> ((Stream stream, object instance) => {
//                 // This delegate gets ran when the MLAPI want's to serialize a object type to the stream.
//                 using (var writer = PooledNetworkWriter.Get (stream)) {
//                     writer.WriteObjectPacked (instance);
//                 }
//             }, (Stream stream) => {
//                 // This delegate gets ran when the MLAPI want's to deserialize a object type from the stream.
//                 using (var reader = PooledNetworkReader.Get (stream)) {
//                     return Object = reader.ReadObjectPacked (typeof (object));
//                 }
//             });
//         }
//     }
// }