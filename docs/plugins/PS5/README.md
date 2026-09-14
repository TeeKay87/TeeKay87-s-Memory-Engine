# PlayStation 5 Plugin

## Purpose

`TeeKay87.MemoryEngine.Platform.PS5` is the PlayStation 5 platform plugin for TeeKay87's Memory Engine. It communicates with a jailbroken PlayStation 5 running ps5debug-NG and keeps all ps5debug-NG wire-protocol behavior outside the shared Core and WPF application.

This directory contains documentation that belongs specifically to the PS5 plugin.

## Plugin Identity

| Property | Value |
| --- | --- |
| Plugin id | `platform.ps5.ps5debug-ng` |
| Plugin version | `0.1.2.rev39` |
| Plugin API | `2.18.0` |
| Platform | PlayStation 5 |
| Backend | ps5debug-NG |
| Architecture | x64, 64-bit pointers, little-endian |

The plugin version/revision is independent from the TeeKay87's Memory Engine host version and from the ps5debug-NG server version.

Current host application `0.1.7.rev36` uses Plugin API `2.18.0` and PS5 plugin `0.1.2.rev39`. The plugin exposes the verified target-memory, native-scan, disassembly, debugger, thread/register, breakpoint/watchpoint, call-stack, and native Step Into services described below. Host-composed Step Over, Step Out, and Run to Address continue to build on neutral call frames, logical Disassembler data, and temporary Software/Execute breakpoints. Rev35 introduced the optional Disassembler watchpoint-target resolver used to derive a safe hardware data-watchpoint request from one selected x86-64 memory-access instruction and the matching paused register context. Rev36 corrects only that resolver's unsupported-address-register definite-assignment path so it compiles cleanly while retaining the same safe-null behavior.

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
- `RegisterAccess`;
- `Breakpoints`;
- `Watchpoints`;
- `CallStack`;
- `StepExecution`.

These capability flags describe functionality actually exposed by this plugin revision. ps5debug-NG supports additional commands, but Memory Engine must not advertise those capabilities until the corresponding neutral service/UI path exists and has been verified.

Disassembly remains advertised because the connected session exposes the verified neutral `IDisassemblerProvider`. Rev29 retains the verified debugger/thread services and `RegisterAccess`: while the target is Paused, the selected thread exposes the mandatory read-only general-register snapshot and, when negotiated and healthy, optional floating-point/SIMD plus FS/GS-base groups through guarded debugger command probes. Rev30 advertises `Breakpoints` because the attached debugger session exposes the neutral breakpoint lifecycle/state services. Rev33 additionally advertises `Watchpoints` and reuses that same neutral service for hardware data conditions. Rev34 introduced `CallStack` and `StepExecution`; rev35 retains those capabilities and corrects the platform-private software-breakpoint stop semantics without changing public capability flags. Assembly/instruction editing, pointer scanning, and cheat operations remain unadvertised. Register writing remains intentionally disabled because the upstream SET-register paths have not been hardware-verified for use by Memory Engine.


## Software Execute Breakpoints

PS5 plugin `0.1.0.rev36` retains the verified neutral software-breakpoint surface through ps5debug-NG `CMD_DEBUG_SET_BREAKPOINT` (`0xBDBB0003`). The wire body remains 16 bytes: a plugin-owned slot index, a 32-bit enabled flag, and the 64-bit target address. ps5debug-NG currently provides 30 software-breakpoint slots; that limit and slot allocation remain private to the PS5 plugin. Before a slot is selected or a backend request is sent, rev32 requires the requested address to belong to the cached current target memory map and to an executable, non-guarded region. The cache is populated by the plugin's normal `IMemoryMapProvider` enumeration, so breakpoint validation does not introduce concurrent command traffic on the target or debugger sockets.

The host creates one-byte Software/Execute requests and may mark them persistent or temporary. The plugin rejects duplicate managed addresses and does not reuse a slot while an earlier breakpoint is awaiting backend cleanup. Running-state Enable/Disable/Remove maps directly to the backend command.

