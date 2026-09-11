# Debugger Architecture

## Purpose

This document defines the shared debugger architecture introduced by TeeKay87's Memory Engine `0.1.7.rev1`, first consumed by the host/Mock implementation in rev2 and connected to real PS5/ps5debug-NG transport in rev3. Thread/register services followed through rev6-rev10, rev11-rev15 established and fully verified the capability-driven Breakpoint Manager plus persistent/temporary software execute breakpoints, rev16 completed hardware data watchpoints, rev17 added Call Stack/Call Frames and stepping/run-to workflows on top of those verified layers, rev19 introduced independent modeless tool-window lifecycle, and rev20/rev21 completed the dispatcher-safe MainWindow/tool shutdown correction. Rev24 removed the repeatedly problematic WPF tab chrome; its selector-button workspace, larger default Debugger size, splitters, and tool cleanup are runtime-accepted. Rev25 then corrected the live PS5 logical software-breakpoint stop context and fast execution-status race; its **132/132**, MainWindow alignment, Mock regression, and live logical-stop/Step-Into gates passed. Rev26 corrected host Step Over when that logical stop sits on a software-breakpointed `call`: the host preserves the original neutral instruction before backend breakpoint installation and consults it before breakpoint-patched live disassembly. Rev27 added explicit ownership/interrupt cleanup for host-composed temporary breakpoints and defensive PS5 software-breakpoint restoration before detach/disposal. Rev28 carried that production implementation unchanged and corrected only the stale deferred-stop source assertion exposed by rev27's 134/135 Windows run. Rev29 kept those verified execution/cleanup paths and added a host/Core logical-disassembly overlay so debugger-owned Software/Execute `INT3` bytes are replaced only in the local decode buffer with the captured original instruction bytes. Rev30 added neutral request pre-validation, Breakpoint/Watchpoint Type-versus-Mechanism presentation, and main-workspace Add Breakpoint/Add Watchpoint shortcuts. Current `0.1.7.rev31` keeps those host features and strengthens PS5 teardown: every client-owned backend-active or staged hardware-watchpoint slot is explicitly disabled before backend detach/disposal, matching the defensive software-breakpoint restore pass already present since PS5 rev36. A generic `Markers` column continues to present breakpoint/watchpoint state separately from code bytes, without changing target memory or introducing platform-specific logic into Core/WPF.

The debugger must remain a host-level workflow with platform-specific execution delegated to plugins. The WPF application and Core must never learn ps5debug-NG packet layouts, x86 debug-register semantics, Windows debugging APIs, Xbox 360 debugger commands, or any other backend-specific protocol detail.

Application `0.1.7.rev1` is the verified contract/Core foundation and passed **84/84** Windows checks. Rev2 is fully verified after **89/89** plus focused Mock acceptance; rev5 fully hardware-verified the real PS5 debugger transport at **97/97** plus focused acceptance; rev6 passed **101/101** for thread services; rev8 passed **107/107** plus all Registers/Stop Context gates; and rev10 remains the verified guarded-register baseline after **110/110** plus live-PS5 runtime/cleanup acceptance. Rev14 passed **114/114** plus comprehensive Mock/live-PS5 breakpoint runtime acceptance, rev15 passed **114/114** plus its focused runtime-fix gates, and rev16 Hardware Watchpoints passed **118/118** plus complete focused Mock/live-PS5 acceptance. Rev17 consumed the existing Plugin API `2.15.0` call-stack/step contracts and passed its complete **124/124** automated gate. Rev24 established the accepted selector-button workspace after the rev18-rev23 tab-rendering correction cycle. Rev25 passed **132/132**, status-bar alignment, focused Mock regression, and the live logical software-breakpoint stop-context/Step-Into gate. Rev26 passed **133/133** and live-corrected breakpoint-aware Step Over at `0xE9F74E`, but a later Run-to interruption left its operation-owned temporary breakpoint active and detach from that state was followed by target termination. Rev27 raised the registry to 135 checks, added neutral interrupted-operation cleanup, and updated PS5 teardown to explicitly restore active/staged software-breakpoint slots before detach/disposal. Its Windows run completed **134/135** because one older source contract still expected the obsolete Continue-only deferred-stop branch. Rev28 corrected that verifier assertion only and subsequently passed **135/135** plus the focused live-PS5 cleanup acceptance. Rev29 raised the registry to **138** with logical debugger-byte overlay, overlay-lifecycle, and Disassembler marker/source coverage while preserving the accepted debugger execution paths. Rev30 then raised the planned registry to **141** for request validation, classification, and row actions, but was superseded before verification when live shutdown testing exposed stale PS5 hardware-watchpoint teardown state. Current rev31 targets **142** checks with an additional PS5 disposal regression that requires active and staged hardware-watchpoint slots to be disabled before backend detach.

## Verified Base

Debugger development begins from the completed `0.1.6.rev14` Disassembler feature block.

`0.1.6` is fully tested and hardware-verified, including the neutral disassembly contract, Core-owned bounded reads, real PS5 x86-64 decoding, continuous origin resolution, navigation/history, readable-region navigation, selection/copy behavior, universal export, and stale-session protection.

The debugger must integrate with those verified services rather than replace or duplicate them.

## Ownership Boundary

