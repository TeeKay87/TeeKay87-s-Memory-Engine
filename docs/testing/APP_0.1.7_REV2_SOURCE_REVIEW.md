# Application 0.1.7.rev2 Source Review — Debugger Workspace and Mock Backend

## Review Context

This source review was performed against the exact user-supplied `0.1.7.rev1 - Debugger Contracts and Core Foundation` package used as the `0.1.7.rev2` development baseline.

Rev1 had already passed the authoritative Windows verification suite with **84/84** checks before rev2 development began.

The purpose of this review is to validate the rev2 source boundary before packaging. It does **not** replace the clean Windows Release build, 89-check verification executable, or Mock runtime/UI acceptance documented in `APP_0.1.7_REV2_VERIFICATION.md`.

## Result

**Pre-package static source review: passed.**

## Version and API Metadata

Confirmed:

- application metadata is `0.1.7.rev2`;
- feature title is `Debugger Workspace and Mock Backend`;
- public Plugin API remains `2.12.0`;
- Mock plugin is `1.0.0.rev8`, targeting API `2.12.0`;
- PS5 plugin remains `0.1.0.rev24`, targeting API `2.11.0`.

No second hardcoded application version/revision source was introduced; application presentation continues to consume centralized `AppInfo` metadata.

## Exact Production Source Boundary

Relative to the supplied rev1 baseline, rev2 adds six production source files:

- `src/TeeKay87.MemoryEngine.App/DebuggerWindow.xaml`;
- `src/TeeKay87.MemoryEngine.App/DebuggerWindow.xaml.cs`;
- `src/TeeKay87.MemoryEngine.App/ViewModels/DebuggerViewModel.cs`;
- `src/TeeKay87.MemoryEngine.App/ViewModels/DebuggerEventViewModel.cs`;
- `src/Plugins/TeeKay87.MemoryEngine.Platform.Mock/MockDebuggerProvider.cs`;
- `src/Plugins/TeeKay87.MemoryEngine.Platform.Mock/MockDebuggerSession.cs`.

Nine existing non-documentation files change:

- centralized `AppInfo` metadata;
- MainWindow XAML/code-behind for the capability-driven Debugger entry;
- `PluginViewModel` for debugger coordinator ownership/current-target validation;
- Mock plugin metadata/capabilities/target-session service lifetime;
- verification `Program.cs` and test project fixture declarations.

No source file is removed.

## Verified Rev1 Foundation Preservation

Recursive byte comparison confirms the following rev1 production trees are unchanged:

- `src/TeeKay87.MemoryEngine.Core/Debugging/`;
- all Plugin SDK `Contracts/` files;
- all Plugin SDK `Models/` files.

Rev2 therefore consumes the verified `DebuggerSessionCoordinator`, debugger identity/event context, public debugger contracts/models, and API `2.12.0` surface without silently modifying their already verified semantics.

## PS5 Preservation

A recursive byte comparison confirms the complete production tree under:

```text
src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/
```

is byte-identical to the supplied rev1 baseline.

PS5 still advertises no `Debugger` capability. Rev2 therefore introduces no PS5 debugger command, event transport/channel, protocol parser, packet structure, or hardware-debugger behavior.

## Mock Backend Boundary

Mock advances independently to `1.0.0.rev8` / API `2.12.0` because it now consumes the rev1 debugger contracts.

Static review confirms:

- only the coarse `TargetCapabilities.Debugger` flag is added from the debugger capability family;
- `IDebuggerProvider` is exposed through the existing connected target-session service boundary;
- only the deterministic target process is accepted;
- only one active debugger attachment is allowed;
- Attach starts in Running state;
- Pause and Continue emit deterministic neutral events using thread id `1` and instruction pointer `0x10000400`;
- target-session disposal disposes an active Mock debugger session;
- no optional thread/register/breakpoint/call-stack/step service is exposed yet.

Existing Mock memory/scanner/disassembler fixture addresses and implementations are not replaced by debugger-specific duplicates.

## Host Workspace and Session-Lifetime Boundary

Static review confirms the modeless Debugger workspace:

- is capability-driven rather than platform-name-driven;
- captures the current target plus connection generation when opened;
- performs no implicit attach merely by opening;
- routes Attach/Pause/Continue/Detach through `DebuggerSessionCoordinator`;
- dispatches asynchronous state/events onto the WPF dispatcher;
- validates `DebuggerSessionIdentity` before presenting events;
- bounds displayed event history to 2,000 rows;
- contains no platform-specific debugger transport or architecture implementation.

`PluginViewModel` owns all coordinators created against its current target session. Cleanup occurs before target-session replacement/disconnect, before Active Target replacement, and during ViewModel disposal.

The final rev2 implementation deliberately separates **current target identity** from temporary **attach availability**. Plugin/process/connection generation determines whether a debugger session/event is current. `CanOpenDebugger` additionally gates creation of a new attachment while a conflicting foreground target operation is active. This prevents transient operations from falsely invalidating a legitimate attached debugger identity.

## Platform-Neutral Host Check

The new Debugger WPF/ViewModel source and the relevant MainWindow entry path were checked for standalone platform tokens associated with ps5debug-NG, Iced/x86/x64, architecture-specific instruction-pointer/stack/frame register names, and backend thread implementation concepts.

No standalone platform-specific token is present in the new host debugger workflow.

The automated source test uses token boundaries rather than raw substring matching so register names such as `RIP` do not falsely match unrelated identifiers/text such as `Description`.

## Verification Registry Preservation

The supplied rev1 baseline contains **84** registered verification checks.

The rev2 registry contains **89** checks:

- all 84 baseline check names remain present;
- no baseline check is removed;
- exactly five new checks are registered.

The new checks are:

1. `Mock debugger provider lifecycle`;
2. `Mock debugger deterministic pause and continue events`;
3. `Mock debugger attachment exclusivity and target cleanup`;
4. `Debugger workspace command and event source contract`;
5. `Debugger target lifetime and stale-session source contract`.

The source-contract conditions for the two WPF/host integration checks were also evaluated directly during this review and all expected bindings/lifetime/identity conditions are present.

## Project and Documentation Structure

Static review confirms:

- all WPF XAML, project, props, and targets XML files are well-formed;
- all static resource keys referenced by the new Debugger window resolve in the application's XAML resources;
- no `bin` or `obj` build-output directory is included;
- no obsolete `0.1.7.rev1`/Mock-rev7 application metadata remains in production code;
- current README/CHANGELOG/development-plan metadata reports rev2, Mock rev8, API 2.12.0, and the 89-check candidate boundary;
- relative Markdown documentation links resolve to existing files;
- historical revision verification records remain historical instead of being rewritten as current rev2 behavior.

## Remaining Verification

The following gates remain intentionally external to this source review:

1. clean Windows Release build;
2. warnings-as-errors compile acceptance;
3. verification executable ending with `All 89 checks passed.`;
4. deterministic Mock Debugger runtime/UI acceptance;
5. brief regression smoke test of existing Mock scanner/Memory Viewer/Disassembler behavior.

No new PS5 debugger hardware gate applies to rev2 because the PS5 production plugin is unchanged and still exposes no debugger service.

## Status

**Static source review: passed.**

**Windows build/89-check verification: pending.**

**Mock Debugger runtime/UI verification: pending.**
