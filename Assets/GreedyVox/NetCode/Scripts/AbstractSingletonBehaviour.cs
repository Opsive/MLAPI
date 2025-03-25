using UnityEngine;

namespace GreedyVox.NetCode
{
    public abstract class AbstractSingletonBehaviour<T> : MonoBehaviour where T : AbstractSingletonBehaviour<T>, new()
    {
        protected static T _Instance;
        public static T Instance
        {
            get
            {
                // This would only EVER be null if some other MonoBehavior requests the instance in its' Awake method.
                if (_Instance == null)
                {
                    Debug.Log($"<color=white>[UnitySingleton]</color><color=green>[{typeof(T).ToString()}]</color> Finding instance of object");
                    _Instance = FindObjectOfType(typeof(T)) as T;
                    // This should only occur if 'T' hasn't been attached to any game objects in the scene.
                    if (!_Instance)
                    {
                        Debug.LogError($"[<color=white>UnitySingleton</color>][<color=red>{typeof(T).ToString()}</color>] No instance of found!");
                    }
                }
                return _Instance;
            }
        }
        #region Basic getters/setters
        // Whether or not this object should persist when loading new scenes.
        // This should be set in the child classes Init() method.
        private bool _Persist = false;
        public bool Persist { get { return _Persist; } protected set { _Persist = value; } }
        #endregion
        // This will initialize our instance, if it hasn't already been prompted to do so by
        // another MonoBehavior's Awake() requesting it first.
        protected virtual void Awake()
        {
            Debug.Log($"<color=white>[UnitySingleton]</color><color=blue>[{typeof(T).ToString()}]</color> Awake");
            if (_Instance == null)
            {
                Debug.Log($"<color=white>[UnitySingleton]</color><color=blue>[{typeof(T).ToString()}]</color> Initializing Singleton in Awake");
                _Instance = (T)this;
                if (Persist) { DontDestroyOnLoad(gameObject); }
            }
        }
        // Make sure no "ghost" objects are left behind when applicaton quits.
        protected virtual void OnApplicationQuit()
        {
            _Instance = null;
        }
    }
}