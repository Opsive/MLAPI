using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "GreedyVox/Networking/Network Settings Test")]
public class NetCodeSettingsTest : NetCodeSettingsAbstract
{
    [SerializeField] private AudioClip m_AudioConnect = null;
    [SerializeField] private AudioClip m_AudioDisconnect = null;
    [Range(0, 1)][SerializeField] private float m_Volume = 0.0f;
    [SerializeField][Range(0, 120)] private int m_SyncPerSecondClient = 20;
    [Tooltip("Sync server no more than amount times a second")]
    [SerializeField][Range(0, 120)] private int m_SyncPerSecondServer = 20;
    public override float SyncRateClient => 1.0f / m_SyncPerSecondClient;
    public override float SyncRateServer => 1.0f / m_SyncPerSecondServer;
    protected override void OnEnable()
    {
        base.OnEnable();
        // Channel = string.IsNullOrEmpty (m_NetworkChannel) ? "MLAPI_DEFAULT_MESSAGE" : m_NetworkChannel;
        // Broadcast = string.IsNullOrEmpty (m_NetworkBroadcast) ? "MLAPI_DEFAULT_MESSAGE" : m_NetworkBroadcast;       
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
        Debug.Log("<color=white><b>ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER ENABLED</b></color>");
#else
        Debug.Log("<color=red><b>ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER DISENABLED</b></color>");
#endif
    }
    public override void PlayConnect(AudioSource source)
    {
        if (m_AudioConnect != null && source != null)
        {
            source.clip = m_AudioConnect;
            source.volume = m_Volume;
            source.Play();
        }
    }
    public override void PlayDisconnect(AudioSource source)
    {
        if (m_AudioDisconnect != null && source != null)
        {
            source.clip = m_AudioDisconnect;
            source.volume = m_Volume;
            source.Play();
        }
    }
    /// <summary>
    /// The update sync
    /// </summary>
    public override IEnumerator NetworkSyncUpdate()
    {
        while (IsActiveAndEnabled)
        {
            if (NetworkSyncUpdateEvent != null) { NetworkSyncUpdateEvent(); }
            yield return null;
        }
    }
    /// <summary>
    /// The update sync
    /// </summary>
    public override IEnumerator NetworkSyncFixedUpdate()
    {
        var wait = new WaitForFixedUpdate();
        while (IsActiveAndEnabled)
        {
            if (NetworkSyncFixedUpdateEvent != null) { NetworkSyncFixedUpdateEvent(); }
            yield return wait;
        }
    }
    /// <summary>
    /// The client sync
    /// </summary>
    public override IEnumerator NetworkSyncClient()
    {
        var wait = new WaitForSecondsRealtime(SyncRateClient);
        while (IsActiveAndEnabled)
        {
            if (NetworkSyncClientEvent != null) { NetworkSyncClientEvent(); }
            yield return wait;
        }
    }
    /// <summary>
    /// The server sync
    /// </summary>
    public override IEnumerator NetworkSyncServer()
    {
        var wait = new WaitForSecondsRealtime(SyncRateServer);
        while (IsActiveAndEnabled)
        {
            if (NetworkSyncServerEvent != null) { NetworkSyncServerEvent(); }
            yield return wait;
        }
    }
}