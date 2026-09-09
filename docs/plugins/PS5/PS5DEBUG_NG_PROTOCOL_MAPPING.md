# ps5debug-NG Protocol Mapping

## Scope

This document records the ps5debug-NG wire-protocol subset currently implemented by the PlayStation 5 plugin. The implemented subset covers target identification/liveness, process enumeration, process memory-map enumeration, raw process-memory reads and writes, TurboScan capability negotiation/authentication, mapped server-resident comparison scans, snapshot-based Unknown Initial Value, resident Next Scan refinement, process suspend/resume control, and the first real debugger transport. The completed `0.1.6.rev14` Disassembler block remains fully hardware-verified. Host `0.1.7.rev1` added only neutral debugger contracts/Core lifecycle coordination, and `0.1.7.rev2` added the generic Debugger workspace plus deterministic Mock backend. Host `0.1.7.rev3` / PS5 plugin `0.1.0.rev25` introduced the Plugin API `2.12.0` debugger consumer, coarse `Debugger` capability, dedicated debugger attach/pause/continue/detach commands, ps5debug-NG outbound async debugger channel on TCP `755`, and fixed 1184-byte interrupt mapping into neutral debugger events. Host `0.1.7.rev5` fully hardware-verified that debugger transport. Host `0.1.7.rev6` / PS5 plugin `0.1.0.rev26` preserved it and added debugger thread enumeration plus individual-thread Suspend/Resume through the API `2.12.0` attached-session services. Current host `0.1.7.rev7` / PS5 plugin `0.1.0.rev27` targets API `2.13.0` and additionally maps paused-thread general-register snapshots through `CMD_DEBUG_GET_REGISTERS` while keeping PS5 register writes disabled. The existing neutral PS5 disassembly path still decodes caller-supplied bytes locally and does not send the upstream server-side disassembly command.

The implementation was initially reviewed against the public ps5debug-NG repository at commit:

```text
d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86
```

The process-list implementation was additionally checked against the public ps5debug-NG `PROTOCOL.md` definition for `CMD_PROC_LIST`, which defines a success status, a `uint32` count, and fixed-size `proc_list_entry` records containing `char name[32]` and `int32_t pid`. The raw-memory-write mapping was rechecked against current upstream protocol/source before the rev16 implementation. TurboScan capability, authorization, multi-segment resident START/GET/END, and process-control mappings were rechecked during rev4 preparation on 2026-08-31; resident COUNT/refinement and rescan-aliasing were rechecked during rev5; the full value-type table and Array of Bytes mask behavior were rechecked during `0.1.3.rev1`. For `0.1.3.rev26`, `PROTOCOL.md`, `scan_compare.c`, and `scan_turbo.c` were rechecked again for `compareType 0..12`, operand requirements, resident refinement, snapshot/include-zero behavior, and the semantics used to decide which Core Scan Types may be mapped natively. For `0.1.7.rev3`, current `PROTOCOL.md`, `debugger/source/debug.c`, and the FreeBSD amd64 register layout were rechecked for `CMD_DEBUG_ATTACH`, `CMD_DEBUG_DETACH`, `CMD_DEBUG_CONTINUE`, the TCP `755` callback sequence, the 1184-byte interrupt layout, and the instruction-pointer offset used by the plugin parser.

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
| Scan authorization | `0xBDAACCFF` | success + challenge + client response + final success |
| TurboScan capabilities | `0xBDAACC10` | success + 16-byte capabilities payload |
| TurboScan START | `0xBDAACC11` | compare mode: success + optional operand bytes + success + segment list + resident summary + final success; snapshot mode: plan/progress/summary + final success |
| TurboScan COUNT | `0xBDAACC12` | success + client comparison bytes + progress/sentinel + survivor count + final success |
| TurboScan GET | `0xBDAACC13` | success + result-count header + resident result records + final success |
| TurboScan END | `0xBDAACC14` | success status |
| Process suspend/resume (non-debugger scan control) | `0xBDBB0500` | success status |
| Debugger attach | `0xBDBB0001` | success / already-debug / error status |
| Debugger detach | `0xBDBB0002` | success status |
| Debugger thread list | `0xBDBB0005` | success, `uint32 count`, then `count` x `uint32 thread id` |
| Debugger suspend thread | `0xBDBB0006` | success status; 4-byte thread-id body |
| Debugger resume thread | `0xBDBB0007` | success status; 4-byte thread-id body |
| Debugger stop-go | `0xBDBB0010` | success status; 4-byte body with action in byte 0 |
| Debugger thread info | `0xBDBB0011` | success, 40-byte `{ id, priority, tdname[32] }` record |
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

