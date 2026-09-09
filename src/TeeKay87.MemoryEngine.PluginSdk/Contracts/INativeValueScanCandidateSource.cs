using System;
using System.Collections.Generic;
using System.Threading;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface INativeValueScanCandidateSource
{
    long Count { get; }

    IAsyncEnumerable<ReadOnlyMemory<ulong>> ReadAddressBatchesAsync(
        int maximumRecordsPerBatch,
        CancellationToken cancellationToken);
}
