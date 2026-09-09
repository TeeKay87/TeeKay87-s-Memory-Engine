# PlayStation 5 Plugin

## Purpose

`TeeKay87.MemoryEngine.Platform.PS5` is the PlayStation 5 platform plugin for TeeKay87's Memory Engine. It communicates with a jailbroken PlayStation 5 running ps5debug-NG and keeps all ps5debug-NG wire-protocol behavior outside the shared Core and WPF application.

This directory contains documentation that belongs specifically to the PS5 plugin.

## Plugin Identity

| Property | Value |
| --- | --- |
| Plugin id | `platform.ps5.ps5debug-ng` |
| Plugin version | `0.1.0.rev27` |
| Plugin API | `2.13.0` |
| Platform | PlayStation 5 |
| Backend | ps5debug-NG |
| Architecture | x64, 64-bit pointers, little-endian |

The plugin version/revision is independent from the TeeKay87's Memory Engine host version and from the ps5debug-NG server version.

Host application `0.1.7.rev7` uses public Plugin API `2.13.0` and advances the PS5 plugin to `0.1.0.rev27`. The rev3 debugger transport carried through the intervening UI revisions was fully hardware-verified in rev5, including Attach/Detach/Pause/Continue, TCP 755 callback ownership, transport isolation, cleanup, reconnection-generation safety, and multiple-window exclusivity. Rev6 preserves those verified paths and adds the first real PS5 consumers of `IDebuggerThreadService` and `IDebuggerThreadControlService` through the existing dedicated debugger command connection.

## Current Capability Scope

The PS5 plugin currently advertises:

- `Connect`;
- `ProcessEnumeration`;
- `ForegroundProcess`;
- `MemoryRegionEnumeration`;
- `MemoryRead`;
- `MemoryWrite`;
- `ProcessSuspend`;
- `ProcessResume`;
- `NativeValueScanning`;
- `Disassembly`;
- `Debugger`;
- `ThreadEnumeration`;
- `ThreadControl`;
- `RegisterAccess`.

These capability flags describe functionality actually exposed by this plugin revision. ps5debug-NG supports additional commands, but Memory Engine must not advertise those capabilities until the corresponding neutral service/UI path exists and has been verified.

Disassembly remains advertised because the connected session exposes the verified neutral `IDisassemblerProvider`. Rev27 retains the verified debugger/thread services and adds `RegisterAccess`: while the target is Paused, the selected thread can expose a read-only general-register snapshot through the dedicated debugger command transport. Breakpoints/watchpoints, call stacks, stepping, assembly/instruction editing, pointer scanning, and cheat operations remain unadvertised. Register writing is intentionally not exposed in rev7 because the upstream SETREGS path has not yet been hardware-verified.

## Debugger General Registers

PS5 plugin `0.1.0.rev27` implements `IDebuggerRegisterService` for paused-thread general-register snapshots. The plugin sends ps5debug-NG `CMD_DEBUG_GET_REGISTERS` (`0xBDBB0008`) with the selected 32-bit backend thread id and reads the 176-byte amd64 register block after a successful status. The FreeBSD `struct reg` offsets are decoded entirely inside the PS5 plugin and converted to neutral `DebuggerRegister` rows.

RIP, RSP, and RBP are tagged with the neutral InstructionPointer, StackPointer, and FramePointer roles. WPF uses those roles rather than architecture-specific names. The values are marked `UnsignedLittleEndian`. All PS5 rows are read-only in rev7; the plugin deliberately does not expose SETREGS until that backend path has been separately reviewed and hardware-verified. Floating-point/SIMD and debug-register blocks remain deferred to rev8.

Rev6 live verification also found that the current tested ps5debug-NG backend returns on-wire `CMD_ERROR` (`0xF0000001`) for the documented individual-thread suspend request even when the LWP id came directly from thread enumeration. The failure did not crash the game, console, or debugger session. The reproduction and server-code notes are recorded in [`../../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md`](../../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md). Memory Engine keeps the existing ThreadControl client path so it can be retested against a corrected payload without redesigning the wire request.

## x86-64 Disassembly

The current PS5 plugin `0.1.0.rev27` retains the `IDisassemblerProvider` implementation introduced in rev24 under Plugin API `2.11.0` and continues to advertise `TargetCapabilities.Disassembly`. The provider is deliberately decode-only: Core obtains a bounded readable-region byte window through the existing `IMemoryReader` service, then passes those bytes to the plugin for architecture-specific interpretation.

