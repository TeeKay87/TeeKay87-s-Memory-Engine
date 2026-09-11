using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record DebuggerBreakpointValidationResult
{
    private DebuggerBreakpointValidationResult(bool isValid, string message)
    {
        IsValid = isValid;
        Message = message ?? string.Empty;
    }

    public bool IsValid { get; }

    public string Message { get; }

    public static DebuggerBreakpointValidationResult Valid() => new(true, string.Empty);

    public static DebuggerBreakpointValidationResult Invalid(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new DebuggerBreakpointValidationResult(false, message);
    }
}
