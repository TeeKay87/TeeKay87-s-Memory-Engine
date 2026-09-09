using System;
using System.Collections.Generic;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public sealed class MemoryScanExecutionResult
{
    public MemoryScanExecutionResult(
        IReadOnlyList<MemoryScanResult> results,
        int readFailureCount)
        : this(results, results?.Count ?? throw new ArgumentNullException(nameof(results)), readFailureCount)
    {
    }

    public MemoryScanExecutionResult(
        IReadOnlyList<MemoryScanResult> results,
        long totalResultCount,
        int readFailureCount)
    {
        Results = results ?? throw new ArgumentNullException(nameof(results));
        if (totalResultCount < Results.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalResultCount),
                totalResultCount,
                "The total result count cannot be smaller than the materialized result preview.");
        }

        if (readFailureCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(readFailureCount));
        }

        TotalResultCount = totalResultCount;
        ReadFailureCount = readFailureCount;
    }

    public IReadOnlyList<MemoryScanResult> Results { get; }

    public long TotalResultCount { get; }

    public int ReadFailureCount { get; }
}
