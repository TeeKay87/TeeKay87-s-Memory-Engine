using System.Threading;
using System.Threading.Tasks;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

/// <summary>
/// Optional extension for debugger backends that can change an existing breakpoint's enabled state.
/// </summary>
public interface IDebuggerBreakpointStateService
{
    Task SetBreakpointEnabledAsync(
        string breakpointId,
        bool isEnabled,
        CancellationToken cancellationToken);
}
