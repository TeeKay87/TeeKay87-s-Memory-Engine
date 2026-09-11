using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDebuggerBreakpointValidationService
{
    DebuggerBreakpointValidationResult ValidateBreakpointRequest(DebuggerBreakpointRequest request);
}
