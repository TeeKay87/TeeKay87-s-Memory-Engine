# PS5 Native Scan and Process Control

## Purpose

This document describes the PlayStation 5-specific native scanner and process-control implementation established by TeeKay87's Memory Engine `0.1.3.rev32` / PS5 plugin `0.1.0.rev22`. The same verified scanner/process-control behavior remains active unchanged in current host `0.1.7.rev31` / PS5 plugin `0.1.0.rev38`; later plugin revisions target newer compatible Plugin API minors for Disassembler/Debugger work, but those additions do not redesign this scanner/process-control subsystem.

Core owns the user-facing Scan Type catalog and comparison semantics. The PS5 plugin owns ps5debug-NG protocol details, native capability negotiation, the Core-to-native mapping table, request translation, resident-session management, and process suspend/resume. The host does not contain ps5debug-NG compare ids or command constants.

The implementation currently provides:

- native First Scan and Next Scan acceleration for Core Scan Types whose ps5debug-NG semantics are equivalent;
- TurboScan snapshot-based **Unknown Initial Value** that explicitly includes zero-valued candidates;
- automatic Core fallback for unmapped or runtime-incompatible shapes;
- multi-segment server-resident scan state and bounded GET streaming;
- scan authorization and TurboScan capability negotiation;
- safe resident-session cleanup on New Scan, target replacement, cancellation/failure, incompatible refinement, or disconnect;
- process suspend/resume for the optional **Pause target while scanning** workflow;
- plugin-owned **Endianness**, **Alignment**, and **Floating-point rounding** controls rendered generically by the host, including API 2.8 toggle/applicability metadata.

The generic mapping contract is documented in [`../../architecture/NATIVE_SCAN_TYPE_MAPPING.md`](../../architecture/NATIVE_SCAN_TYPE_MAPPING.md). This document covers only the PS5 implementation of that contract.

## Version Boundaries

```text
Current host application:     0.1.7.rev31
Host Plugin API:              2.16.0
PS5 target Plugin API:        2.16.0
PlayStation 5 plugin:         0.1.0.rev38 (targets API 2.16.0)
In-Memory Test Target plugin: 1.0.0.rev16 (targets API 2.16.0)
Scanner baseline verified at:  host 0.1.3.rev32 / PS5 0.1.0.rev22 / API 2.9.0
```

Plugin API milestones relevant to this subsystem:

- `1.1.0` — optional `INativeValueScanner` and `IProcessControl`;
- `1.2.0` — optional `INativeValueScanRefiner` and native reset;
- `2.1.0` — plugin-owned `IMemoryScanOption` declarations;
- `2.2.0` — batched native-result streaming and complete candidate sources;
- `2.4.0` — optional concurrent memory writer used by Frozen writes during scans;
- `2.6.0` — `IMemoryValueComparer` for Core ordered/delta predicates;
- `2.7.0` — `INativeScanTypeMappingProvider` and `NativeScanTypeMapping`;
- `2.8.0` — optional `IMemoryScanOptionPresentation` and `IMemoryScanOptionApplicability` for generic option editor/visibility metadata.
- `2.9.0` — optional `INativeValueScanResidentResultSet` plus Previous-value payloads on `NativeValueScanResultBatch` for bounded authoritative backend-resident result windows.
- `2.10.0` — neutral disassembly contracts; no change to the native-scan/process-control contracts documented here.
- `2.11.0` — optional neutral disassembly syntax-presentation tokens; no change to the native-scan/process-control contracts documented here.
- `2.12.0` — neutral debugger contracts; no change to the native-scan/process-control contracts documented here.
- `2.13.0` — neutral debugger register value-encoding metadata; no change to the native-scan/process-control contracts documented here.

## Core-to-ps5debug-NG Semantic Mapping

The connected PS5 session implements `INativeScanTypeMappingProvider`. The host uses the mapping table only as a semantic eligibility gate; the plugin performs all remaining runtime checks before sending a native request.