| Layer | Debugger responsibility |
| --- | --- |
| `TeeKay87.MemoryEngine.PluginSdk` | Public debugger contracts, neutral states/events/threads/registers/frames/breakpoints/step models, coarse capability flags |
| `TeeKay87.MemoryEngine.Core` | Session identity, lifecycle/state coordination, event identity/ordering, backend-independent safety rules |
| `TeeKay87.MemoryEngine.App` | Modeless Debugger workspace, target/session ownership, commands, neutral event/frame presentation, navigation into existing Memory Viewer/Disassembler |
| platform plugin | Attach/detach implementation, debugger transport, backend event decoding, real thread/register/breakpoint/call-stack/step operations |

The Plugin SDK does not reference Core or WPF. Core does not reference a concrete platform plugin.

## Plugin API 2.12.0

`0.1.7.rev1` advances the public Plugin API from `2.11.0` to `2.12.0` because it adds public debugger contracts and models.

The compatibility rule remains unchanged: a 2.x host accepts plugins targeting the same or an older 2.x minor version. In verified rev1, Mock `1.0.0.rev7` and PS5 `0.1.0.rev24` both remained compatible `2.11.0` plugins. Rev2 advanced Mock to `1.0.0.rev8` / API `2.12.0` because it consumes `IDebuggerProvider`/`IDebuggerSession`; rev3 advanced PS5 to `0.1.0.rev25` on the same API; rev6 advanced Mock/PS5 for thread-service consumption without changing the API. Rev7 advanced the public Plugin API to `2.13.0` by adding neutral `DebuggerRegisterValueEncoding` metadata. Rev9 does not require another public API change: arbitrary register widths, grouping, semantic roles, writability, and value encoding are already represented by `DebuggerRegister`. Rev11 adds the optional `IDebuggerBreakpointStateService` and advances the public API to `2.14.0`. Rev16 advances the public API to `2.15.0` by adding optional `DebuggerEvent.TriggeredBreakpoint` context while preserving the original event constructor. This allows a watchpoint event to identify the watched neutral record/address while `InstructionPointer` continues to mean the instruction that caused the access. Mock `1.0.0.rev15` and PS5 `0.1.0.rev36` both targeted API `2.15.0`; rev17 introduced their `CallStack` and `StepExecution` capability advertisement because both attached sessions expose the matching neutral services. Rev25 did not change the public API; the PS5 plugin revision advanced only because it added platform-private logical software-breakpoint stop reconciliation. Rev26 was host-only and left Plugin API `2.15.0` plus both plugin versions unchanged. Rev27 left the public API unchanged while advancing only the PS5 plugin to `0.1.0.rev36` for platform-private software-breakpoint detach cleanup. Rev30 advanced the public API to `2.16.0` with optional non-mutating breakpoint-request validation; Mock advanced to `1.0.0.rev16` and PS5 to `0.1.0.rev37`. Current rev31 keeps API `2.16.0` and Mock unchanged while PS5 advances to `0.1.0.rev38` for plugin-private hardware-watchpoint teardown cleanup.

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

Rev1 did not set, remove, or advertise any real breakpoint/watchpoint. Rev11 first activated software execute breakpoints; rev16 activates hardware data watchpoints through the same service/model boundary.

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

The existence of the enum does not require every backend to implement every step directly. Rev17 uses that design intentionally: plugins provide the native Step Into primitive through `IDebuggerStepService`, while the host composes Step Over, Step Out, and Run to Address from verified Disassembler information, neutral call frames, and the existing temporary software-breakpoint mechanism. This keeps architecture- and transport-specific stepping details inside plugins without duplicating shared control flow in each backend.

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

Rev3 deliberately decoded only the event fields needed by the neutral event model. Ordinary paused register inspection still uses the explicit debugger command paths introduced later. Rev25 adds one narrowly scoped exception for a managed software-breakpoint hit: the PS5 plugin preserves the packet-resident 176-byte GP block plus the 832-byte FPU block beginning at `0x0E0` as that thread's logical breakpoint stop snapshot. The complete packet and offsets remain plugin-private; no packet structure is exposed through the Plugin SDK.

When an interrupt packet arrives, the PS5 session enters neutral `Paused` and emits an event containing the thread id, instruction pointer, wait-status-derived signal stop reason, and a readable thread/signal message. Ordinary interrupt stops continue to use the established live register path. A matched software breakpoint has different timing in current ps5debug-NG: the backend restores the saved instruction byte, rewinds the packet RIP to the breakpoint address, writes that RIP to the stopped thread, performs `PT_STEP`, waits for that instruction to complete, rearms `INT3`, and only then sends the original 1184-byte packet. The packet therefore describes the **logical pre-instruction breakpoint stop**, while a later GETREGS already sees post-instruction live state. Rev25 makes that packet snapshot authoritative for matching register inspection and Call Stack frame zero. A Step Into requested from that logical stop consumes the backend's already-completed transparent step and reports its current live RIP without issuing a second native step. This is client-side semantic reconciliation only; side effects of the backend's transparent step are not rolled back. The external behavior is documented in `docs/bug-reports/ps5debug-ng-software-breakpoint-event-and-live-register-state-diverge.md`.

Unexpected event-channel loss while still attached maps the backend session to neutral `Unknown` and emits a backend diagnostic event. Intentional detach/disposal suppresses that diagnostic.

### Ownership and cleanup

