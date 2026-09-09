using System;

namespace TeeKay87.MemoryEngine.Core.Exporting;

public sealed class ExportRowBatch
{
    private readonly ExportCellValue[] _cells;

    public ExportRowBatch(int rowCount, int columnCount, ExportCellValue[] cells)
    {
        if (rowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowCount));
        }

        if (columnCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Length != checked(rowCount * columnCount))
        {
            throw new ArgumentException(
                "Cell count must equal row count multiplied by column count.",
                nameof(cells));
        }

        RowCount = rowCount;
        ColumnCount = columnCount;
        _cells = cells;
    }

    public int RowCount { get; }

    public int ColumnCount { get; }

    public ExportCellValue GetCell(int rowIndex, int columnIndex)
    {
        if ((uint)rowIndex >= (uint)RowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        if ((uint)columnIndex >= (uint)ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        }

        return _cells[checked(rowIndex * ColumnCount + columnIndex)];
    }
}
