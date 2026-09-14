# ps5debug-NG Protocol Mapping

## Scope

This document records the ps5debug-NG wire-protocol subset currently implemented by the PlayStation 5 plugin. The implemented subset covers target identification/liveness, process enumeration, memory maps/read/write, TurboScan workflows, process suspend/resume, and the real debugger transport. The completed `0.1.6.rev14` Disassembler block remains fully hardware-verified. Host `0.1.7.rev10` / PS5 plugin `0.1.0.rev29` is the verified guarded-register baseline, host `0.1.7.rev15` / PS5 plugin `0.1.0.rev32` is the verified software execute-breakpoint baseline, and host `0.1.7.rev16` / PS5 plugin `0.1.0.rev33` is the verified hardware-watchpoint baseline after **118/118** plus focused Mock/live-PS5 acceptance. Current host `0.1.7.rev31` / PS5 plugin `0.1.0.rev38` targets Plugin API `2.16.0`. Rev29 changed only host/Core logical Disassembler presentation. Rev37 adds optional client-side request validation and no new wire command. Rev38 also adds no new wire command: it explicitly sends the existing hardware-watchpoint command with `enabled = false` for every client-owned active or staged hardware slot before backend detach. Rev36 similarly uses the existing software-breakpoint disable command defensively before detach. Rev34 introduced server-side Call Stack through `CMD_PROC_READ_STACK` and native Step Into through `CMD_DEBUG_STEP` / `CMD_DEBUG_STEP_THREAD`. Rev35 retains that wire mapping and adds client-side logical software-breakpoint stop reconciliation for the current backend's transparent restore/single-step/rearm behavior. Software breakpoint address validation, hardware watchpoint rules, debugger register guards, and the existing neutral local disassembly path remain unchanged.


The implementation was initially reviewed against the public ps5debug-NG repository at commit:

```text
d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86
```

The process-list implementation was additionally checked against the public ps5debug-NG `PROTOCOL.md` definition for `CMD_PROC_LIST`, which defines a success status, a `uint32` count, and fixed-size `proc_list_entry` records containing `char name[32]` and `int32_t pid`. The raw-memory-write mapping was rechecked against current upstream protocol/source before the rev16 implementation. TurboScan capability, authorization, multi-segment resident START/GET/END, and process-control mappings were rechecked during rev4 preparation on 2026-08-31; resident COUNT/refinement and rescan-aliasing were rechecked during rev5; the full value-type table and Array of Bytes mask behavior were rechecked during `0.1.3.rev1`. For `0.1.3.rev26`, `PROTOCOL.md`, `scan_compare.c`, and `scan_turbo.c` were rechecked again for `compareType 0..12`, operand requirements, resident refinement, snapshot/include-zero behavior, and the semantics used to decide which Core Scan Types may be mapped natively. For `0.1.7.rev3`, current `PROTOCOL.md`, `debugger/source/debug.c`, and the FreeBSD amd64 register layout were rechecked for `CMD_DEBUG_ATTACH`, `CMD_DEBUG_DETACH`, `CMD_DEBUG_CONTINUE`, the TCP `755` callback sequence, the 1184-byte interrupt layout, and the instruction-pointer offset used by the plugin parser. For `0.1.7.rev16`, current `PROTOCOL.md`, `common/include/protocol.h`, and `debugger/source/debug.c` were rechecked for `CMD_DEBUG_SET_WATCHPOINT`, its 24-byte request, the four hardware slots, DR7 length/access encoding, per-LWP register application, the interrupt packet's debug-register block, and the current server-side DR6 clearing behavior that limits exact multi-watchpoint attribution. For `0.1.7.rev17`, current `PROTOCOL.md`, `common/include/protocol.h`, `debugger/source/proc.c`, and `debugger/source/debug.c` were rechecked for `CMD_PROC_READ_STACK` (`0xBDAA0023`), its packed 24-byte request and variable frame payload, the maximum stack depth/locals/code-window limits, `CMD_DEBUG_STEP` (`0xBDBB0012`), `CMD_DEBUG_STEP_THREAD` (`0xBDBB0013`), and the native `PT_STEP`/asynchronous debugger-event flow. For `0.1.7.rev25`, current `debugger/source/debug.c` was rechecked again against the live breakpoint reproductions: a matched software breakpoint restores the saved byte, rewinds the packet RIP, applies that RIP to the thread, executes `PT_STEP`, waits for completion, rearms `INT3`, and only then sends the original 1184-byte packet. This establishes that the packet is a logical pre-instruction snapshot while later GETREGS is post-instruction.

Relevant upstream references include `PROTOCOL.md`, `common/include/protocol.h`, `debugger/source/meta.c`, `debugger/source/proc.c`, `debugger/source/auth.c`, `debugger/source/scan.c`, `debugger/source/scan_turbo.c`, `debugger/source/debug.c`, and `ps5-payload-sdk/include/freebsd/x86/reg.h`.

No ps5debug-NG source file is embedded in TeeKay87's Memory Engine. The PS5 plugin is an independent C# client implementation of the documented wire protocol.

### Source precedence note

For this implementation, the referenced server source and `PROTOCOL.md` are treated as authoritative when examples elsewhere in the upstream repository disagree with them. In the connection subset, VERSION, BRANDING, PLATFORM_ID, and FW_VERSION send their payloads directly without a leading status word, while NOP sends `CMD_SUCCESS`. `CMD_PROC_LIST` uses the process-command convention and sends a success status before its count and entries.

## Transport

The normal target command channel uses TCP port `744` by default. Rev3 keeps the established target/memory/scan `Ps5DebugClient` on that connection and opens a separate TCP connection to the same command port for each attached debugger session. ps5debug-NG additionally connects **outbound** from the console to the client on TCP port `755` for asynchronous debugger interrupts; the plugin starts that listener before sending attach.

Every request currently sent by the plugin uses the standard 12-byte little-endian command header:

```text
Offset  Size  Field
0x00    4     packet magic
0x04    4     command id
0x08    4     request body length
```

