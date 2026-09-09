using System;
using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal static class Ps5GeneralRegisterMapper
{
    public static IReadOnlyList<DebuggerRegister> Map(ReadOnlyMemory<byte> rawRegisters)
    {
        if (rawRegisters.Length != Ps5DebugProtocol.DebuggerGeneralRegisterSize)
        {
            throw new ArgumentException(
                $"The PS5 general-register block must be exactly {Ps5DebugProtocol.DebuggerGeneralRegisterSize} bytes.",
                nameof(rawRegisters));
        }

        return new[]
        {
            Create64("rax", "RAX", rawRegisters, 0x70),
            Create64("rbx", "RBX", rawRegisters, 0x58),
            Create64("rcx", "RCX", rawRegisters, 0x68),
            Create64("rdx", "RDX", rawRegisters, 0x60),
            Create64("rsi", "RSI", rawRegisters, 0x48),
            Create64("rdi", "RDI", rawRegisters, 0x40),
            Create64("rbp", "RBP", rawRegisters, 0x50, DebuggerRegisterRole.FramePointer),
            Create64("rsp", "RSP", rawRegisters, 0xA0, DebuggerRegisterRole.StackPointer),
            Create64("r8", "R8", rawRegisters, 0x38),
            Create64("r9", "R9", rawRegisters, 0x30),
            Create64("r10", "R10", rawRegisters, 0x28),
            Create64("r11", "R11", rawRegisters, 0x20),
            Create64("r12", "R12", rawRegisters, 0x18),
            Create64("r13", "R13", rawRegisters, 0x10),
            Create64("r14", "R14", rawRegisters, 0x08),
            Create64("r15", "R15", rawRegisters, 0x00),
            Create64("rip", "RIP", rawRegisters, 0x88, DebuggerRegisterRole.InstructionPointer, "Control"),
            Create64("rflags", "RFLAGS", rawRegisters, 0x98, group: "Control"),
            Create64("cs", "CS", rawRegisters, 0x90, group: "Segments"),
            Create64("ss", "SS", rawRegisters, 0xA8, group: "Segments"),
            Create16("fs", "FS", rawRegisters, 0x7C, "Segments"),
            Create16("gs", "GS", rawRegisters, 0x7E, "Segments"),
            Create16("es", "ES", rawRegisters, 0x84, "Segments"),
            Create16("ds", "DS", rawRegisters, 0x86, "Segments"),
            Create32("trapno", "TRAPNO", rawRegisters, 0x78, "Stop Context"),
            Create32("err", "ERR", rawRegisters, 0x80, "Stop Context")
        };
    }

    private static DebuggerRegister Create64(
        string id,
        string displayName,
        ReadOnlyMemory<byte> source,
        int offset,
        DebuggerRegisterRole role = DebuggerRegisterRole.None,
        string group = "General")
    {
        return Create(id, displayName, 64, source.Slice(offset, 8), group, role);
    }

    private static DebuggerRegister Create32(
        string id,
        string displayName,
        ReadOnlyMemory<byte> source,
        int offset,
        string group)
    {
        return Create(id, displayName, 32, source.Slice(offset, 4), group, DebuggerRegisterRole.None);
    }

    private static DebuggerRegister Create16(
        string id,
        string displayName,
        ReadOnlyMemory<byte> source,
        int offset,
        string group)
    {
        return Create(id, displayName, 16, source.Slice(offset, 2), group, DebuggerRegisterRole.None);
    }

    private static DebuggerRegister Create(
        string id,
        string displayName,
        int bitWidth,
        ReadOnlyMemory<byte> value,
        string group,
        DebuggerRegisterRole role)
    {
        return new DebuggerRegister(
            id,
            displayName,
            bitWidth,
            value,
            group,
            role,
            canWrite: false,
            valueEncoding: DebuggerRegisterValueEncoding.UnsignedLittleEndian);
    }
}
