# Application 0.1.7.rev3 Source Review — PS5 Debug Transport and Target UI Cleanup

> **Status:** Historical source review. Rev3 passed this static review but was superseded by `0.1.7.rev4` before Windows/UI/live-PS5 acceptance. Rev4 preserved the reviewed PS5 debugger production code unchanged; current rev5 retains it as well while changing only responsive host row-1 sizing.

## Review Context

This source review was performed against the exact user-supplied `0.1.7.rev2 - Debugger Workspace and Mock Backend` package used as the `0.1.7.rev3` development baseline.

That rev2 package had already completed its authoritative verification before rev3 development began:

- Windows automated verification: **89/89 PASS**;
- complete focused Mock Debugger runtime/UI acceptance: **PASS**;
- regression smoke testing of Scan, Memory Viewer, Disassembler, Saved Addresses, read/write, themes, and connect/disconnect: **PASS**.

The purpose of this review is to validate the rev3 source boundary and protocol assumptions before packaging. It does **not** replace the clean Windows Release build, 94-check verification executable, requested UI runtime review, or live-PS5 debugger acceptance documented in `APP_0.1.7_REV3_VERIFICATION.md`.

## Result

**Pre-package static source review: passed.**

## Version and API Metadata

Confirmed:

- application metadata is `0.1.7.rev3`;
- feature title is `PS5 Debug Transport and Target UI Cleanup`;
- public Plugin API remains `2.12.0`;
- Mock plugin remains `1.0.0.rev8`, targeting API `2.12.0`;
- PS5 plugin is `0.1.0.rev25`, targeting API `2.12.0`.

No stale `0.1.7.rev2` application metadata or prior rev2 feature title remains in production source. Application presentation continues to consume centralized `AppInfo` metadata.

## Exact Non-Documentation Source Boundary

Relative to the supplied rev2 baseline, rev3 adds exactly three production source files, all inside the PS5 plugin:

- `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/Ps5DebuggerCommandClient.cs`;
- `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/Ps5DebuggerProvider.cs`;
- `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/Ps5DebuggerSession.cs`.

Eight existing non-documentation files change:

- `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/Ps5DebugProtocol.cs`;
- `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/Ps5PluginInfo.cs`;
- `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/Ps5TargetPlugin.cs`;
- `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/Ps5TargetSession.cs`;
- `src/TeeKay87.MemoryEngine.App/Application/AppInfo.cs`;
- `src/TeeKay87.MemoryEngine.App/MainWindow.xaml`;
- `tests/TeeKay87.MemoryEngine.Tests/Program.cs`;
- `tests/TeeKay87.MemoryEngine.Tests/Ps5ProtocolTestServer.cs`.

No source file is removed.

The application changes are restricted to centralized version metadata and the requested main-window presentation layout. The production debugger implementation itself is entirely PS5-plugin-owned.

## Verified Shared Foundation Preservation

Recursive byte comparison confirms the following rev2 production trees are unchanged:

- `src/TeeKay87.MemoryEngine.Core/Debugging/`;
- the complete `src/TeeKay87.MemoryEngine.PluginSdk/` tree;
- the complete `src/Plugins/TeeKay87.MemoryEngine.Platform.Mock/` tree.

Rev3 therefore consumes the already verified Plugin API `2.12.0` debugger contracts, Core `DebuggerSessionCoordinator`, debugger identity/event rules, and Mock reference backend without silently changing their semantics.

## PS5 Plugin Delta

The PS5 plugin adds only the three debugger-owned source files listed above and changes only four existing PS5 files:

- `Ps5DebugProtocol.cs` adds debugger command/status/port/interrupt-layout constants;
- `Ps5PluginInfo.cs` advances PS5 independently from rev24/API 2.11 to rev25/API 2.12;
- `Ps5TargetPlugin.cs` advertises only the coarse `Debugger` capability and updates plugin description metadata;
- `Ps5TargetSession.cs` creates/exposes/disposes the PS5 debugger provider through the existing neutral service boundary.

