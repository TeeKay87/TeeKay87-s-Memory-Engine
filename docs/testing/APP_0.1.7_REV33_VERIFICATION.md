# Application 0.1.7.rev33 Verification — Debugger Finalization, Snapshots and Comparer

## Purpose

Rev33 combines the unverified rev32 watchpoint trigger/current-IP change with the remaining debugger-finalization implementation. Verification must therefore be performed in dependency order. Do not skip directly to snapshots/comparison: if the underlying event semantics are wrong, the persisted evidence will also be wrong.

Rev31 is the latest fully verified live-PS5 teardown baseline. Rev33 must preserve that safety behavior.

## Package identity

| Item | Expected |
| --- | --- |
| Application | `0.1.7.rev33` |
| Feature | `Debugger Finalization, Snapshots and Comparer` |
| Plugin API | `2.17.0` |
| Mock | `1.0.0.rev17` |
| PS5 | `0.1.0.rev39` |
| Automated registry | `152` checks |
| Snapshot schema | `teekay87-memory-engine-debugger-snapshot`, version `1` |

## Gate 1 — Clean Windows build and automated suite

1. Extract the package into a clean folder.
2. Build the full solution on Windows using the normal project toolchain.
3. Treat warnings-as-errors/build failures as blockers.
4. Run the complete automated verification suite.
5. Expected result: **152/152 PASS**.

Do not continue runtime acceptance after a failed build or automated check.

## Gate 2 — Carried rev32 watchpoint semantics first

### Mock

1. Attach Debugger to the deterministic Mock target.
2. Trigger the deterministic hardware-watchpoint stop.
3. Confirm the real stop/current instruction remains the event/register execution context.
4. Confirm the trigger instruction is stored separately.
5. Confirm a resolved event uses `BackendExact` or `DisassemblyDerived` as appropriate and the Disassembler marker is on the trigger instruction.
6. Confirm an intentionally unresolved case does not claim that the current/stop IP caused the access and is presented as unresolved.
7. Confirm a Software/Execute breakpoint still marks its original instruction and no physical trap byte appears as logical game code.
8. Confirm Debugger Events shows separate stop IP, trigger instruction, watched address/access/size, and trigger-resolution information.

### Live PS5

Use the previously established known write/watchpoint scenario where the memory-accessing instruction and post-access stop RIP are adjacent but different.

Verify:

- `Watchpoint hit` appears on the actual memory-accessing instruction;
- stop/current RIP remains the following instruction reported by the backend;
- Registers still describe that real stop context;
- no `RIP - 1` or other fixed-byte assumption is visible;
- unresolved behavior remains honest if a safe boundary cannot be proved;
- Software/Execute breakpoint presentation and breakpoint-aware Step Over remain unchanged.

If this gate fails, stop. Snapshot/comparer results must not be accepted on top of incorrect event semantics.

## Gate 3 — Disassembler Add Breakpoint / Watchpoint

1. Open an attached Debugger and Disassembler for the same Active Target/session.
2. Select exactly one instruction in the Disassembler.
3. Right-click it.
4. Confirm **Add Breakpoint / Watchpoint...** is enabled when the existing neutral validation path allows an action.
5. Open it and confirm the existing breakpoint/watchpoint dialog is used with the selected instruction address.
6. Add a legal Software/Execute breakpoint and confirm it appears in the existing manager.
7. Remove/disable it using the existing manager and confirm original logical bytes remain visible.
8. Select two or more Disassembler rows and right-click a selected row.
9. Confirm **Add Breakpoint / Watchpoint...** is visibly disabled/greyed out.
10. Confirm multi-row Copy/Export selection remains intact.
11. Close/detach the Debugger while keeping Disassembler open. Confirm the action disables rather than implicitly opening/attaching a Debugger.
12. Repeat enough of the flow with a legal watchpoint request to confirm the same existing dialog/validation/add path is used; platform legality remains authoritative.

