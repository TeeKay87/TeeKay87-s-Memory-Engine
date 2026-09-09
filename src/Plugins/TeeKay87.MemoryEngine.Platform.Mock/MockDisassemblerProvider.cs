using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.Mock;

internal sealed class MockDisassemblerProvider : IDisassemblerProvider
{
    public bool SupportsArchitecture(TargetArchitecture architecture)
    {
        ArgumentNullException.ThrowIfNull(architecture);

        return architecture.Cpu == CpuArchitecture.Unknown &&
               architecture.PointerWidthBits == 64 &&
               architecture.AddressWidthBits == 64 &&
               architecture.Endianness == Endianness.Little;
    }

    public Task<IReadOnlyList<DisassembledInstruction>> DisassembleAsync(
        ulong startAddress,
        ReadOnlyMemory<byte> bytes,
        TargetArchitecture architecture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(architecture);
        cancellationToken.ThrowIfCancellationRequested();

        if (!SupportsArchitecture(architecture))
        {
            throw new NotSupportedException("The Mock disassembler supports only the Mock Target's declared custom 64-bit little-endian test architecture.");
        }

        List<DisassembledInstruction> instructions = new();
        ReadOnlySpan<byte> source = bytes.Span;
        int offset = 0;

        while (offset < source.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong instructionAddress = checked(startAddress + (ulong)offset);
            byte opcode = source[offset];

            switch (opcode)
            {
                case 0x00:
                    instructions.Add(CreateInstruction(instructionAddress, source.Slice(offset, 1), "nop", string.Empty));
                    offset += 1;
                    break;

                case 0x10:
                    if (!TryReadTwoByteInstruction(source, offset, out ReadOnlySpan<byte> loadBytes))
                    {
                        instructions.Add(CreateInvalidInstruction(instructionAddress, source.Slice(offset, 1)));
                        offset += 1;
                        break;
                    }

                    instructions.Add(CreateInstruction(
                        instructionAddress,
                        loadBytes,
                        "load",
                        $"r0, 0x{loadBytes[1]:X2}"));
                    offset += 2;
                    break;

                case 0x11:
                    if (!TryReadTwoByteInstruction(source, offset, out ReadOnlySpan<byte> addBytes))
                    {
                        instructions.Add(CreateInvalidInstruction(instructionAddress, source.Slice(offset, 1)));
                        offset += 1;
                        break;
                    }

                    instructions.Add(CreateInstruction(
                        instructionAddress,
                        addBytes,
                        "add",
                        $"r0, 0x{addBytes[1]:X2}"));
                    offset += 2;
                    break;

                case 0x20:
                    if (!TryReadTwoByteInstruction(source, offset, out ReadOnlySpan<byte> callBytes))
                    {
                        instructions.Add(CreateInvalidInstruction(instructionAddress, source.Slice(offset, 1)));
                        offset += 1;
                        break;
                    }

                    ulong callTarget = ResolveRelativeTarget(instructionAddress, callBytes[1]);
                    instructions.Add(CreateInstruction(
                        instructionAddress,
                        callBytes,
                        "call",
                        $"0x{callTarget:X}",
                        DisassemblyFlowControl.Call,
                        callTarget));
                    offset += 2;
                    break;

                case 0x30:
                    if (!TryReadTwoByteInstruction(source, offset, out ReadOnlySpan<byte> jumpBytes))
                    {
                        instructions.Add(CreateInvalidInstruction(instructionAddress, source.Slice(offset, 1)));
                        offset += 1;
                        break;
                    }

                    ulong jumpTarget = ResolveRelativeTarget(instructionAddress, jumpBytes[1]);
                    instructions.Add(CreateInstruction(
                        instructionAddress,
                        jumpBytes,
                        "jump",
                        $"0x{jumpTarget:X}",
                        DisassemblyFlowControl.Jump,
                        jumpTarget));
                    offset += 2;
                    break;

                case 0x31:
                    if (!TryReadTwoByteInstruction(source, offset, out ReadOnlySpan<byte> conditionalBytes))
                    {
                        instructions.Add(CreateInvalidInstruction(instructionAddress, source.Slice(offset, 1)));
                        offset += 1;
                        break;
                    }

                    ulong conditionalTarget = ResolveRelativeTarget(instructionAddress, conditionalBytes[1]);
                    instructions.Add(CreateInstruction(
                        instructionAddress,
                        conditionalBytes,
                        "jump-if",
                        $"0x{conditionalTarget:X}",
                        DisassemblyFlowControl.ConditionalJump,
                        conditionalTarget));
                    offset += 2;
                    break;

                case 0x40:
                    instructions.Add(CreateInstruction(
                        instructionAddress,
                        source.Slice(offset, 1),
                        "return",
                        string.Empty,
                        DisassemblyFlowControl.Return));
                    offset += 1;
                    break;

                case 0x50:
                    instructions.Add(CreateInstruction(
                        instructionAddress,
                        source.Slice(offset, 1),
                        "interrupt",
                        string.Empty,
                        DisassemblyFlowControl.Interrupt));
                    offset += 1;
                    break;

                default:
                    instructions.Add(CreateInvalidInstruction(instructionAddress, source.Slice(offset, 1)));
                    offset += 1;
                    break;
            }
        }

        return Task.FromResult<IReadOnlyList<DisassembledInstruction>>(instructions);
    }

