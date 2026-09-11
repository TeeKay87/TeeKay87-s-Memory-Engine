# Application 0.1.7.rev11 Verification — Breakpoint Manager and Software Breakpoints

## Status

**Superseded before verification.** Rev11 started from the fully verified rev10 baseline, but its first Windows Gate A build stopped on two `CS8600` nullable diagnostics in `Ps5DebuggerSession.cs`. The automated **114-check** suite and focused Mock/live-PS5 breakpoint gates were therefore not run for rev11. The same breakpoint feature is carried forward by `0.1.7.rev12 - Breakpoint Manager Compile Fix`.


## Recorded Gate A Result

**FAIL — build did not complete.**

Reported diagnostics:

```text
CS8600  Converting null literal or possible null value to non-nullable type.  Ps5DebuggerSession.cs
CS8600  Converting null literal or possible null value to non-nullable type.  Ps5DebuggerSession.cs
XLS0414 The type 'System.Object' was not found. Verify that you are not missing an assembly reference and that all referenced assemblies have been built.  MainWindow.xaml
```

The two `CS8600` diagnostics are the primary compile failure. `XLS0414` is treated as a downstream XAML designer/build symptom because the referenced project did not build successfully. Rev11 is not accepted and no 114/114 result is claimed.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev11` |
| Feature | `Breakpoint Manager and Software Breakpoints` |
| Plugin API | `2.14.0` |
| Mock plugin | `1.0.0.rev12` |
| PS5 plugin | `0.1.0.rev30` |
| Automated checks | `114` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev11 ZIP to a clean directory.
2. Build the solution in the normal Windows Release environment.
3. Run the verification executable.
4. Confirm the new breakpoint checks pass, including:

```text
PASS  Mock debugger software breakpoint lifecycle and hits
PASS  Debugger breakpoint manager source contract
PASS  Debugger breakpoint dialog source contract
PASS  PS5 debugger software breakpoint protocol and hit mapping
```

5. Confirm the final line is:

```text
All 114 checks passed.
```

All 110 rev10 registrations must remain present.

## Gate B — Mock Breakpoint Manager Presentation

1. Connect to Mock, set `TestGame.exe` Active, open Debugger, and Attach.
2. Confirm the Breakpoints pane is visible and the existing Threads/Registers layout still behaves normally.
3. Add a persistent breakpoint at a valid Mock code address.
4. Confirm Address, Enabled state, Software type, Execute access, and Persistent lifetime display correctly.
5. Add a temporary breakpoint at another address and confirm Temporary lifetime.
6. Select/Refresh several times and confirm no duplicates and stable selection.
7. Use **Disassembler...** from a selected breakpoint and confirm the existing Disassembler opens at that address.

## Gate C — Mock Enable / Disable / Remove

1. Select the persistent breakpoint and Disable it. Confirm State becomes Disabled.
2. Refresh. Confirm it remains Disabled.
3. Enable it again. Confirm State becomes Enabled.
4. Remove one breakpoint and confirm only that record disappears.
5. Use **Remove All** with Cancel first: nothing must be removed.
6. Use **Remove All** again and confirm: all breakpoint records are removed.
7. Confirm destructive styling/confirmation follows the active Light, Dimmed, and Dark themes.

## Gate D — Mock Persistent and Temporary Hits

1. Add one persistent breakpoint.
2. Continue/run until the deterministic hit occurs.
3. Confirm the Debugger transitions to Paused and Event History reports `Breakpoint` / stop reason `Breakpoint`.
4. Confirm current RIP matches the breakpoint address and Current Instruction -> Disassembler still works.
5. Confirm the persistent breakpoint remains in the manager.
6. Continue again after adding/selecting a temporary breakpoint and wait for its hit.
7. Confirm the temporary breakpoint disappears after its first hit.
8. Continue/Pause/Detach/Reattach once and confirm no stale breakpoint state leaks into the new attachment.

## Gate E — Live PS5 Add and Persistent Hit

Choose a safe executable instruction address in `eboot.bin` that is expected to execute repeatedly and is appropriate for breakpoint testing.

1. Connect to PS5, set `eboot.bin` Active, open Debugger, Attach.
2. Add one persistent Software/Execute breakpoint while Running.
3. Confirm the breakpoint appears Enabled in the manager.
4. Allow execution to hit the address.
5. Required result:
   - Debugger transitions to Paused;
   - Event History reports a Breakpoint hit;
   - event/current RIP equals the managed breakpoint address, not `address + 1`;
   - register refresh still completes through the rev10 guarded path;
   - Current Instruction -> Disassembler opens the correct instruction;
   - the persistent breakpoint remains listed.

If the chosen address never executes, choose another known-safe executable location; absence of a hit is not by itself a transport failure.

## Gate F — Live PS5 Paused Disable/Remove Safety

This gate verifies the rev11 workaround for the current backend side effect.

1. With the target Paused and an enabled persistent breakpoint still managed, press **Disable**.
2. Confirm the manager shows Disabled but the target **remains Paused**. No Resumed event should appear merely because Disable was clicked.
3. Re-enable it before Continue. Confirm the target remains Paused and the state returns to Enabled.
4. Disable it again while Paused and then press **Continue**. Confirm the staged backend cleanup completes and the target resumes normally.
5. Pause again, add/retain an enabled breakpoint, press **Remove** while Paused, and confirm the target remains Paused.
6. Continue. Confirm cleanup completes and the removed breakpoint does not reappear.

Any unexpected resume at the moment Disable/Remove is clicked is a failure.

## Gate G — Live PS5 Temporary Breakpoint

1. Add a temporary breakpoint at a safe executable address.
2. Let it hit once.
3. Confirm Breakpoint event/current RIP is correct.
4. Confirm the temporary record disappears from the manager after the hit.
5. Press Continue. Confirm staged backend cleanup completes and the target continues normally.
6. Confirm the same temporary breakpoint does not remain armed as a managed record.

## Gate H — Slot / Duplicate / Manager Safety

1. Attempt to add the same managed address twice. The duplicate must be rejected clearly.
2. Confirm Refresh never creates duplicate records.
3. Confirm a slot awaiting paused cleanup is not reused before Continue flushes it.
4. Normal users do not need to fill all 30 PS5 slots, but the UI/plugin must surface a clear error if the backend limit is reached.

## Gate I — Cleanup / Reconnect Regression

1. Add breakpoints, then Detach. Confirm the manager clears with the attachment.
2. Reattach. Confirm no old neutral breakpoint list is reused.
3. Close the Debugger with X while breakpoints exist; open a new Debugger and attach cleanly.
4. Disconnect/reconnect the PS5 connection and confirm the old Debugger becomes stale.
5. Set `eboot.bin` Active again, open a fresh Debugger, Attach, and verify a clean breakpoint manager.
6. Recheck Threads, Registers, Current Instruction -> Disassembler, Memory Viewer, Pause/Continue, and Detach.

## Gate J — Capability and Theme Regression

- Breakpoint pane is hidden for a backend without `Breakpoints`.
- Mock/PS5 show it because rev11 advertises that capability.
- Breakpoint Add/Refresh/action controls disable appropriately when detached, busy, stale, or no selection exists.
- Light/Dimmed/Dark have no default-white WPF surfaces in the new pane/dialog.
- Remove/Remove All use the existing destructive theme role.

## Acceptance Criteria

Rev11 is accepted only when:

- Gate A reports **114/114 PASS**;
- Mock manager/state/hit/temporary behavior passes;
- live PS5 persistent breakpoint hit mapping passes;
- Disable/Remove while Paused never resumes the target before explicit Continue;
- temporary breakpoint cleanup passes;
- duplicate/slot/session cleanup is safe;
- rev10 register transport and existing Disassembler/Memory Viewer/debugger lifecycle regressions remain intact.

Rev11 was not accepted. Rev12 is the compile-corrected breakpoint candidate; Hardware Watchpoints move to **0.1.7.rev13**.
