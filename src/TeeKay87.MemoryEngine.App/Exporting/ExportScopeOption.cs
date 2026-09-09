using System;
using TeeKay87.MemoryEngine.Core.Exporting;

namespace TeeKay87.MemoryEngine.App.Exporting;

internal sealed record ExportScopeOption
{
    public ExportScopeOption(string id, string displayName, string description, IExportDataSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(source);

        Id = id;
        DisplayName = displayName;
        Description = description ?? string.Empty;
        Source = source;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public IExportDataSource Source { get; }
}