The implementation uses Iced `1.21.0` inside the PS5 plugin to decode 64-bit little-endian x86-64 instructions and format operands using NASM-style text. It returns the neutral instruction model used by Core, including direct branch/call destinations when statically known. Rev24 also maps Iced formatter text kinds to the optional Plugin API `2.11.0` Mnemonic/FlowControlMnemonic/Register/Number/Keyword/Text presentation tokens; no Iced enum or x86 register list crosses into Core/WPF. Register- or memory-indirect calls/jumps are classified without inventing a destination. Invalid or truncated bytes remain bounded invalid instruction records.

Current ps5debug-NG also has a Zydis-based server-side disassembly-region command. That command was reviewed for this revision but is not used by the normal `IDisassemblerProvider` path because its analysis-record response does not provide the same complete raw-byte and formatted operand representation required by the neutral contract. No additional ps5debug-NG command is sent when the provider decodes a Core-supplied byte window.

See [`PS5_DISASSEMBLY_IMPLEMENTATION.md`](PS5_DISASSEMBLY_IMPLEMENTATION.md) for the backend decision, flow-control mapping, dependency deployment, instruction-boundary limitations, and verification scope.

## Connection Settings

The plugin defines its own connection fields through the Plugin SDK:

| Setting | Required | Default | Description |
| --- | --- | --- | --- |
| `host` | Yes | none | IP address or host name of the PS5 running ps5debug-NG |
| `port` | Yes | `744` | ps5debug-NG main command-server TCP port |

The WPF application renders these definitions generically. It does not contain PS5-specific host or port fields.

Plugin `0.1.0.rev27` continues to use the optional Plugin API `2.3.0` `IPluginSettingsConsumer` contract. Core attaches a settings scope owned by `platform.ps5.ps5debug-ng` before the host reads these connection definitions. If `connection.host` and `connection.port` contain remembered valid values, the plugin exposes them as the connection-field defaults. After a connection successfully completes, the plugin writes the normalized host and port back through `IPluginSettings`. Failed connection attempts do not replace the previous successful values. The plugin never opens or parses `settings.json` itself.

## Connection Validation

A connection is treated as established only after the plugin has:

1. opened a TCP connection to the configured endpoint;
2. read the ps5debug-NG protocol version;
3. verified that `CMD_PLATFORM_ID` reports PlayStation 5 platform id `5`;
4. read the branding payload and verified the `ps5debug-NG` identity;
5. read the running firmware value;
6. completed a `CMD_PROC_NOP` liveness probe and received the wire success status.

If any step fails, no connected target session is returned.

## Process Enumeration

The session exposes the neutral `IProcessProvider` service. Process enumeration uses `CMD_PROC_LIST` (`0xBDAA0001`), validates the returned count, parses 36-byte process entries internally, rejects negative PIDs, and returns only neutral `TargetProcess` objects to the host.

The raw ps5debug-NG process structure never leaves the PS5 plugin.

From PS5 plugin `0.1.0.rev8`, the session also exposes the existing neutral `IForegroundProcessProvider` service and advertises `ForegroundProcess`. For this backend, the preferred game process is the process named `eboot.bin` (case-insensitive) when present. The host uses that neutral service only to preselect the **Target Process** row after enumeration; it does not automatically set or replace the Active Target, and a manual user selection is preserved on later Refresh operations when that process still exists.

## Memory Map Enumeration

The session exposes `IMemoryMapProvider`. For an Active Target, the plugin uses `CMD_PROC_MAPS` (`0xBDAA0004`) and parses the backend's packed 58-byte map records internally.

The PS5 read/write/execute protection bits are translated to neutral `MemoryProtection.Read`, `MemoryProtection.Write`, and `MemoryProtection.Execute` flags. The upstream backing-object `offset` is parsed by the plugin but is not exposed because the current neutral `MemoryRegion` contract has no shared consumer for it.

The host receives only `IReadOnlyList<MemoryRegion>`.

## Raw Memory Read and Write

The session exposes the existing neutral `IMemoryReader` and `IMemoryWriter` services.

Raw reads use `CMD_PROC_READ` (`0xBDAA0002`). Raw writes use `CMD_PROC_WRITE` (`0xBDAA0003`) and consume both documented success acknowledgements around the write-data phase.

From PS5 plugin `0.1.0.rev6`, already-started read/write commands are completed as protocol transactions before caller cancellation is observed. This preserves the shared TCP command stream after **Cancel Scan** and prevents leftover response bytes from being interpreted as the next command's status.

The raw memory diagnostic code remains available for development/regression use, but application `0.1.3.rev18` no longer exposes the Raw Memory Read/Write panels in the main workspace. Their former UI limits and Int32-focused input behavior are not limits of the Plugin SDK memory-access contracts.

