# PlayStation 5 Plugin

## Purpose

`TeeKay87.MemoryEngine.Platform.PS5` is the PlayStation 5 platform plugin for TeeKay87's Memory Engine. It communicates with a jailbroken PlayStation 5 running ps5debug-NG and keeps all ps5debug-NG wire-protocol behavior outside the shared Core and WPF application.

This directory contains documentation that belongs specifically to the PS5 plugin.

## Plugin Identity

| Property | Value |
| --- | --- |
| Plugin id | `platform.ps5.ps5debug-ng` |
| Plugin version | `0.1.0.rev9` |
| Plugin API | `1.2.0` |
| Platform | PlayStation 5 |
| Backend | ps5debug-NG |
| Architecture | x64, 64-bit pointers, little-endian |

The plugin version/revision is independent from the TeeKay87's Memory Engine host version and from the ps5debug-NG server version.

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
- `NativeValueScanning`.

These capability flags describe functionality actually exposed by this plugin revision. ps5debug-NG supports additional commands, but Memory Engine must not advertise those capabilities until the corresponding neutral service/UI path exists and has been verified.

Debugger attachment, breakpoints/watchpoints, assembly/disassembly, pointer scanning, and cheat operations therefore remain unadvertised.

## Connection Settings

The plugin defines its own connection fields through the Plugin SDK:

| Setting | Required | Default | Description |
| --- | --- | --- | --- |
| `host` | Yes | none | IP address or host name of the PS5 running ps5debug-NG |
| `port` | Yes | `744` | ps5debug-NG main command-server TCP port |

The WPF application renders these definitions generically. It does not contain PS5-specific host or port fields.

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

The temporary Raw Memory Read/Write panels are development diagnostics. Their UI limits and Int32-focused input behavior are not limits of the Plugin SDK memory-access contracts.

## Native Value Scan Acceleration

PS5 plugin `0.1.0.rev9` advertises `NativeValueScanning`. After runtime TurboScan capability negotiation, the connected session exposes both `INativeValueScanner` and `INativeValueScanRefiner`. If the required server capabilities are absent, both services are hidden and the host uses the shared Core scanner.

The current native Exact Value path supports the complete ps5debug-NG scan value-type set:

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

Array of Bytes is currently an exact sequence using an all-ones ps5debug-NG mask and is limited to 4,096 bytes for the resident TurboScan path. Wildcard/masked AOB input remains future UI work.

The connection probes TurboScan CAPS (`0xBDAACC10`) and requires protocol version 1+, `TSE_SERVER_RESIDENT`, and `TSE_SNAPSHOT_SEGMENTS`. Supported sessions authorize scanning through `CMD_PROC_AUTH`. First Scan uses multi-segment resident TurboScan START (`0xBDAACC11`) and GET (`0xBDAACC13`), so the target performs the comparison and the PC receives only survivor records.

Unlike rev4, a successful rev5 First Scan does **not** immediately END the resident result set. For integer and exact Array-of-Bytes scans it remains on the ps5debug-NG connection so Next Scan can use resident TurboScan COUNT (`0xBDAACC12`) and GET. When the server advertises `TSE_RESCAN_ALIASING`, the plugin also opts into `TS_RESCAN_ALIASING` for that narrowing pass. For Float/Double, `0.1.3.rev1` deliberately ends the resident set before Next Scan and requests the shared Core fallback because current ps5debug-NG resident refinement uses fuzzy floating-point equality rather than strict Exact Value equality.

Core still owns the visible scanner semantics. Initial native addresses are normalized against the neutral memory map, and native Next Scan survivor addresses are normalized against the preceding host result set so `PreviousValue` remains the previous host-side value. If the resident session is absent, belongs to another PID, or its survivor count diverges from host state, the plugin closes it and the host falls back to the unchanged shared Core Next Scan.

**New Scan** and Active Target replacement request resident-session cleanup through `INativeValueScanRefiner.ResetAsync`. Disconnect also releases server state.

The plugin does not use legacy `CMD_PROC_SCAN` (`0xBDAA0009`).

First Scan and native resident list-based Next Scan use indeterminate progress because the current server response does not provide a meaningful percentage stream. Float/Double Next Scan and other Core fallbacks keep real percentage progress.

Cancellation is cooperative at protocol boundaries. An already-started START/COUNT/GET response is consumed fully before cancellation can return; if the resident set may have mutated, it is closed before control returns to the host. The UI reports the cancellation request immediately while waiting for that safe boundary.

See [`NATIVE_SCAN_AND_PROCESS_CONTROL.md`](NATIVE_SCAN_AND_PROCESS_CONTROL.md) for the full protocol/session-lifetime description.

## Process Suspend and Resume

PS5 plugin `0.1.0.rev9` also exposes `IProcessControl` and advertises both `ProcessSuspend` and `ProcessResume`.

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
INativeValueScanner
INativeValueScanRefiner
IProcessControl
```

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

Host `0.1.3.rev1` / PS5 plugin `0.1.0.rev9` expands Exact Value scanning to all eleven ps5debug-NG value types. Deterministic coverage now includes shared-Core scanning across the complete set and PS5 wire-id/mask framing across all types. Windows and physical-PS5 verification is required for the new value-type expansion.

See:

- [`PS5DEBUG_NG_PROTOCOL_MAPPING.md`](PS5DEBUG_NG_PROTOCOL_MAPPING.md)
- [`NATIVE_SCAN_AND_PROCESS_CONTROL.md`](NATIVE_SCAN_AND_PROCESS_CONTROL.md)
- [`CONNECTION_VERIFICATION.md`](CONNECTION_VERIFICATION.md)
- [`PROCESS_ENUMERATION_VERIFICATION.md`](PROCESS_ENUMERATION_VERIFICATION.md)
- [`MEMORY_MAP_VERIFICATION.md`](MEMORY_MAP_VERIFICATION.md)
- [`MEMORY_READ_VERIFICATION.md`](MEMORY_READ_VERIFICATION.md)
- [`MEMORY_WRITE_VERIFICATION.md`](MEMORY_WRITE_VERIFICATION.md)

## Source Layout

```text
src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/
├── Ps5PluginInfo.cs
├── Ps5ConnectionSettings.cs
├── Ps5DebugProtocol.cs
├── Ps5DebugConnectionInfo.cs
├── Ps5DebugProcessInfo.cs
├── Ps5DebugMemoryRegionInfo.cs
├── Ps5DebugClient.cs
├── Ps5TargetPlugin.cs
├── Ps5TargetSession.cs
└── TeeKay87.MemoryEngine.Platform.PS5.csproj
```

`Ps5PluginInfo` is the authoritative source for the PS5 plugin's own version, revision, and target Plugin API version.