PS5 plugin `0.1.0.rev27` retains the real debugger backend introduced in rev25, the rev26 thread enumeration/control transport, and adds read-only general-register snapshots through the same debugger-owned command stream. The debugger command stream is intentionally separate from the established target/memory/scan connection and from the concurrent Frozen-write connection.

### Attach sequence

Before sending attach, the plugin starts an IPv4 listener on local TCP port `755`. It then opens a dedicated command connection to the configured ps5debug-NG command port and sends:

```text
CMD_DEBUG_ATTACH = 0xBDBB0001
body: int32 pid
```

Current ps5debug-NG records the requesting client's address and, during attach, opens an outbound TCP connection back to that address on port `755`. The plugin requires that event connection to arrive within five seconds. If attach succeeded but the event connection cannot be established, cleanup attempts `CMD_DEBUG_DETACH` before closing the dedicated command transport.

The relevant wire status values are:

```text
Success:       0x80000000
Error:         0xF0000001
Data null:     0xF0000003
Already debug: 0xF0000004
```

### Pause and Continue

Debugger execution control uses the current stop-go command, not the separate scan/process-control command:

```text
CMD_DEBUG_CONTINUE = 0xBDBB0010
body size: 4 bytes
byte 0 = action

action 0 = resume
action 1 = stop / pause
action 2 = kill
```

Memory Engine rev3 exposes only actions `1` and `0` as debugger Pause and Continue. Kill remains unexposed. The existing non-debugger `IProcessControl` implementation continues to use `CMD_DEBUG_PROCESS_STOP` (`0xBDBB0500`) for the independent **Pause target while scanning** workflow.

### Thread Enumeration and Individual Thread Control

Rev6 uses the current ps5debug-NG debugger-thread command family only after a debugger attachment exists. All requests stay serialized on the dedicated debugger command connection.

Thread enumeration:

```text
CMD_DEBUG_GET_THREAD_LIST = 0xBDBB0005
body: none
response: status, uint32 count, count x uint32 thread id
```

The plugin defensively rejects counts above 65,536 before allocating the returned id set. For each id it then attempts:

```text
CMD_DEBUG_THREAD_INFO = 0xBDBB0011
body: uint32 thread id
response after success status: 40 bytes
  uint32 thread id
  uint32 priority
  char   tdname[32]
```

The 32-byte name is decoded as NUL-terminated UTF-8. A thread-info failure does not remove an otherwise valid id from the neutral list; the row is retained with no optional name. Priority remains plugin-private in rev6.

Individual control uses:

```text
CMD_DEBUG_SUSPEND_THREAD = 0xBDBB0006
body: uint32 thread id
response: status

CMD_DEBUG_RESUME_THREAD = 0xBDBB0007
body: uint32 thread id
response: status
```

The public SDK receives only neutral unsigned thread ids. The host never sees ps5debug-NG's LWP terminology or these command ids. The current protocol does not return a per-thread running/suspended flag with list/info, so the plugin maps unsuspended rows from the whole debugger execution state and remembers only individual Suspends/Resumes that it successfully issued. Every fresh enumeration reconciles that local suspended-id set against the ids still returned by the backend. The host permits individual control only while the whole debugger session is `Running`.

### Detach

Explicit debugger detach sends:

```text
CMD_DEBUG_DETACH = 0xBDBB0002
body: none
```

The plugin then closes/cancels its TCP 755 event channel and dedicated debugger command connection. Window/session disposal performs the same ownership cleanup on a best-effort basis so a later debugger session can bind port `755` and attach again.

### Async interrupt packet

Current ps5debug-NG sends fixed-size **1184-byte (`0x4A0`)** debugger interrupt packets over the outbound TCP 755 connection. The debugger backend consumes only the fields needed by the current neutral Debugger workspace:

```text
Offset  Size  Field
0x000     4   uint32 lwp/thread id
0x004     4   uint32 wait status
0x008    40   thread-name storage
0x030   176   amd64 general-register block
0x0E0   832   floating/SIMD state
0x420   128   debug-register block
```

The amd64 instruction pointer is at offset `0x88` inside the 176-byte general-register block, so its absolute packet offset is `0x0B8`. The plugin parses that 64-bit value and exposes it only as neutral `DebuggerEvent.InstructionPointer`; the FreeBSD register layout does not cross into Plugin SDK/Core/WPF.

