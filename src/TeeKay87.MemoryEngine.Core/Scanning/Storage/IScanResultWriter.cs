using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public interface IScanResultWriter : IDisposable
{
    long Count { get; }

    int ValueSize { get; }

    int Alignment { get; }

    void Write(ulong address, ReadOnlySpan<byte> currentValueData);

    void WriteBatch(ReadOnlySpan<ulong> addresses, ReadOnlySpan<byte> currentValueData);

    Task CommitAsync(CancellationToken cancellationToken);
}