Metadata, NOP, TurboScan CAPS, TurboScan END, and process-list commands use zero-length request bodies. Memory-map, raw-memory-read, raw-memory-write, scan-authorization, TurboScan START/COUNT/GET, and process-control commands carry packed request bodies whose sizes are recorded in the command header. `CMD_PROC_WRITE` additionally streams the raw write bytes after the server accepts the packed request body. TurboScan START additionally streams zero, one, or two comparison operands when the mapped predicate requires them, followed by the selected multi-segment region list. Snapshot Unknown Initial Value uses START with no comparison payload plus snapshot/include-zero flags and receives a plan/progress/summary response. Resident TurboScan COUNT streams any Next Scan operands and returns a progress-sentinel/count sequence; TurboScan GET returns bounded resident result records.

The packet magic is:

```text
0xFFAABBCC
```

## Implemented Commands

| Purpose | Command | Response used by plugin |
| --- | --- | --- |
| Protocol version | `0xBD000001` | `uint32 length` + UTF-8 bytes |
| Firmware version | `0xBD000500` | little-endian `uint16` |
| Branding/capability level | `0xBD000501` | `uint32 length` + byte payload |
| Platform id | `0xBD000502` | little-endian `uint16` |
| Process list | `0xBDAA0001` | success status + `uint32 count` + `count × 36-byte entries` |
| Process memory read | `0xBDAA0002` | success status + requested raw bytes |
| Process memory write | `0xBDAA0003` | success status + client raw bytes + final success status |
| Process memory map | `0xBDAA0004` | success status + `uint32 count` + `count × 58-byte entries` |
| Process server-side stack read | `0xBDAA0023` | success status + `uint32 payload length` + variable stack-frame payload |
| Scan authorization | `0xBDAACCFF` | success + challenge + client response + final success |
| TurboScan capabilities | `0xBDAACC10` | success + 16-byte capabilities payload |
| TurboScan START | `0xBDAACC11` | compare mode: success + optional operand bytes + success + segment list + resident summary + final success; snapshot mode: plan/progress/summary + final success |
| TurboScan COUNT | `0xBDAACC12` | success + client comparison bytes + progress/sentinel + survivor count + final success |
| TurboScan GET | `0xBDAACC13` | success + result-count header + resident result records + final success |
| TurboScan END | `0xBDAACC14` | success status |
| Process suspend/resume (non-debugger scan control) | `0xBDBB0500` | success status |
| Debugger attach | `0xBDBB0001` | success / already-debug / error status |
| Debugger detach | `0xBDBB0002` | success status |
| Debugger software breakpoint | `0xBDBB0003` | 16-byte `{ index, enabled, address }` request; success or invalid-index/error status |
| Debugger hardware watchpoint | `0xBDBB0004` | 24-byte `{ index, enabled, length, breaktype, address }` request; success or invalid-index/error status |
| Debugger thread list | `0xBDBB0005` | success, `uint32 count`, then `count` x `uint32 thread id` |
| Debugger suspend thread | `0xBDBB0006` | success status; 4-byte thread-id body |
| Debugger resume thread | `0xBDBB0007` | success status; 4-byte thread-id body |
| Debugger stop-go | `0xBDBB0010` | success status; 4-byte body with action in byte 0 |
| Debugger thread info | `0xBDBB0011` | success, 40-byte `{ id, priority, tdname[32] }` record |
| Debugger native step | `0xBDBB0012` | no body; success status |
| Debugger native thread step | `0xBDBB0013` | 4-byte thread-id body; success status |
| Liveness / NOP | `0xBDAACC06` | wire status `0x80000000` for success |

The server-side success macro is bit-swapped before transmission by ps5debug-NG. The client therefore compares received command statuses against the documented wire value:

```text
0x80000000
```

## Process List Response

`CMD_PROC_LIST` has no request body.

The response is:

```text
uint32 status
uint32 count
proc_list_entry[count]
```

Each process entry is 36 bytes:

```text
Offset  Size  Field
0x00    32    char name[32]
0x20    4     int32 pid
```

The plugin reads the process list as one bounded payload after validating `count`. A defensive maximum of 16,384 process records is applied before allocation. This is not a ps5debug-NG functional limit; it is a client-side corruption/sanity guard.

Process names are decoded as UTF-8 up to the first NUL byte. A full 32-byte field without a NUL terminator is also accepted and decoded in full.

The wire PID is signed. Negative values are rejected as malformed protocol data. Valid PIDs are converted to the unsigned neutral `TargetProcess.Id` representation only after this validation.

## Process Memory Map Response

`CMD_PROC_MAPS` uses command id `0xBDAA0004`. Its request body is a packed 4-byte process id. ps5debug-NG documents that field as `uint32`; the plugin serializes the already validated non-negative 32-bit PID returned by process enumeration, which produces the same four little-endian wire bytes for the supported PID range. The command header therefore declares a request-body length of `4` instead of zero.

The response is:

```text
uint32 status
uint32 count
proc_vm_map_entry[count]
```

Each packed memory-map entry is 58 bytes:

```text
Offset  Size  Field
0x00    32    char name[32]
0x20     8    uint64 start
0x28     8    uint64 end
0x30     8    uint64 offset
0x38     2    uint16 prot
```

The client validates the count before allocating the response payload and applies a defensive maximum of 262,144 records. This is a client-side corruption guard rather than a documented ps5debug-NG functional limit.

The parser rejects entries whose `end` address is lower than `start`. Empty names are represented as `null` in the neutral model. The region size is calculated as `end - start`.

The `prot` field uses the low VM protection bits for read (`0x1`), write (`0x2`), and execute (`0x4`). Those bits are translated to the neutral `MemoryProtection` flags. Other upstream bits, if introduced later, are not guessed or mapped to unrelated neutral flags.

The upstream `offset` field is retained only in the PS5-specific parsed record for now. The current neutral `MemoryRegion` contract has no offset property and no current Core consumer requires it, so the value is deliberately not leaked into the host through a platform-specific extension.


## Raw Process Memory Read

