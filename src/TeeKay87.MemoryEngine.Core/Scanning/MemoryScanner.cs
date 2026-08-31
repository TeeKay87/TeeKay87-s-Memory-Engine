using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public sealed class MemoryScanner
{
    public const int ValueSize = sizeof(int);
    public const int DefaultChunkSize = 256 * 1024;
    public const int MaximumResultCount = 2_000_000;
    public const int MaximumReadFailureCount = 32;
    private const int ProgressReportChunkInterval = 16;

    public Task<MemoryScanExecutionResult> FirstScanInt32ExactNativeAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        INativeValueScanner nativeScanner,
        TargetArchitecture architecture,
        int targetValue,
        CancellationToken cancellationToken)
    {
        return FirstScanExactNativeAsync(
            process,
            memoryRegions,
            nativeScanner,
            CreateInt32Value(architecture, targetValue),
            cancellationToken);
    }

    public Task<MemoryScanExecutionResult> NextScanInt32ExactNativeAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyList<MemoryScanResult> previousResults,
        INativeValueScanRefiner nativeRefiner,
        TargetArchitecture architecture,
        int targetValue,
        CancellationToken cancellationToken)
    {
        return NextScanExactNativeAsync(
            process,
            memoryRegions,
            previousResults,
            nativeRefiner,
            CreateInt32Value(architecture, targetValue),
            cancellationToken);
    }

    public Task<MemoryScanExecutionResult> FirstScanInt32ExactAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IMemoryReader memoryReader,
        TargetArchitecture architecture,
        int targetValue,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        return FirstScanExactAsync(
            process,
            memoryRegions,
            memoryReader,
            architecture,
            CreateInt32Value(architecture, targetValue),
            progress,
            cancellationToken);
    }

    public Task<MemoryScanExecutionResult> NextScanInt32ExactAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyList<MemoryScanResult> previousResults,
        IMemoryReader memoryReader,
        TargetArchitecture architecture,
        int targetValue,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        return NextScanExactAsync(
            process,
            memoryRegions,
            previousResults,
            memoryReader,
            architecture,
            CreateInt32Value(architecture, targetValue),
            progress,
            cancellationToken);
    }

    public async Task<MemoryScanExecutionResult> FirstScanExactNativeAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        INativeValueScanner nativeScanner,
        MemoryScanValue targetValue,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(nativeScanner);
        ArgumentNullException.ThrowIfNull(targetValue);

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, targetValue.Size))
            .OrderBy(region => region.BaseAddress)
            .ToArray();

        NativeValueScanRequest request = CreateNativeRequest(targetValue);
        IReadOnlyList<ulong> addresses = await nativeScanner
            .ScanAsync(process, readableRegions, request, cancellationToken)
            .ConfigureAwait(false);

        List<MemoryScanResult> matches = new(Math.Min(addresses.Count, MaximumResultCount));
        HashSet<ulong> seen = new();

        foreach (ulong address in addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsAligned(address, targetValue.Alignment) || !seen.Add(address))
            {
                continue;
            }

            MemoryRegion? region = FindContainingRegion(readableRegions, address, targetValue.Size);
            if (region is null)
            {
                continue;
            }

            AddResultOrThrow(
                matches,
                new MemoryScanResult(
                    address,
                    targetValue,
                    null,
                    region.Name,
                    region.ModuleName));
        }

        return new MemoryScanExecutionResult(matches, 0);
    }

    public async Task<MemoryScanExecutionResult> NextScanExactNativeAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyList<MemoryScanResult> previousResults,
        INativeValueScanRefiner nativeRefiner,
        MemoryScanValue targetValue,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(nativeRefiner);
        ArgumentNullException.ThrowIfNull(targetValue);

        if (previousResults.Count == 0)
        {
            return new MemoryScanExecutionResult(Array.Empty<MemoryScanResult>(), 0);
        }

        ValidateRefinementValueType(previousResults, targetValue);

        MemoryScanResult[] candidates = previousResults
            .OrderBy(result => result.Address)
            .ToArray();
        ulong[] previousAddresses = candidates
            .Select(result => result.Address)
            .ToArray();

        IReadOnlyList<ulong> addresses = await nativeRefiner
            .RefineAsync(
                process,
                previousAddresses,
                CreateNativeRequest(targetValue),
                cancellationToken)
            .ConfigureAwait(false);

        Dictionary<ulong, MemoryScanResult> previousByAddress = candidates
            .GroupBy(result => result.Address)
            .ToDictionary(group => group.Key, group => group.First());
        List<MemoryScanResult> matches = new(Math.Min(addresses.Count, previousByAddress.Count));
        HashSet<ulong> seen = new();

        foreach (ulong address in addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsAligned(address, targetValue.Alignment) ||
                !seen.Add(address) ||
                !previousByAddress.TryGetValue(address, out MemoryScanResult? previous) ||
                previous is null)
            {
                continue;
            }

            AddResultOrThrow(
                matches,
                new MemoryScanResult(
                    address,
                    targetValue,
                    previous.CurrentValue,
                    previous.RegionName,
                    previous.ModuleName));
        }

        return new MemoryScanExecutionResult(matches, 0);
    }

    public async Task<MemoryScanExecutionResult> FirstScanExactAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IMemoryReader memoryReader,
        TargetArchitecture architecture,
        MemoryScanValue targetValue,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(memoryReader);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(targetValue);

        if (targetValue.Size > DefaultChunkSize)
        {
            throw new NotSupportedException(
                $"Exact Value scans currently support values up to {DefaultChunkSize:N0} bytes in the shared scanner.");
        }

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, targetValue.Size))
            .OrderBy(region => region.BaseAddress)
            .ToArray();

        ulong totalCandidates = readableRegions.Aggregate(
            0UL,
            (total, region) => SaturatingAdd(
                total,
                GetCandidateCount(region, targetValue.Size, targetValue.Alignment)));

        List<MemoryScanResult> matches = new();
        byte[] buffer = new byte[DefaultChunkSize];
        ulong processedCandidates = 0;
        int readFailures = 0;
        int chunksSinceProgressReport = 0;
        ulong maximumCandidatesPerChunk = GetMaximumCandidatesPerChunk(targetValue.Size, targetValue.Alignment);

        foreach (MemoryRegion region in readableRegions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong scanStart = AlignUp(region.BaseAddress, targetValue.Alignment);
            ulong candidateCount = GetCandidateCount(region, targetValue.Size, targetValue.Alignment);
            ulong candidateIndex = 0;

            while (candidateIndex < candidateCount)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ulong chunkCandidateCount = Math.Min(
                    maximumCandidatesPerChunk,
                    candidateCount - candidateIndex);
                ulong chunkAddress = checked(
                    scanStart + candidateIndex * checked((ulong)targetValue.Alignment));
                ulong readLengthValue = checked(
                    (chunkCandidateCount - 1) * checked((ulong)targetValue.Alignment) +
                    checked((ulong)targetValue.Size));
                int readLength = checked((int)readLengthValue);
                bool readSucceeded = false;

                try
                {
                    int bytesRead = await memoryReader
                        .ReadAsync(
                            process,
                            chunkAddress,
                            buffer.AsMemory(0, readLength),
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (bytesRead != readLength)
                    {
                        throw new InvalidOperationException(
                            $"The memory reader returned {bytesRead} bytes for a {readLength}-byte scan request.");
                    }

                    readSucceeded = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    readFailures++;
                    ThrowIfTooManyReadFailures(readFailures);
                }

                if (readSucceeded)
                {
                    ReadOnlySpan<byte> data = buffer.AsSpan(0, readLength);
                    for (ulong localIndex = 0; localIndex < chunkCandidateCount; localIndex++)
                    {
                        int offset = checked((int)(localIndex * checked((ulong)targetValue.Alignment)));
                        if (!MemoryScanValueCodec.MatchesExact(
                                data.Slice(offset, targetValue.Size),
                                targetValue,
                                architecture.Endianness))
                        {
                            continue;
                        }

                        AddResultOrThrow(
                            matches,
                            new MemoryScanResult(
                                checked(chunkAddress + (ulong)offset),
                                targetValue,
                                null,
                                region.Name,
                                region.ModuleName));
                    }
                }

                candidateIndex += chunkCandidateCount;
                processedCandidates = SaturatingAdd(processedCandidates, chunkCandidateCount);
                chunksSinceProgressReport++;

                if (chunksSinceProgressReport >= ProgressReportChunkInterval)
                {
                    progress?.Report(new MemoryScanProgress(processedCandidates, totalCandidates, matches.Count));
                    chunksSinceProgressReport = 0;
                }
            }
        }

        progress?.Report(new MemoryScanProgress(totalCandidates, totalCandidates, matches.Count));
        return new MemoryScanExecutionResult(matches, readFailures);
    }

    public async Task<MemoryScanExecutionResult> NextScanExactAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyList<MemoryScanResult> previousResults,
        IMemoryReader memoryReader,
        TargetArchitecture architecture,
        MemoryScanValue targetValue,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(memoryReader);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(targetValue);

        if (previousResults.Count == 0)
        {
            progress?.Report(new MemoryScanProgress(0, 0, 0));
            return new MemoryScanExecutionResult(Array.Empty<MemoryScanResult>(), 0);
        }

        ValidateRefinementValueType(previousResults, targetValue);

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, targetValue.Size))
            .OrderBy(region => region.BaseAddress)
            .ToArray();
        MemoryScanResult[] candidates = previousResults
            .OrderBy(result => result.Address)
            .ToArray();

        List<MemoryScanResult> matches = new();
        byte[] buffer = new byte[DefaultChunkSize];
        int candidateIndex = 0;
        int processedCandidates = 0;
        int readFailures = 0;
        int chunksSinceProgressReport = 0;

        foreach (MemoryRegion region in readableRegions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong regionEnd = region.EndAddressExclusive;
            while (candidateIndex < candidates.Length &&
                   candidates[candidateIndex].Address < region.BaseAddress)
            {
                candidateIndex++;
                processedCandidates++;
            }

            while (candidateIndex < candidates.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                MemoryScanResult firstCandidate = candidates[candidateIndex];
                if (firstCandidate.Address < region.BaseAddress)
                {
                    candidateIndex++;
                    processedCandidates++;
                    continue;
                }

                if (!CanContainValue(region, firstCandidate.Address, targetValue.Size))
                {
                    if (firstCandidate.Address >= regionEnd)
                    {
                        break;
                    }

                    candidateIndex++;
                    processedCandidates++;
                    continue;
                }

                if (!IsAligned(firstCandidate.Address, targetValue.Alignment))
                {
                    candidateIndex++;
                    processedCandidates++;
                    continue;
                }

                ulong chunkAddress = firstCandidate.Address;
                ulong maximumReadEnd = Math.Min(
                    regionEnd,
                    SaturatingAdd(chunkAddress, checked((ulong)DefaultChunkSize)));
                int chunkCandidateStart = candidateIndex;
                int chunkCandidateEnd = candidateIndex;

                while (chunkCandidateEnd < candidates.Length)
                {
                    MemoryScanResult candidate = candidates[chunkCandidateEnd];
                    if (!CanContainValue(region, candidate.Address, targetValue.Size) ||
                        !IsAligned(candidate.Address, targetValue.Alignment))
                    {
                        break;
                    }

                    ulong candidateEnd = checked(candidate.Address + checked((ulong)targetValue.Size));
                    if (candidateEnd > maximumReadEnd)
                    {
                        break;
                    }

                    chunkCandidateEnd++;
                }

                if (chunkCandidateEnd == chunkCandidateStart)
                {
                    candidateIndex++;
                    processedCandidates++;
                    continue;
                }

                ulong lastCandidateAddress = candidates[chunkCandidateEnd - 1].Address;
                int readLength = checked((int)(
                    checked(lastCandidateAddress - chunkAddress) + checked((ulong)targetValue.Size)));
                bool readSucceeded = false;

                try
                {
                    int bytesRead = await memoryReader
                        .ReadAsync(
                            process,
                            chunkAddress,
                            buffer.AsMemory(0, readLength),
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (bytesRead != readLength)
                    {
                        throw new InvalidOperationException(
                            $"The memory reader returned {bytesRead} bytes for a {readLength}-byte refinement request.");
                    }

                    readSucceeded = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    readFailures++;
                    ThrowIfTooManyReadFailures(readFailures);
                }

                if (readSucceeded)
                {
                    ReadOnlySpan<byte> data = buffer.AsSpan(0, readLength);
                    for (int index = chunkCandidateStart; index < chunkCandidateEnd; index++)
                    {
                        MemoryScanResult previous = candidates[index];
                        int relativeOffset = checked((int)(previous.Address - chunkAddress));

                        if (!MemoryScanValueCodec.MatchesExact(
                                data.Slice(relativeOffset, targetValue.Size),
                                targetValue,
                                architecture.Endianness))
                        {
                            continue;
                        }

                        AddResultOrThrow(
                            matches,
                            new MemoryScanResult(
                                previous.Address,
                                targetValue,
                                previous.CurrentValue,
                                region.Name,
                                region.ModuleName));
                    }
                }

                processedCandidates += chunkCandidateEnd - chunkCandidateStart;
                candidateIndex = chunkCandidateEnd;
                chunksSinceProgressReport++;

                if (chunksSinceProgressReport >= ProgressReportChunkInterval)
                {
                    progress?.Report(new MemoryScanProgress(
                        checked((ulong)processedCandidates),
                        checked((ulong)candidates.Length),
                        matches.Count));
                    chunksSinceProgressReport = 0;
                }
            }
        }

        processedCandidates += candidates.Length - candidateIndex;
        progress?.Report(new MemoryScanProgress(
            checked((ulong)processedCandidates),
            checked((ulong)candidates.Length),
            matches.Count));

        return new MemoryScanExecutionResult(matches, readFailures);
    }

    private static NativeValueScanRequest CreateNativeRequest(MemoryScanValue targetValue)
    {
        return new NativeValueScanRequest(
            targetValue.ValueType,
            ValueScanComparison.ExactValue,
            targetValue.Bytes.Span,
            targetValue.Alignment);
    }

    private static MemoryScanValue CreateInt32Value(TargetArchitecture architecture, int targetValue)
    {
        if (!MemoryScanValueCodec.TryParse(
                targetValue.ToString(CultureInfo.InvariantCulture),
                MemoryValueType.Int32,
                architecture,
                out MemoryScanValue? value,
                out string error) ||
            value is null)
        {
            throw new InvalidOperationException(error);
        }

        return value;
    }

    private static void ValidateRefinementValueType(
        IReadOnlyList<MemoryScanResult> previousResults,
        MemoryScanValue targetValue)
    {
        MemoryValueType expectedType = previousResults[0].ValueType;
        int expectedSize = previousResults[0].CurrentValue.Size;
        int expectedAlignment = previousResults[0].CurrentValue.Alignment;

        if (targetValue.ValueType != expectedType ||
            targetValue.Size != expectedSize ||
            targetValue.Alignment != expectedAlignment ||
            previousResults.Any(result =>
                result.ValueType != expectedType ||
                result.CurrentValue.Size != expectedSize ||
                result.CurrentValue.Alignment != expectedAlignment))
        {
            throw new InvalidOperationException(
                "Next Scan must use the same Value Type and value width as the current scan session. Choose New Scan before changing Value Type.");
        }
    }

    private static void AddResultOrThrow(List<MemoryScanResult> matches, MemoryScanResult result)
    {
        if (matches.Count >= MaximumResultCount)
        {
            throw new ScanResultLimitExceededException(MaximumResultCount);
        }

        matches.Add(result);
    }

    private static void ThrowIfTooManyReadFailures(int readFailures)
    {
        if (readFailures >= MaximumReadFailureCount)
        {
            throw new InvalidOperationException(
                $"The scan stopped after {readFailures} memory-read failures. Refresh the target memory map and verify that the connection is still healthy before retrying.");
        }
    }

    private static MemoryRegion? FindContainingRegion(
        IReadOnlyList<MemoryRegion> regions,
        ulong address,
        int length)
    {
        ulong endAddress;
        try
        {
            endAddress = checked(address + checked((ulong)length));
        }
        catch (OverflowException)
        {
            return null;
        }

        int low = 0;
        int high = regions.Count - 1;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            MemoryRegion region = regions[middle];

            if (address < region.BaseAddress)
            {
                high = middle - 1;
                continue;
            }

            if (address >= region.EndAddressExclusive)
            {
                low = middle + 1;
                continue;
            }

            return endAddress <= region.EndAddressExclusive ? region : null;
        }

        return null;
    }

    private static bool IsScannableRegion(MemoryRegion region, int valueSize)
    {
        return region.Size >= checked((ulong)valueSize) &&
               region.Protection.HasFlag(MemoryProtection.Read) &&
               !region.Protection.HasFlag(MemoryProtection.Guard);
    }

    private static bool CanContainValue(MemoryRegion region, ulong address, int valueSize)
    {
        if (address < region.BaseAddress || address >= region.EndAddressExclusive)
        {
            return false;
        }

        ulong remaining = region.EndAddressExclusive - address;
        return remaining >= checked((ulong)valueSize);
    }

    private static ulong GetCandidateCount(MemoryRegion region, int valueSize, int alignment)
    {
        if (!IsScannableRegion(region, valueSize))
        {
            return 0;
        }

        ulong start;
        try
        {
            start = AlignUp(region.BaseAddress, alignment);
        }
        catch (OverflowException)
        {
            return 0;
        }

        ulong end = region.EndAddressExclusive;
        ulong size = checked((ulong)valueSize);
        if (start >= end || end - start < size)
        {
            return 0;
        }

        return ((end - size - start) / checked((ulong)alignment)) + 1;
    }

    private static ulong GetMaximumCandidatesPerChunk(int valueSize, int alignment)
    {
        if (valueSize > DefaultChunkSize)
        {
            return 1;
        }

        return checked((ulong)((DefaultChunkSize - valueSize) / alignment + 1));
    }

    private static ulong AlignUp(ulong value, int alignment)
    {
        ulong alignmentValue = checked((ulong)alignment);
        ulong remainder = value % alignmentValue;
        if (remainder == 0)
        {
            return value;
        }

        return checked(value + (alignmentValue - remainder));
    }

    private static bool IsAligned(ulong address, int alignment)
    {
        return address % checked((ulong)alignment) == 0;
    }

    private static ulong SaturatingAdd(ulong left, ulong right)
    {
        return ulong.MaxValue - left < right
            ? ulong.MaxValue
            : left + right;
    }
}
