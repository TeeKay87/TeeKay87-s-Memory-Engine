using System;
using System.Collections.Generic;
using System.Threading;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

/// <summary>
/// Optional native-scan contract for a complete result set that remains authoritative
/// in the plugin/backend after a scan operation completes.
/// </summary>
/// <remarks>
/// The host may use bounded result windows for presentation without materializing the
/// complete set locally. Implementations must keep <see cref="Count"/>, <see cref="ValueSize"/>,
/// and <see cref="Alignment"/> stable for the lifetime of the handle. A native refinement
/// may return a replacement resident handle for the refined set.
/// </remarks>
public interface INativeValueScanResidentResultSet : INativeValueScanCandidateSource, IAsyncDisposable
{
    int ValueSize { get; }

    int Alignment { get; }

    /// <summary>
    /// Gets whether the backend result membership is already semantically equivalent to
    /// the Core scan request. When false, the host must consume the normal native result
    /// stream so any required host-side filtering is applied before the set is accepted.
    /// </summary>
    bool IsAuthoritative { get; }

    /// <summary>
    /// Returns whether the backend can refine this resident set natively for the
    /// supplied request without first materializing the complete set in the host.
    /// This check must not mutate or release the resident backend session.
    /// </summary>
    bool CanRefine(NativeValueScanRequest request);

    /// <summary>
    /// Reads a bounded result window without transferring unrelated records.
    /// </summary>
    IAsyncEnumerable<NativeValueScanResultBatch> ReadResultBatchesAsync(
        long startIndex,
        long maximumRecords,
        int maximumRecordsPerBatch,
        bool includePreviousValues,
        CancellationToken cancellationToken);
}
