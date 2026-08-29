namespace TeeKay87.MemoryEngine.App.Application;

public static class AppInfo
{
    public const string Title = "TeeKay87's Memory Engine";
    public const string Version = "0.1.0";
    public const int Revision = 2;
    public const string FeatureTitle = "WPF Application Namespace Compile Fix";

    public static string DisplayVersion => $"{Version}.rev{Revision}";

    public static string WindowTitle => $"{Title} {DisplayVersion}";
}
