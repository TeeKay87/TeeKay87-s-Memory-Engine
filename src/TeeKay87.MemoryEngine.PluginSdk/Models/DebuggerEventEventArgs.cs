using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class DebuggerEventEventArgs : EventArgs
{
    public DebuggerEventEventArgs(DebuggerEvent debugEvent)
    {
        Event = debugEvent ?? throw new ArgumentNullException(nameof(debugEvent));
    }

    public DebuggerEvent Event { get; }
}