### Concurrent Frozen Writes

Plugin API `2.4.0` adds the optional `IConcurrentMemoryWriter` marker contract. PS5 plugin `0.1.0.rev27` exposes this service through the unchanged dedicated `Ps5ConcurrentMemoryWriter`. It lazily opens a second fully validated ps5debug-NG connection on the first in-scan Frozen write and serializes writes on that connection. The normal session's primary `Ps5DebugClient` remains responsible for TurboScan and all previously verified command flows.

The host uses this concurrent writer only when a Frozen row must be reapplied while First/Next Scan is active and **Pause target while scanning** is Off. When Pause is On, Frozen writes are intentionally suppressed for the duration of the paused scan. The secondary client is disposed with the target session.

## Scanner Capability Declarations

Plugin `0.1.0.rev27` targets Plugin API `2.13.0` and supplies the concrete Value Types and PS5-specific Scan Options that the current implementation can execute. `SupportedValueTypes` contains eleven `IMemoryValueType` objects: UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float32, Float64, and ByteArray. `SupportedScanOptions` contains Endianness, Alignment, and Floating-point rounding.

Core owns the universal 13-mode Scan Type catalog. The PS5 plugin does not publish its own production Scan Type list. Instead, the connected session implements `INativeScanTypeMappingProvider` and publishes only the Core predicates that have semantically equivalent ps5debug-NG native implementations for the declared First/Next stages and Value Types. The mapping's native id is opaque to Core/WPF; compare ids, TurboScan flags, and request construction remain inside this plugin.

The three option definitions also implement the optional API `2.8.0` presentation/applicability companions. Endianness maps generic toggle checked/unchecked states to the existing `little`/`big` choice ids; Alignment uses the default choice-list presentation; Floating-point rounding reports Scan Type applicability only for Core Exact Value. The WPF host therefore needs no PS5 id/name condition to render or hide these controls.

The current semantic mapping set is:

| Core Scan Type | PS5 native operation | Native stage(s) | Native Value Types | Core fallback boundary |
| --- | --- | --- | --- | --- |
| Exact Value | `compareType 0` | First + Next | Numeric + Array of Bytes | strict Float/Double Next and incompatible big-endian shapes |
| Fuzzy Value | `compareType 1` | First + Next | Float, Double | unsupported runtime/native capability shapes |
| Bigger Than | `compareType 2` | First + Next | Numeric | big-endian multi-byte magnitude comparisons |
| Smaller Than | `compareType 3` | First + Next | Numeric | big-endian multi-byte magnitude comparisons |
| Between | `compareType 4` | First + Next | Numeric | big-endian multi-byte magnitude comparisons |
| Unknown Initial Value | TurboScan snapshot + include zeros | First | Fixed-width numeric | missing/refused snapshot engine |
| Unknown Initial Low Value | `compareType 12` | First | Integer types | Float/Double use Core because upstream uses absolute magnitude |
| Increased Value | `compareType 5` | Next | Numeric | big-endian multi-byte magnitude comparisons |
| Decreased Value | `compareType 7` | Next | Numeric | big-endian multi-byte magnitude comparisons |
| Changed Value | `compareType 9` | Next | Numeric | Float/Double COUNT uses same-width UInt32/UInt64 wire type; runtime resident-state mismatch |
| Unchanged Value | `compareType 10` | Next | Numeric | Float/Double COUNT uses same-width UInt32/UInt64 wire type; runtime resident-state mismatch |
| Increased By | none | Next | Numeric | always Core; ps5debug-NG width-wrapping arithmetic is not fully equivalent |
| Decreased By | none | Next | Numeric | always Core; ps5debug-NG width-wrapping arithmetic is not fully equivalent |

The plugin deliberately does **not** map Core Unknown Initial Value directly to ps5debug-NG `compareType 11`, because that comparator excludes zero-valued candidates. TurboScan snapshot mode with `TS_SNAPSHOT_INCLUDE_ZEROS` preserves Core's true snapshot semantics instead.

Changed Value and Unchanged Value are snapshot-byte predicates in Core: they compare the fixed-width bytes stored by the previous scan. ps5debug-NG's native Float/Double `compareType 9/10` uses IEEE-754 `!=`/`==`, which makes an unchanged NaN compare as changed. Plugin `0.1.0.rev22` keeps these predicates native without transferring the resident set to Core by sending TurboScan COUNT with wire `UInt32` for Float and `UInt64` for Double. The width is identical, so ps5debug-NG compares the same four/eight bytes while GET data continues to be decoded as the user's selected Float/Double Value Type. Other floating-point scan predicates retain their normal floating wire type.


