# Application 0.1.7.rev11 Source Review — Breakpoint Manager and Software Breakpoints

## Status

**Superseded after the first Windows build gate.** The static/source package review completed, but the first authoritative Windows build later reported two `CS8600` nullable diagnostics in `Ps5DebuggerSession.cs`. Because warnings are treated as errors, rev11 did not reach the packaged **114-check** suite or focused runtime/hardware acceptance. The correction is carried by `0.1.7.rev12 - Breakpoint Manager Compile Fix`.

This revision starts from the fully verified `0.1.7.rev10 - PS5 Extended Register Transport Guard` baseline: **110/110 PASS** plus focused live-PS5 transport/runtime acceptance. Rev11 is limited to the first breakpoint layer and the metadata/documentation/test changes required by that layer.


## Post-Package Windows Build Result

The first rev11 Windows build exposed a gap that the static source review could not validate without the .NET compiler:

- `CS8600` in `RemoveBreakpointAsync` at the direct `TryGetValue(..., out entry)` assignment;
- `CS8600` in `SetBreakpointEnabledAsync` at the corresponding direct `TryGetValue(..., out entry)` assignment;
- Visual Studio also displayed `XLS0414` for `System.Object` in `MainWindow.xaml` after the referenced PS5 project failed to build. No independent MainWindow source change was identified.

The two dictionary paths used a non-nullable `SoftwareBreakpointEntry` local as the `out` target. Under nullable reference analysis, `Dictionary<TKey,TValue>.TryGetValue` may assign the default/null value when it returns false. Rev12 changes those paths to receive into an explicitly nullable local, guard the missing/null result, and only then assign to the non-nullable local used by the existing logic. No breakpoint runtime semantics change.

## Revision Boundary

Production changes are limited to:

- Plugin API `2.14.0` optional breakpoint state service;
- Debugger Breakpoint Manager presentation/dialog/actions;
- Mock `1.0.0.rev12` deterministic software-breakpoint implementation;
- PS5 `0.1.0.rev30` software-breakpoint protocol/session implementation;
- application/plugin metadata.

Core debugger coordination and all unrelated scanner, Saved Addresses, Memory Viewer, Disassembler, export, theme, settings, and target-memory behavior remain unchanged.

## External Backend Source Contract Recheck

The ps5debug-NG source was rechecked at commit `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86` before locking the PS5 behavior.

- `CMD_DEBUG_SET_BREAKPOINT` is `0xBDBB0003`.
- The request is a packed 16-byte `{ uint32 index, uint32 enabled, uint64 address }` structure.
- The backend accepts indices `0-29`.
- Enable stores the breakpoint metadata/original byte and writes `0xCC` to target memory.
- The async event path detects the managed INT3, restores the original byte, rewinds RIP by one to the breakpoint address, steps the original instruction, and reinstalls the breakpoint before emitting the corrected context.
- The disable branch restores the original byte and then calls `ptrace_raw(PT_CONTINUE, pid, (void *)1, 0)`. That means an immediate disable/remove while the target is already Paused can resume it unexpectedly.

The final point is an external backend behavior rather than a host/UI concern. It is recorded separately under `docs/bug-reports/ps5debug-ng-disabling-software-breakpoint-resumes-paused-target.md`.

## Public API Review

The existing debugger foundation already contained neutral breakpoint request/record models and `IDebuggerBreakpointService`. Rev11 adds only `IDebuggerBreakpointStateService` for Enable/Disable. The service accepts the opaque neutral breakpoint id and desired state; it exposes no backend slot, command id, INT3 byte, or platform-specific state.

Plugin API advances from `2.13.0` to `2.14.0`. The existing major/minor compatibility rule is unchanged.

## Host UI Review

The Debugger ViewModel and window use `TargetCapabilities.Breakpoints` plus the attached-session breakpoint services. The manager is hidden when capability is absent and its commands also require the current plugin/process/connection generation.

The current Add dialog produces one-byte Software/Execute requests and records a persistent/temporary lifetime. Refresh and state changes preserve selection by neutral id where possible. Remove All uses the existing destructive confirmation service. Disassembler navigation reuses the normal modeless Disassembler workspace.

No PS5-specific slot count or command semantics are present in WPF/Core.

