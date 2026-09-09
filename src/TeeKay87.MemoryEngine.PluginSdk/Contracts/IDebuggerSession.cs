using System;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDebuggerSession : IAsyncDisposable
{
    TargetProcess Process { get; }

    DebuggerExecutionState State { get; }

    event EventHandler<DebuggerEventEventArgs>? EventReceived;

    Task PauseAsync(CancellationToken cancellationToken);

    Task ContinueAsync(CancellationToken cancellationToken);

    Task DetachAsync(CancellationToken cancellationToken);

    TService? GetService<TService>() where TService : class;
}
