using System;
using System.Linq;
using System.Windows;
using TeeKay87.MemoryEngine.App.Debugging;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal partial class RegisterEditDialog : Window
{
    private readonly DebuggerRegister _register;

    public RegisterEditDialog(DebuggerRegister register)
    {
        _register = register ?? throw new ArgumentNullException(nameof(register));
        if (!_register.CanWrite)
        {
            throw new InvalidOperationException("The selected register is read-only.");
        }

        InitializeComponent();

        string currentValue = DebuggerRegisterValueCodec.Format(_register);
        Title = $"Edit Register - {_register.DisplayName}";
        RegisterDetailsTextBlock.Text = string.IsNullOrWhiteSpace(_register.Group)
            ? $"{_register.DisplayName} · {_register.BitWidth}-bit"
            : $"{_register.DisplayName} · {_register.BitWidth}-bit · {_register.Group}";
        CurrentValueTextBox.Text = currentValue;
        NewValueTextBox.Text = currentValue;
    }

    public byte[]? ReplacementValue { get; private set; }

    private void RegisterEditDialog_Loaded(object sender, RoutedEventArgs e)
    {
        NewValueTextBox.Focus();
        NewValueTextBox.SelectAll();
    }

    private void WriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!DebuggerRegisterValueCodec.TryParse(
                _register,
                NewValueTextBox.Text,
                out byte[] replacementValue,
                out string error))
        {
            ValidationTextBlock.Text = error;
            NewValueTextBox.Focus();
            return;
        }

        if (_register.Value.Span.SequenceEqual(replacementValue))
        {
            ValidationTextBlock.Text = "Change the value before writing.";
            NewValueTextBox.Focus();
            return;
        }

        ReplacementValue = replacementValue;
        DialogResult = true;
        e.Handled = true;
    }
}
