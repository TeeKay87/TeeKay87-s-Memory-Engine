using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

internal sealed class ScanResultFileWriter : IScanResultWriter
{
    private const int StreamBufferSize = 1024 * 1024;
    private readonly ScanResultStorageSession _session;
    private readonly FileStream _stream;
    private readonly byte[] _recordBuffer;
    private readonly string _temporaryPath;
    private readonly string _finalFileName;
    private readonly int _generation;
    private bool _committed;
    private bool _disposed;

    internal ScanResultFileWriter(
        ScanResultStorageSession session,
        string temporaryPath,
        string finalFileName,
        int generation,
        int valueSize,
        int alignment)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalFileName);
        ScanResultBinaryFormat.ValidateShape(valueSize, alignment);

        _temporaryPath = temporaryPath;
        _finalFileName = finalFileName;
        _generation = generation;
        ValueSize = valueSize;
        Alignment = alignment;
        _recordBuffer = new byte[checked(sizeof(ulong) + valueSize)];
        _stream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            StreamBufferSize,
            FileOptions.SequentialScan);

        byte[] header = ScanResultBinaryFormat.CreateHeader(valueSize, alignment, 0);
        _stream.Write(header);
    }

    public long Count { get; private set; }

    public int ValueSize { get; }

    public int Alignment { get; }

    public void Write(ulong address, ReadOnlySpan<byte> currentValueData)
    {
        ThrowIfDisposed();
        if (_committed)
        {
            throw new InvalidOperationException("Cannot write records after the result set has been committed.");
        }

        if (currentValueData.Length != ValueSize)
        {
            throw new ArgumentException(
                $"Result data must contain exactly {ValueSize} byte(s).",
                nameof(currentValueData));
        }

        BinaryPrimitives.WriteUInt64LittleEndian(_recordBuffer.AsSpan(0, sizeof(ulong)), address);
        currentValueData.CopyTo(_recordBuffer.AsSpan(sizeof(ulong), ValueSize));
        _stream.Write(_recordBuffer);
        Count = checked(Count + 1);
    }

    public void WriteBatch(ReadOnlySpan<ulong> addresses, ReadOnlySpan<byte> currentValueData)
    {
        ThrowIfDisposed();
        if (_committed)
        {
            throw new InvalidOperationException("Cannot write records after the result set has been committed.");
        }

        if (currentValueData.Length != checked(addresses.Length * ValueSize))
        {
            throw new ArgumentException(
                "Current-value data length must equal address count multiplied by value size.",
                nameof(currentValueData));
        }

        if (addresses.IsEmpty)
        {
            return;
        }

        int recordSize = _recordBuffer.Length;
        byte[] serialized = new byte[checked(addresses.Length * recordSize)];
        for (int index = 0; index < addresses.Length; index++)
        {
            Span<byte> record = serialized.AsSpan(index * recordSize, recordSize);
            BinaryPrimitives.WriteUInt64LittleEndian(record[..sizeof(ulong)], addresses[index]);
            currentValueData.Slice(index * ValueSize, ValueSize)
                .CopyTo(record.Slice(sizeof(ulong), ValueSize));
        }

        _stream.Write(serialized);
        Count = checked(Count + addresses.Length);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (_committed)
        {
            throw new InvalidOperationException("The result writer has already been committed.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        _stream.Position = ScanResultBinaryFormat.CountOffset;
        byte[] countBytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(countBytes, Count);
        await _stream.WriteAsync(countBytes, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        _stream.Flush(flushToDisk: true);
        _stream.Dispose();

        cancellationToken.ThrowIfCancellationRequested();
        _session.PublishResultSet(
            _temporaryPath,
            _finalFileName,
            _generation,
            Count,
            ValueSize,
            Alignment);
        _committed = true;
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();

        if (!_committed)
        {
            TryDeleteTemporaryFile();
            _session.ReleaseResultWriter(this);
        }
    }

    internal void MarkPublished()
    {
        _committed = true;
        _disposed = true;
    }

    private void TryDeleteTemporaryFile()
    {
        try
        {
            if (File.Exists(_temporaryPath))
            {
                File.Delete(_temporaryPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
