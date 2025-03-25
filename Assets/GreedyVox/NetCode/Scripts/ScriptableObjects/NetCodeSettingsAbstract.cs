using System.Collections;
using UnityEngine;

public abstract class NetCodeSettingsAbstract : ScriptableObject
{
    public bool IsActiveAndEnabled = false;
    public abstract void PlayConnect(AudioSource source);
    public abstract void PlayDisconnect(AudioSource source);
    public abstract IEnumerator NetworkSyncUpdate();
    public abstract IEnumerator NetworkSyncClient();
    public abstract IEnumerator NetworkSyncServer();
    public abstract IEnumerator NetworkSyncFixedUpdate();
    public delegate void EventNetworkSyncUpdate();
    public delegate void EventNetworkSyncClient();
    public delegate void EventNetworkSyncServer();
    public delegate void EventNetworkSyncFixedUpdate();
    public EventNetworkSyncUpdate NetworkSyncUpdateEvent;
    public EventNetworkSyncClient NetworkSyncClientEvent;
    public EventNetworkSyncServer NetworkSyncServerEvent;
    public EventNetworkSyncFixedUpdate NetworkSyncFixedUpdateEvent;
    private float _SyncRateClient = 0.0f;
    public virtual float SyncRateClient
    {
        set { _SyncRateClient = value; }
        get { return _SyncRateClient; }
    }
    private float _SyncRateServer = 0.0f;
    public virtual float SyncRateServer
    {
        set { _SyncRateServer = value; }
        get { return _SyncRateServer; }
    }
    protected virtual void OnEnable()
    {
        IsActiveAndEnabled = true;
    }
    protected virtual void OnDisable()
    {
        IsActiveAndEnabled = false;
    }
    /// <summary>
    /// The server destroyed.
    /// </summary>
    protected virtual void OnDestroy()
    {
        NetworkSyncUpdateEvent = null;
        NetworkSyncClientEvent = null;
        NetworkSyncServerEvent = null;
    }
}