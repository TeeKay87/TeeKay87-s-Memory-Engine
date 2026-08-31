using System.Collections.Generic;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public sealed record MemoryScanExecutionResult(
    IReadOnlyList<MemoryScanResult> Results,
    int ReadFailureCount);
