using System;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed record ScanValueTypeViewModel(IMemoryValueType ValueType)
{
    public string Id => ValueType.Id;

    public string DisplayName => ValueType.DisplayName;

    public string Description => ValueType.Description;

    public string InputDescription => ValueType.InputDescription;

    public override string ToString() => DisplayName;
}
