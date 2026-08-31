using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.Mock;

public sealed class MockTargetPlugin : ITargetPlugin
{
    public PluginMetadata Metadata { get; } = new(
        Id: MockPluginInfo.Id,
        Name: MockPluginInfo.Name,
        Platform: "Development",
        Backend: "In-Memory",
        Version: MockPluginInfo.SemanticVersion,
        Revision: MockPluginInfo.Revision,
        ApiVersion: MockPluginInfo.TargetApiVersion,
        Description: "Deterministic target used to validate the shared plugin and memory-access architecture.",
        Architecture: new TargetArchitecture(
            CpuArchitecture.X64,
            pointerWidthBits: 64,
            addressWidthBits: 64,
            endianness: Endianness.Little));

    public TargetCapabilities Capabilities =>
        TargetCapabilities.Connect |
        TargetCapabilities.ProcessEnumeration |
        TargetCapabilities.ForegroundProcess |
        TargetCapabilities.MemoryRegionEnumeration |
        TargetCapabilities.MemoryRead |
        TargetCapabilities.MemoryWrite;

    public IReadOnlyList<TargetConnectionSettingDefinition> ConnectionSettings { get; } =
        Array.Empty<TargetConnectionSettingDefinition>();

    public Task<ITargetSession> ConnectAsync(
        TargetConnectionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        ITargetSession session = new MockTargetSession(Metadata);
        return Task.FromResult(session);
    }
}
