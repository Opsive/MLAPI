using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;

namespace GreedVox.Networked.Collections {
    public class NetworkedDictionary<TK, TV> : NetworkVariableBase, IDictionary<TK, TV>, IEnumerable<KeyValuePair<TK, TV>>
        where TK : unmanaged, IEquatable<TK> where TV : unmanaged {
            /// <summary>
            /// Delegate type for dictionary changed event
            /// </summary>
            /// <param name="changeEvent">Struct containing information about the change event</param>
            public delegate void OnDictionaryChangedDelegate (NetworkedDictionaryEvent<TK, TV> changeEvent);
            /// <summary>
            /// The callback to be invoked when the dictionary gets changed
            /// </summary>
            public event OnDictionaryChangedDelegate OnDictionaryChanged;
            public int m_Index;
            private NativeHashMap<TK, TV> m_HashMap = new NativeHashMap<TK, TV> (64, Allocator.Persistent);
            private NativeList<NetworkedDictionaryEvent<TK, TV>> m_DirtyEvents =
                new NativeList<NetworkedDictionaryEvent<TK, TV>> (64, Allocator.Persistent);
            /// <inheritdoc />
            public int Count => m_HashMap.Count ();
            public int LastModifiedTick {
                // todo: implement proper network tick for NetworkList
                get { return NetworkTickSystem.NoTick; }
            }
            /// <inheritdoc />
            public TV this [TK key] {
                get => m_HashMap[key];
                set {
                    m_HashMap[key] = value;
                    HandleAddListEvent (new NetworkedDictionaryEvent<TK, TV> () {
                        Type = NetworkedDictionaryEvent<TK, TV>.EventType.Value, Key = key, Value = value
                    });
                }
            }
            /// <summary>
            /// Creates a NetworkedDictionary with the default value and settings
            /// </summary>
            public NetworkedDictionary () { }
            public NetworkedDictionary (int capacity) {
                m_HashMap.Capacity = capacity;
                m_DirtyEvents.Capacity = capacity;
            }
            /// <summary>
            /// Creates a NetworkedDictionary with a custom value and the default settings
            /// </summary>
            /// <param name="values">The initial value to use for the NetworkedDictionary</param>
            public NetworkedDictionary (IEnumerator<KeyValue<TK, TV>> items) {
                while (items.MoveNext ()) { m_HashMap.Add (items.Current.Key, items.Current.Value); }
            }
            /// <summary>
            /// Creates a NetworkedDictionary with the default value and custom settings
            /// </summary>
            /// <param name="readPerm">The read permission to use for the NetworkedDictionary</param>
            /// <param name="values">The initial value to use for the NetworkedDictionary</param>
            public NetworkedDictionary (NetworkVariableReadPermission readPerm, IEnumerator<KeyValue<TK, TV>> items) : base (readPerm) {
                while (items.MoveNext ()) { m_HashMap.Add (items.Current.Key, items.Current.Value); }
            }
            /// <inheritdoc />
            public override void ResetDirty () {
                base.ResetDirty ();
                if (m_Index == 0) { m_DirtyEvents.Clear (); } else {
                    m_DirtyEvents.RemoveRange (0, m_Index);
                }
            }
            /// <inheritdoc />
            public override bool IsDirty () {
                // we call the base class to allow the SetDirty() mechanism to work
                return base.IsDirty () || m_DirtyEvents.Length > 0;
            }
            /// <inheritdoc />
            public override void ReadField (FastBufferReader reader) {
                m_HashMap.Clear ();
                reader.ReadValueSafe (out ushort count);
                for (int i = 0; i < count; i++) {
                    reader.ReadValueSafe (out TK key);
                    reader.ReadValueSafe (out TV value);
                    m_HashMap.Add (key, value);
                }
            }
            /// <inheritdoc />
            public override void WriteField (FastBufferWriter writer) {
                writer.WriteValueSafe ((ushort) m_HashMap.Count ());
                var e = m_HashMap.GetEnumerator ();
                while (e.MoveNext ()) {
                    writer.WriteValueSafe (e.Current.Key);
                    writer.WriteValueSafe (e.Current.Value);
                }
            }
            /// <inheritdoc />
            public override void ReadDelta (FastBufferReader reader, bool dirty) {
                reader.ReadValueSafe (out ushort count);
                for (int i = 0; i < count; i++) {
                    reader.ReadValueSafe (out NetworkedDictionaryEvent<TK, TV>.EventType type);
                    switch (type) {
                        case NetworkedDictionaryEvent<TK, TV>.EventType.Add:
                            {
                                reader.ReadValueSafe (out TK key);
                                reader.ReadValueSafe (out TV value);
                                m_HashMap.Add (key, value);
                                if (OnDictionaryChanged != null) {
                                    OnDictionaryChanged (new NetworkedDictionaryEvent<TK, TV> {
                                        Type = type,
                                        Key = key,
                                        Value = value
                                    });
                                }
                                if (dirty) {
                                    m_DirtyEvents.Add (new NetworkedDictionaryEvent<TK, TV> () {
                                        Type = type, Key = key, Value = value
                                    });
                                }
                            }
                            break;
                        case NetworkedDictionaryEvent<TK, TV>.EventType.Remove:
                            {
                                reader.ReadValueSafe (out TK key);
                                m_HashMap.Remove (key);
                                if (OnDictionaryChanged != null) {
                                    OnDictionaryChanged (new NetworkedDictionaryEvent<TK, TV> {
                                        Type = type,
                                        Key = key
                                    });
                                }
                                if (dirty) {
                                    m_DirtyEvents.Add (new NetworkedDictionaryEvent<TK, TV> () {
                                        Type = type, Key = key
                                    });
                                }
                            }
                            break;
                        case NetworkedDictionaryEvent<TK, TV>.EventType.Value:
                            {
                                reader.ReadValueSafe (out TK key);
                                reader.ReadValueSafe (out TV value);
                                m_HashMap[key] = value;
                                if (OnDictionaryChanged != null) {
                                    OnDictionaryChanged (new NetworkedDictionaryEvent<TK, TV> {
                                        Type = type,
                                        Key = key,
                                        Value = value
                                    });
                                }
                                if (dirty) {
                                    m_DirtyEvents.Add (new NetworkedDictionaryEvent<TK, TV> () {
                                        Type = type, Key = key, Value = value
                                    });
                                }
                            }
                            break;
                        case NetworkedDictionaryEvent<TK, TV>.EventType.Clear:
                            {
                                m_HashMap.Clear ();
                                if (OnDictionaryChanged != null) {
                                    OnDictionaryChanged (new NetworkedDictionaryEvent<TK, TV> { Type = type });
                                }
                                if (dirty) {
                                    m_DirtyEvents.Add (new NetworkedDictionaryEvent<TK, TV> () { Type = type });
                                }
                            }
                            break;
                        case NetworkedDictionaryEvent<TK, TV>.EventType.Full:
                            {
                                ReadField (reader);
                                ResetDirty ();
                            }
                            break;
                    }
                }
            }
            /// <inheritdoc />
            public override void WriteDelta (FastBufferWriter writer) {
                if (base.IsDirty ()) {
                    writer.WriteValueSafe ((ushort) 1);
                    writer.WriteValueSafe (NetworkedDictionaryEvent<TK, TV>.EventType.Full);
                    WriteField (writer);
                } else {
                    writer.WriteValueSafe ((ushort) m_DirtyEvents.Length);
                    for (m_Index = 0; m_Index < m_DirtyEvents.Length; m_Index++) {
                        switch (m_DirtyEvents[m_Index].Type) {
                            case NetworkedDictionaryEvent<TK, TV>.EventType.Remove:
                                if (FastBufferWriter.GetWriteSize (m_DirtyEvents[m_Index].Type) +
                                    FastBufferWriter.GetWriteSize (m_DirtyEvents[m_Index].Key) +
                                    writer.Length > writer.MaxCapacity) {
                                    return;
                                }
                                writer.WriteValueSafe (m_DirtyEvents[m_Index].Type);
                                writer.WriteValueSafe (m_DirtyEvents[m_Index].Key);
                                break;
                            default:
                                if (FastBufferWriter.GetWriteSize (m_DirtyEvents[m_Index].Type) +
                                    FastBufferWriter.GetWriteSize (m_DirtyEvents[m_Index].Key) +
                                    FastBufferWriter.GetWriteSize (m_DirtyEvents[m_Index].Value) +
                                    writer.Length > writer.MaxCapacity) {
                                    return;
                                }
                                writer.WriteValueSafe (m_DirtyEvents[m_Index].Type);
                                writer.WriteValueSafe (m_DirtyEvents[m_Index].Key);
                                writer.WriteValueSafe (m_DirtyEvents[m_Index].Value);
                                break;
                        }
                    }
                }
            }
            /// <inheritdoc />
            public bool IsReadOnly => true;
            /// <inheritdoc />
            public bool ContainsKey (TK key) => m_HashMap.ContainsKey (key);
            /// <inheritdoc />
            public bool Contains (KeyValuePair<TK, TV> item) => m_HashMap.ContainsKey (item.Key);
            /// <inheritdoc />
            IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
            /// <inheritdoc />
            public ICollection<TK> Keys => m_HashMap.GetKeyArray (Allocator.Temp).ToArray ();
            /// <inheritdoc />
            public ICollection<TV> Values => m_HashMap.GetValueArray (Allocator.Temp).ToArray ();
            /// <inheritdoc />
            public bool TryGetValue (TK key, out TV value) => m_HashMap.TryGetValue (key, out value);
            /// <inheritdoc />
            public override void Dispose () {
                m_HashMap.Dispose ();
                m_DirtyEvents.Dispose ();
            }
            /// <inheritdoc />
            public void Clear () {
                m_HashMap.Clear ();
                HandleAddListEvent (new NetworkedDictionaryEvent<TK, TV> () {
                    Type = NetworkedDictionaryEvent<TK, TV>.EventType.Clear
                });
            }
            /// <inheritdoc />
            public void Add (TK key, TV value) {
                m_HashMap.Add (key, value);
                HandleAddListEvent (new NetworkedDictionaryEvent<TK, TV> () {
                    Type = NetworkedDictionaryEvent<TK, TV>.EventType.Add, Key = key, Value = value,
                });
            }
            /// <inheritdoc />
            public void Add (KeyValuePair<TK, TV> item) {
                m_HashMap.Add (item.Key, item.Value);
                HandleAddListEvent (new NetworkedDictionaryEvent<TK, TV> () {
                    Type = NetworkedDictionaryEvent<TK, TV>.EventType.Add, Key = item.Key, Value = item.Value,
                });
            }
            /// <inheritdoc />
            public bool Remove (TK key) {
                if (m_HashMap.Remove (key)) {
                    HandleAddListEvent (new NetworkedDictionaryEvent<TK, TV> () {
                        Type = NetworkedDictionaryEvent<TK, TV>.EventType.Remove, Key = key
                    });
                    return true;
                }
                return false;
            }
            /// <inheritdoc />
            public bool Remove (KeyValuePair<TK, TV> item) {
                if (m_HashMap.Remove (item.Key)) {
                    HandleAddListEvent (new NetworkedDictionaryEvent<TK, TV> () {
                        Type = NetworkedDictionaryEvent<TK, TV>.EventType.Remove, Key = item.Key
                    });
                    return true;
                }
                return false;
            }
            /// <inheritdoc />
            public void CopyTo (KeyValuePair<TK, TV>[] array, int index) {
                var map = m_HashMap.GetKeyValueArrays (Allocator.Temp);
                array = new KeyValuePair<TK, TV>[map.Length];
                for (int i = 0; i < array.Length; i++) {
                    array[i] = new KeyValuePair<TK, TV> (map.Keys[i], map.Values[i]);
                }
            }
            /// <inheritdoc />
            public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator () {
                var map = m_HashMap.GetKeyValueArrays (Allocator.Temp);
                var list = new List<KeyValuePair<TK, TV>> (map.Length);
                for (int i = 0; i < map.Length; i++) {
                    list.Add (new KeyValuePair<TK, TV> (map.Keys[i], map.Values[i]));
                }
                return list.GetEnumerator ();
            }
            private void HandleAddListEvent (NetworkedDictionaryEvent<TK, TV> e) {
                m_DirtyEvents.Add (e);
                OnDictionaryChanged?.Invoke (e);
            }
        }
    /// <summary>
    /// Struct containing event information about changes to a NetworkedDictionary.
    /// </summary>
    /// <typeparam name="T">The type for the list that the event is about</typeparam>
    public struct NetworkedDictionaryEvent<TK, TV> {
        /// <summary>
        /// Enum representing the operation made to the list.
        /// </summary>
        public EventType Type;
        /// <summary>
        /// the key changed, added or removed if available.
        /// </summary>
        public TK Key;
        /// <summary>
        /// The value changed, added or removed if available.
        /// </summary>
        public TV Value;
        /// <summary>
        /// Enum representing the different operations available for triggering an event.
        /// </summary>
        public enum EventType : byte {
            Add,
            Remove,
            Value,
            Clear,
            Full
        }
    }
}