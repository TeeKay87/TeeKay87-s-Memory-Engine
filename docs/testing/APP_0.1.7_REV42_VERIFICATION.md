# Application 0.1.7.rev42 Verification — Call Stack Group Creation and Application Icon

## Package identity

| Item | Expected |
| --- | --- |
| Application | `0.1.7.rev42` |
| Feature | `Call Stack Group Creation and Application Icon` |
| Plugin API | `2.18.0` |
| Mock | `1.0.1.rev17` |
| PS5 | `0.1.2.rev39` |
| Automated registry | `152` checks |
| Snapshot schema | `teekay87-memory-engine-debugger-snapshot`, version `1` |

## Baseline

Rev42 is built directly from the user-supplied rev41 package. Rev41 passed **152/152**. Its first runtime group UI check failed because the Group ComboBox did not visibly expose how a new group should be created. Rev42 keeps the same session-local group model and replaces only that creation interaction. The supplied TK artwork is also installed as the application icon.

## Gate 1 — Clean build and automated suite

Build from a clean extraction on Windows and require **152/152 PASS**. Do not continue runtime verification if the build or registry fails.

## Gate 2 — Application icon

Launch the application and verify:

1. MainWindow shows the supplied TK icon in the native title bar/taskbar representation.
2. Open at least one modeless tool window such as Debugger or Call Stack Comparer and verify the same icon.
3. Open at least one application-owned dialog and verify the same icon.
4. No theme, title text, layout, or control sizing changed because of the icon integration.

## Gate 3 — Snapshot Group dropdown creation surface

Open Debugger, reach a valid Paused stop, open Call Stack Comparer, and capture one snapshot.

1. Open that snapshot's **Group** dropdown.
2. The first popup row must be a real text input, clearly separate from the existing-group choices.
3. With no groups created yet, the text input must still be present even though the normal choice list is empty.
4. Type `Player` and press Enter.
5. The dropdown closes and the snapshot row now shows `Player` as its assigned Group.
6. Reopen the same dropdown. The creation input remains first and `Player` appears below it as an existing choice.

## Gate 4 — Reuse and membership

Capture/import at least two more snapshots.

1. Open snapshot 2 Group and select existing `Player`; do not type it again.
2. Open snapshot 3 Group, create `Enemy` through the first-row input, and press Enter.
3. All snapshot Group dropdowns must now list both `Player` and `Enemy`.
4. Assign at least two snapshots to `Player`. Those rows must form one logical comparison group.
5. Reassign one row to `Enemy`; only that snapshot's membership changes.
6. Attempt to create `player` after `Player`. The session must reuse the existing case-insensitive name instead of creating a duplicate logical group.

## Gate 5 — Group A / Group B restrictions

1. Group A and Group B remain normal non-editable dropdowns.
2. They contain the registered names `Player` and `Enemy`.
3. They contain no new-group text input and do not accept arbitrary typing.
4. Select `Player` and `Enemy`, then run **Compare Groups**.
5. The existing comparison engine receives every snapshot currently assigned to each selected name.

## Gate 6 — Persistence and import

1. Close/reopen only the comparer within the same Debugger session; snapshots, memberships, and group names must persist.
2. Export a snapshot with Group metadata and import it. The imported group must register into the same session catalog.
3. Import remains offline-safe and must not attach, reconnect, restore breakpoints/watchpoints, or write target memory.

## Gate 7 — Carried rev40 shared Export-dialog presentation

Open export from Scan Results, Saved Addresses, Disassembler, Debugger, and Call Stack Comparer **Export Results**. Scope and Format must show only user-facing labels (`Threads`, `JSON`, and so on), never record diagnostic representations. Change scope and confirm description/columns still refresh.

## Gate 8 — Resume Debugger Universal Export

Continue the interrupted debugger export verification for Threads, Registers, Breakpoints/Watchpoints, Call Stack, and Events. Exercise JSON plus at least one tabular format. Event export must keep Stop/current IP, Trigger instruction, watched address, access, size, and trigger resolution separate.

## Acceptance

Rev42 is accepted when the clean Windows build and **152/152** pass, the application icon is consistent, the first-row group creation input is discoverable and functional, existing-group assignment/multi-snapshot membership works, Group A/B remain restricted selectors, same-session/import behavior remains safe, and the carried export gates pass without regressions.