## Native Value Scan Acceleration

The Scan panel renders the plugin's three scan options generically through Plugin API `2.8.0` presentation/applicability metadata. Endianness is presented as **Little-endian byte order**: checked selects Little Endian and unchecked selects Big Endian, with Little Endian checked by default. Toggle Scan Options are grouped with the host's Pause-target checkbox rather than rendered as choice lists. Alignment remains a choice list and defaults to the active Value Type's natural alignment, with explicit 1/2/4/8/16/32/64/128-byte choices. Floating-point rounding is visible only when the selected Value Type is Float/Double and the selected Core Scan Type is Exact Value; it defaults to Strict with an opt-in `ps5debug-NG tolerance (1e-6)` mode. Endianness and Alignment lock after First Scan because they affect session interpretation/shape. Floating-point rounding does not lock, allowing the Exact Value comparison mode to be selected before any later Exact Next Scan.

PS5 plugin `0.1.0.rev27` advertises `NativeValueScanning`. After runtime TurboScan capability negotiation, the connected session exposes legacy `INativeValueScanner`/`INativeValueScanRefiner`, API 2.2 streaming `INativeValueScanStreamProvider`/`INativeValueScanStreamRefiner`, API 2.7 `INativeScanTypeMappingProvider`, and API 2.9 resident result handles from authoritative native streams through `INativeValueScanResidentResultSet`. If the required TurboScan capabilities are absent, the native scanner services are hidden and the host uses the shared Core scanner. If the mapping provider is available but no semantic mapping matches the selected Core Scan Type/stage/Value Type, the host skips native execution and goes directly to Core.

The PS5 wire value-type set remains:

| Wire id | Value type | Width / alignment |
| ---: | --- | --- |
| 0 | UInt8 | 1 / 1 |
| 1 | Int8 | 1 / 1 |
| 2 | UInt16 | 2 / 2 |
| 3 | Int16 | 2 / 2 |
| 4 | UInt32 | 4 / 4 |
| 5 | Int32 | 4 / 4 |
| 6 | UInt64 | 8 / 8 |
| 7 | Int64 | 8 / 8 |
| 8 | Float | 4 / 4 |
| 9 | Double | 8 / 8 |
| 10 | Array of Bytes | variable / 1 |

Array of Bytes remains an exact sequence using an all-ones ps5debug-NG mask and is limited to 4,096 bytes for the resident TurboScan path. Wildcard/masked AOB input remains future UI work.

The connection probes TurboScan CAPS (`0xBDAACC10`) and requires protocol version 1+, `TSE_SERVER_RESIDENT`, and segment support before exposing the native scanning services. The client also records optional snapshot and rescan-aliasing engines. Supported sessions authorize scanning through `CMD_PROC_AUTH`.

Mapped compare-style First Scans use multi-segment TurboScan START (`0xBDAACC11`) with `TS_SERVER_RESIDENT | TS_SNAPSHOT_SEGMENTS`, then GET (`0xBDAACC13`) to retrieve survivor records. The request builder now sends zero, one, or two operand payloads according to the Core predicate: Changed/Unchanged/Increased/Decreased need no comparison bytes, normal direct predicates use one operand, and Between uses two.

Core Unknown Initial Value uses a separate native START shape: `TS_SNAPSHOT | TS_SNAPSHOT_INCLUDE_ZEROS | TS_SNAPSHOT_SEGMENTS`. The client consumes the snapshot plan, the complete sentinel-terminated progress stream, summary, and final status before exposing the resident survivor stream. There is no fixed host-side progress-record limit: a large target can legitimately exceed 1,024 progress records when ps5debug-NG falls back to smaller snapshot I/O windows. If the snapshot engine is unavailable or the server cannot allocate the snapshot, the fully framed response is drained first and the plugin returns `NotSupportedException` so the host can perform the Core snapshot on the same synchronized connection. A failed native snapshot must not require Disconnect/Connect. Later compatible Next Scans can refine a successful snapshot through the same resident COUNT/GET mechanism.

Native Next Scan uses TurboScan COUNT (`0xBDAACC12`) when a semantically mapped predicate is selected and the resident target-side session still matches the host's complete resident result count, PID, Value Type, width, alignment, and generation. When `TSE_RESCAN_ALIASING` is advertised, the plugin opts into `TS_RESCAN_ALIASING`. A compatible COUNT creates a replacement resident generation; GET then loads only the requested preview window. If the selected predicate cannot remain native, Core first materializes the complete current resident set into the shared disk-backed format and then performs the existing Core refinement.

