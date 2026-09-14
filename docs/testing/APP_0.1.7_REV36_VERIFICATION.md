# Application 0.1.7.rev36 Verification — PS5 Disassembly Watchpoint Resolver Compile Fix

## Purpose

Rev36 is a narrow compile-fix revision over rev35. It carries the complete rev32-rev35 debugger finalization candidate unchanged except for the PS5 Disassembler watchpoint resolver definite-assignment correction required to make the solution build.

Rev34 passed **152/152** automated checks. During focused runtime verification, rev32's Mock and live-PS5 trigger/current-IP separation passed, including a live `DisassemblyDerived` trigger at `0x8033D981` with real stop/current IP `0x8033D984`, followed by a successful Software/Execute breakpoint regression. Rev35 then added the separate Disassembler debugger actions and dual green/yellow hit-stop highlighting, but its first clean Windows build stopped on `CS0177` in `Ps5DisassemblyWatchpointResolver.TryReadAddressRegister` before the automated suite could start. Rev36 fixes only that definite-assignment path, so verification restarts at Gate 1 and then resumes the rev35 runtime sequence.

## Package identity

| Item | Expected |
| --- | --- |
| Application | `0.1.7.rev36` |
| Feature | `PS5 Disassembly Watchpoint Resolver Compile Fix` |
| Plugin API | `2.18.0` |
| Mock | `1.0.1.rev17` |
| PS5 | `0.1.2.rev39` |
| Automated registry | `152` checks |
| Snapshot schema | `teekay87-memory-engine-debugger-snapshot`, version `1` |

## Gate 1 — Clean Windows build and automated suite

1. Extract rev36 into a clean folder.
2. Build the full solution on Windows using the normal project toolchain.
3. Treat warnings-as-errors/build failures as blockers.
4. Run the complete automated verification suite.
5. Expected result: **152/152 PASS**.

Do not continue after any failed check.

## Gate 2 — Watchpoint trigger/stop semantics and dual highlighting

### Mock regression

1. Attach the Debugger to Mock.
2. Trigger the deterministic hardware-watchpoint stop.
3. Confirm the event preserves separate Trigger Instruction and Stop/Current IP and reports `BackendExact`.
4. Confirm `Watchpoint hit` remains on the known trigger instruction.
5. Because Mock does not expose the optional architecture-specific Disassembler watchpoint resolver, automatic **Add Watchpoint** in Mock Disassembler may remain disabled; this is expected.

### Live PS5

Use the already established write/watchpoint scenario if available:

```text
Trigger: 0x8033D981  add [rax+40h],esi
Stop:    0x8033D984  mov rcx,[rdi+30h]
```

Verify:

- `Watchpoint hit` is on `0x8033D981` and that row is highlighted green;
- `Stop / Current IP` is on `0x8033D984` and that row is highlighted yellow;
- Registers still report IP `0x8033D984`;
- Debugger Events still keep the two addresses separate and report `DisassemblyDerived`;
- Continue/stepping behavior follows the real stop context, not the green presentation row;
- an unresolved watchpoint, if encountered, has only the yellow stop row and does not invent a green trigger;
- a normal Software/Execute breakpoint remains green on its intended instruction, shows logical/original bytes, and does not gain an unnecessary yellow stop marker.

Check the dual colors in the currently active theme and later repeat a visual smoke check in Light, Dimmed, and Dark during the final UI regression.

## Gate 3 — Disassembler Add Breakpoint / Add Watchpoint

### Selection gating

1. Keep an attached Debugger and Disassembler open for the same target/session.
2. Select exactly one valid executable instruction and right-click it.
3. Confirm the menu contains separate **Add Breakpoint** and **Add Watchpoint** entries.
4. Select two or more rows and right-click one selected row.
5. Confirm both actions are disabled/greyed out while the multi-row selection remains intact for Copy/Export.
6. Detach/close the matching Debugger while keeping Disassembler open and confirm both actions disable rather than implicitly attaching/opening a debugger.

### Add Breakpoint

1. With exactly one executable instruction selected, confirm **Add Breakpoint** is enabled when the existing validator accepts the request.
2. Invoke it.
3. Confirm a persistent Software/Execute breakpoint is added directly to the existing Breakpoints/Watchpoints manager.
4. Confirm logical/original instruction bytes remain visible.
5. Remove/disable it using the existing manager and confirm normal behavior is unchanged.

### Add Watchpoint — positive live PS5 case

Pause on a resolved/current memory-access instruction for which the active register snapshot belongs to that same stop. Preferred known case:

```text
Instruction: add [rax+40h],esi
RAX:         0x2394E4000   (or the current equivalent in the new run)
```

Expected derivation for that concrete register value:

```text
Address: 0x2394E4040
Access:  Write
Size:    4
```

