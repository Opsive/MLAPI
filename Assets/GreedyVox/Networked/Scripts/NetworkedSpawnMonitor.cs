using GreedyVox.ProjectManagers.Events;
using MLAPI;
using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Audio;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;
using UnityEngine.Events;

namespace GreedVox.Networked {
    public class NetworkedSpawnMonitor : NetworkBehaviour {
        [Tooltip ("Game object to invoke when Event is raised.")]
        [SerializeField] private GameEventGameObject m_GameEvent;
        [Tooltip ("Audio clips to play when networked object starts.")]
        [SerializeField] private AudioClipSet m_RespawnAudioClipSet = new AudioClipSet ();
        [Tooltip ("Response to invoke when Event is raised.")]
        [SerializeField] private UnityEvent<GameObject> m_Response;
        private UltimateCharacterLocomotion m_CharacterLocomotion;
        private void Awake () {
            m_CharacterLocomotion = GetComponent<UltimateCharacterLocomotion> ();
        }
        public override void NetworkStart () {
            EventHandler.ExecuteEvent (gameObject, "OnWillRespawn");
            m_Response?.Invoke (gameObject);
            m_GameEvent?.Raise (gameObject);
            m_CharacterLocomotion?.SetPositionAndRotation (transform.position, transform.rotation);
            m_RespawnAudioClipSet.PlayAudioClip (gameObject);
            EventHandler.ExecuteEvent (gameObject, "OnRespawn");
        }
    }
}