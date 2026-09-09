using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

internal static class ScanResultBinaryFormat
{
    public const int Version = 1;
    public const int HeaderSize = 32;
    public const int CountOffset = 24;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("TK87SR01");

    public static byte[] CreateHeader(int valueSize, int alignment, long resultCount)
    {
        ValidateShape(valueSize, alignment);
        if (resultCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resultCount));
        }

        byte[] header = new byte[HeaderSize];
        Magic.CopyTo(header, 0);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), Version);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), valueSize);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), alignment);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20, 4), checked(sizeof(ulong) + valueSize));
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(CountOffset, 8), resultCount);
        return header;
    }

    public static (int ValueSize, int Alignment, int RecordSize, long Count) ReadAndValidateHeader(
        ReadOnlySpan<byte> header)
    {
        if (header.Length != HeaderSize || !header[..Magic.Length].SequenceEqual(Magic))
        {
            throw new InvalidDataException("The scan-result file has an invalid or unsupported header.");
        }

        int version = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(8, 4));
        if (version != Version)
        {
            throw new InvalidDataException($"Scan-result record format {version} is not supported by this build.");
        }

        int valueSize = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(12, 4));
        int alignment = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(16, 4));
        int recordSize = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(20, 4));
        long count = BinaryPrimitives.ReadInt64LittleEndian(header.Slice(CountOffset, 8));
        ValidateShape(valueSize, alignment);

        if (recordSize != checked(sizeof(ulong) + valueSize) || count < 0)
        {
            throw new InvalidDataException("The scan-result file contains invalid record metadata.");
        }

        return (valueSize, alignment, recordSize, count);
    }

    public static long GetExpectedFileLength(long count, int recordSize)
    {
        if (count < 0 || recordSize <= sizeof(ulong))
        {
            throw new ArgumentOutOfRangeException();
        }

        return checked(HeaderSize + checked(count * (long)recordSize));
    }

    public static void ValidateShape(int valueSize, int alignment)
    {
        if (valueSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueSize), valueSize, "Value size must be positive.");
        }

        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Alignment must be positive.");
        }
    }
}