## Gate 4 — Standard Snapshot capture

### Paused-state gating

1. With Debugger Running, open Call Stack Comparer.
2. Confirm **Capture Current** is disabled.
3. Pause at a valid stop and confirm Capture Current becomes enabled.
4. Capture a snapshot.
5. Confirm the collection adds one immutable snapshot with a sensible default label/time/source/event/trigger/stop/process presentation.

### Captured data

Inspect/export the snapshot and confirm, where supported:

- source application/plugin/API/platform/process/architecture metadata;
- event sequence/timestamp/state/reason/thread;
- real stop/current IP;
- separate trigger instruction and resolution;
- watched address/access/size and triggered record context where relevant;
- Registers with exact raw bytes;
- Call Stack frames;
- logical/original disassembly around stop/trigger;
- Breakpoint/Watchpoint records with Type, Mechanism, Access, Size, Lifetime, Enabled, and triggered-item state;
- bounded Stack memory when safely readable;
- honest section status for unavailable/skipped/failed optional data.

### Session consistency

1. Capture at Stop A.
2. Resume/continue to Stop B.
3. Confirm Snapshot A does not change.
4. Confirm a new capture produces a new SnapshotId/context.
5. Exercise disconnect/reconnect or stale-target transition and confirm an old Debugger/comparer cannot capture against the new session by accident.
6. If practical, resume during a deliberately slowed/debug capture path and confirm no mixed-stop snapshot is published. Automated tests cover this invariant even if runtime timing is difficult to reproduce.

## Gate 5 — Snapshot JSON export/import

1. Select one captured snapshot and Export it.
2. Confirm JSON identifies schema `teekay87-memory-engine-debugger-snapshot` version `1`.
3. Confirm 64-bit addresses are `0x...` strings rather than precision-risk JSON numbers.
4. Confirm raw register/instruction/memory bytes survive exactly.
5. Import the same file into Call Stack Comparer.
6. Confirm imported presentation matches the original capture and is labeled as imported/offline in the comparer.
7. Compare original versus imported snapshot. Expected comparison: equivalent captured data.
8. Disconnect/detach the live target and confirm the imported snapshot remains inspectable/comparable.
9. Confirm import does not reconnect, attach, recreate breakpoints/watchpoints, or alter target memory.
10. Try an invalid schema identity and unsupported schema version; confirm a clear import failure.
11. Cancel/fail an export where practical and confirm no partial destination replaces a valid prior file. Automated tests additionally cover transactional publication.

## Gate 6 — Universal Export for debugger tables

From a populated Debugger, use **Export...** and verify each available scope independently:

1. Threads
2. Registers
3. Breakpoints / Watchpoints
4. Call Stack
5. Events

For each scope:

- exercise JSON plus at least one delimited/table format; full automated suite covers all writers;
- confirm column selection works through the existing export dialog;
- confirm no target state changes occur;
- confirm Registers include raw bytes;
- confirm Breakpoints/Watchpoints distinguish Type, Mechanism, Access, Size, Enabled, and Lifetime;
- confirm Events keep Instruction Pointer and Trigger Instruction separate and include watched-address and trigger-resolution context.

## Gate 7 — Call Stack Comparer pairwise behavior

Capture two known different stops.

Verify:

- comparer is modeless;
- MainWindow can activate above it and comparer can activate above MainWindow;
- exactly two selected snapshots are required by **Compare 2**;
- results include Summary, Call Stack, Registers, Context, Instructions, Breakpoint Context, and Memory where data exists;
- raw register bytes, not formatted text alone, drive register equality;
- trigger and stop/current instruction remain distinct;
- logical/original bytes are compared, not physical `INT3` instrumentation;
- module-relative locations are displayed/used where captured;
- an extra/missing wrapper frame is described with ordered alignment evidence rather than forcing index equality;
- **Export Results...** uses the shared Universal Export flow.

## Gate 8 — Group comparison

Create at least two user-defined groups. Preferred live scenario when convenient:

