using System;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public sealed class ScanResultLimitExceededException : InvalidOperationException
{
    public ScanResultLimitExceededException(int maximumResults)
        : base($"The scan produced more than {maximumResults:N0} matches. Use a more specific value before continuing.")
    {
        MaximumResults = maximumResults;
    }

    public int MaximumResults { get; }
}
