using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Disassembly;

public sealed class DisassemblyReader
{
    public const int DefaultWindowByteCount = 512;
    public const int DefaultContextByteCount = 512;
    public const int MaximumWindowByteCount = 65_536;

    public async Task<DisassemblySnapshot> ReadAsync(
        IMemoryReader reader,
        IDisassemblerProvider disassembler,
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        TargetArchitecture architecture,
        ulong address,
        int requestedByteCount = DefaultWindowByteCount,
        CancellationToken cancellationToken = default)
    {
        ValidateCommonArguments(
            reader,
            disassembler,
            process,
            memoryRegions,
            architecture,
            cancellationToken);

        if (requestedByteCount <= 0 || requestedByteCount > MaximumWindowByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedByteCount),
                $"Disassembly window size must be between 1 and {MaximumWindowByteCount:N0} bytes.");
        }

        MemoryRegion region = FindRequiredReadableRegion(memoryRegions, address);
        ulong remainingRegionBytes = region.EndAddressExclusive - address;
        int readLength = checked((int)Math.Min((ulong)requestedByteCount, remainingRegionBytes));
        if (readLength <= 0)
        {
            throw new InvalidOperationException("The containing memory region has no readable bytes at the requested address.");
        }

        return await ReadRangeAsync(
                reader,
                disassembler,
                process,
                architecture,
                requestedAddress: address,
                startAddress: address,
                readLength,
                region,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DisassemblySnapshot> ReadAroundAsync(
        IMemoryReader reader,
        IDisassemblerProvider disassembler,
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        TargetArchitecture architecture,
        ulong address,
        int beforeByteCount = DefaultContextByteCount,
        int afterByteCount = DefaultContextByteCount,
        CancellationToken cancellationToken = default)
    {
        ValidateCommonArguments(
            reader,
            disassembler,
            process,
            memoryRegions,
            architecture,
            cancellationToken);

        if (beforeByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(beforeByteCount),
                "Disassembly context before the requested address cannot be negative.");
        }

        if (afterByteCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(afterByteCount),
                "Disassembly context after the requested address must contain at least one byte.");
        }

        long requestedTotal = (long)beforeByteCount + afterByteCount;
        if (requestedTotal > MaximumWindowByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(afterByteCount),
                $"Combined disassembly context cannot exceed {MaximumWindowByteCount:N0} bytes.");
        }

        MemoryRegion region = FindRequiredReadableRegion(memoryRegions, address);

        ulong availableBefore = address - region.BaseAddress;
        ulong actualBefore = Math.Min((ulong)beforeByteCount, availableBefore);

        ulong availableAfter = region.EndAddressExclusive - address;
        ulong actualAfter = Math.Min((ulong)afterByteCount, availableAfter);

        ulong startAddress = address - actualBefore;
        int readLength = checked((int)(actualBefore + actualAfter));
        if (readLength <= 0)
        {
            throw new InvalidOperationException("The containing memory region has no readable bytes around the requested address.");
        }

        byte[] bytes = await ReadBytesAsync(
                reader,
                process,
                requestedAddress: address,
                startAddress,
                readLength,
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<DisassembledInstruction> instructions = await DecodeAsync(
                disassembler,
                startAddress,
                bytes,
                architecture,
                cancellationToken)
            .ConfigureAwait(false);

        ValidateInstructions(startAddress, bytes, instructions);

        return new DisassemblySnapshot(
            address,
            startAddress,
            bytes,
            region,
            architecture,
            instructions);
    }

    private static void ValidateCommonArguments(
        IMemoryReader reader,
        IDisassemblerProvider disassembler,
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        TargetArchitecture architecture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(disassembler);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(architecture);

        cancellationToken.ThrowIfCancellationRequested();

        if (!disassembler.SupportsArchitecture(architecture))
        {
            throw new NotSupportedException(
                $"The active disassembly provider does not support target architecture {architecture.Cpu}.");
        }
    }

    private static MemoryRegion FindRequiredReadableRegion(
        IEnumerable<MemoryRegion> memoryRegions,
        ulong address)
    {
        return memoryRegions.FirstOrDefault(region =>
                   region.Size > 0 &&
                   region.Protection.HasFlag(MemoryProtection.Read) &&
                   !region.Protection.HasFlag(MemoryProtection.Guard) &&
                   address >= region.BaseAddress &&
                   address < region.EndAddressExclusive)
               ?? throw new InvalidOperationException(
                   $"Address 0x{address:X} is not inside a readable, non-guarded memory region.");
    }

    private static async Task<DisassemblySnapshot> ReadRangeAsync(
        IMemoryReader reader,
        IDisassemblerProvider disassembler,
        TargetProcess process,
        TargetArchitecture architecture,
        ulong requestedAddress,
        ulong startAddress,
        int readLength,
        MemoryRegion region,
        CancellationToken cancellationToken)
    {
        byte[] bytes = await ReadBytesAsync(
                reader,
                process,
                requestedAddress,
                startAddress,
                readLength,
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<DisassembledInstruction> instructions = await DecodeAsync(
                disassembler,
                startAddress,
                bytes,
                architecture,
                cancellationToken)
            .ConfigureAwait(false);

        ValidateInstructions(startAddress, bytes, instructions);

        return new DisassemblySnapshot(
            requestedAddress,
            startAddress,
            bytes,
            region,
            architecture,
            instructions);
    }

    private static async Task<byte[]> ReadBytesAsync(
        IMemoryReader reader,
        TargetProcess process,
        ulong requestedAddress,
        ulong startAddress,
        int readLength,
        CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[readLength];
        int bytesRead = await reader
            .ReadAsync(process, startAddress, bytes, cancellationToken)
            .ConfigureAwait(false);

        if (bytesRead < 0 || bytesRead > bytes.Length)
        {
            throw new InvalidOperationException(
                $"The memory reader returned an invalid byte count of {bytesRead} for a {bytes.Length}-byte disassembly request.");
        }

        if (bytesRead == 0)
        {
            throw new InvalidOperationException("The memory reader returned no data for the requested disassembly range.");
        }

        if (bytesRead != bytes.Length)
        {
            Array.Resize(ref bytes, bytesRead);
        }

        ulong actualEndAddress = checked(startAddress + (ulong)bytes.Length);
        if (requestedAddress < startAddress || requestedAddress >= actualEndAddress)
        {
            throw new InvalidOperationException(
                "The memory reader returned a partial disassembly range that does not include the requested origin address.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return bytes;
    }

    private static async Task<IReadOnlyList<DisassembledInstruction>> DecodeAsync(
        IDisassemblerProvider disassembler,
        ulong startAddress,
        ReadOnlyMemory<byte> bytes,
        TargetArchitecture architecture,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DisassembledInstruction>? instructions = await disassembler
            .DisassembleAsync(startAddress, bytes, architecture, cancellationToken)
            .ConfigureAwait(false);

        if (instructions is null)
        {
            throw new InvalidOperationException("The disassembly provider returned a null instruction collection.");
        }

        return instructions;
    }

    private static void ValidateInstructions(
        ulong startAddress,
        ReadOnlySpan<byte> bytes,
        IReadOnlyList<DisassembledInstruction> instructions)
    {
        ulong endAddress = checked(startAddress + (ulong)bytes.Length);
        ulong previousEndAddress = startAddress;
        bool hasPreviousInstruction = false;

        for (int index = 0; index < instructions.Count; index++)
        {
            DisassembledInstruction? instruction = instructions[index];
            if (instruction is null)
            {
                throw new InvalidOperationException(
                    $"The disassembly provider returned a null instruction at index {index}.");
            }

            ulong instructionEndAddress = checked(instruction.Address + (ulong)instruction.Length);
            if (instruction.Address < startAddress || instructionEndAddress > endAddress)
            {
                throw new InvalidOperationException(
                    $"The disassembly provider returned instruction 0x{instruction.Address:X} outside the requested byte range.");
            }

            if (hasPreviousInstruction && instruction.Address < previousEndAddress)
            {
                throw new InvalidOperationException(
                    "The disassembly provider returned instructions that are out of order or overlap each other.");
            }

            int byteOffset = checked((int)(instruction.Address - startAddress));
            if (!bytes.Slice(byteOffset, instruction.Length).SequenceEqual(instruction.RawBytes.Span))
            {
                throw new InvalidOperationException(
                    $"The raw bytes reported for instruction 0x{instruction.Address:X} do not match the bytes read from target memory.");
            }

            previousEndAddress = instructionEndAddress;
            hasPreviousInstruction = true;
        }
    }
}
