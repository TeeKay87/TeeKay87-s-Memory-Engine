using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class DebuggerRegister
{
    private readonly byte[] _value;

    public DebuggerRegister(
        string id,
        string displayName,
        int bitWidth,
        ReadOnlyMemory<byte> value,
        string? group = null,
        DebuggerRegisterRole role = DebuggerRegisterRole.None,
        bool canWrite = false)
        : this(
            id,
            displayName,
            bitWidth,
            value,
            group,
            role,
            canWrite,
            DebuggerRegisterValueEncoding.Bytes)
    {
    }

    public DebuggerRegister(
        string id,
        string displayName,
        int bitWidth,
        ReadOnlyMemory<byte> value,
        string? group,
        DebuggerRegisterRole role,
        bool canWrite,
        DebuggerRegisterValueEncoding valueEncoding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (bitWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitWidth));
        }

        int expectedByteCount = checked((bitWidth + 7) / 8);
        if (value.Length != expectedByteCount)
        {
            throw new ArgumentException(
                $"A {bitWidth}-bit register must provide exactly {expectedByteCount} value bytes.",
                nameof(value));
        }

        Id = id;
        DisplayName = displayName;
        BitWidth = bitWidth;
        _value = value.ToArray();
        Group = group;
        Role = role;
        CanWrite = canWrite;
        ValueEncoding = valueEncoding;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public int BitWidth { get; }

    public ReadOnlyMemory<byte> Value => _value;

    public string? Group { get; }

    public DebuggerRegisterRole Role { get; }

    public bool CanWrite { get; }

    public DebuggerRegisterValueEncoding ValueEncoding { get; }
}
