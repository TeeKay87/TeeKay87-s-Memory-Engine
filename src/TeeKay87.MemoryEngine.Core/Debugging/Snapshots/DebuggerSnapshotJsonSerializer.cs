using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TeeKay87.MemoryEngine.Core.Debugging.Snapshots;

public sealed record DebuggerSnapshotDocument(string Schema, int SchemaVersion, DebuggerSnapshot Snapshot);

public sealed class DebuggerSnapshotJsonSerializer
{
    public const string SchemaName = "teekay87-memory-engine-debugger-snapshot";
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public string Serialize(DebuggerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(new DebuggerSnapshotDocument(SchemaName, CurrentSchemaVersion, snapshot), Options);
    }

    public DebuggerSnapshot Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        DebuggerSnapshotDocument document = JsonSerializer.Deserialize<DebuggerSnapshotDocument>(json, Options)
            ?? throw new InvalidDataException("Snapshot JSON did not contain a debugger snapshot document.");
        if (!string.Equals(document.Schema, SchemaName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Snapshot file has an unsupported schema identity.");
        }
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new NotSupportedException($"Snapshot file uses unsupported schema version {document.SchemaVersion}.");
        }
        if (document.Snapshot is null)
        {
            throw new InvalidDataException("Snapshot file does not contain snapshot data.");
        }
        ValidateSnapshot(document.Snapshot);
        return DebuggerSnapshotCaptureService.Freeze(document.Snapshot);
    }

    public async Task ExportAsync(string path, DebuggerSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, Serialize(snapshot), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, fullPath, true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }

    public async Task<DebuggerSnapshot> ImportAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Deserialize(json);
    }


    private static void ValidateSnapshot(DebuggerSnapshot snapshot)
    {
        if (snapshot.Source is null || snapshot.Event is null || snapshot.Sections is null ||
            snapshot.Registers is null || snapshot.CallStack is null || snapshot.Disassembly is null ||
            snapshot.Breakpoints is null || snapshot.Memory is null)
        {
            throw new InvalidDataException("Snapshot file is missing one or more required sections.");
        }
        if (snapshot.SnapshotId == Guid.Empty)
        {
            throw new InvalidDataException("Snapshot id is missing or invalid.");
        }
        if (snapshot.Source.AddressWidth <= 0 || snapshot.Source.AddressWidth > 64 ||
            snapshot.Source.PointerWidth <= 0 || snapshot.Source.PointerWidth > 64)
        {
            throw new InvalidDataException("Snapshot address/pointer width is invalid.");
        }

        ulong maximumAddress = snapshot.Source.AddressWidth == 64
            ? ulong.MaxValue
            : (1UL << snapshot.Source.AddressWidth) - 1UL;
        static void RequireHex(string value, string field)
        {
            if (value.Length % 2 != 0)
                throw new InvalidDataException($"{field} contains an odd number of hexadecimal characters.");
            for (int index = 0; index < value.Length; index++)
                if (!Uri.IsHexDigit(value[index]))
                    throw new InvalidDataException($"{field} contains invalid hexadecimal data.");
        }
        void RequireAddress(ulong? value, string field)
        {
            if (value.HasValue && value.Value > maximumAddress)
                throw new InvalidDataException($"{field} exceeds the snapshot address width.");
        }

        RequireAddress(snapshot.Event.InstructionPointer, "instructionPointer");
        RequireAddress(snapshot.Event.TriggerInstructionAddress, "triggerInstructionAddress");
        RequireAddress(snapshot.Event.WatchedAddress, "watchedAddress");
        foreach (DebuggerSnapshotRegister register in snapshot.Registers)
        {
            if (register.BitWidth <= 0 || register.BitWidth > 4096)
                throw new InvalidDataException($"Register '{register.Id}' bit width is invalid.");
            if (register.RawBytes is null)
                throw new InvalidDataException($"Register '{register.Id}' rawBytes is missing.");
            RequireHex(register.RawBytes, $"register '{register.Id}' rawBytes");
            int expectedBytes = checked((register.BitWidth + 7) / 8);
            if (register.RawBytes.Length != expectedBytes * 2)
                throw new InvalidDataException($"Register '{register.Id}' raw byte length does not match bit width.");
        }
        foreach (DebuggerSnapshotCallFrame frame in snapshot.CallStack)
        {
            RequireAddress(frame.InstructionAddress, "callStack.instructionAddress");
            RequireAddress(frame.ReturnAddress, "callStack.returnAddress");
            RequireAddress(frame.StackPointer, "callStack.stackPointer");
            RequireAddress(frame.FramePointer, "callStack.framePointer");
            RequireAddress(frame.ModuleBase, "callStack.moduleBase");
        }
        foreach (DebuggerSnapshotInstruction instruction in snapshot.Disassembly)
        {
            RequireAddress(instruction.Address, "disassembly.address");
            RequireAddress(instruction.DirectTarget, "disassembly.directTarget");
            if (instruction.Bytes is null)
                throw new InvalidDataException("disassembly.bytes is missing.");
            if (instruction.Markers is null)
                throw new InvalidDataException("disassembly.markers is missing.");
            RequireHex(instruction.Bytes, "disassembly.bytes");
        }
        foreach (DebuggerSnapshotBreakpoint breakpoint in snapshot.Breakpoints)
        {
            RequireAddress(breakpoint.Address, "breakpoints.address");
        }
        foreach (DebuggerSnapshotMemoryBlock block in snapshot.Memory)
        {
            RequireAddress(block.StartAddress, "memory.startAddress");
            if (block.Bytes is null)
                throw new InvalidDataException("memory.bytes is missing.");
            RequireHex(block.Bytes, "memory.bytes");
            ulong byteCount = (ulong)(block.Bytes.Length / 2);
            if (byteCount > 0 && block.StartAddress > maximumAddress - (byteCount - 1))
                throw new InvalidDataException("Captured memory block exceeds the snapshot address width.");
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowTrailingCommas = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new HexUlongConverter());
        return options;
    }

    private sealed class HexUlongConverter : JsonConverter<ulong>
    {
        public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("64-bit debugger values must be hexadecimal strings.");
            string text = reader.GetString() ?? throw new JsonException("Address value is missing.");
            if (!text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                !ulong.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.AllowHexSpecifier,
                    System.Globalization.CultureInfo.InvariantCulture, out ulong value))
                throw new JsonException($"Invalid hexadecimal 64-bit value '{text}'.");
            return value;
        }

        public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options) =>
            writer.WriteStringValue($"0x{value:X}");
    }
}
