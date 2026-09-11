# Application 0.1.7.rev30 Source Review — Debugger Address Actions and Breakpoint Classification

## Scope Reviewed

The supplied verified `0.1.7.rev29` package was used as the sole baseline. Before editing, the repository documentation and complete source tree were traversed, including root documentation, every Markdown file under `docs/`, C# projects, WPF XAML/resources, plugin projects, Plugin SDK/Core contracts, tests, JSON resources, and scripts. The focused source review followed the existing MainWindow Scan Results/Saved Addresses row-context workflows, modeless `ToolWindowManager`, `DebuggerViewModel` target/session identity checks, neutral breakpoint models/services, Mock and PS5 request validation rules, and the Breakpoints / Watchpoints table presentation.

Rev29 runtime acceptance was also recorded before rev30 work began: **138/138** automated checks passed, Software breakpoint logical disassembly preserved the original `01 70 40 / add [rax+40h],esi`, Hardware watchpoint stops produced `Watchpoint hit` without modifying code presentation, and both marker types were shown together while paused.

## Production Changes

### Breakpoint/Watchpoint classification

`DebuggerBreakpointViewModel` now separates the semantic record classification from the implementation mechanism. `Type` is `Breakpoint` for Execute-trigger records and `Watchpoint` for data-access records. `Mechanism` exposes the existing neutral Software/Hardware `DebuggerBreakpointKind`. The Debugger table therefore becomes **Address | State | Type | Mechanism | Access | Size | Lifetime**. Existing request/state/backend behavior is unchanged.

### Validated main-workspace address actions

Scan Results and Saved Addresses now expose **Add Breakpoint** and **Add Watchpoint** in their row context menus. These are convenience actions, not implicit debugger lifecycle commands. They are enabled only when an already-open Debugger is attached to the same plugin/process/connection generation and the complete request can be validated without mutating target state.

- Add Breakpoint creates a persistent Software/Execute request of size 1.
- Add Watchpoint creates a persistent Hardware/Write request using the selected row's current/saved value width.
- The two actions are gated independently.
- A mismatched/stale/detached/busy Debugger, missing capability/service, invalid mapping/protection, unsupported access/width, bad alignment, exhausted slot set, duplicate request, or staged cleanup keeps the relevant menu action disabled.
- Clicking an enabled action still routes through the existing `DebuggerViewModel.AddBreakpointAsync` path; plugin runtime validation remains final and authoritative.

### Plugin API 2.16 request validation

Plugin API `2.16.0` adds optional `IDebuggerBreakpointValidationService` and `DebuggerBreakpointValidationResult`. The service is deliberately non-mutating and accepts the same neutral `DebuggerBreakpointRequest` that the existing add path uses. This lets generic host UI preflight a request without knowing PS5, x86 debug-register, or other platform-specific legality rules.

Mock `1.0.0.rev16` implements the service by reusing its executable-range, watchpoint alignment/size, duplicate, and slot-limit rules. PS5 `0.1.0.rev37` implements it by reusing the established mapped/executable software-breakpoint validation and mapped-range/guard/access/size/alignment/slot/duplicate/pending-cleanup watchpoint rules. No PS5 wire command changes. Compatible older 2.x plugins remain loadable; if they omit the optional validator, only the new shortcuts remain disabled.

## External Backend Finding

The rev29 combined-marker exploration exposed a ps5debug-NG behavior outside this codebase: a Software breakpoint on the exact instruction that would also trigger a hardware watchpoint causes the backend's internal restored-instruction single-step to consume the overlapping watchpoint stop. The report is stored at `docs/bug-reports/ps5debug-ng-software-breakpoint-step-consumes-overlapping-watchpoint-hit.md`. Rev30 does not invent or synthesize a missing watchpoint event.

## Automated Contracts

The registry target increases from **138** to **141**. New checks cover:

- neutral `IDebuggerBreakpointValidationService` behavior and built-in request validation;
- Breakpoint/Watchpoint **Type** versus Software/Hardware **Mechanism** source presentation; and
- Scan Results/Saved Addresses Add Breakpoint/Add Watchpoint menu integration, same-target attached Debugger lookup, backend validation gating, and row-value watchpoint width propagation.

## Version / Compatibility

| Component | rev30 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev30` | Current candidate |
| Feature | `Debugger Address Actions and Breakpoint Classification` | New host feature title |
| Plugin API | `2.16.0` | Optional request validation |
| Mock plugin | `1.0.0.rev16` | Validator implementation |
| PS5 plugin | `0.1.0.rev37` | Validator implementation; wire unchanged |
| Automated registry | `141` | Three new checks |

## Development Order

At source-review time, rev30 was expected to pass a clean Windows build, **141/141**, focused table-classification checks, context-menu enable/disable and creation checks for both Scan Results and Saved Addresses, and a short rev29 logical-disassembly regression before Debugger **Integration, Export and Finalization** moved forward. That plan was superseded by the later live shutdown finding recorded below; rev31 is the teardown-safety correction and final integration now moves to rev32.


## Later Status

Rev30 was superseded before formal verification. During live PS5 shutdown testing, an enabled hardware watchpoint could remain armed after the client closed and terminate the game when the watched access occurred later. `0.1.7.rev31` adds explicit client-owned hardware-watchpoint cleanup before detach/disposal and moves Debugger **Integration, Export and Finalization** to rev32.
