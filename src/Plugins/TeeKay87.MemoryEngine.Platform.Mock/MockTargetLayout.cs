using System;

namespace TeeKay87.MemoryEngine.Platform.Mock;

public static class MockTargetLayout
{
    public const ulong ProcessId = 1001;
    public const string ProcessName = "TestGame.exe";
    public const ulong BaseAddress = 0x10000000;
    public const int MemorySize = 0x10000;
    public const ulong HealthAddress = BaseAddress + 0x100;
    public const ulong AmmoAddress = BaseAddress + 0x104;
    public const ulong MoneyAddress = BaseAddress + 0x108;
    public const ulong CodeAddress = BaseAddress + 0x400;

    private static readonly byte[] CodeData =
    {
        0x10, 0x2A,
        0x20, 0x04,
        0x11, 0x01,
        0x31, 0x02,
        0x40,
        0x00,
        0x40
    };

    public static ReadOnlyMemory<byte> CodeBytes => CodeData;
}