## Mock Review

Mock implements add/list/remove/enable/disable with deterministic ids, duplicate-address rejection, and a finite 30-record fixture limit. Continue schedules one deterministic hit against the first enabled breakpoint. A hit sets Main RIP to the requested address, transitions to Paused, and emits the neutral Breakpoint event. Temporary records are removed after that first hit.

The Mock behavior is intentionally synthetic; it verifies generic lifecycle semantics rather than modeling an x86 INT3 implementation.

## PS5 Review

### Slot and request ownership

The PS5 plugin owns the 30-slot backend pool. Neutral ids include slot/sequence information only as opaque strings; WPF/Core never interpret them. Slots awaiting deferred cleanup are excluded from allocation. Duplicate managed addresses are rejected.

### Running-state operations

Add/Enable/Disable/Remove map to `0xBDBB0003` when the backend transition is safe to perform immediately. The command client validates slot range and emits the 16-byte request body before consuming the normal status response.

### Paused-state disable/remove

An enabled breakpoint cannot be disabled immediately while Paused because the upstream handler can Continue the target. Rev11 changes only local manager state and records the slot/address in `_pendingBreakpointDisables`. Remove drops the neutral record but keeps the slot unavailable until cleanup. Re-enable of a still-present record cancels its pending disable.

`ContinueAsync` flushes staged backend disables before sending the explicit Continue action. This makes the state-changing backend side effect coincide with a user-requested resume boundary instead of an inspection-time breakpoint action.

### Hit mapping and temporary cleanup

The existing event loop reads the corrected interrupt RIP. A SIGTRAP is classified as a managed breakpoint only when that address matches an enabled/backend-enabled managed record. A temporary record is then removed locally and its slot is staged for disable on the next Continue. Persistent records remain active.

## Automated Coverage Review

The test registry contains **114** unique checks: all 110 rev10 registrations plus four rev11 registrations/expansions covering:

- Mock software-breakpoint lifecycle and deterministic hits;
- Debugger Breakpoint Manager source contract;
- breakpoint dialog source contract;
- PS5 software-breakpoint command framing, state transitions, paused safety staging, hit mapping, temporary cleanup, and final removal.

The PS5 deterministic protocol fixture records `(slot, enabled, address)` requests and rejects invalid request sizes/ranges.

## Required Static Package Checks

Before packaging:

- reread every Markdown document after final edits;
- reread all production/test source and project text;
- parse XAML/project XML and JSON;
- resolve relative Markdown links;
- confirm no `bin/`/`obj/` output;
- inspect changed C# files for required `using` directives;
- confirm Core is byte-identical to rev10;
- confirm only intended Plugin SDK/App/Mock/PS5/test files changed from rev10;
- confirm application `0.1.7.rev11`, Plugin API `2.14.0`, Mock `1.0.0.rev12`, PS5 `0.1.0.rev30`;
- confirm exactly 114 unique registered checks;
- create the final ZIP, run `ZipFile.testzip()`, and hash-match every archived file against the locked work tree.

## Final Static Review Result

The locked rev11 work tree passed the required non-.NET source/package-preparation review before ZIP creation:

- **146** Markdown files reread successfully;
- **252** source/project/config text files reread successfully;
- **23** XAML/project/XML files parsed with **0** errors;
- **3** JSON files parsed with **0** errors;
- **51** relative Markdown links resolved with **0** broken links;
- **0** `bin`/`obj` directories are present;
- application/plugin/API metadata consistently identifies application `0.1.7.rev11`, Plugin API `2.14.0`, Mock `1.0.0.rev12`, and PS5 `0.1.0.rev30`;
- the packaged test registry contains exactly **114** unique registrations;
- the rev10-to-rev11 source comparison contains **7 added**, **26 changed**, and **0 removed** files;
- shared `TeeKay87.MemoryEngine.Core` is byte-identical to rev10;
- **17** added/changed C# files were reviewed for required namespace imports and balanced source structure; no missing-`using` issue was found in the changed code.

This static result does not replace the Windows build/test gate or focused runtime/hardware acceptance.

## Environment Limitation

No .NET/WPF pass is claimed by this source review. The authoritative Windows environment must run the packaged verification suite and runtime/UI/hardware gates.
