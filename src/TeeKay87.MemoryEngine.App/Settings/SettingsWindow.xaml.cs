using System;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TeeKay87.MemoryEngine.App.Application;
using TeeKay87.MemoryEngine.Core.Scanning.Storage;

namespace TeeKay87.MemoryEngine.App.Settings;

internal partial class SettingsWindow : Window
{
    private readonly ApplicationSettingsStore _settingsStore;

    public SettingsWindow(
        ApplicationSettingsStore settingsStore,
        string? activeStorageRoot,
        string? storageStartupError)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        InitializeComponent();

        SavedAddressesUpdateIntervalMilliseconds = _settingsStore.LoadSavedAddressesUpdateIntervalMilliseconds();
        SavedAddressesUpdateIntervalTextBox.Text =
            SavedAddressesUpdateIntervalMilliseconds.ToString(CultureInfo.InvariantCulture);
        FrozenWriteIntervalMilliseconds = _settingsStore.LoadFrozenWriteIntervalMilliseconds();
        FrozenWriteIntervalTextBox.Text =
            FrozenWriteIntervalMilliseconds.ToString(CultureInfo.InvariantCulture);

        string configuredPath = _settingsStore.LoadScanResultsStorageLocation()
            ?? ApplicationPaths.DefaultScanResultsStoragePath;
        StoragePathTextBox.Text = configuredPath;
        ActiveStoragePathTextBlock.Text = string.IsNullOrWhiteSpace(activeStorageRoot)
            ? "Unavailable for this application session."
            : activeStorageRoot;

        if (!string.IsNullOrWhiteSpace(storageStartupError))
        {
            ShowValidationError(storageStartupError);
        }
    }

    public int SavedAddressesUpdateIntervalMilliseconds { get; private set; }

    public int FrozenWriteIntervalMilliseconds { get; private set; }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new()
        {
            Title = "Choose Scan Results Storage Location",
            Multiselect = false
        };

        string currentPath = StoragePathTextBox.Text.Trim();
        if (Directory.Exists(currentPath))
        {
            dialog.InitialDirectory = currentPath;
        }

        if (dialog.ShowDialog(this) == true)
        {
            StoragePathTextBox.Text = dialog.FolderName;
            ClearValidationError();
        }
    }

    private void UseDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        StoragePathTextBox.Text = ApplicationPaths.DefaultScanResultsStoragePath;
        ClearValidationError();
    }

    private void UseDefaultSavedAddressesIntervalButton_Click(object sender, RoutedEventArgs e)
    {
        SavedAddressesUpdateIntervalTextBox.Text =
            ApplicationSettingsStore.DefaultSavedAddressesUpdateIntervalMilliseconds.ToString(CultureInfo.InvariantCulture);
        ClearValidationError();
    }

    private void UseDefaultFrozenWriteIntervalButton_Click(object sender, RoutedEventArgs e)
    {
        FrozenWriteIntervalTextBox.Text =
            ApplicationSettingsStore.DefaultFrozenWriteIntervalMilliseconds.ToString(CultureInfo.InvariantCulture);
        ClearValidationError();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ClearValidationError();
        SaveStatusTextBlock.Text = string.Empty;

        if (!int.TryParse(
                SavedAddressesUpdateIntervalTextBox.Text.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int savedAddressesUpdateInterval) ||
            !ApplicationSettingsStore.IsValidSavedAddressesUpdateInterval(savedAddressesUpdateInterval))
        {
            ShowValidationError(
                $"Saved Addresses update interval must be a whole number from " +
                $"{ApplicationSettingsStore.MinimumSavedAddressesUpdateIntervalMilliseconds:N0} through " +
                $"{ApplicationSettingsStore.MaximumSavedAddressesUpdateIntervalMilliseconds:N0} milliseconds.");
            return;
        }

        if (!int.TryParse(
                FrozenWriteIntervalTextBox.Text.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int frozenWriteInterval) ||
            !ApplicationSettingsStore.IsValidFrozenWriteInterval(frozenWriteInterval))
        {
            ShowValidationError(
                $"Frozen write interval must be a whole number from " +
                $"{ApplicationSettingsStore.MinimumFrozenWriteIntervalMilliseconds:N0} through " +
                $"{ApplicationSettingsStore.MaximumFrozenWriteIntervalMilliseconds:N0} milliseconds.");
            return;
        }

        if (!ScanResultStoragePathValidator.TryValidateAndPrepare(
                StoragePathTextBox.Text,
                out string validatedPath,
                out string validationError))
        {
            ShowValidationError($"The selected storage location cannot be used: {validationError}");
            return;
        }

        try
        {
            _settingsStore.SaveSavedAddressesUpdateIntervalMilliseconds(savedAddressesUpdateInterval);
            _settingsStore.SaveFrozenWriteIntervalMilliseconds(frozenWriteInterval);
            _settingsStore.SaveScanResultsStorageLocation(validatedPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowValidationError($"The setting could not be saved: {exception.Message}");
            return;
        }

        SavedAddressesUpdateIntervalMilliseconds = savedAddressesUpdateInterval;
        SavedAddressesUpdateIntervalTextBox.Text =
            savedAddressesUpdateInterval.ToString(CultureInfo.InvariantCulture);
        FrozenWriteIntervalMilliseconds = frozenWriteInterval;
        FrozenWriteIntervalTextBox.Text = frozenWriteInterval.ToString(CultureInfo.InvariantCulture);
        StoragePathTextBox.Text = validatedPath;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ShowValidationError(string message)
    {
        ValidationErrorTextBlock.Text = message;
        ValidationErrorTextBlock.Visibility = Visibility.Visible;
    }

    private void ClearValidationError()
    {
        ValidationErrorTextBlock.Text = string.Empty;
        ValidationErrorTextBlock.Visibility = Visibility.Collapsed;
    }
}
