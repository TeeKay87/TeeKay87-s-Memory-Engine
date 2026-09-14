using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TeeKay87.MemoryEngine.Core.Disassembly;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class DisassemblyInstructionViewModel
{
    public DisassemblyInstructionViewModel(
        DisassembledInstruction instruction,
        ulong requestedAddress,
        IEnumerable<DisassemblyMarker>? markers = null)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        SourceInstruction = instruction;
        AddressValue = instruction.Address;
        Address = $"0x{instruction.Address:X}";
        RawBytes = instruction.RawBytes;
        Bytes = FormatHex(instruction.RawBytes.Span);
        DisassemblyMarker[] markerSnapshot = (markers ?? Enumerable.Empty<DisassemblyMarker>()).ToArray();
        Markers = string.Join(
            " · ",
            markerSnapshot
                .Select(marker => marker.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.Ordinal));
        IsWatchpointHitRow = markerSnapshot.Any(marker =>
            string.Equals(marker.Text, "Watchpoint hit", StringComparison.Ordinal));
        IsWatchpointStopRow = markerSnapshot.Any(marker =>
            string.Equals(marker.Text, "Stop / Current IP", StringComparison.Ordinal) ||
            string.Equals(marker.Text, "Watchpoint stop (trigger unresolved)", StringComparison.Ordinal));
        Mnemonic = instruction.Mnemonic;
        Operands = instruction.Operands;
        Instruction = string.IsNullOrWhiteSpace(instruction.Operands)
            ? instruction.Mnemonic
            : $"{instruction.Mnemonic} {instruction.Operands}";
        Length = instruction.Length;
        FlowControl = instruction.FlowControl;
        BranchTarget = instruction.BranchTarget;
        BranchTargetText = instruction.BranchTarget.HasValue
            ? $"0x{instruction.BranchTarget.Value:X}"
            : string.Empty;
        IsValid = instruction.IsValid;
        SyntaxTokens = instruction.SyntaxTokens;
        IsOriginRow = instruction.Address <= requestedAddress &&
                      requestedAddress - instruction.Address < (ulong)instruction.Length;
    }

    public DisassembledInstruction SourceInstruction { get; }

    public ulong AddressValue { get; }

    public string Address { get; }

    public ReadOnlyMemory<byte> RawBytes { get; }

    public string Bytes { get; }

    public string Markers { get; }

    public bool IsWatchpointHitRow { get; }

    public bool IsWatchpointStopRow { get; }

    public string Mnemonic { get; }

    public string Operands { get; }

    public string Instruction { get; }

    public int Length { get; }

    public DisassemblyFlowControl FlowControl { get; }

    public ulong? BranchTarget { get; }

    public string BranchTargetText { get; }

    public bool IsValid { get; }

    public IReadOnlyList<DisassemblyTextToken> SyntaxTokens { get; }

    public bool IsOriginRow { get; }

    public bool CanFollowTarget =>
        IsValid &&
        BranchTarget.HasValue &&
        FlowControl is DisassemblyFlowControl.Call
            or DisassemblyFlowControl.Jump
            or DisassemblyFlowControl.ConditionalJump;

    private static string FormatHex(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        StringBuilder builder = new(checked(bytes.Length * 3 - 1));
        for (int index = 0; index < bytes.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
