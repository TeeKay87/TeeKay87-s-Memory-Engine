# ps5debug-NG Protocol Mapping

## Scope

This document records the ps5debug-NG wire-protocol subset currently implemented by the PlayStation 5 plugin. The implemented subset covers target identification/liveness, process enumeration, process memory-map enumeration, raw process-memory reads and writes, TurboScan capability negotiation/authenticated server-resident Exact Value First Scan and Next Scan refinement, and process suspend/resume control.

The implementation was initially reviewed against the public ps5debug-NG repository at commit:

```text
d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86
```

The process-list implementation was additionally checked against the public ps5debug-NG `PROTOCOL.md` definition for `CMD_PROC_LIST`, which defines a success status, a `uint32` count, and fixed-size `proc_list_entry` records containing `char name[32]` and `int32_t pid`. The raw-memory-write mapping was rechecked against current upstream protocol/source before the rev16 implementation. TurboScan capability, authorization, multi-segment resident START/GET/END, and process-control mappings were rechecked against current public ps5debug-NG source/protocol during rev4 preparation on 2026-08-31. TurboScan resident COUNT/refinement and optional rescan-aliasing flags were rechecked against the same current protocol during rev5 preparation. The complete scan value-type table and Array of Bytes mask behavior were rechecked against current `PROTOCOL.md`, `scan_compare.c`, and `scan_turbo.c` during `0.1.3.rev1` preparation.

Relevant upstream references include `PROTOCOL.md`, `common/include/protocol.h`, `debugger/source/meta.c`, `debugger/source/proc.c`, `debugger/source/auth.c`, `debugger/source/scan.c`, and `debugger/source/scan_turbo.c`.

No ps5debug-NG source file is embedded in TeeKay87's Memory Engine. The PS5 plugin is an independent C# client implementation of the documented wire protocol.

### Source precedence note

For this implementation, the referenced server source and `PROTOCOL.md` are treated as authoritative when examples elsewhere in the upstream repository disagree with them. In the connection subset, VERSION, BRANDING, PLATFORM_ID, and FW_VERSION send their payloads directly without a leading status word, while NOP sends `CMD_SUCCESS`. `CMD_PROC_LIST` uses the process-command convention and sends a success status before its count and entries.

## Transport

The implemented command channel uses TCP port `744` by default.

Every request currently sent by the plugin uses the standard 12-byte little-endian command header:

```text
Offset  Size  Field
0x00    4     packet magic
0x04    4     command id
0x08    4     request body length
```

Metadata, NOP, TurboScan CAPS, TurboScan END, and process-list commands use zero-length request bodies. Memory-map, raw-memory-read, raw-memory-write, scan-authorization, TurboScan START/COUNT/GET, and process-control commands carry packed request bodies whose sizes are recorded in the command header. `CMD_PROC_WRITE` additionally streams the raw write bytes after the server accepts the packed request body. TurboScan START additionally streams the comparison value and the selected multi-segment region list after its acknowledgements; resident TurboScan COUNT streams the new comparison value and returns a progress-sentinel/count sequence; TurboScan GET returns bounded resident result records.

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
| TurboScan START | `0xBDAACC11` | success + client comparison bytes + success + client segment list + resident summary + final success |
| TurboScan COUNT | `0xBDAACC12` | success + client comparison bytes + progress/sentinel + survivor count + final success |
| TurboScan GET | `0xBDAACC13` | success + result-count header + resident result records + final success |
| TurboScan END | `0xBDAACC14` | success status |
| Process suspend/resume | `0xBDBB0500` | success status |
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


## Native Exact Value Scan

PS5 plugin `0.1.0.rev9` accelerates First Scan for all eleven supported scan value types and compatible integer/Array-of-Bytes Next Scan refinement through ps5debug-NG TurboScan rather than the legacy single-pass `CMD_PROC_SCAN` command. Float/Double Next Scan uses Core fallback because ps5debug-NG resident refinement is fuzzy for floating-point values.

### Value-type mapping

Memory Engine exposes exactly the scan value types defined by current ps5debug-NG:

| Wire id | ps5debug-NG type | Memory Engine type | Width | Alignment |
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

Array of Bytes is currently exposed as an exact sequence. For wire type 10, Memory Engine sends the value bytes followed by a same-length mask containing `0x01` for every byte. This deliberately does not expose wildcard syntax yet.

### Capability probe

After the existing liveness probe, the connection sends TurboScan CAPS (`0xBDAACC10`). A successful 16-byte capability payload contains:

```text
uint32 version
uint32 engines
uint32 max_threads
uint32 reserved
```

The session exposes `INativeValueScanner` only when `version >= 1` and `engines` contains both `TSE_SERVER_RESIDENT` (`0x00000004`) and `TSE_SNAPSHOT_SEGMENTS` (`0x00000010`). A non-success CAPS response is treated as acceleration unavailable rather than a failed PS5 connection.

### Scan authorization

TurboScan requires process-scan authorization. Memory Engine sends `CMD_PROC_AUTH` (`0xBDAACCFF`) with `{ uint32 magic = 0xBB40E64D; uint32 flags = 0x00000002; }`, completes the documented challenge/response exchange, and caches successful authorization for the connection.

