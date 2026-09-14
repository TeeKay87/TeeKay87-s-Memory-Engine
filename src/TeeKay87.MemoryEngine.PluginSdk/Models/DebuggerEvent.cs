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
        : this(
            kind,
            executionState,
            stopReason,
            threadId,
            instructionPointer,
            triggeredBreakpoint: null,
            triggerInstructionAddress: null,
            triggerResolution: DebuggerTriggerResolution.Unresolved,
            message: message,
            timestamp: timestamp)
    {
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
        : this(
            kind,
            executionState,
            stopReason,
            threadId,
            instructionPointer,
            triggeredBreakpoint,
            triggerInstructionAddress: null,
            triggerResolution: DebuggerTriggerResolution.Unresolved,
            message: message,
            timestamp: timestamp)
    {
        ArgumentNullException.ThrowIfNull(triggeredBreakpoint);
    }

    public DebuggerEvent(
        DebuggerEventKind kind,
        DebuggerExecutionState executionState,
        DebuggerStopReason stopReason,
        ulong? threadId,
        ulong? instructionPointer,
        DebuggerBreakpoint? triggeredBreakpoint,
        ulong? triggerInstructionAddress,
        DebuggerTriggerResolution triggerResolution,
        string? message = null,
        DateTimeOffset? timestamp = null)
    {
        if (triggerInstructionAddress.HasValue && triggerResolution == DebuggerTriggerResolution.Unresolved)
        {
            throw new ArgumentException(
                "A trigger instruction address requires an explicit trigger-resolution status.",
                nameof(triggerResolution));
        }

        if (!triggerInstructionAddress.HasValue && triggerResolution != DebuggerTriggerResolution.Unresolved)
        {
            throw new ArgumentException(
                "A resolved trigger status requires a trigger instruction address.",
                nameof(triggerResolution));
        }

        Kind = kind;
        ExecutionState = executionState;
        StopReason = stopReason;
        ThreadId = threadId;
        InstructionPointer = instructionPointer;
        TriggeredBreakpoint = triggeredBreakpoint;
        TriggerInstructionAddress = triggerInstructionAddress;
        TriggerResolution = triggerResolution;
        Message = message;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }

    public DebuggerEventKind Kind { get; }

    public DebuggerExecutionState ExecutionState { get; }

    public DebuggerStopReason StopReason { get; }

    public ulong? ThreadId { get; }

    public ulong? InstructionPointer { get; }

    public DebuggerBreakpoint? TriggeredBreakpoint { get; }

    public ulong? TriggerInstructionAddress { get; }

    public DebuggerTriggerResolution TriggerResolution { get; }

    public ulong? WatchedAddress => Kind == DebuggerEventKind.Watchpoint
        ? TriggeredBreakpoint?.Request.Address
        : null;

    public DebuggerBreakpointAccess? WatchpointAccess => Kind == DebuggerEventKind.Watchpoint
        ? TriggeredBreakpoint?.Request.Access
        : null;

    public int? WatchpointSize => Kind == DebuggerEventKind.Watchpoint
        ? TriggeredBreakpoint?.Request.Size
        : null;

    public string? Message { get; }

    public DateTimeOffset Timestamp { get; }

    public DebuggerEvent WithTriggerInstruction(
        ulong triggerInstructionAddress,
        DebuggerTriggerResolution triggerResolution,
        string? message = null)
    {
        if (triggerResolution == DebuggerTriggerResolution.Unresolved)
        {
            throw new ArgumentOutOfRangeException(
                nameof(triggerResolution),
                "Resolved trigger instructions require BackendExact or DisassemblyDerived status.");
        }

        return new DebuggerEvent(
            Kind,
            ExecutionState,
            StopReason,
            ThreadId,
            InstructionPointer,
            TriggeredBreakpoint,
            triggerInstructionAddress,
            triggerResolution,
            message ?? Message,
            Timestamp);
    }
}
