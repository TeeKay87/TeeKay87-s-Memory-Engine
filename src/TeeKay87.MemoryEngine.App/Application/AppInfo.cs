namespace TeeKay87.MemoryEngine.App.Application;

public static class AppInfo
{
    public const string Title = "TeeKay87's Memory Engine";
    public const string Version = "0.1.7";
    public const int Revision = 31;
    public const string FeatureTitle = "Safe PS5 Watchpoint Detach Cleanup";

    public static string DisplayVersion => $"{Version}.rev{Revision}";

    public static string WindowTitle => Title;
}
