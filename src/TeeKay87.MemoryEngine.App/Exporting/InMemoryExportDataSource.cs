using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.Core.Exporting;

namespace TeeKay87.MemoryEngine.App.Exporting;

internal sealed class InMemoryExportDataSource : IExportDataSource
{
    private readonly ExportCellValue[][] _rows;

    public InMemoryExportDataSource(
        string type,
        IReadOnlyList<ExportColumn> columns,
        IEnumerable<IReadOnlyList<ExportCellValue>> rows,
        IReadOnlyDictionary<string, ExportCellValue>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        if (columns.Count == 0) throw new ArgumentException("At least one export column is required.", nameof(columns));
        Type = type;
        Columns = columns;
        _rows = rows.Select(row =>
        {
            if (row.Count != columns.Count) throw new ArgumentException("Every row must match the export column count.", nameof(rows));
            return row.ToArray();
        }).ToArray();
        Metadata = metadata ?? new Dictionary<string, ExportCellValue>();
    }

    public string Type { get; }
    public int SchemaVersion => 1;
    public long Count => _rows.LongLength;
    public IReadOnlyList<ExportColumn> Columns { get; }
    public IReadOnlyDictionary<string, ExportCellValue> Metadata { get; }

    public async IAsyncEnumerable<ExportRowBatch> ReadBatchesAsync(
        IReadOnlyList<string> columnIds,
        int maximumRowsPerBatch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (maximumRowsPerBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRowsPerBatch));
        int[] indexes = columnIds.Select(id =>
        {
            int index = -1;
            for (int i = 0; i < Columns.Count; i++) if (string.Equals(Columns[i].Id, id, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
            return index >= 0 ? index : throw new ArgumentException($"Unknown export column '{id}'.", nameof(columnIds));
        }).ToArray();

        for (int start = 0; start < _rows.Length; start += maximumRowsPerBatch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(maximumRowsPerBatch, _rows.Length - start);
            ExportCellValue[] cells = new ExportCellValue[checked(count * indexes.Length)];
            for (int row = 0; row < count; row++)
                for (int col = 0; col < indexes.Length; col++)
                    cells[row * indexes.Length + col] = _rows[start + row][indexes[col]];
            yield return new ExportRowBatch(count, indexes.Length, cells);
            await Task.Yield();
        }
    }
}
