# TeeKay87's Memory Engine 0.1.3.rev21 Verification

Application: `0.1.3.rev21 - Queued Saved Address Removal`  
Plugin API: `2.4.0`  
PS5 plugin: `0.1.0.rev16`  
Mock plugin: `1.0.0.rev3`

## Purpose

Verify that individual Saved Address removal is no longer rejected merely because Saved Address I/O is active, while preserving the rev18-rev20 refresh/Frozen/direct-write coordination and the protocol-safety rule that an already-started target transaction is allowed to finish normally.

Rev21 changes only host/WPF Saved Address coordination and documentation. Core scanner/storage code, Plugin SDK, PS5 plugin, Mock plugin, native TurboScan behavior, and the existing `IConcurrentMemoryWriter` implementation are unchanged.

## 1. Idle Removal

1. Connect to a target and save at least two addresses.
2. Use the row **Remove** button on an unfrozen row while no Saved Address operation is visibly pending.
3. Repeat with **Remove address** from the row context menu.

Expected: the selected row is removed immediately, the count updates once, selection moves to a remaining row when necessary, and no confirmation dialog is shown for an individual row.

## 2. Removal During Value Refresh

1. Configure a short Value refresh interval, for example `50-100 ms`.
2. Save several addresses so one refresh cycle has multiple rows to traverse.
3. Repeatedly remove rows while refresh traffic is active.
4. Exercise both the row button and the context-menu action.

Expected:

- no `Cannot remove <address> while its update cycle is active.` rejection appears;
- if refresh is active, the row reports that removal is queued;
- the already-started read may finish, but its result is not published back into a row already pending removal;
- the refresh loop stops after its currently awaited row instead of continuing through the entire snapshot;
- the queued row disappears automatically as soon as Saved Address I/O returns to idle;
- the user does not need to click Remove a second time.

## 3. Removal During Frozen Enforcement

1. Freeze one or more Saved Addresses.
2. Use a short Frozen write interval such as `50-100 ms`.
3. Remove a Frozen row while repeated writes are active.

Expected:

- the row's Frozen state is cleared immediately when removal is requested;
- no new Frozen timer write is started for that row after the request;
- a write already in flight is allowed to complete normally;
- the active Frozen cycle stops after its current awaited operation;
- the row is removed automatically when Saved Address I/O becomes idle.

## 4. Removal During Freeze Enablement

1. Start with an unfrozen Saved Address.
2. Enable Frozen and, while its initial live refresh/immediate write sequence is active, request removal of the row.
3. Repeat enough times to exercise both the initial-read and immediate-write timing boundaries.

Expected: removal remains authoritative. The freeze-enable operation must not re-enable Frozen after the remove request. Once any already-started target operation completes, the row is removed automatically.

## 5. Removal During Direct Value Write

1. Edit a Saved Address Value and commit it.
2. While the explicit user write is active, request removal of the same row or another Saved Address row.
3. Repeat on PS5 where direct writes use the concurrent writer, and on Mock or another path without `IConcurrentMemoryWriter` where practical.

Expected: the earlier explicit Value write is allowed to finish in its established ordering, no new timer work starts while removal is pending, and the queued removal runs automatically after the user-write state and any overlapping background Saved Address I/O are both idle.

## 6. Multiple Queued Rows

1. Save several rows.
2. During one active Saved Address I/O period, request removal of two or more different rows.
3. Click Remove more than once on at least one of those rows if the timing window allows it.

Expected: each distinct row is removed once. Duplicate requests for the same row do not produce duplicate collection mutations or count errors. If the selected row is among the queued rows, selection resolves to a remaining row after the batch is removed.

## 7. Remove All Regression

1. Confirm **Remove All** still asks for confirmation.
2. Confirm its existing busy-state behavior is unchanged in rev21.
3. Once idle, confirm accepting the dialog clears every row and disables all Frozen states.

Expected: rev21 changes only individual row removal.

## 8. Rev20 Coordination Regression

Confirm the previously verified rev20 behavior remains intact:

- First/Next Scan buttons recover automatically after overlapping background Saved Address I/O;
- periodic refresh/Frozen ticks do not make unrelated controls visibly blink disabled/enabled;
- Value editor text is not overwritten while it owns keyboard focus;
- direct Value writes are not silently lost to a refresh/Frozen tick;
- editing a Frozen Value makes the edited bytes the new Frozen payload;
- stale refresh completions cannot overwrite a later successful direct/Frozen write.

## 9. Scanner and Target Regression

Confirm no regression in:

- First Scan / Next Scan / New Scan;
- complete disk-backed result retention and 50,000-row UI preview;
- Frozen writes during scanning when Pause is Off;
- Frozen suppression while the target is intentionally paused;
- target/process identity safety;
- Saved Address interval persistence;
- Disconnect/reconnect behavior.

## Static Preparation

Per project workflow, the preparation environment does not attempt a .NET build/runtime run. Before packaging, review the changed Saved Address paths for obsolete/redundant synchronization logic, validate all changed C# structure and existing XAML event/command references, verify version/documentation consistency, confirm Plugin SDK and platform plugin source remain unchanged, and leave actual .NET build/runtime verification to the Windows development environment.
