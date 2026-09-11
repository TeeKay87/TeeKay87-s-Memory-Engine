# Application 0.1.7.rev29 Verification — Logical Disassembly and Debugger Markers

## Status

**VERIFIED.** The Windows automated suite passed **138/138**. Focused live-PS5 acceptance then confirmed that Software/Execute breakpoint instrumentation no longer replaces the original bytes/instruction shown in the Disassembler, Hardware/Write watchpoint stops produce `Watchpoint hit` marker metadata without changing code bytes, and `Breakpoint` plus `Watchpoint hit` can coexist in one displayed range while the logical/original instruction stream remains authoritative.

## Verified Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev29` |
| Feature | `Logical Disassembly and Debugger Markers` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev36` |
| Automated checks | `138` |

## Accepted Baseline Carried Forward

Rev28 passed **135/135** and its focused live PS5 acceptance completed successfully. Do not repeat the full debugger matrix before rev29's focused presentation checks. The following remain accepted unless rev29 exposes a direct regression:

- logical software-breakpoint event/IP/Register/Call Stack context;
- Step Into from a logical software-breakpoint stop;
- breakpoint-aware Step Over;
- Step Out;
- successful and interrupted Run to Address;
- temporary-breakpoint retirement after success/interruption;
- staged cleanup through Continue;
- safe Detach with pending breakpoint cleanup without terminating the game;
- normal Detach/Reattach fresh-state behavior; and
- existing scanner, Memory Viewer, standalone Disassembler navigation/copy, themes, and universal export behavior.

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev29 ZIP into a clean directory.
2. Build the complete solution/application normally in Visual Studio.
3. Confirm there are no C# or XAML compile failures and no warnings promoted to errors.
4. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

5. Confirm the final line is exactly:

```text
All 138 checks passed.
```

Do not continue runtime acceptance if any automated check fails.

## Gate B — Basic Disassembler/UI Regression

1. Open the Disassembler against Mock Target or PS5 without an active debugger breakpoint in the displayed area.
2. Confirm the visible column order is exactly **Address / Bytes / Markers / Instruction**.
3. Confirm ordinary rows have an empty Markers cell and otherwise retain the established row sizing, origin highlighting, syntax highlighting, selection, scrolling, and navigation behavior.
4. Spot-check Light, Dimmed, and Dark. The Markers column must use normal theme-driven DataGrid presentation; there must be no fixed white/light surface or new hardcoded marker color.
5. Run a normal Displayed Instructions export and confirm `Markers` is available as a selectable column while the existing structured columns remain available.

## Gate C — Mock Logical Software-Breakpoint Presentation

Use a deterministic executable Mock instruction.

1. Attach Debugger and Pause.
2. Open the same code in Disassembler and note its original Bytes and Instruction.
3. Add a persistent **Software / Execute** breakpoint on that instruction.
4. Refresh or reopen the Disassembler at that address.
5. Confirm:
   - Bytes still show the complete original instruction bytes;
   - Instruction still shows the original decoded instruction;
   - Markers shows **Breakpoint**;
   - no `int3`/trap instruction or false instruction boundary appears because of the debugger breakpoint.
6. Disable the breakpoint while Paused and refresh/reopen the Disassembler. Confirm the original instruction remains visible while paused backend retirement is staged; the active `Breakpoint` marker must not falsely remain active. A disabled record may show **Breakpoint (disabled)** when the manager still exposes that record.
7. Continue so staged cleanup completes, Pause again, and confirm the original instruction still displays normally without a stale active marker.
8. Remove the breakpoint and confirm no stale marker survives.

## Gate D — Mock Hardware-Watchpoint Marker

1. Create a hardware watchpoint on a deterministic Mock data address.
2. Continue until it triggers.
3. From the paused stop context, open the current instruction in Disassembler.
4. Confirm the instruction at the event/current IP displays **Watchpoint hit** in Markers.
5. Confirm Bytes and Instruction are unchanged by that marker.
6. Continue or create another ordinary paused stop and confirm the stale **Watchpoint hit** marker clears.

This gate verifies that watchpoints contribute metadata only; they must never create a code-byte overlay.

## Gate E — Live PS5 Software-Breakpoint Original Instruction

If the same game build and mapping used during the discovery session are still active, the known instruction may be reused:

```text
0x8033D981  01 70 40  add [rax+40h],esi
```

If that address is no longer valid for the active build, choose another known executable multi-byte instruction and record its original bytes before arming the breakpoint.

1. Attach and Pause.
2. Open Disassembler at the chosen instruction and record the original Bytes/Instruction.
3. Add a persistent **Software / Execute** breakpoint at that exact instruction start.
4. Refresh or reopen Disassembler while the breakpoint remains enabled.
5. For the known money example, confirm the row remains:

```text
Address      Bytes       Markers      Instruction
0x8033D981   01 70 40    Breakpoint   add [rax+40h],esi
```

6. Specifically confirm the Disassembler does **not** show:

```text
0x8033D981  CC       int3
0x8033D982  70 40    jo ...
```

7. Continue until the Software/Execute breakpoint triggers. While stopped logically on that breakpoint, reopen/refresh the Disassembler and confirm the same original bytes/instruction plus `Breakpoint` marker remain visible.
8. Disable or remove the breakpoint while Paused. Refresh/reopen before Continue and confirm no backend `CC` leaks into the visible instruction during staged cleanup.
9. Continue so cleanup can complete, Pause again, and confirm the instruction remains original and no stale active breakpoint marker survives.

This gate is presentation-only. The breakpoint must remain functionally armed until it is disabled/removed; rev29 must not restore the target byte merely to make the Disassembler look correct.

## Gate F — Live PS5 Hardware-Watchpoint Marker

The previously discovered money data address may be reused only if it is still valid for the same game build/session:

```text
Money data: 0x239629040
Write instruction: 0x8033D981  add [rax+40h],esi
Reported post-access/current IP during the observed write: 0x8033D984
```

1. Create a Hardware/Write watchpoint on the valid money/data address.
2. Continue and perform the in-game action that changes the watched value.
3. Wait for the watchpoint event and Paused state.
4. Open the Disassembler from the semantic current instruction context.
5. Confirm the row at the reported event/current IP carries **Watchpoint hit**.
6. Confirm neighboring code, including the actual write instruction when in range, still shows its normal original bytes/instructions. The watchpoint must not inject or replace instruction bytes.
7. Continue or Pause at an unrelated context and confirm the old watchpoint-hit marker no longer appears as current.

## Gate G — Export and Session-Lifetime Regression

1. With a breakpoint marker or watchpoint-hit marker visible, export Displayed Instructions to JSON.
2. Confirm the relevant row exports the logical/original `bytes` and `instruction` plus its separate `markers` value.
3. Detach and confirm the Disassembler no longer receives stale debugger markers from the detached coordinator after refresh.
4. Reattach, Pause, and confirm old breakpoint/watchpoint-hit markers do not return unless the new debugger session actually owns them.
5. Spot-check existing Copy Bytes / Copy Instruction / Copy Selected. They must project the same logical/original bytes and instruction text shown in the table.
6. Confirm no target crash, transport/framing error, stale marker, fake instruction boundary, or regression in ordinary Disassembler navigation occurs.

## Verified Runtime Result

The completed rev29 acceptance recorded the following concrete runtime evidence:

- the packaged Windows suite ended with `All 138 checks passed.`;
- with a Software/Execute breakpoint on `0x8033D981`, the Disassembler displayed `01 70 40` / `add [rax+40h],esi` with the `Breakpoint` marker instead of exposing the backend `CC / int3` instrumentation or a false decode at `0x8033D982`;
- with a Hardware/Write watchpoint on money data `0x239629040`, the watchpoint stop opened at reported current IP `0x8033D984` and displayed `Watchpoint hit` while the surrounding original instruction stream remained unchanged;
- while paused after the watchpoint stop, adding the Software/Execute breakpoint at `0x8033D981` produced both markers simultaneously in the same disassembly range without altering either row's logical bytes/instruction; and
- the existing automated export/overlay lifecycle contracts remained green, including structured `markers` export and stale-session overlay clearing.

During the combined runtime exploration an external ps5debug-NG interaction was also isolated: if a Software breakpoint is already armed on the exact instruction whose execution would trigger an active hardware watchpoint, the backend's internal restore/`PT_STEP`/`wait4`/rearm sequence consumes the overlapping watchpoint stop instead of delivering it as a second client event. This is documented separately in `docs/bug-reports/ps5debug-ng-software-breakpoint-step-consumes-overlapping-watchpoint-hit.md`. It does not invalidate rev29's logical presentation behavior.

## Completion Decision

Rev29 is accepted. Its logical Disassembler/Markers feature is complete and becomes the baseline for rev30. The next revision is consumed by Breakpoint/Watchpoint classification and validated Scan Results/Saved Addresses debugger-address actions; Debugger Integration, Export and Finalization therefore moves to rev31.
