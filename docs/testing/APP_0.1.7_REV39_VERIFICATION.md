# Application 0.1.7.rev39 Verification — Call Stack Comparer Session Persistence and Explicit Capture

Rev39 is a focused correction discovered during the rev38 snapshot/comparer runtime gate. Rev38 passed the clean Windows automated suite at **152/152** and the live PS5 Disassembler action checks, including arbitrary-row Add Watchpoint, Add Breakpoint, multi-selection gating, non-memory gating, and green Watchpoint Hit / yellow Stop-Current-IP presentation. Verification then exposed comparer-window/session lifecycle problems that must be corrected before snapshot JSON and grouped comparison acceptance continues.

## Package identity

| Item | Expected |
| --- | --- |
| Application | `0.1.7.rev39` |
| Feature | `Call Stack Comparer Session Persistence and Explicit Capture` |
| Plugin API | `2.18.0` |
| Mock | `1.0.1.rev17` |
| PS5 | `0.1.2.rev39` |
| Automated registry | `152` checks |
| Snapshot schema | `teekay87-memory-engine-debugger-snapshot`, version `1` |

## Gate 1 — Clean Windows build and automated suite

1. Extract rev39 into a clean folder.
2. Build the complete solution on Windows.
3. Treat any compile warning/error as a blocker.
4. Run the complete verification project.
5. Expected result: **152/152 PASS**.

Do not continue after a failed check.

## Gate 2 — Comparer explicit-capture behavior

Use one attached live debugger session and reach a valid Paused stop.

1. Open **Call Stack > Compare...**.
2. Confirm the comparer opens with the existing snapshot collection only.
3. Confirm opening the comparer does **not** add a new snapshot automatically.
4. Confirm **Capture Current** is enabled while a valid paused stop context exists.
5. Press **Capture Current** once and confirm exactly one new live snapshot is added.
6. Press **Compare...** again while the same comparer window is open and confirm the existing window is activated without adding another snapshot.

## Gate 3 — Same-session close/reopen persistence

1. Keep at least one captured/imported snapshot in the comparer.
2. Close only the Call Stack Comparer window.
3. Keep the same Debugger window/session alive.
4. Open **Compare...** again.
5. Confirm the previous snapshot collection, editable metadata, and any current comparison result are still present.
6. Confirm reopening does not auto-capture another snapshot.

The comparer presentation window may be recreated, but its same-session workspace state must persist.

## Gate 4 — Capture Current Running -> Paused refresh

1. Start from a valid Paused stop and confirm **Capture Current** is enabled.
2. Press **Continue** and confirm **Capture Current** becomes disabled while Running.
3. Trigger a new breakpoint/watchpoint stop without closing the comparer.
4. Confirm **Capture Current** becomes enabled again when the new valid Paused stop context arrives.
5. Capture the new stop explicitly and confirm exactly one additional snapshot is added.
6. Repeat once more if practical to catch stale one-shot notification behavior.

## Gate 5 — Debugger close / offline ownership

1. Keep the comparer open with captured/imported snapshots.
2. Close or detach the associated Debugger.
3. Confirm existing comparer data remains viewable/comparable/exportable.
4. Confirm **Capture Current** is disabled because the live debugger authority is gone.
5. Open/reconnect a new Debugger session and confirm old snapshots are not silently rebound as live state.

## Gate 6 — Snapshot JSON export/import round trip

1. Export one captured snapshot.
2. Confirm schema `teekay87-memory-engine-debugger-snapshot`, version `1`.
3. Confirm 64-bit addresses remain exact hexadecimal strings and raw bytes survive exactly.
4. Import the same file and confirm imported/offline source presentation.
5. Compare original vs imported and expect equivalent captured data.
6. Confirm import never reconnects, attaches, recreates breakpoint/watchpoint state, or writes target memory.
7. Confirm invalid schema/version is rejected clearly.

## Gate 7 — Universal Export for debugger tables

Verify independent export scopes for Threads, Registers, Breakpoints/Watchpoints, Call Stack, and Events. Exercise JSON plus at least one tabular format and confirm event export keeps trigger and stop/current addresses separate.

## Gate 8 — Call Stack Comparer pairwise behavior

Capture/import two snapshots and verify pairwise selection, Summary, Call Stack, Registers, Context, Instructions, Breakpoint Context, Memory, raw register equality/difference, module-relative identity, ordered frame alignment, logical/original instruction bytes, and Export Results.

## Gate 9 — Group comparison

Create two user-defined groups with multiple samples and verify stable common frames, stable first divergence, stable cross-group register differences, variable-value handling, evidence-based Potential discriminator wording, and imported/live snapshot equivalence.

## Gate 10 — Modeless lifecycle and application shutdown

Exercise activation between MainWindow, Debugger, Disassembler, and Call Stack Comparer. Close/reopen the comparer during the same session, keep it open while closing Debugger, and close the application with multiple modeless tools open. No close re-entry, stale-live binding, or shutdown cleanup regression is acceptable.

## Gate 11 — Permanent rev31 teardown safety regression

Mandatory live PS5 regression:

- application exit with an active hardware watchpoint, then trigger the watched access: game continues;
- explicit detach with an active hardware watchpoint, then trigger the watched access: game continues;
- active/staged Software/Execute breakpoint cleanup restores original bytes and leaves no stale trap.

## Gate 12 — Full debugger regression

Run the established Mock and live-PS5 cycle covering attach/detach/reattach, Pause/Continue, Threads, Registers, breakpoint/watchpoint manager actions, multiple-watchpoint conservative attribution, Call Stack, Current Instruction -> Disassembler, logical breakpoint-byte overlay, Step Into, breakpoint-aware Step Over, Step Out, Run to Address, temporary-breakpoint cleanup, disconnect/reconnect stale-session behavior, Light/Dimmed/Dark presentation, and smoke regression for Scan Results, Saved Addresses, Memory Viewer, and Disassembler.

## Acceptance

Rev39 can continue through the remaining debugger-finalization gates only when the clean automated suite is green and Gates 2–5 prove that comparer evidence has explicit user-controlled capture semantics, survives presentation-window close/reopen for the same Debugger session, refreshes capture availability on every valid Paused stop, and loses live authority safely when that Debugger ends.
