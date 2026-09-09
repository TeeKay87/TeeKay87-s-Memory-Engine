using System;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public interface IScanResultStorage : IDisposable
{
    Guid ApplicationSessionId { get; }

    string RootPath { get; }

    string ApplicationSessionPath { get; }

    IScanResultStorageSession CreateScanSession(ScanResultSessionContext context);
}
