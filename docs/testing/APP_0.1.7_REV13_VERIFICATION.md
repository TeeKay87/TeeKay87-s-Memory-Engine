# Application 0.1.7.rev13 Verification — Breakpoint Test Build and Layout Fix

## Status

**Superseded after automated Gate A.** Rev13 carries the rev11/rev12 Breakpoint Manager and Software Breakpoints implementation forward, repairs the missing test assertion helper that prevented the rev12 114-check executable from compiling, and changes the initial Breakpoints/Events split to the requested upper-heavy layout. The clean Windows test executable did run, but the supplied result completed at **113/114** because `PS5 plugin metadata and connection settings` still expected the pre-breakpoint PS5 capability set and therefore rejected the correctly advertised `Breakpoints` flag. Rev13 is not verified and is superseded by rev14.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev13` |
| Feature | `Breakpoint Test Build and Layout Fix` |
| Plugin API | `2.14.0` |
| Mock plugin | `1.0.0.rev12` |
| PS5 plugin | `0.1.0.rev31` |
| Automated checks | `114` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev13 ZIP into a clean directory.
2. Build the application/solution in the normal Windows environment.
3. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

4. Confirm there are no `CS0103` errors for `AssertContains`.
5. Confirm the breakpoint checks execute, including:

```text
PASS  Mock debugger software breakpoint lifecycle and hits
PASS  Debugger breakpoint manager source contract
PASS  Debugger breakpoint dialog source contract
PASS  PS5 debugger software breakpoint protocol and hit mapping
```

6. Confirm the final line is:

```text
All 114 checks passed.
```

All 110 rev10 registrations and all four breakpoint registrations must remain present.

## Gate B — Breakpoints/Events Initial Split

Use Mock first so no live target is required for layout validation.

1. Connect to Mock, set the target Active, and open Debugger.
2. At the normal Debugger window size, confirm the right-side **Breakpoints** pane starts visibly larger than **Events**, approximately matching the supplied reference: about **65% Breakpoints / 35% Events**.
3. Confirm the splitter is still draggable in both directions.
4. Drag toward both limits and confirm it stops around the established 20/80 and 80/20 relative bounds while respecting practical pane minimum heights.
5. Move the splitter to a non-default position, resize the whole Debugger window vertically, and confirm both right-side panes scale proportionally without reverting to fixed pixel heights.
6. Close that Debugger window and open a fresh one. Confirm the new window starts again at the rev13 default position.
7. Verify Light, Dimmed, and Dark themes show the same theme-aware splitter rather than a bright default WPF surface.
8. Confirm the left Threads/Registers splitter still starts and behaves as previously verified.

## Gate C — Mock Breakpoint Manager Presentation

1. Attach to Mock.
2. Confirm the Breakpoints pane is visible and action controls are gated correctly by attachment/selection state.
3. Add a persistent breakpoint at a valid Mock code address.
4. Confirm Address, Enabled state, Software type, Execute access, and Persistent lifetime display correctly.
5. Add a temporary breakpoint at another valid address and confirm Temporary lifetime.
6. Refresh several times and confirm no duplicates and stable selection.
7. Use **Disassembler...** from a selected breakpoint and confirm the existing Disassembler opens at that address.

## Gate D — Mock Enable / Disable / Remove

1. Select the persistent breakpoint and Disable it. Confirm State becomes Disabled.
2. Refresh. Confirm it remains Disabled.
3. Enable it again. Confirm State becomes Enabled.
4. Remove one breakpoint and confirm only that record disappears.
5. Use **Remove All** with Cancel first: nothing must be removed.
6. Use **Remove All** again and confirm all breakpoint records are removed.
7. Confirm Remove/Remove All use the application danger/destructive styling.

## Gate E — Mock Persistent and Temporary Hits

1. Add one persistent breakpoint.
2. Continue until the deterministic hit occurs.
3. Confirm Debugger transitions to Paused and Event History reports `Breakpoint` with stop reason `Breakpoint`.
4. Confirm current RIP matches the breakpoint address and Current Instruction -> Disassembler still works.
5. Confirm the persistent breakpoint remains in the manager.
6. Add a temporary breakpoint and Continue until that deterministic hit occurs.
7. Confirm the temporary breakpoint disappears after its first hit.
8. Continue/Pause/Detach/Reattach and confirm no stale breakpoint state leaks into a new attachment.

## Gate F — Live PS5 Add and Persistent Hit

Choose a safe executable instruction address in `eboot.bin` that is expected to execute and is appropriate for debugger testing.

1. Connect to PS5, set `eboot.bin` Active, open Debugger, and Attach.
2. Add one persistent Software/Execute breakpoint while Running.
3. Confirm it appears Enabled.
4. Allow the target to hit the address.
5. Required result:
   - Debugger transitions to Paused;
   - Event History reports a Breakpoint hit;
   - event/current RIP equals the managed breakpoint address, not `address + 1`;
   - register refresh still completes through the rev10 guarded path;
   - Current Instruction -> Disassembler opens the correct instruction;
   - the persistent breakpoint remains listed.

If the selected address does not execute, choose another known-safe executable address; absence of execution is not by itself a transport failure.

## Gate G — Live PS5 Paused Disable/Remove Safety

This verifies the client workaround for the current ps5debug-NG disable side effect.

1. With the target Paused and an enabled persistent breakpoint managed, press **Disable**.
2. Confirm the manager shows Disabled while the target **remains Paused**. No Resumed event should appear merely because Disable was clicked.
3. Re-enable before Continue. Confirm the target remains Paused and State returns to Enabled.
4. Disable again while Paused, then press **Continue**. Confirm staged backend cleanup completes and execution resumes normally.
5. Pause again, retain/add an enabled breakpoint, press **Remove** while Paused, and confirm the target remains Paused.
6. Continue. Confirm cleanup completes and the removed breakpoint does not return.

Any unexpected resume at the instant Disable/Remove is clicked is a failure.

## Gate H — Live PS5 Temporary Breakpoint

1. Add a temporary breakpoint at a safe executable address.
2. Let it hit once.
3. Confirm Breakpoint event/current RIP is correct.
4. Confirm the temporary record disappears from the manager after the hit.
5. Press Continue. Confirm staged backend cleanup completes and target execution continues normally.
6. Confirm the temporary breakpoint does not remain armed as a managed record.

## Gate I — Duplicate, Slot, and Manager Safety

1. Attempt to add the same managed address twice. The duplicate must be rejected clearly.
2. Confirm Refresh never creates duplicate records.
3. Confirm a slot awaiting paused cleanup is not reused before Continue flushes it.
4. Normal runtime verification does not need to fill all 30 PS5 slots, but the plugin/UI must report a clear error when the backend limit is reached in deterministic coverage.

## Gate J — Cleanup / Reconnect Regression

1. Add breakpoints, then Detach. Confirm the manager clears with the attachment.
2. Reattach. Confirm no old neutral breakpoint list is reused.
3. Close Debugger with X while breakpoints exist; reopen and attach cleanly.
4. Disconnect/reconnect PS5 while Debugger exists and confirm the old window becomes stale/inactive.
5. Set `eboot.bin` Active again, open a fresh Debugger, Attach, and verify a clean breakpoint manager.
6. Recheck Threads, Registers, Current Instruction -> Disassembler, Memory Viewer, Pause/Continue, and Detach.
7. Confirm there are no command-stream framing errors, stale breakpoint hits, duplicate events, or optional-register regressions.

## Gate K — Capability and Theme Regression

- Breakpoint pane is hidden for a backend without `Breakpoints`.
- Mock and PS5 show it because they advertise that capability.
- Add/Refresh/action controls disable appropriately when detached, busy, stale, or without a required selection.
- Light, Dimmed, and Dark have no default-white WPF surfaces in the new manager/dialog/splitter area.
- Remove, Remove All, Cancel, Detach, and other dismissive/destructive actions keep their established semantic styles.

## Acceptance Criteria

Rev13 required **114/114** plus the focused breakpoint/runtime gates. Because Gate A completed at **113/114**, those acceptance criteria were not met and rev13 is superseded. The same runtime acceptance continues with rev14 after the capability-expectation correction. Hardware Watchpoints therefore move to rev15.


## Recorded Windows Gate A Result

The supplied rev13 run completed all 114 registrations and reported exactly one failure:

```text
FAIL  PS5 plugin metadata and connection settings
      PS5 plugin should advertise its implemented connection, memory access, native scan, process-control, disassembly, debugger, thread-control, and register-read support. Expected: Connect, ProcessEnumeration, ForegroundProcess, MemoryRegionEnumeration, MemoryRead, MemoryWrite, ProcessSuspend, ProcessResume, NativeValueScanning, Disassembly, Debugger, RegisterAccess, ThreadEnumeration, ThreadControl; actual: Connect, ProcessEnumeration, ForegroundProcess, MemoryRegionEnumeration, MemoryRead, MemoryWrite, ProcessSuspend, ProcessResume, NativeValueScanning, Disassembly, Debugger, Breakpoints, RegisterAccess, ThreadEnumeration, ThreadControl.

1 of 114 checks failed.
```

Production is correct here: both the PS5 plugin and the breakpoint-specific verification paths intentionally advertise/use `TargetCapabilities.Breakpoints`. The stale equality expectation in `VerifyPs5PluginMetadataAsync` is the only observed failing check. Rev14 corrects that verification expectation and leaves the runtime breakpoint implementation unchanged.
