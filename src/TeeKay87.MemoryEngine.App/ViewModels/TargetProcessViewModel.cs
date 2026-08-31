using System;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class TargetProcessViewModel
{
    public TargetProcessViewModel(TargetProcess process)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public TargetProcess Process { get; }

    public ulong Id => Process.Id;

    public string Name => Process.Name;

    public string DisplayName => !string.IsNullOrWhiteSpace(Process.DisplayName)
        ? Process.DisplayName
        : !string.IsNullOrWhiteSpace(Process.Name)
            ? Process.Name
            : "<unnamed process>";

    public string ProcessIdDisplay => $"0x{Id:X8}";

    public string SelectionDisplay => $"{DisplayName} ({ProcessIdDisplay})";

    public override string ToString() => SelectionDisplay;
}
