using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

public sealed class Ps5TargetPlugin : ITargetPlugin
{
    public PluginMetadata Metadata { get; } = new(
        Id: Ps5PluginInfo.Id,
        Name: Ps5PluginInfo.Name,
        Platform: "PlayStation 5",
        Backend: "ps5debug-NG",
        Version: Ps5PluginInfo.SemanticVersion,
        Revision: Ps5PluginInfo.Revision,
        ApiVersion: Ps5PluginInfo.TargetApiVersion,
        Description: "Connects TeeKay87's Memory Engine to a PlayStation 5 running ps5debug-NG and exposes process enumeration, preferred game-process selection, memory maps, raw memory access, native Exact Value scanning/refinement across all ps5debug-NG value types, and process pause/resume.",
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
        TargetCapabilities.MemoryWrite |
        TargetCapabilities.ProcessSuspend |
        TargetCapabilities.ProcessResume |
        TargetCapabilities.NativeValueScanning;

    public IReadOnlyList<TargetConnectionSettingDefinition> ConnectionSettings =>
        Ps5ConnectionSettings.Definitions;

    public async Task<ITargetSession> ConnectAsync(
        TargetConnectionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.TryGetValue(Ps5ConnectionSettings.HostKey, out string host) ||
            string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("A PS5 IP address or host name is required.", nameof(options));
        }

        if (!options.TryGetValue(Ps5ConnectionSettings.PortKey, out string portText) ||
            !int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out int port) ||
            port is < 1 or > 65535)
        {
            throw new ArgumentException("The ps5debug-NG port must be a number between 1 and 65535.", nameof(options));
        }

        Ps5DebugClient client = await Ps5DebugClient
            .ConnectAsync(host.Trim(), port, cancellationToken)
            .ConfigureAwait(false);

        return new Ps5TargetSession(Metadata, client);
    }
}