| Core Scan Type | Native implementation | First | Next | Native Value Types | Important restriction |
| --- | --- | :---: | :---: | --- | --- |
| Exact Value | `compareType 0` | Yes | Yes | Integer, Float, Double, Array of Bytes | strict Float/Double Next uses Core; incompatible big-endian numeric shapes use Core |
| Fuzzy Value | `compareType 1` | Yes | Yes | Float, Double | absolute difference `< 1.0` matches Core |
| Bigger Than | `compareType 2` | Yes | Yes | Numeric | multi-byte big-endian magnitude comparisons use Core |
| Smaller Than | `compareType 3` | Yes | Yes | Numeric | multi-byte big-endian magnitude comparisons use Core |
| Between | `compareType 4` | Yes | Yes | Numeric | two operands; multi-byte big-endian magnitude comparisons use Core |
| Unknown Initial Value | TurboScan snapshot + include zeros | Yes | No | Fixed-width numeric | requires snapshot engine; does not use `compareType 11` |
| Unknown Initial Low Value | `compareType 12` | Yes | No | Integer only | Float/Double use Core because upstream compares absolute magnitude |
| Increased Value | `compareType 5` | No | Yes | Numeric | multi-byte big-endian magnitude comparisons use Core |
| Decreased Value | `compareType 7` | No | Yes | Numeric | multi-byte big-endian magnitude comparisons use Core |
| Changed Value | `compareType 9` | No | Yes | Numeric | resident previous value required |
| Unchanged Value | `compareType 10` | No | Yes | Numeric | resident previous value required |
| Increased By | Core fallback | No | No | Numeric | upstream `compareType 6` can wrap at target width |
| Decreased By | Core fallback | No | No | Numeric | upstream `compareType 8` can wrap at target width |

The mapping table deliberately does not equate operations merely because their names are similar. Core remains authoritative for the user-visible meaning.

### Why Core Unknown Initial Value does not map to compareType 11

ps5debug-NG `compareType 11` retains only non-zero values. Core **Unknown Initial Value** is defined as a true initial snapshot of every fixed-width candidate, including zero.

Modern ps5debug-NG TurboScan provides a separate snapshot mode with:

```text
TS_SNAPSHOT               = 0x00000004
TS_SNAPSHOT_INCLUDE_ZEROS = 0x00000008
TS_SNAPSHOT_SEGMENTS      = 0x00000010
```

PS5 plugin `0.1.0.rev22` uses that snapshot mode instead. The native optimization therefore preserves Core semantics rather than silently dropping zero-valued candidates.

### Why Increased By and Decreased By stay in Core

ps5debug-NG `compareType 6` and `8` compute arithmetic at the target value width. At integer boundaries that can wrap before equality is tested. Core's Increased By / Decreased By semantics are directional and do not rely on target-width wrapping. The PS5 plugin therefore publishes no native mapping for those two predicates.

### Why floating Unknown Initial Low stays in Core

Core **Unknown Initial Low Value** means:

```text
0 < current <= upperLimit
```

for the selected numeric representation.

ps5debug-NG `compareType 12` uses absolute magnitude for Float/Double. That can retain negative values whose magnitude is under the limit and is therefore not equivalent to the Core predicate. The native mapping is restricted to integer Value Types.

## Runtime Baseline

Before target-side acceleration, the shared scanner was proven correct on a real PS5 but a full-process Int32 Exact Value First Scan could take approximately seven minutes. Earlier TurboScan work reduced comparable native First Scans to roughly 15 seconds in reported live tests, and rev15 later verified a massive signed-byte native First Scan with `10,953,954` committed results followed by complete disk-backed refinement to `8,796,420` survivors.

Rev26 retains that verified Exact Value infrastructure and generalizes the native predicate routing. The new mapped predicates and snapshot-based Unknown Initial Value still require live rev26 verification before the expanded native matrix is considered verified.

## Capability Negotiation

During connection setup the client sends:

```text
0xBDAACC10  TurboScan CAPS
```

The 16-byte capability response is:

```text
uint32 version
uint32 engines
uint32 max_threads
uint32 reserved
```

The PS5 session exposes native scanner services only when the server supports the established resident/segment baseline:

```text
version >= 1
TSE_SERVER_RESIDENT  = 0x00000004
TSE_SNAPSHOT_SEGMENTS = 0x00000010
```

Optional engine bits are retained for per-request decisions, including:

```text
TSE_SNAPSHOT        = 0x00000008
TSE_RESCAN_ALIASING = 0x00000200
```

Unknown Initial Value additionally requires `TSE_SNAPSHOT`. If the baseline native capabilities are absent, the session does not expose the native scanner services and the host uses Core. If only a specific optional engine is missing, only the affected mapped operation falls back.

## Scan Authorization

TurboScan START/COUNT/GET/END requires process-scan authorization.

Memory Engine sends:

```text
CMD_PROC_AUTH = 0xBDAACCFF
```

with:

```text
uint32 magic = 0xBB40E64D
uint32 flags = 0x00000002
```

The client completes the documented challenge/response exchange and caches successful scan authorization for the lifetime of the connection.

## Native First Scan

### Compare-style START

Mapped direct predicates use:

```text
0xBDAACC11  TurboScan START
```