`CMD_PROC_READ` uses command id `0xBDAA0002`. The request body is the documented packed 16-byte `cmd_proc_read_packet`:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 address
0x0C    4     uint32 length
```

The PS5 plugin uses the already validated non-negative 32-bit PID representation returned by process enumeration and serializes the same four little-endian bytes expected by the unsigned upstream field. The target address is serialized as a little-endian 64-bit value and the destination-buffer length as a little-endian 32-bit value.

The response is:

```text
uint32 status
byte data[length]
```

The success status must match the wire value `0x80000000`. The client then reads exactly `length` raw bytes into the destination supplied by the neutral `IMemoryReader` contract. ps5debug-NG may stream the response internally in 64 KiB chunks, but there is no count prefix or per-chunk header on the wire.

A zero-length destination is treated as a local no-op and does not send a protocol request. Manual WPF inspection currently limits a single request to 4096 bytes for usability; the PS5 protocol implementation itself does not impose that UI-specific cap.

## Raw Process Memory Write

`CMD_PROC_WRITE` uses command id `0xBDAA0003`. Its packed 16-byte request body has the same field layout as `CMD_PROC_READ`:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 address
0x0C    4     uint32 length
```

The transaction is not a single status followed by data. The documented command sequence is:

```text
client -> command header + packed 16-byte request body
server -> uint32 success status
client -> byte data[length]
server -> uint32 final success status
```

Both server statuses must equal the ps5debug-NG wire success value `0x80000000`. The first status acknowledges that the server accepted the write request and is ready to receive data. The second status confirms command completion after the payload has been consumed. The client reads both statuses before returning so the next command begins on a synchronized stream boundary.

The plugin writes exactly the caller-supplied neutral `ReadOnlyMemory<byte>` payload after the first acknowledgement and flushes the stream before waiting for completion. A zero-length source is handled as a local no-op and does not send `CMD_PROC_WRITE`.

The ps5debug-NG server implementation may process large transfers internally in 64 KiB chunks. Those chunks do not add per-chunk command headers to this client transaction. The current WPF manual-write inspector imposes a 4096-byte usability limit, but that is not an `IMemoryWriter` or protocol limit.


## Upstream Disassembly Analysis Command

Current ps5debug-NG defines `CMD_PROC_DISASM_REGION` (`0xBDAA0020`) and implements server-side instruction analysis with Zydis. The response uses fixed-size analysis records containing information such as instruction address, RIP-relative target, memory displacement, instruction length, flow kind, register/scale fields, and compact mnemonic metadata.

PS5 plugin `0.1.0.rev24` does **not** send this command for its Plugin API `2.11.0` `IDisassemblerProvider`. The neutral provider contract requires exact raw instruction bytes plus full mnemonic/operand presentation, while Core already performs the bounded readable-region memory read through `IMemoryReader`. Rev24 continues to decode those caller-supplied bytes locally with Iced `1.21.0` inside the PS5 plugin and additionally maps formatter text kinds to neutral syntax tokens.

This is an intentional separation rather than an unsupported-backend workaround. `CMD_PROC_DISASM_REGION` may be useful later for backend-side bulk analysis or xref-oriented features, but adopting it for such a feature must not create a second implicit target-I/O path for the normal neutral Disassembler workflow.

## Native Scan Mapping and TurboScan

PS5 plugin `0.1.0.rev25` retains the selected Core Scan Type mappings to current ps5debug-NG native operations that were verified in rev22. Core/WPF never sees the `compareType` values; they remain plugin protocol details.

### Value-type mapping

| Wire id | ps5debug-NG type | Memory Engine type | Width | Natural alignment |
| ---: | --- | --- | ---: | ---: |
| 0 | `valTypeUInt8` | `UInt8` | 1 | 1 |
| 1 | `valTypeInt8` | `Int8` | 1 | 1 |
| 2 | `valTypeUInt16` | `UInt16` | 2 | 2 |
| 3 | `valTypeInt16` | `Int16` | 2 | 2 |
| 4 | `valTypeUInt32` | `UInt32` | 4 | 4 |
| 5 | `valTypeInt32` | `Int32` | 4 | 4 |
| 6 | `valTypeUInt64` | `UInt64` | 8 | 8 |
| 7 | `valTypeInt64` | `Int64` | 8 | 8 |
| 8 | `valTypeFloat` | `Float32` | 4 | 4 |
| 9 | `valTypeDouble` | `Float64` | 8 | 8 |
| 10 | `valTypeArrBytes` | `ByteArray` | `lenData` | 1 |

Array of Bytes is currently exposed only as an exact sequence. The plugin sends a same-length mask containing `0x01` for every byte.

### Upstream compareType table

Current ps5debug-NG exposes `compareType 0..12`:

| Value | Upstream name | Memory Engine treatment |
| ---: | --- | --- |
| 0 | Exact Value | mapped when runtime shape is semantically compatible |
| 1 | Fuzzy Value | mapped for Float/Double |
| 2 | Bigger Than | mapped for numeric types subject to endianness checks |
| 3 | Smaller Than | mapped for numeric types subject to endianness checks |
| 4 | Value Between | mapped for numeric types; two operands |
| 5 | Increased Value | mapped for numeric Next Scan |
| 6 | Increased Value By | **not mapped** because target-width wrapping can differ from Core |
| 7 | Decreased Value | mapped for numeric Next Scan |
| 8 | Decreased Value By | **not mapped** because target-width wrapping can differ from Core |
| 9 | Changed Value | mapped for numeric Next Scan |
| 10 | Unchanged Value | mapped for numeric Next Scan |
| 11 | Unknown Initial Value | **not mapped directly** because it excludes zero values |
| 12 | Unknown Initial Low Value | mapped only for integer Value Types; floating semantics differ |

Core Unknown Initial Value is instead implemented through TurboScan snapshot mode with explicit zero inclusion.

### Capability probe

After the liveness probe, the client sends TurboScan CAPS (`0xBDAACC10`). The 16-byte payload is:

```text
uint32 version
uint32 engines
uint32 max_threads
uint32 reserved
```

The session exposes the established native scanner services only when `version >= 1` and the server advertises the resident/segment baseline. The plugin also records optional snapshot and rescan-aliasing bits. A non-success CAPS response is treated as native acceleration unavailable rather than a failed PS5 connection.

### Scan authorization

TurboScan requires process-scan authorization. Memory Engine sends `CMD_PROC_AUTH` (`0xBDAACCFF`) with:

```text
uint32 magic = 0xBB40E64D
uint32 flags = 0x00000002
```