The current signal is derived from the wait status as `(status >> 8) & 0xFF`. A nonzero signal maps to neutral `DebuggerStopReason.Signal`; a zero signal maps to `Backend`. Thread id and thread name are included when available.

### Execution-state semantics after an interrupt

The verified debugger backend maps a received async interrupt to neutral **Paused** state. This was rechecked against current ps5debug-NG source before packaging: `dispatch_debug_events` sends the 1184-byte packet and then calls its application-layer resume helper, but it does not issue `PT_CONTINUE` for the traced stop after sending the packet. The debugger stop therefore remains pending until the client sends stop-go action `0`/Continue. Treating the event as Running would make the host enable the wrong controls and lose the stop context.

The server suppresses its ordinary stop signal used by explicit pause from the async event stream, so the plugin emits its own neutral `PauseRequested` event after a successful explicit Pause command. Continue similarly emits a neutral Resumed event after the backend acknowledges action `0`.

### Event-channel failure

Unexpected EOF/socket loss on TCP `755` while the debugger is otherwise attached moves the plugin session to neutral `Unknown` and raises a backend event. This deliberately disables further Pause/Continue state assumptions while leaving explicit Detach available for deterministic cleanup. Intentional detach/disposal suppresses that diagnostic.

## Branding Payload

Current ps5debug-NG builds return branding as:

```text
<human-readable brand> NUL <capability level>
```

The plugin splits the payload at the first NUL byte. The human-readable portion must begin with `ps5debug-NG` for this backend plugin to accept the server.

The capability-level portion is parsed and retained internally for future feature negotiation. It is not yet used to advertise Memory Engine plugin capabilities.

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
```

The in-flight transport portion therefore uses a non-cancellable token. This is intentional protocol preservation, not ignored cancellation. Live `0.1.2.rev1` scanner testing showed that cancelling `NetworkStream.ReadExactlyAsync` mid-read left target payload bytes on the shared command TCP stream; the next process-list request then parsed those bytes as an unexpected status. Disconnect -> Connect fixed the symptom only by replacing the stream.

The shared Core scanner still owns normal cancellation. After the current PS5 command finishes and its response is fully drained, Core observes the already-cancelled token before it can issue the next read. This makes **Cancel Scan** safe for session reuse while keeping all PS5 framing rules inside the plugin.

## Deliberately Not Implemented Yet

This protocol layer does not yet send:

- foreground-application wire commands (preferred `eboot.bin` selection is derived from the already implemented process list through the neutral plugin service);
- process-info commands;
- process-authentication flags other than the scan authorization needed by TurboScan;
- AOB/native byte-pattern scan commands;
- advanced debugger thread/register/breakpoint/watchpoint/call-stack/step commands beyond the rev3 attach/control/event subset;
- assembler/disassembler commands;
- kernel or other console commands.

Those commands should be added only when the corresponding PS5 plugin capability is implemented.


## Debugger General Register Mapping (rev27)

Host `0.1.7.rev7` / PS5 plugin `0.1.0.rev27` adds read-only paused-thread general-register snapshots. The command stays plugin-private:

```text
CMD_DEBUG_GET_REGISTERS = 0xBDBB0008
request body: uint32 lwpid
response: CMD_SUCCESS + 176-byte amd64 struct reg
```

The current ps5debug-NG source handles the request with `PT_GETREGS` for the supplied LWP id and returns `REG_BLOB_SIZE` bytes. The PS5 mapper follows the FreeBSD amd64 `struct reg` order used by the payload SDK: R15..RAX, trap/segment/error fields, RIP, CS, RFLAGS, RSP, and SS. RIP/RSP/RBP are translated to neutral semantic roles; the host never receives native offsets or LWP terminology.

The related SETREGS command is not exposed by Memory Engine in rev7. Its target-side behavior has not been hardware-verified, so PS5 register rows remain read-only even though ps5debug-NG contains a SETREGS handler.

## Known External Thread-Control Issue

During live rev6 acceptance, `CMD_DEBUG_SUSPEND_THREAD` (`0xBDBB0006`) returned the on-wire `CMD_ERROR` value `0xF0000001` for an LWP id obtained from the same debugger attachment's thread list. No crash or connection loss occurred. See [`../../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md`](../../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md) for the complete reproduction.
