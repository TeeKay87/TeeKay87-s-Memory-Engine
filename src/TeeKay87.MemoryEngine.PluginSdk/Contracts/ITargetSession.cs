using System;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface ITargetSession : IAsyncDisposable
{
    PluginMetadata Plugin { get; }

    TargetArchitecture Architecture { get; }

    bool IsConnected { get; }

    TService? GetService<TService>() where TService : class;
}
