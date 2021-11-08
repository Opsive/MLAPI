// using Unity.Netcode;
// using UnityEngine;
// /// <summary>
// /// Interface which indicates that the object has initialization data that should be sent when the object is spawned.
// /// </summary>
// public interface ISpawnDataObject {
//     /// <summary>
//     /// Returns the initialization data that is required when the object spawns. This allows the remote players to initialize the object correctly.
//     /// </summary>
//     void SpawnData (FastBufferWriter writer);
//     /// <summary>
//     /// Callback after the object has been spawned.
//     /// </summary>
//     void ObjectSpawned<T> (T val, GameObject go) where T : unmanaged;
//     // void ObjectSpawned (FastBufferWriter writer, GameObject go);
// }