Breakpoint hits arrive through the existing TCP 755 async event channel. Current ps5debug-NG restores the original instruction byte, rewinds the **interrupt packet** RIP to the breakpoint address, writes that RIP to the stopped thread, single-steps the original instruction, waits for completion, reinserts the INT3, and only then sends the original logical packet to the client. The packet therefore describes the pre-instruction logical breakpoint stop while a later live GETREGS is already post-instruction. Live rev24 isolation confirmed this both for `0xE9F747 -> 0xE9F74C` and for `0xE9F74E call 0x1A3BC30 -> live 0x1A3BC30`. Rev35 caches the matching packet's 176-byte GP and 832-byte FPU blocks as the authoritative logical stop snapshot for that managed breakpoint/thread. Rev30's SIGTRAP attribution rule remains: a neutral managed breakpoint hit is emitted only when the logical event RIP matches an active managed breakpoint address. Temporary records are removed from the manager after their first hit and backend slot cleanup remains staged. See [`../../bug-reports/ps5debug-ng-software-breakpoint-event-and-live-register-state-diverge.md`](../../bug-reports/ps5debug-ng-software-breakpoint-event-and-live-register-state-diverge.md).

### Paused Disable/Remove Safety

Current ps5debug-NG has a side effect in the disable branch of `debug_set_breakpoint_handle`: after restoring the saved byte and handling potentially stuck threads, it calls `PT_CONTINUE` on the target before returning success. Sending that command while the user has the debugger Paused can therefore resume the target unexpectedly. See [`../../bug-reports/ps5debug-ng-disabling-software-breakpoint-resumes-paused-target.md`](../../bug-reports/ps5debug-ng-disabling-software-breakpoint-resumes-paused-target.md).

Rev30 avoids that behavior by staging disable/remove for an enabled breakpoint while Paused. The neutral breakpoint presentation changes immediately, but the plugin sends the backend disable only immediately before the next explicit Continue. Re-enable before Continue cancels the pending disable. This is a PS5/backend-specific safety rule and does not change the public breakpoint model.

Rev36 adds a separate teardown-only path. Once explicit detach or disposal begins, all software-breakpoint slots still known to be backend-active, including paused removals already staged for later cleanup, are explicitly disabled/restored before the normal backend detach command. The local event channel remains open during this restore pass so complete callback packets can be drained, but packets received while detach is active are ignored for session state/event projection; the channel is closed after the detach attempt. Detach is still attempted if one of those defensive restore requests fails, and successful detach clears the client tables only after the backend detach request has completed. This does not change normal paused Disable/Remove behavior.

Current ps5debug-NG also stops its own detach-time software-breakpoint restore loop at the first empty indexed slot. A previously disabled lower slot can therefore hide a later active slot from that loop. The source-level issue is recorded in [`../../bug-reports/ps5debug-ng-debugger-detach-stops-breakpoint-restore-at-first-empty-slot.md`](../../bug-reports/ps5debug-ng-debugger-detach-stops-breakpoint-restore-at-first-empty-slot.md). Rev36 does not change the backend; it defensively restores every software-breakpoint slot still tracked by the client before requesting detach.

## Hardware Data Watchpoints

PS5 plugin `0.1.0.rev36` reuses `IDebuggerBreakpointService` / `IDebuggerBreakpointStateService` for neutral `Hardware` data-watchpoint requests and advertises `TargetCapabilities.Watchpoints`. The host does not receive DR-register indices or encoding values.

The current ps5debug-NG request is `CMD_DEBUG_SET_WATCHPOINT` (`0xBDBB0004`) with a 24-byte body containing backend slot, enabled flag, DR7 length encoding, DR7 access encoding, and address. Current upstream accepts slot indices `0-3`. The plugin maps neutral sizes as 1 -> `0`, 2 -> `1`, 4 -> `3`, and 8 -> `2`; Write maps to DR7 R/W value `1`, while ReadWrite maps to value `3`. A neutral Read-only request is rejected instead of being silently broadened because amd64 DR7 has no separate read-only data mode.

Before allocating a hardware slot or sending the backend command, the plugin requires:

- Hardware kind with Write or ReadWrite access;
- size 1, 2, 4, or 8 bytes;
- natural alignment to the selected size;
- the complete byte range to fit inside one cached current-process memory region;
- the target region not to be guarded.

