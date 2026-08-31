using System;
using System.IO;
using System.Text.Json;

namespace TeeKay87.MemoryEngine.App.Theming;

internal sealed class ThemePreferenceStore
{
    private readonly string _settingsPath;

    public ThemePreferenceStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
    }

    public string? LoadThemeId()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            string json = File.ReadAllText(_settingsPath);
            ThemePreferences? preferences = JsonSerializer.Deserialize<ThemePreferences>(json);
            return string.IsNullOrWhiteSpace(preferences?.ThemeId)
                ? null
                : preferences.ThemeId.Trim();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void SaveThemeId(string themeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeId);

        try
        {
            string? directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(
                new ThemePreferences { ThemeId = themeId },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class ThemePreferences
    {
        public string? ThemeId { get; set; }
    }
}
