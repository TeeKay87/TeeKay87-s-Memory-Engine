using System;
using System.Globalization;
using TeeKay87.MemoryEngine.Core.Debugging;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class DebuggerEventViewModel
{
    public DebuggerEventViewModel(DebuggerEventContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public DebuggerEventContext Context { get; }

    public long Sequence => Context.Sequence;

    public string Time => Context.Event.Timestamp
        .ToLocalTime()
        .ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    public string Kind => Context.Event.Kind.ToString();

    public string State => Context.Event.ExecutionState.ToString();

    public string StopReason => Context.Event.StopReason == DebuggerStopReason.None
        ? string.Empty
        : Context.Event.StopReason.ToString();

    public string Thread => Context.Event.ThreadId is ulong threadId
        ? $"0x{threadId:X}"
        : string.Empty;

    public string InstructionPointer => Context.Event.InstructionPointer is ulong address
        ? $"0x{address:X}"
        : string.Empty;

    public string Message => Context.Event.Message ?? string.Empty;
}
