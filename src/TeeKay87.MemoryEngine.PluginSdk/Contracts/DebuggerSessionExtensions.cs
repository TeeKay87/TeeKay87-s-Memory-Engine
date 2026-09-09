using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public static class DebuggerSessionExtensions
{
    public static TService GetRequiredService<TService>(this IDebuggerSession session)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(session);

        TService? service = session.GetService<TService>();
        return service ?? throw new NotSupportedException(
            $"The debugger session does not provide {typeof(TService).Name}.");
    }
}
