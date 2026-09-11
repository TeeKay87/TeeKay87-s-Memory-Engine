# Application 0.1.7.rev31 Source Review — Safe PS5 Watchpoint Detach Cleanup

## Scope Reviewed

The supplied `0.1.7.rev30` package is the sole code baseline for this revision. Before editing, the repository documentation and complete source/configuration tree were traversed again, including root documentation, every Markdown file under `docs/`, C# projects, WPF XAML/resources, Plugin SDK/Core contracts, built-in plugin projects, test sources, JSON resources, and scripts.

The focused review followed the complete debugger shutdown chain from MainWindow/tool cleanup through `DebuggerSessionCoordinator.DetachCoreAsync()` into PS5 `DetachAsync()` / `DisposeAsync()`, then through active and staged breakpoint/watchpoint bookkeeping and the ps5debug-NG command client.

Rev30 was not treated as verified. Live PS5 testing before its formal Windows/runtime gate exposed a teardown-safety issue that supersedes rev30 as a candidate: leaving a hardware watchpoint enabled and closing the application could leave the watchpoint armed after client teardown, with the game later terminating when the watched access occurred.

## Root Cause Boundary

The host already performs coordinated asynchronous tool cleanup, and Core already calls the attached debugger session's `DetachAsync()` followed by `DisposeAsync()`. The PS5 session therefore receives a normal cleanup opportunity during application exit.

The missing client-side protection was inside the PS5 plugin. Rev36 had already added an explicit pre-detach restore pass for software breakpoints, but hardware watchpoints still relied on ps5debug-NG's own `debug_full_teardown()` to rediscover and clear debug-register state.

Current ps5debug-NG source makes that backend cleanup conditional on a preliminary `PT_GETDBREGS` result. The local DBREG buffer is zeroed first, the call result is not checked, and the low DR7 enable bits decide whether the per-LWP zeroing path runs. A failed or non-representative probe can therefore be interpreted as no active hardware debug registers and bypass the per-thread clear before `PT_DETACH`.

The external issue is documented separately in `docs/bug-reports/ps5debug-ng-detach-can-leave-hardware-watchpoints-active.md`.

## Production Changes

### Explicit hardware-watchpoint cleanup before detach

PS5 plugin `0.1.0.rev38` now treats client-owned hardware watchpoints the same way rev36 already treats software breakpoints: the plugin removes the instrumentation it owns before requesting backend detach.

`RestoreHardwareWatchpointsBeforeDetachAsync()` builds one slot-indexed snapshot from:

- active watchpoint records whose backend slot is still enabled; and
- staged temporary-watchpoint removals waiting in `_pendingWatchpointDisables`.

Every collected slot is disabled through the existing `CMD_DEBUG_SET_WATCHPOINT` transport. The pass continues through all slots even if one disable reports an error, preserving the first failure for the final detach result.

### Shared hardware-disable bookkeeping

`DisableHardwareWatchpointBackendSlotAsync()` now owns the common backend-disable plus local bookkeeping sequence used by both normal staged cleanup and pre-detach cleanup. Successful disable removes the pending slot, marks a matching active record backend-disabled, and updates its neutral enabled state when necessary.

No new protocol command, Plugin SDK contract, host platform branch, or duplicate watchpoint transport was added.

### Disposal path

PS5 `DisposeAsync()` now attempts software-breakpoint restoration and hardware-watchpoint clearing independently before backend detach. Either cleanup failure remains best-effort during disposal, but one failed category no longer prevents the other from being attempted.

Explicit `DetachAsync()` reports instrumentation-cleanup and detach failures without hiding either category. The existing event channel remains alive until cleanup/detach completes, and the existing `_detaching` gate continues to prevent teardown packets from becoming logical debugger events.

## Preserved Behavior

The change is deliberately limited to PS5 debugger teardown. Rev30's request validation, Breakpoint/Watchpoint Type-versus-Mechanism presentation, Scan Results/Saved Addresses shortcuts, and Plugin API `2.16.0` remain unchanged. Mock remains `1.0.0.rev16`.

Rev29 logical Disassembler bytes/Markers, rev27 software-breakpoint teardown behavior, watchpoint validation and slot allocation, hit attribution, stepping/run-to composition, register safeguards, scanner/export behavior, Memory Viewer, themes, and modeless-window cleanup remain unchanged.

## Automated Coverage

The registry target advances from **141** to **142** checks with one focused protocol regression:

- **PS5 debugger safe detach clears hardware watchpoints** creates one persistent active hardware watchpoint and one temporary watchpoint that has triggered and is staged for cleanup, disposes the debugger session directly, and verifies that both backend slots receive explicit disable actions before detach completes.

The existing software-breakpoint safe-detach regression remains active, so software and hardware teardown protections are tested independently.

## Version / Compatibility

| Component | rev31 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev31` | Current candidate |
| Feature | `Safe PS5 Watchpoint Detach Cleanup` | Focused teardown correction |
| Plugin API | `2.16.0` | Unchanged |
| Mock plugin | `1.0.0.rev16` | Unchanged |
| PS5 plugin | `0.1.0.rev38` | Explicit hardware-watchpoint teardown cleanup |
| Automated registry | `142` | One new cleanup regression |

## Development Order

Rev31 must pass a clean Windows build, **142/142**, the focused PS5 watchpoint teardown regression, explicit-Detach cleanup, application-exit cleanup, and a short rev30/rev29 regression. If accepted without another correction, Debugger **Integration, Export and Finalization** moves to `0.1.7.rev32`.


## Static Package Review

The rev31 source tree passed the pre-package static review available in the build environment:

- application metadata resolves to `0.1.7.rev31 - Safe PS5 Watchpoint Detach Cleanup`;
- PS5 metadata resolves to `0.1.0.rev38` targeting Plugin API `2.16.0`;
- the automated registry contains exactly **142** checks and includes the focused hardware-watchpoint teardown regression;
- both PS5 `DetachAsync()` and `DisposeAsync()` invoke the hardware-watchpoint restore/disable pass before backend detach;
- modified C# delimiter/source-contract checks passed;
- all project/XAML/XML files parsed successfully;
- all JSON files parsed successfully;
- relative links in changed documentation resolved;
- no `bin`, `obj`, or `.vs` build artifacts are included.

The environment does not provide the Windows/.NET toolchain, so this static review does not replace Gate A. The candidate still requires the clean Windows build and exact `All 142 checks passed.` result before runtime acceptance.
