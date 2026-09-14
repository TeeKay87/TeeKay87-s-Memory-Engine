using System;
using System.Collections.Generic;
using System.Linq;

namespace TeeKay87.MemoryEngine.Core.Debugging.Snapshots;

public sealed record DebuggerSnapshotCaptureRequest(
    DebuggerSnapshotSource Source,
    DebuggerSnapshotEventContext Event,
    IReadOnlyList<DebuggerSnapshotRegister> Registers,
    IReadOnlyList<DebuggerSnapshotCallFrame> CallStack,
    IReadOnlyList<DebuggerSnapshotInstruction> Disassembly,
    IReadOnlyList<DebuggerSnapshotBreakpoint> Breakpoints,
    IReadOnlyList<DebuggerSnapshotMemoryBlock> Memory,
    DebuggerSnapshotSections Sections,
    string Label,
    string Group = "",
    string Notes = "");

public sealed class DebuggerSnapshotCaptureService
{
    public DebuggerSnapshot Capture(DebuggerSnapshotCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Event);
        ArgumentNullException.ThrowIfNull(request.Sections);
        ArgumentNullException.ThrowIfNull(request.Registers);
        ArgumentNullException.ThrowIfNull(request.CallStack);
        ArgumentNullException.ThrowIfNull(request.Disassembly);
        ArgumentNullException.ThrowIfNull(request.Breakpoints);
        ArgumentNullException.ThrowIfNull(request.Memory);

        return Freeze(new DebuggerSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            request.Label ?? string.Empty,
            request.Notes ?? string.Empty,
            request.Group ?? string.Empty,
            DebuggerSnapshotCaptureSource.Live,
            request.Source,
            request.Sections,
            request.Event,
            Array.AsReadOnly(request.Registers.ToArray()),
            Array.AsReadOnly(request.CallStack.ToArray()),
            Array.AsReadOnly(request.Disassembly.ToArray()),
            Array.AsReadOnly(request.Breakpoints.ToArray()),
            Array.AsReadOnly(request.Memory.ToArray())));
    }

    public static DebuggerSnapshot Freeze(DebuggerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot with
        {
            Registers = Array.AsReadOnly(snapshot.Registers.ToArray()),
            CallStack = Array.AsReadOnly(snapshot.CallStack.ToArray()),
            Disassembly = Array.AsReadOnly(snapshot.Disassembly
                .Select(instruction => instruction with
                {
                    Markers = Array.AsReadOnly(instruction.Markers.ToArray())
                })
                .ToArray()),
            Breakpoints = Array.AsReadOnly(snapshot.Breakpoints.ToArray()),
            Memory = Array.AsReadOnly(snapshot.Memory.ToArray())
        };
    }
}
