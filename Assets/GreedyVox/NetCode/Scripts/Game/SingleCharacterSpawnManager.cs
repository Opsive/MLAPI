// using Unity.Netcode;
// using UnityEngine;

// namespace GreedyVox.NetCode.Game
// {
//     /// <summary>
//     /// Manages the character instantiation within a NetCode room.
//     /// </summary>
//     public class SingleCharacterSpawnManager : NetCodeSpawnManagerBase
//     {
//         [Tooltip("A reference to the character that NetCode should spawn. This character must be setup using the NetCode Multiplayer Manager.")]
//         [SerializeField] protected GameObject m_Character;
//         public GameObject Character { get { return m_Character; } set { m_Character = value; } }
//         /// <summary>
//         /// Abstract method that allows for a character to be spawned based on the game logic.
//         /// </summary>
//         /// <param name="newPlayer">The player that entered the room.</param>
//         /// <returns>The character prefab that should spawn.</returns>
//         protected override GameObject GetCharacterPrefab(NetworkObject net)
//         {
//             // Return the same character for all instances.
//             return m_Character;
//         }
//     }
// }