using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.MemoryViewer;

public sealed class MemoryViewerReader
{
    public const int DefaultBytesPerRow = 16;
    public const int DefaultWindowByteCount = 512;
    public const int MaximumWindowByteCount = 65_536;

    public async Task<MemoryViewSnapshot> ReadWindowAsync(
        IMemoryReader reader,
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        ulong address,
        int requestedByteCount = DefaultWindowByteCount,
        int bytesPerRow = DefaultBytesPerRow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);

        if (requestedByteCount <= 0 || requestedByteCount > MaximumWindowByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedByteCount),
                $"Memory Viewer window size must be between 1 and {MaximumWindowByteCount:N0} bytes.");
        }

        if (bytesPerRow <= 0 || bytesPerRow > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesPerRow));
        }

        MemoryRegion? region = FindReadableRegion(memoryRegions, address);
        if (region is null)
        {
            throw new InvalidOperationException(
                $"Address 0x{address:X} is not inside a readable, non-guarded memory region.");
        }

        ulong requestedLength = Math.Min((ulong)requestedByteCount, region.Size);
        if (requestedLength == 0)
        {
            throw new InvalidOperationException("The containing memory region has no readable bytes.");
        }

        ulong regionEnd = region.EndAddressExclusive;
        ulong maximumStart = regionEnd - requestedLength;
        ulong halfWindow = requestedLength / 2;
        ulong desiredStart = address >= halfWindow ? address - halfWindow : 0;
        ulong startAddress = Math.Max(region.BaseAddress, Math.Min(desiredStart, maximumStart));

        if (startAddress > region.BaseAddress)
        {
            ulong alignment = checked((ulong)bytesPerRow);
            ulong alignedStart = startAddress - (startAddress % alignment);
            ulong alignedEnd = checked(alignedStart + requestedLength);
            if (alignedStart >= region.BaseAddress &&
                alignedEnd <= regionEnd &&
                address >= alignedStart &&
                address < alignedEnd)
            {
                startAddress = alignedStart;
            }
        }

        if (startAddress > maximumStart)
        {
            startAddress = maximumStart;
        }

        int readLength = checked((int)requestedLength);
        byte[] bytes = new byte[readLength];
        int bytesRead = await reader
            .ReadAsync(process, startAddress, bytes, cancellationToken)
            .ConfigureAwait(false);

        if (bytesRead < 0 || bytesRead > bytes.Length)
        {
            throw new InvalidOperationException(
                $"The memory reader returned an invalid byte count of {bytesRead} for a {bytes.Length}-byte Memory Viewer request.");
        }

        if (bytesRead == 0)
        {
            throw new InvalidOperationException("The memory reader returned no data for the requested Memory Viewer range.");
        }

        ulong actualEndAddress = checked(startAddress + (ulong)bytesRead);
        if (address < startAddress || address >= actualEndAddress)
        {
            throw new InvalidOperationException(
                "The memory reader returned a partial range that does not include the requested Memory Viewer address.");
        }

        if (bytesRead != bytes.Length)
        {
            Array.Resize(ref bytes, bytesRead);
        }

        return new MemoryViewSnapshot(
            address,
            startAddress,
            bytes,
            region,
            bytesPerRow);
    }

    private static MemoryRegion? FindReadableRegion(
        IEnumerable<MemoryRegion> memoryRegions,
        ulong address)
    {
        return memoryRegions.FirstOrDefault(region =>
            MemoryViewerRegionNavigator.IsReadableRegion(region) &&
            address >= region.BaseAddress &&
            address < region.EndAddressExclusive);
    }
}
