using System;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed record Ps5DebugConnectionInfo(
    string ProtocolVersion,
    string Branding,
    string? CapabilityLevel,
    ushort FirmwareVersion)
{
    private static readonly Version MinimumExtendedDebuggerRegisterProtocol = new(1, 3);
    private static readonly Version MinimumExtendedDebuggerRegisterCapability = new(1, 0);

    public bool SupportsExtendedDebuggerRegisterReads =>
        IsVersionAtLeast(ProtocolVersion, MinimumExtendedDebuggerRegisterProtocol) &&
        IsVersionAtLeast(CapabilityLevel, MinimumExtendedDebuggerRegisterCapability);

    private static bool IsVersionAtLeast(string? value, Version minimum)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               Version.TryParse(value.Trim(), out Version? version) &&
               version is not null &&
               version.CompareTo(minimum) >= 0;
    }
}
