namespace TeeKay87.MemoryEngine.App.Application;

public static class AppInfo
{
    public const string Title = "TeeKay87's Memory Engine";
    public const string Version = "0.1.3";
    public const int Revision = 1;
    public const string FeatureTitle = "PS5 Value Type Expansion";

    public static string DisplayVersion => $"{Version}.rev{Revision}";

    public static string WindowTitle => $"{Title} {DisplayVersion}";
}
