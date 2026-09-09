using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class DebuggerRegisterWriteRequest
{
    private readonly byte[] _value;

    public DebuggerRegisterWriteRequest(string registerId, ReadOnlyMemory<byte> value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerId);

        if (value.IsEmpty)
        {
            throw new ArgumentException("A register write must contain at least one byte.", nameof(value));
        }

        RegisterId = registerId;
        _value = value.ToArray();
    }

    public string RegisterId { get; }

    public ReadOnlyMemory<byte> Value => _value;
}