Strict Float/Double **Exact Value** remains a special boundary because ps5debug-NG's Exact comparator uses relative `1e-6` tolerance. Strict First Scan exact-filters the native GET records before commit, and strict Exact Next Scan uses Core refinement. The explicit `ps5debug-NG tolerance (1e-6)` option preserves native Exact refinement. **Fuzzy Value** is a separate Core predicate with absolute-difference `< 1.0` semantics and maps to ps5debug-NG `compareType 1`.

Native numeric comparisons that interpret multi-byte magnitude are little-endian in ps5debug-NG. Big-endian Bigger/Smaller/Between/Increased/Decreased therefore use Core fallback. Integer raw equality/inequality predicates such as Exact/Changed/Unchanged can remain native because byte equality is endian-independent. Unknown Initial snapshot is also endian-independent because it stores raw fixed-width slots and explicitly includes zero.

The streaming path is not constrained by the former two-million host list-materialization ceiling. Plugin API `2.9.0` lets a successful authoritative TurboScan stream expose `INativeValueScanResidentResultSet`. When the complete count is above the 50,000-row presentation ceiling, the host keeps the set resident on the PS5 and requests only the bounded preview through GET instead of immediately committing every survivor to local disk. TurboScan START/COUNT remains authoritative for membership; GET transports only requested windows and can include current/previous values for Next Scan preview rows.

**New Scan**, Active Target replacement, incompatible native refinement, and scan failure/cancellation cleanup request resident-session release through `INativeValueScanRefiner.ResetAsync`; disconnect also releases server state. The plugin does not use legacy `CMD_PROC_SCAN` (`0xBDAA0009`).

Cancellation remains cooperative at protocol boundaries. Once START/COUNT/GET has begun, the client consumes the complete current response before observing cancellation so the shared TCP command stream cannot be left misaligned.

See [`NATIVE_SCAN_AND_PROCESS_CONTROL.md`](NATIVE_SCAN_AND_PROCESS_CONTROL.md) and [`PS5DEBUG_NG_PROTOCOL_MAPPING.md`](PS5DEBUG_NG_PROTOCOL_MAPPING.md) for the detailed mapping and wire behavior.

## Process Suspend and Resume

PS5 plugin `0.1.0.rev27` also exposes `IProcessControl` and advertises both `ProcessSuspend` and `ProcessResume`.

The implementation uses ps5debug-NG `CMD_DEBUG_PROCESS_STOP` (`0xBDBB0500`) with the packed five-byte body:

```text
uint32 pid
uint8  state
```

Current states used by Memory Engine:

```text
0 = resume
1 = suspend / stop
```

ps5debug-NG documents this command as working without an active debugger session.

The WPF Scan panel exposes **Pause target while scanning** only when both process-control capabilities exist. The option is Off by default. When enabled, the host suspends the Active Target immediately before First Scan or Next Scan and attempts to resume it from a `finally` path after success, cancellation, or ordinary failure.

The pause workflow is host orchestration through the neutral interface; no ps5debug command id appears in App/Core.

## Debugger Transport and Session

PS5 plugin `0.1.0.rev25` was the first PS5 revision to consume Plugin API `2.13.0` debugger contracts through `IDebuggerProvider` / `IDebuggerSession`. Current plugin `0.1.0.rev27` keeps that verified transport and additionally advertises `TargetCapabilities.ThreadEnumeration` and `TargetCapabilities.ThreadControl`. Register access, breakpoints, watchpoints, call-stack access, and step execution remain intentionally unadvertised.

The connected target session exposes `IDebuggerProvider`. The provider owns one active debugger attachment and uses a dedicated `Ps5DebuggerCommandClient`. The debugger command connection is separate from both the primary `Ps5DebugClient` and the concurrent Frozen-write client, so debugger attach/control traffic cannot interleave with an in-flight memory/scan transaction.

### Attach / detach and TCP 755

Current ps5debug-NG requires the debugger client to listen on TCP `755` before attachment because the server opens an outbound connection to the client's IP for async interrupts. Rev25 therefore starts the local listener first, opens the dedicated command connection, sends `CMD_DEBUG_ATTACH` (`0xBDBB0001`) with the signed 32-bit PID, and accepts the outbound event socket. A successful session begins in neutral `Running` state.

Explicit detach uses `CMD_DEBUG_DETACH` (`0xBDBB0002`). Window/session disposal performs best-effort detach and always tears down the local event/command sockets. Only one debugger session can be active for a connected PS5 target session at a time.

### Pause / continue

Attached execution control uses `CMD_DEBUG_CONTINUE` / stop-go (`0xBDBB0010`). Its four-byte body uses the first byte as action:

```text
0 = resume
1 = stop / pause
2 = kill
```

