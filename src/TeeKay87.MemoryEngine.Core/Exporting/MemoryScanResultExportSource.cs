using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.Core.Scanning;
using TeeKay87.MemoryEngine.Core.Scanning.Storage;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Exporting;

public static class MemoryScanResultExportColumnIds
{
    public const string Address = "address";
    public const string Value = "value";
    public const string Previous = "previous";
    public const string Type = "type";
    public const string Protection = "protection";
    public const string RegionOrModule = "regionOrModule";
}

public sealed class MemoryScanResultExportSource : IExportDataSource
{
    private enum SourceKind
    {
        Materialized,
        DiskBacked,
        NativeResident
    }

    private static readonly ExportColumn[] MaterializedColumns =
    {
        new(MemoryScanResultExportColumnIds.Address, "Address"),
        new(MemoryScanResultExportColumnIds.Value, "Value"),
        new(MemoryScanResultExportColumnIds.Previous, "Previous"),
        new(MemoryScanResultExportColumnIds.Type, "Type"),
        new(MemoryScanResultExportColumnIds.Protection, "Protection"),
        new(MemoryScanResultExportColumnIds.RegionOrModule, "Region / Module")
    };

    private static readonly ExportColumn[] CompleteStoredColumns =
    {
        new(MemoryScanResultExportColumnIds.Address, "Address"),
        new(MemoryScanResultExportColumnIds.Value, "Value"),
        new(MemoryScanResultExportColumnIds.Type, "Type"),
        new(MemoryScanResultExportColumnIds.Protection, "Protection")
    };

    private readonly SourceKind _sourceKind;
    private readonly IReadOnlyList<MemoryScanResult>? _materializedResults;
    private readonly IScanResultSet? _diskBackedResults;
    private readonly INativeValueScanResidentResultSet? _residentResults;
    private readonly IMemoryValueType? _valueType;
    private readonly TargetArchitecture? _architecture;
    private readonly IReadOnlyList<MemoryRegion> _memoryRegions;
    private readonly int _alignment;

