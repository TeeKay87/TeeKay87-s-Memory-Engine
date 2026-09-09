using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

internal sealed class ScanResultFileSet : IScanResultSet
{
    private const int DefaultStreamBufferSize = 1024 * 1024;
    private readonly string _filePath;
    private readonly int _recordSize;
    private bool _disposed;

    public ScanResultFileSet(
        string filePath,
        long expectedCount,
        int expectedValueSize,
        int expectedAlignment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (expectedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCount));
        }

        _filePath = Path.GetFullPath(filePath);

        using FileStream stream = new(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ScanResultBinaryFormat.HeaderSize,
            FileOptions.SequentialScan);
        byte[] header = new byte[ScanResultBinaryFormat.HeaderSize];
        stream.ReadExactly(header);
        (int valueSize, int alignment, int recordSize, long count) =
            ScanResultBinaryFormat.ReadAndValidateHeader(header);

        if (count != expectedCount || valueSize != expectedValueSize || alignment != expectedAlignment)
        {
            throw new InvalidDataException(
                "The committed scan-result file does not match its scan-session metadata.");
        }

        long expectedLength = ScanResultBinaryFormat.GetExpectedFileLength(count, recordSize);
        if (stream.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"The committed scan-result file length is {stream.Length:N0} bytes, but {expectedLength:N0} bytes were expected.");
        }

        Count = count;
        ValueSize = valueSize;
        Alignment = alignment;
        _recordSize = recordSize;
    }

    public long Count { get; }

    public int ValueSize { get; }

    public int Alignment { get; }

    public async IAsyncEnumerable<ScanResultRecordBatch> ReadBatchesAsync(
        int maximumRecordsPerBatch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (maximumRecordsPerBatch <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRecordsPerBatch),
                maximumRecordsPerBatch,
                "Batch size must be positive.");
        }

        await using FileStream stream = new(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            DefaultStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        byte[] header = new byte[ScanResultBinaryFormat.HeaderSize];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        ScanResultBinaryFormat.ReadAndValidateHeader(header);

        long remaining = Count;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int batchCount = checked((int)Math.Min(remaining, maximumRecordsPerBatch));
            byte[] rawRecords = new byte[checked(batchCount * _recordSize)];
            await stream.ReadExactlyAsync(rawRecords, cancellationToken).ConfigureAwait(false);

            ulong[] addresses = new ulong[batchCount];
            byte[] values = new byte[checked(batchCount * ValueSize)];
            for (int index = 0; index < batchCount; index++)
            {
                ReadOnlySpan<byte> record = rawRecords.AsSpan(index * _recordSize, _recordSize);
                addresses[index] = BinaryPrimitives.ReadUInt64LittleEndian(record[..sizeof(ulong)]);
                record.Slice(sizeof(ulong), ValueSize)
                    .CopyTo(values.AsSpan(index * ValueSize, ValueSize));
            }

            yield return new ScanResultRecordBatch(addresses, values, ValueSize);
            remaining -= batchCount;
        }
    }

    public async IAsyncEnumerable<ReadOnlyMemory<ulong>> ReadAddressBatchesAsync(
        int maximumRecordsPerBatch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (ScanResultRecordBatch batch in ReadBatchesAsync(
                           maximumRecordsPerBatch,
                           cancellationToken).ConfigureAwait(false))
        {
            yield return batch.Addresses;
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