`Ps5DebuggerProvider` permits one active attached debugger session per connected PS5 target session. `Ps5TargetSession.DisposeAsync()` disposes the debugger provider before closing the concurrent writer and primary client. The attached session performs best-effort backend detach, cancels/shuts down the event channel, closes the event and command sockets, and releases provider ownership so a later window can attach again.

PS5 rev25 advertised only the coarse `Debugger` flag. Rev6 advanced the plugin to rev26 and activated `ThreadEnumeration` and `ThreadControl` in addition to `Debugger`. Rev7 advanced it to rev27/API `2.13.0` and additionally advertised `RegisterAccess`. Rev9 advanced the plugin to rev28 and first consumed the optional extended-register read commands. Rev10 advances it to rev29 and guards those optional reads with negotiated eligibility, disposable bounded probes, and per-attachment degradation; Breakpoints, Watchpoints, CallStack, and StepExecution remain off.

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

## Registers and Stop Context — Verified Through Rev8

`0.1.7.rev7` activated the register service defined in the debugger foundation and added the neutral value-encoding metadata required for safe generic presentation. `DebuggerRegisterValueEncoding` was the only public API addition, advancing Plugin API to `2.13.0`. Rev8 corrected a verifier-only constructor-source assertion and then completed the full rev7 acceptance: **107/107** Windows checks plus all **11/11** focused Mock/live-PS5 register/stop-context steps passed.

The Registers pane appears only when `RegisterAccess` is advertised and the attached session supplies `IDebuggerRegisterService`. A snapshot is meaningful only while Paused and for the currently selected thread; Continue, Detach, stale-target invalidation, or loss of selection clears it.

Register rows carry bytes, bit width, writability, optional group, value encoding, and semantic role. The host uses `DebuggerRegisterRole.InstructionPointer` for the current-instruction address and reuses the normal Disassembler launcher. It never tests for a platform register name. Writable backends use the shared Edit -> Write -> Refresh -> byte-for-byte verification path.

Mock `1.0.0.rev10` supplied the verified writable 64-bit acceptance path. PS5 `0.1.0.rev27` supplied verified read-only general-register snapshots from ps5debug-NG `0xBDBB0008`; its 176-byte FreeBSD amd64 layout and offsets remain plugin-private. PS5 SETREGS remains deliberately unexposed because the upstream write path has not been hardware-verified.

The rev6 individual PS5 ThreadControl request remains implemented but externally blocked on the tested ps5debug-NG payload, which returns `CMD_ERROR`. The issue is documented under `docs/bug-reports/`; it does not change the neutral ThreadControl contracts.

## Rev9 Extended Register State — Acceptance Result

Rev9 deliberately reused the existing public register model instead of adding architecture-specific SDK types. Backends can append any fixed-width register group through `DebuggerRegister`; the shared host continues to format arbitrary widths through neutral value encoding.

Mock `1.0.0.rev11` preserved the verified writable 64-bit General/Control rows and added deterministic read-only 80-bit floating-point, 128-bit SIMD, 256-bit SIMD, and 64-bit Debug fixtures. Its focused presentation/write/lifecycle gates passed.

PS5 `0.1.0.rev28` kept the mandatory verified GETREGS read and added the following optional selected-thread reads:

| Backend command | Command id | Response | Neutral rows |
| --- | --- | ---: | --- |
| GETREGS | `0xBDBB0008` | 176 bytes | existing general/control/segment/stop-context rows |
| GETFPREGS | `0xBDBB000A` | 832 bytes | FPU control, ST0-ST7, XMM0-XMM15, YMM0-YMM15 |
| GETDBREGS | `0xBDBB000C` | 128 bytes | DR0-DR3, DR6, DR7 |
| GETFSGSBASE | `0xBDBB000E` | 16 bytes | FSBASE, GSBASE |

The mappings themselves remain valid and plugin-private. The 832-byte FPU block is interpreted as the amd64 FXSAVE-compatible state plus xstate YMM upper halves; x87 stack values remain exact 80-bit raw patterns. Only meaningful debug-register slots are exposed. All PS5 rows remain read-only.

The rev9 transport decision did not survive live acceptance. Rev9 issued the optional blocks serially on the debugger-owner command connection after GETREGS. The Windows suite passed **108/108**, and focused splitter/Mock Gates B-D passed. During live PS5 Gate E, Attach and global Pause succeeded, the UI remained responsive, the target was paused, and 82 threads were enumerated, but the register refresh never completed and remained at `Registers 0` with no current instruction. Because the optional post-dispatch response read deliberately used an uncancelled token to protect a shared framed stream, a non-completing optional operation could hold the complete register refresh indefinitely.

That live result supersedes rev9 as the accepted PS5 extended-register transport. The rev10 source review then identified a concrete backend hazard in addition to the host-side unbounded wait: the current ps5debug-NG `GETDBREGS` path can block when it is called after the debugger has already paused the target.

The current Pause path sends `SIGSTOP` and consumes the stop notification with a blocking `wait4()`. `debug_getdbregs_handle()` subsequently calls `is_process_stopped()`, whose non-blocking `wait4()` sees no unread stop notification and can therefore report false even though the process is still stopped. GETDBREGS then sends a second stop signal and waits for another stop transition. Because the target was already stopped, that blocking wait may never complete. The server executes debugger commands behind shared debugger/process mutexes, so a blocked GETDBREGS can also prevent later Continue/Detach operations from progressing. The external backend report is `docs/bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md`.