The client completes the challenge/response exchange and caches successful authorization for the connection.

### TurboScan START — compare mode

The packed 27-byte body for `0xBDAACC11` is:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 address       // zero for segmented resident mode
0x0C    4     uint32 length        // zero for segmented resident mode
0x10    1     uint8 valueType
0x11    1     uint8 compareType    // selected by PS5 semantic mapping
0x12    1     uint8 alignment
0x13    4     uint32 lenData       // total operand bytes
0x17    4     uint32 flags
```

Mapped compare-style First Scan uses:

```text
TS_SERVER_RESIDENT   = 0x00000002
TS_SNAPSHOT_SEGMENTS = 0x00000010
flags                 = 0x00000012
```

After the first acknowledgement the client sends the operand bytes required by the Core predicate:

- one value for Exact/Fuzzy/Bigger/Smaller/Unknown Initial Low;
- two values for Between;
- no user value for predicates that are not valid as compare-style First Scan.

Array of Bytes Exact Value additionally sends its all-ones mask. After the second acknowledgement the client sends `uint32 segment_count` followed by packed `{ uint64 address; uint32 length; }` entries.

The server returns:

```text
uint32 resident_stored
uint64 count
CMD_SUCCESS
```

If resident storage is declined, the plugin consumes the documented fallback framing and reports native acceleration unavailable so Core can rescan through `IMemoryReader`.

### TurboScan START — Unknown Initial snapshot

Core Unknown Initial Value must include every fixed-width candidate, including zero. ps5debug-NG's ordinary `compareType 11` excludes zeros, so PS5 plugin `0.1.0.rev22` uses snapshot mode instead:

```text
compareType = 11
lenData     = 0
flags       = TS_SNAPSHOT |
              TS_SNAPSHOT_INCLUDE_ZEROS |
              TS_SNAPSHOT_SEGMENTS
```

After the segment list, the snapshot response is:

```text
uint64 slot_count
uint64 total_bytes
zero or more uint64 progress records
uint64 0xFFFFFFFFFFFFFFFF sentinel
uint32 snapshot_ok
uint64 survivor_count
CMD_SUCCESS
```

The progress list is sentinel-terminated and has no protocol-defined record-count ceiling. A large snapshot may emit more than 1,024 progress records when ps5debug-NG uses smaller fallback I/O windows, so the client consumes records until the sentinel rather than enforcing a host-side count limit. It then reads `snapshot_ok`, `survivor_count`, and the final status before validation. `snapshot_ok == 0` is treated as a synchronized native-resource refusal and raises `NotSupportedException`, allowing the host to use Core fallback without Disconnect/Connect. If a malformed summary nevertheless claims a stored resident session, cleanup retains enough ownership state to send TurboScan END before the exception escapes.

The snapshot session remains resident and can be narrowed by later mapped Next Scan predicates.

### TurboScan COUNT

Resident refinement uses `0xBDAACC12` with a packed 22-byte body:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 base_address  // zero for resident mode
0x0C    1     uint8 valueType
0x0D    1     uint8 compareType    // selected by PS5 semantic mapping
0x0E    4     uint32 lenData       // zero/one/two operand bytes
0x12    4     uint32 flags
```

The resident flag is always set. `TS_RESCAN_ALIASING` is added when advertised.

Next Scan operand examples:

- Changed/Unchanged/Increased/Decreased: `lenData = 0`;
- Exact/Fuzzy/Bigger/Smaller: one value;
- Between: two values.

For **Changed Value** (`compareType 9`) and **Unchanged Value** (`compareType 10`), Core defines change by the fixed-width bytes retained from the previous scan. The upstream Float/Double point comparator instead uses IEEE-754 `!=`/`==`; an unchanged NaN therefore satisfies `NaN != NaN` and is incorrectly retained by Changed Value. PS5 plugin `0.1.0.rev22` corrects the wire shape for those two predicates only:

```text
Selected Memory Engine type   TurboScan COUNT valueType
Float                         UInt32 (4)
Double                        UInt64 (6)
```

The integer comparator has the same width and performs exact bit equality/inequality. START snapshot storage, survivor addresses, previous-value bytes, alignment, and GET decoding remain Float/Double. Other Float/Double predicates continue to send wire ids 8/9.

The response is:

```text
zero or more uint64 progress records
uint64 0xFFFFFFFFFFFFFFFF sentinel
uint64 new_survivor_count
CMD_SUCCESS
```

The client rejects a count that grows relative to the previous resident count. If the resident PID, Value Type, width, alignment, or complete host survivor count no longer matches, the native session is closed and Core refinement is used instead.

### TurboScan GET

Resident results are fetched with `0xBDAACC13` and:

```text
uint32 start_index
uint32 count
uint32 flags
```

For selected value width `N`, the normal record shape is:

```text
uint64 address
byte current_value[N]
byte previous_value[N]
```

If response-header bit 31 is set, another `N` bytes contain the retained first value. The plugin calculates record stride dynamically and caps each temporary payload to approximately 4 MiB. API 2.2 streaming exposes bounded `NativeValueScanResultBatch` values. Plugin API `2.9.0` additionally allows an authoritative stream to expose `INativeValueScanResidentResultSet`, so the host can request a bounded GET window without enumerating the entire survivor set. The PS5 implementation uses that contract when the complete result count exceeds the 50,000-row presentation ceiling.

For a large authoritative resident First Scan, the host requests only the display preview and leaves the complete set in the target-side TurboScan session. A compatible mapped Next Scan uses COUNT directly against that resident generation, then GET retrieves only the new preview window. GET can include target-stored Previous values so the displayed `Previous` column remains meaningful without materializing the complete set locally.

If a later predicate requires Core fallback, the host deliberately enumerates the complete resident set through bounded GET windows, commits it to the normal disk-backed result store, ends the TurboScan resident session, and then runs shared Core refinement. This materialization is the point at which the historical full-transfer cost is paid.

START/COUNT defines survivor membership; GET transports requested windows of that resident set. The plugin therefore does not re-run each mapped predicate on GET records. The deliberate exception is default Strict Float/Double Exact First Scan, where the upstream Exact comparator is tolerant and returned records must be exact-filtered before they can be treated as authoritative Core results.

