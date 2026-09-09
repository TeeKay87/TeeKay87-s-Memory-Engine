using System;

namespace TeeKay87.MemoryEngine.Platform.Mock;

public static class MockPluginInfo
{
    public const string Id = "platform.mock.in-memory";
    public const string Name = "In-Memory Test Target";
    public const string Version = "1.0.0";
    public const int Revision = 10;
    public const string ApiVersion = "2.13.0";

    public static string DisplayVersion => $"{Version}.rev{Revision}";

    public static System.Version SemanticVersion { get; } = System.Version.Parse(Version);

    public static System.Version TargetApiVersion { get; } = System.Version.Parse(ApiVersion);
}
