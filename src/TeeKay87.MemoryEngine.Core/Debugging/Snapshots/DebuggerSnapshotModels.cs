using System;
using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Debugging.Snapshots;

public enum DebuggerSnapshotCaptureSource { Live, Imported }
public enum DebuggerSnapshotBreakpointType { Breakpoint, Watchpoint }
public enum DebuggerSnapshotBreakpointLifetime { Persistent, Temporary }
public enum DebuggerSnapshotSectionState { Complete, Unavailable, Skipped, Failed }

public sealed record DebuggerSnapshotSectionStatus(DebuggerSnapshotSectionState State, string? Error = null)
{
    public static DebuggerSnapshotSectionStatus Complete { get; } = new(DebuggerSnapshotSectionState.Complete);
    public static DebuggerSnapshotSectionStatus Unavailable(string? error = null) => new(DebuggerSnapshotSectionState.Unavailable, error);
    public static DebuggerSnapshotSectionStatus Skipped(string? error = null) => new(DebuggerSnapshotSectionState.Skipped, error);
    public static DebuggerSnapshotSectionStatus Failed(string error) => new(DebuggerSnapshotSectionState.Failed, error);
}

public sealed record DebuggerSnapshotSource(
    string ApplicationVersion,
    int ApplicationRevision,
    string PluginId,
    string PluginName,
    string PluginVersion,
    int PluginRevision,
    string PluginApiVersion,
    string PlatformDisplayName,
    ulong ProcessId,
    string ProcessName,
    CpuArchitecture Architecture,
    int AddressWidth,
    int PointerWidth,
    Endianness Endianness,
    long ConnectionGenerationAtCapture,
    DebuggerSnapshotCaptureSource CaptureSource);

public sealed record DebuggerSnapshotEventContext(
    long Sequence,
    DateTimeOffset Timestamp,
    DebuggerEventKind Kind,
    DebuggerExecutionState ExecutionState,
    DebuggerStopReason StopReason,
    ulong? ThreadId,
    string? ThreadName,
    ulong? InstructionPointer,
    ulong? TriggerInstructionAddress,
    DebuggerTriggerResolution TriggerResolution,
    ulong? WatchedAddress,
    DebuggerBreakpointAccess? WatchpointAccess,
    int? WatchpointSize,
    string? TriggeredBreakpointId,
    DebuggerSnapshotBreakpointType? TriggeredBreakpointType,
    DebuggerBreakpointKind? TriggeredBreakpointMechanism,
    DebuggerSnapshotBreakpointLifetime? TriggeredBreakpointLifetime,
    string? Message);

public sealed record DebuggerSnapshotRegister(
    string Id,
    string Name,
    string? Group,
    DebuggerRegisterRole Role,
    int BitWidth,
    DebuggerRegisterValueEncoding ValueEncoding,
    string RawBytes,
    string FormattedValue,
    bool IsWritable);

public sealed record DebuggerSnapshotCallFrame(
    int Index,
    ulong InstructionAddress,
    ulong? ReturnAddress,
    ulong? StackPointer,
    ulong? FramePointer,
    string? Module,
    ulong? ModuleBase,
    ulong? ModuleOffset,
    string? Symbol);

public sealed record DebuggerSnapshotInstruction(
    ulong Address,
    string? Module,
    ulong? ModuleOffset,
    string Bytes,
    string InstructionText,
    DisassemblyFlowControl FlowControl,
    ulong? DirectTarget,
    IReadOnlyList<string> Markers);

public sealed record DebuggerSnapshotBreakpoint(
    string Id,
    ulong Address,
    bool Enabled,
    DebuggerSnapshotBreakpointType Type,
    DebuggerBreakpointKind Mechanism,
    DebuggerBreakpointAccess Access,
    int Size,
    DebuggerSnapshotBreakpointLifetime Lifetime,
    bool IsTriggeredItem);

public sealed record DebuggerSnapshotMemoryBlock(
    string Kind,
    ulong StartAddress,
    string Bytes,
    string? SourceRegister = null);

public sealed record DebuggerSnapshotSections(
    DebuggerSnapshotSectionStatus Registers,
    DebuggerSnapshotSectionStatus CallStack,
    DebuggerSnapshotSectionStatus Disassembly,
    DebuggerSnapshotSectionStatus Breakpoints,
    DebuggerSnapshotSectionStatus Memory);

public sealed record DebuggerSnapshot(
    Guid SnapshotId,
    DateTimeOffset CapturedAtUtc,
    string Label,
    string Notes,
    string Group,
    DebuggerSnapshotCaptureSource CaptureMode,
    DebuggerSnapshotSource Source,
    DebuggerSnapshotSections Sections,
    DebuggerSnapshotEventContext Event,
    IReadOnlyList<DebuggerSnapshotRegister> Registers,
    IReadOnlyList<DebuggerSnapshotCallFrame> CallStack,
    IReadOnlyList<DebuggerSnapshotInstruction> Disassembly,
    IReadOnlyList<DebuggerSnapshotBreakpoint> Breakpoints,
    IReadOnlyList<DebuggerSnapshotMemoryBlock> Memory)
{
    public DebuggerSnapshot WithMetadata(string? label = null, string? notes = null, string? group = null) => this with
    {
        Label = label ?? Label,
        Notes = notes ?? Notes,
        Group = group ?? Group
    };
}
