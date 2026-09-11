# Application 0.1.7.rev31 Verification — Safe PS5 Watchpoint Detach Cleanup

## Status

**CANDIDATE.** Rev31 supersedes rev30 before rev30 verification because live PS5 shutdown testing exposed a target-safety issue with hardware watchpoints surviving client teardown.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev31` |
| Feature | `Safe PS5 Watchpoint Detach Cleanup` |
| Plugin API | `2.16.0` |
| Mock plugin | `1.0.0.rev16` |
| PS5 plugin | `0.1.0.rev38` |
| Automated checks | `142` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the rev31 ZIP to a clean directory and build the full solution in Visual Studio.
2. Confirm no C#/XAML compile failure and no warning promoted to an error.
3. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

4. The final line must be exactly `All 142 checks passed.`

The new check **PS5 debugger safe detach clears hardware watchpoints** must pass. It verifies both a still-active persistent hardware watchpoint and a staged temporary-watchpoint cleanup during direct debugger disposal.

## Gate B — Original Application-Exit Failure Reproduction

This is the primary live acceptance gate for rev31.

1. Connect to PS5, set the correct Active Target, open Debugger, and Attach.
2. Add a hardware Write watchpoint to a value that can be triggered reliably.
3. Trigger it once and confirm the watchpoint event works normally.
4. Continue if needed so the game is running again.
5. Leave the hardware watchpoint enabled. Do **not** Remove or Disable it manually.
6. Close the entire application normally.
7. Return to the game and perform the action that would have triggered the watchpoint again.
8. **PASS:** the game continues normally and does not terminate because of a stale debug-register trap.

Repeat once with the target paused on a watchpoint hit when the application is closed. The shutdown path must still complete without leaving the watchpoint armed.

## Gate C — Explicit Debugger Detach with Active Watchpoint

1. Reopen the application, connect, open Debugger, and Attach.
2. Add a hardware Write or Read/Write watchpoint and leave it enabled.
3. Use the Debugger's normal **Detach** action without manually removing the watchpoint first.
4. Trigger the formerly watched access in the game after detach.
5. Confirm the game remains running and no debugger trap is left behind.
6. Reattach the Debugger and confirm the previous watchpoint record is not present.

## Gate D — Multiple Hardware Slots

1. Attach and add at least two legal hardware watchpoints in different DR slots.
2. Leave both enabled and close the application, or explicitly Detach.
3. Trigger each formerly watched access after teardown.
4. Confirm neither access terminates the game.

This checks that cleanup is not limited to the first hardware slot.

## Gate E — Temporary Watchpoint Cleanup Regression

1. Create a temporary hardware watchpoint through a debugger workflow that removes it logically after its first hit.
2. Stop immediately after the hit, before a normal Continue has had a chance to flush staged backend cleanup.
3. Detach or close the application.
4. Confirm teardown clears the staged slot and the game remains stable when the old watched access occurs again.

The automated protocol test covers this state deterministically; the live step is a focused sanity check.

## Gate F — Rev30 Workflow Regression

1. Confirm the Breakpoints / Watchpoints table still shows **Address | State | Type | Mechanism | Access | Size | Lifetime**.
2. Confirm a Software/Execute record shows `Type = Breakpoint`, `Mechanism = Software`.
3. Confirm a hardware data watchpoint shows `Type = Watchpoint`, `Mechanism = Hardware`.
4. With Debugger attached to the same target, confirm Scan Results and Saved Addresses still expose correctly gated **Add Breakpoint** / **Add Watchpoint** actions.
5. Confirm those actions are disabled when Debugger is detached or the address/request is invalid.

## Gate G — Rev29 Logical Disassembly Regression

1. Keep a Software/Execute breakpoint active on a known multi-byte instruction.
2. Open Disassembler and confirm original logical bytes/instruction remain visible with `Breakpoint` in `Markers`, never backend `CC / int3` instrumentation.
3. Trigger a hardware watchpoint independently and confirm `Watchpoint hit` remains metadata-only.

## Gate H — Final Cleanup / Reattach

1. After one application-exit cleanup cycle and one explicit-Detach cleanup cycle, reconnect and attach again.
2. Add/remove a new hardware watchpoint and a software breakpoint normally.
3. Confirm Pause/Continue/Detach still work and no stale slot, duplicate rejection, unexpected target resume, or transport failure is carried across sessions.

## Completion Rule

Rev31 may be accepted only after a clean Windows build, **142/142**, application-exit cleanup PASS, explicit-Detach cleanup PASS, multi-slot/staged-cleanup sanity PASS, rev30 workflow regression PASS, rev29 logical-disassembly regression PASS, and clean reattach PASS.

If accepted without another correction, the next planned revision is `0.1.7.rev32 — Integration, Export and Finalization`.