    private static bool TryReadTwoByteInstruction(
        ReadOnlySpan<byte> source,
        int offset,
        out ReadOnlySpan<byte> instructionBytes)
    {
        if (offset + 2 > source.Length)
        {
            instructionBytes = default;
            return false;
        }

        instructionBytes = source.Slice(offset, 2);
        return true;
    }

    private static DisassembledInstruction CreateInstruction(
        ulong address,
        ReadOnlySpan<byte> rawBytes,
        string mnemonic,
        string operands,
        DisassemblyFlowControl flowControl = DisassemblyFlowControl.None,
        ulong? branchTarget = null)
    {
        return new DisassembledInstruction(
            address,
            rawBytes,
            mnemonic,
            operands,
            flowControl,
            branchTarget,
            isValid: true,
            syntaxTokens: CreateSyntaxTokens(mnemonic, operands, flowControl));
    }


    private static IReadOnlyList<DisassemblyTextToken> CreateSyntaxTokens(
        string mnemonic,
        string operands,
        DisassemblyFlowControl flowControl)
    {
        List<DisassemblyTextToken> tokens = new()
        {
            new(
                mnemonic,
                flowControl == DisassemblyFlowControl.None
                    ? DisassemblyTextTokenKind.Mnemonic
                    : DisassemblyTextTokenKind.FlowControlMnemonic)
        };

        if (string.IsNullOrEmpty(operands))
        {
            return tokens;
        }

        tokens.Add(new DisassemblyTextToken(" ", DisassemblyTextTokenKind.Text));

        const string RegisterOperandPrefix = "r0, ";
        if (operands.StartsWith(RegisterOperandPrefix, StringComparison.Ordinal))
        {
            tokens.Add(new DisassemblyTextToken("r0", DisassemblyTextTokenKind.Register));
            tokens.Add(new DisassemblyTextToken(", ", DisassemblyTextTokenKind.Text));
            tokens.Add(new DisassemblyTextToken(
                operands[RegisterOperandPrefix.Length..],
                DisassemblyTextTokenKind.Number));
        }
        else
        {
            tokens.Add(new DisassemblyTextToken(operands, DisassemblyTextTokenKind.Number));
        }

        return tokens;
    }

    private static DisassembledInstruction CreateInvalidInstruction(
        ulong address,
        ReadOnlySpan<byte> rawBytes)
    {
        return new DisassembledInstruction(
            address,
            rawBytes,
            "invalid",
            string.Empty,
            DisassemblyFlowControl.Other,
            branchTarget: null,
            isValid: false);
    }

    private static ulong ResolveRelativeTarget(ulong instructionAddress, byte encodedDisplacement)
    {
        ulong nextAddress = checked(instructionAddress + 2UL);
        sbyte displacement = unchecked((sbyte)encodedDisplacement);

        return displacement >= 0
            ? checked(nextAddress + (ulong)displacement)
            : checked(nextAddress - (ulong)(-(int)displacement));
    }
}
