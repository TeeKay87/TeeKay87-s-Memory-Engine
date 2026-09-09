# Debugger Architecture

## Purpose

This document defines the shared debugger architecture introduced by TeeKay87's Memory Engine `0.1.7.rev1`, first consumed by the host/Mock implementation in `0.1.7.rev2`, first connected to real PS5/ps5debug-NG debugger transport in `0.1.7.rev3`, fully hardware-verified through `0.1.7.rev5`, and extended with the first optional advanced services in `0.1.7.rev6`.

The debugger must remain a host-level workflow with platform-specific execution delegated to plugins. The WPF application and Core must never learn ps5debug-NG packet layouts, x86 debug-register semantics, Windows debugging APIs, Xbox 360 debugger commands, or any other backend-specific protocol detail.

Application `0.1.7.rev1` is the verified contract/Core foundation and passed **84/84** Windows checks. Application `0.1.7.rev2` is fully verified after **89/89** Windows checks plus focused Mock runtime/UI acceptance; it provides the capability-driven modeless Debugger workspace and deterministic Mock attach/event backend. The real PS5/ps5debug-NG provider first introduced in rev3 was carried through the superseding header revisions and is fully hardware-verified as part of `0.1.7.rev5`, whose **97/97** Windows gate plus complete Mock/UI/live-PS5 acceptance passed. Rev5 verified dedicated command transport, TCP 755 callback ownership, real Attach/Detach/Pause/Continue, transport isolation, target-generation cleanup, repeated reattachment, and multiple-window exclusivity. The optional natural async-stop event could not be safely triggered and remains deferred rather than failed. Application `0.1.7.rev6` is the current candidate and implements the planned **Threads and Thread Control** milestone through the API `2.12.0` contracts already verified in rev1.

## Verified Base

Debugger development begins from the completed `0.1.6.rev14` Disassembler feature block.

`0.1.6` is fully tested and hardware-verified, including the neutral disassembly contract, Core-owned bounded reads, real PS5 x86-64 decoding, continuous origin resolution, navigation/history, readable-region navigation, selection/copy behavior, universal export, and stale-session protection.

The debugger must integrate with those verified services rather than replace or duplicate them.

## Ownership Boundary

| Layer | Debugger responsibility |
| --- | --- |
| `TeeKay87.MemoryEngine.PluginSdk` | Public debugger contracts, neutral states/events/threads/registers/frames/breakpoints/step models, coarse capability flags |
| `TeeKay87.MemoryEngine.Core` | Session identity, lifecycle/state coordination, event identity/ordering, backend-independent safety rules |
| `TeeKay87.MemoryEngine.App` | Modeless Debugger workspace, target/session ownership, commands, neutral event presentation, later navigation into existing Memory Viewer/Disassembler |
| platform plugin | Attach/detach implementation, debugger transport, backend event decoding, real thread/register/breakpoint/call-stack/step operations |

The Plugin SDK does not reference Core or WPF. Core does not reference a concrete platform plugin.

## Plugin API 2.12.0

`0.1.7.rev1` advances the public Plugin API from `2.11.0` to `2.12.0` because it adds public debugger contracts and models.

The compatibility rule remains unchanged: a 2.x host accepts plugins targeting the same or an older 2.x minor version. In verified rev1, Mock `1.0.0.rev7` and PS5 `0.1.0.rev24` both remained compatible `2.11.0` plugins. Verified rev2 advances only Mock to `1.0.0.rev8` / API `2.12.0` because it consumes `IDebuggerProvider`/`IDebuggerSession`. Rev3 keeps Plugin API `2.12.0` unchanged and advances PS5 to `0.1.0.rev25` / API `2.12.0` because the PS5 plugin now consumes those debugger contracts directly. Rev6 does not change the public API: it advances Mock to `1.0.0.rev9` and PS5 to `0.1.0.rev26` because both now consume the already-defined `IDebuggerThreadService` / `IDebuggerThreadControlService` contracts.

No platform-plugin revision is required merely because the host SDK added optional contracts that the plugin does not use.

## Capability Mapping

The existing capability model remains authoritative. Rev1 reuses the already-reserved debugger flags and adds only the missing distinction between thread enumeration and thread control.

