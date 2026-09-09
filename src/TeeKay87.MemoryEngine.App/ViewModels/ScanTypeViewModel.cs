using TeeKay87.MemoryEngine.PluginSdk.Contracts;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed record ScanTypeViewModel(IMemoryScanType ScanType)
{
    public string Id => ScanType.Id;

    public string DisplayName => ScanType.DisplayName;

    public string Description => ScanType.Description;

    public override string ToString() => DisplayName;
}