### Floating-point Exact versus Fuzzy Value

These are separate user-facing Core predicates:

- **Exact Value** defaults to strict equality. ps5debug-NG's Exact Float/Double comparator uses relative `1e-6` tolerance, so default Strict First Scan filters native results and Strict Next Scan uses Core fallback. The PS5 option `ps5debug-NG tolerance (1e-6)` deliberately opts into native Exact semantics.
- **Fuzzy Value** uses absolute difference strictly below `1.0` and maps to upstream `compareType 1` for Float/Double.

### Endianness

All command fields are little-endian. The host's Endianness option controls value encoding/decoding.

For multi-byte numeric predicates that interpret magnitude, ps5debug-NG native comparison is little-endian and Big Endian requests use Core fallback. Integer raw equality/inequality predicates such as Exact/Changed/Unchanged can remain native because byte equality is endian-independent. Snapshot Unknown Initial Value is also endian-independent. Native Float/Double comparisons use Core fallback for Big Endian.

### TurboScan END

`0xBDAACC14` releases the resident/snapshot session on New Scan/reset, incompatible refinement, cancellation/error cleanup, target replacement, or connection teardown.

Core receives only neutral addresses/value bytes through the public native scanner contracts. ps5debug-NG compare ids, flags, segment structures, and resident protocol state remain private to the PS5 plugin.

## Scan Option Protocol Semantics

TurboScan START has an explicit `uint8 alignment` field, so custom Alignment is sent directly to ps5debug-NG. Endianness and Floating-point rounding have no dedicated wire fields. The host/plugin use those selected options to decide value encoding and whether a mapped native operation is semantically safe.

## Process Suspend / Resume

PS5 plugin `0.1.0.rev16` implements neutral process suspend/resume through `CMD_DEBUG_PROCESS_STOP` (`0xBDBB0500`). ps5debug-NG documents this command as usable without an active debugger session.

The request body is exactly five raw bytes:

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

The response is one normal ps5debug-NG status word. A successful operation requires wire status `0x80000000`. State `2` (kill) is intentionally not exposed through the current neutral `IProcessControl` contract.

The WPF scan workflow uses these operations only when **Pause target while scanning** is enabled. The host performs resume from a cleanup path even after cancellation or scan failure.

## Debugger Attach, Control, Detach, and Async Events

PS5 plugin `0.1.0.rev38` retains the real debugger backend introduced in rev25, the rev26 thread enumeration/control transport, the rev27 verified general-register read, the rev29 guarded extended-register transport, and the rev32 verified software-breakpoint address validation. Rev30 added software execute breakpoints through command `0xBDBB0003`; rev33 added hardware data watchpoints through command `0xBDBB0004`; rev34 added server-side stack reads and native stepping; rev35 adds no new command id and instead reconciles the existing software-breakpoint interrupt packet with later live register state through `0xBDAA0023`, `0xBDBB0012`, and `0xBDBB0013`. Software execute breakpoints are still sent only after plugin-local validation confirms that the address is inside an executable, non-guarded region from the latest current-process memory-map snapshot. Hardware data watchpoints are likewise validated locally for mapped range, guard state, supported width, and natural alignment before a backend slot is changed. Rejected requests never reach the wire.

The command ownership model is now:

- the connection that performs `CMD_DEBUG_ATTACH` remains the debugger-owner command stream;
- Attach/Detach, Pause/Continue, thread list/info/control, and mandatory GETREGS continue on that owner stream;
- safe paused-state extended reads currently comprise GETFPREGS and GETFSGSBASE; they use optional one-command probe connections only when the identified backend advertises protocol `1.3` or newer and capability level `1.0` or newer;
- each safe optional probe has a two-second linked timeout and its socket is discarded afterward;
- a failed safe optional command is cached unavailable for the current debugger attachment while the other safe group remains independently refreshable;
- paused GETDBREGS is deliberately not sent in rev29 because the current backend can block when the target is already stopped.

This isolation matters because cancellation after command dispatch is not framing-safe on a connection that will be reused: the client cannot know how many bytes of a late response remain. Rev29 therefore does not time out the owner stream. It times out only disposable safe optional connections.

Breakpoint address validation does not open another connection and does not issue a debugger command. `Ps5TargetSession` caches the immutable neutral `MemoryRegion` list returned by the normal `IMemoryMapProvider` enumeration for the current process; `Ps5DebuggerSession` reads that plugin-private snapshot synchronously before slot allocation. This avoids adding concurrent traffic to either the general target client or the debugger-owner stream.

The current upstream server source uses one `server_client` per TCP connection. Only the connection that performs Attach is marked as the debugger-owning client; `free_client` invokes full debugger teardown only for a client whose `debugging` field is nonzero. Closing a non-owner optional probe connection therefore does not detach the active debugger session. Debug commands are still serialized by the backend's debugger/process mutexes, so any server-side handler that blocks while holding those locks can still stall other debugger commands even if the client socket is disposable.

### Current GETDBREGS stopped-target hazard

The rev10 source review identified a concrete unsafe sequence in the current ps5debug-NG GETDBREGS path. Debugger Pause sends `SIGSTOP` and waits for the stop event. That wait consumes the event. `debug_getdbregs_handle()` later uses `is_process_stopped()` to determine whether it must stop the process again, but the helper uses another non-blocking `wait4()` as the state test. If the earlier Pause already consumed the stop event, that second call can return no status even though the process is still stopped. GETDBREGS then sends another `SIGSTOP` and performs a blocking `wait4()` for a transition that may never occur.

Because the main server wraps debug commands with the shared debugger/process mutexes, a blocked GETDBREGS handler can also prevent Continue, Detach, and other debug commands from acquiring those locks. Closing the probe socket is not a reliable recovery for that server-side wait.

Rev29 therefore suppresses GETDBREGS from the paused register-refresh path. The 128-byte command mapping and decoder remain documented for future corrected backend/event use, but no current paused snapshot requests it. The external backend report is [`../../bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md`](../../bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md).

## Branding Payload

Current ps5debug-NG builds return branding as:

```text
<human-readable brand> NUL <capability level>
```

The plugin splits the payload at the first NUL byte. The human-readable portion must begin with `ps5debug-NG` for this backend plugin to accept the server.

