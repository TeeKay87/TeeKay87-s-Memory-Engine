using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record DebuggerBreakpoint
{
    public DebuggerBreakpoint(string id, DebuggerBreakpointRequest request, bool isEnabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(request);

        Id = id;
        Request = request;
        IsEnabled = isEnabled;
    }

    public string Id { get; }

    public DebuggerBreakpointRequest Request { get; }

    public bool IsEnabled { get; }
}
