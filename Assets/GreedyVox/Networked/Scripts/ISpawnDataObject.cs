using System.IO;
using MLAPI.Serialization.Pooled;
using UnityEngine;
/// <summary>
/// Interface which indicates that the object has initialization data that should be sent when the object is spawned.
/// </summary>
public interface ISpawnDataObject {
    /// <summary>
    /// Returns the initialization data that is required when the object spawns. This allows the remote players to initialize the object correctly.
    /// </summary>
    void SpawnData (PooledNetworkWriter writer);
    /// <summary>
    /// Callback after the object has been spawned.
    /// </summary>
    void ObjectSpawned (Stream stream, GameObject go);
}