using System;
using System.IO;
using System.Windows;
using TeeKay87.MemoryEngine.App.Theming;
using WpfApplication = System.Windows.Application;

namespace TeeKay87.MemoryEngine.App;

public partial class App : WpfApplication
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string themeDirectory = Path.Combine(AppContext.BaseDirectory, "Themes");
        string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeeKay87",
            "MemoryEngine",
            "settings.json");

        ThemeManager themeManager = new(Resources, themeDirectory, settingsPath);
        themeManager.Initialize();

        MainWindow window = new(themeManager);
        MainWindow = window;
        window.Show();
    }
}
