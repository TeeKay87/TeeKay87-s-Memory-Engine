using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public enum CpuArchitecture
{
    Unknown,
    X86,
    X64,
    Arm32,
    Arm64,
    PowerPc,
    Mips
}

public enum Endianness
{
    Little,
    Big
}

public sealed record TargetArchitecture
{
    public TargetArchitecture(
        CpuArchitecture cpu,
        int pointerWidthBits,
        int addressWidthBits,
        Endianness endianness)
    {
        if (pointerWidthBits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointerWidthBits));
        }

        if (addressWidthBits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(addressWidthBits));
        }

        Cpu = cpu;
        PointerWidthBits = pointerWidthBits;
        AddressWidthBits = addressWidthBits;
        Endianness = endianness;
    }

    public CpuArchitecture Cpu { get; }

    public int PointerWidthBits { get; }

    public int AddressWidthBits { get; }

    public Endianness Endianness { get; }
}
