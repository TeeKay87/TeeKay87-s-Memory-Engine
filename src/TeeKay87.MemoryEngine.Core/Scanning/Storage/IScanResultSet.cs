using System;
using System.Collections.Generic;
using System.Threading;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public interface IScanResultSet : IDisposable, INativeValueScanCandidateSource
{
    int ValueSize { get; }

    int Alignment { get; }

    IAsyncEnumerable<ScanResultRecordBatch> ReadBatchesAsync(
        int maximumRecordsPerBatch,
        CancellationToken cancellationToken);
}
