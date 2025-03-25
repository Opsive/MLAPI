// using UnityEngine;

// namespace GreedyVox.NetCode.ScriptableObjects
// {
//     [CreateAssetMenu(fileName = "NewPlayerClient", menuName = "GreedyVox/NetCode/PlayerClient")]
//     public class PlayerClient : ScriptableObject
//     {
//         public ulong ClientID, PlayerID;
//         private static PlayerClient _Instance;
//         public static PlayerClient Instance
//         {
//             get
//             {
//                 if (_Instance == null)
//                 {
//                     _Instance = Resources.Load<PlayerClient>("PlayerClient");
//                     if (_Instance == null)
//                         _Instance = CreateInstance<PlayerClient>();
//                 }
//                 return _Instance;
//             }
//         }
//     }
// }