Persistent and temporary lifetimes use the same neutral manager semantics as software breakpoints. Enable, Disable, Remove, and Remove All operate through the hardware command path. Temporary hardware records are removed only when the responsible slot can be attributed safely.

### Watchpoint event attribution

The 1184-byte ps5debug-NG interrupt already contains a 128-byte debug-register block at offset `0x420`; DR6 is at packet offset `0x450`. Rev33 reads that in-band value and does **not** issue paused `GETDBREGS`, preserving the verified rev29/rev10 transport guard. If DR6 B0-B3 identifies an active hardware slot, the plugin emits a neutral `Watchpoint` event. `InstructionPointer` remains the instruction that performed the memory access, and `TriggeredBreakpoint.Request.Address` identifies the watched data range.

Current upstream `dispatch_debug_events()` clears `pkt_dr[6]` before later preserving the data-watchpoint status, so a client can receive zero DR6 bits even though a hardware watchpoint caused the trap. Rev33 handles this conservatively: when exactly one enabled/backend-active hardware watchpoint exists, a signal-5 stop with zero DR6 can be attributed to that sole record; with more than one active watchpoint, the same stop remains an unattributed signal event and no temporary record is removed. The source-level backend issue is documented in [`../../bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md`](../../bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md).

## Call Stack and Stepping

PS5 plugin `0.1.0.rev36` implements `IDebuggerCallStackService` and `IDebuggerStepService` while keeping all backend stack-walk and stepping mechanics plugin-private. Both services require the existing attached debugger session, and Call Stack requires a current Paused stop context for the selected neutral thread.

### Server-side Call Stack

For an ordinary paused context, the call-stack path reuses the verified live general-register read for the selected debugger thread to obtain RIP, RBP, and RSP. For a matching rev35 logical software-breakpoint stop, those three values instead come from the cached interrupt-packet GP block so frame zero remains on the reported breakpoint instruction even though the backend has already transparently stepped it. The plugin then sends ps5debug-NG `CMD_PROC_READ_STACK` (`0xBDAA0023`) with the same packed 24-byte request `{ uint32 pid; uint64 rbp; uint64 rsp; uint32 depth; }`. The requested depth is capped at the backend maximum of 64 frames.

ps5debug-NG performs the RBP-chain walk server-side and returns a length-prefixed variable payload. Every backend frame contains current RBP/RSP, saved RBP, return address, flags, a locals length, a code-window length, and optional locals/code bytes. Rev34 validates the declared payload length, frame count, fixed frame headers, variable lengths, truncation, backend maxima, and trailing bytes before publishing neutral data. Locals and code-window bytes are intentionally not exposed because the shared `DebuggerStackFrame` model does not require them.

The first neutral frame uses the selected thread's current RIP. Later neutral frame instruction addresses are derived from the preceding backend return address. RSP/RBP are mapped into StackPointer/FramePointer, zero return addresses become null, and module names are resolved through the existing current-process memory map when possible. Symbol names remain optional. If the backend safely returns no walked frames, rev34 can still publish one current frame from the selected thread's verified register context instead of inventing caller frames.

### Native Step Into

Selected-thread Step Into maps to `CMD_DEBUG_STEP_THREAD` (`0xBDBB0013`) with the 32-bit backend thread id. The process-wide `CMD_DEBUG_STEP` (`0xBDBB0012`) is also mapped internally for a call without a selected thread id, although the current host normally steps the selected thread. Before stepping, pending software-breakpoint and watchpoint disables are flushed through their existing safe cleanup path.

After a successful ordinary native step command the session enters neutral Running state and records the pending step thread. The matching asynchronous signal-5 trap is translated to a Paused `StepCompleted` event. Managed software-breakpoint and hardware-watchpoint attribution is evaluated first so a known breakpoint/watchpoint hit is never relabeled as a step completion. Rev35 adds one special case for Step Into requested while the selected thread is represented by a logical software-breakpoint snapshot: ps5debug-NG has already performed the transparent step, so the plugin reads the current live post-step RIP, consumes the snapshot, and emits one neutral Resumed/StepCompleted transition **without sending a second `CMD_DEBUG_STEP_THREAD`**. Manual Pause, explicit Continue, Detach, new interrupt receipt, and snapshot consumption clear that special state. Detach/disposal and target invalidation retain the existing debugger cleanup rules.

