using System;
using System.Windows;
using TeeKay87.MemoryEngine.App.Application;
using TeeKay87.MemoryEngine.App.Diagnostics;
using TeeKay87.MemoryEngine.App.Settings;
using TeeKay87.MemoryEngine.App.Theming;
using TeeKay87.MemoryEngine.Core.Scanning.Storage;
using WpfApplication = System.Windows.Application;

namespace TeeKay87.MemoryEngine.App;

public partial class App : WpfApplication
{
    private ScanResultStorageManager? _scanResultStorageManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplicationSettingsStore settingsStore = new(ApplicationPaths.SettingsPath);
        FileApplicationLogger logger = new(ApplicationPaths.LogPath);

        ThemeManager themeManager = new(
            Resources,
            ApplicationPaths.ThemeDirectory,
            ApplicationPaths.SettingsPath);
        themeManager.Initialize();

        string configuredStoragePath = settingsStore.LoadScanResultsStorageLocation()
            ?? ApplicationPaths.DefaultScanResultsStoragePath;
        string storageStartupError = string.Empty;

        try
        {
            _scanResultStorageManager = new ScanResultStorageManager(configuredStoragePath, logger);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            System.IO.IOException or
            System.IO.InvalidDataException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            storageStartupError =
                $"Scan-result storage could not be initialized at '{configuredStoragePath}': {exception.Message}";
            logger.Error(storageStartupError, exception);
        }

        MainWindow window = new(
            themeManager,
            settingsStore,
            _scanResultStorageManager,
            storageStartupError);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scanResultStorageManager?.Dispose();
        _scanResultStorageManager = null;
        base.OnExit(e);
    }
}
