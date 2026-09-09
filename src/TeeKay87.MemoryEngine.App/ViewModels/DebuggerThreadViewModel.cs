using System;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class DebuggerThreadViewModel
{
    public DebuggerThreadViewModel(DebuggerThreadInfo thread)
    {
        Thread = thread ?? throw new ArgumentNullException(nameof(thread));
    }

    public DebuggerThreadInfo Thread { get; }

    public ulong Id => Thread.Id;

    public string IdText => $"0x{Id:X}";

    public string Name => Thread.Name ?? string.Empty;

    public string State => Thread.State.ToString();

    public DebuggerThreadState ThreadState => Thread.State;
}