Step Over, Step Out, and Run to Address are intentionally not implemented as PS5-specific commands. The host composes those workflows through neutral Disassembler/call-frame data and the already verified temporary software-breakpoint mechanism. Host rev26 additionally preserves the original neutral instruction before a Software/Execute breakpoint is installed; on the matching logical breakpoint stop, Step Over uses that instruction before live disassembly so the backend-rearmed `INT3` cannot hide an original `call`. No PS5-specific Step Over command or new wire behavior is introduced.

## Debugger Register State

PS5 plugin `0.1.0.rev36` retains `IDebuggerRegisterService` for paused-thread register snapshots. The already verified general-register read remains mandatory and unchanged: the plugin sends ps5debug-NG `CMD_DEBUG_GET_REGISTERS` (`0xBDBB0008`) with the selected 32-bit backend thread id and reads the 176-byte FreeBSD amd64 general-register block on the debugger-owner command connection.

The backend also exposes the following extended commands:

| Group | ps5debug-NG command | Response size | Current paused-register use |
| --- | --- | ---: | --- |
| Floating-point / SIMD | `CMD_DEBUG_GET_FPREGS` (`0xBDBB000A`) | 832 bytes | guarded disposable probe; exposes FCW, FSW, FTW, FOP, FIP, FDP, MXCSR, MXCSR_MASK, ST0-ST7, XMM0-XMM15, YMM0-YMM15 |
| Debug registers | `CMD_DEBUG_GET_DBREGS` (`0xBDBB000C`) | 128 bytes | **suppressed while Paused in rev29** because the current backend handler can block after the stop event has already been consumed |
| Segment bases | `CMD_DEBUG_GET_FSGSBASE` (`0xBDBB000E`) | 16 bytes | guarded disposable probe; exposes FSBASE, GSBASE |

The 832-byte FPU payload is decoded from the FreeBSD amd64 FXSAVE/xstate layout entirely inside the PS5 plugin. x87 values are preserved as exact 80-bit raw register bytes. XMM rows use their 128-bit lower state, while each 256-bit YMM row is reconstructed from the corresponding XMM lower half plus the upper half from the xstate YMM area. RIP, RSP, and RBP retain the neutral InstructionPointer, StackPointer, and FramePointer roles. For an ordinary pause these values still come from the live GETREGS plus safe optional probes. For the selected thread at a managed rev35 logical software-breakpoint stop, the packet GP/FPU blocks are used instead so register presentation matches the breakpoint event; FSBASE/GSBASE remain the existing disposable live probe because they are not carried in that packet area. The existing plugin-private debug-register decoder remains in place for future safe backend/event use, but paused register refresh does not invoke GETDBREGS. All PS5 values use `UnsignedLittleEndian` and all remain read-only.

A fully successful current paused snapshot contains **76 rows**: 26 mandatory general rows, 48 FPU/SIMD rows, and 2 FS/GS-base rows. If the safe optional blocks are unavailable, the surface degrades toward the verified 26-row general-only snapshot.

### Rev9 live failure and rev29 transport guard

The rev28 implementation originally requested GETFPREGS, GETDBREGS, and GETFSGSBASE serially on the debugger-owner command connection after GETREGS. Host rev9 passed **108/108** automated checks and focused Mock Gates B-D. On real PS5 hardware, however, Attach and Pause succeeded and thread enumeration completed, while the register refresh remained pending at `Registers 0` / unavailable IP. Because a response read on a reusable framed command stream cannot safely be abandoned halfway through, rev28's optional post-dispatch reads had no timeout and could hold the complete refresh indefinitely.

The rev10 source review also identified a concrete ps5debug-NG stopped-target problem in GETDBREGS. The debugger Pause path sends `SIGSTOP` and consumes the stop event with `wait4()`. GETDBREGS later calls `is_process_stopped()`, which uses another non-blocking `wait4()` as its state test. With the stop event already consumed, that check can report false even though the target is still stopped. GETDBREGS then sends another stop signal and waits for a new stop transition that may never arrive. Because debugger commands are serialized behind shared backend locks, that blocked wait can also prevent later debugger commands from progressing. The external issue is documented in [`../../bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md`](../../bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md).

