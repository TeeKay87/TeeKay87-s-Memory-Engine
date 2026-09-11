using System;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class DebuggerStackFrameViewModel
{
    public DebuggerStackFrameViewModel(DebuggerStackFrame frame)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    public DebuggerStackFrame Frame { get; }

    public int Index => Frame.Index;

    public ulong InstructionAddress => Frame.InstructionAddress;

    public string InstructionAddressText => $"0x{InstructionAddress:X}";

    public string StackPointerText => FormatAddress(Frame.StackPointer);

    public string FramePointerText => FormatAddress(Frame.FramePointer);

    public string ReturnAddressText => FormatAddress(Frame.ReturnAddress);

    public string ModuleName => Frame.ModuleName ?? string.Empty;

    public string SymbolName => Frame.SymbolName ?? string.Empty;

    private static string FormatAddress(ulong? address)
    {
        return address.HasValue ? $"0x{address.Value:X}" : "Unavailable";
    }
}
