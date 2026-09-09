using System;
using System.Collections.Generic;
using System.Threading;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface INativeValueScanResultStream : IAsyncDisposable
{
    ulong SourceResultCount { get; }

    IAsyncEnumerable<NativeValueScanResultBatch> ReadBatchesAsync(
        CancellationToken cancellationToken);
}
