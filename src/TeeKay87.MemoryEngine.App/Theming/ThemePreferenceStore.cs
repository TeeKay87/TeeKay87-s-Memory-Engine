using System;
using System.IO;
using TeeKay87.MemoryEngine.App.Settings;

namespace TeeKay87.MemoryEngine.App.Theming;

internal sealed class ThemePreferenceStore
{
    private readonly ApplicationSettingsStore _settingsStore;

    public ThemePreferenceStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsStore = new ApplicationSettingsStore(settingsPath);
    }

    public string? LoadThemeId()
    {
        return _settingsStore.LoadThemeId();
    }

    public void SaveThemeId(string themeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeId);

        try
        {
            _settingsStore.SaveThemeId(themeId);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
