using System;

namespace TeeKay87.MemoryEngine.Core.MemoryViewer;

public sealed record MemoryViewWriteResult(
    MemoryViewWriteOutcome Outcome,
    ulong Address,
    ReadOnlyMemory<byte> ExpectedBytes,
    ReadOnlyMemory<byte> ReplacementBytes,
    ReadOnlyMemory<byte> ObservedBytes)
{
    public int ByteCount => ReplacementBytes.Length;

    public bool WriteWasIssued =>
        Outcome is MemoryViewWriteOutcome.Verified or MemoryViewWriteOutcome.VerificationFailed;
}