Rev29 changes the client boundary accordingly:

- the mandatory GETREGS request stays on the verified debugger-owner connection;
- safe optional probing is enabled only when the already-read connection metadata reports ps5debug-NG protocol `1.3` or newer and capability level `1.0` or newer;
- GETFPREGS and GETFSGSBASE each open a fresh short-lived command connection and have a linked two-second timeout;
- paused GETDBREGS is not sent;
- backend error/data-null status, timeout, socket/connection failure, or a malformed/unexpected probe-local response disables only that safe optional group;
- a failed safe optional command is cached as unavailable for the current debugger attachment so Refresh does not repeatedly hit the same failure;
- independently successful safe optional groups continue refreshing;
- Detach/dispose clears that unavailable set so a new attachment starts cleanly;
- caller/session cancellation still propagates and is not converted into an unsupported-group result.

A timed-out probe socket is discarded rather than reused, so it cannot leave the debugger-owner command stream partially framed. Current ps5debug-NG source also assigns debugger ownership to the connection that performs Attach; cleanup of a non-owner connection does not invoke full debugger teardown. Rev29 relies on that source contract for the safe disposable read probes.

If the backend does not advertise the required protocol/capability metadata, the plugin sends no optional register command and returns the verified general-register snapshot only. If both safe optional probes succeed, the current 76-row surface is available. If only one succeeds, only that group is appended.

The plugin deliberately does not expose SETREGS, SETFPREGS, SETDBREGS, or FS/GS write operations in rev31. Those target-side write paths require separate review and hardware verification before the neutral UI may enable PS5 register editing.

Rev6 live verification also found that the current tested ps5debug-NG backend returns on-wire `CMD_ERROR` (`0xF0000001`) for the documented individual-thread suspend request even when the LWP id came directly from thread enumeration. The failure did not crash the game, console, or debugger session. The reproduction and server-code notes are recorded in [`../../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md`](../../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md). Memory Engine keeps the existing ThreadControl client path so it can be retested against a corrected payload without redesigning the wire request.

## x86-64 Disassembly

The current PS5 plugin `0.1.0.rev36` retains the `IDisassemblerProvider` implementation introduced in rev24 under Plugin API `2.11.0` and continues to advertise `TargetCapabilities.Disassembly`. The provider is deliberately decode-only: Core obtains a bounded readable-region byte window through the existing `IMemoryReader` service, then passes those bytes to the plugin for architecture-specific interpretation.

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

Plugin `0.1.0.rev32` continues to use the optional Plugin API `2.3.0` `IPluginSettingsConsumer` contract. Core attaches a settings scope owned by `platform.ps5.ps5debug-ng` before the host reads these connection definitions. If `connection.host` and `connection.port` contain remembered valid values, the plugin exposes them as the connection-field defaults. After a connection successfully completes, the plugin writes the normalized host and port back through `IPluginSettings`. Failed connection attempts do not replace the previous successful values. The plugin never opens or parses `settings.json` itself.

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

Plugin API `2.4.0` adds the optional `IConcurrentMemoryWriter` marker contract. PS5 plugin `0.1.0.rev36` exposes this service through the unchanged dedicated `Ps5ConcurrentMemoryWriter`. It lazily opens a second fully validated ps5debug-NG connection on the first in-scan Frozen write and serializes writes on that connection. The normal session's primary `Ps5DebugClient` remains responsible for TurboScan and all previously verified command flows.

The host uses this concurrent writer only when a Frozen row must be reapplied while First/Next Scan is active and **Pause target while scanning** is Off. When Pause is On, Frozen writes are intentionally suppressed for the duration of the paused scan. The secondary client is disposed with the target session.

## Scanner Capability Declarations

Plugin `0.1.0.rev36` targets Plugin API `2.15.0` and retains the concrete Value Types and PS5-specific Scan Options that the current implementation can execute. `SupportedValueTypes` contains eleven `IMemoryValueType` objects: UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float32, Float64, and ByteArray. `SupportedScanOptions` contains Endianness, Alignment, and Floating-point rounding.

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

