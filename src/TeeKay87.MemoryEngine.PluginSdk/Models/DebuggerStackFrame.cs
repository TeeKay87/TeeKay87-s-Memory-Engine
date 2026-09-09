using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record DebuggerStackFrame
{
    public DebuggerStackFrame(
        int index,
        ulong instructionAddress,
        ulong? stackPointer = null,
        ulong? framePointer = null,
        ulong? returnAddress = null,
        string? moduleName = null,
        string? symbolName = null)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        Index = index;
        InstructionAddress = instructionAddress;
        StackPointer = stackPointer;
        FramePointer = framePointer;
        ReturnAddress = returnAddress;
        ModuleName = moduleName;
        SymbolName = symbolName;
    }

    public int Index { get; }

    public ulong InstructionAddress { get; }

    public ulong? StackPointer { get; }

    public ulong? FramePointer { get; }

    public ulong? ReturnAddress { get; }

    public string? ModuleName { get; }

    public string? SymbolName { get; }
}
