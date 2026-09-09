using System;
using System.Windows;
using TeeKay87.MemoryEngine.App.Dialogs;
using TeeKay87.MemoryEngine.App.ViewModels;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App;

public partial class DebuggerWindow : Window
{
    private readonly Func<bool>? _canOpenDisassembler;
    private readonly Action<ulong>? _openDisassembler;

    public DebuggerWindow(
        DebuggerViewModel viewModel,
        Func<bool>? canOpenDisassembler = null,
        Action<ulong>? openDisassembler = null)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _canOpenDisassembler = canOpenDisassembler;
        _openDisassembler = openDisassembler;
        InitializeComponent();
    }

    private async void EditRegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel ||
            !viewModel.CanEditSelectedRegister ||
            viewModel.SelectedRegister?.Register is not DebuggerRegister { CanWrite: true } register)
        {
            return;
        }

        RegisterEditDialog dialog = new(register)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.ReplacementValue is byte[] replacementValue)
        {
            await viewModel.WriteSelectedRegisterAsync(replacementValue).ConfigureAwait(true);
        }

        e.Handled = true;
    }

    private void OpenCurrentInstructionButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel ||
            viewModel.CurrentInstructionPointer is not ulong address ||
            _canOpenDisassembler?.Invoke() != true ||
            _openDisassembler is null)
        {
            return;
        }

        _openDisassembler(address);
        e.Handled = true;
    }

    private async void DebuggerWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is DebuggerViewModel viewModel)
        {
            await viewModel.DisposeAsync().ConfigureAwait(true);
        }
    }
}
