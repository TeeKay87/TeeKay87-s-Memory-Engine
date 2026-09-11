# Application 0.1.7.rev26 Source Review — Breakpoint-Aware Step Over Fix

## Scope Reviewed

The supplied `0.1.7.rev25` package was used as the sole application baseline. Before changing production code, the complete repository documentation and source tree were traversed: root README/CHANGELOG, every Markdown document under `docs/`, all C# and XAML sources, project/build files, themes, tests, scripts, Core, Plugin SDK, Mock, PS5, and the existing debugger source contracts.

The focused review followed the failing live Step Over path through `DebuggerViewModel.StepOverAsync`, the host-composed Run-to implementation, neutral breakpoint/event models, breakpoint add/enable flows, Disassembler integration, Mock fixtures, and the PS5 rev35 logical software-breakpoint snapshot handling. No duplicate host facility already preserved the complete decoded instruction that existed before a software breakpoint replaced its first byte.

## Runtime Evidence Carried Into Rev26

Rev25 completed the following acceptance before the blocker:

- clean Windows build and **132/132 PASS**;
- MainWindow bottom-status centerline verification in Dimmed, Dark, and Light;
- focused Mock debugger regression including stepping, breakpoint, watchpoint, and session cleanup behavior;
- live PS5 logical software-breakpoint stop consistency at `0xE9F747` across event/IP/Registers/Call Stack;
- Step Into from that logical stop finishing at `0xE9F74C` without a second native backend step; and
- live PS5 logical stop consistency at the known `call` address `0xE9F74E` across event/IP/Registers/Call Stack.

The first Gate E Step Over test then failed. Starting from the persistent software-breakpoint logical stop at:

```text
0xE9F74E  call 0x1A3BC30
0xE9F753  jmp short 0xE9F731
```

pressing Step Over produced a Step Into completion at `0x1A3BC30` rather than stopping at the call fall-through `0xE9F753`.

A disassembly export taken while the persistent breakpoint was armed showed `0xE9F74E` as `CC / int3`. That observation is consistent with the already-reviewed ps5debug-NG sequence: after the backend restores and transparently single-steps the original instruction, it rearms `INT3` before delivering the logical breakpoint packet to the client.

## Root Cause

The PS5 rev35 correction made the breakpoint packet's register snapshot authoritative for the host-visible logical stop, but the generic host Step Over composition still classified the current instruction by reading target memory **after** the software breakpoint was installed.

`DebuggerViewModel.StepOverAsync()` therefore received a decoded `int3` at the logical breakpoint address instead of the original `call`. Its existing non-call branch correctly invoked native Step Into. PS5 rev35 then correctly consumed the backend's already-completed transparent step, which exposed the live post-call callee RIP. Each component behaved according to its local contract, but the host had lost the original instruction needed to choose the correct composed Step Over path.

The fix belongs in the host composition layer rather than the PS5 plugin because Step Over is already a platform-neutral host workflow: classify the neutral `DisassembledInstruction`; use native Step Into for non-calls; use a temporary Run-to breakpoint at the fall-through for calls. No PS5 wire structure or architecture-specific rule needs to enter Core or the Plugin SDK.

## Production Correction

### Original-instruction capture

`DebuggerViewModel` now maintains a session-local map from software execute breakpoint address to the original neutral `DisassembledInstruction`.

Before a Software/Execute breakpoint is installed, the host uses the existing target-safe Disassembler path to request a small context beginning exactly at the requested address. If the provider returns a valid instruction at that exact address, the decoded instruction is retained only after the breakpoint add succeeds. This means a later rearmed `INT3` cannot erase the flow-control classification or original instruction length needed by Step Over.

The captured instruction remains associated with that breakpoint address across ordinary enable/disable state changes within the same debugger session. Rev26 deliberately does not perform a new capture during re-enable: the existing PS5 paused-disable workaround can stage a disable without immediately restoring the physical instruction byte, so re-reading during that state could see the still-armed `INT3` and overwrite the known original instruction.

Instruction capture is deliberately optional. If the target cannot currently provide a disassembly snapshot, breakpoint creation still follows its existing behavior rather than being rejected merely because the additional metadata could not be recorded. After a fresh Software/Execute breakpoint add succeeds without a captured instruction, any older cached instruction at the same address is discarded so the new breakpoint cannot inherit stale decode metadata.

### Logical-stop matching

The host records the address/thread of the current logical software execute breakpoint stop only when the neutral debugger event is a `Breakpoint` event and `TriggeredBreakpoint` identifies a Software/Execute record. A non-breakpoint event or transition away from Paused clears that logical-stop association.

Step Over uses the cached original instruction only when all of the following agree:

- the current instruction pointer equals the logical software-breakpoint stop address;
- the selected thread matches the stop event thread when one was supplied; and
- an original instruction was captured for that address.

Every other Step Over context continues through the established live Disassembler lookup. The cache is therefore not a replacement for ordinary disassembly and cannot silently redirect an unrelated paused thread to stale breakpoint metadata.

### Temporary breakpoint compatibility

The cache is keyed by address and is not pruned merely because a temporary breakpoint record disappears from the visible manager after its hit. This matters because ps5debug-NG can still have the physical `INT3` rearmed while the client is presenting the logical stop. A subsequent Step Over at such a stop can therefore still resolve the original instruction correctly.

Session/target lifecycle cleanup clears both the cached instructions and current logical-stop association on debugger disposal, new attach setup, detach, and stale-target invalidation. Running/non-paused coordinator state clears the stop association while retaining the session's breakpoint instruction snapshots for later hits.

## Verification Changes

One new top-level source contract raises the registry from **132** to **133** checks:

- **Debugger breakpoint-aware Step Over source contract** verifies that original neutral instructions are captured before backend Software/Execute breakpoint installation, retained by address, tied to neutral `TriggeredBreakpoint` logical-stop context, preferred by Step Over before live disassembly, and cleared with debugger lifecycle state.

The existing Call Stack/stepping, Run to Address, breakpoint-manager, PS5 logical-stop, and status-alignment contracts remain in place.

## Preserved Boundaries

No production change is made to Core, the public Plugin SDK, Mock debugger behavior, PS5 debugger transport, PS5 logical register/frame snapshot handling, breakpoint/watchpoint wire protocol, native Step Into, Call Stack transport, MainWindow status bar, tool-window lifecycle, scanner, Memory Viewer, Disassembler provider, or export infrastructure.

The PS5 plugin therefore remains `0.1.0.rev35`, Mock remains `1.0.0.rev15`, and Plugin API remains `2.15.0`. Rev26 is a host corrective revision only.

## Version and Compatibility

| Component | rev26 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev26` | Corrective host stepping revision |
| Feature title | `Breakpoint-Aware Step Over Fix` | New application title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev35` | Unchanged |
| Automated registry | `133` | One new breakpoint-aware Step Over source contract |

## Development Order

Rev25's accepted Gates B-D are carried forward because rev26 does not alter their production paths. Rev26 must first pass a clean Windows WPF build and **133/133** automated checks. Runtime verification then resumes at the exact failed live-PS5 Step Over case on the persistent `0xE9F74E` call breakpoint. If that passes, the remaining Step Out, Run to Address, and cleanup/reconnect gates continue from the rev25 plan.

If no further code correction is required, Debugger **Integration, Export and Finalization** becomes `0.1.7.rev27`.
