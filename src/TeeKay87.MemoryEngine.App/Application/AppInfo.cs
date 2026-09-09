namespace TeeKay87.MemoryEngine.App.Application;

public static class AppInfo
{
    public const string Title = "TeeKay87's Memory Engine";
    public const string Version = "0.1.7";
    public const int Revision = 7;
    public const string FeatureTitle = "Registers and Stop Context";

    public static string DisplayVersion => $"{Version}.rev{Revision}";

    public static string WindowTitle => Title;
}
