namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public enum DebuggerStopReason
{
    None,
    PauseRequested,
    Breakpoint,
    Watchpoint,
    StepCompleted,
    Exception,
    Signal,
    Backend,
    Other
}
