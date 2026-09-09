using System;

namespace TeeKay87.MemoryEngine.Core.Debugging;

public sealed class DebuggerSessionStateChangedEventArgs : EventArgs
{
    public DebuggerSessionStateChangedEventArgs(
        DebuggerSessionState previousState,
        DebuggerSessionState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    public DebuggerSessionState PreviousState { get; }

    public DebuggerSessionState CurrentState { get; }
}
