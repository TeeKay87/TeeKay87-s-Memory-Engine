using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDebuggerBreakpointService
{
    Task<IReadOnlyList<DebuggerBreakpoint>> GetBreakpointsAsync(CancellationToken cancellationToken);

    Task<DebuggerBreakpoint> AddBreakpointAsync(
        DebuggerBreakpointRequest request,
        CancellationToken cancellationToken);

    Task RemoveBreakpointAsync(string breakpointId, CancellationToken cancellationToken);
}