PS5 plugin `0.1.0.rev32` advertises `NativeValueScanning`. After runtime TurboScan capability negotiation, the connected session exposes legacy `INativeValueScanner`/`INativeValueScanRefiner`, API 2.2 streaming `INativeValueScanStreamProvider`/`INativeValueScanStreamRefiner`, API 2.7 `INativeScanTypeMappingProvider`, and API 2.9 resident result handles from authoritative native streams through `INativeValueScanResidentResultSet`. If the required TurboScan capabilities are absent, the native scanner services are hidden and the host uses the shared Core scanner. If the mapping provider is available but no semantic mapping matches the selected Core Scan Type/stage/Value Type, the host skips native execution and goes directly to Core.

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

PS5 plugin `0.1.0.rev32` also exposes `IProcessControl` and advertises both `ProcessSuspend` and `ProcessResume`.

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

PS5 plugin `0.1.0.rev25` was the first PS5 revision to consume the neutral debugger contracts through `IDebuggerProvider` / `IDebuggerSession`. Current plugin `0.1.0.rev36` keeps that verified transport, advertises `TargetCapabilities.ThreadEnumeration`, `TargetCapabilities.ThreadControl`, `TargetCapabilities.RegisterAccess`, `TargetCapabilities.Breakpoints`, `TargetCapabilities.Watchpoints`, `TargetCapabilities.CallStack`, and `TargetCapabilities.StepExecution`, provides general plus guarded extended read-only register snapshots while Paused, retains persistent/temporary software execute breakpoints and hardware Write/ReadWrite data watchpoints, and adds server-side call-stack access plus native Step Into.

The connected target session exposes `IDebuggerProvider`. The provider owns one active debugger attachment and uses a dedicated `Ps5DebuggerCommandClient`. The debugger command connection is separate from both the primary `Ps5DebugClient` and the concurrent Frozen-write client, so debugger attach/control traffic cannot interleave with an in-flight memory/scan transaction.

### Attach / detach and TCP 755

Current ps5debug-NG requires the debugger client to listen on TCP `755` before attachment because the server opens an outbound connection to the client's IP for async interrupts. Rev25 therefore starts the local listener first, opens the dedicated command connection, sends `CMD_DEBUG_ATTACH` (`0xBDBB0001`) with the signed 32-bit PID, and accepts the outbound event socket. A successful session begins in neutral `Running` state.

Explicit detach uses `CMD_DEBUG_DETACH` (`0xBDBB0002`). Rev36 explicitly restores every software-breakpoint slot still known to be backend-active or staged for paused cleanup and then sends the normal detach request. The event callback remains connected during the restore phase to drain any complete backend packets generated when breakpoint disable resumes a paused target, but those packets are ignored while detach is active; the callback is closed after the detach attempt. Backend detach is still attempted if one of those defensive restore requests fails. Window/session disposal applies the same restore-before-detach path on a best-effort basis and always tears down the local event/command sockets. Only one debugger session can be active for a connected PS5 target session at a time.

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

The PS5 plugin maps those fields to neutral `DebuggerEvent` data and never exposes the FreeBSD register/FPU/debug-register structs to Core or WPF. An interrupt leaves the neutral session `Paused`. Ordinary stops continue to use live register reads. For a managed software-breakpoint hit, current ps5debug-NG has already restored and transparently single-stepped the breakpointed instruction before the interrupt packet is delivered; rev35 therefore preserves the packet's pre-instruction GP/FPU state as the logical stop context for the matching thread instead of mixing that event with later post-step GETREGS state.

If the event channel disconnects unexpectedly while the backend remains attached, the neutral session becomes `Unknown` and emits a backend diagnostic event so run/pause controls cannot pretend the execution state is known. Detach remains the cleanup path.

### Thread enumeration and individual-thread control

Rev26 implements the already-public API `2.12.0` optional attached-session thread services:

- `IDebuggerThreadService` through `CMD_DEBUG_GET_THREAD_LIST` (`0xBDBB0005`) plus optional `CMD_DEBUG_THREAD_INFO` (`0xBDBB0011`) lookups;
- `IDebuggerThreadControlService.SuspendThreadAsync` through `CMD_DEBUG_SUSPEND_THREAD` (`0xBDBB0006`);
- `IDebuggerThreadControlService.ResumeThreadAsync` through `CMD_DEBUG_RESUME_THREAD` (`0xBDBB0007`).

