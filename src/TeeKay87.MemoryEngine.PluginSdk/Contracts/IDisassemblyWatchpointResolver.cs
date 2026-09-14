using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDisassemblyWatchpointResolver
{
    DisassemblyWatchpointTarget? ResolveWatchpointTarget(
        DisassembledInstruction instruction,
        IReadOnlyList<DebuggerRegister> registers);
}
