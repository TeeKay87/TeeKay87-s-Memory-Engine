using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDebuggerRegisterService
{
    Task<IReadOnlyList<DebuggerRegister>> GetRegistersAsync(
        ulong threadId,
        CancellationToken cancellationToken);

    Task WriteRegisterAsync(
        ulong threadId,
        DebuggerRegisterWriteRequest request,
        CancellationToken cancellationToken);
}
