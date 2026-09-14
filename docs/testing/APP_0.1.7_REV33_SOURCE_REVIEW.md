# Application 0.1.7.rev33 Source Review — Debugger Finalization, Snapshots and Comparer

## Baseline and scope

Rev33 is built from the supplied `0.1.7.rev32` package. Rev32 had not yet been runtime-verified, so rev33 deliberately preserves its watchpoint trigger/current-IP implementation and includes it in the ordered rev33 acceptance procedure. Rev31 remains the latest fully verified PS5 teardown baseline.

Before the rev33 changes, the project documentation and source tree were reviewed to reuse existing services rather than create parallel implementations. The implementation reuses:

- `DebuggerViewModel` and its attached-session/connection-generation safety model;
- existing `DebuggerBreakpointRequest`, plugin validation, `BreakpointDialog`, and `AddBreakpointAsync` flow;
- Core logical `DisassemblyReader`/snapshot/overlay behavior;
- existing Memory Viewer bounded read path for standard stack memory;
- Universal Export writers/dialog/progress/cancellation/publication services;
- `ToolWindowManager` modeless lifecycle conventions.

## Source changes reviewed

### Disassembler debugger action

The new context action is a host entry point only. It does not own breakpoint/watchpoint logic. The menu is enabled only for exactly one selected instruction and a matching attached debugger; request legality is still checked by the existing neutral validation path and final Add remains authoritative.

### Snapshot Core model

New Core models separate capture metadata, section status, event context, registers, call frames, logical instructions, breakpoint/watchpoint state, and bounded memory blocks. Breakpoint Type, Mechanism, and Lifetime are independent fields. Published collections and instruction marker collections are copied to read-only instances.

### Capture service/integration

Live capture requires Paused state, the current target/session generation, and a current stop context. The stop sequence is frozen before bounded reads and revalidated before publication. Capture uses the already resolver-updated rev32 event context, so a watchpoint snapshot does not collapse trigger instruction and real stop RIP.

No implicit Pause is introduced. Cancellation or stop/session change returns no published partial snapshot.

### Snapshot JSON

Schema identity/version are explicit. The serializer uses string enum names and hexadecimal 64-bit values, validates source widths/raw data/address ranges, tolerates unknown additive version-1 properties, freezes deserialized collections, and publishes exports only after a temporary file is complete.

### Universal Export

The debugger uses an in-memory structured adapter only for already materialized list data. Threads, Registers, Breakpoints/Watchpoints, Call Stack, and Events share the established writer/dialog/progress pipeline. Full snapshots remain outside this flat abstraction.

### Comparer

The comparer consumes only `DebuggerSnapshot` values. Code location comparison prefers Module + Offset; registers compare raw bytes; trigger/current IP remain separate; logical disassembly is compared from captured original bytes; group first-divergence reporting requires within-group stability. Candidate summary rows are promoted only from stable cross-group differences.

### Window lifecycle

Call Stack Comparer follows the existing modeless tool-window manager. Its live Debugger subscription is released when the comparer closes. Captured/imported snapshots have no API for execution control or target writes.

## Static review performed in the packaging environment

The packaging environment does not contain the Windows/.NET WPF toolchain, so no clean Windows compile or executable automated run is claimed here.

Static checks performed before packaging include:

- parsing all XAML/project XML files;
- parsing all repository JSON files;
- checking modified files for merge-conflict markers;
- checking application metadata/version strings;
- reviewing new/modified C# files for required namespaces and neutral/platform ownership boundaries;
- reviewing the final changed-file set against the rev32 baseline;
- confirming the automated test registry contains **152** checks;
- confirming no platform plugin source file is changed by rev33;
- confirming the final zip contains the complete application tree rather than only modified files.

## Required external gate

The authoritative compile/test/runtime gate is `docs/testing/APP_0.1.7_REV33_VERIFICATION.md`. Rev33 must not be marked verified until its 152-check Windows suite and ordered Mock/live-PS5 gates pass.
