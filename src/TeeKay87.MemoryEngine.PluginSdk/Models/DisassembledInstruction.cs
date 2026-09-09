using System;
using System.Collections.Generic;
using System.Linq;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class DisassembledInstruction
{
    private readonly byte[] _rawBytes;
    private readonly IReadOnlyList<DisassemblyTextToken> _syntaxTokens;

    public DisassembledInstruction(
        ulong address,
        ReadOnlySpan<byte> rawBytes,
        string mnemonic,
        string operands,
        DisassemblyFlowControl flowControl = DisassemblyFlowControl.None,
        ulong? branchTarget = null,
        bool isValid = true)
        : this(
            address,
            rawBytes,
            mnemonic,
            operands,
            flowControl,
            branchTarget,
            isValid,
            syntaxTokens: null)
    {
    }

    public DisassembledInstruction(
        ulong address,
        ReadOnlySpan<byte> rawBytes,
        string mnemonic,
        string operands,
        DisassemblyFlowControl flowControl,
        ulong? branchTarget,
        bool isValid,
        IEnumerable<DisassemblyTextToken>? syntaxTokens)
    {
        if (rawBytes.IsEmpty)
        {
            throw new ArgumentException("A disassembled instruction must contain at least one raw byte.", nameof(rawBytes));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(mnemonic);
        ArgumentNullException.ThrowIfNull(operands);

        if (branchTarget.HasValue &&
            flowControl != DisassemblyFlowControl.Call &&
            flowControl != DisassemblyFlowControl.Jump &&
            flowControl != DisassemblyFlowControl.ConditionalJump)
        {
            throw new ArgumentException(
                "A direct branch target is valid only for call, jump, or conditional-jump instructions.",
                nameof(branchTarget));
        }

        if (!isValid && branchTarget.HasValue)
        {
            throw new ArgumentException(
                "An invalid instruction cannot expose a direct branch target.",
                nameof(branchTarget));
        }

        DisassemblyTextToken[] copiedTokens = syntaxTokens?.ToArray() ?? Array.Empty<DisassemblyTextToken>();
        if (copiedTokens.Any(token => token is null))
        {
            throw new ArgumentException(
                "Disassembly syntax tokens cannot contain null entries.",
                nameof(syntaxTokens));
        }

        Address = address;
        _rawBytes = rawBytes.ToArray();
        Mnemonic = mnemonic;
        Operands = operands;
        FlowControl = flowControl;
        BranchTarget = branchTarget;
        IsValid = isValid;
        _syntaxTokens = Array.AsReadOnly(copiedTokens);
    }

    public ulong Address { get; }

    public ReadOnlyMemory<byte> RawBytes => _rawBytes;

    public int Length => _rawBytes.Length;

    public string Mnemonic { get; }

    public string Operands { get; }

    public DisassemblyFlowControl FlowControl { get; }

    public ulong? BranchTarget { get; }

    public bool IsValid { get; }

    public IReadOnlyList<DisassemblyTextToken> SyntaxTokens => _syntaxTokens;
}
