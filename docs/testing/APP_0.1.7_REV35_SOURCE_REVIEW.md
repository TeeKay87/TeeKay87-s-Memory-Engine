# Application 0.1.7.rev35 Source Review — Disassembler Debugger Actions and Watchpoint Hit-Stop Highlighting

## Baseline

Rev35 is built directly from the supplied `0.1.7.rev34` package after the complete Windows automated suite passed **152/152** and the carried rev32 watchpoint semantics were then verified in focused Mock and live-PS5 runtime testing. The live PS5 case resolved the actual memory-accessing instruction at `0x8033D981` while preserving the backend stop/current IP at `0x8033D984`; the subsequent Software/Execute breakpoint regression at `0x8033D981` also passed.

Rev31 remains the verified permanent PS5 teardown-safety baseline and must not regress.

## Requested refinement

Two Disassembler usability issues were identified before continuing the remaining rev33 snapshot/comparer acceptance gates:

1. The rev33 combined **Add Breakpoint / Watchpoint...** command required the user to choose details manually even though the selected disassembled instruction contains enough architecture-specific information to determine whether a data watchpoint is meaningful and, when paused at that instruction, which effective address/width/access mode should be used.
2. After rev32 correctly separated a resolved watchpoint trigger from the real post-access stop/current IP, the Disassembler marker moved to the right instruction but the primary row highlight still followed only the requested/current origin. The user requested the cause and current execution position to be visually distinct: green for the resolved hit/trigger and yellow for the real stop/current IP.

## Architecture review

The existing breakpoint/watchpoint manager and request-validation path remain authoritative. Rev35 does not add a second manager or platform rules to WPF/Core.

A new optional Plugin API `2.18.0` service, `IDisassemblyWatchpointResolver`, accepts one neutral `DisassembledInstruction` plus the current neutral register snapshot and returns only a neutral `DisassemblyWatchpointTarget` (`Address`, `Size`, `Access`) when the platform can resolve the memory access safely. PS5 owns the x86-64/Iced operand interpretation. Mock deliberately leaves the optional service absent because its disassembler uses a synthetic custom instruction set.

The host enables automatic Disassembler **Add Watchpoint** only when all of these are true:

- exactly one valid row is selected;
- a Debugger is attached to the same plugin/process/connection generation;
- the debugger is Paused and has current register data;
- the selected instruction is the current stop instruction or the resolved trigger instruction for that same stop;
- the plugin resolver returns an unambiguous address/width/access tuple;
- the existing debugger breakpoint/watchpoint validator accepts the resulting request.

This prevents a register snapshot from one stop being used to calculate an effective address for an unrelated historical/arbitrary instruction.

## PS5 resolver safety

The PS5 resolver reuses Iced decoding and instruction-info metadata. It accepts one explicit memory operand and only widths supported by the existing PS5 hardware-watchpoint implementation: 1, 2, 4, and 8 bytes. Effective-address resolution supports ordinary 64-bit GPR base/index/scale/displacement forms, RIP-relative memory, and FS/GS only when the corresponding neutral base register is available.

The resolver refuses to guess for:

- no real memory operand;
- `lea`/address calculation without a memory access;
- multiple explicit memory operands;
- unsupported width/form;
- missing required register data;
- an address base/index register modified by the same instruction;
- stale/non-paused register context.

Definite write accesses map to neutral `Write`. Reads and read/write accesses map to `ReadWrite` because amd64 DR7 cannot represent a data-breakpoint mode that traps reads but excludes writes.

## Disassembler marker/highlight review

`DebuggerDisassemblyOverlayState` continues to retain the rev32 trigger address separately from `InstructionPointer`. For a resolved watchpoint whose trigger and stop differ, the overlay now emits both:

- `Watchpoint hit` at the resolved trigger address;
- `Stop / Current IP` at the real stop address.

The row view model derives presentation flags from those neutral marker strings. The Disassembler uses the existing `SuccessMutedBrush` for the hit row and a new derived `WarningMutedBrush` for the stop/current row. `ThemeManager` derives `WarningMutedBrush` from the existing theme `WarningText` color, so no new required theme JSON key is introduced. An unresolved watchpoint remains a yellow stop row with `Watchpoint stop (trigger unresolved)` and no fabricated green trigger row.

Registers, stepping, call stack, execution control, snapshots, and event history still use the real stop/current IP. The green hit row changes presentation only.

## Versioning

- Application: `0.1.7.rev35`
- Feature: `Disassembler Debugger Actions and Watchpoint Hit-Stop Highlighting`
- Plugin API: `2.18.0`
- Mock semantic version: `1.0.1` (legacy revision metadata remains `17`)
- PS5 semantic version: `0.1.1` (legacy revision metadata remains `39`)
- Automated registry: `152` checks, strengthened rather than expanded

Both plugin semantic versions advance by `0.0.1` because their API target/metadata changed; PS5 additionally implements the new optional service.

## Verification requirement

Because rev35 changes the public Plugin API and PS5 plugin code, the Windows build and all **152 checks** must be rerun even though rev34 already passed them. The focused watchpoint runtime gate must then be repeated for the new dual-highlight presentation and new Disassembler actions before continuing with the still-pending snapshot/export/import/comparer gates.
