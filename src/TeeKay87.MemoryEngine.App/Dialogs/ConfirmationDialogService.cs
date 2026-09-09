using System;
using System.Windows;

namespace TeeKay87.MemoryEngine.App.Dialogs;

public sealed class ConfirmationDialogService
{
    public bool Show(Window owner, ConfirmationDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(options);

        if (!owner.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException("Confirmation dialogs must be opened from the WPF UI thread.");
        }

        ConfirmationDialog dialog = new(options)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true;
    }
}
