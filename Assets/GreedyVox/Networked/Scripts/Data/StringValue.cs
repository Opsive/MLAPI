using Unity.Netcode;

public struct StringValue : INetworkSerializable {
    public string Value { get { return Value ?? "N/A"; } set { Value = value; } }
    public StringValue (string value) : this () => Value = value;
    public static implicit operator string (StringValue val) => val.Value;
    public static implicit operator StringValue (string val) => new StringValue (val);
    public void NetworkSerialize<T> (BufferSerializer<T> serializer) where T : IReaderWriter {
        var value = Value;
        serializer.SerializeValue (ref value);
    }
}