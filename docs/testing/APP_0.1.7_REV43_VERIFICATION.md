# Application 0.1.7.rev43 Verification — Comparer State and Export UI Consistency

## Package identity

| Item | Expected |
| --- | --- |
| Application | `0.1.7.rev43` |
| Feature | `Comparer State and Export UI Consistency` |
| Plugin API | `2.18.0` |
| Mock | `1.0.1.rev17` |
| PS5 | `0.1.2.rev39` |
| Automated registry | `152` checks |
| Snapshot schema | `teekay87-memory-engine-debugger-snapshot`, version `1` |

## Baseline

Rev43 is built directly from the user-supplied rev42 package. Rev42 passed the complete Windows automated suite at **152/152** and completed the focused runtime verification. Snapshot capture/import, grouping, pair/group comparison semantics, Universal Export data, modeless same-session persistence, offline behavior, and the application icon were accepted. The remaining rev42 findings were limited to Call Stack Comparer action-state/stale-result behavior, destructive confirmation consistency, explicit ungrouping, the shared Universal Export zero-column workflow, and requested Main Window tool-button styling.

## Gate 1 — Clean build and automated suite

Build from a clean extraction on Windows and require **152/152 PASS**. Do not continue runtime verification if the build or registry fails.

## Gate 2 — Main Window primary tool buttons

1. Launch the application in the current theme.
2. Confirm **Disassembler...** and **Debugger...** use the same theme-driven Primary visual role as the active **First Scan** action.
3. Switch through Light, Dimmed, and Dark and confirm both buttons follow each theme.
4. Confirm placement, visibility, capability gating, and tool-opening behavior are unchanged.

## Gate 3 — Call Stack Comparer empty-state and exact selection gating

Open Debugger and Call Stack Comparer.

1. With no snapshots, **Remove All**, **Compare 2**, snapshot **Export...**, **Compare Groups**, and **Export Results...** must be disabled as applicable; **Import...** remains available.
2. Add one snapshot: snapshot **Export...** must enable, **Compare 2** remains disabled, **Remove All** enables.
3. Select exactly two snapshots: **Compare 2** enables and snapshot **Export...** disables.
4. Select three or more snapshots: **Compare 2** remains disabled.
5. Deselect back to exactly one snapshot and verify snapshot **Export...** re-enables.

## Gate 4 — Group assignment, No group, and Compare Groups gating

1. Create two session groups through the existing new-group text input, for example `Player` and `Enemy`.
2. Verify every row Group popup shows **No group** at the top, the new-group input, then existing groups.
3. Assign snapshots so both groups contain at least one snapshot.
4. Verify **Compare Groups** enables only when Group A and Group B are both selected, different, and both non-empty.
5. Select the same group on both sides: **Compare Groups** disables.
6. Empty one selected group by choosing **No group** on its only member: **Compare Groups** disables immediately.
7. Confirm the emptied group name remains present in Group A/Group B and row dropdown catalogs.
8. Reassign a snapshot to that existing group and confirm the action can become valid again without recreating the group name.

## Gate 5 — Destructive confirmation behavior

1. Use a row **Remove** action.
2. Confirm the application-owned themed Danger dialog appears, with **Remove** and **Cancel**, and destructive confirmation is not the Enter-key default.
3. Cancel and verify the snapshot remains.
4. Repeat and confirm; verify only that row is removed.
5. Use **Remove All** with at least two snapshots.
6. Confirm the same application-owned dialog pattern appears with **Remove All** and **Cancel**.
7. Cancel once, then confirm once; verify all snapshots are removed and **Remove All** becomes disabled.
8. Confirm the removed panel-level multi-select **Remove** button is no longer present.

## Gate 6 — Pairwise stale-result invalidation

1. Capture/import at least three snapshots.
2. Select exactly two and run **Compare 2**.
3. Verify **Export Results...** enables.
4. Edit Label and Notes on either compared snapshot; the pairwise result must remain.
5. Change only Group metadata on either compared snapshot; the pairwise result must remain.
6. Remove an unrelated third snapshot; the pairwise result must remain.
7. Remove either one of the two compared snapshots; the pairwise result must clear immediately and **Export Results...** must disable.

## Gate 7 — Group-comparison stale-result invalidation

1. Build two non-empty groups and run a valid **Compare Groups**.
2. Verify **Export Results...** enables.
3. Change only Label or Notes on a member; the group result must remain.
4. Move a snapshot into or out of either compared group; the result must clear immediately and **Export Results...** must disable.
5. Re-run the group comparison, then choose **No group** on an involved member; the result must clear.
6. Re-run again, then remove an involved member; the result must clear.
7. Re-run again, then change Group A or Group B to a different registered group; the result must clear.
8. Re-run a valid comparison and close/reopen only the comparer without changing any input; the still-valid result must remain.
9. **Remove All** must continue to clear snapshots and results while retaining the session group-name catalog.

## Gate 8 — Shared Universal Export zero-column workflow

Open the shared export dialog from any consumer and repeat a spot-check in at least one second consumer.

1. Confirm **Select None** appears immediately before **Select All**.
2. Click **Select None**. Every column clears, **Continue** disables immediately, and `Select at least one column.` is shown without requiring a Continue click.
3. Select one column manually. **Continue** enables immediately and the validation message clears immediately.
4. Click **Select None** again, then **Select All**. All columns select and **Continue** enables.
5. Export one selected column and confirm only that column is written.
6. Scope and Format must continue to display user-facing names, not record diagnostic text.

## Gate 9 — Existing empty-data export regression

1. Main Window Scan Results with zero results: **Export...** disabled.
2. Saved Addresses with zero rows: **Export** and **Remove All** disabled.
3. Add one Saved Address: both enable as appropriate.
4. Disassembler with no displayed instructions: **Export** disabled; after a successful disassembly it enables.

## Acceptance

Rev43 is accepted when the clean Windows build and **152/152** pass and all focused runtime gates above pass. No change is expected in Plugin API, Mock/PS5 plugin versions, snapshot schema, debugger capture, comparison algorithms, transport behavior, or Universal Export data schemas.