### TurboScan START

The plugin sends `0xBDAACC11` with the packed 27-byte body:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 address       // zero in segmented resident mode
0x0C    4     uint32 length        // zero in segmented resident mode
0x10    1     uint8 valueType      // table above
0x11    1     uint8 compareType    // 0 = Exact Value
0x12    1     uint8 alignment      // selected type alignment
0x13    4     uint32 lenData       // selected value width
0x17    4     uint32 flags         // 0x12
```

`flags = TS_SERVER_RESIDENT | TS_SNAPSHOT_SEGMENTS` (`0x12`). After the first success acknowledgement the client sends the comparison bytes and, for Array of Bytes, its all-ones mask. After the second acknowledgement it sends `uint32 segment_count` followed by `{ uint64 address, uint32 length }` records.

Core supplies the normal readable/non-Guarded region set. Segment starts follow the selected alignment. Segment splitting is based on candidate-start ranges and includes the full selected value width, so a multi-byte Array of Bytes candidate is not lost merely because a very large region must be divided to fit the protocol's `uint32 length` field.

The server then returns `{ uint32 resident_stored; uint64 count; }` and a final success status. If resident storage is declined, the plugin consumes the defined fallback framing and lets the host use shared Core scanning.

### TurboScan COUNT

A retained resident set is narrowed with `0xBDAACC12`. Its 22-byte request carries the same selected `valueType`, Exact Value comparison id `0`, dynamic `lenData`, and `TS_SERVER_RESIDENT`. `TS_RESCAN_ALIASING` is added when advertised. Memory Engine uses this resident COUNT path for integer and exact Array-of-Bytes types. Float/Double are intentionally not narrowed through COUNT in Exact Value mode because current ps5debug-NG routes their resident equality comparison through fuzzy floating-point semantics; the plugin ends the resident session and requests Core refinement instead.

After the acknowledgement, the client sends the new comparison bytes plus the all-ones mask for Array of Bytes. The resident response remains:

```text
zero or more uint64 progress records
uint64 0xFFFFFFFFFFFFFFFF sentinel
uint64 new_survivor_count
CMD_SUCCESS
```

GET is then used to fetch the narrowed absolute survivor addresses.

### TurboScan GET

Resident results are fetched with `0xBDAACC13` and `{ uint32 start_index; uint32 count; uint32 flags; }`.

Record width is no longer assumed to be Int32. For a selected value width `N`, the normal record is:

```text
uint64 address
byte current_value[N]
byte previous_value[N]
```

If header bit 31 indicates retained first values, another `N` bytes follow. Memory Engine calculates record size dynamically and caps each temporary GET payload to approximately 4 MiB while retaining the existing per-request record-count cap. The common native-result safety ceiling remains 2,000,000 addresses.

### TurboScan END

`0xBDAACC14` releases the per-connection resident scan session when New Scan/reset, an incompatible refinement, cancellation/error cleanup, or connection teardown requires it.

Core receives neutral absolute addresses through `INativeValueScanner`/`INativeValueScanRefiner` and still validates selected alignment, region containment, duplicates, result limits, and host-side Current/Previous presentation.

## Process Suspend / Resume

PS5 plugin `0.1.0.rev9` implements neutral process suspend/resume through `CMD_DEBUG_PROCESS_STOP` (`0xBDBB0500`). ps5debug-NG documents this command as usable without an active debugger session.

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

Raw process-memory reads and writes have a stricter transaction boundary from PS5 plugin `0.1.0.rev6`. Native scans and process-control commands follow the same stream-integrity principle from plugin `0.1.0.rev7`; rev8 extends it to resident COUNT refinement; rev9 generalizes the same transaction framing to variable-width comparison data and Array of Bytes masks. A caller cancellation request is checked before a framed/streamed transaction begins. Once a command has been sent, the client consumes the complete operation before returning where abandoning the response would leave the TCP stream between protocol records:

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
```

The in-flight transport portion therefore uses a non-cancellable token. This is intentional protocol preservation, not ignored cancellation. Live `0.1.2.rev1` scanner testing showed that cancelling `NetworkStream.ReadExactlyAsync` mid-read left target payload bytes on the shared command TCP stream; the next process-list request then parsed those bytes as an unexpected status. Disconnect -> Connect fixed the symptom only by replacing the stream.

The shared Core scanner still owns normal cancellation. After the current PS5 command finishes and its response is fully drained, Core observes the already-cancelled token before it can issue the next read. This makes **Cancel Scan** safe for session reuse while keeping all PS5 framing rules inside the plugin.

## Deliberately Not Implemented Yet

This protocol layer does not yet send:

- foreground-application wire commands (preferred `eboot.bin` selection is derived from the already implemented process list through the neutral plugin service);
- process-info commands;
- process-authentication flags other than the scan authorization needed by TurboScan;
- AOB/native byte-pattern scan commands;
- debugger attach or async interrupt-channel commands;
- assembler/disassembler commands;
- kernel or other console commands.

Those commands should be added only when the corresponding PS5 plugin capability is implemented.
