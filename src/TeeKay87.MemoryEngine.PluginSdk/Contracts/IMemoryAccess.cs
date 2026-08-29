using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IMemoryReader
{
    Task<int> ReadAsync(
        TargetProcess process,
        ulong address,
        Memory<byte> destination,
        CancellationToken cancellationToken);
}

public interface IMemoryWriter
{
    Task WriteAsync(
        TargetProcess process,
        ulong address,
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken);
}

public interface IMemoryMapProvider
{
    Task<IReadOnlyList<MemoryRegion>> GetMemoryRegionsAsync(
        TargetProcess process,
        CancellationToken cancellationToken);
}
