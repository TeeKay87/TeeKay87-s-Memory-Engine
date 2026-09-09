using System;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public sealed record ScanResultSessionContext(
    string PluginId,
    ulong TargetProcessId,
    string ValueTypeId,
    string ScanTypeId,
    int RecordFormatVersion = 1)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ValueTypeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ScanTypeId);

        if (RecordFormatVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RecordFormatVersion),
                RecordFormatVersion,
                "Record format version must be 1 or greater.");
        }
    }
}
