# Application 0.1.7.rev43 Source Review — Comparer State and Export UI Consistency

## Baseline review

Rev43 was built directly from the user-supplied rev42 package. Before changing code, the root `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete source/test/project text surface were read. The implementation reuses the existing comparer workspace, immutable snapshot metadata path, `DebuggerSnapshotComparer`, shared confirmation dialog, shared Universal Export dialog, theme button resources, and existing empty-data export gates rather than introducing parallel mechanisms.

## Call Stack Comparer state model

The comparer now records whether its current result came from a pairwise or group comparison and retains only the minimal source identity required to decide whether that result is still valid. Pairwise state tracks the two comparer-row view-model instances, so exported/reimported copies that intentionally preserve the same snapshot ID remain distinct rows for stale-result invalidation. Group state tracks the two compared group names. This bookkeeping is presentation/workspace state only and does not alter `DebuggerSnapshot`, snapshot JSON schema, or Core comparison algorithms.

Snapshot Group changes report old/new membership back to the comparer workspace. A group result is invalidated only when membership crosses either compared group boundary. Pairwise results deliberately ignore Group metadata changes because Group is not a pairwise comparison input. Snapshot removal checks the active result type before removal: removing one of the two pairwise source snapshots or a member of either compared group invalidates the current result; unrelated rows do not. Changing Group A/Group B away from the pair that produced a group result also invalidates that result. Closing/reopening the modeless comparer leaves a still-valid retained result untouched.

## Action-state gating

The comparer view model exposes explicit state for:

- `CanComparePair` — exactly two selected snapshots;
- `CanExportSnapshot` — exactly one selected snapshot;
- `CanCompareGroups` — two distinct selected non-empty groups;
- `HasSnapshots` — Remove All availability;
- `HasResults` — Export Results availability.

The window updates only the selected-snapshot count from the DataGrid selection event; the rest is computed from retained workspace state. Existing click handlers keep defensive validation for safety.

## Group UI and destructive actions

The custom Group popup now starts with a **No group** action, followed by the existing new-group text input and ordinary registered group choices. Clearing membership writes the existing empty Group metadata value through the same immutable `WithMetadata` path and does not delete the group name from `GroupNames`.

The redundant panel-level Remove action is removed. Per-row Remove and Remove All both use the existing `ConfirmationDialogService` with `ConfirmationDialogTone.Danger` and `confirmIsDefault: false`, matching the established Saved Addresses destructive-dialog behavior.

## Shared Universal Export dialog

`DataExportDialog` now treats each column choice as an `INotifyPropertyChanged` item. The shared dialog therefore updates Continue and validation state immediately for checkbox changes and for bulk Select None/Select All operations. `ContinueButton.IsEnabled` is true only when at least one column is selected. The zero-column state displays the existing validation text immediately; restoring any selection clears it.

No Core export interfaces, schemas, writers, row sources, destination behavior, progress/cancellation, or transactional publication logic changed.

## Main Window and retained empty-state behavior

Disassembler and Debugger tool-entry buttons now reference `PrimaryButtonStyle` directly. That style already resolves its palette through theme resources, so no theme JSON or hard-coded color changes were required.

The requested empty-state checks for Scan Results Export, Saved Addresses Export/Remove All, and Disassembler Export were already present in rev42 (`HasVisibleScanResults`, `HasSavedAddresses`, and `Instructions.Count > 0`). Rev43 leaves the production paths unchanged and strengthens source-contract coverage so those gates cannot regress unnoticed.

## Regression boundary

Rev43 does not change Plugin API, Mock or PS5 plugin source/version, debugger transport, breakpoint/watchpoint behavior, debugger capture composition, snapshot schema/version, Core comparison logic, export schemas, or theme palette values. The existing automated registry remains **152 checks**; source-contract coverage is strengthened in place and adds only the shared export-dialog code-behind as a fixture.
