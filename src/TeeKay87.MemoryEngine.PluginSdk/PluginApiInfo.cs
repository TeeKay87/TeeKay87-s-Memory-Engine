using System;

namespace TeeKay87.MemoryEngine.PluginSdk;

public static class PluginApiInfo
{
    public const string CurrentVersionText = "2.16.0";

    public static Version CurrentVersion { get; } = Version.Parse(CurrentVersionText);

    public static bool IsCompatible(Version requestedVersion)
    {
        ArgumentNullException.ThrowIfNull(requestedVersion);

        return requestedVersion.Major == CurrentVersion.Major &&
               requestedVersion.Minor <= CurrentVersion.Minor;
    }
}
