using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using TeeKay87.MemoryEngine.App.Application;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.Plugins;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly PluginHost _pluginHost = new();
    private PluginViewModel? _selectedPlugin;
    private string _statusText = string.Empty;
    private string _errorText = string.Empty;
    private bool _disposed;

    public MainWindowViewModel()
    {
        ReloadPluginsCommand = new RelayCommand(ReloadPlugins);
        ReloadPlugins();
    }

    public string WindowTitle => AppInfo.WindowTitle;

    public string ApplicationTitle => AppInfo.Title;

    public string DisplayVersion => AppInfo.DisplayVersion;

    public string FeatureTitle => AppInfo.FeatureTitle;

    public string PluginDirectory => Path.Combine(AppContext.BaseDirectory, "Plugins");

    public ObservableCollection<PluginViewModel> Plugins { get; } = new();

    public PluginViewModel? SelectedPlugin
    {
        get => _selectedPlugin;
        set => SetProperty(ref _selectedPlugin, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public ICommand ReloadPluginsCommand { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SelectedPlugin = null;
        Plugins.Clear();
        _pluginHost.Dispose();
        _disposed = true;
    }

    private void ReloadPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        SelectedPlugin = null;
        Plugins.Clear();

        PluginDiscoveryResult result = _pluginHost.Discover(PluginDirectory);

        foreach (DiscoveredPlugin plugin in result.Plugins)
        {
            Plugins.Add(new PluginViewModel(plugin));
        }

        SelectedPlugin = Plugins.FirstOrDefault();

        StatusText = Plugins.Count switch
        {
            0 => "No platform plugins were discovered.",
            1 => "1 platform plugin discovered.",
            _ => $"{Plugins.Count} platform plugins discovered."
        };

        ErrorText = result.Errors.Count == 0
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                result.Errors.Select(error => $"{Path.GetFileName(error.AssemblyPath)}: {error.Message}"));
    }
}