```text
Player
  Player 1
  Player 2
  Player 3

Enemy
  Enemy 1
  Enemy 2
  Enemy 3
```

The exact labels are user metadata; the engine must not hardcode Player/Enemy semantics.

Verify:

1. Assign snapshots to Group A and Group B by editing Group.
2. Run **Compare Groups**.
3. If both groups share stable leading frames, confirm they are reported as common.
4. If a first differing frame is stable inside each group, confirm it is reported as the first stable divergence.
5. If one group varies at the candidate frame, confirm no false stable first divergence is claimed.
6. For a deliberately stable register fixture/dataset, confirm `Stable in both groups, different between groups` behavior.
7. For a deliberately variable register, confirm it is not promoted as stable.
8. Confirm Summary uses wording such as **Potential discriminator** / stable difference and does not invent semantic labels such as Player Flag.
9. Confirm imported snapshots participate in the same pipeline as live-captured snapshots.

A real game is not required to contain an obvious player/enemy discriminator. Acceptance depends on truthful classification of the evidence that was captured.

## Gate 9 — Modeless lifecycle and offline ownership

1. Keep Call Stack Comparer open with at least two snapshots.
2. Close/detach the associated Debugger.
3. Confirm existing/imported snapshot comparison remains available offline.
4. Confirm live Capture Current no longer acts as if the closed debugger were valid.
5. Reconnect/open another target and confirm old snapshots are not silently rebound to the new live target.
6. Close/reopen comparer and verify normal activation/lifecycle.
7. Close the application with comparer/debugger/disassembler windows open; confirm orderly shutdown and no WPF close re-entry regression.

## Gate 10 — Permanent rev31 teardown safety regression

This is mandatory before rev33 acceptance.

### Hardware watchpoint application-exit case

1. On live PS5, attach Debugger.
2. Add/enable a known legal hardware watchpoint.
3. Leave it active.
4. Close the application without manually removing the watchpoint.
5. Trigger the watched access afterward.
6. The game/target must continue normally; the stale watchpoint must not remain armed.

### Explicit detach

Repeat with explicit Debugger Detach while the watchpoint remains active, then trigger the access afterward. Target must continue normally.

### Software breakpoint cleanup

Repeat the established active/staged Software/Execute breakpoint cleanup checks. Original bytes must be restored and no stale trap may survive detach/application exit.

## Gate 11 — Full debugger regression

Run the established Mock and live-PS5 debugger cycle, including:

- attach/detach/reattach;
- Pause/Continue;
- Threads and selection;
- Registers and PS5 read-only behavior;
- Breakpoints/Watchpoints add/enable/disable/remove/remove-all;
- multiple-watchpoint conservative attribution when DR6 is unavailable;
- Call Stack;
- Current Instruction -> Disassembler;
- logical breakpoint-byte overlay;
- Step Into;
- breakpoint-aware Step Over;
- Step Out;
- Run to Address;
- temporary-breakpoint cleanup;
- disconnect/reconnect stale-session behavior;
- themes and modeless activation;
- no regression in Scan Results, Saved Addresses, Memory Viewer, or Disassembler.

The known external overlapping Software Breakpoint + Hardware Watchpoint limitation remains external: do not fail host trigger resolution merely because ps5debug-NG consumed an event it never delivered separately.

## Acceptance

Rev33 may be marked verified only after:

- clean Windows build;
- **152/152 PASS**;
- Gate 2 rev32 semantics pass;
- Disassembler debugger action pass;
- snapshot capture and JSON round-trip pass;
- five debugger-table exports pass;
- pairwise and group comparer pass;
- modeless/offline lifecycle pass;
- rev31 teardown safety pass;
- final Mock/live-PS5 regression pass.

After acceptance, the required `0.1.7` Debugger feature block is functionally complete. Optional Deep Snapshot pointer-memory analysis and future assembly/patching for Cheat Maker remain later work.
