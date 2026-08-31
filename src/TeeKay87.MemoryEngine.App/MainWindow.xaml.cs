using System;
using System.Windows;
using TeeKay87.MemoryEngine.App.Theming;
using TeeKay87.MemoryEngine.App.ViewModels;

namespace TeeKay87.MemoryEngine.App;

public partial class MainWindow : Window
{
    public MainWindow(ThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        InitializeComponent();
        DataContext = new MainWindowViewModel(themeManager);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}
