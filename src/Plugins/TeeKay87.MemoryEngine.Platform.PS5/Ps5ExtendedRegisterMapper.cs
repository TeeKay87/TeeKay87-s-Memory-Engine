using System;
using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal static class Ps5ExtendedRegisterMapper
{
    private const int FpuEnvironmentOffset = 0x000;
    private const int FpuStackOffset = 0x020;
    private const int FpuStackSlotSize = 0x10;
    private const int FpuStackValueSize = 0x0A;
    private const int XmmOffset = 0x0A0;
    private const int XmmRegisterSize = 0x10;
    private const int YmmUpperOffset = 0x240;
    private const int YmmUpperRegisterSize = 0x10;

    public static IReadOnlyList<DebuggerRegister> Map(
        byte[]? floatingPointRegisters,
        byte[]? debugRegisters,
        byte[]? fsGsBase)
    {
        List<DebuggerRegister> registers = new();

        if (floatingPointRegisters is not null)
        {
            AddFloatingPointRegisters(registers, floatingPointRegisters);
        }

        if (debugRegisters is not null)
        {
            AddDebugRegisters(registers, debugRegisters);
        }

        if (fsGsBase is not null)
        {
            AddSegmentBaseRegisters(registers, fsGsBase);
        }

        return registers;
    }

    private static void AddFloatingPointRegisters(
        ICollection<DebuggerRegister> registers,
        ReadOnlyMemory<byte> source)
    {
        ValidateBlockLength(
            source,
            Ps5DebugProtocol.DebuggerFloatingPointRegisterSize,
            "floating-point register");

        registers.Add(Create("fcw", "FCW", 16, source.Slice(FpuEnvironmentOffset + 0x00, 2), "FPU"));
        registers.Add(Create("fsw", "FSW", 16, source.Slice(FpuEnvironmentOffset + 0x02, 2), "FPU"));
        registers.Add(Create("ftw", "FTW", 8, source.Slice(FpuEnvironmentOffset + 0x04, 1), "FPU"));
        registers.Add(Create("fop", "FOP", 16, source.Slice(FpuEnvironmentOffset + 0x06, 2), "FPU"));
        registers.Add(Create("fip", "FIP", 64, source.Slice(FpuEnvironmentOffset + 0x08, 8), "FPU"));
        registers.Add(Create("fdp", "FDP", 64, source.Slice(FpuEnvironmentOffset + 0x10, 8), "FPU"));
        registers.Add(Create("mxcsr", "MXCSR", 32, source.Slice(FpuEnvironmentOffset + 0x18, 4), "SIMD Control"));
        registers.Add(Create("mxcsr_mask", "MXCSR_MASK", 32, source.Slice(FpuEnvironmentOffset + 0x1C, 4), "SIMD Control"));

        for (int index = 0; index < 8; index++)
        {
            int offset = FpuStackOffset + (index * FpuStackSlotSize);
            registers.Add(Create(
                $"st{index}",
                $"ST{index}",
                80,
                source.Slice(offset, FpuStackValueSize),
                "FPU"));
        }

        for (int index = 0; index < 16; index++)
        {
            int offset = XmmOffset + (index * XmmRegisterSize);
            registers.Add(Create(
                $"xmm{index}",
                $"XMM{index}",
                128,
                source.Slice(offset, XmmRegisterSize),
                "SIMD"));
        }

        for (int index = 0; index < 16; index++)
        {
            byte[] value = new byte[XmmRegisterSize + YmmUpperRegisterSize];
            source.Slice(XmmOffset + (index * XmmRegisterSize), XmmRegisterSize).Span.CopyTo(value);
            source.Slice(YmmUpperOffset + (index * YmmUpperRegisterSize), YmmUpperRegisterSize)
                .Span
                .CopyTo(value.AsSpan(XmmRegisterSize));

            registers.Add(Create(
                $"ymm{index}",
                $"YMM{index}",
                256,
                value,
                "SIMD"));
        }
    }

    private static void AddDebugRegisters(
        ICollection<DebuggerRegister> registers,
        ReadOnlyMemory<byte> source)
    {
        ValidateBlockLength(
            source,
            Ps5DebugProtocol.DebuggerDebugRegisterSize,
            "debug-register");

        foreach (int index in new[] { 0, 1, 2, 3, 6, 7 })
        {
            registers.Add(Create(
                $"dr{index}",
                $"DR{index}",
                64,
                source.Slice(index * sizeof(ulong), sizeof(ulong)),
                "Debug"));
        }
    }

    private static void AddSegmentBaseRegisters(
        ICollection<DebuggerRegister> registers,
        ReadOnlyMemory<byte> source)
    {
        ValidateBlockLength(
            source,
            Ps5DebugProtocol.DebuggerFsGsBaseSize,
            "FS/GS base");

        registers.Add(Create("fsbase", "FSBASE", 64, source.Slice(0, 8), "Segments"));
        registers.Add(Create("gsbase", "GSBASE", 64, source.Slice(8, 8), "Segments"));
    }

    private static DebuggerRegister Create(
        string id,
        string displayName,
        int bitWidth,
        ReadOnlyMemory<byte> value,
        string group)
    {
        return new DebuggerRegister(
            id,
            displayName,
            bitWidth,
            value,
            group,
            DebuggerRegisterRole.None,
            canWrite: false,
            valueEncoding: DebuggerRegisterValueEncoding.UnsignedLittleEndian);
    }

    private static void ValidateBlockLength(
        ReadOnlyMemory<byte> source,
        int expectedLength,
        string blockName)
    {
        if (source.Length != expectedLength)
        {
            throw new ArgumentException(
                $"The PS5 {blockName} block must be exactly {expectedLength} bytes.",
                nameof(source));
        }
    }
}
