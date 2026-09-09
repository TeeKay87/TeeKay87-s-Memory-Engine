using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal partial class MemoryEditDialog : Window
{
    private readonly byte[] _originalBytes;

    public MemoryEditDialog(ulong address, ReadOnlyMemory<byte> originalBytes)
    {
        if (originalBytes.IsEmpty)
        {
            throw new ArgumentException("Memory edit data cannot be empty.", nameof(originalBytes));
        }

        InitializeComponent();

        Address = address;
        _originalBytes = originalBytes.ToArray();
        string formattedBytes = FormatBytes(_originalBytes);

        Title = $"Edit Memory - 0x{address:X}";
        TargetTextBlock.Text = $"0x{address:X} · {_originalBytes.Length:N0} bytes";
        CurrentBytesTextBox.Text = formattedBytes;
        NewBytesTextBox.Text = formattedBytes;
        SafetyTextBlock.Text =
            "Before writing, Memory Viewer rereads these bytes and requires them to still match the displayed value. " +
            "The write is blocked if the target changed. A successful write is read back immediately for verification.";
    }

    public ulong Address { get; }

    public byte[]? ReplacementBytes { get; private set; }

    private void MemoryEditDialog_Loaded(object sender, RoutedEventArgs e)
    {
        NewBytesTextBox.Focus();
        NewBytesTextBox.SelectAll();
    }

    private void WriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseBytes(
                NewBytesTextBox.Text,
                _originalBytes.Length,
                out byte[] replacementBytes,
                out string error))
        {
            ValidationTextBlock.Text = error;
            NewBytesTextBox.Focus();
            return;
        }

        if (_originalBytes.AsSpan().SequenceEqual(replacementBytes))
        {
            ValidationTextBlock.Text = "Change at least one byte before writing.";
            NewBytesTextBox.Focus();
            return;
        }

        ReplacementBytes = replacementBytes;
        DialogResult = true;
        e.Handled = true;
    }

    private static bool TryParseBytes(
        string text,
        int expectedByteCount,
        out byte[] bytes,
        out string error)
    {
        string compact = string.Concat((text ?? string.Empty).Where(character => !char.IsWhiteSpace(character)));
        int expectedHexCharacters = checked(expectedByteCount * 2);

        if (compact.Length != expectedHexCharacters)
        {
            bytes = Array.Empty<byte>();
            error = $"Enter exactly {expectedByteCount:N0} bytes ({expectedHexCharacters:N0} hexadecimal characters).";
            return false;
        }

        bytes = new byte[expectedByteCount];
        for (int index = 0; index < bytes.Length; index++)
        {
            ReadOnlySpan<char> pair = compact.AsSpan(index * 2, 2);
            if (!byte.TryParse(pair, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[index]))
            {
                bytes = Array.Empty<byte>();
                error = "Use only hexadecimal byte values (00 through FF), optionally separated by whitespace.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static string FormatBytes(ReadOnlySpan<byte> bytes)
    {
        return string.Join(" ", bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