| Capability | Expected attached debugger service |
| --- | --- |
| `Debugger` | target session provides `IDebuggerProvider`; provider can create an `IDebuggerSession` |
| `ThreadEnumeration` | attached debugger session may provide `IDebuggerThreadService` |
| `ThreadControl` | attached debugger session may provide `IDebuggerThreadControlService` |
| `RegisterAccess` | attached debugger session may provide `IDebuggerRegisterService` |
| `Breakpoints` | attached debugger session may provide `IDebuggerBreakpointService` for execute breakpoints |
| `Watchpoints` | the same breakpoint service may accept read/write/access watchpoint requests supported by the backend |
| `CallStack` | attached debugger session may provide `IDebuggerCallStackService` |
| `StepExecution` | attached debugger session may provide `IDebuggerStepService` |

A future host command must require both the appropriate advertised capability and the corresponding runtime service. A capability flag is not permission to cast to a platform type, and the presence of a service must not be inferred from the platform name.

## Provider and Attached Session

The debugger has two public lifecycle levels.

### `IDebuggerProvider`

`IDebuggerProvider` belongs to the connected target session and owns the platform-specific attach operation:

```text
connected ITargetSession
        |
        +-- GetService<IDebuggerProvider>()
                 |
                 +-- AttachAsync(TargetProcess)
                          |
                          v
                   IDebuggerSession
```

The provider receives the neutral `TargetProcess` selected as Active Target. A plugin maps that process into its backend's real PID/thread/debugger representation internally.

### `IDebuggerSession`

An attached debugger session owns:

- the target process it actually attached to;
- current neutral execution state;
- debugger events;
- Pause;
- Continue;
- explicit Detach;
- asynchronous disposal;
- optional attached-session services.

Optional services are intentionally retrieved from the attached debugger session rather than from the general target session. Thread/register/breakpoint/call-stack state is meaningful only inside the debugger attachment that owns the backend event stream. Thread enumeration and thread control are deliberately separate services so a backend can expose a thread list without implementing suspend/resume.

## Neutral Execution State

Plugin API `2.12.0` defines:

```text
Unknown
Running
Paused
Detached
```

This is backend execution state, not the complete Core lifecycle.

Core additionally defines:

```text
Detached
Attaching
Attached
Running
Paused
Detaching
Faulted
```

`Attached` means the backend is attached but has not provided a stronger Running/Paused state. `Faulted` records an attach failure after cleanup; an explicit detach/reset returns the coordinator to `Detached` before another attach attempt.

## Core Session Coordinator

`DebuggerSessionCoordinator` is the first shared debugger orchestration primitive.

It owns one immutable `DebuggerSessionIdentity`, one requested `TargetProcess`, one neutral `IDebuggerProvider`, and at most one active `IDebuggerSession`.

The coordinator:

- serializes attach, pause, continue, and detach operations;
- permits attach only from `Detached`;
- validates that the backend returned the same process id and process name that was requested;
- rejects a provider that reports a detached session as the result of a successful attach;
- subscribes only to the active attached session's event stream;
- maps backend Running/Paused state into the shared Core state machine;
- unsubscribes before explicit detach/disposal;
- disposes a backend session returned by a failed attach;
- returns to `Detached` after explicit cleanup;
- exposes optional debugger-session services only while the coordinator is attached;
- ignores events that arrive after the active session has been cleared or from a different sender.

The coordinator deliberately does **not** know how a PS5, Windows process, Xbox 360, emulator, or offline target performs debugging.

## Session Identity

`DebuggerSessionIdentity` uses the same safety concept already proven by the Disassembler and Memory Viewer:

```text
PluginId
ProcessId
ProcessName
ConnectionGeneration
```

`DisplayName` is not identity because it is presentation metadata.

The rev2 modeless Debugger workspace captures this identity when it is opened and revalidates it against the current plugin/process/connection generation before operations and when events reach the UI. Disconnect/reconnect therefore creates a different debugger identity even if the process happens to have the same name or PID.

Target identity is intentionally separate from temporary command availability. A scan/read/write or another foreground reservation may temporarily make `CanOpenDebugger` false without changing plugin/process/connection identity. Existing attached-session events remain current in that situation. Creating a new attachment still requires both a current identity and the stricter operational gate.