## Rev10 PS5 Extended Register Transport Guard

Rev10 keeps the verified general-register path separate from optional state and changes only PS5 plugin-private transport behavior.

### Negotiated eligibility

The normal PS5 target connection already reads the backend protocol version, branding payload, optional capability level, and firmware version. Rev10 carries that immutable connection metadata into the debugger provider. Automatic safe extended-register probes require:

- protocol version `1.3` or newer; and
- capability level `1.0` or newer.

If either value is absent, malformed, or older, `IDebuggerRegisterService.GetRegistersAsync` returns the mandatory general-register snapshot and sends no optional register command. This is a compatibility gate, not a new public capability flag; the neutral `RegisterAccess` capability still describes the overall register surface.

### Owner-stream rule

`GETREGS` remains on the debugger-owner command connection exactly as in the verified rev8 path. Attach/Detach, thread enumeration/control, Pause/Continue, and mandatory general-register framing do not share a connection with the safe optional probes.

This distinction is intentional. A framed request that is canceled after only part of its response has arrived cannot be safely reused. Rev10 therefore never times out an optional command and then resumes using that same socket.

### Safe disposable optional probes

The current paused-state probe set is deliberately limited to GETFPREGS and GETFSGSBASE. Each eligible operation opens a fresh ps5debug-NG command connection and performs exactly one fixed-size optional read. The probe uses a linked **two-second** timeout. The connection is disposed after success or failure.

The following conditions make only that optional group unavailable:

- bounded timeout;
- backend `CMD_ERROR` / data-null response;
- connection/socket failure;
- unexpected probe-local status or malformed/truncated probe response.

Caller/session cancellation is not swallowed; it still propagates normally.

Because a failed probe connection is discarded, there is no partially consumed optional frame to poison the debugger-owner stream. The current ps5debug-NG server source also assigns debugger ownership only to the connection that performs Attach; non-owner server-client cleanup does not invoke full debugger teardown. That source contract is why short-lived optional connections are used rather than canceling a request on the attached owner connection.

### GETDBREGS suppression while Paused

Rev10 does **not** send `GETDBREGS` from the Registers pane while the debugger state is Paused. This is not a missing neutral model: the rev9 plugin-private 128-byte decoder for DR0-DR3/DR6/DR7 remains available. The suppression is a backend-safety boundary caused by the current server stop-detection/wait sequence described above.

The current safe full PS5 paused snapshot is therefore:

- 26 mandatory general/control/stop-context rows;
- 48 optional FPU/SIMD rows from GETFPREGS;
- 2 optional segment-base rows from GETFSGSBASE;
- **76 rows total** when both safe optional blocks succeed.

Debug-register rows are intentionally absent from the paused command path until ps5debug-NG is corrected and the replacement behavior is hardware-verified. Future breakpoint/watchpoint work must not re-enable the blocking GETDBREGS sequence merely to populate the register list. A later safe source may use corrected backend behavior or already-delivered debugger interrupt context.

### Per-attachment degradation

`Ps5DebuggerSession` remembers safe optional command ids that returned no usable block. A failed/timed-out group is not retried on every later Refresh during that same attachment. Successful safe optional groups remain independent and continue refreshing. Detach/dispose clears the unavailable set, so a new debugger attachment gets a fresh capability/probe decision.

The general snapshot is mapped before optional work and remains the authoritative stop-context fallback. If both safe optional probes succeed, the current 76-row surface is available. If one or both fail, only those groups disappear.

No SETREGS, SETFPREGS, SETDBREGS, or FS/GS write operation is exposed in rev10.

## Rev9/Rev10 Threads/Registers Pane Splitter

The left Debugger workspace continues to reuse `ProportionalGridSplitter`. Threads and Registers start at equal star-sized rows, producing a **50/50** split. The existing row splitter style, `ResizeBehavior=PreviousAndNext`, and **0.2 / 0.8** relative limits remain unchanged from rev9 and passed focused runtime/UI acceptance. Practical minimum heights remain in force, and `RegisterAccess`-absent backends collapse the register row and splitter without reserving empty space.

## Rev13 Breakpoints/Events Pane Splitter Default

The right Debugger workspace also reuses the same `ProportionalGridSplitter`; no debugger-specific splitter implementation is introduced. Rev13 changes only its initial star-row weighting to `13* / 7*`, giving Breakpoints approximately **65%** and Events approximately **35%** of the available right-side vertical workspace to match the preferred runtime layout. The existing **0.2 / 0.8** relative movement limits, pane minimum heights, theme-aware splitter style, and adaptive star sizing remain unchanged. When the active backend advertises neither `Breakpoints` nor `Watchpoints`, the Breakpoints / Watchpoints row and splitter collapse so Events receives the available space.

## Rev11 Breakpoint Manager and Software Execute Breakpoints

Rev11 reuses the existing neutral breakpoint request/record/service model and adds only the optional `IDebuggerBreakpointStateService` required for Enable/Disable. The WPF manager consumes `TargetCapabilities.Breakpoints`, `IDebuggerBreakpointService`, and when available the state service; it never knows backend slots or instruction encodings.

The current host workflow creates one-byte `Software` / `Execute` requests and preserves the request lifetime as persistent or temporary. Temporary breakpoints are first-class records because later Step Over, Step Out, and Run-to operations need the same cleanup mechanism. The manager provides Add, Refresh, Enable, Disable, Remove, Remove All, and navigation to the existing Disassembler.

