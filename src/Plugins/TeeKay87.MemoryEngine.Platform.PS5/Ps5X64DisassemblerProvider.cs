using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Iced.Intel;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;
using NeutralFlowControl = TeeKay87.MemoryEngine.PluginSdk.Models.DisassemblyFlowControl;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed class Ps5X64DisassemblerProvider : IDisassemblerProvider
{
    private const int Bitness = 64;

    public bool SupportsArchitecture(TargetArchitecture architecture)
    {
        ArgumentNullException.ThrowIfNull(architecture);

        return architecture.Cpu == CpuArchitecture.X64 &&
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
            throw new NotSupportedException(
                $"The PlayStation 5 disassembler supports only 64-bit little-endian x86-64 targets; received {architecture.Cpu}, {architecture.AddressWidthBits}-bit addresses, {architecture.Endianness} endian.");
        }

        if (bytes.IsEmpty)
        {
            return Task.FromResult<IReadOnlyList<DisassembledInstruction>>(
                Array.Empty<DisassembledInstruction>());
        }

        byte[] source = bytes.ToArray();
        ByteArrayCodeReader codeReader = new(source);
        Decoder decoder = Decoder.Create(Bitness, codeReader);
        decoder.IP = startAddress;

        NasmFormatter formatter = new();
        TokenizingFormatterOutput formatterOutput = new();
        List<DisassembledInstruction> instructions = new();

        while (codeReader.CanReadByte)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong instructionAddress = decoder.IP;
            decoder.Decode(out Instruction decoded);

            int offset = checked((int)(instructionAddress - startAddress));
            int length = decoded.Length;
            if (length <= 0 || offset < 0 || offset >= source.Length || length > source.Length - offset)
            {
                throw new InvalidOperationException(
                    $"The x86-64 decoder returned an invalid instruction length of {length} at 0x{instructionAddress:X}.");
            }

            ReadOnlySpan<byte> rawBytes = source.AsSpan(offset, length);
            if (decoded.IsInvalid)
            {
                instructions.Add(new DisassembledInstruction(
                    instructionAddress,
                    rawBytes,
                    "invalid",
                    string.Empty,
                    NeutralFlowControl.Other,
                    branchTarget: null,
                    isValid: false));
                continue;
            }

            formatter.Format(decoded, formatterOutput);
            IReadOnlyList<FormattedToken> formattedTokens = formatterOutput.TakeTokens();
            string formattedInstruction = string.Concat(formattedTokens.Select(token => token.Text));
            string mnemonic = decoded.Mnemonic.ToString().ToLowerInvariant();
            string operands = ExtractOperands(formattedInstruction, mnemonic, decoded.OpCount);
            (NeutralFlowControl flowControl, ulong? branchTarget) = ResolveFlowControl(decoded);
            IReadOnlyList<DisassemblyTextToken> syntaxTokens = CreateSyntaxTokens(
                formattedTokens,
                flowControl);

            instructions.Add(new DisassembledInstruction(
                instructionAddress,
                rawBytes,
                mnemonic,
                operands,
                flowControl,
                branchTarget,
                isValid: true,
                syntaxTokens: syntaxTokens));
        }

        return Task.FromResult<IReadOnlyList<DisassembledInstruction>>(instructions);
    }

    private static string ExtractOperands(
        string formattedInstruction,
        string mnemonic,
        int operandCount)
    {
        if (operandCount == 0 || string.IsNullOrWhiteSpace(formattedInstruction))
        {
            return string.Empty;
        }

        int mnemonicIndex = CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            formattedInstruction,
            mnemonic,
            CompareOptions.IgnoreCase);
        if (mnemonicIndex >= 0)
        {
            int operandStart = mnemonicIndex + mnemonic.Length;
            return formattedInstruction[operandStart..].TrimStart();
        }

        int firstWhitespace = formattedInstruction.IndexOfAny(new[] { ' ', '\t' });
        return firstWhitespace < 0
            ? string.Empty
            : formattedInstruction[(firstWhitespace + 1)..].TrimStart();
    }

    private static IReadOnlyList<DisassemblyTextToken> CreateSyntaxTokens(
        IReadOnlyList<FormattedToken> formattedTokens,
        NeutralFlowControl flowControl)
    {
        List<DisassemblyTextToken> result = new();

        foreach (FormattedToken formattedToken in formattedTokens)
        {
            DisassemblyTextTokenKind kind = MapTextKind(formattedToken.Kind, flowControl);
            if (result.Count > 0 && result[^1].Kind == kind)
            {
                DisassemblyTextToken previous = result[^1];
                result[^1] = new DisassemblyTextToken(previous.Text + formattedToken.Text, kind);
            }
            else
            {
                result.Add(new DisassemblyTextToken(formattedToken.Text, kind));
            }
        }

        return result;
    }

    private static DisassemblyTextTokenKind MapTextKind(
        FormatterTextKind kind,
        NeutralFlowControl flowControl)
    {
        return kind switch
        {
            FormatterTextKind.Mnemonic =>
                flowControl == NeutralFlowControl.None
                    ? DisassemblyTextTokenKind.Mnemonic
                    : DisassemblyTextTokenKind.FlowControlMnemonic,
            FormatterTextKind.Prefix => DisassemblyTextTokenKind.Mnemonic,
            FormatterTextKind.Register => DisassemblyTextTokenKind.Register,
            FormatterTextKind.Number or
            FormatterTextKind.SelectorValue or
            FormatterTextKind.LabelAddress or
            FormatterTextKind.FunctionAddress => DisassemblyTextTokenKind.Number,
            FormatterTextKind.Directive or
            FormatterTextKind.Keyword or
            FormatterTextKind.Decorator or
            FormatterTextKind.Data or
            FormatterTextKind.Label or
            FormatterTextKind.Function => DisassemblyTextTokenKind.Keyword,
            _ => DisassemblyTextTokenKind.Text
        };
    }

    private static (NeutralFlowControl FlowControl, ulong? BranchTarget) ResolveFlowControl(
        Instruction instruction)
    {
        return instruction.FlowControl switch
        {
            Iced.Intel.FlowControl.Call =>
                (NeutralFlowControl.Call, TryGetDirectBranchTarget(instruction)),
            Iced.Intel.FlowControl.IndirectCall =>
                (NeutralFlowControl.Call, null),
            Iced.Intel.FlowControl.UnconditionalBranch =>
                (NeutralFlowControl.Jump, TryGetDirectBranchTarget(instruction)),
            Iced.Intel.FlowControl.IndirectBranch =>
                (NeutralFlowControl.Jump, null),
            Iced.Intel.FlowControl.ConditionalBranch =>
                (NeutralFlowControl.ConditionalJump, TryGetDirectBranchTarget(instruction)),
            Iced.Intel.FlowControl.Return =>
                (NeutralFlowControl.Return, null),
            Iced.Intel.FlowControl.Interrupt =>
                (NeutralFlowControl.Interrupt, null),
            Iced.Intel.FlowControl.Next =>
                (NeutralFlowControl.None, null),
            _ =>
                (NeutralFlowControl.Other, null)
        };
    }

    private static ulong? TryGetDirectBranchTarget(Instruction instruction)
    {
        return instruction.Op0Kind is OpKind.NearBranch16 or OpKind.NearBranch32 or OpKind.NearBranch64
            ? instruction.NearBranchTarget
            : null;
    }

    private readonly record struct FormattedToken(string Text, FormatterTextKind Kind);

    private sealed class TokenizingFormatterOutput : FormatterOutput
    {
        private readonly List<FormattedToken> _tokens = new();

        public override void Write(string text, FormatterTextKind kind)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _tokens.Add(new FormattedToken(text, kind));
            }
        }

        public IReadOnlyList<FormattedToken> TakeTokens()
        {
            if (_tokens.Count == 0)
            {
                return Array.Empty<FormattedToken>();
            }

            List<FormattedToken> result = new(_tokens);
            _tokens.Clear();

            TrimEdgeWhitespace(result);
            return result;
        }

        private static void TrimEdgeWhitespace(List<FormattedToken> tokens)
        {
            while (tokens.Count > 0)
            {
                FormattedToken first = tokens[0];
                string trimmed = first.Text.TrimStart();
                if (trimmed.Length == 0)
                {
                    tokens.RemoveAt(0);
                    continue;
                }

                if (!string.Equals(trimmed, first.Text, StringComparison.Ordinal))
                {
                    tokens[0] = first with { Text = trimmed };
                }

                break;
            }

            while (tokens.Count > 0)
            {
                int lastIndex = tokens.Count - 1;
                FormattedToken last = tokens[lastIndex];
                string trimmed = last.Text.TrimEnd();
                if (trimmed.Length == 0)
                {
                    tokens.RemoveAt(lastIndex);
                    continue;
                }

                if (!string.Equals(trimmed, last.Text, StringComparison.Ordinal))
                {
                    tokens[lastIndex] = last with { Text = trimmed };
                }

                break;
            }
        }
    }
}
