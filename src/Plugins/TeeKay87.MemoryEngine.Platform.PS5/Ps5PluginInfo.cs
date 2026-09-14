using System;

namespace TeeKay87.MemoryEngine.Platform.PS5;

public static class Ps5PluginInfo
{
    public const string Id = "platform.ps5.ps5debug-ng";
    public const string Name = "PlayStation 5";
    public const string Version = "0.1.2";
    public const int Revision = 39;
    public const string ApiVersion = "2.18.0";

    public static string DisplayVersion => $"{Version}.rev{Revision}";

    public static System.Version SemanticVersion { get; } = System.Version.Parse(Version);

    public static System.Version TargetApiVersion { get; } = System.Version.Parse(ApiVersion);
}