with the packed 27-byte body:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 address       // zero in segmented resident mode
0x0C    4     uint32 length        // zero in segmented resident mode
0x10    1     uint8 valueType
0x11    1     uint8 compareType    // selected from plugin mapping
0x12    1     uint8 alignment
0x13    4     uint32 lenData       // total comparison operand bytes
0x17    4     uint32 flags
```

Normal mapped First Scan flags are:

```text
TS_SERVER_RESIDENT = 0x00000002
TS_SNAPSHOT_SEGMENTS = 0x00000010
flags = 0x00000012
```

After the first acknowledgement, the plugin sends zero, one, or two encoded operands according to the Core Scan Type:

- zero operands are used only by predicates whose native First Scan shape requires none;
- Exact/Fuzzy/Bigger/Smaller/Unknown Initial Low use one operand;
- Between uses two operands in Core lower/upper order.

Array of Bytes is currently supported only for Exact Value. Its one operand is followed by a same-length mask filled with `0x01`, so every byte must match.

After the second acknowledgement, the client streams the disjoint segment list:

```text
uint32 segment_count
segment_count x {
    uint64 address
    uint32 length
}
```

The server then returns a resident summary and final status. If resident storage is refused, the client consumes the complete defined framing and raises `NotSupportedException`; the host then runs the shared Core First Scan.

### Unknown Initial snapshot START

Unknown Initial Value uses the same START command with:

```text
compareType = 11                 // protocol field remains populated
lenData     = 0
flags       = TS_SNAPSHOT |
              TS_SNAPSHOT_INCLUDE_ZEROS |
              TS_SNAPSHOT_SEGMENTS
```

Snapshot mode ignores normal compare matching and creates the resident value snapshot from the supplied segments.

After the segment list the server returns:

```text
uint64 slot_count
uint64 total_bytes
zero or more uint64 progress records
uint64 0xFFFFFFFFFFFFFFFF sentinel
uint32 snapshot_ok
uint64 survivor_count
CMD_SUCCESS
```

The progress sequence is terminated only by the `0xFFFFFFFFFFFFFFFF` sentinel; it is not a fixed-count array. ps5debug-NG normally uses large snapshot I/O windows, but it can fall back to smaller buffers when target-side allocation pressure changes. Large scans can therefore legitimately produce more than 1,024 progress records. The PS5 client must keep reading until the sentinel and must then consume the summary and completion status before validating `snapshot_ok` or survivor counts. This keeps the TCP command stream framed even when the target refuses the snapshot. A clean `snapshot_ok == 0` becomes `NotSupportedException` and Core fallback on the same connection. If the server claims a resident session but reports malformed summary state, the client records that ownership early enough to send TurboScan END during cleanup.

The client validates that the summary is sane and that `survivor_count <= slot_count`. A failed snapshot is treated as native acceleration unavailable, not as a successful empty scan.

The snapshot remains resident so later mapped Next Scan predicates can narrow it through COUNT/GET.

## Segment Construction

Core first supplies the neutral readable/non-Guarded region set. The PS5 plugin then:

- applies the selected/default alignment;
- preserves the complete candidate width at range boundaries;
- splits ranges when required by the protocol's `uint32 length` field;
- sends disjoint segments so gaps between selected regions are not scanned;
- keeps all ps5debug-NG segment structures inside the plugin.

## Value Types

The PS5 plugin maps the eleven standard Value Types to ps5debug-NG wire ids:

| Wire id | Memory Engine Value Type | Width | Natural alignment |
| ---: | --- | ---: | ---: |
| 0 | UInt8 | 1 | 1 |
| 1 | Int8 | 1 | 1 |
| 2 | UInt16 | 2 | 2 |
| 3 | Int16 | 2 | 2 |
| 4 | UInt32 | 4 | 4 |
| 5 | Int32 | 4 | 4 |
| 6 | UInt64 | 8 | 8 |
| 7 | Int64 | 8 | 8 |
| 8 | Float32 | 4 | 4 |
| 9 | Float64 | 8 | 8 |
| 10 | ByteArray | dynamic | 1 |

Array of Bytes remains an exact sequence; wildcard/masked input is not exposed yet.

## API 2.9 Resident Result Handle

A successful authoritative TurboScan START is exposed as `INativeValueScanResidentResultSet`. The handle binds the host-visible result count/shape to the current PS5 TurboScan generation without exposing a ps5debug-NG session id.

For result counts above the host's 50,000-row presentation ceiling, First Scan calls bounded GET only for the preview window. The remaining survivors stay in the target-side TurboScan session. `ReadResultBatchesAsync(startIndex, maximumRecords, ...)` maps the neutral bounded-window request to TurboScan GET and validates the protocol's 32-bit start-index boundary. The host does not enumerate unrelated records.

The handle's `CanRefine(...)` check verifies process id, current TurboScan generation, survivor count, Value Type id, value width, alignment, mapped request shape, and strict Float/Double constraints without mutating the resident session.

A native COUNT refinement increments the PS5 client's resident generation and returns a replacement handle. The previous handle is then stale and cannot issue GET/refinement requests; disposing that stale handle does not terminate the replacement generation. This prevents old WPF/Core references from accidentally closing a newer native session.

For a resident Next Scan preview, GET includes the target-stored previous value in addition to the current value. Core uses that payload for the neutral `MemoryScanResult.PreviousValue` field.

If Core fallback is required, the host reads the complete current resident set in bounded windows, commits it through the existing disk-backed writer, sends TurboScan END/reset, and only then executes shared refinement.

## Native Next Scan Refinement

Mapped Next Scan predicates use:

```text
0xBDAACC12  TurboScan COUNT
```

with the packed 22-byte body:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 base_address  // zero for resident mode
0x0C    1     uint8 valueType
0x0D    1     uint8 compareType    // selected from plugin mapping
0x0E    4     uint32 lenData       // zero/one/two-operand payload length
0x12    4     uint32 flags
```