Memory Engine exposes actions `0` and `1` through the neutral Debugger workspace; kill is not exposed. This is deliberately separate from the non-debugger `IProcessControl` implementation below, which continues to use `CMD_DEBUG_PROCESS_STOP` (`0xBDBB0500`).

### Async interrupt translation

The outbound event channel sends fixed 1184-byte packets. Rev25 currently extracts only:

- LWP/thread id at `0x000`;
- wait status at `0x004`;
- thread name from the 40-byte field at `0x008`;
- instruction pointer from the GP-register block at absolute offset `0x0B8`.

The PS5 plugin maps those fields to neutral `DebuggerEvent` data and never exposes the FreeBSD register/FPU/debug-register structs to Core or WPF. An interrupt leaves the neutral session `Paused`. Current ps5debug-NG resumes the application layer after sending the packet but does not `PT_CONTINUE` the traced process at that point; the ptrace stop remains until debugger Continue sends stop-go action `0`.

If the event channel disconnects unexpectedly while the backend remains attached, the neutral session becomes `Unknown` and emits a backend diagnostic event so run/pause controls cannot pretend the execution state is known. Detach remains the cleanup path.

### Thread enumeration and individual-thread control

Rev26 implements the already-public API `2.13.0` optional attached-session thread services:

- `IDebuggerThreadService` through `CMD_DEBUG_GET_THREAD_LIST` (`0xBDBB0005`) plus optional `CMD_DEBUG_THREAD_INFO` (`0xBDBB0011`) lookups;
- `IDebuggerThreadControlService.SuspendThreadAsync` through `CMD_DEBUG_SUSPEND_THREAD` (`0xBDBB0006`);
- `IDebuggerThreadControlService.ResumeThreadAsync` through `CMD_DEBUG_RESUME_THREAD` (`0xBDBB0007`).

The plugin maps backend thread ids and optional names into neutral `DebuggerThreadInfo` rows. The host never receives FreeBSD LWP terminology, wire structs, command ids, or priority fields. Current ps5debug-NG list/info replies do not provide a reliable individual execution-state field, so rev26 derives ordinary Running/Stopped presentation from the whole debugger session and tracks successful individual Suspend/Resume operations it owns. Per-thread control is accepted only while the overall debugger target is Running.

Register services, breakpoints/watchpoints, call-stack access, and step execution remain later PS5 debugger revisions.

## Command-Stream Safety

ps5debug-NG uses one shared framed TCP command stream. Memory Engine therefore treats multi-stage/streamed operations conservatively:

- raw read: once sent, consume status + requested bytes;
- raw write: once sent, consume both status words and complete data phase;
- TurboScan: once a START/COUNT/GET transaction begins, consume its complete response before observing cancellation; close resident state with END when it can no longer be safely reused;
- process control: once sent, consume the command status.

Caller cancellation is checked before beginning these transactions. Where abandoning a response would leave unread protocol data, cancellation is observed only after the current command reaches a clean boundary.

## Session Services

The PS5 target session currently exposes:

```text
IProcessProvider
IForegroundProcessProvider
IMemoryMapProvider
IMemoryReader
IMemoryWriter
IConcurrentMemoryWriter
INativeValueScanner
INativeValueScanRefiner
INativeValueScanStreamProvider
INativeValueScanStreamRefiner
INativeValueScanResidentResultSet (returned by authoritative TurboScan streams)
INativeScanTypeMappingProvider
IProcessControl
IDisassemblerProvider
IDebuggerProvider
```

`IConcurrentMemoryWriter` is supplied by the dedicated secondary Frozen-write transport. `IDebuggerProvider` is supplied by a separate debugger-owned command/event transport introduced in rev25. The four native scan execution services are exposed only when TurboScan is available at runtime; `INativeScanTypeMappingProvider` remains available as semantic metadata and tells the host which Core predicates are eligible for those native services.

## Verification Status

Live testing completed before rev4 established:

- real PS5 connection: PASS;
- process enumeration: PASS;
- memory-map enumeration: PASS;
- raw memory read: PASS;
- raw memory write/read-back: PASS;
- Safe Write Test: PASS twice;
- shared First Scan finding/editing a real money value: PASS;
- Cancel Scan followed by Refresh/New Scan on the same connection: PASS;
- rev3 generic PS5 First Scan for Int32 `10002`: 266 results in approximately `07:04.3`;
- subsequent Next Scan: effectively instant.

