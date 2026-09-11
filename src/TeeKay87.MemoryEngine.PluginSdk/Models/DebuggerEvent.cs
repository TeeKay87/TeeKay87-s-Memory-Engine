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

    public DebuggerEvent(
        DebuggerEventKind kind,
        DebuggerExecutionState executionState,
        DebuggerStopReason stopReason,
        ulong? threadId,
        ulong? instructionPointer,
        DebuggerBreakpoint triggeredBreakpoint,
        string? message = null,
        DateTimeOffset? timestamp = null)
        : this(kind, executionState, stopReason, threadId, instructionPointer, message, timestamp)
    {
        ArgumentNullException.ThrowIfNull(triggeredBreakpoint);
        TriggeredBreakpoint = triggeredBreakpoint;
    }

    public DebuggerEventKind Kind { get; }

    public DebuggerExecutionState ExecutionState { get; }

    public DebuggerStopReason StopReason { get; }

    public ulong? ThreadId { get; }

    public ulong? InstructionPointer { get; }

    public DebuggerBreakpoint? TriggeredBreakpoint { get; }

    public string? Message { get; }

    public DateTimeOffset Timestamp { get; }
}
