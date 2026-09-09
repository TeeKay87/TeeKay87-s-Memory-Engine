using System;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Debugging;

public sealed record DebuggerEventContext
{
    public DebuggerEventContext(
        DebuggerSessionIdentity identity,
        long sequence,
        DebuggerEvent debugEvent)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(debugEvent);

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        Identity = identity;
        Sequence = sequence;
        Event = debugEvent;
    }

    public DebuggerSessionIdentity Identity { get; }

    public long Sequence { get; }

    public DebuggerEvent Event { get; }
}