Required resident flag:

```text
TS_SERVER_RESIDENT = 0x00000002
```

When available, the plugin also enables:

```text
TS_RESCAN_ALIASING = 0x00000100
```

After the acknowledgement, the client sends the mapped comparison operand bytes, if any. COUNT returns:

```text
zero or more uint64 progress records
uint64 0xFFFFFFFFFFFFFFFF sentinel
uint64 new_survivor_count
CMD_SUCCESS
```

The plugin rejects a survivor count that increases relative to the prior resident count.

Before native refinement, the plugin also requires:

- an active resident TurboScan session;
- the same PID;
- the same Value Type id/width and alignment;
- host previous-result count equal to the resident survivor count;
- a semantic mapping for the selected Next Scan predicate/value type;
- a runtime shape compatible with ps5debug-NG endianness and floating-point behavior.

If any condition fails, the resident state is closed when necessary and the host uses the shared disk-backed Core refinement.

### Strict Float/Double Exact Value

ps5debug-NG Exact Value uses a relative `1e-6` floating tolerance for Float/Double. Memory Engine's default **Strict** Exact Value semantics require exact value equality.

For Strict mode:

- First Scan may use native START, but returned GET records are strict-filtered before they enter Core storage;
- Next Scan does not use native resident Exact refinement and instead uses the shared Core path.

Selecting the PS5 scan option **ps5debug-NG tolerance (1e-6)** deliberately changes Exact Value semantics to match the backend and allows compatible native Next Scan refinement.

This behavior is separate from Core **Fuzzy Value**, which uses absolute difference `< 1.0` and maps to `compareType 1`.

## Endianness Rules

The ps5debug-NG wire protocol itself is little-endian. The Scan panel's Endianness option controls how Memory Engine encodes/decodes target values.

Native multi-byte comparisons that interpret numeric magnitude are therefore not semantically safe for Big Endian values and fall back to Core. This includes Bigger Than, Smaller Than, Between, Increased Value, Decreased Value, and other magnitude-sensitive operations.

Raw byte equality/inequality is endian-independent. Integer Exact Value, Changed Value, and Unchanged Value can therefore remain native when the selected representation is otherwise compatible. From PS5 plugin `0.1.0.rev22`, Float/Double Changed Value and Unchanged Value also preserve Core raw-byte snapshot semantics: COUNT sends the same-width unsigned integer wire Value Type (`UInt32` for Float, `UInt64` for Double), avoiding IEEE-754 NaN equality while keeping the resident session width and Float/Double GET decoding unchanged. Snapshot Unknown Initial Value is also endian-independent because it stores raw fixed-width slots rather than interpreting a numeric magnitude during First Scan.

Float/Double native comparisons that interpret numeric magnitude remain little-endian operations, so Big Endian floating requests use Core fallback for those predicates. Changed/Unchanged are the exception because rev22 deliberately reduces them to endian-independent raw-byte equality/inequality.

## GET Result Retrieval

Resident survivors are fetched with:

```text
0xBDAACC13  TurboScan GET
```

Request:

```text
uint32 start_index
uint32 count
uint32 flags
```

For selected width `N`, normal records are:

```text
uint64 address
byte current_value[N]
byte previous_value[N]
```

