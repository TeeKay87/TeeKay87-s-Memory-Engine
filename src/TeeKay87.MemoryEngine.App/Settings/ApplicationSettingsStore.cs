using System;
using System.Globalization;
using TeeKay87.MemoryEngine.Core.Settings;

namespace TeeKay87.MemoryEngine.App.Settings;

internal sealed class ApplicationSettingsStore
{
    private const string ThemeIdKey = "themeId";
    private const string ScanResultsStorageLocationKey = "scanResultsStorageLocation";
    private const string SavedAddressesUpdateIntervalKey = "savedAddressesUpdateIntervalMilliseconds";
    private const string FrozenWriteIntervalKey = "frozenWriteIntervalMilliseconds";
    private readonly JsonSettingsStore _settingsStore;

    public const int DefaultSavedAddressesUpdateIntervalMilliseconds = 500;
    public const int MinimumSavedAddressesUpdateIntervalMilliseconds = 50;
    public const int MaximumSavedAddressesUpdateIntervalMilliseconds = 10_000;

    public const int DefaultFrozenWriteIntervalMilliseconds = 100;
    public const int MinimumFrozenWriteIntervalMilliseconds = 50;
    public const int MaximumFrozenWriteIntervalMilliseconds = 10_000;

    public ApplicationSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsStore = new JsonSettingsStore(settingsPath);
    }

    internal JsonSettingsStore SharedStore => _settingsStore;

    public string? LoadThemeId()
    {
        return NormalizeOptionalValue(_settingsStore.LoadApplicationString(ThemeIdKey));
    }

    public string? LoadScanResultsStorageLocation()
    {
        return NormalizeOptionalValue(_settingsStore.LoadApplicationString(ScanResultsStorageLocationKey));
    }

    public int LoadSavedAddressesUpdateIntervalMilliseconds()
    {
        string? storedValue = NormalizeOptionalValue(
            _settingsStore.LoadApplicationString(SavedAddressesUpdateIntervalKey));

        return int.TryParse(
                   storedValue,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out int interval) &&
               IsValidSavedAddressesUpdateInterval(interval)
            ? interval
            : DefaultSavedAddressesUpdateIntervalMilliseconds;
    }

    public int LoadFrozenWriteIntervalMilliseconds()
    {
        string? storedValue = NormalizeOptionalValue(
            _settingsStore.LoadApplicationString(FrozenWriteIntervalKey));

        return int.TryParse(
                   storedValue,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out int interval) &&
               IsValidFrozenWriteInterval(interval)
            ? interval
            : DefaultFrozenWriteIntervalMilliseconds;
    }

    public void SaveThemeId(string themeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeId);
        _settingsStore.SaveApplicationString(ThemeIdKey, themeId.Trim());
    }

    public void SaveScanResultsStorageLocation(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _settingsStore.SaveApplicationString(ScanResultsStorageLocationKey, path.Trim());
    }

    public void SaveSavedAddressesUpdateIntervalMilliseconds(int intervalMilliseconds)
    {
        if (!IsValidSavedAddressesUpdateInterval(intervalMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMilliseconds),
                $"Saved Addresses update interval must be between {MinimumSavedAddressesUpdateIntervalMilliseconds} and {MaximumSavedAddressesUpdateIntervalMilliseconds} milliseconds.");
        }

        _settingsStore.SaveApplicationString(
            SavedAddressesUpdateIntervalKey,
            intervalMilliseconds.ToString(CultureInfo.InvariantCulture));
    }

    public static bool IsValidSavedAddressesUpdateInterval(int intervalMilliseconds)
    {
        return intervalMilliseconds is >= MinimumSavedAddressesUpdateIntervalMilliseconds and
            <= MaximumSavedAddressesUpdateIntervalMilliseconds;
    }

    public void SaveFrozenWriteIntervalMilliseconds(int intervalMilliseconds)
    {
        if (!IsValidFrozenWriteInterval(intervalMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMilliseconds),
                $"Frozen write interval must be between {MinimumFrozenWriteIntervalMilliseconds} and {MaximumFrozenWriteIntervalMilliseconds} milliseconds.");
        }

        _settingsStore.SaveApplicationString(
            FrozenWriteIntervalKey,
            intervalMilliseconds.ToString(CultureInfo.InvariantCulture));
    }

    public static bool IsValidFrozenWriteInterval(int intervalMilliseconds)
    {
        return intervalMilliseconds is >= MinimumFrozenWriteIntervalMilliseconds and
            <= MaximumFrozenWriteIntervalMilliseconds;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
