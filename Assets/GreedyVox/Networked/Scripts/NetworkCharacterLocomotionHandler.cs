using GreedyVox.Networked;
using Opsive.Shared.Camera;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Camera;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;

namespace Opsive.UltimateCharacterController.AddOns.Multiplayer.Character {
    /// <summary>
    /// THIS SCRIPT IS NEEDED FOR COMPILING NETWORK CODE, DO NOT RENAME OR CHANGE ITS NAMESPACE.
    /// Script which will define networking for the Ultimate Character Controller compiler symbols so the components are aware of the asset import status.
    /// Required for the "DefineCompilerSymbols.cs" script in the directory "Opsive/UltimateCharacterController/Editor/Utility/".
    /// </summary>
    public class NetworkCharacterLocomotionHandler : UltimateCharacterLocomotionHandler {
        [Tooltip ("Should the camera be attached to the local player?")]
        [SerializeField] protected bool m_AttachCamera = true;
        private NetworkedInfo m_NetworkInfo;
        /// <summary>
        /// Initializes the default values.
        /// </summary>
        protected override void Awake () {
            base.Awake ();
            m_NetworkInfo = gameObject.GetCachedComponent<NetworkedInfo> ();
        }
        /// <summary>
        /// Determines if the handler should be enabled.
        /// </summary>
        private void Start () {
            if (m_NetworkInfo.IsLocalPlayer) {
                if (m_AttachCamera) {
                    var camera = CameraUtility.FindCamera (gameObject);
                    if (camera != null) {
                        camera.GetComponent<CameraController> ().Character = gameObject;
                    }
                }
            } else {
                // Non-local players will not be controlled with player input.
                enabled = false;
                m_CharacterLocomotion.enabled = false;
            }
        }
        /// <summary>
        /// The character has respawned.
        /// </summary>
        protected override void OnRespawn () {
            if (m_NetworkInfo.IsLocalPlayer) {
                base.OnRespawn ();
            }
        }
    }
}