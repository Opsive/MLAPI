using UnityEngine;

namespace GreedyVox.Networked.Utilities {
    public sealed class ComponentUtility {
        public static bool HasComponent<T> (GameObject obj)
        where T : Component {
            return obj.GetComponent<T> () != null;
        }
        public static void CopyValues<T> (T from, T to)
        where T : Component {
            var json = JsonUtility.ToJson (from);
            JsonUtility.FromJsonOverwrite (json, to);
        }
        public static void CopyValues<F, T> (F from, T to)
        where F : Component where T : Component {
            var json = JsonUtility.ToJson (from);
            JsonUtility.FromJsonOverwrite (json, to);
        }
        public static void RemoveCopyValues<T> (T from, T to)
        where T : Component {
            var json = JsonUtility.ToJson (from);
            JsonUtility.FromJsonOverwrite (json, to);
            GameObject.DestroyImmediate (from, true);
        }
        public static void RemoveCopyValues<F, T> (F from, T to)
        where F : Component where T : Component {
            var json = JsonUtility.ToJson (from);
            JsonUtility.FromJsonOverwrite (json, to);
            GameObject.DestroyImmediate (from, true);
        }
        public static bool TryAddComponent<T> (GameObject obj)
        where T : Component {
            var component = obj.GetComponent<T> ();
            if (component == null) {
                obj.AddComponent<T> ();
                return true;
            }
            return false;
        }
        public static bool TryAddComponent<T> (GameObject obj, out T com)
        where T : Component {
            com = obj.GetComponent<T> ();
            if (com == null) {
                com = obj.AddComponent<T> ();
                return true;
            }
            return false;
        }
        public static T TryAddGetComponent<T> (GameObject obj)
        where T : Component {
            var component = obj.GetComponent<T> ();
            if (component == null) {
                component = obj.AddComponent<T> ();
            }
            return component;
        }
        public static bool TryAddGetComponent<T> (GameObject obj, out T com)
        where T : Component {
            com = obj.GetComponent<T> ();
            if (com == null) {
                com = obj.AddComponent<T> ();
                return true;
            }
            return false;
        }
        public static bool TryRemoveComponent<T> (GameObject obj)
        where T : Component {
            var component = obj.GetComponent<T> ();
            if (component != null) {
                GameObject.DestroyImmediate (component, true);
                return true;
            }
            return false;
        }
    }
}