The plugin maps backend thread ids and optional names into neutral `DebuggerThreadInfo` rows. The host never receives FreeBSD LWP terminology, wire structs, command ids, or priority fields. Current ps5debug-NG list/info replies do not provide a reliable individual execution-state field, so rev26 derives ordinary Running/Stopped presentation from the whole debugger session and tracks successful individual Suspend/Resume operations it owns. Per-thread control is accepted only while the overall debugger target is Running.

Register services are implemented through the current read-only `IDebuggerRegisterService`. Rev30 additionally exposed the neutral software-breakpoint lifecycle/state services described above; rev33 adds hardware data watchpoints through the same generic manager/service. Call-stack access and step execution remain later PS5 debugger revisions.

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

Host `0.1.7.rev8` completed the debugger register/stop-context acceptance baseline: **107/107** Windows checks and all **11/11** focused Mock/live-PS5 register, current-instruction, read-only-safety, transport, and cleanup steps passed. Host `0.1.7.rev10` completed the guarded extended-register correction, rev15 completed the verified Software/Execute breakpoint baseline, and rev16 completed Hardware Watchpoints with **118/118** plus focused Mock/live-PS5 acceptance. Rev31 established the permanent live-verified PS5 hardware-watchpoint detach/disposal cleanup baseline. Rev32 separated watchpoint Trigger Instruction from real Stop/Current IP, and rev35 introduced the optional PS5 `IDisassemblyWatchpointResolver` using Iced metadata. Rev36 fixed its compile-time definite-assignment path; rev37 corrected the read-modify-write verification expectation and subsequently passed the complete **152/152** Windows gate. Current host `0.1.7.rev38` keeps PS5 plugin `0.1.2.rev39` and Plugin API `2.18.0` unchanged while relaxing only the host-side Disassembler row-identity gate: any single safely resolvable memory-access instruction may now derive a watchpoint candidate from the current paused register snapshot. PS5 still owns x86-64 operand/access/width/effective-address rules, and the existing backend validator remains authoritative before any hardware slot is changed.

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

PS5 plugin `0.1.2.rev39` retains the verified Iced x86-64 disassembly implementation used by current host `0.1.7.rev38`. The complete host `0.1.6.rev14` Disassembler block remains the verified baseline for bounded decode, selection, copy/export, readable-region/module-relative presentation, and branch-target behavior. Later debugger revisions compose additional logical-byte overlays, markers, trigger/stop semantics, and direct debugger actions without replacing that decoder. Plugin API `2.18.0` adds the optional `IDisassemblyWatchpointResolver`, implemented by PS5 with the same Iced instruction metadata so operand access, width, and effective-address rules remain plugin-owned. Host rev38 changes only when that resolver may be called: any single selected row may be evaluated while the matching debugger is Paused with current registers, rather than only the current stop/trigger row.

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
├── Ps5ExtendedRegisterMapper.cs
├── Ps5GeneralRegisterMapper.cs
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


## Rev37 Request Validation for Host Address Shortcuts

PS5 plugin `0.1.0.rev37` added no ps5debug-NG command and did not change breakpoint/watchpoint transport. It targets Plugin API `2.16.0` and exposes optional `IDebuggerBreakpointValidationService` so the generic host can grey out impossible Scan Results/Saved Addresses debugger shortcuts before sending a backend request. Software/Execute validation reuses the existing current-process mapped/executable/non-guarded rules plus slot/duplicate/staged-cleanup checks. Hardware/Write validation reuses the existing mapped-range, guard, supported-width, natural-alignment, access-mode, DR0-DR3 slot, duplicate, and staged-cleanup rules. Final Add remains authoritative.

The rev29 live marker test also exposed an upstream ps5debug-NG interaction when a Software breakpoint is placed on the exact instruction whose execution would trigger an active hardware watchpoint. The backend's internal restored-instruction single-step consumes the overlapping watchpoint stop. See `docs/bug-reports/ps5debug-ng-software-breakpoint-step-consumes-overlapping-watchpoint-hit.md`.


## Rev38 Safe Hardware Watchpoint Teardown

