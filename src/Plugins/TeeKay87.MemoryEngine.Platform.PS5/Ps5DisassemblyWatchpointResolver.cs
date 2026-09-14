using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Iced.Intel;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed class Ps5DisassemblyWatchpointResolver : IDisassemblyWatchpointResolver
{
    private const int Bitness = 64;

    public DisassemblyWatchpointTarget? ResolveWatchpointTarget(
        DisassembledInstruction instruction,
        IReadOnlyList<DebuggerRegister> registers)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(registers);

        if (!instruction.IsValid || instruction.RawBytes.IsEmpty)
        {
            return null;
        }

        ByteArrayCodeReader reader = new(instruction.RawBytes.ToArray());
        Decoder decoder = Decoder.Create(Bitness, reader);
        decoder.IP = instruction.Address;
        decoder.Decode(out Instruction decoded);

        if (decoded.IsInvalid || decoded.Length != instruction.Length)
        {
            return null;
        }

        int memoryOperand = -1;
        for (int operand = 0; operand < decoded.OpCount; operand++)
        {
            if (decoded.GetOpKind(operand) != OpKind.Memory)
            {
                continue;
            }

            if (memoryOperand >= 0)
            {
                return null;
            }

            memoryOperand = operand;
        }

        if (memoryOperand < 0)
        {
            return null;
        }

        InstructionInfoFactory infoFactory = new();
        ref readonly InstructionInfo info = ref infoFactory.GetInfo(decoded);
        DebuggerBreakpointAccess? watchpointAccess = MapWatchpointAccess(info.GetOpAccess(memoryOperand));
        if (!watchpointAccess.HasValue)
        {
            return null;
        }

        int size = decoded.MemorySize.GetSize();
        if (size is not (1 or 2 or 4 or 8))
        {
            return null;
        }

        if (IsAddressRegisterModified(info, decoded.MemoryBase) ||
            IsAddressRegisterModified(info, decoded.MemoryIndex))
        {
            return null;
        }

        if (!TryResolveAddress(decoded, registers, out ulong address))
        {
            return null;
        }

        return new DisassemblyWatchpointTarget(address, size, watchpointAccess.Value);
    }


    private static bool IsAddressRegisterModified(InstructionInfo info, Register register)
    {
        if (register == Register.None || register is Register.RIP or Register.EIP)
        {
            return false;
        }

        foreach (UsedRegister usedRegister in info.GetUsedRegisters())
        {
            if (usedRegister.Register != register)
            {
                continue;
            }

            if (usedRegister.Access is OpAccess.Write or
                OpAccess.CondWrite or
                OpAccess.ReadWrite or
                OpAccess.ReadCondWrite)
            {
                return true;
            }
        }

        return false;
    }

    private static DebuggerBreakpointAccess? MapWatchpointAccess(OpAccess access)
    {
        return access switch
        {
            OpAccess.Write or OpAccess.CondWrite => DebuggerBreakpointAccess.Write,
            OpAccess.Read or OpAccess.CondRead or OpAccess.ReadWrite or OpAccess.ReadCondWrite =>
                DebuggerBreakpointAccess.ReadWrite,
            _ => null
        };
    }

    private static bool TryResolveAddress(
        Instruction instruction,
        IReadOnlyList<DebuggerRegister> registers,
        out ulong address)
    {
        ulong segmentBase = 0;
        if (instruction.MemorySegment == Register.FS)
        {
            if (!TryReadRegister(registers, "fsbase", out segmentBase))
            {
                address = 0;
                return false;
            }
        }
        else if (instruction.MemorySegment == Register.GS)
        {
            if (!TryReadRegister(registers, "gsbase", out segmentBase))
            {
                address = 0;
                return false;
            }
        }

        if (instruction.IsIPRelativeMemoryOperand)
        {
            address = unchecked(segmentBase + instruction.IPRelativeMemoryAddress);
            return true;
        }

        ulong baseValue = 0;
        if (instruction.MemoryBase != Register.None &&
            !TryReadAddressRegister(registers, instruction.MemoryBase, out baseValue))
        {
            address = 0;
            return false;
        }

        ulong indexValue = 0;
        if (instruction.MemoryIndex != Register.None &&
            !TryReadAddressRegister(registers, instruction.MemoryIndex, out indexValue))
        {
            address = 0;
            return false;
        }

        address = unchecked(
            segmentBase +
            baseValue +
            (indexValue * (uint)instruction.MemoryIndexScale) +
            instruction.MemoryDisplacement64);
        return true;
    }

    private static bool TryReadAddressRegister(
        IReadOnlyList<DebuggerRegister> registers,
        Register register,
        out ulong value)
    {
        string? id = register switch
        {
            Register.RAX => "rax",
            Register.RBX => "rbx",
            Register.RCX => "rcx",
            Register.RDX => "rdx",
            Register.RSI => "rsi",
            Register.RDI => "rdi",
            Register.RBP => "rbp",
            Register.RSP => "rsp",
            Register.R8 => "r8",
            Register.R9 => "r9",
            Register.R10 => "r10",
            Register.R11 => "r11",
            Register.R12 => "r12",
            Register.R13 => "r13",
            Register.R14 => "r14",
            Register.R15 => "r15",
            _ => null
        };

        if (id is null)
        {
            value = 0;
            return false;
        }

        return TryReadRegister(registers, id, out value);
    }

    private static bool TryReadRegister(
        IReadOnlyList<DebuggerRegister> registers,
        string id,
        out ulong value)
    {
        DebuggerRegister? register = registers.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (register is null || register.Value.Length > sizeof(ulong))
        {
            value = 0;
            return false;
        }

        ReadOnlySpan<byte> bytes = register.Value.Span;
        value = bytes.Length switch
        {
            1 => bytes[0],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            8 => BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            _ => 0
        };
        return bytes.Length is 1 or 2 or 4 or 8;
    }
}