Mock `1.0.0.rev13` provides deterministic software-breakpoint lifecycle and hit events and accepts execute breakpoints only inside the mapped synthetic code fixture. PS5 `0.1.0.rev32` maps the same neutral requests to ps5debug-NG `CMD_DEBUG_SET_BREAKPOINT` (`0xBDBB0003`) and keeps its 30-slot limit private. Before allocating or mutating a backend slot, the PS5 plugin validates the address against the latest memory-map snapshot already enumerated for the same process and requires an executable, non-guarded region. An unavailable map, unmapped address, non-executable region, or guarded region is rejected locally. No extra target/debugger socket command is introduced for this validation. The async event translator classifies a managed SIGTRAP event as a neutral breakpoint hit when the corrected event RIP matches an active managed breakpoint address. Persistent records remain active; temporary records are removed from the manager after their first hit.

PS5 paused Software/Execute Disable/Remove remains staged because current ps5debug-NG's disable handler can resume a paused target. Rev27 adds a distinct teardown rule: once detach has been requested, every software-breakpoint slot still marked backend-active, together with every slot already staged for paused cleanup, is explicitly disabled/restored before `CMD_DEBUG_DETACH`. The callback event channel stays open during this restore pass so backend interrupts caused by the disable path can be drained safely; while detach is active those packets are ignored for debugger state projection, and the channel is closed after the detach attempt. This pre-restore is best-effort and does not replace backend teardown; detach is still attempted even if a restore request fails. Session disposal uses the same defensive restore path when explicit detach did not already complete.

### PS5 paused-disable safety

Current ps5debug-NG restores the saved instruction byte when disabling a software breakpoint and then executes `PT_CONTINUE` before returning success. An immediate Disable/Remove command while the user has the target Paused would therefore resume execution as a side effect of breakpoint management. Rev11 stages enabled-breakpoint disable/remove requests while Paused and sends them only immediately before the next explicit Continue. Re-enabling before Continue cancels the pending disable. Slots with pending cleanup are not reused. The external behavior is recorded in `docs/bug-reports/ps5debug-ng-disabling-software-breakpoint-resumes-paused-target.md`.

The staging rule belongs only to the PS5 plugin; generic Core/WPF semantics remain "set enabled state" / "remove breakpoint".

### Immediate re-hit stop-context refresh

The host clears Registers when a debugger state transition reaches Running. A breakpoint can re-hit quickly enough that the corresponding Paused event arrives while the Continue command is still completing and the Debugger ViewModel is busy. Rev15 retains the newest Paused stop-context event only for that Continue transition and replays the existing Breakpoints/Threads/Registers refresh after Continue leaves its busy state, provided the session is still current and Paused. This avoids dropping the new stop context without introducing a second refresh for ordinary manual Pause.

## Rev16 Hardware Data Watchpoints

Rev16 reuses the neutral breakpoint contracts rather than introducing a second watchpoint collection or platform-specific host API. The manager is shown when either `Breakpoints` or `Watchpoints` is advertised. Its Add dialog exposes Software Execute Breakpoint and/or Hardware Watchpoint according to those capability flags. Hardware requests carry only neutral address, byte size, access, and lifetime. The active plugin owns all validation of legal sizes, alignment, access modes, mapped range, slot count, and native encoding.

The manager title is **Breakpoints / Watchpoints**. Refresh, Enable, Disable, Remove, and Remove All use the same service methods for both record kinds. The existing State column remains authoritative; no additional Enabled checkbox is introduced. The list also exposes Size so hardware-watchpoint width is not lost after creation. Disassembler navigation remains limited to `Execute` requests because a data-watchpoint address identifies memory being observed rather than code to decode.

### Event context

A data-watchpoint stop has two important addresses that must not be conflated:

- `DebuggerEvent.InstructionPointer` is the instruction that performed the watched access;
- `DebuggerEvent.TriggeredBreakpoint.Request.Address` is the watched data address.

Plugin API `2.15.0` adds the optional `TriggeredBreakpoint` property and a compatible constructor overload. Existing event producers and compatible 2.x plugins can continue using the original constructor, which leaves `TriggeredBreakpoint` null.

### Mock backend

Mock `1.0.0.rev14` advertises `Watchpoints` and accepts Hardware requests with Read, Write, or ReadWrite access. Sizes are limited to 1, 2, 4, or 8 bytes, addresses must be naturally aligned, and the complete watched range must remain inside the deterministic target map. Four hardware records are available independently from the existing 30 software-breakpoint records. Deterministic watchpoint hits keep the watched address in the triggered record while reporting a synthetic code address as the accessing instruction. Persistent and temporary lifetime behavior is shared with software breakpoints.

### PS5 / ps5debug-NG backend

PS5 `0.1.0.rev33` advertises `Watchpoints` and maps Hardware data requests to `CMD_DEBUG_SET_WATCHPOINT` (`0xBDBB0004`). ps5debug-NG exposes four hardware slots backed by DR0-DR3. The 24-byte request, DR7 length/access encoding, and slot numbers remain private to the PS5 plugin.

The amd64 DR7 data encodings used by the backend distinguish Write and Read/Write but provide no true Read-only mode. The plugin therefore rejects neutral Read-only requests rather than silently widening their semantics. Supported sizes are 1, 2, 4, and 8 bytes with natural alignment. The entire range must be within one current mapped, non-guarded memory region before any backend command is sent.