The capability-level portion is parsed and retained internally. Rev29 uses it together with the protocol version as a plugin-private eligibility gate for safe optional debugger register probes. It still does not change the public coarse `RegisterAccess` capability by itself.

## Firmware Value

The firmware command returns a decimal-packed `uint16` representation such as:

```text
900   -> 9.00
1240  -> 12.40
```

The current connection layer records the raw returned value. Firmware-specific feature gating has not been introduced yet.

## Timeout and Cancellation

Connection establishment and the identification handshake use a five-second timeout. A caller-provided cancellation request takes precedence over the timeout.

Process enumeration and memory-map enumeration continue to use the session caller's cancellation token directly.

Raw process-memory reads and writes have a stricter transaction boundary from PS5 plugin `0.1.0.rev6`. Native scans and process-control commands follow the same stream-integrity principle from plugin `0.1.0.rev7`; rev8 extends it to resident COUNT refinement; rev9 generalizes the same transaction framing to variable-width comparison data and Array of Bytes masks; rev10 preserves that protocol behavior while adding explicit Plugin API scanner capability declarations; plugin rev13 keeps the same wire framing while changing GET consumption to bounded API 2.2 result batches. A caller cancellation request is checked before a framed/streamed transaction begins. Once a command has been sent, the client consumes the complete operation before returning where abandoning the response would leave the TCP stream between protocol records:

```text
CMD_PROC_READ:
request -> success status -> exact requested payload

CMD_PROC_WRITE:
request -> first success -> exact payload -> final success

CMD_PROC_AUTH:
request -> success -> challenge -> response -> final success

TurboScan START:
request -> success -> comparison payload -> success -> segment list
        -> resident summary -> final success

TurboScan COUNT:
request -> success -> comparison payload -> progress/sentinel -> survivor count -> final success

TurboScan GET:
request -> success -> count header + exact result records -> final success

TurboScan END:
request -> success

CMD_DEBUG_PROCESS_STOP:
request(pid + state) -> success status

Dedicated debugger commands (separate command socket):
CMD_DEBUG_ATTACH -> success status
CMD_DEBUG_CONTINUE -> success status
CMD_DEBUG_DETACH -> success status
CMD_DEBUG_SET_BREAKPOINT -> success/invalid-index/error status
CMD_DEBUG_SET_WATCHPOINT -> success/invalid-index/error status
```

The in-flight transport portion therefore uses a non-cancellable token. This is intentional protocol preservation, not ignored cancellation. Live `0.1.2.rev1` scanner testing showed that cancelling `NetworkStream.ReadExactlyAsync` mid-read left target payload bytes on the shared command TCP stream; the next process-list request then parsed those bytes as an unexpected status. Disconnect -> Connect fixed the symptom only by replacing the stream.

The shared Core scanner still owns normal cancellation. After the current PS5 command finishes and its response is fully drained, Core observes the already-cancelled token before it can issue the next read. This makes **Cancel Scan** safe for session reuse while keeping all PS5 framing rules inside the plugin.

## Deliberately Not Implemented Yet

This protocol layer does not yet send:

- foreground-application wire commands (preferred `eboot.bin` selection is derived from the already implemented process list through the neutral plugin service);
- process-info commands;
- process-authentication flags other than the scan authorization needed by TurboScan;
- AOB/native byte-pattern scan commands;
- assembler/disassembler commands;
- kernel or other console commands.

Those commands should be added only when the corresponding PS5 plugin capability is implemented.


## Debugger Register Mapping (rev27-rev29)

Host `0.1.7.rev8` completed verification of the PS5 plugin rev27 read-only paused-thread general-register snapshot. Rev28 retains that mandatory command unchanged:

```text
CMD_DEBUG_GET_REGISTERS = 0xBDBB0008
request body: uint32 lwpid
response: CMD_SUCCESS + 176-byte amd64 struct reg
```

The current ps5debug-NG source handles the request with `PT_GETREGS` for the supplied LWP id and returns `REG_BLOB_SIZE` bytes. The PS5 mapper follows the FreeBSD amd64 `struct reg` order used by the payload SDK: R15..RAX, trap/segment/error fields, RIP, CS, RFLAGS, RSP, and SS. RIP/RSP/RBP are translated to neutral semantic roles; the host never receives native offsets or LWP terminology.

Rev28 added mappings for three extended selected-thread commands. Each request uses the same four-byte `uint32 lwpid` body and, after a success status, returns one fixed-size payload:

| Group | Command | Payload | Backend path | Neutral rows | Rev29 paused-use status |
| --- | --- | ---: | --- | --- | --- |
| Floating point / SIMD | `0xBDBB000A` (`GETFPREGS`) | 832 bytes (`0x340`) | kernel helper with `PT_GETFPREGS` fallback | FCW, FSW, FTW, FOP, FIP, FDP, MXCSR, MXCSR_MASK, ST0-ST7, XMM0-XMM15, YMM0-YMM15 | guarded disposable probe |
| Debug registers | `0xBDBB000C` (`GETDBREGS`) | 128 bytes (`0x80`) | firmware-dependent kernel helper or `PT_GETDBREGS` | DR0-DR3, DR6, DR7 | **suppressed while Paused** because the current stop-detection path can block |
| Segment bases | `0xBDBB000E` (`GETFSGSBASE`) | 16 bytes (`0x10`) | `kern_get_fsgsbase` | FSBASE, GSBASE | guarded disposable probe |

The 832-byte block matches the current amd64 `savefpu_ymm`/xstate layout used by ps5debug-NG. The plugin-private offsets used by rev28 are:

```text
0x000  32 bytes   FPU/SSE environment
0x020  8 x 16     x87 stack slots (10-byte value + padding)
0x0A0  16 x 16    XMM0-XMM15 lower 128-bit values
0x200  64 bytes   xstate header
0x240  16 x 16    YMM0-YMM15 upper 128-bit values
```

Each neutral YMM row is reconstructed as the corresponding 16-byte XMM lower half followed by its 16-byte xstate upper half. x87 stack values are preserved as exact 80-bit raw numeric state. The 128-byte debug block contains sixteen 64-bit slots; only architectural DR0-DR3, DR6, and DR7 are published, leaving reserved slots private. FSBASE and GSBASE are read as two little-endian 64-bit values. All architecture-specific offsets and names remain inside the PS5 plugin.

