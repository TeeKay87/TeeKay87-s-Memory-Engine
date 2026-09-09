using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Microsoft.Win32;
using TeeKay87.MemoryEngine.Core.Exporting;

namespace TeeKay87.MemoryEngine.App.Exporting;

internal static class ExportDestinationPicker
{
    public static bool TryChoose(
        Window owner,
        string fileStem,
        TabularExportFormat format,
        [NotNullWhen(true)] out string? path)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileStem);

        string extension;
        string filter;
        switch (format)
        {
            case TabularExportFormat.Json:
                extension = ".json";
                filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                break;
            case TabularExportFormat.Csv:
                extension = ".csv";
                filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                break;
            case TabularExportFormat.Tsv:
                extension = ".tsv";
                filter = "TSV files (*.tsv)|*.tsv|All files (*.*)|*.*";
                break;
            case TabularExportFormat.MarkdownTable:
                extension = ".md";
                filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.");
        }

        SaveFileDialog dialog = new()
        {
            Title = "Save Export",
            FileName = $"{fileStem}{extension}",
            DefaultExt = extension,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(owner) != true)
        {
            path = null;
            return false;
        }

        path = dialog.FileName;
        return true;
    }
}
