using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

public sealed class Ps5TargetPlugin : ITargetPlugin, IPluginSettingsConsumer
{
    private IPluginSettings? _settings;
    private IReadOnlyList<TargetConnectionSettingDefinition> _connectionSettings =
        Ps5ConnectionSettings.CreateDefinitions(settings: null);

    public PluginMetadata Metadata { get; } = new(
        Id: Ps5PluginInfo.Id,
        Name: Ps5PluginInfo.Name,
        Platform: "PlayStation 5",
        Backend: "ps5debug-NG",
        Version: Ps5PluginInfo.SemanticVersion,
        Revision: Ps5PluginInfo.Revision,
        ApiVersion: Ps5PluginInfo.TargetApiVersion,
        Description: "Connects TeeKay87's Memory Engine to a PlayStation 5 running ps5debug-NG and exposes process enumeration, preferred game-process selection, memory maps, raw memory access, plugin-driven scan options, semantic native Scan Type acceleration with Core fallback, process pause/resume, x86-64 disassembly, debugger attach/pause/continue/detach, thread enumeration/control, and read-only general-purpose register snapshots through a dedicated ps5debug-NG debugger transport.",
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
        TargetCapabilities.NativeValueScanning |
        TargetCapabilities.Disassembly |
        TargetCapabilities.Debugger |
        TargetCapabilities.ThreadEnumeration |
        TargetCapabilities.ThreadControl |
        TargetCapabilities.RegisterAccess;

    public IReadOnlyList<TargetConnectionSettingDefinition> ConnectionSettings => _connectionSettings;

    public IReadOnlyList<IMemoryValueType> SupportedValueTypes => Ps5ScanDefinitions.ValueTypes;

    public IReadOnlyList<IMemoryScanOption> SupportedScanOptions => Ps5ScanDefinitions.ScanOptions;

    public string DefaultValueTypeId => Ps5ScanDefinitions.DefaultValueTypeId;

    public void AttachSettings(IPluginSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _connectionSettings = Ps5ConnectionSettings.CreateDefinitions(_settings);
    }

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

        string normalizedHost = host.Trim();
        string normalizedPort = port.ToString(CultureInfo.InvariantCulture);

        Ps5DebugClient client = await Ps5DebugClient
            .ConnectAsync(normalizedHost, port, cancellationToken)
            .ConfigureAwait(false);

        _settings?.TrySetString(Ps5ConnectionSettings.SavedHostKey, normalizedHost);
        _settings?.TrySetString(Ps5ConnectionSettings.SavedPortKey, normalizedPort);

        return new Ps5TargetSession(Metadata, client, normalizedHost, port);
    }
}