The general-register block is mandatory for a successful snapshot. Rev29 automatically probes only the FPU/SIMD and FS/GS-base groups when the backend metadata is eligible. Ordinary backend `CMD_ERROR` or `CMD_DATA_NULL`, a bounded timeout, connection failure, or probe-local malformed/truncated response omits only the affected safe group. GETDBREGS is not treated as an ordinary optional probe in the paused workflow because its current server-side stop/wait sequence can block while holding shared debugger locks.

When both safe optional reads succeed, the paused PS5 register service exposes 76 rows: 26 general + 48 FPU/SIMD + 2 FS/GS. The six mapped debug-register rows remain unavailable through the paused command path until corrected backend behavior is hardware-verified.

The related SETREGS (`0xBDBB0009`), SETFPREGS (`0xBDBB000B`), SETDBREGS (`0xBDBB000D`), and FS/GS write (`0xBDBB000F`) commands are not exposed by the current rev28 verification candidate. Their target-side behavior has not been accepted through the project's live safety gate, so every PS5 register row remains read-only even though ps5debug-NG contains write handlers.


## Debugger Software Breakpoint Command

`CMD_DEBUG_SET_BREAKPOINT` is command `0xBDBB0003`. The request body is exactly 16 bytes:

```text
Offset  Size  Field
0x00    4     uint32 slot index
0x04    4     uint32 enabled (0 or 1)
0x08    8     uint64 address
```

Current ps5debug-NG accepts indices `0-29`. Enable stores the address/original byte in backend debugger context and writes `0xCC` at the requested address. Disable restores the saved byte and clears the backend slot. The slot count, INT3 encoding, and cleanup mechanics are PS5-plugin details and do not cross into the neutral API.

A breakpoint hit is delivered through the normal 1184-byte interrupt packet. Current upstream `dispatch_debug_events()` restores the original byte, subtracts one from the **packet** RIP, applies that rewound RIP to the stopped thread, issues `PT_STEP`, waits for that step to complete, rearms the `0xCC`, and only afterward sends the packet. Consequently the event packet remains a logical pre-instruction snapshot even though a later GETREGS observes post-instruction state. Live rev24 isolation reproduced this on `0xE9F747` (event `0xE9F747`, live `0xE9F74C`) and on `0xE9F74E call 0x1A3BC30` (event `0xE9F74E`, live callee `0x1A3BC30`). Rev35 still matches managed hits against the logical event RIP, caches the packet GP/FPU blocks for matching register/top-frame inspection, and consumes the already-completed transparent step if the host requests Step Into from that logical stop. The external backend report is [`../../bug-reports/ps5debug-ng-software-breakpoint-event-and-live-register-state-diverge.md`](../../bug-reports/ps5debug-ng-software-breakpoint-event-and-live-register-state-diverge.md).

### Current paused-disable caveat

The current server disable branch calls `ptrace_raw(PT_CONTINUE, pid, (void *)1, 0)` after restoring the original byte. A breakpoint Disable/Remove request can therefore resume a target that was already stopped for unrelated user inspection. The current PS5 plugin does not send an enabled-breakpoint disable/removal while the neutral session is Paused; it stages the cleanup and sends it immediately before the next user-requested Continue. The external backend report is [`../../bug-reports/ps5debug-ng-disabling-software-breakpoint-resumes-paused-target.md`](../../bug-reports/ps5debug-ng-disabling-software-breakpoint-resumes-paused-target.md).

Rev36 keeps that normal paused-management rule unchanged and adds a teardown-only exception. Once detach/disposal has begun, the client explicitly disables every software-breakpoint slot still marked backend-active, plus any slot already staged for paused cleanup, before issuing `CMD_DEBUG_DETACH`. The event callback remains connected during the restore pass so any complete packets produced by the backend's resume side effect are drained; packets received while detach is active are ignored for session state/event projection, and the callback is closed after the detach attempt. The detach command is still sent if one of those defensive restore requests reports an error so ps5debug-NG's own `debug_full_teardown()` remains the final backend cleanup opportunity. This sequence does not add a protocol command and remains entirely inside the PS5 plugin.

The current backend teardown itself has a separate sparse-slot issue: its software-breakpoint restore loop exits on the first zero-address slot instead of checking the remaining indexed slots. Because the normal disable path clears a slot address, an active breakpoint after a hole can be skipped by backend detach cleanup. The source-level issue is documented in [`../../bug-reports/ps5debug-ng-debugger-detach-stops-breakpoint-restore-at-first-empty-slot.md`](../../bug-reports/ps5debug-ng-debugger-detach-stops-breakpoint-restore-at-first-empty-slot.md). Rev36's explicit client-owned slot restore pass avoids relying on that contiguous-slot assumption.

## Debugger Hardware Watchpoint Command

`CMD_DEBUG_SET_WATCHPOINT` is command `0xBDBB0004`. The packed request body is exactly 24 bytes:

```text
Offset  Size  Field
0x00    4     uint32 slot index
0x04    4     uint32 enabled (0 or 1)
0x08    4     uint32 length encoding
0x0C    4     uint32 break type
0x10    8     uint64 address
```

Current ps5debug-NG accepts hardware slot indices `0-3`, corresponding to DR0-DR3. The server clears the selected slot's DR7 enable/type/length bits before applying a new value, writes the watched address when enabled, and applies the resulting debug-register block to every current LWP with `PT_SETDBREGS` or the firmware-specific kernel path. If the handler has to stop a running process to enumerate/apply thread state, it resumes that process before returning. When the target is already stopped, the normal successful path does not issue an extra Continue.

The DR7 length encoding used by the current server/client protocol is:

| Requested width | Wire `length` |
| ---: | ---: |
| 1 byte | `0` |
| 2 bytes | `1` |
| 4 bytes | `3` |
| 8 bytes | `2` |

The data-access encoding used by rev33 is:

| Neutral access | Wire `breaktype` | Notes |
| --- | ---: | --- |
| Write | `1` | Break on writes |
| ReadWrite | `3` | Break on reads or writes |
| Read | not exposed | amd64 DR7 has no true read-only data-watchpoint encoding; use ReadWrite when reads must be observed |