## Event Identity and Ordering

The platform event itself is intentionally neutral. `DebuggerEvent` can describe:

- attached/detached lifecycle events;
- pause/resume;
- breakpoint/watchpoint hits;
- completed steps;
- exceptions/signals/backend stops;
- thread creation/exit;
- other backend events;
- resulting execution state;
- stop reason;
- optional thread id;
- optional instruction pointer;
- optional backend-neutral message;
- event timestamp.

Core never forwards a raw platform event by itself. `DebuggerSessionCoordinator` wraps every accepted event in `DebuggerEventContext` containing:

- the immutable `DebuggerSessionIdentity`;
- a positive, monotonically increasing session-local sequence number;
- the neutral `DebuggerEvent`.

This is the required base for future event logs, Find What Writes/Accesses hit lists, Break and Trace, and stale-session rejection.

## Thread Model

`DebuggerThreadInfo` contains only neutral information:

- opaque numeric thread id;
- optional display name;
- neutral state (`Unknown`, `Running`, `Suspended`, `Stopped`, `Exited`).

`IDebuggerThreadService` provides thread enumeration only. `IDebuggerThreadControlService` provides suspend/resume operations. The separate `ThreadControl` capability and service exist because a backend may be able to enumerate threads without safely controlling individual threads.

Backend concepts such as FreeBSD LWP ids or Windows thread handles remain plugin-private.

## Register Model

`DebuggerRegister` contains:

- stable backend/plugin-defined register id;
- display name;
- bit width;
- register value as an immutable snapshot of bytes;
- optional group/category;
- neutral semantic role;
- per-register `CanWrite` state.

The semantic roles currently defined are:

```text
None
InstructionPointer
StackPointer
FramePointer
```

These roles allow future WPF/Core behavior to recognize important navigation registers without testing architecture-specific names such as `RIP`, `RSP`, `RBP`, `PC`, or PowerPC register identifiers.

Register values are opaque fixed-width bit vectors at the SDK boundary. The plugin owns conversion from its backend representation. WPF must not parse target-specific register structures.

`DebuggerRegisterWriteRequest` carries a register id and a copied value payload. The backend remains responsible for validating whether that exact register can be changed in the current stop state and whether the supplied width is legal.

## Breakpoint and Watchpoint Model

Rev1 defines the common representation before any plugin begins setting breakpoints.

A `DebuggerBreakpointRequest` contains:

- address;
- byte size;
- software or hardware kind;
- Execute, Read, Write, or ReadWrite trigger semantics;
- temporary/permanent intent.

`DebuggerBreakpoint` adds an opaque backend/plugin id and enabled state.

This common model is required so later host features can share one breakpoint manager and temporary-breakpoint mechanism. Platform slot counts, alignment rules, legal watch sizes, software-breakpoint patch mechanics, and backend handles must remain inside the implementing plugin/service.

Rev1 does not set, remove, or advertise any real breakpoint/watchpoint.

## Call Frames

`DebuggerStackFrame` contains neutral frame information:

- frame index;
- instruction address;
- optional stack pointer;
- optional frame pointer;
- optional return address;
- optional module name;
- optional symbol name.

The backend decides how frames are unwound. Core must not assume frame-pointer walking, DWARF, Windows unwind metadata, x64, or PowerPC.

## Step Contract

`DebuggerStepKind` currently identifies Into, Over, and Out as neutral requested operations.

The existence of the enum does not require every backend to implement every step directly. Later host revisions may compose Step Over/Step Out from verified Disassembler information, call frames, and temporary breakpoints when that is safer or more portable than a backend-specific direct command.

## Rev2 Host Workspace and Lifetime Ownership

`0.1.7.rev2` is the first production consumer of the verified rev1 coordinator.

The main target strip exposes **Debugger...** only when the selected plugin advertises `TargetCapabilities.Debugger`. Opening the window captures the current `TargetProcess` and host connection generation; opening a window does not attach automatically.

The workspace provides:

- explicit Attach;
- Pause while Running;
- Continue while Paused;
- explicit Detach;
- current shared lifecycle state;
- a bounded 2,000-row neutral event history;
- sequence, timestamp, event kind, execution state, stop reason, thread id, instruction pointer, and message presentation;
- stale-target rejection before operations and before event presentation.

`PluginViewModel` owns registrations for every `DebuggerSessionCoordinator` created against its current target session. Those coordinators are disposed before:

- a connected target session is replaced;
- Disconnect disposes the target session;
- Active Target changes;
- the plugin ViewModel is disposed during application shutdown.

This ownership rule is required for every future modeless debugger backend. A window may outlive the validity of the target it was created for, but its backend attachment may not.

## Rev2 Mock Backend

Mock plugin `1.0.0.rev8` is the first backend implementation and targets Plugin API `2.12.0`.

It advertises only `TargetCapabilities.Debugger` from the debugger capability family. Its connected target session exposes `IDebuggerProvider`, which accepts only the existing deterministic `TestGame.exe` process and permits one active debugger session at a time.

The attached Mock session behaves deterministically:

```text
Attach    -> Running
Pause     -> Paused  + Paused/PauseRequested event
Continue  -> Running + Resumed event
Detach    -> Detached
```

The Pause/Continue event context uses:

```text
Thread ID:            1
Instruction pointer:  0x10000400
Pause message:        Mock target paused.
Continue message:     Mock target resumed.
```

Disposing the Mock target session disposes any active debugger session before the target session becomes disconnected.

Rev2 intentionally provides no `IDebuggerThreadService`, `IDebuggerThreadControlService`, `IDebuggerRegisterService`, `IDebuggerBreakpointService`, `IDebuggerCallStackService`, or `IDebuggerStepService`. The plugin must not advertise the corresponding capabilities until those implementations exist.

## Rev3 PS5 Debugger Backend (verified through rev5)

Application `0.1.7.rev3` introduced the first real-hardware debugger backend candidate. Rev4/rev5 changed host presentation around it without redesigning the backend, and the complete transport/lifecycle path is hardware-verified as part of rev5. The PS5 plugin does not add a second host debugger architecture: it implements the same `IDebuggerProvider` / `IDebuggerSession` boundary already verified through Mock.

### Transport ownership

Each PS5 debugger attachment owns a dedicated `Ps5DebuggerCommandClient` connection to the configured ps5debug-NG command port. This connection is intentionally separate from:

- the primary `Ps5DebugClient` used by process enumeration, memory, maps, scans, and normal process control;
- the optional `Ps5ConcurrentMemoryWriter` used by Frozen writes.

The debugger command transport serializes debugger commands on its own stream. Core/WPF never sees packet magic, command ids, status words, port numbers, or packet layouts.

### Attach and event-channel sequence

ps5debug-NG's attach handshake requires the client to be listening before attach because the console connects outbound to the debugger client. Rev3 therefore uses this order:

```text
open TCP listener on local port 755
        |
open dedicated command connection to configured ps5debug-NG port
        |
send CMD_DEBUG_ATTACH (0xBDBB0001) + int32 PID
        |
ps5debug-NG connects back to client TCP 755
        |
receive attach success + accept event socket
        |
IDebuggerSession starts Running
```

Attach fails cleanly when TCP 755 cannot be bound, the dedicated command connection cannot be opened, ps5debug-NG reports an existing debugger, or the outbound event connection does not arrive within the bounded attach timeout. Failure cleanup closes all temporary sockets and attempts backend detach if the attach command had already succeeded.

### Execution control

Attached Pause/Continue uses ps5debug-NG `CMD_DEBUG_CONTINUE` / stop-go (`0xBDBB0010`) with a four-byte request body whose first byte is the action:

```text
0 = resume
1 = pause / stop
2 = kill (not exposed by the current neutral UI)
```

Rev3 exposes only actions `0` and `1`. This path is distinct from the PS5 target session's already verified general `IProcessControl` implementation using `CMD_DEBUG_PROCESS_STOP` (`0xBDBB0500`). The shared debugger UI does not know either command id.

