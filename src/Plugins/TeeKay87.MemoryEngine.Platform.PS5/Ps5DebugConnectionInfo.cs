namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed record Ps5DebugConnectionInfo(
    string ProtocolVersion,
    string Branding,
    string? CapabilityLevel,
    ushort FirmwareVersion);
