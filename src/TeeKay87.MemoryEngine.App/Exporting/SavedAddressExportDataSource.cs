using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.App.ViewModels;
using TeeKay87.MemoryEngine.Core.Exporting;

namespace TeeKay87.MemoryEngine.App.Exporting;

internal static class SavedAddressExportColumnIds
{
    public const string Frozen = "frozen";
    public const string Description = "description";
    public const string Address = "address";
    public const string Type = "type";
    public const string Value = "value";
    public const string Protection = "protection";
}

internal sealed class SavedAddressExportDataSource : IExportDataSource
{
    private static readonly ExportColumn[] AvailableColumns =
    {
        new(SavedAddressExportColumnIds.Frozen, "Frozen"),
        new(SavedAddressExportColumnIds.Description, "Description"),
        new(SavedAddressExportColumnIds.Address, "Address"),
        new(SavedAddressExportColumnIds.Type, "Type"),
        new(SavedAddressExportColumnIds.Value, "Value"),
        new(SavedAddressExportColumnIds.Protection, "Protection")
    };

    private readonly SavedAddressExportRow[] _rows;

    public SavedAddressExportDataSource(
        IEnumerable<SavedAddressViewModel> rows,
        IReadOnlyDictionary<string, ExportCellValue> metadata)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _rows = rows.Select(SavedAddressExportRow.FromViewModel).ToArray();
    }

    public string Type => "saved-addresses";

    public int SchemaVersion => 1;

    public long Count => _rows.LongLength;

    public IReadOnlyList<ExportColumn> Columns => AvailableColumns;

    public IReadOnlyDictionary<string, ExportCellValue> Metadata { get; }

    public async IAsyncEnumerable<ExportRowBatch> ReadBatchesAsync(
        IReadOnlyList<string> columnIds,
        int maximumRowsPerBatch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(columnIds);
        if (maximumRowsPerBatch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRowsPerBatch));
        }

        HashSet<string> available = AvailableColumns
            .Select(column => column.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string columnId in columnIds)
        {
            if (!available.Contains(columnId))
            {
                throw new ArgumentException(
                    $"Saved-address export column '{columnId}' is not available.",
                    nameof(columnIds));
            }
        }

        for (int start = 0; start < _rows.Length; start += maximumRowsPerBatch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int batchCount = Math.Min(maximumRowsPerBatch, _rows.Length - start);
            ExportCellValue[] cells = new ExportCellValue[checked(batchCount * columnIds.Count)];
            for (int rowIndex = 0; rowIndex < batchCount; rowIndex++)
            {
                SavedAddressExportRow row = _rows[start + rowIndex];
                for (int columnIndex = 0; columnIndex < columnIds.Count; columnIndex++)
                {
                    cells[checked(rowIndex * columnIds.Count + columnIndex)] =
                        row.GetCell(columnIds[columnIndex]);
                }
            }

            yield return new ExportRowBatch(batchCount, columnIds.Count, cells);
            await Task.Yield();
        }
    }

    private sealed record SavedAddressExportRow(
        bool Frozen,
        string Description,
        string Address,
        string Type,
        string Value,
        string? Protection)
    {
        public static SavedAddressExportRow FromViewModel(SavedAddressViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            return new SavedAddressExportRow(
                viewModel.IsFrozenRequested,
                viewModel.Description,
                viewModel.AddressText,
                viewModel.SelectedValueType.DisplayName,
                viewModel.ValueText,
                viewModel.ProtectionValue?.ToString());
        }

        public ExportCellValue GetCell(string columnId)
        {
            return columnId switch
            {
                SavedAddressExportColumnIds.Frozen => ExportCellValue.FromBoolean(Frozen),
                SavedAddressExportColumnIds.Description => ExportCellValue.FromString(Description),
                SavedAddressExportColumnIds.Address => ExportCellValue.FromString(Address),
                SavedAddressExportColumnIds.Type => ExportCellValue.FromString(Type),
                SavedAddressExportColumnIds.Value => ExportCellValue.FromString(Value),
                SavedAddressExportColumnIds.Protection => ExportCellValue.FromString(Protection),
                _ => throw new InvalidOperationException($"Unknown saved-address export column '{columnId}'.")
            };
        }
    }
}