The PS5 plugin keeps those encodings private. Before slot allocation it requires a width of 1, 2, 4, or 8 bytes, natural alignment to that width, and a complete watched range inside one current mapped non-guarded memory region. Execute hardware breakpoints are not exposed by rev17; software execute breakpoints remain the established execute-breakpoint path.

Hardware watchpoint hits arrive on the normal 1184-byte asynchronous debugger packet. The debug-register block begins at packet offset `0x420`; DR6 is therefore at packet offset `0x450`. When DR6 preserves B0-B3, rev33 maps the asserted hardware slot back to the managed watchpoint exactly. The neutral event keeps the interrupt instruction pointer as `InstructionPointer` and carries the watched data address in `TriggeredBreakpoint.Request.Address`, so callers do not have to misuse the instruction pointer as the watched address. No paused `GETDBREGS` command is required for this mapping.

### Current DR6 watchpoint-attribution limitation

The current ps5debug-NG event dispatcher copies the debug-register block into the outgoing packet and then clears `pkt_dr[6]` for the debug exception path before later watchpoint processing. The subsequent cleanup saves/restores that already-cleared packet value. As a result, a client can receive a watchpoint interrupt with DR6 B0-B3 all zero even though a hardware slot caused the stop.

Rev33 handles that external limitation conservatively:

- when DR6 identifies a slot, the hit is attributed exactly;
- when DR6 is zero and exactly one managed hardware watchpoint is enabled, the plugin can safely infer that single watchpoint and emits a Watchpoint event with an explanatory message;
- when DR6 is zero and more than one managed hardware watchpoint is enabled, the plugin does not guess. It emits a generic signal/other stop explaining that exact attribution is unavailable and does not apply temporary-watchpoint cleanup to an arbitrary slot.

This limitation is documented for the upstream project in [`../../bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md`](../../bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md). Rev16 live acceptance verified the conservative single-watchpoint path and the required multi-watchpoint no-guess behavior. Multiple simultaneous watchpoints remain usable only to the extent that the backend preserves DR6 slot status on the tested firmware/backend build.

## Debugger Call Stack and Native Step Commands

### `CMD_PROC_READ_STACK`

Rev34 maps server-side stack reading through process command `0xBDAA0023`. The request body is packed and exactly 24 bytes:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 rbp
0x0C    8     uint64 rsp
0x14    4     uint32 depth
```

Current upstream limits depth to 64, frame-local bytes to `0x1000`, and the optional code window to 200 bytes. A successful response is `CMD_SUCCESS`, a `uint32` payload length, and a payload beginning with `uint32 frame_count`. Each backend frame then carries 44 fixed bytes followed by variable locals/code data:

```text
uint64 current_rbp
uint64 current_rsp
uint64 saved_rbp
uint64 return_address
uint32 flags
uint32 locals_length
uint32 code_length
byte   locals[locals_length]
byte   code[code_length]
```

The server walks the RBP chain itself. The plugin validates the outer payload length, frame count, every fixed header, maximum variable lengths, truncation, and final consumption before mapping any neutral frames. Locals and code-window payloads are skipped because current `DebuggerStackFrame` does not expose them. The first neutral frame uses the selected thread's current RIP read through the established register path; later instruction addresses use the preceding backend return address. Stack/frame pointers and optional return/module values are then published without exposing RBP-chain rules to Core/WPF.

### `CMD_DEBUG_STEP` and `CMD_DEBUG_STEP_THREAD`

Rev34 maps both native step commands:

```text
CMD_DEBUG_STEP        = 0xBDBB0012   body: none
CMD_DEBUG_STEP_THREAD = 0xBDBB0013   body: uint32 lwpid
```

Both return one ordinary ps5debug-NG status word. The current server uses `PT_STEP`; the thread variant records the requested LWP id and performs the step against that thread. The host normally supplies the selected neutral debugger thread, so the PS5 plugin uses `CMD_DEBUG_STEP_THREAD`. LWP terminology and 32-bit wire ids remain plugin-private.

After a successful ordinary request the plugin records a pending native step, publishes Running, and waits for the normal TCP 755 asynchronous interrupt. A matching signal-5 trap becomes neutral `StepCompleted` only after managed software-breakpoint and hardware-watchpoint attribution have been checked. Rev35 does not issue this command when Step Into is requested from a matching logical software-breakpoint snapshot because the server has already transparently performed that step before sending the event packet; the plugin instead reads the resulting live RIP and emits one neutral Resumed/StepCompleted transition. Manual Pause, Continue, detach, disposal, new interrupt receipt, and stale-session cleanup clear the special snapshot/pending step state. Step Over, Step Out, and Run to Address are host-composed and do not introduce additional PS5 wire commands.

## Known External Thread-Control Issue

During live rev6 acceptance, `CMD_DEBUG_SUSPEND_THREAD` (`0xBDBB0006`) returned the on-wire `CMD_ERROR` value `0xF0000001` for an LWP id obtained from the same debugger attachment's thread list. No crash or connection loss occurred. See [`../../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md`](../../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md) for the complete reproduction.


### Rev37 validation-only note

Plugin rev37 does not add or alter a ps5debug-NG opcode. `IDebuggerBreakpointValidationService` is satisfied entirely from the plugin's existing request shape rules, current cached memory-map information, active/staged breakpoint/watchpoint records, and local slot allocation state. A successful validation is only a preflight hint to generic UI; the existing `0xBDBB0003` / `0xBDBB0004` add paths repeat authoritative validation before writing backend state.

## Host 0.1.7.rev32 / PS5 0.1.0.rev39 — Watchpoint Stop RIP Semantics

No ps5debug-NG wire command changes in this revision. For hardware data watchpoints, the interrupt packet's RIP is retained as the debugger stop/current instruction pointer. On x86-64 this can be the instruction following the memory access that triggered the debug trap. The plugin therefore no longer labels callback RIP as the accessing instruction. The host resolves a separate trigger address from logical disassembly when possible; unresolved cases remain explicit. The existing DR6 attribution limitations and the documented same-instruction software-breakpoint/watchpoint event-consumption behavior are unchanged.
