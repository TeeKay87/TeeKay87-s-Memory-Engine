using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.MemoryViewer;

public sealed class MemoryViewerWriter
{
    public const int MaximumEditByteCount = 256;

    public async Task<MemoryViewWriteResult> WriteAndVerifyAsync(
        IMemoryReader reader,
        IMemoryWriter writer,
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        ulong address,
        ReadOnlyMemory<byte> expectedBytes,
        ReadOnlyMemory<byte> replacementBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);

        if (expectedBytes.IsEmpty)
        {
            throw new ArgumentException("Memory Viewer edits must contain at least one byte.", nameof(expectedBytes));
        }

        if (expectedBytes.Length > MaximumEditByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedBytes),
                $"Memory Viewer edits are limited to {MaximumEditByteCount:N0} bytes per operation.");
        }

        if (replacementBytes.Length != expectedBytes.Length)
        {
            throw new ArgumentException(
                "The replacement byte count must match the byte count that was originally displayed.",
                nameof(replacementBytes));
        }

        MemoryRegion? region = FindReadableWritableRegion(memoryRegions, address, replacementBytes.Length);
        if (region is null)
        {
            throw new InvalidOperationException(
                "The edited range is not fully contained in a readable, writable, non-guarded memory region.");
        }

        byte[] expected = expectedBytes.ToArray();
        byte[] replacement = replacementBytes.ToArray();
        if (expected.AsSpan().SequenceEqual(replacement))
        {
            return new MemoryViewWriteResult(
                MemoryViewWriteOutcome.NoChanges,
                address,
                expected,
                replacement,
                expected);
        }

        byte[] current = await ReadExactAsync(
                reader,
                process,
                address,
                replacement.Length,
                "pre-write validation",
                cancellationToken)
            .ConfigureAwait(false);

        if (!current.AsSpan().SequenceEqual(expected))
        {
            return new MemoryViewWriteResult(
                MemoryViewWriteOutcome.SourceChanged,
                address,
                expected,
                replacement,
                current);
        }

        await writer
            .WriteAsync(process, address, replacement, cancellationToken)
            .ConfigureAwait(false);

        byte[] readBack;
        try
        {
            readBack = await ReadExactAsync(
                    reader,
                    process,
                    address,
                    replacement.Length,
                    "read-back verification",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "The memory write was sent, but read-back verification could not be completed. Refresh the Memory Viewer before making another edit.",
                exception);
        }

        MemoryViewWriteOutcome outcome = readBack.AsSpan().SequenceEqual(replacement)
            ? MemoryViewWriteOutcome.Verified
            : MemoryViewWriteOutcome.VerificationFailed;

        return new MemoryViewWriteResult(
            outcome,
            address,
            expected,
            replacement,
            readBack);
    }

    private static async Task<byte[]> ReadExactAsync(
        IMemoryReader reader,
        TargetProcess process,
        ulong address,
        int byteCount,
        string operationName,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[byteCount];
        int bytesRead = await reader
            .ReadAsync(process, address, buffer, cancellationToken)
            .ConfigureAwait(false);

        if (bytesRead != byteCount)
        {
            throw new InvalidOperationException(
                $"Memory Viewer {operationName} returned {bytesRead:N0} of {byteCount:N0} requested bytes.");
        }

        return buffer;
    }

    private static MemoryRegion? FindReadableWritableRegion(
        IEnumerable<MemoryRegion> memoryRegions,
        ulong address,
        int byteCount)
    {
        ulong endAddress;
        try
        {
            endAddress = checked(address + (ulong)byteCount);
        }
        catch (OverflowException)
        {
            return null;
        }

        return memoryRegions.FirstOrDefault(region =>
            region.Size > 0 &&
            region.Protection.HasFlag(MemoryProtection.Read) &&
            region.Protection.HasFlag(MemoryProtection.Write) &&
            !region.Protection.HasFlag(MemoryProtection.Guard) &&
            address >= region.BaseAddress &&
            endAddress <= region.EndAddressExclusive);
    }
}
