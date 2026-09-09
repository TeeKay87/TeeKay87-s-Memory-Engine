using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public sealed partial class MemoryScanner
{
    public const int DefaultChunkSize = 256 * 1024;
    public const int MaximumResultCount = 2_000_000;
    public const int MaximumReadFailureCount = 32;
    private const int ProgressReportChunkInterval = 16;

    public MemoryScanShape GetScanShape(
        IMemoryValueType valueType,
        IReadOnlyList<MemoryScanValue> inputValues,
        TargetArchitecture architecture,
        MemoryScanOptions scanOptions)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(scanOptions);

        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);
        return new MemoryScanShape(valueSize, alignment);
    }

    public Task<MemoryScanExecutionResult> FirstScanNativeAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        INativeValueScanner nativeScanner,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        CancellationToken cancellationToken)
    {
        return FirstScanNativeAsync(
            process,
            memoryRegions,
            nativeScanner,
            architecture,
            valueType,
            scanType,
            inputValues,
            MemoryScanOptions.Empty,
            cancellationToken);
    }

    public async Task<MemoryScanExecutionResult> FirstScanNativeAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        INativeValueScanner nativeScanner,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(nativeScanner);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);

        ValidateScanDefinition(valueType, scanType, inputValues, architecture, MemoryScanStage.FirstScan);
        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, valueSize))
            .OrderBy(region => region.BaseAddress)
            .ToArray();

        NativeValueScanRequest request = CreateNativeRequest(
            valueType,
            scanType,
            inputValues,
            valueSize,
            alignment,
            scanOptions);
        IReadOnlyList<NativeValueScanResult> nativeResults = await nativeScanner
            .ScanAsync(process, readableRegions, request, cancellationToken)
            .ConfigureAwait(false);

        List<MemoryScanResult> matches = new(Math.Min(nativeResults.Count, MaximumResultCount));
        HashSet<ulong> seen = new();

        foreach (NativeValueScanResult nativeResult in nativeResults)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong address = nativeResult.Address;
            if (!IsAligned(address, alignment) || !seen.Add(address))
            {
                continue;
            }

            if (nativeResult.CurrentValueData.Length != valueSize)
            {
                throw new InvalidOperationException(
                    $"Native scanner '{scanType.DisplayName}' returned {nativeResult.CurrentValueData.Length} value bytes for '{valueType.DisplayName}', which requires {valueSize} bytes.");
            }

            MemoryRegion? region = FindContainingRegion(readableRegions, address, valueSize);
            if (region is null)
            {
                continue;
            }

            MemoryScanValue currentValue = valueType.CreateValue(
                nativeResult.CurrentValueData.Span,
                alignment,
                architecture);

            AddResultOrThrow(
                matches,
                new MemoryScanResult(
                    address,
                    currentValue,
                    null,
                    region.Name,
                    region.ModuleName,
                    region.Protection));
        }

        return new MemoryScanExecutionResult(matches, 0);
    }

    public Task<MemoryScanExecutionResult> NextScanNativeAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyList<MemoryScanResult> previousResults,
        INativeValueScanRefiner nativeRefiner,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        CancellationToken cancellationToken)
    {
        return NextScanNativeAsync(
            process,
            memoryRegions,
            previousResults,
            nativeRefiner,
            architecture,
            valueType,
            scanType,
            inputValues,
            MemoryScanOptions.Empty,
            cancellationToken);
    }

    public async Task<MemoryScanExecutionResult> NextScanNativeAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyList<MemoryScanResult> previousResults,
        INativeValueScanRefiner nativeRefiner,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(nativeRefiner);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);

        ValidateScanDefinition(valueType, scanType, inputValues, architecture, MemoryScanStage.NextScan);
        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);

        if (previousResults.Count == 0)
        {
            return new MemoryScanExecutionResult(Array.Empty<MemoryScanResult>(), 0);
        }

        ValidateRefinementValueType(previousResults, valueType.Id, valueSize, alignment);

        MemoryScanResult[] candidates = previousResults
            .OrderBy(result => result.Address)
            .ToArray();
        ulong[] previousAddresses = candidates
            .Select(result => result.Address)
            .ToArray();

        IReadOnlyList<NativeValueScanResult> nativeResults = await nativeRefiner
            .RefineAsync(
                process,
                previousAddresses,
                CreateNativeRequest(valueType, scanType, inputValues, valueSize, alignment, scanOptions),
                cancellationToken)
            .ConfigureAwait(false);

        Dictionary<ulong, MemoryScanResult> previousByAddress = candidates
            .GroupBy(result => result.Address)
            .ToDictionary(group => group.Key, group => group.First());
        List<MemoryScanResult> matches = new(Math.Min(nativeResults.Count, previousByAddress.Count));
        HashSet<ulong> seen = new();

        foreach (NativeValueScanResult nativeResult in nativeResults)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong address = nativeResult.Address;
            if (!IsAligned(address, alignment) ||
                !seen.Add(address) ||
                !previousByAddress.TryGetValue(address, out MemoryScanResult? previous) ||
                previous is null)
            {
                continue;
            }

            if (nativeResult.CurrentValueData.Length != valueSize)
            {
                throw new InvalidOperationException(
                    $"Native scanner '{scanType.DisplayName}' returned {nativeResult.CurrentValueData.Length} value bytes for '{valueType.DisplayName}', which requires {valueSize} bytes.");
            }

            MemoryScanValue currentValue = valueType.CreateValue(
                nativeResult.CurrentValueData.Span,
                alignment,
                architecture);

            AddResultOrThrow(
                matches,
                new MemoryScanResult(
                    address,
                    currentValue,
                    previous.CurrentValue,
                    previous.RegionName,
                    previous.ModuleName,
                    previous.Protection));
        }

        return new MemoryScanExecutionResult(matches, 0);
    }

    public Task<MemoryScanExecutionResult> FirstScanAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IMemoryReader memoryReader,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        return FirstScanAsync(
            process,
            memoryRegions,
            memoryReader,
            architecture,
            valueType,
            scanType,
            inputValues,
            MemoryScanOptions.Empty,
            progress,
            cancellationToken);
    }

    public async Task<MemoryScanExecutionResult> FirstScanAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IMemoryReader memoryReader,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(memoryReader);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);

        ValidateScanDefinition(valueType, scanType, inputValues, architecture, MemoryScanStage.FirstScan);
        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);

        if (valueSize > DefaultChunkSize)
        {
            throw new NotSupportedException(
                $"Shared scans currently support values up to {DefaultChunkSize:N0} bytes per candidate.");
        }

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, valueSize))
            .OrderBy(region => region.BaseAddress)
            .ToArray();

        ulong totalCandidates = readableRegions.Aggregate(
            0UL,
            (total, region) => SaturatingAdd(
                total,
                GetCandidateCount(region, valueSize, alignment)));

        List<MemoryScanResult> matches = new();
        byte[] buffer = new byte[DefaultChunkSize];
        ulong processedCandidates = 0;
        int readFailures = 0;
        int chunksSinceProgressReport = 0;
        ulong maximumCandidatesPerChunk = GetMaximumCandidatesPerChunk(valueSize, alignment);

        foreach (MemoryRegion region in readableRegions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong scanStart = AlignUp(region.BaseAddress, alignment);
            ulong candidateCount = GetCandidateCount(region, valueSize, alignment);
            ulong candidateIndex = 0;

            while (candidateIndex < candidateCount)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ulong chunkCandidateCount = Math.Min(
                    maximumCandidatesPerChunk,
                    candidateCount - candidateIndex);
                ulong chunkAddress = checked(
                    scanStart + candidateIndex * checked((ulong)alignment));
                ulong readLengthValue = checked(
                    (chunkCandidateCount - 1) * checked((ulong)alignment) +
                    checked((ulong)valueSize));
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
                        int offset = checked((int)(localIndex * checked((ulong)alignment)));
                        ReadOnlySpan<byte> candidateBytes = data.Slice(offset, valueSize);

                        if (!scanType.IsMatch(
                                valueType,
                                candidateBytes,
                                null,
                                inputValues,
                                architecture,
                                MemoryScanStage.FirstScan,
                                scanOptions))
                        {
                            continue;
                        }

                        MemoryScanValue currentValue = valueType.CreateValue(
                            candidateBytes,
                            alignment,
                            architecture);

                        AddResultOrThrow(
                            matches,
                            new MemoryScanResult(
                                checked(chunkAddress + (ulong)offset),
                                currentValue,
                                null,
                                region.Name,
                                region.ModuleName,
                                region.Protection));
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

    public Task<MemoryScanExecutionResult> NextScanAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyList<MemoryScanResult> previousResults,
        IMemoryReader memoryReader,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        return NextScanAsync(
            process,
            memoryRegions,
            previousResults,
            memoryReader,
            architecture,
            valueType,
            scanType,
            inputValues,
            MemoryScanOptions.Empty,
            progress,
            cancellationToken);
    }

    public async Task<MemoryScanExecutionResult> NextScanAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IReadOnlyList<MemoryScanResult> previousResults,
        IMemoryReader memoryReader,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(memoryReader);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);

        ValidateScanDefinition(valueType, scanType, inputValues, architecture, MemoryScanStage.NextScan);
        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);

        if (previousResults.Count == 0)
        {
            progress?.Report(new MemoryScanProgress(0, 0, 0));
            return new MemoryScanExecutionResult(Array.Empty<MemoryScanResult>(), 0);
        }

        ValidateRefinementValueType(previousResults, valueType.Id, valueSize, alignment);

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, valueSize))
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

                if (!CanContainValue(region, firstCandidate.Address, valueSize))
                {
                    if (firstCandidate.Address >= regionEnd)
                    {
                        break;
                    }

                    candidateIndex++;
                    processedCandidates++;
                    continue;
                }

                if (!IsAligned(firstCandidate.Address, alignment))
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
                    if (!CanContainValue(region, candidate.Address, valueSize) ||
                        !IsAligned(candidate.Address, alignment))
                    {
                        break;
                    }

                    ulong candidateEnd = checked(candidate.Address + checked((ulong)valueSize));
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
                    checked(lastCandidateAddress - chunkAddress) + checked((ulong)valueSize)));
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
                        ReadOnlySpan<byte> currentBytes = data.Slice(relativeOffset, valueSize);

                        if (!scanType.IsMatch(
                                valueType,
                                currentBytes,
                                previous.CurrentValue,
                                inputValues,
                                architecture,
                                MemoryScanStage.NextScan,
                                scanOptions))
                        {
                            continue;
                        }

                        MemoryScanValue currentValue = valueType.CreateValue(
                            currentBytes,
                            alignment,
                            architecture);

                        AddResultOrThrow(
                            matches,
                            new MemoryScanResult(
                                previous.Address,
                                currentValue,
                                previous.CurrentValue,
                                region.Name,
                                region.ModuleName,
                                region.Protection));
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

    private static void ValidateScanDefinition(
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        TargetArchitecture architecture,
        MemoryScanStage stage)
    {
        bool stageSupported = stage == MemoryScanStage.FirstScan
            ? scanType.AvailableForFirstScan
            : scanType.AvailableForNextScan;

        if (!stageSupported)
        {
            throw new NotSupportedException(
                $"Scan Type '{scanType.DisplayName}' is not available for {(stage == MemoryScanStage.FirstScan ? "First Scan" : "Next Scan")}.");
        }

        if (!scanType.SupportsValueType(valueType))
        {
            throw new NotSupportedException(
                $"Scan Type '{scanType.DisplayName}' does not support Value Type '{valueType.DisplayName}'.");
        }

        if (scanType.InputValueCount is int expectedInputCount && inputValues.Count != expectedInputCount)
        {
            throw new InvalidOperationException(
                $"Scan Type '{scanType.DisplayName}' requires {expectedInputCount} input value(s), but {inputValues.Count} were supplied.");
        }

        if (inputValues.Any(value =>
                !string.Equals(value.ValueTypeId, valueType.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"All scan inputs must use the selected Value Type '{valueType.DisplayName}'.");
        }

        if (!scanType.TryValidateInputValues(
                valueType,
                inputValues,
                architecture,
                stage,
                out string validationError))
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(validationError)
                    ? $"Scan Type '{scanType.DisplayName}' rejected the supplied input values."
                    : validationError);
        }
    }

    private static void ResolveScanShape(
        IMemoryValueType valueType,
        IReadOnlyList<MemoryScanValue> inputValues,
        TargetArchitecture architecture,
        MemoryScanOptions scanOptions,
        out int valueSize,
        out int alignment)
    {
        if (!valueType.TryResolveScanShape(
                inputValues,
                architecture,
                out valueSize,
                out alignment,
                out string error))
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? $"Value Type '{valueType.DisplayName}' could not resolve its scan width and alignment."
                    : error);
        }

        if (valueSize <= 0)
        {
            throw new InvalidOperationException(
                $"Value Type '{valueType.DisplayName}' returned an invalid scan width of {valueSize}.");
        }

        if (alignment <= 0)
        {
            throw new InvalidOperationException(
                $"Value Type '{valueType.DisplayName}' returned an invalid scan alignment of {alignment}.");
        }

        if (scanOptions.TryGetValue(
                TeeKay87.MemoryEngine.PluginSdk.Scanning.StandardMemoryScanOptionIds.Alignment,
                out string alignmentChoice) &&
            !string.Equals(
                alignmentChoice,
                TeeKay87.MemoryEngine.PluginSdk.Scanning.StandardMemoryScanOptionChoiceIds.DefaultAlignment,
                StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(alignmentChoice, out int selectedAlignment) || selectedAlignment is < 1 or > byte.MaxValue)
            {
                throw new InvalidOperationException(
                    $"The selected scan alignment '{alignmentChoice}' is not valid. Alignment must be between 1 and {byte.MaxValue} bytes.");
            }

            alignment = selectedAlignment;
        }
    }

    private static NativeValueScanRequest CreateNativeRequest(
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        int valueSize,
        int alignment,
        MemoryScanOptions scanOptions)
    {
        return new NativeValueScanRequest(
            valueType.Id,
            scanType.Id,
            valueSize,
            alignment,
            inputValues.Select(value => value.Bytes),
            scanOptions);
    }

    private static void ValidateRefinementValueType(
        IReadOnlyList<MemoryScanResult> previousResults,
        string valueTypeId,
        int valueSize,
        int alignment)
    {
        string expectedTypeId = previousResults[0].ValueTypeId;
        int expectedSize = previousResults[0].CurrentValue.Size;
        int expectedAlignment = previousResults[0].CurrentValue.Alignment;

        if (!string.Equals(valueTypeId, expectedTypeId, StringComparison.OrdinalIgnoreCase) ||
            valueSize != expectedSize ||
            alignment != expectedAlignment ||
            previousResults.Any(result =>
                !string.Equals(result.ValueTypeId, expectedTypeId, StringComparison.OrdinalIgnoreCase) ||
                result.CurrentValue.Size != expectedSize ||
                result.CurrentValue.Alignment != expectedAlignment))
        {
            throw new InvalidOperationException(
                "Next Scan must use the same Value Type, value width, and alignment as the current scan session. Choose New Scan before changing Value Type.");
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
