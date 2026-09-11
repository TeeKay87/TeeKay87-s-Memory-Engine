using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.Core.Disassembly;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Exporting;

public static class DisassemblyExportColumnIds
{
    public const string Address = "address";
    public const string Bytes = "bytes";
    public const string Markers = "markers";
    public const string Instruction = "instruction";
    public const string Mnemonic = "mnemonic";
    public const string Operands = "operands";
    public const string Length = "length";
    public const string FlowControl = "flowControl";
    public const string BranchTarget = "branchTarget";
    public const string Valid = "valid";
    public const string RegionOrModule = "regionOrModule";
    public const string Protection = "protection";
    public const string ModuleRelativeAddress = "moduleRelativeAddress";
}

public sealed class DisassemblyExportSource : IExportDataSource
{
    private static readonly ExportColumn[] AvailableColumns =
    {
        new(DisassemblyExportColumnIds.Address, "Address"),
        new(DisassemblyExportColumnIds.Bytes, "Bytes"),
        new(DisassemblyExportColumnIds.Markers, "Markers"),
        new(DisassemblyExportColumnIds.Instruction, "Instruction"),
        new(DisassemblyExportColumnIds.Mnemonic, "Mnemonic"),
        new(DisassemblyExportColumnIds.Operands, "Operands"),
        new(DisassemblyExportColumnIds.Length, "Length"),
        new(DisassemblyExportColumnIds.FlowControl, "Flow Control"),
        new(DisassemblyExportColumnIds.BranchTarget, "Branch Target"),
        new(DisassemblyExportColumnIds.Valid, "Valid"),
        new(DisassemblyExportColumnIds.RegionOrModule, "Region / Module"),
        new(DisassemblyExportColumnIds.Protection, "Protection"),
        new(DisassemblyExportColumnIds.ModuleRelativeAddress, "Module Relative")
    };

    private readonly DisassemblyExportRow[] _rows;

    public DisassemblyExportSource(
        IEnumerable<DisassembledInstruction> instructions,
        MemoryRegion region,
        ulong? moduleBaseAddress,
        IReadOnlyDictionary<string, ExportCellValue> metadata,
        IEnumerable<DisassemblyMarker>? markers = null)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(region);
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        IReadOnlyDictionary<ulong, string> markersByAddress = (markers ?? Enumerable.Empty<DisassemblyMarker>())
            .GroupBy(marker => marker.Address)
            .ToDictionary(
                group => group.Key,
                group => string.Join(
                    " · ",
                    group.Select(marker => marker.Text)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Distinct(StringComparer.Ordinal)));

        _rows = instructions
            .Select(instruction => DisassemblyExportRow.FromInstruction(
                instruction,
                region,
                moduleBaseAddress,
                markersByAddress.TryGetValue(instruction.Address, out string? markerText)
                    ? markerText
                    : null))
            .ToArray();
    }

    public string Type => "disassembly";

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
                    $"Disassembly export column '{columnId}' is not available.",
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
                DisassemblyExportRow row = _rows[start + rowIndex];
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

    private sealed record DisassemblyExportRow(
        string Address,
        string Bytes,
        string? Markers,
        string Instruction,
        string Mnemonic,
        string Operands,
        int Length,
        string FlowControl,
        string? BranchTarget,
        bool Valid,
        string? RegionOrModule,
        string Protection,
        string? ModuleRelativeAddress)
    {
        public static DisassemblyExportRow FromInstruction(
            DisassembledInstruction instruction,
            MemoryRegion region,
            ulong? moduleBaseAddress,
            string? markers)
        {
            ArgumentNullException.ThrowIfNull(instruction);
            ArgumentNullException.ThrowIfNull(region);

            string instructionText = string.IsNullOrWhiteSpace(instruction.Operands)
                ? instruction.Mnemonic
                : $"{instruction.Mnemonic} {instruction.Operands}";
            string? regionOrModule = !string.IsNullOrWhiteSpace(region.ModuleName)
                ? region.ModuleName
                : !string.IsNullOrWhiteSpace(region.Name)
                    ? region.Name
                    : null;
            string? moduleRelativeAddress = moduleBaseAddress.HasValue &&
                                            !string.IsNullOrWhiteSpace(region.ModuleName) &&
                                            instruction.Address >= moduleBaseAddress.Value
                ? $"{region.ModuleName} + 0x{instruction.Address - moduleBaseAddress.Value:X}"
                : null;

            return new DisassemblyExportRow(
                $"0x{instruction.Address:X}",
                FormatHex(instruction.RawBytes.Span),
                markers,
                instructionText,
                instruction.Mnemonic,
                instruction.Operands,
                instruction.Length,
                instruction.FlowControl.ToString(),
                instruction.BranchTarget.HasValue ? $"0x{instruction.BranchTarget.Value:X}" : null,
                instruction.IsValid,
                regionOrModule,
                region.Protection.ToString(),
                moduleRelativeAddress);
        }

        public ExportCellValue GetCell(string columnId)
        {
            return columnId switch
            {
                DisassemblyExportColumnIds.Address => ExportCellValue.FromString(Address),
                DisassemblyExportColumnIds.Bytes => ExportCellValue.FromString(Bytes),
                DisassemblyExportColumnIds.Markers => ExportCellValue.FromString(Markers),
                DisassemblyExportColumnIds.Instruction => ExportCellValue.FromString(Instruction),
                DisassemblyExportColumnIds.Mnemonic => ExportCellValue.FromString(Mnemonic),
                DisassemblyExportColumnIds.Operands => ExportCellValue.FromString(Operands),
                DisassemblyExportColumnIds.Length => ExportCellValue.FromInt64(Length),
                DisassemblyExportColumnIds.FlowControl => ExportCellValue.FromString(FlowControl),
                DisassemblyExportColumnIds.BranchTarget => ExportCellValue.FromString(BranchTarget),
                DisassemblyExportColumnIds.Valid => ExportCellValue.FromBoolean(Valid),
                DisassemblyExportColumnIds.RegionOrModule => ExportCellValue.FromString(RegionOrModule),
                DisassemblyExportColumnIds.Protection => ExportCellValue.FromString(Protection),
                DisassemblyExportColumnIds.ModuleRelativeAddress => ExportCellValue.FromString(ModuleRelativeAddress),
                _ => throw new InvalidOperationException($"Unknown disassembly export column '{columnId}'.")
            };
        }

        private static string FormatHex(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                return string.Empty;
            }

            StringBuilder builder = new(checked(bytes.Length * 3 - 1));
            for (int index = 0; index < bytes.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
