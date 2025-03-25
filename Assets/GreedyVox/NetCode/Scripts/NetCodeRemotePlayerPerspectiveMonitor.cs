using Opsive.Shared.Events;
using Opsive.Shared.Networking;
using Opsive.UltimateCharacterController.Camera;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Identifiers;
using Opsive.UltimateCharacterController.ThirdPersonController.Character;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Manages the perspective switch for a networked remote player. This is a lightweight version of the Third Person Controller Perspective Monitor.
/// </summary>
namespace GreedyVox.NetCode
{
    [DisallowMultipleComponent]
    public class NetCodeRemotePlayerPerspectiveMonitor : NetworkBehaviour
    {
        [Tooltip("The material used to make the object invisible but still cast shadows.")]
        [SerializeField] protected Material m_InvisibleMaterial;
        public Material InvisibleMaterial { get { return m_InvisibleMaterial; } set { m_InvisibleMaterial = value; } }
        private INetworkInfo m_NetworkInfo;
        private GameObject m_GameObject;
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake()
        {
#if THIRD_PERSON_CONTROLLER
            // If the third person Perspective Monitor exists then that component will manage the remote player's perspective.
            var perspectiveMonitor = gameObject.GetComponent<PerspectiveMonitor>();
            if (perspectiveMonitor != null)
            {
                Destroy(this);
                return;
            }
#endif
            m_GameObject = gameObject;
            m_NetworkInfo = m_GameObject.GetComponent<INetworkInfo>();
            if (m_NetworkInfo == null)
            {
                Debug.LogError("Error: The character must have a NetworkInfo object.");
                return;
            }
            EventHandler.RegisterEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
        }
        /// <summary>
        /// A new ILookSource object has been attached to the character.
        /// </summary>
        /// <param name="lookSource">The ILookSource object attached to the character.</param>
        private void OnAttachLookSource(ILookSource lookSource)
        {
            var firstPersonPerspective = false;
            if (lookSource != null && m_NetworkInfo.IsLocalPlayer())
            {
                var cameraController = lookSource as CameraController;
                if (cameraController != null)
                {
                    firstPersonPerspective = cameraController.ActiveViewType.FirstPersonPerspective;
                }
            }
            // The character is a first person character. Set the third person objects to the invisible shadow castor material.
            if (firstPersonPerspective)
            {
                var thirdPersonObjects = gameObject.GetComponentsInChildren<ThirdPersonObject>(true);
                for (int i = 0; i < thirdPersonObjects.Length; ++i)
                {
                    var renderers = thirdPersonObjects[i].GetComponentsInChildren<Renderer>(true);
                    for (int j = 0; j < renderers.Length; ++j)
                    {
                        var materials = renderers[j].materials;
                        for (int k = 0; k < materials.Length; ++k)
                        {
                            materials[k] = m_InvisibleMaterial;
                        }
                        renderers[j].materials = materials;
                    }
                }
            }
            Destroy(this);
        }
        /// <summary>
        /// The object has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            if (m_GameObject != null)
                EventHandler.UnregisterEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
            base.OnDestroy();
        }
    }
}