using System;
using System.Collections.Generic;
using System.Globalization;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal static class Ps5ConnectionSettings
{
    public const string HostKey = "host";
    public const string PortKey = "port";
    public const string SavedHostKey = "connection.host";
    public const string SavedPortKey = "connection.port";
    public const int DefaultPort = 744;

    public static IReadOnlyList<TargetConnectionSettingDefinition> CreateDefinitions(IPluginSettings? settings)
    {
        string? savedHost = null;
        if (settings is not null &&
            settings.TryGetString(SavedHostKey, out string host) &&
            !string.IsNullOrWhiteSpace(host))
        {
            savedHost = host.Trim();
        }

        string portDefault = DefaultPort.ToString(CultureInfo.InvariantCulture);
        if (settings is not null &&
            settings.TryGetString(SavedPortKey, out string savedPort) &&
            int.TryParse(savedPort, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedPort) &&
            parsedPort is >= 1 and <= 65535)
        {
            portDefault = parsedPort.ToString(CultureInfo.InvariantCulture);
        }

        return Array.AsReadOnly(new[]
        {
            new TargetConnectionSettingDefinition(
                HostKey,
                "PS5 IP address or host name",
                "Address of the PlayStation 5 running ps5debug-NG.",
                savedHost,
                IsRequired: true),
            new TargetConnectionSettingDefinition(
                PortKey,
                "Port",
                "Main ps5debug-NG command-server port.",
                portDefault,
                IsRequired: true)
        });
    }
}