A requested Pause returns neutral state `Paused` plus `Paused/PauseRequested`. Continue returns `Running` plus `Resumed`. Current ps5debug-NG filters the SIGSTOP used by debugger stop-go from its async event stream, so rev3 intentionally emits the requested Pause event locally rather than waiting for a duplicate interrupt packet.

### Async interrupt packet

The TCP 755 channel carries fixed 1184-byte packets. Rev3 decodes only fields needed by the current neutral session:

```text
0x000  uint32  LWP/thread id
0x004  uint32  wait status
0x008  40-byte thread-name field
0x030  176-byte GP register block
...
0x0B8  uint64  instruction pointer (0x030 + GP offset 0x88)
```

The remaining FPU/debug-register data stays plugin-private and is not exposed until later register/watchpoint revisions define and verify their neutral service behavior.

When an interrupt packet arrives, the PS5 session enters neutral `Paused` and emits an event containing the thread id, instruction pointer, wait-status-derived signal stop reason, and a readable thread/signal message. This is correct for current ps5debug-NG: after sending the interrupt packet, the backend calls its application-layer resume helper but does **not** issue `PT_CONTINUE` for the traced process. The ptrace stop therefore remains active until stop-go action `0` is processed by Continue.

Unexpected event-channel loss while still attached maps the backend session to neutral `Unknown` and emits a backend diagnostic event. Intentional detach/disposal suppresses that diagnostic.

### Ownership and cleanup

`Ps5DebuggerProvider` permits one active attached debugger session per connected PS5 target session. `Ps5TargetSession.DisposeAsync()` disposes the debugger provider before closing the concurrent writer and primary client. The attached session performs best-effort backend detach, cancels/shuts down the event channel, closes the event and command sockets, and releases provider ownership so a later window can attach again.

PS5 rev25 advertised only the coarse `Debugger` flag. Rev6 advanced the plugin to rev26 and activated `ThreadEnumeration` and `ThreadControl` in addition to `Debugger`. Rev7 advances it to rev27/API `2.13.0` and additionally advertises `RegisterAccess`; Breakpoints, Watchpoints, CallStack, and StepExecution remain off.

## Rev6 Threads and Thread Control

`0.1.7.rev6` is the first production consumer of the optional thread contracts that were intentionally defined in rev1. It does not introduce a new debugger architecture or a new Plugin API version.

### Host presentation

The Debugger workspace adds a left-side **Threads** pane only when `ThreadEnumeration` is advertised. The pane contains:

- a neutral hexadecimal thread id;
- optional thread name;
- neutral `DebuggerThreadState`;
- explicit **Refresh**;
- single-row selection.

If `ThreadControl` is also advertised, **Suspend** and **Resume** are shown. These actions require the overall debugger session to be Running. They are disabled while the whole target is Paused so whole-process stop semantics are never mixed with an individual-thread control request.

The host obtains services only from the attached `IDebuggerSession` through `DebuggerSessionCoordinator.GetService<T>()`. Capability advertisement and runtime service availability must both be present. Thread selection is preserved by neutral id across refreshes. Attach/Pause/Continue refresh the thread snapshot; Detach and stale-target invalidation clear it.

### Mock backend

Mock `1.0.0.rev9` advertises `Debugger | ThreadEnumeration | ThreadControl` and exposes three deterministic threads:

```text
0x1  Main
0x2  Worker
0x3  Render
```

Normal rows are Running while the target runs and Stopped while the entire target is paused. A thread explicitly suspended through `IDebuggerThreadControlService` remains `Suspended` across whole-target Pause/Continue until Resume is requested. This deterministic fixture provides the first safe runtime acceptance path for the shared thread UI before live-PS5 control is exercised.

### PS5 backend

PS5 `0.1.0.rev26` implements the same services without exposing ps5debug-NG terminology to the host. The plugin-private command mapping is:

```text
0xBDBB0005  get thread list
0xBDBB0006  suspend thread
0xBDBB0007  resume thread
0xBDBB0011  get thread info
```

Thread enumeration reads the backend count and 32-bit ids, bounds the count defensively, then requests the optional 40-byte thread-info response for names. A failed thread-info request does not discard an id that was successfully enumerated. The complete list/info sequence is serialized on the already-dedicated debugger command connection.

