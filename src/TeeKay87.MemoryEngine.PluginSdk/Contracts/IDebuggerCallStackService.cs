using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDebuggerCallStackService
{
    Task<IReadOnlyList<DebuggerStackFrame>> GetCallStackAsync(
        ulong threadId,
        CancellationToken cancellationToken);
}
