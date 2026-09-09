using System;
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

    IReadOnlyList<IMemoryValueType> SupportedValueTypes => Array.Empty<IMemoryValueType>();

    [Obsolete("Standard Scan Types are Core-owned from TeeKay87's Memory Engine 0.1.3.rev25 onward. This compatibility member is ignored by the host.")]
    IReadOnlyList<IMemoryScanType> SupportedScanTypes => Array.Empty<IMemoryScanType>();

    IReadOnlyList<IMemoryScanOption> SupportedScanOptions => Array.Empty<IMemoryScanOption>();

    string? DefaultValueTypeId => null;

    [Obsolete("The default Scan Type is Core-owned from TeeKay87's Memory Engine 0.1.3.rev25 onward. This compatibility member is ignored by the host.")]
    string? DefaultScanTypeId => null;

    Task<ITargetSession> ConnectAsync(
        TargetConnectionOptions options,
        CancellationToken cancellationToken);
}
