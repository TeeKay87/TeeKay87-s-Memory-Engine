using System;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public sealed record ScanResultCommitInfo(long ResultCount)
{
    public void Validate()
    {
        if (ResultCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ResultCount),
                ResultCount,
                "Result count cannot be negative.");
        }
    }
}