1. Right-click exactly that instruction.
2. Confirm **Add Watchpoint** is enabled.
3. Invoke it and confirm no second architecture-specific dialog is required.
4. Confirm the existing manager receives a Hardware/Write watchpoint with the derived address and size.
5. Trigger it and confirm the normal rev32 event/marker flow still works.

### Add Watchpoint — negative/safety cases

Verify **Add Watchpoint** is disabled for representative cases:

- a non-memory instruction (`test`, branch, `nop`, etc.);
- `lea`;
- an unrelated Disassembler instruction while paused at a different stop/trigger address;
- a row whose effective address requires unavailable register context;
- any request the existing PS5 breakpoint/watchpoint validator rejects.

If a read memory instruction is tested, remember that the PS5 hardware implementation cannot express read-only data trapping. The derived neutral request is therefore `ReadWrite`, not an invented Read-only request.

## Gate 4 — Standard Snapshot capture

Continue the rev34 procedure unchanged:

1. Running -> **Capture Current** disabled.
2. Pause -> Capture Current enabled.
3. Capture and inspect source/event/register/call-stack/logical-disassembly/breakpoint-context/stack-memory data and section status.
4. Confirm trigger and stop/current IP remain distinct in a watchpoint snapshot.
5. Resume to a new stop and confirm the old snapshot is immutable.
6. Confirm stale connection/target/session state cannot capture against a new target accidentally.

## Gate 5 — Snapshot JSON export/import

1. Export a captured snapshot.
2. Confirm schema `teekay87-memory-engine-debugger-snapshot`, version `1`.
3. Confirm 64-bit addresses remain exact hex strings and raw bytes survive exactly.
4. Import the same file and confirm offline/imported status.
5. Compare original vs imported and expect equivalent captured data.
6. Detach and confirm imported data remains usable offline.
7. Confirm import never reconnects, attaches, recreates breakpoint/watchpoint state, or writes target memory.
8. Confirm invalid schema/version is rejected clearly.

## Gate 6 — Universal Export for debugger tables

Verify independent export scopes for:

1. Threads
2. Registers
3. Breakpoints / Watchpoints
4. Call Stack
5. Events

Confirm structured data, relevant columns, and the stop/trigger distinction in event exports. Exercise JSON plus at least one tabular format.

## Gate 7 — Call Stack Comparer pairwise behavior

Capture/import two snapshots and verify:

- modeless lifecycle/activation;
- Compare 2 selection rules;
- Summary, Call Stack, Registers, Context, Instructions, Breakpoint Context, Memory as data exists;
- raw register-byte equality;
- trigger and stop/current instruction remain distinct;
- logical/original instruction bytes are compared;
- module-relative code identity works;
- ordered frame alignment handles missing/extra frames;
- Export Results uses Universal Export.

## Gate 8 — Group comparison

Create at least two user-defined groups with multiple samples. Verify:

- stable common frames;
- first divergence only when stable within each group;
- stable cross-group register differences;
- variable values are not promoted as stable;
- Summary uses evidence wording such as **Potential discriminator**, not invented game semantics;
- imported and live snapshots use the same comparison pipeline.

## Gate 9 — Modeless lifecycle and offline ownership

With comparer snapshots present:

- detach/close Debugger and continue offline comparison;
- ensure Capture Current no longer treats the old debugger as live;
- reconnect another target and ensure old snapshots are not rebound;
- verify clean activation/close/reopen behavior;
- close the application with Debugger/Disassembler/Comparer open and confirm orderly shutdown.

## Gate 10 — Permanent rev31 teardown safety regression

Mandatory live PS5 regression:

- application exit with active hardware watchpoint, then trigger access: game continues;
- explicit detach with active hardware watchpoint, then trigger access: game continues;
- active/staged Software/Execute breakpoint cleanup restores original bytes and leaves no stale trap.

## Gate 11 — Full debugger regression

Run the established Mock and live-PS5 cycle:

- attach/detach/reattach;
- Pause/Continue;
- Threads;
- Registers and PS5 read-only behavior;
- Breakpoints/Watchpoints add/enable/disable/remove/remove-all;
- multiple-watchpoint conservative attribution;
- Call Stack;
- Current Instruction -> Disassembler;
- logical breakpoint-byte overlay;
- Step Into;
- breakpoint-aware Step Over;
- Step Out;
- Run to Address;
- temporary-breakpoint cleanup;
- disconnect/reconnect stale-session behavior;
- Light/Dimmed/Dark including green hit + yellow stop visibility;
- no regression in Scan Results, Saved Addresses, Memory Viewer, or Disassembler.

The known external overlapping Software Breakpoint + Hardware Watchpoint limitation remains external and must not be reclassified as a host trigger-resolution failure when ps5debug-NG does not deliver a second event.

## Acceptance

Rev36 may be marked verified only after all eleven gates pass. Only then should the remaining `0.1.7` debugger-finalization block be considered verified and ready for the normal version-completion decision.
