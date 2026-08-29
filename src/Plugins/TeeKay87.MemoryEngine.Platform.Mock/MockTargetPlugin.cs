using System;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.Mock;

public sealed class MockTargetPlugin : ITargetPlugin
{
    public PluginMetadata Metadata { get; } = new(
        Id: "platform.mock.in-memory",
        Name: "In-Memory Test Target",
        Platform: "Development",
        Backend: "In-Memory",
        Version: new Version(1, 0, 0),
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
