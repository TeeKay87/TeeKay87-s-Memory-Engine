using System;

namespace TeeKay87.MemoryEngine.Core.Exporting;

public sealed record ExportColumn
{
    public ExportColumn(string id, string header)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(header);

        Id = id;
        Header = header;
    }

    public string Id { get; }

    public string Header { get; }
}
