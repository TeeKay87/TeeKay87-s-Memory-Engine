using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.Core.Scanning.Storage;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public sealed partial class MemoryScanner
{
    private const int DefaultStoredResultBatchSize = 32 * 1024;

    public async Task<INativeValueScanResultStream> StartFirstScanNativeStreamAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        INativeValueScanStreamProvider nativeScanner,
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

        return await nativeScanner
            .StartScanAsync(
                process,
                readableRegions,
                CreateNativeRequest(valueType, scanType, inputValues, valueSize, alignment, scanOptions),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<INativeValueScanResultStream> StartNextScanNativeStreamAsync(
        TargetProcess process,
        IScanResultSet previousResults,
        INativeValueScanStreamRefiner nativeRefiner,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(nativeRefiner);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);

        ValidateScanDefinition(valueType, scanType, inputValues, architecture, MemoryScanStage.NextScan);
        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);
        ValidateStoredResultShape(previousResults, valueSize, alignment);

        return await nativeRefiner
            .StartRefineAsync(
                process,
                previousResults,
                CreateNativeRequest(valueType, scanType, inputValues, valueSize, alignment, scanOptions),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public bool CanRefineNativeResidentResultSet(
        INativeValueScanResidentResultSet previousResults,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions)
    {
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);

        ValidateScanDefinition(valueType, scanType, inputValues, architecture, MemoryScanStage.NextScan);
        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);
        ValidateResidentResultShape(previousResults, valueSize, alignment);

        if (!previousResults.IsAuthoritative)
        {
            return false;
        }

        NativeValueScanRequest request = CreateNativeRequest(
            valueType,
            scanType,
            inputValues,
            valueSize,
            alignment,
            scanOptions);
        return previousResults.CanRefine(request);
    }

    public async Task<INativeValueScanResultStream> StartNextScanNativeStreamAsync(
        TargetProcess process,
        INativeValueScanResidentResultSet previousResults,
        INativeValueScanStreamRefiner nativeRefiner,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(nativeRefiner);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);

        ValidateScanDefinition(valueType, scanType, inputValues, architecture, MemoryScanStage.NextScan);
        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);
        ValidateResidentResultShape(previousResults, valueSize, alignment);

        NativeValueScanRequest request = CreateNativeRequest(
            valueType,
            scanType,
            inputValues,
            valueSize,
            alignment,
            scanOptions);
        if (!previousResults.IsAuthoritative || !previousResults.CanRefine(request))
        {
            throw new NotSupportedException(
                "The resident native result set cannot refine this Scan Type without local materialization.");
        }

        return await nativeRefiner
            .StartRefineAsync(process, previousResults, request, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MemoryScanExecutionResult> ReadNativeResidentPreviewAsync(
        IReadOnlyList<MemoryRegion> memoryRegions,
        INativeValueScanResidentResultSet residentResults,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        int maximumPreviewResults,
        bool includePreviousValues,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(residentResults);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        if (maximumPreviewResults < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPreviewResults));
        }

        if (!residentResults.IsAuthoritative)
        {
            throw new InvalidOperationException(
                "A non-authoritative native result set cannot be used as the host's complete resident scan state.");
        }

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, residentResults.ValueSize))
            .OrderBy(region => region.BaseAddress)
            .ToArray();
        List<MemoryScanResult> preview = new(Math.Min(maximumPreviewResults, 4096));
        long requestedPreview = Math.Min(residentResults.Count, maximumPreviewResults);
        bool hasLastAddress = false;
        ulong lastAddress = 0;
        long received = 0;

        await foreach (NativeValueScanResultBatch batch in residentResults
                           .ReadResultBatchesAsync(
                               0,
                               requestedPreview,
                               DefaultStoredResultBatchSize,
                               includePreviousValues,
                               cancellationToken)
                           .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResidentBatchShape(batch, residentResults.ValueSize, includePreviousValues);

            for (int index = 0; index < batch.Count; index++)
            {
                ulong address = batch.Addresses.Span[index];
                ValidateResidentAddress(
                    address,
                    residentResults.Alignment,
                    residentResults.ValueSize,
                    readableRegions,
                    ref hasLastAddress,
                    ref lastAddress,
                    out MemoryRegion region);

                ReadOnlyMemory<byte> currentBytes = batch.GetCurrentValueData(index);
                MemoryScanValue currentValue = valueType.CreateValue(
                    currentBytes.Span,
                    residentResults.Alignment,
                    architecture);
                MemoryScanValue? previousValue = includePreviousValues && batch.HasPreviousValueData
                    ? valueType.CreateValue(
                        batch.GetPreviousValueData(index).Span,
                        residentResults.Alignment,
                        architecture)
                    : null;
                preview.Add(new MemoryScanResult(
                    address,
                    currentValue,
                    previousValue,
                    region.Name,
                    region.ModuleName,
                    region.Protection));
                received++;
            }
        }

        if (received != requestedPreview)
        {
            throw new InvalidDataException(
                $"Resident native preview returned {received:N0} of {requestedPreview:N0} requested result record(s).");
        }

        return new MemoryScanExecutionResult(preview, residentResults.Count, 0);
    }

    public async Task MaterializeNativeResidentResultSetAsync(
        IReadOnlyList<MemoryRegion> memoryRegions,
        INativeValueScanResidentResultSet residentResults,
        IScanResultWriter writer,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(residentResults);
        ArgumentNullException.ThrowIfNull(writer);
        ValidateResidentResultShape(residentResults, writer.ValueSize, writer.Alignment);

        if (!residentResults.IsAuthoritative)
        {
            throw new InvalidOperationException(
                "A non-authoritative native result set cannot be materialized as the host's complete scan state.");
        }

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, writer.ValueSize))
            .OrderBy(region => region.BaseAddress)
            .ToArray();
        bool hasLastAddress = false;
        ulong lastAddress = 0;
        long received = 0;

        await foreach (NativeValueScanResultBatch batch in residentResults
                           .ReadResultBatchesAsync(
                               0,
                               residentResults.Count,
                               DefaultStoredResultBatchSize,
                               includePreviousValues: false,
                               cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResidentBatchShape(batch, writer.ValueSize, requirePreviousValues: false);

            for (int index = 0; index < batch.Count; index++)
            {
                ulong address = batch.Addresses.Span[index];
                ValidateResidentAddress(
                    address,
                    writer.Alignment,
                    writer.ValueSize,
                    readableRegions,
                    ref hasLastAddress,
                    ref lastAddress,
                    out _);
            }

            writer.WriteBatch(batch.Addresses.Span, batch.CurrentValueData.Span);
            received = checked(received + batch.Count);
            progress?.Report(new MemoryScanProgress(
                checked((ulong)received),
                checked((ulong)residentResults.Count),
                writer.Count));
        }

        if (received != residentResults.Count)
        {
            throw new InvalidDataException(
                $"Resident native result set ended after {received:N0} of {residentResults.Count:N0} advertised record(s).");
        }

        progress?.Report(new MemoryScanProgress(
            checked((ulong)residentResults.Count),
            checked((ulong)residentResults.Count),
            writer.Count));
    }

    public async Task<MemoryScanExecutionResult> ConsumeNativeScanStreamAsync(
        IReadOnlyList<MemoryRegion> memoryRegions,
        INativeValueScanResultStream nativeResults,
        IScanResultWriter writer,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        int maximumPreviewResults,
        IScanResultSet? previousResults,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(nativeResults);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        if (maximumPreviewResults < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPreviewResults));
        }

        if (previousResults is not null)
        {
            ValidateStoredResultShape(previousResults, writer.ValueSize, writer.Alignment);
        }

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, writer.ValueSize))
            .OrderBy(region => region.BaseAddress)
            .ToArray();

        List<MemoryScanResult> preview = new(Math.Min(maximumPreviewResults, 4096));
        await using StoredResultCursor? previousCursor = previousResults is null
            ? null
            : new StoredResultCursor(previousResults, DefaultStoredResultBatchSize, cancellationToken);

        bool hasLastAddress = false;
        ulong lastAddress = 0;
        ulong sourceProcessed = 0;

        await foreach (NativeValueScanResultBatch batch in nativeResults
                           .ReadBatchesAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (batch.ValueSize != writer.ValueSize)
            {
                throw new InvalidDataException(
                    $"Native result batches contain {batch.ValueSize} value byte(s), but the active scan requires {writer.ValueSize}.");
            }

            ReadOnlyMemory<ulong> addresses = batch.Addresses;
            ulong[] acceptedAddresses = new ulong[batch.Count];
            byte[] acceptedValues = new byte[checked(batch.Count * writer.ValueSize)];
            int acceptedCount = 0;

            for (int index = 0; index < batch.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ulong address = addresses.Span[index];

                if (hasLastAddress && address < lastAddress)
                {
                    throw new InvalidDataException(
                        "Native scan results must be returned in ascending address order for disk-backed scanning.");
                }

                if (hasLastAddress && address == lastAddress)
                {
                    continue;
                }

                hasLastAddress = true;
                lastAddress = address;

                if (!IsAligned(address, writer.Alignment))
                {
                    continue;
                }

                MemoryRegion? region = FindContainingRegion(readableRegions, address, writer.ValueSize);
                if (region is null)
                {
                    continue;
                }

                ReadOnlyMemory<byte> currentBytes = batch.GetCurrentValueData(index);
                ReadOnlyMemory<byte> previousValueData = ReadOnlyMemory<byte>.Empty;
                if (previousCursor is not null)
                {
                    if (!await previousCursor.MoveToAddressAsync(address).ConfigureAwait(false))
                    {
                        continue;
                    }

                    previousValueData = previousCursor.CurrentValueData;
                }
                else if (batch.HasPreviousValueData)
                {
                    previousValueData = batch.GetPreviousValueData(index);
                }

                acceptedAddresses[acceptedCount] = address;
                currentBytes.Span.CopyTo(
                    acceptedValues.AsSpan(acceptedCount * writer.ValueSize, writer.ValueSize));
                acceptedCount++;

                if (preview.Count < maximumPreviewResults)
                {
                    MemoryScanValue currentValue = valueType.CreateValue(
                        currentBytes.Span,
                        writer.Alignment,
                        architecture);
                    MemoryScanValue? previousValue = previousValueData.IsEmpty
                        ? null
                        : valueType.CreateValue(
                            previousValueData.Span,
                            writer.Alignment,
                            architecture);
                    preview.Add(new MemoryScanResult(
                        address,
                        currentValue,
                        previousValue,
                        region.Name,
                        region.ModuleName,
                        region.Protection));
                }
            }

            if (acceptedCount > 0)
            {
                writer.WriteBatch(
                    acceptedAddresses.AsSpan(0, acceptedCount),
                    acceptedValues.AsSpan(0, checked(acceptedCount * writer.ValueSize)));
            }

            if (batch.SourceRecordsProcessed < sourceProcessed ||
                batch.SourceRecordsProcessed > nativeResults.SourceResultCount)
            {
                throw new InvalidDataException(
                    "Native scan result progress must be monotonic and cannot exceed the advertised source-result count.");
            }

            sourceProcessed = batch.SourceRecordsProcessed;
            progress?.Report(new MemoryScanProgress(
                sourceProcessed,
                nativeResults.SourceResultCount,
                writer.Count));
        }

        if (sourceProcessed != nativeResults.SourceResultCount)
        {
            throw new InvalidDataException(
                $"Native scan result stream ended after {sourceProcessed:N0} of {nativeResults.SourceResultCount:N0} advertised source record(s).");
        }

        progress?.Report(new MemoryScanProgress(
            nativeResults.SourceResultCount,
            nativeResults.SourceResultCount,
            writer.Count));
        return new MemoryScanExecutionResult(preview, writer.Count, 0);
    }

    public async Task<MemoryScanExecutionResult> FirstScanToStorageAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IMemoryReader memoryReader,
        IScanResultWriter writer,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        int maximumPreviewResults,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(memoryReader);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);
        if (maximumPreviewResults < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPreviewResults));
        }

        ValidateScanDefinition(valueType, scanType, inputValues, architecture, MemoryScanStage.FirstScan);
        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);
        ValidateWriterShape(writer, valueSize, alignment);

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
            (total, region) => SaturatingAdd(total, GetCandidateCount(region, valueSize, alignment)));

        List<MemoryScanResult> preview = new(Math.Min(maximumPreviewResults, 4096));
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
                ulong chunkCandidateCount = Math.Min(maximumCandidatesPerChunk, candidateCount - candidateIndex);
                ulong chunkAddress = checked(scanStart + candidateIndex * checked((ulong)alignment));
                ulong readLengthValue = checked(
                    (chunkCandidateCount - 1) * checked((ulong)alignment) + checked((ulong)valueSize));
                int readLength = checked((int)readLengthValue);
                bool readSucceeded = false;

                try
                {
                    int bytesRead = await memoryReader
                        .ReadAsync(process, chunkAddress, buffer.AsMemory(0, readLength), cancellationToken)
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

                        ulong address = checked(chunkAddress + (ulong)offset);
                        writer.Write(address, candidateBytes);
                        if (preview.Count < maximumPreviewResults)
                        {
                            MemoryScanValue currentValue = valueType.CreateValue(candidateBytes, alignment, architecture);
                            preview.Add(new MemoryScanResult(
                                address,
                                currentValue,
                                null,
                                region.Name,
                                region.ModuleName,
                                region.Protection));
                        }
                    }
                }

                candidateIndex += chunkCandidateCount;
                processedCandidates = SaturatingAdd(processedCandidates, chunkCandidateCount);
                chunksSinceProgressReport++;
                if (chunksSinceProgressReport >= ProgressReportChunkInterval)
                {
                    progress?.Report(new MemoryScanProgress(processedCandidates, totalCandidates, writer.Count));
                    chunksSinceProgressReport = 0;
                }
            }
        }

        progress?.Report(new MemoryScanProgress(totalCandidates, totalCandidates, writer.Count));
        return new MemoryScanExecutionResult(preview, writer.Count, readFailures);
    }

    public async Task<MemoryScanExecutionResult> NextScanFromStorageAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        IScanResultSet previousResults,
        IMemoryReader memoryReader,
        IScanResultWriter writer,
        TargetArchitecture architecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        int maximumPreviewResults,
        IProgress<MemoryScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(memoryReader);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);
        if (maximumPreviewResults < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPreviewResults));
        }

        ValidateScanDefinition(valueType, scanType, inputValues, architecture, MemoryScanStage.NextScan);
        ResolveScanShape(valueType, inputValues, architecture, scanOptions, out int valueSize, out int alignment);
        ValidateStoredResultShape(previousResults, valueSize, alignment);
        ValidateWriterShape(writer, valueSize, alignment);

        if (previousResults.Count == 0)
        {
            progress?.Report(new MemoryScanProgress(0, 0, 0));
            return new MemoryScanExecutionResult(Array.Empty<MemoryScanResult>(), 0L, 0);
        }

        MemoryRegion[] readableRegions = memoryRegions
            .Where(region => IsScannableRegion(region, valueSize))
            .OrderBy(region => region.BaseAddress)
            .ToArray();
        List<MemoryScanResult> preview = new(Math.Min(maximumPreviewResults, 4096));
        byte[] buffer = new byte[DefaultChunkSize];
        ulong processedCandidates = 0;
        ulong totalCandidates = checked((ulong)previousResults.Count);
        int readFailures = 0;
        int chunksSinceProgressReport = 0;

        await foreach (ScanResultRecordBatch storedBatch in previousResults
                           .ReadBatchesAsync(DefaultStoredResultBatchSize, cancellationToken)
                           .ConfigureAwait(false))
        {
            int candidateIndex = 0;
            ReadOnlyMemory<ulong> addresses = storedBatch.Addresses;

            while (candidateIndex < storedBatch.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ulong address = addresses.Span[candidateIndex];
                MemoryRegion? region = FindContainingRegion(readableRegions, address, valueSize);
                if (region is null || !IsAligned(address, alignment))
                {
                    candidateIndex++;
                    processedCandidates++;
                    continue;
                }

                ulong chunkAddress = address;
                ulong maximumReadEnd = Math.Min(
                    region.EndAddressExclusive,
                    SaturatingAdd(chunkAddress, checked((ulong)DefaultChunkSize)));
                int chunkCandidateStart = candidateIndex;
                int chunkCandidateEnd = candidateIndex;

                while (chunkCandidateEnd < storedBatch.Count)
                {
                    ulong candidateAddress = addresses.Span[chunkCandidateEnd];
                    if (!CanContainValue(region, candidateAddress, valueSize) ||
                        !IsAligned(candidateAddress, alignment))
                    {
                        break;
                    }

                    ulong candidateEnd = checked(candidateAddress + checked((ulong)valueSize));
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

                ulong lastCandidateAddress = addresses.Span[chunkCandidateEnd - 1];
                int readLength = checked((int)(checked(lastCandidateAddress - chunkAddress) + checked((ulong)valueSize)));
                bool readSucceeded = false;

                try
                {
                    int bytesRead = await memoryReader
                        .ReadAsync(process, chunkAddress, buffer.AsMemory(0, readLength), cancellationToken)
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
                        ulong candidateAddress = addresses.Span[index];
                        int relativeOffset = checked((int)(candidateAddress - chunkAddress));
                        ReadOnlySpan<byte> currentBytes = data.Slice(relativeOffset, valueSize);
                        MemoryScanValue previousValue = valueType.CreateValue(
                            storedBatch.GetCurrentValueData(index).Span,
                            alignment,
                            architecture);

                        if (!scanType.IsMatch(
                                valueType,
                                currentBytes,
                                previousValue,
                                inputValues,
                                architecture,
                                MemoryScanStage.NextScan,
                                scanOptions))
                        {
                            continue;
                        }

                        writer.Write(candidateAddress, currentBytes);
                        if (preview.Count < maximumPreviewResults)
                        {
                            MemoryScanValue currentValue = valueType.CreateValue(currentBytes, alignment, architecture);
                            preview.Add(new MemoryScanResult(
                                candidateAddress,
                                currentValue,
                                previousValue,
                                region.Name,
                                region.ModuleName,
                                region.Protection));
                        }
                    }
                }

                processedCandidates = SaturatingAdd(
                    processedCandidates,
                    checked((ulong)(chunkCandidateEnd - chunkCandidateStart)));
                candidateIndex = chunkCandidateEnd;
                chunksSinceProgressReport++;
                if (chunksSinceProgressReport >= ProgressReportChunkInterval)
                {
                    progress?.Report(new MemoryScanProgress(processedCandidates, totalCandidates, writer.Count));
                    chunksSinceProgressReport = 0;
                }
            }
        }

        progress?.Report(new MemoryScanProgress(totalCandidates, totalCandidates, writer.Count));
        return new MemoryScanExecutionResult(preview, writer.Count, readFailures);
    }

    private static void ValidateResidentResultShape(
        INativeValueScanResidentResultSet resultSet,
        int valueSize,
        int alignment)
    {
        if (resultSet.Count < 0)
        {
            throw new InvalidDataException("Resident native result count cannot be negative.");
        }

        if (resultSet.ValueSize != valueSize || resultSet.Alignment != alignment)
        {
            throw new InvalidOperationException(
                $"Resident native results use value size {resultSet.ValueSize} and alignment {resultSet.Alignment}, " +
                $"but the active scan requires value size {valueSize} and alignment {alignment}.");
        }
    }

    private static void ValidateResidentBatchShape(
        NativeValueScanResultBatch batch,
        int valueSize,
        bool requirePreviousValues)
    {
        if (batch.ValueSize != valueSize)
        {
            throw new InvalidDataException(
                $"Resident native result batches contain {batch.ValueSize} value byte(s), but the active scan requires {valueSize}.");
        }

        if (requirePreviousValues && batch.Count > 0 && !batch.HasPreviousValueData)
        {
            throw new InvalidDataException(
                "Resident native Next Scan results did not include the required previous-value data.");
        }
    }

    private static void ValidateResidentAddress(
        ulong address,
        int alignment,
        int valueSize,
        IReadOnlyList<MemoryRegion> readableRegions,
        ref bool hasLastAddress,
        ref ulong lastAddress,
        out MemoryRegion region)
    {
        if (hasLastAddress && address <= lastAddress)
        {
            throw new InvalidDataException(
                "Resident native scan results must be unique and returned in strictly ascending address order.");
        }

        if (!IsAligned(address, alignment))
        {
            throw new InvalidDataException(
                $"Resident native scan result 0x{address:X} is not aligned to {alignment} byte(s).");
        }

        MemoryRegion? containingRegion = FindContainingRegion(readableRegions, address, valueSize);
        if (containingRegion is null)
        {
            throw new InvalidDataException(
                $"Resident native scan result 0x{address:X} is outside the readable memory map.");
        }

        hasLastAddress = true;
        lastAddress = address;
        region = containingRegion;
    }

    private static void ValidateStoredResultShape(
        IScanResultSet previousResults,
        int valueSize,
        int alignment)
    {
        if (previousResults.ValueSize != valueSize || previousResults.Alignment != alignment)
        {
            throw new InvalidOperationException(
                "Next Scan must use the same Value Type, value width, and alignment as the current scan session. Choose New Scan before changing Value Type.");
        }
    }

    private static void ValidateWriterShape(
        IScanResultWriter writer,
        int valueSize,
        int alignment)
    {
        if (writer.ValueSize != valueSize || writer.Alignment != alignment)
        {
            throw new InvalidOperationException(
                "The scan-result writer does not match the resolved Value Type width and alignment.");
        }
    }

    private sealed class StoredResultCursor : IAsyncDisposable
    {
        private readonly IAsyncEnumerator<ScanResultRecordBatch> _enumerator;
        private ScanResultRecordBatch? _batch;
        private int _index;
        private bool _completed;

        public StoredResultCursor(
            IScanResultSet resultSet,
            int batchSize,
            CancellationToken cancellationToken)
        {
            _enumerator = resultSet
                .ReadBatchesAsync(batchSize, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
        }

        public ReadOnlyMemory<byte> CurrentValueData
        {
            get
            {
                if (_batch is null || _completed || (uint)_index >= (uint)_batch.Count)
                {
                    throw new InvalidOperationException("The stored-result cursor is not positioned on a result.");
                }

                return _batch.GetCurrentValueData(_index);
            }
        }

        public async Task<bool> MoveToAddressAsync(ulong address)
        {
            while (await EnsureCurrentAsync().ConfigureAwait(false))
            {
                ulong currentAddress = _batch!.Addresses.Span[_index];
                if (currentAddress >= address)
                {
                    return currentAddress == address;
                }

                _index++;
            }

            return false;
        }

        public async ValueTask DisposeAsync()
        {
            await _enumerator.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<bool> EnsureCurrentAsync()
        {
            while (!_completed && (_batch is null || _index >= _batch.Count))
            {
                if (!await _enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    _completed = true;
                    _batch = null;
                    return false;
                }

                _batch = _enumerator.Current;
                _index = 0;
            }

            return !_completed && _batch is not null && _index < _batch.Count;
        }
    }
}
