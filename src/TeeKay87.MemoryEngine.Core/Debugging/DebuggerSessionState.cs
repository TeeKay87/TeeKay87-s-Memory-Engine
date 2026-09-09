namespace TeeKay87.MemoryEngine.Core.Debugging;

public enum DebuggerSessionState
{
    Detached,
    Attaching,
    Attached,
    Running,
    Paused,
    Detaching,
    Faulted
}
