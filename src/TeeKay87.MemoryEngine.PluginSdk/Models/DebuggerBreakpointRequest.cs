using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record DebuggerBreakpointRequest
{
    public DebuggerBreakpointRequest(
        ulong address,
        int size,
        DebuggerBreakpointKind kind,
        DebuggerBreakpointAccess access,
        bool isTemporary = false)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Address = address;
        Size = size;
        Kind = kind;
        Access = access;
        IsTemporary = isTemporary;
    }

    public ulong Address { get; }

    public int Size { get; }

    public DebuggerBreakpointKind Kind { get; }

    public DebuggerBreakpointAccess Access { get; }

    public bool IsTemporary { get; }
}
