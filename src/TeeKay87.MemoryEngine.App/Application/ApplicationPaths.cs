using System;
using System.IO;

namespace TeeKay87.MemoryEngine.App.Application;

public static class ApplicationPaths
{
    public static string LocalDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeeKay87",
        "MemoryEngine");

    public static string SettingsPath => Path.Combine(LocalDataDirectory, "settings.json");

    public static string DefaultScanResultsStoragePath => Path.Combine(LocalDataDirectory, "ScanResults");

    public static string LogPath => Path.Combine(LocalDataDirectory, "Logs", "MemoryEngine.log");

    public static string ThemeDirectory => Path.Combine(AppContext.BaseDirectory, "Themes");

    public static string PluginDirectory => Path.Combine(AppContext.BaseDirectory, "Plugins");
}