When response-header bit 31 is set, another `N` bytes contain the retained first value. The client calculates the record stride dynamically and caps temporary GET payloads to approximately 4 MiB.

The API 2.2 streaming path converts these records into bounded `NativeValueScanResultBatch` objects. Core validates and writes the complete set into its disk-backed result generation while materializing only the bounded WPF preview. The historical two-million-result limit therefore applies only to legacy list materialization, not to the streaming disk-backed path.

START/COUNT is authoritative for survivor membership. GET transports those survivors and their current values; it is not reinterpreted as another predicate evaluation, except for the deliberate Strict Float/Double Exact First Scan filter described above.

## Resident Session Cleanup

`INativeValueScanRefiner.ResetAsync` maps to:

```text
0xBDAACC14  TurboScan END
```

The host/plugin closes resident state when:

- **New Scan** is selected;
- Active Target changes;
- the resident session no longer matches the host scan session;
- a native error/cancellation path may have mutated resident state;
- the selected next operation cannot safely continue on the current resident session.

Disconnect also frees server-side state as part of connection teardown.

## Cancellation and Command-Stream Integrity

ps5debug-NG uses one shared framed TCP command stream. Once a streamed START/COUNT/GET transaction begins, abandoning the local read can leave protocol bytes queued for the next command.

Memory Engine therefore:

1. checks caller cancellation before starting a command when possible;
2. completes an already-started streamed command with a non-cancellable transport token;
3. observes caller cancellation at the next safe protocol boundary;
4. closes resident state if the interrupted logical operation can no longer be reused safely;
5. only then returns cancellation to Core.

The protocol currently exposes no asynchronous TurboScan abort command. The UI may therefore show that cancellation was requested while the target-side operation finishes safely.

## PS5 Scan Options

### Endianness

Choices remain **Little Endian** (default) and **Big Endian**, but API 2.8 presentation metadata asks the host to render them as a **Little-endian byte order** checkbox. Checked maps to Little Endian; unchecked maps to Big Endian. This is a Memory Engine encoding/decoding choice; there is no ps5debug-NG TurboScan endianness field. The checkbox is hidden for one-byte and Array-of-Bytes Value Types where endianness is irrelevant and locks after First Scan.

### Alignment

Choices are **Default**, 1, 2, 4, 8, 16, 32, 64, and 128 bytes. Default resolves to the selected Value Type's natural alignment. The resolved value is written to TurboScan START's `uint8 alignment` field and also governs Core fallback scanning.

### Floating-point rounding

This option applies to Float/Double Exact Value only. API 2.8 Scan Type applicability metadata makes the host hide it for Fuzzy, Between, changed-value, delta, and every other Core Scan Type. **Strict** is the default. **ps5debug-NG tolerance (1e-6)** explicitly selects the upstream Exact Value tolerance. Core implements the same tolerance when fallback is required so the selected option retains one meaning across execution paths. Unlike Endianness and Alignment, this option remains editable after First Scan so a later Exact Value Next Scan can choose its comparison mode.

## Process Suspend and Resume

The PS5 process-control implementation uses:

```text
CMD_DEBUG_PROCESS_STOP = 0xBDBB0500
```

The packed request body is:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    1     uint8 state
```

Memory Engine uses:

```text
state = 0  resume
state = 1  suspend / stop
```

State `2` (kill) is intentionally not exposed through the current neutral `IProcessControl` contract.

The WPF Scan panel exposes **Pause target while scanning** only when both suspend and resume capabilities/services exist. When enabled, the host suspends immediately before First/Next Scan and resumes in cleanup after success, cancellation, or ordinary failure. Process-control command ids remain inside the PS5 plugin.

## Verification Boundary

The earlier Exact Value TurboScan, massive disk-backed result flow, Pause target behavior, connection/read/write paths, and cancellation command-stream safety have live PS5 verification recorded in the existing plugin/test documents.

Rev26 adds new native mapping behavior that must still be verified on Windows and, where practical, on a physical PS5:

- Fuzzy Value native First/Next;
- Bigger/Smaller/Between native mapping;
- Changed/Unchanged and Increased/Decreased native refinement;
- snapshot-based Unknown Initial Value including zero candidates;
- integer Unknown Initial Low Value native mapping;
- Core fallback for Increased By/Decreased By;
- Core fallback for floating Unknown Initial Low;
- big-endian fallback boundaries;
- resident-session cleanup when changing to an unmapped/incompatible Next Scan predicate.

The current application-level procedure is in [`../../testing/APP_0.1.3_REV26_VERIFICATION.md`](../../testing/APP_0.1.3_REV26_VERIFICATION.md).
