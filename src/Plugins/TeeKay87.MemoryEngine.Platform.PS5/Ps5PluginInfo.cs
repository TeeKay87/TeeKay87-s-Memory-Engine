using System;

namespace TeeKay87.MemoryEngine.Platform.PS5;

public static class Ps5PluginInfo
{
    public const string Id = "platform.ps5.ps5debug-ng";
    public const string Name = "PlayStation 5";
    public const string Version = "0.1.0";
    public const int Revision = 9;
    public const string ApiVersion = "1.2.0";

    public static string DisplayVersion => $"{Version}.rev{Revision}";

    public static System.Version SemanticVersion { get; } = System.Version.Parse(Version);

    public static System.Version TargetApiVersion { get; } = System.Version.Parse(ApiVersion);
}
