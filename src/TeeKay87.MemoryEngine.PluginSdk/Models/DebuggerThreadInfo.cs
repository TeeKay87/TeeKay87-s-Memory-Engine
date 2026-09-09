using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record DebuggerThreadInfo
{
    public DebuggerThreadInfo(
        ulong id,
        string? name = null,
        DebuggerThreadState state = DebuggerThreadState.Unknown)
    {
        Id = id;
        Name = name;
        State = state;
    }

    public ulong Id { get; }

    public string? Name { get; }

    public DebuggerThreadState State { get; }
}