Rev4 was subsequently verified on Windows and a physical PS5: all 16 deterministic checks passed; native First Scan completed in approximately `15.1 s` in one measured run and `14.5 s` in another run with 35,278 matches; the reported rev4 Core Next Scan took approximately `6.6 s`; and **Pause target while scanning** stopped and automatically resumed the game on normal completion. Cancellation remains deferred until an in-flight target scan reaches a safe protocol boundary.

Rev5 subsequently completed verification: the Windows verification executable passed all 17 checks and the live PS5/UI behavior was reported working as intended, completing the `0.1.2` scanner-workflow milestone.

Host `0.1.3.rev18` originally continued the then-current plugin-owned scanner-definition model while adding API `2.4.0` concurrent writes. Host `0.1.3.rev25` moves the universal Scan Type catalog to Core. Host `0.1.3.rev26` / PS5 plugin `0.1.0.rev18` / Plugin API `2.7.0` added the generic semantic Core-to-native mapping layer and extended PS5 TurboScan beyond Exact Value where current ps5debug-NG semantics match Core. Host `0.1.3.rev28` / PS5 plugin `0.1.0.rev19` / Plugin API `2.8.0` added generic Scan Option toggle/applicability presentation without changing those native scan semantics. Rev28 passed all 40 automated checks and most live scan/UI acceptance tests, but exposed a New Scan default-selection regression and a large TurboScan Unknown Initial snapshot framing bug. Host `0.1.3.rev29` / PS5 plugin `0.1.0.rev20` fixed the TurboScan long-progress/command-stream recovery regression but live testing still reproduced the New Scan blank-selection edge case after a Next-only Scan Type. The same live run also demonstrated a Float Unknown Initial snapshot advertising **705,163,264** survivors, exposing the cost of immediate full GET/disk materialization. Host `0.1.3.rev30` / PS5 plugin `0.1.0.rev21` / Plugin API `2.9.0` therefore keeps large authoritative TurboScan results target-resident, retrieves only bounded display windows, and materializes to Core storage only when a non-native refinement requires it. The native Scan Type mapping table itself remains unchanged. Host `0.1.3.rev32` / PS5 plugin `0.1.0.rev22` then corrects Float/Double Changed/Unchanged resident refinement by using same-width unsigned integer wire comparison for those two predicates only, preventing unchanged NaN payloads from surviving every Changed Value pass. PS5 plugin `0.1.0.rev23` / host `0.1.6.rev2` later adopts Plugin API `2.10.0`, advertises neutral Disassembly, and adds the plugin-local x86-64 provider without changing the established process/memory/scan transport paths. Host `0.1.6.rev3` then adds the generic Disassembler workspace without a further PS5 plugin revision or transport change. Host `0.1.6.rev4` adds bounded pre-origin context and Scan Results/Saved Addresses **Open in Disassembler** entry points entirely in Core/host code; PS5 plugin `0.1.0.rev23`, Iced integration, and ps5debug-NG transport remain unchanged. Host `0.1.6.rev5` then replaces the rev4 split-at-origin decode orchestration with one continuous around-origin stream, allowing an instruction that begins before the requested byte to remain intact. This correction also stays entirely in Core/host code; PS5 plugin `0.1.0.rev23`, Iced integration, and ps5debug-NG transport remain unchanged. Host `0.1.6.rev6` / PS5 plugin `0.1.0.rev24` then advances the neutral contract to Plugin API `2.11.0` and publishes optional formatter-derived syntax tokens. Host `0.1.6.rev7` keeps PS5 plugin `0.1.0.rev24` and that transport/decoder path unchanged while adding only host/Core Memory Viewer value-span presentation. The decoder, branch-target semantics, bounded Core memory-read ownership, and ps5debug-NG transport remain unchanged.

Rev15 subsequently completed the large-result live PS5 verification: a signed-byte Exact Value First Scan for `50` committed `10,953,954` results, and a same-value Next Scan refined the complete disk-backed set to `8,796,420`. The survivor set included addresses beyond the 50,000-row UI preview, confirming complete-set refinement. First Scan and Next Scan were also repeated successfully with **Pause target while scanning** enabled.

Rev25 / host `0.1.7.rev5` is the current debugger transport candidate; rev5 retains the rev3 PS5 debugger implementation unchanged and supersedes rev4 only for responsive row-1 host sizing. The current source review is documented in `docs/testing/APP_0.1.7_REV5_SOURCE_REVIEW.md`; the authoritative remaining gate is a clean Windows **97/97** run followed by focused responsive-header/button UI review and live-PS5 Attach/Pause/Continue/Detach acceptance in `docs/testing/APP_0.1.7_REV5_VERIFICATION.md`. Rev3 was superseded before live acceptance, so no live rev3 debugger result is claimed.

See:

