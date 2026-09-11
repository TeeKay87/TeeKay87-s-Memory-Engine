using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class DebuggerBreakpointViewModel
{
    public DebuggerBreakpointViewModel(DebuggerBreakpoint breakpoint)
    {
        Breakpoint = breakpoint;
    }

    public DebuggerBreakpoint Breakpoint { get; }

    public string Id => Breakpoint.Id;

    public ulong Address => Breakpoint.Request.Address;

    public string AddressText => $"0x{Address:X}";

    public string Type => Breakpoint.Request.Access == DebuggerBreakpointAccess.Execute
        ? "Breakpoint"
        : "Watchpoint";

    public string Mechanism => Breakpoint.Request.Kind.ToString();

    public string Kind => Mechanism;

    public int Size => Breakpoint.Request.Size;

    public string Access => Breakpoint.Request.Access switch
    {
        DebuggerBreakpointAccess.ReadWrite => "Read / Write",
        _ => Breakpoint.Request.Access.ToString()
    };

    public string Lifetime => Breakpoint.Request.IsTemporary ? "Temporary" : "Persistent";

    public string State => Breakpoint.IsEnabled ? "Enabled" : "Disabled";

    public bool IsEnabled => Breakpoint.IsEnabled;
}
