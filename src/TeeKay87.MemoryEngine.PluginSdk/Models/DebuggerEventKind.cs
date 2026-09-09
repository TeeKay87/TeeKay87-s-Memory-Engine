namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public enum DebuggerEventKind
{
    Attached,
    Detached,
    Paused,
    Resumed,
    Breakpoint,
    Watchpoint,
    StepCompleted,
    Exception,
    ThreadCreated,
    ThreadExited,
    Other
}
