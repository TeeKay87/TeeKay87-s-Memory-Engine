using System;
using System.Collections.Generic;
using System.Linq;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Disassembly;

public sealed class DisassemblySnapshot
{
    private readonly byte[] _bytes;
    private readonly IReadOnlyList<DisassembledInstruction> _instructions;

    public DisassemblySnapshot(
        ulong requestedAddress,
        ulong startAddress,
        ReadOnlySpan<byte> bytes,
        MemoryRegion region,
        TargetArchitecture architecture,
        IEnumerable<DisassembledInstruction> instructions)
    {
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("A disassembly snapshot must contain at least one byte.", nameof(bytes));
        }

        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(instructions);

        ulong endAddressExclusive = checked(startAddress + (ulong)bytes.Length);
        if (requestedAddress < startAddress || requestedAddress >= endAddressExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedAddress),
                "The requested disassembly origin must be contained by the snapshot byte range.");
        }

        RequestedAddress = requestedAddress;
        StartAddress = startAddress;
        _bytes = bytes.ToArray();
        Region = region;
        Architecture = architecture;
        _instructions = Array.AsReadOnly(instructions.ToArray());
    }

    public ulong RequestedAddress { get; }

    public ulong StartAddress { get; }

    public ReadOnlyMemory<byte> Bytes => _bytes;

    public ulong EndAddressExclusive => checked(StartAddress + (ulong)_bytes.Length);

    public MemoryRegion Region { get; }

    public TargetArchitecture Architecture { get; }

    public IReadOnlyList<DisassembledInstruction> Instructions => _instructions;
}