PS5 plugin `0.1.0.rev38`, used by host `0.1.7.rev31`, adds explicit client-owned hardware-watchpoint cleanup before explicit debugger detach and session disposal. Every hardware slot still known to be backend-enabled is disabled through the existing watchpoint command before the backend detach request is sent. This includes temporary watchpoints that have already been removed from the logical list but are still staged in pending backend cleanup.

The rev36 software-breakpoint restore pass remains independent, so software and hardware debugger instrumentation are both removed before detach without one cleanup category suppressing the other. No Plugin API change or new ps5debug-NG wire command was required.

This protection was added after live shutdown testing showed that leaving a hardware watchpoint enabled and closing the debugger client could leave the target with an armed debug register; the game then terminated when that watchpoint condition occurred later. The upstream teardown issue and relevant ps5debug-NG source path are documented in [`../../bug-reports/ps5debug-ng-detach-can-leave-hardware-watchpoints-active.md`](../../bug-reports/ps5debug-ng-detach-can-leave-hardware-watchpoints-active.md).

## 0.1.0.rev39 — Watchpoint Event Semantics

PS5 now targets Plugin API `2.17.0`. The backend callback RIP for a hardware data watchpoint continues to be preserved as the real stop/current instruction pointer. The plugin no longer describes that RIP as the memory-accessing instruction. Trigger resolution is intentionally left unresolved at the plugin event boundary unless the backend can provide an authoritative trigger; the host may derive the previous logical instruction through the existing Disassembler pipeline and mark it `DisassemblyDerived` only when the instruction boundary is safe.

The rev38 explicit active/staged hardware-watchpoint cleanup before detach/disposal is unchanged.



## 0.1.2.rev39 — Disassembly Watchpoint Resolver Compile Fix

Host `0.1.7.rev36` keeps Plugin API `2.18.0` and advances the PS5 semantic plugin version to `0.1.2` because the PS5 resolver source changed. The only runtime-code correction is definite assignment in `Ps5DisassemblyWatchpointResolver.TryReadAddressRegister(...)`: when an Iced address-register kind is not one of the supported x64 GPR mappings, the method now assigns the `out` value to zero before returning `false`.

The resolver therefore keeps the same conservative behavior introduced in `0.1.1`: unsupported address forms do not produce a watchpoint candidate, and the host leaves automatic Disassembler **Add Watchpoint** disabled rather than guessing. No ps5debug-NG wire command, debugger transport, breakpoint/watchpoint slot logic, detach cleanup, register mapping, disassembly decode, or Plugin API contract changes in this semantic plugin update.

## 0.1.1.rev39 — Disassembler Watchpoint Target Resolution

Host `0.1.7.rev35` advances the optional Plugin API surface to `2.18.0` and PS5 semantic plugin version to `0.1.1` while retaining the existing legacy revision metadata for compatibility. The attached PS5 target session now exposes `IDisassemblyWatchpointResolver` so a single selected x86-64 Disassembler instruction can be converted into a hardware data-watchpoint request without leaking Iced operand types into Core/WPF.

The resolver uses the existing Iced decoder and the paused debugger register snapshot. It accepts exactly one explicit memory operand, derives a 1/2/4/8-byte width, computes the effective address from the supported base/index/scale/displacement form (including RIP-relative and available FS/GS bases), and maps a definite write to neutral `Write`. Because amd64 DR7 has no read-only data mode, a read or read/write memory access is represented as neutral `ReadWrite` rather than silently claiming a read-only hardware capability.

Resolution is deliberately conservative. `lea`, non-memory instructions, multiple explicit memory operands, unsupported widths/forms, missing register values, address registers modified by the selected instruction, stale/non-paused debugger contexts, and requests rejected by the existing breakpoint validator all leave **Add Watchpoint** disabled. No target memory is written while resolving the candidate. Final creation still passes through the existing debugger breakpoint/watchpoint manager and PS5 validation path.

Rev35 also changes only the host presentation of resolved watchpoint stops: the `Watchpoint hit` trigger instruction is highlighted with the existing success/green semantic, while the distinct real stop/current instruction receives a warning/yellow highlight. The underlying rev32 event semantics and rev31 safe detach cleanup are unchanged.