Watchpoint hit attribution does **not** re-enable paused `GETDBREGS`. The translator reads DR6 only from the debug-register block already embedded in the asynchronous 1184-byte interrupt packet. When B0-B3 are present, they map exact active hardware slots. Current upstream ps5debug-NG source clears packet-side DR6 before preserving the data-watchpoint trigger bits, so rev16 includes a conservative compatibility path: with exactly one enabled/backend-active hardware watchpoint, a signal-5 stop with zero DR6 can be attributed to that sole record; with more than one active watchpoint, the same zero-DR6 condition remains a generic signal stop and no temporary watchpoint is removed. This avoids manufacturing an exact hit that the backend did not identify. The upstream source issue is documented in `docs/bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md`.

## Rev17 Call Stack, Call Frames and Stepping

Rev17 activates the call-stack and step contracts that already exist in Plugin API `2.15.0`; it does not add a platform-specific host API or bump the public API version. The new functionality is scoped to a valid attached debugger session, the selected thread, and the current Paused stop context. Resuming, detaching, disconnecting, changing target generation, or otherwise losing that stop context clears the presented frames.

### Debugger workspace layout

The Debugger keeps Threads, Registers, and Events simultaneously visible. The upper-right workspace is shared by **Breakpoints / Watchpoints** and **Call Stack** without an additional permanently visible pane. Since accepted rev24, two compact application-styled selector buttons choose which workspace is visible; only one content panel is rendered at a time, and the selected button is identified by the theme accent outline. This replaces the WPF TabControl/TabItem presentation used by rev17-rev23. The main left/right workspace uses the existing `ProportionalGridSplitter` with a 36/64 starting ratio, practical minimum widths, and the established 20/80 relative movement limits. The right-side Breakpoints-or-Call-Stack / Events vertical split retains the verified 13/7 starting ratio.

Execution controls are arranged on two rows. Attach, Pause, Continue, and Detach remain the lifecycle controls; Step Into, Step Over, Step Out, and Run to... occupy the second row. Event count and Clear Events belong to the Events pane header rather than the execution-control surface.

The accepted rev24 upper-workspace selectors make the old duplicated inner Breakpoints / Watchpoints and Call Stack title/count rows unnecessary. Breakpoint Enable/Disable/Remove/Remove All/Disassembler actions remain left-aligned in the footer while Add/Refresh are right-aligned on the same row. The Call Stack footer keeps Disassembler/Memory Viewer on the left and moves Refresh to the right. The Call Stack grid deliberately shows only the high-value columns `#`, Instruction Address, Module, and Symbol. The selected-frame detail area exposes Stack Pointer, Frame Pointer, and Return Address without forcing a wide grid. A selected frame can open its instruction address in the existing Disassembler or Memory Viewer; no debugger-local code or memory viewer is introduced.

### Host stepping composition

- **Step Into** calls `IDebuggerStepService.StepAsync(DebuggerStepKind.Into, selectedThreadId, ...)`.
- **Step Over** remains a host-composed neutral operation. In ordinary paused contexts it reads a bounded disassembly context through the existing Core/Disassembler path. For Software/Execute breakpoints, rev26 captures the valid neutral instruction at the requested address **before** backend add can replace its first byte. When a `DebuggerEvent.TriggeredBreakpoint` Software/Execute event identifies the current logical stop and selected thread, Step Over prefers that captured original instruction; otherwise it falls back to live disassembly. If the resolved instruction is not a call, the host uses native Step Into. If it is a call, the host computes the original instruction's fall-through address and uses Run to Address. This prevents a backend-rearmed `INT3` from making a breakpointed `call` look like a non-call to the host.
- **Step Out** uses the selected call frame's neutral Return Address and the same Run-to path.
- **Run to Address** requires a current Paused debugger session with StepExecution and Breakpoints. An existing enabled Software/Execute breakpoint at the destination is reused. If none exists, the host creates a temporary one-byte Software/Execute breakpoint through the existing breakpoint service and continues. An existing disabled breakpoint is not silently enabled or rewritten. Rev27 records the exact breakpoint id plus whether the operation owns the temporary record. The next Paused event either matches that target or interrupts the composed operation. On interruption, only an operation-owned temporary breakpoint is retired; a reused persistent breakpoint is preserved.

The existing deferred stop-context refresh used for immediate breakpoint re-hits also covers step/run-to transitions so a fast `Paused` event is not discarded while Continue is completing. Rev25 re-reads the coordinator's final state after awaited Continue, native Step Into, and Run-to commands; if a deferred stop event arrived while the command was busy, that newer Paused/breakpoint/StepCompleted message remains authoritative instead of being overwritten by stale `Target running.` or `Run to Address is running toward ...` text. Rev27 extends the same deferred path to interruption cleanup: if Continue or an explicit Pause is still busy when a different stop wins, the operation-owned temporary breakpoint is retired before the final paused-context refresh. Managed breakpoint/watchpoint hit attribution remains higher priority than interpreting a signal-5 event as a native step completion.

Rev26's original-instruction cache is session-local and address-keyed. Capture failure does not block a valid breakpoint operation. The cached instruction is used only for a matching logical software execute stop; transitions away from Paused clear that stop association, while disposal, new attach setup, detach, and stale-target invalidation clear the complete cache. Temporary breakpoint hits may keep their cached instruction for the remainder of the debugger session because ps5debug-NG can already have rearmed the physical `INT3` by the time the one-shot neutral record is removed.

