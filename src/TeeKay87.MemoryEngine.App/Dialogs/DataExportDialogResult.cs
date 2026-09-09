using System.Collections.Generic;
using TeeKay87.MemoryEngine.App.Exporting;
using TeeKay87.MemoryEngine.Core.Exporting;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal sealed record DataExportDialogResult(
    ExportScopeOption Scope,
    TabularExportFormat Format,
    IReadOnlyList<string> ColumnIds);
