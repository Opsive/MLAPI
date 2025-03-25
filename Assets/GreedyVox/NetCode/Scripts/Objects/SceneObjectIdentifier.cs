using GreedyVox.NetCode.Utilities;
using Opsive.UltimateCharacterController.Objects;
using Unity.Netcode;

/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace GreedyVox.NetCode.Objects
{
    /// <summary>
    /// A lightweight class that can be uniquely identified within the scene for the addon.
    /// </summary>
    public class SceneObjectIdentifier : ObjectIdentifier
    {
        private bool m_Registered;
        /// <summary>
        /// Registers itself as a Scene Object Identifier.
        /// </summary>
        private void Awake()
        {
            if (GetComponent<NetworkObject>() == null)
            {
                NetCodeUtility.RegisterSceneObjectIdentifier(this);
                m_Registered = true;
            }
        }
        /// <summary>
        /// Unregisters itself as a Scene Object Identifier.
        /// </summary>
        private void OnDestroy()
        {
            if (m_Registered)
            {
                NetCodeUtility.UnregisterSceneObjectIdentifier(this);
                m_Registered = false;
            }
        }
    }
}