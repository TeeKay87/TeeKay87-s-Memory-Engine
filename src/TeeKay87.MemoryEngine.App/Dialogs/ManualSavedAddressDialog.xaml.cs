using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TeeKay87.MemoryEngine.App.SavedAddresses;
using TeeKay87.MemoryEngine.App.ViewModels;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal partial class ManualSavedAddressDialog : Window
{
    private readonly IReadOnlyList<ScanValueTypeViewModel> _valueTypes;

    public ManualSavedAddressDialog(
        string targetDisplayName,
        IReadOnlyList<ScanValueTypeViewModel> valueTypes,
        ScanValueTypeViewModel? preferredValueType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDisplayName);
        ArgumentNullException.ThrowIfNull(valueTypes);
        if (valueTypes.Count == 0)
        {
            throw new ArgumentException("Manual Saved Address entry requires at least one plugin-defined Value Type.", nameof(valueTypes));
        }

        _valueTypes = valueTypes;
        InitializeComponent();

        Title = "Add Saved Address Manually";
        TargetTextBlock.Text = targetDisplayName;
        ValueTypeComboBox.ItemsSource = _valueTypes;
        ValueTypeComboBox.SelectedItem = preferredValueType is null
            ? _valueTypes[0]
            : _valueTypes.FirstOrDefault(option =>
                string.Equals(option.Id, preferredValueType.Id, StringComparison.OrdinalIgnoreCase)) ?? _valueTypes[0];
        RefreshValueTypeState();
    }

    public ManualSavedAddressDialogResult? Result { get; private set; }

    private void ManualSavedAddressDialog_Loaded(object sender, RoutedEventArgs e)
    {
        AddressTextBox.Focus();
    }

    private void ValueTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        RefreshValueTypeState();
        ValidationTextBlock.Text = string.Empty;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseHexAddress(AddressTextBox.Text, out ulong address))
        {
            ValidationTextBlock.Text = "Enter a hexadecimal address, with or without a 0x prefix.";
            AddressTextBox.Focus();
            return;
        }

        if (ValueTypeComboBox.SelectedItem is not ScanValueTypeViewModel valueType)
        {
            ValidationTextBlock.Text = "Choose a Value Type.";
            ValueTypeComboBox.Focus();
            return;
        }

        if (!TryResolveValueSize(valueType, out int valueSize, out string lengthError))
        {
            ValidationTextBlock.Text = lengthError;
            LengthTextBox.Focus();
            return;
        }

        Result = new ManualSavedAddressDialogResult(
            address,
            DescriptionTextBox.Text?.Trim() ?? string.Empty,
            valueType,
            valueSize);
        DialogResult = true;
        Close();
        e.Handled = true;
    }

    private void RefreshValueTypeState()
    {
        if (ValueTypeComboBox.SelectedItem is not ScanValueTypeViewModel valueType)
        {
            return;
        }

        ValueTypeDescriptionTextBlock.Text =
            $"{valueType.Description} Signedness is represented by the selected Value Type rather than a separate checkbox.";

        if (valueType.ValueType.FixedSize is int fixedSize)
        {
            LengthTextBox.Text = fixedSize.ToString(CultureInfo.InvariantCulture);
            LengthTextBox.IsEnabled = false;
            LengthDescriptionTextBlock.Text = $"{valueType.DisplayName} has a fixed size of {fixedSize:N0} byte(s).";
            return;
        }

        LengthTextBox.IsEnabled = true;
        if (!int.TryParse(LengthTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int currentLength) ||
            currentLength is < ManualSavedAddressEntryLimits.MinimumVariableLength or > ManualSavedAddressEntryLimits.MaximumVariableLength)
        {
            LengthTextBox.Text = ManualSavedAddressEntryLimits.DefaultVariableLength.ToString(CultureInfo.InvariantCulture);
        }

        LengthDescriptionTextBlock.Text =
            $"{valueType.DisplayName} is variable-length. Choose {ManualSavedAddressEntryLimits.MinimumVariableLength:N0} through {ManualSavedAddressEntryLimits.MaximumVariableLength:N0} bytes to read and track.";
    }

    private bool TryResolveValueSize(
        ScanValueTypeViewModel valueType,
        out int valueSize,
        out string error)
    {
        if (valueType.ValueType.FixedSize is int fixedSize)
        {
            valueSize = fixedSize;
            error = string.Empty;
            return true;
        }

        if (!int.TryParse(LengthTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out valueSize) ||
            valueSize is < ManualSavedAddressEntryLimits.MinimumVariableLength or > ManualSavedAddressEntryLimits.MaximumVariableLength)
        {
            error = $"Enter a variable Value Type length from {ManualSavedAddressEntryLimits.MinimumVariableLength:N0} through {ManualSavedAddressEntryLimits.MaximumVariableLength:N0} bytes.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryParseHexAddress(string text, out ulong address)
    {
        address = 0;
        string candidate = (text ?? string.Empty).Trim();
        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[2..];
        }

        return candidate.Length is > 0 and <= 16 &&
               ulong.TryParse(candidate, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out address);
    }
}
