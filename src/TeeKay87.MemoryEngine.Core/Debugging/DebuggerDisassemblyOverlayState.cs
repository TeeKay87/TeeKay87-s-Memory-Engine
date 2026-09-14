using System;
using System.Collections.Generic;
using System.Linq;
using TeeKay87.MemoryEngine.Core.Disassembly;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Debugging;

public sealed class DebuggerDisassemblyOverlayState
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, DisassembledInstruction> _originalSoftwareInstructions = new();
    private readonly Dictionary<string, DebuggerBreakpoint> _breakpoints = new(StringComparer.Ordinal);
    private readonly HashSet<ulong> _stagedSoftwareBreakpointRetirements = new();
    private ulong? _watchpointMarkerAddress;
    private string? _watchpointMarkerText;
    private ulong? _watchpointStopAddress;

    public void RememberSoftwareBreakpointInstruction(DisassembledInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        if (!instruction.IsValid)
        {
            return;
        }

        lock (_gate)
        {
            _originalSoftwareInstructions[instruction.Address] = instruction;
        }
    }

    public void SynchronizeBreakpoints(
        IEnumerable<DebuggerBreakpoint> breakpoints,
        DebuggerSessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        DebuggerBreakpoint[] snapshot = breakpoints.ToArray();

        lock (_gate)
        {
            ulong[] previouslyEnabledSoftwareAddresses = _breakpoints.Values
                .Where(IsEnabledSoftwareExecuteBreakpoint)
                .Select(item => item.Request.Address)
                .Distinct()
                .ToArray();

            _breakpoints.Clear();
            foreach (DebuggerBreakpoint breakpoint in snapshot)
            {
                _breakpoints[breakpoint.Id] = breakpoint;
            }

            if (sessionState == DebuggerSessionState.Paused)
            {
                HashSet<ulong> currentlyEnabledSoftwareAddresses = _breakpoints.Values
                    .Where(IsEnabledSoftwareExecuteBreakpoint)
                    .Select(item => item.Request.Address)
                    .ToHashSet();

                foreach (ulong address in previouslyEnabledSoftwareAddresses)
                {
                    if (!currentlyEnabledSoftwareAddresses.Contains(address))
                    {
                        _stagedSoftwareBreakpointRetirements.Add(address);
                    }
                }

                foreach (ulong address in currentlyEnabledSoftwareAddresses)
                {
                    _stagedSoftwareBreakpointRetirements.Remove(address);
                }
            }
            else
            {
                _stagedSoftwareBreakpointRetirements.Clear();
            }

            RemoveUnusedOriginalInstructionsNoLock();
        }
    }

    public void StageSoftwareBreakpointRetirement(ulong address)
    {
        lock (_gate)
        {
            if (_originalSoftwareInstructions.ContainsKey(address))
            {
                _stagedSoftwareBreakpointRetirements.Add(address);
            }
        }
    }

    public void CancelSoftwareBreakpointRetirement(ulong address)
    {
        lock (_gate)
        {
            _stagedSoftwareBreakpointRetirements.Remove(address);
        }
    }

    public void CompleteStagedSoftwareBreakpointRetirements()
    {
        lock (_gate)
        {
            _stagedSoftwareBreakpointRetirements.Clear();
            RemoveUnusedOriginalInstructionsNoLock();
        }
    }

    public void ObserveEvent(DebuggerEvent debugEvent)
    {
        ArgumentNullException.ThrowIfNull(debugEvent);

        lock (_gate)
        {
            if (debugEvent.ExecutionState == DebuggerExecutionState.Running)
            {
                _watchpointMarkerAddress = null;
                _watchpointMarkerText = null;
                _watchpointStopAddress = null;
                return;
            }

            if (debugEvent.ExecutionState != DebuggerExecutionState.Paused)
            {
                return;
            }

            if (debugEvent.Kind != DebuggerEventKind.Watchpoint)
            {
                _watchpointMarkerAddress = null;
                _watchpointMarkerText = null;
                _watchpointStopAddress = null;
                return;
            }

            _watchpointStopAddress = debugEvent.InstructionPointer;
            if (debugEvent.TriggerInstructionAddress.HasValue)
            {
                _watchpointMarkerAddress = debugEvent.TriggerInstructionAddress.Value;
                _watchpointMarkerText = "Watchpoint hit";
            }
            else
            {
                _watchpointMarkerAddress = debugEvent.InstructionPointer;
                _watchpointMarkerText = _watchpointMarkerAddress.HasValue
                    ? "Watchpoint stop (trigger unresolved)"
                    : null;
            }
        }
    }

    public DisassemblyOverlay CreateOverlay()
    {
        lock (_gate)
        {
            HashSet<ulong> addressesRequiringOriginalBytes = _breakpoints.Values
                .Where(IsEnabledSoftwareExecuteBreakpoint)
                .Select(item => item.Request.Address)
                .ToHashSet();
            addressesRequiringOriginalBytes.UnionWith(_stagedSoftwareBreakpointRetirements);

            List<DisassemblyByteOverlay> byteOverlays = new();
            foreach (ulong address in addressesRequiringOriginalBytes.OrderBy(value => value))
            {
                if (_originalSoftwareInstructions.TryGetValue(address, out DisassembledInstruction? instruction))
                {
                    byteOverlays.Add(new DisassemblyByteOverlay(address, instruction.RawBytes.Span));
                }
            }

            List<DisassemblyMarker> markers = new();
            foreach (DebuggerBreakpoint breakpoint in _breakpoints.Values
                         .OrderBy(item => item.Request.Address)
                         .ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                if (breakpoint.Request.Kind != DebuggerBreakpointKind.Software ||
                    breakpoint.Request.Access != DebuggerBreakpointAccess.Execute)
                {
                    continue;
                }

                markers.Add(new DisassemblyMarker(
                    breakpoint.Request.Address,
                    breakpoint.IsEnabled ? "Breakpoint" : "Breakpoint (disabled)"));
            }

            if (_watchpointMarkerAddress.HasValue && !string.IsNullOrWhiteSpace(_watchpointMarkerText))
            {
                markers.Add(new DisassemblyMarker(
                    _watchpointMarkerAddress.Value,
                    _watchpointMarkerText));
            }

            if (_watchpointStopAddress.HasValue &&
                (!_watchpointMarkerAddress.HasValue || _watchpointStopAddress.Value != _watchpointMarkerAddress.Value))
            {
                markers.Add(new DisassemblyMarker(
                    _watchpointStopAddress.Value,
                    "Stop / Current IP"));
            }

            return new DisassemblyOverlay(byteOverlays, markers);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _originalSoftwareInstructions.Clear();
            _breakpoints.Clear();
            _stagedSoftwareBreakpointRetirements.Clear();
            _watchpointMarkerAddress = null;
            _watchpointMarkerText = null;
            _watchpointStopAddress = null;
        }
    }

    private static bool IsEnabledSoftwareExecuteBreakpoint(DebuggerBreakpoint breakpoint)
    {
        return breakpoint.IsEnabled &&
               breakpoint.Request.Kind == DebuggerBreakpointKind.Software &&
               breakpoint.Request.Access == DebuggerBreakpointAccess.Execute;
    }

    private void RemoveUnusedOriginalInstructionsNoLock()
    {
        HashSet<ulong> retainedAddresses = _breakpoints.Values
            .Where(item => item.Request.Kind == DebuggerBreakpointKind.Software &&
                           item.Request.Access == DebuggerBreakpointAccess.Execute)
            .Select(item => item.Request.Address)
            .ToHashSet();
        retainedAddresses.UnionWith(_stagedSoftwareBreakpointRetirements);

        foreach (ulong address in _originalSoftwareInstructions.Keys
                     .Where(address => !retainedAddresses.Contains(address))
                     .ToArray())
        {
            _originalSoftwareInstructions.Remove(address);
        }
    }
}
