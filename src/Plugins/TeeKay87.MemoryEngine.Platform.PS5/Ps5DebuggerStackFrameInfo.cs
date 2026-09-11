namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed record Ps5DebuggerStackFrameInfo(
    ulong FramePointer,
    ulong StackPointer,
    ulong SavedFramePointer,
    ulong ReturnAddress);