All other PS5 production files are byte-identical to rev2. In particular, the established primary ps5debug-NG client, concurrent Frozen-write transport, native scanner/mapping implementation, memory reader/writer, process control, and Iced disassembler are not rewritten for debugger support.

## Capability Boundary

Static review confirms PS5 rev25 advertises `TargetCapabilities.Debugger` but does **not** advertise:

- `ThreadEnumeration`;
- `ThreadControl`;
- `RegisterAccess`;
- `Breakpoints`;
- `Watchpoints`;
- `CallStack`;
- `StepExecution`.

The attached PS5 debugger session returns no optional advanced debugger service in rev3. The host therefore cannot expose functionality whose backend has not yet been implemented.

## Dedicated Debugger Transport

`Ps5DebuggerCommandClient` owns a separate TCP connection to the configured ps5debug-NG command port. Its commands are serialized by a private semaphore and never share the normal target/memory/scan `Ps5DebugClient` stream.

Static review confirms the rev3 command mapping:

```text
CMD_DEBUG_ATTACH   0xBDBB0001  body: int32 PID
CMD_DEBUG_DETACH   0xBDBB0002  body: none
CMD_DEBUG_CONTINUE 0xBDBB0010  body: 4 bytes, action in byte 0

stop-go action 0 = resume
stop-go action 1 = pause/stop
```

Action `2`/kill is represented only as a protocol limit check and is not exposed through the neutral debugger UI.

Once a framed debugger command is sent, status receipt completes with a non-cancellable in-flight token so caller cancellation cannot abandon a status word and corrupt that dedicated stream. Cancellation is still honored before a command begins.

The existing non-debugger `IProcessControl` remains unchanged and continues to use `CMD_DEBUG_PROCESS_STOP` (`0xBDBB0500`) for **Pause target while scanning**. Rev3 does not merge those two ownership paths.

## Attach and TCP 755 Ownership

Static review confirms attach follows the required ps5debug-NG sequence:

1. bind/listen on local IPv4 TCP `755`;
2. begin accepting the outbound event connection;
3. open the dedicated debugger command connection;
4. send `CMD_DEBUG_ATTACH` with the selected process PID;
5. require the outbound TCP `755` event connection within the bounded five-second attach window;
6. construct the attached neutral session only after both paths exist.

If attach has succeeded but the event connection fails/times out, cleanup attempts backend detach before closing the dedicated command client. Listener/socket ownership stays inside the PS5 plugin.

`Ps5DebuggerProvider` enforces one active debugger session per connected PS5 target session. Session disposal releases provider ownership so a later debugger window can attach again.

## Async Interrupt Packet Review

Current ps5debug-NG upstream protocol/source was rechecked during rev3 finalization for the callback and packet assumptions used by the plugin.

Confirmed current layout:

```text
packet size: 1184 bytes / 0x4A0
0x000  uint32  LWP/thread id
0x004  uint32  wait status
0x008  40-byte thread-name storage
0x030  176-byte amd64 general-register block
0x0E0  832-byte floating/SIMD state
0x420  128-byte debug-register block
```

The FreeBSD amd64 instruction pointer is at offset `0x88` inside the general-register block, producing absolute packet offset `0x0B8`. Rev3 parses that 64-bit value only inside the PS5 plugin and projects it as neutral `DebuggerEvent.InstructionPointer`.

The signal is derived from `(waitStatus >> 8) & 0xFF`. Nonzero signals map to `DebuggerStopReason.Signal`; zero maps to `Backend`.

## Async Execution-State Semantics

The final protocol review specifically checked whether receipt of an async interrupt should leave the neutral session `Paused` or `Running`.

Current ps5debug-NG `dispatch_debug_events` sends the 1184-byte packet and then calls its application-layer resume helper. It does **not** issue `PT_CONTINUE` for the traced stop after sending the packet. The ptrace stop remains pending until debugger stop-go action `0`/Continue is processed.

Rev3 therefore correctly maps a received async debugger interrupt to neutral `Paused` state. Mapping the event to Running would make the shared coordinator enable the wrong command set and discard the actual stop context.

