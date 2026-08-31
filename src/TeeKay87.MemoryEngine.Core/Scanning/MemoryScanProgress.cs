namespace TeeKay87.MemoryEngine.Core.Scanning;

public sealed record MemoryScanProgress(
    ulong UnitsProcessed,
    ulong UnitsTotal,
    int ResultsFound);
