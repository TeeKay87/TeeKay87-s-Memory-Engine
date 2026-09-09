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
        Description: "Deterministic target used to validate shared plugin, memory-access, scanning, disassembly, debugger, thread-control, and register-access architecture.",
        Architecture: new TargetArchitecture(
            CpuArchitecture.Unknown,
            pointerWidthBits: 64,
            addressWidthBits: 64,
            endianness: Endianness.Little));

    public TargetCapabilities Capabilities =>
        TargetCapabilities.Connect |
        TargetCapabilities.ProcessEnumeration |
        TargetCapabilities.ForegroundProcess |
        TargetCapabilities.MemoryRegionEnumeration |
        TargetCapabilities.MemoryRead |
        TargetCapabilities.MemoryWrite |
        TargetCapabilities.Disassembly |
        TargetCapabilities.Debugger |
        TargetCapabilities.ThreadEnumeration |
        TargetCapabilities.ThreadControl |
        TargetCapabilities.RegisterAccess;

    public IReadOnlyList<TargetConnectionSettingDefinition> ConnectionSettings { get; } =
        Array.Empty<TargetConnectionSettingDefinition>();

    public IReadOnlyList<IMemoryValueType> SupportedValueTypes => MockScanDefinitions.ValueTypes;

    public string DefaultValueTypeId => MockScanDefinitions.DefaultValueTypeId;

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
