using System;
using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal static class Ps5ConnectionSettings
{
    public const string HostKey = "host";
    public const string PortKey = "port";
    public const int DefaultPort = 744;

    public static IReadOnlyList<TargetConnectionSettingDefinition> Definitions { get; } =
        Array.AsReadOnly(new[]
        {
            new TargetConnectionSettingDefinition(
                HostKey,
                "PS5 IP address or host name",
                "Address of the PlayStation 5 running ps5debug-NG.",
                IsRequired: true),
            new TargetConnectionSettingDefinition(
                PortKey,
                "Port",
                "Main ps5debug-NG command-server port.",
                DefaultPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                IsRequired: true)
        });
}
