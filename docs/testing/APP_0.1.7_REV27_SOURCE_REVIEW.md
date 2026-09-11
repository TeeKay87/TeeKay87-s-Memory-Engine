# Application 0.1.7.rev27 Source Review — Interrupted Operation and Safe Detach Breakpoint Cleanup

## Scope Reviewed

The supplied `0.1.7.rev26` package was used as the sole application baseline. Before production edits, the repository documentation and source tree were traversed again, including root README/CHANGELOG, all Markdown under `docs/`, application/Core/Plugin SDK/plugin C# sources, XAML/resources, project/build files, themes, tests, and scripts. The focused review followed the composed Step Over/Step Out/Run-to path through `DebuggerViewModel`, neutral debugger event/breakpoint contracts, coordinator lifecycle, PS5 breakpoint state handling, the dedicated PS5 debugger command/event transports, and the protocol test server.

The review preserved the rev26 breakpoint-aware Step Over correction. No replacement stepping design or public Plugin SDK change is introduced here. Current ps5debug-NG `debugger/source/debug.c` at commit `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86` was rechecked for the software-breakpoint disable path and `debug_full_teardown()` before the PS5 teardown correction was finalized.

## Runtime Evidence Carried Into Rev27

Rev26 passed its clean Windows automated gate with **133/133** checks. Live PS5 verification then produced these results:

- breakpoint-aware Step Over from the persistent `0xE9F74E` call stop correctly finished Paused at fall-through `0xE9F753` instead of entering callee `0x1A3BC30`;
- the temporary Step Over breakpoint was cleaned from the visible manager while the original persistent `0xE9F74E` breakpoint remained;
- a clean natural-pause Step Out reached its selected frame return address `0x800005AEB`, and no temporary breakpoint remained visible afterward;
- a later Run to Address toward `0x81C15634` was interrupted first by a PS5 signal-10 stop at `0x81C1563B`;
- after that unrelated stop, the operation-owned temporary Software/Execute breakpoint at `0x81C15634` remained Enabled/Temporary in the manager; and
- detaching from that paused state with the orphan temporary breakpoint still active was followed by the target game terminating.

The signal-10 stop and target termination are runtime observations only. This revision does not claim that either behavior is caused by ps5debug-NG. The client-side defects that can be established from the supplied code are narrower: the host did not retire an operation-owned temporary breakpoint when a different stop interrupted the composed operation, and the PS5 client cleared its local breakpoint tables on detach without first explicitly restoring every still-backend-active or staged software-breakpoint slot.

## Host Production Correction

### Composed-operation ownership

`DebuggerViewModel` now tracks the active composed execution operation together with the exact breakpoint id it is waiting for and whether that breakpoint was created temporarily by the operation or merely reused from an existing persistent breakpoint.

A Paused debugger event completes the active composed operation only when the neutral event identifies the same target breakpoint. Any other Paused event ends that operation as interrupted. If the operation owns a temporary breakpoint, cleanup is deferred until the current execution command has left its busy section, then routed through the existing neutral `IDebuggerBreakpointService`.

This keeps the correction platform-neutral. The host does not inspect PS5 signals, slots, packet layouts, or transport state. A manual Pause, signal/exception stop, watchpoint, different breakpoint, or any other neutral Paused event can therefore interrupt Run to Address, Step Over, or Step Out without leaving the host-owned temporary record behind.

### Cleanup timing and status

The existing deferred stop-context mechanism is extended rather than duplicated. If an interruption arrives while Continue/Pause is still completing, the stop event and pending temporary-breakpoint cleanup are retained until `IsBusy` is cleared. Cleanup happens before the final paused-context refresh. The resulting status reports that the composed operation was interrupted rather than leaving the older `...is running toward...` text visible.

If the backend has already consumed the temporary breakpoint before the host inspects it, cleanup treats the missing record as already retired and proceeds with the normal stop-context refresh. Existing persistent breakpoints reused as an operation target are never removed by interruption cleanup.

Attach, explicit detach completion, stale-target invalidation, and ViewModel disposal clear the new transient operation-tracking state together with the existing logical-breakpoint/disassembly state.

