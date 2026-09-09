using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IMemoryScanOptionApplicability
{
    bool SupportsScanType(IMemoryScanType scanType, MemoryScanStage stage);
}
