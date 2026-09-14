using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record DisassemblyWatchpointTarget
{
    public DisassemblyWatchpointTarget(
        ulong address,
        int size,
        DebuggerBreakpointAccess access)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        if (access == DebuggerBreakpointAccess.Execute)
        {
            throw new ArgumentException(
                "A disassembly-derived data watchpoint cannot use Execute access.",
                nameof(access));
        }

        Address = address;
        Size = size;
        Access = access;
    }

    public ulong Address { get; }

    public int Size { get; }

    public DebuggerBreakpointAccess Access { get; }
}
