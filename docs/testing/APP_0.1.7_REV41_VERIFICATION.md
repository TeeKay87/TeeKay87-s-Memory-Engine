# Application 0.1.7.rev41 Verification — Call Stack Comparer Snapshot Group Assignment

## Package identity

| Item | Expected |
| --- | --- |
| Application | `0.1.7.rev41` |
| Feature | `Call Stack Comparer Snapshot Group Assignment` |
| Plugin API | `2.18.0` |
| Mock | `1.0.1.rev17` |
| PS5 | `0.1.2.rev39` |
| Automated registry | `152` checks |
| Snapshot schema | `teekay87-memory-engine-debugger-snapshot`, version `1` |

## Baseline

Rev41 is built directly from rev40. Rev40 already passed the clean Windows automated suite at **152/152**. Its shared Export-dialog presentation fix remains in rev41, but the cross-consumer runtime/UI check was not completed before rev41 was requested.

Snapshot/comparer behavior already verified before rev41 includes explicit-only capture, same-session comparer close/reopen persistence, Capture Current re-enabling on a new paused stop, immutable historical captures, pairwise comparison, JSON snapshot export/import round-trip, offline comparison after Debugger close, new-session isolation, import safety while Detached, and invalid schema/version rejection.

## Gate 1 — Clean build and automated suite

Build from a clean extraction on Windows and require **152/152 PASS**.

Do not continue runtime verification if the build or automated registry fails.

## Gate 2 — Snapshot row presentation

Open a Debugger, reach a valid Paused stop, open **Call Stack Comparer**, and capture at least one snapshot.

Verify the snapshot row uses the same always-visible control style as Saved Addresses where relevant:

1. **Label** is an always-visible in-row text editor.
2. **Group** is an always-visible dropdown/editor.
3. **Notes** is an always-visible in-row text editor.
4. Captured, Source, Event, Trigger, Stop IP, and Process remain read-only.
5. A Danger-styled **Remove** button is present on the row and matches the Saved Addresses row action sizing/presentation.
6. With no snapshots, the table shows **No snapshots.**.
7. Existing multi-select **Remove** and **Remove All** actions remain functional.

## Gate 3 — Create and reuse session groups

Use at least three snapshots.

1. In snapshot 1, type a new group name such as `Player` into the row Group editor and commit it with Enter or by leaving the control.
2. Open snapshot 2's Group dropdown. `Player` must now be available as an existing choice.
3. Select `Player` for snapshot 2.
4. Create a second group such as `Enemy` from snapshot 3.
5. Open other snapshot Group dropdowns and confirm both names are available.
6. Reassign one snapshot from one group to another and confirm only that snapshot's membership changes.
7. Clear a snapshot's Group value and confirm that snapshot is no longer included in that group.
8. Create/reuse a name with different casing, for example `player` after `Player`. The session must not create a second logical group with different casing.

Expected: group names belong to the current comparer session and are reusable across all snapshot rows.

## Gate 4 — Group A / Group B restrictions

Verify the comparison selectors below the snapshot list:

1. **Group A** is a dropdown, not a free-form text box.
2. **Group B** is a dropdown, not a free-form text box.
3. Both contain the group names created through snapshot rows.
4. Neither selector allows typing or creating a new group name.
5. Select `Player` for Group A and `Enemy` for Group B.
6. Run **Compare Groups**.

Expected: the existing grouped comparison engine receives every snapshot currently assigned to the selected group names.

## Gate 5 — Multiple snapshots in one group

Assign at least two snapshots to Group A and at least two snapshots to Group B.

Run **Compare Groups** and verify:

- all snapshots sharing the same row Group value participate in that group;
- stable/variable classification still behaves as before;
- call-stack, raw-register, trigger/current-IP, disassembly, breakpoint/watchpoint, and memory evidence remain part of the comparison result;
- no target traffic is caused by comparison itself.

This gate verifies the new UI/group-membership workflow without changing Core comparison semantics.

## Gate 6 — Imported group metadata

Export a snapshot that has a non-empty Group value, then import it into the same comparer session or a clean comparer session.

Verify:

1. The imported row retains its Group metadata.
2. That group name appears in the session Group dropdown choices.
3. The name becomes available in Group A / Group B.
4. Import does not attach, reconnect, restore debugger state, or perform target writes.

## Gate 7 — Same-session persistence

With snapshots and at least two created group names:

1. Close only Call Stack Comparer.
2. Reopen it from the same Debugger window/session.
3. Confirm snapshots, row Group assignments, and the session group-name dropdown choices remain available.
4. Confirm no automatic snapshot is added.

Then close the Debugger while leaving the comparer open and confirm the existing offline snapshots/groups remain inspectable while **Capture Current** becomes unavailable.

## Gate 8 — Carried rev40 shared Export-dialog presentation

The rev40 automated gate already passed, but its focused runtime/UI check remains required. Open export from each available consumer: Scan Results, Saved Addresses, Disassembler, Debugger, and Call Stack Comparer **Export Results**.

For every dialog:

1. Scope shows only the user-facing scope name; no `ExportScopeOption { ... }` text is visible.
2. Open Scope and verify every item uses its user-facing name.
3. Format shows `JSON`, `CSV`, `TSV`, or `Markdown table`; no `ExportFormatOption { ... }` text is visible.
4. Open Format and verify all four labels.
5. Change scope and confirm description/columns still refresh correctly.

## Gate 9 — Resume Debugger Universal Export

Continue the interrupted debugger export verification after Gate 8 passes. Export Threads, Registers, Breakpoints / Watchpoints, Call Stack, and Events. Exercise JSON plus at least one tabular format.

Events must keep these concepts separate:

- Stop/current IP;
- Trigger instruction;
- watched address;
- access;
- size;
- trigger resolution.

## Acceptance

Rev41 is accepted when:

- the clean Windows build and **152/152** automated suite pass;
- snapshot rows visually/behaviorally match the Saved Addresses in-row control pattern where relevant;
- session group creation, reuse, reassignment, imported metadata registration, and multi-snapshot membership work as specified;
- Group A/B are restricted non-editable selectors of registered session groups;
- same-session comparer persistence remains intact;
- the carried rev40 cross-consumer Export-dialog presentation gate passes;
- no previously verified snapshot/import/offline behavior regresses.
