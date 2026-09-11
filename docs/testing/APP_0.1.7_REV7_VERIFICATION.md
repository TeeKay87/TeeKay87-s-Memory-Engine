# Application 0.1.7.rev7 Verification — Registers and Stop Context


> **Status:** Superseded by `0.1.7.rev8` after the packaged rev7 Windows verification repeatedly produced **106/107**. The only failure was `Debugger workspace command and event source contract`; investigation showed the production connection-generation binding was intact and the source-contract check was falsely requiring the old explicit `new DebuggerViewModel(...)` spelling instead of accepting the rev7 target-typed `new(...)` constructor expression.
## Purpose

This checklist verifies `0.1.7.rev7 - Registers and Stop Context` on Windows, first with the deterministic Mock backend and then with a live PS5/ps5debug-NG target. Rev7 starts from the accepted rev6 baseline: **101/101** Windows checks passed, all Mock/live-PS5 thread gates passed except individual PS5 Suspend/Resume, and that one request is documented as an external ps5debug-NG backend blocker rather than a client crash/regression.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev7` |
| Feature | `Registers and Stop Context` |
| Plugin API | `2.13.0` |
| Mock plugin | `1.0.0.rev10` |
| PS5 plugin | `0.1.0.rev27` |
| Automated checks | `107` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the rev7 package to a clean directory.
2. Build the solution in the normal Windows Release environment.
3. Run the verification executable.
4. Confirm the final line is:

```text
All 107 checks passed.
```

The six rev7 registrations are:

- `Mock debugger register snapshots and writes`;
- `Debugger workspace register panel source contract`;
- `Debugger register value codec source contract`;
- `Debugger register edit dialog source contract`;
- `Debugger current instruction Disassembler integration`;
- `PS5 debugger general register snapshot protocol`.

## Gate B — Register Capability and Lifecycle Presentation

Run this with Mock first.

1. Connect, select `TestGame.exe`, set Active Target, and open Debugger.
2. Before Attach, confirm no stale register values are shown.
3. Attach while the target is Running.
4. Confirm the Threads pane remains as verified in rev6 and the Registers section is available because Mock advertises `RegisterAccess`.
5. Confirm register values are not populated while Running.
6. Pause the target.
7. Confirm the selected thread now receives a register snapshot.
8. Continue and confirm the register snapshot is cleared rather than left visible as if it were current.
9. Detach and confirm register presentation is cleared.

Expected result: register data exists only for a valid selected-thread stop context.

## Gate C — Mock Register Snapshot and Thread Selection

1. Attach to Mock and Pause.
2. Confirm the Registers table populates for the selected thread.
3. Confirm the list contains stable 64-bit values and includes semantic frame-pointer, stack-pointer, and instruction-pointer rows.
4. Note the current instruction-pointer address.
5. Select another Mock thread while still Paused.
6. Confirm the snapshot refreshes for that thread and remains coherent.
7. Return to the original thread and confirm its deterministic snapshot returns.
8. Use **Refresh** repeatedly and confirm no duplicate rows or selection corruption appears.

The host must not depend on the literal names `RIP`, `RSP`, or `RBP` to perform semantic actions; those names are merely Mock/backend presentation.

## Gate D — Mock Safe Register Editing and Read-Back Verification

1. Stay Paused on Mock and select a writable ordinary register.
2. Click **Edit...**.
3. Confirm the dialog shows the current value and rejects malformed input.
4. Confirm submitting the unchanged value is rejected.
5. Enter a different value that fits the register width and choose **Write & Verify**.
6. Confirm the write succeeds, the snapshot refreshes, and the displayed value matches the requested value.
7. Refresh again and confirm the value remains changed.
8. Continue the target and confirm **Edit...** is no longer usable because the paused stop context no longer exists.

Expected result: the shared write path is gated, width-safe, and verified by exact backend read-back rather than optimistic local UI mutation.

## Gate E — Mock Current Instruction to Disassembler

1. Pause Mock and select a thread with a current instruction pointer.
2. Note the displayed current-instruction address.
3. Click the debugger's **Disassembler...** action for the current instruction.
4. Confirm the normal Disassembler workspace opens at that address and highlights/resolves it using the already-verified Disassembler behavior.
5. Return to Debugger, select another thread, and repeat if its instruction pointer differs.

Expected result: Debugger reuses the normal Disassembler instead of maintaining a separate debugger-local decoder/view.

## Gate F — Mock Regression Smoke

Perform a focused regression check after the new register workflows:

- Attach/Pause/Continue/Detach and event history;
- rev6 Threads Refresh and per-thread Mock Suspend/Resume;
- simple Scan;
- Saved Addresses;
- Memory Viewer;
- Disassembler;
- theme switch;
- Disconnect/Reconnect and fresh debugger Attach.

## Gate G — Live PS5 General Register Snapshot

Do not test register writing on PS5 in rev7.

1. Connect to the known-good PS5/ps5debug-NG target.
2. Select `eboot.bin`, set Active Target, open Debugger, and Attach.
3. Confirm Threads populate as in verified rev6.
4. Select a thread and click whole-target **Pause**.
5. Confirm the Registers section populates for the selected thread.
6. Confirm the snapshot contains the complete mapped general-register set (26 neutral rows for the current amd64 block).
7. Confirm values render as fixed-width hexadecimal data with plausible 64/32/16-bit widths according to the returned row.
8. Confirm the semantic current-instruction address is present when the selected snapshot contains an instruction pointer.
9. Confirm PS5 register rows are read-only and **Edit...** is disabled/unavailable.
10. Confirm no protocol/framing error is shown.

Expected result: `CMD_DEBUG_GET_REGISTERS` produces a read-only selected-thread stop-context snapshot without exposing PS5/FreeBSD register structures to the host.

## Gate H — Live PS5 Thread Selection and Register Refresh

While the target remains Paused:

1. Note several values for the currently selected thread.
2. Select another thread.
3. Confirm a fresh register snapshot is requested and the presentation changes where the backend state differs.
4. Select the original thread again and confirm a coherent snapshot returns.
5. Click register **Refresh** several times.
6. Confirm row count/widths remain stable, no duplicates accumulate, and the debugger remains attached.

Do not interpret equal values across two real threads as a failure by itself; the acceptance condition is a valid refresh for the selected thread, not forced artificial differences.

## Gate I — Live PS5 Current Instruction to Disassembler

1. With PS5 Paused and a valid instruction pointer displayed, record the address.
2. Click **Disassembler...** from the debugger stop context.
3. Confirm the existing Disassembler opens at that address and resolves/highlights the containing instruction as usual.
4. Confirm returning to Debugger does not detach or alter the paused session.

## Gate J — Live PS5 Continue Clears Stop Context

1. From a populated Paused register snapshot, click **Continue**.
2. Confirm debugger State becomes `Running` and a normal Resumed event is recorded.
3. Confirm register rows/current-instruction presentation are cleared while Running.
4. Pause again and confirm the selected thread receives a new current snapshot.
5. Repeat Pause/Continue several times and confirm no stale register data survives a Running transition.

## Gate K — Live PS5 Transport, Cleanup, and Reconnect Regression

1. While attached and Running, refresh the ordinary process list and make a safe Memory Viewer read.
2. Pause and confirm registers can still be refreshed afterward.
3. Detach/Reattach and confirm Threads plus paused register snapshots recover.
4. Close an attached Debugger window with **X**, reopen it, Attach/Pause, and confirm register access recovers without TCP 755 conflicts.
5. Disconnect the main PS5 session while Debugger is open and confirm the old window becomes stale/unusable and register data is cleared.
6. Reconnect, set `eboot.bin` Active Target, open a new Debugger, Attach/Pause, and confirm a fresh register snapshot loads.

## Gate L — Final PS5 Regression Smoke

Perform a short final check of previously verified functionality:

- process enumeration / Active Target;
- memory map;
- safe memory read and an optional already-known safe write/read-back;
- simple First Scan / Next Scan;
- Saved Addresses;
- Memory Viewer;
- Disassembler and syntax highlighting/navigation;
- theme switching;
- Debugger Threads enumeration and whole-target Pause/Continue;
- Disconnect/Reconnect.

The rev6 ps5debug-NG individual-thread Suspend blocker does not need to be repeatedly forced during rev7 verification. Its reproduction is already recorded under `docs/bug-reports/` and the Memory Engine client path remains unchanged.

## Acceptance Criteria

`0.1.7.rev7` is verified only when:

- Windows build succeeds and **107/107** automated checks pass;
- Mock register snapshot, thread switching, writable edit/read-back, lifecycle clearing, and current-instruction Disassembler integration pass;
- live PS5 paused-thread general-register snapshots load correctly and remain read-only;
- PS5 thread switching/Refresh and current-instruction Disassembler navigation pass;
- Continue/Detach/stale-target transitions clear stop-context data;
- transport/cleanup/reconnect regression passes;
- final Mock/PS5 regression smoke passes;
- no PS5 register write is claimed or required in rev7.
