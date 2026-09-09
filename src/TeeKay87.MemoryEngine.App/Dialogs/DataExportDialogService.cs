using System;
using System.Collections.Generic;
using System.Windows;
using TeeKay87.MemoryEngine.App.Exporting;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal sealed class DataExportDialogService
{
    public DataExportDialogResult? Show(
        Window owner,
        string title,
        IReadOnlyList<ExportScopeOption> scopes)
    {
        ArgumentNullException.ThrowIfNull(owner);
        DataExportDialog dialog = new(title, scopes)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
}
