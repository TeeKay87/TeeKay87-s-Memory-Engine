using System;
using System.Globalization;
using System.Windows;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal partial class RunToAddressDialog : Window
{
    public RunToAddressDialog(ulong? suggestedAddress = null)
    {
        InitializeComponent();
        if (suggestedAddress.HasValue)
        {
            AddressTextBox.Text = $"0x{suggestedAddress.Value:X}";
        }
    }

    public ulong? Address { get; private set; }

    private void RunToAddressDialog_Loaded(object sender, RoutedEventArgs e)
    {
        AddressTextBox.Focus();
        AddressTextBox.SelectAll();
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseHexAddress(AddressTextBox.Text, out ulong address))
        {
            ValidationTextBlock.Text = "Enter a valid hexadecimal address between 0x0 and 0xFFFFFFFFFFFFFFFF.";
            AddressTextBox.Focus();
            AddressTextBox.SelectAll();
            return;
        }

        Address = address;
        DialogResult = true;
        Close();
        e.Handled = true;
    }

    private static bool TryParseHexAddress(string? text, out ulong address)
    {
        address = 0;
        string candidate = (text ?? string.Empty).Trim();
        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[2..];
        }

        return candidate.Length is >= 1 and <= 16 &&
               ulong.TryParse(
                   candidate,
                   NumberStyles.AllowHexSpecifier,
                   CultureInfo.InvariantCulture,
                   out address);
    }
}