The server filters the SIGSTOP path used by explicit debugger Pause from normal async event delivery. Rev3 therefore emits one local neutral `Paused/PauseRequested` event after a successful action-1 command and one `Resumed` event after successful action-0 Continue, avoiding dependence on a duplicate callback that the backend intentionally suppresses.

## Event-Channel Failure and Cleanup

Unexpected TCP 755 EOF/socket failure while still attached moves the plugin session to `Unknown` and emits a neutral backend event. Pause/Continue can no longer assume a valid run state, while Detach remains available for cleanup.

Intentional Detach/disposal marks teardown before shutting down the event socket, suppressing a false backend-disconnect diagnostic.

Target-session disposal disposes the debugger provider before the existing concurrent writer and primary PS5 client. An active debugger session performs best-effort backend detach, stops the event loop, closes both debugger sockets, disposes the dedicated command client, and releases provider ownership.

## Main Workspace Presentation Boundary

Static review confirms the requested host UI changes are presentation-only:

- Platform and Target Process selectors share the first target row;
- Refresh and Set Active Target are on that same row;
- the process selector's column collapses naturally for a future plugin without process enumeration;
- Disassembler and Debugger remain together on the second row and are left-aligned;
- `SelectedPlugin.ActiveProcessText` is no longer rendered;
- `SelectedPlugin.ProcessStatusText` is no longer rendered;
- variable `ConnectionStatusText` operation prose is not used as the permanent bottom indicator;
- the far-left bottom status item is driven by `SelectedPlugin.IsConnected` and displays only **Connected** / **Not connected**;
- connected state uses the existing success border/muted success background, while disconnected state uses the existing danger border;
- the existing Active Target commands/state are unchanged, so selecting a process still does not activate it until **Set Active Target** is executed.

No platform-specific branch was introduced into `MainWindow.xaml`.

## Verification Registry Preservation

The supplied rev2 baseline contains **89** registered verification checks.

The rev3 registry contains **94** checks:

- all 89 baseline check names remain present;
- no baseline check is removed;
- no duplicate check name is introduced;
- exactly five new checks are registered.

The new checks are:

1. `PS5 debugger provider lifecycle and exclusivity`;
2. `PS5 debugger attach pause continue and detach protocol`;
3. `PS5 debugger asynchronous interrupt mapping`;
4. `PS5 debugger dedicated transport isolation`;
5. `Main workspace target controls and connection status layout`.

The protocol fixture verifies separate debugger command connections, exact attach PID, stop-go actions `1`/`0`, backend detach, event-channel callback, 1184-byte interrupt parsing, neutral Paused state, thread/signal/instruction-pointer mapping, and continued use of the ordinary process command stream while the debugger remains attached.

## Project and Documentation Structure

Static review confirms:

- all **21** WPF XAML/project/props/targets XML files are structurally well-formed;
- no `bin` or `obj` build-output directory is included;
- all relative Markdown links checked in the package resolve to existing files;
- current application/plugin/API metadata is internally consistent;
- no old rev2 application metadata remains in production source;
- the rev2 Core debugger foundation, complete Plugin SDK, and complete Mock plugin remain byte-identical;
- no advanced PS5 debugger capability is advertised prematurely.

Historical revision/test documents remain historical. Current README, CHANGELOG, development plan, debugger architecture, main-workspace documentation, PS5 plugin documentation, protocol mapping, scanner/disassembly boundary documents, and rev3 verification plan are synchronized to the rev3 candidate.

## Remaining Verification

The following gates intentionally remain external to this source review:

1. clean Windows Release build;
2. warnings-as-errors compile acceptance;
3. verification executable ending with `All 94 checks passed.`;
4. requested main-workspace UI review in Light, Dimmed, and Dark;
5. live PS5 debugger Attach/Pause/Continue/Detach and cleanup/reconnect acceptance;
6. brief PS5 regression smoke test of established scanner/memory/Saved Addresses/Memory Viewer/Disassembler behavior.

## Status

**Static source review: passed.**

**Windows build/94-check verification: not completed as rev3; superseded by rev4.**

**Main-window runtime/UI verification: rev3 layout rejected during first review and superseded by rev4.**

**Live PS5 debugger hardware verification: carried forward to rev4.**
