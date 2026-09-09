# Application 0.1.7.rev6 Verification — Threads and Thread Control

## Purpose

Application `0.1.7.rev6 - Threads and Thread Control` builds directly on the fully verified `0.1.7.rev5` baseline. Rev5 passed all **97/97** automated Windows checks plus the focused responsive-header/Mock/live-PS5 acceptance. The rev3 PS5 debugger transport carried through rev4/rev5 is therefore considered hardware-verified for Attach/Detach/Pause/Continue, TCP 755 callback ownership, transport isolation, cleanup, reconnect-generation invalidation, multiple-window exclusivity/recovery, and regression behavior. The optional natural async-interrupt event mapping check could not be safely triggered and remains deferred rather than failed.

Rev6 adds the next dependency-ordered debugger layer: neutral thread enumeration/selection/refresh and capability-gated individual-thread Suspend/Resume. It also removes passive successful memory-map count text from the permanent target header while preserving the actual memory-map load and error path.

## Build Under Test

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev6` |
| Feature title | `Threads and Thread Control` |
| Plugin API | `2.12.0` |
| Mock plugin | `1.0.0.rev9` |
| PS5 plugin | `0.1.0.rev26` |
| Expected automated checks | `101` |

## Gate A — Clean Windows Release Build and Automated Verification

1. Extract the rev6 package to a clean directory.
2. Build the solution in **Release** on Windows.
3. Confirm that no new compile error or unexpected warning is introduced by rev6.
4. Run `TeeKay87.MemoryEngine.Tests`.
5. The required final line is:

```text
All 101 checks passed.
```

Rev6 must not be marked verified if any previous rev5 registration disappears. The expected registry is the complete 97-check rev5 set plus exactly four new checks:

- `Mock debugger thread enumeration and control`;
- `Debugger workspace thread panel source contract`;
- `PS5 debugger thread enumeration and control protocol`;
- `Main workspace passive memory-map status removal`.

## Gate B — Main Workspace Header Cleanup

Run this first with the Mock plugin and then spot-check it with PS5.

1. Connect and set an Active Target so memory-map enumeration succeeds.
2. Confirm that passive success prose such as `Memory map: 1 region loaded.` or `Memory map: 1418 regions loaded.` is **not** shown in the permanent two-row target header.
3. Confirm that the actual memory-map-dependent workflows still function: Memory Viewer region information/navigation and other existing consumers must continue to receive the loaded map.
4. If a real memory-map enumeration error can be produced safely, confirm that the existing error surface remains visible. Do not manufacture a dangerous target failure solely for this check.
5. Reconfirm that the verified two-row order, responsive 180-unit maximum row-1 inputs, side padding, button sizing/text alignment, Plugin details placement, and bottom connection indicator remain unchanged.

Expected result: only passive successful map-count text is removed; functionality is not removed.

## Gate C — Mock Threads Pane and Enumeration

1. Select **In-Memory Test Target**.
2. Connect, select `TestGame.exe`, and click **Set Active Target**.
3. Open **Debugger...**.
4. Before Attach, confirm the normal detached state remains unchanged.
5. Click **Attach**.
6. Confirm that the new **Threads** pane is visible because Mock advertises `ThreadEnumeration`.
7. Confirm exactly three deterministic rows appear:

| Thread | Name | State after Attach |
| --- | --- | --- |
| `0x1` | `Main` | `Running` |
| `0x2` | `Worker` | `Running` |
| `0x3` | `Render` | `Running` |

8. Confirm a thread can be selected independently from the event list.
9. Select `Worker`, click the thread **Refresh** button, and confirm `Worker` remains selected because its neutral id still exists.
10. Confirm the thread count remains three and no duplicate rows appear after repeated Refresh.

## Gate D — Mock Individual Thread Control and Whole-Target State

With the Mock debugger attached and overall state `Running`:

1. Select `Worker (0x2)`.
2. Click **Suspend**.
3. Confirm Worker becomes `Suspended` and **Resume** becomes available for that row.
4. Confirm Main and Render remain `Running`.
5. Click whole-target **Pause**.
6. Confirm overall debugger state becomes `Paused`.
7. Confirm Main and Render present as `Stopped`, while Worker remains `Suspended`.
8. Confirm individual **Suspend/Resume** controls are disabled while the whole target is Paused.
9. Click whole-target **Continue**.
10. Confirm Main/Render return to `Running`, while Worker still presents as `Suspended`.
11. Select Worker and click **Resume**.
12. Confirm all three rows now present as `Running`.
13. Explicitly Detach and confirm the thread list is cleared.
14. Reattach and confirm the deterministic three-row list repopulates cleanly.

Expected result: per-thread state and whole-target state remain distinct, and rev6 never issues individual-thread control while the debugger target is globally Paused.

## Gate E — Mock Regression Smoke

After the thread checks, perform a focused smoke test rather than repeating the complete historical suite:

- existing Attach/Pause/Continue/Detach and neutral event history;
- one simple scan;
- Saved Addresses;
- Memory Viewer;
- Disassembler;
- theme switch;
- Disconnect/Reconnect.

Expected result: rev6 thread UI/backend additions do not alter the previously verified rev2/rev5 workflows.

## Gate F — Live PS5 Thread Enumeration

Use the same known-good PS5/ps5debug-NG environment used to verify rev5.

1. Connect PS5, select `eboot.bin`, and set it as Active Target.
2. Open Debugger and Attach.
3. Confirm the debugger enters `Running` as in rev5.
4. Confirm the **Threads** pane becomes populated automatically after Attach.
5. Confirm returned thread ids are non-zero and unique.
6. Confirm names are displayed where ps5debug-NG returns them; an empty name for an otherwise valid id is permitted because rev6 deliberately retains a row if the optional thread-info request fails.
7. Click thread **Refresh** several times and confirm the list remains responsive and no duplicate rows accumulate.
8. Select one thread, Refresh, and confirm selection is preserved when that id is still returned.

Do not require a specific thread count or fixed names on real hardware. Those values belong to the running game/firmware/backend and are not part of the neutral contract.

## Gate G — Live PS5 Individual Thread Suspend / Resume

This is a hardware-affecting debugger operation. Use only a thread that can be identified as safe/non-critical for a brief test. Do **not** suspend an unknown main/game/render/system-critical thread simply to satisfy the gate.

1. With debugger state `Running`, select a known safe non-critical thread.
2. Record its id/name.
3. Click **Suspend**.
4. Confirm the command succeeds and the selected row presents as `Suspended`.
5. Immediately click **Resume**.
6. Confirm the same row returns to `Running`.
7. Confirm the game remains responsive and the debugger remains attached.
8. Refresh the thread list and confirm the row is still coherent if the thread still exists.

If no thread can be safely identified, record this specific live ThreadControl gate as **deferred for safety**, not as a false PASS. Mock and automated protocol coverage still verify the logic, but rev6 should not be called fully hardware-verified for individual PS5 thread control until a safe live Suspend/Resume succeeds.

## Gate H — Live PS5 Whole-Target Interaction After Thread Work

1. With no intentionally suspended individual thread, run whole-target **Pause → Continue** once.
2. Confirm the thread list refreshes after each transition.
3. Confirm ordinary rows present as stopped while whole-target Paused and return to Running after Continue.
4. Confirm per-thread controls are unavailable while whole-target Paused.
5. Confirm the established event history still records the normal Pause/Resume events.

Do not combine a deliberately suspended real PS5 thread with a whole-target Pause unless there is a known safe reason to do so; the deterministic mixed-state rule is already covered by Mock.

## Gate I — Live PS5 Dedicated Transport and Cleanup Regression

After thread enumeration/control:

1. While Debugger is attached and Running, refresh the normal process list.
2. Perform a safe Memory Viewer read.
3. Confirm the debugger remains attached and thread Refresh still works.
4. Detach, then reattach without reconnecting the PS5 plugin; confirm Threads repopulates.
5. Close an attached Debugger window with **X**, reopen, attach again, and confirm TCP 755 plus thread enumeration recover normally.
6. Disconnect the main PS5 session while Debugger is open; confirm the old Debugger becomes stale/unusable.
7. Reconnect, set Active Target, open a new Debugger, attach, and confirm Threads populate on the new connection generation.

Expected result: the new thread commands remain isolated on the dedicated debugger command connection and participate in all existing debugger cleanup rules.

## Gate J — Final PS5 Regression Smoke

Perform a short final check of previously verified PS5 functionality:

- process enumeration and Active Target;
- memory map functionality despite removal of passive success text;
- safe memory read and, if desired, a known-safe write/read-back;
- simple scan/refinement;
- Saved Addresses;
- Memory Viewer;
- Disassembler;
- theme switching;
- Disconnect/Reconnect.

The natural TCP 755 async-breakpoint/watchpoint interrupt scenario remains outside rev6's forced acceptance because rev6 does not yet add breakpoints/watchpoints and no unsafe crash should be manufactured. If a natural interrupt occurs, it should continue to follow the already-reviewed neutral Paused event mapping.

## Acceptance Criteria

`0.1.7.rev6` is verified only when:

- the clean Windows Release build succeeds;
- all **101/101** automated checks pass;
- header passive-memory-map status removal is visually verified without breaking map consumers/errors;
- Mock thread enumeration, selection preservation, Suspend/Resume, mixed whole-target state, detach/reattach, and regression smoke all pass;
- real PS5 thread enumeration/refresh/selection pass;
- a safe real PS5 individual-thread Suspend/Resume passes, or that specific gate is explicitly recorded as deferred rather than silently claimed;
- PS5 whole-target interaction, dedicated transport/cleanup, and final regression smoke pass;
- no previously verified functionality is regressed.


## Final Recorded Result

Rev6 completed its Windows and focused runtime verification on 2026-09-09.

- Automated Windows suite: **101/101 PASS**.
- Main-workspace passive memory-map presentation cleanup: **PASS**.
- Mock thread enumeration/control and regression gates: **PASS**.
- Live PS5 thread enumeration: **PASS**; 82 real threads were enumerated in the tested `eboot.bin` session and repeated refresh preserved stable ids/selection.
- Live PS5 individual thread Suspend/Resume: **IMPLEMENTED / BACKEND BLOCKED**. The documented suspend request for enumerated thread `0x18E74` (`Job.Worker 0`) returned ps5debug-NG on-wire `CMD_ERROR` `0xF0000001`. The game, PS5, debugger attachment, and normal traffic remained stable. Resume was not forced because Suspend never succeeded.
- Live PS5 whole-target Pause/Continue with thread refresh, dedicated transport/cleanup/reconnect, and final regression smoke: **PASS**.

The client-side rev6 feature is accepted as the verified baseline for rev7. The external ps5debug-NG failure is documented in [`../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md`](../bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md) and is not treated as a Memory Engine crash/regression.