- [`PS5DEBUG_NG_PROTOCOL_MAPPING.md`](PS5DEBUG_NG_PROTOCOL_MAPPING.md)
- [`NATIVE_SCAN_AND_PROCESS_CONTROL.md`](NATIVE_SCAN_AND_PROCESS_CONTROL.md)
- [`CONNECTION_VERIFICATION.md`](CONNECTION_VERIFICATION.md)
- [`PROCESS_ENUMERATION_VERIFICATION.md`](PROCESS_ENUMERATION_VERIFICATION.md)
- [`MEMORY_MAP_VERIFICATION.md`](MEMORY_MAP_VERIFICATION.md)
- [`MEMORY_READ_VERIFICATION.md`](MEMORY_READ_VERIFICATION.md)
- [`MEMORY_WRITE_VERIFICATION.md`](MEMORY_WRITE_VERIFICATION.md)

## Debugger Threads and Thread Control

Rev6 extends the already hardware-verified rev5 PS5 debugger attachment with neutral thread enumeration and individual-thread control. The implementation remains entirely inside the PS5 plugin and uses the existing dedicated debugger command connection; it does not reuse the ordinary target/memory/scan command stream.

The backend maps current ps5debug-NG commands as follows:

- `0xBDBB0005` — enumerate debugger thread ids;
- `0xBDBB0011` — obtain the optional thread name/priority record for one id;
- `0xBDBB0006` — suspend one thread;
- `0xBDBB0007` — resume one thread.

Only the neutral id, optional name, and neutral execution state reach the host in rev6. ps5debug-NG's LWP terminology, 32-bit wire ids, priority field, packet layout, and command/status framing remain plugin-private. Current ps5debug-NG thread-list/thread-info replies do not contain a reliable per-thread execution-state field, so the plugin derives ordinary rows from the debugger's verified whole-target Running/Paused state and tracks only successful individual Suspend/Resume operations it issued. The suspended-id set is reconciled against each fresh enumeration so exited ids cannot remain stale. Per-thread control is allowed only while the whole debugger target is Running.

## Current Disassembler Host Integration

PS5 plugin `0.1.0.rev27` contains the same verified x86-64 disassembly implementation used by current host `0.1.7.rev5`; rev25 changes debugger integration, not the decoder, and rev4/rev5 do not change PS5 plugin code. The complete host `0.1.6.rev14` Disassembler block is now fully tested and hardware-verified; rev12-rev14 added only shared host/Core selection, copy/export, readable-region/module-relative presentation, and host selection/copy corrections. The plugin's Iced decoder, direct/indirect branch-target semantics, bounded caller-supplied byte contract, and ps5debug-NG transport remain unchanged. Rev25 now consumes the Plugin API `2.13.0` debugger contracts through its separate debugger provider/session; the existing Iced disassembly implementation remains unchanged from rev24.

## Source Layout

```text
src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/
├── Ps5PluginInfo.cs
├── Ps5ConcurrentMemoryWriter.cs
├── Ps5ConnectionSettings.cs
├── Ps5DebugProtocol.cs
├── Ps5DebugConnectionInfo.cs
├── Ps5DebugProcessInfo.cs
├── Ps5DebugMemoryRegionInfo.cs
├── Ps5DebugClient.cs
├── Ps5DebuggerCommandClient.cs
├── Ps5DebuggerProvider.cs
├── Ps5DebuggerSession.cs
├── Ps5NativeScanTypeMappings.cs
├── Ps5ScanDefinitions.cs
├── Ps5X64DisassemblerProvider.cs
├── Ps5TargetPlugin.cs
├── Ps5TargetSession.cs
└── TeeKay87.MemoryEngine.Platform.PS5.csproj
```

`Ps5PluginInfo` is the authoritative source for the PS5 plugin's own version, revision, and target Plugin API version.


### Host rev26 Scan Type behavior

Host `0.1.3.rev26` presents the same Core-owned Scan Type names regardless of target platform. The PS5 session publishes semantic mappings through `INativeScanTypeMappingProvider`; the host therefore attempts ps5debug-NG native execution only for mapped stage/Value Type combinations. Native identifiers remain private to the plugin.

The mapping table deliberately distinguishes capability from equivalence. Direct `compareType 11` is **not** used for Core Unknown Initial Value because it drops zero values; snapshot mode with include-zero semantics is used instead. `compareType 6/8` are not advertised for Increased By/Decreased By because target-width wrapping can change boundary results. Float/Double `compareType 12` is not advertised for Unknown Initial Low because upstream compares absolute magnitude. Those cases automatically use the shared Core scanner without changing the UI-selected predicate.