    private MemoryScanResultExportSource(
        SourceKind sourceKind,
        long count,
        IReadOnlyDictionary<string, ExportCellValue> metadata,
        IReadOnlyList<MemoryScanResult>? materializedResults = null,
        IScanResultSet? diskBackedResults = null,
        INativeValueScanResidentResultSet? residentResults = null,
        IMemoryValueType? valueType = null,
        TargetArchitecture? architecture = null,
        IReadOnlyList<MemoryRegion>? memoryRegions = null,
        int alignment = 0)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        _sourceKind = sourceKind;
        Count = count;
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _materializedResults = materializedResults;
        _diskBackedResults = diskBackedResults;
        _residentResults = residentResults;
        _valueType = valueType;
        _architecture = architecture;
        _memoryRegions = memoryRegions is null
            ? Array.Empty<MemoryRegion>()
            : memoryRegions
                .OrderBy(region => region.BaseAddress)
                .ThenBy(region => region.Size)
                .ToArray();
        _alignment = alignment;
        Columns = sourceKind == SourceKind.Materialized
            ? MaterializedColumns
            : CompleteStoredColumns;
    }

    public string Type => "scan-results";

    public int SchemaVersion => 1;

    public long Count { get; }

    public IReadOnlyList<ExportColumn> Columns { get; }

    public IReadOnlyDictionary<string, ExportCellValue> Metadata { get; }

    public bool UsesNativeResidentSource => _sourceKind == SourceKind.NativeResident;

    public static MemoryScanResultExportSource FromMaterialized(
        IReadOnlyList<MemoryScanResult> results,
        IReadOnlyDictionary<string, ExportCellValue> metadata,
        IReadOnlyList<MemoryRegion>? memoryRegions = null)
    {
        ArgumentNullException.ThrowIfNull(results);
        return new MemoryScanResultExportSource(
            SourceKind.Materialized,
            results.Count,
            metadata,
            materializedResults: results,
            memoryRegions: memoryRegions);
    }

    public static MemoryScanResultExportSource FromDiskBacked(
        IScanResultSet results,
        IMemoryValueType valueType,
        TargetArchitecture architecture,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyDictionary<string, ExportCellValue> metadata)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(memoryRegions);

        return new MemoryScanResultExportSource(
            SourceKind.DiskBacked,
            results.Count,
            metadata,
            diskBackedResults: results,
            valueType: valueType,
            architecture: architecture,
            memoryRegions: memoryRegions,
            alignment: results.Alignment);
    }

    public static MemoryScanResultExportSource FromNativeResident(
        INativeValueScanResidentResultSet results,
        IMemoryValueType valueType,
        TargetArchitecture architecture,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyDictionary<string, ExportCellValue> metadata)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(memoryRegions);

        return new MemoryScanResultExportSource(
            SourceKind.NativeResident,
            results.Count,
            metadata,
            residentResults: results,
            valueType: valueType,
            architecture: architecture,
            memoryRegions: memoryRegions,
            alignment: results.Alignment);
    }

    public IAsyncEnumerable<ExportRowBatch> ReadBatchesAsync(
        IReadOnlyList<string> columnIds,
        int maximumRowsPerBatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(columnIds);
        if (maximumRowsPerBatch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRowsPerBatch));
        }

        ValidateRequestedColumns(columnIds);
        return _sourceKind switch
        {
            SourceKind.Materialized => ReadMaterializedBatchesAsync(
                columnIds,
                maximumRowsPerBatch,
                cancellationToken),
            SourceKind.DiskBacked => ReadDiskBackedBatchesAsync(
                columnIds,
                maximumRowsPerBatch,
                cancellationToken),
            SourceKind.NativeResident => ReadResidentBatchesAsync(
                columnIds,
                maximumRowsPerBatch,
                cancellationToken),
            _ => throw new InvalidOperationException("Unknown scan-result export source kind.")
        };
    }

    private async IAsyncEnumerable<ExportRowBatch> ReadMaterializedBatchesAsync(
        IReadOnlyList<string> columnIds,
        int maximumRowsPerBatch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<MemoryScanResult> results = _materializedResults
            ?? throw new InvalidOperationException("Materialized scan results are unavailable.");

        for (int start = 0; start < results.Count; start += maximumRowsPerBatch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int batchCount = Math.Min(maximumRowsPerBatch, results.Count - start);
            ExportCellValue[] cells = new ExportCellValue[checked(batchCount * columnIds.Count)];
            for (int rowIndex = 0; rowIndex < batchCount; rowIndex++)
            {
                MemoryScanResult result = results[start + rowIndex];
                for (int columnIndex = 0; columnIndex < columnIds.Count; columnIndex++)
                {
                    cells[checked(rowIndex * columnIds.Count + columnIndex)] =
                        GetMaterializedCell(result, columnIds[columnIndex]);
                }
            }

            yield return new ExportRowBatch(batchCount, columnIds.Count, cells);
            await Task.Yield();
        }
    }

    private async IAsyncEnumerable<ExportRowBatch> ReadDiskBackedBatchesAsync(
        IReadOnlyList<string> columnIds,
        int maximumRowsPerBatch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IScanResultSet results = _diskBackedResults
            ?? throw new InvalidOperationException("Disk-backed scan results are unavailable.");

        await foreach (ScanResultRecordBatch batch in results
                           .ReadBatchesAsync(maximumRowsPerBatch, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return CreateStoredBatch(
                batch.Addresses,
                batch.CurrentValueData,
                batch.Count,
                batch.ValueSize,
                columnIds);
        }
    }

    private async IAsyncEnumerable<ExportRowBatch> ReadResidentBatchesAsync(
        IReadOnlyList<string> columnIds,
        int maximumRowsPerBatch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        INativeValueScanResidentResultSet results = _residentResults
            ?? throw new InvalidOperationException("Native resident scan results are unavailable.");

        await foreach (NativeValueScanResultBatch batch in results
                           .ReadResultBatchesAsync(
                               startIndex: 0,
                               maximumRecords: results.Count,
                               maximumRecordsPerBatch: maximumRowsPerBatch,
                               includePreviousValues: false,
                               cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return CreateStoredBatch(
                batch.Addresses,
                batch.CurrentValueData,
                batch.Count,
                batch.ValueSize,
                columnIds);
        }
    }

    private ExportRowBatch CreateStoredBatch(
        ReadOnlyMemory<ulong> addresses,
        ReadOnlyMemory<byte> currentValueData,
        int rowCount,
        int valueSize,
        IReadOnlyList<string> columnIds)
    {
        IMemoryValueType valueType = _valueType
            ?? throw new InvalidOperationException("The scan Value Type is unavailable for export formatting.");
        TargetArchitecture architecture = _architecture
            ?? throw new InvalidOperationException("The target architecture is unavailable for export formatting.");

        ExportCellValue[] cells = new ExportCellValue[checked(rowCount * columnIds.Count)];
        bool includeProtection = columnIds.Contains(
            MemoryScanResultExportColumnIds.Protection,
            StringComparer.OrdinalIgnoreCase);
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            ulong address = addresses.Span[rowIndex];
            ReadOnlySpan<byte> bytes = currentValueData.Span.Slice(checked(rowIndex * valueSize), valueSize);
            MemoryScanValue value = valueType.CreateValue(bytes, _alignment, architecture);
            MemoryRegion? region = includeProtection
                ? FindContainingRegion(_memoryRegions, address, valueSize)
                : null;

            for (int columnIndex = 0; columnIndex < columnIds.Count; columnIndex++)
            {
                string columnId = columnIds[columnIndex];
                ExportCellValue cell = columnId switch
                {
                    MemoryScanResultExportColumnIds.Address => ExportCellValue.FromString($"0x{address:X}"),
                    MemoryScanResultExportColumnIds.Value => ExportCellValue.FromString(value.DisplayText),
                    MemoryScanResultExportColumnIds.Type => ExportCellValue.FromString(value.ValueTypeDisplayName),
                    MemoryScanResultExportColumnIds.Protection => ExportCellValue.FromString(
                        region?.Protection.ToString()),
                    _ => throw new InvalidOperationException(
                        $"Column '{columnId}' is not available for complete stored scan-result export.")
                };
                cells[checked(rowIndex * columnIds.Count + columnIndex)] = cell;
            }
        }

        return new ExportRowBatch(rowCount, columnIds.Count, cells);
    }

    private ExportCellValue GetMaterializedCell(MemoryScanResult result, string columnId)
    {
        return columnId switch
        {
            MemoryScanResultExportColumnIds.Address => ExportCellValue.FromString($"0x{result.Address:X}"),
            MemoryScanResultExportColumnIds.Value => ExportCellValue.FromString(result.CurrentValue.DisplayText),
            MemoryScanResultExportColumnIds.Previous => ExportCellValue.FromString(result.PreviousValue?.DisplayText),
            MemoryScanResultExportColumnIds.Type => ExportCellValue.FromString(result.CurrentValue.ValueTypeDisplayName),
            MemoryScanResultExportColumnIds.Protection => ExportCellValue.FromString(
                (FindContainingRegion(_memoryRegions, result.Address, result.CurrentValue.Size)?.Protection
                    ?? result.Protection).ToString()),
            MemoryScanResultExportColumnIds.RegionOrModule => ExportCellValue.FromString(
                !string.IsNullOrWhiteSpace(result.ModuleName)
                    ? result.ModuleName
                    : !string.IsNullOrWhiteSpace(result.RegionName)
                        ? result.RegionName
                        : null),
            _ => throw new InvalidOperationException($"Unknown scan-result export column '{columnId}'.")
        };
    }

    private static MemoryRegion? FindContainingRegion(
        IReadOnlyList<MemoryRegion> regions,
        ulong address,
        int valueSize)
    {
        if (regions.Count == 0 || valueSize <= 0)
        {
            return null;
        }

        int low = 0;
        int high = regions.Count - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            MemoryRegion region = regions[middle];
            if (address < region.BaseAddress)
            {
                high = middle - 1;
                continue;
            }

            if (address >= region.EndAddressExclusive)
            {
                low = middle + 1;
                continue;
            }

            ulong offset = address - region.BaseAddress;
            ulong requestedSize = checked((ulong)valueSize);
            return offset <= region.Size && requestedSize <= region.Size - offset
                ? region
                : null;
        }

        return null;
    }

    private void ValidateRequestedColumns(IReadOnlyList<string> columnIds)
    {
        HashSet<string> available = Columns
            .Select(column => column.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string columnId in columnIds)
        {
            if (!available.Contains(columnId))
            {
                throw new ArgumentException(
                    $"Scan-result export column '{columnId}' is not available from this source.",
                    nameof(columnIds));
            }
        }
    }
}
