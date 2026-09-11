using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal partial class BreakpointDialog : Window
{
    private readonly bool _supportsSoftwareBreakpoints;
    private readonly bool _supportsHardwareWatchpoints;

    public BreakpointDialog(
        bool supportsSoftwareBreakpoints,
        bool supportsHardwareWatchpoints,
        ulong? suggestedAddress = null)
    {
        if (!supportsSoftwareBreakpoints && !supportsHardwareWatchpoints)
        {
            throw new ArgumentException("At least one breakpoint or watchpoint capability must be available.");
        }

        _supportsSoftwareBreakpoints = supportsSoftwareBreakpoints;
        _supportsHardwareWatchpoints = supportsHardwareWatchpoints;

        InitializeComponent();
        Title = "Add Breakpoint / Watchpoint";
        SoftwareBreakpointItem.Visibility = supportsSoftwareBreakpoints ? Visibility.Visible : Visibility.Collapsed;
        HardwareWatchpointItem.Visibility = supportsHardwareWatchpoints ? Visibility.Visible : Visibility.Collapsed;
        KindComboBox.SelectedItem = supportsSoftwareBreakpoints
            ? SoftwareBreakpointItem
            : HardwareWatchpointItem;

        if (suggestedAddress.HasValue)
        {
            AddressTextBox.Text = $"0x{suggestedAddress.Value:X}";
        }

        UpdateKindPresentation();
    }

    public DebuggerBreakpointRequest? Request { get; private set; }

    private bool IsHardwareWatchpointSelected =>
        ReferenceEquals(KindComboBox.SelectedItem, HardwareWatchpointItem);

    private void BreakpointDialog_Loaded(object sender, RoutedEventArgs e)
    {
        AddressTextBox.Focus();
        AddressTextBox.SelectAll();
    }

    private void KindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HardwareOptionsGrid is not null)
        {
            UpdateKindPresentation();
        }
    }

    private void UpdateKindPresentation()
    {
        bool hardware = IsHardwareWatchpointSelected;
        HardwareOptionsGrid.Visibility = hardware ? Visibility.Visible : Visibility.Collapsed;
        ScopeTextBlock.Text = hardware
            ? "Hardware-watchpoint slot limits, supported access modes, legal sizes, alignment, and native encoding are validated by the active platform plugin."
            : "Software execute breakpoints use the existing one-byte neutral request. Backend slot allocation and instruction patching remain owned by the active platform plugin.";
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationTextBlock.Text = string.Empty;
        if (!TryParseHexAddress(AddressTextBox.Text, out ulong address))
        {
            ValidationTextBlock.Text = "Enter a hexadecimal address, with or without a 0x prefix.";
            AddressTextBox.Focus();
            return;
        }

        bool isTemporary = TemporaryCheckBox.IsChecked == true;
        if (!IsHardwareWatchpointSelected)
        {
            if (!_supportsSoftwareBreakpoints)
            {
                ValidationTextBlock.Text = "The active platform does not advertise software-breakpoint support.";
                return;
            }

            Request = new DebuggerBreakpointRequest(
                address,
                1,
                DebuggerBreakpointKind.Software,
                DebuggerBreakpointAccess.Execute,
                isTemporary);
        }
        else
        {
            if (!_supportsHardwareWatchpoints)
            {
                ValidationTextBlock.Text = "The active platform does not advertise hardware-watchpoint support.";
                return;
            }

            if (!int.TryParse(SizeTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int size) || size <= 0)
            {
                ValidationTextBlock.Text = "Enter a positive whole-number watchpoint size in bytes.";
                SizeTextBox.Focus();
                return;
            }

            if (!TryGetSelectedAccess(out DebuggerBreakpointAccess access))
            {
                ValidationTextBlock.Text = "Select a watchpoint access mode.";
                return;
            }

            Request = new DebuggerBreakpointRequest(
                address,
                size,
                DebuggerBreakpointKind.Hardware,
                access,
                isTemporary);
        }

        DialogResult = true;
        Close();
        e.Handled = true;
    }

    private bool TryGetSelectedAccess(out DebuggerBreakpointAccess access)
    {
        access = DebuggerBreakpointAccess.Write;
        if (AccessComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return false;
        }

        return Enum.TryParse(tag, ignoreCase: false, out access) && access != DebuggerBreakpointAccess.Execute;
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
