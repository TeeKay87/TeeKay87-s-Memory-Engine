using System;
using System.Linq;
using TeeKay87.MemoryEngine.Core.Disassembly;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Debugging;

public sealed class DebuggerWatchpointTriggerResolver
{
    public DebuggerEvent Resolve(DebuggerEvent debugEvent, DisassemblySnapshot logicalDisassembly)
    {
        ArgumentNullException.ThrowIfNull(debugEvent);
        ArgumentNullException.ThrowIfNull(logicalDisassembly);

        if (debugEvent.Kind != DebuggerEventKind.Watchpoint ||
            debugEvent.ExecutionState != DebuggerExecutionState.Paused ||
            !debugEvent.InstructionPointer.HasValue ||
            debugEvent.TriggerInstructionAddress.HasValue ||
            debugEvent.TriggeredBreakpoint is null)
        {
            return debugEvent;
        }

        ulong instructionPointer = debugEvent.InstructionPointer.Value;
        DisassembledInstruction[] candidates = logicalDisassembly.Instructions
            .Where(instruction =>
                instruction.IsValid &&
                instruction.Address < instructionPointer &&
                TryEndsAt(instruction, instructionPointer))
            .ToArray();

        if (candidates.Length != 1)
        {
            return debugEvent;
        }

        return debugEvent.WithTriggerInstruction(
            candidates[0].Address,
            DebuggerTriggerResolution.DisassemblyDerived);
    }

    private static bool TryEndsAt(DisassembledInstruction instruction, ulong address)
    {
        try
        {
            return checked(instruction.Address + (ulong)instruction.Length) == address;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
