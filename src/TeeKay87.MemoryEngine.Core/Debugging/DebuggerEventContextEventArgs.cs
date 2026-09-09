using System;

namespace TeeKay87.MemoryEngine.Core.Debugging;

public sealed class DebuggerEventContextEventArgs : EventArgs
{
    public DebuggerEventContextEventArgs(DebuggerEventContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public DebuggerEventContext Context { get; }
}
