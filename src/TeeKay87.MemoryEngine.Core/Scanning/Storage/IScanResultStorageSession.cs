using System;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public interface IScanResultStorageSession : IDisposable
{
    Guid ApplicationSessionId { get; }

    Guid ScanSessionId { get; }

    string SessionPath { get; }

    string MetadataPath { get; }

    ScanResultSessionState State { get; }

    bool IsCommitted { get; }

    long? ResultCount { get; }

    IScanResultWriter CreateResultWriter(int valueSize, int alignment);

    IScanResultSet OpenResultSet();

    void Cancel();

    void Fail(string? reason = null);

    void Invalidate();
}