Current ps5debug-NG list/info responses do not provide a trustworthy individual-thread execution-state field. The PS5 plugin therefore reports:

- `Running` when the debugger target is Running;
- `Stopped` when the whole debugger target is Paused;
- `Suspended` for thread ids successfully suspended through this debugger session until a successful Resume or until the id disappears from a later enumeration.

That state is an ownership-aware neutral projection, not an attempt to infer undocumented kernel state. Future backends with richer thread-state APIs may populate the same neutral model directly.

## Rev7 Registers and Stop Context

`0.1.7.rev7` activates the register service that was defined in the debugger foundation and adds the neutral value-encoding metadata required for safe generic presentation. `DebuggerRegisterValueEncoding` is the only public API addition, advancing Plugin API to `2.13.0`.

The Registers pane appears only when `RegisterAccess` is advertised and the attached session supplies `IDebuggerRegisterService`. A snapshot is meaningful only while Paused and for the currently selected thread; Continue, Detach, stale-target invalidation, or loss of selection clears it.

Register rows carry bytes, bit width, writability, optional group, value encoding, and semantic role. The host uses `DebuggerRegisterRole.InstructionPointer` for the current-instruction address and reuses the normal Disassembler launcher. It never tests for a string such as `RIP`. Writable backends use the shared Edit -> Write -> Refresh -> byte-for-byte verification path.

Mock `1.0.0.rev10` supplies deterministic writable 64-bit snapshots for Main/Worker/Render. PS5 `0.1.0.rev27` supplies read-only general-register snapshots from ps5debug-NG `0xBDBB0008`; its 176-byte FreeBSD amd64 layout and offsets remain plugin-private. PS5 SETREGS is deliberately not exposed in rev7 because that upstream path has not been hardware-verified. FPU/SIMD/debug-register state is left to rev8.

The rev6 individual PS5 ThreadControl request remains implemented but externally blocked on the tested ps5debug-NG payload, which returns `CMD_ERROR`. The issue is documented under `docs/bug-reports/`; it does not change the neutral ThreadControl contracts.

## Rev7 Non-Goals

Rev7 does **not** implement:

- floating/SIMD/debug-register state;
- PS5 register editing / SETREGS;
- breakpoint or watchpoint management;
- call stacks;
- stepping/run-to operations;
- Find What Writes / Reads / Accesses;
- Break and Trace;
- debugger export.

## Development Order After Rev7

The next milestones remain dependency ordered:

1. **rev8 — Extended Register State.**
2. **rev9 — Breakpoint Manager and Software Breakpoints.**
3. **rev10 — Hardware Watchpoints.**
4. **rev11 — Call Stack and Call Frames.**
5. **rev12 — Stepping and Run-to Operations.**
6. **rev13 — Integration, Export and Finalization.**

`Find What Writes / Reads / Accesses` follows the verified debugger block, and `Break and Trace` follows the code-finder/watchpoint infrastructure.

## Extension Rules

Future debugger work must preserve these rules:

1. Do not expose platform debugger packet structures through the Plugin SDK.
2. Do not put architecture-specific register names, breakpoint encodings, or thread-handle semantics in Core/WPF.
3. Require both coarse capability advertisement and the matching runtime service before enabling a feature.
4. Bind every modeless debugger instance and every accepted event to plugin/process/connection generation.
5. Never reinterpret a stale event as belonging to a newly connected target.
6. Keep debugger transport ownership inside the platform plugin. If a backend benefits from a dedicated command/event connection, do not route it through generic target-memory code merely to avoid a second plugin-private transport.
7. Reuse the existing Memory Viewer and Disassembler for memory/code navigation instead of implementing debugger-local duplicates.
8. Use temporary breakpoints as first-class breakpoint records so later Step Over/Out/Run-to workflows can share one cleanup path.
9. Keep backend slot limits, legal breakpoint sizes, register layouts, stack-unwind mechanics, and exception/signal details plugin-owned.
10. Do not advertise `Debugger`, `Breakpoints`, `Watchpoints`, `RegisterAccess`, `ThreadEnumeration`, `ThreadControl`, `CallStack`, or `StepExecution` until the corresponding backend behavior is implemented and verified.
