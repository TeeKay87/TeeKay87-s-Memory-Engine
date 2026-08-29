using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public static class TargetSessionExtensions
{
    public static TService GetRequiredService<TService>(this ITargetSession session)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(session);

        TService? service = session.GetService<TService>();
        return service ?? throw new NotSupportedException(
            $"The active target session does not provide {typeof(TService).Name}.");
    }
}
