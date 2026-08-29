using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IProcessProvider
{
    Task<IReadOnlyList<TargetProcess>> GetProcessesAsync(CancellationToken cancellationToken);
}

public interface IForegroundProcessProvider
{
    Task<TargetProcess?> GetForegroundProcessAsync(CancellationToken cancellationToken);
}
