using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface ITargetPlugin
{
    PluginMetadata Metadata { get; }

    TargetCapabilities Capabilities { get; }

    IReadOnlyList<TargetConnectionSettingDefinition> ConnectionSettings { get; }

    Task<ITargetSession> ConnectAsync(
        TargetConnectionOptions options,
        CancellationToken cancellationToken);
}
