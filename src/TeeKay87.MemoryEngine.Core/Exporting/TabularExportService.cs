using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.Core.Operations;

namespace TeeKay87.MemoryEngine.Core.Exporting;

public sealed class TabularExportService
{
    private const int DefaultRowsPerBatch = 4096;
    private const int StreamBufferSize = 1024 * 1024;

    public async Task<TabularExportResult> ExportAsync(
        string destinationPath,
        TabularExportFormat format,
        IExportDataSource source,
        IReadOnlyList<string> columnIds,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(columnIds);

        if (source.SchemaVersion <= 0)
        {
            throw new InvalidOperationException("Export data sources must use a positive schema version.");
        }

        if (source.Count < 0)
        {
            throw new InvalidOperationException("Export data source row count cannot be negative.");
        }

        ExportColumn[] columns = ResolveColumns(source.Columns, columnIds);
        string fullDestinationPath = Path.GetFullPath(destinationPath);
        string? directory = Path.GetDirectoryName(fullDestinationPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"The export destination directory does not exist: '{directory ?? fullDestinationPath}'.");
        }

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");
        bool published = false;

        try
        {
            progress?.Report(new OperationProgress(
                "Exporting data...",
                source.Count == 0 ? 1d : 0d,
                $"0 / {source.Count:N0} rows"));

            long rowsWritten = format == TabularExportFormat.Json
                ? await WriteJsonAsync(
                        temporaryPath,
                        source,
                        columns,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await WriteTextTableAsync(
                        temporaryPath,
                        format,
                        source,
                        columns,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (rowsWritten != source.Count)
            {
                throw new InvalidDataException(
                    $"The export source advertised {source.Count:N0} row(s), but emitted {rowsWritten:N0} row(s).");
            }

            long bytesWritten = new FileInfo(temporaryPath).Length;
            PublishCompletedFile(temporaryPath, fullDestinationPath);
            published = true;

            progress?.Report(new OperationProgress(
                "Export complete.",
                1d,
                $"{rowsWritten:N0} row(s) • {bytesWritten:N0} bytes"));

            return new TabularExportResult(rowsWritten, bytesWritten);
        }
        finally
        {
            if (!published)
            {
                TryDeleteFile(temporaryPath);
            }
        }
    }

    private static ExportColumn[] ResolveColumns(
        IReadOnlyList<ExportColumn> availableColumns,
        IReadOnlyList<string> columnIds)
    {
        if (columnIds.Count == 0)
        {
            throw new ArgumentException("At least one export column must be selected.", nameof(columnIds));
        }

        Dictionary<string, ExportColumn> byId = new(StringComparer.OrdinalIgnoreCase);
        foreach (ExportColumn column in availableColumns)
        {
            if (!byId.TryAdd(column.Id, column))
            {
                throw new InvalidOperationException(
                    $"The export data source declares duplicate column id '{column.Id}'.");
            }
        }

        HashSet<string> selectedIds = new(StringComparer.OrdinalIgnoreCase);
        ExportColumn[] resolved = new ExportColumn[columnIds.Count];
        for (int index = 0; index < columnIds.Count; index++)
        {
            string columnId = columnIds[index];
            if (!selectedIds.Add(columnId))
            {
                throw new ArgumentException(
                    $"Export column '{columnId}' was selected more than once.",
                    nameof(columnIds));
            }

            if (!byId.TryGetValue(columnId, out ExportColumn? column) || column is null)
            {
                throw new ArgumentException(
                    $"Export column '{columnId}' is not available from this data source.",
                    nameof(columnIds));
            }

            resolved[index] = column;
        }

        return resolved;
    }

    private static async Task<long> WriteJsonAsync(
        string path,
        IExportDataSource source,
        IReadOnlyList<ExportColumn> columns,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions
        {
            Indented = true,
            SkipValidation = false
        });

        writer.WriteStartObject();
        writer.WriteString("type", source.Type);
        writer.WriteNumber("schemaVersion", source.SchemaVersion);
        writer.WriteString("exportedUtc", DateTimeOffset.UtcNow);
        writer.WriteNumber("rowCount", source.Count);

        writer.WriteStartObject("metadata");
        foreach ((string key, ExportCellValue value) in source.Metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            value.WriteJson(writer);
        }
        writer.WriteEndObject();

        writer.WriteStartArray("columns");
        foreach (ExportColumn column in columns)
        {
            writer.WriteStartObject();
            writer.WriteString("id", column.Id);
            writer.WriteString("header", column.Header);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteStartArray("rows");
        long rowsWritten = 0;
        string[] columnIds = columns.Select(column => column.Id).ToArray();
        await foreach (ExportRowBatch batch in source
                           .ReadBatchesAsync(columnIds, DefaultRowsPerBatch, cancellationToken)
                           .ConfigureAwait(false))
        {
            ValidateBatch(batch, columns.Count, rowsWritten, source.Count);
            for (int rowIndex = 0; rowIndex < batch.RowCount; rowIndex++)
            {
                writer.WriteStartObject();
                for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                {
                    writer.WritePropertyName(columns[columnIndex].Id);
                    batch.GetCell(rowIndex, columnIndex).WriteJson(writer);
                }
                writer.WriteEndObject();
            }

            rowsWritten = checked(rowsWritten + batch.RowCount);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            ReportProgress(progress, rowsWritten, source.Count);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return rowsWritten;
    }

    private static async Task<long> WriteTextTableAsync(
        string path,
        TabularExportFormat format,
        IExportDataSource source,
        IReadOnlyList<ExportColumn> columns,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        char delimiter = format switch
        {
            TabularExportFormat.Csv => ',',
            TabularExportFormat.Tsv => '\t',
            TabularExportFormat.MarkdownTable => '|',
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported text export format.")
        };

        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StreamBufferSize,
            leaveOpen: false);

        if (format == TabularExportFormat.MarkdownTable)
        {
            await writer.WriteLineAsync(
                    $"| {string.Join(" | ", columns.Select(column => EscapeMarkdown(column.Header)))} |")
                .ConfigureAwait(false);
            await writer.WriteLineAsync(
                    $"| {string.Join(" | ", columns.Select(_ => "---"))} |")
                .ConfigureAwait(false);
        }
        else
        {
            string header = string.Join(delimiter, columns.Select(column => EscapeDelimited(column.Header, delimiter)));
            await writer.WriteLineAsync(header).ConfigureAwait(false);
        }

        long rowsWritten = 0;
        string[] columnIds = columns.Select(column => column.Id).ToArray();
        await foreach (ExportRowBatch batch in source
                           .ReadBatchesAsync(columnIds, DefaultRowsPerBatch, cancellationToken)
                           .ConfigureAwait(false))
        {
            ValidateBatch(batch, columns.Count, rowsWritten, source.Count);
            StringBuilder builder = new(capacity: Math.Max(1024, batch.RowCount * columns.Count * 12));
            for (int rowIndex = 0; rowIndex < batch.RowCount; rowIndex++)
            {
                if (format == TabularExportFormat.MarkdownTable)
                {
                    builder.Append("| ");
                    for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        if (columnIndex > 0)
                        {
                            builder.Append(" | ");
                        }

                        builder.Append(EscapeMarkdown(batch.GetCell(rowIndex, columnIndex).ToInvariantString()));
                    }
                    builder.AppendLine(" |");
                }
                else
                {
                    for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        if (columnIndex > 0)
                        {
                            builder.Append(delimiter);
                        }

                        builder.Append(EscapeDelimited(
                            batch.GetCell(rowIndex, columnIndex).ToInvariantString(),
                            delimiter));
                    }
                    builder.AppendLine();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(builder.ToString().AsMemory(), cancellationToken).ConfigureAwait(false);
            rowsWritten = checked(rowsWritten + batch.RowCount);
            ReportProgress(progress, rowsWritten, source.Count);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return rowsWritten;
    }

    private static void ValidateBatch(
        ExportRowBatch batch,
        int expectedColumnCount,
        long rowsWritten,
        long sourceCount)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.ColumnCount != expectedColumnCount)
        {
            throw new InvalidDataException(
                $"The export source returned {batch.ColumnCount} column(s), but {expectedColumnCount} were requested.");
        }

        if (batch.RowCount == 0)
        {
            throw new InvalidDataException("The export source returned an empty batch before reaching its advertised row count.");
        }

        if (checked(rowsWritten + batch.RowCount) > sourceCount)
        {
            throw new InvalidDataException("The export source emitted more rows than its advertised row count.");
        }
    }

    private static void ReportProgress(
        IProgress<OperationProgress>? progress,
        long rowsWritten,
        long totalRows)
    {
        if (progress is null)
        {
            return;
        }

        double fraction = totalRows == 0
            ? 1d
            : Math.Clamp(rowsWritten / (double)totalRows, 0d, 1d);
        progress.Report(new OperationProgress(
            "Exporting data...",
            fraction,
            $"{rowsWritten:N0} / {totalRows:N0} rows"));
    }

    private static string EscapeDelimited(string value, char delimiter)
    {
        if (value.IndexOf(delimiter) < 0 &&
            value.IndexOf('"') < 0 &&
            value.IndexOf('\r') < 0 &&
            value.IndexOf('\n') < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string EscapeMarkdown(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\r", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static void PublishCompletedFile(string temporaryPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, destinationPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
