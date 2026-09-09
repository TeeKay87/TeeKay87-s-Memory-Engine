using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record DebuggerEvent
{
    public DebuggerEvent(
        DebuggerEventKind kind,
        DebuggerExecutionState executionState,
        DebuggerStopReason stopReason = DebuggerStopReason.None,
        ulong? threadId = null,
        ulong? instructionPointer = null,
        string? message = null,
        DateTimeOffset? timestamp = null)
    {
        Kind = kind;
        ExecutionState = executionState;
        StopReason = stopReason;
        ThreadId = threadId;
        InstructionPointer = instructionPointer;
        Message = message;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }

    public DebuggerEventKind Kind { get; }

    public DebuggerExecutionState ExecutionState { get; }

    public DebuggerStopReason StopReason { get; }

    public ulong? ThreadId { get; }

    public ulong? InstructionPointer { get; }

    public string? Message { get; }

    public DateTimeOffset Timestamp { get; }
}