## PS5 Production Correction

PS5 plugin `0.1.0.rev36` keeps Plugin API `2.15.0` and the existing ps5debug-NG wire protocol.

Before sending `CMD_DEBUG_DETACH`, the plugin now builds a unique slot/address set from:

- software breakpoints still marked backend-enabled; and
- paused removals/temporary-hit cleanups already staged in `_pendingBreakpointDisables`.

Each such slot is explicitly disabled/restored through the existing `CMD_DEBUG_SET_BREAKPOINT` path before backend detach. The debugger event channel stays connected during that restore phase because the already documented ps5debug-NG breakpoint-disable behavior can resume a paused target as a side effect; complete packets are drained but ignored while `_detaching` is active, and the channel is closed only after the detach attempt. Explicit detach then runs regardless of whether one of the pre-restore requests failed, so the backend still receives its normal teardown opportunity. If detach succeeds but a pre-restore failed, the caller receives a cleanup error after the session has been placed in Detached state rather than silently treating the restore failure as success.

The same best-effort software-breakpoint restoration is used by session disposal when an explicit detach did not already complete. Successful restoration marks the client-side slot inactive and clears any matching pending-disable entry before final state teardown.

This is defensive client cleanup. It does not replace ps5debug-NG's own detach teardown and does not assert that the external backend caused the observed game termination. During the teardown review, current ps5debug-NG source also exposed a separate source-level cleanup defect: `debug_full_teardown()` stops its software-breakpoint restore loop at the first zero-address slot even though indexed breakpoint slots can be sparse after a disable. That issue is documented separately in `docs/bug-reports/ps5debug-ng-debugger-detach-stops-breakpoint-restore-at-first-empty-slot.md`; rev36's explicit client-side restore pass also avoids depending on that contiguous-slot assumption for breakpoints it owns.

## Preserved Boundaries

The correction does not redesign Core debugger contracts, Plugin SDK `2.15.0`, Mock `1.0.0.rev15`, breakpoint/watchpoint models, rev25 logical software-breakpoint register/frame snapshots, rev26 original-instruction capture, native Step Into, Call Stack transport, Disassembler, Memory Viewer, scanner/export behavior, status-bar layout, or modeless window ownership.

Step Over remains host-composed from neutral disassembly/original-instruction state. Step Out still uses the selected frame return address. User Run to Address keeps its existing process-wide breakpoint semantics. The change only adds explicit operation ownership/interrupt cleanup around that shared path.

## Verification Changes

Two new top-level checks raise the registry from **133** to **135**:

- **Debugger interrupted composed-operation cleanup source contract** verifies active-operation ownership, exact target-breakpoint matching, temporary-only interruption cleanup, deferred cleanup before paused-context refresh, and lifecycle clearing.
- **PS5 debugger safe detach restores software breakpoints** exercises a paused session containing both a still-active software breakpoint and a staged paused removal, then verifies that both backend slots receive disable/restore commands before the test server accepts detach.

The PS5 plugin metadata expectation is updated to `0.1.0.rev36`. Existing tests remain registered.

## Version and Compatibility

| Component | rev27 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev27` | Corrective debugger cleanup revision |
| Feature title | `Interrupted Operation and Safe Detach Breakpoint Cleanup` | New application title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev36` | Explicit software-breakpoint restore before detach/disposal |
| Automated registry | `135` | Two new cleanup/safe-detach checks |

## Development Order

Rev25's accepted MainWindow/Mock/logical-stop gates remain carried forward. Rev26's corrected Step Over behavior is also accepted runtime evidence, but rev27 touches the common composed-operation cleanup path, so a short Step Over/Step Out cleanup regression is required after the new **135/135** Windows gate. Verification then resumes at the failed Run-to interruption case and specifically tests manual-Pause interruption, temporary-breakpoint retirement, safe detach with staged cleanup, reconnect, and a normal successful Run to Address.

If rev27 passes without another code correction, Debugger **Integration, Export and Finalization** becomes `0.1.7.rev28`.
