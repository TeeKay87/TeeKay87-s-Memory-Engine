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
}
