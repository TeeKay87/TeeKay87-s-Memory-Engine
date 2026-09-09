using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace TeeKay87.MemoryEngine.App.Theming;

public sealed class ThemeManager
{
    public const string DefaultThemeId = "dark";

    private static readonly IReadOnlyDictionary<string, string> LegacyThemeIdAliases =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["darker"] = "dimmed",
            ["darkest"] = "dark"
        });

    private static readonly IReadOnlyDictionary<string, string> LegacyBundledThemeFileReplacements =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Darker.json"] = "Dimmed.json",
            ["Darkest.json"] = "Dark.json"
        });

    private static readonly IReadOnlyDictionary<string, string> PaletteResourceKeys =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WindowBackground"] = "WindowBackgroundBrush",
            ["PanelBackground"] = "PanelBackgroundBrush",
            ["PanelSecondary"] = "PanelSecondaryBrush",
            ["SurfaceRaised"] = "SurfaceRaisedBrush",
            ["Border"] = "BorderBrush",
            ["BorderStrong"] = "BorderStrongBrush",
            ["PrimaryText"] = "PrimaryTextBrush",
            ["SecondaryText"] = "SecondaryTextBrush",
            ["Accent"] = "AccentBrush",
            ["AccentMuted"] = "AccentMutedBrush",
            ["Selection"] = "SelectionBrush",
            ["InputBackground"] = "InputBackgroundBrush",
            ["InputBorder"] = "InputBorderBrush",
            ["PrimaryButtonBackground"] = "PrimaryButtonBackgroundBrush",
            ["PrimaryButtonBorder"] = "PrimaryButtonBorderBrush",
            ["PrimaryButtonText"] = "PrimaryButtonTextBrush",
            ["SecondaryButtonBackground"] = "SecondaryButtonBackgroundBrush",
            ["SecondaryButtonBorder"] = "SecondaryButtonBorderBrush",
            ["SecondaryButtonText"] = "SecondaryButtonTextBrush",
            ["DisabledBackground"] = "DisabledButtonBackgroundBrush",
            ["DisabledBorder"] = "DisabledButtonBorderBrush",
            ["DisabledText"] = "DisabledButtonTextBrush",
            ["DangerBackground"] = "DangerButtonBackgroundBrush",
            ["DangerBorder"] = "DangerButtonBorderBrush",
            ["DangerText"] = "DangerButtonTextBrush",
            ["ErrorText"] = "ErrorTextBrush",
            ["SuccessText"] = "SuccessTextBrush",
            ["WarningText"] = "WarningTextBrush",
            ["ToolTipBackground"] = "ToolTipBackgroundBrush",
            ["ToolTipBorder"] = "ToolTipBorderBrush",
            ["ToolTipText"] = "ToolTipTextBrush",
            ["HoverOverlay"] = "ButtonHoverOverlayBrush",
            ["PressedOverlay"] = "ButtonPressedOverlayBrush"
        });

    private static readonly IReadOnlyDictionary<string, (string ResourceKey, string FallbackPaletteKey)> OptionalPaletteResourceKeys =
        new ReadOnlyDictionary<string, (string ResourceKey, string FallbackPaletteKey)>(
            new Dictionary<string, (string ResourceKey, string FallbackPaletteKey)>(StringComparer.OrdinalIgnoreCase)
            {
                ["DisassemblyMnemonic"] = ("DisassemblyMnemonicBrush", "Accent"),
                ["DisassemblyFlowControl"] = ("DisassemblyFlowControlBrush", "WarningText"),
                ["DisassemblyRegister"] = ("DisassemblyRegisterBrush", "SuccessText"),
                ["DisassemblyNumber"] = ("DisassemblyNumberBrush", "WarningText"),
                ["DisassemblyKeyword"] = ("DisassemblyKeywordBrush", "SecondaryText")
            });

    private readonly ResourceDictionary _applicationResources;
    private readonly string _themeDirectory;
    private readonly ThemePreferenceStore _preferenceStore;
    private readonly Dictionary<string, LoadedTheme> _themesById = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ThemeDescriptor> _themes = Array.Empty<ThemeDescriptor>();

    public ThemeManager(
        ResourceDictionary applicationResources,
        string themeDirectory,
        string settingsPath)
    {
        _applicationResources = applicationResources ?? throw new ArgumentNullException(nameof(applicationResources));
        ArgumentException.ThrowIfNullOrWhiteSpace(themeDirectory);
        _themeDirectory = themeDirectory;
        _preferenceStore = new ThemePreferenceStore(settingsPath);
    }

    public IReadOnlyList<ThemeDescriptor> Themes => _themes;

    public ThemeDescriptor? ActiveTheme { get; private set; }

    public IReadOnlyList<string> LoadErrors { get; private set; } = Array.Empty<string>();

    public void Initialize()
    {
        LoadThemes();

        if (_themes.Count == 0)
        {
            ActiveTheme = null;
            return;
        }

        string? preferredThemeId = _preferenceStore.LoadThemeId();
        ThemeDescriptor initialTheme = FindTheme(preferredThemeId)
            ?? FindTheme(DefaultThemeId)
            ?? _themes[0];

        ApplyTheme(initialTheme.Id, persistSelection: false);

        string? migratedThemeId = GetLegacyThemeReplacement(preferredThemeId);
        if (migratedThemeId is not null &&
            string.Equals(initialTheme.Id, migratedThemeId, StringComparison.OrdinalIgnoreCase))
        {
            _preferenceStore.SaveThemeId(migratedThemeId);
        }
    }

    public bool ApplyTheme(string themeId)
    {
        return ApplyTheme(themeId, persistSelection: true);
    }

    private void LoadThemes()
    {
        _themesById.Clear();
        List<string> errors = new();

        if (!Directory.Exists(_themeDirectory))
        {
            errors.Add($"Theme directory was not found: {_themeDirectory}");
            LoadErrors = errors;
            _themes = Array.Empty<ThemeDescriptor>();
            return;
        }

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        string[] themeFiles;
        try
        {
            themeFiles = Directory.GetFiles(_themeDirectory, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errors.Add($"Theme directory could not be read: {exception.Message}");
            LoadErrors = errors;
            _themes = Array.Empty<ThemeDescriptor>();
            return;
        }

        HashSet<string> themeFileNames = themeFiles
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string path in themeFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (ShouldIgnoreLegacyBundledThemeFile(path, themeFileNames))
            {
                continue;
            }

            try
            {
                ThemeDefinition? definition = JsonSerializer.Deserialize<ThemeDefinition>(
                    File.ReadAllText(path),
                    options);

                if (definition is null)
                {
                    throw new InvalidDataException("The file did not contain a theme definition.");
                }

                LoadedTheme loadedTheme = ValidateAndCreateTheme(definition, path);
                if (!_themesById.TryAdd(loadedTheme.Descriptor.Id, loadedTheme))
                {
                    throw new InvalidDataException(
                        $"Theme id '{loadedTheme.Descriptor.Id}' is already used by another theme file.");
                }
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                JsonException or
                InvalidDataException or
                FormatException or
                ArgumentException)
            {
                errors.Add($"{Path.GetFileName(path)}: {exception.Message}");
            }
        }

        _themes = _themesById.Values
            .Select(theme => theme.Descriptor)
            .OrderBy(theme => theme.Order)
            .ThenBy(theme => theme.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        LoadErrors = errors;
    }

    private bool ApplyTheme(string themeId, bool persistSelection)
    {
        string? resolvedThemeId = ResolveThemeId(themeId);
        if (resolvedThemeId is null ||
            !_themesById.TryGetValue(resolvedThemeId, out LoadedTheme? theme) ||
            theme is null)
        {
            return false;
        }

        foreach ((string paletteKey, string resourceKey) in PaletteResourceKeys)
        {
            Color color = theme.Colors[paletteKey];
            SolidColorBrush brush = new(color);
            brush.Freeze();
            _applicationResources[resourceKey] = brush;
        }

        foreach (KeyValuePair<string, (string ResourceKey, string FallbackPaletteKey)> entry in OptionalPaletteResourceKeys)
        {
            Color color = theme.Colors[entry.Key];
            SolidColorBrush brush = new(color);
            brush.Freeze();
            _applicationResources[entry.Value.ResourceKey] = brush;
        }

        Color successColor = theme.Colors["SuccessText"];
        SolidColorBrush successMutedBrush = new(Color.FromArgb(
            0x38,
            successColor.R,
            successColor.G,
            successColor.B));
        successMutedBrush.Freeze();
        _applicationResources["SuccessMutedBrush"] = successMutedBrush;

        ActiveTheme = theme.Descriptor;

        if (persistSelection)
        {
            _preferenceStore.SaveThemeId(theme.Descriptor.Id);
        }

        return true;
    }

    private ThemeDescriptor? FindTheme(string? themeId)
    {
        string? resolvedThemeId = ResolveThemeId(themeId);
        if (resolvedThemeId is null ||
            !_themesById.TryGetValue(resolvedThemeId, out LoadedTheme? theme) ||
            theme is null)
        {
            return null;
        }

        return theme.Descriptor;
    }

    private static string? ResolveThemeId(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId))
        {
            return null;
        }

        string normalizedThemeId = themeId.Trim();
        return LegacyThemeIdAliases.TryGetValue(normalizedThemeId, out string? replacement)
            ? replacement
            : normalizedThemeId;
    }

    private static string? GetLegacyThemeReplacement(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId))
        {
            return null;
        }

        return LegacyThemeIdAliases.TryGetValue(themeId.Trim(), out string? replacement)
            ? replacement
            : null;
    }

    private static bool ShouldIgnoreLegacyBundledThemeFile(
        string path,
        IReadOnlySet<string> discoveredFileNames)
    {
        string fileName = Path.GetFileName(path);
        return LegacyBundledThemeFileReplacements.TryGetValue(fileName, out string? replacementFileName) &&
               replacementFileName is not null &&
               discoveredFileNames.Contains(replacementFileName);
    }

    private static LoadedTheme ValidateAndCreateTheme(ThemeDefinition definition, string path)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new InvalidDataException("Theme id is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new InvalidDataException("Theme name is required.");
        }

        Dictionary<string, string> normalizedColors = new(
            definition.Colors ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Color> colors = new(StringComparer.OrdinalIgnoreCase);

        foreach (string paletteKey in PaletteResourceKeys.Keys)
        {
            if (!normalizedColors.TryGetValue(paletteKey, out string? value) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"Required color '{paletteKey}' is missing.");
            }

            colors[paletteKey] = ParseColor(value, paletteKey, path);
        }

        foreach (KeyValuePair<string, (string ResourceKey, string FallbackPaletteKey)> entry in OptionalPaletteResourceKeys)
        {
            string paletteKey = entry.Key;
            if (normalizedColors.TryGetValue(paletteKey, out string? value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                colors[paletteKey] = ParseColor(value, paletteKey, path);
            }
            else
            {
                colors[paletteKey] = colors[entry.Value.FallbackPaletteKey];
            }
        }

        string canonicalThemeId = ResolveThemeId(definition.Id)
            ?? throw new InvalidDataException("Theme id is required.");

        ThemeDescriptor descriptor = new(
            canonicalThemeId,
            definition.Name.Trim(),
            definition.Order);

        return new LoadedTheme(descriptor, colors);
    }

    private static Color ParseColor(string value, string paletteKey, string path)
    {
        string trimmed = value.Trim();
        if (trimmed.Length is not (7 or 9) || trimmed[0] != '#')
        {
            throw new InvalidDataException(
                $"Color '{paletteKey}' in {Path.GetFileName(path)} must use #RRGGBB or #AARRGGBB format.");
        }

        string hex = trimmed[1..];
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed))
        {
            throw new InvalidDataException(
                $"Color '{paletteKey}' in {Path.GetFileName(path)} contains invalid hexadecimal digits.");
        }

        return hex.Length == 6
            ? Color.FromRgb(
                (byte)(parsed >> 16),
                (byte)(parsed >> 8),
                (byte)parsed)
            : Color.FromArgb(
                (byte)(parsed >> 24),
                (byte)(parsed >> 16),
                (byte)(parsed >> 8),
                (byte)parsed);
    }

    private sealed record LoadedTheme(
        ThemeDescriptor Descriptor,
        IReadOnlyDictionary<string, Color> Colors);
}
