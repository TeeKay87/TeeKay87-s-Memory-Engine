using System.Collections.Generic;
using System.Threading;

namespace TeeKay87.MemoryEngine.Core.Exporting;

public interface IExportDataSource
{
    string Type { get; }

    int SchemaVersion { get; }

    long Count { get; }

    IReadOnlyList<ExportColumn> Columns { get; }

    IReadOnlyDictionary<string, ExportCellValue> Metadata { get; }

    IAsyncEnumerable<ExportRowBatch> ReadBatchesAsync(
        IReadOnlyList<string> columnIds,
        int maximumRowsPerBatch,
        CancellationToken cancellationToken);
}
