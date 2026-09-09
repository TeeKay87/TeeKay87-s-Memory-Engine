using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDisassemblerProvider
{
    bool SupportsArchitecture(TargetArchitecture architecture);

    Task<IReadOnlyList<DisassembledInstruction>> DisassembleAsync(
        ulong startAddress,
        ReadOnlyMemory<byte> bytes,
        TargetArchitecture architecture,
        CancellationToken cancellationToken);
}
