using TeeKay87.MemoryEngine.Core.Scanning;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed record ScanValueTypeViewModel(MemoryValueType ValueType, string DisplayName)
{
    public static ScanValueTypeViewModel Create(MemoryValueType valueType)
    {
        return new ScanValueTypeViewModel(valueType, MemoryScanValueCodec.GetDisplayName(valueType));
    }

    public override string ToString() => DisplayName;
}