### Mock backend

Mock `1.0.0.rev15` provides deterministic acceptance fixtures for both contracts. While Paused it returns three neutral frames for the selected thread, anchored to the thread's current instruction/stack/frame values and deterministic caller/entry addresses. Call-stack requests while Running or for an unknown thread are rejected. Native Step Into transitions through Running and completes asynchronously with a deterministic `StepCompleted` event and updated instruction pointer. Mock does not claim native Over/Out support; those operations exercise the same host composition used for real plugins.

### PS5 / ps5debug-NG backend

PS5 `0.1.0.rev36` keeps all x86-64 and wire-format details inside the plugin. For ordinary paused contexts, Call Stack first reads the selected thread's general registers through the established debugger register transport to obtain RIP/RBP/RSP, then sends `CMD_PROC_READ_STACK` (`0xBDAA0023`) using the 24-byte `{ pid, rbp, rsp, depth }` request. For a matching managed software-breakpoint stop, rev35 instead seeds RIP/RBP/RSP from the interrupt packet's logical pre-instruction snapshot before issuing the same stack-read command. The server performs the RBP-chain walk and returns up to 64 variable-length frame records. The plugin strictly validates payload length, frame count, locals/code lengths, truncation, and trailing data; locals/code blocks are skipped because the neutral Call Stack UI does not expose them. The top neutral frame uses the selected logical thread RIP, later frame instruction addresses are resolved from the preceding return address, and module names are resolved through the existing current memory-map data.

PS5 native Step Into normally uses `CMD_DEBUG_STEP_THREAD` (`0xBDBB0013`) when a selected thread id is supplied; the process-wide `CMD_DEBUG_STEP` (`0xBDBB0012`) is also mapped internally. Pending software-breakpoint/watchpoint disables are flushed before an ordinary native step. Rev25/rev35 adds one special case: if the selected thread is currently represented by a managed software-breakpoint logical snapshot, ps5debug-NG has already transparently stepped that breakpointed instruction. The plugin reads the current live post-step RIP, consumes the logical snapshot, and produces the neutral Resumed -> StepCompleted transition without sending another native step command. Manual Pause, Continue, Detach, new interrupt receipt, and snapshot consumption clear the special state.

## Rev20 Workspace Chrome and Modeless Window Lifecycle

Rev19 established the host-owned workspace tab templates, independent modeless z-order, and tracked-tool shutdown coordinator. Its Windows automated suite passed **128/128**, but runtime inspection identified two remaining host-only failures. First, the selected/last Call Stack header still lost its final right vertical border despite the one-unit rev19 inset. Second, closing MainWindow with Debugger open could call `Close()` again before WPF had unwound the original `OnClosing` stack, raising `InvalidOperationException`.

Rev20 introduced a dedicated `TabRightEdge`, but the rev21 Windows runtime check showed that the right-aligned overlay was still absent on the selected/last header. Rev22 moved the edge into its own one-unit Grid column; Windows runtime then showed that the added column/edge path broke the header presentation itself, hiding both labels and filling the selected header with the accent color. Rev23 returned to a single themed `TabBorder`/header path and passed **130/130**, but runtime still showed the right edge clipped. Rev24 therefore removes the active Debugger `TabControl`/`TabItem` presentation instead of layering another geometry workaround. `WorkspaceTabControlStyle` and `WorkspaceTabItemStyle` are removed from active shared resources; the upper workspace now reuses the ordinary application button template through compact selector styles in `ButtonStyles.xaml`. Debugger data/commands and backend behavior remain unchanged.

Debugger, Disassembler, and Memory Viewer remain modeless tool workspaces rather than modal dialogs. MainWindow still uses `ToolWindowManager` to establish CenterOwner placement during initial `Show()`, then clears WPF ownership so normal desktop z-order applies in both directions. `Topmost` is not used. Modal dialogs retain their existing owner relationship.

MainWindow still owns explicit modeless-tool shutdown coordination because those windows are no longer WPF-owned after launch. Rev20 makes `OnClosing` synchronous: the first close request is cancelled, MainWindow is disabled, and `CompleteToolWindowShutdownAsync()` awaits the existing `ToolWindowManager.CloseAllAsync()` path. The helper sets the completed guard and posts the final `Close()` with `Dispatcher.BeginInvoke`. This ensures the final close request is processed only after WPF has returned from the original `OnClosing` callback, while retaining Debugger asynchronous detach/session cleanup and the Disassembler/Memory Viewer disposal paths.

## Current Rev31 Candidate Non-Goals

The current rev31 verification candidate does **not** implement:

- PS5 register editing or any SET-register transport;
- debugger Universal Export;
- Find What Writes / Reads / Accesses;
- Break and Trace;
- hardware execute-breakpoint support beyond the existing software execute path.

## Development Order After Rev31

Rev29 completed logical Disassembler/Markers acceptance with **138/138** plus focused live-PS5 verification. Rev30 added request pre-validation, record classification, and Scan Results/Saved Addresses debugger shortcuts but was superseded before its formal gate when live application-exit testing exposed stale hardware-watchpoint state. Rev31 is the focused teardown-safety correction and must pass a clean Windows **142/142** gate, direct-disposal hardware-watchpoint cleanup coverage, explicit Detach cleanup, application-exit cleanup with the watched access retriggered afterward, multi-slot/staged cleanup sanity, rev30 workflow regression, and rev29 logical-disassembly regression. If no further correction is required, one debugger milestone remains:

1. **rev32 — Integration, Export and Finalization:** debugger Universal Export, final cross-feature integration checks, stale-session/cleanup stress coverage, complete Mock regression, final live-PS5 regression, documentation cleanup, and completion of the `0.1.7` Debugger block.

`0.1.8.rev1 — Find What Writes / Reads / Accesses` follows the verified debugger block. `Break and Trace` follows after the code-finder/watchpoint infrastructure because it additionally depends on robust stepping and event sequencing.

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
10. Candidate revisions may advertise a newly implemented capability so its required gates can be exercised, but an accepted/verified build must not retain `Debugger`, `Breakpoints`, `Watchpoints`, `RegisterAccess`, `ThreadEnumeration`, `ThreadControl`, `CallStack`, or `StepExecution` unless the corresponding runtime service exists and has passed the required verification.

## Logical Disassembly and Debugger Markers (`0.1.7.rev29`)

Rev29 keeps the debugger session and backend contracts unchanged while making the existing Disassembler aware of debugger-owned runtime instrumentation through Core-owned presentation state. Each `DebuggerSessionCoordinator` owns one `DebuggerDisassemblyOverlayState`, so original Software/Execute instruction bytes, breakpoint markers, watchpoint-hit markers, and staged breakpoint-retirement state share the same plugin/process/connection-generation lifetime as the debugger session. Detach and reattach reset that state.

The Debugger already captured the original instruction before installing a Software/Execute breakpoint for breakpoint-aware Step Over. Rev29 reuses that capture instead of adding a second memory-read path. Enabled Software/Execute breakpoints publish `Breakpoint`; disabled records publish `Breakpoint (disabled)`. If a paused remove/disable is staged because the backend cannot safely retire the trap immediately, the marker can disappear while the original-byte mask remains until Continue completes the backend retirement. This prevents a still-present `INT3` from becoming visible as code during the staging window.

A paused Hardware Watchpoint event records `Watchpoint hit` at the event's semantic instruction pointer. This is marker-only state: watchpoints do not supply replacement code bytes. Running or another paused stop clears the stale watchpoint-hit marker.

The Disassembler consumes this information through the normal host/Core read path. Neither the WPF workspace nor Core depends on the PS5 debugger implementation, and no presentation operation writes target memory. Future assembler/NOP/patch functionality must remain a separate intentional-code-change layer so debugger instrumentation can never redefine the original instruction shown to or edited by the user.


## Rev30 Address Actions and Record Classification

Host `0.1.7.rev30` / Plugin API `2.16.0` adds optional `IDebuggerBreakpointValidationService`. The service is attached-session scoped and non-mutating. It validates the same complete `DebuggerBreakpointRequest` used by `IDebuggerBreakpointService`, so platform-specific mapped-region, protection, access, width, alignment, slot, duplicate, and staged-cleanup rules stay inside the plugin. Final Add remains authoritative even after a successful preflight.

The Breakpoints / Watchpoints table now separates **Type** from **Mechanism**. Type describes the semantic record: Execute records are `Breakpoint`; data-access records are `Watchpoint`. Mechanism reports the neutral `DebuggerBreakpointKind`: `Software` or `Hardware`. This permits future Hardware/Execute breakpoints without mislabeling them as watchpoints.

Scan Results and Saved Addresses expose Add Breakpoint/Add Watchpoint as convenience actions only when the modeless Debugger is already open and attached to the same plugin, process id/name, and connection generation. The host does not implicitly open or attach a Debugger from a row context menu. Add Breakpoint constructs a persistent one-byte Software/Execute request. Add Watchpoint constructs a persistent Hardware/Write request using the row value width. If no matching attached Debugger exists, the relevant capability is unavailable, the optional validator is absent, or validation rejects the request, the menu item is disabled.


## Rev31 Safe PS5 Watchpoint Detach Cleanup

PS5 plugin `0.1.0.rev38` no longer relies solely on ps5debug-NG's generic detach teardown to remove hardware watchpoints. Before explicit detach or disposal, the session snapshots every client-owned hardware slot that is either still backend-active or staged for cleanup after a temporary hit and sends the existing hardware-watchpoint command with `enabled = false` for each slot. The same shared helper updates local backend-enabled/pending-cleanup state after a successful disable.

This mirrors the software-breakpoint safety boundary established in PS5 rev36: debugger instrumentation owned by the client is removed by the client before backend detach is requested. The event channel remains alive during both cleanup passes, while `_detaching` prevents teardown interrupts from being surfaced as ordinary debugger stops. No public debugger model, capability, Plugin API contract, watchpoint legality rule, or wire opcode changes.

The change addresses a live shutdown failure where an enabled hardware watchpoint could survive application exit and later terminate the target when the watched access occurred. Current ps5debug-NG source conditionally enters its per-LWP DBREG clear path only after a preliminary `PT_GETDBREGS` probe whose return value is not checked. The external behavior and source path are documented in `docs/bug-reports/ps5debug-ng-detach-can-leave-hardware-watchpoints-active.md`. Rev31 keeps the backend bug external and adds deterministic client-side cleanup instead of guessing post-detach target state.
