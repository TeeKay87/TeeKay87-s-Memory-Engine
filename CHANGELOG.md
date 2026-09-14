# Changelog

## TeeKay87's Memory Engine 0.1.7.rev48 - Comparer No Group Selection Reset Fix

### Status

Rev48 is built directly from the user-supplied `0.1.7.rev47` package after focused runtime verification proved that rev47's binding-target refresh did not clear the visible Group value. The underlying snapshot membership was already correct and persisted as ungrouped after reopening the comparer; only the active ComboBox selection box remained stale.

### Fixed — Call Stack Comparer No group live selection

- Removed the ineffective rev47 `SelectedItem` binding-target refresh from the `No group` action.
- After clearing the snapshot model membership, the row ComboBox now clears its live selection with `SelectedIndex = -1`.
- This is the correct WPF state transition because the empty/ungrouped value is deliberately not an item in `AvailableGroups`; attempting to push an empty string through `SelectedItem` cannot select a matching item and allowed the previous selection box content to remain visible.
- `SelectedIndex` is not data-bound in this control, so clearing it preserves the existing two-way `SelectedItem` binding for future normal group assignments.
- Existing model behavior remains unchanged: the snapshot is stored with an empty group, the old group name remains registered in the session catalog, affected Group Comparison results are invalidated through the existing group-change callback, and reopening the comparer continues to show the persisted ungrouped state.

### Verification coverage

- Replaced the rev47 source contract with a requirement for explicit live selection clearing through `comboBox.SelectedIndex = -1`.
- Added a regression assertion that rejects the ineffective rev47 `UpdateTarget()` workaround.
- Automated registry remains **152 checks**; no existing verification case was removed or bypassed.
- A clean Windows build and the focused live `No group` UI check remain the authoritative gates for this revision.

### Version and documentation

- Advanced centralized application metadata to `0.1.7.rev48` with feature title `Comparer No Group Selection Reset Fix`.
- Updated README, development plan, source review, and verification notes to reflect the corrected WPF selection-state behavior.
- Plugin API remains `2.18.0`; Mock remains `1.0.1.rev17`; PS5 remains `0.1.2.rev39`; snapshot schema remains version 1.

## TeeKay87's Memory Engine 0.1.7.rev47 - Comparer No Group Live UI Synchronization

### Status

Rev47 is built directly from the user-supplied `0.1.7.rev46` package after the focused rev46 runtime pass completed with 152/152 automated checks and four of five focused runtime checks fully passing. The remaining issue was limited to the Call Stack Comparer row display after selecting `No group`: the snapshot model was cleared correctly, but the ComboBox continued to display the previous group until the window was reopened.

### Fixed — Call Stack Comparer No group live display

- Kept the verified rev46 ungroup model behavior unchanged.
- After the `No group` action clears the snapshot membership, the row Group ComboBox now explicitly refreshes its `SelectedItem` binding target before the dropdown closes.
- The row therefore reflects the empty group immediately instead of retaining the previous selection visually until the comparer is reopened.
- Group catalog retention, comparison invalidation, status reporting, snapshot persistence, and group membership semantics are unchanged.

### Verification coverage

- Extended the existing Call Stack Comparer source contract to require the immediate Group ComboBox binding refresh.
- Automated registry remains **152 checks**.
- Rev46's Windows automated gate passed **152/152** before this focused UI correction.

### Version and documentation

- Advanced centralized application metadata to `0.1.7.rev47` with feature title `Comparer No Group Live UI Synchronization`.
- Updated README, development plan, source review, and verification notes for the focused UI synchronization correction.
- Plugin API remains `2.18.0`; Mock remains `1.0.1.rev17`; PS5 remains `0.1.2.rev39`; snapshot schema remains version 1.

## TeeKay87's Memory Engine 0.1.7.rev46 - Rev45 Verification Compile Fix

### Status

Rev46 is built directly from the user-supplied `0.1.7.rev45` package after the first Windows build attempt exposed a `CS0103` error in the automated verification project. The rev45 application changes are retained unchanged.

### Fixed — automated verification compile failure

- Corrected `VerifyCallStackComparerWorkspaceSourceAsync` to use its existing `comparerViewModel` fixture-source local when validating the rev45 `No group` setter contract.
- Removed the invalid reference to the undeclared `comparerViewModelSource` identifier that caused `CS0103` at `Program.cs` line 8916.
- No application UI, comparer runtime behavior, debugger export gating, Saved Addresses removal behavior, Plugin API contract, plugin implementation, snapshot schema, or transport behavior was changed.

### Version and documentation

- Advanced centralized application metadata to `0.1.7.rev46` with feature title `Rev45 Verification Compile Fix`.
- Updated README, development plan, source review, and verification notes for the compile-only correction.
- Plugin API remains `2.18.0`; Mock remains `1.0.1.rev17`; PS5 remains `0.1.2.rev39`; snapshot schema remains version 1; automated registry remains **152 checks**.

## TeeKay87's Memory Engine 0.1.7.rev45 - Runtime State and Removal Confirmation Fixes

### Status

Rev45 is built directly from the user-supplied `0.1.7.rev44` package after focused runtime verification identified two remaining state defects and one Saved Addresses removal-safety improvement. The rev43/rev44 functional changes remain intact.

### Fixed — Debugger export state

- Added `CanExportDebuggerData` to the Debugger view model and bound the Debugger **Export...** action to it.
- Debugger Export is disabled before a successful Attach, enabled only while the debugger is in an attached session state, and disabled again during/after detach or fault cleanup.
- The state follows the existing debugger session state machine rather than a window-local flag.

### Fixed — Call Stack Comparer No group

- Corrected the snapshot Group setter so an empty group value bypasses group-name registration and is stored as an actual empty membership.
- **No group** now clears the row's group immediately while retaining the registered group name in the session catalog.
- Existing group-membership notifications continue to update Compare Groups gating and invalidate an affected Group Comparison.
- The existing success status is now reached only after the model can perform the real clear operation.

### Added — Saved Addresses single-remove confirmation

- Single-row **Remove** now uses the application confirmation dialog when the Saved Address has a non-empty Description.
- The dialog identifies the address by its trimmed Description and uses the existing Danger confirmation semantics.
- Addresses with an empty or whitespace-only Description are removed immediately without an extra confirmation.
- The same behavior is used by both the row Remove button and the context-menu **Remove address** action.
- Existing **Remove All** confirmation behavior is unchanged.

### Version and documentation

- Advanced centralized application metadata to `0.1.7.rev45` with feature title `Runtime State and Removal Confirmation Fixes`.
- Updated README, development plan, source review, and verification notes for the focused rev45 changes.
- Plugin API remains `2.18.0`; Mock remains `1.0.1.rev17`; PS5 remains `0.1.2.rev39`; snapshot schema remains version 1; automated registry remains **152 checks**.

## TeeKay87's Memory Engine 0.1.7.rev44 - Rev43 Verification Compile Fix

### Status

Rev44 is built directly from the user-supplied `0.1.7.rev43` package after the first Windows build attempt exposed two `CS0103` errors in the automated verification project. The rev43 application changes are retained unchanged. This revision only repairs the test-source scope error and advances application revision metadata/documentation so the corrected package can be built and verified cleanly.

### Fixed — automated verification compile failure

- Corrected `VerifyDisassemblerSelectionCopyExportSourceAsync` so its `PluginViewModel.cs` and `MainWindow.xaml` fixture paths are declared inside the method that reads them.
- Removed the misplaced declarations from `VerifyDisassemblerFollowTargetSourceAsync`, where they were outside the consuming method's scope.
- Added fixture-existence assertions for all five files consumed by the combined Disassembler/export UI source-contract check, so a missing copied fixture reports a focused verification failure instead of failing later during `File.ReadAllText`.
- The two reported `CS0103` errors for `pluginViewModelSourcePath` and `mainWindowXamlPath` are therefore resolved at source level.
- No application UI, comparer behavior, export behavior, debugger logic, Plugin API contract, plugin implementation, snapshot schema, or transport behavior was changed.

### Version and documentation

- Advanced centralized application metadata from `0.1.7.rev43` to `0.1.7.rev44` and set the feature title to `Rev43 Verification Compile Fix`.
- Updated the README and full development action plan to identify rev44 as the current build while retaining rev43 as the functional change set under verification.
- Added the rev44 source review and verification notes under `docs/testing/`.
- Plugin API remains `2.18.0`; Mock remains `1.0.1.rev17`; PS5 remains `0.1.2.rev39`; snapshot schema remains version 1; the automated registry remains **152 checks**.

## TeeKay87's Memory Engine 0.1.7.rev43 - Comparer State and Export UI Consistency

### Status

Rev43 is built directly from the user-supplied `0.1.7.rev42` package after the rev42 Windows automated suite passed **152/152** and the focused runtime/UI verification completed. Rev42's grouping, snapshot persistence/import, Universal Export data semantics, application icon, and comparison engine all worked, but runtime testing identified several state-gating and stale-result issues in Call Stack Comparer plus one shared Universal Export column-selection UX issue. Rev43 corrects those focused defects without changing debugger capture, comparison algorithms, snapshot schema, Plugin API, platform plugins, or target transport.

### Fixed — Call Stack Comparer action state

- Removed the redundant panel-level **Remove** button. Snapshot removal now has one clear per-row action plus **Remove All**.
- Added themed confirmation dialogs for per-row snapshot **Remove** and comparer **Remove All**, reusing the existing application-owned `ConfirmationDialogService` and Danger semantic role. Destructive confirmation is not the Enter-key default.
- **Remove All** is disabled while the snapshot list is empty.
- Added an explicit **No group** action at the top of every snapshot Group dropdown. It removes that snapshot's membership while leaving the session-local group name registered for later reuse and Group A/Group B selection.
- **Compare 2** is enabled only when exactly two snapshots are selected.
- Complete-snapshot **Export...** is enabled only when exactly one snapshot is selected.
- **Compare Groups** is enabled only when Group A and Group B are both selected, are different names, and both currently contain at least one snapshot.
- **Export Results...** is enabled only while a current comparison result exists.
- Existing click-time validation remains as a defensive fallback rather than being the primary UX state mechanism.

### Fixed — stale comparison results

- Added explicit comparison-input tracking so retained results can distinguish pairwise and grouped comparisons.
- Pairwise results are now invalidated immediately if either of the two compared snapshots is removed.
- Pairwise results intentionally remain valid when only Label, Notes, or Group metadata changes because those fields are not pairwise comparison inputs.
- Group Comparison results are now invalidated when a snapshot moves into or out of either compared group, when an involved snapshot is ungrouped, or when an involved snapshot is removed.
- Group Comparison results are also invalidated when Group A or Group B selection changes away from the pair that produced the current result.
- **Remove All** continues to clear snapshots and results while preserving the comparer-session group-name catalog.
- Closing and reopening Call Stack Comparer in the same Debugger session continues to preserve a result when its inputs remain valid.
- Result invalidation automatically disables **Export Results...** through the shared `HasResults` state.

### Fixed — shared Universal Export column selection

- Added **Select None** next to the existing **Select All** action in the shared `DataExportDialog`.
- Column choices now publish selection changes through `INotifyPropertyChanged` so dialog state updates immediately.
- **Continue** is disabled as soon as zero columns are selected and re-enabled as soon as at least one column is selected.
- The validation text is synchronized live: zero selected columns shows `Select at least one column.`, while selecting any column clears the message immediately.
- Scope/Format `DisplayName` rendering from rev40, destination selection, schemas, data sources, writers, cancellation, and transactional publication are unchanged.

### Changed — Main Window tool action styling

- Changed **Disassembler...** and **Debugger...** on the permanent second target row from `SecondaryButtonStyle` to the theme-driven `PrimaryButtonStyle`.
- The two tool-entry actions now use the same primary visual role seen on the active **First Scan** action and automatically follow Light, Dimmed, and Dark themes.
- Their placement, visibility/capability gating, enabled state, click behavior, and target/session safety are unchanged.

### Verified existing empty-state gating retained

- Scan Results **Export...** already requires `HasVisibleScanResults`; rev43 preserves that behavior and adds source-contract coverage.
- Saved Addresses **Export** already requires `HasSavedAddresses`; rev43 preserves that behavior and adds source-contract coverage.
- Saved Addresses **Remove All** already binds to `HasSavedAddresses`; rev43 preserves that behavior and adds source-contract coverage.
- Disassembler **Export** already requires at least one displayed instruction; rev43 preserves that behavior and adds source-contract coverage.

### Verification coverage

- Strengthened the existing Call Stack Comparer source-contract check in place for No group, exact-selection button gating, result gating, confirmation-dialog reuse, stale-result tracking, and removal of the redundant panel Remove action.
- Strengthened the existing Universal Export/Disassembler source-contract check in place for **Select None**, live Continue/validation gating, and empty-data Export gating across Scan Results, Saved Addresses, and Disassembler.
- Strengthened the existing Main Workspace layout check in place so **Disassembler...** and **Debugger...** must use `PrimaryButtonStyle`.
- Added `DataExportDialog.xaml.cs` as a test fixture so the shared live column-selection state is covered without adding a new registry entry.
- Automated registry remains **152 checks**.

### Versioning

- Advanced centralized application metadata from `0.1.7.rev42` to **`0.1.7.rev43`** with feature title `Comparer State and Export UI Consistency`.
- Plugin API remains **`2.18.0`**.
- PS5 remains **`0.1.2.rev39`** because no PS5 plugin source changed.
- Mock remains **`1.0.1.rev17`** because no Mock plugin source changed.
- Snapshot schema remains `teekay87-memory-engine-debugger-snapshot` version `1`.

### Verification requirement

Rev43 must pass a clean Windows build and **152/152**. Focused runtime verification must confirm comparer confirmation dialogs, No group behavior, exact action-state gating, pair/group stale-result invalidation, retained valid-result reopen behavior, shared Universal Export Select None/live Continue state, and theme-aware Primary styling for Main Window Disassembler/Debugger. Existing Scan Results/Saved Addresses/Disassembler empty-state export gating should be spot-checked as regression coverage.

## TeeKay87's Memory Engine 0.1.7.rev42 - Call Stack Group Creation and Application Icon

### Status

Rev42 is built directly from the user-supplied `0.1.7.rev41` package. Rev41 passed the complete Windows automated suite at **152/152**. Its first runtime Call Stack Comparer UI check then showed that the Group control looked like a normal dropdown with no discoverable way to create a new group, even though the underlying editable ComboBox path technically accepted text. Rev42 corrects that interaction without changing the session-local group model or comparison engine, and also adds the user-supplied TK artwork as the official application icon.

### Fixed — snapshot group creation

- Replaced the snapshot row Group control's implicit editable-ComboBox creation path with a dedicated Group dropdown template.
- Opening a snapshot Group dropdown now shows a real `TextBox` as the first row of the popup. This field is exclusively for creating a new session-local group.
- Pressing Enter with a non-empty name registers the group through the existing central, case-insensitive session catalog and immediately assigns that group to the snapshot row from which it was created.
- The new-group field is cleared after a successful assignment and the dropdown closes, leaving the newly assigned group visible in the row.
- Existing session groups remain normal dropdown choices below the creation field and selecting one assigns only that snapshot to the selected group.
- Group A and Group B remain non-editable ComboBoxes and still cannot create new group names. They continue to consume the same session-local group catalog.
- The immutable `DebuggerSnapshot.WithMetadata` update path and `DebuggerSnapshotComparer.CompareGroups` engine remain unchanged. Multiple snapshots assigned the same group still form one comparison set.

### Changed — application icon

- Added the supplied TK artwork to `src/TeeKay87.MemoryEngine.App/Assets/` as the source PNG and a generated multi-size Windows ICO.
- Configured the WPF application project `ApplicationIcon` to embed `Assets\TK87ME.ico` into the executable.
- Added the ICO as a WPF resource and assigned it to every application window so the same branding is used consistently in window title bars and shell/task-switcher presentation.
- No theme colors, control styles, layout metrics, or runtime behavior were changed by the icon integration.

### Verification coverage

- Strengthened the existing Call Stack Comparer source-contract check without increasing the registry count. The check now requires the dedicated first-row new-group `TextBox`, its Enter handler, direct existing-group selection binding, and explicit create-and-assign view-model path.
- Automated registry remains **152 checks**.
- Rev41's runtime result is recorded as **152/152 automated PASS; first group-creation UI gate FAIL due to discoverability**. Rev42 replaces that failed UI path and must restart the focused comparer group runtime gate before carried export/debugger testing resumes.

### Versioning

- Advanced centralized application metadata from `0.1.7.rev41` to **`0.1.7.rev42`** with feature title `Call Stack Group Creation and Application Icon`.
- Plugin API remains **`2.18.0`**.
- PS5 remains **`0.1.2.rev39`** because no PS5 plugin source changed.
- Mock remains **`1.0.1.rev17`** because no Mock plugin source changed.
- Snapshot schema remains `teekay87-memory-engine-debugger-snapshot` version `1`.

### Verification requirement

Rev42 must pass a clean Windows build and **152/152**. Runtime/UI verification must first confirm the dedicated new-group input, group reuse across snapshot rows, Group A/B population and restriction, multi-snapshot membership, and application icon presentation. After that, resume the carried rev40 shared Export-dialog presentation gate and Debugger Universal Export verification.

## TeeKay87's Memory Engine 0.1.7.rev41 - Call Stack Comparer Snapshot Group Assignment

### Status

Rev41 is built directly from `0.1.7.rev40` after the rev40 clean Windows automated suite passed **152/152**. The shared Export-dialog presentation fix from rev40 is retained unchanged; its cross-consumer runtime/UI verification had not yet been completed when this requested Call Stack Comparer usability revision was started.

### Changed

- Reworked the Call Stack Comparer snapshot table to use the same always-visible template-control row style used by Saved Addresses where that interaction is relevant to snapshots.
- **Label** and **Notes** are now rendered as in-row `TextBox` editors instead of `DataGridTextColumn` edit mode.
- **Group** is now an in-row editable `ComboBox`. Each snapshot can select an existing comparer-session group or type a new group name directly in its row.
- Added a per-row **Remove** Danger button using the same height, margin, padding, and semantic button style as Saved Addresses. Existing multi-select **Remove** and **Remove All** controls remain available.
- Added the same empty-list overlay pattern used by Saved Addresses; the comparer now shows **No snapshots.** when its snapshot collection is empty.
- Read-only snapshot metadata remains read-only and is presented through centered row content: Captured, Source, Event, Trigger, Stop IP, and Process.
- Replaced free-form Group A and Group B text boxes with non-editable `ComboBox` selectors. They contain only session group names that have already been created or discovered through snapshot Group assignments. New group names cannot be created from Group A or Group B.

### Group assignment behavior

- A newly captured or imported snapshot still enters the snapshot list as one independent row.
- Assigning a group affects only that snapshot row.
- Typing a new non-empty group name in a row registers it once for the current comparer session and assigns the snapshot to it.
- Selecting an existing name assigns the snapshot to that same group.
- Group-name reuse is case-insensitive, so `Boss` and `boss` resolve to the same session group instead of creating duplicate logical groups.
- Multiple snapshots assigned to the same group name are passed together to the existing `DebuggerSnapshotComparer.CompareGroups` pipeline when that name is selected as Group A or Group B.
- A snapshot can be reassigned to another group or unassigned without recapturing or reimporting it.
- Session group names remain local to the retained comparer workspace and are not written to application settings. A new Debugger/comparer session starts with a new group-name collection.
- Imported snapshots that already contain non-empty Group metadata register that name into the current session list, preserving the imported metadata while making it selectable for other rows.
- **Remove All** removes snapshots/results but does not erase the comparer-session group-name catalog; the catalog ends with the comparer session itself.

### Verification coverage

- Strengthened the existing Call Stack Comparer source-contract check without increasing the registry count.
- The contract now requires Saved-Addresses-style template rows, editable row Group assignment, session-local group-name storage, non-editable Group A/B selectors, explicit group commit handling, per-row Remove, and removal of the old Group A/B free-form text boxes.
- Automated registry remains **152 checks**.

### Versioning

- Advanced centralized application metadata from `0.1.7.rev40` to **`0.1.7.rev41`** with feature title `Call Stack Comparer Snapshot Group Assignment`.
- Plugin API remains **`2.18.0`**.
- PS5 remains **`0.1.2.rev39`** because no PS5 plugin source changed.
- Mock remains **`1.0.1.rev17`** because no Mock plugin source changed.
- Snapshot schema remains `teekay87-memory-engine-debugger-snapshot` version `1`; Group remains snapshot metadata and no schema migration is required.

### Verification requirement

Rev41 must pass a clean Windows build and **152/152**. Runtime/UI verification must confirm the new snapshot-row layout and session group workflow, then verify the carried rev40 shared Export-dialog presentation before debugger Universal Export verification resumes.

## TeeKay87's Memory Engine 0.1.7.rev40 - Universal Export Option Display Fix

### Status

Rev40 is a focused shared-UI correction built directly from the user-supplied `0.1.7.rev39` package. Rev39 passed **152/152** and the live snapshot/comparer gates through JSON round-trip, offline ownership, and invalid schema/version rejection. At the start of debugger Universal Export verification, the shared export dialog exposed record diagnostic text for Scope and Format instead of the intended user-facing names. The same defect was confirmed across all consumers of the shared export dialog.

### Fixed

- Replaced the shared export dialog's `DisplayMemberPath` presentation with explicit `ComboBox.ItemTemplate` bindings to `DisplayName` for both Scope and Format.
- Scope choices now render names such as **Threads**, **Registers**, **All Results**, or the consumer-specific scope label instead of `ExportScopeOption { ... }`.
- Format choices now render **JSON**, **CSV**, **TSV**, and **Markdown table** instead of `ExportFormatOption { ... }`.
- The fix is centralized in `DataExportDialog.xaml`, so Scan Results, Saved Addresses, Disassembler, Debugger, and Call Stack Comparer result export all use the corrected presentation without per-consumer duplication.
- Export selection objects, scope descriptions, column selection, destination handling, writers, schemas, and transactional publication are unchanged.

### Verification coverage

- Added the shared export-dialog XAML to the automated source fixtures.
- Strengthened the existing universal-export source-contract coverage to require explicit `DisplayName` item templates and reject the previous `DisplayMemberPath="DisplayName"` presentation path.
- Automated registry remains **152 checks**.

### Versioning

- Advanced centralized application metadata from `0.1.7.rev39` to **`0.1.7.rev40`** with feature title `Universal Export Option Display Fix`.
- Plugin API remains **`2.18.0`**.
- PS5 remains **`0.1.2.rev39`** because no PS5 plugin source changed.
- Mock remains **`1.0.1.rev17`** because no Mock plugin source changed.
- Snapshot schema remains `teekay87-memory-engine-debugger-snapshot` version `1`.

### Verification requirement

Rev40 must pass a clean Windows build and **152/152**. Visually verify the shared export dialog from Scan Results, Saved Addresses, Disassembler, Debugger, and Call Stack Comparer result export: Scope and Format must show only their user-facing names. After that focused regression passes, resume debugger Universal Export verification where rev39 Gate 7 was blocked.

## TeeKay87's Memory Engine 0.1.7.rev39 - Call Stack Comparer Session Persistence and Explicit Capture

### Status

Rev39 is a focused debugger-finalization correction built directly from the user-supplied `0.1.7.rev38` package after rev38 passed the clean Windows gate at **152/152** and the focused live PS5 Disassembler action checks. Snapshot verification then exposed two Call Stack Comparer lifecycle defects: closing the comparer discarded its in-memory snapshot collection even though the same Debugger session remained alive, and **Capture Current** could remain disabled after Continue -> new Pause because the comparer was not notified when a fresh paused stop context became available. The same review also established a clearer capture workflow: opening the comparer should never create evidence implicitly.

Rev39 fixes those lifecycle issues without changing the snapshot schema, comparison engine, Plugin API, Core debugger contracts, Mock plugin, or PS5 plugin.

### Changed

- The Debugger now retains one `CallStackComparerViewModel` workspace for the lifetime of that Debugger window/session instead of creating a new workspace whenever the comparer window is recreated.
- Closing and reopening the modeless Call Stack Comparer therefore preserves captured/imported snapshots, editable metadata, and current comparison results while the same Debugger session remains alive.
- Opening or reactivating the comparer is now inspection-only. `OpenComparerFromDebuggerAsync()` no longer calls `CaptureSnapshotAsync()` or inserts a snapshot automatically.
- New live evidence is added only through the explicit **Capture Current** button.
- If the comparer window is already open, **Compare...** activates that window without mutating its snapshot collection.
- When the Debugger window closes, the retained comparer workspace detaches from that disposed live Debugger. If the comparer remains open, its existing snapshots remain offline analysis data and cannot control or capture from a later debugger session.

### Fixed

- Fixed snapshot collection loss when the Call Stack Comparer window was closed and reopened during the same live Debugger session.
- Fixed **Capture Current** remaining disabled after a Running -> Paused transition. `DebuggerViewModel` now raises `CanCaptureSnapshot` change notification when a running event clears the stop context and when a new paused event installs `_latestStopContext`; the existing `IsBusy` notification continues to cover the final transition after deferred stop refresh work completes.
- Removed the comparer-window `Closed` handler that detached `LiveDebugger` merely because the presentation window was closed. Live authority now follows the Debugger session lifecycle rather than the comparer window lifetime.
- Extended the existing Call Stack Comparer source-contract verification to require same-session workspace persistence, explicit-only capture, debugger-close detachment, and paused-stop capture-state refresh.

### Versioning

- Advanced centralized application metadata from `0.1.7.rev38` to **`0.1.7.rev39`** with feature title `Call Stack Comparer Session Persistence and Explicit Capture`.
- Plugin API remains **`2.18.0`** because no public contract changed.
- PS5 remains **`0.1.2.rev39`** because no PS5 plugin source changed.
- Mock remains **`1.0.1.rev17`** because no Mock plugin source changed.
- Snapshot schema remains `teekay87-memory-engine-debugger-snapshot` version `1`.
- Automated registry remains **152 checks**; one existing source-contract check is strengthened rather than adding another registry entry.

### Verification requirement

Rev39 must restart Gate 1 from a clean Windows extraction/build and require **152/152 PASS**. Runtime verification should then resume at the Call Stack Comparer snapshot gates: confirm opening the comparer adds no snapshot, explicit **Capture Current** adds exactly one snapshot, close/reopen during the same Debugger session preserves the collection, Continue disables capture, the next real Pause re-enables it, and closing the Debugger removes live capture authority without deleting snapshots from an already-open comparer. After those focused checks pass, continue JSON round-trip, debugger list export, pairwise/group comparison, teardown safety, and the final debugger regression.

## TeeKay87's Memory Engine 0.1.7.rev38 - Disassembler Arbitrary-Row Watchpoint Resolution

### Status

Rev38 is a focused usability correction built directly from the user-supplied `0.1.7.rev37` package after rev37 passed the complete Windows automated gate at **152/152**. During live PS5 verification of the rev35 Disassembler debugger actions, the new **Add Watchpoint** command was found to be unnecessarily limited to the exact current Stop/Current-IP row or resolved Watchpoint Hit/Trigger row. Valid memory-access instructions elsewhere in the same Disassembler view remained disabled even though the attached debugger was safely Paused and its current register snapshot was available.

Rev38 removes only that row-identity restriction. The current paused register snapshot may now be used to evaluate any single selected instruction in the same target/session. The plugin-owned resolver and existing debugger validator remain authoritative, so non-memory instructions, `LEA`, ambiguous/multiple memory operands, unsupported sizes, unavailable registers, unsupported addressing forms, invalid mappings/alignment, exhausted watchpoint slots, stale sessions, Running state, and multi-selection still disable the action rather than guessing.

### Changed

- Replaced the address-specific host gate `CanResolveDisassemblyWatchpointAt(instruction.Address)` with a paused-session gate that requires the matching Debugger to be current, not busy, Paused, and to expose a non-empty current register snapshot.
- **Add Watchpoint** can now resolve an arbitrary single selected memory-access instruction in the current Disassembler view instead of only the current stop/trigger instruction.
- The selected instruction is still passed unchanged to the platform-owned `IDisassemblyWatchpointResolver`; Core/WPF still does not parse x86 operands or duplicate PS5-specific address rules.
- The resulting `DisassemblyWatchpointTarget` still passes the existing `ValidateAddressActionRequest(...)` path before the hardware watchpoint is created.
- RIP-relative instructions can continue to resolve directly from instruction metadata, while base/index forms use the current paused register snapshot. This represents the effective address implied by the **current paused register state**; it is not a claim that an unrelated instruction previously or subsequently executed with those same register values.
- Green Watchpoint Hit / yellow Stop-Current-IP presentation, rev32 trigger semantics, snapshot/export/import/comparer work, and rev31 teardown safety are unchanged.

### Versioning

- Advanced centralized application metadata from `0.1.7.rev37` to **`0.1.7.rev38`** with feature title `Disassembler Arbitrary-Row Watchpoint Resolution`.
- Plugin API remains **`2.18.0`** because no public contract changed.
- PS5 remains **`0.1.2.rev39`** because no PS5 plugin source changed.
- Mock remains **`1.0.1.rev17`** because no Mock source changed.
- Automated registry remains **152 checks**; the existing Disassembler action source-contract test now explicitly requires the generalized paused-session gate.

### Verification requirement

Rev38 must restart Gate 1 from a clean extraction/build and require **152/152 PASS**. Focused runtime verification should then return to Gate 3: confirm **Add Watchpoint** is enabled for safely resolvable memory-access rows away from the current Hit/Stop pair, confirm it derives the expected address/size/access from the current paused register context, and recheck multi-selection, non-memory/`LEA`, detach, and validator rejection cases before continuing snapshots/export/import/comparer verification.

## TeeKay87's Memory Engine 0.1.7.rev37 - PS5 Watchpoint Access Verification Correction

### Status

Rev37 is a narrow verification correction built directly from `0.1.7.rev36` after the clean Windows build succeeded and the automated suite reached **151/152 PASS**. The sole failure was the PS5 x86-64 disassembly decoding check expecting a `Write` watchpoint access mode for `add [rax+40h],esi`, while the resolver returned `ReadWrite`.

The resolver behavior is correct. `add [rax+40h],esi` is a read-modify-write instruction: the existing destination value is read from memory, the addition is performed, and the result is written back. Iced therefore reports the memory operand as `ReadWrite`. The PS5 resolver intentionally maps read/write memory access to neutral `DebuggerBreakpointAccess.ReadWrite`, which matches the documented amd64 DR7 behavior and the rev35/rev36 design. Rev37 corrects the test expectation instead of weakening runtime semantics.

### Fixed

- Corrected the PS5 x86-64 disassembly verification assertion for `add [rax+40h],esi` from `DebuggerBreakpointAccess.Write` to **`DebuggerBreakpointAccess.ReadWrite`**.
- Updated the assertion message to state explicitly that the expected mode belongs to a read-modify-write instruction.
- Kept the existing effective-address expectation (`RAX + 0x40`) and four-byte width expectation unchanged.
- No PS5 resolver/runtime implementation was changed. Definite write-only memory operands still map to `Write`; read-only and read/write operands still map to `ReadWrite`; `LEA` and other non-memory/unsafe cases remain rejected.

### Versioning

- Advanced centralized application metadata from `0.1.7.rev36` to **`0.1.7.rev37`** with feature title `PS5 Watchpoint Access Verification Correction`.
- Plugin API remains **`2.18.0`**.
- PS5 remains **`0.1.2.rev39`** because no plugin source changed.
- Mock remains **`1.0.1.rev17`** because no Mock source changed.
- Automated registry remains **152 checks**.

### Verification requirement

Rev37 restarts Gate 1 from a clean extraction/build and requires **152/152 PASS**. Once Gate 1 is green, continue the existing ordered runtime verification from the rev35/rev36 finalization sequence: first verify the separate Disassembler Add Breakpoint/Add Watchpoint actions and green Hit/yellow Stop presentation, then proceed through snapshot capture/export/import, debugger list export, Call Stack Comparer, offline/modeless lifecycle, teardown safety, and full debugger regression.

## TeeKay87's Memory Engine 0.1.7.rev36 - PS5 Disassembly Watchpoint Resolver Compile Fix

### Status

Rev36 is a narrow corrective revision built directly from the user-supplied `0.1.7.rev35` package after the first clean Windows build failed before the automated verification suite could start. The primary compiler failure was `CS0177` in `Ps5DisassemblyWatchpointResolver.TryReadAddressRegister`: the expression-bodied short-circuit return could exit when an unsupported Iced address register mapped to `null` without assigning the required `out ulong value` parameter. The accompanying XAML `System.Object` and missing PS5 plugin metadata-file errors were downstream build failures caused by the PS5 project not producing its assembly. No XAML/project-reference workaround is introduced.

Rev36 changes only the definite-assignment path in the PS5 Disassembler watchpoint resolver plus version/documentation metadata. The rev35 debugger action/highlight design, Plugin API `2.18.0`, Core/WPF behavior, watchpoint teardown safety, snapshots, exports, importer, and comparer remain unchanged.

### Fixed

- Corrected `Ps5DisassemblyWatchpointResolver.TryReadAddressRegister(...)` so unsupported/non-GPR Iced register kinds explicitly assign `value = 0` and return `false` before control leaves the method. Supported x64 GPR mappings still delegate to the existing `TryReadRegister(...)` implementation without changing effective-address semantics.
- Removed the compiler path that produced `CS0177: The out parameter 'value' must be assigned before control leaves the current method`.
- Kept resolver safety behavior unchanged: unsupported address-register forms still disable automatic Disassembler **Add Watchpoint** rather than guessing a target address.
- No changes were made for the reported `XLS0414 System.Object` or `CS0006 ... Platform.PS5.dll could not be found` messages because those were dependency-chain failures produced after the PS5 project stopped on CS0177. A successful PS5 build is expected to eliminate those follow-on errors.

### Versioning

- Advanced centralized application metadata from `0.1.7.rev35` to **`0.1.7.rev36`** with feature title `PS5 Disassembly Watchpoint Resolver Compile Fix`.
- Plugin API remains **`2.18.0`** because no public contract changed.
- Advanced the PS5 plugin semantic version from `0.1.1` to **`0.1.2`** because the plugin source changed. The existing legacy revision field remains `39` for compatibility with the current metadata model; this revision does not introduce another plugin revision increment.
- Mock remains `1.0.1.rev17` because no Mock code changed.
- Updated the existing plugin-version verification assertion to expect PS5 `0.1.2.rev39`; the automated registry remains 152 checks.

### Verification requirement

Rev35 did not reach the automated suite because the solution failed to build. Rev36 therefore restarts **Gate 1** from a clean extraction/build and requires **152/152 PASS** before any runtime verification continues. Once Gate 1 passes, resume the existing ordered rev35 runtime sequence: Disassembler actions and green/yellow watchpoint presentation first, then snapshot capture/export/import, debugger list exports, Call Stack Comparer, modeless/offline lifecycle, permanent teardown safety, and full debugger regression.

## TeeKay87's Memory Engine 0.1.7.rev35 - Disassembler Debugger Actions and Watchpoint Hit-Stop Highlighting

### Status

Rev35 is built directly from the supplied `0.1.7.rev34` verification candidate after the rev34 automated suite passed **152/152** and the carried rev32 watchpoint semantics passed focused Mock and live-PS5 runtime verification. The live PS5 test confirmed a hardware watchpoint at `0x2394E4040` was correctly presented with trigger instruction `0x8033D981` (`add [rax+40h],esi`) and real stop/current IP `0x8033D984` (`mov rcx,[rdi+30h]`). A subsequent Software/Execute breakpoint regression at `0x8033D981` also passed with logical/original bytes preserved.

Rev35 refines the Disassembler debugger workflow and visual stop semantics before the remaining snapshot/export/import/comparer verification continues. Rev31 remains the permanent verified teardown-safety baseline.

### Added

- Added optional Plugin SDK contract `IDisassemblyWatchpointResolver` and neutral `DisassemblyWatchpointTarget`. The resolver receives one already-decoded neutral instruction plus the current debugger register snapshot and can return one derived data-watchpoint address, byte width, and access mode. The contract is optional; plugins that do not expose it simply leave **Add Watchpoint** unavailable from Disassembler.
- Added a PS5 x86-64 implementation backed by the existing Iced decoder/instruction-info layer. It derives one explicit memory operand only, obtains the operand access mode and exact memory width, resolves the effective address from the paused register context, supports ordinary 64-bit GPR base/index addressing plus RIP-relative and FS/GS-base addressing, and returns no target when the result cannot be established safely.
- Added explicit safety rejection for non-memory/`NoMemAccess` instructions such as `LEA`, multiple explicit memory operands, unsupported/variable memory widths, unavailable register/segment-base context, unsupported address-register forms, and memory-address registers that the selected instruction itself modifies. No textual operand parsing or host-side x86 assumptions are used.
- Added a second transient Disassembler marker, **`Stop / Current IP`**, for resolved watchpoint stops when the backend stop/current instruction differs from the resolved trigger instruction.
- Added a theme-derived `WarningMutedBrush` so watchpoint stop/current rows can use a readable yellow warning highlight in Light, Dimmed, and Dark without hard-coded per-view colors.

### Changed

- Replaced the Disassembler's combined **Add Breakpoint / Watchpoint...** context-menu command with separate **Add Breakpoint** and **Add Watchpoint** entries, matching the established Scan Results and Saved Addresses interaction.
- Both Disassembler debugger actions now require exactly one selected row. Multiple selected rows keep both entries visible but disabled, preserving extended selection for copy/export.
- **Add Breakpoint** is enabled only for a valid decoded instruction in the current executable memory region and only when the existing attached-debugger request validator accepts the generated persistent Software/Execute request.
- **Add Watchpoint** is enabled only when the matching debugger is Paused with a valid current register snapshot and the selected row is the current stop instruction or the current resolved trigger instruction. The plugin resolver must derive one safe address/size/access tuple and the existing debugger validator must accept the final persistent Hardware request. This prevents stale/arbitrary register state from being applied to unrelated disassembly rows.
- PS5 read-only instruction accesses are mapped to a Hardware Read/Write request because x86 debug-register data breakpoints do not provide a read-only encoding; write-only memory accesses remain Write. The normal plugin validator remains authoritative for supported widths, alignment, mapped-range rules, and slot availability.
- Resolved watchpoint presentation now uses two visual highlights in Disassembler: **green = actual Hit/Trigger Instruction**, **yellow = real Stop/Current IP**. If trigger resolution is unavailable, only the stop/current row is shown with the yellow unresolved-watchpoint presentation; no green trigger is fabricated.
- Software breakpoint highlighting remains unchanged: the breakpoint/origin instruction keeps the existing green presentation, and debugger `INT3` instrumentation remains masked by the logical/original-byte overlay.
- Plugin API advances from `2.17.0` to **`2.18.0`** for the new optional disassembly-watchpoint resolver contract. Mock semantic version advances from `1.0.0` to **`1.0.1`** and PS5 semantic version advances from `0.1.0` to **`0.1.1`** in accordance with plugin update versioning; their existing revision fields remain present for compatibility with the current metadata model. Their API target advances to `2.18.0`.
- Application metadata advances from `0.1.7.rev34` to `0.1.7.rev35` with feature title `Disassembler Debugger Actions and Watchpoint Hit-Stop Highlighting`.

### Verification coverage

- Kept the automated registry at **152 checks** while strengthening existing checks rather than adding duplicate entries.
- Extended the PS5 x86-64 disassembly check to verify that `add [rax+40h],esi` resolves to the expected effective address, 4-byte size, and Write watchpoint request from a known RAX value, and that `LEA` is rejected as a non-memory-access watchpoint source.
- Extended the debugger overlay lifecycle check to require both `Watchpoint hit` at the trigger instruction and `Stop / Current IP` at the separate backend stop instruction.
- Extended Disassembler source-contract verification for the separate menu actions, single-selection gating, plugin-owned resolver use, derived size/access consumption, existing request validation, and green/yellow row-highlight bindings.

### Verification requirement

Rev35 must restart the clean Windows automated gate because Plugin SDK and PS5 plugin code changed. Require **152/152 PASS** before continuing the runtime sequence. After that, recheck the already proven watchpoint hit/stop presentation specifically for the new green/yellow row coloring and verify the two separate Disassembler actions, including automatic PS5 watchpoint derivation from the live `add [rax+40h],esi` example. The remaining snapshot/export/import/comparer gates then continue in order from the updated rev35 verification document.

## TeeKay87's Memory Engine 0.1.7.rev34 - Debugger Finalization Verification Test Corrections

### Status

Rev34 is a verification-correction revision built directly from the unverified `0.1.7.rev33` candidate after the first Windows automated run exposed two deterministic defects in the newly added test assertions. The underlying snapshot importer and comparer behavior covered by those two checks did not require runtime changes. Rev31 remains the latest fully verified live-PS5 teardown baseline, and the complete rev32-rev33 runtime verification sequence remains pending after the corrected automated gate passes.

### Changed

- Corrected the debugger snapshot JSON address-width validation fixture. Rev33 changed the serialized `addressWidth` from 64 to 32 bits, but every address in that fixture was still representable in 32 bits, so accepting the document was correct. The rev34 test now declares a 28-bit address width, which is valid schema metadata but is smaller than the fixture's `0x10000000` code address and therefore genuinely exercises the importer's existing out-of-range rejection path.
- Corrected the snapshot comparer verification to treat neutral register identifiers case-insensitively. The fixture stores the canonical register id as `r12`, while the rev33 assertion searched for the literal case-sensitive value `R12`; the comparer row existed but the test's `Single(...)` lookup could not find it. The detailed register lookup and promoted summary lookup now use ordinal case-insensitive matching, consistent with the comparer/register model.
- Updated the snapshot test fixture's captured application revision metadata from 33 to 34 so JSON round-trip/comparison verification represents the current application candidate.
- Application metadata advances from `0.1.7.rev33` to `0.1.7.rev34` with feature title `Debugger Finalization Verification Test Corrections`. Plugin API remains `2.17.0`; Mock remains `1.0.0.rev17`; PS5 remains `0.1.0.rev39` because this revision changes no plugin contracts or platform transport/runtime implementation.

### Intermittent PS5 register test observation

- During the first rev33 Windows verification attempt, `PS5 debugger general register snapshot protocol` passed. During the second complete run it returned 28 registers instead of the expected 76. A 28-register result is the already supported fallback shape when the optional FPU/SIMD probe is unavailable while general registers and FS/GS base remain available.
- Because the failure did not reproduce consistently and rev33/rev34 do not change the PS5 register transport or mapper, rev34 does not alter the verified optional-register fallback behavior or production timeout policy. The corrected full automated suite must be rerun. If this register-count failure repeats, it becomes a separate investigation before runtime acceptance continues.

### Verification requirement

Run the full Windows suite again and require **152/152 PASS** before continuing to runtime Gate 2. If the PS5 general-register check fails again, stop and preserve the complete output so the intermittent optional-register probe path can be isolated without weakening the existing timeout-safety behavior.

## TeeKay87's Memory Engine 0.1.7.rev33 - Debugger Finalization, Snapshots and Comparer

### Status

Rev33 is a combined **verification candidate** built on rev32. Rev31 remains the latest fully verified PS5 teardown baseline; rev32 was not separately runtime-accepted before this package. The rev32 watchpoint trigger/current-IP work is therefore retained as the first verification gate for rev33 rather than being treated as an already verified dependency.

### Added

- Added **Add Breakpoint / Watchpoint...** to the Disassembler row context menu. It is enabled only when exactly one instruction is selected, requires an already attached Debugger for the same plugin/process/connection generation, runs the same neutral plugin validation used elsewhere, and opens the existing `BreakpointDialog` before using the existing `DebuggerViewModel.AddBreakpointAsync` path. Multiple selected instructions leave the command disabled.
- Added the Core-owned immutable debugger snapshot model with explicit source metadata, section status, event context, exact register bytes, call frames, bounded logical disassembly, breakpoint/watchpoint state, and bounded memory blocks.
- Added one shared live snapshot capture path in `DebuggerViewModel`. Capture requires a Paused debugger, current target identity, current connection generation, and an unchanged stop-event sequence through the complete read. It captures already resolved rev32 trigger semantics, requires the selected thread to match the stop thread when both are known, and revalidates the stop before publishing.
- Added bounded Standard Snapshot context: up to 64 bytes before/after stop and trigger through the existing logical Disassembler pipeline plus a bounded 256-byte Memory Viewer window around the semantic stack pointer when safely available. Optional failures are represented by section state instead of being silently converted to valid-looking zero/default data.
- Added debugger snapshot JSON schema `teekay87-memory-engine-debugger-snapshot` version `1`, exact hexadecimal 64-bit address serialization, string enum values, raw-byte validation, width/range checks, additive-field tolerance, import validation, and temporary-file transactional publication.
- Added a separate modeless **Call Stack Comparer** window. Snapshots can be captured directly while the associated debugger is Paused or imported from JSON for completely offline analysis. Label, Group, and Notes remain user-editable metadata while captured debugger data remains immutable.
- Added pairwise and arbitrary user-defined group comparison. The comparer reports common/stable call-stack paths, first stable cross-group divergence, ordered-frame alignment evidence, exact raw register differences, separate trigger/current instruction context, logical/original disassembly differences, breakpoint/watchpoint context, and bounded stack-memory differences. Module + Offset is preferred over raw code addresses where available so captures remain comparable across ASLR/process restarts.
- Added explainable **Potential discriminator** summary rows only for values that are stable inside both compared groups and different between them. Variable data remains visible but is not promoted as a semantic conclusion.
- Added Universal Export from the Debugger for Threads, Registers, Breakpoints / Watchpoints, Call Stack, and Events using the existing JSON/CSV/TSV/Markdown table pipeline. Breakpoint export separates Type, Mechanism, Access, Size, Enabled state, and Lifetime; event export separates Instruction Pointer, Trigger Instruction, watched-address context, and Trigger Resolution.
- Added Universal Export for the current derived comparison-result table.
- Added `InMemoryExportDataSource` as the bounded structured adapter for already materialized debugger/comparer tables.
- Added automated/source-contract coverage for snapshot immutability, live capture safety, JSON round trip and validation, transactional cancellation, comparer pair/group behavior, module-relative/frame alignment behavior, Disassembler debugger actions, Call Stack Comparer integration, and the five debugger table exports. The registry target is now **152 checks**.
- Added `docs/architecture/DEBUGGER_SNAPSHOTS_AND_COMPARER.md` and the ordered rev33 verification/source-review documents.

### Changed

- Application metadata advances from `0.1.7.rev32` to `0.1.7.rev33`. Plugin API remains `2.17.0`; Mock remains `1.0.0.rev17`; PS5 remains `0.1.0.rev39` because no plugin contract or platform transport changed in rev33.
- Snapshot breakpoint records now model **Type** (`Breakpoint`/`Watchpoint`), **Mechanism** (`Software`/`Hardware`), and **Lifetime** (`Persistent`/`Temporary`) independently before schema version 1 is released.
- Snapshot publication deep-copies collection state, including per-instruction marker collections, so later debugger/UI mutation cannot alter a previously published capture.
- Snapshot section status now reports supported-but-empty Registers or Call Stack as unavailable rather than claiming a complete section with no captured data.
- Group call-stack divergence is only reported when each group is internally stable at the claimed frame; a difference between the first sample of each group is no longer sufficient.
- The Call Stack Comparer subscribes to the associated live Debugger only while the comparer is open. **Capture Current** reflects `CanCaptureSnapshot`, and closing the comparer releases that reference while retained/imported snapshots remain ordinary offline data.
- Complete snapshot JSON and flat Universal Export remain deliberately separate: snapshot JSON is the lossless hierarchical source-of-truth format, while Universal Export is used for individual debugger/comparison tables.

### Preserved safety and compatibility

- Rev32's separate stop/current IP and trigger instruction semantics are unchanged and are captured/exported independently.
- Rev31's explicit PS5 software/hardware breakpoint/watchpoint teardown and staged-cleanup behavior is untouched and remains a permanent live regression gate.
- Snapshot capture, import, export, and comparison are read-only. Import never reconnects to a target and never recreates breakpoint/watchpoint state.
- Logical/original Disassembler bytes remain authoritative. Software-breakpoint `INT3` instrumentation is not saved as game code and does not create comparison differences.
- Existing Scan Results and Saved Addresses debugger actions, stepping, Run to Address, Breakpoints/Watchpoints manager, Threads, Registers, Call Stack, Memory Viewer, themes, and modeless-window behavior are retained.

### Verification requirement

This package was prepared in an environment without the Windows/.NET WPF toolchain, so the 152-check executable suite is **not claimed as passed here**. Static package validation must be followed by the ordered Windows/Mock/live-PS5 procedure in `docs/testing/APP_0.1.7_REV33_VERIFICATION.md`. Because rev32 was not separately tested, that procedure begins by verifying the carried-forward rev32 watchpoint semantics before testing the new rev33 work.


## TeeKay87's Memory Engine 0.1.7.rev32 - Watchpoint Trigger Resolution and Event Semantics

Revision 32 begins the final debugger-finalization sequence from the verified `0.1.7.rev31` baseline. The revision does not change the rev31 PS5 watchpoint-detach safety path. It corrects the remaining watchpoint presentation ambiguity first, so later Debugger Snapshot export/import and Call Stack Comparer work can be built on an event model that preserves the difference between where execution stopped and which instruction actually caused a watched memory access.

### Added - Neutral watchpoint trigger semantics

- Plugin API advances from `2.16.0` to **`2.17.0`**.
- Added `DebuggerTriggerResolution` with the initial public states `Unresolved`, `BackendExact`, and `DisassemblyDerived`.
- `DebuggerEvent` now preserves `TriggerInstructionAddress` and `TriggerResolution` independently from the existing authoritative `InstructionPointer`.
- Watchpoint events expose neutral `WatchedAddress`, `WatchpointAccess`, and `WatchpointSize` values from their triggered watchpoint context without replacing the original breakpoint/watchpoint request model.
- The pre-existing `DebuggerEvent` constructors remain available for compatible older 2.x plugins. Events created through those constructors default to an unresolved trigger unless a backend supplies a resolved trigger through the new constructor.
- Added `DebuggerEvent.WithTriggerInstruction(...)` so Core/host enrichment can create a new immutable event while preserving the original stop context and timestamp.

### Added - Core disassembly-derived trigger resolver

- Added `DebuggerWatchpointTriggerResolver` in Core.
- The resolver accepts a neutral watchpoint event plus a logical `DisassemblySnapshot`; it does not contain x86-specific decoding logic.
- A trigger is accepted only when exactly one valid logical instruction ends at the event's stop/current instruction pointer.
- Existing backend-exact trigger information is never replaced by Core.
- If the boundary cannot be established safely, the original event is retained as `Unresolved`; no `RIP - 1` or other fixed-length guess is used.
- Resolution consumes the same logical Disassembler path used elsewhere in the application, so debugger byte overlays remain authoritative and software-breakpoint `INT3` instrumentation is not intentionally treated as game code.

### Changed - Disassembler watchpoint markers

- `DebuggerDisassemblyOverlayState` no longer treats every watchpoint event's `InstructionPointer` as the trigger address.
- Resolved watchpoints show **`Watchpoint hit`** at `TriggerInstructionAddress`.
- Unresolved watchpoints show **`Watchpoint stop (trigger unresolved)`** at the real stop/current instruction pointer instead of falsely identifying that instruction as the memory-accessing instruction.
- Resume and detach/reset paths still clear transient watchpoint markers.
- Software-breakpoint markers, logical byte overlays, staged breakpoint-retirement behavior, and marker coexistence remain unchanged.

### Changed - Debugger event presentation

- The Events table now exposes separate **Instruction Pointer**, **Trigger Instruction**, **Watched Address**, and **Trigger Resolution** columns.
- The Debugger attempts best-effort disassembly-derived trigger resolution for paused unresolved watchpoint events before refreshing the rest of the stop context.
- Successful resolution updates the stored/displayed event snapshot and the shared Disassembler overlay while preserving the original stop IP.
- Failed resolution remains explicit and non-fatal; registers, call stack, breakpoint/watchpoint management, stepping, and normal debugger refresh continue from the real stop context.
- PS5 watchpoint event text no longer claims that the callback RIP is the accessing instruction. It now describes that RIP as the address where execution stopped until host resolution establishes a trigger.

### Changed - Deterministic Mock watchpoint fixture

- Mock advances from `1.0.0.rev16` to **`1.0.0.rev17`** and targets Plugin API `2.17.0`.
- The deterministic hardware-watchpoint fixture now models the post-access stop explicitly: its stop/current IP is the instruction following the synthetic trigger instruction.
- Mock supplies the known synthetic trigger as `BackendExact`, allowing automated coverage to prove that backend-exact and disassembly-derived trigger paths remain distinct.
- PS5 advances from `0.1.0.rev38` to **`0.1.0.rev39`** for the Plugin API target and corrected watchpoint stop wording. The rev31 hardware-watchpoint teardown implementation is otherwise unchanged.

### Verification coverage

- Added **Core watchpoint trigger resolution** to the automated registry.
- Extended the neutral event-model coverage for trigger address, trigger resolution, watched address, access mode, and size.
- Updated Mock hardware-watchpoint verification so stop/current IP and trigger instruction are asserted independently.
- Extended the debugger Disassembly-overlay lifecycle check to prove both unresolved and resolved marker behavior and to ensure the resolved marker is not left on the post-access stop instruction.
- The automated registry target advances from **142 to 143 checks**.

### Preserved safety and regression requirements

- Rev31 PS5 active/staged hardware-watchpoint cleanup before detach/disposal remains unchanged and remains a mandatory live regression gate.
- Rev36 software-breakpoint cleanup, rev28 composed-operation cleanup, breakpoint-aware Step Over, Step Out, Run to Address, logical breakpoint bytes, stale-session guards, register safety, and modeless-window lifecycle are not redesigned by this revision.
- The documented ps5debug-NG same-instruction software-breakpoint/watchpoint event-consumption limitation remains external; rev32 does not fabricate an event that the backend never delivered.

### Development order

- The debugger finalization handover is now dependency-driven. Rev32 implements **Phase A — Event semantics** first.
- The next accepted revision should continue with the neutral immutable Debugger Snapshot model and shared capture pipeline before complete snapshot JSON persistence or the Call Stack Comparer is added.
- Universal Export for flat debugger tables remains a separate finalization requirement and is not substituted for the hierarchical snapshot model.

### Version / compatibility

| Component | rev32 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev32` | Watchpoint trigger/current-IP separation |
| Feature title | `Watchpoint Trigger Resolution and Event Semantics` | New application feature title |
| Plugin API | `2.17.0` | Add neutral trigger-resolution event context |
| Mock plugin | `1.0.0.rev17` | Backend-exact post-access watchpoint fixture |
| PS5 plugin | `0.1.0.rev39` | API target and accurate stop wording; rev31 cleanup retained |
| Automated registry | `143` | One new Core trigger-resolution check plus updated regressions |

## TeeKay87's Memory Engine 0.1.7.rev31 - Safe PS5 Watchpoint Detach Cleanup

Revision 31 continues the active `0.1.7` Debugger block from the supplied rev30 package. Rev30 introduced debugger-address shortcuts and clearer Breakpoint/Watchpoint classification, but it was superseded before verification after live PS5 shutdown testing exposed a target-safety problem: leaving a hardware watchpoint enabled and then closing the application could leave the watchpoint armed after debugger teardown. The game continued running until the watched access occurred again, at which point it could terminate because the stale hardware debug-register condition was still active without the client debugger attached.

### Changed - PS5 hardware-watchpoint teardown ownership

- PS5 plugin advances from `0.1.0.rev37` to **`0.1.0.rev38`**.
- Explicit debugger `DetachAsync` now performs client-owned hardware-watchpoint cleanup before sending the backend detach command.
- `DisposeAsync`, including application/tool-window shutdown cleanup, performs the same hardware-watchpoint cleanup on a best-effort basis before backend detach.
- The cleanup pass includes both hardware watchpoints still tracked as backend-active and staged temporary-watchpoint removals that have not yet been flushed by Continue.
- Hardware slots are deduplicated by backend slot index and disabled through the existing `CMD_DEBUG_SET_WATCHPOINT` path, preserving the plugin's current access/size/address encoding rather than adding a second transport path.
- A shared `DisableHardwareWatchpointBackendSlotAsync` helper now owns state updates for staged Continue cleanup and detach/disposal cleanup, avoiding duplicate slot-disable bookkeeping.
- Cleanup attempts every tracked hardware slot even if an earlier slot reports an error. Explicit detach still gives backend teardown a chance to run and reports combined instrumentation/detach failures when necessary.
- The debugger event channel remains open while software-breakpoint and hardware-watchpoint cleanup commands run. Existing detaching-state gating continues to prevent teardown interrupts from mutating the logical debugger workspace before the channel is closed.

### Preserved - Existing debugger behavior

- Rev36's defensive software-breakpoint restoration remains unchanged in purpose and still runs before backend detach/disposal.
- Rev30's Breakpoint/Watchpoint **Type** and Software/Hardware **Mechanism** columns remain unchanged.
- Rev30's validated **Add Breakpoint** / **Add Watchpoint** shortcuts for Scan Results and Saved Addresses remain unchanged and still require an already-open Debugger attached to the same target/session.
- Rev29 logical Disassembler bytes and `Markers` remain unchanged; debugger instrumentation is still presented as metadata instead of replacing the original instruction view.
- Hardware watchpoint add/remove/enable/disable rules, DR0-DR3 slot allocation, Write/Read-Write mapping, size/alignment validation, hit attribution, and conservative zero-DR6 behavior are not redesigned.
- Plugin API remains `2.16.0`, Mock remains `1.0.0.rev16`, and no new public contract or ps5debug-NG opcode is introduced.

### Added - External ps5debug-NG bug report

- Added `docs/bug-reports/ps5debug-ng-detach-can-leave-hardware-watchpoints-active.md`.
- The report documents the observed reproduction and the current upstream teardown path in `debugger/source/debug.c`.
- `debug_full_teardown()` zero-initializes a local DBREG buffer, calls `PT_GETDBREGS` using the process id, does not check the return value, and uses the resulting low DR7 byte to decide whether per-LWP debug-register clearing is needed. A failed or non-representative probe can therefore look identical to "no active hardware debug registers" and bypass the per-thread zeroing path before `PT_DETACH`.
- Rev31 does not modify the external backend. The plugin instead removes every hardware-watchpoint slot it owns explicitly before requesting backend detach, so target safety does not depend solely on teardown rediscovery.

### Verification coverage

- Added **PS5 debugger safe detach clears hardware watchpoints** to the automated registry.
- The new protocol test creates one still-active persistent hardware watchpoint and one temporary watchpoint whose triggered removal is staged, then disposes the debugger session directly. It verifies that both backend slots receive explicit disable commands before detach completes.
- The automated registry target advances from **141 to 142 checks**.
- Rev30 was not accepted as a verified baseline before this correction; its planned `141/141` gate is therefore superseded by rev31's `142/142` gate.
- Live rev31 acceptance must reproduce the original failure path: leave a hardware watchpoint enabled, close the application without manually removing it, then trigger the formerly watched access and confirm the game remains running. Explicit Debugger Detach with an active watchpoint must pass the same safety regression.

### Documentation and development order

- Updated README current-state/version information, PS5 plugin documentation, debugger architecture, ps5debug-NG protocol mapping, Plugin SDK compatibility notes, verification documentation, and the full development action plan.
- Rev30 verification is recorded as superseded before completion by this target-safety correction.
- Debugger **Integration, Export and Finalization** moves from rev31 to **rev32** so rev31 can remain focused on safe PS5 watchpoint teardown and its regression coverage.

### Version / compatibility

| Component | rev31 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev31` | Safe PS5 watchpoint detach cleanup |
| Feature title | `Safe PS5 Watchpoint Detach Cleanup` | New application feature title |
| Plugin API | `2.16.0` | Unchanged |
| Mock plugin | `1.0.0.rev16` | Unchanged |
| PS5 plugin | `0.1.0.rev38` | Explicit hardware-watchpoint cleanup before detach/disposal |
| Automated registry | `142` | One new PS5 teardown regression |

## TeeKay87's Memory Engine 0.1.7.rev30 - Debugger Address Actions and Breakpoint Classification

Revision 30 continues the active `0.1.7` Debugger block after rev29 runtime acceptance. The revision improves the Breakpoints / Watchpoints table terminology, adds validated debugger shortcuts to Scan Results and Saved Addresses, advances the optional Plugin SDK validation surface required to enable those shortcuts safely, records the completed rev29 verification, and documents an external ps5debug-NG interaction discovered during the combined breakpoint/watchpoint marker test.

### Changed - Breakpoint and watchpoint classification

- The Breakpoints / Watchpoints table now separates the semantic record type from its implementation mechanism.
- **Type** now reports `Breakpoint` for execute-trigger records and `Watchpoint` for data-access records.
- Added **Mechanism**, which reports the neutral backend mechanism (`Software` or `Hardware`).
- The visible table is now **Address | State | Type | Mechanism | Access | Size | Lifetime**. Existing breakpoint/watchpoint ids, requests, state transitions, lifetime behavior, and backend mappings are unchanged.
- This split is intentionally future-facing: a future Hardware/Execute record can still be presented as `Type = Breakpoint`, `Mechanism = Hardware`, `Access = Execute` without overloading one column with two different concepts.

### Added - Scan Results and Saved Addresses debugger shortcuts

- Added **Add Breakpoint** and **Add Watchpoint** to the row context menus for both Scan Results and Saved Addresses.
- The shortcuts deliberately require an already-open Debugger window that is attached to the same plugin, target process, and connection generation. A row context menu never opens or attaches the Debugger implicitly.
- **Add Breakpoint** creates a persistent one-byte Software/Execute request for the selected address.
- **Add Watchpoint** creates a persistent Hardware/Write request using the selected row's value width (`CurrentValue.Size` for Scan Results and `ValueSize` for Saved Addresses).
- Each menu item is enabled independently. It remains disabled when the matching capability is unavailable, the Debugger is detached/busy/stale, the Debugger belongs to another target/session generation, the backend cannot pre-validate the request, or the plugin rejects the address/access/size/alignment/slot/duplicate/pending-cleanup combination.
- Final request creation still goes through the existing `DebuggerViewModel.AddBreakpointAsync` path and the plugin's normal breakpoint service. The shortcut does not bypass runtime validation or backend safety.

### Added - Optional breakpoint request validation contract

- Plugin API advances from `2.15.0` to **`2.16.0`** with optional `IDebuggerBreakpointValidationService` and neutral `DebuggerBreakpointValidationResult`.
- The service performs a non-mutating legality/availability check for a complete `DebuggerBreakpointRequest`, allowing generic host UI to decide whether an action can be offered before changing debugger state.
- Plugins targeting an older compatible Plugin API remain loadable. If an attached debugger session does not expose the optional validation service, the new address shortcuts stay disabled while the existing Debugger **Add** workflow remains available and authoritative.
- Mock advances to **`1.0.0.rev16`** and exposes deterministic validation for executable software breakpoints, aligned hardware watchpoints, duplicate requests, and slot limits.
- PS5 advances to **`0.1.0.rev37`** and exposes validation by reusing its existing software-breakpoint mapped/executable checks and hardware-watchpoint mapped-range, protection, access, width, natural-alignment, slot, duplicate, and staged-cleanup rules. No ps5debug-NG wire command changes in this revision.

### Documented - Rev29 runtime acceptance and external backend behavior

- Rev29 is now recorded as verified after **138/138** automated checks and focused live-PS5 acceptance of logical Software/Execute breakpoint rendering, hardware `Watchpoint hit` rendering, and simultaneous `Breakpoint` plus `Watchpoint hit` marker presentation while original bytes/instructions remained authoritative.
- Added `docs/bug-reports/ps5debug-ng-software-breakpoint-step-consumes-overlapping-watchpoint-hit.md` for the external ps5debug-NG behavior observed when a Software breakpoint is placed on the exact instruction whose memory write would trigger a hardware watchpoint. The backend restores the original byte, single-steps the instruction, synchronously consumes the resulting stop with `wait4`, and rearms `INT3`; the overlapping watchpoint stop is therefore not delivered as a separate client event.
- The external overlap behavior does not change rev29 logical Disassembler presentation and is not worked around by broadening or guessing debugger events in the host.

### Verification and preservation

- The automated registry target increases from **138** to **141 checks**. New coverage verifies the optional neutral validation service, Breakpoint/Watchpoint `Type` versus Software/Hardware `Mechanism` presentation, and both main-workspace context-menu integrations.
- Existing rev29 logical disassembly/Markers behavior, debugger stepping/run-to/cleanup semantics, breakpoint/watchpoint transport, scanner behavior, Saved Addresses, Memory Viewer, Universal Export, and modeless-window lifecycle are preserved.
- The previously planned Debugger **Integration, Export and Finalization** milestone moves from rev30 to **rev31**. Rev30 is consumed by these debugger-address workflow and classification corrections.

### Version / compatibility

| Component | rev30 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev30` | Debugger address actions and classification |
| Feature title | `Debugger Address Actions and Breakpoint Classification` | New application title |
| Plugin API | `2.16.0` | Optional breakpoint-request validation service |
| Mock plugin | `1.0.0.rev16` | Validation-service implementation |
| PS5 plugin | `0.1.0.rev37` | Validation-service implementation; wire protocol unchanged |
| Automated registry | `141` | Three new checks |

## TeeKay87's Memory Engine 0.1.7.rev29 - Logical Disassembly and Debugger Markers

Revision 29 extends the still-active `0.1.7` Debugger block with the missing presentation boundary between debugger instrumentation and the Disassembler. Runtime cheat-development testing after rev28 acceptance demonstrated the problem directly: a Software/Execute breakpoint placed on `0x8033D981` replaced the first byte of the original `01 70 40  add [rax+40h],esi` instruction with `CC`. The debugger behaved correctly, but the existing Disassembler read the instrumented process bytes literally and rendered `int3` followed by a false instruction decoded from the remaining bytes. That presentation is unsuitable both for debugger analysis and for the later assembly/patch workflow, where the application must retain a trustworthy view of the instruction that belongs to the game rather than exposing debugger-owned trap bytes as if they were game code.

### Added - Logical disassembly overlays

- Added Core `DisassemblyOverlay`, `DisassemblyByteOverlay`, and `DisassemblyMarker` models. They describe presentation-only byte substitutions and address markers without changing `IDisassemblerProvider`, target memory, or any platform protocol.
- Added an overload path in `DisassemblyReader` that applies debugger-owned original bytes to a local copy of the bounded read buffer before decoding. The target read remains untouched; only the bytes passed to the decoder and stored in the resulting `DisassemblySnapshot` are made logical.
- `DisassemblySnapshot` now carries the markers that fall inside its bounded byte range while preserving the existing constructor for callers that do not provide presentation metadata.
- Logical bytes remain subject to the existing range/order/raw-byte validation, so provider output is still checked against the exact byte sequence that the user-facing Disassembler presents.

### Added - Debugger/Disassembler presentation state

- Added Core `DebuggerDisassemblyOverlayState` and attached one instance to each `DebuggerSessionCoordinator`.
- The state keeps the original neutral `DisassembledInstruction` captured by the existing breakpoint-aware Step Over path before a Software/Execute breakpoint is installed. The same captured bytes are now reused for Disassembler presentation instead of creating a second original-byte cache.
- Enabled Software/Execute breakpoints publish a `Breakpoint` marker at their code address while their original instruction bytes replace debugger-owned `INT3` instrumentation in the logical view.
- Disabled breakpoint records remain visible as `Breakpoint (disabled)` markers. They do not mask live bytes once backend retirement has completed.
- If a Software/Execute breakpoint is disabled, removed, or automatically retired while the target is Paused, the original bytes remain presentation-active until the staged backend cleanup is known to have completed. This prevents a transient/staged ps5debug-NG `CC` byte from leaking into the Disassembler between UI removal and Continue/Detach cleanup.
- Successful Continue clears completed staged retirements. Detach/disposal resets the debugger presentation state with the coordinator lifecycle.
- A hardware-watchpoint stop publishes `Watchpoint hit` at the event's reported instruction pointer. Watchpoints do not replace instruction bytes, so they contribute marker metadata only. The marker is cleared when execution resumes or a later non-watchpoint paused stop replaces that context.
- `PluginViewModel` resolves the attached debugger coordinator matching the Disassembler's plugin/process/connection generation and supplies its current overlay to Core. Disassembler windows opened from the Debugger, MainWindow, Memory Viewer, or another existing entry point therefore share the same logical code view without platform checks in WPF.

### Changed - Disassembler presentation and export

- Added a fourth visible Disassembler column named **Markers** between **Bytes** and **Instruction**. The stable display order is now **Address | Bytes | Markers | Instruction**.
- `DisassemblyInstructionViewModel` joins multiple address markers with a neutral separator while retaining the existing origin-row and syntax-token presentation.
- Active Software/Execute breakpoints now display the original bytes and original decoded instruction in the normal Bytes/Instruction columns rather than `CC / int3` plus a false decode of the remaining instruction bytes.
- Added `markers` to the structured Disassembler export column set. Displayed and Selected exports preserve marker metadata together with the logical bytes/instruction already visible to the user. The flexible disassembly export schema remains version `1`, consistent with previous additive columns.
- Existing clipboard commands are unchanged; this revision does not alter the verified selection/copy behavior from the completed `0.1.6` Disassembler block.

### Preserved - Runtime instrumentation and future patch boundary

- No debugger breakpoint or watchpoint transport is changed. Software breakpoints still use the backend's normal runtime trap mechanism, and hardware watchpoints remain data-breakpoint records owned by the platform debugger implementation.
- The logical byte overlay is read-only presentation. It never writes original bytes back to the target, never disables a breakpoint to perform a Disassembler read, and never changes debugger event/step semantics.
- The PS5 plugin remains `0.1.0.rev36`; its safe detach, staged breakpoint cleanup, logical stop snapshot, watchpoint mapping, and transport behavior are unchanged.
- Plugin API remains `2.15.0` and Mock remains `1.0.0.rev15`; no public plugin contract is required for this host/Core integration.
- The later assembly/instruction-editing feature remains separate. Intentional user-created code patches must be tracked explicitly when that feature is implemented and must not be confused with debugger-owned instrumentation. Revision 29 establishes the original-instruction/logical-code foundation that later patching can build on.

### Verification and documentation

- Added **Core disassembly debugger-byte overlay** coverage. The test deliberately writes an `INT3` byte into Mock target memory, supplies the original instruction through the logical overlay, verifies that the provider decodes the original instruction, verifies both breakpoint/watchpoint markers, and then proves the target memory still contains `CC` after the read.
- Added **Core debugger disassembly overlay lifecycle** coverage for enabled software breakpoints, paused staged retirement, completed retirement, watchpoint-hit markers, and marker cleanup on resume.
- Added **Disassembler debugger markers and logical instruction source contract** coverage for the new column order/binding, debugger state publication, target-matched overlay consumption, and platform-neutral host ownership.
- Extended the existing structured Disassembler export test to require the `Markers` column and marker JSON value.
- Automated registry target increases from **135** to **138 checks**.
- Added `APP_0.1.7_REV29_SOURCE_REVIEW.md` and `APP_0.1.7_REV29_VERIFICATION.md` with the focused Windows/Mock/live-PS5 acceptance path.
- Updated README, debugger/disassembly architecture, export documentation, UI documentation, PS5 disassembly/protocol notes, and the full development action plan. Debugger **Integration, Export and Finalization** moves to rev30 because rev29 is consumed by this debugger/disassembler correction.

### Version and compatibility

- Host application: `0.1.7.rev29`.
- Feature title: `Logical Disassembly and Debugger Markers`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev36`.
- Automated registry target: **138 checks**.
- Next planned revision after rev29 acceptance: `0.1.7.rev30 - Integration, Export and Finalization`.

## TeeKay87's Memory Engine 0.1.7.rev28 - Debugger Deferred Stop Verification Contract Fix

Revision 28 is a verification-contract correction built directly from the supplied `0.1.7.rev27` candidate after the Windows automated run completed **134/135** checks. The single failing check was `Debugger breakpoint manager source contract`. Production debugger code was not failing: rev27 had intentionally widened its deferred-stop branch from a Continue-only condition to a combined Continue/interrupted-composed-operation condition, while the older breakpoint-manager source assertion still required the exact obsolete source text. The dedicated rev27 interrupted-operation cleanup contract already validated the widened condition and passed in the same run.

### Changed - Deferred stop source verification

- Corrected the existing breakpoint-manager source contract to require `else if (_continueInProgress || _pendingInterruptedComposedExecutionOperation is not null)` instead of the obsolete literal `else if (_continueInProgress)`.
- Updated the assertion message so it describes both protected cases: a Paused breakpoint event arriving while Continue is still completing and a Paused interruption that must remain deferred until composed-operation cleanup can run safely.
- Kept the older breakpoint-manager requirements for `_deferredStopContextEvent`, `_continueInProgress`, and `RefreshDeferredStopContextIfNeeded()` intact; the contract is aligned with the rev27 implementation rather than weakened.
- No new automated test is added. The registry remains **135 checks**.

### Preserved - Rev27 production behavior

- `DebuggerViewModel` production source is unchanged from rev27, including exact composed-operation breakpoint ownership, target-event matching, interruption cleanup, deferred paused-context refresh, and execution-status reconciliation.
- PS5 plugin `0.1.0.rev36` is unchanged, including defensive restoration of active/staged software breakpoints before detach/disposal.
- Rev25 logical software-breakpoint event/Register/Call Stack stop reconciliation remains unchanged.
- Rev26 breakpoint-aware Step Over original-instruction capture remains unchanged.
- Core debugger contracts, Plugin SDK `2.15.0`, Mock `1.0.0.rev15`, scanner, Memory Viewer, Disassembler, Universal Export foundation, themes, status-bar layout, and modeless tool-window lifecycle are unchanged.

### Verification and documentation

- Recorded rev27's actual Windows result as **134/135 PASS** and marked rev27 superseded before live acceptance.
- Added `APP_0.1.7_REV28_SOURCE_REVIEW.md` documenting why the failure was a stale assertion and why no production correction is required.
- Added `APP_0.1.7_REV28_VERIFICATION.md`. After a clean Windows build and **135/135 PASS**, runtime verification resumes with the rev27 correction-sensitive Step Over/Step Out cleanup regression, controlled interrupted Run to Address, safe Detach/reconnect, successful Run to Address, and final cleanup regression.
- Updated README, current architecture/plugin references, and the full development action plan to `0.1.7.rev28`.
- Debugger **Integration, Export and Finalization** moves to `0.1.7.rev29` because rev28 is consumed by this corrective verifier revision.

### Version and compatibility

- Host application: `0.1.7.rev28`.
- Feature title: `Debugger Deferred Stop Verification Contract Fix`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev36`.
- Automated registry target: **135 checks**.
- Next planned feature revision after rev28 acceptance: `0.1.7.rev29 - Integration, Export and Finalization`.

## TeeKay87's Memory Engine 0.1.7.rev27 - Interrupted Operation and Safe Detach Breakpoint Cleanup

Revision 27 is a focused debugger cleanup correction built directly from the supplied `0.1.7.rev26` package after rev26 passed its Windows **133/133** automated gate and live-corrected breakpoint-aware Step Over. The live Step Over regression at `0xE9F74E call 0x1A3BC30` finished Paused at the expected fall-through `0xE9F753` and removed its temporary breakpoint. A clean natural-pause Step Out also reached its selected return address and left no visible temporary breakpoint behind.

The remaining blocker appeared during Run to Address. A Run-to operation targeting `0x81C15634` was interrupted first by a PS5 signal-10 stop at `0x81C1563B`. The target stop itself was not the requested Run-to breakpoint, but the operation-owned temporary Software/Execute breakpoint remained Enabled/Temporary in the manager. Detaching from that interrupted paused state was then followed by the game terminating. The signal and target termination are recorded as runtime observations only; this revision does not assign either event to ps5debug-NG. The client-side defects addressed here are the orphaned host-owned temporary breakpoint after an unrelated Paused event and the lack of an explicit client-side restore pass for still-active/staged software-breakpoint slots before teardown.

### Changed - Composed execution operation ownership

- Added explicit host state for the currently active Step Over, Step Out, or Run to Address composition and for an interrupted operation whose cleanup must wait until the current execution command leaves its busy section.
- Each composed execution operation now records the exact neutral breakpoint id used as its stop target, the requested address, the operation name, and whether the breakpoint was created temporarily by that operation or was an existing persistent breakpoint being reused.
- Run to Address resolves the exact temporary breakpoint record immediately after successful creation. If the created record cannot be resolved, the operation does not continue with ambiguous ownership.
- A Paused event now completes the active composition only when it is a neutral breakpoint event whose `TriggeredBreakpoint.Id` matches the exact target breakpoint id.
- Any other Paused event interrupts the active operation. This applies without platform-specific event inspection, so PauseRequested, signals/exceptions, hardware watchpoints, another breakpoint, and other neutral Paused stops can all terminate a composed operation safely.
- Existing persistent breakpoints reused as the Run-to target are never removed by interruption cleanup. Cleanup is restricted to breakpoints explicitly owned as temporary by the current composed operation.

### Changed - Interrupted temporary-breakpoint cleanup and deferred stop handling

- Extended the existing deferred stop-context mechanism rather than adding a second event pipeline.
- If an interrupting Paused event arrives while Continue or manual Pause is still completing, the event and pending operation cleanup are retained until `IsBusy` clears.
- Deferred interruption cleanup runs before the final paused-context refresh and removes the exact operation-owned temporary breakpoint through the neutral `IDebuggerBreakpointService`.
- If the backend has already consumed/removed the temporary record, the host treats the missing id as already retired and proceeds with the normal stop-context refresh.
- Manual Pause now releases deferred composed-operation cleanup in its completion path, providing a deterministic way to cancel an in-flight Run-to operation without leaving the temporary record visible.
- Continue-command cancellation/failure also retires an operation-owned temporary breakpoint instead of abandoning it.
- Attach setup, completed detach, ViewModel disposal, and stale-target invalidation clear active/pending composed-operation state together with the existing deferred stop state.
- The final status after an interruption describes the interrupted operation and cleanup result rather than leaving stale `...is running toward...` text visible.

### Changed - PS5 safe software-breakpoint teardown

- Advanced the PlayStation 5 plugin from `0.1.0.rev35` to `0.1.0.rev36`; Plugin API remains `2.15.0`.
- Added a teardown-only software-breakpoint restore pass before explicit PS5 debugger detach.
- The restore set is built from every managed software-breakpoint slot still marked backend-enabled plus every paused disable/removal already staged in `_pendingBreakpointDisables`; duplicate slot/address entries are collapsed.
- Each collected slot is explicitly disabled/restored through the existing ps5debug-NG software-breakpoint command before the normal backend detach request is sent.
- The debugger event channel remains open during the teardown-only restore pass because the already documented ps5debug-NG disable behavior can resume a paused target as a side effect. While `_detaching` is active, complete interrupt packets are drained from the callback socket but ignored for session state/event projection; the channel is closed after the detach attempt finishes.
- A failed pre-restore does not suppress the backend detach attempt. If both cleanup and detach fail, both failures are retained; if detach succeeds but a pre-restore failed, the session is still placed in Detached state and the cleanup error is returned to the caller instead of being hidden.
- Session disposal uses the same restore-before-detach logic on a best-effort basis when explicit detach did not already complete.
- Successful restore updates client-side backend-enabled/pending-disable bookkeeping before final teardown state is cleared.
- Hardware watchpoint teardown remains unchanged; the correction is specific to software execute breakpoints and does not alter the public breakpoint model.

### Preserved - Previously accepted debugger behavior

- Rev25 logical software-breakpoint event/IP/Register/Call Stack frame-zero reconciliation is unchanged.
- Rev25's Step Into handling from a logical software-breakpoint stop remains unchanged and still avoids a second native backend step.
- Rev26 original-instruction capture and breakpoint-aware Step Over classification are preserved unchanged.
- Step Over still composes calls by running to the original call fall-through address, Step Out still uses the selected neutral frame return address, and Run to Address remains a host-composed neutral operation.
- Normal paused PS5 breakpoint Disable/Remove behavior remains staged; rev27's explicit restore pass is limited to debugger teardown and does not make ordinary paused breakpoint management send the known state-changing backend disable command immediately.
- Core debugger contracts, Plugin SDK `2.15.0`, Mock `1.0.0.rev15`, scanner/export behavior, Memory Viewer, Disassembler, status-bar layout, breakpoint/watchpoint legality, register safety, and modeless tool-window ownership are not redesigned.

### Verification coverage

- Added **Debugger interrupted composed-operation cleanup source contract** covering active/pending operation state, exact target-breakpoint identity, temporary-only ownership cleanup, busy/deferred event retention, manual-Pause release of deferred cleanup, neutral breakpoint removal, and lifecycle clearing.
- Added **PS5 debugger safe detach restores software breakpoints** protocol coverage. The test creates one still-active software breakpoint plus one temporary breakpoint whose removal is staged while Paused, then verifies that both backend slots receive explicit disable/restore requests before detach completes.
- Updated the PS5 plugin metadata expectation to `0.1.0.rev36`.
- The automated registry advances from **133 to 135 checks**.
- Rev26's verification record is updated to preserve the actual **133/133 PASS**, live Step Over/Step Out acceptance, Run-to interruption failure, orphan temporary breakpoint evidence, and subsequent detach/target-termination observation.
- Added dedicated rev27 source-review and verification documents. After a clean Windows build and **135/135 PASS**, verification performs a short Step Over/Step Out cleanup regression, a controlled interrupted Run-to using manual Pause, detach/reconnect from that cleanup state, a normal successful Run to Address, and final cleanup/tool-window regression.

### Documentation and development order

- Updated README current-state/version information, PS5 plugin documentation, ps5debug-NG protocol mapping, Debugger architecture, Plugin SDK foundation, Universal Export current-version reference, and the full development action plan for rev27.
- The development plan records rev26 as superseded after partial live acceptance while retaining its successful Step Over and Step Out evidence.
- No external-backend cause is assigned to the observed signal-10 stop or subsequent target termination because the available runtime evidence does not establish one. Source review did, however, establish a separate ps5debug-NG teardown defect: `debug_full_teardown()` stops software-breakpoint restoration at the first empty indexed slot. Added `docs/bug-reports/ps5debug-ng-debugger-detach-stops-breakpoint-restore-at-first-empty-slot.md` with the exact source sequence and sparse-slot reproduction. The existing paused software-breakpoint-disable report remains relevant to why normal paused cleanup is staged and why teardown keeps the event channel alive only as a drain until the detach attempt has finished.
- Debugger **Integration, Export and Finalization** moves to `0.1.7.rev28` because rev27 is consumed by this corrective cleanup work.

### Version and compatibility

- Host application: `0.1.7.rev27`.
- Feature title: `Interrupted Operation and Safe Detach Breakpoint Cleanup`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: `0.1.0.rev36`.
- Automated registry target: **135 checks**.
- Next planned feature revision after rev27 acceptance: `0.1.7.rev28 - Integration, Export and Finalization`.


## TeeKay87's Memory Engine 0.1.7.rev26 - Breakpoint-Aware Step Over Fix

Revision 26 is a focused host-side stepping correction built directly from the supplied `0.1.7.rev25` package after rev25 passed its Windows **132/132** automated gate, MainWindow status-bar alignment checks, focused Mock regression, and complete live-PS5 logical software-breakpoint stop-context gate. Verification then reached the first live Step Over case and exposed one remaining interaction between host-composed stepping and ps5debug-NG's software-breakpoint implementation.

The accepted rev25 logical stop at `0xE9F74E` correctly showed the original `call 0x1A3BC30` address in the breakpoint event, displayed IP, Registers `RIP`, and Call Stack frame zero. Pressing **Step Over** nevertheless produced a Step Into completion at `0x1A3BC30` instead of the expected fall-through `0xE9F753`. A disassembly export captured while that persistent software breakpoint was armed showed the physical target byte at `0xE9F74E` as `CC / int3`, which established the host-side failure mode: rev25's generic Step Over composition re-read target memory after ps5debug-NG had rearmed `INT3`, so the host no longer saw the original instruction as a `Call` and selected its normal non-call native Step Into path.

### Changed - Breakpoint-aware Step Over instruction resolution

- Added a session-local host cache that retains the original neutral `DisassembledInstruction` for Software/Execute breakpoint addresses.
- Before a Software/Execute breakpoint is installed, the Debugger uses the existing target-safe Disassembler path to request a small context beginning at the breakpoint address and captures the valid instruction at that exact address.
- The decoded instruction is committed to the cache only after the breakpoint itself is successfully added. Failed breakpoint creation therefore cannot leave behind a false installed-breakpoint instruction record.
- If a fresh successful Software/Execute add cannot capture its original instruction, any older cached instruction at that address is discarded rather than being reused for the new breakpoint.
- The captured original instruction is retained across ordinary enable/disable state changes within the same debugger session. Rev26 intentionally does not re-read the instruction during re-enable because the existing PS5 paused-disable workaround may leave the physical `INT3` armed until the next explicit Continue; a re-read at that point could overwrite valid original metadata with the patched byte.
- Original-instruction capture is deliberately optional. Failure to obtain the extra Disassembler context does not reject or alter otherwise valid breakpoint creation; the existing breakpoint behavior remains available and Step Over can still fall back to its ordinary live-disassembly path when no cached instruction exists.

### Changed - Logical breakpoint stop matching

- The Debugger now records the current logical software-breakpoint stop address and optional thread id from the neutral `DebuggerEvent.TriggeredBreakpoint` context.
- Cached original instructions are eligible for Step Over only when the current instruction pointer matches that logical Software/Execute breakpoint stop and the selected thread matches the event thread when one is present.
- Step Over now checks that breakpoint-aware original instruction **before** reading live target disassembly. A matching cached `Call` therefore retains its original length/flow-control classification even while the backend has rearmed the physical address as `INT3`.
- Non-breakpoint pauses, other breakpoint kinds/accesses, other threads, and ordinary paused instruction contexts continue to use the verified live Disassembler lookup. The cache does not replace general instruction resolution.
- Transitions away from Paused clear the current logical-stop association while preserving session breakpoint instruction snapshots for later hits.
- Debugger disposal, new attach setup, detach, and stale-target invalidation clear both the cache and logical-stop association so data cannot cross debugger/target lifetimes.

### Preserved - Step Over / Run-to composition

- The existing platform-neutral Step Over algorithm is unchanged after instruction resolution: non-calls use native Step Into, while a decoded `Call` calculates `instruction.Address + instruction.Length` and composes Step Over through the existing temporary Run-to breakpoint path.
- The expected live regression case remains `0xE9F74E call 0x1A3BC30` -> temporary fall-through target `0xE9F753`.
- Temporary software-breakpoint instruction snapshots are retained for the current debugger session even after the one-shot record is removed on hit. This is intentional because ps5debug-NG may already have rearmed the physical `INT3` while the client is presenting that logical stop; a subsequent Step Over at such a stop still needs the original decoded instruction.
- Step Out and Run to Address are not redesigned by this revision. Their remaining live acceptance resumes after the corrected Step Over regression passes.

### Preserved - Platform and public contracts

- No Core debugger model, Plugin SDK contract, public capability, or platform-specific wire protocol is changed.
- PS5 plugin remains `0.1.0.rev35` and continues to own the rev25 logical software-breakpoint packet snapshot/transparent-Step-Into reconciliation.
- Plugin API remains `2.15.0`.
- Mock plugin remains `1.0.0.rev15`.
- MainWindow status-bar alignment, rev24 selector/splitter presentation, Call Stack transport, register handling, native Step Into, breakpoint/watchpoint legality, modeless tool-window cleanup, scanner, Memory Viewer, Disassembler provider, and existing export infrastructure are unchanged.

### Verification coverage

- Added **Debugger breakpoint-aware Step Over source contract**.
- The new contract verifies that the original Software/Execute instruction is captured before backend breakpoint installation, stored only after successful add, and preferred by Step Over before breakpoint-patched live disassembly when the neutral logical stop matches.
- The contract also verifies that matching is driven by neutral `TriggeredBreakpoint` Software/Execute context and that cached state participates in debugger lifecycle cleanup.
- The automated registry advances from **132 to 133 checks**.
- Rev25's verification record is updated with the actual **132/132 PASS**, status-bar PASS, Mock PASS, live logical-stop/Step-Into PASS, and the Gate E Step Over failure at `0xE9F74E`.
- Added dedicated rev26 source-review and verification documents. After a clean Windows build and **133/133 PASS**, live verification resumes directly at the failed Step Over case rather than repeating the already accepted rev25 MainWindow, Mock, and logical-stop gates.
- After Step Over passes, the remaining live Step Out, Run to Address, and cleanup/reconnect gates continue from the rev25 plan.

### Documentation and development order

- Updated the README to describe the rev26 candidate and breakpoint-aware host stepping behavior without changing unrelated usage documentation.
- Currentized the README verification section to the 133-check rev26 registry/current verification documents and corrected the current PS5 plugin reference there to `0.1.0.rev35`.
- Corrected stale current-version references in README/architecture/PS5 disassembly documentation so the application, Plugin API, and PS5 plugin current metadata consistently report `0.1.7.rev26`, `2.15.0`, and `0.1.0.rev35` while preserving historical revision descriptions.
- Updated the Debugger architecture, PS5 plugin documentation, and full development action plan to record why host Step Over must preserve pre-breakpoint instruction metadata while PS5 remains responsible only for platform-private breakpoint transport/stop reconciliation.
- Rev25 is recorded as superseded after Gate E Step Over failure; all earlier accepted rev25 gates remain explicit carried-forward evidence.
- The Debugger **Integration, Export and Finalization** milestone moves from rev26 to **rev27** because rev26 is consumed by this corrective revision under the project's version/revision rules.

### Version and compatibility

- Host application: `0.1.7.rev26`.
- Feature title: `Breakpoint-Aware Step Over Fix`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev35`.
- Automated registry target: **133 checks**.
- Next planned feature revision after rev26 acceptance: `0.1.7.rev27 - Integration, Export and Finalization`.

## TeeKay87's Memory Engine 0.1.7.rev25 - PS5 Breakpoint Stop Context and Status Alignment Fix

Revision 25 is a focused corrective revision built directly from the supplied `0.1.7.rev24` package after the rev24 runtime cycle completed the redesigned Debugger workspace acceptance and the complete Mock Call Stack/stepping cycle, then exposed a live PS5 software-breakpoint stop-context mismatch in ps5debug-NG. The rev24 Windows registry returned **129/130** because one source contract still searched for the removed TabItem-era `Text="Breakpoints / Watchpoints"` presentation instead of the new selector Button `Content`. The application behavior behind that single source-contract failure was already runtime-accepted.

Live PS5 verification passed server-side Call Stack population, thread switching, frame navigation, and selected-thread native Step Into. The later Run to Address / software-breakpoint isolation sequence established that ps5debug-NG reports a logical software-breakpoint hit at the requested instruction while transparently restoring and single-stepping that instruction before the client receives the 1184-byte interrupt packet. A breakpoint at `0xE9F747` reported `0xE9F747` in the event but a subsequent live GETREGS returned `0xE9F74C`; a breakpoint at the five-byte `call` on `0xE9F74E` reported `0xE9F74E` but live RIP had already entered the callee at `0x1A3BC30`. This is not call-specific and cannot be treated as a host Run-to-only problem.

### PS5 logical software-breakpoint stop context

- Advanced the PlayStation 5 plugin from `0.1.0.rev34` to `0.1.0.rev35`; Plugin API remains `2.15.0`.
- Added explicit interrupt-packet floating-point register offset metadata alongside the existing 1184-byte debugger interrupt layout.
- When a managed software breakpoint is attributed to an incoming interrupt, the PS5 session now preserves the packet's 176-byte general-register block and 832-byte floating-point block as the authoritative **logical breakpoint stop snapshot** for that thread and breakpoint address.
- Register inspection for the matching stopped thread uses that packet snapshot instead of immediately replacing it with ps5debug-NG's already advanced live GETREGS state. Other threads and ordinary pause/step stops continue to use the existing live register path.
- Call Stack frame zero for the matching stopped thread now starts from the logical breakpoint packet RIP/RBP/RSP, keeping the visible frame consistent with the breakpoint event instead of mixing a pre-instruction event with post-instruction live registers.
- Because ps5debug-NG has already transparently executed the restored instruction before delivering the breakpoint event, **Step Into from a logical software-breakpoint stop does not issue a second native step**. The plugin reads the backend's current post-step general registers, consumes the logical snapshot, and publishes one neutral Resumed/StepCompleted transition ending at the backend's already reached RIP.
- The logical snapshot is cleared on explicit Pause, Continue, Detach, a new interrupt, and after the transparent breakpoint step has been consumed so stale pre-instruction state cannot leak into a later debugger context.
- This correction deliberately does not pretend to roll back memory or other side effects already performed by the backend's transparent step. It reconciles the host-visible debugger stop and composed operation semantics with the packet ps5debug-NG actually sends.

### Debugger execution-status race

- Corrected a host-side race where an awaited Continue, native Step Into, or Run to Address command could finish after a fast asynchronous stop event and then overwrite the newer Paused/breakpoint/StepCompleted message with stale text such as `Target running.` or `Run to Address is running toward ...`.
- Added one post-execution state reconciliation path that re-reads the coordinator's final state after the awaited operation and reapplies the final command state.
- When a stop event arrived while the command was busy, its deferred stop-context message remains authoritative. A Paused result is no longer overwritten by the stale Running text produced before the interrupt was processed.

### MainWindow status-bar alignment

- Updated the permanent bottom status bar so every visible item shares the same vertical centerline within the horizontal status row.
- Connection state, general status text, error text, scan status text, the existing progress/elapsed group, and the right-side `AppInfo.DisplayVersion` value now all use explicit centered vertical alignment.
- Column order, spacing, bindings, progress behavior, elapsed-time behavior, and the rule that version/revision appears only at the status bar's right edge are unchanged.

### Verification contracts and fixtures

- Corrected **Debugger breakpoint manager source contract** to validate the rev24 selector Button's `Content="Breakpoints / Watchpoints"` instead of the removed TabItem-era `Text` attribute.
- Added **PS5 debugger logical software-breakpoint stop context** coverage. The test server now places deterministic general/FPU state in the same interrupt-packet offsets used by ps5debug-NG so the regression verifies event-address/register/call-frame agreement and verifies that Step Into from that logical stop does not send a second native step command.
- Added **Main status bar content alignment source contract** covering the connection badge, status/error/scan text, progress group, and version field.
- Extended the existing Run to Address source contract so a fast stop cannot regress to stale running-status presentation.
- The automated registry therefore advances from 130 to **132 checks**.

### External backend documentation

- Added `docs/bug-reports/ps5debug-ng-software-breakpoint-event-and-live-register-state-diverge.md` with the two live reproductions, the exact current ps5debug-NG restore/rewind/PT_STEP/wait/rearm/send sequence, expected/actual behavior, and impact on debugger clients.
- The previously documented ps5debug-NG behavior where disabling a software breakpoint while paused resumes the target is unchanged; the existing plugin-side staged cleanup remains in place and this revision does not broaden that workaround.

### Documentation and development order

- Updated the README, Debugger architecture, Plugin SDK foundation, PS5 plugin/protocol documentation, Main Workspace documentation, rev24 verification record, and full development action plan to the rev25 state.
- Rev24 is recorded with its actual **129/130** automated result, accepted selector/splitter/window behavior, complete Mock Call Stack/stepping PASS, partial live-PS5 PASS, and the breakpoint stop-context blocker found during live verification.
- Added dedicated rev25 source-review and verification documents. A clean Windows WPF build, **132/132** automated checks, focused live PS5 logical-breakpoint/Run-to/Step Over/Step Out regression, and carried-forward cleanup/lifecycle regression are required before rev25 can be accepted.
- The Debugger **Integration, Export and Finalization** milestone moves from rev25 to **rev26** because this corrective revision consumes rev25 under the project revision rules.

### Version and compatibility

- Host application: `0.1.7.rev25`.
- Feature title: `PS5 Breakpoint Stop Context and Status Alignment Fix`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: `0.1.0.rev35`.
- Automated registry target: **132 checks**.
- Next planned feature revision after rev25 acceptance: `0.1.7.rev26 - Integration, Export and Finalization`.

## TeeKay87's Memory Engine 0.1.7.rev24 - Debugger Mode Switcher Layout Refresh

### Summary

Revision 24 is a focused Debugger workspace presentation correction built directly from the supplied `0.1.7.rev23` package after Windows runtime verification completed the **130/130** automated gate but confirmed that the WPF tab-header right edge still rendered as abruptly clipped. The previously verified MainWindow-driven tool-window cleanup continued to work correctly. Rather than adding another TabItem border workaround, rev24 removes the Debugger's active `TabControl`/`TabItem` presentation entirely and replaces it with two compact application-styled selector buttons that switch the same existing Breakpoints / Watchpoints and Call Stack views.

The layout refresh also removes the duplicated inner section titles and their item-count labels, moves the former title-row actions down to the existing action rows, and increases the Debugger's default window size slightly while keeping it smaller than MainWindow. Call Stack data, breakpoint/watchpoint behavior, stepping, target/session safety, tool-window lifecycle, Core, Plugin SDK, Mock, and PS5 backend behavior are unchanged.

### Changed - Debugger Workspace Mode Switcher

- Removed the Debugger's `TabControl` and both `TabItem` headers from the upper-right workspace.
- Added compact **Breakpoints / Watchpoints** and **Call Stack** selector buttons above the shared content area.
- Added ViewModel-owned selection state and commands so only the selected workspace is visible; Breakpoints / Watchpoints remains the default when both capabilities exist, while a call-stack-only backend falls back to Call Stack automatically.
- Added shared `DebuggerWorkspaceSwitchButtonStyle` in `ButtonStyles.xaml`, derived from the existing Secondary button style so the selectors reuse normal application hover, pressed, disabled, focus, and theme behavior.
- Selector buttons use a deliberate compact `28`-unit height and `12`-point label size. The selected selector receives a two-unit `AccentBrush` outline instead of a separate tab chrome implementation.
- Added small selector-specific derived styles for the two selection-state bindings without duplicating the shared button template.
- Removed the now-unused `WorkspaceTabControlStyle` and `WorkspaceTabItemStyle` from active shared resources. Historical rev18-rev23 verification documents continue to describe the superseded tab experiments.

### Changed - Upper Workspace Content Layout

- Removed the duplicated **Breakpoints / Watchpoints** and **Call Stack** title rows inside their content surfaces.
- Removed the `Breakpoints.Count` and `CallFrames.Count` labels that existed only beside those removed inner titles.
- Breakpoints / Watchpoints keeps Enable, Disable, Remove, Remove All, and Disassembler left-aligned on the bottom action row.
- The former title-row **Add...** and **Refresh** actions are now right-aligned on that same bottom row.
- Call Stack keeps Disassembler and Memory Viewer left-aligned on its bottom action row.
- The former title-row Call Stack **Refresh** action is now right-aligned on that same row.
- SP, FP, and Return details remain directly below the Call Stack table and above its action row.
- Existing capability gating, command bindings, DataGrid columns, selection bindings, double-click navigation, and splitters are preserved.

### Changed - Debugger Default Window Size

- Increased the Debugger default size from `1120 x 720` to `1240 x 780` device-independent units.
- MainWindow remains larger at `1460 x 880`; Debugger minimum size remains `820 x 520`.
- The change affects only the initial Debugger workspace size and does not alter splitter ratios or minimum-pane rules.

### Preserved - Verified Window Lifecycle

- The rev21/rev23 MainWindow shutdown path is unchanged. Runtime verification already confirmed that closing MainWindow closes the open modeless tools through their normal cleanup paths without the previous WPF close re-entry exception.
- `MainWindow.xaml.cs` and `ToolWindowManager.cs` are unchanged.
- Debugger, Disassembler, and Memory Viewer retain independent modeless z-order after initial centered placement.

### Changed - Verification Coverage

- The automated registry remains **130** top-level checks. The three superseded tab-presentation source contracts are rewritten in place as mode-switcher, selected-button-theme, and panel-layout contracts.
- Rev24 source checks reject reintroduction of Debugger `TabControl`/`TabItem`, verify ViewModel-owned exclusive workspace visibility, verify the compact shared selector-button styles and accent selected outline, ensure the removed inner titles/counts stay absent, check the new bottom-row action placement, and enforce the `1240 x 780` default size.
- Rev24 Gate A requires a clean Windows WPF build and **130/130 PASS**. Runtime acceptance starts with the new selector/layout behavior in Dimmed, Dark, and Light before the carried-forward Call Stack/Stepping gates resume.

### Documentation

- Recorded rev23's **130/130 PASS** and the continued right-edge runtime failure without relabeling rev23 verified.
- Updated README, the full development action plan, Debugger architecture, shared button/control-metric guidance, theme documentation, Plugin SDK/current-host status, and current PS5 host references.
- Added dedicated rev24 source-review and verification documents under `docs/testing/`.
- The planned Debugger **Integration, Export and Finalization** milestone moves from rev24 to **rev25** because this corrective revision consumes rev24 under the project revision rules.

### Versioning

- Host application: `0.1.7.rev24`.
- Feature title: `Debugger Mode Switcher Layout Refresh`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev34`.
- Automated registry: unchanged at `130` checks.
- Next planned feature revision after rev24 acceptance: `0.1.7.rev25 - Integration, Export and Finalization`.

## TeeKay87's Memory Engine 0.1.7.rev23 - Debugger Tab Header Rendering Fix

### Summary

Revision 23 is a focused WPF presentation correction built directly from the `0.1.7.rev22` package after Windows runtime inspection showed that the reserved-column tab template introduced a larger visual regression instead of fixing the original edge defect. The **Breakpoints / Watchpoints** and **Call Stack** header text disappeared, and the selected Call Stack header rendered as a solid accent-colored block. The previously accepted MainWindow/tool-window shutdown behavior continued to work and is not changed here.

Rev23 removes the separate right-edge visual and the two-column header layout entirely. The shared `WorkspaceTabItemStyle` returns to a single application-owned `Border`/`ContentPresenter` structure so header measurement, text rendering, selected background, and theme state handling follow the last known-good themed layout. The border's right side is widened to two device-independent units while the other sides remain one unit. This keeps the visible right stroke inside the rendered header even if the outermost device pixel is lost at the WPF/TabPanel boundary, without introducing another overlay or layout column. Core, Plugin SDK, Mock, PS5, debugger transport, Call Stack/Call Frames, stepping, breakpoints/watchpoints, registers, Memory Viewer, Disassembler, window lifecycle, and all platform-specific behavior are unchanged.

### Fixed - Workspace Tab Header Rendering

- Removed the rev22 two-column `Grid` from `WorkspaceTabItemStyle`.
- Removed `TabRightEdge`, its templated-parent `BorderBrush` background binding, and the separate right-edge z-order path.
- Restored the header to one `TabBorder` containing the normal `ContentPresenter`, which keeps the header text in the same measured visual that owns its background and border.
- Restored hover, selected, and disabled border-color triggers to target `TabBorder` directly instead of routing state through a separate edge visual.
- Uses `BorderThickness="1,1,2,1"`: left/top/bottom remain one unit and only the right stroke is widened.
- Keeps `UseLayoutRounding=True` and `SnapsToDevicePixels=True` on the rendered border.
- Preserves the existing theme resources, padding, inter-tab spacing, rounded top corners, and Debugger content layout.

### Preserved - Window Lifecycle and Debugger Functionality

- Rev21 runtime verification already confirmed that closing MainWindow closes open tool windows through their normal cleanup paths. Rev23 does not modify `MainWindow.xaml.cs` or `ToolWindowManager.cs`.
- Debugger, Disassembler, and Memory Viewer remain independent modeless windows after initial centered placement.
- The rev17 Call Stack/Call Frames and Step Into/Step Over/Step Out/Run-to implementation is unchanged.
- Core, Plugin SDK, Mock `1.0.0.rev15`, and PS5 `0.1.0.rev34` production code is unchanged.

### Changed - Verification Coverage

- The automated registry remains **130** top-level checks. The two existing workspace-tab source contracts are strengthened rather than adding duplicate test registrations.
- The complete-tab-border contract now requires the single-border template, visible header `ContentPresenter`, widened right border, layout rounding, and explicit absence of the failed rev22 column/edge structure.
- The selected-tab contract now requires theme state to target `TabBorder` directly and rejects the rev22 path that used the current border brush as a separate background visual.
- Rev23 Gate A requires a clean Windows WPF build and **130/130 PASS**.
- Runtime Gate B first verifies readable tab labels plus complete selected/unselected right edges in Dimmed, Dark, and Light. Only after that visual gate passes should the carried-forward Call Stack/Stepping tests resume.

### Documentation

- Recorded rev22 as superseded after the observed Windows tab-header rendering failure.
- Updated README, the full development action plan, Debugger architecture, theme guidance, Main Workspace lifecycle note, Plugin SDK/current-host status, PS5 current-host references, and the carried-forward rev17 verification pointer.
- Added dedicated rev23 source-review and verification documents under `docs/testing/`.
- The planned Debugger **Integration, Export and Finalization** milestone moves from rev23 to **rev24** because this corrective code revision consumes rev23 under the project revision rules.

### Versioning

- Host application: `0.1.7.rev23`.
- Feature title: `Debugger Tab Header Rendering Fix`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev34`.
- Automated registry: unchanged at `130` checks.
- Next planned feature revision after rev23 acceptance: `0.1.7.rev24 - Integration, Export and Finalization`.

## TeeKay87's Memory Engine 0.1.7.rev22 - Debugger Tab Right Edge Layout Fix

### Summary

Revision 22 is a narrowly scoped WPF presentation correction built directly from the `0.1.7.rev21` candidate after the clean Windows automated gate completed at **130/130 PASS**. Runtime verification confirmed that the rev20/rev21 MainWindow shutdown sequence now closes the open tool windows cleanly, but the reusable **Breakpoints / Watchpoints** and **Call Stack** tab headers still rendered without their right vertical edge. The previous rev20 strategy placed an overlay edge at the right alignment boundary of an unconstrained template grid; although the source contract proved that the visual existed, Windows runtime rendering still did not allocate a dedicated layout column for it.

Rev22 changes only the reusable WPF `WorkspaceTabItemStyle`, its automated source contracts, host version metadata, and documentation. The verified MainWindow/tool-window cleanup implementation is preserved unchanged. Core, Plugin SDK, Mock, PS5, debugger transport, Call Stack/Call Frames, stepping, breakpoints/watchpoints, registers, Memory Viewer, Disassembler, and all platform-specific behavior are unchanged.

### Fixed - Workspace Tab Right Edge Layout

- Replaced the unconstrained overlay placement used by the rev20/rev21 `TabRightEdge` with an explicit two-column header-template layout.
- The first column owns the normal header width and the second column is a dedicated one-unit right-edge column. Because that column participates in WPF measure/arrange, the right-edge visual no longer depends on `HorizontalAlignment=Right` at the same boundary that was disappearing at runtime.
- `TabBorder` spans both columns and deliberately draws only its left, top, and bottom edges (`1,1,0,1`). The dedicated `TabRightEdge` is therefore the sole right-hand stroke instead of competing with or depending on a clipped outer Border stroke.
- `TabRightEdge` is placed in the fixed right-edge column and rendered above the background through `Panel.ZIndex=2`.
- The explicit edge binds directly to the templated `TabItem.BorderBrush`, so Normal, Hover, Selected, and Disabled states continue to use the existing Light/Dimmed/Dark theme resources.
- `UseLayoutRounding` is enabled on the header root so the dedicated one-unit column is arranged consistently at fractional display scaling.
- Existing tab text, external four-unit inter-tab spacing, rounded top corners, content layout, Debugger bindings, and capability behavior are unchanged.

### Preserved - Window Lifecycle Fixes

- Rev21's Windows runtime check confirmed that closing MainWindow now closes the open tool windows as intended without the previous WPF close re-entry exception.
- `MainWindow.xaml.cs` is unchanged in rev22, including the dispatcher-posted guarded final close and explicit discard of the returned `DispatcherOperation`.
- `ToolWindowManager.cs` is unchanged. Debugger asynchronous cleanup/detach, synchronous tool disposal, normal modeless window closure, and independent desktop z-order behavior remain on the rev19-rev21 implementation.
- No modal-dialog ownership behavior is changed.

### Changed - Verification Coverage

- The automated registry remains **130** checks because rev22 strengthens the two existing workspace-tab border contracts rather than adding a duplicate top-level test.
- The complete-tab-border source contract now requires an explicit one-unit layout column, a spanning background/border, and removal of the outer right Border stroke.
- The selected-tab right-edge source contract now requires the edge to occupy that dedicated column, bind directly to the templated parent border brush, and render above the tab background.
- Rev22 Gate A requires a clean Windows application build and **130/130 PASS**.
- Runtime acceptance starts with the exact failure from rev21: selected and unselected right edges in Dimmed, Dark, and Light. Window shutdown is rechecked as a regression only; after those host gates pass, the carried-forward Call Stack/Stepping verification resumes.

### Documentation

- Recorded rev21's **130/130 PASS**, successful tested MainWindow/tool-window shutdown behavior, and failed tab-right-edge runtime gate without relabeling rev21 as verified.
- Updated README, the full development action plan, Debugger architecture, theme documentation, Main Workspace lifecycle notes, Plugin SDK/current-host status, and PS5 current-host references for rev22.
- Added dedicated rev22 source-review and verification documents under `docs/testing/`.
- The planned Debugger **Integration, Export and Finalization** milestone moves from rev22 to **rev23** because this corrective code revision consumes rev22 under the project revision rules.

### Versioning

- Host application: `0.1.7.rev22`.
- Feature title: `Debugger Tab Right Edge Layout Fix`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev34`.
- Automated registry: unchanged at `130` checks.
- Next planned feature revision after rev22 acceptance: `0.1.7.rev23 - Integration, Export and Finalization`.

## TeeKay87's Memory Engine 0.1.7.rev21 - MainWindow Shutdown Compile Fix

### Summary

Revision 21 is a narrow corrective host revision built directly from the `0.1.7.rev20` package after its first Windows build exposed one warnings-as-errors compiler failure in the new MainWindow shutdown path. The failure is `CS4014` on the `Dispatcher.BeginInvoke(new Action(Close))` call in `MainWindow.xaml.cs`: WPF's `DispatcherOperation` is awaitable, so discarding the returned operation implicitly triggers the compiler warning, and the repository-wide `TreatWarningsAsErrors` policy converts that warning into a build error.

The same Visual Studio Error List also showed XAML designer/type-resolution errors such as `XLS0414`, `XDG0008`, `XDG0024`, `XDG0006`, and `XDG0010` in `MainWindow.xaml`. Source review found no corresponding removal or rename of `UiMetrics`, `ProportionalGridSplitter`, or `TextBoxInputFilter`; those diagnostics appeared alongside the failed application build and are treated as downstream designer resolution fallout until the clean Windows Gate A confirms otherwise.

Rev21 changes only the host application's acknowledgement of the dispatcher operation and the associated source-verification/documentation contracts. It preserves rev20's explicit in-bounds workspace-tab right-edge implementation unchanged so that the visual fix can receive its first runtime verification after the application builds successfully. Core, Plugin SDK, Mock, PS5, debugger transport, Call Stack, stepping, breakpoints/watchpoints, registers, Memory Viewer, and Disassembler behavior are unchanged.

### Fixed - Warnings-as-Errors Build Failure

- Changed the final queued MainWindow close from `Dispatcher.BeginInvoke(new Action(Close));` to `_ = Dispatcher.BeginInvoke(new Action(Close));`.
- The explicit discard documents that the returned `DispatcherOperation` is intentionally fire-and-forget. The close callback must remain queued rather than awaited from the shutdown helper because the purpose of the rev20 sequencing fix is to let the original WPF `OnClosing` callback unwind before the final `Close()` request executes.
- No exception path is lost by this change: `CompleteToolWindowShutdownAsync()` still catches cleanup failures, sets the shutdown-completed guard in `finally`, and only then queues the final close.
- No new `using` directive is required; all namespaces used by `MainWindow.xaml.cs` remain complete.

### Preserved - Rev20 Runtime Corrections

- The rev20 `WorkspaceTabItemStyle` and its explicit in-bounds `TabRightEdge` are unchanged. Rev20 never reached runtime because of the compile failure, so rev21 carries that exact visual correction forward for verification rather than introducing another untested tab geometry change.
- Independent modeless z-order for Debugger, Disassembler, and Memory Viewer is unchanged.
- `ToolWindowManager.CloseAllAsync()` cleanup order is unchanged: tools are disabled, asynchronous Debugger cleanup is awaited, synchronous tool cleanup is run where applicable, and each tracked window is closed through its normal WPF close path.
- The final MainWindow close remains dispatcher-posted and guarded against shutdown re-entry.

### Changed - Verification Coverage

- Strengthened the existing **Main-window shutdown re-entry guard source contract** so it now requires the dispatcher call to be explicitly discarded as `_ = Dispatcher.BeginInvoke(new Action(Close));`. This would reject the exact source form that produced `CS4014` in rev20.
- No redundant top-level verification case is added; the registry remains **130** unique checks because the existing shutdown source contract is the correct owner for this requirement.
- Rev21 Gate A requires a clean Windows application build and **130/130 PASS** before any runtime UI/shutdown acceptance resumes.
- After Gate A, rev21 first re-tests the rev20 workspace-tab edge and MainWindow cleanup/z-order changes, then resumes the carried-forward Call Stack/Stepping runtime verification.

### Documentation

- Recorded the rev20 Windows build failure in the historical rev20 source-review/verification documents without marking rev20 as verified.
- Updated README, the full development action plan, Debugger architecture, Main Workspace lifecycle documentation, theme guidance, Plugin SDK status, and current PS5 status references to rev21.
- Added dedicated rev21 source-review and verification documents under `docs/testing/`.
- The planned Debugger **Integration, Export and Finalization** milestone moves from rev21 to **rev22** because this compile-fix revision consumes rev21 under the project revision rules.

### Versioning

- Host application: `0.1.7.rev21`.
- Feature title: `MainWindow Shutdown Compile Fix`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev34`.
- Next planned feature revision after rev21 acceptance: `0.1.7.rev22 - Integration, Export and Finalization`.

## TeeKay87's Memory Engine 0.1.7.rev20 - Debugger Tab Border and Shutdown Re-entry Fixes

### Summary

Revision 20 is a focused corrective host/UI revision built directly from the `0.1.7.rev19` candidate after its Windows automated gate completed at **128/128 PASS**. The first rev19 runtime inspection showed that the previous one-unit inset did not fully solve the selected/last workspace-tab header clipping: the right vertical edge of the selected **Call Stack** header was still absent. The same runtime pass exposed a WPF close re-entry failure when MainWindow was closed while Debugger remained open: `InvalidOperationException` reported that `Close` could not be called while a `Window` was already closing.

Both failures are confined to host WPF presentation/lifecycle code. Rev20 does not change Core, Plugin SDK, Mock, PS5, debugger transport, Call Stack data, stepping semantics, breakpoints/watchpoints, registers, Memory Viewer data, or Disassembler data. Plugin API remains `2.15.0`, Mock remains `1.0.0.rev15`, and PS5 remains `0.1.0.rev34`.

### Fixed - Selected Workspace Tab Right Edge

- Reworked only the reusable `WorkspaceTabItemStyle` template in `Resources/Styles/ControlStyles.xaml`; the shared `WorkspaceTabControlStyle`, Debugger tab content, dimensions, bindings, and capability behavior remain unchanged.
- Replaced the rev19 single `TabBorder` margin workaround with an in-bounds template grid that reserves one unit at the right side of the header.
- Added a dedicated one-unit `TabRightEdge` visual inside that grid. This guarantees that the final right vertical edge is rendered inside the TabItem allocation instead of depending on the outer border stroke that WPF clipped on the selected/last header.
- The explicit edge follows the TabItem's template-bound `BorderBrush`, so normal, hover, selected, and disabled states continue to use the existing theme brushes. The selected state still uses `AccentBrush`; no literal color is introduced.
- Existing rounded top corners, header padding, external spacing, Light/Dimmed/Dark live-theme behavior, and Breakpoints / Watchpoints + Call Stack layout are preserved.

### Fixed - MainWindow Shutdown Close Re-entry

- Root cause: rev19 used an `async void OnClosing` override and called `Close()` directly in its `finally` block after `CloseAllAsync()`. When all tracked cleanup completed synchronously or without yielding long enough to unwind the original WPF close stack, the second `Close()` re-entered the same MainWindow while WPF still considered the first close request active. WPF correctly raised `InvalidOperationException`.
- `MainWindow.OnClosing` is now synchronous. It still cancels the first close request, preserves the existing in-progress/completed guards, disables MainWindow, and starts the established asynchronous tracked-tool cleanup.
- Asynchronous work moved into `CompleteToolWindowShutdownAsync()`, which still awaits `ToolWindowManager.CloseAllAsync()` and retains the existing error tracing.
- The final MainWindow close is now posted with `Dispatcher.BeginInvoke(new Action(Close))` after cleanup. Posting rather than invoking immediately guarantees that WPF has returned from the original `OnClosing` call before the final close request is issued.
- The completed guard is set before the queued close, so the second `OnClosing` pass follows the normal direct-close path and cannot start cleanup again.
- `ToolWindowManager`, independent modeless z-order, DataContext cleanup order, tool disabling, normal child `Close()` calls, and modal-dialog ownership are unchanged from rev19.

### Changed - Verification Coverage

- Preserved all **128** existing automated checks and strengthened the rev19 tab-border and MainWindow-cleanup source contracts to match the corrected implementation.
- Added **Debugger selected-tab right-edge rendering source contract**, which requires the explicit in-bounds `TabRightEdge` and verifies that it follows the template-bound selected/theme border brush.
- Added **Main-window shutdown re-entry guard source contract**, which requires the asynchronous shutdown helper, dispatcher-posted final close, completed guard, and absence of a direct standalone `Close()` in the helper.
- The automated registry increases from **128 to 130** unique checks.
- Rev20 runtime acceptance begins by re-running the two failures observed in rev19: selected/unselected workspace-tab borders in all three themes, then MainWindow shutdown with an open Debugger and with multiple modeless tools. Only after those gates pass does the carried-forward Call Stack/Stepping acceptance resume.

### Documentation

- Recorded rev19's successful **128/128 PASS** automated gate and the two runtime failures without relabeling rev19 as verified.
- Updated README, the full development action plan, debugger architecture, theme guidance, main-workspace lifecycle documentation, Plugin SDK status text, and current PS5 protocol-mapping status to point to the rev20 corrective candidate.
- Added dedicated rev20 source-review and verification documents under `docs/testing/`.
- The planned Debugger **Integration, Export and Finalization** milestone moves from rev20 to **rev21** because this corrective code revision consumes rev20 under the project's revision rules.

### Versioning

- Host application: `0.1.7.rev20`.
- Feature title: `Debugger Tab Border and Shutdown Re-entry Fixes`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev34`.
- Next planned feature revision after rev20 acceptance: `0.1.7.rev21 - Integration, Export and Finalization`.

## TeeKay87's Memory Engine 0.1.7.rev19 - Debugger UI and Window Lifecycle Fixes

### Summary

Revision 19 is a focused corrective host/UI revision built from the `0.1.7.rev18` Debugger Tab Theme Fix candidate before the carried-forward Call Stack/Stepping runtime cycle resumed. Runtime inspection confirmed that the operating-system-white tab surface had been removed, but the application-owned Breakpoints / Watchpoints and Call Stack headers still clipped their one-pixel right border at the `TabPanel` layout edge. The same inspection cycle also established a host-window lifecycle requirement: the modeless Debugger, Disassembler, and Memory Viewer must participate in normal desktop z-order instead of remaining permanently above MainWindow, while closing MainWindow must still run each open tool's cleanup before the application exits.

The correction is intentionally host-owned. No Core debugger contract, Plugin SDK contract, Mock backend, PS5 backend, call-stack implementation, stepping implementation, breakpoint/watchpoint transport, register handling, scanner behavior, memory viewer behavior, or disassembly behavior is changed. Plugin API remains `2.15.0`, Mock remains `1.0.0.rev15`, and PS5 remains `0.1.0.rev34`.

### Changed - Complete Workspace Tab Border

- Kept the shared `WorkspaceTabControlStyle` and `WorkspaceTabItemStyle` introduced in rev18 and changed only the application-owned `TabItem` template geometry.
- Inset the rendered `TabBorder` by one device-independent unit on the right. This keeps the complete right-hand border inside the header's allocated `TabPanel` layout area instead of letting the final stroke be clipped at the item's layout boundary.
- Preserved the existing header padding, external tab spacing, rounded top corners, normal/hover/selected/disabled states, and all `DynamicResource` theme bindings.
- No Breakpoints / Watchpoints or Call Stack content, binding, command, capability, splitter, or debugger-state behavior was changed.

### Added - Modeless Tool Window Lifecycle Manager

- Added host-owned `ToolWindowManager` in the WPF application layer. The manager tracks the modeless Debugger, Disassembler, and Memory Viewer windows opened through MainWindow.
- Modeless tools temporarily use MainWindow as their WPF owner only while `Show()` establishes the existing `CenterOwner` startup placement. Ownership is cleared immediately after the window is shown, leaving the tool as a normal independent top-level window for desktop z-order.
- MainWindow can therefore be activated and raised above an open Debugger, Disassembler, or Memory Viewer. Activating a tool window can in turn raise that tool above MainWindow. No `Topmost` behavior is used.
- Existing modal dialogs remain owned by the window that invoked them and retain normal modal blocking/placement behavior. The new manager is only for the application's modeless tool workspaces.
- Tool windows remove themselves from the manager when they close normally, preventing stale `Window` references from being retained.

### Changed - Main Window Shutdown Cleanup

- MainWindow now performs a two-stage close when modeless tools are still open. The first close request is held while tracked tool cleanup runs; after cleanup and child-window closure finish, MainWindow completes its normal `OnClosed` disposal path.
- Before asynchronous shutdown cleanup starts, all currently tracked modeless tools are disabled so no new tool action can race the application-exit sequence. The manager then invokes each tool DataContext's established cleanup contract. `IAsyncDisposable` is awaited first, which covers the Debugger's asynchronous detach/session-release path. Existing `IDisposable` cleanup is used for the Disassembler and Memory Viewer.
- Each tracked window is then closed through normal WPF `Close()`, so its existing `Closed` handlers and window-specific cleanup remain part of the shutdown path. Existing ViewModel cleanup implementations are idempotent, so the normal per-window `Closed` handler remains safe after manager-initiated pre-cleanup.
- Cleanup is attempted for every tracked tool even if another tool reports a cleanup error. Errors are collected and reported to the diagnostic trace; the shutdown sequence still closes all tracked windows before MainWindow completes application exit.
- Closing MainWindow with no modeless tool windows open follows the existing direct close path.

### Changed - Verification Coverage

- Added the new tool-window manager source to the verification fixtures.
- Added one source-contract check requiring the complete right-side workspace-tab border geometry.
- Added one source-contract check requiring all three modeless tool launch paths to use the shared manager, release WPF ownership after modeless `Show()`, and avoid `Topmost`.
- Added one source-contract check requiring MainWindow to await tracked tool cleanup and requiring the manager to honor both `IAsyncDisposable` and `IDisposable` before closing windows.
- The automated registry increases from **125 to 128** unique checks.
- Rev19 runtime acceptance must verify the tab border in Light/Dimmed/Dark, two-way MainWindow/tool-window activation order, multiple simultaneous modeless tools, and MainWindow shutdown while an attached Mock Debugger plus other tools are open before the carried-forward Call Stack/Stepping gates resume.

### Versioning

- Host application: `0.1.7.rev19`.
- Feature title: `Debugger UI and Window Lifecycle Fixes`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev34`.
- The Debugger Integration, Export and Finalization milestone moves from rev19 to **rev20** because rev19 is now the corrective revision required before runtime acceptance can continue.

## TeeKay87's Memory Engine 0.1.7.rev18 - Debugger Tab Theme Fix

### Summary

Revision 18 is a focused corrective revision for the `0.1.7.rev17` Call Stack, Call Frames and Stepping candidate. The complete rev17 Windows automated gate passed at **124/124**, but the first runtime/UI inspection exposed an unthemed WPF `TabControl` in the new upper-right Debugger workspace. In Dimmed theme, the system-default tab template rendered the tab strip and selected-content surface white while the surrounding Debugger remained theme-aware, making the tab headers difficult to read and breaking the application's established Light/Dimmed/Dark visual consistency.

No debugger backend, Core contract, Plugin SDK contract, call-stack logic, stepping logic, breakpoint/watchpoint behavior, transport, target-lifetime behavior, or platform plugin implementation is changed by rev18. The fix is limited to shared WPF tab presentation, its Debugger usage, regression coverage, version metadata, and documentation. Because rev17 required a code correction after its automated gate, rev18 becomes the corrective candidate and the planned Debugger Integration, Export and Finalization milestone moves to rev19 in accordance with the revision rules.

### Changed - Theme-Aware Workspace Tabs

- Added reusable `WorkspaceTabControlStyle` and `WorkspaceTabItemStyle` definitions to the existing shared `ControlStyles.xaml` resource dictionary rather than introducing Debugger-local hardcoded colors.
- Replaced the operating-system default `TabControl` content surface with an application-owned template using `PanelBackgroundBrush`, `BorderBrush`, and the current theme resources.
- Replaced the operating-system default `TabItem` header template with an application-owned theme-aware template using `PrimaryTextBrush`, `PanelSecondaryBrush`, `SurfaceRaisedBrush`, `BorderBrush`, `BorderStrongBrush`, `AccentBrush`, and `AccentMutedBrush`.
- Added clear theme-aware states for normal, hover, selected, and disabled tab headers while preserving the existing tab layout and the Breakpoints / Watchpoints + Call Stack workspace structure introduced in rev17.
- Applied the shared styles to both upper-right Debugger tabs. No content layout, binding, command, splitter, or capability-gating behavior inside either tab was changed.
- The fix uses `DynamicResource` throughout so switching between Light, Dimmed, and Dark continues to update already-open controls through the existing theme system.

### Changed - Verification Coverage

- Added the shared control-style file to the test project's source fixtures.
- Added one focused source-contract regression check that requires the Debugger to use the shared workspace `TabControl`/`TabItem` styles and requires those styles to consume the application's theme resources.
- The automated registry therefore increases from **124 to 125** unique checks.
- Updated the rev17 verification record to preserve the successful **124/124 PASS** automated result and record that runtime acceptance stopped before the focused Call Stack/Stepping gates because the new tab surface was visibly unthemed.
- Added a dedicated rev18 verification document. Rev18 must first pass **125/125** on Windows, then the themed Debugger tab surface must be visually confirmed in Light, Dimmed, and Dark before the remaining rev17 Call Stack/Stepping runtime gates resume.

### Versioning

- Host application: `0.1.7.rev18`.
- Feature title: `Debugger Tab Theme Fix`.
- Plugin API: unchanged at `2.15.0`.
- Mock plugin: unchanged at `1.0.0.rev15`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev34`.
- The Debugger Integration, Export and Finalization milestone moves from rev18 to **rev19**.

## TeeKay87's Memory Engine 0.1.7.rev17 - Call Stack, Call Frames and Stepping

### Summary

Revision 17 continues the active `0.1.7` Debugger feature block by combining the previously separate Call Stack/Call Frames and Stepping milestones into one dependency-complete revision. The implementation builds on the fully verified `0.1.7.rev16` Hardware Watchpoints baseline: rev16 completed the Windows automated gate at **118/118 PASS** and the complete focused Mock/live-PS5 hardware-watchpoint acceptance cycle, including persistent/temporary watchpoints, 1/2/4/8-byte live PS5 coverage, all four hardware slots plus slot reuse, Enable/Disable/Remove/Remove All, debugger/session cleanup, software-breakpoint coexistence, and the conservative DR6 attribution fallback required by the current ps5debug-NG backend.

Rev17 does not introduce new public debugger contracts. The neutral `IDebuggerCallStackService`, `IDebuggerStepService`, `DebuggerStackFrame`, `DebuggerStepKind`, `TargetCapabilities.CallStack`, and `TargetCapabilities.StepExecution` contracts already exist in Plugin API `2.15.0` through the debugger foundation. The host now consumes those existing contracts, Mock and PS5 implement them, and all platform-specific stack-walk/step transport remains inside the corresponding plugin.

### Added - Call Stack and Call Frames

- Added capability-driven Call Stack presentation to the modeless Debugger workspace. The tab is visible only when the active plugin advertises `TargetCapabilities.CallStack`.
- Added `DebuggerStackFrameViewModel` as the WPF presentation adapter for the existing neutral `DebuggerStackFrame` model; no new architecture-specific frame model was introduced.
- Added frame columns for frame index, instruction address, module, and symbol, plus selected-frame detail for stack pointer, frame pointer, and return address.
- Added explicit Refresh for the selected paused debugger thread's call stack.
- Call-frame snapshots are valid only while the bound debugger session is Paused and the selected thread/target/connection generation still match the request that produced them.
- Call frames are cleared on Continue, detach, stale-target invalidation, connection-generation replacement, debugger cleanup, or any other transition away from a valid Paused stop context.
- Changing the selected debugger thread while Paused refreshes both Registers and Call Stack for that thread through the existing stop-context path.
- Added Call Stack navigation to the existing Disassembler using the selected frame's instruction address. Double-clicking a frame performs the same Disassembler navigation when the target still supports it.
- Added Call Stack navigation to the existing Memory Viewer using the selected frame's instruction address. The Debugger does not create duplicate memory/disassembly viewers or bypass their existing target/session safety rules.
- Preserved frame ordering supplied by the neutral service and selects the previous frame index again when possible after refresh.

### Added - Stepping and Run-to Operations

- Added **Step Into**, **Step Over**, **Step Out**, and **Run to...** controls to a dedicated second execution-control row in the Debugger header. The row is capability-driven by `TargetCapabilities.StepExecution`.
- Step operations require a current Paused debugger attachment and a selected debugger thread; commands are disabled outside that valid stop context.
- **Step Into** calls the existing neutral `IDebuggerStepService` with `DebuggerStepKind.Into`. Mock and PS5 implement native Step Into only.
- **Step Over** reuses the existing Core Disassembler pipeline to inspect the current instruction. Non-call instructions use native Step Into. A direct current Call instruction is stepped over by calculating its fall-through address and running to that address through the established temporary software-breakpoint lifecycle.
- **Step Out** uses the selected neutral call frame's return address and the same Run-to/temporary-breakpoint path instead of introducing a platform-specific return implementation in WPF or Core.
- Added the themed `RunToAddressDialog` for direct hexadecimal target-address entry. It accepts the standard optional `0x` prefix, rejects invalid/overflow input, and contains no platform-specific address semantics.
- **Run to Address** requires both `StepExecution` and software-breakpoint capability. If an enabled software execute breakpoint already exists at the destination it is reused; otherwise a temporary one-byte Software/Execute breakpoint is created through the existing breakpoint service before Continue.
- A disabled software execute breakpoint already present at the requested run-to address blocks the operation with a clear error instead of silently changing that user's breakpoint state.
- Step Over, Step Out, and Run to Address therefore reuse already established Disassembler, breakpoint, temporary-lifetime, Continue, event, cleanup, and stale-session behavior rather than adding parallel execution-control mechanisms.
- Existing rapid `Continue -> Paused event` stop-context deferral is reused for stepping/run-to operations so a fast stop is not lost while the initiating command is still busy.

### Changed - Debugger Workspace Layout

- Reworked the Debugger layout so rev17 functionality fits without adding another permanently visible full-size pane.
- The existing Breakpoints / Watchpoints manager and the new Call Stack share the upper-right workspace through tabs.
- Threads and Registers remain simultaneously visible in the left workspace when their capabilities are present.
- Events remains permanently available in the lower-right workspace and now owns its own **Events / count / Clear Events** header instead of placing event controls in the execution-control card.
- Added a vertical `ProportionalGridSplitter` between the left Threads/Registers workspace and the right tabbed/events workspace. It starts at approximately **36/64** and retains the shared **20/80 to 80/20** proportional movement limits.
- Retained the existing proportional Threads/Registers splitter on the left.
- Retained the existing approximately **65/35** upper-workspace/Events starting split on the right; the upper row collapses when neither breakpoint/watchpoint management nor Call Stack is available.
- Reused the application's existing `ColumnWorkspaceSplitterStyle`, `RowWorkspaceSplitterStyle`, button styles, card styling, DataGrid styling, theme resources, and responsive modeless-window behavior rather than creating debugger-specific duplicates.

### Added - Mock Call Stack and Step Backend

- Advanced Mock from `1.0.0.rev14` to `1.0.0.rev15`; Plugin API remains `2.15.0`.
- Added `TargetCapabilities.CallStack` and `TargetCapabilities.StepExecution` to Mock metadata.
- Updated the Mock plugin description so the public metadata reflects call-stack and stepping support.
- `MockDebuggerSession` now implements the existing `IDebuggerCallStackService` and `IDebuggerStepService` contracts.
- Added a deterministic three-frame stack for each Mock thread while Paused. The top frame uses the thread's current instruction/stack/frame pointers, caller frames use deterministic fixture addresses, module text uses `TestGame.exe`, and deterministic symbol names allow host/UI verification without real symbol infrastructure.
- Call-stack access is rejected while Running and for unknown thread ids, matching the existing paused-only register safety model.
- Added deterministic native Step Into. It transitions the Mock debugger to Running, emits a normal Resumed event, advances the selected thread's synthetic instruction pointer through the existing Mock instruction fixture, and asynchronously returns to Paused with a `StepCompleted` event.
- Mock deliberately rejects native Step Over/Step Out because those operations are host-composed from verified shared services in rev17.
- Existing deterministic breakpoint/watchpoint, thread, register, memory, scanner, Memory Viewer, and Disassembler behavior remains unchanged.

### Added - PS5 Call Stack Backend

- Advanced the PS5 plugin from `0.1.0.rev33` to `0.1.0.rev34`; Plugin API remains `2.15.0`.
- Added `TargetCapabilities.CallStack` and `TargetCapabilities.StepExecution` to PS5 metadata.
- Updated the PS5 plugin description so the public metadata reflects server-side call-stack access and native Step Into.
- `Ps5DebuggerSession` now implements the existing `IDebuggerCallStackService` and `IDebuggerStepService` contracts.
- Added ps5debug-NG `CMD_PROC_READ_STACK` (`0xBDAA0023`) support on the debugger-owner command client.
- The plugin first reads the selected paused thread's verified 176-byte general-register block to obtain RIP/RBP/RSP, then sends the packed 24-byte `{ pid, rbp, rsp, depth }` server-side stack-walk request.
- Call-stack depth is bounded to the backend's documented maximum of 64 frames.
- Added strict response framing checks for payload length, frame count, fixed 44-byte frame headers, per-frame locals/code lengths, truncation, and unexpected trailing data before any frame is exposed to the host.
- The variable frame-local/code payload returned by ps5debug-NG is validated and skipped in rev17 because the neutral Call Stack UI needs frame topology/addresses only; those backend bytes do not leak into Core/WPF.
- The top neutral frame uses the selected thread's current RIP. Subsequent neutral frame instruction addresses use the previous frame's return address. RSP/RBP/return address are mapped from the backend response, and module names are resolved through the PS5 plugin's existing current-process memory-map snapshot.
- If the server-side walk returns no frames, the plugin still exposes one safe neutral current frame from RIP/RSP/RBP rather than fabricating caller frames.

### Added - PS5 Native Step Into

- Added `CMD_DEBUG_STEP` (`0xBDBB0012`) and `CMD_DEBUG_STEP_THREAD` (`0xBDBB0013`) protocol constants and command-client support.
- Rev17 normally uses `CMD_DEBUG_STEP_THREAD` with the currently selected debugger thread id; the process-wide command remains supported by the plugin-private client when no thread id is supplied.
- Before native Step Into, the PS5 session flushes any staged software-breakpoint or hardware-watchpoint disables using the already verified paused-disable cleanup path.
- The PS5 session tracks only the pending step/thread identity required to classify the matching asynchronous SIGTRAP as neutral `DebuggerEventKind.StepCompleted` / `DebuggerStopReason.StepCompleted`.
- Manual Pause clears pending step state so an unrelated later trap cannot be misclassified as the completion of an abandoned step.
- Existing managed software-breakpoint and hardware-watchpoint attribution takes precedence over generic StepCompleted classification when a trap identifies one of those managed records.
- No register-write command, architecture-specific stepping enum, or Core/WPF ps5debug-NG command knowledge was introduced.

### Added - Verification Coverage

- Expanded the automated verification registry from **118 to 124 unique checks**.
- Added deterministic Mock call-stack service coverage, including paused-only access, frame ordering/metadata, and invalidation after Continue.
- Added deterministic Mock native Step Into coverage, including Running transition, `StepCompleted` event semantics, selected-thread RIP update, and rejection outside Paused state.
- Added Debugger Call Stack/stepping workspace source-contract coverage for capability gating, tabs, frame detail/navigation, shared proportional splitters, and host-composed Step Over/Step Out behavior.
- Added Run to Address source-contract coverage for themed hexadecimal input, capability gating, temporary software breakpoint reuse, disabled-existing-breakpoint rejection, and platform-neutral implementation.
- Added PS5 server-side call-stack protocol coverage for GETREGS-derived RBP/RSP input, `CMD_PROC_READ_STACK` framing, response parsing, depth bounds, and neutral frame mapping.
- Added PS5 native Step Into protocol/event coverage for selected-thread `CMD_DEBUG_STEP_THREAD`, immediate Running state, matching asynchronous StepCompleted mapping, and rejection of backend-native Step Over/Out.
- Updated the ps5debug-NG protocol test server with deterministic call-stack responses and native step command capture needed by the new checks.
- Rev17 remains a **candidate** until the clean Windows suite reports `All 124 checks passed.` and the focused Mock/live-PS5 Call Stack/Stepping runtime gates in `docs/testing/APP_0.1.7_REV17_VERIFICATION.md` pass.

### Rev16 Verification Result

- Rev16 completed the clean Windows automated gate at **118/118 PASS**.
- Mock hardware-watchpoint lifecycle, temporary/persistent behavior, validation, and manager UI acceptance passed.
- Live PS5 hardware-watchpoint acceptance passed for Write/ReadWrite behavior and 1/2/4/8-byte naturally aligned sizes.
- All four PS5 hardware watchpoint slots were exercised and released/reused successfully.
- Persistent and temporary watchpoints, Enable/Disable/Remove/Remove All, detach/reattach, Debugger close/reopen, disconnect/reconnect cleanup, and software-breakpoint coexistence passed.
- The known upstream zero-DR6 case remained safely handled: exact slot attribution is used when available, the sole active watchpoint is inferred only when unambiguous, and no arbitrary record is chosen when multiple active watchpoints cannot be distinguished.
- Rev16 is therefore the fully verified baseline for rev17.

### Version and Compatibility Notes

- Host application: `0.1.7.rev17`.
- Feature title: `Call Stack, Call Frames and Stepping`.
- Plugin API: unchanged at `2.15.0`; rev17 consumes existing debugger contracts and introduces no public API change.
- In-Memory Test Target plugin: `1.0.0.rev15`.
- PlayStation 5 plugin: `0.1.0.rev34`.
- Application metadata remains centralized in `AppInfo`; plugin metadata remains independent in each plugin's `xxPluginInfo` source.
- Compatible third-party Plugin API 2.x plugins are unaffected unless they choose to advertise the existing `CallStack` and/or `StepExecution` capabilities and provide the matching attached-session services.

### Documentation

- Updated `README.md` for the current rev17 candidate, verified rev16 baseline, new Debugger workspace layout, Call Stack/Call Frames behavior, stepping/run-to workflow, and current built-in plugin versions/capabilities.
- Updated the full development action plan so the former separate rev17 Call Stack and rev18 Stepping milestones are combined into rev17, with **rev18 - Integration, Export and Finalization** becoming the remaining debugger-closing revision.
- Updated the Debugger architecture for current Call Stack/Step consumption, host composition rules, frame lifetime, workspace layout, and revised remaining development order.
- Updated Plugin SDK foundation documentation to record that rev17 activates the already existing `CallStack` and `StepExecution` contracts without changing Plugin API `2.15.0`.
- Updated Mock plugin documentation for `1.0.0.rev15` deterministic frames and native Step Into.
- Updated PS5 plugin and ps5debug-NG protocol documentation for `0.1.0.rev34`, server-side stack walking, native step commands, and host-composed Step Over/Out/Run-to behavior.
- Updated `APP_0.1.7_REV16_VERIFICATION.md` from candidate to the actual verified acceptance result.
- Added `APP_0.1.7_REV17_SOURCE_REVIEW.md` and `APP_0.1.7_REV17_VERIFICATION.md`.

### Unchanged / Deferred

- Plugin SDK/Core debugger public contracts are unchanged in rev17.
- PS5 register editing remains intentionally unavailable; no SETREGS/SETFPREGS/SETDBREGS/SETFSGSBASE path is enabled.
- The current guarded PS5 extended-register strategy remains unchanged, including suppression of paused `GETDBREGS`.
- The verified software breakpoint and Hardware Watchpoint manager/lifecycle behavior remains unchanged except where temporary software breakpoints are now reused by run-to composition.
- Individual PS5 Thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the tested ps5debug-NG backend.
- Debugger universal export, final integration/cleanup stress acceptance, and feature-block closure are intentionally deferred to `0.1.7.rev18`.
- Find What Writes / Reads / Accesses follows the completed and verified `0.1.7` Debugger block rather than being mixed into rev17.



## TeeKay87's Memory Engine 0.1.7.rev16 - Hardware Watchpoints

### Summary

Revision 16 extends the still-active `0.1.7` Debugger block with neutral hardware data watchpoints. It builds directly on the fully verified `0.1.7.rev15` software-breakpoint baseline: rev15 completed the Windows suite at **114/114 PASS** and the focused runtime checks for breakpoint address validation, immediate breakpoint re-hit register refresh, and the Disassembler presentation cleanup all passed.

The existing Breakpoint Manager is expanded rather than duplicated. Software execute breakpoints and hardware data watchpoints share the same neutral lifecycle/state service, list, Enable/Disable/Remove controls, event history, session cleanup, and plugin-owned backend policy. Platform-specific slot counts, DR7 encodings, address rules, and transport remain inside the respective plugins.

### Added - Neutral Hardware Watchpoint Context

- Advanced Plugin API from `2.14.0` to `2.15.0` without creating a second watchpoint-only management service.
- Reused the established `DebuggerBreakpointKind.Hardware`, `DebuggerBreakpointAccess`, `DebuggerBreakpointRequest`, `DebuggerEventKind.Watchpoint`, `DebuggerStopReason.Watchpoint`, and `TargetCapabilities.Watchpoints` neutral contracts.
- Extended `DebuggerEvent` with optional `TriggeredBreakpoint` context while retaining the existing constructor for compatible event producers.
- `InstructionPointer` continues to mean the instruction that caused the stop. For a data watchpoint, the watched memory address is available through `TriggeredBreakpoint.Request.Address` instead of being conflated with the instruction pointer.
- Capability gating remains neutral: a plugin can advertise Breakpoints, Watchpoints, both, or neither.

### Added - Breakpoints / Watchpoints Manager

- Renamed the Debugger pane heading to **Breakpoints / Watchpoints** while retaining the existing manager/command layout and adding a compact Size column so hardware watchpoint widths remain visible after creation.
- The pane is available when the active plugin advertises either `Breakpoints` or `Watchpoints`.
- Extended the Add dialog with a type selector for **Software Execute Breakpoint** and **Hardware Watchpoint**.
- Hardware Watchpoint creation exposes neutral Address, Access, Size, and Temporary/Persistent lifetime inputs.
- Reused the host `HexAddress` and `UnsignedInteger` live input filters for the Add dialog Address and Size fields; final plugin validation remains authoritative for mapped ranges, legal widths, and alignment.
- Preserved the existing State column and the existing Add, Refresh, Enable, Disable, Remove, Remove All, and Disassembler buttons; Address, State, Type, Access, Size, and Lifetime are visible for managed records.
- Disassembler navigation is enabled only for selected execute breakpoints. A data watchpoint is not treated as a code address simply because it is selected in the same list.
- Access/size/alignment policy is intentionally delegated to the active plugin so Core/WPF does not contain PS5 or Mock register rules.

### Added - Mock Hardware Watchpoints

- Advanced Mock from `1.0.0.rev13` to `1.0.0.rev14` and target Plugin API from `2.14.0` to `2.15.0`.
- Added `TargetCapabilities.Watchpoints` to Mock metadata.
- Added four hardware-watchpoint slots independent of the existing 30 software execute-breakpoint slots.
- Mock accepts Read, Write, and ReadWrite data watchpoints with widths 1, 2, 4, or 8 bytes.
- Hardware watchpoints require natural alignment and a watched range fully contained inside the deterministic Mock memory map.
- Duplicate detection includes breakpoint kind, address, size, and access so distinct legal watchpoint definitions do not collide accidentally.
- Deterministic Mock Continue can now produce a Watchpoint stop. The event reports a synthetic accessing instruction inside the Mock code fixture while `TriggeredBreakpoint` identifies the watched data range.
- Temporary Mock watchpoints are removed after their first deterministic hit; persistent watchpoints remain enabled for repeated hits.

### Added - PS5 Hardware Data Watchpoints

- Advanced the PS5 plugin from `0.1.0.rev32` to `0.1.0.rev33` and target Plugin API from `2.14.0` to `2.15.0`.
- Added `TargetCapabilities.Watchpoints` to PS5 metadata.
- Implemented ps5debug-NG `CMD_DEBUG_SET_WATCHPOINT` (`0xBDBB0004`) with the documented 24-byte `{ index, enabled, length, breaktype, address }` request.
- Kept the backend's four DR0-DR3 hardware slots private to the PS5 plugin.
- Maps neutral Write to DR7 break type `1` and ReadWrite to `3`.
- Maps 1/2/4/8-byte widths to the backend's `0/1/3/2` DR7 length encoding.
- Rejects a true Read-only request because amd64 DR7 provides no equivalent data-watchpoint mode; the error directs callers to ReadWrite when read observation is required.
- Validates supported width, natural alignment, complete mapped range, and guard state before mutating a PS5 hardware slot.
- Keeps software execute breakpoints on the already verified INT3 path; rev16 does not replace them with hardware execute breakpoints.
- Enable, Disable, Remove, Remove All, temporary lifetime, Detach, debugger-window cleanup, and connection-generation teardown cover both managed breakpoint kinds.

### Added - Watchpoint Hit Attribution and Backend Guarding

- Reads hardware-trigger context from the 128-byte debug-register block already embedded in the normal 1184-byte asynchronous debugger event packet; rev16 does not re-enable paused `GETDBREGS`.
- Maps DR6 B0-B3 to the corresponding managed PS5 hardware slot when the backend preserves those bits.
- Added a safe fallback for the current ps5debug-NG event-dispatch behavior: if DR6 is zero and exactly one managed hardware watchpoint is enabled, that one watchpoint can be inferred without ambiguity.
- If DR6 is zero while multiple managed hardware watchpoints are enabled, the plugin deliberately does not guess. It reports a generic signal/other stop explaining that exact watchpoint attribution is unavailable and leaves temporary watchpoints intact.
- Added `docs/bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md` documenting the upstream event-dispatch sequence that clears the outgoing DR6 trigger-slot status before the client receives it.
- The existing rev10 guarded optional-register transport and rev15 rapid re-hit register refresh remain unchanged.

### Verification Coverage

- Expanded the automated verification registry from **114 to 118 unique checks**.
- Added neutral triggered-breakpoint event-context coverage.
- Added Mock hardware-watchpoint lifecycle, validation, persistent/temporary hit, and slot behavior coverage.
- Added Debugger hardware-watchpoint manager/Add-dialog source-contract coverage.
- Added PS5 watchpoint packet framing, access/length encoding, validation, slot lifecycle, exact DR6 attribution, zero-DR6 single-watchpoint fallback, ambiguous multi-watchpoint handling, and no-paused-GETDBREGS coverage.
- Rev16 completed the clean Windows automated gate at **118/118 PASS** and the focused Mock/live-PS5 hardware-watchpoint runtime acceptance succeeded.

### Rev15 Verification Result

- Rev15 completed the Windows automated gate at **114/114 PASS**.
- Focused runtime verification passed software execute-breakpoint address validation on Mock/PS5, the immediate `Continue -> same breakpoint hit` Registers refresh correction, and removal of the Disassembler explanatory paragraph.
- Rev15 is the accepted software-breakpoint baseline for rev16.

### Version and Metadata

- Advanced the application from `0.1.7.rev15` to `0.1.7.rev16` with feature title **Hardware Watchpoints**.
- Advanced Plugin API from `2.14.0` to `2.15.0` for optional triggered-breakpoint event context.
- Advanced Mock from `1.0.0.rev13` to `1.0.0.rev14` because Mock production watchpoint behavior changed.
- Advanced PS5 from `0.1.0.rev32` to `0.1.0.rev33` because PS5 production watchpoint transport and event mapping changed.
- Centralized application metadata remains sourced from `AppInfo`; plugin metadata remains independent.

### Documentation

- Updated the root README, full development action plan, Debugger architecture, Plugin SDK foundation, Mock plugin guide, PS5 plugin guide, and ps5debug-NG protocol mapping for the current watchpoint implementation.
- Updated `APP_0.1.7_REV15_VERIFICATION.md` with the actual 114/114 and focused runtime PASS result.
- Added `APP_0.1.7_REV16_SOURCE_REVIEW.md` and `APP_0.1.7_REV16_VERIFICATION.md`.
- Added a dedicated external ps5debug-NG DR6 watchpoint-attribution bug report under `docs/bug-reports/`.

### Unchanged

- The existing Breakpoint Manager State column and separate Enable/Disable controls remain unchanged; no Enabled checkbox is introduced.
- Software execute-breakpoint framing, 30-slot PS5 software allocation, corrected-RIP handling, persistent/temporary behavior, and paused Disable/Remove staging remain unchanged from the verified rev15 baseline.
- The Breakpoints/Events pane still starts at approximately 65/35 and uses the existing proportional splitter.
- PS5 paused register refresh still suppresses `GETDBREGS`; successful full safe snapshots continue to expose the verified 76-row general/FPU-SIMD/FS-GS surface.
- Individual PS5 Thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the currently tested ps5debug-NG backend.
- Scanner, Saved Addresses, Memory Viewer, Disassembler, export, themes, settings, and ordinary target-memory workflows are unchanged except where the shared Debugger event context is consumed.

## TeeKay87's Memory Engine 0.1.7.rev15 - Breakpoint Runtime Validation Fixes

### Summary

Revision 15 is a focused correction for two defects found during the complete rev14 Mock/live-PS5 Breakpoint Manager acceptance cycle, plus a small Disassembler presentation cleanup requested during that review. Rev14 passed the full Windows verification suite at **114/114** and its breakpoint runtime workflow passed persistent and temporary hits, repeated hits, state changes, paused-state cleanup, Remove All, debugger lifecycle cleanup, connection-generation invalidation, Disassembler navigation, and the rev10 guarded register-transport regression. Runtime acceptance also exposed two remaining host/plugin defects: invalid software execute-breakpoint addresses were not rejected before backend mutation, and an immediate breakpoint re-hit could arrive while Continue was still completing, leaving the Registers list empty after the target returned to Paused.

Rev15 corrects those defects without changing the public Plugin API, ps5debug-NG breakpoint framing, the verified paused Disable/Remove staging rule, breakpoint lifetime semantics, or the existing Breakpoints/Events layout.

### Fixed - Software Execute-Breakpoint Address Validation

- Added deterministic Mock validation before a software execute breakpoint is accepted.
- Mock now rejects addresses outside its target memory map and rejects mapped data addresses that are outside the explicit synthetic code fixture beginning at `0x10000400`.
- Preserved the existing one-byte Software/Execute contract and duplicate-address handling.
- Added PS5 plugin-owned address validation before any `CMD_DEBUG_SET_BREAKPOINT` request is sent.
- The PS5 target session now retains the latest memory-map snapshot produced by its normal `IMemoryMapProvider` enumeration for the current process. The debugger consumes that immutable plugin-private snapshot instead of issuing another command on the debugger/target transport.
- PS5 software execute breakpoints are rejected when the current map is unavailable, when the address is unmapped, when the containing region is not executable, or when the region is guarded.
- Memory-map/protection policy remains platform-specific. No PS5 addresses, page rules, slots, or wire details were moved into Core or WPF.
- Rejected breakpoint requests do not allocate a managed/backend slot or mutate ps5debug-NG breakpoint state.

### Fixed - Registers After Immediate Breakpoint Re-hit

- Corrected the Debugger ViewModel race where `Continue` temporarily transitions the target to Running and clears Registers, but the same enabled breakpoint can re-hit before the Continue command has fully left its busy state.
- A Paused stop-context event that arrives specifically while Continue is still completing is now retained as deferred stop context instead of being discarded because the UI is busy.
- Once Continue leaves its busy state, the newest deferred stop context is replayed through the existing Breakpoints/Threads/Registers refresh path when the target is still Paused and current.
- Running transitions still clear stale register data immediately.
- Manual Pause keeps its existing explicit register refresh and is not changed into a duplicate event-driven refresh path.
- Removed the redundant unconditional register clear after Continue; non-Paused coordinator state already owns that cleanup, while a rapid Paused re-hit must retain the new stop state.

### Changed - Disassembler Presentation Cleanup

- Removed the complete explanatory paragraph beginning `The visible range is decoded as one continuous stream...` from the Disassembler.
- Removed the now-unused spacing/layout row associated only with that paragraph so the remaining controls do not leave an empty gap.
- Disassembly navigation, decoding, syntax highlighting, origin selection, region/module/protection display, splitters, and table layout are otherwise unchanged.

### Verification Coverage

- Kept the automated verification registry at **114 unique checks**.
- Extended the existing Mock breakpoint lifecycle check to reject both a mapped data address and an out-of-map address before exercising the valid executable fixture.
- Extended the existing PS5 breakpoint protocol check to populate the target memory-map cache, reject mapped non-executable and unmapped addresses, and verify that rejected requests produce no backend breakpoint command.
- Extended the existing Debugger breakpoint source contract to protect the Continue-specific deferred stop-context refresh path.
- Extended the existing Disassembler source contract to ensure the removed explanatory text does not return.
- Rev15 completed a clean Windows run at **114/114 PASS**, and the focused Mock/live-PS5 runtime correction gates also passed. Rev15 is fully verified and is the accepted baseline for rev16.

### Rev14 Verification Result

- Rev14 completed the clean Windows automated gate at **114/114 PASS**.
- Mock runtime acceptance passed persistent and temporary breakpoint hits, repeated hits, Enable/Disable, Remove/Remove All, Disassembler integration, Detach/Reattach, window-close cleanup, stale-session protection, and the 65/35 Breakpoints/Events splitter.
- Live PS5 acceptance passed persistent/repeated and temporary breakpoint hits, paused-safe Disable/Enable/Remove/Remove All behavior, Detach, window close, Disconnect/Reconnect, duplicate handling, Disassembler navigation, and the guarded register-transport regression.
- Rev14 is superseded by rev15 because the runtime cycle exposed the two defects corrected here rather than because the core breakpoint transport failed.

### Version and Metadata

- Advanced the application from `0.1.7.rev14` to `0.1.7.rev15` with feature title **Breakpoint Runtime Validation Fixes**.
- Kept Plugin API at `2.14.0`; no public contract changed.
- Advanced Mock from `1.0.0.rev12` to `1.0.0.rev13` because Mock production breakpoint validation changed.
- Advanced PS5 from `0.1.0.rev31` to `0.1.0.rev32` because PS5 production memory-map/breakpoint validation changed.
- Centralized application metadata remains sourced from `AppInfo`; plugin metadata remains independent.

### Documentation

- Updated the root README to describe the current rev15 behavior and verification state.
- Updated the full development action plan so rev15 is the runtime-fix candidate and Hardware Watchpoints moves to rev16.
- Updated debugger, Plugin SDK, Mock, PS5, and ps5debug-NG mapping documentation for the new validation and stop-context behavior.
- Recorded the actual rev14 automated/runtime result in its verification documentation.
- Added `APP_0.1.7_REV15_SOURCE_REVIEW.md` and `APP_0.1.7_REV15_VERIFICATION.md`.
- No external-tool bug report was added because both corrected defects are in this codebase; the existing ps5debug-NG paused-disable report remains unchanged.

### Unchanged

- Breakpoint Manager retains Add, Refresh, Enable, Disable, Remove, Remove All, and Disassembler navigation; no Enabled checkbox is added and the existing State column remains unchanged.
- Persistent/temporary semantics, Mock deterministic hit generation, PS5 30-slot allocation, SIGTRAP hit mapping, and corrected-RIP handling remain unchanged.
- PS5 Disable/Remove while Paused continues to stage backend cleanup until explicit Continue to avoid the documented ps5debug-NG side effect.
- Rev10 guarded PS5 optional-register transport and the 76-row successful register surface remain unchanged.
- The Breakpoints/Events pane still starts at approximately 65/35 and uses the existing proportional splitter.
- Scanner, Saved Addresses, Memory Viewer, export, themes, settings, and ordinary target-memory paths are unchanged.
- Individual PS5 Thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the currently tested ps5debug-NG backend.

## TeeKay87's Memory Engine 0.1.7.rev14 - PS5 Breakpoint Capability Verification Fix

### Summary

Revision 14 is a focused verification correction for the still-unverified Breakpoint Manager and Software Breakpoints feature block. Rev13 successfully repaired the breakpoint test-project compile blocker and allowed all 114 checks to execute, but the supplied Windows run completed at **113/114**. The only failure was `PS5 plugin metadata and connection settings`: its exact expected capability set had not been updated when PS5 software breakpoints were introduced, so it rejected the production plugin's correct `TargetCapabilities.Breakpoints` flag as an unexpected extra capability.

Rev14 updates that stale expectation while preserving exact capability-set validation. No production breakpoint behavior, PS5 transport, Mock behavior, public Plugin API contract, Debugger WPF workflow, or rev13 splitter layout is redesigned.

### Fixed - PS5 Capability Verification

- Updated `VerifyPs5PluginMetadataAsync` in `tests/TeeKay87.MemoryEngine.Tests/Program.cs` so the exact expected PS5 capability set includes `TargetCapabilities.Breakpoints`.
- Updated the assertion message to name software-breakpoint support explicitly.
- Kept the assertion as exact equality rather than weakening it to individual `HasFlag` checks; missing or unexpected PS5 capabilities therefore continue to fail deterministically.
- Kept the verification registry at **114 checks**. No test was removed, skipped, bypassed, or reclassified.
- Confirmed the production PS5 capability declaration, breakpoint-specific protocol test, and Mock/PS5 debugger-capability contract already agree that Breakpoints is implemented.

### Rev13 Gate A Result

- Rev13 is recorded as **superseded after automated Gate A**.
- Its clean Windows run compiled and executed the full 114-check registry.
- **113 checks passed** and exactly one failed: `PS5 plugin metadata and connection settings`.
- The failure reported expected capabilities without `Breakpoints` and actual capabilities with `Breakpoints`.
- Because 114/114 did not pass, rev13 is not a verified baseline and focused breakpoint runtime acceptance remains attached to the superseding rev14 candidate.

### Version and Metadata

- Advanced the application from `0.1.7.rev13` to `0.1.7.rev14` with feature title **PS5 Breakpoint Capability Verification Fix**.
- Kept Plugin API at `2.14.0`; no public contract changed.
- Kept Mock at `1.0.0.rev12`; no Mock production source changed.
- Kept PS5 at `0.1.0.rev31`; no PS5 production source changed.
- Centralized application metadata remains sourced from `AppInfo`.

### Documentation

- Updated the root README to identify rev14 as the active breakpoint candidate and record rev13's 113/114 result.
- Updated the full development action plan to mark rev13 superseded and move Hardware Watchpoints to rev15.
- Updated Debugger architecture and current Mock/PS5 host/protocol documentation.
- Updated `APP_0.1.7_REV13_VERIFICATION.md` with the actual one-check failure.
- Added `docs/testing/APP_0.1.7_REV14_SOURCE_REVIEW.md`.
- Added `docs/testing/APP_0.1.7_REV14_VERIFICATION.md`.
- No external bug report was added because this defect is in the project's own verification expectation rather than an external tool/backend.

### Unchanged

- Breakpoint Manager UI/commands and the rev13 approximate 65/35 Breakpoints/Events starting split are unchanged.
- Mock persistent/temporary breakpoint lifecycle and deterministic hit behavior are unchanged.
- PS5 software breakpoint framing, slot allocation, SIGTRAP hit mapping, temporary cleanup, duplicate handling, and paused Disable/Remove staging are unchanged.
- Rev10 guarded PS5 register transport remains unchanged.
- Scanner, Saved Addresses, Memory Viewer, Disassembler, export, themes, settings, and ordinary target-memory paths are unchanged.
- Individual PS5 Thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the currently tested ps5debug-NG backend.

## TeeKay87's Memory Engine 0.1.7.rev13 - Breakpoint Test Build and Layout Fix

### Summary

Revision 13 is a focused correction for the still-unverified Breakpoint Manager and Software Breakpoints feature block. Rev12 fixed the PS5 nullable compile errors that had blocked rev11, and the application itself could launch, but the packaged verification project failed to compile before the 114-check suite could execute. The breakpoint source-contract tests introduced with rev11 called an `AssertContains` helper that was not present in the test harness, producing repeated `CS0103` errors in `Program.cs`.

Rev13 adds the missing shared assertion helper without changing the breakpoint test intent, adds source-contract coverage for the requested right-side Debugger layout, and changes the initial Breakpoints/Events split to approximately 65/35 as shown in the supplied runtime reference. The existing proportional splitter remains fully draggable and retains its 20/80 to 80/20 movement limits. No breakpoint transport, event mapping, plugin contract, target-memory path, or previously verified feature behavior is redesigned.

### Fixed - Breakpoint Verification Harness Build

- Added the missing `AssertContains(string source, string expected, string message)` helper to `tests/TeeKay87.MemoryEngine.Tests/Program.cs`.
- The helper reuses the existing `AssertTrue` failure path and performs an ordinal string comparison through `string.Contains(..., StringComparison.Ordinal)`.
- The existing breakpoint-manager and breakpoint-dialog source-contract assertions now compile without replacing or weakening any of the rev11/rev12 checks.
- Kept the verification registry at **114 unique checks**. This revision repairs the test harness; it does not inflate the check count solely for the compile correction.
- Added two assertions inside the existing **Debugger breakpoint manager source contract** registration so the requested initial Breakpoints/Events row proportions are protected against accidental regression.

### Changed - Breakpoints/Events Initial Split

- Changed the right-side Debugger workspace's initial star-row proportions from `2* / 3*` to `13* / 7*`.
- The resulting starting position gives Breakpoints approximately **65%** and Events approximately **35%** of the available right-side vertical workspace, matching the supplied reference layout much more closely.
- Reused the existing `ProportionalGridSplitter`; no second splitter implementation or fixed pixel-height behavior was introduced.
- Preserved `MinimumPreviousRatio=0.2` and `MaximumPreviousRatio=0.8`, so the user can still drag between the established 20/80 and 80/20 relative limits.
- Preserved both pane minimum heights, theme-aware splitter styling, adaptive window resizing, and capability-driven collapse of the Breakpoints row when a backend does not advertise `Breakpoints`.
- The already verified left-side Threads/Registers 50/50 splitter is unchanged.

### Version and Metadata

- Advanced the application from `0.1.7.rev12` to `0.1.7.rev13` with feature title **Breakpoint Test Build and Layout Fix**.
- Kept Plugin API at `2.14.0`; no public contract changed.
- Kept Mock at `1.0.0.rev12`; no Mock production source changed.
- Kept PS5 at `0.1.0.rev31`; no PS5 production source changed.
- Centralized application metadata remains sourced from `AppInfo`.

### Rev12 Gate A Result

- Rev12 is recorded as **superseded before verification**.
- The authoritative Windows test command failed during test-project compilation with repeated `CS0103` errors stating that `AssertContains` did not exist in the current context.
- The errors originated in the new breakpoint source-contract methods at `Program.cs` lines 2316-2340 in the rev12 package.
- Because the verification executable never started, rev12 has no 114/114 automated PASS and no complete breakpoint runtime acceptance.
- The supplied rev12 runtime screenshot confirms the application/Debugger UI could launch; that does not replace the blocked automated gate.

### Documentation

- Updated the root README for the rev13 candidate, unchanged plugin/API versions, repaired 114-check gate, and new Breakpoints/Events default layout.
- Updated the full development action plan so rev12 is marked superseded and rev13 remains the active breakpoint candidate.
- Shifted later debugger milestones again: Hardware Watchpoints -> rev14, Call Stack/Frames -> rev15, Stepping/Run-to -> rev16, Integration/Export/Finalization -> rev17.
- Updated Debugger architecture and current Mock/PS5 host context.
- Updated the rev12 source-review and verification documents with the actual Windows test-project failure.
- Added `docs/testing/APP_0.1.7_REV13_SOURCE_REVIEW.md`.
- Added `docs/testing/APP_0.1.7_REV13_VERIFICATION.md`.

### Unchanged

- Breakpoint Manager commands and presentation remain otherwise unchanged from rev12.
- Mock persistent/temporary breakpoint lifecycle and deterministic hit behavior are unchanged.
- PS5 software breakpoint transport, 30-slot ownership, SIGTRAP hit mapping, duplicate handling, temporary cleanup, and paused Disable/Remove staging are unchanged.
- Rev10 guarded PS5 register transport remains unchanged.
- Scanner, Saved Addresses, Memory Viewer, Disassembler, export, themes, settings, and ordinary target-memory paths are unchanged.
- Individual PS5 Thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the currently tested ps5debug-NG backend.

## TeeKay87's Memory Engine 0.1.7.rev12 - Breakpoint Manager Compile Fix

### Summary

Revision 12 is a focused build correction for the rev11 Breakpoint Manager and Software Breakpoints candidate. The first Windows build of rev11 stopped before the automated/runtime gates because nullable analysis is enabled globally and warnings are treated as errors. `Ps5DebuggerSession.cs` produced two `CS8600` diagnostics in the PS5 breakpoint lookup paths used by Remove and Enable/Disable. Visual Studio also reported `XLS0414` for `System.Object` in `MainWindow.xaml`; that XAML designer error is a downstream symptom of the referenced project not producing a valid build after the PS5 compile failure rather than an independent MainWindow change.

Rev12 preserves the complete rev11 breakpoint implementation and changes only the nullable-safe lookup form, affected metadata/test expectations, and documentation required to record the failed Gate A and the correction. No breakpoint transport, slot allocation, paused cleanup, hit mapping, UI command, Mock behavior, or public Plugin API contract is redesigned.

### Fixed - PS5 Nullable Breakpoint Lookup

- Corrected both PS5 breakpoint dictionary lookups that previously passed a non-nullable `SoftwareBreakpointEntry` variable directly to `Dictionary<TKey,TValue>.TryGetValue`.
- Each lookup now receives the dictionary result into an explicitly nullable local, rejects the missing/null path together, and assigns the proven non-null value to the existing non-nullable `entry` variable only after the guard succeeds.
- The correction covers:
  - `RemoveBreakpointAsync`;
  - `SetBreakpointEnabledAsync`.
- The existing unknown-breakpoint `KeyNotFoundException` behavior is unchanged.
- No null-forgiving operator is used to suppress nullable analysis; the control flow now establishes the non-null state explicitly.
- No new namespace dependency is required, and the existing `using` set in `Ps5DebuggerSession.cs` remains sufficient.

### Version and Metadata

- Advanced the application from `0.1.7.rev11` to `0.1.7.rev12` with feature title **Breakpoint Manager Compile Fix**.
- Advanced the PS5 plugin from `0.1.0.rev30` to `0.1.0.rev31` because the corrected source is inside the PS5 plugin.
- Kept Mock at `1.0.0.rev12`; no Mock source changed.
- Kept Plugin API at `2.14.0`; the public breakpoint contracts are unchanged.
- Updated the deterministic plugin-version verification expectation to PS5 `0.1.0.rev31`.
- The verification registry remains **114 checks** because this correction does not add a new runtime feature or new public behavior.

### Rev11 Gate A Result

- Rev11 is now recorded as **superseded before verification**.
- Its first clean Windows build reported:
  - `CS8600` in `Ps5DebuggerSession.cs` at the rev11 Remove breakpoint lookup;
  - `CS8600` in `Ps5DebuggerSession.cs` at the rev11 Enable/Disable lookup;
  - `XLS0414` in `MainWindow.xaml` after the referenced assembly failed to build.
- No rev11 automated/runtime PASS is claimed because Gate A did not complete.
- The breakpoint implementation itself is carried forward unchanged into rev12 apart from the compile correction above.

### Documentation

- Updated the root README for the rev12 candidate and PS5 plugin `0.1.0.rev31`.
- Updated the full development action plan so rev11 is marked superseded by the compile failure and the breakpoint feature remains the active candidate in rev12.
- Shifted later debugger revision numbers by one: Hardware Watchpoints -> rev13, Call Stack/Frames -> rev14, Stepping/Run-to -> rev15, Integration/Export/Finalization -> rev16.
- Updated Debugger architecture, Plugin SDK foundation, PS5 plugin/protocol documentation, and Mock host-version context.
- Updated the rev11 verification document with its failed Gate A result.
- Added `docs/testing/APP_0.1.7_REV12_SOURCE_REVIEW.md`.
- Added `docs/testing/APP_0.1.7_REV12_VERIFICATION.md`.

### Unchanged

- Breakpoint Manager presentation and commands are unchanged from rev11.
- Mock persistent/temporary breakpoint lifecycle and deterministic hit behavior are unchanged.
- PS5 `CMD_DEBUG_SET_BREAKPOINT` framing, 30-slot ownership, duplicate-address handling, SIGTRAP hit mapping, temporary-breakpoint cleanup, and paused Disable/Remove staging are unchanged.
- Rev10 guarded register transport remains unchanged.
- Scanner, Saved Addresses, Memory Viewer, Disassembler, export, themes, settings, and ordinary target-memory paths are unchanged.
- Individual PS5 Thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the currently tested ps5debug-NG backend.

## TeeKay87's Memory Engine 0.1.7.rev11 - Breakpoint Manager and Software Breakpoints

### Summary

Revision 11 begins the breakpoint layer of the active Debugger feature block from the fully verified `0.1.7.rev10` baseline. Rev10 passed the complete **110/110** Windows verification suite and the focused live-PS5 transport/runtime acceptance, including the correction for the rev9 optional-register stall, repeated Pause/Continue, Current Instruction -> Disassembler, Detach/Reattach, Debugger-window cleanup, and disconnect/reconnect stale-session handling.

Rev11 adds the first generic Breakpoint Manager and implements software execute breakpoints through the existing neutral debugger contracts. The host still owns only generic breakpoint presentation and lifecycle actions. Backend slot allocation, ps5debug-NG command ids, INT3 behavior, and PS5-specific cleanup rules remain inside the PS5 plugin. Persistent and temporary breakpoints are first-class records so later Step Over, Step Out, and Run-to workflows can build on the same manager instead of introducing a second breakpoint mechanism.

### Added - Public Breakpoint State Contract

- Advanced the public Plugin API from `2.13.0` to `2.14.0`.
- Added the optional `IDebuggerBreakpointStateService` contract for debugger backends that can enable or disable an existing breakpoint without removing the neutral breakpoint record.
- Reused the existing neutral `DebuggerBreakpoint`, `DebuggerBreakpointRequest`, `DebuggerBreakpointKind`, `DebuggerBreakpointAccess`, and `IDebuggerBreakpointService` types introduced with the debugger foundation. No PS5 slot id, INT3 byte, packet structure, or architecture-specific breakpoint concept was added to Core or WPF.
- Preserved the existing 2.x compatibility rule: plugins targeting an older compatible 2.x minor remain loadable, while rev11 Mock/PS5 plugins advance to API `2.14.0` because they consume the new state service.

### Added - Breakpoint Manager UI

- Added a capability-gated **Breakpoints** pane to the modeless Debugger workspace.
- Added neutral breakpoint columns for Address, State, Type, Access, and Lifetime.
- Added **Add...**, **Refresh**, **Enable**, **Disable**, **Remove**, **Remove All**, and **Disassembler...** actions.
- Added a theme-aware breakpoint dialog for hexadecimal address entry plus a **Temporary breakpoint** option. The current host workflow creates one-byte Software/Execute requests; backend validation remains authoritative.
- Added a destructive confirmation before **Remove All** and reused the existing application Danger button/confirmation styling.
- Added breakpoint-to-Disassembler navigation through the existing Disassembler workspace rather than introducing a debugger-local code viewer.
- Added host-side selection preservation by neutral breakpoint id during refresh/state changes.
- Breakpoint controls are hidden when the active plugin does not advertise `Breakpoints`, and remain unavailable until the Debugger is attached to the current Active Target/session generation.

### Added - Mock Breakpoint Backend

- Advanced Mock from `1.0.0.rev11` to `1.0.0.rev12` and API `2.14.0`.
- Mock now advertises `Breakpoints` and exposes both `IDebuggerBreakpointService` and `IDebuggerBreakpointStateService`.
- Added deterministic software-execute breakpoint add/list/remove/enable/disable behavior with duplicate-address and finite-slot validation.
- Added deterministic breakpoint-hit generation after Continue. A hit moves the session to Paused, updates the Main thread RIP to the breakpoint address, and emits a neutral `Breakpoint` event/stop reason.
- Persistent Mock breakpoints remain after a hit. Temporary Mock breakpoints are removed after their first hit.
- Existing deterministic thread/register behavior remains unchanged.

### Added - PS5 Software Breakpoints

- Advanced the PS5 plugin from `0.1.0.rev29` to `0.1.0.rev30` and API `2.14.0`.
- PS5 now advertises `Breakpoints` and implements software execute breakpoints through ps5debug-NG `CMD_DEBUG_SET_BREAKPOINT` (`0xBDBB0003`).
- The packed 16-byte request remains plugin-private: backend slot index, enabled flag, and 64-bit address.
- Kept ps5debug-NG's **30-slot** software-breakpoint limit entirely inside the PS5 plugin. The neutral host stores only backend-issued breakpoint ids and breakpoint requests.
- Added plugin-owned slot allocation, duplicate-address rejection, running-state enable/disable/remove, and cleanup state.
- Added SIGTRAP breakpoint-hit classification on the existing async debugger event channel. ps5debug-NG rewinds the event RIP to the managed software-breakpoint address before delivering the interrupt packet; rev11 matches that corrected address against active managed breakpoints and emits neutral `DebuggerEventKind.Breakpoint` / `DebuggerStopReason.Breakpoint`.
- Added first-class temporary breakpoints. A temporary breakpoint disappears from the manager after its first hit and its backend cleanup is staged for the next safe Continue boundary.

### PS5 Paused-Breakpoint Safety Boundary

- Source review identified an external ps5debug-NG behavior that makes immediate disable/remove unsafe while the target is already Paused. In `debug_set_breakpoint_handle`, the disable branch restores the saved byte and then calls `PT_CONTINUE` on the debugged process before returning success.
- Rev11 therefore does **not** send a disable/remove command immediately for an enabled PS5 software breakpoint while the neutral debugger state is Paused. The UI state is updated locally and the backend disable is staged.
- Staged disables/removals are flushed immediately before the next explicit Continue. Re-enabling a breakpoint while it is still only staged-disabled cancels that pending disable without touching the backend.
- Slots awaiting staged cleanup are not reused. This prevents a new breakpoint from being assigned to a backend slot whose old INT3 is still installed.
- The external behavior is documented in `docs/bug-reports/ps5debug-ng-disabling-software-breakpoint-resumes-paused-target.md`.
- This workaround is deliberately plugin-specific and does not change the generic breakpoint contract.

### Verification Coverage Added

- Expanded the verification registry from **110 to 114** unique checks.
- Added deterministic Mock breakpoint lifecycle/hit coverage, including persistent/temporary semantics and enable/disable state.
- Added Debugger Breakpoint Manager source-contract coverage for capability gating, neutral breakpoint services, destructive confirmation, and Disassembler integration.
- Added breakpoint-dialog source-contract coverage for the current Software/Execute/Temporary workflow.
- Added PS5 protocol coverage for command `0xBDBB0003`, 16-byte request framing, backend slot use, running-state state changes, paused disable/remove staging, temporary-hit mapping, staged cleanup on Continue, and persistent removal.
- The packaged **114-check** suite is a candidate gate and is not recorded as passed until run in the authoritative Windows environment.

### Documentation

- Updated the root README, Debugger architecture, development action plan, PS5 plugin README/protocol mapping, and Mock plugin README for rev11.
- Updated rev10 verification documentation to record its final **110/110 PASS** and live-PS5 acceptance.
- Added `docs/testing/APP_0.1.7_REV11_SOURCE_REVIEW.md`.
- Added `docs/testing/APP_0.1.7_REV11_VERIFICATION.md`.
- Added the GitHub-issue-ready external ps5debug-NG breakpoint-disable report under `docs/bug-reports/`.

### Unchanged

- Scanner, Saved Addresses, Memory Viewer, Disassembler, export, themes, settings, and normal target-memory behavior are not redesigned by rev11.
- The rev10 PS5 extended-register transport guard remains intact: paused GETDBREGS is still suppressed and safe optional register groups remain isolated/bounded.
- PS5 register rows remain read-only.
- Individual PS5 Thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the currently tested ps5debug-NG backend.
- Hardware watchpoints, call stacks, stepping/run-to, debugger export, Find What Writes/Reads/Accesses, and Break and Trace remain later milestones.


## TeeKay87's Memory Engine 0.1.7.rev10 - PS5 Extended Register Transport Guard

### Summary

Revision 10 is a focused correction to the live-PS5 failure found while accepting rev9. Rev9's automated suite passed all **108/108** checks and its focused Mock splitter, extended-register, and lifecycle gates passed. During live PS5 Gate E, Attach and global Pause succeeded, the target stopped, the UI remained responsive, and thread enumeration completed, but the register refresh remained pending with `Registers 0` and no instruction pointer.

The source review performed for this correction identified two separate safety requirements. First, optional extended-register responses must never be allowed to leave the reusable debugger-owner command stream waiting indefinitely or partially framed. Second, the current ps5debug-NG `GETDBREGS` handler is unsafe for the already-paused state used by the Registers pane: the debugger Pause path consumes the stop notification, `is_process_stopped()` can then report false on its later non-blocking `wait4()`, and `GETDBREGS` can send a second `SIGSTOP` followed by a blocking `wait4()` while the process is already stopped. Because debugger commands execute behind shared backend locks, that condition can also prevent Continue/Detach from progressing.

Rev10 therefore keeps the fully verified 176-byte general-register transport unchanged, moves safe optional reads to disposable bounded probe connections, gates those probes on negotiated backend metadata, and deliberately suppresses paused `GETDBREGS` until the external backend behavior is corrected and hardware-verified.

### Changed

- Advanced application metadata from `0.1.7.rev9` to `0.1.7.rev10` with feature title **PS5 Extended Register Transport Guard**.
- Advanced the PS5 plugin from `0.1.0.rev28` to `0.1.0.rev29`; Plugin API remains `2.13.0`.
- Kept Mock at `1.0.0.rev11`; no Mock production behavior was changed by this correction.
- Preserved mandatory PS5 `GETREGS` (`0xBDBB0008`) on the established debugger-owner command connection. Its 176-byte general-register mapping and semantic RIP/RSP/RBP roles are unchanged from the fully verified rev8 baseline.
- Added explicit extended-register eligibility derived from the ps5debug-NG connection metadata already collected during connection. Automatic optional reads require protocol version `1.3` or newer and capability level `1.0` or newer; a backend without that advertisement uses the verified general-register snapshot only.
- Reworked optional `GETFPREGS` (`0xBDBB000A`) and `GETFSGSBASE` (`0xBDBB000E`) reads so each runs through its own short-lived ps5debug-NG command connection instead of the debugger-owner stream.
- Added a two-second linked timeout to each disposable optional probe. Timeout, socket/connection failure, ordinary backend error/data-null status, or a probe-local malformed/truncated response returns that optional group as unavailable instead of blocking the complete Registers refresh.
- Kept caller cancellation distinct from optional fallback: user/session cancellation still propagates and is not converted into an unsupported-group result.
- Added a per-attached-session unavailable-command cache. Once an optional command fails or times out, that command is not retried on every later Register Refresh in the same attachment; independently successful optional groups continue to refresh normally.
- Deliberately stopped issuing `GETDBREGS` (`0xBDBB000C`) from the paused Registers workflow. Current ps5debug-NG can block in that handler when Pause has already consumed the stop status, so PS5 Debug rows are temporarily omitted from paused snapshots rather than risking a backend-wide debugger stall. The rev9 plugin-private debug-register decoder is retained for future safe event/backend use, but no paused command is sent.
- A fully successful current PS5 extended snapshot therefore contains **76 rows**: 26 general rows, 48 FPU/SIMD rows, and 2 FS/GS-base rows. The six DR rows from rev9 are not requested in rev10.
- Kept all PS5 register rows read-only. No SETREGS, SETFPREGS, SETDBREGS, or FS/GS write path was enabled.
- Retained rev9's proportional Threads/Registers splitter and wide neutral register mapping without host/Core architecture-specific changes.

### Verification coverage added

- Expanded the verification registry from **108 to 110** unique checks.
- Added a PS5 extended-register capability-gating test that omits the backend capability suffix, verifies a 26-row general-only snapshot, confirms that no optional register probe is sent, and confirms Continue/Detach remain functional.
- Added a PS5 optional-register timeout-isolation test that intentionally leaves `GETFPREGS` unanswered, requires the register refresh to complete within a bounded interval, verifies that the 26 general rows plus the healthy 2-row FS/GS group remain available, verifies the timed-out FPU/SIMD group is not retried in the same attachment, verifies FS/GS continues to refresh, verifies **zero paused GETDBREGS traffic**, and checks that Continue/Detach on the owner debugger session remain usable.
- Updated the existing PS5 register-service regression to require a 76-row general/FPU/SIMD/FS-GS snapshot and to reject any paused GETDBREGS request.
- Extended the deterministic ps5debug-NG protocol fixture so debugger-owner traffic and disposable optional probe connections can be exercised independently, including a deliberately silent optional-register response.
- The new **110-check** suite is packaged but is not marked passed until it is run in the authoritative Windows environment.

### Documentation

- Recorded the rev9 acceptance result: **108/108 PASS**, focused Mock Gates B-D PASS, and live PS5 Gate E FAIL at the optional extended-register refresh boundary.
- Added rev10 source-review and verification documents under `docs/testing/`.
- Added `docs/bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md`, a GitHub-issue-ready external backend report that documents the stopped-target `GETDBREGS` wait path without referencing this project.
- Updated the development plan, Debugger architecture, PS5 plugin README/protocol mapping, Mock README, and root README for the corrected rev10 transport boundary and shifted the remaining `0.1.7` debugger milestones by one revision.

### Unchanged

- Core and the public Plugin SDK are unchanged; Plugin API remains `2.13.0`.
- Scanner, Saved Addresses, Memory Viewer, Disassembler, export, theme, settings, and normal target-memory behavior are outside this correction and remain unchanged.
- PS5 individual Thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the already tested ps5debug-NG path and is not reopened by rev10.

## TeeKay87's Memory Engine 0.1.7.rev9 - Extended Register State and Debugger Pane Splitter

### Baseline and Revision Scope

- Advanced the application from `0.1.7.rev8` to `0.1.7.rev9` while remaining inside the active `0.1.7` Debugger feature block.
- Rev8 is now the verified baseline: the Windows verification suite passed **107/107**, and the complete focused Mock/live-PS5 Registers and Stop Context acceptance passed all **11/11** runtime steps. This includes register presentation/refresh/editing in Mock, live PS5 general-register snapshots, Pause/Continue stop-context refresh, Current Instruction -> Disassembler, PS5 read-only safety, and debugger transport/cleanup regression coverage.
- Individual PS5 thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the currently tested ps5debug-NG backend and is intentionally not reopened by rev9.
- Rev9 implements the next ordered Debugger milestone: backend-provided extended register groups plus the requested resizable Threads/Registers layout. Breakpoints, watchpoints, call stacks, and stepping remain later revisions.
- Updated centralized `AppInfo` metadata to `0.1.7.rev9 - Extended Register State and Debugger Pane Splitter`.

### Added - Extended Register State

- Reused the existing architecture-neutral `DebuggerRegister` model and Plugin API `2.13.0`; no new public API revision was required because the current model already supports arbitrary bit widths, neutral groups, semantic roles, read-only/writable metadata, and unsigned little-endian values.
- Advanced the Mock plugin from `1.0.0.rev10` to `1.0.0.rev11`. Its existing deterministic writable 64-bit General/Control rows are unchanged, while the paused-thread snapshot now also exposes deterministic read-only 80-bit floating-point, 128-bit SIMD, 256-bit SIMD, and debug-register fixtures. This exercises wide-value presentation without assigning a real CPU architecture to the Mock backend.
- Advanced the PS5 plugin from `0.1.0.rev27` to `0.1.0.rev28` and added plugin-private support for the current ps5debug-NG extended register commands:
  - `CMD_DEBUG_GET_FPREGS` / `0xBDBB000A`: 832-byte FPU/YMM state;
  - `CMD_DEBUG_GET_DBREGS` / `0xBDBB000C`: 128-byte debug-register state;
  - `CMD_DEBUG_GET_FSGSBASE` / `0xBDBB000E`: 16-byte FS/GS base state.
- Added plugin-private decoding for x87 control/state fields, `ST0`-`ST7`, `XMM0`-`XMM15`, reconstructed `YMM0`-`YMM15`, `DR0`-`DR3`, `DR6`, `DR7`, `FSBASE`, and `GSBASE`. Reserved debug-register slots are not exposed.
- Kept all x86-64/FreeBSD offsets and native wire layouts inside the PS5 plugin. Core, Plugin SDK, and WPF continue to consume only neutral register rows and do not gain architecture-specific register types.
- PS5 extended-register reads are additive to the already verified 176-byte general-register snapshot. If an optional extended command returns the backend's ordinary error/data-null status, the corresponding group is omitted while the mandatory general-register snapshot remains usable.
- PS5 register writing remains disabled. Rev9 does not expose SETREGS, SETFPREGS, SETDBREGS, or FS/GS write commands merely because related backend handlers exist.

### Added - Debugger Threads/Registers Splitter

- Replaced the fixed 210-unit Registers-pane height with the existing shared `ProportionalGridSplitter` used elsewhere in the application.
- Threads and Registers now begin at an equal `*`/`*` **50/50** share of the available left-side Debugger height.
- The splitter is horizontal, uses the existing theme-aware row-splitter style, and preserves the established visual spacing/handle presentation.
- Applied relative **20/80 to 80/20** movement limits through the shared proportional splitter, plus minimum usable pane heights. The limits therefore adapt to the actual Debugger window height rather than relying on fixed maximum pixel values.
- Both panes grow and shrink with the Debugger window after resizing, while the user's current star ratio is retained.
- When a backend does not advertise `RegisterAccess`, the splitter collapses and the register row contributes no unused height, preserving the existing thread-only layout.

### Verification Coverage

- Expanded the deterministic Mock register verification to require the new 80/128/256-bit fixtures while preserving the established general-register write/read-back test.
- Expanded the PS5 protocol fixture and register test to validate GETREGS plus GETFPREGS, GETDBREGS, and GETFSGSBASE request routing, exact response sizes, FPU/x87 offsets, XMM/YMM assembly, debug-register slot filtering, FS/GS base mapping, read-only gating, and absence of write-generated backend traffic.
- Added a dedicated Debugger Threads/Registers splitter source-contract check covering reuse of `ProportionalGridSplitter`, relative 20/80 limits, equal star-sized baseline, capability-driven register-row collapse, and removal of the old fixed `Height="210"` layout.
- The verification registry now contains **108 unique checks**. No existing check was removed or disabled.

### Documentation

- Synchronized the Debugger architecture document with the current revision order after rev8's corrective source-contract revision shifted the remaining milestones.
- Updated the full development action plan so rev8 is recorded as fully verified and rev9 is the active Extended Register State / pane-splitter candidate.
- Updated the PS5 protocol mapping and plugin documentation with the extended register commands, block sizes, plugin-private mappings, optional-group fallback behavior, and continued read-only policy.
- Updated the Mock plugin documentation with its new neutral wide-register fixtures.
- Added rev9 source-review and focused verification guidance under `docs/testing/`.

### Verification Boundary

- No Windows/.NET build or runtime result is claimed by this package. The next authoritative automated gate is:

```text
All 108 checks passed.
```

- After the Windows gate passes, focused runtime/UI testing should verify the Debugger's default 50/50 split, dragging/resizing limits, Mock wide-register display/selection stability, and live PS5 extended register enumeration/refresh/read-only behavior.

## TeeKay87's Memory Engine 0.1.7.rev8 - Debugger Workspace Source Contract Fix

### Baseline and Revision Scope

- Advanced the application from `0.1.7.rev7` to `0.1.7.rev8` after the packaged rev7 Windows verification repeatedly produced the same single failure: **106/107 checks passed**, with only `Debugger workspace command and event source contract` failing.
- Traced the failure to the verification source-contract itself rather than to Debugger target-generation behavior. The rev7 production code still captures `plugin.ConnectionGeneration`, passes the captured value into `DebuggerViewModel`, and uses the same captured generation when validating debugger-to-Disassembler navigation.
- Rev7 changed the constructor expression in `MainWindow.xaml.cs` from explicit `new DebuggerViewModel(...)` syntax to target-typed `new(...)`. The existing source-contract check still required the literal text `new DebuggerViewModel`, so it reported a false regression even though the connection-generation argument remained present and active.
- Rev8 is intentionally corrective and narrow. No debugger lifecycle behavior, register implementation, plugin transport, Core logic, Plugin SDK contract, Mock backend, PS5 backend, scanner, Memory Viewer, Disassembler, export path, or theme behavior is changed.
- Updated centralized `AppInfo` metadata to `0.1.7.rev8 - Debugger Workspace Source Contract Fix`.

### Fixed — Debugger Workspace Verification Contract

- Replaced the brittle literal `new DebuggerViewModel` source assertion with a constructor-binding check that accepts both explicit constructor syntax and C# target-typed `new(...)` syntax.
- Strengthened the same check to require the exact generation capture `long connectionGeneration = plugin.ConnectionGeneration;` and to require that `plugin`, `targetProcess`, and the captured `connectionGeneration` are passed to the `DebuggerViewModel` constructor in order.
- Kept the failure message and the surrounding target-identity/coordinator assertions intact so the check still fails if the debugger window is actually disconnected from the current target generation.
- The verification registry remains at **107 unique checks**. No test was removed, skipped, renamed, or weakened to make the Windows gate pass.

### Documentation Corrections

- Added rev8 source-review and verification documents under `docs/testing/`.
- Recorded rev7 as superseded after its repeatable **106/107** Windows gate result and documented the exact false-negative cause.
- Corrected stale current-build/current-verification references in `README.md` that still pointed to rev5 despite later debugger revisions.
- Updated the full development action plan so rev8 is the corrective source-contract revision and the previously planned Extended Register State milestone moves to rev9, with subsequent debugger milestones shifted by one revision.
- Plugin versions remain unchanged because no plugin source changed: Mock `1.0.0.rev10`, PS5 `0.1.0.rev27`, Plugin API `2.13.0`.

### Verification Boundary

- Rev8 still requires the user's Windows/.NET gate. The expected result remains:

```text
All 107 checks passed.
```

- After that gate passes, the focused Registers and Stop Context runtime/hardware acceptance from the rev7 candidate must still be completed against the rev8 package. The corrective revision does not treat the previously failed source-string assertion as runtime verification of the register feature.

## TeeKay87's Memory Engine 0.1.7.rev7 - Registers and Stop Context

### Baseline and Revision Scope

- Advanced the host from the verified `0.1.7.rev6` baseline to `0.1.7.rev7` while remaining inside the active `0.1.7` Debugger feature block.
- Rev6 passed **101/101** automated Windows checks and all focused Mock/live-PS5 verification gates except individual PS5 thread Suspend/Resume. The client request follows the documented ps5debug-NG `CMD_DEBUG_SUSPEND_THREAD` wire format, but the tested backend returns on-wire `CMD_ERROR` (`0xF0000001`). The failure is clean: the game, console, debugger attachment, thread enumeration, whole-target Pause/Continue, memory traffic, and reconnect flows remain usable.
- Added the project-wide external bug-report location `docs/bug-reports/` and recorded the ps5debug-NG thread-control failure as an issue-ready report. This is an external-backend blocker rather than a reason to remove the already-implemented neutral ThreadControl path.
- Rev7 implements the next debugger milestone: neutral register snapshots for the selected paused thread, safe writable-register handling where a backend supports it, semantic stop-context roles, and direct current-instruction navigation into the existing Disassembler.
- Updated centralized `AppInfo` metadata to `0.1.7.rev7 - Registers and Stop Context`.

### Added — Plugin API 2.13 Register Value Encoding

- Advanced the public Plugin API from `2.12.0` to `2.13.0` with additive `DebuggerRegisterValueEncoding` metadata. The existing seven-parameter `DebuggerRegister` constructor is retained unchanged and forwards to the new encoding-aware overload with `Bytes`, preserving the previous compiled constructor surface for compatible plugins.
- `DebuggerRegister` can now describe whether its byte payload should be treated as opaque bytes, an unsigned little-endian integer, or an unsigned big-endian integer.
- Kept architecture names, native register structures, register widths, protocol ids, and backend layouts outside the shared host. WPF formats and parses through neutral width/encoding metadata rather than hard-coding x86-64 register rules.
- Existing debugger contracts remain intact; the API change is limited to the additional register-value interpretation metadata required by the rev7 presentation/editing pipeline.

### Added — Shared Registers and Stop Context Workspace

- Added a capability-driven **Registers** section to the existing modeless Debugger workspace. It is shown only when the target advertises `RegisterAccess` and the attached debugger session supplies `IDebuggerRegisterService`.
- Register snapshots are requested only while the debugger is Paused and a valid thread is selected. They are cleared when the target resumes, detaches, becomes stale, or otherwise loses a valid paused stop context.
- Added neutral Register / Value / Group presentation, explicit register Refresh, selected-register tracking, and state-safe command gating.
- Added semantic handling for `DebuggerRegisterRole.InstructionPointer`, `StackPointer`, and `FramePointer`. The host does not search for names such as RIP/RSP/RBP.
- Added current-instruction display and **Disassembler...** navigation through the existing Disassembler launcher. No debugger-local disassembly viewer or platform-specific address route was introduced.
- Current-instruction state is cleared when a refreshed snapshot has no semantic instruction-pointer row, and navigation availability is gated through the target's normal Disassembler capability/service path so a stale or unsupported address action is never presented as usable.
- Added a reusable register value codec that formats and parses fixed-width neutral register values without truncating wider values or assuming host architecture.
- The unsigned hexadecimal parser prefixes a zero nibble before `BigInteger` hex parsing so values with the top bit set remain unsigned instead of being misread through two's-complement sign semantics.
- Added a register edit dialog for writable rows. The dialog rejects malformed or width-incompatible input, requires an actual value change, performs the backend write, refreshes the snapshot, and verifies the exact read-back bytes before reporting success.

### Added — Deterministic Mock Register Backend

- Advanced the Mock plugin from `1.0.0.rev9` to `1.0.0.rev10` and retargeted it to Plugin API `2.13.0`.
- Mock now advertises `RegisterAccess` in addition to `Debugger`, `ThreadEnumeration`, and `ThreadControl`.
- Added deterministic per-thread 64-bit register snapshots for Main, Worker, and Render while the target is Paused.
- Added neutral semantic roles for frame pointer, stack pointer, and instruction pointer and explicit little-endian unsigned value encoding.
- Mock register rows are writable so the shared edit/write/read-back workflow can be exercised safely without a physical target.
- Register access is rejected while Running, detached, stale, or disposed, preserving the debugger's stop-context ownership rules.

### Added — PS5 General Register Snapshots

- Advanced the PS5 plugin from `0.1.0.rev26` to `0.1.0.rev27` and retargeted it to Plugin API `2.13.0`.
- PS5 now advertises `RegisterAccess` and exposes read-only general-register snapshots while the debugger is Paused.
- Added plugin-private ps5debug-NG `CMD_DEBUG_GET_REGISTERS` (`0xBDBB0008`) support with a 4-byte LWP request and the backend's 176-byte amd64 general-register response.
- Added plugin-private mapping for the FreeBSD amd64 `struct reg` layout, including general-purpose registers, segment/control fields, RIP, RFLAGS, RSP, and SS. Only neutral `DebuggerRegister` rows cross the plugin boundary.
- Mapped RIP/RSP/RBP to the neutral semantic instruction-pointer, stack-pointer, and frame-pointer roles used by the host.
- PS5 register rows are intentionally read-only in rev7. The upstream SETREGS path has not been hardware-verified and is not exposed merely because ps5debug-NG contains a command handler for it.
- Floating-point/SIMD and debug-register state remain outside rev7 and are reserved for the next ordered debugger milestone.

### External Bug Report — ps5debug-NG Thread Suspend/Resume

- Added `docs/bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md` with the live reproduction from rev6 verification.
- The report records the actual process/thread used, exact wire status, request framing, observable safety behavior, relevant server handler, and the distinction between a confirmed backend rejection and a suspected underlying ptrace-argument issue.
- The existing Memory Engine suspend/resume client code remains unchanged. If ps5debug-NG fixes the handler behind the same `0xBDBB0006` / `0xBDBB0007` contract, the current client path can be retested without a protocol redesign.

### Verification Coverage

- Preserved the rev6 verification registry and added six rev7 checks for **107** total registrations:
  1. `Mock debugger register snapshots and writes`;
  2. `Debugger workspace register panel source contract`;
  3. `Debugger register value codec source contract`;
  4. `Debugger register edit dialog source contract`;
  5. `Debugger current instruction Disassembler integration`;
  6. `PS5 debugger general register snapshot protocol`.
- Updated plugin metadata/capability expectations to Mock `1.0.0.rev10`, PS5 `0.1.0.rev27`, Plugin API `2.13.0`, and `Debugger + ThreadEnumeration + ThreadControl + RegisterAccess` for both built-in debugger backends.
- Added deterministic PS5 protocol-fixture coverage for `0xBDBB0008`, the 4-byte selected thread id, status framing, and the complete 176-byte register block.
- Added source-level host-neutrality checks so platform register names, LWP terminology, ptrace details, and PS5 protocol ids stay out of shared WPF register presentation code.
- Rev7 is not considered verified until a clean Windows run reports **107/107** and the focused Mock/live-PS5 register runtime gates pass. PS5 register writing is explicitly outside the rev7 acceptance boundary.

### Documentation

- Updated `README.md`, debugger architecture, Plugin SDK foundation, built-in plugin documentation, UI documentation, and the full development action plan for the rev7 register milestone.
- Updated the rev6 verification record with the completed **101/101** result and the external ps5debug-NG ThreadControl blocker observed during live hardware testing.
- Added `docs/testing/APP_0.1.7_REV7_SOURCE_REVIEW.md` and `docs/testing/APP_0.1.7_REV7_VERIFICATION.md` for the source gate and Windows/runtime acceptance of this revision.

## TeeKay87's Memory Engine 0.1.7.rev6 - Threads and Thread Control

### Baseline and Revision Scope

- Advanced the host application from the fully verified `0.1.7.rev5` baseline to `0.1.7.rev6` while remaining inside the active `0.1.7` Debugger feature block.
- Recorded rev5 as fully accepted: Windows verification passed **97/97**, the focused responsive-header/button/Mock runtime checks passed, and the complete live-PS5 debugger acceptance passed for capability gating, Attach, TCP 755 callback establishment, repeated Pause/Continue, dedicated-transport isolation, explicit Detach/Reattach from Running and Paused states, window-close cleanup, connection-generation invalidation, multiple Debugger-window exclusivity/recovery, and the PS5 regression smoke test. The opportunistic natural async-interrupt check was not safely triggerable and remains explicitly deferred rather than failed.
- Rev6 implements the next debugger milestone from `docs/TeeKay87_Memory_Engine_Full_Development_Action_Plan.md`: shared thread enumeration/selection and capability-gated per-thread Suspend/Resume, first through the deterministic Mock backend and then through the real ps5debug-NG PS5 backend.
- Rev6 also applies the already-requested main-workspace cleanup by removing the passive `Memory map: <n> region(s) loaded.` presentation text from the permanent target header. The Active Target memory-map load, stored region collection, error reporting, Memory Viewer, Disassembler, scanner, and all region-dependent behavior remain unchanged.
- Plugin API remains **2.12.0** because `IDebuggerThreadService`, `IDebuggerThreadControlService`, `DebuggerThreadInfo`, `DebuggerThreadState`, `TargetCapabilities.ThreadEnumeration`, and `TargetCapabilities.ThreadControl` were deliberately introduced and verified in rev1 for this later consumer revision.
- Updated centralized `AppInfo` metadata to `0.1.7.rev6 - Threads and Thread Control`.

### Added — Shared Debugger Thread Workspace

- Added a generic **Threads** pane to the existing modeless Debugger workspace. The pane is host-owned and appears only when the active plugin advertises `TargetCapabilities.ThreadEnumeration`.
- Added a read-only, selectable, virtualized thread table with neutral **Thread**, **Name**, and **State** columns. Thread ids are presented as hexadecimal opaque ids; WPF does not know whether a backend calls them LWPs, thread handles, or another platform-specific identifier.
- Added **Refresh** for explicit re-enumeration through `IDebuggerThreadService`.
- Added **Suspend** and **Resume** controls that are visible only when `TargetCapabilities.ThreadControl` is advertised and are routed exclusively through `IDebuggerThreadControlService`.
- Per-thread Suspend/Resume is enabled only while the overall debugger target is in the neutral Running state. Whole-target Paused/Stopped state therefore cannot accidentally be mixed with a per-thread control request.
- Thread selection is preserved by neutral thread id across refreshes whenever that id is still present. If it disappears, selection falls back to the first current thread.
- The thread list is populated automatically after successful Attach and refreshed after whole-target Pause/Continue so its neutral state presentation follows the current debugger execution state.
- Thread rows are cleared on explicit Detach and stale-target invalidation. Existing debugger target/process/connection-generation ownership rules remain authoritative.
- Added `DebuggerThreadViewModel` as a presentation-only wrapper around the already-public neutral `DebuggerThreadInfo` model. No platform-specific field, register structure, protocol token, or handle type is introduced into the host UI.

### Added — Deterministic Mock Thread Backend

- Advanced the Mock plugin from `1.0.0.rev8` to `1.0.0.rev9`, still targeting Plugin API `2.12.0`.
- Mock now advertises `ThreadEnumeration` and `ThreadControl` in addition to its already-verified coarse `Debugger` capability.
- The attached Mock debugger session implements both `IDebuggerThreadService` and `IDebuggerThreadControlService` directly and exposes a deterministic three-thread set:
  - `0x1` — `Main`;
  - `0x2` — `Worker`;
  - `0x3` — `Render`.
- Threads enumerate as Running while the target runs and Stopped while the whole target is paused. A thread explicitly suspended through the per-thread service retains the distinct neutral Suspended state across whole-target Pause/Continue transitions until it is resumed.
- Mock per-thread control rejects unknown ids, duplicate suspend, resume of a non-suspended thread, detached/disposed use, and per-thread control while the whole debugger target is not Running.
- Existing deterministic Mock Pause/Continue events, instruction pointer, attachment exclusivity, target cleanup, memory/scanner/disassembly behavior, and all previously verified services remain unchanged.

### Added — PS5 Thread Enumeration and Thread Control

- Advanced the PS5 plugin from `0.1.0.rev25` to `0.1.0.rev26`, still targeting Plugin API `2.12.0`.
- PS5 now advertises `ThreadEnumeration` and `ThreadControl` alongside the rev25 `Debugger` capability. RegisterAccess, Breakpoints, Watchpoints, CallStack, and StepExecution remain intentionally unadvertised.
- Extended the plugin-private dedicated debugger command client with current ps5debug-NG thread commands:
  - `CMD_DEBUG_GET_THREAD_LIST = 0xBDBB0005`;
  - `CMD_DEBUG_SUSPEND_THREAD = 0xBDBB0006`;
  - `CMD_DEBUG_RESUME_THREAD = 0xBDBB0007`;
  - `CMD_DEBUG_THREAD_INFO = 0xBDBB0011`.
- Thread-list enumeration validates normal status framing, reads the returned `uint32` count and 32-bit thread ids, enforces a defensive upper bound of 65,536 entries, and keeps the entire list-plus-info sequence serialized on the dedicated debugger command stream.
- For each enumerated thread, the PS5 plugin opportunistically requests the current 40-byte thread-info record (`thread id`, `priority`, `tdname[32]`). The neutral host consumes only id/name/state in rev6; priority remains plugin-private until a public neutral model needs it.
- A thread-info failure for an otherwise valid enumerated thread does not discard that thread. It is retained with an empty optional name so basic thread enumeration remains usable.
- Per-thread Suspend/Resume transmits only the plugin-private 32-bit backend thread id and requires the overall debugger target to be Running.
- Because current ps5debug-NG thread-list/thread-info replies do not expose an individual execution-state field, the PS5 plugin maps neutral thread state from verified debugger-session state plus successful Suspend/Resume operations it owns. Backend ids and FreeBSD LWP terminology do not cross into Plugin SDK/Core/WPF.
- Successful local suspension ownership is reconciled against every new backend thread list so ids that have exited disappear from the suspended set rather than remaining stale.
- Existing rev25 Attach/Detach/Pause/Continue, TCP 755 async event handling, debugger exclusivity, transport isolation, and cleanup logic are preserved and extended rather than duplicated.

### Changed — Main Workspace Passive Memory-Map Status Cleanup

- Removed the permanent-row binding that rendered `SelectedPlugin.MemoryRegionStatusText`, including text such as `Memory map: 1 region loaded.` or `Memory map: 1418 regions loaded.`.
- Kept the actual Active Target memory-map enumeration and `ActiveMemoryRegions` assignment intact.
- Kept `MemoryRegionErrorText` visible so real memory-map failures remain actionable instead of being hidden with the passive success prose.
- Kept the verified two-row header order, 180-unit responsive row-1 input maximum, protected side padding, connection-state indicator, button height/text alignment, and Plugin details placement unchanged.

### Verification Coverage

- Preserved all **97** verification registrations from the fully verified rev5 baseline; no previous registration was removed.
- Added exactly four rev6 checks:
  1. `Mock debugger thread enumeration and control`;
  2. `Debugger workspace thread panel source contract`;
  3. `PS5 debugger thread enumeration and control protocol`;
  4. `Main workspace passive memory-map status removal`.
- Updated existing plugin metadata/debugger capability checks to require exactly `Debugger + ThreadEnumeration + ThreadControl` from the debugger-capability family for Mock/PS5 while continuing to reject premature register/breakpoint/watchpoint/call-stack/step advertisement.
- Updated independent plugin-version expectations to Mock `1.0.0.rev9` and PS5 `0.1.0.rev26`; Plugin API remains `2.12.0`.
- Extended the PS5 protocol test server with the real thread-list, thread-info, suspend-thread, and resume-thread wire shapes and records the per-thread control commands for verification.
- The verification registry therefore increases from **97** to **101** checks. Rev6 is not considered verified until a clean Windows Release build passes **101/101** and the focused Mock/live-PS5 thread runtime gates pass.

### Documentation

- Updated `README.md` to identify rev6 as the current candidate and rev5 as fully verified, describe the new Threads pane and both new backend consumers, and record the passive memory-map status removal.
- Updated `docs/architecture/DEBUGGER_ARCHITECTURE.md` with the rev6 host thread-workspace behavior, capability/service boundary, state rules, Mock deterministic fixture, and PS5 state-mapping limitation.
- Updated `docs/architecture/PLUGIN_SDK_FOUNDATION.md` to document the first production consumers of the thread contracts introduced in API `2.12.0` and the capability/service rules future plugins must follow.
- Updated `docs/ui/MAIN_WORKSPACE.md` to make clear that passive successful memory-map counts do not belong in the permanent target header while errors remain visible.
- Updated the dedicated Mock and PS5 plugin documentation for versions `1.0.0.rev9` and `0.1.0.rev26` and their new advertised thread capabilities.
- Updated `docs/plugins/PS5/PS5DEBUG_NG_PROTOCOL_MAPPING.md` with the exact thread-list/thread-info/Suspend/Resume debugger commands and response framing used by rev6.
- Updated `docs/TeeKay87_Memory_Engine_Full_Development_Action_Plan.md` to close rev5 as fully verified, mark rev6 Threads/Thread Control as the current implementation candidate, and retain Registers/Stop Context as the next debugger milestone.
- Added `docs/testing/APP_0.1.7_REV6_VERIFICATION.md` and `docs/testing/APP_0.1.7_REV6_SOURCE_REVIEW.md`.

## TeeKay87's Memory Engine 0.1.7.rev5 - Responsive Target Header Input Sizing

### Baseline and Revision Scope

- Advanced the host application from `0.1.7.rev4` to `0.1.7.rev5` while remaining inside the active `0.1.7` Debugger feature block.
- Rev4 was built and visually reviewed on Windows before its automated/live-PS5 acceptance was completed. That review confirmed the intended permanent two-row target/header composition and the shared button text-alignment correction, but exposed one remaining presentation defect: fixed 240-unit ordinary row-1 inputs could extend beyond the target bar's right edge when the main window was narrowed.
- Rev5 supersedes rev4 as the package to verify. It preserves the complete rev3 PS5 debugger transport/session implementation, the rev4 two-row action order, the rev4 shared button text fix, Plugin API `2.12.0`, Mock plugin `1.0.0.rev8`, and PS5 plugin `0.1.0.rev25` unchanged.
- Rev5 changes only host target-header sizing/presentation, verification coverage, application metadata, and documentation. No scanner, Saved Addresses, Memory Viewer, Disassembler, debugger backend, plugin protocol, Core debugger contract, Plugin SDK contract, theme color, or button visual/interaction behavior is redesigned.
- Updated centralized `AppInfo` metadata to `0.1.7.rev5 - Responsive Target Header Input Sizing`.

### Changed — Responsive Row-1 Target Input Sizing

- Replaced the rev4 fixed `UiMetrics.TopTargetInputWidth = 240` rule with `UiMetrics.TopTargetInputMaxWidth = 180`.
- The 180-unit value is now explicitly a **maximum**, not a fixed width.
- Platform, every ordinary plugin-declared connection TextBox/ComboBox rendered from `ConnectionSettings`, and Target Process share one host-computed runtime width.
- At normal/wide window sizes the shared width is capped at 180 device-independent units. No row-1 minimum width is imposed, so those ordinary inputs may shrink below 180 whenever horizontal space becomes constrained.
- Added a host-owned `TopTargetInputWidth` dependency property on `MainWindow`. All ordinary row-1 input containers bind to this one value, ensuring Platform, plugin connection fields, and Target Process remain equal-width while resizing.
- Added first-row layout recalculation through `TopTargetFirstRow_LayoutUpdated`. The calculation starts from the target bar's actual width after its left/right padding is removed, counts the currently rendered plugin connection fields, detects whether process controls are present, reserves fixed action-button widths, and reserves the existing row/inter-control margins before dividing the remaining width evenly across ordinary inputs.
- The responsive calculation clamps available input space at zero and the final common width at `UiMetrics.TopTargetInputMaxWidth`, giving the row an effective sizing range of `0..180` instead of a fixed 240-unit value.
- The calculation uses `TargetConnectionBar.ActualWidth` and explicitly subtracts `TargetConnectionBar.Padding.Left` and `.Right` from the available content width. The existing `Padding="14,8"` therefore remains protected and cannot be consumed by responsive field growth.
- Existing horizontal spacing is preserved: the plugin-input lane keeps its leading margin and per-input separation, Connect/Disconnect retain their margins, and Target Process/Refresh retain their existing spacing. The change does not compress or remove those visual boundaries.
- The PS5 Port field no longer has any special width behavior. Like Platform, PS5 host/IP, and Target Process, it uses the same responsive common width and the same 180-unit maximum.
- Future plugins inherit this behavior automatically through the generic host renderer. Ordinary TextBox/ComboBox connection fields in row 1 must not introduce plugin-specific WPF widths or minimums; extensive configuration still belongs in plugin/settings/details surfaces rather than a third permanent target row.

### Preserved — Permanent Two-Row Header and Button Presentation

- Kept the permanent first-row order exactly as established in rev4: **Platform -> plugin-declared connection inputs -> Connect -> Disconnect -> Target Process -> Refresh -> Set Active Target**.
- Kept the permanent second-row order exactly as established in rev4: **Reload Plugins -> Disassembler... -> Debugger...**, with future top-level action/tool buttons appended to that same row.
- Kept the main-window default width at `1460` and existing `MinWidth=1100`; rev5 solves the narrower-window overflow through responsive field sizing rather than by raising the minimum window width.
- Kept the redundant `Active <process>` text and passive process-count/enumeration prose removed.
- Kept the Connected/Not connected indicator at the far left of the bottom status bar with the existing theme-aware success/danger presentation.
- Kept every ordinary button at `UiMetrics.StandardControlHeight = 34` and retained rev4's `14,2` internal padding plus explicit horizontal/vertical text centering. No button height, semantic style, border geometry, corner radius, hover/pressed/focus behavior, or per-view width is changed by rev5.

### Preserved — Rev3 PS5 Debugger Candidate

- Kept PS5 plugin `0.1.0.rev25` and Plugin API `2.12.0` unchanged.
- Kept the dedicated PS5 debugger command connection, TCP 755 callback listener/event socket, attach/detach, stop-go Pause/Continue, fixed 1184-byte interrupt parsing, neutral event translation, debugger ownership/exclusivity, and cleanup behavior byte-identical to rev4.
- Kept the verified rev1 Core debugger coordinator/contracts and complete Plugin SDK byte-identical to rev4.
- Kept the fully verified rev2 Mock plugin/debugger backend byte-identical to rev4.
- No advanced PS5 debugger capability is newly advertised. ThreadEnumeration, ThreadControl, RegisterAccess, Breakpoints, Watchpoints, CallStack, and StepExecution remain later work.

### Verification Coverage

- Preserved all **96** verification registrations from the rev4 candidate; no prior check name is removed.
- Updated the existing `Main workspace two-row target header standard` check so the same registration now requires `UiMetrics.TopTargetInputMaxWidth = 180`, the shared responsive width binding for Platform/plugin fields/Target Process, and the existing two-row/order contract.
- Added exactly one new registration: `Main workspace responsive target input widths`.
- The new source contract checks that the first row is wired to responsive recalculation, uses the target bar's padded content width and dynamic plugin-input count, reserves fixed connection-action widths, clamps available space at zero, caps the final common width at 180, applies the shared maximum to all ordinary row-1 inputs, and does not use that maximum as a minimum width.
- The verification registry therefore increases from **96** to **97** checks. Rev5 is not considered verified until a clean Windows Release build passes all **97/97** checks.
- Runtime/UI acceptance now explicitly includes resizing both Mock and PS5 layouts from the normal/default width down to the existing `MinWidth=1100` and back up, confirming equal-width ordinary inputs, a hard 180-unit maximum, shared shrink behavior below 180, intact left/right target-bar padding, no overlap, no right-edge overflow, and immediate recalculation when switching plugins.
- The inherited live PS5 debugger Attach/Pause/Continue/Detach, TCP 755 cleanup/reconnect, dedicated-transport isolation, stale-session, multiple-window, and regression gates remain required because rev3/rev4 were superseded before that hardware acceptance was completed.

### Documentation

- Updated `README.md` to identify rev5 as the current candidate, explain the 180-unit maximum/no-minimum responsive sizing rule, and point to the rev5 verification/source-review documents.
- Updated `docs/ui/CONTROL_METRICS.md` so the permanent top-input standard is documented as a responsive common width capped at `TopTargetInputMaxWidth = 180`, including the rule that target-bar left/right padding remains reserved.
- Updated `docs/ui/MAIN_WORKSPACE.md` with the responsive first-row behavior while preserving the permanent two-row order.
- Updated `docs/architecture/PLUGIN_SDK_FOUNDATION.md` so future plugin authors know that ordinary row-1 connection fields inherit host-computed responsive sizing and must not encode one-off WPF widths/minimums.
- Updated debugger/disassembly/export/Memory Viewer and Mock/PS5 living documentation to identify rev5 as the current host while making clear that those subsystems are unchanged.
- Marked the rev4 verification/source-review documents as historical/superseded records rather than rewriting them as completed acceptance.
- Updated `TeeKay87_Memory_Engine_Full_Development_Action_Plan.md` so rev5 is the current candidate and later debugger milestones shift forward by one revision: Threads/Thread Control now begins at rev6.
- Added `docs/testing/APP_0.1.7_REV5_VERIFICATION.md` and `docs/testing/APP_0.1.7_REV5_SOURCE_REVIEW.md` as the authoritative rev5 acceptance/source-review records.

## TeeKay87's Memory Engine 0.1.7.rev4 - Two-Row Target Header and Button Alignment

### Baseline and Revision Scope

- Advanced the host application from `0.1.7.rev3` to `0.1.7.rev4` while remaining inside the active `0.1.7` Debugger feature block.
- Rev3 was **not** marked verified before this revision. Its first real PS5 debugger transport implementation remains the active debugger candidate, but the initial rev3 main-target layout did not match the intended permanent two-row design during the first Windows UI review.
- Rev4 therefore supersedes rev3 as the package to verify. It preserves the rev3 PS5 debugger transport/session implementation and PS5 plugin `0.1.0.rev25` unchanged, then corrects only shared host UI presentation, button-content layout, verification coverage, version metadata, and documentation.
- Kept Plugin API `2.12.0`, Mock plugin `1.0.0.rev8`, and PS5 plugin `0.1.0.rev25` unchanged. No new plugin contract or platform-specific capability is introduced by rev4.
- Updated centralized `AppInfo` metadata to `0.1.7.rev4 - Two-Row Target Header and Button Alignment`.

### Changed — Permanent Two-Row Target Header

- Rebuilt the normal main-window target/connection surface so it has exactly **two permanent control rows** instead of the three-row rev3 arrangement.
- Established the first-row order as:
  1. **Platform**;
  2. zero or more plugin-declared connection inputs;
  3. **Connect**;
  4. **Disconnect**;
  5. **Target Process** when process enumeration is supported;
  6. **Refresh**;
  7. **Set Active Target**.
- Moved the generic plugin connection fields (`SelectedPlugin.ConnectionSettings`) from the former third connection row into the first row immediately after Platform. PS5 host/IP and Port therefore appear before Connect/Disconnect, matching the intended connection-first workflow without adding platform-specific XAML.
- Moved **Connect** and **Disconnect** into the same first row and aligned them to the bottom of the labeled TextBox/ComboBox controls so all interactive controls share the existing 34-unit baseline.
- Moved **Target Process**, **Refresh**, and **Set Active Target** after the connection actions on that same first row. Selected Process and Active Target semantics are unchanged: choosing a process still does not activate it until Set Active Target is executed.
- Rebuilt the second row as a left-aligned horizontal action lane with the permanent order **Reload Plugins -> Disassembler... -> Debugger...**. Future top-level host/tool action buttons are to be appended to this same row rather than creating another permanent row.
- Preserved the existing memory-region status and collapsible **Plugin details** surface on the second-row area without changing the required action order.
- Preserved the rev3 removal of the redundant `Active <process>` text and passive process-count/enumeration prose, plus the rev3 binary Connected/Not connected indicator at the far left of the bottom status bar.
- Kept connection/process/memory-region error TextBlocks below the permanent rows. These remain collapsed when empty and therefore do not create a third normal control row.
- Increased only the main window's default startup width from `1380` to `1460` units so the fixed 240-unit PS5 first-row inputs and existing action buttons fit the intended two-row composition at startup. The existing `MinWidth=1100` remains unchanged, so this does not remove the user's ability to resize the window more narrowly.

### Added — Shared 240-Unit Top Input Standard

- Added `UiMetrics.TopTargetInputWidth = 240` as the single host-owned width metric for ordinary TextBox/ComboBox controls in the main target/connection header.
- Applied the 240-unit width to the **Platform** ComboBox.
- Applied the same 240-unit width to every generic plugin-declared connection input rendered from `ITargetPlugin.ConnectionSettings`. This includes both PS5 **IP address / host name** and **Port**; Port no longer receives a narrower special-case width.
- Applied the same 240-unit width to the **Target Process** ComboBox.
- Deliberately kept this rule in host presentation rather than plugin metadata. Future platform plugins contribute connection-setting definitions only; they do not declare WPF widths or create platform-specific layout branches.
- Documented that the permanent target header is a compact connection/target surface. A future plugin that requires extensive extra configuration should place additional options in an appropriate settings/details workflow instead of forcing a third permanent target row or introducing one-off input widths.
- The application-bar Theme ComboBox is not part of this target/plugin input standard and retains its existing application-bar sizing.

### Changed — Shared Button Text Layout

- Preserved `UiMetrics.StandardControlHeight = 34` for every ordinary application button. Rev4 does **not** make buttons shorter or taller.
- Preserved the existing Primary/Secondary/Danger semantic styles, background/border/text brushes, corner radii, focus border, hover overlay, pressed overlay, disabled-state palette, cursor rules, and per-view button widths/margins.
- Reduced only the shared ordinary button's internal content padding from `14,8` to `14,2`, keeping the existing 14-unit horizontal padding while providing more usable vertical space inside the unchanged 34-unit outer button.
- Kept `HorizontalContentAlignment=Center` and `VerticalContentAlignment=Center` authoritative in `ButtonBaseStyle`.
- Added explicit `VerticalAlignment=Center` to both generated string-content types (`AccessText` and `TextBlock`) inside the common button `ContentPresenter`. This prevents WPF-generated button labels from sitting too high in the template and leaves enough room for descenders such as `g`, `j`, `p`, `q`, and `y`.
- Reduced the one remaining normal-height local Select All button override in the export dialog from `8,3` to `8,2` so it follows the same compact vertical-content rule. The deliberately smaller 28-unit Saved Address row Remove button already used `8,2` and remains unchanged.
- No button interaction behavior or semantic meaning changes in rev4; this is strictly a content-layout correction inside the existing visual system.

### Preserved — Rev3 PS5 Debugger Candidate

- Kept all rev3 PS5 debugger production files and behavior unchanged: dedicated debugger command connection, TCP 755 listener/callback, attach/detach, stop-go Pause/Continue, fixed 1184-byte interrupt parsing, neutral event mapping, debugger ownership/exclusivity, transport cleanup, and target-session disposal behavior.
- Kept the verified rev1 Core debugger coordinator/contracts and complete Plugin SDK unchanged.
- Kept the fully verified rev2 Mock debugger backend unchanged.
- Kept all existing scanner, Saved Addresses, Memory Viewer, Disassembler, universal export, themes, settings, and PS5 primary memory/scan/disassembly transports unchanged except for the shared button-content template that intentionally affects button text layout application-wide.
- No new advanced PS5 debugger capability is advertised. ThreadEnumeration, ThreadControl, RegisterAccess, Breakpoints, Watchpoints, CallStack, and StepExecution remain outside this revision.

### Verification Coverage

- Preserved all **94** verification registrations present in the rev3 candidate.
- Updated the existing `Main workspace target controls and connection status layout` source contract so it validates the corrected row order rather than the superseded rev3 arrangement.
- Added `Main workspace two-row target header standard`, which verifies that the target strip contains one permanent first-row marker and one permanent second-row marker, that `UiMetrics.TopTargetInputWidth` is centralized at `240`, that Platform/plugin connection inputs/Target Process all consume that metric, and that plugin connection fields precede Connect.
- Added `Shared button content alignment and vertical padding`, which verifies that standard button height still comes from `UiMetrics.StandardControlHeight`, ordinary shared padding is `14,2`, horizontal/vertical content alignment remains centered, and generated AccessText/TextBlock labels receive explicit vertical centering.
- Added the shared button style and UI metrics source files as verification fixtures.
- The verification registry therefore increases from **94** to **96** checks. Rev4 is not considered verified until a clean Windows Release build passes all **96/96** checks.
- Because rev4 supersedes rev3 before live acceptance, the rev4 runtime gate also includes the full PS5 debugger Attach/Pause/Continue/Detach, TCP 755 cleanup/reconnect, dedicated-transport isolation, and regression acceptance originally prepared for rev3.

### Documentation

- Updated `README.md` to identify rev4 as the current candidate and describe the permanent two-row target-header and 240-unit input-width standard.
- Updated `docs/ui/MAIN_WORKSPACE.md` with the exact row order and the rule that future tool/action buttons stay on row 2.
- Updated `docs/ui/CONTROL_METRICS.md` with `UiMetrics.TopTargetInputWidth = 240` and the future-plugin rendering rule.
- Updated `docs/ui/BUTTON_STYLES.md` to state explicitly that button height remains 34 units and only internal vertical text padding/alignment changed.
- Updated `docs/architecture/PLUGIN_SDK_FOUNDATION.md` so future plugin authors know that connection-setting fields are host-rendered at 240 units on row 1 and must not introduce platform-specific WPF widths.
- Updated the full development action plan so rev4 is the current debugger candidate and later debugger milestones shift forward by one revision.
- Added rev4 source-review and verification documents. Retained the rev3 verification/source-review documents as historical records and annotated their status to make clear that rev3 was superseded before Windows/UI/live-PS5 acceptance.

## TeeKay87's Memory Engine 0.1.7.rev3 - PS5 Debug Transport and Target UI Cleanup

### Verified Baseline

- Advanced the application from `0.1.7.rev2` to `0.1.7.rev3` while remaining inside the active `0.1.7` Debugger feature block.
- Recorded `0.1.7.rev2 - Debugger Workspace and Mock Backend` as fully verified: the complete Windows suite passed **89/89** checks and every focused Mock Debugger runtime/UI acceptance step passed, including capability gating, modeless open, explicit attach, deterministic pause/resume events, event-history clearing, detach/reattach, window-close cleanup, disconnect/connection-generation invalidation, Active Target lifetime coverage, multiple-window exclusivity/recovery, and regression smoke testing of Scan, Memory Viewer, Disassembler, Saved Addresses, read/write, themes, and connect/disconnect behavior.
- Preserved Plugin API `2.12.0`, the verified rev1 Core/SDK debugger foundation, and Mock plugin `1.0.0.rev8` unchanged. Rev3 consumes the existing debugger contracts rather than extending or duplicating them.
- Updated centralized `AppInfo` metadata to `0.1.7.rev3 - PS5 Debug Transport and Target UI Cleanup`.

### Added — PS5 Debugger Provider and Dedicated Transport

- Advanced the PlayStation 5 plugin from `0.1.0.rev24` / API `2.11.0` to `0.1.0.rev25` / API `2.12.0` because it now consumes the debugger contracts introduced in Plugin API 2.12.
- Added `TargetCapabilities.Debugger` to the PS5 plugin and exposed `IDebuggerProvider` from each connected `Ps5TargetSession`. No advanced debugger capability is advertised yet: thread enumeration/control, register access, breakpoints, watchpoints, call stacks, and stepping remain unavailable until their dedicated revisions implement and verify those services.
- Added a plugin-owned `Ps5DebuggerProvider` that permits one active debugger session per connected PS5 target session, validates the signed ps5debug-NG PID range, releases completed sessions cleanly, and disposes the active debugger before the owning target session closes.
- Added a dedicated plugin-local debugger command transport through `Ps5DebuggerCommandClient`, owned by `Ps5DebuggerProvider` / `Ps5DebuggerSession`. It does not reuse the normal `Ps5DebugClient` memory/scan command stream or the concurrent Frozen-write connection. Each debugger attachment owns its own command TCP connection to the configured ps5debug-NG server.
- Added the required ps5debug-NG async debugger listener on TCP `755`. The listener is started before `CMD_DEBUG_ATTACH` is sent so the console can establish its documented outbound interrupt connection during attach. Listener/socket ownership remains entirely inside the PS5 plugin.
- Added PS5 protocol constants for `CMD_DEBUG_ATTACH` (`0xBDBB0001`), `CMD_DEBUG_DETACH` (`0xBDBB0002`), debugger wire status values, TCP 755, and the fixed 1184-byte interrupt packet layout required by the current ps5debug-NG protocol.
- Added fixed-packet interrupt decoding for thread id, wait status, thread name, and instruction pointer. The FreeBSD/x86-64 register block offset used to obtain the instruction pointer remains plugin-private; Core/WPF receives only neutral debugger event fields.
- Added explicit PS5 debugger Pause/Continue through ps5debug-NG `CMD_DEBUG_CONTINUE` / stop-go (`0xBDBB0010`) over the debugger-owned command transport; action `1` pauses and action `0` resumes. Pause maps to the neutral `Paused/PauseRequested` event and Continue maps to `Resumed` without modifying the general non-debugger `IProcessControl` implementation.
- Added explicit backend detach and asynchronous transport cleanup. Closing/disposal tears down the event socket and dedicated command connection and releases provider ownership so a later debugger window can attach again.
- Added clear diagnostics for unavailable TCP 755, debugger command connection failures, already-attached backend status, attach failures, and a missing outbound event connection/firewall path.

### Added — PS5 Debugger Session Event Mapping

- Added a PS5 `IDebuggerSession` implementation that begins in neutral `Running` state after a successful backend attach and tracks `Running`, `Paused`, and `Detached` without exposing ps5debug-NG-specific state types to the host.
- Async ps5debug-NG interrupt packets are translated into neutral `Paused` events with `DebuggerStopReason.Signal`, optional thread id, optional instruction pointer, and a readable signal/thread message.
- Rechecked current upstream event-dispatch semantics before packaging: ps5debug-NG sends the interrupt packet and resumes its application layer, but does not issue `PT_CONTINUE` for the traced stop at that point. The ptrace stop remains pending until stop-go action `0`, so the neutral post-interrupt state is correctly `Paused` rather than `Running`.
- Unexpected loss of the async event channel maps the backend session to neutral `Unknown`/Attached state with a backend event so Pause/Continue are disabled while Detach remains available for deterministic cleanup; intentional detach/disposal suppresses that diagnostic event.
- Session disposal unsubscribes transport callbacks before teardown, attempts backend detach when still attached, and always closes the dedicated transport even if backend detach fails.

### Changed — Main Target/Connection UI

- Moved the **Target Process** ComboBox onto the same top target row as the **Platform** ComboBox. **Refresh** and **Set Active Target** now sit on that same row beside the process selector.
- Kept **Disassembler...** and **Debugger...** on the second target row and left-aligned them as the first actions on that row. Connection-setting fields plus Connect/Disconnect/Reload Plugins remain on the following compact connection row so the top process row does not become cramped at supported window widths.
- Removed the visible `Active <process>` summary from the main workspace. Selected process and Active Target remain separate internal states; only the redundant text presentation was removed.
- Removed the visible `Connect to enumerate processes`, `1 process loaded`, and `<n> processes loaded` process-status presentation from the main window. The existing ViewModel status remains available internally for operation/error coordination, but it is no longer rendered as permanent UI prose.
- Moved the connection state to the far left of the permanent bottom status bar. `Connected` is presented with the existing theme-aware success border plus muted success background; `Not connected` uses the existing danger border with the normal theme background. The indicator is driven only by the generic `IsConnected` state, displays only `Connected` / `Not connected`, and contains no platform-specific styling branch.
- Preserved connection errors, process errors, memory-region status/errors, scan status/progress, general application status, and the right-aligned application version field.

### Preserved Functionality and Boundaries

- Kept Plugin API `2.12.0` unchanged because rev3 requires no new public debugger model or service contract.
- Kept Mock plugin `1.0.0.rev8` byte-for-byte unchanged. Its already verified deterministic debugger backend remains the regression/reference implementation.
- Kept the rev1 Core `DebuggerSessionCoordinator`, debugger identity/event contracts, and Plugin SDK debugger types unchanged. PS5 is implemented as a consumer of those verified shared abstractions.
- Preserved the existing PS5 primary connection/handshake, process enumeration, memory maps, memory read/write, native TurboScan, process-control, concurrent Frozen-write transport, and Iced disassembly paths. The new debugger connection is intentionally separate from those established transports.
- Preserved Active Target semantics: choosing a process in the moved ComboBox still does not activate it until **Set Active Target** is used.
- Preserved the capability-driven Debugger entry and host stale-session protections introduced and verified in rev2.

### Verification Coverage

- Extended the verification registry from **89** to **94** checks without removing or weakening any prior check.
- Updated plugin version/capability checks for PS5 `0.1.0.rev25`, Plugin API `2.12.0`, and the newly advertised coarse Debugger capability.
- Added **PS5 debugger provider lifecycle and exclusivity** coverage for service discovery, one-active-session enforcement, ownership release, and reattach after disposal.
- Added **PS5 debugger attach pause continue detach protocol** coverage using a dedicated protocol fixture that validates the separate command connection, exact attach PID, stop-go actions `1`/`0`, state transitions, neutral Pause/Resume events, and explicit detach.
- Added **PS5 debugger async interrupt channel** coverage for the TCP 755 callback path and fixed 1184-byte packet translation, including thread id, signal, and instruction pointer.
- Added **PS5 debugger dedicated transport isolation** coverage proving the ordinary PS5 process command stream remains usable while a debugger session is attached on its own command socket.
- Added **Main workspace target controls and connection indicator layout** source coverage for the new top-row selectors/actions, removal of the Active/process-status presentations, and the bottom red/green connection indicator.
- The first authoritative rev3 gate is a clean Windows Release build plus **94/94** automated checks. This must be followed by the focused UI regression and live-PS5 debugger acceptance in `docs/testing/APP_0.1.7_REV3_VERIFICATION.md`; rev3 is not considered verified until those runtime/hardware gates pass.

### Documentation

- Updated `README.md` for the current rev3 behavior, fully verified rev2 baseline, PS5 debugger ownership, plugin/API versions, and compact target/status UI.
- Updated the full development action plan so rev2 is recorded as verified and rev3 is the current candidate before thread work begins.
- Updated `docs/architecture/DEBUGGER_ARCHITECTURE.md` with PS5 transport ownership, attach/event-channel sequencing, event translation, cleanup, and rev3 non-goals.
- Updated `docs/ui/MAIN_WORKSPACE.md` with the new selector/action rows and bottom connection-state indicator.
- Updated the PS5 plugin README and ps5debug-NG protocol mapping with the debugger command/event subset implemented by rev25.
- Added `docs/testing/APP_0.1.7_REV3_VERIFICATION.md` and `docs/testing/APP_0.1.7_REV3_SOURCE_REVIEW.md` for the Windows/UI/live-hardware gate and pre-package source-boundary review.


## TeeKay87's Memory Engine 0.1.7.rev2 - Debugger Workspace and Mock Backend

### Verified Baseline

- Advanced the host application from `0.1.7.rev1` to `0.1.7.rev2` while remaining inside the active `0.1.7` Debugger feature block.
- Recorded `0.1.7.rev1 - Debugger Contracts and Core Foundation` as verified after the complete Windows verification suite passed **84/84** checks. No live debugger hardware acceptance was required for rev1 because it contained no platform debugger backend or user-facing debugger workspace.
- Preserved the verified Plugin API `2.12.0` debugger contracts and Core `DebuggerSessionCoordinator` implementation unchanged. Rev2 consumes that foundation instead of introducing a parallel debugger lifecycle.
- Updated centralized `AppInfo` metadata to `0.1.7.rev2 - Debugger Workspace and Mock Backend`.

### Added — Capability-Driven Debugger Workspace

- Added the first modeless WPF **Debugger** workspace. The main target strip exposes **Debugger...** only when the selected plugin advertises `TargetCapabilities.Debugger`, and the button becomes usable only for a connected current Active Target when the host is not inside another conflicting foreground target operation.
- Bound every Debugger window permanently to the plugin id, process id/name, and connection generation captured when the window is opened. A disconnected/reconnected or changed target cannot silently reuse an older Debugger window.
- Added explicit **Attach**, **Pause**, **Continue**, and **Detach** commands routed through the verified Core `DebuggerSessionCoordinator`. The WPF layer does not call a platform debugger implementation directly.
- Added a virtualized read-only debugger event table with session-local sequence, local timestamp, event kind, execution state, stop reason, thread id, instruction pointer, and neutral message columns.
- Added **Clear Events** with the application-wide destructive/danger semantic. The event presentation is bounded to the newest 2,000 rows so a long-lived debugger window cannot grow its WPF collection without limit.
- Added UI-thread dispatch for asynchronous Core debugger state/events and command-state refresh when connection/Active Target state changes.
- Kept the new workspace architecture-neutral. It contains no ps5debug-NG packet knowledge, PS5 checks, x86/x64 register-name logic, or platform-specific transport assumptions.

### Added — Host Debugger Session Ownership and Target Safety

- Added host-side registration of active `DebuggerSessionCoordinator` instances to `PluginViewModel`. This gives the target owner an explicit lifetime relationship with every modeless Debugger window using that target session.
- Debugger coordinators are disposed before the owning target session is replaced, disconnected, the Active Target is changed, or the plugin ViewModel itself is disposed. A modeless window therefore cannot keep a backend debugger attachment alive after its target becomes invalid.
- Separated immutable debugger-target identity from temporary command availability. Target identity depends on plugin/process/connection generation and connected-session validity; it does not become stale merely because an unrelated foreground operation temporarily disables creation of a new debugger attachment.
- Starting a new debugger attachment still requires the stricter `CanOpenDebugger` operational gate. This prevents an old-but-current detached window from attaching in the middle of an incompatible target operation while preserving valid attached-session event identity.
- Debugger windows revalidate the captured identity before operations and again when events reach the UI, so stale events are ignored rather than projected into a new target context.

### Added — Deterministic Mock Debugger Backend

- Advanced the In-Memory Test Target plugin from `1.0.0.rev7` / API `2.11.0` to `1.0.0.rev8` / API `2.12.0` because the plugin now consumes the debugger contracts introduced in rev1.
- Added `TargetCapabilities.Debugger` to Mock and exposed a real `IDebuggerProvider` from its connected target session. No advanced debugger capability is advertised yet: thread enumeration/control, register access, breakpoints/watchpoints, call stack, and stepping remain disabled until their corresponding revisions implement them.
- Added a deterministic `IDebuggerSession` for the existing `TestGame.exe` fixture. Attach begins in `Running`, Pause transitions to `Paused`, Continue returns to `Running`, and Detach/disposal returns to `Detached`.
- Pause emits one neutral `Paused` event with `PauseRequested`, deterministic thread id `1`, instruction pointer `0x10000400`, and message `Mock target paused.`. Continue emits one neutral `Resumed` event with the same deterministic thread/instruction context and message `Mock target resumed.`.
- The Mock provider allows only one active debugger attachment and rejects a process that does not belong to its target session. Disposing the target session also disposes any active Mock debugger session.
- Kept all existing Mock scanner, memory, Saved Addresses, export, Memory Viewer, and synthetic Disassembler fixtures unchanged.

### Compatibility and Preserved Functionality

- Kept the public Plugin API at `2.12.0`; rev2 adds no new public SDK contract and therefore does not create an unnecessary API minor version.
- Kept PS5 plugin `0.1.0.rev24` byte-for-byte unchanged and targeting compatible Plugin API `2.11.0`. It still advertises no debugger capability and rev2 sends no PS5 debugger command, opens no debugger event port, and changes no ps5debug-NG transport behavior.
- Preserved the rev1 Plugin SDK/Core debugger foundation byte-for-byte. Rev2 is the first consumer of that verified foundation.
- Preserved all previously verified scanner, scan-result storage, Saved Addresses/freeze, universal export, Memory Viewer, Disassembler, themes, and target I/O behavior outside the narrow host lifetime integration required to own debugger coordinators safely.

### Verification Coverage

- Extended the verification registry from **84** to **89** checks without removing or weakening any existing check.
- Added **Mock debugger provider lifecycle** coverage for API/capability metadata, service discovery, attach state/process identity, absence of unimplemented advanced services, and detach.
- Added **Mock debugger deterministic pause and continue events** coverage for state transitions, exact event count/kind/stop reason, deterministic thread/instruction context, messages, and rejection of Continue while already running.
- Added **Mock debugger attachment exclusivity and target cleanup** coverage for one-active-attachment enforcement, wrong-process rejection, reattach after cleanup, target-session disposal, and service invalidation after target disposal.
- Added **Debugger workspace command and event source contract** coverage for capability-driven entry, Attach/Pause/Continue/Detach/Clear bindings, event columns, connection-generation capture, shared coordinator usage, stale-event checks, and absence of platform-specific host terms.
- Added **Debugger target lifetime and stale-session source contract** coverage for coordinator ownership, disposal before target replacement/disconnect/Active Target changes, and plugin/process/connection-generation validation.
- The authoritative next gate is a clean Windows Release build followed by **89/89** automated checks and the rev2 Mock Debugger runtime/UI acceptance documented in `docs/testing/APP_0.1.7_REV2_VERIFICATION.md`.
- No new live-PS5 debugger hardware acceptance applies to rev2 because the PS5 plugin remains unchanged and does not yet implement `IDebuggerProvider`.

### Documentation

- Updated `README.md` to describe `0.1.7.rev2`, the verified 84/84 rev1 baseline, the new modeless Debugger workspace, deterministic Mock debugger behavior, current plugin/API versions, and the 89-check verification boundary.
- Updated `docs/TeeKay87_Memory_Engine_Full_Development_Action_Plan.md` to mark rev1 verified, identify rev2 as the current implementation candidate, and keep rev3 PS5 Debug Transport and Attach/Detach as the next debugger milestone after rev2 verification.
- Updated `docs/architecture/DEBUGGER_ARCHITECTURE.md` with the rev2 host workspace, target-lifetime ownership rules, Mock backend semantics, and explicit separation between target identity and temporary attach availability.
- Updated current-state Plugin SDK, Disassembly, Memory Viewer, Universal Export, main-workspace, Mock, and PS5 documentation without rewriting historical revision records.
- Added `docs/testing/APP_0.1.7_REV2_VERIFICATION.md` and `docs/testing/APP_0.1.7_REV2_SOURCE_REVIEW.md` for the new revision's Windows/runtime acceptance and pre-package source-boundary review.

### Final Verification Result — 2026-09-08

- The complete Windows verification suite subsequently passed **89/89** checks.
- The complete focused Mock Debugger runtime/UI acceptance also passed: capability gating, modeless open without implicit attach, explicit attach, deterministic pause/resume events, event-history clearing, explicit detach/reattach, window-close cleanup, disconnect/connection-generation invalidation, Active Target lifetime coverage, multiple Debugger-window exclusivity/recovery, and regression smoke testing of the existing Scan, Memory Viewer, Disassembler, Saved Addresses, read/write, themes, and connect/disconnect paths.
- `0.1.7.rev2` is therefore the fully verified host/Mock debugger baseline for the PS5 backend introduced in rev3. No live-PS5 debugger acceptance applied to rev2 because that revision contained no PS5 debugger implementation.


## TeeKay87's Memory Engine 0.1.7.rev1 - Debugger Contracts and Core Foundation

### Version Transition and Verified Baseline

- Advanced the host application from the completed `0.1.6` Disassembler feature block to the new `0.1.7` Debugger feature block and reset the application revision to `rev1`.
- Recorded `0.1.6.rev14 - Disassembler Multi-Selection Copy Consistency` as the final fully tested and hardware-verified `0.1.6` implementation. The completed block passed all **79/79** automated checks plus deterministic Mock runtime acceptance and live PlayStation 5 hardware acceptance.
- Updated the final `0.1.6.rev14` verification record and added `docs/testing/APP_0.1.6_FINAL_VERIFICATION.md` so the completed Disassembler block is an explicit regression boundary for all debugger development.
- Updated `AppInfo` to `0.1.7.rev1 - Debugger Contracts and Core Foundation`; application title/version presentation continues to consume this centralized metadata rather than introducing another hardcoded display value.

### Added — Public Debugger Contracts

- Advanced the public Plugin API additively from `2.11.0` to `2.12.0` for the first architecture-neutral debugger contract surface.
- Added `IDebuggerProvider`, owned by a connected target session, to attach a debugger to a neutral `TargetProcess` without exposing backend transport details to Core or WPF.
- Added `IDebuggerSession` as the lifetime boundary for one attached process. The contract exposes neutral execution state, debugger events, Pause, Continue, Detach, optional attached-session service discovery, and asynchronous disposal.
- Added optional attached-session service contracts for:
  - thread enumeration through `IDebuggerThreadService`;
  - individual-thread suspend/resume through the separate `IDebuggerThreadControlService`;
  - register snapshots and explicit register writes through `IDebuggerRegisterService`;
  - software breakpoints and hardware/watchpoint requests through `IDebuggerBreakpointService`;
  - call-stack retrieval through `IDebuggerCallStackService`;
  - architecture-neutral step requests through `IDebuggerStepService`.
- Added `DebuggerSessionExtensions.GetRequiredService<T>()` so callers can request an optional debugger service through the same explicit failure pattern already used by target sessions.
- Added neutral debugger models for execution states, event kinds, stop reasons, event payloads, threads, register snapshots/write requests, semantic register roles, stack frames, breakpoint/watchpoint requests, breakpoint access modes, and step kinds.
- Register values are represented as fixed-width byte vectors with optional architecture-neutral `InstructionPointer`, `StackPointer`, and `FramePointer` roles. No architecture-specific register enum or backend register structure is introduced into the shared API.
- Added `TargetCapabilities.ThreadControl` as a separate explicit capability so future plugins can advertise thread enumeration without falsely promising per-thread suspend/resume support.

### Added — Core Debugger Foundation

- Added `DebuggerSessionIdentity`, binding debugger work to plugin id, process id/name, and host connection generation. The identity follows the same stale-session safety principle already verified by the Memory Viewer and Disassembler.
- Added Core-owned debugger lifecycle state for detached, attaching, attached, running, paused, detaching, and faulted coordination without exposing a platform-specific backend state machine to the application.
- Added `DebuggerEventContext` and event arguments so every event accepted by Core carries the immutable debugger-session identity plus a positive monotonic session-local sequence number.
- Added `DebuggerSessionCoordinator` as the shared lifecycle/control owner for future debugger workspaces. The coordinator:
  - serializes attach, pause, continue, and detach operations;
  - rejects attach attempts from an invalid lifecycle state;
  - validates that the backend actually attached to the neutral process requested by Core;
  - cleans up failed or mismatched backend sessions;
  - forwards events only from the currently attached debugger session;
  - maps backend execution state into the shared Core lifecycle;
  - resolves optional debugger services only from the active attached session;
  - unsubscribes events and disposes the backend session during detach;
  - prevents stale events from a detached session from being accepted as current debugger state.

### Compatibility and Preserved Functionality

- Kept Mock plugin `1.0.0.rev7` and PS5 plugin `0.1.0.rev24` unchanged. Both continue to target Plugin API `2.11.0` and remain compatible with the `2.12.0` host through the existing same-major/older-minor API compatibility rule.
- Neither built-in plugin advertises `Debugger`, debugger thread/register/breakpoint/watchpoint/call-stack/step capabilities, or exposes `IDebuggerProvider` in rev1. Adding shared contracts does not enable a backend implicitly.
- No PS5 debugger command, event port, packet parser, transport, or x86-specific debugger implementation is added in this revision.
- No WPF Debugger workspace is added in rev1. The existing main workspace, scanner, Saved Addresses, export surfaces, Memory Viewer, and fully verified Disassembler remain unchanged production workflows.
- Preserved the complete `0.1.6.rev14` Disassembler implementation as the navigation/inspection base future debugger revisions will integrate with rather than duplicate or refactor prematurely.

### Verification Coverage

- Extended the verification registry from **79** to **84** checks without removing or weakening any existing check.
- Added **Debugger neutral model contracts** coverage for register value immutability/width validation and the neutral thread/frame/breakpoint/event models.
- Added **Debugger optional service contracts** coverage for attached-session service discovery and required-service failure behavior.
- Added **Debugger session target identity** coverage for plugin/process/connection-generation matching and stale identity rejection.
- Added **Core debugger session lifecycle and event binding** coverage for attach, pause, continue, event identity/sequence forwarding, detach, and backend disposal.
- Added **Core debugger attach safety and cleanup** coverage for wrong-process attach rejection, failed-session cleanup, recovery to a new valid attachment, and suppression of stale post-detach events.
- Added `docs/testing/APP_0.1.7_REV1_VERIFICATION.md` with the authoritative Windows Release build, **84/84** automated-test, compatibility, source-boundary, and regression acceptance procedure.
- Added `docs/testing/APP_0.1.7_REV1_SOURCE_REVIEW.md` recording the pre-package static review: all 79 baseline checks remain registered, exactly five new checks raise the registry to 84, both production plugin trees are byte-identical to rev14, no debugger capability is advertised by either plugin, no platform-specific token appears in the new shared debugger source, project/XAML XML is structurally valid, and documentation links resolve.
- This source revision is a verification candidate until the Windows Release build and all 84 automated checks pass. No new live-PS5 debugger hardware test is required for rev1 because no platform debugger backend exists yet.

### Documentation

- Updated `README.md` to describe the complete current `0.1.7.rev1` functionality instead of retaining rev13/rev14 candidate wording.
- Updated `docs/TeeKay87_Memory_Engine_Full_Development_Action_Plan.md` to mark the entire `0.1.6` Disassembler block complete, make the debugger the active development block, and define the ordered `0.1.7` debugger revision plan from shared contracts through final integration/export verification.
- Added `docs/architecture/DEBUGGER_ARCHITECTURE.md` defining debugger ownership boundaries, capability/service mapping, session identity, event ordering, thread/register/breakpoint/call-stack/step models, rev1 non-goals, and the required extension rules for future platform backends.
- Updated the Plugin SDK, Disassembly, Universal Export, Memory Viewer, main-workspace, Mock-plugin, and PS5-plugin documentation so current-state/version wording matches host `0.1.7.rev1` while preserving historical revision records.

## TeeKay87's Memory Engine 0.1.6.rev14 - Disassembler Multi-Selection Copy Consistency

### Fixed

- Fixed the remaining Disassembler multi-selection copy inconsistency discovered during the rev13 Mock runtime pass. Rev13 correctly preserved the complete Extended selection when any selected row was right-clicked, but **Copy Address**, **Copy Bytes**, **Copy Instruction**, and **Copy Address + Instruction** still copied only the context-clicked row.
- Changed all four granular Disassembler copy actions to consume the same display-ordered selected-row set already used by **Copy Selected**, `Ctrl+C`, and the Selected Instructions export scope.
- Preserved the intended context-selection rule from rev13: right-clicking a row that is already selected keeps the full multi-selection, while right-clicking an unselected row first collapses the selection to that row. Because every copy action now reads the current selected set, the latter case still produces a single-row result naturally.

### Copy Semantics

- **Copy Address** now emits one address per selected instruction row.
- **Copy Bytes** now emits one byte string per selected instruction row.
- **Copy Instruction** now emits one decoded instruction per selected instruction row.
- **Copy Address + Instruction** now emits one `<address>: <instruction>` line per selected instruction row.
- **Copy Selected** and `Ctrl+C` retain the existing `<address>: <bytes>\t<instruction>` format.
- Every multi-row copy projection is normalized through the existing `GetSelectedRowsInDisplayOrder()` path, so clipboard order follows the visible instruction order rather than Ctrl-click order.
- Clipboard operations continue to use the already materialized Disassembler presentation state and perform no new target-memory read or write.

### Added

- Added automated verification check **Disassembler multi-selection copy consistency**. The new source-contract check verifies that the four granular copy handlers project Address, Bytes, Instruction, and Address + Instruction from the shared selected-row formatter, reuse display-order normalization, and no longer contain the old single-context-row `CopyText(...)` paths.
- Increased the verification registry from 78 to **79 checks** without removing or weakening any existing test.
- Added `docs/testing/APP_0.1.6_REV14_VERIFICATION.md` with the focused Windows/Mock regression procedure and the remaining live-PS5 finalization gate for version `0.1.6`.

### Changed

- Advanced centralized application metadata from `0.1.6.rev13` to `0.1.6.rev14` with feature title `Disassembler Multi-Selection Copy Consistency`. The native window title and compact application title remain title-only; the permanent bottom status bar remains the sole persistent version/revision presentation.
- Refactored the Disassembler clipboard path so the existing no-argument `CopySelectedRows()` delegates to a shared formatter overload. The same overload now serves every granular copy projection and retains the existing clipboard status/error handling.
- Updated README, Disassembler architecture documentation, and the full development action plan to describe the final intended selection-aware copy semantics.

### Preserved

- Preserved rev13's right-click selection behavior and row-specific Follow Target capability evaluation.
- Preserved rev12's Displayed/Selected universal export, readable-region navigation, module-relative origin presentation, Mock Custom / Unknown architecture metadata, Follow Target behavior, syntax highlighting, continuous origin resolution, and successful-address Back/Forward history.
- Preserved Plugin API `2.11.0`, Mock plugin `1.0.0.rev7`, and PS5 plugin `0.1.0.rev24`.
- No Core, Plugin SDK, Mock provider, PS5 provider, scanner, target transport, Memory Viewer, Saved Addresses, export writer, or theme behavior is changed by this host-only copy correction.

### Verification and Finalization Boundary

- Rev13 was reported as **78/78 automated checks passed**.
- Focused Mock runtime testing confirmed that rev13 fixed selection preservation: right-clicking any already-selected row kept the full selection, while right-clicking an unselected row correctly selected only that row.
- The same runtime pass confirmed **Copy Selected** and `Ctrl+C` already copied the complete selected set, while the four granular copy commands remained single-context-row operations. Rev14 addresses only that remaining inconsistency.
- The first rev14 Windows gate is a clean build plus **79/79** automated checks, followed by the focused Mock copy regression in `docs/testing/APP_0.1.6_REV14_VERIFICATION.md`.
- After the focused Mock copy regression passes, continue the remaining live-PS5 rev12-rev14 acceptance checklist. Version `0.1.6` must not be marked complete or advanced until that final live-target verification is confirmed.

### Final Verification Result — 2026-09-08

- The required final verification was subsequently completed successfully: **79/79** automated checks passed, the focused Mock regression passed, and the complete `0.1.6` Disassembler workflow passed live PlayStation 5 hardware acceptance.
- `0.1.6.rev14` is therefore the final accepted and hardware-verified revision of the `0.1.6` Disassembler feature block. The detailed final record is maintained in `docs/testing/APP_0.1.6_FINAL_VERIFICATION.md`.

## TeeKay87's Memory Engine 0.1.6.rev13 - Disassembler Context Selection Fix

### Fixed

- Fixed the runtime Disassembler multi-selection regression discovered during the rev12 Mock acceptance pass. Right-clicking a selected row that was not the current `SelectedItem` could collapse an Extended selection to that single row before context-menu copy/export commands observed the selection.
- Added a `PreviewMouseRightButtonDown` guard on the Disassembler instruction grid. When the pointer is over a row that is already part of a multi-selection, the host consumes the selection-changing right-button-down behavior before WPF can replace the existing `SelectedItems` set. Right-clicking an unselected row remains unchanged and intentionally selects only that new context row.
- Removed the context-menu-opening write to the TwoWay-bound `SelectedInstruction` property. Context-menu enablement now evaluates Follow Target capability for the clicked row directly, so simply opening the menu no longer mutates the grid's primary selection and cannot collapse the existing multi-selection through the `SelectedItem` binding.

### Added

- Added row-specific `DisassemblerViewModel.CanFollowTarget(DisassemblyInstructionViewModel?)` evaluation so the context menu can determine whether the clicked instruction has a valid direct target without changing `SelectedInstruction`. Existing command execution still assigns the clicked row only when Follow Target is actually invoked.
- Added automated verification check **Disassembler right-click preserves extended selection**. The check verifies the preview right-click hook, the selected-row multi-selection guard, the retained unselected-row collapse behavior, the absence of a context-menu-opening `SelectedInstruction` rewrite, and row-specific Follow Target capability evaluation.
- Increased the verification registry from 77 to **78 checks** without removing or weakening any existing test.
- Added `docs/testing/APP_0.1.6_REV13_VERIFICATION.md` with a focused Windows/Mock regression procedure and the remaining live-PS5 finalization gate for version `0.1.6`.

### Changed

- Advanced centralized application metadata from `0.1.6.rev12` to `0.1.6.rev13` with feature title `Disassembler Context Selection Fix`. The native window title and compact application title remain title-only; the permanent bottom status bar remains the sole persistent version/revision presentation.
- Updated the current Disassembler documentation and full development action plan to identify rev13 as the corrective final `0.1.6` candidate after rev12's automated suite passed and Mock runtime testing exposed the context-selection defect.

### Preserved

- Preserved rev12's Disassembler multi-selection model, copy formats, Displayed/Selected universal export, readable-region navigation, module-relative origin presentation, Mock Custom / Unknown architecture metadata, Follow Target behavior, syntax highlighting, and successful-address Back/Forward history.
- Preserved the single-row context-menu semantics: Copy Address, Copy Bytes, Copy Instruction, Copy Address + Instruction, and Follow Target still operate on the row that was actually right-clicked; only Copy Selected and Selected Instructions export consume the complete selected set.
- Preserved the intended context behavior for an unselected row: right-clicking outside the current multi-selection collapses the old selection and makes the clicked row the new single selection.
- Preserved Plugin API `2.11.0`, Mock plugin `1.0.0.rev7`, and PS5 plugin `0.1.0.rev24`. No Core, Plugin SDK, Mock provider, PS5 provider, scanner, target transport, Memory Viewer, Saved Addresses, or export-writer behavior is changed by this host-only correction.

### Verification and Finalization Boundary

- The rev12 automated suite was reported as **77/77 passed** before runtime testing began.
- Mock runtime verification confirmed the rev12 Custom / Unknown architecture presentation, deterministic disassembly fixture, Follow Target/history, region navigation, copy through `Ctrl+C`, universal export, and the other tested rev12 behavior. The only reported defect was the right-click collapse of a multi-selection when the clicked row was not the primary selected row.
- Rev13 is intentionally a narrow corrective revision. The first Windows gate is a clean build plus **78/78** automated checks, followed by a focused Mock right-click regression: right-click the first, middle, and final rows of the same multi-selection and confirm the full selection remains intact; right-click an unselected row and confirm selection collapses only in that case.
- After the focused rev13 regression passes, continue the remaining rev12/rev13 live-PS5 acceptance checklist. Version `0.1.6` must not be marked complete or advanced until that final live-target verification is confirmed.

## TeeKay87's Memory Engine 0.1.6.rev12 - Disassembler Export, Region Navigation and Finalization

### Added

- Added **extended multi-selection** to the Disassembler instruction table. Ctrl/Shift selection now works without changing the existing Address / Bytes / Instruction column structure, syntax renderer, origin-row geometry, or Follow Target selection semantics.
- Added Disassembler copy actions for **Copy Address**, **Copy Bytes**, **Copy Instruction**, **Copy Address + Instruction**, and **Copy Selected**. `Ctrl+C` invokes Copy Selected, and multi-row clipboard output is normalized to the current displayed instruction order rather than selection-click order.
- Added a visible **Export** button and an **Export...** context-menu action to the Disassembler. The workflow reuses the existing universal export dialog, column selector, format selector, transactional writer, progress/cancellation dialog, and destination-publication guarantees instead of introducing a Disassembler-specific file writer.
- Added Core `DisassemblyExportSource`, a structured `IExportDataSource` for Disassembler rows. Available columns are Address, Bytes, Instruction, Mnemonic, Operands, Length, Flow Control, Branch Target, Valid, Region / Module, Protection, and Module Relative. JSON therefore preserves structured instruction data instead of exporting only the rendered combined instruction string.
- Added **Displayed Instructions** and **Selected Instructions** export scopes. Selected export snapshots only the current selected rows while preserving their displayed order; Displayed exports the complete bounded instruction list currently materialized in the workspace.
- Added shared host `ExportDestinationPicker` so Scan Results, Saved Addresses, and Disassembler export use one file-extension/filter/destination implementation for JSON, CSV, TSV, and Markdown table output.
- Added Disassembler region-navigation commands: **Previous Region**, **Region Start**, **Region End**, and **Next Region**. Previous/Next reuse Core's existing `MemoryViewerRegionNavigator`, so readable/non-guarded filtering is shared rather than reimplemented in the Disassembler.
- Added module-relative origin presentation when the current memory map provides a real `ModuleName`. The host resolves the lowest mapped base for that module and presents the current origin as `<module> + 0x<offset>` without fabricating module names for anonymous mappings.
- Added automated verification checks for structured Disassembly export, Disassembler selection/copy/export wiring, readable-region navigation/module-relative presentation, and Mock Target custom-architecture declaration. The verification registry now contains **77 checks**.
- Added `docs/testing/APP_0.1.6_REV12_VERIFICATION.md` as the combined final acceptance checklist for the complete `0.1.6` Disassembler feature block.

### Changed

- Advanced centralized application metadata from `0.1.6.rev11` to `0.1.6.rev12` with feature title `Disassembler Export, Region Navigation and Finalization`. The native window title and compact application title remain title-only; `AppInfo.DisplayVersion` remains the sole persistent version/revision presentation in the bottom status bar.
- Changed Mock Target architecture metadata from `X64` to `Unknown` while retaining 64-bit addresses, 64-bit pointers, and little-endian byte order. The Mock provider implements a synthetic deterministic instruction set, so presenting it as x86-64 was semantically incorrect even though its address model is 64-bit.
- Updated `MockDisassemblerProvider` to accept only the Mock Target's custom/unknown 64-bit little-endian architecture descriptor and to reject a real X64 descriptor. This prevents the synthetic provider from claiming compatibility with real x86-64 targets.
- Advanced the Mock plugin from `1.0.0.rev6` to **`1.0.0.rev7`** for the architecture-metadata correction. Plugin API remains `2.11.0`. PS5 plugin remains `0.1.0.rev24` because rev12 adds no PS5-specific decoding or transport behavior.
- Updated the Disassembler Architecture display so a neutral `CpuArchitecture.Unknown` descriptor is shown as **Custom / Unknown** instead of implying a hardware ISA.
- Refactored the existing Scan Results and Saved Addresses export destination selection to use the new shared `ExportDestinationPicker`; their scopes, data sources, output formats, transactional behavior, and user-visible semantics are unchanged.

### Disassembler Export Semantics

- Disassembly export is a presentation snapshot and performs no new target-memory reads while writing the file. The exported rows come from the already materialized bounded Disassembler snapshot.
- JSON uses the existing schema-aware universal export writer and retains typed values where appropriate, including numeric Length and boolean Valid. A missing direct Branch Target remains null/blank rather than being inferred from rendered operand text.
- Region / Module and Protection are taken from the actual memory region associated with the current Disassembler snapshot. Module-relative values are emitted only when a real module name/base can be resolved from the loaded memory map.
- Export cancellation and failure retain the existing transactional guarantee: the requested destination is published only after the complete export succeeds; partial temporary output is not exposed as a successful file.

### Region and Navigation Semantics

- Previous/Next Region consider only readable, non-guarded regions, exactly like Memory Viewer. Region Start navigates to the first byte; Region End navigates to the final byte (`EndAddressExclusive - 1`).
- Every successful region navigation uses the same `NavigateAndRecordAsync` path as Go To and Follow Target, so Back/Forward history remains one coherent successful-address timeline.
- Failed region navigation/read attempts keep the previous instruction list and history position, preserving the existing stale-session and read-failure safety behavior.
- Refresh continues to re-read the current origin without creating a history entry, and origin/instruction-boundary resolution remains the continuous rev5 behavior.

### Preserved

- Preserved Plugin API `2.11.0` and PS5 plugin `0.1.0.rev24`. No Plugin SDK contract, PS5 Iced decoder behavior, ps5debug-NG transport, scanner path, or target I/O contract changes are required for rev12.
- Preserved rev11 Follow Target behavior for direct provider-supplied Call/Jump/ConditionalJump targets, including context-menu, double-click, Enter, Back/Forward integration, and rejection of unresolved indirect targets.
- Preserved the rev5 continuous 512-byte-before / 512-byte-from-origin bounded decode model, origin resolution when the requested address lies inside an instruction, rev6 syntax highlighting, and the stable three-column Disassembler layout.
- Preserved Memory Viewer value-span highlighting, Saved Addresses manual entry, Scan Results/Saved Addresses context menus, freeze/write behavior, universal export consumers, themes, and unrelated verified application functionality.

### Verification and Finalization Boundary

- Rev12 starts from the user-supplied `0.1.6.rev11` package whose Follow Target behavior was runtime-confirmed.
- The planned `0.1.6.rev12` selection/export work and former `0.1.6.rev13` region-navigation/final-polish work are intentionally combined into this single final implementation candidate, so one complete Windows/Mock/PS5 acceptance pass can verify the finished Disassembler subsystem as a whole.
- Static review confirms the new platform-specific behavior is limited to the Mock plugin's metadata correction; Disassembler export/navigation remains shared Core/host functionality. No PS5/x86-64 assumptions were added to Core/WPF.
- A clean Windows build and **77/77** automated checks are the first acceptance gate. Final `0.1.6` completion additionally requires the rev12 runtime/UI checklist: multi-selection/copy, all export formats/scopes, Previous/Start/End/Next region navigation, module-relative presentation where available, Refresh/history, Follow Target regression, Mock Target behavior, theme/layout regression, stale-session safety, and live PS5 verification.
- `0.1.6` must not be marked complete or advanced to `0.1.7` until that combined verification is confirmed.

## TeeKay87's Memory Engine 0.1.6.rev11 - Branch and Call Target Navigation

### Added

- Added **Follow Target** to the Disassembler row context menu. The action is enabled only for valid neutral instruction records whose provider supplied a direct `BranchTarget` and whose `FlowControl` is `Call`, `Jump`, or `ConditionalJump`.
- Added double-click target following for the same eligible direct-flow rows. Double-clicking an ordinary instruction or an indirect flow instruction does not navigate.
- Added **Enter** as a keyboard shortcut for following the currently selected direct target. The context-menu item advertises the same shortcut.
- Added row-level `CanFollowTarget` presentation state derived entirely from the existing neutral `DisassembledInstruction` fields. No opcode parsing, register-name matching, operand-text parsing, or platform branching was introduced in WPF.
- Added `FollowTargetCommand` to the Disassembler ViewModel. Successful target following reuses the same `NavigateAndRecordAsync` path as Go To, so Follow Target participates naturally in Back/Forward history and in forward-branch truncation after navigating Back.
- Added automated verification check **Disassembler direct target navigation contract**. The verification project now copies the production Disassembler XAML, code-behind, row ViewModel, and workspace ViewModel as fixtures and verifies the direct-target gating, interaction wiring, shared history path, and absence of PS5/Iced-specific host logic.
- Added `docs/testing/APP_0.1.6_REV11_VERIFICATION.md` with Mock and live-PS5 direct Call/Jump/ConditionalJump, indirect-target safety, failure/history, keyboard/mouse/context-menu, theme, and cross-workspace regression checks.

### Changed

- Advanced centralized host metadata from `0.1.6.rev10` to `0.1.6.rev11` with feature title `Branch and Call Target Navigation`. Version/revision remains visible only in the permanent bottom status bar; native and compact application titles remain title-only.
- Increased the automated verification registry from 72 to **73 checks**. No existing check was removed or weakened.
- Updated README and current Disassembler/UI documentation so direct target navigation is described as implemented rather than a future milestone.

### Safety and Navigation Semantics

- Direct target navigation never derives an address by parsing rendered operand text. The host trusts only the neutral provider's nullable `BranchTarget`.
- Indirect instructions such as `call rax`, `call [rax]`, and `jmp rbx` remain non-followable because their provider records intentionally expose no static target. Future debugger/register context may add a separate dynamic-resolution path, but rev11 does not guess.
- A successful Follow Target read becomes a normal Disassembler history entry. Back/Forward therefore traverses Go To and Follow Target destinations uniformly.
- A failed target read keeps the previous instruction list and history position, matching the already verified failed-Go-To semantics.
- Follow Target continues through the captured Disassembler plugin/process/connection-generation identity and the existing foreground target reservation; it does not open a new transport or bypass stale-session protection.

### Preserved

- Preserved Plugin API `2.11.0`, Mock plugin `1.0.0.rev6`, and PS5 plugin `0.1.0.rev24`. Both providers already supplied the direct/indirect target metadata rev11 consumes, so no plugin revision is required.
- Preserved Core disassembly reads and validation byte-for-byte. Rev11 does not change the 512-before/512-after bounded context, continuous decode stream, instruction-boundary/origin resolution, region clamping, or provider-output validation.
- Preserved Disassembler syntax highlighting and the existing Address / Bytes / Instruction layout and row geometry.
- Preserved Scan Results, Saved Addresses, Memory Viewer, manual Saved Address entry, value-span highlighting, scanner behavior, universal export, themes, target I/O coordination, and all platform-specific behavior unrelated to target following.

### Verification and Documentation

- Rev11 starts from the user-supplied `0.1.6.rev10` package.
- Source review confirms the functional implementation is limited to the host Disassembler presentation/navigation layer plus verification/documentation and centralized host metadata. Core, Plugin SDK, Mock plugin, and PS5 plugin behavior is unchanged.
- Windows clean build and **73/73** automated checks are the authoritative first verification gates. Runtime acceptance then requires direct Call/Jump/ConditionalJump following plus indirect-target rejection and Back/Forward verification on Mock and PS5.

## TeeKay87's Memory Engine 0.1.6.rev10 - Manual Saved Address Compile Fix

### Fixed

- Fixed Windows compile error `CS0177` in `ManualSavedAddressDialog.TryParseHexAddress`. The rev9 implementation returned a short-circuit `candidate.Length ... && ulong.TryParse(..., out address)` expression. For candidates that failed the length guard, `ulong.TryParse` was not executed and the compiler could not prove that the `out address` parameter had been assigned before the method returned.
- Initialized the `out` address to `0` before address normalization and the short-circuit length check. Valid hexadecimal parsing behavior is unchanged; invalid length/input paths now return `false` with a definitely assigned output value.
- Addressed the root cause of the accompanying XAML designer/build errors reported for `MainWindow.xaml`. Those missing-type/member errors appeared after the App assembly failed to compile and are not treated as independent changes to `ProportionalGridSplitter`, `TextBoxInputFilter`, or their XAML namespaces.

### Added

- Added automated verification check **Saved Addresses manual-entry address parser out initialization**. The verification project now copies `ManualSavedAddressDialog.xaml.cs` as a production source fixture and verifies that the parser initializes its `out` address before the short-circuit length guard.
- Added `docs/testing/APP_0.1.6_REV10_VERIFICATION.md` covering the clean Windows build, disappearance of the cascading XAML errors, the 72-check suite, invalid/valid manual address parsing, variable-length validation, and regression smoke tests.

### Changed

- Advanced centralized host metadata from `0.1.6.rev9` to `0.1.6.rev10` with feature title `Manual Saved Address Compile Fix`. Version/revision remains visible only in the permanent bottom status bar; native and compact application titles remain title-only.
- Increased the automated verification registry from 71 to **72 checks**. No existing check was removed or weakened.
- Updated README and the current Saved Addresses, Memory Viewer, Disassembler, main-workspace, input-validation, theme, and research documentation to identify rev10 as the corrective host state while preserving rev9 as the revision that introduced manual Saved Address entry.

### Preserved

- Preserved the rev9 **Add Manually / Remove All / Export** toolbar order and all manual-dialog controls, validation ranges, target/session revalidation, duplicate semantics, immediate refresh, and disabled planned placeholders.
- Preserved Plugin API `2.11.0`, Mock plugin `1.0.0.rev6`, and PS5 plugin `0.1.0.rev24`; no Core, Plugin SDK, scanner, decoder, or platform-plugin behavior is changed by this fix.
- Preserved Memory Viewer value-span highlighting and explicit OneWay bindings, and preserved Disassembler continuous decoding, syntax highlighting, navigation history, and workspace entry points.

### Verification and Documentation

- Rev10 starts from the user-supplied rev9 package after the first Windows build exposed `CS0177` at the manual hexadecimal parser and cascading XAML designer errors.
- The functional code correction is intentionally one-line and host-side: `address = 0;` is assigned before the existing parser guards.
- Windows clean build and **72/72** automated checks are the authoritative verification gates. Runtime smoke testing must confirm the manual dialog opens and rejects invalid hexadecimal addresses without exception.

## TeeKay87's Memory Engine 0.1.6.rev9 - Manual Saved Address Entry

### Added

- Added **Add Manually** to the Saved Addresses toolbar, producing the requested toolbar order **Add Manually / Remove All / Export** without changing the Saved Addresses table layout or the existing destructive styling of Remove All.
- Added a theme-aware **Add Saved Address Manually** dialog. Functional fields are Address, Description, Value Type, and Length. Address accepts the existing host hexadecimal-address syntax, Value Type is populated only from the active plugin's current `IMemoryValueType` declarations, and fixed-size types display their required byte count while variable-length types accept an explicit 1-4,096 byte length.
- Added immediate post-create value refresh. A successfully created manual row is bound to the current Active Target and is immediately read through the existing Saved Addresses refresh path; from that point it uses the same Value editing, Freeze, refresh, Protection, export, Memory Viewer, Disassembler, copy, and remove workflows as a row captured from Scan Results.
- Added target-safe duplicate handling for manual rows using the existing Saved Address identity rule: target process + address + Value Type. Attempting to add an identity that is already saved selects the existing row instead of creating a duplicate.
- Added visible disabled placeholders based on the public Cheat Engine manual-address workflow for functionality that Memory Engine does not yet model: hexadecimal display preference, Binary start bit, Text Unicode/code-page options, and Pointer base-address/offset controls with Add Offset / Remove Offset. The placeholders are explicitly marked as planned and cannot affect a created row.
- Added `docs/research/CHEAT_ENGINE_MANUAL_ADDRESS_DIALOG.md`, documenting the reviewed public Cheat Engine controls and the neutral mapping chosen for Memory Engine rather than copying platform-specific behavior into WPF.
- Added `docs/testing/APP_0.1.6_REV9_VERIFICATION.md` with clean-build, toolbar/dialog, fixed-size/variable-size, duplicate, invalid/unreadable address, placeholder, theme, Mock Target, and live PS5 acceptance checks.
- Added two automated verification checks for the production Saved Addresses toolbar contract and the manual-entry dialog contract. The verification executable now carries `MainWindow.xaml` and `ManualSavedAddressDialog.xaml` as fixtures for these UI-level regression checks.

### Changed

- Advanced centralized host metadata from `0.1.6.rev8` to `0.1.6.rev9` with feature title `Manual Saved Address Entry`. The native Windows title and compact application-title row remain title-only; the permanent bottom status bar remains the only persistent version/revision presentation.
- Extended `SavedAddressViewModel` with a manual-construction path that starts without fabricated target bytes and then receives its real current bytes through the already-existing refresh pipeline. The original Scan Result construction path retains its previous initialization semantics.
- Centralized manual variable-length constraints in host `ManualSavedAddressEntryLimits` (`1` minimum, `10` default, `4,096` maximum) so the dialog and final commit guard cannot drift to different limits.
- Added `CanAddSavedAddressManually` and a foreground-safe manual-add operation to `PluginViewModel`. The command is available only when the selected plugin can read memory, exposes at least one Value Type, has a valid current Active Target, and is not in an incompatible target operation. The operation captures and revalidates session/process identity before the row is committed.
- Updated current README, Saved Addresses, main-workspace, input-validation, theme, Memory Viewer, and Disassembler documentation for the rev9 host state and manual-entry workflow.
- Increased the automated verification registry from 69 to **71 checks**. No existing check was removed or weakened.

### Preserved

- Preserved Plugin API `2.11.0`, Mock plugin `1.0.0.rev6`, and PS5 plugin `0.1.0.rev24`. Manual entry uses already-declared neutral Value Types and target services; no Plugin SDK, Core, decoder, scanner, or platform-plugin source change is required.
- Preserved all existing Saved Address rows and behaviors. Double-click/**Save Address** capture from Scan Results, direct Address/Type/Value editing, Frozen writes, independent live refresh, target identity, Protection display, export snapshots, Remove/Remove All, and Memory Viewer/Disassembler entry points remain unchanged.
- Preserved the established Saved Address duplicate identity semantics instead of introducing a separate manual-only rule.
- Preserved the rev7/rev8 Memory Viewer value-span renderer and explicit OneWay binding correction. Manual entry does not alter Memory Viewer highlighting, read/write safety, history, bookmarks, or region navigation.
- Preserved the rev5/rev6 Disassembler continuous context decoding, origin resolution, syntax highlighting, Back/Forward history, and workspace routing. Rev9 does not change disassembly contracts or presentation.
- Preserved theme compatibility. The new dialog uses existing shared WPF brushes/styles and disabled-control presentation and introduces no new required theme key or hardcoded platform-specific color.

### Verification and Documentation

- Rev9 starts from the user-supplied, runtime-working `0.1.6.rev8` package after the rev8 Memory Viewer binding correction.
- Source review confirms the functional code change is host-side Saved Addresses/UI work. Core, Plugin SDK, Mock plugin, and PS5 plugin remain source-identical to the rev8 base.
- The manual dialog intentionally does not claim pointer-chain support. Pointer base/offset controls remain disabled until a platform-neutral pointer model, dereference rules, target/session handling, and verification path exist.
- The automated verification registry contains **71 checks**. Windows clean build, **71/71** automated checks, Mock Target manual-add runtime testing, and live-PS5 manual-add testing remain the authoritative acceptance gates.


## TeeKay87's Memory Engine 0.1.6.rev8 - Memory Viewer Value-Span Binding Fix

### Fixed

- Fixed the runtime `InvalidOperationException` that occurred when opening Memory Viewer after the rev7 value-span renderer was introduced. WPF was allowed to choose its default binding mode for the six `Run.Text` bindings that render `HexPrefix`, `HighlightedHexBytes`, `HexSuffix`, `AsciiPrefix`, `HighlightedAscii`, and `AsciiSuffix`; those source properties are intentionally read-only presentation values, so a TwoWay/OneWayToSource binding cannot target them.
- Set all six Memory Viewer `Run.Text` bindings explicitly to `Mode=OneWay`. The inline prefix/highlight/suffix renderer remains unchanged otherwise, so the full known value span can still be highlighted independently inside Hex Bytes and ASCII without requiring writable ViewModel properties or presentation-only setters.

### Added

- Added the automated verification check **Memory Viewer value-span bindings are OneWay**. The verification project now copies the production `MemoryViewerWindow.xaml` as a test fixture and asserts that every read-only inline segment used by the value-span renderer is explicitly OneWay-bound. This directly covers the runtime failure reported against rev7 rather than only retesting the Core span-intersection calculations.
- Added `docs/testing/APP_0.1.6_REV8_VERIFICATION.md` with the clean-build, 69-check, Memory Viewer opening, four-byte span, cross-row span, history, clipboard/edit, and Disassembler-route acceptance steps for the corrective revision.

### Changed

- Advanced centralized host metadata from `0.1.6.rev7` to `0.1.6.rev8` with feature title `Memory Viewer Value-Span Binding Fix`. The native Windows title and compact application-title row remain title-only; the permanent bottom status bar remains the only persistent version/revision presentation.
- Updated README and the current Memory Viewer/UI/theme/disassembly documentation to identify `0.1.6.rev8` as the current host revision while preserving the established rev7 value-span behavior and all earlier revision history.
- Increased the automated verification registry from 68 to **69 checks**. No existing verification case was removed or weakened.

### Preserved

- Preserved the rev7 `MemoryViewHighlightSpan` / `MemoryViewRowHighlight` calculations, source-size propagation from Scan Results and Saved Addresses, cross-row span behavior, one-byte manual/bookmark/region navigation, span-aware Back/Forward state, larger bounded pages for unusually long values, and status reporting for partially visible spans.
- Preserved Memory Viewer row geometry and presentation semantics: Address / Hex Bytes / ASCII remains the table format, the green origin-row surface remains independent from the byte-span overlay, and the fix introduces no font-weight, border, margin, padding, column-width, or row-height change.
- Preserved plain clipboard/edit data. `Bytes`, `HexBytes`, `Ascii`, `ClipboardRow`, and the verified full-row raw-byte editing/write/read-back path are unchanged; the OneWay fix affects only WPF presentation bindings.
- Preserved Plugin API `2.11.0`, Mock plugin `1.0.0.rev6`, and PS5 plugin `0.1.0.rev24`. No plugin source, disassembly provider, target transport, scanner, Saved Addresses model, universal export contract, or platform-specific behavior changes in rev8.
- Preserved rev6/rev7 Disassembler syntax highlighting, continuous around-origin decode, successful-address Back/Forward history, Scan Results/Saved Addresses entry points, and Memory Viewer **Open in Disassembler** routing.

### Verification and Documentation

- Rev8 starts from the user-supplied `0.1.6.rev7` package after a Windows runtime test exposed the read-only `Run.Text` binding exception immediately when Memory Viewer attempted to render its value-span cells.
- Source review confirms the corrective code path is limited to explicit OneWay binding metadata plus its verification fixture/check; the Core value-span logic and platform plugins are unchanged.
- The automated verification registry contains **69 checks**. The new binding-mode regression check complements, rather than replaces, the rev7 Core value-span highlighting check.
- Windows clean build, **69/69** automated checks, and runtime opening of Memory Viewer remain the authoritative acceptance gates. Runtime verification should repeat the four-byte `0x42D43C` case and confirm that the viewer opens without exception and highlights the intended four bytes in Hex and ASCII.


## TeeKay87's Memory Engine 0.1.6.rev7 - Memory Viewer Value-Span Highlighting

### Added

- Added Core `MemoryViewHighlightSpan` / `MemoryViewRowHighlight` models for platform-neutral intersection of an exact address + byte count with each visible Memory Viewer row. The calculation supports spans contained inside one row, spans that cross row boundaries, non-overlapping rows, and high addresses without unsigned overflow.
- Added value-size-aware Memory Viewer entry payloads from both existing navigation sources. **Scan Results -> Open in Memory Viewer** now passes `CurrentValue.Size`; **Saved Addresses -> Open in Memory Viewer** now passes the row's current `ValueSize`. The host does not infer size from display text or platform-specific type names.
- Added byte-level visual highlighting inside the existing **Hex Bytes** and **ASCII** cells. The complete visible source-value span is rendered using the existing theme-aware Primary button background/text brush pair, while the containing row keeps the already-verified green origin marker. No new required theme key is introduced.
- Added span-aware Memory Viewer history state. Back/Forward now restores both the successful requested address and the byte-count highlight that belonged to that navigation entry. Manual Go To, bookmarks, and region navigation intentionally create one-byte highlight entries because those workflows do not carry external value-size context.
- Added larger bounded Memory Viewer page requests for unusually long known source values. Values that fit comfortably inside the normal 512-byte view keep the established page size; larger spans request approximately twice their byte count plus row-alignment headroom, capped by the existing `MemoryViewerReader.MaximumWindowByteCount` of 65,536 bytes and still clamped to one readable non-guarded region.
- Added `docs/testing/APP_0.1.6_REV7_VERIFICATION.md` covering the 68-check suite, four-byte PS5 example at `0x42D43C`, cross-row spans, Saved Address Value Type size propagation, manual one-byte navigation, Back/Forward span restoration, theme/layout behavior, clipboard/edit regressions, and large Array-of-Bytes visibility.

### Changed

- Advanced centralized host metadata from `0.1.6.rev6` to `0.1.6.rev7` with feature title `Memory Viewer Value-Span Highlighting`. The native window title and compact top application row remain title-only; `0.1.6.rev7` remains the only persistent version/revision text in the bottom status bar.
- Replaced only the Memory Viewer **Hex Bytes** and **ASCII** DataGrid column cell presenters with inline prefix/highlight/suffix templates. The user-facing table remains **Address / Hex Bytes / ASCII**, uses the same column widths and monospaced text, and does not add a new column, border, font-size change, bold weight, margin, padding, or row-height change.
- Extended `MemoryViewerRowViewModel` with presentation-only highlight segments while preserving the existing plain `Bytes`, `HexBytes`, `Ascii`, and `ClipboardRow` values. Copy and edit workflows therefore continue to consume the same unformatted byte/text data rather than rendered highlight markup.
- Changed Memory Viewer navigation history from address-only entries to address + highlight-byte-count entries. Duplicate detection now compares both values, Refresh remains history-neutral, and a new successful navigation after Back still discards the abandoned Forward branch.
- Updated the Memory Viewer status text for multi-byte source entries to report whether the full value span is visible. If a region/address-space/window boundary prevents the complete span from appearing, the status reports the visible highlighted byte count instead of claiming full coverage.
- Updated README and current Memory Viewer/theme/main-workspace/disassembly/export/PS5 documentation to describe host `0.1.6.rev7` and make clear that Plugin API and platform-plugin behavior are unchanged.

### Preserved

- Preserved Plugin API `2.11.0`, Mock plugin `1.0.0.rev6`, and PS5 plugin `0.1.0.rev24`. No plugin source, decoder, scan transport, or capability contract change is required for this host/Core presentation feature.
- Preserved the existing `MemoryViewerReader` safety rules: every read remains bounded, belongs to one readable non-guarded `MemoryRegion`, and uses the same target/process/connection-generation validation plus foreground target-I/O reservation.
- Preserved the original 512-byte page for normal one-byte navigation and ordinary numeric source values. Larger pages are requested only when the known source span is large enough that the normal centered page could not contain it fully.
- Preserved the independent green origin-row marker and ordinary DataGrid selection behavior. The new byte-span overlay does not replace origin state and does not make additional rows navigation origins when a span crosses a row boundary.
- Preserved Memory Viewer row editing semantics: **Edit... / Edit Hex Bytes...** still edits and verifies the complete selected displayed row. The visual value-span highlight does not silently narrow the existing write range.
- Preserved all Memory Viewer clipboard outputs exactly. Highlighted runs are presentation-only; copied Hex Bytes/ASCII/row text contains the same plain text as rev6 with no inserted brackets, markup, or delimiters.
- Preserved rev6 Memory Viewer **Open in Disassembler**, Disassembler syntax highlighting, continuous origin resolution, Back/Forward history, Scan Results/Saved Addresses Disassembler entry points, and all previously verified scanner, storage, Saved Addresses, export, theme, and target-state behavior.

### Verification and Documentation

- Rev7 starts from the user-supplied `0.1.6.rev6` package after all **67/67** automated checks passed. Runtime testing also confirmed the new Disassembler syntax highlighting in the Dimmed theme, the Scan Results/Saved Addresses/Memory Viewer navigation entry points, and working Disassembler Back/Forward history.
- The automated verification registry now contains **68 checks**. The new `Memory Viewer value-span highlighting` test verifies in-row and cross-row range intersections, non-overlap behavior, high-address saturation, and invalid zero-length rejection; all 67 rev6 checks remain present.
- Source review confirms the implementation introduces no PS5/x86/Iced knowledge into Memory Viewer/Core highlighting and no new platform command path. Source size comes from existing neutral scan/Saved Address value metadata.
- Windows clean build, **68/68** automated checks, and runtime/UI verification remain the authoritative acceptance gates. Runtime verification should specifically confirm the previously observed `0x42D43C` four-byte case, a cross-row value span, one-byte manual navigation, Back/Forward span restoration, and layout stability in the active themes.


## TeeKay87's Memory Engine 0.1.6.rev6 - Disassembly Syntax Highlighting and Navigation History

### Added

- Added architecture-neutral Disassembler syntax presentation metadata to Plugin API `2.11.0` through `DisassemblyTextToken` and `DisassemblyTextTokenKind`. Providers may now describe formatted instruction text as semantic `Mnemonic`, `FlowControlMnemonic`, `Register`, `Number`, `Keyword`, or plain `Text` segments without exposing target-specific register/opcode knowledge to Core or WPF.
- Added a backward-compatible syntax-token surface to `DisassembledInstruction`. The original constructor remains present with its previous signature and produces an empty token collection, while the new overload accepts provider-supplied tokens. Existing compatible Plugin API 2.x plugins therefore continue to render plain instruction text even when they do not opt into syntax metadata.
- Added PS5 syntax-token generation to `Ps5X64DisassemblerProvider`. Iced `1.21.0` remains the architecture-specific decoder/formatter inside the PS5 plugin; its `FormatterOutput` token kinds are translated into the neutral Plugin SDK categories before leaving the plugin. Flow-control mnemonics are classified through the already-neutral `DisassemblyFlowControl` result rather than through WPF string matching.
- Added deterministic syntax-token output to the Mock disassembler so syntax presentation can be regression-tested without hardware.
- Added theme-aware Disassembler syntax brush resources for mnemonic, flow control, register, number, and keyword text. Bundled Light, Dimmed, and Dark themes define explicit values. The theme loader treats the new syntax colors as optional extensions with semantic fallbacks to existing required palette entries, so custom theme files created before rev6 remain valid.
- Added successful-address **Back** / **Forward** navigation history to the Disassembler, including Alt+Left / Alt+Right shortcuts. The initial address and successful Go To operations are recorded; Refresh is intentionally history-neutral, failed reads do not advance history, and a new successful navigation after going Back removes the abandoned Forward branch.
- Added **Open in Disassembler** to the Memory Viewer row context menu. The action opens the selected row address through the existing generic Disassembler workspace and is enabled only while the Memory Viewer’s captured plugin/process/connection generation still matches a current Active Target that can provide Disassembly.
- Added `docs/testing/APP_0.1.6_REV6_VERIFICATION.md` covering the 67-check automated suite, syntax-token fidelity, Light/Dimmed/Dark highlighting, unchanged table geometry, Disassembler history semantics, Memory Viewer entry-point safety, Mock runtime behavior, and live PS5 preservation checks.

### Changed

- Advanced centralized host metadata from `0.1.6.rev5` to `0.1.6.rev6` with feature title `Disassembly Syntax Highlighting and Navigation History`. The native window title and compact top application row remain title-only; `0.1.6.rev6` remains visible in the permanent bottom status bar.
- Advanced Plugin API compatibility metadata from `2.10.0` to `2.11.0` for the additive neutral syntax-token model.
- Advanced Mock plugin metadata from `1.0.0.rev5` to `1.0.0.rev6` because its disassembler now emits neutral syntax tokens and targets Plugin API `2.11.0`.
- Advanced PS5 plugin metadata from `0.1.0.rev23` to `0.1.0.rev24` because its x86-64 provider now exports neutral syntax-token metadata and targets Plugin API `2.11.0`.
- Changed only the rendered content implementation of the existing **Instruction** DataGrid column from a plain generated text element to a custom TextBlock that emits WPF `Run` elements for provider tokens. The user-facing table remains **Address / Bytes / Instruction** with the same widths, row selection, origin background, virtualization, and monospaced instruction text.
- Moved the existing tag-based context-menu enabled-state helper out of `MainWindow` into a reusable host UI utility so Scan Results, Saved Addresses, and the new Memory Viewer Disassembler action share one implementation instead of duplicating menu-item lookup/state logic.
- Updated README and current architecture/UI/plugin documentation to describe Plugin API `2.11.0`, syntax-token ownership, theme fallback behavior, active Disassembler history, and Memory Viewer-to-Disassembler navigation.

### Preserved

- Preserved the rev5 continuous decode/origin-resolution path unchanged: up to 512 bytes before plus 512 bytes from the requested origin are read once, clamped to one readable non-guarded region, and decoded as one continuous provider stream. Syntax highlighting does not trigger additional target reads or a second decode pass.
- Preserved `IDisassemblerProvider` itself and all established memory-I/O/session boundaries. PS5 decoding still uses caller-supplied bytes through Iced inside the PS5 plugin; no new ps5debug-NG command path is introduced.
- Preserved plain-text compatibility for providers that return no syntax tokens. The custom Instruction renderer validates that token text reconstructs the existing combined instruction string and falls back to the original plain text when metadata is absent or inconsistent.
- Preserved the exact requested address as Disassembler history/origin state even when the highlighted instruction begins before that byte.
- Preserved Scan Results and Saved Addresses **Open in Memory Viewer** / **Open in Disassembler** behavior and their existing target-safety rules.
- Preserved all Memory Viewer read/write/history/bookmark/region-navigation behavior; rev6 only adds a new context-menu route out to the Disassembler and does not change Memory Viewer memory acquisition or editing.
- Preserved all previously verified scanner, scan-result storage, Saved Addresses, universal export, target-I/O coordination, process-control, theme switching, and version/title presentation behavior.

### Verification and Documentation

- The automated verification registry now contains **67 checks**: all 66 rev5 checks remain present and a new neutral syntax-token model regression check is added. Existing Mock and PS5 disassembly checks are also extended to verify token categories and that token concatenation exactly reproduces the previously rendered instruction text.
- Source-level review verifies that no x86 register names, opcode tables, or Iced types are introduced into Core or WPF. Iced-specific `FormatterTextKind` handling remains confined to the PS5 plugin.
- The runtime acceptance focus for this revision is visual: syntax colors must improve readability in Light, Dimmed, and Dark without changing the three-column Disassembler layout, row dimensions, selection/origin geometry, or the exact instruction text seen in rev5.


## TeeKay87's Memory Engine 0.1.6.rev5 - Continuous Disassembly Stream and Origin Resolution

### Changed

- Changed Core `DisassemblyReader.ReadAroundAsync(...)` from the rev4 two-stream strategy to one continuous decode across the complete bounded around-origin byte range. Core still performs one bounded target read and still requests up to 512 bytes before plus 512 bytes from the requested origin, but the provider now receives the complete returned byte range in a single call rather than separate pre-origin and exact-origin slices.
- Removed the artificial decode seam at the requested origin. An origin that lies inside a multi-byte instruction is no longer forced to begin a second instruction stream, so the instruction that started before the requested address can continue across it.
- Kept the requested address as presentation origin rather than silently changing it to the instruction start. `DisassemblyInstructionViewModel` already marks a row when the requested address falls anywhere inside that instruction's byte range, so the existing green origin marker now naturally resolves to the containing instruction when continuous decoding identifies one.
- Updated the Disassembler boundary notice to describe the actual rev5 behavior: the visible range is decoded as one continuous stream, the origin marks the decoded instruction containing the requested address, and only the beginning of an arbitrary variable-length context window remains best-effort because raw bytes alone cannot prove that the first byte is a canonical instruction boundary.
- Advanced centralized host metadata from `0.1.6.rev4` to `0.1.6.rev5` and changed the feature title to `Continuous Disassembly Stream and Origin Resolution`. The native Windows title and top application row continue to show only the application title; `0.1.6.rev5` remains visible only in the permanent bottom status bar.

### Added

- Added the automated **Core disassembly continuous origin resolution** regression check. It requests a Mock Target address that intentionally falls inside a two-byte instruction and verifies that Core returns the original instruction start/length/mnemonic, that the instruction contains the requested origin, and that no fabricated second instruction begins at the interior origin byte.
- Added `docs/testing/APP_0.1.6_REV5_VERIFICATION.md` with clean-build, 66-check automated, Mock runtime, live-PS5 boundary-resolution, context-menu regression, target/session safety, theme/layout, and preservation checks.

### Preserved

- Preserved the rev4 around-origin acquisition boundary: up to 512 bytes before and 512 bytes from the requested address, clamped independently to one readable non-guarded region, with a maximum combined Core window of 65,536 bytes. No additional target-memory read is introduced by the continuous decode change.
- Preserved the original exact-start `DisassemblyReader.ReadAsync(...)` semantics for callers that explicitly request decoding from one address forward.
- Preserved Plugin API `2.10.0`, Mock plugin `1.0.0.rev5`, and PS5 plugin `0.1.0.rev23`. No Plugin SDK contract, plugin capability, Iced dependency, ps5debug-NG command, PS5 transport, or platform-specific decoder code changes in this revision.
- Preserved Scan Results and Saved Addresses **Open in Memory Viewer** / **Open in Disassembler** entry points, including Saved Address Active Target safety.
- Preserved foreground target-I/O coordination, connection-generation/session validation, memory-region rules, Go To/Refresh behavior, origin/selection layout neutrality, and existing theme resources.
- Preserved all previously verified scanner, scan-result storage, Saved Addresses, universal export, Memory Viewer, PS5 native scanning/process-control, and application-state behavior.

### Verification and Documentation

- The automated verification registry now contains **66 checks**: the 65 rev4 checks remain present and the new continuous-origin-resolution regression is added. Windows build/runtime execution remains the user's authoritative verification step.
- Updated `README.md`, `docs/architecture/DISASSEMBLY_ARCHITECTURE.md`, `docs/ui/MAIN_WORKSPACE.md`, `docs/ui/THEMES.md`, and PS5 disassembly documentation so current behavior no longer claims that the requested origin starts a separate decode stream.
- The rev4 runtime observation that motivated this correction is explicitly represented in the rev5 verification plan: when an address falls inside a multi-byte x86-64 instruction, the row beginning before the address should remain intact and receive the origin marker instead of being truncated into an invalid pre-context record plus a false instruction stream at the requested byte.

## TeeKay87's Memory Engine 0.1.6.rev4 - Bidirectional Disassembly Context and Workspace Entry Points

### Added

- Added bounded **bidirectional Disassembler context** through a new Core `DisassemblyReader.ReadAroundAsync(...)` path. The default workspace request now collects up to **512 bytes before** the requested origin plus **512 bytes starting at** the origin, for up to 1,024 bytes of visible context when the containing readable region has enough data on both sides.
- Added independent region-boundary clamping for the pre-origin and origin/forward portions. The context reader never crosses the containing readable, non-guarded `MemoryRegion` merely to fill either side of the requested page, and partial memory-reader results are rejected if they no longer contain the requested origin.
- Added an exact-origin decode split for variable-length instruction sets. Pre-origin bytes are decoded as a separate best-effort provider stream, while bytes beginning at the user's requested address always start their own provider decode. This prevents uncertain pre-context alignment from consuming the requested origin or changing the already-verified forward decode that begins there. Core still does not attempt to infer x86-64 instruction semantics or claim that an arbitrary pre-context byte is a canonical program boundary.
- Added a Core regression check for bidirectional context. It verifies the default 512-before/512-from-origin window, exact requested-origin preservation, separation between pre-context and origin decode streams, and region-start clamping. The automated verification registry therefore increases from **64 to 65 checks**.
- Added **Open in Disassembler** to the Scan Results context menu. The command is capability-driven and opens the clicked scan-result address as the origin of the existing modeless Disassembler workspace for the current Active Target.
- Added **Open in Disassembler** to the Saved Addresses context menu. The command reuses the Saved Address's captured target identity and is enabled only when that identity still matches the current Active Target and the plugin can provide the complete neutral Disassembly/memory workflow. A retained address therefore cannot be silently disassembled against a different process.
- Added `docs/testing/APP_0.1.6_REV4_VERIFICATION.md` with clean-build, 65-check, bidirectional-context, context-menu, target-safety, theme, Mock, and live-PS5 verification procedures.

### Changed

- Advanced centralized host metadata from `0.1.6.rev3` to `0.1.6.rev4` with feature title `Bidirectional Disassembly Context and Workspace Entry Points`. `AppInfo` remains the single source for host title/version/revision metadata; the native Windows title and compact top application row remain title-only, while `AppInfo.DisplayVersion` remains in the permanent bottom status bar.
- Changed the Disassembler workspace from a forward-only 512-byte page to an around-origin view backed by the new Core context reader. **Go To** and **Refresh** now preserve the exact requested address as the origin while exposing preceding bytes in the same view whenever the containing region permits it.
- Changed the Disassembler boundary guidance to explain the two-stream rule explicitly: the exact requested origin is protected, while earlier raw-byte context on variable-length architectures remains best-effort because no generic byte reader can prove a canonical instruction boundary from arbitrary memory alone.
- Changed origin-row detection so the presentation can mark an instruction that contains the requested address, not only a row whose start address equals it. The current exact-origin stream normally produces a row starting at the origin, but the presentation rule remains correct for providers or future workflows that return a containing instruction.
- Renamed the user-facing Scan Results and Saved Addresses context-menu action **Browse Memory** to **Open in Memory Viewer**. The underlying Memory Viewer workflow, captured target identity, stale-session protection, and read/write behavior are unchanged.
- Extended `PluginViewModel` with an additive bidirectional Disassembly read bridge and a target-aware Disassembler availability check for Saved Addresses. Both continue to use the existing connection-generation validation, cached memory map, foreground target-I/O reservation, and neutral Core/provider services.
- Updated README plus Disassembly, Memory Viewer, Saved Addresses, Universal Export, main-workspace, theme, and PS5 documentation to describe the current rev4 behavior and distinguish implemented entry points from later navigation/history work.

### Preserved

- The already-verified exact-start `DisassemblyReader.ReadAsync(...)` contract remains available with its original forward-only semantics. Rev4 adds a separate around-origin path rather than changing existing callers or provider expectations underneath them.
- Plugin API remains `2.10.0`. No Plugin SDK contract or capability value changes are made in rev4.
- Mock plugin remains `1.0.0.rev5` and PS5 plugin remains `0.1.0.rev23`. The PS5 Iced `1.21.0` decoder integration, ps5debug-NG transport, plugin deployment/dependency layout, and provider implementation are unchanged.
- No PS5, `eboot.bin`, Iced, opcode-table, or x86-64 decode rule is introduced into Core or WPF. Core owns generic bounded context acquisition/orchestration; plugins continue to own architecture-specific byte-to-instruction decoding.
- Scan Results/Saved Addresses **Open in Memory Viewer** still opens the same existing Memory Viewer and does not change memory protection or issue target writes by itself.
- Previously verified scanner/storage, universal export, Saved Addresses refresh/edit/freeze/remove, Memory Viewer read/write/bookmark/history/region-navigation, Active Target state, foreground-I/O coordination, theme, Danger-button, settings, and status-bar behaviors are not redesigned by rev4.
- Disassembler Back/Forward history remains intentionally disabled, and Memory Viewer-to-Disassembler navigation, direct branch/call target following, disassembly copy/multi-selection/export, module-relative presentation, assembly/instruction editing, debugger/watchpoint/register/thread/stepping/call-stack features, Find What Writes/Accesses, decompilation, and symbol resolution remain later milestones.

### Verification and Documentation

- This revision starts from the exact user-supplied `0.1.6.rev3` package that passed the complete **64/64** Windows automated verification suite and was subsequently live-verified against a physical PS5 executable `Read, Execute` region. In that verification, Memory Viewer and Disassembler bytes matched and coherent x86-64 instructions were decoded from real executable memory.
- README, CHANGELOG, all Markdown documentation under `docs/`, and the complete source/project/test inventory were reviewed again before rev4 code changes. The implementation reuses the verified Core disassembly snapshot, target/session identity, foreground-I/O reservation, existing Memory Viewer opening path, and existing concrete DataGrid context-menu pattern rather than creating parallel infrastructure.
- The automated verification registry now contains **65 checks**. The new check is limited to the generic Core bidirectional-context behavior; the previously verified 64 checks remain registered unchanged and must continue to pass.
- `docs/testing/APP_0.1.6_REV4_VERIFICATION.md` defines the required Windows build/test acceptance plus Mock and live-PS5 checks for the enlarged visible range, exact-origin decode behavior, region clamping, context-menu labels/actions, Saved Address target identity, title/version presentation, and regression safety.
- No .NET build, automated-test pass, or runtime/PS5 verification is claimed from the packaging environment. Clean Windows compilation, **65/65** automated checks, and the documented runtime/UI checks remain the authoritative acceptance gates for rev4.

## TeeKay87's Memory Engine 0.1.6.rev3 - Disassembler Workspace

### Added

- Added the first user-facing, modeless **Disassembler** workspace on top of the architecture-neutral contracts/Core reader verified in `0.1.6.rev1` and the PS5 x86-64 provider verified in `0.1.6.rev2`. The new `DisassemblerWindow`, `DisassemblerViewModel`, and `DisassemblyInstructionViewModel` are host presentation components only; they contain no PS5 process rule, ps5debug command, x86 opcode table, Iced dependency, or `eboot.bin` assumption.
- Added a capability-driven **Disassembler...** action to the Target / Connection strip. It is visible only when the selected plugin advertises `TargetCapabilities.Disassembly` and becomes enabled only when a connected Active Target has a loaded memory map, normal memory-read availability, no conflicting foreground target operation, and at least one readable non-guarded region. The generic initial-address policy prefers readable executable regions, then named/module regions, then lower addresses without hardcoding a platform or module.
- Added hexadecimal Address navigation with **Go To** and **Refresh**. Successful requests use the existing Core `DisassemblyReader.DefaultWindowByteCount` of 512 bytes and remain clamped to one readable, non-guarded memory region. Invalid hexadecimal input, unreadable/unmapped addresses, stale sessions, and provider/read failures are surfaced as workspace errors without fabricating instruction data or discarding the last successful view.
- Added a virtualized read-only instruction table with **Address**, **Bytes**, and combined **Instruction** columns. The presentation model retains raw bytes plus separate mnemonic/operands/flow/branch-target/validity metadata even though rev3's default Instruction column renders mnemonic and operands together.
- Added Region / Module, Visible range, Protection, Architecture, target/process identity, operation status, and error presentation. Region/Module uses backend-reported module name first and region name second; anonymous mappings are not assigned invented names.
- Added a persistent theme-aware origin marker for the exact requested start-address row using the existing `SuccessMutedBrush`. Selected-origin presentation deliberately changes no layout-affecting border, padding, height, width, or column property, carrying forward the layout-neutral correction verified for Memory Viewer in `0.1.5.rev5`.
- Added an explicit instruction-boundary warning to the workspace. Decoding begins exactly at the requested address, but the UI does not claim that an arbitrary address is a canonical instruction boundary on variable-length architectures such as x86-64.
- Added visible **Back** and **Forward** controls as disabled rev3 placeholders so the navigation layout is established without falsely presenting history before that feature is implemented. Successful-address Back/Forward history is intentionally reserved for `0.1.6.rev4`.
- Added `docs/testing/APP_0.1.6_REV3_VERIFICATION.md` with clean-build, unchanged 64-check regression, capability/UI-state, deterministic Mock decode, origin/layout, bounded-region, stale-session, foreground-I/O, theme, and live-PS5 comparison procedures. The deterministic visual fixture begins at Mock address `0x10000400` so the first user-facing Disassembler can be checked without physical hardware before live PS5 verification.

### Changed

- Advanced centralized host metadata from `0.1.6.rev2` to `0.1.6.rev3` with feature title `Disassembler Workspace`. `AppInfo` remains the single source for host title/version/revision metadata. The native Windows title and compact top application row remain application-title-only; `AppInfo.DisplayVersion` remains the sole persistent version/revision presentation in the bottom status bar.
- Extended `PluginViewModel` with the generic host bridge used by Disassembler windows. Before every read it validates the captured process and connection generation, required capabilities/services, loaded memory map, and current memory-I/O state; it then reserves the existing foreground target path, snapshots the current memory regions, invokes neutral Core `DisassemblyReader`, and releases the reservation in `finally`.
- Extended foreground target-state notifications so **Disassembler...** enablement follows the same transient read/export/foreground-operation ownership rules as other explicit target actions instead of remaining clickable during a conflicting operation.
- Updated README plus Disassembly, Memory Viewer, Universal Export, theme, main-workspace, and PS5 documentation to describe the actual rev3 host workspace and distinguish implemented standalone navigation from the still-deferred cross-workspace/history/branch/export milestones.

### Preserved

- Plugin API remains `2.10.0`. No public Plugin SDK contract changes are made in rev3.
- Mock plugin remains `1.0.0.rev5` and PS5 plugin remains `0.1.0.rev23`; their already-verified decoders and service discovery are unchanged. Iced remains a PS5-plugin-private dependency and no new decoder dependency is added to Core/WPF.
- `DisassemblyReader`, `DisassemblySnapshot`, `DisassemblySessionIdentity`, `IDisassemblerProvider`, `DisassembledInstruction`, and `DisassemblyFlowControl` semantics remain unchanged from the verified rev1/rev2 foundation.
- Existing scanner/storage, universal export, Saved Addresses, Memory Viewer, process/Active Target, PS5 transport/TurboScan, theme, settings, Danger-button, and status-bar behaviors are not redesigned by rev3.
- The Disassembler still does not provide Scan Results/Saved Addresses/Memory Viewer **Disassemble Here**, Back/Forward history, direct branch/call target following, multi-selection/copy, universal export, module-relative offset presentation, assembly/instruction editing, breakpoints/watchpoints, registers/threads/stepping/call stack, Find What Writes/Accesses, decompilation, or symbol-server behavior. These remain later milestones rather than partial rev3 implementations.
- Existing files under `tools/preflight/` remain unchanged; further development of that auxiliary tooling remains paused unless explicitly revisited.

### Verification and Documentation

- This revision starts from the exact user-supplied `0.1.6.rev2` package that passed the complete **64/64** Windows automated verification suite.
- README, CHANGELOG, every Markdown document under `docs/`, and the complete source/project/test inventory were reviewed again before rev3 code changes so the workspace could reuse the existing target/session, memory-map, foreground-I/O, theming, input-filter, and modeless-window patterns instead of introducing parallel implementations.
- The automated verification registry deliberately remains at **64 checks** because rev3 does not change the Plugin SDK/Core/provider contract surface covered by those tests; its new acceptance surface is WPF compilation and runtime presentation/state behavior. All 64 previously verified checks must still pass unchanged.
- `docs/testing/APP_0.1.6_REV3_VERIFICATION.md` defines deterministic Mock visual output at `0x10000400`, live PS5 byte comparison against Memory Viewer, stale-target/reconnect rejection, bounded-region behavior, origin-layout verification, Light/Dimmed/Dark checks, and regression acceptance before rev3 is considered verified.
- No .NET build, automated-test pass, or runtime/PS5 verification is claimed from the packaging environment. Clean Windows compilation, **64/64** regression checks, Mock visual verification, and live PS5 verification remain the authoritative acceptance gates for this revision.

## TeeKay87's Memory Engine 0.1.6.rev2 - PS5 x86-64 Disassembly Provider

### Added

- Added the first real platform disassembly implementation through `Ps5X64DisassemblerProvider` in the PlayStation 5 plugin. The provider implements the Plugin API `2.10.0` `IDisassemblerProvider` contract and decodes caller-supplied PlayStation 5 x86-64 bytes without introducing architecture-specific decoding into Core or WPF.
- Added Iced `1.21.0` as a PS5-plugin-private x86/x64 decoder dependency. The provider uses Iced's 64-bit decoder and NASM formatter to return the neutral Address, Raw Bytes, Length, Mnemonic, Operands, FlowControl, optional direct BranchTarget, and validity fields introduced by rev1.
- Added neutral flow-control translation for direct calls, direct unconditional jumps, direct conditional jumps, indirect calls/jumps, returns, interrupts, sequential instructions, and other instruction-flow categories. Direct branch/call destinations are exposed only when Iced identifies a statically known near-branch operand; indirect control flow deliberately returns no invented target.
- Added bounded invalid/truncated-byte handling. Undecodable x86-64 input remains represented by invalid neutral instruction records that retain the consumed source bytes, while unsupported target architectures are rejected explicitly rather than decoded under guessed assumptions.
- Added PlayStation 5 session discovery for `IDisassemblerProvider` and advertised the neutral `TargetCapabilities.Disassembly` capability. The provider is stateless and does not create or own a ps5debug-NG transport.
- Added generic plugin deployment output collection through root `Directory.Build.targets`. Project references marked `DeployAsPlugin=true` now use one shared post-build path that asks each plugin for its assembly, generated `.deps.json`, and private copy-local dependencies and copies the deduplicated set into the consuming host's `Plugins` directory. Both the WPF application and verification executable use this path, so isolated `AssemblyDependencyResolver` discovery sees the same dependency-complete plugin layout. The PS5 project enables dynamic plugin dependency metadata so Iced resolves from that layout.
- Added two automated verification checks covering deterministic PS5 x86-64 decoding and provider safety. The deterministic fixture covers NOP, register MOV, ADD, SUB, CMP, direct CALL/JMP/conditional branch targets, RET, and RIP-relative addressing. Safety coverage checks architecture acceptance/rejection, bounded invalid input, indirect CALL/JMP target handling, cancellation, and no fabricated invalid branch destination. The verification registry increases from 62 to **64 checks**.
- Added `docs/plugins/PS5/PS5_DISASSEMBLY_IMPLEMENTATION.md` and `docs/testing/APP_0.1.6_REV2_VERIFICATION.md` covering the selected backend strategy, dependency deployment, flow-control mapping, instruction-boundary limits, architecture rules, package expectations, and Windows verification procedure.

### Changed

- Advanced centralized host metadata from `0.1.6.rev1` to `0.1.6.rev2` with feature title `PS5 x86-64 Disassembly Provider`. `AppInfo` remains the single source for host title/version/revision metadata.
- Advanced the PlayStation 5 plugin from `0.1.0.rev22` to `0.1.0.rev23` and its targeted Plugin API from compatible `2.9.0` to `2.10.0` because this plugin revision now implements the public disassembly contract added in host rev1. The public Plugin API itself remains `2.10.0`.
- Removed the version/revision badge from the compact top application/title row. The native Windows title remains `TeeKay87's Memory Engine` only, and `AppInfo.DisplayVersion` remains visible solely at the right edge of the permanent bottom status bar. This intentionally supersedes the `0.1.5.rev8` presentation that had retained a duplicate version badge in the compact application bar.
- Updated the README, Disassembly architecture documentation, PS5 plugin documentation, protocol mapping notes, and main-workspace documentation to describe the actual rev2 provider and the single status-bar version/revision presentation.
- Updated the source-preflight documentation to refer to the automated verification executable without the old dependency-free qualifier, because rev2 provider verification restores the PS5 plugin's Iced package dependency. The preflight checks themselves are unchanged.

### Backend Strategy

- Reviewed current ps5debug-NG disassembly support before selecting the rev2 implementation path. The upstream backend includes a Zydis-based `CMD_PROC_DISASM_REGION` operation (`0xBDAA0020`) that emits fixed-size analysis records with instruction address/length, flow classification, RIP-relative/memory metadata, and compact mnemonic metadata.
- The upstream server-side disassembly operation is not used as rev2's neutral instruction provider because its response shape does not provide the same complete raw-byte plus formatted mnemonic/operand representation required by the already-verified `IDisassemblerProvider` contract. Core also already performs bounded region-safe reads for the common workflow, so routing those bytes through a client-side decoder avoids a parallel memory/disassembly transport path.
- Iced is therefore intentionally confined to the PS5 plugin. Core and WPF contain no Iced reference, x86-64 opcode table, PS5 branch, ps5debug disassembly command, or `eboot.bin` assumption. The upstream server analysis operation remains documented as a possible future backend primitive for use cases such as bulk analysis/xref generation where its server-side metadata may be advantageous.

### Preserved

- The architecture-neutral Plugin API `2.10.0` contracts, `DisassembledInstruction` model, `DisassemblyFlowControl` model, `DisassemblyReader`, `DisassemblySnapshot`, and `DisassemblySessionIdentity` introduced and verified with 62/62 checks in `0.1.6.rev1` are unchanged.
- Mock plugin `1.0.0.rev5` remains unchanged on Plugin API `2.10.0`; its synthetic deterministic instruction set remains a hardware-independent Core/contract fixture and is not converted into or coupled to x86-64.
- Existing PS5 process enumeration, preferred-process behavior, memory-map/read/write paths, concurrent Frozen writes, process suspend/resume, native TurboScan negotiation/mapping/streaming/resident-results behavior, connection settings, and transport synchronization are not redesigned by the new provider.
- Rev2 sends no new ps5debug-NG command when decoding. Target bytes still reach Core through the existing `IMemoryReader`; `IDisassemblerProvider` only interprets the supplied byte window.
- All previously verified scanner/storage, universal export, Saved Addresses, Memory Viewer, Active Target gating, theme, and Danger-button behavior remain outside the functional scope of the PS5 provider work.
- Rev2 still does not add the Disassembler WPF workspace, `Disassemble Here` context actions, Go To/Back/Forward, branch-follow navigation, disassembly copy/export UI, assembler/instruction editing, debugger, breakpoints/watchpoints, register/thread/call-stack views, Find What Writes/Accesses, decompiler, or symbol-server behavior. Those remain later `0.1.6`/post-`0.1.6` milestones as documented.
- Existing files under `tools/preflight/` remain unchanged; further development of that auxiliary tooling remains paused unless explicitly revisited.

### Verification and Documentation

- This revision starts from the exact user-supplied `0.1.6.rev1` package that passed the complete **62/62** Windows automated verification suite.
- README, CHANGELOG, every Markdown file under `docs/`, and the complete source/project/test inventory were reviewed again before rev2 production changes so the provider could reuse the existing architecture, target I/O, capability, plugin-loading, and session-service paths.
- Static source review confirms Iced is referenced only by the PS5 plugin project/provider, the PS5 provider performs no target I/O, and no PS5/x86-64 decode logic has been introduced into Core or the WPF application.
- Static package-project review confirms the PS5 plugin now reports the dependency artifacts required by the existing `AssemblyDependencyResolver` loader, while both application and verification projects mark platform references for the same shared plugin-deployment target instead of relying on Iced being present in a consumer's normal dependency graph.
- `docs/testing/APP_0.1.6_REV2_VERIFICATION.md` defines the clean Windows build, expected **64/64** automated result, plugin dependency-output checks, PS5 provider checks, title/status presentation check, and existing-regression acceptance criteria.
- No .NET build, automated-test pass, or live PS5 runtime verification is claimed from the packaging environment. Those checks remain required on the user's Windows/.NET 9 environment before `0.1.6.rev2` is accepted and work proceeds to the Disassembler workspace milestone.

## TeeKay87's Memory Engine 0.1.6.rev1 - Disassembly Contracts and Core Foundation

### Added

- Added the first public architecture-neutral disassembly surface to Plugin API `2.10.0`: `IDisassemblerProvider` accepts a start address, caller-supplied target bytes, the existing neutral `TargetArchitecture`, and cancellation, then returns ordered neutral instruction records. The provider contract deliberately owns decoding only; target memory I/O remains outside the provider so platform transports do not leak into Core or WPF.
- Added `DisassembledInstruction` with Address, defensively copied Raw Bytes, derived Length, separate Mnemonic/Operands, neutral `DisassemblyFlowControl`, optional direct Branch Target, and explicit validity state. The model rejects empty raw-byte records, branch targets on non-branch flow-control categories, and branch targets on invalid instructions.
- Added the initial neutral flow-control catalog: None, Call, Jump, ConditionalJump, Return, Interrupt, and Other. Direct targets are permitted only for Call/Jump/ConditionalJump so future providers do not fabricate destinations for indirect flow.
- Added `Core/Disassembly/DisassemblyReader`. Core now validates that the requested address belongs to one readable non-guarded region, clamps the operation to that region, performs the read through the existing `IMemoryReader`, forwards only the bytes actually read to the provider, and rejects zero/invalid reader counts.
- Added provider-result validation before decoded data is accepted by Core. Returned instructions must remain inside the actual read range, be ordered and non-overlapping, and report Raw Bytes that exactly match the target bytes at each instruction address.
- Added `DisassemblySnapshot` as presentation-neutral Core result data retaining requested/start address, the copied byte window, containing `MemoryRegion`, existing `TargetArchitecture`, calculated end address, and the validated instruction collection.
- Added `DisassemblySessionIdentity`, capturing plugin id, process id/name, and host connection generation. This establishes the same stale-target safety basis used by Memory Viewer without introducing a second target/session model; WPF integration is deferred until the Disassembler workspace exists.
- Added a deterministic Mock disassembly provider and stable code fixture at `0x10000400`. The provider uses a deliberately synthetic Mock instruction set to cover ordinary instructions, direct call/conditional-jump targets, return, no-op, invalid/truncated records, and cancellation without pretending to be an x86-64 decoder.
- Added eight dependency-free verification checks for the instruction model, Mock capability/provider discovery, bounded Core reads, region-boundary clamping, unreadable/guarded rejection, invalid provider ordering, cancellation, and disassembly target/session identity. The automated registry increases from 54 to **62 checks**.
- Added `docs/architecture/DISASSEMBLY_ARCHITECTURE.md` and `docs/testing/APP_0.1.6_REV1_VERIFICATION.md` describing the new ownership boundary, contracts, safety rules, Mock fixture, deferred work, and Windows verification requirements.

### Changed

- Advanced centralized host metadata from `0.1.5.rev8` to `0.1.6.rev1` with feature title `Disassembly Contracts and Core Foundation`. `AppInfo` remains the single application title/version/revision source and the verified native-window/status presentation model is unchanged.
- Advanced the public Plugin API compatibility version from `2.9.0` to `2.10.0` because rev1 adds new public disassembly contracts/models. The existing same-major/older-minor compatibility rule is unchanged, so plugins that target earlier compatible 2.x minors remain loadable.
- Advanced the In-Memory Test Target from `1.0.0.rev4` to `1.0.0.rev5`, moved its targeted Plugin API to `2.10.0`, advertised the already-reserved neutral `Disassembly` capability, exposed `IDisassemblerProvider` from its session, and seeded the new deterministic code fixture. Its existing process, memory-region dimensions/protection, health/ammo/money addresses, scanner declarations, and memory read/write behavior remain unchanged.
- Updated README and architecture/plugin documentation to describe `0.1.6.rev1`, Plugin API `2.10.0`, Mock `1.0.0.rev5`, the completed/verified `0.1.5` Memory Viewer block, and the new disassembly foundation without presenting PS5 or WPF Disassembler functionality as implemented.
- Marked the architecture-neutral disassembly-contract roadmap step as initially implemented in `0.1.6.rev1`; PS5 disassembly remains the next separate milestone.

### Preserved

- PlayStation 5 plugin `0.1.0.rev22` is functionally unchanged and continues to target compatible Plugin API `2.9.0`. Rev1 adds no ps5debug-NG command, transport packet, x86-64 decoder, `eboot.bin` assumption, or PS5-specific branch in Core/WPF.
- The existing `TargetArchitecture` model remains the only CPU/address-width/pointer-width/endianness contract. No parallel architecture system was introduced for Disassembly.
- Existing Core/host target memory I/O contracts are reused. `IDisassemblerProvider` receives bytes and cannot establish a separate target command stream through the neutral contract.
- All `0.1.3` scanner/storage behavior, `0.1.4` universal export behavior, Saved Addresses refresh/edit/freeze coordination, and fully verified `0.1.5.rev8` Memory Viewer/UI-state/theme behavior remain outside the functional scope of this revision.
- Rev1 does not add a Disassembler WPF workspace, `Disassemble Here` context actions, Go To/Back/Forward, branch-follow navigation, selection/copy/export integration, assembler/instruction editing, debugger, breakpoints/watchpoints, register/thread/call-stack support, Find What Writes/Accesses, decompiler, or symbol-server behavior.
- The Mock provider is intentionally synthetic and is not an x86-64 implementation. Real PS5/x86-64 provider strategy and decoding are reserved for the next `0.1.6` milestone.
- Existing files under `tools/preflight/` remain unchanged; further development of that auxiliary tooling remains paused.

### Verification and Documentation

- Started from the exact user-supplied, fully verified `0.1.5.rev8` package. README, CHANGELOG, all Markdown documentation under `docs/`, the supplied `0.1.5` handover, and the complete source/project/test inventory were reviewed before production changes.
- Static architecture review confirms the new Core disassembly subsystem contains no PS5 id, ps5debug-NG command id, `eboot.bin` logic, x86/x64 opcode table, WPF dependency, or independent transport. The provider receives bytes through the public contract and Core performs target reads through `IMemoryReader`.
- Static review confirms the Mock plugin's new fixture is additive: existing deterministic health/ammo/money addresses, one-region memory-map shape, and read/write paths are retained.
- `docs/testing/APP_0.1.6_REV1_VERIFICATION.md` defines the clean Windows build, expected **62/62** automated result, new contract/Core/Mock checks, architecture review, and regression acceptance criteria.
- No .NET build or runtime test is claimed from the packaging environment. The real Windows/.NET 9 build and the complete 62-check verification executable remain required before `0.1.6.rev1` is accepted and before work advances to the PS5 x86-64 provider milestone.

## TeeKay87's Memory Engine 0.1.5.rev8 - UI State and Theme Consistency

### Fixed

- Fixed the Memory Viewer bookmark selector's closed/selected presentation. The expanded dropdown already showed each bookmark's `DisplayText`, but the shared ComboBox selection presenter could fall back to the backing `MemoryViewerBookmarkViewModel` object's CLR type name. `MemoryViewerBookmarkViewModel.ToString()` now returns the same `DisplayText`, so the selected bookmark consistently shows its hexadecimal address and optional backend-supplied Region / Module label without changing bookmark identity, history, or target I/O behavior.
- Fixed the Scan panel appearing partially interactive before an explicit Active Target existed. The complete interactive Scan control surface is now gated by a new host `HasActiveTarget` presentation state. Merely selecting/browsing a process no longer leaves Value operands, Scan Type, Value Type, plugin Scan Options, pause-scanning options, or scan action controls enabled; choosing **Set Active Target** enables the surface subject to each control's existing capability/session/busy-state rule.
- Fixed inconsistent semantic styling for application-owned destructive/dismissive buttons. Memory Viewer bookmark **Remove** and the **Cancel** buttons in Settings, Data Export, Memory Edit, and Confirmation dialogs now use the existing shared `DangerButtonStyle`, matching the already-correct Disconnect, Cancel Scan, operation-progress Cancel, Saved Address Remove, and Remove All actions. No hard-coded colors or new theme keys were introduced.
- Fixed main-window identity presentation so the native Windows title bar no longer appends the host version/revision. `AppInfo.WindowTitle` now resolves to the application title only, while the compact application bar keeps its existing version badge.
- Fixed the permanent status bar's rightmost identity field to show the centralized `AppInfo.DisplayVersion` value instead of the revision feature title. The status bar therefore shows values such as `0.1.5.rev8` and no longer exposes the internal feature/revision title as persistent UI chrome.

### Changed

- Advanced centralized host metadata from `0.1.5.rev7` to `0.1.5.rev8` with feature title `UI State and Theme Consistency`.
- Added `PluginViewModel.HasActiveTarget` as a presentation-facing derived state of the already-existing explicit `ActiveProcess` selection. The property is notified whenever Active Target changes; it adds no new target/session concept and does not alter command eligibility logic that already requires an Active Target for actual scans.
- Removed the now-unused `MainWindowViewModel.FeatureTitle` presentation property after the status bar moved to the existing centralized `DisplayVersion` value. `AppInfo.FeatureTitle` remains the authoritative release metadata used for revision documentation/package identity.
- Clarified the application-wide semantic button rule: Remove/Delete/Cancel/Exit/Abort/Disconnect-style `Button` controls use the active theme's Danger palette. Neutral supporting actions remain Secondary and affirmative workflow actions remain Primary.
- Updated README and UI/Memory Viewer architecture documentation to describe Active Target Scan gating, native-title/status-bar version presentation, bookmark selected-item rendering, and the expanded Danger-button consistency rule. Refreshed the current-version references in the scanner and universal-export architecture documents to `0.1.5.rev8` without changing those verified subsystems.
- Recorded the rev7 runtime result: all 54 automated checks passed and the new bookmarks, region navigation/history integration, and Saved Addresses tooltip correction behaved correctly in use. Rev8 addresses the UI/state inconsistencies discovered during that validation rather than redesigning those verified workflows.

### Preserved

- Memory Viewer bookmark storage, exact-address identity, duplicate prevention, Go/Remove behavior, window-local lifetime, Back/Forward history integration, region navigation, green origin marking, copy behavior, and safe read/write services are unchanged.
- Rev6 safe-editing guarantees remain unchanged: writable-region gating, no page-protection override, stale-source pre-read rejection, neutral `IMemoryWriter`, immediate read-back verification, and automatic refresh after stale/issued writes.
- Saved Addresses empty row-tooltip suppression from rev7 remains unchanged; explicit Frozen and Remove child-control tooltips are preserved.
- The Scan panel's existing capability/session/busy-state logic is preserved underneath the new Active Target parent gate. First Scan/Next Scan/New Scan/Cancel Scan command semantics, Core Scan Types, plugin Value Types/options, pause-scanning behavior, resident PS5 scanning, and disk-backed storage are unchanged.
- No Core, Plugin SDK, PS5 plugin, Mock plugin, scanner protocol, export format, or scan-result storage contract is changed by rev8.
- Plugin API remains `2.9.0`, PS5 plugin remains `0.1.0.rev22`, and Mock plugin remains `1.0.0.rev4`.
- Existing files under `tools/preflight/` remain unchanged; further development of that helper remains paused.
- The automated verification registry remains at **54 checks** because rev8 is a host-WPF presentation/state consistency revision and does not add a new Core/plugin contract suitable for the existing non-WPF test executable.

### Verification and Documentation

- Started from the exact user-supplied/runtime-tested `0.1.5.rev7` package. README, CHANGELOG, all Markdown documentation under `docs/`, and the complete source/XAML/project/test inventory were reviewed before production changes.
- Static review must confirm every application-owned literal Remove/Cancel/Disconnect-style `Button` uses `DangerButtonStyle`, all `IsCancel=True` application buttons use the same Danger style, and no local destructive color was introduced.
- Static review must confirm the Scan control column has one parent Active Target gate and that `PluginViewModel` notifies `HasActiveTarget` whenever `ActiveProcess` changes, while all existing individual `IsEnabled`/Command conditions remain intact beneath it.
- Static review must confirm the main native title resolves from centralized `AppInfo.WindowTitle`, the status bar resolves from centralized `DisplayVersion`, and the top application-bar version badge is retained.
- Static review must confirm the bookmark selected-item fix is confined to presentation (`ToString() => DisplayText`) and does not alter bookmark address/region data or navigation logic.
- `docs/testing/APP_0.1.5_REV8_VERIFICATION.md` covers clean Windows build, the unchanged 54-check automated suite, Active Target Scan-panel gating, destructive-button theme behavior across Light/Dimmed/Dark, native title/status-bar identity, bookmark selected-item display, and rev7 Memory Viewer regression behavior.
- Native Windows/.NET/WPF compilation and runtime verification remain required before rev8 is accepted.

## TeeKay87's Memory Engine 0.1.5.rev7 - Memory Viewer Bookmarks and Region Navigation

### Added

- Added viewer-local **Memory Viewer bookmarks**. The current navigation/origin address can be added to a bookmark list, selected later, revisited through **Go**, and removed without changing the saved-address table or target memory. Bookmarks preserve the exact origin address rather than rounding to the 16-byte display-row base.
- Added platform-neutral **Memory Viewer region navigation** through a new Core `MemoryViewerRegionNavigator`. The viewer can move to the previous readable non-guarded region, current region start, current region end, or next readable non-guarded region without embedding platform-specific memory-map assumptions in WPF.
- Added a dedicated region-navigation toolbar with **Previous Region**, **Region Start**, **Region End**, and **Next Region** actions. Previous/Next skip write-only, unreadable, and guarded mappings and navigate to the selected region's base address; Region End navigates to the final byte so the existing bounded reader naturally clamps the page to the region boundary.
- Added a bookmark toolbar with **Add Current**, a window-local bookmark selector, **Go**, and **Remove**. Duplicate bookmarks for the same exact origin address are prevented within one viewer window. Bookmark navigation is a normal successful navigation and therefore participates in the existing Back/Forward history.
- Added automated Core coverage **Memory Viewer readable region navigation**, verifying that previous/next region lookup works with an unsorted memory map, skips guarded and unreadable regions, and correctly reports the beginning/end of the readable-region sequence. The automated registry therefore increases from 53 to **54 checks**.
- Added `docs/testing/APP_0.1.5_REV7_VERIFICATION.md` covering clean Windows build, all 54 automated checks, bookmark behavior, region navigation, history integration, live PS5 memory-map navigation, the Saved Addresses tooltip correction, and regression coverage for the fully verified rev6 write workflow.

### Fixed

- Fixed empty tooltip popups over blank Saved Addresses cell space. The row-level `StatusText` tooltip is now suppressed when `StatusText` is empty instead of allowing WPF to materialize an empty tooltip popup.
- The tooltip correction specifically removes the empty hint surface observed when hovering the unused cell area around **Frozen**, **Protection**, and the **Remove** column while preserving the meaningful tooltips on the Frozen checkbox and Remove button themselves. Non-empty row status/error tooltips remain available when a Saved Address actually has status text to show.

### Changed

- Advanced centralized host metadata from `0.1.5.rev6` to `0.1.5.rev7` with feature title `Memory Viewer Bookmarks and Region Navigation`.
- Reused the existing Core readable-region rule for both bounded Memory Viewer reads and region navigation so readable/non-guarded eligibility is defined once rather than duplicated between reader and navigator code.
- Expanded the Memory Viewer navigation card into separate address/history, region-navigation, bookmark, and region-context rows while continuing to use existing shared WPF control styles and theme resources.
- Successful navigation through bookmarks or region actions records the exact destination in the same in-window history used by Go To. Refresh still creates no history entry, ordinary row selection still does not navigate, and a new successful navigation after Back still discards the obsolete Forward branch.
- Recorded `0.1.5.rev6` as fully verified. The Windows suite passed 53/53 and runtime verification on Mock Target and real PS5 hardware confirmed verified writes/read-back, larger 4-byte edits, Saved Addresses observing written values, read-only Edit gating, stale-data rejection with no write attempted, automatic refresh, and Back/Forward navigation.
- Updated README, Memory Viewer architecture, Saved Addresses/UI/theme guidance, the early-development guide, and current-version references to describe bookmarks, region navigation, and the tooltip behavior as current functionality.

### Preserved

- Memory Viewer bookmarks are intentionally scoped to one open Memory Viewer window. Rev7 does not persist them to application settings, projects, Saved Addresses, or disk; persistent bookmark/project ownership remains a later design decision rather than being hidden inside general settings.
- Region navigation uses only the current neutral `MemoryRegion` list and existing `IMemoryReader` path. It adds no Plugin SDK contract, no PS5 protocol command, and no platform-name check.
- Existing target/process/connection-generation validation and foreground target-I/O coordination apply unchanged to bookmark and region destinations because every navigation ultimately uses the same verified `ReadMemoryViewerWindowAsync(...)` path.
- Rev6 safe editing/write semantics remain unchanged: no page-protection override, exact displayed-row edit size, stale-source pre-read, neutral `IMemoryWriter`, immediate read-back verification, and refresh after stale/issued writes.
- Rev4-rev5 selection/copy/history/origin behavior remains unchanged. The green origin marker is still independent from ordinary selection and remains layout-neutral.
- Scan Results/Saved Addresses scanning, export, value editing, Freeze, Protection, target handling, resident PS5 scans, and plugin settings remain unchanged except for the Saved Addresses empty-tooltip presentation fix.
- Plugin API remains `2.9.0`, PS5 plugin remains `0.1.0.rev22`, and Mock plugin remains `1.0.0.rev4`.
- Existing files under `tools/preflight/` remain unchanged; further development of that helper remains paused.

### Verification and Documentation

- Started from the exact user-supplied and fully runtime-verified `0.1.5.rev6` package. README, CHANGELOG, all Markdown documentation under `docs/`, and the complete source/XAML/project/test inventory were reviewed before the production changes.
- `MemoryViewerRegionNavigator` lives in Core and depends only on neutral `MemoryRegion` data. The WPF ViewModel consumes the current Active Target memory-map snapshot exposed through the existing plugin workspace and sends all actual reads through the already coordinated viewer read path.
- Bookmark state is host presentation/session state only and causes no target I/O until the user explicitly chooses **Go**. Adding/removing a bookmark does not affect navigation history; navigating to a bookmark does.
- Static review must confirm the new WPF command bindings resolve to existing ViewModel properties, the bookmark/region controls introduce no new code-behind event wiring, Saved Addresses keeps child-control tooltips while empty row tooltips are suppressed, the automated registry contains exactly 54 checks, Plugin SDK/plugin versions remain unchanged, `tools/preflight/` is unchanged, local Markdown links resolve, and no build artifacts are packaged.
- Native Windows/.NET/WPF compilation and runtime verification remain required. Live PS5 region-navigation testing should use ordinary readable mappings and must not imply that Previous/Next Region changes memory or page protection.

## TeeKay87's Memory Engine 0.1.5.rev6 - Memory Viewer Safe Editing and Write Support

### Added

- Added the first production Memory Viewer write path through a new platform-neutral Core `MemoryViewerWriter`. The service accepts the existing neutral `IMemoryReader`, `IMemoryWriter`, `TargetProcess`, and `MemoryRegion` contracts and therefore requires no platform-name checks, PS5 protocol leakage, or new Plugin SDK contract.
- Added safe optimistic write validation for Memory Viewer edits. Before any requested bytes are written, Core rereads the exact displayed range and requires it to still match the bytes that were shown when the edit dialog opened. If the target changed, the operation returns `SourceChanged` and no write is issued.
- Added immediate read-back verification after each Memory Viewer write. Core reports `Verified` only when the complete written range can be reread and matches the requested bytes; a differing read-back is reported as `VerificationFailed`, while a post-write read error explicitly states that the write was sent but verification could not be completed.
- Added `MemoryViewWriteOutcome` and `MemoryViewWriteResult` so the host can distinguish unchanged edits, stale-source rejection, verified writes, and read-back mismatch without inferring write state from UI text.
- Added a themed **Edit Hex Bytes** dialog for a single Memory Viewer row. The dialog shows the row address, current bytes, an editable replacement byte sequence, the exact required byte count, and the pre-read/read-back safety rule. The affirmative action is explicit **Write & Verify** and is deliberately not the default Enter-key action.
- Added **Edit...** to the Memory Viewer toolbar and **Edit Hex Bytes...** to the row context menu. The action is available only when the displayed region is readable, writable, non-guarded, and the active plugin advertises both memory-read and memory-write support.
- Added two automated Core verification checks: **Memory Viewer safe write and stale-data protection** and **Memory Viewer write protection rejection**. The safe-write check covers stale displayed bytes, a deliberately non-writing writer that forces read-back mismatch, a successful verified write, and unchanged-edit no-op semantics. The protection check confirms a read-only region is rejected without changing target memory. The automated registry therefore increases from 51 to **53 checks**.
- Added `docs/testing/APP_0.1.5_REV6_VERIFICATION.md` covering clean Windows build, all 53 automated checks, Mock-target editing, stale-data rejection, read-only protection behavior, write/read-back presentation, existing selection/copy/history/origin regressions, theme coverage, and controlled live-PS5 verification.

### Changed

- Advanced centralized host metadata from `0.1.5.rev5` to `0.1.5.rev6` with feature title `Memory Viewer Safe Editing and Write Support`.
- Changed the Memory Viewer header from a fixed **Read-only** label to a snapshot-derived **Read/write** or **Read-only** access label. The DataGrid itself remains presentation-only; raw edits are committed only through the explicit modal edit workflow.
- Extended `MemoryViewerRowViewModel` with an immutable copy of the displayed row bytes and row-level edit eligibility. Copy/selection/origin behavior continues to use the same row object and formatting as rev5.
- Added a host `WriteMemoryViewerBytesAsync(...)` operation beside the existing viewer read operation. It preserves the captured process identity and connection-generation checks, requires the same Active Target, takes a current memory-map snapshot, reserves the existing foreground target-operation path, waits for Saved Address I/O to become idle through the already verified coordinator, and executes the Core write/verify service on the primary session reader/writer.
- Successful verified writes refresh the current 512-byte Memory Viewer snapshot immediately. Stale-source rejection also refreshes the viewer so the user sees the target's newer bytes; a read-back mismatch refreshes the current view while retaining an explicit verification error. Navigation history and the green origin address do not change because of a write/refresh.
- Updated README, Memory Viewer architecture, current workspace/theme/early-architecture documentation, and current-version references to describe the new editable boundary and the remaining `0.1.5` work.
- Recorded `0.1.5.rev5` as fully verified: the Windows automated suite passed 51/51 and runtime testing confirmed the selected green origin row keeps identical geometry without introducing a horizontal scrollbar.

### Preserved

- Memory Viewer writes never change target page protections. A range that is not already readable and writable, or carries `Guard`, remains non-editable.
- Rev6 writes exactly one displayed row at a time and requires the replacement byte count to match that row's displayed byte count. Byte-level cursor editing, arbitrary-length paste across rows, code patch assembly, and protection override are deliberately outside this revision.
- The existing bounded 512-byte reader, one-region clamping, Go To, Refresh, Back/Forward history, persistent green origin marker, layout-neutral selected-origin rendering, extended selection, copy actions, and target/reconnect safety remain unchanged.
- Saved Address value editing/Freeze coordination, scan/result storage, universal export, resident PS5 scanning, raw diagnostic write behavior, and plugin settings are unchanged.
- No Plugin SDK or platform-plugin code changes are required. Plugin API remains `2.9.0`, PS5 plugin remains `0.1.0.rev22`, and Mock plugin remains `1.0.0.rev4`.
- Existing files under `tools/preflight/` are left unchanged; further development of that helper remains paused.

### Verification and Documentation

- Started from the exact user-supplied, fully verified `0.1.5.rev5` package and reread README, CHANGELOG, all Markdown documentation under `docs/`, and reviewed the complete source/project/resource/test inventory before changing code.
- Core safe-write validation is intentionally separate from WPF. The writer requires one complete readable+writable+non-guarded region, performs an exact pre-read, refuses stale displayed bytes before `IMemoryWriter.WriteAsync(...)`, and performs an exact read-back after the write.
- Host coordination uses the existing foreground-target reservation rather than creating another write queue or using the PS5-specific concurrent Frozen-write channel. This preserves the already verified rule that explicit target actions do not overlap the primary target command stream.
- Static preparation must confirm all new XAML event handlers exist on concrete window/dialog elements, the dialog and Memory Viewer XAML parse, all new C# files contain required `using` directives, the two new Core tests are registered exactly once, the automated count is 53, Plugin SDK/plugin versions are unchanged, local Markdown links resolve, and no build artifacts are packaged.
- Native Windows/.NET/WPF compilation and runtime verification remain required. Live PS5 editing should begin with a deliberately chosen, already writable test range or a known disposable game value and must confirm the normal command stream remains usable afterward.

## TeeKay87's Memory Engine 0.1.5.rev5 - Memory Viewer Origin Selection Layout Fix

### Fixed

- Fixed the Memory Viewer origin row changing physical size when the persistent green origin row was also selected. Rev4 added `BorderThickness="1"` only for the combined `IsOriginRow + IsSelected` state; WPF included that border in the row's desired size, which increased the selected origin row by a few pixels, shifted every row below it downward, widened the measured row content, and could force a horizontal scrollbar to appear.
- Removed the selection-only row border entirely. The origin marker is now presentation-only through the existing theme-aware `SuccessMutedBrush` background and does not modify `BorderThickness`, padding, margin, row height, column width, or any other layout-affecting property when selection changes.
- Preserved the independent state model introduced in rev4: the current navigation/origin row remains green while another row or a multi-selection uses the normal DataGrid selection surface, and selecting the green origin row no longer changes its geometry.

### Changed

- Advanced centralized host metadata from `0.1.5.rev4` to `0.1.5.rev5` with feature title `Memory Viewer Origin Selection Layout Fix`.
- Updated README and Memory Viewer/UI/theme/current-version documentation to record the rev4 runtime presentation defect and the rev5 layout-neutral correction.
- Shifted the next functional Memory Viewer stage, **safe editing/write support**, to `0.1.5.rev6` because rev5 is a corrective revision rather than the next planned feature stage. Bookmarks, richer region handling, and runtime-driven additions remain later `0.1.5` work.

### Preserved

- Memory Viewer selection, multi-selection, clipboard actions, Back/Forward history, Go To, Refresh, origin-address semantics, target/process/connection-generation safety, and bounded read-only memory access are unchanged from rev4.
- No Core, Plugin SDK, PS5 plugin, Mock plugin, scanner/storage/export, Saved Address, or target-protocol behavior is changed by this host-WPF presentation correction.
- Plugin API remains `2.9.0`, PS5 plugin remains `0.1.0.rev22`, Mock plugin remains `1.0.0.rev4`, and the automated verification registry remains at **51 checks**.
- Existing files under `tools/preflight/` remain unchanged; further development of that helper remains paused.

### Verification and Documentation

- Before changing rev4, reread README, CHANGELOG, project Markdown documentation, and reviewed the complete current source/project/resource inventory from the packaged rev4 baseline.
- Source review identifies the rev4 `DataGridRow.BorderThickness=1` setter in the selected-origin `MultiDataTrigger` as the sole layout-affecting state responsible for the observed geometry change. Rev5 removes the layout-affecting border setters from that combined selected-origin trigger and retains only background setters, so both the ordinary origin state and selected-origin state stay green without changing geometry.
- Static preparation must confirm the Memory Viewer origin row style contains no selection-dependent border thickness, padding, margin, height, or width changes; XAML/project XML and bundled JSON parse; relative Markdown links resolve; the test registry remains at 51 checks; plugin/API versions remain unchanged; and no build artifacts are packaged.
- Native Windows/.NET/WPF compilation and manual runtime verification remain required. The key runtime acceptance is that selecting and deselecting the green origin row does not move any row, change the DataGrid's measured width, or create/remove a horizontal scrollbar.

## TeeKay87's Memory Engine 0.1.5.rev4 - Memory Viewer Selection Copy and Navigation

### Added

- Added independent extended row selection to the read-only Memory Viewer. Selecting one or several rows is now presentation state only and does not change the address the viewer was opened/navigated to or trigger additional target I/O.
- Added a persistent green origin-row marker for the row containing the current Memory Viewer navigation address. The origin state is stored separately from DataGrid selection, so the row remains green after the user clicks or multi-selects other rows. A new successful Go To, Back, or Forward navigation moves the origin marker; Refresh keeps it at the current address.
- Added Memory Viewer clipboard actions: **Copy Address**, **Copy Hex Bytes**, **Copy ASCII**, **Copy Row**, and **Copy Selected**. Row/selection copies use tab-separated Address, Hex Bytes, and ASCII fields with one selected row per line. `Ctrl+C` invokes the same Copy Selected path.
- Added Memory Viewer Back/Forward history for successful navigation addresses. The initial Browse Memory address becomes the first history entry, successful Go To operations add entries, Back/Forward restore previous/next addresses, `Alt+Left`/`Alt+Right` invoke the same navigation, Refresh does not add history, and ordinary row selection does not affect history.
- Added forward-branch replacement: after navigating Back, a successful Go To removes the obsolete forward branch before recording the new address. Failed reads leave the current history index and previous visible snapshot unchanged.
- Added a theme-derived `SuccessMutedBrush` generated from the active theme's existing `SuccessText` color. The translucent brush provides the Memory Viewer origin-row surface without adding a new required theme JSON key or invalidating existing custom theme files.
- Added `docs/testing/APP_0.1.5_REV4_VERIFICATION.md` covering clean Windows build, the unchanged 51-check automated suite, persistent origin highlighting, multi-selection/copy behavior, history semantics, theme switching, and existing Memory Viewer/Scan Results/Saved Addresses regressions.

### Changed

- Advanced centralized host metadata from `0.1.5.rev3` to `0.1.5.rev4` with feature title `Memory Viewer Selection Copy and Navigation`.
- Changed the Memory Viewer DataGrid from single-row to extended full-row selection while preserving the existing bounded 16-byte-row presentation and read-only behavior.
- Changed the Memory Viewer toolbar to include Back and Forward controls ahead of the Address/Go To/Refresh workflow.
- Changed the row model's previous `ContainsRequestedAddress` presentation flag to the clearer `IsOriginRow` semantic and added a tab-separated row representation used by the host clipboard actions.
- Updated README, Memory Viewer architecture, main-workspace, theme, early-architecture, scanner/export current-version references, and verification documentation for the rev4 behavior and the next planned Memory Viewer stage.
- Recorded the project decision to pause further development of `tools/preflight/`. The existing preflight files remain in the repository unchanged; this revision adds no rules or behavior to that subsystem, and the real Windows/Roslyn/WPF build remains the authoritative compile gate.

### Preserved

- The Memory Viewer remains read-only. No memory-write/editing path, page-protection override, disassembly, pointer interpretation, bookmark persistence, or automatic live refresh is introduced in rev4.
- Core `MemoryViewerReader`, `MemoryViewSnapshot`, target/process/connection-generation safety, region clamping, Guard rejection, foreground target-I/O coordination, and Region / Module + Protection presentation remain unchanged from the rev1 foundation.
- Scan Results multi-selection Save Address behavior and the rev2/rev3 concrete DataGrid context-menu ownership remain unchanged.
- Universal export, scanner/storage, Saved Address refresh/write/Freeze/removal, and PS5 native/resident scan behavior are not modified.
- Plugin API remains `2.9.0`, PS5 plugin remains `0.1.0.rev22`, Mock plugin remains `1.0.0.rev4`, and the dependency-free automated verification registry remains at **51 checks**.
- `tools/preflight/Invoke-SourcePreflight.ps1` and `tools/preflight/Run-SourcePreflight.cmd` are byte-for-byte unchanged from rev3.

### Verification and Documentation

- Before changing rev3, reread README, CHANGELOG, the project Markdown documentation, and reviewed the complete C#/XAML/project/test/source inventory from the exact supplied `0.1.5.rev3` package.
- Source review confirms the rev4 feature remains host/WPF-only except for the derived theme brush resource. No Plugin SDK or platform-plugin contract is required for selection, clipboard, navigation history, or origin-row presentation.
- Source-level checks must verify XAML/XML/JSON structure, all new Memory Viewer event-handler names/signatures, navigation command wiring, version/revision references, Markdown links, unchanged plugin/API versions, unchanged 51-test registry, release-tree cleanliness, and byte preservation of Core/Plugin SDK/plugins/tests/preflight scripts where claimed.
- Native Windows/.NET/WPF compilation and manual runtime verification remain required; no assistant-side build result is claimed.

## TeeKay87's Memory Engine 0.1.5.rev3 - Memory Viewer C# Scope Compile Fix and Preflight Hardening

### Fixed

- Fixed the Windows/Roslyn `CS0136` build failure exposed by the first `0.1.5.rev2` build. The shared `TryPrepareRowContextMenu<TItem>()` helper declared the pattern variable name `contextMenu` once inside the failed-row branch and again later in the enclosing method declaration space; C# rejects that shadowing even though the two checks are reached on different control-flow paths.
- Reworked the helper to read `dataGrid.ContextMenu` once into a nullable `ContextMenu? contextMenu` local and reuse that single local for clearing stale menu DataContext, validating menu availability, and assigning the current row DataContext. This removes the declaration-space conflict rather than merely renaming one of two competing pattern variables.
- Preserved the rev2 context-menu ownership and selection semantics. Scan Results/Saved Addresses context menus remain concrete `DataGrid.ContextMenu` instances, right-clicking an already selected Scan Result retains the multi-selection, right-clicking outside the selection targets the clicked row, and existing Save Address/Browse Memory/copy/freeze/remove actions continue through the same handlers/commands.
- Recorded the actual rev2 Windows result as build failure rather than acceptance. The `MC6007` failure from rev1 was gone, but rev2 could not reach the 51-check/runtime acceptance phase because `CS0136` prevented the App project from compiling.

### Added

- Hardened `tools/preflight/Invoke-SourcePreflight.ps1` with a conservative C# pattern-variable declaration-space rule. For project/WPF-style PascalCase type patterns inside one method, reusing the same pattern-variable identifier is now rejected before the authoritative Windows build. This directly detects the rev2 `contextMenu` failure class while remaining intentionally narrower than a general C# compiler.
- Added a dedicated PASS line for the new C# pattern-variable declaration-space check so the source-preflight output makes the new gate visible.
- Added `docs/testing/APP_0.1.5_REV3_VERIFICATION.md` covering the hardened source preflight, clean Windows build, unchanged 51-check automated suite, context-menu/multi-select regressions, and Memory Viewer smoke verification.

### Changed

- Advanced centralized host metadata from `0.1.5.rev2` to `0.1.5.rev3` with feature title `Memory Viewer C# Scope Compile Fix and Preflight Hardening`.
- Updated README, Memory Viewer/Main Workspace/Saved Addresses architecture notes, Universal Export/Scanner current-version references, the early architecture guideline's preflight note, and source-preflight documentation to reflect the rev2 build result and rev3 correction.
- The planned Memory Viewer feature order is unchanged, but two corrective revisions now sit between the rev1 foundation and the next functional stage. Selection/copy/navigation history therefore moves to `0.1.5.rev4`; safe editing/write support follows after that, then bookmarks/richer region behavior and other runtime-driven additions.

### Preserved

- Core Memory Viewer behavior is unchanged: bounded read-only region-safe reads, Address/Hex/ASCII rows, Go To, Refresh, target/process/connection-generation safety, Protection and region presentation all retain rev1 semantics.
- Scan Results multi-selection Save Address behavior is unchanged from rev1/rev2.
- Core, Plugin SDK, PS5 plugin, Mock plugin, scanner/storage/export implementation, Saved Address I/O/freeze behavior, and PS5 protocol behavior are not functionally modified by this compile correction.
- Plugin API remains `2.9.0`, PS5 plugin remains `0.1.0.rev22`, Mock plugin remains `1.0.0.rev4`, and the automated verification registry remains at **51 checks**.

### Verification and Documentation

- Before changing rev2, reread README, CHANGELOG, all Markdown files under `docs/`, and reviewed the complete source/XAML/project/test inventory from the exact packaged rev2 baseline.
- Static regression analysis against the exact packaged rev2 source identifies one repeated typed pattern-variable conflict: `contextMenu` in `TryPrepareRowContextMenu<TItem>()` at rev2 lines 151 and 165. The corrected rev3 source contains no conflict under the same rule.
- The source preflight remains an auxiliary gate only. No assistant-side Windows/.NET/WPF build result is claimed; clean Windows build, the 51-check verification runner, and manual Memory Viewer/context-menu acceptance remain required.

## TeeKay87's Memory Engine 0.1.5.rev2 - Memory Viewer XAML Compile Fix and Source Preflight

### Fixed

- Fixed the first Windows/WPF build failure in `0.1.5.rev1`. Rev1 added code-behind `MenuItem.Click` handlers inside `ContextMenu` instances created through `DataGridRow` `Setter.Value`; the XAML remained valid XML but WPF markup compilation failed with `MC6007` and then emitted cascading designer/type errors such as missing `System.Object`, `ProportionalGridSplitter`, `TextBoxInputFilter`, and `UiMetrics` references.
- Removed all three rev1 code-behind `Click` hookups from `Setter.Value` object graphs. The Scan Results and Saved Addresses row menus now live as concrete `DataGrid.ContextMenu` instances, where ordinary code-behind event wiring is compiled in the normal control object graph.
- Added `ContextMenuOpening` preparation for both DataGrids. The clicked row becomes the menu DataContext before commands/events execute. If the row is outside the current selection, the previous selection is cleared and that row becomes the selection; if the context-clicked Scan Result is already part of a multi-selection, the existing selected set is retained so **Save Address** can still process all selected rows as intended.
- Preserved existing row commands after the context-menu ownership change. Scan Results **Copy address**/**Copy value** and Saved Addresses Freeze/Unfreeze, **Copy address**, **Copy value**, and **Remove address** continue to bind to the context row rather than to the main window/plugin DataContext.

### Added

- Added repository source-preflight tooling under `tools/preflight/`: `Run-SourcePreflight.cmd` provides the Windows entry point and `Invoke-SourcePreflight.ps1` performs source-level checks before a real application build.
- Added XAML event-wiring validation that checks known event-handler names, verifies matching code-behind methods, rejects direct event hookups inside shared `Setter.Value` object graphs, and, when Windows `PresentationFramework` is available, reflects built-in WPF types so an event name must exist on the element type that declares it.
- Added source-level `StaticResource` validation for simple named resources and `clr-namespace` validation that checks project-owned XAML custom-control/attached-property owner types against the C# source inventory. These checks are intended to catch common custom-type/resource failures before they cascade through the WPF designer.
- Added JSON/project validation, explicit `ProjectReference` path checks, centralized `AppInfo`/README/CHANGELOG/current-verification consistency checks, current verification-registry count validation, and release-tree rejection of `bin`, `obj`, and `.vs` directories.
- Added `docs/development/SOURCE_PREFLIGHT.md` documenting the preflight scope, limitations, execution order, and maintenance rule. The preflight is explicitly an additional source gate and does not claim to replace Roslyn, WPF BAML generation, Windows build verification, automated regression execution, or live-target testing.
- Added `docs/testing/APP_0.1.5_REV2_VERIFICATION.md` covering source preflight, clean Windows build, the existing 51 automated checks, corrected Scan Results/Saved Addresses context menus, multi-selection Save Address, Browse Memory, Memory Viewer regression, and existing scanner/export/Saved Address/PS5 boundaries.

### Changed

- Advanced the host from `0.1.5.rev1` to `0.1.5.rev2` with feature title `Memory Viewer XAML Compile Fix and Source Preflight` through centralized `AppInfo`.
- Recorded the actual rev1 verification outcome in its verification document: rev1 failed at the clean Windows/WPF build before its 51-check/runtime acceptance could begin. The compile failure is therefore not recorded as a functional Memory Viewer regression or as a passed revision.
- Updated README and Memory Viewer/UI/architecture documentation to describe the corrected context-menu ownership and the new preflight-before-build release workflow.
- Adjusted the working `0.1.5` feature sequence without changing its functional order. Because rev2 is a corrective revision, selection/copy/navigation history moves to the next functional revision, safe Memory Viewer editing follows after that, and bookmarks/richer region handling remain later `0.1.5` work. Architecture-neutral disassembly remains the expected next major feature block only after Memory Viewer is complete and verified.

### Preserved

- The rev1 Core Memory Viewer design is unchanged: neutral `MemoryViewerReader`, 512-byte bounded reads, readable/non-Guarded single-region clamping, target/process + connection-generation safety, foreground target I/O coordination, Address/Hex/ASCII presentation, Go To, Refresh, Region / Module, Protection, and visible range all remain as implemented in rev1.
- Scan Results multi-selection **Save Address** semantics are unchanged from rev1; rev2 only repairs how the WPF context menu reaches that already-implemented host method.
- Plugin API remains `2.9.0`, PS5 plugin remains `0.1.0.rev22`, and Mock plugin remains `1.0.0.rev4`. No Plugin SDK contract, PS5 protocol command, native scan mapping, memory reader/writer implementation, or plugin-specific functionality changes.
- Core scanner/storage/export behavior, the 50,000-row presentation boundary, complete-result disk/resident semantics, Protection resolution/export, Saved Address refresh/write/Freeze behavior, and pretty JSON export remain unchanged.
- The dependency-free automated registry remains exactly **51 checks**. Rev2 changes WPF compile/integration and repository source tooling, so no existing automated production check is removed or renumbered.

### Verification

- The supplied `0.1.5.rev1` ZIP was used as the baseline. README, CHANGELOG, all Markdown files under `docs/`, and the complete source/project inventory were reviewed before the corrective code change.
- Static regression review confirms there are no code-behind event attributes remaining under a direct `Setter.Value` object graph in the current XAML source. An equivalent source-preflight rule detects all three problematic rev1 event placements and none in the corrected rev2 tree.
- MainWindow XAML/context-menu code was reviewed together with the existing Scan Results/Saved Addresses selection, copy, Freeze, Remove, Browse Memory, and bulk-save paths so the compile fix does not silently replace row semantics with window/plugin semantics.
- Final Windows/WPF build, source-preflight execution under Windows `PresentationFramework`, the **51/51** verification run, and runtime context-menu/Memory Viewer checks remain user-run acceptance steps documented in `docs/testing/APP_0.1.5_REV2_VERIFICATION.md`.

## TeeKay87's Memory Engine 0.1.5.rev1 - Memory Viewer Foundation

### Added

- Added the first production **Memory Viewer Foundation** as a platform-neutral read-only subsystem. `TeeKay87.MemoryEngine.Core.MemoryViewer.MemoryViewerReader` reads through the existing `IMemoryReader` contract and returns a neutral `MemoryViewSnapshot`; no PS5-specific protocol command or Plugin SDK contract was added.
- Added bounded Memory Viewer reads with a default 512-byte visible window, 16-byte row width, a 65,536-byte defensive maximum request size, readable-region validation, Guard rejection, centered-window positioning where possible, row-boundary alignment, and strict clamping to the containing `MemoryRegion`. Row alignment is applied only when the aligned page still contains the requested address, including near intentionally unaligned region ends. Partial backend reads are accepted only when they still contain the requested address. The viewer never reads through a region boundary merely to fill its page.
- Added a themed, modeless `MemoryViewerWindow` with hexadecimal **Address** input, **Go To**, **Refresh**, target/plugin identity, Region / Module, containing region range, Protection, visible range, and a virtualized read-only Address / Hex Bytes / ASCII table. Auto-generated DataGrid columns are disabled, non-printable ASCII bytes render as `.`, and the row containing the requested address is selected and scrolled into view after each successful read.
- Added **Browse Memory** to the Scan Results context menu. It opens Memory Viewer at the clicked result address while capturing the current Active Target identity.
- Added **Browse Memory** to the Saved Addresses context menu. It opens Memory Viewer at the saved absolute address while carrying the Saved Address process identity rather than assuming the currently selected process.
- Added target/connection safety for Memory Viewer. Each window captures the owning process identity and the host connection generation; every read verifies both before using the current session, so an old viewer cannot silently redirect itself to another process or to a later reconnect that happens to reuse the same process id/name.
- Preserved backend-provided Region / Module naming semantics in Memory Viewer: module name is preferred, then region name, and anonymous mappings remain blank instead of receiving a host-invented label.
- Added host-side Memory Viewer I/O coordination through the existing foreground-target reservation. Viewer reads prevent new Saved Address timer work from starting, wait for already-running Saved Address I/O to reach its safe idle boundary, use the primary neutral `IMemoryReader`, and release the reservation afterward. Existing scan/write/other foreground conflicts are rejected rather than overlapped.
- Added two automated Core verification checks: **Memory Viewer bounded readable window** and **Memory Viewer guarded-region rejection**. The bounded-window check also covers an intentionally unaligned near-end region boundary so row alignment cannot move the requested address outside the returned page. The verification registry therefore increases from 49 to **51 checks**.
- Added `docs/architecture/MEMORY_VIEWER.md`, documenting the implemented boundary, target safety, read/window semantics, WPF presentation, and the planned `0.1.5` progression.
- Added `docs/testing/APP_0.1.5_REV1_VERIFICATION.md` with clean-build, 51-check, Mock-target, PS5-live, target-safety, theme, multi-selection, and regression verification steps.

### Changed

- Changed Scan Results context-menu **Save Address** so it respects WPF multi-row selection. When the context-clicked row is part of a selection containing multiple current Scan Results, every selected row is processed rather than only the row that supplied the context menu.
- Changed bulk Save Address handling to skip rows whose Saved Address identity already exists, refresh the derived Saved Address count/export-state properties once after the additions, select the last relevant row, and report added/already-saved counts in one status message. The existing target + address + Value Type identity rule is unchanged.
- Kept Scan Results double-click as the existing single-row Save Address shortcut; the multi-selection behavior applies specifically to the context-menu Save Address workflow requested for selected rows.
- Advanced the host application version from verified `0.1.4.rev5` to `0.1.5.rev1` and reset revision numbering to 1 because the Universal Export feature block is complete and verified and development has moved to the Memory Viewer feature block. `AppInfo` remains the authoritative title/version/revision/feature source.
- Updated README and architecture/UI documentation to describe Memory Viewer as current functionality rather than future work, to document the new Scan Results multi-selection behavior, and to record that `0.1.4.rev5` passed all 49 automated checks plus its runtime visual acceptance.
- Updated the early-development architecture guide only as implementation status/guidance: it now records the rev1 Memory Viewer foundation while explicitly retaining the document's non-frozen nature.

### Preserved

- The verified `0.1.4` export pipeline is unchanged: JSON/CSV/TSV/Markdown formats, scopes, column selection, transactional temp-file publication, progress/cancellation, Protection export, disk-backed/resident complete-set semantics, and indented JSON all retain their rev5 behavior.
- Scan Type semantics, disk-backed generation lifecycle, backend-resident TurboScan behavior, 50,000-row WPF scan preview boundary, live Scan Result refresh, Saved Address read/write/freeze coordination, Protection resolution, and target process-selection rules are unchanged except for the new explicit Memory Viewer read caller and multi-row Save Address invocation.
- Plugin API remains `2.9.0`, PS5 plugin remains `0.1.0.rev22`, and Mock plugin remains `1.0.0.rev4`. No platform plugin source or protocol mapping is changed by this host/Core feature.
- Memory Viewer rev1 is deliberately read-only. Byte/cell selection/copy/navigation history are deferred to a later `0.1.5` revision, safe editing/write support follows after that, and bookmarks/richer region behavior remain rev4+ work. Architecture-neutral disassembly is expected only after the Memory Viewer feature block is complete and verified.

### Verification and Documentation

- Recorded the final user verification result for `0.1.4.rev5`: **All 49 checks passed** and the template-based Saved Addresses Protection text was visually confirmed centered at runtime. The `0.1.4` block is therefore closed as verified.
- Reviewed README, CHANGELOG, all Markdown documentation under `docs/`, and the complete source/XAML/project/test inventory before changing the verified rev5 baseline.
- Added 51-check automated coverage for the new Core read boundary while retaining every previous scanner/export/plugin/PS5 verification check.
- Final Windows build, WPF runtime behavior, Mock Memory Viewer workflow, PS5 live memory inspection, multi-selected Save Address behavior, and theme checks remain user-run acceptance steps for this revision and are documented in `docs/testing/APP_0.1.5_REV1_VERIFICATION.md`.

## TeeKay87's Memory Engine 0.1.4.rev5 - Saved Address Protection Rendering Alignment Fix

### Fixed

- Corrected the Saved Addresses **Protection** vertical-alignment defect that remained visible at runtime in `0.1.4.rev4`. Rev4 changed the `DataGridCell.VerticalContentAlignment`, but the `DataGridTextColumn` still generated its own `TextBlock` presentation element and the text continued to render too high within the taller Saved Address row.
- Replaced the Saved Addresses Protection `DataGridTextColumn` with an explicit read-only `DataGridTemplateColumn`. The column now stretches its cell content vertically and renders Protection through an explicit `TextBlock` with `VerticalAlignment="Center"`, so the element that actually draws the text is centered rather than relying on container alignment to influence a generated element.
- Preserved the original column's sortable/clipboard semantics explicitly through `SortMemberPath="Protection"` and `ClipboardContentBinding="{Binding Protection}"`; moving to a template column does not intentionally remove header sorting or DataGrid clipboard content for Protection.
- Kept the correction local to the Saved Addresses Protection column. No global `DataGridCell`, `TextBlock`, row-height, TextBox, ComboBox, Scan Results, or theme style is changed as part of this revision.

### Changed

- Advanced the host application from `0.1.4.rev4` to `0.1.4.rev5` with feature title `Saved Address Protection Rendering Alignment Fix` through centralized `AppInfo`. The semantic application version remains `0.1.4` while the Universal Export/Protection feature block remains under runtime verification.
- Updated README and current architecture/UI documentation to describe the explicit template-based Protection rendering used by the current build rather than claiming the ineffective rev4 container-only correction was sufficient.
- Recorded the actual rev4 verification outcome: the Windows automated verification suite passed **49/49**, while the required Saved Addresses Protection visual alignment check failed at runtime. Rev4 therefore did not satisfy its acceptance criteria.
- Added `docs/testing/APP_0.1.4_REV5_VERIFICATION.md` with clean-build, existing 49-check regression, explicit Protection alignment, selection/theme, resize, multi-row, data, and export verification steps.

### Preserved

- Protection data semantics are unchanged. Rev5 does not alter `MemoryProtection`, memory-map lookup, address/range validation, refresh timing, or the conditions under which Protection is blank.
- Scan Results Protection presentation is unchanged; only the Saved Addresses Protection rendering path is replaced.
- Universal Export is unchanged: Protection selection/output, indented JSON, CSV/TSV/Markdown output, complete-set streaming, progress/cancellation, and transactional destination publication retain rev3/rev4 behavior.
- Saved Address Description/Address/Type/Value editing, direct writes, Frozen writes, background refresh, target-identity checks, pending removal, user-operation coordination, and export snapshot behavior are unchanged.
- Plugin API remains **2.9.0**, PS5 plugin remains **0.1.0.rev22**, Mock plugin remains **1.0.0.rev4**, and no platform-specific code is changed.
- The automated verification registry remains at **49 checks** because rev5 is a host-WPF rendering correction. Existing automated checks continue to cover the Protection/export/scanner contracts; the visual alignment itself remains a runtime UI acceptance item.

### Verification and Documentation

- `0.1.4.rev4` was run on Windows and completed the full automated verification suite with **All 49 checks passed**. Runtime inspection in the Dimmed theme demonstrated that the Protection text was still positioned too high, proving the rev4 `DataGridCell.VerticalContentAlignment` approach did not correct the generated `DataGridTextColumn` text element.
- Before editing rev5, reread README, CHANGELOG, every Markdown document under `docs/`, and reviewed the complete current source/project/resource inventory from the packaged rev4 baseline.
- Reviewed the existing WPF DataGrid styles and all current `ElementStyle`/template usage before changing the column. No existing shared centered read-only DataGrid-text style was available to reuse without broadening the change.
- The rev5 implementation explicitly controls both sides required for deterministic centering: the `DataGridCell` content host stretches vertically, and the template-owned `TextBlock` centers itself within that stretched area.
- Per established project workflow, no assistant-side .NET/WPF build result is claimed. Windows build, the existing 49/49 suite, and visual runtime verification in Light/Dimmed/Dark remain user-run according to `docs/testing/APP_0.1.4_REV5_VERIFICATION.md`.

## TeeKay87's Memory Engine 0.1.4.rev4 - Saved Address Protection Vertical Alignment Fix

### Fixed

- Fixed the read-only **Protection** text in the **Saved Addresses** table being aligned against the top of each row instead of vertically centered. Saved Address rows are taller than ordinary text-only DataGrid rows because Description, Address, Type, and Value use the shared 34-unit editor controls; the new Protection `DataGridTextColumn` had inherited the default cell vertical-content alignment introduced in rev3.
- Added a local `DataGridCell` style only to the Saved Addresses Protection column and set `VerticalContentAlignment` to `Center`. The style is based on the existing application DataGridCell style so theme, selection, focus, borders, and other shared DataGrid behavior remain intact.

### Changed

- Advanced the host application from `0.1.4.rev3` to `0.1.4.rev4` with feature title `Saved Address Protection Vertical Alignment Fix` through centralized `AppInfo`. The semantic application version remains `0.1.4` because the Universal Export/Protection feature block is still being runtime-verified.
- Updated README, Saved Addresses architecture documentation, Main Workspace UI documentation, the early architecture guideline's current implementation note, and current-version references in scanner/export documentation.
- Added `docs/testing/APP_0.1.4_REV4_VERIFICATION.md` for clean build, existing 49-check regression verification, Saved Addresses alignment checks, theme checks, Protection data regression, and export regression.

### Preserved

- Protection data semantics are unchanged. Rev4 does not alter `MemoryProtection`, memory-map lookup, address/range validation, refresh timing, or the conditions under which Protection is blank.
- Scan Results Protection presentation is unchanged. The alignment correction applies only to the Saved Addresses Protection column reported in the rev3 runtime UI.
- Universal Export is unchanged: Protection selection/output, indented JSON, CSV/TSV/Markdown output, complete-set streaming, cancellation, progress, and transactional publication retain rev3 behavior.
- Saved Address editing, direct writes, Frozen writes, background refresh, target-identity checks, pending removal, and user-operation coordination are unchanged.
- Plugin API remains **2.9.0**, PS5 plugin remains **0.1.0.rev22**, Mock plugin remains **1.0.0.rev4**, and no platform-specific code is changed.
- The automated verification registry remains at **49 checks** because rev4 is a host-WPF visual alignment correction; existing automated checks continue to cover the affected Protection/export data contracts.

### Verification and Documentation

- Rev3 was observed running successfully enough to expose the new Protection data in both Scan Results and Saved Addresses; the reported issue was specifically that Saved Addresses Protection text was not vertically centered. Rev4 addresses that presentation defect without changing the verified data path.
- Windows build, the 49-check automated suite, and visual verification in Light/Dimmed/Dark remain user-run according to `docs/testing/APP_0.1.4_REV4_VERIFICATION.md`.

### Static Review Preparation

- Used the packaged `0.1.4.rev3` source tree as the baseline and reread all Markdown documentation plus the complete source/project inventory before editing.
- Confirmed the misalignment originated from the Saved Addresses Protection `DataGridTextColumn` using default cell vertical-content alignment while neighboring template columns use taller controls.
- Kept the fix local rather than changing the global DataGridCell style, avoiding unintended layout changes to Scan Results, other columns, dialogs, or future DataGrids.
- Structural pre-package checks cover XAML/project XML parsing, JSON parsing, Markdown relative links, centralized AppInfo metadata, exact 49-check registry count, unchanged plugin/API trees, absence of build artifacts, and ZIP roundtrip integrity. Per project workflow, .NET/WPF compilation and runtime verification remain user-run on Windows.

## TeeKay87's Memory Engine 0.1.4.rev3 - Memory Protection Columns and Pretty JSON Export

### Added

- Added a read-only **Protection** column to **Scan Results**. Materialized `MemoryScanResult` rows now carry the neutral `MemoryProtection` flags of the containing `MemoryRegion`, allowing the UI to distinguish ranges such as `Read`, `Read, Write`, and `Read, Execute` without inventing a region/module name when the backend supplied none.
- Added a read-only **Protection** column to **Saved Addresses**. A saved row resolves the protection of its complete current Value Type range from the cached Active Target memory map and keeps that metadata separate from its editable Address/Type/Value state.
- Added **Protection** to the shared export-column catalog for Scan Results and Saved Addresses. Displayed/Selected Scan Results export the materialized row protection, while Saved Addresses export the resolved protection captured in the stable host snapshot.
- Added complete-set Protection export for disk-backed and backend-resident **All Results**. The export source resolves each streamed address/value range against the memory-map snapshot supplied by the host, so Protection is available beyond the 50,000-row WPF presentation preview without creating millions of new WPF objects.
- Extended the existing automated export/scanner checks to verify Protection preservation, text/JSON output, and Protection resolution for disk-backed/backend-resident exports beyond the 50,000-row preview boundary. The verification registry remains at **49 checks** because existing checks were strengthened rather than adding another top-level test.
- Added `docs/testing/APP_0.1.4_REV3_VERIFICATION.md` covering Windows build, 49-check verification, Scan Results/Saved Addresses Protection, complete-set export, pretty JSON, cancellation, live PS5 behavior, and UI/theme regressions.

### Changed

- Advanced the host application from `0.1.4.rev2` to `0.1.4.rev3` with feature title `Memory Protection Columns and Pretty JSON Export` through centralized `AppInfo`. The semantic application version remains `0.1.4` because Universal Export is still in its current verification block.
- Changed structured JSON export from compact single-line output to **indented human-readable JSON** by enabling `JsonWriterOptions.Indented` on the existing streaming `Utf8JsonWriter`. JSON is formatted while it is produced; there is no second beautifier pass and no requirement to load the completed file or complete result set into memory. Schema version `1`, typed values, metadata, selected-column structure, progress, cancellation, row-count validation, and transactional publication remain unchanged.
- Scan Result protection is refreshed for currently materialized rows when the Active Target memory map changes. This refresh changes presentation/export metadata only and does not alter scan membership, Previous values, native/disk baselines, or target memory.
- Saved Address protection is recalculated when the Active Target memory map changes and after Address, Type, or successful variable-size Value changes that can change the covered range. Protection is cleared when the row does not belong to the Active Target, no map is available, or no single region contains the complete range.
- Updated **All Results** scope descriptions to state that complete disk-backed/backend-resident sources now guarantee Address, Value, Type, and Protection. Previous and Region / Module remain presentation-only unless the complete set itself is materialized.
- Updated README, Universal Export, Memory Scanner, Saved Addresses, Main Workspace, Early Development Architecture guideline, and current verification documentation for the rev3 behavior.

### Preserved

- **Region / Module** behavior is intentionally unchanged. If ps5debug-NG or another backend supplies no region/module name, the field remains blank; rev3 does not fabricate a label from address ranges or protection flags.
- Memory protection is informational/presentation data. Rev3 does not change page permissions, call a target protection API, or bypass the existing host checks that require readable ranges for refresh and writable ranges for direct writes/freeze operations.
- The verified scan-result disk format is unchanged. Protection is not appended to every stored record solely for export; complete stored/native exports resolve it from the already-loaded neutral memory map while streaming.
- The Plugin SDK already contained `MemoryProtection` and `MemoryRegion.Protection`, and the PS5 implementation already maps ps5debug-NG R/W/X values into that neutral model. Plugin API therefore remains **2.9.0**, PS5 plugin remains **0.1.0.rev22**, and Mock plugin remains **1.0.0.rev4**.
- Scan Type semantics, native mappings, resident result lifecycle, disk generations, 50,000-row WPF preview boundary, live-value refresh, Saved Address write/freeze/removal coordination, export scopes/formats, transactional cancellation, and target-I/O coordination remain unchanged apart from exposing the new Protection metadata.

### Verification and Documentation

- Recorded the user-run `0.1.4.rev2` verification result: **all 49 automated checks passed** on Windows, including Universal Export, disk-backed/resident complete-set export, scanner semantics, PS5 native protocol, Plugin API, storage, and plugin-host regression coverage.
- Rev3 keeps the same 49-check registry and strengthens relevant assertions for Protection plus indented JSON. Windows compilation/runtime and manual/live PS5 acceptance remain user-run according to `docs/testing/APP_0.1.4_REV3_VERIFICATION.md`.
- Reviewed the existing neutral memory-map model and current PS5 mapping before implementation. No platform-specific branch or duplicate protection enum was added to Core/WPF.

### Static Review Preparation

- Used the user-supplied `0.1.4.rev2` package as the exact baseline and reviewed the project documentation and complete source/project inventory before finalizing rev3.
- Audited every `MemoryScanResult` construction path so Core reader-based scans, list-based native scans, disk-backed previews, and backend-resident previews all preserve the containing region's Protection metadata.
- Audited all `MemoryScanResultExportSource` factory callers after adding the memory-map snapshot requirement for complete stored sources. Materialized, disk-backed, and backend-resident paths all expose Protection without introducing a platform-specific export branch.
- Confirmed PS5 plugin, Mock plugin, and Plugin SDK trees remain byte-identical to rev2; no public compatibility/version change is required.
- Structural pre-package checks cover XAML/project XML parsing, JSON parsing, Markdown relative links, exact 49-check registry count, centralized AppInfo metadata, absence of build/draft artifacts, and release-tree diff scope. Per project workflow, .NET/WPF build and runtime/live PS5 verification remain user-run on Windows.

## TeeKay87's Memory Engine 0.1.4.rev2 - Tabular Export Nullability Compile Fix

### Fixed

- Fixed the Windows build-blocking `CS8600` diagnostic in `TeeKay87.MemoryEngine.Core/Exporting/TabularExportService.cs`. `Directory.Build.props` intentionally enables nullable reference analysis and promotes warnings to errors, and `Dictionary<TKey,TValue>.TryGetValue(...)` annotates its `out` value as potentially null when the lookup fails. Rev1 passed that result directly into a non-nullable `ExportColumn` local, so the Core project could not produce `TeeKay87.MemoryEngine.Core.dll`.
- Changed the selected-column lookup to receive `TryGetValue(...)` into `ExportColumn?` and explicitly reject either a failed lookup or a null value before assigning the column to the resolved export array. This preserves the existing invalid-column exception behavior while satisfying nullable analysis without using the null-forgiving operator and without weakening the repository's warning policy.
- Identified the accompanying `CS0006`, `XLS0414`, and `XDG0008` diagnostics reported for `MainWindow.xaml` as downstream assembly-load/designer failures caused by the missing Core build output. `ProportionalGridSplitter` and `TextBoxInputFilter` remain present with the same public types and namespaces used by `MainWindow.xaml`; no XAML workaround or namespace change is introduced for those cascading errors.

### Changed

- Advanced the host application from `0.1.4.rev1` to `0.1.4.rev2` with feature title `Tabular Export Nullability Compile Fix` through the centralized `AppInfo` source. The semantic application version remains `0.1.4` because the Universal Export feature block is still awaiting Windows/runtime verification.
- Updated the current README revision/checklist references to rev2 while retaining the full rev1 Universal Export functionality description.
- Recorded the actual rev1 Windows build failure in `docs/testing/APP_0.1.4_REV1_VERIFICATION.md` and added `docs/testing/APP_0.1.4_REV2_VERIFICATION.md` for the corrected clean-build, existing 49-check, export/runtime, and rev32 regression verification path.
- Updated current-version wording in the scanner/export architecture documentation where the document describes the current host build or emits an example `applicationVersion`; historical statements that Universal Export was introduced in rev1 remain historical.

### Preserved

- Universal Export behavior is unchanged from rev1: JSON/CSV/TSV/Markdown writing, transactional temporary-file publication, selectable scopes/columns, complete disk-backed/backend-resident **All Results**, 50,000-row displayed-preview semantics, Saved Address snapshots, progress/cancellation, and resident target-I/O coordination are not redesigned by this revision.
- The verification registry remains at **49 checks**. No test is removed or weakened; rev2 exists so the already-added rev1 tests can actually build and run on Windows.
- Plugin API remains **2.9.0**, PS5 plugin remains **0.1.0.rev22**, and Mock plugin remains **1.0.0.rev4**. No plugin contract, ps5debug-NG protocol path, native scan mapping, scanner predicate, result-storage format, Saved Address write/freeze path, target/session lifecycle, theme resource, or XAML layout behavior changes.
- `Nullable` remains enabled and `TreatWarningsAsErrors` remains enabled in `Directory.Build.props`. The compile correction conforms to the existing quality policy rather than bypassing it.

### Documentation and Verification Preparation

- Re-read `README.md`, `CHANGELOG.md`, all **85** Markdown files under `docs/`, and the complete current source/project/resource tree from the packaged rev1 baseline before changing code.
- Reviewed the reported compiler/designer chain against the actual project references and declarations. `TeeKay87.MemoryEngine.App` still references Core, `ProportionalGridSplitter` remains a public control in `TeeKay87.MemoryEngine.App.Controls`, and `TextBoxInputFilter` remains a public attached-property owner in `TeeKay87.MemoryEngine.App.Input`; their XAML namespace declarations are unchanged and valid in source.
- Audited the new Core export files and their callers for another copy of the same non-nullable `TryGetValue` pattern. The reported `ExportColumn` lookup is the only occurrence in the rev1 export implementation requiring this correction.
- Per established project workflow, no assistant-side .NET/WPF build result is claimed. Windows **Rebuild Solution**, the existing **49/49** verification run, and the rev1 export/live-PS5 acceptance tests remain required before the `0.1.4` export feature block is considered verified.

## TeeKay87's Memory Engine 0.1.4.rev1 - Universal Export Foundation

### Added

- Added the first shared **universal list/table export foundation** to Core. `IExportDataSource`, typed `ExportCellValue` values, stable export-column definitions, bounded `ExportRowBatch` streaming, and `TabularExportService` now provide one reusable path for current and future list-based application features instead of requiring each view to implement its own file writer.
- Added generic **JSON**, **CSV**, **TSV**, and **Markdown table** writers. CSV/TSV quote embedded delimiters, quotes, and line breaks; Markdown escapes table separators/backslashes and converts embedded line breaks so exported rows remain structurally valid.
- Added a structured JSON envelope with export type, schema version, UTC export timestamp, authoritative row count, metadata, selected-column descriptors, and typed row properties. Rev1 defines schema version 1 for the current Scan Results and Saved Addresses sources; it does not define a re-import contract.
- Added transactional destination handling for every export. Data is streamed into a uniquely named temporary file in the destination directory, the source's emitted row count is validated, and the completed file is published only after a successful write. Cancellation or failure removes the temporary output and does not replace an existing completed destination file.
- Added a reusable theme-aware **Export Data** dialog for choosing export scope, format, and included columns. The dialog is shared by the first two export consumers rather than being hard-coded to one workspace and reuses the application's existing semantic control/error resources, including `ErrorTextBrush` for validation feedback.
- Activated **Scan Results -> Export...** with three scopes where applicable:
  - **All Results** reads the complete authoritative scan result set;
  - **Displayed Results** exports only the currently materialized WPF presentation rows;
  - **Selected Results** exports only the selected displayed rows.
- Added complete-set Scan Results export adapters for all current result-storage shapes. Small materialized sets are exported directly, committed `IScanResultSet` generations are read in bounded disk-backed batches, and `INativeValueScanResidentResultSet` sources are read in bounded backend-resident batches without first materializing the entire set on the PC.
- Added explicit complete-set column boundaries. Materialized Scan Results can export **Address**, **Value**, **Previous**, **Type**, and **Region / Module**. Complete disk-backed/backend-resident exports guarantee **Address**, **Value**, and **Type**, matching the data retained by the already-verified complete-result storage contracts without widening the storage format merely for export.
- Activated **Saved Addresses -> Export...** with **All Addresses** and **Selected Addresses** scopes. Saved Address export snapshots **Frozen**, **Description**, **Address**, **Type**, and **Value** before file writing, so no target read/write is required during export.
- Reused the existing generic modal operation-progress service for long exports, including determinate row progress and cancellation.
- Added five dependency-free verification checks for the new subsystem: text-format escaping, structured JSON behavior, transactional cancellation, complete disk-backed Scan Results export beyond the 50,000-row presentation boundary, and complete backend-resident Scan Results export beyond that same boundary. The verification registry therefore increases from **44 to 49 checks**.
- Added `docs/architecture/UNIVERSAL_EXPORT.md` and `docs/testing/APP_0.1.4_REV1_VERIFICATION.md` for the implemented architecture, schema/scope boundaries, large-result behavior, and Windows/live verification plan.

### Changed

- Advanced the host from the user-verified `0.1.3.rev32` scanner block to `0.1.4.rev1` with feature title `Universal Export Foundation` through centralized `AppInfo`. The new version begins at revision 1 because the user confirmed `0.1.3.rev32` works as intended and closes the `0.1.3` scanner feature block.
- Changed the existing disabled Scan Results and Saved Addresses **Export...** placeholders into functional commands backed by the shared export pipeline.
- Large Scan Results export now follows the same authoritative-result boundary as Next Scan. **All Results** must not be confused with the WPF preview: a scan containing more than 50,000 candidates reads the complete disk-backed or backend-resident set, while **Displayed Results** is explicitly the bounded presentation scope.
- Backend-resident **All Results** export participates in the existing target-I/O coordination rules because it reads the resident result set through the plugin's primary native command stream. Ordinary live refresh and conflicting foreground target actions are suppressed for that resident export. Existing Frozen writes may continue only when the plugin provides the already-established concurrent memory writer path; otherwise they pause until the export leaves the resident target stream.
- Scan Results export is unavailable while a scan/foreground result operation can replace or dispose the active complete result set. This prevents an export from retaining a disk-backed or backend-resident source while Next Scan is concurrently publishing a replacement set.
- Saved Addresses **Export...** is temporarily disabled while a direct Saved Address user operation (such as an Address/Type/Value commit) is in progress. This prevents clicking Export immediately after an edit from snapshotting the pre-commit value while the LostFocus-triggered write is still completing. Background refresh/Frozen scheduling does not require this gate because export snapshots the current host rows synchronously and performs no target I/O.
- Updated `README.md` to describe the current `0.1.4.rev1` export workflow, complete-result versus displayed-result semantics, active Saved Address export, 49-check verification boundary, and current development boundary. The stale current-verification reference to rev31 is replaced by the rev1 checklist.
- Updated scanner, scan-result storage, Saved Addresses, main-workspace, modal-progress, and early-architecture documentation to describe the actual implemented export boundary. `EARLY_DEVELOPMENT_ARCHITECTURE.md` remains a design guide rather than a frozen implementation specification.
- Recorded the user's 2026-09-05 acceptance of `0.1.3.rev32` in its verification document before opening the new `0.1.4` feature block.

### Preserved

- Plugin API remains **2.9.0**. Universal export is a shared Core/host feature and introduces no new plugin contract.
- PS5 plugin remains **0.1.0.rev22** and Mock plugin remains **1.0.0.rev4**. No ps5debug-NG protocol command, native Scan Type mapping, plugin capability, or plugin-specific export implementation is added in this revision.
- The verified disk-backed scan-result record format, backend-resident result contract, 50,000-row WPF presentation ceiling, First/Next/New Scan semantics, Changed/Unchanged byte semantics, live Scan Result refresh, Saved Address editing/freezing, and target/session lifecycle remain unchanged outside the temporary I/O coordination required while a resident export is actively reading its source.
- Complete Scan Results export does not manufacture unavailable historical fields. Rev1 deliberately leaves the verified complete-result storage format unchanged instead of adding Previous/Region data solely to satisfy an early conceptual JSON example.
- Export output is not treated as the application's future project/cheat persistence format, and rev1 does not add import/re-import, pointer persistence, Memory Viewer export, debugger export, disassembly export, or plugin cheat exporters.

### Documentation

- Added `docs/architecture/UNIVERSAL_EXPORT.md` as the authoritative description of the rev1 shared export contracts, ownership boundary, streaming strategy, output formats, JSON envelope, scopes, complete-result behavior, cancellation, and current non-goals.
- Added `docs/testing/APP_0.1.4_REV1_VERIFICATION.md` with clean-build, 49-check, UI-format/scope, disk-backed large-result, live PS5 resident-result, cancellation, overwrite-safety, I/O-coordination, and rev32 regression verification steps.
- Updated `docs/architecture/SCAN_RESULT_STORAGE_FOUNDATION.md` with complete-set export integration for materialized, disk-backed, and backend-resident result sets.
- Updated `docs/architecture/SAVED_ADDRESSES.md` with the active Saved Address export integration and removed export from the feature's obsolete non-goals.
- Updated `docs/architecture/MEMORY_SCANNER_FOUNDATION.md`, `docs/ui/MAIN_WORKSPACE.md`, and `docs/ui/MODAL_OPERATION_PROGRESS.md` for the current export consumer/progress behavior.
- Updated `docs/architecture/EARLY_DEVELOPMENT_ARCHITECTURE.md` so its development-order status reflects the user-verified completion of `0.1.3` and the initial `0.1.4.rev1` export implementation while preserving the document's role as an architectural guideline.

### Verification

- Started from the complete user-supplied and user-verified `0.1.3.rev32` ZIP. `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, and the complete source/project inventory were reviewed before export code was added.
- Added verification coverage that writes **50,005** complete disk-backed results and independently exposes **50,005** backend-resident results, proving the universal **All Results** source requests and writes beyond the 50,000-row WPF presentation ceiling instead of silently exporting only the preview.
- Added cancellation coverage with a pre-existing destination file to verify that cancellation propagates, the old completed destination remains unchanged, and the temporary partial export is removed.
- The final pre-package review includes source/documentation diffs, C#/XAML/project/JSON structural checks, Markdown relative-link checks, version/plugin metadata consistency, exact 49-check registry validation, release-tree cleanliness, and archive round-trip verification.
- Per established project workflow, no assistant-side .NET/WPF build or live PS5 result is claimed. The clean Windows build, **49/49** executable verification run, and live export behavior remain user-run verification steps documented in the rev1 checklist.

## TeeKay87's Memory Engine 0.1.3.rev32 - PS5 Floating Changed-Unchanged Semantics Fix

### Fixed

- Fixed a live PS5 Float/Double **Changed Value / Unchanged Value** semantic defect discovered while verifying the rev30/rev31 resident-result path. A Float **Unknown Initial Value** scan produced roughly 707 million resident candidates; the following **Changed Value** pass reduced the set to **66,937,361** results, but the bounded preview was dominated by `NaN` values that had not necessarily changed in memory.
- Fixed the root cause in shared Core scan semantics. **Changed Value** and **Unchanged Value** now compare the fixed-width bytes retained by consecutive scan generations instead of using the Value Type's numeric equality contract. This makes change detection representation-stable: an identical NaN payload remains unchanged, a different NaN payload is changed, and representation changes such as `+0.0` to `-0.0` are treated as byte changes.
- Fixed the corresponding PS5-native resident refinement path without forcing hundreds of millions of candidates through Core materialization. Current ps5debug-NG evaluates Float/Double compare types 9/10 through IEEE-754 `!=`/`==`; because `NaN != NaN` is true, unchanged NaN payloads can survive every Changed pass. For **Changed Value** and **Unchanged Value** only, the PS5 plugin now transmits the same-width unsigned integer Value Type in the TurboScan COUNT request: Float uses UInt32 and Double uses UInt64. ps5debug-NG therefore performs exact four-/eight-byte equality/inequality while the resident result width remains unchanged.
- Preserved selected Value Type interpretation across the workaround. TurboScan START, resident-session shape, result GET decoding, WPF display, and subsequent Core metadata remain Float/Double; only the native COUNT comparison type is reinterpreted for the two byte-change predicates.

### Changed

- Advanced the host from `0.1.3.rev31` to `0.1.3.rev32` with feature title `PS5 Floating Changed-Unchanged Semantics Fix` through centralized `AppInfo`.
- Advanced the PS5 plugin from `0.1.0.rev21` to `0.1.0.rev22` because its native TurboScan refinement request mapping changes. Its Plugin API target remains `2.9.0`.
- Plugin API remains `2.9.0`; rev32 does not add or modify a public plugin contract.
- The dependency-free verification registry increases from **43 to 44 checks**. The new **PS5 native floating Changed/Unchanged bitwise protocol** check verifies a Float Unknown Initial resident session followed by Changed COUNT using wire UInt32, and a Double Unknown Initial resident session followed by Unchanged COUNT using wire UInt64. Both cases also verify that GET records remain the selected floating-point width and expose correct previous/current bytes.
- Extended the existing Core Scan Type semantic check with explicit NaN-payload cases proving that identical floating snapshot bytes are Unchanged and not Changed, while a different NaN payload is Changed.

### Preserved

- Exact Value semantics are unchanged and continue to use the Value Type equality contract. Rev32 does not redefine user-entered floating-point equality, strict/tolerant Exact behavior, Fuzzy Value, Bigger/Smaller, Between, Increased/Decreased, Increased By/Decreased By, parsing, formatting, or rounding options.
- Integer Changed/Unchanged behavior remains equivalent because byte identity for a fixed-width integer is the same representation criterion already expected from consecutive scan snapshots.
- The rev30 backend-resident result architecture is unchanged: large PS5 TurboScan result sets can remain target-resident, only a bounded preview is fetched, compatible native Next Scans refine the complete target-side set, and Core materialization is deferred until a non-native predicate requires it.
- The 50,000-row presentation ceiling, visible-row live Value refresh, disk-backed generation format, transactional materialization, New Scan Exact-default fix, Saved Addresses, scan-option applicability, Endianness/Alignment locking, target I/O serialization, and rev29 snapshot command-stream recovery are unchanged.
- Core and WPF remain platform-neutral. The ps5debug-NG-specific Float/Double wire reinterpretation is contained entirely inside the PS5 plugin; Core defines only the shared stored-byte semantics of Changed/Unchanged.
- Mock plugin remains `1.0.0.rev4` targeting Plugin API `2.0.0`.

### Documentation

- Updated `README.md` for application `0.1.3.rev32`, PS5 plugin `0.1.0.rev22`, the stored-byte Changed/Unchanged definition, the PS5 same-width integer COUNT workaround, and the 44-check verification boundary.
- Updated scanner architecture and native-mapping documentation to distinguish Exact Value's Value Type equality from Changed/Unchanged's consecutive-snapshot byte comparison.
- Updated PS5 plugin, native scan, and ps5debug-NG protocol mapping documentation with the Float -> UInt32 and Double -> UInt64 COUNT-only mapping and the reason it is required for NaN-stable resident refinement.
- Recorded the actual rev31 Windows/live verification outcome in `docs/testing/APP_0.1.3_REV31_VERIFICATION.md`: **43/43 automated checks passed**, the New Scan regression passed live, the large resident path avoided immediate full materialization, and the subsequent Float Changed pass exposed the NaN semantic mismatch.
- Added `docs/testing/APP_0.1.3_REV32_VERIFICATION.md` covering the 44-check runner and focused live PS5 regression for Float/Double Changed/Unchanged while retaining resident-result, visible-refresh, New Scan, and fallback behavior.

### Verification

- Started from the complete user-supplied `0.1.3.rev31` ZIP and reviewed the project documentation and full source/project inventory before changing scan semantics or PS5 transport behavior.
- Rechecked current ps5debug-NG source for TurboScan COUNT and point comparison. COUNT uses its request `valueType` for comparison, requires only the resident session's byte width to match, and its optimized compare types 9/10 use typed `!=`/`==`. This makes same-width UInt32/UInt64 a protocol-compatible way to obtain byte equality for resident Float/Double snapshots without changing record width or GET decoding.
- Added regression coverage at both semantic layers: Core directly tests unchanged/changed Float NaN payloads, while the PS5 loopback fixture independently asserts the exact COUNT wire Value Type and returned previous/current byte payloads for Float Changed and Double Unchanged.
- Reviewed the change boundary so the only production semantic changes are shared Changed/Unchanged stored-byte comparison, the PS5 refinement Value Type selector, centralized host revision metadata, and PS5 plugin revision metadata. No Plugin SDK contract, storage format, WPF layout, Mock implementation, or native Scan Type id changes are required.
- The rev32 pre-package static audit covers **233 files**, including structural checks across **129 C# files**, parse checks for **14 XAML/project XML files** and **3 JSON files**, relative-link resolution across **85 Markdown files**, and an exact **44-check** test registry. Version metadata, protocol constants used by the workaround, release-tree cleanliness, and generic/platform code boundaries pass.
- Diff review against the user-supplied rev31 package shows **15 intentionally changed files, one new rev32 verification document, and no removed files**. No Plugin SDK contract, WPF production source, Mock production source, storage format source, project file, or theme file changes are introduced.
- Per project workflow, no assistant-side .NET/WPF runtime build result is claimed. The release package is statically audited and archive-verified; the clean Windows build, **44/44** executable verification run, and live PS5 acceptance remain user-run verification steps documented in the rev32 checklist.

## TeeKay87's Memory Engine 0.1.3.rev31 - Native Value-Type Verification Lifecycle Fix

### Fixed

- Fixed the deterministic verification-suite deadlock in **PS5 native value-type mapping protocol** introduced by the rev30 backend-resident result lifecycle. The fixture performs a native Exact First Scan for every supported ps5debug-NG Value Type. Each of those First Scans leaves an authoritative resident TurboScan session open until the client explicitly ends it.
- Fixed the Float/Double branch of `VerifyPs5NativeValueTypesAsync`. That branch intentionally verifies that strict Exact native refinement throws `NotSupportedException`, because ps5debug-NG's resident Float/Double Exact comparison uses relative `1e-6` tolerance and strict semantics must fall back to shared Core. Rev30 correctly caught that fallback request but then awaited `Ps5ProtocolTestServer.Completion` without first ending the resident First Scan session. The loopback server was waiting for `CMD_PROC_TURBOSCAN_END` while the test was waiting for server completion, so neither side could advance.
- Unified fixture cleanup so **every** Value Type path now calls `INativeValueScanRefiner.ResetAsync(...)` after its semantic assertions and before waiting for the loopback protocol server. Integer, Float, Double, and Array-of-Bytes cases therefore all exercise the same explicit resident-session lifecycle boundary.

### Changed

- Advanced the host from `0.1.3.rev30` to `0.1.3.rev31` with feature title `Native Value-Type Verification Lifecycle Fix` through centralized `AppInfo`.
- Kept the verification registry at **43 checks**. Rev31 repairs the lifecycle of an existing check rather than adding/removing coverage.
- Updated the current verification documentation so the interrupted rev30 Windows run is recorded accurately and rev31 acceptance explicitly requires the runner to advance from **PS5 native custom-alignment protocol** through **PS5 native value-type mapping protocol** and the remaining checks.

### Preserved

- Plugin API remains `2.9.0`; no public Plugin SDK contract changes are introduced.
- PlayStation 5 plugin remains `0.1.0.rev21` targeting Plugin API `2.9.0`; no PS5 production source changes are introduced by rev31.
- In-Memory Test Target remains `1.0.0.rev4` targeting Plugin API `2.0.0`.
- Rev30's backend-resident result architecture is unchanged: large authoritative PS5 TurboScan sets can stay target-resident, only the bounded presentation preview is fetched initially, compatible Next Scans refine the resident set directly, and Core fallback materializes the complete set on demand.
- Rev30's visible Scan Result live Value refresh is unchanged. Only currently realized DataGrid rows are periodically reread, `Previous` remains the previous-scan baseline, and off-screen preview rows are not periodically traversed/read.
- Rev30's New Scan stable-id/default-selection implementation is unchanged and remains part of the live acceptance checklist.
- Core's 13 Scan Types, PS5 native Scan Type mapping table, strict/tolerant Float semantics, disk-backed scan-result format, result-storage lifecycle, Saved Addresses behavior, scan-option applicability, target I/O coordination, process/memory protocols, themes, and WPF layouts are unchanged.
- The production behavior in which strict Float/Double Exact refinement requests shared-Core fallback is intentionally unchanged. Rev31 changes only the deterministic fixture's responsibility to explicitly close the resident First Scan session after verifying that fallback boundary.

### Documentation

- Updated `README.md` to identify `0.1.3.rev31` as the current build while continuing to describe the complete current scanner/resident-result/live-refresh functionality rather than using README as version history.
- Recorded the actual rev30 Windows automated-verification result in `docs/testing/APP_0.1.3_REV30_VERIFICATION.md`: all checks through **PS5 native custom-alignment protocol** passed, then **PS5 native value-type mapping protocol** stalled because the Float/Double fixture waited for server completion without issuing TurboScan END.
- Added `docs/testing/APP_0.1.3_REV31_VERIFICATION.md` covering the clean Windows build, 43-check rerun, exact value-type fixture regression, and the still-required live rev30 feature acceptance for New Scan defaults, large resident scans, native refinement, Core materialization, and visible Scan Result live refresh.

### Verification

- Re-extracted the complete rev30 release ZIP and used it as the rev31 baseline before making the targeted changes.
- Reviewed the complete documentation inventory and source/project inventory before finalizing the correction, including the resident-result lifecycle, PS5 refiner reset path, loopback server protocol sequence, and the exact Float/Double strict-fallback branch that stalled the user-run verifier.
- Confirmed the loopback server's native-scan path expects `CMD_PROC_TURBOSCAN_END` whenever a native resident session was created, including Float/Double First Scans; `ResetAsync(...)` maps to the existing production native-session reset service and therefore supplies the missing protocol completion without changing scan semantics.
- Confirmed required C# namespace imports remain present; the rev31 test change introduces no new type/namespace dependencies.
- The pre-package static audit covers **232 files**, including **129 C# files**, **14 XAML/project XML files**, **3 JSON files**, and **84 Markdown files**. XML/JSON parsing, Markdown relative-link resolution, C# delimiter/string/comment structure, version metadata, release-tree cleanliness, and the exact **43-check** registry all pass.
- Diff review against rev30 shows no removed files and only the intended test, centralized host metadata, README/changelog, current architecture/version references, rev30 result note, and new rev31 verification document changed. All PS5 production source, Core production source, Plugin SDK production source, Mock production source, XAML, themes, and project files are byte-identical to rev30.
- Per project workflow, no assistant-side .NET build/runtime result is claimed. The final package is statically audited and archive-verified; the Windows build and **43/43** verification run remain the user-run acceptance step.

## TeeKay87's Memory Engine 0.1.3.rev30 - Resident Scan Results and Live Visible Values

### Fixed

- Fixed the remaining **New Scan** Scan Type selection regression found during rev29 live verification. The failure occurred specifically after a successful First Scan when the user selected a Next-only Scan Type such as Changed Value and then clicked New Scan. Replacing the filtered `ScanTypes` collection caused WPF to invalidate the old `SelectedItem` while the new First Scan list was being published, leaving the ComboBox visually blank even though the host attempted to choose Exact Value. The host now keeps one persistent observable Scan Type collection, clears the stale backing selection before mutating that collection, rebuilds the complete valid stage list in place, and publishes the resolved stable-id selection only after Core's default is present. New Scan therefore restores **Exact Value** even when the previous selection is not legal for First Scan.
- Removed the unnecessary immediate full-transfer penalty for very large authoritative native result sets. Rev29 could successfully create a PS5 Unknown Initial snapshot containing hundreds of millions of candidates, but then immediately fetched every survivor through TurboScan GET and wrote the complete set to the PC before the user could continue. Rev30 can retain that complete set in the plugin/backend and fetch only the bounded presentation window.
- Preserved the rev29 TurboScan protocol-recovery fix while extending resident refinement: sentinel-terminated COUNT progress streams no longer use an arbitrary host record-count limit, so long native refinement progress sequences are drained to their protocol boundary before the returned survivor count is accepted.

### Added

- Added Plugin API `2.9.0` optional contract `INativeValueScanResidentResultSet`. A native result stream can now expose an authoritative 64-bit count, stable Value Size/alignment, bounded result-window reads, bounded address enumeration, previous-value transport, and a non-mutating `CanRefine(...)` query while keeping the complete result membership in the plugin/backend.
- Extended `NativeValueScanResultBatch` with optional packed Previous-value bytes. Existing constructors remain available, so older streaming producers that provide only current values keep the same call shape. Core consumes Previous values only when the batch explicitly carries them.
- Added Core helpers to validate resident result shape/address ordering, load a bounded resident preview, determine whether a requested Next Scan can remain native, start native resident refinement, and materialize a complete resident set into the existing transactional disk-backed writer when host-side refinement is required.
- Added an on-demand **Materializing Scan Results** operation using the existing generic modal progress component. Materialization reports transferred/stored counts, supports cancellation, commits only after the complete resident set is written, and leaves partial generations unpublished on cancellation/failure.
- Added live Value refresh for **only the Scan Results rows currently realized by the WPF DataGrid viewport**. DataGrid row virtualization registers/unregisters visible `ScanResultViewModel` instances, and the existing Live value refresh scheduler rereads only that small set through neutral `IMemoryReader`. Off-screen rows are not periodically read. The `Previous` column remains the previous-scan baseline and is not changed by live refresh.
- Added two verification checks, increasing the dependency-free suite from 41 to **43 checks**: a generic resident-result contract/materialization test and a PS5 resident TurboScan bounded-preview/native-refinement protocol test.

### Changed

- Advanced the host from `0.1.3.rev29` to `0.1.3.rev30` with feature title `Resident Scan Results and Live Visible Values` through centralized `AppInfo`.
- Advanced Plugin API from `2.8.0` to `2.9.0` for the optional backend-resident result-set contract and Previous-value batch extension. Same-major 2.x compatibility rules remain unchanged.
- Advanced the PS5 plugin from `0.1.0.rev20` to `0.1.0.rev21` and its target API from `2.8.0` to `2.9.0` because its TurboScan result stream now implements the resident-result contract. Mock remains `1.0.0.rev4` targeting API `2.0.0`.
- Large authoritative PS5 TurboScan First Scans now remain target-resident when their result count exceeds the 50,000-row presentation ceiling. The host requests only the first 50,000 records for presentation instead of immediately transferring the complete result set to local disk.
- Compatible mapped Next Scans run directly against the resident TurboScan session. The refined resident handle replaces the previous generation and bounded preview GET records include the target-provided previous value so the UI's `Previous` column retains correct Next Scan semantics without a local previous-generation file.
- A Next Scan that cannot be represented by the resident native backend automatically materializes the complete current resident set into the established Core disk format and then runs the shared Core refinement. This preserves predicates deliberately kept as Core fallback, including Increased By/Decreased By and other runtime-incompatible native shapes.
- The Live value refresh interval in Settings now controls both Saved Address value refresh and visible Scan Result value refresh. The existing serialized target-I/O coordination remains authoritative: automatic reads pause during First/Next Scan and yield to foreground target operations.

### Preserved

- Core still owns the same 13 standard Scan Types, stable ids, operand counts, comparison semantics, First/Next-stage availability, and Value Type compatibility filtering.
- The PS5 Core-to-native Scan Type mapping table is unchanged. Rev30 changes how a complete native result set is retained/consumed, not which predicates are considered semantically equivalent.
- The compact disk-backed record format, generation commit protocol, application/scan-session isolation, stale-session cleanup rules, and 50,000-row WPF presentation ceiling are unchanged. Resident result sets are an additional complete-result representation, not a replacement for shared disk storage.
- Rev29's long snapshot-progress handling and same-connection snapshot-refusal recovery remain intact.
- Endianness, Alignment, Floating-point rounding, Pause target while scanning, Value Type parsing/filtering, Saved Address editing/freezing/removal, themes, process selection, raw memory access, and plugin discovery keep their existing behavior.
- Scan Result live refresh changes only the displayed current `Value`; it does not mutate the stored/native scan baseline, `Previous`, result membership, or total count.

### Documentation

- Updated README for application `0.1.3.rev30`, Plugin API `2.9.0`, PS5 plugin `0.1.0.rev21`, resident result behavior, deferred materialization, visible Scan Result live refresh, and the 43-check verification boundary.
- Updated scanner, native-mapping, Plugin SDK, scan-storage, Saved Addresses coordination, modal-progress, main-workspace, PS5 plugin, native-scan, and ps5debug-NG protocol documentation where rev30 changes the current architecture.
- Recorded the user-run rev29 verification outcome in `docs/testing/APP_0.1.3_REV29_VERIFICATION.md`: 41/41 automated checks passed, the Next-only -> New Scan blank-selection regression remained reproducible, and a live Float Unknown Initial run advertised **705,163,264** resident candidates before the immediate host materialization cost prompted the rev30 design change.
- Added `docs/testing/APP_0.1.3_REV30_VERIFICATION.md` covering the Windows build, 43-check runner, exact New Scan regression, large resident PS5 First/Next Scans, on-demand Core materialization, visible-row live refresh, cleanup, and UI/theme regressions.

### Verification

- Re-extracted the user-supplied rev29 ZIP and confirmed the rev30 working baseline matches it exactly before the intended rev30 changes.
- Reviewed every changed production/test path against the rev29 baseline. Platform-specific TurboScan protocol/state remains inside the PS5 plugin; the new public contract and resident validation/materialization logic are platform-neutral. WPF contains no ps5debug-NG command ids or PS5-specific resident branch.
- Traced resident lifecycle across First Scan, native Next Scan replacement, Core-fallback materialization, New Scan, target/session reset, cancellation/failure, and result-storage release. The existing native reset service remains responsible for releasing target-side sessions before local scan state is cleared.
- Reviewed the visible-row refresh path against DataGrid virtualization and the existing Saved Address I/O scheduler. Row realization controls membership in the refresh set; scan replacement clears stale registrations; automatic reads are suppressed while scanning or while foreground/user target operations own the serialized path.
- Verified source metadata consistency for host `0.1.3.rev30`, Plugin API `2.9.0`, PS5 `0.1.0.rev21` / API `2.9.0`, and Mock `1.0.0.rev4` / API `2.0.0`.
- The pre-package static audit covers **231 files**, including structural checks across **129 C# files**, parse checks for **14 XAML/project XML files** and **3 JSON files**, relative-link resolution across **83 Markdown files**, the **43-check** test registry, absence of build/draft artifacts, exact intended additions/removals relative to rev29, and platform-boundary checks for generic App/Core/Plugin SDK code.
- Per project workflow, no assistant-side .NET build/runtime claim is made. The final source package is statically audited and archive-verified; the Windows build, 43-check executable, WPF behavior, performance improvement, and live PS5 resident-session behavior remain user-run acceptance steps documented in the rev30 verification checklist.

## TeeKay87's Memory Engine 0.1.3.rev29 - New Scan Defaults and TurboScan Snapshot Recovery

### Fixed

- Fixed the **New Scan** Scan Type selection regression found during rev28 live verification. Rebuilding the First Scan-compatible `ScanTypes` list could leave WPF's ComboBox visually unselected even though the ViewModel still had a valid backing selection. New Scan now explicitly prefers Core's `MemoryScanTypeCatalog.DefaultScanTypeId` and republishes `SelectedScanType` whenever the filtered Scan Type `ItemsSource` is rebuilt. The default remains **Exact Value**.
- Fixed the large PS5 **Unknown Initial Value** failure reported during rev28 live testing. The PS5 client imposed a host-only limit of 1,024 TurboScan snapshot progress records even though the ps5debug-NG protocol defines a sentinel-terminated progress stream with no such record-count limit.
- Removed that arbitrary progress-record cap. A valid native snapshot now consumes progress records until the protocol sentinel regardless of how many target-side I/O windows were needed.
- Fixed the command-stream instability caused by the old progress cap. Throwing after record 1,025 left the remaining progress records, snapshot summary, and completion status unread on the shared ps5debug-NG TCP stream, so later commands could interpret leftover snapshot bytes as command responses and require Disconnect/Connect to recover.
- Snapshot START now consumes the complete progress stream, summary, and final status before applying host-side summary validation or selecting Core fallback. A server-side `snapshot_ok == 0` therefore remains a normal `NotSupportedException` acceleration refusal after a clean protocol boundary, allowing the existing shared scanner fallback to run without reconnecting.
- Hardened stored-session cleanup for malformed snapshot summaries: when the server reports that a snapshot was stored, the client records that a target-side session may exist before validating survivor/slot consistency, allowing the existing `finally` cleanup path to send TurboScan END if validation fails.
- Hardened the adjacent list-resident TurboScan START path so its final status is consumed before rejecting an invalid `resident_stored` state. Normal successful framing and the documented `resident_stored == 0` Core-fallback behavior are unchanged.

### Changed

- Advanced the host from `0.1.3.rev28` to `0.1.3.rev29` with feature title `New Scan Defaults and TurboScan Snapshot Recovery` through centralized `AppInfo`.
- Advanced the PS5 plugin from `0.1.0.rev19` to `0.1.0.rev20` because its TurboScan snapshot transport/recovery behavior changed.
- Plugin API remains `2.8.0`; rev29 does not add or modify a public Plugin SDK contract.
- The dependency-free verification runner now contains **41 checks**. The existing PS5 Unknown Initial snapshot test now sends 1,500 valid progress records, directly covering the old 1,024-record regression, and a new rejection-recovery check verifies that a fully consumed `snapshot_ok == 0` response leaves the same PS5 command stream usable for a subsequent process command.

### Preserved

- Core still owns the same 13 standard Scan Types and their comparison semantics. No Scan Type id, operand count, First/Next-stage rule, Value Type compatibility rule, or previous-value behavior changes in rev29.
- The generic Plugin API `2.7.0` native Scan Type mapping contract and the PS5 semantic mapping table remain unchanged. Unknown Initial Value still maps to TurboScan snapshot mode with explicit zero inclusion; Increased By/Decreased By and floating Unknown Initial Low remain deliberate Core fallbacks.
- The disk-backed result format, generation commit rules, complete-set refinement outside the 50,000-row UI preview, and massive native result streaming are unchanged.
- Rev28's PS5 Scan Option presentation/applicability behavior is unchanged: Little-endian remains a checkbox, Alignment remains a choice list, Floating-point rounding remains visible only for Exact Float/Double, and Between/multi-value inputs remain side-by-side.
- Saved Addresses, Frozen I/O coordination, process selection, raw memory access, process suspend/resume, themes, confirmation dialogs, and plugin discovery behavior are unchanged.

### Documentation

- Updated README to describe rev29's deterministic New Scan default and large TurboScan snapshot recovery behavior.
- Updated current scanner/native-mapping, PS5 plugin, PS5 protocol, native-scan/process-control, and main-workspace documentation where the corrected behavior is part of the current implementation.
- Recorded the user-run rev28 verification results in `docs/testing/APP_0.1.3_REV28_VERIFICATION.md`, including **40/40 automated PASS**, successful Scan Type/option/live comparison tests, and the two regressions that led to rev29.
- Added `docs/testing/APP_0.1.3_REV29_VERIFICATION.md` with focused build, New Scan default, large native snapshot, snapshot-rejection recovery, fallback, and regression checks.

### Verification

- Re-read the supplied rev28 documentation set and inventoried the complete source/project tree before changing code.
- Traced the rev28 New Scan issue through `ClearScanState`, `SetHasScanSession`, `RefreshScanTypesForCurrentStage`, and the WPF `SelectedItem` binding. The fix stays in generic host Scan Type state management and adds no platform-specific branch.
- Compared the ps5debug-NG TurboScan snapshot implementation and protocol documentation with the rev28 PS5 client. ps5debug-NG emits one `uint64` progress record per snapshot I/O window and terminates the stream with `0xFFFFFFFFFFFFFFFF`; its 16 MiB preferred buffers can fall back to smaller windows. Therefore a large valid snapshot may legitimately exceed 1,024 progress records even when a similarly sized run does not.
- Confirmed the observed instability follows directly from throwing before the snapshot sentinel/summary/final status were consumed. Rev29 moves all host-side snapshot validation to a safe post-response boundary.
- Extended the protocol test fixture to generate arbitrary valid snapshot progress-record counts and explicit snapshot acceptance/refusal responses while preserving normal resident-session framing for existing tests.
- Per project workflow, the Windows .NET build/runtime and live PS5 verification remain user-run; the package is subjected to the same static source, version, documentation, diff, and archive-integrity checks used for prior revisions.

## TeeKay87's Memory Engine 0.1.3.rev28 - Scan Type Selection and PS5 Scan Panel Refinement

### Fixed

- Fixed the post-scan Scan Type selector state reported during rev27 live testing. `CanSelectScanType` depends on `IsScanningMemory`, but the `IsScanningMemory` setter notified scan commands, `CanConfigureScanPause`, `CanSelectScanValueType`, and Scan Option state without notifying `CanSelectScanType`. WPF could therefore leave the selector visually disabled after First/Next Scan completed until unrelated input caused another binding reevaluation. Rev28 explicitly raises `PropertyChanged` for `CanSelectScanType` whenever scanning enters or leaves the active state.
- Audited the other `IsScanningMemory`-dependent Scan-panel properties while fixing the stale selector. Existing command-state refresh, pause configuration, Value Type locking, Scan Option state refresh, Saved Addresses timer coordination, and scan progress behavior remain in their established paths; no additional stale derived property was found that required a separate notification.

### Added

- Added optional Plugin API `2.8.0` contract `IMemoryScanOptionPresentation` plus neutral `MemoryScanOptionPresentationKind`. A plugin-owned Scan Option may now request the normal `ChoiceList` presentation or a generic two-state `Toggle` presentation without exposing platform ids or control-specific branches to WPF.
- Toggle presentation maps the UI's checked and unchecked states back to two existing stable `MemoryScanOptionChoice` ids and carries an independent user-facing toggle label. Scan execution continues to receive the same `MemoryScanOptions` id/choice pairs as before; the presentation layer does not create new backend semantics.
- Added optional Plugin API `2.8.0` contract `IMemoryScanOptionApplicability`. A plugin can now declare whether a Scan Option is relevant for a selected Core-owned Scan Type and First/Next stage in addition to the existing Value Type applicability rule.
- Extended plugin discovery validation for the new optional metadata. The host rejects unknown presentation kinds, toggle declarations without a label, missing/duplicate checked/unchecked mappings, applicability implementations that throw, and option declarations that do not apply to any current Core Scan Type.
- Added an automated verification check for generic Scan Option presentation/applicability metadata, increasing the verification suite from 39 to **40 checks**.

### Changed

- Advanced the host from `0.1.3.rev27` to `0.1.3.rev28` with feature title `Scan Type Selection and PS5 Scan Panel Refinement` through centralized `AppInfo`.
- Advanced Plugin API from `2.7.0` to `2.8.0` for the optional Scan Option presentation/applicability contracts. Existing same-major plugins targeting older 2.x API minors remain compatible; the Mock plugin remains `1.0.0.rev4` targeting API `2.0.0`.
- Advanced the PS5 plugin from `0.1.0.rev18` to `0.1.0.rev19` and its target Plugin API from `2.7.0` to `2.8.0` because its Scan Option definitions now consume the new optional presentation/applicability contracts.
- PS5 Endianness still uses the existing `standard.endianness` option and `little`/`big` choice ids, but now declares Toggle presentation with label **Little-endian byte order**. Checked selects Little Endian, unchecked selects Big Endian, and the existing Little Endian default therefore renders checked on a new scan.
- Generic toggle Scan Options are rendered in the Scan panel's compact checkbox group alongside **Pause target while scanning**. Choice-list Scan Options continue to use the existing label + ComboBox presentation.
- Scan Option visibility now combines the existing `SupportsValueType(...)` result with optional Core Scan Type/stage applicability. Unsupported option rows collapse instead of remaining as irrelevant disabled controls.
- PS5 **Floating-point rounding** now declares applicability only for Core **Exact Value**. Together with its existing Float/Double Value Type restriction, this means the control appears only for Exact Value Float/Double scans and is hidden for Fuzzy, Between, changed-value, delta, integer, and other unrelated scan combinations.
- PS5 Floating-point rounding no longer locks after First Scan. It changes only the current Exact Value comparison mode, not candidate address shape/width, so it remains configurable when a user switches back to Exact Value before a later Next Scan. PS5 Endianness and Alignment retain their existing post-First-Scan locks.
- Reworked the Scan operand layout so two-input Scan Types such as **Between** render **Value 1** and **Value 2** side-by-side with equal available width. One-input Scan Types continue to use one full-width Value editor, while zero-input Scan Types continue to hide operand input entirely.
- Updated the PS5 plugin metadata description to describe semantic native Scan Type acceleration with Core fallback rather than the older Exact-only wording.

### Preserved

- Core remains the owner of all 13 standard Scan Type identities, stage rules, operand counts, comparison semantics, previous-value behavior, and Value Type compatibility filtering.
- The rev26 `INativeScanTypeMappingProvider` / `NativeScanTypeMapping` semantic mapping contract and the PS5 Core-to-ps5debug-NG mapping table are unchanged. No ps5debug-NG compare id, TurboScan flag, request body, snapshot behavior, or native fallback boundary changes in rev28.
- Disk-backed scan-result generation/storage, the 50,000-row UI preview boundary, massive native stream support, Previous values, Unknown Initial snapshot records, and scan-session lifecycle are unchanged.
- Value Type ids, parsing, signed/unsigned ranges, default signed **4 Bytes**/Int32 selection, live input filtering, whitespace handling, Address validation, Endianness choice ids, Alignment choices, and Floating-point rounding choice ids are unchanged.
- Saved Addresses refresh/Frozen scheduling, queued removal, user-intent I/O coordination, concurrent PS5 writer behavior, confirmation dialogs, themes, connection behavior, process selection, and process pause/resume are unchanged.

### Documentation

- Updated README to describe rev28, Plugin API `2.8.0`, PS5 plugin `0.1.0.rev19`, the generic Scan Option presentation/applicability model, post-scan Scan Type selection, side-by-side multi-value operands, the Little-endian checkbox, and Exact-only Floating-point rounding visibility.
- Updated Plugin SDK, scanner, early-architecture, main-workspace, native-mapping, and PS5 plugin/native-control documentation where the new generic Scan Option boundary or current version identity is relevant.
- Recorded the user-run rev27 automated verification result of **39/39 PASS** in the rev27 verification document.
- Added `docs/testing/APP_0.1.3_REV28_VERIFICATION.md` covering build/startup, the post-scan selector regression, Core stage filtering, multi-value layout, Endianness toggle semantics, Floating-point rounding applicability/editability, generic API compatibility, native/Core regression, and theme/layout checks.

### Verification

- Re-read the supplied rev27 documentation set and inventoried all source/XAML/projects before changing code, with special review of scanner lifecycle, Plugin SDK Scan Option contracts, current PS5 option definitions, native mapping/fallback behavior, and WPF Scan-panel bindings.
- Confirmed the reported Between workflow is not a Core Scan Type restriction: the stale state is caused by the missing `CanSelectScanType` property notification after `IsScanningMemory` returns to `false`.
- Confirmed the new Scan Option contracts are optional companions rather than new required members on `IMemoryScanOption`, preserving older Plugin API 2.x plugin compatibility.
- Confirmed WPF rendering remains platform-neutral: `MainWindow.xaml` and `PluginViewModel` branch on generic presentation/applicability metadata and contain no PS5 plugin id, platform name, ps5debug-NG compare id, or Endianness-specific selection logic.
- Added deterministic metadata assertions for PS5 toggle checked/unchecked mappings, the Exact-only Floating-point applicability rule, and the intentionally unlocked post-First-Scan rounding option.
- Completed the rev28 static pre-package audit across the complete 228-file tree: all 128 C# files passed structural checks, all 14 XAML/project XML files and all 3 JSON files parsed successfully, all relative links across 81 Markdown files resolve, the verification runner contains 40 registered checks, and no `bin`, `obj`, or `.vs` artifacts are present.
- Compared rev28 against the supplied rev27 baseline with no removed files. Core scanner code, PS5 transport/native mapping/native scanner/native refiner code, and Mock plugin production code remain byte-identical; host source contains no PS5 protocol/native compare constants or platform-specific scan-option branches.
- Reviewed the new PluginHost metadata validation under the repository-wide nullable/warnings-as-errors policy and made toggle choice validation flow-analysis-safe before packaging.
- Per project workflow, native .NET build/runtime verification remains user-run on Windows; the release package is statically reviewed and includes a dedicated rev28 verification checklist.

## TeeKay87's Memory Engine 0.1.3.rev27 - Native Scan Mapping Compile Fix

### Fixed

- Fixed four `CS8604` warnings promoted to build errors by the repository-wide `TreatWarningsAsErrors=true` policy. `FirstScanAsync` and `NextScanAsync` pass the values produced by `TryPrepareScan(...)` into the in-memory and disk-backed scan execution paths; the method already guaranteed that `inputValues` was non-null whenever it returned `true`, but that success-state guarantee was not expressed to nullable-flow analysis.
- Added `[NotNullWhen(true)]` to the `TryPrepareScan(...)` `inputValues` out parameter so the compiler understands the existing success contract without adding null-forgiving operators at each call site or weakening nullable analysis.
- Fixed the resulting WPF/XAML designer cascade in which `System.Object`, `ProportionalGridSplitter`, and the `TextBoxInputFilter.Mode` / `TextBoxInputFilter.MemoryValueType` attached properties were reported as unresolved after the App assembly failed to build. No XAML type, attached-property, namespace, control, or assembly-name change was required.

### Changed

- Advanced the host from `0.1.3.rev26` to `0.1.3.rev27` with feature title `Native Scan Mapping Compile Fix` through centralized `AppInfo`.
- Updated README current-status/version text to identify rev27 as the buildable correction layer over the rev26 native Scan Type mapping implementation.
- Recorded the failed Windows compile attempt against rev26 in its verification document and added a dedicated rev27 verification checklist.

### Preserved

- Core's 13 standard Scan Types, their semantics, stage/input metadata, and disk-backed previous-value behavior are unchanged from rev26.
- Plugin API remains `2.7.0`; the generic `INativeScanTypeMappingProvider` / `NativeScanTypeMapping` contract is unchanged.
- PS5 plugin remains `0.1.0.rev18` targeting Plugin API `2.7.0`; its Core-to-ps5debug-NG mapping table, TurboScan request construction, Unknown Initial snapshot/include-zero path, and Core fallback boundaries are unchanged.
- Mock plugin remains `1.0.0.rev4` targeting Plugin API `2.0.0`.
- `MainWindow.xaml`, `TextBoxInputFilter`, `ProportionalGridSplitter`, Core scanner code, Plugin SDK production code, PS5 production code, Mock production code, themes, Saved Addresses coordination, and confirmation-dialog behavior are not changed by this compile correction.

### Verification

- Re-inspected the supplied rev26 package and traced every error shown in the Windows Error List. The four `CS8604` entries all originate from the same nullable-flow omission at `TryPrepareScan(...)`; the XAML errors are downstream assembly-resolution failures rather than independent XAML defects.
- Confirmed `MainWindow.xaml`, `TextBoxInputFilter.cs`, and `ProportionalGridSplitter.cs` are byte-identical to the previous scanner revision where those UI types were already established, so the correction intentionally avoids unrelated XAML/UI edits.
- Confirmed `TryPrepareScan(...)` assigns a parsed input list before every successful return and returns `false` on all validation failures before callers can use that value. `[NotNullWhen(true)]` therefore documents existing runtime behavior rather than changing it.
- Static release review passed all 25 package-preparation checks: expected diff scope, version/API/plugin boundaries, unchanged Core/Plugin SDK/plugin/test trees, unchanged XAML/input/splitter sources, XML/XAML/project parsing, JSON parsing, C# structural checks across all 125 source files, relative-link resolution across all 80 Markdown files, and release-tree cleanliness.
- Per project workflow, native .NET compilation/runtime verification remains user-run on Windows. Rev27 should be rebuilt before proceeding to the rev26/rev27 scanner verification checklist.

## TeeKay87's Memory Engine 0.1.3.rev26 - Native Scan Type Mapping and Complete Core Scan Types

### Added

- Added Core Scan Types **Fuzzy Value** and **Unknown Initial Low Value**, completing the 13-mode standard Core Scan Type catalog used by every platform plugin.
- Added optional Plugin API `2.7.0` contract `INativeScanTypeMappingProvider` and the neutral `NativeScanTypeMapping` model. A plugin can now declare that a Core-owned Scan Type has a semantically equivalent native implementation for specific First/Next stages and Value Types without exposing platform-specific compare ids to Core or WPF.
- Added Core `NativeScanTypeResolver` as the shared decision point for native delegation. When a connected session publishes a mapping table, only declared semantic matches are offered to the native scanner; missing mappings route directly to the shared Core scanner. Older compatible 2.x plugins that do not publish the optional table retain the established try-native-then-fallback behavior.
- Added the PS5 plugin's explicit Core-to-ps5debug-NG native mapping table. Plugin-owned native ids remain opaque to Core and currently identify either a ps5debug-NG `compareType` or the dedicated snapshot mode used for Unknown Initial Value.
- Added native PS5 **Unknown Initial Value** through TurboScan snapshot mode with `TS_SNAPSHOT_INCLUDE_ZEROS`, preserving Core's definition that zero-valued candidates are included instead of mapping directly to ps5debug-NG `compareType 11`, which excludes zeros.
- Added native PS5 protocol coverage for **Between** First Scan with two operands, zero-operand **Changed Value** refinement, and Unknown Initial snapshot creation followed by native refinement.
- Added verification coverage for the generic mapping contract, Core Fuzzy/Unknown Initial Low semantics, PS5 mapping metadata, native multi/zero-operand payloads, and the PS5 snapshot mapping. The verification executable now contains **39 checks**.
- Added `docs/architecture/NATIVE_SCAN_TYPE_MAPPING.md` describing the reusable Core-to-plugin native acceleration contract for current and future plugins.
- Added `docs/testing/APP_0.1.3_REV26_VERIFICATION.md` with host, protocol, fallback, and live PS5 checks for the completed Scan Type work.

### Changed

- Advanced the host from `0.1.3.rev25` to `0.1.3.rev26` with feature title `Native Scan Type Mapping and Complete Core Scan Types` through centralized `AppInfo`.
- Advanced Plugin API from `2.6.0` to `2.7.0` for the new optional native Scan Type mapping contract/model. Existing compatible 2.x plugins remain valid under the established same-major/older-minor compatibility rule.
- Advanced the PS5 plugin from `0.1.0.rev17` to `0.1.0.rev18` and moved its declared target API from `2.4.0` to `2.7.0` because the plugin now consumes and exposes the new mapping contract. The Mock plugin remains `1.0.0.rev4` targeting API `2.0.0`.
- Generalized the PS5 native TurboScan request builder from Exact Value-only payloads to mapped Core predicates with zero, one, or two comparison operands. `compareType`, payload length, and native mode now come from the plugin-owned mapping definition rather than an Exact Value constant in the scanner path.
- PS5 native First/Next delegation now includes semantically equivalent Exact Value, Fuzzy Value, Bigger Than, Smaller Than, Between, Increased Value, Decreased Value, Changed Value, Unchanged Value, Unknown Initial Value snapshot, and integer Unknown Initial Low Value operations when the connected TurboScan capabilities and current option/value shape permit them.
- **Increased By** and **Decreased By** deliberately remain Core fallback on PS5. ps5debug-NG `compareType 6/8` performs target-width arithmetic that can wrap at integer boundaries, while Core uses directional, non-wrapping delta semantics; similarly named operations are therefore not declared equivalent.
- PS5 **Unknown Initial Low Value** maps natively only for integer Value Types. ps5debug-NG's floating implementation compares absolute magnitude, which is not equivalent to Core's positive-nonzero `0 < value <= upperLimit` definition, so Float/Double use Core fallback.
- Big-endian native routing is now predicate-aware. Endian-independent integer equality/inequality may stay native, while native comparisons that interpret multi-byte numeric magnitude use Core fallback; the Unknown Initial snapshot is endian-independent because it stores raw slots and explicitly includes zeros.
- Strict Float/Double host-side filtering is now explicitly limited to **Exact Value**. Fuzzy Value and other mapped floating predicates retain their own Core/native semantics instead of being reinterpreted as Exact Value during GET.
- Ordered and delta Core Scan Types now require a fixed-width Value Type as well as `IMemoryValueComparer`, preventing variable-width Array of Bytes from being offered merely because its reusable implementation class carries the companion interface.
- Standard Float/Double ordered comparison now treats NaN as unordered rather than relying on .NET `CompareTo` ordering, keeping relational Core predicates aligned with conventional/native comparison semantics.

### Core Scan Type Semantics

- **Exact Value**: First + Next, one operand.
- **Fuzzy Value**: First + Next, one Float/Double operand, absolute difference strictly below `1.0`.
- **Bigger Than** / **Smaller Than**: First + Next, one ordered operand.
- **Between**: First + Next, two inclusive ordered bounds.
- **Unknown Initial Value**: First only, no operand, includes every fixed-width candidate including zero.
- **Unknown Initial Low Value**: First only, one positive upper limit, retains positive nonzero numeric candidates up to and including that limit.
- **Increased Value** / **Decreased Value** / **Changed Value** / **Unchanged Value**: Next only, no operand, compare against the previous committed scan value.
- **Increased By** / **Decreased By**: Next only, one non-negative delta operand, use Core directional delta semantics.

### Preserved

- Core remains the authoritative owner of standard Scan Type identity and semantics. Plugins still own Value Types, platform-specific Scan Options, transport/native code, and the decision to advertise only genuinely equivalent native accelerators.
- The native mapping table is optional and does not make native support mandatory. Any unsupported runtime shape, unavailable server capability, native resource refusal, incompatible endianness, or semantic mismatch continues through the established `NotSupportedException`/Core fallback path.
- Disk-backed result generations and previous-value storage remain unchanged. The existing address-plus-current-value record already provides the previous snapshot required by Core Next Scan predicates.
- Existing rev20-rev25 Saved Address coordination, textbox filtering, confirmation dialogs, scan cancellation safety, progress handling, and verified PS5 read/write behavior are not changed by this revision.
- Application version remains `0.1.3` because the completed Scan Type expansion and rev24-rev26 input/scanner work still require Windows/live-target verification before the project version may advance.

### Documentation

- Updated README and scanner/Plugin SDK/workspace documentation to describe the 13 Core Scan Types and the new semantic native mapping model.
- Updated the PS5 plugin documentation with the exact Core-to-ps5debug-NG mapping/fallback table, snapshot-based Unknown Initial Value handling, new plugin/API versions, and runtime option restrictions.
- Updated scan-type research notes to distinguish Core-owned semantics from backend-native acceleration opportunities.

### Static Verification

- Reused the full rev25 documentation/source audit completed before implementation and re-reviewed every changed scan/native/test path before packaging.
- Compared current ps5debug-NG `compareType 0..12`, TurboScan resident refinement, and snapshot/include-zero behavior against each Core predicate before declaring native equivalence.
- Confirmed no platform-specific compare values or PS5 protocol details were introduced into Core or WPF; the public mapping contract contains only stable Core ids, opaque plugin-native ids, stages, and Value Type restrictions.
- Confirmed the Mock plugin production code remains unchanged and continues to demonstrate backward compatibility with Plugin API `2.0.0`.
- Confirmed the final source tree contains 13 Core Scan Type registrations, 11 PS5 semantic native mappings, and 39 registered verification checks.
- Parsed all 14 XAML/project XML files and all 3 JSON files successfully, checked delimiter/string/comment structure across all 125 C# files, and verified every relative Markdown link across all 79 Markdown files resolves.
- Confirmed all newly added/modified mapping files contain the required namespace imports, no `bin`, `obj`, or `.vs` build-artifact directories are present, and application/API/plugin version constants consistently identify host `0.1.3.rev26`, Plugin API `2.7.0`, and PS5 plugin `0.1.0.rev18`.
- Compared the final tree against the supplied rev25 baseline: no files were removed; changes are limited to the documented Core/Plugin SDK/PS5/WPF scan-routing paths, verification code, centralized metadata, and relevant documentation. Mock plugin production files remain byte-identical to rev25.
- Per project workflow, .NET compilation/runtime and live-target verification remain user-run on Windows.

## TeeKay87's Memory Engine 0.1.3.rev25 - Core Scan Types and Whitespace Filtering Fix

### Added

- Added the Core-owned `MemoryScanTypeCatalog` as the single production catalog for standard scan predicates used by every platform plugin.
- Added Core Scan Types **Bigger Than**, **Smaller Than**, **Between**, **Unknown Initial Value**, **Increased Value**, **Decreased Value**, **Changed Value**, **Unchanged Value**, **Increased By**, and **Decreased By** alongside the existing **Exact Value** behavior.
- Added optional Plugin SDK contract `IMemoryValueComparer` for Value Types that support ordered and exact delta comparisons. The reusable standard integer, Float, and Double Value Types implement it; Array of Bytes deliberately does not expose numeric ordering.
- Added stage-aware/dynamic Scan input layout in WPF: zero-input predicates hide Value, normal predicates render one Value editor, and Between renders Value 1 plus Value 2.
- Added scan-type input validation through `IMemoryScanType.TryValidateInputValues(...)`, including inclusive Between-bound validation.
- Added verification coverage for the Core catalog and its direct/previous-value/delta comparison semantics.
- Added `docs/testing/APP_0.1.3_REV25_VERIFICATION.md` covering the whitespace correction, universal Scan Type UI, shared/PS5 fallback behavior, snapshot scans, and regression checks.

### Changed

- Advanced the host from `0.1.3.rev24` to `0.1.3.rev25` with feature title `Core Scan Types and Whitespace Filtering Fix` through centralized `AppInfo`.
- Advanced Plugin API from `2.5.0` to `2.6.0` because `IMemoryValueComparer` and Scan Type input-validation support are new optional public contracts. Existing 2.x plugins remain compatible under the same-major/older-minor rule.
- The production host no longer reads or validates plugin-provided `SupportedScanTypes` or `DefaultScanTypeId`. Those Plugin API 2.x members remain only as obsolete compatibility members; new plugins should not implement them.
- Advanced PS5 plugin from `0.1.0.rev16` to `0.1.0.rev17` and Mock plugin from `1.0.0.rev3` to `1.0.0.rev4`. Their production `SupportedScanTypes`/`DefaultScanTypeId` implementations were removed because standard Scan Type availability is now supplied by Core and filtered against the selected Value Type.
- PS5 plugin `0.1.0.rev17` native TurboScan remains an **Exact Value** accelerator. Selecting another Core Scan Type causes the existing native path to return `NotSupportedException`, after which the host uses the established shared Core scanner/refiner without changing the requested predicate semantics.
- The disk-backed result format remains unchanged: each record already stores address plus current-value bytes, and those stored bytes are reused as the previous snapshot for Changed/Unchanged/Increased/Decreased/By refinement. No large in-memory previous-value dictionary was introduced.
- `TextBoxInputFilter` now also handles `PreviewKeyDown` for Space. Numeric Value editors and Saved Address Address therefore reject Space immediately, while Array of Bytes still accepts spaces because its Value Type policy treats whitespace as a valid separator. Paste continues to use the same candidate validation.

### Preserved

- Plugin ownership of Value Types, native/backend mappings, connection behavior, and platform-specific Scan Options remains unchanged.
- Exact Value native PS5 behavior, floating-point Strict versus ps5debug-NG tolerance handling, disk-backed generations, 50,000-row presentation cap, cancellation safety, Saved Address I/O coordination, and themed dialogs are unchanged.
- Final parser/range validation remains authoritative after live textbox filtering.
- Application version remains `0.1.3` because the new Scan Type subsystem and rev24/rev25 input-validation work still require Windows/live-target verification. Per project versioning rules, a version increment is deferred until this functionality is complete and verified.

### Documentation

- Updated README, Plugin SDK architecture, Memory Scanner architecture, Main Workspace, input-validation documentation, scan-type research notes, and PS5/Mock plugin documentation for the Core-owned Scan Type model.
- Documented the compatibility status of the obsolete Plugin API 2.x scan-type declaration members.
- Added focused rev25 runtime/live-target verification steps.

### Static Verification Preparation

- Re-read the complete supplied rev24 Markdown documentation set before code changes and reviewed the full source inventory before modifying ownership or scan behavior.
- Confirmed the rev13 disk-backed result record already stores each candidate's current bytes, so snapshot-based Next Scans can use those bytes directly as `previousValue` without changing the on-disk format.
- Confirmed PS5 native request validation already rejects non-Exact scan ids with `NotSupportedException`, providing the required safe fallback path for the new Core predicates.
- Confirmed the only platform-plugin source changes are removal of the now-obsolete standard Scan Type declarations plus the independent plugin revision metadata updates; no PS5 transport/protocol implementation changed.
- Confirmed no new platform-specific branches were added to WPF/Core for PS5, PC, Xbox 360, or other future plugins.
- Per project workflow, .NET compilation/runtime and live-target verification remain user-run on Windows.

## TeeKay87's Memory Engine 0.1.3.rev24 - Memory Input Validation

### Added

- Added the reusable WPF `TextBoxInputFilter` attached behavior under `TeeKay87.MemoryEngine.App/Input`. The behavior validates the complete candidate text before accepting ordinary text composition or clipboard paste, so fields can reject impossible syntax without duplicating event-handler logic in individual windows.
- Added host-owned `UnsignedInteger` and `HexAddress` filter modes. `UnsignedInteger` permits only decimal digits while retaining an empty edit state; `HexAddress` permits an optional `0x`/`0X` prefix plus at most 16 hexadecimal digits, matching the application's neutral 64-bit address representation.
- Added optional Plugin SDK contract `IMemoryValueInputPolicy`. A concrete `IMemoryValueType` may implement this companion interface to describe whether the current editor text is still a potentially valid intermediate state while the user is typing.
- Added live-input policies to all reusable standard Value Types. Signed and unsigned integers support their existing decimal/`0x` syntax, Float/Double support invariant decimal/scientific notation and normal intermediate edit states, and Array of Bytes supports the separators and hexadecimal token forms already understood by its final parser.
- Added automated verification for the standard Value Type live-input policies, increasing the dependency-free verification executable from 34 to 35 checks.
- Added `docs/ui/TEXT_INPUT_VALIDATION.md` documenting the two-layer validation model, reusable host filter, optional plugin contract, standard policies, current consumers, deliberately unrestricted fields, and extension rules.
- Added `docs/testing/APP_0.1.3_REV24_VERIFICATION.md` with focused Windows/runtime checks for typed input, paste, incomplete edit states, final range validation, Value Type changes, Saved Address address edits, Settings interval fields, and existing regression coverage.

### Changed

- Advanced the host application from `0.1.3.rev23` to `0.1.3.rev24` with feature title `Memory Input Validation` through centralized `AppInfo`.
- Advanced the Plugin API from `2.4.0` to `2.5.0` because `IMemoryValueInputPolicy` is a new public optional Plugin SDK contract. Existing compatible 2.0-2.4 plugins remain accepted by the existing same-major/older-minor compatibility rule.
- The Scan-panel **Value** TextBox now follows the selected plugin Value Type's optional live-input policy. Standard integer types reject impossible letters/symbols while retaining decimal and `0x` hexadecimal entry; Float/Double retain valid decimal/scientific editing states; Array of Bytes accepts only syntax that can still form the parser's supported hexadecimal sequence.
- Each Saved Address **Value** TextBox now uses the row's selected plugin Value Type for the same live-input policy. Changing the row's Type changes the editor policy with it; the host does not infer syntax from type ids or display names.
- Saved Address **Address** now rejects non-hexadecimal typing and paste immediately, preserves the existing optional `0x` prefix, and limits the editable address payload to 16 hexadecimal digits before the established final `ulong` parser runs.
- The Settings **Value refresh (ms)** and **Frozen write (ms)** TextBoxes now accept decimal digits only while editing. Their existing 50-10,000 ms validation remains authoritative when Settings is saved.
- Text paste now follows exactly the same candidate validation as ordinary typing in all fields using the reusable filter, preventing clipboard input from bypassing the live restrictions.

### Preserved

- Live filtering is deliberately not the final safety boundary. Scan and Saved Address Value operations still call the selected plugin-owned `IMemoryValueType.TryParse(...)`; Saved Address Address still uses the existing 64-bit hexadecimal parser; Settings still performs its established numeric range validation before persistence.
- The live policy accepts temporary editor states that may not yet be valid commit values when they can still become valid through continued editing, such as an empty field, `-`, `0x`, `1.`, and `1e-` where appropriate. Committing an incomplete state is still rejected by the final parser.
- A custom plugin Value Type is not required to implement `IMemoryValueInputPolicy`. Without the optional policy, its Value TextBox remains freely editable and its existing `TryParse(...)` implementation remains the definitive validation path. The host therefore does not hardcode PS5, PC, Xbox 360, or other future platform-specific value grammar.
- Plugin-defined connection fields remain unrestricted by the host because their grammar is platform/backend owned. The plugin continues to perform authoritative validation for host names, ports, credentials, or other connection-specific values. Description and storage-path fields also remain intentionally free-form.
- PS5 plugin remains `0.1.0.rev16` targeting Plugin API `2.4.0`; it does not require a revision because no PS5-specific code or metadata changed. Its standard Value Types gain the new live-input behavior through the shared Plugin SDK definitions. Mock plugin remains `1.0.0.rev3` targeting Plugin API `2.0.0` for the same reason.
- Core scanning/storage code, ps5debug-NG transport/native scan mappings, Saved Address I/O coordination from rev20-rev22, rev23 confirmation-dialog behavior, theme JSON, and all existing Value Type ids/ranges/byte encoding semantics are unchanged.

### Documentation

- Updated README with rev24 as the current build, Plugin API `2.5.0`, the layered input-validation behavior, current field coverage, future-plugin policy behavior, and the 35-check verification count.
- Updated Plugin SDK architecture documentation with the optional `IMemoryValueInputPolicy` contract, compatibility rationale, and rule that final parsing remains authoritative.
- Updated Memory Scanner, Saved Addresses, and Main Workspace documentation to describe type-aware Value filtering, strict hexadecimal Address editing, paste handling, numeric Settings filtering, and the unrestricted-field boundary.
- Added dedicated UI documentation for reusable textbox validation and a rev24 verification checklist.

### Static Verification Preparation

- Re-read the supplied rev23 README, CHANGELOG, and complete `docs/` Markdown inventory before modifying code, then reviewed the full source inventory to identify existing final parsers and avoid duplicating validation logic.
- Confirmed Scan Value and Saved Address Value already used plugin-owned `IMemoryValueType.TryParse(...)` at commit time; rev24 adds only the live-editing layer in front of those existing authoritative parsers.
- Confirmed Saved Address Address already used the host's hexadecimal `ulong` parser and Settings intervals already enforced 50-10,000 ms at Save; those existing final checks remain intact behind the new input filters.
- Confirmed the reusable WPF filter evaluates the complete candidate text for both keyboard text composition and paste, includes the current selection replacement, and leaves deletion/backspace available so users can restructure intermediate input.
- Confirmed the new Value Type policy is optional rather than being added to `IMemoryValueType` itself, preserving binary/source expectations for older compatible 2.x plugins and custom Value Type implementations.
- Confirmed no Core, PS5 plugin, Mock plugin, ps5debug-NG protocol, theme JSON, confirmation-dialog, or Saved Address I/O-coordination implementation files were changed by this revision.
- Per project workflow, .NET compilation/runtime and live-target verification remain user-run on Windows; package preparation performs source, structure, documentation, version-consistency, and archive-integrity review without claiming runtime verification.

## TeeKay87's Memory Engine 0.1.3.rev23 - Themed Confirmation Dialog and Value Type Defaults

### Added

- Added a reusable application-owned confirmation-dialog component under `TeeKay87.MemoryEngine.App/Dialogs`. The component is deliberately independent of Saved Addresses, scanning, target transports, and platform plugins so future host workflows can request the same confirmation surface without creating another one-off window.
- Added `ConfirmationDialogOptions` for caller-owned title, message, affirmative button text, cancel button text, semantic tone, and default-button behavior.
- Added `ConfirmationDialogService` as the common owner-modal entry point. The service requires a WPF owner, opens the dialog centered on that owner, and returns a simple affirmative/cancel result while leaving the calling workflow responsible for its business logic.
- Added Information, Warning, and Danger confirmation tones. The tones reuse existing shared brushes and Primary/Danger button styles instead of adding dialog-specific palettes or theme keys.
- Added `docs/ui/CONFIRMATION_DIALOG.md` describing the reusable dialog contract, modality, theme integration, semantic tones, keyboard behavior, current Remove All use, and reuse rules.
- Added `docs/testing/APP_0.1.3_REV23_VERIFICATION.md` with focused Windows/runtime checks for all three bundled themes, modal/keyboard behavior, Remove All regression behavior, Value Type labels/default selection, and existing automated verification.

### Changed

- Advanced the host application from `0.1.3.rev22` to `0.1.3.rev23` with feature title `Themed Confirmation Dialog and Value Type Defaults` through centralized `AppInfo`.
- Replaced the Saved Addresses **Remove All** operating-system `MessageBox` with the reusable host confirmation dialog. The prompt now follows the active Light, Dimmed, or Dark theme and uses the same visual language as the rest of the application.
- The Remove All confirmation now labels its affirmative action **Remove All**, labels the safe action **Cancel**, and uses the Danger semantic role. The destructive action is deliberately not the Enter-key default.
- Standard signed integer Value Type display names are now the compact primary labels `1 Byte`, `2 Bytes`, `4 Bytes`, and `8 Bytes`. The corresponding unsigned definitions remain explicit as `1 Byte (Unsigned)`, `2 Bytes (Unsigned)`, `4 Bytes (Unsigned)`, and `8 Bytes (Unsigned)`.
- Confirmed and regression-checked that both built-in plugins continue to declare signed Int32 (`standard.int32`) as `DefaultValueTypeId`. The default Scan-panel selection is therefore displayed as **4 Bytes** without changing the underlying default type.
- Extended the existing plugin scan-capability verification to assert both built-in Int32 defaults and the new standard display-name convention.

### Preserved

- Remove All business logic and rev22 pending-removal coordination are unchanged. Confirming while Saved Address I/O is active still records the current rows for safe deferred removal, disables their Frozen state, and completes removal automatically at the established all-I/O idle boundary.
- The rev22 user-intent/background-I/O coordination for Disconnect, process refresh, target selection, scan startup/reset, Saved Address edits, Frozen behavior, direct Value writes, and individual removal is unchanged.
- Stable standard Value Type ids are unchanged. `standard.int8`, `standard.int16`, `standard.int32`, `standard.int64`, `standard.uint8`, `standard.uint16`, `standard.uint32`, and `standard.uint64` retain their previous identities.
- Signed/unsigned numeric ranges, decimal/hex parsing, target-endian byte conversion, value widths, default alignments, exact comparison semantics, saved-address interpretation, disk-backed scanner behavior, and ps5debug-NG native type mappings are unchanged. Rev23 changes display metadata, not memory semantics.
- Plugin API remains `2.4.0`; no public plugin contract was added or changed.
- PS5 plugin remains `0.1.0.rev16`; no PS5 transport, TurboScan, concurrent-writer, process-control, or plugin-owned option code was changed.
- Mock plugin remains `1.0.0.rev3`; no Mock target behavior was changed.
- Existing Light, Dimmed, and Dark theme JSON files and their schema remain unchanged. The new dialog consumes the existing shared theme resources.

### Documentation

- Updated README to identify rev23 as the current application build, document the reusable confirmation service, describe the standard signed/unsigned display-name policy and built-in 4 Bytes/Int32 default, document the existing Dialogs source area, and point to the rev23 verification checklist.
- Updated the theme documentation to include the reusable confirmation surface among application-owned modal UI and to require the same Light/Dimmed/Dark regression coverage.
- Updated the main-workspace and Saved Addresses documentation to describe the themed Remove All confirmation while preserving rev22 deferred-removal semantics.
- Updated scanner and Plugin SDK architecture documentation to distinguish compact signed integer display names from the unchanged stable ids and signedness semantics.

### Static Verification Preparation

- Re-read the supplied rev22 README, changelog, and complete `docs/` Markdown inventory before modifying code, then reviewed the full source inventory and the relevant WPF, theme, scanner-definition, plugin-default, and verification paths.
- Confirmed the previous Remove All prompt was the only remaining `MessageBox.Show` call in the supplied codebase and replaced that one path with the reusable service rather than creating parallel confirmation logic.
- Confirmed both built-in plugins already used signed Int32 as their default before rev23. No plugin default or platform-specific scanner mapping needed to change to satisfy the requested **4 Bytes** default.
- Confirmed the dialog XAML uses `x:ClassModifier="internal"` and shared `DynamicResource`/style references, matching the application's existing reusable dialog conventions and avoiding a separate palette.
- Confirmed the new dialog keeps operation-specific text and decisions outside the component; the Remove All caller only asks for a confirmation result and then enters the existing removal code path.
- Confirmed no theme JSON, Core scanner/storage, PS5 plugin, Mock plugin, native protocol, or Saved Address coordination implementation files were changed by this revision.
- Per project workflow, .NET compilation/runtime and live-target verification remain user-run on Windows; the package preparation environment is used for source, structure, consistency, and package-integrity review rather than runtime claims.


## TeeKay87's Memory Engine 0.1.3.rev22 - User Intent I/O Coordination

### Fixed

- Fixed **Disconnect** sometimes requiring a second click when the first attempt coincided with a Saved Addresses refresh or Frozen operation. Transient Saved Address I/O no longer makes Disconnect non-executable; the request reserves the foreground immediately, waits for already-running Saved Address I/O to reach a safe idle boundary, and then continues automatically.
- Fixed the same transient-I/O lost-intent pattern for **Refresh Processes**, **Set Active Target**, **First Scan**, **Next Scan**, **New Scan**, and the retained Raw Memory Read/Write/Safe Write Test diagnostic commands. These actions no longer depend on the user clicking outside the short background refresh/Frozen window.
- Fixed Freeze enable, Address edits, and Value Type edits being rejected when their commit happened to overlap a Saved Address background timer operation. Explicit Saved Address user operations now suppress future timer work and wait for the current background cycle instead of requiring a retry.
- Fixed **Remove All** requiring another attempt when confirmed during active Saved Address I/O. The rows present at confirmation time are now unfrozen and registered for pending removal, then removed automatically when all Saved Address I/O returns to idle.
- Fixed a stale freeze-enable sequence being able to re-enable Frozen after the user had issued a newer explicit Off request. Freeze requests now have a per-row generation/state marker and the latest requested state wins.
- Closed a dispatcher timing edge case where stopping the refresh timer after accepting a foreground action was not sufficient by itself: a timer tick already queued for dispatch could still pass the old operation guard. The foreground reservation is now checked inside the refresh execution guard and both multi-row loops as well as in timer enablement.

### Changed

- Advanced the host application from `0.1.3.rev21` to `0.1.3.rev22` with feature title `User Intent I/O Coordination` through centralized `AppInfo`.
- Generalized the rev20 `_savedAddressUserWriteInProgress` concept into a Saved Address user-operation state used by direct Value writes, Freeze enable, Address commits, and Value Type commits. The generalized state has its own idle signal so target-level foreground transitions can wait for an already-started Saved Address user operation without cancelling it mid-transaction.
- Added a host-owned foreground target-operation reservation. Once accepted, it immediately stops future Saved Address timers, prevents another foreground target transition from stacking on top of it, makes an already-running multi-row timer cycle stop after its current awaited row, waits for Saved Address I/O to become fully idle, and then lets the original user command continue.
- The foreground reservation is deliberately **not** a general FIFO command queue. Real foreground conflicts retain their existing `CanExecute` guards; for example a second scan is not stored for later execution while another scan is active, and Disconnect is not queued behind an active scan. This prevents stale user commands from replaying against a materially different target state.
- First Scan and Next Scan release the foreground reservation immediately after `IsScanningMemory` becomes true. This preserves the verified rev18-rev20 scan interaction: ordinary Saved Address refresh remains paused, while Frozen writes may continue during a scan through `IConcurrentMemoryWriter` when **Pause target while scanning** is Off.
- Unfreeze remains immediate. Freeze enable is coordinated as an explicit user operation and waits for background Saved Address I/O; its initial refresh/capture/write must complete before repeated Frozen state is enabled. The Frozen checkbox and context-menu label now follow the latest requested Frozen state while that initial operation is pending, giving immediate visual acknowledgement of the click.
- Address and Value Type commits now wait for background Saved Address I/O. Existing target identity/value-type validation remains in force, and changing Address/Type still disables Frozen before the new location/interpretation is used.
- Individual queued removal from rev21 is retained. Remove All now reuses that same pending-removal set and all-I/O idle boundary rather than creating a second removal mechanism.

### Documentation

- Added `docs/testing/APP_0.1.3_REV22_VERIFICATION.md` with focused runtime/live-target verification for target-command deferral, scan startup, Freeze latest-intent semantics, Address/Type edits, Remove All, rev20/rev21 regressions, real foreground conflict guards, and PS5 protocol safety.
- Updated README, Saved Addresses architecture, main-workspace documentation, and early-development architecture to describe the generalized user-intent/background-I/O boundary and the intentional distinction between transient background collisions and genuine foreground conflicts.

### Preserved

- Plugin API remains `2.4.0`.
- PS5 plugin remains `0.1.0.rev16`; no ps5debug-NG transport, TurboScan, or concurrent-writer code was changed.
- Mock plugin remains `1.0.0.rev3`.
- Core scanner/storage code, disk-backed generation semantics, 50,000-row UI preview ceiling, legacy 2,000,000 list-materialization safety ceiling, plugin-owned scan definitions/options, settings persistence, Saved Address interval defaults, rev20 active-editor and target-write-generation protection, rev21 individual queued removal, and target identity/memory-map safety checks remain unchanged.
- Plugin Reload, application shutdown, plugin selection, settings actions, copy commands, scan-result Save Address, and other UI paths that were not blocked by transient Saved Address I/O are not routed through the new foreground reservation.

### Static Verification Preparation

- Re-read the full supplied rev21 documentation inventory and reviewed the complete code inventory before modifying the host/WPF coordination paths.
- Audited every `CanExecute` guard and visible command/event surface for transient Saved Address-I/O dependencies. The affected target-level commands now coordinate at execution time instead of disabling solely for that background condition.
- Traced the foreground reservation against background refresh, Frozen enforcement, concurrent Saved Address Value writes, pending row removal, target-state transitions, scan startup, scan cancellation, disconnect/disposal, and WPF command-state invalidation.
- Confirmed an accepted foreground operation stops future timer scheduling **and** is checked by the refresh/Frozen execution guards, preventing an already-dispatched tick from starting new work after user intent has priority.
- Confirmed an already-started Saved Address explicit operation can finish while a later foreground transition is pending; new Saved Address user operations are blocked after that foreground reservation is established.
- Confirmed First/Next Scan release only the temporary startup reservation after setting scan-active state, so the existing PS5 concurrent Frozen writer can continue during non-paused scans.
- Confirmed rev22 changes no Core, Plugin SDK, PS5 plugin, Mock plugin, protocol, or scan-storage files. The only XAML change binds the Saved Address Frozen checkbox to the row's latest requested Frozen state so queued intent is reflected immediately.
- Per project workflow, no .NET build/runtime attempt is made in the preparation environment; Windows compilation/runtime and live PS5 verification remain user-run.

## TeeKay87's Memory Engine 0.1.3.rev21 - Queued Saved Address Removal

### Fixed

- Fixed individual Saved Address removal being rejected whenever a refresh, Frozen write, or direct Saved Address Value write happened to be active. The row-level **Remove** button and **Remove address** context-menu action now retain the user's request and complete it automatically at the next safe Saved Address idle boundary.
- Fixed the removal path being able to require repeated user attempts at short refresh/Frozen intervals. A removal request now has priority over future timer work: the affected row is unfrozen immediately, both Saved Address timers are suppressed while a removal is pending, and an already-running multi-row refresh/Frozen cycle stops after the currently awaited row.
- Fixed a pending removal being able to lose priority to the multi-step freeze-enable flow. If removal is requested while the initial live refresh or immediate freeze write is in flight, the freeze sequence observes the pending state before it can capture/re-enable Frozen for another cycle.
- Fixed an in-flight refresh completion being able to replace the row's display after the user had already requested removal. Pending rows discard that refresh result and are removed as soon as the owning I/O state returns to idle.

### Changed

- Advanced the host application from `0.1.3.rev20` to `0.1.3.rev21` with feature title `Queued Saved Address Removal` through centralized `AppInfo`.
- Added a host-owned pending-removal set for individual Saved Address rows. Duplicate Remove clicks for the same row are coalesced, while separate rows can be queued together if the user removes more than one during the same busy period.
- Pending removals are processed only when **all** Saved Address I/O is idle. The existing rev20 background-idle transition drains the queue after refresh/Frozen work completes, while the explicit user-write completion path performs the same check after direct Value writes. This preserves ordering when background I/O and a manual write overlap.
- An already-started target read/write is still allowed to finish normally; rev21 does not introduce transport cancellation into Saved Addresses. This preserves the established protocol-safety rule that in-flight target transactions are completed rather than abandoned mid-frame.
- The existing **Remove All** confirmation and busy-state behavior is intentionally unchanged. Rev21 changes only individual row removal.

### Documentation

- Added `docs/testing/APP_0.1.3_REV21_VERIFICATION.md` with focused runtime checks for queued removal during value refresh, Frozen enforcement, direct user writes, freeze enablement, multiple queued rows, and idle removal.
- Updated README, Saved Addresses architecture, and main-workspace documentation to describe the queued individual-removal lifecycle and its interaction with rev20 I/O coordination.

### Preserved

- Plugin API remains `2.4.0`.
- PS5 plugin remains `0.1.0.rev16`; no ps5debug-NG transport, TurboScan, or concurrent-writer code was changed.
- Mock plugin remains `1.0.0.rev3`.
- Core scanner/storage code, disk-backed generation semantics, 50,000-row UI preview ceiling, legacy 2,000,000 list-materialization safety ceiling, scan options, settings persistence, Saved Address refresh/Frozen interval behavior, direct Value-write priority, stale-read generation protection, and rev20 command-state requery behavior remain unchanged.

### Static Verification Preparation

- Re-read the complete project documentation inventory and reviewed the source inventory before changing the Saved Addresses host path.
- Traced individual removal through row commands, `PluginViewModel`, both Saved Address timers, explicit user writes, freeze enablement, disconnect/disposal cleanup, selection state, and command-state invalidation to avoid introducing a second or conflicting synchronization mechanism.
- Confirmed queued removal reuses the existing rev20 Saved Address idle transition rather than changing Plugin SDK or platform transport contracts.
- Confirmed a queued Frozen row is unfrozen before waiting, so no later timer snapshot can start another Frozen write for that row; a write already in flight may complete once but cannot re-arm the row.
- Confirmed multiple queued rows are removed as one idle-boundary batch and the selected-row reference is repaired after removal.
- Per project workflow, no .NET build/runtime attempt is made in the preparation environment; Windows compilation/runtime verification remains user-run.

## TeeKay87's Memory Engine 0.1.3.rev20 - Saved Address I/O Coordination and Scan Command State Fix

### Fixed

- Fixed Scan-panel commands occasionally remaining visually disabled after a completed First/Next Scan. The race occurred when scan state returned to idle while a periodic Saved Address refresh/Frozen operation was still active; WPF cached the false `CanExecute` result and no later command notification was emitted when the background operation finished. Background Saved Address I/O now emits a single dependent-command requery when the final in-flight background operation returns to idle.
- Fixed direct Saved Address Value edits being silently discarded whenever a refresh or Frozen timer tick happened to be active at commit time. Explicit user writes now have their own operation state instead of reusing the background-refresh flag and are coordinated separately from periodic I/O.
- Fixed the Value TextBox being susceptible to source updates while the user was typing. Background refresh and Frozen completions continue updating the row's internal current bytes, but the displayed editor text is not replaced while that Value cell owns keyboard focus.
- Fixed a direct-write/read display race by generalizing the rev19 Frozen-only generation marker into a target-write generation. Both manual writes and Frozen writes advance the marker, so an older in-flight read completion cannot overwrite the display after either kind of successful write.
- Fixed editing a Frozen row being able to race an already-started Frozen write with the old captured value. The user's parsed edit becomes the captured Frozen payload before the manual write is queued; on plugins with `IConcurrentMemoryWriter`, the existing writer serialization guarantees an older already-queued Frozen write completes before the newer manual write, leaving the user's new value authoritative.

### Changed

- Advanced the host application from `0.1.3.rev19` to `0.1.3.rev20` with feature title `Saved Address I/O Coordination and Scan Command State Fix` through centralized `AppInfo`.
- Direct Saved Address Value writes now prefer the existing optional `IConcurrentMemoryWriter` whenever the active plugin provides it. On PS5 this reuses the already verified separate ps5debug-NG writer connection from rev18/rev19; no PS5 protocol code changes are required.
- Plugins without a concurrent writer no longer lose the user's direct Value edit because a periodic Saved Address operation is active. Future timer ticks are stopped while the explicit write is pending, and the host waits for already-running Saved Address background I/O to become idle before using the primary `IMemoryWriter`.
- Explicit direct Value writes temporarily suppress new refresh/Frozen timer ticks and update dependent command state at operation start/end. If a timer cycle is already traversing multiple Saved Address rows, it stops after its current awaited row as soon as the pending user write takes priority. Periodic background ticks still do not invalidate commands at start, preserving rev18's no-blinking UI behavior.
- Manual write failures now always update the Saved Addresses panel status. For a Frozen row, the newly requested frozen bytes are retained so the normal Frozen scheduler can retry them on the next interval.

### Documentation

- Added `docs/testing/APP_0.1.3_REV20_VERIFICATION.md` with focused live verification for scan-command recovery, active Value editor protection, direct-write priority, Frozen-value replacement, and stale-refresh protection.
- Added the rev19 runtime follow-up that led to this revision without rewriting rev19's historical implementation record.
- Updated README, Saved Addresses architecture, and main-workspace documentation for the new direct-write/background-I/O coordination behavior.
- Corrected an obsolete README sentence that still described the Saved Addresses toolbar as removing only the selected row; the implemented toolbar action is confirmed **Remove All**, while per-row Remove remains available.

### Preserved

- Plugin API remains `2.4.0`.
- PS5 plugin remains `0.1.0.rev16`; its source and separate concurrent writer implementation are unchanged.
- Mock plugin remains `1.0.0.rev3`.
- Core scanner/storage code, ps5debug-NG TurboScan protocol paths, disk-backed generation semantics, 50,000-row UI preview ceiling, legacy 2,000,000 list-materialization safety ceiling, scan options, settings persistence, and Saved Address interval defaults remain unchanged.

### Static Verification Preparation

- Re-read all project Markdown documentation and reviewed the complete rev19 source inventory before changing host/WPF Saved Address paths.
- Reviewed every use of the Saved Address refresh, Frozen-write, target-transition, command-state, and direct Value-write state flags after introducing explicit user-write coordination.
- Confirmed background timer start still does not raise global command-state notifications, while transition back to background idle now does, preventing stale disabled command state without restoring the rev17 blink behavior.
- Confirmed the active Value editor cannot receive timer-driven `ValueText` replacement while focused and that successful manual/Frozen writes share one target-write generation for stale-read rejection.
- Per project workflow, no .NET build/runtime attempt is made in the preparation environment; Windows compilation/runtime verification remains user-run.

## TeeKay87's Memory Engine 0.1.3.rev19 - Frozen Value Reliability and Header Alignment

### Fixed

- Fixed the **Scan Results** section title alignment by giving it the same vertical centering used by the **Saved Addresses** title in the adjacent toolbar-style header.
- Fixed repeated Frozen writes silently disabling a Saved Address after a transient transport/write exception. The row now remains Frozen, reports the failure, and retries the same captured Frozen bytes on the next configured interval.
- Fixed ordinary Saved Addresses refresh reads being able to suppress Frozen ticks on plugins that already expose an independent `IConcurrentMemoryWriter`. The concurrent writer is now preferred for repeated Frozen writes whenever it is available, not only while scanning.
- Fixed a read/write display race where a Saved Addresses refresh started before a successful Frozen write could complete afterward and overwrite the row display with the older read result. Successful Frozen writes now advance a per-row generation marker; a stale in-flight read completion is discarded if that generation changed.

### Changed

- Advanced the host application from `0.1.3.rev18` to `0.1.3.rev19` with feature title `Frozen Value Reliability and Header Alignment` through centralized `AppInfo`.
- Changed the default **Frozen write interval** from `500 ms` to `100 ms`, matching the current Cheat Engine default reviewed for its address-list freeze behavior. The supported `50-10,000 ms` range is unchanged, and an explicitly persisted user value is preserved.
- Successful initial and repeated Frozen writes now update the Saved Address row's current/display bytes to the value that was actually written, while the separately captured Frozen bytes remain the authoritative repeated-write payload.
- Outside scans, plugins that expose Plugin API `2.4.0` `IConcurrentMemoryWriter` now use that independent writer for Frozen enforcement so ordinary display refresh can continue without serializing every freeze tick behind the primary reader. Plugins without a concurrent writer retain safe serialized `IMemoryWriter` fallback behavior outside scans.
- Scan behavior remains unchanged: Saved Addresses display refresh pauses during First/Next Scan; Frozen writes are suppressed when **Pause target while scanning** is enabled; when Pause is disabled, in-scan Frozen writes require `IConcurrentMemoryWriter`.
- Updated Settings explanatory text to show the distinct 500 ms refresh and 100 ms Frozen defaults.

### Research and Documentation

- Reviewed Cheat Engine's current open-source address-list freeze behavior before changing Memory Engine. The reviewed source separates update/freeze intervals, defaults freeze to 100 ms, captures a dedicated frozen value, repeatedly reapplies that value, and does not silently clear the active/frozen state on an individual repeated-write failure.
- Added `docs/research/CHEAT_ENGINE_FREEZE_BEHAVIOR.md` with the source paths and the specific behavioral principles adapted for Memory Engine.
- Added `docs/testing/APP_0.1.3_REV19_VERIFICATION.md` with focused header/Frozen/settings/scan-interaction verification.
- Updated current README, Saved Addresses architecture, Settings persistence, workspace, and early-development documentation. The early roadmap now records the completed disk-backed massive-result, Saved Addresses, and freeze milestones rather than describing those implemented features as future work.
- Added a runtime follow-up section to the historical rev18 verification document recording the two issues that led to rev19 without rewriting rev18's historically correct 500 ms default.

### Preserved

- Plugin API remains `2.4.0`.
- PS5 plugin remains `0.1.0.rev16`; its separate concurrent ps5debug-NG writer implementation is reused without plugin-code changes.
- Mock plugin remains `1.0.0.rev3`.
- Core scanner, disk-backed scan-result format/lifecycle, native TurboScan implementation, 50,000-row UI preview ceiling, legacy 2,000,000 list-materialization safety ceiling, plugin settings, Raw Memory diagnostic code, Saved Address row/edit/remove workflow, and export placeholders are unchanged.

### Static Verification Preparation

- Re-read the project documentation and reviewed the complete rev18 codebase before changing the affected host/WPF paths.
- Reviewed the rev18 Frozen/read schedulers for obsolete or redundant paths after introducing concurrent-writer preference and transient-failure retry semantics.
- Confirmed the separate captured Frozen bytes remain the authoritative repeated-write value and manual Value edits on a Frozen row still replace that captured value after a successful write.
- Confirmed `IConcurrentMemoryWriter` already supplies the required safe PS5 background channel, so rev19 does not change Plugin SDK or PS5 protocol code.
- Per project workflow, no .NET build/runtime attempt is made in the preparation environment; Windows compilation/runtime and live target verification remain user-run.

## TeeKay87's Memory Engine 0.1.3.rev18 - Saved Addresses Workflow Refinement

### Added

- Added a dedicated **Remove** button at the far right of every Saved Addresses row. The existing row context-menu removal path is preserved and both actions remove only that row.
- Added a toolbar-level **Remove All** action for Saved Addresses. It requires explicit Yes/No confirmation before clearing the table and disables each Frozen row before collection removal.
- Added a separate application-level **Frozen write interval** setting stored as `frozenWriteIntervalMilliseconds` in the existing shared `settings.json`. It defaults to `500 ms`, accepts `50-10,000 ms`, persists across launches, and applies immediately to already-loaded plugin workspaces after Settings is saved.
- Added Plugin API `2.4.0` optional `IConcurrentMemoryWriter`. The contract extends `IMemoryWriter` and explicitly identifies a writer that is safe for host use while another long-running operation owns the session's primary transport.
- Added PS5 `Ps5ConcurrentMemoryWriter`. It opens a second ps5debug-NG connection lazily on the first in-scan Frozen write and serializes repeated writes on that secondary connection, preventing `CMD_PROC_WRITE` traffic from interleaving with TurboScan START/COUNT/GET traffic on the verified primary command stream.
- Added Scan Results **Export...** toolbar placement matching the Saved Addresses panel. Export remains disabled until the shared export subsystem is implemented.
- Added `docs/testing/APP_0.1.3_REV18_VERIFICATION.md` covering the focused UI, settings, refresh, Frozen-write, scan-interaction, version, and regression checks for this revision.

### Changed

- Advanced the host application from `0.1.3.rev17` to `0.1.3.rev18` with feature title `Saved Addresses Workflow Refinement` through centralized `AppInfo`.
- Advanced Plugin API from `2.3.0` to `2.4.0` because `IConcurrentMemoryWriter` is a new public optional Plugin SDK contract. Existing 2.0-2.3 plugins remain compatible under the existing same-major/older-minor rule.
- Advanced the PS5 plugin independently from `0.1.0.rev15` to `0.1.0.rev16` and its targeted Plugin API from `2.3.0` to `2.4.0` because it now exposes the concurrent writer implementation. Mock remains `1.0.0.rev3` targeting `2.0.0`.
- Changed Saved Addresses background scheduling from one combined refresh/freeze timer to two independent schedules. **Value refresh** uses `savedAddressesUpdateIntervalMilliseconds`; **Frozen writes** use `frozenWriteIntervalMilliseconds`.
- Changed scan interaction so ordinary Saved Addresses value refresh is stopped for the complete First Scan/Next Scan operation. Frozen writes are treated separately: when **Pause target while scanning** is enabled they are suppressed while the target is paused; when Pause is disabled they continue at the configured Frozen interval through `IConcurrentMemoryWriter` when the plugin provides that service.
- Changed routine Saved Addresses timer state handling so periodic refresh/freeze ticks no longer raise global Connect/Disconnect/process/raw-memory command `CanExecuteChanged` notifications. This removes the visible enabled/disabled flashing of unrelated UI buttons on every refresh while preserving `CanExecute` safety checks when commands are actually evaluated.
- Changed new Saved Address Description values from the stored literal `No description` to an empty string. The Description cell remains directly editable and its tooltip communicates the editing affordance.
- Changed the Scan Results footer back to a direct count presentation: `Showing <displayed> results of <total>.` The footer no longer displays the `Results remain temporary...` sentence.
- Changed the Saved Addresses toolbar Remove action from selected-row removal to confirmed **Remove All**. Single-row removal remains available through each row's new Remove button and context menu.
- Changed Frozen status/tooltip text so it refers to the dedicated Frozen write interval rather than the Saved Addresses value-refresh interval.
- Changed Settings explanatory text to document both independent intervals and their scan behavior.
- Changed the visible workspace so Raw Memory Read and Raw Memory Write development inspectors are no longer rendered. Their ViewModel properties, commands, validation, Core/plugin memory-access services, Safe Write Test logic, and protocol implementations are intentionally retained in code for development/regression use.

### Removed

- Removed the obsolete selected-row `RemoveSavedAddressCommand` toolbar path now that the toolbar action is Remove All and each row owns its own Remove command.
- Removed the small disabled `Export` text from the lower-right Scan Results footer; the future export affordance now exists only as the disabled toolbar **Export...** button.
- Removed Frozen writes from the ordinary Saved Addresses value-refresh code path. Refresh now reads/updates display values only; the dedicated Frozen scheduler is the sole repeated-write path.
- Removed the literal `No description` value from newly created Saved Address rows.
- Removed Raw Memory Read/Write inspector XAML from the visible main workspace without removing the underlying diagnostic implementation.
- Removed the Scan Results **Change value** menu entry because it only loaded the now-hidden Raw Memory Write diagnostic surface; its backing command and diagnostic implementation remain in code.

### Safety and Preserved Behavior

- During a scan with **Pause target while scanning** Off, PS5 Frozen writes never share the native scanner's primary ps5debug-NG TCP stream. The secondary writer connection is created only when required and is disposed with the target session.
- During a scan with Pause enabled, Frozen writes stop before the suspended target is scanned and resume after the scan finishes and the target has been resumed.
- Plugins that do not expose `IConcurrentMemoryWriter` are never forced to perform unsafe overlapping writes; an in-scan Frozen write is deferred while the row remains Frozen.
- Outside scans, existing generic `IMemoryWriter` behavior remains unchanged. Saved Address target identity and readable/writable region checks remain in force.
- The disk-backed scan-result format, 50,000-row UI preview ceiling, legacy 2,000,000 list-materialization safety ceiling, TurboScan survivor semantics, plugin-owned Value Type/Scan Type definitions, connection persistence, and scan-result storage lifecycle are unchanged.

### Verification Preparation

- Re-read the supplied rev17 README, CHANGELOG, architecture/plugin/UI documentation, verification history, and reviewed the complete source inventory before making changes.
- Reviewed every changed Saved Addresses scheduler/cell/toolbar/settings path and removed the obsolete selected-row toolbar command after the new Remove All path replaced it.
- Verified structurally that Raw Memory Read/Write backing properties, commands, and methods remain in source while their XAML inspectors are absent.
- Verified the Frozen scan path uses `IConcurrentMemoryWriter` only while scanning, ordinary Saved Address refresh remains disabled during scanning, and the PS5 concurrent writer uses an independent lazily opened `Ps5DebugClient`.
- Updated the existing plugin-settings preservation check so plugin writes must also preserve `frozenWriteIntervalMilliseconds`; the verification executable remains focused and does not add a long-running stress test.
- Per project workflow, no .NET build/runtime attempt is made in the preparation environment. Windows compilation/runtime and live PS5 verification remain user-run steps documented under `docs/testing/APP_0.1.3_REV18_VERIFICATION.md`.

## TeeKay87's Memory Engine 0.1.3.rev17 - Saved Addresses

### Added

- Added the first functional **Saved Addresses** data model and host workflow. A displayed Scan Result can now be added by double-clicking its row or by choosing **Save Address** from the Scan Result context menu. Saving the same target/address/Value Type identity again selects the existing Saved Address instead of silently adding another duplicate.
- Added the interactive Saved Addresses columns **Frozen**, **Description**, **Address**, **Type**, and **Value**. The previous placeholder-only layout and duplicate Active/Frozen concept are replaced by a single Frozen state and directly editable row fields.
- Added generic Saved Address current-value refresh through the active session's neutral `IMemoryReader`. Values are converted back to display text through the row's plugin-owned `IMemoryValueType`, preserving the platform-plugin ownership of concrete Value Types instead of adding a Core/WPF master catalog.
- Added direct Saved Address value editing through neutral `IMemoryWriter`. Edited text is parsed by the selected plugin Value Type, validated against the writable memory map when available, written to the Active Target, and reflected in the row after a successful write. A successful edit on a Frozen row also becomes the new frozen value.
- Added direct hexadecimal Address editing. Invalid input is rejected and the last valid display value is restored; changing the address disables an active freeze before the new location is used.
- Added direct Type selection populated from the same Value Types declared by the active plugin. Changing type updates width/alignment semantics, disables freeze, and refreshes the new representation.
- Added Saved Address freeze/repeated-write behavior. Enabling **Frozen** first refreshes the live target value, captures those bytes, performs an immediate write, then reapplies the frozen bytes at the Saved Addresses update interval. Disabling Frozen stops the repeated writes.
- Added a Saved Address row context menu with **Freeze/Unfreeze**, **Copy address**, **Copy value**, and **Remove address**, plus a toolbar Remove action for the selected row. Export remains intentionally disabled until shared export infrastructure exists.
- Added application-level **Saved Addresses update interval** to Settings. It defaults to `500 ms`, accepts `50-10,000 ms`, is stored in the existing shared `%LocalAppData%\TeeKay87\MemoryEngine\settings.json`, applies to both refresh and Frozen writes, and is propagated immediately to all loaded plugin workspaces after Settings is saved.
- Added target-safety handling for Saved Addresses. Each row retains the process id/name from the Active Target that produced it; rows remain visible but automatic/direct memory operations are rejected while another process is active. Disconnecting stops scheduled Saved Address work and disables active freezes so repeated writes cannot silently resume after a later connection.
- Added `docs/architecture/SAVED_ADDRESSES.md` describing row ownership, plugin-owned Value Types, read/write/freeze flow, update scheduling, target safety, scan-session independence, and current non-goals.
- Added `docs/testing/APP_0.1.3_REV17_VERIFICATION.md` with a concise verification plan covering both Save Address gestures, direct cell interaction, freeze, New Scan independence, target safety, and Settings persistence.

### Changed

- Advanced the host application from `0.1.3.rev16` to `0.1.3.rev17` with feature title `Saved Addresses` through the centralized `AppInfo` source. Plugin API remains `2.3.0`; PlayStation 5 remains `0.1.0.rev15`; In-Memory Test Target remains `1.0.0.rev3`.
- Changed the Scan Result context menu to include **Save Address** and connected row double-click to the same action. Existing Copy address, Copy value, and Int32-only diagnostic Change value behavior is preserved.
- Changed Saved Addresses from a disabled UI foundation to a live collection owned by each loaded `PluginViewModel`. The collection is intentionally independent from scan-session reset state, so First Scan, Next Scan, and New Scan do not clear user-saved rows. Rev17 does not yet serialize the rows across application launches or Plugin Reload; that belongs to the later project/cheat persistence model.
- Changed the application Settings document usage so plugin-scoped writes are also verified to preserve the new `savedAddressesUpdateIntervalMilliseconds` application value. The existing Core-owned shared settings/preservation model from rev16 remains unchanged.
- Updated README and workspace/architecture documentation to describe the implemented Saved Addresses workflow, current project-lifetime boundary, generic Value Type/read/write ownership, Frozen behavior, and update-interval setting.

### Removed

- Removed the obsolete Saved Addresses **Active** column. **Frozen** is now the single user-controlled repeated-write state.
- Removed the disabled placeholder **Add Address** and **Edit** controls. Addresses now enter the table from Scan Results, while Description, Address, Type, and Value are edited directly in their cells.
- Removed the placeholder **Notes** column from the current table. Notes remain a future project/cheat metadata feature rather than a non-functional column.
- Removed documentation that described Saved Addresses, value editing, and freeze as entirely unimplemented future work. Export, pointer work, Memory Viewer/disassembly navigation, hotkeys/groups, address expressions, and cross-launch project persistence remain future features.

### Safety and Preserved Behavior

- Saved Address automatic refresh/freeze work is serialized in the host and intentionally skips conflicting connection/process/map/raw-memory/scan operations rather than intentionally overlapping independent target protocol transactions. The background timer is explicitly stopped for the full duration of First Scan and Next Scan and restarts only after scanning returns to idle; Disconnect, Active Target changes, and New Scan native-session reset likewise suppress it while those target-state transitions are in progress.
- A Frozen row is never automatically written while another process is Active Target. Active freezes are explicitly disabled when the target connection ends, and removing a row clears its Frozen state before collection removal so a timer snapshot cannot reapply a removed entry.
- Memory-map-capable targets must contain the complete Saved Address value range inside the appropriate readable/writable region before the host sends refresh/write/freeze operations.
- Changing a Saved Address's Address or Type disables its existing freeze before using the changed location/interpretation.
- Scanner code, Core disk-backed result format/lifecycle, Plugin SDK contracts, PS5 plugin source, Mock plugin source, TurboScan behavior, Pause target behavior, the `50,000` UI-preview ceiling, and the legacy `2,000,000` in-memory/list materialization ceiling are not changed by rev17.
- Plugin-owned concrete Value Types remain authoritative. Saved Addresses consumes the plugin's existing definitions rather than reintroducing a hardcoded Core list.

### Verification

- Re-read README, CHANGELOG, all Markdown documentation under `docs/`, and reviewed the complete rev16 source/test/resource inventory before implementing Saved Addresses.
- Traced the existing Scan Result model, scan-reset lifecycle, Active Target/process transitions, `IMemoryReader`, `IMemoryWriter`, plugin-owned `IMemoryValueType`, application settings persistence, DataGrid styles, and command infrastructure before adding the new paths.
- Reviewed the changed code for obsolete/dead/redundant paths. The old Active/placeholder table path is removed; generic scan-result Change value remains intentionally separate because it is still the existing Raw Memory Write Int32 diagnostic, while Saved Addresses now provides the general plugin-defined value editor.
- Reviewed target-lifetime safety so Saved Addresses survive scan resets without allowing automatic writes to a different Active Target; disconnect explicitly disables active freezes. The final static review also confirmed that the Saved Addresses timer is stopped while First/Next Scan is active and remains suppressed during Disconnect, Set Active Target, and New Scan target/session transitions.
- Extended the existing plugin-settings deterministic check so plugin writes must preserve the new application-level Saved Addresses interval. The top-level verification count remains 34.
- Per the established project workflow, no .NET build/runtime attempt is made in the preparation environment. Windows build/runtime verification is left to the user; the focused checklist is recorded in `docs/testing/APP_0.1.3_REV17_VERIFICATION.md`.

## TeeKay87's Memory Engine 0.1.3.rev16 - Plugin Settings Persistence and Platform Selector

### Added

- Added Plugin API `2.3.0` host-managed settings contracts: `IPluginSettings` exposes plugin-scoped `TryGetString`, `TrySetString`, and `TryRemove` operations, while the optional `IPluginSettingsConsumer` contract lets Core attach that scoped service to plugins that need persisted state. Plugins never receive the settings file path and cannot address another plugin's namespace through the injected service.
- Added Core `JsonSettingsStore` as the shared persistence implementation for the existing application settings document and the new plugin namespaces. It preserves application-level values and plugin-level values in one document, keeps plugin data under stable `PluginMetadata.Id` namespaces, uses case-insensitive JSON property lookup, and retains atomic temporary-file/replace writes.
- Added Core PluginHost settings injection. After basic metadata/API validation and before connection declarations are read, a plugin implementing `IPluginSettingsConsumer` receives its own settings scope. This allows remembered plugin values to influence generic connection-field defaults without placing platform-specific persistence logic in WPF.
- Added PS5 persistence keys `connection.host` and `connection.port`. PS5 plugin `0.1.0.rev15` restores valid remembered values as the host/port connection defaults and saves the normalized endpoint only after a ps5debug-NG connection has completed successfully.
- Added deterministic plugin-settings coverage to the verification executable. The new top-level check validates persistence, plugin-id namespace isolation, preservation of application settings, removal, and reload behavior; existing PS5 metadata/handshake and PluginHost discovery checks now also cover remembered defaults, successful-connection writes, and pre-declaration settings injection. The verification executable now contains 34 top-level checks.
- Added `docs/architecture/PLUGIN_SETTINGS_PERSISTENCE.md` describing the plugin/Core ownership boundary, JSON layout, injection lifecycle, failure semantics, PS5 usage, and extension rules.
- Added `docs/testing/APP_0.1.3_REV16_VERIFICATION.md` with focused automated and manual verification steps for plugin settings, remembered PS5 connection fields, and the simplified Platform selector.

### Changed

- Advanced the host application from `0.1.3.rev15` to `0.1.3.rev16` with feature title `Plugin Settings Persistence and Platform Selector` through the centralized `AppInfo` source.
- Advanced Plugin API from `2.2.0` to `2.3.0` because the public SDK now exposes optional plugin-settings contracts. The existing same-major/host-minor-or-newer compatibility rule is unchanged, so the Mock plugin targeting API `2.0.0` remains compatible.
- Advanced the PlayStation 5 plugin from `0.1.0.rev14` to `0.1.0.rev15` and its targeted Plugin API from `2.2.0` to `2.3.0` because it now consumes the host-managed settings service. Scanner/protocol behavior from rev15 is otherwise unchanged.
- Refactored App `ApplicationSettingsStore` to delegate theme and Scan Results Storage Location values to Core `JsonSettingsStore`. The existing top-level JSON keys and `%LocalAppData%\TeeKay87\MemoryEngine\settings.json` location are preserved, while application-level saves can no longer discard plugin namespaces. `MainWindowViewModel` receives the public Core store rather than exposing the App-internal wrapper through its public constructor, preserving the existing accessibility boundary.
- Changed the Platform ComboBox to display only `PluginViewModel.Name`, which maps directly to `PluginMetadata.Name`/the built-in plugin's `xxPluginInfo.Name`. Backend is still shown in the expandable Plugin details section.
- Updated README, Plugin SDK architecture, scanner/workspace architecture, PS5 plugin/connection documentation, and current protocol/process-control documentation for host `0.1.3.rev16`, PS5 plugin `0.1.0.rev15`, and Plugin API `2.3.0`. The workspace documentation was also brought forward from its obsolete rev9 in-memory-result description to the live-verified disk-backed generation model.
- Updated the rev15 verification record with the completed physical-PS5 results: 33/33 deterministic checks passed; a signed-byte Exact Value First Scan for `50` committed `10,953,954` results; a same-value Next Scan refined the complete set to `8,796,420`; and First/Next Scan both passed with **Pause target while scanning** enabled.

### Removed

- Removed the obsolete App-only `ApplicationSettings` DTO. The previous serializer would not know about future plugin namespaces and could drop those properties when rewriting the shared file; the Core JSON document store now preserves the complete settings document.
- Removed the obsolete `PluginViewModel.SelectorDisplay` (`Name · Backend`) presentation path. No other UI or logic used that property after the Platform selector moved to the plugin name directly.

### Preserved

- The settings file remains `%LocalAppData%\TeeKay87\MemoryEngine\settings.json`; existing `themeId` and `scanResultsStorageLocation` values remain valid without migration.
- PS5 connection fields remain plugin-declared and WPF remains unaware of PS5-specific IP/port semantics. The persistence decision and keys belong to the PS5 plugin.
- Failed PS5 connection attempts do not replace remembered successful host/port values. A persistence failure does not invalidate an otherwise successful target connection.
- Plugin Backend metadata remains available and visible under Plugin details; only the Platform dropdown presentation changed.
- Plugin API `2.2.0` native streaming contracts remain unchanged and continue to back the disk-backed massive-result scanner.
- The rev13-rev15 disk-backed binary result format, generation commit semantics, complete-set Next Scan behavior, 50,000-row UI preview ceiling, TurboScan survivor retrieval correction, Pause target behavior, and legacy `2,000,000` list-materialization ceiling are unchanged.
- In-Memory Test Target remains `1.0.0.rev3` targeting Plugin API `2.0.0`.

### Verification

- Re-read README, CHANGELOG, all Markdown documentation under `docs/`, and reviewed the complete rev15 source/test/resource inventory before making the revision.
- Traced every existing `ApplicationSettingsStore`, theme preference, Settings window, PluginHost discovery, plugin connection-definition, PS5 connection, and Platform-selector call path before replacing or extending it.
- Reviewed the new settings path for obsolete/duplicate implementations: the App-only `ApplicationSettings` DTO and `SelectorDisplay` path were removed; ThemePreferenceStore remains active as the theme-specific failure-handling wrapper; legacy plugin versions remain intentionally supported through the existing API compatibility rule.
- Reviewed all new/modified C# files for required `using` directives, nullable flow, plugin namespace isolation, and successful-connection persistence semantics.
- Native .NET/WPF build/runtime execution is intentionally left to the Windows development machine.

## TeeKay87's Memory Engine 0.1.3.rev15 - TurboScan Survivor Retrieval Fix

### Fixed

- Fixed live PS5 TurboScan Next Scan failures where the target had already reported an address as a resident survivor through `CMD_PROC_TURBOSCAN_COUNT`, but the client aborted while reading `CMD_PROC_TURBOSCAN_GET` because the returned current-value payload did not equal the comparison value a second time.
- Changed PS5 TurboScan GET handling so START/COUNT remains authoritative for resident survivor membership. Integer, Array-of-Bytes, and ps5debug-NG-tolerance floating-point survivors are no longer rejected by a redundant client-side Exact Value comparison during GET retrieval.
- Preserved the existing Strict Float/Double behavior. ps5debug-NG's native Float/Double Exact Value comparison uses relative tolerance, so Strict First Scan still exact-filters the returned floating-point records before host commit, while Strict Next Scan continues to close the native resident session and use shared Core disk-backed refinement.
- Removed the obsolete generic TurboScan value revalidation path and its now-unused fuzzy floating-point helper methods from `Ps5DebugClient`; the remaining strict floating-point helper now expresses the only GET-side predicate check that is still required.
- Updated the Settings description for Scan Results Storage Location. It now states that complete disk-backed result sets are retained under the managed storage root while the results table keeps only a bounded in-memory preview, replacing the obsolete rev9-era text that said scanner candidates were still kept in memory.

### Changed

- Advanced the host application from `0.1.3.rev14` to `0.1.3.rev15` with feature title `TurboScan Survivor Retrieval Fix` through the centralized `AppInfo` source.
- Advanced the PlayStation 5 plugin from `0.1.0.rev13` to `0.1.0.rev14` because the plugin's TurboScan result-retrieval behavior changed. Plugin API remains `2.2.0`.
- Extended the existing PS5 native exact-value refinement verification scenario so TurboScan COUNT selects a survivor using one comparison value while the subsequent GET returns a deliberately different current-value payload. The verification now requires the client to preserve the server-selected survivor and the returned current-value bytes instead of throwing a mismatched-value exception.
- Extended `Ps5ProtocolTestServer` with an optional independent native-refinement GET current-value payload so the survivor-membership/GET-payload distinction can be verified without adding a separate test harness.
- Updated current scanner, Plugin SDK, PS5 protocol, plugin, README, and revision-verification documentation to describe the corrected survivor retrieval semantics and the new PS5 plugin revision.

### Preserved

- The rev13 disk-backed binary result format, application/scan-session storage lifecycle, generation commit semantics, complete-set Next Scan model, and 50,000-row UI preview ceiling are unchanged.
- Core still validates native stream framing, value width, mapped-region containment, alignment, ascending address order, duplicate addresses, monotonic source progress, stream completeness, and previous-candidate membership.
- The legacy list/in-memory APIs retain the historical `2,000,000` safety ceiling; the disk-backed streaming path remains independent of that limit.
- Plugin API remains `2.2.0`.
- In-Memory Test Target remains `1.0.0.rev3`.
- PS5 Endianness, Alignment, Floating-point rounding, Pause target while scanning, process-control, cancellation, connection, memory-read/write, and protocol-framing behavior are otherwise unchanged.

## TeeKay87's Memory Engine 0.1.3.rev14 - Native Stream Enumerator Cancellation Compile Fix

### Fixed

- Fixed the rev13 verification-project build failure `CS8425` in `SyntheticNativeValueScanResultStream.ReadBatchesAsync`. The synthetic async iterator accepts a `CancellationToken` and is consumed through `await foreach`, so its cancellation-token parameter now carries `[EnumeratorCancellation]` as required by the compiler when warnings are promoted to errors.
- Added the required `System.Runtime.CompilerServices` import to the verification program for `EnumeratorCancellationAttribute`.
- The `System.Object` error reported by the WPF XAML designer is treated as a downstream solution-build/designer symptom unless it remains after the verification-project compiler error is removed. No unrelated `MainWindow.xaml` change is included in this revision.

### Changed

- Advanced the host application from `0.1.3.rev13` to `0.1.3.rev14` with feature title `Native Stream Enumerator Cancellation Compile Fix` through the centralized `AppInfo` source.
- Updated the rev13 verification record with the actual Windows build result and explicitly left rev13 unverified.
- Added `docs/testing/APP_0.1.3_REV14_VERIFICATION.md` describing the narrow build correction and the checks to perform on the Windows development machine.
- Updated current-host documentation to identify `0.1.3.rev14` while retaining PS5 plugin `0.1.0.rev13` and Plugin API `2.2.0` because no plugin or public-contract code changed.

### Preserved

- The complete rev13 disk-backed massive-result implementation is unchanged: bounded native result streaming, compact binary result generations, complete-set Next Scan refinement, the 50,000-row WPF preview ceiling, legacy list fallback behavior, and generation-safe commit/cancellation semantics remain intact.
- Plugin API remains `2.2.0`.
- PlayStation 5 plugin remains `0.1.0.rev13` with no source/protocol changes.
- In-Memory Test Target remains `1.0.0.rev3`.
- The historical `2,000,000` safety ceiling remains only on legacy list/in-memory materialization paths and is not reintroduced into the disk-backed streaming path.
- No `MainWindow.xaml`, storage-format, scanner, TurboScan, settings, theme, progress-dialog, or plugin-owned scan-option behavior is changed by this compile-only revision.

### Verification

- Re-read README, CHANGELOG, all Markdown documentation under `docs/`, and reviewed the complete rev13 source/test/resource inventory before making the correction.
- Reviewed every async iterator in Core, PS5, and Tests that accepts a `CancellationToken`. All production iterators already use `[EnumeratorCancellation]`; the synthetic verification stream was the only missing occurrence.
- Reviewed the rev13 streaming/storage code paths for duplicate or obsolete alternatives introduced by the new disk-backed architecture. The obsolete metadata-only commit path removed in rev13 remains absent, while legacy list scanning remains intentionally retained as a compatibility fallback rather than dead code.
- Confirmed the rev14 source change outside documentation/version metadata is limited to the missing async-iterator cancellation annotation/import in the verification program.
- Native .NET/WPF build/runtime execution is intentionally left to the Windows development machine.

## TeeKay87's Memory Engine 0.1.3.rev13 - Disk-Backed Massive Scan Results

### Added

- Added a versioned binary scan-result record format for temporary scan-session data. Each committed result file stores a fixed header followed by compact address/current-value records, avoiding millions of JSON objects and keeping sequential write/read overhead bounded.
- Added generation-based disk result commits inside the existing host-owned scan-session lifecycle. First Scan publishes generation 1; each successful Next Scan publishes a new generation and only then replaces the active metadata reference. An incomplete, cancelled, or failed replacement generation cannot displace the previous committed result set.
- Added `IScanResultWriter` and `IScanResultSet` abstractions in Core for bounded sequential result writes, batched reads, result counts, stored value width/alignment, and neutral address-batch enumeration.
- Added disk-backed Core First Scan and Next Scan paths. First Scan writes every matching candidate to the active scan-result writer while retaining only the bounded UI preview. Next Scan reads and refines the entire committed result file rather than the rows materialized for the WPF table.
- Added optional streaming native-scan contracts to Plugin SDK: `INativeValueScanStreamProvider`, `INativeValueScanResultStream`, `INativeValueScanStreamRefiner`, and `INativeValueScanCandidateSource`, plus neutral `NativeValueScanResultBatch` payloads. These contracts allow plugins to deliver very large target-side result sets in bounded batches instead of returning one `IReadOnlyList` containing every result.
- Added PS5 TurboScan streaming support. TurboScan GET records are fetched and normalized in bounded batches and can be written directly into the host disk-backed result store without constructing a multi-million-result managed list first.
- Added streaming resident TurboScan refinement support for compatible PS5 Next Scans. The host supplies the complete disk-backed candidate source identity/count, while ps5debug-NG keeps its compatible resident survivor set target-side and returns refined survivors in batches.
- Added three automated regression checks for the massive-result implementation: generation-commit replacement safety, Next Scan survival of a candidate outside the 50,000-row UI preview, and a synthetic 2,000,001-result native stream that proves the disk-backed path exceeds the legacy two-million in-memory limit while retaining a bounded preview.

### Changed

- Advanced the host application from `0.1.3.rev12` to `0.1.3.rev13` with feature title `Disk-Backed Massive Scan Results` through the centralized `AppInfo` source.
- Advanced Plugin API from `2.1.0` to `2.2.0` because rev13 adds optional public streaming scan contracts. The compatibility rule remains same-major/host-minor-or-newer, so the existing Mock plugin targeting API `2.0.0` remains compatible.
- Advanced the PlayStation 5 plugin from `0.1.0.rev12` to `0.1.0.rev13` and its targeted Plugin API to `2.2.0` because its TurboScan implementation now exposes the streaming First/Next Scan services.
- Separated **total/stored result count** from **materialized UI result count**. `MemoryScanExecutionResult` now carries the true total independently from the preview list, and scan progress uses 64-bit result counts.
- The WPF Scan Results table continues to materialize at most the first `50,000` results, but the status/header reports the complete result count and explicitly indicates when only the first 50,000 are displayed.
- The reusable modal operation-progress dialog introduced by the storage foundation is now used for large native result transfers/commits. It reports stored/received counts, blocks conflicting host interaction, and links optional dialog cancellation to the active scan cancellation token.
- The PS5 plugin's former `2,000,000` check is now retained only on the legacy list-materialization compatibility API. The new streaming TurboScan path does not reject a valid result set merely because its count exceeds two million.
- Core's established `MemoryScanner.MaximumResultCount = 2,000,000` remains in the legacy in-memory/list fallback path as a safety boundary. It is no longer the data limit for the new disk-backed streaming/shared paths.

### Removed

- Removed the rev9 metadata-only `BeginWriting()` / `Commit(...)` scan-session path and the now-obsolete `ScanResultCommitInfo` type. Rev13 has a real disk-backed result format, so a session can now reach `Committed` only by publishing a flushed and validated result-file generation. This removes the contradictory state where metadata could claim `Committed` even though no usable result file existed.

### Safety and Correctness

- Scan-result files remain temporary application-session implementation state. They are still bound to unique application-session and scan-session GUIDs and are never adopted from stale directories by filename or recency.
- Result publication is transactional at the generation level: a writer flushes and validates its binary file before scan metadata is updated to reference that generation. If publication fails, the new file is removed when possible and the previous committed generation remains authoritative.
- A cancelled or failed **First Scan** still terminates/invalidates its scan-storage session. A cancelled or failed **Next Scan** leaves the previous committed generation active, allowing the user to retry refinement without losing the last valid result set.
- Native streamed results are validated for value width, address ordering, duplicate addresses, alignment, readable mapped regions, and monotonic source-progress counts before being committed.
- Disk-backed native Next Scan cross-checks streamed survivor addresses against the complete previous committed result set so a plugin cannot silently introduce candidates that were not part of the prior host scan session.
- The PS5 resident survivor count must match the host's committed candidate count before native refinement is allowed. Incompatible state closes the resident session and falls back to shared Core refinement.
- Strict Float/Double semantics remain unchanged: ps5debug-NG's native relative `1e-6` behavior is used only when selected. Strict refinement falls back to Core, and host-side strict filtering can intentionally make the host count differ from the resident native count, causing a safe shared-refinement fallback on Next Scan.
- Disk-backed Next Scan now treats only the streaming native refiner as a native path; a legacy list-only refiner no longer leaves the progress UI indeterminate or labels a shared full-set refinement as native.
- Legacy materialized native results are accepted for disk persistence only when the reported total equals the materialized list, and they are ordered by address before being written so the sequential disk-refinement invariant cannot be violated by an unsorted legacy plugin result list.
- PS5 native refinement now closes any active resident TurboScan session when the requested refinement is unsupported by the native path before Core falls back to shared disk-backed refinement, preventing stale resident state from lingering after an incompatible Next Scan.
- First Scan now determines its native-progress/status path after scan-result storage session creation. If storage is unavailable, a plugin that only provides the new streaming native capability falls back cleanly to the usable legacy/shared path instead of displaying a native indeterminate state for a stream the in-memory fallback cannot consume.

### Preserved

- Plugin-owned concrete Value Types, Scan Types, and Scan Options remain authoritative. Core stores stable plugin-defined ids and neutral records; it does not regain a concrete scan/type catalog.
- PS5 **Pause target while scanning**, **Endianness**, **Alignment**, and **Floating-point rounding** remain plugin-declared controls with their established semantics.
- The existing transaction-safe ps5debug-NG command-channel cancellation rules, process control, memory reads/writes, Safe Write Test, target selection, memory-map handling, tooltip wrapping, Scan-panel overflow behavior, Settings layout, theme persistence, and storage-root configuration are preserved.
- The In-Memory Test Target remains `1.0.0.rev3` targeting Plugin API `2.0.0`.
- Infinite scrolling/paging through millions of rows is not introduced. Rev13 deliberately keeps the 50,000-row presentation ceiling while retaining all records for refinement.

### Verification

- Expanded the verification executable from 30 to 33 top-level checks.
- Added a generation safety check proving an abandoned replacement writer cannot replace a prior committed file and that a later successful generation becomes active.
- Added a deterministic Mock-target check where First Scan stores more than 50,000 matches, a value outside the visible preview is changed, and Next Scan finds that hidden candidate from the complete disk-backed set.
- Added a synthetic native-stream check containing `2,000,001` records, one more than the legacy limit, with a 32-row preview. The check verifies the complete count is written, committed, re-read, and retains the expected final address.
- Extended PS5 capability checks so streaming scan/refinement services are hidden when TurboScan is unavailable and exposed when the server advertises the required capability.
- Performed repository-level static verification of the rev13 source/resource/package structure in the preparation environment. Native .NET/WPF compilation and execution of the 33-check verification executable cannot be run in that environment because no .NET SDK is installed; the Windows build, automated run, and real >2M PS5 live scan therefore remain required before rev13 is declared a verified runtime baseline.

## TeeKay87's Memory Engine 0.1.3.rev12 - Settings Storage Panel Auto Height Fix

### Fixed

- Fixed the application-level Settings window clipping the lower content of the **Scan Results Storage Location** card when wrapped explanatory text required more vertical space than the fixed window layout supplied.
- Replaced the Settings window's fixed `Height="390"` sizing with `SizeToContent="Height"`, allowing the window to measure its content vertically instead of constraining the storage card to a predetermined initial height.
- Changed the main Settings content Grid row from star sizing to `Auto` so the storage card receives its desired height. Wrapped explanatory text, long active storage paths, and visible validation errors can now increase the card/window height instead of being clipped.

### Changed

- Advanced the host application from `0.1.3.rev11` to `0.1.3.rev12` with feature title `Settings Storage Panel Auto Height Fix` through the centralized `AppInfo` source.
- Updated the rev11 verification record with the reported successful Windows build/startup result and the subsequently discovered Settings layout defect.
- Added `docs/testing/APP_0.1.3_REV12_VERIFICATION.md` with build, automated, Settings-layout, theme, storage-foundation, and regression verification steps.
- Updated README and current PS5 implementation documentation to identify the current host revision.

### Preserved

- The rev11 `x:ClassModifier="internal"` accessibility correction remains unchanged for both Settings and operation-progress windows.
- Settings persistence, storage-path validation, next-session activation, application/scan-session identity, storage metadata state, stale cleanup, New Scan invalidation, shutdown cleanup, logging, and the reusable modal progress framework are unchanged.
- Main workspace XAML, `ProportionalGridSplitter`, shared control metrics/styles, Core scanner source, Plugin SDK, PS5 plugin, and Mock plugin are unchanged.
- The scanner continues to use the established in-memory/native candidate path.
- The `2,000,000` scan-result safety limit and `50,000` WPF presentation cap remain unchanged.
- Plugin API remains `2.1.0`; PS5 plugin remains `0.1.0.rev12`; In-Memory Test Target remains `1.0.0.rev3`.
- Disk-backed massive-result handling remains deferred until the Scan Result Storage Foundation is fully verified.

### Verification

- Re-read README, CHANGELOG, every Markdown file under `docs/`, and reviewed the complete rev11 solution/source/resource inventory before applying the layout correction.
- Confirmed the clipping is caused by the fixed Settings window height together with the star-sized card row rather than by the card style, text style, storage-path logic, or theme resources.
- Confirmed the fix is isolated to Settings layout plus centralized version/documentation updates; no scanner, plugin, Core storage-lifecycle, shared-control, main-workspace, or progress-dialog behavior is changed.
- Parsed XAML/XML and JSON resources after the change and verified `SettingsWindow` retains `x:ClassModifier="internal"`.
- Native .NET/WPF build execution still needs to be performed on the Windows development machine because the preparation environment does not provide a .NET SDK.

## TeeKay87's Memory Engine 0.1.3.rev11 - WPF Dialog Accessibility Compile Fix

### Fixed

- Fixed the rev10 App-project `CS0262` compile failures for `OperationProgressDialog` and `SettingsWindow`. Both code-behind classes are intentionally `internal`, but the corresponding XAML roots omitted an explicit class modifier and therefore produced WPF-generated partial declarations with conflicting accessibility. Both XAML roots now declare `x:ClassModifier="internal"` so markup-generated and code-behind partial declarations have the same accessibility.
- Resolved the related `CS0051` error on `SettingsWindow` without exposing application-internal settings infrastructure publicly. Once the XAML-generated `SettingsWindow` partial is internal as intended, its constructor no longer forms an externally visible API surface containing the internal `ApplicationSettingsStore` parameter.
- Identified the reported `System.Object` and `ProportionalGridSplitter` XAML-designer errors as downstream App-build/designer failures unless they remain after the accessibility correction. `MainWindow.xaml`, `ProportionalGridSplitter.cs`, and their existing namespace relationship are unchanged.

### Changed

- Advanced the host application from `0.1.3.rev10` to `0.1.3.rev11` with feature title `WPF Dialog Accessibility Compile Fix` through the centralized `AppInfo` source.
- Updated the rev10 verification document with the actual Windows build result and explicitly recorded rev10 as unverified.
- Added `docs/testing/APP_0.1.3_REV11_VERIFICATION.md` with clean-build expectations, all 30 automated checks, rev9 foundation runtime checks, downstream XAML-designer guidance, and the unchanged massive-result boundary.
- Updated README and current PS5 implementation documentation to identify the current host revision.
- The disk-backed massive-result phase remains deferred until the Scan Result Storage Foundation can be built and runtime-verified; no part of that phase is included in this compile-only revision.

### Preserved

- `OperationProgressDialog`, `SettingsWindow`, and `ApplicationSettingsStore` remain host-internal implementation details rather than being made public solely to satisfy the compiler.
- Rev9 Scan Result Storage Foundation behavior is otherwise unchanged: settings persistence, configurable storage root, application/scan-session identities, metadata lifecycle, stale-session isolation, cleanup semantics, and reusable modal progress behavior are preserved.
- `MainWindow.xaml` and `ProportionalGridSplitter.cs` are unchanged.
- The scanner continues to use the established in-memory/native result path.
- The `2,000,000` scan-result safety limit and `50,000` WPF presentation cap remain unchanged.
- Plugin API remains `2.1.0`; PS5 plugin remains `0.1.0.rev12`; In-Memory Test Target remains `1.0.0.rev3`.
- Plugin-owned Value Types, Scan Types, Scan Options, PS5 Endianness, Alignment, Floating-point rounding, Pause target, TurboScan protocol/session behavior, raw memory access, Safe Write Test, tooltip wrapping, and Scan-panel overflow behavior are unchanged.

### Verification

- Re-read README, CHANGELOG, all Markdown documentation under `docs/`, and reviewed the complete rev10 solution/source/resource inventory before applying the correction.
- Confirmed both reported `CS0262` errors map directly to the two new internal code-behind window classes whose XAML roots lacked an explicit `x:ClassModifier`.
- Confirmed the reported `CS0051` is a consequence of the same effective public XAML partial declaration rather than a requirement to publish `ApplicationSettingsStore`.
- Parsed all XAML/XML and JSON resources after the change and verified both affected windows explicitly declare `x:ClassModifier="internal"`.
- Confirmed `MainWindow.xaml`, `ProportionalGridSplitter.cs`, Plugin SDK, PS5 plugin, Mock plugin, scanner limits, and massive-result behavior are unchanged from rev10.
- Native .NET/WPF build execution remains to be performed on the Windows development machine because the preparation environment does not provide a .NET SDK.

## TeeKay87's Memory Engine 0.1.3.rev10 - Scan Result Storage Compile Fix

### Fixed

- Fixed the rev9 Core compile failure in `ScanResultStorageSession.Cancel()`. `TransitionToTerminalState` requires a parameterless `Action`, but the cancellation path supplied `ScanResultStorageManager.LogCommitCancelled` directly even though that method requires a scan-session `Guid`. The callback now captures the active `ScanSessionId` through a parameterless lambda and invokes `LogCommitCancelled(ScanSessionId)`, matching the established callback pattern already used by the failure path.
- Resolved the root cause of the downstream build errors reported by Visual Studio: missing Core metadata references in the App/Tests projects and XAML designer failures for `System.Object`/`ProportionalGridSplitter`. No XAML, project reference, or `ProportionalGridSplitter` implementation change was required; those errors were consequences of Core not producing its assembly.

### Changed

- Advanced the host application from `0.1.3.rev9` to `0.1.3.rev10` with feature title `Scan Result Storage Compile Fix` through the existing centralized `AppInfo` source.
- Recorded the actual failed Windows build result in the rev9 verification document so rev9 cannot be mistaken for a verified baseline.
- Added `docs/testing/APP_0.1.3_REV10_VERIFICATION.md` with the required clean-build, 30-check automated verification, rev9 manual foundation verification, regression checks, and explicit massive-result scope boundary.
- Updated current-host version references in README and PS5 implementation documentation.
- The previously planned disk-backed massive-result work moves to the revision after this compile correction; no part of that feature is implemented here.

### Preserved

- Rev9 Scan Result Storage Foundation behavior is otherwise unchanged: settings persistence, configurable storage root, application/scan session identities, metadata lifecycle, cleanup rules, stale-session isolation, and reusable modal progress infrastructure remain as implemented.
- The scanner still keeps candidate address/value data in the established in-memory/native paths.
- The `2,000,000` scan-result safety limit remains unchanged.
- The `50,000` WPF result presentation cap remains unchanged.
- Plugin API remains `2.1.0`.
- PlayStation 5 plugin remains `0.1.0.rev12` with no source or protocol changes.
- In-Memory Test Target remains `1.0.0.rev3`.
- Plugin-owned Value Types, Scan Types, Scan Options, PS5 Endianness, Alignment, Floating-point rounding, Pause target behavior, TurboScan semantics, cancellation framing, raw memory access, Safe Write Test, rev8 tooltip wrapping, and Scan-panel overflow behavior are unchanged.

### Verification

- Re-read the supplied rev9 README, CHANGELOG, all Markdown documentation under `docs/`, and reviewed the complete solution/source/resource inventory before applying the compile correction.
- Confirmed the reported `CS1503` is caused by a single callback-signature mismatch at the rev9 cancellation transition and that the failure-path call already demonstrates the intended parameterless-lambda pattern.
- Confirmed the reported `CS0006` and XAML designer errors are downstream of the failed Core build and require no unrelated UI/project changes.
- Confirmed no PS5 plugin source, Plugin SDK contract, scanner limits, or massive-result behavior is changed by this revision.
- Native .NET/WPF build execution remains to be performed on the Windows development machine because the preparation environment does not provide a .NET SDK.

## TeeKay87's Memory Engine 0.1.3.rev9 - Scan Result Storage Foundation

### Added

- Added an application-level **Settings** window to the main application bar. Settings are host-owned and remain available independently of the selected platform plugin.
- Added a persisted **Scan Results Storage Location** setting. The default is `%LocalAppData%\TeeKay87\MemoryEngine\ScanResults`, the current path can be changed with a native folder picker or restored to the default, and candidate paths are validated with a real create/write/flush/delete probe before they are accepted.
- Extended the existing `%LocalAppData%\TeeKay87\MemoryEngine\settings.json` persistence path instead of introducing a parallel settings system. Theme persistence now delegates to the shared application settings store, so saving a theme preserves the scan-storage location and saving the scan-storage location preserves the selected theme.
- Added centralized `ApplicationPaths` definitions for application-local settings, themes, plugins, scan-result storage, and logs.
- Added a Core-owned scan-result storage abstraction and implementation with a unique GUID application-session identity created for every launch and a unique GUID scan-session identity created for every First Scan.
- Added managed storage-root ownership metadata plus versioned application `session.json` and scan `scan.json` metadata. Scan metadata records the application/scan identities, plugin id, target process id, plugin-defined Value Type/Scan Type ids, record-format version, lifecycle state, timestamps, and committed result count.
- Added explicit scan-result metadata states: `Creating`, `Writing`, `Committed`, `Failed`, `Cancelled`, and host invalidation state `Invalidated`. Only `Committed` represents a successfully committed storage session; no partial or terminal non-committed state is eligible for future storage-backed use.
- Added atomic metadata replacement using same-directory temporary files, explicit stream flush-to-disk, and final atomic move/replace so a partially written metadata file is not published as the active state.
- Added conservative startup stale-session cleanup. The host only considers GUID-named child directories whose `session.json` contains the expected Memory Engine ownership signature, supported format version, and matching application-session id. Unrelated directories are ignored.
- Added New Scan/session replacement invalidation and cleanup hooks. A previous scan session becomes ineligible before best-effort physical deletion, and a later First Scan always receives a new scan-session GUID.
- Added normal-shutdown cleanup for the current application-session directory and best-effort stale cleanup logging. Cleanup failure does not block shutdown or permit a stale session to be reused.
- Added application lifecycle logging to `%LocalAppData%\TeeKay87\MemoryEngine\Logs\MemoryEngine.log` for storage-root initialization, application/scan session creation, commit, cancellation/failure, invalidation, stale detection/deletion, and cleanup failure. Individual scan results are never logged.
- Added a reusable platform-neutral `OperationProgress` contract with optional status, optional detail text, and a nullable 0-1 fraction. A null fraction explicitly represents indeterminate progress.
- Added a reusable theme-aware WPF modal operation-progress dialog/service. It supports determinate and indeterminate progress, live status/detail updates, optional cancellation, owner-window modality, controlled close behavior, and propagation of operation results, cancellation, and exceptions to the caller.
- Added five deterministic verification checks covering storage-path validation, application-session isolation, conservative stale-session cleanup, scan-session lifecycle/commit semantics, and the generic operation-progress model. The verification executable now contains 30 top-level checks.
- Added detailed storage architecture, progress-dialog, and rev9 verification documentation under `docs/`.

### Changed

- Advanced the host application from `0.1.3.rev8` to `0.1.3.rev9` with feature title `Scan Result Storage Foundation` through the existing centralized `AppInfo` source.
- First Scan now creates host-side scan-session metadata before scan execution and commits only the session metadata/result count after a successful scan. Compatible Next Scan operations retain that same host scan-session identity; New Scan, target replacement, plugin reload, or ViewModel disposal invalidates it.
- Storage initialization uses the persisted configured root or the documented default. If that configured location cannot be initialized, the application reports/logs the failure and does not silently redirect scan-storage state to a different folder. Because rev9 does not yet depend on disk-backed candidate records, the existing scanner remains usable while the storage setting is corrected.
- A changed Scan Results Storage Location is persisted but becomes active on the next application launch. The active application-session directory is never migrated between storage roots while it is in use.
- Updated README and architecture/UI/plugin documentation to describe the new host infrastructure and clearly separate it from the future disk-backed massive-result implementation.

### Safety and Lifecycle Guarantees

- Scan-session correctness does not depend on successful file deletion. Active storage state is bound to explicit current application-session and scan-session GUIDs; old files are never selected because they merely exist or appear recent.
- Failed, cancelled, invalidated, interrupted, or otherwise uncommitted scan-session metadata is never promoted to `Committed` by cleanup or startup discovery.
- Startup cleanup is scoped to positively identified Memory Engine scan-storage directories and does not recursively remove arbitrary content from a user-selected parent folder.
- Recursive managed-directory deletion refuses filesystem reparse-point roots and validates that the directory being removed is an immediate child of the expected managed parent.
- Storage metadata remains host/Core infrastructure. No PS5-specific storage branch, Windows-path responsibility, cleanup responsibility, or progress-dialog responsibility was added to a platform plugin.

### Preserved

- The current scanner result data path remains in memory. Rev9 does **not** write candidate addresses/values to disk and does not make Next Scan depend on the new metadata files.
- The `2,000,000` scan-result safety limit remains unchanged.
- The `50,000` WPF result presentation cap and existing virtualization remain unchanged.
- Plugin-owned Value Types, Scan Types, and Scan Options remain authoritative. Core records stable plugin-defined ids only as metadata and does not reintroduce concrete scanner catalogs or PS5-specific type checks.
- Plugin API remains `2.1.0`.
- PlayStation 5 plugin remains `0.1.0.rev12`; Pause target while scanning, Endianness, Alignment, Floating-point rounding, TurboScan behavior, cancellation framing, process control, memory reads/writes, and Safe Write Test behavior are unchanged.
- In-Memory Test Target remains `1.0.0.rev3` targeting Plugin API `2.0.0`.
- The rev8 Scan-panel overflow behavior and long-tooltip wrapping remain unchanged.

### Deferred to the Massive-Result Revision

- Candidate address/value files such as `addresses.bin`/`values.bin` are not introduced in rev9.
- First Scan does not yet stream/batch very large result sets to disk.
- Next Scan does not yet read/refine a disk-backed full result set.
- The existing 2,000,000-result rejection is not removed in this revision.
- The reusable modal progress dialog is not yet invoked for scan-result persistence because rev9 performs only small lifecycle-metadata writes. The future large-result writer is expected to use this generic component.

### Verification Scope

- Build the complete solution in Release configuration with zero warnings/errors; warnings remain treated as errors.
- Run the dependency-free verification executable and require `All 30 checks passed.`.
- Confirm the Settings window opens from the application bar in Light, Dimmed, and Dark, displays the current configured storage root, supports Browse/Use Default, rejects an unwritable location with a controlled message, and persists a valid custom path without losing the selected theme.
- Restart after changing the storage location and confirm the new root becomes active only for the new application session.
- Confirm each launch creates a different application-session GUID and each First Scan creates a different scan-session GUID; compatible Next Scan must retain the same scan-session identity and New Scan must invalidate the previous session.
- Confirm normal exit removes the active application-session directory when deletion succeeds.
- Seed a valid managed stale session and confirm startup removes it; seed an unrelated GUID directory without valid Memory Engine ownership metadata and confirm startup leaves it untouched.
- Simulate a stale/incomplete `Writing` session and confirm a subsequent application session never adopts it as current state even if physical deletion is prevented.
- Exercise the reusable progress component in determinate/indeterminate modes, with and without cancellation, in all bundled themes. Confirm the owner cannot be interacted with while the dialog is open and failures propagate to the caller after controlled dialog closure.
- Re-run the existing scanner/UI regression checks, including rev8 tooltip/Scan-panel behavior and the unchanged 2,000,000/50,000 boundaries.

## TeeKay87's Memory Engine 0.1.3.rev8 - Scan Panel Overflow and Tooltip Fix

### Fixed

- Fixed long application-owned ToolTips/hints being clipped at the configured popup `MaxWidth` instead of wrapping to additional lines. The shared ToolTip template now applies `TextWrapping=Wrap` to the `TextBlock`/`AccessText` elements WPF materializes for string tooltip content, so plugin-provided descriptions such as the PS5 Alignment hint remain fully readable without expanding beyond the intended tooltip width.
- Fixed the right-side Scan panel becoming vertically clipped when plugin-provided Scan Options increase the required control height beyond the available workspace height. The complete Scan-panel content is now hosted in an automatic vertical `ScrollViewer`; horizontal scrolling is disabled.
- Added an explicit 10-DIP content gap between Scan controls and the vertical scrollbar while preserving the panel's existing 14-DIP outer padding. Controls therefore do not sit directly against the scroll track when the scrollbar is visible.
- Kept Scan-panel content stretched to the available viewport width inside the new `ScrollViewer`, preventing selectors/buttons from collapsing to their desired text width after the scrolling container was introduced.

### Changed

- Advanced the host application from `0.1.3.rev7` to `0.1.3.rev8` with feature title `Scan Panel Overflow and Tooltip Fix`.
- Updated host UI documentation to define long-tooltip wrapping and Scan-panel overflow behavior as reusable application presentation rules rather than PS5-specific exceptions.
- Added `docs/testing/APP_0.1.3_REV8_VERIFICATION.md` covering tooltip wrapping, automatic Scan-panel scrolling, scrollbar/control spacing, window resize behavior, bundled themes, and rev7/regression checks.

### Preserved

- Plugin API remains `2.1.0`.
- PlayStation 5 plugin remains `0.1.0.rev12`; its Endianness, Alignment, Floating-point rounding, and Pause target controls are unchanged functionally.
- In-Memory Test Target remains `1.0.0.rev3` targeting Plugin API `2.0.0`.
- The rev6 global disabled-button text rendering fix remains unchanged.
- Plugin-owned Value Type/Scan Type/Scan Option architecture, PS5 TurboScan protocol behavior, scan-session locking, floating-point semantics, alignment handling, cancellation, process pause/resume, memory read/write, Safe Write Test, result limits, Saved Addresses placeholders, and all other scanner behavior are unchanged.
- This revision does not introduce Settings, disk-backed massive scan results, result-storage cleanup, or the planned reusable modal progress dialog.

### Verification Scope

- Build the complete solution in Release configuration and require zero compiler errors.
- Run the existing dependency-free verification executable and require `All 25 checks passed.`.
- At a window height where all Scan controls fit, confirm the Scan-panel vertical scrollbar is hidden.
- Reduce the window height until the PS5 Scan Options no longer fit. Confirm an automatic vertical scrollbar appears, every control including New Scan and Cancel Scan remains reachable, no horizontal scrollbar appears, and there is visible spacing between the controls and scroll track.
- Hover the PS5 Alignment selector and other long hints. Confirm the complete hint wraps over multiple lines instead of being clipped at the right edge.
- Repeat visual checks in Light, Dimmed, and Dark and confirm rev6 disabled-button label rendering remains correct.

## TeeKay87's Memory Engine 0.1.3.rev7 - PS5 Scan Options

### Added

- Added the optional `IMemoryScanOption` Plugin SDK contract and `MemoryScanOptionChoice` model. A platform plugin can now declare additional scan controls without the WPF host knowing the platform name or hard-coding a PS5-specific control.
- Added `ITargetPlugin.SupportedScanOptions` with an empty default implementation. Existing Plugin API 2.0 plugins therefore continue to load without implementing the new property, while Plugin API 2.1 plugins can supply concrete scan options.
- Added immutable `MemoryScanOptions` state and carried the selected option values through shared scanner execution and `NativeValueScanRequest`. Native and shared paths therefore receive the same scan-session option state.
- Added reusable standard option ids/choice ids for Endianness, Alignment, and Floating-point rounding. These are neutral shared concepts, not a Core-owned list of target capabilities; the active plugin still decides which options and choices it exposes.
- Added a generic `ScanOptionViewModel` and Scan-panel item template. Any plugin-provided option is rendered as a theme-aware selector using the plugin's own display name, description, choices, default, Value Type applicability, and First Scan lock policy.
- Extended plugin discovery validation to cover scan-option declarations: option ids must be unique, every option must expose valid unique choices, the default must reference a declared choice, and option applicability must be evaluable against the plugin's declared Value Types. Invalid option metadata is rejected before it reaches the UI.
- Added three concrete PS5 scan options:
  - **Endianness** — Little Endian (default) or Big Endian for multi-byte values;
  - **Alignment** — Default natural alignment or explicit 1, 2, 4, 8, 16, 32, 64, or 128 byte scan steps;
  - **Floating-point rounding** — Strict (default) or **ps5debug-NG tolerance (1e-6)** for Float/Double.

### Changed

- Advanced the host application from `0.1.3.rev6` to `0.1.3.rev7` with feature title `PS5 Scan Options`.
- Advanced Plugin API from `2.0.0` to `2.1.0`. The change is additive: the current host accepts plugins targeting an older 2.x minor API, and the Mock plugin intentionally remains `1.0.0.rev3` targeting API `2.0.0` to verify this compatibility path.
- Advanced the PlayStation 5 plugin from `0.1.0.rev11` to `0.1.0.rev12` and updated its target Plugin API to `2.1.0`.
- Scan value parsing/formatting now uses an effective per-scan `TargetArchitecture` when a plugin supplies the standard Endianness option. The target metadata itself remains the physical architecture; selecting Big Endian changes only the scan representation.
- Shared Core scanning now honors an explicit standard Alignment choice when supplied. `Default` retains the selected Value Type's plugin-defined natural alignment.
- Extended standard Exact Value comparison with an opt-in ps5debug-NG-compatible relative floating-point tolerance. Strict remains the default and unchanged behavior for plugins/options that do not request tolerance.
- PS5 native validation now accepts any positive alignment that fits ps5debug-NG's `uint8 alignment` field instead of incorrectly requiring only the natural Value Type alignment.
- PS5 native Float/Double behavior is now explicit:
  - Strict First Scan may still use TurboScan, but host retrieval filters any additional fuzzy native matches back to strict numeric equality;
  - Strict Next Scan closes the resident TurboScan session and uses shared Core refinement, preserving the existing strict semantics;
  - `ps5debug-NG tolerance (1e-6)` intentionally accepts the payload's native relative tolerance and allows compatible resident Float/Double Next Scan through TurboScan COUNT/GET;
  - Big-endian Float/Double uses the shared Core path because ps5debug-NG interprets native floating-point values in little-endian form.
- Applicable PS5 scan options lock after a successful First Scan and unlock on New Scan. Options that do not apply to the selected Value Type remain visible but disabled; Floating-point rounding is therefore available only for Float/Double, while Endianness is disabled for one-byte and Array-of-Bytes representations where byte order has no effect.

### ps5debug-NG Mapping Verified

- Confirmed from the current ps5debug-NG protocol/source that TurboScan START contains an explicit one-byte `alignment` field and that the server uses `alignment ? alignment : value_length` as its candidate step. Memory Engine sends the selected explicit alignment directly.
- Confirmed that ps5debug-NG command/multi-byte protocol fields are little-endian and that there is no separate TurboScan wire field for Endianness. Memory Engine therefore implements Endianness as scan-value encoding/decoding semantics rather than inventing a protocol flag.
- Confirmed from current `scan_compare.c` that native Exact Value Float/Double comparisons use a relative tolerance of `1e-6`. The new floating-point mode names that behavior explicitly instead of presenting it as strict equality.

### Verification

- Expanded the dependency-free verification executable from 22 to 25 top-level checks.
- Added assertions for Plugin API `2.1.0`, PS5 plugin `0.1.0.rev12`, the PS5 option declaration order/defaults/applicability, and API 2.0 Mock-plugin compatibility with no declared scan options.
- Added shared scan-option verification for Big Endian encoding, custom one-byte alignment, and Strict versus relative-`1e-6` floating-point matching.
- Added PS5 protocol verification that a custom alignment reaches TurboScan START and that opt-in tolerant Float refinement remains on the resident native COUNT/GET path.
- Added `docs/testing/APP_0.1.3_REV7_VERIFICATION.md` covering automated verification, generic UI rendering, session locking, live Endianness/Alignment/Float tests, and regressions.

### Preserved

- Core still does not own concrete Value Type or Scan Type catalogs. Plugins continue to own the concrete scanner definitions and decide what appears in the host selectors.
- Rev6 global disabled-button text rendering remains unchanged.
- The current 2,000,000-result safety limit, 50,000-row display cap, result model, cancellation framing, target pause/resume workflow, raw memory read/write, Safe Write Test, preferred `eboot.bin` selection, and Saved Addresses placeholders remain unchanged.
- Settings/result-storage configuration, disk-backed massive result sets, and the reusable modal progress dialog remain planned later work and are not introduced by this revision.

## TeeKay87's Memory Engine 0.1.3.rev6 - Disabled Button Text Rendering Fix

### Fixed

- Fixed the remaining global disabled-button label rendering defect discovered during live rev5 verification. The rev5 template correctly applied the disabled background and border and also set the button/content-presenter foreground, but WPF can materialize a string `Button.Content` as an internal `AccessText` or `TextBlock`. That generated text element could retain a normal text foreground instead of visually consuming the disabled foreground, leaving disabled labels white in every standard button style.
- Added local `AccessText` and `TextBlock` styles inside the shared `ButtonBaseStyle` content presenter. Generated button-label text now binds its enabled foreground directly to the containing `Button.Foreground`, preserving Primary/Secondary/Danger semantic colors, and has an explicit ancestor-`IsEnabled=False` trigger that applies `DisabledButtonTextBrush` directly to the rendered text element.
- Kept the existing rev5 disabled background/border enforcement and content-presenter `TextElement.Foreground` setter as complementary safeguards. The resulting disabled state no longer depends on one WPF foreground inheritance path.
- The fix remains application-wide. All current application buttons use string content and the shared button template, so command-disabled and explicitly disabled labels now follow the active theme's disabled text brush without Scan-panel-specific styling.

### Changed

- Advanced the host application from `0.1.3.rev5` to `0.1.3.rev6` with feature title `Disabled Button Text Rendering Fix`.
- Updated button/theme documentation and added a rev6 verification checklist focused on the rendered foreground color of disabled labels in all bundled themes.

### Preserved

- Plugin API remains `2.0.0`.
- PlayStation 5 plugin remains `0.1.0.rev11`.
- In-Memory Test Target plugin remains `1.0.0.rev3`.
- Plugin-owned Value Type and Scan Type architecture, scanner behavior, PS5 transport/protocol handling, result limits, cancellation, process control, Raw Memory Read/Write, Safe Write Test, target selection, and all other verified functionality are unchanged.
- Button command availability and semantic enabled colors are unchanged; this revision only corrects how disabled label text is rendered.
- Future Settings/result-storage work, disk-backed massive result sets, reusable modal progress UI, and PS5 Scan-panel options for Endianness, Alignment, and Floating-point rounding remain outside this revision.

### Verification Scope

- Build the complete solution in Release configuration and require zero compiler errors.
- Run the existing verification executable and require `All 22 checks passed.`.
- In Dimmed, Dark, and Light themes, start with no active target and verify the labels of disabled **First Scan**, **Next Scan**, **New Scan**, and **Cancel Scan** buttons use the theme's disabled text color rather than the normal white/primary text color.
- Verify the explicitly disabled Saved Addresses **Add Address**, **Edit**, **Remove**, and **Export...** labels use the same disabled text brush.
- Connect to the Mock target and verify buttons immediately return to their normal semantic foreground when enabled and return to disabled foreground when command state becomes unavailable.
- Verify the enabled Primary, Secondary, and Danger button text colors remain unchanged.

## TeeKay87's Memory Engine 0.1.3.rev5 - Global Disabled Button State Fix

### Fixed

- Fixed the application-wide WPF button disabled-state presentation. Buttons whose `IsEnabled` becomes `False` through command `CanExecute`, bindings, or a direct property value could keep the normal semantic foreground/background/border supplied by `PrimaryButtonStyle`, `SecondaryButtonStyle`, or `DangerButtonStyle`, even though the shared base style already declared disabled palette setters. This made disabled actions such as **Next Scan**, **New Scan**, **Cancel Scan**, and the Saved Addresses placeholder buttons look substantially more usable than they actually were.
- Moved the visual enforcement of the disabled button palette into the shared `ButtonBaseStyle` control template. The template now names the content presenter and, while disabled, directly applies `DisabledButtonBackgroundBrush`, `DisabledButtonBorderBrush`, and `DisabledButtonTextBrush` to the rendered border/content. This avoids relying only on inherited style-property precedence when semantic or view-local derived styles also set Foreground, Background, or BorderBrush.
- Kept the existing disabled cursor behavior, hover/pressed overlay suppression, and focus-outline suppression. The correction therefore changes only the visual reliability of the already-defined disabled state and does not change button commands or availability logic.

### Changed

- Advanced the host application from `0.1.3.rev4` to `0.1.3.rev5` with feature title `Global Disabled Button State Fix`.
- Updated shared button/theme documentation and added a dedicated rev5 verification checklist covering Primary, Secondary, Danger, workflow-aware Scan buttons, command-driven disabled state, explicit `IsEnabled="False"`, and all three bundled themes.

### Preserved

- Plugin API remains `2.0.0`.
- PlayStation 5 plugin remains `0.1.0.rev11`.
- In-Memory Test Target plugin remains `1.0.0.rev3`.
- Rev3/rev4 plugin-owned Value Type and Scan Type architecture is unchanged.
- PS5 scanning, native refinement, Float/Double fallback, cancellation/session safety, process pause/resume, Raw Memory Read/Write, Safe Write Test, target selection, result limits, and result presentation behavior are unchanged.
- ComboBox, TextBox, CheckBox, ContextMenu/MenuItem, DataGrid, tooltip, splitter, and workspace behavior are unchanged.
- Future Settings/result-storage work, massive disk-backed result sets, reusable modal progress UI, and PS5 Scan-panel options for Endianness, Alignment, and Floating-point rounding remain outside this revision.

### Verification Scope

- Build the complete solution in Release configuration and require zero compiler errors.
- Run the existing verification executable and require `All 22 checks passed.`; no scanner/plugin test count changes are required because rev5 is a WPF presentation-only host correction.
- In Light, Dimmed, and Dark themes, verify that disabled Primary, Secondary, and Danger buttons visibly use the disabled background, border, and text palette rather than their semantic enabled colors.
- Verify the initial Scan panel state: **First Scan** remains enabled/primary while **Next Scan**, **New Scan**, and **Cancel Scan** are visibly disabled, including clearly dimmed label text.
- Verify a successful First Scan/Next Scan workflow still changes command availability and primary emphasis correctly, and that disabled buttons immediately return to the common disabled visual state when `CanExecute` becomes false.
- Verify the permanently disabled Saved Addresses placeholder buttons use the same disabled visuals, proving the fix applies globally rather than only to the Scan panel.

## TeeKay87's Memory Engine 0.1.3.rev4 - Incremental Upgrade Compile Fix

### Fixed

- Fixed the rev3 source-package upgrade path when the package is extracted over an existing rev2 project directory. Rev3 correctly removed the old Core-owned scan catalogs and enums from its clean package, but normal archive extraction does not delete files that are absent from the newer archive. The obsolete rev2 `Core.Scanning.MemoryScanValue` source could therefore remain on disk and shadow `PluginSdk.Models.MemoryScanValue` inside the Core namespace, producing the CS0029/CS1503/CS1061 errors reported from `MemoryScanner.cs` and `MemoryScanResult.cs`.
- Added inert upgrade tombstones at all six source paths removed by the rev3 architecture change: `MemoryScanValue.cs`, `MemoryScanValueCodec.cs`, `MemoryValueTypeCatalog.cs`, `MemoryScanTypeCatalog.cs`, `MemoryValueType.cs`, and `MemoryScanType.cs`. Each file intentionally defines no type. Extracting rev4 over a rev2/rev3 working tree therefore overwrites any stale rev2 implementation instead of leaving it eligible for SDK-style wildcard compilation.
- Prevented stale rev2 `MemoryValueType` and `MemoryScanType` enum source files from silently surviving an incremental extraction and violating the Plugin API 2.0.0 plugin-owned definition model.
- Extended the existing plugin-owned scan-definition verification to assert that Core does not contain the obsolete rev2 `MemoryScanValue` type and that Plugin SDK does not contain the obsolete rev2 `MemoryValueType` or `MemoryScanType` enum types. The automated suite remains 22 top-level checks because these assertions strengthen the existing contract check rather than create separate test cases.

### Changed

- Advanced the host application from `0.1.3.rev3` to `0.1.3.rev4` with feature title `Incremental Upgrade Compile Fix`.
- Kept Plugin API at `2.0.0`, the PlayStation 5 plugin at `0.1.0.rev11`, and the In-Memory Test Target plugin at `1.0.0.rev3`; no plugin contract or plugin runtime behavior changed in this revision.
- Updated README and the current verification documentation to describe the safe incremental source-package upgrade behavior and the rev4 acceptance procedure.

### Preserved

- The rev3 plugin-owned Value Type and Scan Type architecture is unchanged: Core owns only generic contracts/orchestration, while each plugin supplies the concrete definitions shown by the UI.
- Rev2's stronger disabled-state styling is unchanged.
- PS5 scan behavior, ps5debug-NG wire mappings, TurboScan First/Next behavior, Float/Double Core-refinement fallback, cancellation/session safety, process suspend/resume, target selection, raw memory read/write, Safe Write Test, the 2,000,000-result safety limit, and the 50,000-row presentation cap are unchanged.
- Future Settings/result-storage work, massive disk-backed result sets, the reusable modal progress dialog, and PS5 Scan-panel options for Endianness, Alignment, and Floating-point rounding remain outside this revision.

### Verification Scope

- Extract rev4 over a copy of the rev2 source tree and verify that all six obsolete source paths are replaced by inert tombstones rather than their previous class/enum/catalog implementations.
- Build the complete solution in Release configuration and require zero compiler errors. In particular there must be no conversion between `Core.Scanning.MemoryScanValue` and `PluginSdk.Models.MemoryScanValue`, because the Core type must no longer exist.
- Run the verification executable and require `All 22 checks passed.`.
- Verify that the application identifies itself as `0.1.3.rev4`, Plugin API as `2.0.0`, the PS5 plugin as `0.1.0.rev11`, and the Mock plugin as `1.0.0.rev3`.
- Re-run the rev3 selector/disabled-state checks and a representative PS5 Exact Value First/Next Scan to confirm the compile fix did not alter runtime behavior.
- Native Windows compilation remains the authoritative compile verification because the package-preparation environment does not provide the .NET 9 WPF SDK/toolchain.

## TeeKay87's Memory Engine 0.1.3.rev3 - Plugin-Owned Scan Definitions

### Added

- Added the public `IMemoryValueType` Plugin SDK contract. A Value Type now describes its own stable id, display metadata, fixed or dynamic width, default alignment, text parsing, scan-shape resolution, byte-to-display conversion, and equality semantics. Core can execute a Value Type without knowing what concrete representation it describes.
- Added the public `IMemoryScanType` Plugin SDK contract. A Scan Type now describes its own stable id, display metadata, First Scan/Next Scan availability, required input count, supported Value Types, and comparison behavior for the shared reader-based scanner.
- Added `MemoryScanStage` to the Plugin SDK so Scan Type implementations can distinguish First Scan and Next Scan without depending on WPF or Core-specific UI state.
- Moved the neutral parsed scan-value model into the Plugin SDK. `MemoryScanValue` now carries the plugin-provided Value Type id/display name, encoded bytes, display text, width, and alignment across the plugin/Core boundary.
- Added `NativeValueScanResult` so native scanners return both an address and the current bytes found at that address. Core converts those bytes through the active plugin-provided `IMemoryValueType` instead of assuming a Core-owned codec.
- Added optional reusable standard implementations in `TeeKay87.MemoryEngine.PluginSdk.Scanning`. `StandardMemoryValueTypes` provides the eleven already implemented integer/floating-point/Array-of-Bytes definitions and `StandardMemoryScanTypes.ExactValue` provides the existing exact-comparison behavior. These are reusable SDK building blocks, not a host-owned registry: a plugin may use them, omit them, wrap them, or provide entirely custom definitions with its own ids and behavior.
- Added explicit plugin-owned scan-definition containers for the PS5 and Mock plugins. Each plugin now selects the concrete Value Type and Scan Type objects that it exposes to the host and supplies its own default ids.
- Added deterministic verification that Core contains no concrete Value Type catalog, Scan Type catalog, or value codec, and that the shared scanner accepts a completely custom test-only Value Type and Scan Type that are unknown to Core.

### Changed

- Advanced the host application from `0.1.3.rev2` to `0.1.3.rev3` with feature title `Plugin-Owned Scan Definitions`.
- Advanced Plugin API from `1.3.0` to `2.0.0`. This is intentionally a major contract change because the rev2 enum/catalog capability model is replaced rather than extended.
- Updated the PlayStation 5 plugin from `0.1.0.rev10` to `0.1.0.rev11` and its target Plugin API to `2.0.0`.
- Updated the In-Memory Test Target plugin from `1.0.0.rev2` to `1.0.0.rev3` and its target Plugin API to `2.0.0`.
- Changed `ITargetPlugin.SupportedValueTypes` from a list of shared enum identifiers to a list of concrete `IMemoryValueType` definitions supplied by the plugin.
- Changed `ITargetPlugin.SupportedScanTypes` from a list of shared enum identifiers to a list of concrete `IMemoryScanType` definitions supplied by the plugin.
- Added `DefaultValueTypeId` and `DefaultScanTypeId` to the plugin contract so a plugin controls its initial Scan-panel selections without a host-side platform/type assumption.
- Reworked `MemoryScanner` so shared First Scan and Next Scan orchestration remains in Core, while parsing, value width/alignment, formatting, equality, stage validity, input requirements, and comparison semantics are delegated to the selected plugin-owned definitions.
- Reworked native scan requests to use stable string Value Type and Scan Type ids plus explicit width, alignment, and encoded input values rather than Core-owned enums. This keeps native transport implementations extensible without adding new concrete type identifiers to Core.
- Reworked native result normalization so Core receives current bytes from the native plugin and asks the active Value Type definition to create the neutral displayed value.
- Reworked the Scan panel bindings so Value Type and Scan Type entries are created directly from the active plugin's definition objects. The host no longer looks up plugin declarations in a Core catalog.
- Reworked Scan Type filtering so the active plugin definition itself declares whether it is available for First Scan/Next Scan and whether it supports the selected Value Type.
- Kept Value Type selection available whenever the active plugin exposes scanner definitions, even if the currently selected Value Type has no compatible Scan Type. This prevents a plugin-defined unsupported combination from trapping the UI in an unchangeable selection while preserving normal Scan Type filtering.
- Reworked plugin-host validation to validate plugin definitions structurally: non-null collections/entries, stable non-empty ids/display names, unique ids, valid width/alignment metadata, usable scan-stage declarations, valid input counts, and default ids that belong to the plugin's own declaration set.
- The PS5 native TurboScan implementation now maps the stable ids of the standard definitions it deliberately exposes to ps5debug-NG wire ids internally. Exact Value validation and Float/Double native-refinement fallback are likewise owned by the PS5 plugin rather than Core.

### Removed

- Removed the rev2 Core `MemoryValueTypeCatalog`. Core no longer contains or maintains a master list of concrete Value Types.
- Removed the rev2 Core `MemoryScanTypeCatalog`. Core no longer contains or maintains a master list of concrete Scan Types.
- Removed the shared `MemoryValueType` and `MemoryScanType` enum registries introduced by rev2.
- Removed `MemoryScanValueCodec` from Core. Parsing, encoding/decoding, display formatting, scan width/alignment, and value equality now belong to the selected `IMemoryValueType` implementation.
- Removed plugin discovery's dependency on recognizing declarations through a host-owned catalog. A plugin may now expose a new custom Value Type or Scan Type without requiring a Core update first.

### Preserved

- The rev2 global disabled-state styling remains unchanged. Disabled buttons, ComboBoxes, CheckBoxes, context-menu items, and related controls continue to use the stronger theme-aware visual treatment introduced there.
- The PS5 Scan panel continues to expose exactly the same eleven currently implemented Value Types: UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float, Double, and Array of Bytes. Its only Scan Type remains Exact Value.
- Existing PS5 TurboScan wire ids, multi-segment First Scan, server-resident compatible refinement, strict Float/Double Core fallback, cancellation/session preservation, process pause/resume, preferred `eboot.bin` selection, raw memory read/write, Safe Write Test, scan keyboard workflow, result presentation, 2,000,000-result safety limit, and 50,000-row presentation cap are not intentionally changed.
- Raw Memory Write remains a signed Int32 diagnostic and Scan Results -> Change value remains enabled only for the existing standard signed Int32 result type.
- Massive-result disk storage, configurable result-storage paths, stale-session cleanup, the reusable modal progress dialog, and additional PS5 scan options remain separate future work.

### Verification Scope

- Verify that the application identifies itself as `0.1.3.rev3`, Plugin API as `2.0.0`, the PS5 plugin as `0.1.0.rev11`, and the Mock plugin as `1.0.0.rev3`.
- Run the deterministic verification executable and require all 22 checks to pass.
- Confirm that Core contains no concrete Value Type/Scan Type catalog or codec and that the custom `test.byte` / `test.equals` definitions execute through the shared scanner without being registered in Core.
- With the PS5 plugin active, confirm that the Value Type list still contains exactly its eleven plugin-supplied entries and Scan Type still contains only Exact Value.
- Confirm that Value Type remains locked after First Scan, New Scan restores selection, and Scan Type applicability is taken from the active plugin definition rather than a host catalog.
- Re-run representative PS5 Exact Value First/Next Scan coverage and the existing cancellation/process-control/raw-memory regressions.
- Re-check rev2 disabled-state presentation in Light, Dimmed, and Dark because rev3 intentionally preserves that unverified rev2 UI change while replacing its catalog architecture.
- Native Windows compilation and the 22-check executable must be run on the Windows development machine if the preparation environment does not provide the .NET 9 SDK/WPF toolchain.

## TeeKay87's Memory Engine 0.1.3.rev2 - Capability-Driven Scan Catalogs and Disabled States

### Added

- Added a shared Core Value Type catalog that defines the platform-neutral scanner vocabulary independently of any one platform plugin. The catalog now covers the value-type families identified through a survey of Cheat Engine, PINCE, scanmem/GameConqueror, Squalr, ArtMoney, GameGuardian, MemoryEngine360, DijoScan, Bit Slicer, PyMemoryEditor, ReClass.NET, and related memory-editing tooling.
- Expanded the shared `MemoryValueType` identifiers beyond the previously reserved primitive/string set. The catalog now includes signed/unsigned 24-bit integers, IEEE half precision, legacy six-byte Real48, 80-bit extended floating point, Boolean, Binary, BitField, target-sized Pointer values, UTF-32, XOR-encoded values, aggregate integer/float/number/all modes, Grouped values, typed arrays, structures, and a deliberately extensible Custom representation. Existing enum numeric values `0..13` remain unchanged so the SDK update does not renumber previously published identifiers.
- Added a shared Core Scan Type catalog. It defines direct comparisons, unknown/snapshot comparisons, change-based refinements, percentage/difference variants, and specialized search methods including sequence, hex-sequence, encoded-value, formula, structure, address-mask, fuzzy, and regular-expression searches. Catalog definitions also record whether a mode is meaningful for First Scan, Next Scan, or both and how many primary input values it requires.
- Added `MemoryScanType` to the Plugin SDK so Scan Type capability declarations are no longer represented by application-specific UI assumptions.
- Added `ITargetPlugin.SupportedValueTypes` and `ITargetPlugin.SupportedScanTypes`. Default empty implementations preserve load compatibility for older same-major Plugin API binaries; older plugins simply expose no scanner choices until they adopt the new capability contract.
- Added plugin-host validation for declared scan capabilities. Unknown catalog entries, duplicate Value Type declarations, duplicate Scan Type declarations, and null collections are rejected during discovery instead of reaching the UI as an invalid plugin state.
- Added a dedicated research record at `docs/research/MEMORY_EDITOR_SCAN_TYPE_SURVEY.md` documenting the external memory-editor survey, normalization decisions, deliberate exclusions, and the distinction between catalog vocabulary and actually implemented plugin capabilities.
- Added deterministic verification for Core catalog completeness and built-in plugin scan declarations. The verification executable now contains 22 checks.

### Changed

- Advanced the host application from `0.1.3.rev1` to `0.1.3.rev2`.
- Advanced Plugin API from `1.2.0` to `1.3.0` for the new scanner capability declarations.
- Updated the PlayStation 5 plugin from `0.1.0.rev9` to `0.1.0.rev10` and its target Plugin API to `1.3.0`.
- Updated the In-Memory Test Target plugin from `1.0.0.rev1` to `1.0.0.rev2` and its target Plugin API to `1.3.0`.
- The PlayStation 5 plugin now explicitly declares only the eleven Value Types it actually implements through ps5debug-NG (`UInt8`, `Int8`, `UInt16`, `Int16`, `UInt32`, `Int32`, `UInt64`, `Int64`, `Float32`, `Float64`, and `ByteArray`) and `ExactValue` as its only supported Scan Type. The Mock plugin declares the same currently implemented shared scanner set for deterministic regression coverage.
- The Scan panel no longer builds Value Type choices from a Core implementation list and no longer hard-codes an `Exact Value` ComboBox item in XAML. Both selectors are populated from the active plugin's declarations and use the shared Core catalogs only for common naming/metadata.
- Scan Type is now a real bound selection. Core catalog metadata marks each Scan Type as valid for First Scan, Next Scan, or both, and the UI filters the active plugin's declaration to the current scan stage. A plugin exposing one applicable Scan Type produces a single-item disabled selector; a future plugin exposing multiple applicable types can make the selector available without a platform-specific application change. Value Type remains locked after First Scan, while Scan Type can change between supported Next Scan predicates as required by Cheat Engine-style refinement workflows.
- `MemoryScanValueCodec` continues to own only the Exact Value representations that the shared scanner can currently parse/compare. Display names are delegated to the Core Value Type catalog so the complete catalog and the currently implemented codec are no longer conflated.
- Strengthened disabled-state presentation across bundled themes. Disabled text is deliberately dimmer and disabled button/input surfaces use lower-contrast backgrounds and borders so `IsEnabled="False"` is visibly different from an enabled control.
- Updated ComboBox styling so the drop-down arrow follows the control foreground and therefore dims together with disabled text. Disabled CheckBox content receives reduced opacity, and disabled context-menu items such as Scan Results -> **Change value** now dim the whole item and cannot retain a hover-like highlighted surface.

### Fixed

- Removed the architectural assumption that every platform must expose the PS5 scanner's Value Type set.
- Removed the architectural assumption that the application itself owns the Scan Type list.
- Fixed disabled context-menu items being visually almost indistinguishable from enabled items under some themes even though WPF correctly blocked interaction.
- Fixed disabled ComboBox arrows remaining visually active while the rest of the control was disabled.

### Preserved

- No new Scan Type comparison algorithm is enabled by this revision. `ExactValue` remains the only Scan Type advertised by the current PS5 and Mock plugins, so all previously verified scan semantics remain unchanged.
- The PS5 plugin still exposes exactly the eleven ps5debug-NG Value Types verified in `0.1.3.rev1`; adding the complete Core master catalog does not cause unsupported types to appear in the PS5 UI.
- Native PS5 TurboScan First Scan, compatible resident Next Scan, Float/Double Core fallback, cancellation/session preservation, process pause/resume, preferred `eboot.bin` selection, raw memory read/write, Safe Write Test, keyboard scan workflow, result presentation, and current result safety/display limits are unchanged.
- Massive-result disk storage, configurable scan-result storage paths, stale-session cleanup, and the reusable modal progress dialog remain separate future work. This revision establishes capability/catalog architecture only and does not change scan-result persistence.

### Verification Scope

- Verify that the application identifies itself as `0.1.3.rev2`, the PS5 plugin as `0.1.0.rev10`, the Mock plugin as `1.0.0.rev2`, and Plugin API as `1.3.0`.
- Run the deterministic verification executable and require all 22 checks to pass.
- With the PS5 plugin active, confirm that Value Type contains exactly its eleven implemented ps5debug-NG types and Scan Type contains only `Exact Value`; unsupported Core catalog entries must not appear.
- Confirm that Value Type locks after First Scan, New Scan restores the First Scan selection state, and Scan Type is filtered by its Core First/Next-stage metadata while remaining selectable whenever the active plugin exposes more than one applicable mode.
- Verify disabled-state contrast in Light, Dimmed, and Dark themes, including ordinary buttons, disabled ComboBoxes, the pause-scan CheckBox when unavailable, and the disabled Scan Results -> **Change value** context-menu item for non-Int32 results.
- Re-run representative Exact Value First/Next Scan and existing regression checks to confirm that the capability-driven lists did not change the verified PS5 scanner behavior.
- Native Windows compilation and the 22-check executable must be run on the Windows development machine because the source-preparation environment does not contain the .NET SDK/WPF toolchain.

## TeeKay87's Memory Engine 0.1.3.rev1 - PS5 Value Type Expansion

### Added

- Expanded the Exact Value scanner to the complete ps5debug-NG scan value-type set instead of exposing only signed Int32. The supported types are `UInt8`, `Int8`, `UInt16`, `Int16`, `UInt32`, `Int32`, `UInt64`, `Int64`, `Float`, `Double`, and `Array of Bytes`, corresponding to ps5debug-NG wire value-type ids `0` through `10`.
- Added shared Core `MemoryScanValue` and `MemoryScanValueCodec` models so scan values now carry their value type, encoded bytes, display text, width, and alignment instead of assuming a single `int` throughout the scanner.
- Added target-endian parsing and encoding for all integer and floating-point scan types. Signed and unsigned integer types accept decimal values and `0x`-prefixed hexadecimal input; Float and Double use invariant decimal notation.
- Added exact Array of Bytes input using hexadecimal byte sequences such as `DE AD BE EF`, compact forms such as `DEADBEEF`, and comma/hyphen/space separators. The current resident TurboScan boundary is 4,096 bytes. This revision uses an all-ones ps5debug-NG mask, so wildcard/masked AOB syntax is intentionally not exposed yet.
- Added a functional Value Type selector to the Scan panel. It is available before First Scan and locked after a scan session begins so Next Scan cannot silently change the active type. New Scan unlocks the selector again.
- Added value-type-aware Scan Results display for current value, previous value, and the Type column.
- Added PS5 TurboScan wire mapping for all eleven ps5debug-NG value types, dynamic START/COUNT value widths, Array of Bytes mask transmission, dynamic GET record parsing, and payload-bounded GET batching for large byte-array values.
- Added shared-Core regression coverage across the complete ps5debug-NG value-type set and PS5 protocol coverage that exercises all wire value-type ids, including Array of Bytes mask framing. The verification executable now contains 19 checks.

### Changed

- Advanced the host application from the completed/verified `0.1.2` milestone to `0.1.3.rev1`.
- Updated the PS5 plugin from `0.1.0.rev8` to `0.1.0.rev9`. Plugin API remains `1.2.0` because all required `MemoryValueType` values and the generic `NativeValueScanRequest` contract already existed; no public Plugin SDK contract change was required.
- Generalized the shared Core First Scan and Next Scan implementations from Int32-specific matching to type/width/alignment-aware Exact Value matching while retaining the existing Int32 methods as compatibility wrappers.
- Generalized PS5 TurboScan segment construction so the full value width is respected for every selected type. Large protocol segments are partitioned by candidate-start range so multi-byte Array of Bytes values are not lost at artificial `uint32` segment boundaries.
- Float and Double continue to use native TurboScan First Scan, but PS5 resident Next Scan refinement intentionally falls back to shared Core. Current ps5debug-NG uses fuzzy floating-point equality in resident refinement, so using it for Memory Engine's Exact Value mode would change the requested comparison semantics. Integer and exact Array-of-Bytes refinement remain native.
- PS5 TurboScan GET now derives record size from the resident value width rather than assuming the former 16-byte Int32 record. Batch size is also bounded by a 4 MiB payload target so large Array of Bytes values cannot create oversized temporary buffers.
- Scan status text and Value-field help now identify the selected Value Type instead of describing every scan as a four-byte signed integer scan.
- Scan Results -> **Change value** remains connected to the temporary Raw Memory Write diagnostic only for signed Int32 results. The command is disabled for other result types because Raw Memory Write remains intentionally limited to one signed Int32 value and must not reinterpret wider, floating-point, unsigned, or byte-array results incorrectly.

### Fixed

- Removed the hard-coded four-byte assumptions from native result validation, native refinement request framing, result display, and shared scanner matching.
- Preserved exact candidate coverage for values wider than their scan alignment, including Array of Bytes scans with one-byte candidate alignment.
- Prevented Value Type changes during an active scan session, avoiding native-session/type-width mismatches between First Scan and Next Scan.

### Preserved

- The fully verified `0.1.2.rev5` scan workflow remains intact: Enter in Value runs the highlighted First/Next action, scan-button emphasis follows session state, New Scan resets the workflow, Danger buttons remain theme-controlled, and the PS5 plugin still prefers `eboot.bin` without automatically changing Active Target.
- PS5 TurboScan First Scan acceleration, resident TurboScan Next Scan refinement, process pause/resume, safe deferred cancellation, raw memory access, Safe Write Test, theme behavior, process selection, plugin discovery, and Mock functionality remain intact.
- Exact Value remains the only scan comparison mode in this milestone. Unknown Initial Value, Changed/Unchanged, Increased/Decreased, range comparisons, and other comparative modes remain future work.
- Raw Memory Write remains the verified signed Int32 diagnostic; this revision does not broaden write semantics as a side effect of broadening scan value types.

### Verification Scope

- `0.1.2.rev5` was completed before this version transition: the Windows verification executable passed all 17 checks and the live PS5/UI workflow was reported working as intended, establishing `0.1.2` as the verified baseline for `0.1.3`.
- Source preparation for this revision verifies application/plugin version boundaries, the eleven ps5debug-NG value-type mappings, generic scanner/parser wiring, Array of Bytes mask framing, Value Type selector bindings, XAML/JSON/project-file syntax, and release-package integrity.
- Native Windows compilation and the 19-check verification executable must be run on the Windows development machine because the source-preparation environment does not contain the .NET SDK/WPF toolchain. The PS5 value-type protocol coverage also verifies that Float/Double resident refinement requests a safe Core fallback instead of using ps5debug-NG's fuzzy floating-point refinement semantics.
- Live PS5 verification should exercise representative signed/unsigned integer widths, Float, Double, and Array of Bytes through First Scan and Next Scan while confirming the established rev5 workflow remains unchanged.

## TeeKay87's Memory Engine 0.1.2.rev5 - Scanner Workflow and Turbo Refinement

### Added

- Added the optional Plugin SDK `INativeValueScanRefiner` contract for target-side refinement of an existing native value-scan result set. The host Plugin API is now `1.2.0`; existing `1.0.x`/`1.1.x` plugins remain compatible under the established same-major/older-minor rule.
- Added PS5 TurboScan resident Next Scan refinement through `CMD_PROC_TURBOSCAN_COUNT` (`0xBDAACC12`) and `CMD_PROC_TURBOSCAN_GET` (`0xBDAACC13`). A successful PS5 First Scan now keeps its server-resident survivor set available for subsequent Exact Value narrowing instead of ending the TurboScan session immediately.
- Added optional use of ps5debug-NG `TS_RESCAN_ALIASING` when the connected server advertises the corresponding rescan-aliasing engine bit. The normal resident refinement path remains valid when that optional engine is absent.
- Added a generic preferred-process selection step through the existing `IForegroundProcessProvider` contract. The PS5 plugin now advertises `ForegroundProcess` and returns `eboot.bin` when that process exists, allowing the host to select it automatically in **Target Process** without making it the Active Target.
- Added a scan submit command used by the Value field keyboard workflow. Pressing Enter in the Scan **Value** field now runs First Scan when no scan session exists and Next Scan after a successful First Scan.
- Added explicit scan-action presentation state so the theme-primary color follows the next logical scan action: First Scan before a session exists, then Next Scan after First Scan succeeds. **New Scan** returns the primary action to First Scan.
- Added deterministic protocol coverage for PS5 resident TurboScan refinement and preferred `eboot.bin` process discovery. The verification executable now contains 17 checks.

### Changed

- Updated the PS5 plugin from `0.1.0.rev7` to `0.1.0.rev8` and its target Plugin API from `1.1.0` to `1.2.0`.
- PS5 First Scan still uses the rev4 multi-segment server-resident TurboScan path, but the resident session is now intentionally retained after a successful scan so the next Exact Value scan can narrow it directly on the target.
- PS5 Next Scan now prefers `INativeValueScanRefiner` when available. The previous shared Core/read-based Next Scan implementation remains unchanged and is used automatically when no compatible native refinement session exists or the native session can no longer be used safely.
- Native PS5 First Scan and native PS5 Next Scan use indeterminate progress presentation because the current resident Exact Value list path does not provide a meaningful percentage stream. Shared Core fallback scans continue to display determinate percentage progress.
- **New Scan** now releases any active native resident scan session before clearing host-side candidates. Changing the Active Target also releases an available native resident scan session before switching targets.
- **Cancel Scan** now reports `Cancellation requested — waiting for the current target scan operation to finish safely...` immediately after the request. This reflects the ps5debug-NG framing requirement that an already-started streamed transaction must reach a clean protocol boundary before cancellation can return.
- `Disconnect` and `Cancel Scan` now use the existing theme-owned `DangerButtonStyle`. The bundled theme danger palettes remain the standard red stop/cancel treatment and can be independently recolored by custom themes.
- The Scan **Value** tooltip now documents the Enter-key workflow.

### Fixed

- Fixed scan-action emphasis so First Scan and Next Scan are no longer both shown as primary actions at the same time.
- Fixed native scan-session lifetime management so successful First Scan results can be refined natively, while cancelled/failed/mismatched native operations close the resident session before returning to the host or falling back to Core.
- Fixed the PS5 memory-write verification documentation's stale "current plugin" reference so current-version instructions no longer identify `0.1.0.rev6` as the current PS5 plugin.

### Preserved

- The shared Core 4-byte Int32 Exact Value First Scan and Next Scan algorithms remain available and unchanged as the compatibility fallback.
- Rev4 PS5 First Scan acceleration, transaction-safe cancellation, process pause/resume, raw read/write, Safe Write Test, Scan Results context-menu behavior, themes, plugin discovery, and Mock functionality remain intact.
- **Pause target while scanning** remains Off by default and continues to resume the Active Target from the scan cleanup path after completion, cancellation, or ordinary failure.

### Verification Scope

- Source preparation verifies version/API consistency, the new contract/service wiring, TurboScan COUNT/GET framing, preferred-process capability wiring, XAML/JSON/XML validity, and release-package integrity.
- Native Windows compilation and the 17-check verification executable must be run on the Windows development machine because the source-preparation environment does not contain the .NET SDK/WPF toolchain.
- Live PS5 verification should confirm Enter-driven scan flow, dynamic scan-button emphasis, automatic `eboot.bin` Target Process selection, theme-aware danger buttons, resident TurboScan Next Scan timing/correctness, fallback behavior, and safe deferred cancellation.

## TeeKay87's Memory Engine 0.1.2.rev4 - PS5 Scan Acceleration and Process Pause

### Added

- Added the public Plugin SDK `INativeValueScanner` service contract for optional target/plugin-side value-scan acceleration. The contract receives the neutral readable memory-region set selected by Core so platform implementations can preserve the same scan boundaries as the shared scanner.
- Added the public Plugin SDK `IProcessControl` service contract for neutral process suspend/resume operations.
- Added PS5 TurboScan capability probing through `0xBDAACC10`. The connected PS5 session exposes `INativeValueScanner` only when the server advertises TurboScan protocol version 1 or newer together with server-resident result storage and multi-segment support.
- Added ps5debug-NG scan authorization through `CMD_PROC_AUTH` (`0xBDAACCFF`) using scan flag `0x02` and the documented challenge/XOR-keystream handshake.
- Added PS5 accelerated First Scan through TurboScan START/GET/END (`0xBDAACC11`, `0xBDAACC13`, `0xBDAACC14`). Readable neutral memory regions are sent as disjoint segments and matches remain server-resident while the host retrieves only result records.
- Added PS5 `CMD_DEBUG_PROCESS_STOP` (`0xBDBB0500`) support with state `1` for suspend and state `0` for resume.
- Added capability-driven **Pause target while scanning** to the Scan panel. The option is Off by default, is shown only for plugins that advertise both `ProcessSuspend` and `ProcessResume`, and is locked while a scan is active.
- Added an indeterminate status-bar progress mode for target-side scans that do not expose incremental percentage progress while preserving live elapsed-time reporting.
- Added deterministic protocol verification for TurboScan capability negotiation, scan authorization, multi-segment resident exact-value scanning, GET result retrieval, END cleanup, cancellation-safe session reuse, and process suspend/resume.
- Added dedicated rev4 verification documentation and PS5 native scan/process-control documentation.

### Fixed

- Fixed the Scan Results context menu in Dimmed/Dark themes by replacing WPF's default `ContextMenu`, `MenuItem`, and `Separator` chrome. The operating-system light icon/checkmark gutter no longer appears beside menu items.
- Preserved command-stream synchronization when cancelling a PS5 accelerated scan. Once a TurboScan protocol transaction starts, its current response is drained to a defined command boundary and the resident scan session is closed with END before cancellation returns.
- Avoided using the legacy `CMD_PROC_SCAN` (`0xBDAA0009`) path for Memory Engine acceleration. Rev4 instead uses TurboScan's multi-segment server-resident protocol so the plugin can represent the host-selected disjoint memory regions directly and retrieve the corresponding absolute survivor addresses.

### Changed

- PS5 First Scan now prefers the plugin's `INativeValueScanner` acceleration path when TurboScan support is negotiated at connection time. The existing Core `IMemoryReader` scanner remains the platform-neutral fallback and continues to be used by Mock, older ps5debug-NG servers, and resource-fallback cases.
- Native PS5 First Scan now sends the neutral readable/non-guarded scan regions to the plugin. Core still owns scanner semantics and normalizes returned addresses into the same `MemoryScanResult` model, including alignment, containment, de-duplication, and the common result limit.
- Added automatic fallback to the shared Core First Scan when the PS5 native service reports that the current scan cannot use server-resident acceleration. The visible scan workflow does not change.
- Next Scan remains the shared Core candidate-refinement implementation. This preserves the already verified effectively-instant refinement behavior from the real-console rev3 test.
- Process pause is performed through `IProcessControl` and wrapped around both First Scan and Next Scan with a resume attempt in `finally` after success, cancellation, or failure. Resume failures are surfaced to the user.
- Plugin API advanced from `1.0.0` to `1.1.0` because rev4 adds two public optional service contracts. Existing `1.0.0` plugins remain compatible under the established same-major/older-minor compatibility rule.
- PS5 plugin revision advanced from `0.1.0.rev6` to `0.1.0.rev7` and now targets Plugin API `1.1.0`.
- Recorded completed rev3 runtime verification: all 13 automated checks passed; WPF startup/progress worked; Cancel -> Refresh/New Scan reused the same live PS5 session successfully; a real Int32 First Scan for `10002` returned 266 results from a 10,105-region target in approximately `07:04.3`; and the subsequent Next Scan completed effectively instantly. This measurement motivated the rev4 target-side First Scan acceleration.

### Removed

- Removed reliance on WPF's default context-menu icon/checkmark gutter and separator rendering.
- Removed the proposed legacy `CMD_PROC_SCAN` acceleration implementation before release of rev4; it was replaced by the negotiated TurboScan path during final protocol review.
- No previously verified target-access, scanner-refinement, raw-memory, theme, splitter, or workspace functionality was removed.

### Version and Compatibility Notes

```text
Host application:             0.1.2.rev4
PlayStation 5 plugin:         0.1.0.rev7
In-Memory Test Target plugin: 1.0.0.rev1
Plugin API:                   1.1.0
```

The Mock plugin intentionally remains at Plugin API `1.0.0`; this verifies that a host exposing Plugin API `1.1.0` continues to accept plugins built against an older minor version in the same major compatibility family.

### Verification

Source/static verification for the packaged revision includes project/XML/XAML parsing, theme JSON validation, documentation-link checks, version-boundary checks, public contract/service wiring review, protocol-fixture review, and package byte comparison. The preparation environment does not provide the .NET/WPF SDK, so the Windows build and executable verification must be performed on the development machine.

The verification executable contains 16 checks. A successful run ends with:

```text
All 16 checks passed.
```

Live PS5 verification must measure the accelerated First Scan against the rev3 `07:04.3` baseline, confirm that Next Scan remains fast, verify Cancel -> Refresh/New Scan without reconnecting, and test **Pause target while scanning** for normal completion and cancellation. See `docs/testing/APP_0.1.2_REV4_VERIFICATION.md`.


## TeeKay87's Memory Engine 0.1.2.rev3 - Scan Progress Binding Fix

### Fixed

- Fixed a WPF startup failure introduced with the rev2 status-bar scan-progress presentation. `ProgressBar.Value` was bound to the output-only `PluginViewModel.ScanProgressPercentage` property without an explicit binding mode, causing WPF to reject the binding as TwoWay/OneWayToSource against a property with no public setter.
- Changed the progress binding to explicit `Mode=OneWay`, matching the intended data flow: scanner/ViewModel code owns progress updates and the progress bar only displays them.
- Preserved the existing private setter on `ScanProgressPercentage`; the ViewModel encapsulation was not weakened merely to satisfy WPF binding behavior.

### Changed

- Advanced the host revision from `0.1.2.rev2` to `0.1.2.rev3` while remaining in the same initial scanner milestone.
- Updated `AppInfo.FeatureTitle` to `Scan Progress Binding Fix`.
- Updated README current-state/version text and UI/testing documentation to record the binding contract and the rev2 Windows runtime result.
- Added `docs/testing/APP_0.1.2_REV3_VERIFICATION.md` with startup, progress, regression, and deferred live-PS5 cancellation verification steps.

### Removed

- Removed no scanner feature, result action, status-bar feature, plugin capability, protocol command, theme behavior, workspace element, or target-access functionality.

### Version and Compatibility Notes

- Host application: `0.1.2.rev3`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev6`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`.
- Core scanner code, PS5 transport code, Plugin SDK contracts, and both plugin capability sets are unchanged from rev2.

### Verification

- Started from the complete `TK87ME_0.1.2.rev2___Scanner-Workflow-and-Cancellation-Reliability.zip` baseline produced from the user's rev1 codebase and reviewed README, CHANGELOG, all Markdown under `docs/`, and the complete source/project/theme/test tree before modifying code.
- Recorded the user's Windows result that the rev2 verification executable completed successfully with **All 13 checks passed**.
- Recorded the subsequent rev2 WPF startup failure: `System.InvalidOperationException` reported that a TwoWay or OneWayToSource binding cannot work on the read-only `ScanProgressPercentage` property.
- Confirmed the only source change required to correct that exception is the status-bar `ProgressBar.Value` binding in `MainWindow.xaml`; it now explicitly specifies `Mode=OneWay`.
- Confirmed `PluginViewModel.ScanProgressPercentage` remains output-only to the view through a private setter.
- Confirmed the previously corrected output-only Raw Memory Read and Raw Memory Write result TextBoxes remain explicitly OneWay-bound.
- Confirmed editable TextBox inputs remain TwoWay where required.
- The preparation environment does not provide a Windows WPF runtime, so native WPF startup is not claimed here. Windows rebuild, the existing 13-check verification executable, WPF startup, progress presentation, and the live PS5 Cancel -> Refresh/New Scan closure remain required runtime verification.

## TeeKay87's Memory Engine 0.1.2.rev2 - Scanner Workflow and Cancellation Reliability

### Added

- Added scan progress presentation to the application status bar. Active First Scan and Next Scan operations now expose a percentage-based progress bar derived from the shared Core `MemoryScanProgress` values and an elapsed-time counter updated while the scan is running.
- Added final elapsed-time retention after scan completion or cancellation so the duration remains visible until the scan session is reset with New Scan, Active Target replacement, disconnect, or equivalent scan-state cleanup.
- Added a Scan Results row context menu with **Copy address**, **Copy value**, and **Change value** actions. Copy operations place the rendered result address/value on the Windows clipboard. Change value transfers the selected result address to the temporary Raw Memory Write diagnostic without carrying over a potentially stale write value.
- Added application-owned `ContextMenu` and `MenuItem` styles so the new Scan Results menu follows the active Light/Dimmed/Dark palette instead of falling back to unrelated operating-system colors.
- Added deterministic PS5 regression coverage for scan cancellation while a `CMD_PROC_READ` transaction is in flight. The fixture cancels the shared Core scan after the read request has reached the loopback ps5debug-NG server, then immediately performs process enumeration on the same session and requires that command to succeed.
- Extended the ps5debug-NG test server with a controllable delayed memory-read response and a follow-up process-list transaction so command-stream synchronization can be verified without a physical console.
- Added `docs/testing/APP_0.1.2_REV2_VERIFICATION.md` with the Windows, deterministic, UI, and live-PS5 verification procedure for this revision.

### Fixed

- Fixed a live PS5 cancellation defect discovered during the first real-console scanner test. Cancelling a scan could previously cancel `NetworkStream.ReadExactlyAsync` in the middle of a ps5debug-NG memory-read response. Unconsumed response bytes then remained on the shared TCP stream, causing the next process-list/read/map command to interpret target memory bytes as a command status. The observed symptom was an unexpected status such as `0xC0F6410A`, and Disconnect -> Connect restored operation only because it created a new TCP session.
- Fixed PS5 read cancellation boundaries so a `CMD_PROC_READ` transaction is now treated as one framed command once it has started. Caller cancellation is checked before sending the request; after the command begins, the request, success status, and complete requested payload are consumed before control returns. The Core scanner then observes cancellation before issuing its next read request.
- Applied the same transaction-preservation rule to the already two-phase `CMD_PROC_WRITE` path: cancellation is checked before the command starts, then both success acknowledgements and the complete payload exchange are allowed to finish so a future caller cannot leave the PS5 command stream desynchronized.
- Fixed Scan Results address presentation so values no longer contain unnecessary fixed-width leading zeroes. Addresses now render in compact hexadecimal form such as `0x10000104` while retaining the `0x` prefix and full significant address value.

### Changed

- Advanced the host revision from `0.1.2.rev1` to `0.1.2.rev2` while remaining in the same initial scanner milestone.
- Updated `AppInfo.FeatureTitle` to `Scanner Workflow and Cancellation Reliability`.
- Updated the PlayStation 5 plugin independently from `0.1.0.rev5` to `0.1.0.rev6` because the PS5 command-transport implementation changed to preserve protocol framing across cancellation. The capability set is unchanged.
- Moved scan status and scan error presentation out of the right-side Scan panel and into the permanent application status bar. The Scan panel now remains focused on user inputs/actions while current scan state, progress, elapsed time, and failures are visible globally at the bottom of the window.
- Changed the temporary Raw Memory Write diagnostic from arbitrary hexadecimal-byte entry to one signed **4 Bytes / Int32** value entered in decimal form. The host converts the decimal Int32 to exactly four bytes according to `TargetArchitecture.Endianness` before invoking the unchanged neutral `IMemoryWriter` contract.
- Updated Safe Write Test presentation to populate the decimal write-value field by decoding the selected unchanged four-byte pattern using the active target's endianness. The actual safety behavior remains same-byte write-back and immediate byte-for-byte verification.
- Updated scanner result presentation so the Address column can use less horizontal space after removal of fixed-width leading zeroes.
- Updated README, scanner architecture documentation, main-workspace documentation, PS5 plugin documentation, protocol mapping, and verification documents for the current cancellation semantics, status-bar workflow, result actions, compact addresses, and decimal diagnostic writes.

### Removed

- Removed the old scan status/error text rows from beneath the Scan buttons; the same information is now surfaced in the application status bar.
- Removed the temporary Raw Memory Write UI's arbitrary hexadecimal-byte parser and its 4096-byte manual-write input limit. The underlying `IMemoryWriter` contract remains byte-oriented and unrestricted by this UI change; the current diagnostic surface deliberately edits one signed Int32 value.
- Removed no shared scanner algorithm, scan-result candidate, plugin capability, Plugin SDK contract, Mock behavior, verified PS5 command, theme, splitter, control-height rule, Raw Memory Read diagnostic, Safe Write Test behavior, or Saved Addresses separation.

### Version and Compatibility Notes

- Host application: `0.1.2.rev2`.
- PlayStation 5 plugin: `0.1.0.rev6`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`; no public Plugin SDK contract changed.
- The shared scanner remains in Core and continues to use `IMemoryReader`, `MemoryRegion`, `TargetProcess`, and `TargetArchitecture`. The PS5 cancellation fix is isolated inside the PS5 transport implementation and does not add a platform-specific scanner branch.
- Existing theme JSON schema is unchanged from rev1.

### Verification

- Started from the user-supplied `TK87ME_0.1.2.rev1___Initial-Memory-Scanner-Foundation(1).zip` codebase/current rev1 baseline and re-read README, CHANGELOG, all Markdown under `docs/`, and the complete source/project/theme/test tree before modifying code.
- Recorded rev1 runtime results supplied before this revision: the verification executable returned **All 12 checks passed**; Mock First Scan found `0x10000104 = 30`; Raw Memory Write changed that value to `25`; Next Scan retained the correct address with Current = 25 and Previous = 30; New Scan, Cancel Scan, and the updated theme/button/tooltip presentation were initially reported working in normal Mock/UI use.
- Recorded the first live PS5 scanner success: the shared 4-byte Exact Value scanner found a real in-game money value and the value could be changed successfully through the existing memory-access chain.
- Recorded the subsequently isolated live PS5 cancellation failure: after Cancel Scan, a new scan/process refresh could fail with a malformed/unexpected ps5debug-NG status until Disconnect -> Connect. This runtime observation is the regression specifically addressed by the transport change in this revision.
- Added a thirteenth deterministic verification check named **PS5 scan cancellation preserves command stream**. The test cancels while a memory-read response is intentionally delayed, then verifies `IProcessProvider.GetProcessesAsync` succeeds immediately on the same session.
- Confirmed the modified PS5 client checks cancellation before starting a read/write command and uses a non-cancellable transport token only for completion of the already-started framed command transaction.
- Confirmed the new Raw Memory Write decimal codec uses `TargetArchitecture.Endianness` rather than assuming little-endian globally.
- Confirmed Scan Results addresses retain their complete numeric value while omitting only redundant leading zeroes.
- Confirmed the context-menu **Change value** action transfers only the result address and clears the write-value field before user input.
- The preparation environment does not provide the .NET Windows/WPF SDK toolchain, so no native Windows build/runtime result is claimed for `0.1.2.rev2`. Windows **Build -> Rebuild Solution**, the verification executable (expected **All 13 checks passed**), and the live PS5 Cancel Scan -> immediate session-reuse procedure remain required before this revision is runtime-verified.

## TeeKay87's Memory Engine 0.1.2.rev1 - Initial Memory Scanner Foundation

### Added

- Added the first shared Core memory scanner under `src/TeeKay87.MemoryEngine.Core/Scanning/`. The scanner is platform-neutral and consumes the existing `TargetProcess`, `MemoryRegion`, `TargetArchitecture`, and `IMemoryReader` contracts rather than introducing PS5-specific scan behavior.
- Added the first usable scan combination: **4 Bytes / Int32 + Exact Value**. First Scan reads eligible target memory, decodes Int32 values using the active target's declared endianness, and retains matching four-byte-aligned addresses as temporary scan candidates.
- Added **Next Scan** exact-value refinement over the complete previous candidate set. Refinement reads only memory chunks that contain existing candidates, updates Current Value, preserves the preceding value as Previous Value, and retains only candidates matching the new exact Int32 value.
- Added **New Scan** to clear the temporary scan session without affecting the separate Saved Addresses workspace.
- Added **Cancel Scan** and scan-operation cancellation tokens. A cancelled First Scan does not publish a partial result set; a cancelled Next Scan preserves the previous complete candidates.
- Added scan progress/status reporting and scan error presentation to the permanent right-side Scan panel.
- Added bounded 256 KiB scanner reads, explicit four-byte alignment, readable/non-Guard region filtering, a 2,000,000-result safety limit, and a stop condition after 32 target read failures so a stale/disconnected target cannot cause unbounded repeated failing requests.
- Added Core scan models `MemoryScanResult`, `MemoryScanExecutionResult`, `MemoryScanProgress`, and `ScanResultLimitExceededException`. These remain in Core rather than Plugin SDK because ordinary scan algorithms/session state do not cross the plugin boundary.
- Added `ScanResultViewModel` and live Scan Results bindings for Address, Value, Previous, Type, and Region / Module. The WPF grid uses row/column virtualization and materializes at most the first 50,000 presentation rows while Core retains/refines the complete candidate set.
- Added deterministic verification coverage for the shared scanner using the Mock target: First Scan finds Ammo = 30, the test writes Ammo = 25 through the existing neutral `IMemoryWriter`, and Next Scan confirms the same address survives with Current = 25 and Previous = 30.
- Added `docs/architecture/MEMORY_SCANNER_FOUNDATION.md` documenting scanner ownership, chunking, alignment, endianness, candidate storage, failure handling, cancellation, and planned expansion.
- Added `docs/testing/APP_0.1.2_REV1_VERIFICATION.md` with Windows, Mock, live-PS5 scanner, theme, preservation, and pass-criteria procedures.
- Added independent theme palette entries for Primary button background/border/text and Secondary button background/border/text. Danger buttons continue to use their existing dedicated danger palette.
- Added theme palette entries for tooltip/hint background, border, and text.
- Added an application-owned implicit WPF `ToolTip` template so hover hints use theme resources instead of falling back to operating-system tooltip chrome.

### Fixed

- Fixed Dimmed/Dark hover hints appearing as bright operating-system white tooltips with very low-contrast text by routing tooltip background, border, and text through the active application theme.
- Fixed Primary action colors being coupled to the general `Accent` resource and Secondary button colors being coupled to generic panel/text resources. Themes can now tune button surfaces/text independently without recoloring unrelated UI.
- Adjusted the bundled Light theme's Primary button palette to a lighter cyan surface with high-contrast dark text so colored actions no longer appear excessively dark in Light mode.

### Changed

- Advanced the host semantic version from `0.1.1.rev17` to `0.1.2.rev1` because the complete `0.1.1` low-level target-access milestone is now live-verified and the project is beginning the shared scanner milestone.
- Updated `AppInfo.FeatureTitle` to `Initial Memory Scanner Foundation`.
- Connected the existing permanent Scan UI to real commands/state instead of placeholder disabled controls. The Value input is active for shared scanner-capable sessions; Scan Type and Value Type remain fixed/non-editable because Exact Value + 4 Bytes is the only implemented combination in this revision.
- Changed host operation coordination so Disconnect, process Refresh, Set Active Target, Raw Memory Read, Raw Memory Write, and Safe Write Test cannot overlap an active scan.
- Changed Active Target/process cleanup to clear temporary scan state when the target disappears or a different Active Target is selected.
- Updated README to describe the complete current scanner behavior, result limits, theme palette contract, and completed low-level PS5 verification rather than treating the Scan controls as placeholders.
- Updated `docs/ui/THEMES.md`, `docs/ui/BUTTON_STYLES.md`, and `docs/ui/MAIN_WORKSPACE.md` for independently themed buttons, themed tooltips/hints, and the functional scanner workspace.
- Updated rev17 and PS5 memory-write verification documentation with the real-console result supplied before this revision: the Safe Write Test was run twice against `eboot.bin` and returned `Verification: PASS` both times.

### Removed

- Removed the disabled-placeholder behavior from the initial Value / First Scan / Next Scan / New Scan workflow now that the first scanner implementation exists.
- Removed no Plugin SDK contract, Core plugin-host behavior, PS5 transport behavior, Mock target behavior, raw read/write diagnostic, Safe Write Test, theme id, splitter behavior, Saved Addresses separation, or standard 34-unit control-height rule.
- Removed no platform scanner capability claim because the generic Core scanner does not require plugins to advertise `NativeValueScanning`; PS5 continues to advertise only the target-access capabilities it actually implements.

### Version and Compatibility Notes

- Host application: `0.1.2.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev5`; no PS5 plugin code or protocol mapping changes are required for the generic scanner.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`; the scanner reuses existing public memory/process/architecture contracts and stays in Core.
- Existing external custom themes based on the earlier palette schema must add the new Primary/Secondary button and ToolTip palette keys to load as complete themes under this revision. Bundled Light, Dimmed, and Dark files have all been updated together.

### Verification

- Started from the exact user-supplied complete `TK87ME_0.1.1.rev17___Automatic-Safe-Write-Test(1).zip` baseline.
- Re-read README, CHANGELOG, every Markdown file under `docs/`, and reviewed the complete C#/XAML/project/theme/test source tree before modifying code.
- Recorded the supplied real-PS5 rev17 evidence: 9,301 memory regions loaded for `eboot.bin`; Safe Write Test selected `0xA069FFC`; Original/Requested/Read-back were `00 00 00 00`; `Verification: PASS`; the test was repeated twice successfully.
- Confirmed the scanner implementation lives in Core and references no PS5 project/type/command.
- Confirmed PS5 plugin version/capabilities and Plugin API remain unchanged.
- Confirmed scanner decoding uses `TargetArchitecture.Endianness` and the generic host requires `MemoryRead + MemoryRegionEnumeration` rather than platform identity.
- Confirmed theme JSON, XAML resources, MainWindow XAML, and project XML parse successfully after the palette/template changes.
- Added deterministic Mock scanner coverage to the existing verification executable.
- The preparation environment does not provide the .NET Windows/WPF SDK toolchain, so no native Windows build or runtime result is claimed for `0.1.2.rev1`. Windows **Build -> Rebuild Solution**, the verification executable, and live PS5 scanner/theme verification remain required before this revision is considered runtime-verified.

## TeeKay87's Memory Engine 0.1.1.rev17 - Automatic Safe Write Test

### Added

- Added an explicit **Safe Write Test** action to the temporary Raw Memory Write diagnostic area. The action is available only when the active plugin/session provides memory-map enumeration, memory reads, and memory writes.
- Added generic host-side automatic candidate selection using the already cached neutral `MemoryRegion` list. Candidate regions must be readable and writable and must not be executable or guarded.
- Added a conservative four-byte stability check before any automatic test write. The host samples an interior address repeatedly with short delays and rejects candidates whose bytes change between reads.
- Added a final pre-write read and equality check immediately before the write. If the candidate changed after the stability samples, the test aborts without invoking `IMemoryWriter`.
- Added same-byte write-back verification for the automatic test. The bytes read immediately before the write are written back unchanged, then read again and compared byte-for-byte. This verifies the live write transport without intentionally changing the target value.
- Added automatic population of the Raw Memory Write Address and Bytes fields with the selected test address and bytes so the exact diagnostic operation remains visible to the user.
- Added `docs/testing/APP_0.1.1_REV17_VERIFICATION.md` documenting the purpose, safety boundaries, version boundaries, preservation requirements, and Windows/live-target verification procedure for this host-only diagnostic helper.

### Fixed

- Fixed the rev16 live-verification usability gap where the user was instructed to choose a known safe readable/writable address even though the current UI does not expose the memory-region list or otherwise provide a practical way to identify such an address.

### Changed

- Updated the host application revision from `0.1.1.rev16` to `0.1.1.rev17`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Automatic Safe Write Test`.
- Extended the Raw Memory Write diagnostic row with a secondary **Safe Write Test** button while preserving the existing manual **Write + Verify** workflow.
- Updated README and PS5 write-verification documentation so the preferred real-console protocol verification no longer requires manual address discovery.
- Updated the rev16 verification document with a forward note directing rev17-or-newer runtime testing to the automatic Safe Write Test while preserving rev16 historical implementation status.
- Updated host memory-command state notifications so the new Safe Write Test command follows the same connection, Active Target, memory-map, read/write-busy, refresh, and disconnect coordination as the existing raw-memory commands.

### Removed

- Removed no application feature, Plugin SDK contract, Core behavior, PS5 transport behavior, Mock-plugin behavior, theme, splitter behavior, shared control-height rule, TextBox padding fix, scanner placeholder, Saved Addresses placeholder, or existing manual Raw Memory Read/Write functionality.
- No automatic write occurs when a target is selected. The diagnostic write is still performed only after the user explicitly invokes **Safe Write Test**.

### Version and Compatibility Notes

- Host application: `0.1.1.rev17`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev5`; rev17 does not modify the ps5debug-NG backend or plugin capability set.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`; the implementation reuses the existing neutral `IMemoryMapProvider`, `IMemoryReader`, `IMemoryWriter`, `MemoryRegion`, and `MemoryProtection` contracts.
- The diagnostic helper is platform-neutral. Any future plugin exposing the required neutral capabilities can use the same host action without platform-specific UI branches.

### Verification

- Started from the exact user-supplied complete `0.1.1.rev16 - PS5 Raw Memory Write and Read-Back` ZIP.
- Re-read `README.md`, `CHANGELOG.md`, all 36 Markdown documents under the project/root documentation tree, and reviewed the complete C#/XAML/project/solution/theme source tree before modifying code.
- Confirmed the existing rev16 PS5 write implementation remains isolated in the PS5 plugin and that no new backend protocol work, Plugin SDK contract, or Core abstraction is required for automatic test-address selection.
- Confirmed the Safe Write Test filters on neutral protection flags only: `Read` and `Write` are required, while `Execute` and `Guard` are rejected.
- Confirmed the automatic test writes exactly the bytes observed immediately before the write; it does not deliberately mutate the target value and aborts before `IMemoryWriter` if the candidate becomes unstable.
- Confirmed the existing manual **Write + Verify** implementation remains available and unchanged in behavior.
- The preparation environment does not provide the .NET Windows/WPF SDK toolchain, so no native Windows build result is claimed. **Build -> Rebuild Solution**, the existing verification executable, and the real-PS5 Safe Write Test remain required on the Windows development machine.

## TeeKay87's Memory Engine 0.1.1.rev16 - PS5 Raw Memory Write and Read-Back

### Added

- Added PlayStation 5 raw process-memory writes through ps5debug-NG `CMD_PROC_WRITE` (`0xBDAA0003`). The PS5 command client serializes the documented packed 16-byte PID/address/length request, waits for the server's first success acknowledgement, sends exactly the requested raw bytes, and consumes the second completion status before returning.
- Added `IMemoryWriter` to `Ps5TargetSession` by reusing the public Plugin SDK contract that has existed since the architecture foundation; no PS5-specific write interface or wire model was added to the shared SDK.
- Added the `MemoryWrite` capability to the PlayStation 5 plugin now that the corresponding session service and backend protocol implementation exist.
- Added a capability-driven **Raw Memory Write** inspector to the WPF target area. It accepts a hexadecimal address and raw hexadecimal byte input while retaining the established 34-unit height for ordinary single-line TextBoxes.
- Added generic host-side writable-range validation using cached neutral `MemoryRegion` objects and `MemoryProtection.Write`. For map-capable targets, a write is blocked before the plugin call unless its complete range is contained in a writable Active Target region.
- Added generic write/read-back verification. When the selected range is also readable, the host captures the original bytes through `IMemoryReader`, performs the write through `IMemoryWriter`, reads the same range back, compares it byte-for-byte, and displays the original/requested/read-back values with `PASS` or `FAIL`.
- Added bounded hexadecimal byte parsing for manual writes, supporting compact or commonly separated input with optional `0x` prefixes. The interactive write inspector is limited to 4096 bytes; this is a host diagnostic-UI limit and does not change the neutral `IMemoryWriter` contract.
- Added deterministic loopback coverage for `CMD_PROC_WRITE`, including the packed request fields, both ps5debug-NG success acknowledgements, exact write payload reception, command-stream synchronization, and immediate neutral `IMemoryReader` read-back of the bytes written through `IMemoryWriter`.
- Added `docs/plugins/PS5/MEMORY_WRITE_VERIFICATION.md` with protocol, deterministic, Windows, safe live-console write/read-back/restore, and pass-criteria documentation.
- Added `docs/testing/APP_0.1.1_REV16_VERIFICATION.md` documenting the rev16 version boundaries, preservation requirements, static release checks, and pending Windows/live PS5 closure steps.

### Fixed

- No previously reported defect is targeted by this revision. Rev16 resumes the planned backend milestone after the rev15 TextBox presentation correction was runtime-verified successfully.

### Changed

- Updated the host application revision from `0.1.1.rev15` to `0.1.1.rev16`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `PS5 Raw Memory Write and Read-Back`.
- Updated the PlayStation 5 plugin independently from `0.1.0.rev4` to `0.1.0.rev5`.
- Extended the PS5 plugin description and capability set from read-only target-memory access to implemented raw read/write access.
- Changed host command-state coordination so manual reads and writes cannot overlap each other, and Disconnect, process Refresh, and Set Active Target remain unavailable while a write is in progress. This protects the existing explicit Active Target workflow from changing underneath a write transaction.
- Changed Active Target/process/disconnect cleanup so Raw Memory Write input/result/error state is reset together with the existing process, map, and read state.
- Updated README, PS5 plugin documentation, ps5debug-NG protocol mapping, Plugin SDK architecture notes, early-development architecture status, and main-workspace documentation to reflect the new write layer.
- Updated rev15 test documentation with the successful Windows runtime result supplied before rev16 work began: the TextBox fix is visually verified while the 34-unit control-height foundation remains intact.

### Removed

- Removed raw memory write from the PS5 plugin's documented unimplemented capability list because it is now implemented.
- Removed no public Plugin SDK contract, Core feature, Mock-plugin behavior, theme, splitter behavior, control-height rule, rev15 TextBox fix, process-selection/Active Target separation, scanner placeholder, Saved Addresses placeholder, or previously verified PS5 connection/process/map/read functionality.

### Version and Compatibility Notes

- Host application: `0.1.1.rev16`.
- PlayStation 5 plugin: `0.1.0.rev5`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`; `MemoryWrite` and `IMemoryWriter` already existed in the public contract, so no SDK compatibility change is required.
- The PS5 plugin continues to advertise only implemented host-visible capabilities: `Connect`, `ProcessEnumeration`, `MemoryRegionEnumeration`, `MemoryRead`, and `MemoryWrite`.
- The next major subsystem remains the shared Core memory scanner after rev16's controlled live PS5 write/read-back/restore verification is completed successfully.

### Verification

- Started from the exact user-supplied complete `0.1.1.rev15 - TextBox Content Padding Fix` ZIP and reviewed its root README, CHANGELOG, all Markdown documentation under `docs/`, and the complete code/resource/project tree before modifying the revision.
- Rechecked the current public ps5debug-NG protocol documentation before implementation. `CMD_PROC_WRITE` is `0xBDAA0003`, uses a packed `{ uint32 pid; uint64 address; uint32 length; }` body, requires one success response before the client streams the write bytes, and requires a second success response after the server consumes them.
- Preserved the existing Plugin SDK `IMemoryWriter` contract and implemented the backend entirely inside the PS5 plugin plus generic host behavior.
- Extended the deterministic protocol fixture so an immediate `CMD_PROC_READ` follows the write on the same TCP stream. This verifies both the transmitted bytes and that the final write acknowledgement is consumed rather than being left to corrupt the next command.
- Recorded the supplied runtime evidence that rev15 TextBox presentation is PASS and that rev11/rev12 remain live-verified on the real PS5, including the demonstrated `eboot.bin` session with 8,750 loaded regions and a successful 64-byte read at `0x400000`.
- Completed **484/484 static release checks successfully**, covering version separation, protocol/write-state invariants, XAML/project/theme parsing, project-reference resolution, deterministic fixture integration, local Markdown links, intended-file diff boundaries, preservation of unrelated verified files, release cleanliness, and lightweight C# delimiter-balance checks.
- The release-preparation environment does not include the .NET Windows/WPF SDK toolchain. No native build result is claimed here; **Build -> Rebuild Solution**, execution of the 11-check verification project, and the controlled real-PS5 write/read-back/restore test remain required on the Windows development machine.

## TeeKay87's Memory Engine 0.1.1.rev15 - TextBox Content Padding Fix

### Added

- Added `docs/testing/APP_0.1.1_REV15_VERIFICATION.md` documenting the reported TextBox clipping defect, the shared-template root cause, the centralized correction, version boundaries, static checks, and required Windows runtime verification.
- Added `docs/testing/APP_0.1.1_REV11_REV12_LIVE_PS5_VERIFICATION.md` as the authoritative combined result for the completed real-console rev11/rev12 verification. The document records successful non-zero PS5 memory-map enumeration, default and repeated raw reads, host-side invalid-range rejection, Active Target/map retention through refresh, and disconnect cleanup.
- Added completed live-result sections to the existing rev11, rev12, rev14, PS5 memory-map, and PS5 raw-memory-read verification documents so the documentation no longer describes those checks as pending.

### Fixed

- Fixed vertically clipped text in standard single-line WPF `TextBox` controls without increasing the established 34-unit control height. The defect was visible in plugin-defined connection inputs and the Raw Memory Read Address/Length inputs.
- Reduced the shared single-line TextBox internal padding from `9,6` to `9,3`, preserving the existing horizontal spacing while restoring six additional device-independent units to the usable text viewport.
- Changed the shared TextBox `PART_ContentHost` from a vertically centered host element to a stretched host that fills the remaining padded interior. This prevents the content viewport itself from being unnecessarily constrained inside a fixed-height control.
- Bound the content host's horizontal and vertical content alignment to the templated TextBox, allowing the existing `VerticalContentAlignment=Center` rule to center normal one-line text correctly while preserving deliberate local overrides such as the top-aligned multiline Raw Memory Read result surface.

### Changed

- Updated the host application revision from `0.1.1.rev14` to `0.1.1.rev15`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `TextBox Content Padding Fix`.
- Updated README/current UI documentation to describe the corrected TextBox content layout while retaining `UiMetrics.StandardControlHeight = 34`.
- Updated the current project status to record that the rev11 memory-map and rev12 raw-memory-read paths are now live-verified on a real PlayStation 5.
- Updated the early-development architecture milestone list so memory-map enumeration and raw memory read are marked implemented and live-verified rather than pending.

### Removed

- Removed no application feature, target operation, plugin capability, Plugin SDK contract, Core behavior, PS5 protocol behavior, Mock behavior, theme, splitter, scanner placeholder, Saved Addresses placeholder, or existing control-height rule.
- No TextBox outer height was changed. `UiMetrics.StandardControlHeight` remains exactly `34d`.

### Version and Compatibility Notes

- Host application: `0.1.1.rev15`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev4` because this revision changes only host presentation resources and documentation.
- Plugin API: unchanged at `1.0.0`; no public SDK contract changed.
- The next PS5 backend implementation remains raw memory write/read-back through the already existing neutral `IMemoryWriter` contract. Because rev15 is consumed by this UI correction, that backend work will use the next available host revision rather than the previously suggested rev15 number.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and reviewed the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev14` baseline before modifying code.
- Audited every current TextBox declaration and confirmed standard one-line fields inherit the single shared implicit TextBox style; no duplicate per-view template or height workaround was required.
- Confirmed the multiline Raw Memory Read result TextBox deliberately retains its local `Height=150`, `Padding=10,8`, and `VerticalContentAlignment=Top` settings.
- Confirmed `UiMetrics.StandardControlHeight` remains `34d` and Button/ComboBox templates, workspace geometry, splitters, themes, Core, Plugin SDK, both platform plugins, and protocol tests are unchanged from rev14.
- Completed **108/108** static release checks covering XML/XAML and JSON parsing, version separation, TextBox template invariants, rev14 binding preservation, byte-identical unrelated source/test baselines, local Markdown links, and release-tree cleanliness.
- Recorded the user's completed live rev11/rev12 real-PS5 verification as PASS in testing and PS5-plugin documentation.
- The preparation environment does not provide the .NET Windows/WPF SDK, so the rev15 TextBox presentation correction still requires Windows-side rebuild/startup and visual confirmation before the revision is considered runtime-verified.

## TeeKay87's Memory Engine 0.1.1.rev14 - Raw Memory Read Result Binding Fix

### Added

- Added `docs/testing/APP_0.1.1_REV14_VERIFICATION.md` documenting the rev13 WPF startup exception, the binding-mode root cause, the exact correction, and the combined rev11/rev12 live verification that remains to be completed after startup succeeds.
- Added the observed Windows rev13 runtime result to `docs/testing/APP_0.1.1_REV13_VERIFICATION.md`, recording that the rev13 compiler fix progressed successfully to WPF startup before the separate result-binding exception occurred.

### Fixed

- Fixed the Raw Memory Read result `TextBox` startup exception by changing its `MemoryReadResultText` binding from the implicit `TextBox.Text` TwoWay default to explicit `Mode=OneWay`.
- Preserved `MemoryReadResultText` as an output-only ViewModel property with a private setter instead of weakening ViewModel encapsulation merely to satisfy the WPF binding engine.
- Confirmed the editable Raw Memory Read address and length fields remain explicitly TwoWay and that no other current `TextBox` targets a read-only ViewModel property.

### Changed

- Updated the host application revision from `0.1.1.rev13` to `0.1.1.rev14`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Raw Memory Read Result Binding Fix`.
- Updated README/current-workspace documentation to identify `0.1.1.rev14` as the current host build.
- Updated rev12 verification history to record that rev13 fixed compilation but a separate WPF result-binding issue blocked the pending combined memory-map/raw-read live test until rev14.

### Removed

- Removed no application feature, Plugin SDK contract, Core behavior, PS5 protocol behavior, Mock behavior, theme, splitter behavior, control style, scanner placeholder, or saved-address placeholder.
- No raw-memory-read functionality was rolled back; the correction only changes the direction of the result-display binding.

### Version and Compatibility Notes

- Host application: `0.1.1.rev14`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev4` because no plugin code changed.
- Plugin API: unchanged at `1.0.0`.
- Core, Plugin SDK, both platform plugins, protocol fixtures, themes, splitters, and shared control resources remain functionally unchanged from rev13.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev13` baseline before modifying code.
- Traced the reported `InvalidOperationException` to the default TwoWay binding mode of `TextBox.Text` and confirmed `IsReadOnly=True` does not change that binding direction.
- Audited all current host `TextBox` bindings: editable connection/address/length values are writable and explicitly TwoWay where bound; the raw-memory result is the only read-only bound TextBox and is now explicitly OneWay.
- Confirmed no .NET SDK or Windows WPF build toolchain is available in the preparation environment; Windows-side **Build -> Rebuild Solution** and startup remain required.

## TeeKay87's Memory Engine 0.1.1.rev13 - Raw Memory Read Compile Fix

### Added

- Added `docs/testing/APP_0.1.1_REV13_VERIFICATION.md` with the exact Windows build failure observed in rev12, the root-cause analysis, the expected post-fix Designer recovery behavior, and the remaining combined rev11/rev12 runtime verification steps.
- Added the real Windows-side rev12 build result to `docs/testing/APP_0.1.1_REV12_VERIFICATION.md` so the failed compile is preserved as test documentation rather than being represented as a successful runtime verification.

### Fixed

- Fixed `CS0177` in `PluginViewModel.TryParseHexAddress(...)`. The previous short-circuit expression could return before `ulong.TryParse(..., out address)` executed, leaving the `out` parameter not definitely assigned when the address field was empty.
- Initialized the hexadecimal-address `out` value before input normalization and changed the empty-address path to an explicit `return false`, preserving the intended validation behavior while satisfying C# definite-assignment rules.
- Identified the accompanying Visual Studio XAML Designer errors for `UiMetrics`, `ProportionalGridSplitter`, `System.Object`, and `DependencyProperty.UnsetValue` as downstream design-time assembly-load failures caused by the host project's C# compile failure. The valid public XAML types and their namespaces were deliberately left unchanged instead of adding unnecessary workarounds.

### Changed

- Updated the host application revision from `0.1.1.rev12` to `0.1.1.rev13`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Raw Memory Read Compile Fix`.
- Updated README/current-workspace documentation to identify `0.1.1.rev13` as the current host build while retaining rev12 as the revision that introduced raw memory read.

### Removed

- Removed no application feature, Core contract, Plugin SDK contract, PS5 protocol behavior, Mock behavior, theme, workspace layout, splitter behavior, control style, or test fixture.
- No raw-memory-read functionality was rolled back; the revision only corrects the host compile path required to run and verify it.

### Version and Compatibility Notes

- Host application: `0.1.1.rev13`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev4` because no plugin code changed.
- Plugin API: unchanged at `1.0.0`.
- Core, Plugin SDK, both platform plugins, protocol fixtures, themes, splitters, and shared control resources remain functionally unchanged from rev12.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev12` baseline before modifying code.
- Reproduced the definite-assignment problem directly from the rev12 source: `candidate.Length > 0 && ulong.TryParse(..., out address)` can short-circuit when the candidate is empty, so `address` was not assigned on every return path.
- Confirmed `UiMetrics` is a public type in `TeeKay87.MemoryEngine.App.Application`, `ProportionalGridSplitter` is a public type in `TeeKay87.MemoryEngine.App.Controls`, and both XAML namespace mappings are correct. Their Visual Studio Designer errors are therefore treated as cascading failures until the corrected host assembly is rebuilt.
- Completed a full preparation audit covering all 64 C#/XAML/project/theme source files and all 31 README/CHANGELOG/docs Markdown files; XML/JSON structure, local documentation links, Designer type mappings, and release-tree cleanliness passed.
- Confirmed Core, Plugin SDK, both platform plugins, protocol tests, themes, the proportional splitter implementation, and shared style resources are byte-for-byte unchanged from rev12.
- Confirmed no .NET SDK or Windows WPF build toolchain is available in the preparation environment; Windows-side **Build -> Rebuild Solution** remains required to close this verification.

## TeeKay87's Memory Engine 0.1.1.rev12 - PS5 Raw Memory Read

### Added

- Added PlayStation 5 `CMD_PROC_READ` (`0xBDAA0002`) support to the native C# ps5debug-NG command client.
- Added `IMemoryReader` support to the connected PS5 target session using the Plugin SDK contract that already existed from the foundation revision.
- Added the `MemoryRead` capability to the PS5 plugin now that a real implementation exists.
- Added a capability-driven **Raw Memory Read** inspector to the host target area. The inspector is shared by every plugin exposing `MemoryRead`; it contains no PlayStation-specific platform check.
- Added hexadecimal address input, decimal or `0x`-prefixed hexadecimal byte-length input, a generic Read Memory command, operation status/error reporting, and a monospaced hexadecimal/ASCII result dump.
- Added automatic default read-address selection from the first sufficiently large readable Active Target memory region when a memory map is available.
- Added host-side readable-range validation using the neutral cached `MemoryRegion` and `MemoryProtection` models before manual reads are sent to map-capable plugins.
- Added a 4096-byte manual-inspector limit to keep interactive raw-read output bounded without imposing that limit on `IMemoryReader`, the PS5 plugin protocol implementation, or future scanner buffers.
- Added deterministic ps5debug-NG raw-memory-read protocol coverage verifying the packed 16-byte request body, PID/address/length fields, success-status ordering, returned byte count, and exact returned byte sequence.
- Added `docs/plugins/PS5/MEMORY_READ_VERIFICATION.md` with PS5-specific protocol behavior and a combined live rev11/rev12 verification procedure.
- Added `docs/testing/APP_0.1.1_REV12_VERIFICATION.md` with project-wide architecture, version, source-level, and Windows runtime verification requirements.

### Fixed

- No previously reported runtime defect is targeted by this revision. The revision extends the verified target-access architecture from memory-map enumeration to read-only memory access.

### Changed

- Updated the host application revision from `0.1.1.rev11` to `0.1.1.rev12`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `PS5 Raw Memory Read`.
- Updated the PlayStation 5 plugin independently from `0.1.0.rev3` to `0.1.0.rev4`.
- Expanded the PS5 plugin capability set from `Connect | ProcessEnumeration | MemoryRegionEnumeration` to `Connect | ProcessEnumeration | MemoryRegionEnumeration | MemoryRead`.
- Extended the PS5 target session to expose `IMemoryReader` alongside its existing `IProcessProvider` and `IMemoryMapProvider` services.
- Serialized ps5debug-NG raw-read requests as the documented packed layout: 4-byte process id, 8-byte target address, and 4-byte requested length.
- Required the ps5debug-NG wire success status before consuming the requested raw response bytes.
- Changed host command-state coordination so Disconnect, process Refresh, and Set Active Target are disabled while a manual raw-memory read is in progress, preventing intentional target/session changes during the operation.
- Changed Active Target/process clearing so raw-read input/result/error state is reset together with process and memory-map state.
- Updated README, Plugin SDK architecture notes, early development ordering, PS5 plugin documentation, ps5debug-NG protocol mapping, main-workspace documentation, rev11 verification notes, and memory-map verification notes to reflect the new read layer and the combined live verification plan.

### Removed

- Removed raw memory read from the PS5 plugin's documented unimplemented capability list because it is now implemented.
- Removed no Core contract, Plugin SDK contract, Mock-plugin behavior, theme, splitter behavior, control metric, scan placeholder, saved-address placeholder, or previously verified PS5 connection/process behavior.
- Memory write and scanning remain deliberately unimplemented and unadvertised.

### Version and Compatibility Notes

- Host application: `0.1.1.rev12`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: `0.1.0.rev4`.
- Plugin API: unchanged at `1.0.0` because `MemoryRead` and `IMemoryReader` already existed in the public contract.
- Core and Plugin SDK source are unchanged from rev11.
- The permanent CE-inspired workspace, external color themes, proportional splitters, and centralized 34-unit standard control-height system remain unchanged except for the functional Raw Memory Read expander added to the target area.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev11` baseline before modifying code.
- Confirmed the existing Plugin SDK already provides the required neutral `MemoryRead` capability and `IMemoryReader`, so no duplicate or PS5-specific public contract was introduced.
- Checked `CMD_PROC_READ` against the current public ps5debug-NG protocol documentation: command `0xBDAA0002`, packed 16-byte `{ uint32 pid; uint64 address; uint32 length; }` request, success status, then raw response bytes.
- Extended the deterministic protocol fixture so read serialization and raw response handling can be verified without a physical console.
- Prepared combined live verification so rev11 memory-map enumeration and rev12 raw-memory read are proven together on the real PS5 rather than requiring two separate stop points.
- Completed 484 static release checks covering source/resource validity, project references, version separation, capability boundaries, PS5 protocol layout, host integration, deterministic test coverage, documentation links, release cleanliness, and byte-for-byte preservation of the unchanged Core/Plugin SDK/Mock/UI-foundation files.
- Checked the raw-read receive behavior against the referenced ps5debug-NG server source, which sends the full requested byte count after `CMD_SUCCESS` while chunking internally at 64 KiB without extra per-chunk wire framing.
- Native .NET/WPF compilation and live PS5 execution remain required on the Windows development machine because the preparation environment does not contain the .NET SDK or a physical PS5.

## TeeKay87's Memory Engine 0.1.1.rev11 - PS5 Memory Map Enumeration

### Added

- Added PlayStation 5 `CMD_PROC_MAPS` (`0xBDAA0004`) support to the native C# ps5debug-NG command client.
- Added PS5-specific `Ps5DebugMemoryRegionInfo` as an internal wire-model record that remains confined to the PS5 plugin.
- Added `IMemoryMapProvider` support to the connected PS5 target session using the Plugin SDK contract that already existed from the foundation revision.
- Added automatic Active Target memory-map loading in the WPF host for any plugin advertising `MemoryRegionEnumeration` and exposing `IMemoryMapProvider`.
- Added host-side `ActiveMemoryRegions`, memory-map status, and memory-map error state so the neutral region list is ready for subsequent memory read/write and scanner work.
- Added automatic memory-map refresh when a process-list refresh preserves the same Active Target.
- Added a deterministic ps5debug-NG memory-map protocol fixture covering request-body serialization, response entry parsing, empty names, size conversion, and read/write/execute protection translation.
- Added `docs/plugins/PS5/MEMORY_MAP_VERIFICATION.md` with plugin-specific protocol/runtime verification requirements.
- Added `docs/testing/APP_0.1.1_REV11_VERIFICATION.md` with host/project verification requirements.
- Added the Windows runtime result for rev10 confirming the unified 34-unit control-height foundation looks correct and cohesive.

### Fixed

- No previously reported defect is targeted by this revision. The change resumes planned backend development after the rev10 UI foundation was verified.

### Changed

- Updated the host application revision from `0.1.1.rev10` to `0.1.1.rev11`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `PS5 Memory Map Enumeration`.
- Updated the PlayStation 5 plugin independently from `0.1.0.rev2` to `0.1.0.rev3` because that plugin now implements an additional backend capability.
- Expanded the PS5 plugin capability set from `Connect | ProcessEnumeration` to `Connect | ProcessEnumeration | MemoryRegionEnumeration`.
- Extended the PS5 command sender so requests may carry a bounded body while preserving the existing zero-body path used by connection metadata, NOP, and process-list commands.
- Added parsing for packed 58-byte ps5debug-NG `proc_vm_map_entry` records containing name, start, end, offset, and protection fields.
- Added defensive validation for impossible end-before-start ranges and an upper bound of 262,144 memory-map records before allocating the response payload.
- Translated PS5 VM protection bits `0x1`, `0x2`, and `0x4` to the neutral `MemoryProtection.Read`, `Write`, and `Execute` flags. Unknown bits are not guessed into unrelated neutral flags.
- Changed Set Active Target from a synchronous host command to an asynchronous host command so activation can complete the capability-driven memory-map request without blocking the WPF UI thread.
- Updated the target status area to report the Active Target memory-map load state/count and to surface memory-map errors separately from connection/process errors.
- Updated README, Plugin SDK architecture notes, PS5 plugin documentation, PS5 protocol mapping, main-workspace documentation, and early development ordering to reflect memory maps as the current completed implementation layer before raw read/write.

### Removed

- Removed memory-map enumeration from the PS5 plugin's documented unimplemented list because it is now implemented.
- Removed no Plugin SDK contract, Core behavior, theme, splitter behavior, control style, process-selection behavior, or existing plugin feature.
- The ps5debug-NG map `offset` value is intentionally not exposed through a PS5-specific host extension; the neutral `MemoryRegion` contract remains unchanged until a shared consumer requires such data.

### Version and Compatibility Notes

- Host application: `0.1.1.rev11`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: `0.1.0.rev3`.
- Plugin API: unchanged at `1.0.0` because `MemoryRegionEnumeration`, `IMemoryMapProvider`, `MemoryRegion`, and `MemoryProtection` already existed in the public contract.
- No Core or Plugin SDK source change was required for the new PS5 implementation.
- UI themes, splitters, shared control metrics, and the permanent workspace layout remain functionally unchanged from rev10.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, and the complete C#/XAML/project/solution source from the supplied `0.1.1.rev10` baseline before modifying code.
- Confirmed the existing Plugin SDK already provides all generic memory-map abstractions required by the PS5 implementation, avoiding a duplicate or PS5-specific contract.
- Checked the `CMD_PROC_MAPS` wire layout against the public ps5debug-NG protocol reference: 4-byte PID request; success status; `uint32` count; packed 58-byte entries with 32-byte name plus start/end/offset/protection fields.
- Extended the deterministic protocol server so the new request/response path can be verified without a physical console.
- Static release checks cover version separation, capability consistency, protocol constants/entry sizes, PS5-type isolation, neutral memory-region mapping, host Active Target state handling, XML/XAML validity, documentation links, and release-tree cleanliness.
- Native .NET/WPF compilation and live PS5 memory-map verification remain required on the Windows development machine.

## TeeKay87's Memory Engine 0.1.1.rev10 - Unified Interactive Control Heights

### Added

- Added central `UiMetrics.StandardControlHeight` host UI metric with a value of `34` WPF device-independent units.
- Added `docs/ui/CONTROL_METRICS.md` documenting the application-wide sizing contract for standard single-line interactive controls and the rules future control styles must follow.
- Added `docs/testing/APP_0.1.1_REV10_VERIFICATION.md` with source-level checks and Windows runtime tests for consistent control heights across the target bar and Scan workspace.
- Added the rev9 Windows runtime result confirming that the proportional horizontal splitter and preserved Scan-panel width bounds work correctly in both windowed and fullscreen use.

### Fixed

- Fixed standard buttons being taller than the Platform selector because `ButtonBaseStyle` used an independent `MinHeight=38` while ComboBox and TextBox styles used a 34-unit baseline.
- Fixed the Target Process selector stretching to the height of the taller buttons in its Grid row, making it visibly taller than the Platform selector even though both used the same ComboBox style.
- Fixed connection TextBoxes, target selectors, scan inputs, scan selectors, and command buttons not sharing one predictable application-wide single-line control height.

### Changed

- Updated the host application revision from `0.1.1.rev9` to `0.1.1.rev10`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Unified Interactive Control Heights`.
- Changed `ButtonBaseStyle` from an independent `38`-unit minimum height to the central fixed `UiMetrics.StandardControlHeight`.
- Changed the implicit TextBox and ComboBox styles from independent 34-unit minimum heights to the same central fixed `UiMetrics.StandardControlHeight`.
- Standardized all current normal single-line `Button`, `TextBox`, and `ComboBox` instances at the Platform selector's existing 34-unit height without adding per-view height overrides.
- Established that future standard single-line interactive control styles must consume the same central metric unless a control has a deliberate documented reason to use a different form factor.
- Updated README, button-style documentation, main-workspace documentation, and testing documentation to describe the shared sizing contract.

### Removed

- Removed the independent `MinHeight=38` value from the shared button style.
- Removed the independent `MinHeight=34` declarations from the shared TextBox and ComboBox styles in favor of the central metric.
- Removed no user-facing feature, splitter behavior, theme, plugin behavior, backend capability, or existing command.

### Version and Compatibility Notes

- Host application: `0.1.1.rev10`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Theme schema, external theme files, splitter ratios/bounds, Core contracts, and plugin contracts are unchanged.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the `0.1.1.rev9` baseline before modifying code.
- Confirmed the visual mismatch from the user's live rev9 screenshot is caused by independent style heights and Grid stretch behavior, not by platform-specific layout code.
- Confirmed the Platform ComboBox's established 34-unit baseline can be preserved as the shared application metric while bringing Buttons, TextBoxes, and other ComboBoxes to the same height.
- Confirmed the change is isolated to the WPF host presentation layer; Core, Plugin SDK, Mock plugin, PS5 plugin, theme JSON files, and existing backend/protocol tests require no modification.
- Static release checks verify the centralized 34-unit metric, implicit style coverage, absence of conflicting standard-control height declarations, version separation, XML/XAML validity, unchanged backend/plugin projects, documentation links, and clean release contents.
- Native Windows WPF compilation and runtime verification remain required in Visual Studio.

## TeeKay87's Memory Engine 0.1.1.rev9 - Proportional Workspace Splitter Range

### Added

- Added reusable `ProportionalGridSplitter` host UI control for splitter pairs that must be constrained by relative workspace ratios rather than fixed pixel limits.
- Added configurable minimum/maximum previous-panel ratio properties and live star-sizing normalization so proportional splits scale with the available window size.
- Added `docs/testing/APP_0.1.1_REV9_VERIFICATION.md` with source-level checks and Windows runtime tests for the 50/50 default and 20/80–80/20 resize range.
- Added the rev8 Windows runtime observation documenting that the Scan-panel width bounds are correct while the horizontal fixed-height bounds are too restrictive.

### Fixed

- Fixed the horizontal Scan Results/Saved Addresses splitter having an unnaturally narrow resize range in fullscreen because Saved Addresses was capped by a fixed `360 px` maximum.
- Fixed the horizontal split starting from an asymmetric fixed-height Saved Addresses row instead of sharing the available workspace equally.
- Fixed horizontal splitter proportions not being defined relative to the actual available workspace height.

### Changed

- Updated the host application revision from `0.1.1.rev8` to `0.1.1.rev9`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Proportional Workspace Splitter Range`.
- Changed Scan Results and Saved Addresses from mixed star/fixed row sizing to equal `*` / `*` row sizing, producing a 50% / 50% initial allocation.
- Changed the horizontal resize model from fixed pixel minimum/preferred/maximum heights to a proportional **20% / 80% through 80% / 20%** range.
- Preserved the rev8 vertical Scan-panel bounds unchanged: preferred `310 px`, minimum `280 px`, maximum `420 px`, with a `640 px` minimum left workspace.
- Updated README and main-workspace documentation to describe proportional horizontal resizing.

### Removed

- Removed the rev8 fixed horizontal row constraints: Scan Results `MinHeight=240` and Saved Addresses `Height=235`, `MinHeight=180`, `MaxHeight=360`.
- Removed no user-facing feature, theme, plugin behavior, backend capability, splitter visual style, or vertical Scan-panel constraint.

### Version and Compatibility Notes

- Host application: `0.1.1.rev9`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Theme schema, theme ids, persisted preferences, Core contracts, and plugin contracts are unchanged.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the `0.1.1.rev8` baseline before modifying code.
- Confirmed from the user's live rev8 screenshots that the remaining problem is specific to fixed horizontal height bounds; the vertical Scan-panel width constraints should remain unchanged.
- Confirmed no existing shared Core or Plugin SDK abstraction is relevant to this host-only presentation behavior, so the proportional splitter is isolated to the WPF App project.
- Static release checks verify the 50/50 star-row default, 20/80–80/20 ratio configuration, preserved vertical bounds, version separation, XML/XAML validity, unchanged backend/plugin projects, documentation links, and clean release contents.
- Native Windows WPF compilation and runtime verification remain required in Visual Studio.

## TeeKay87's Memory Engine 0.1.1.rev8 - Workspace Splitter Bounds Fix

### Added

- Added explicit usability bounds for the vertical workspace split so the left memory workspace and Scan panel cannot be resized into impractical widths.
- Added explicit height bounds for the horizontal Scan Results/Saved Addresses split so neither table can consume an unusable share of the left workspace.
- Added `docs/testing/APP_0.1.1_REV8_VERIFICATION.md` with source-level checks and required Windows runtime verification for both splitter extremes.
- Added the rev7 Windows runtime observation documenting that the theme-id warning fix succeeded while revealing the separate splitter-boundary issue.

### Fixed

- Fixed the vertical splitter allowing the Scan panel to become excessively wide and compress the left workspace enough to clip or visually distort controls.
- Fixed the horizontal splitter allowing the Saved Addresses/Scan Results allocation to reach impractical proportions that reduced workspace usability.
- Fixed the effective resize limits being based only on permissive row/column minimums rather than on dimensions that preserve the permanent workspace layout.

### Changed

- Updated the host application revision from `0.1.1.rev7` to `0.1.1.rev8`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Workspace Splitter Bounds Fix`.
- Increased the left workspace minimum width from `520` to `640` pixels.
- Changed the Scan panel constraints from a `250` pixel minimum with no maximum to a `280` pixel minimum and `420` pixel maximum while retaining its `310` pixel preferred width.
- Increased the Scan Results minimum height from `220` to `240` pixels.
- Changed Saved Addresses from a `150` pixel minimum with no maximum to a `180` pixel minimum and `360` pixel maximum while retaining its `235` pixel preferred height.
- Updated README and main-workspace documentation to describe the bounded splitter behavior.

### Removed

- Removed no user-facing feature, splitter, theme, memory-tool workflow, platform capability, plugin behavior, or backend functionality.
- Removed no resize capability; only layout states that made the surrounding UI unusable are no longer reachable.

### Version and Compatibility Notes

- Host application: `0.1.1.rev8`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Theme schema, canonical theme ids, and persisted theme preferences are unchanged from rev7.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the `0.1.1.rev7` baseline before modifying code.
- Confirmed the runtime symptom is caused by workspace `RowDefinition`/`ColumnDefinition` constraints rather than the reusable splitter ControlTemplates introduced in rev7.
- Preserved the rev7 14-pixel hit areas and centered 4-pixel splitter handles unchanged.
- Confirmed the fix is host-layout-only; Core, Plugin SDK, Mock plugin, PS5 plugin, and existing backend/protocol tests do not require modification.
- Static release checks verify the new width/height bounds, version separation, XML/XAML validity, unchanged backend projects, documentation links, and clean release contents.
- Native Windows WPF compilation and runtime verification remain required in Visual Studio.

## TeeKay87's Memory Engine 0.1.1.rev7 - Theme Identity and Splitter Spacing Fix

### Added

- Added canonical theme-id compatibility aliases so preferences and legacy theme definitions using `darker`/`darkest` resolve to `dimmed`/`dark`.
- Added automatic persistence migration so a successfully resolved legacy saved theme id is rewritten to its canonical replacement.
- Added runtime recognition of the obsolete bundled `Darker.json` and `Darkest.json` filenames when their current replacement files are present.
- Added build and publish cleanup targets that remove stale copies of those two legacy bundled theme files from generated `Themes` directories after incremental builds.
- Added reusable `RowWorkspaceSplitterStyle` and `ColumnWorkspaceSplitterStyle` resources. Each keeps a 14-pixel draggable track while drawing a centered 4-pixel theme-aware handle.
- Added `docs/testing/APP_0.1.1_REV7_VERIFICATION.md` and recorded the Windows runtime observations that led to this correction in the rev6 verification document.

### Fixed

- Fixed duplicate theme-id warnings observed after rev6 incremental builds. Visual Studio could leave copied `Darker.json` and `Darkest.json` files in the runtime `Themes` directory after the bundled files were renamed, causing old and current files to share ids.
- Fixed the same stale-file condition causing the Theme selector to load an obsolete `Darker` or `Darkest` display entry instead of the current Dimmed or Dark definition.
- Fixed uneven splitter spacing that made the horizontal and vertical drag handles appear attached to one neighboring panel.

### Changed

- Updated the host application revision from `0.1.1.rev6` to `0.1.1.rev7`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Theme Identity and Splitter Spacing Fix`.
- Changed the bundled Dimmed theme id from legacy `darker` to canonical `dimmed`.
- Changed the bundled Dark theme id from legacy `darkest` to canonical `dark` and updated the default external theme id accordingly.
- Changed both splitter layout tracks from 6 pixels to 14 pixels and added reusable shared splitter templates that center a 4-pixel visible handle inside the full interactive track with 5 pixels of breathing room on both sides.
- Removed the previous one-sided margins from Saved Addresses and the Scan panel because splitter spacing is now symmetric inside the splitter tracks.
- Updated README, theme documentation, workspace documentation, rev6 runtime observations, and rev7 verification documentation.

### Removed

- Removed no user-facing feature, memory-tool workflow, platform capability, plugin behavior, or theme palette.
- The obsolete theme ids are not accepted as canonical bundled ids anymore, but remain supported as migration aliases for preferences written by earlier revisions.

### Version and Compatibility Notes

- Host application: `0.1.1.rev7`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Existing rev4-rev6 saved theme preferences remain compatible through automatic id migration.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the `0.1.1.rev6` baseline before modifying code.
- Confirmed the runtime duplicate warning is caused by stale renamed theme files in incremental build output rather than by malformed current JSON.
- Kept duplicate-id validation active for real conflicts while isolating compatibility handling to the two known bundled legacy files/ids.
- Confirmed the change remains host-only; Core, Plugin SDK, Mock plugin, PS5 plugin, and existing backend/protocol tests do not require modification.
- Static release checks verify theme ids/aliases, stale-file cleanup, splitter dimensions/margins, XML/XAML/JSON validity, version separation, documentation links, unchanged backend projects, and clean release contents.
- Native Windows WPF compilation and runtime verification remain required in Visual Studio.

## TeeKay87's Memory Engine 0.1.1.rev6 - Workspace Layout and Theme Refinement

### Added

- Added a vertical resizable `GridSplitter` between the left-side memory lists and the Scan panel so the user can adjust the scanner-control width at runtime.
- Added a shared `ErrorMessageTextStyle` that collapses empty error messages, preventing invisible error rows from reserving unnecessary height in the target/connection area.
- Added explicit display-string behavior to `ThemeDescriptor`, `PluginViewModel`, and `TargetProcessViewModel` so the application's custom ComboBox template consistently presents the intended theme, platform, and target-process labels.
- Added project-wide verification documentation for the rev6 workspace/theme refinement under `docs/testing/APP_0.1.1_REV6_VERIFICATION.md`.

### Changed

- Updated the host application revision from `0.1.1.rev5` to `0.1.1.rev6`; the semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Workspace Layout and Theme Refinement`.
- Restructured the central workspace so **Scan Results** and **Saved Addresses** occupy the left side while the **Scan** panel spans their combined full height on the right.
- Preserved the existing horizontal splitter between Scan Results and Saved Addresses inside the left workspace and moved it into the new nested layout.
- Reduced target/connection padding and vertical spacing so the memory workspace begins closer to the normal connection/process status line.
- Renamed the user-facing **Darkest** theme to **Dark** while preserving its original dark palette.
- Renamed the user-facing **Darker** theme to **Dimmed** and changed its palette to a substantially lighter mid-dark set of surfaces, borders, inputs, selection, and disabled-state colors so it is meaningfully positioned between Light and Dark.
- Renamed the bundled external theme source files to `Light.json`, `Dimmed.json`, and `Dark.json` while deliberately retaining the established internal ids `light`, `darker`, and `darkest` for persisted-settings compatibility.
- Updated current README, architecture, UI-theme, UI-workspace, and button-style documentation for the refined layout and theme names.

### Removed

- Removed the explanatory `Classic first/next scan workflow...` development text from the permanent Scan panel.
- Removed the accent information box explaining why disabled scanner controls were visible.
- Removed unused vertical layout space caused by empty connection/process error rows.
- No scanner behavior, saved-address behavior, PS5 communication, process enumeration, target selection, plugin capability, Core contract, or Plugin SDK contract was removed.

### Version and Compatibility Notes

- Host application: `0.1.1.rev6`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Theme ids remain `light`, `darker`, and `darkest`; only the latter two user-facing names/source filenames changed, so earlier persisted theme selections remain compatible.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev5` baseline before changing code.
- Confirmed that the requested UI work can remain entirely inside the host application and documentation; no Core, Plugin SDK, Mock plugin, PS5 plugin, or protocol changes are required.
- Confirmed the Dark palette is unchanged from rev5's Darkest theme and that Dimmed uses a distinct intermediate palette.
- Confirmed all XAML and JSON files parse successfully after restructuring.
- Static release checks cover root/workspace row/column structure, both splitters, removed placeholder copy, collapsing error rows, ComboBox display labels, theme completeness, version separation, unchanged backend projects, documentation links, and clean release contents.
- Native Windows WPF compilation and visual/runtime verification must be performed in Visual Studio because the release-preparation environment does not contain the .NET SDK or Windows WPF toolchain.

## TeeKay87's Memory Engine 0.1.1.rev5 - Theme Manager Nullability Compile Fix

### Fixed

- Fixed two nullable-reference compiler errors (`CS8600`) in `ThemeManager` that prevented the WPF host project from compiling with the project-wide `Nullable=enable` and `TreatWarningsAsErrors=true` settings.
- Fixed runtime theme lookup in `ApplyTheme` so the value returned through `Dictionary.TryGetValue` is explicitly treated as nullable until both lookup success and a non-null `LoadedTheme` instance have been established.
- Fixed persisted/startup theme lookup in `FindTheme` using the same explicit nullable guard, preserving the existing behavior of returning `null` when no valid theme matches the requested id.

### Changed

- Updated the host application revision from `0.1.1.rev4` to `0.1.1.rev5`; the semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Theme Manager Nullability Compile Fix`.
- Updated `README.md` and current UI documentation to identify `0.1.1.rev5` as the active host revision.
- Added project-wide verification documentation for the rev5 compile correction.

### Removed

- No functionality was removed.
- No theme schema, theme color, theme file, WPF style, workspace control, PS5 connection behavior, process-enumeration behavior, target-selection behavior, Core contract, Plugin SDK contract, or plugin implementation was removed or redesigned.

### Version and Compatibility Notes

- Host application: `0.1.1.rev5`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- The correction is confined to host-side nullable handling in `ThemeManager`; it does not alter plugin compatibility or the external JSON theme contract.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev4` baseline before changing code.
- Confirmed that both reported `CS8600` diagnostics originate from `Dictionary.TryGetValue` out values at the theme-application and theme-lookup call sites.
- Changed only the nullability handling necessary to make those lookups explicit and safe before `LoadedTheme` is dereferenced.
- Preserved the rev4 workspace, themes, Core, Plugin SDK, Mock plugin, PS5 plugin, protocol implementation, and existing verification tests.
- Static release checks verify version separation, nullable guards, project/documentation references, unchanged plugin/API versions, XML/XAML/JSON syntax, and clean release contents.
- Native Windows WPF compilation remains the authoritative verification for the reported compiler errors and must be performed in Visual Studio after applying this revision.

## TeeKay87's Memory Engine 0.1.1.rev4 - Cheat Engine-Inspired Workspace and Color Themes

### Added

- Added a permanent Cheat Engine-inspired main workspace that preserves the familiar target-first memory-tool workflow while using the application's own modern WPF presentation rather than reproducing Cheat Engine's visual chrome.
- Added a compact application bar containing the application identity/version and a runtime Theme selector.
- Added a compact target/connection area that reuses the existing generic platform selection, plugin-defined connection fields, Connect/Disconnect commands, process refresh, selected process, and explicit Active Target workflow.
- Added a dedicated **Scan Results** workspace for future temporary scan candidates, with prepared columns for address, current value, previous value, value type, and memory region/module context.
- Added a dedicated right-side **Scan** panel with prepared Value, Scan Type, Value Type, First Scan, Next Scan, and New Scan controls. These controls are intentionally disabled until scanner behavior is implemented.
- Added a separate lower **Saved Addresses** workspace with prepared Active, Description, Address, Type, Value, Frozen, and Notes columns. Add/Edit/Remove/Export actions are intentionally disabled until the persistent-address model is implemented.
- Added a resizable horizontal divider between the scanner workspace and Saved Addresses so result-heavy and address-heavy workflows can allocate space differently.
- Added collapsible **Plugin details** so diagnostic plugin metadata and capability badges remain available without permanently occupying the primary working area.
- Added `Resources/Styles/ControlStyles.xaml` with reusable theme-aware styles/templates for common WPF presentation including TextBlock, TextBox, ComboBox, ComboBoxItem, CheckBox, DataGrid, DataGrid headers/rows/cells, cards, and Expander presentation.
- Added an external JSON color-theme system under `TeeKay87.MemoryEngine.App/Theming` that discovers, validates, orders, applies, and reports theme files without allowing themes to supply XAML or replace the UI.
- Added `ThemeManager` to map a stable JSON palette contract to shared WPF `SolidColorBrush` resources and replace those resources at runtime.
- Added complete-theme validation so a malformed or partial theme is skipped instead of inheriting stale colors from the previously active theme.
- Added strict `#RRGGBB` / `#AARRGGBB` color parsing and duplicate theme-id validation.
- Added **Light**, **Darker**, and **Darkest** external JSON themes. `Darkest` preserves the original application palette used before theme support.
- Added immediate theme switching through `DynamicResource` brush replacement so the already-open main window changes palette without restarting.
- Added persistence of the selected theme id under `%LocalAppData%\TeeKay87\MemoryEngine\settings.json`.
- Added startup fallback behavior that prefers the persisted valid theme, then `Darkest`, then the first valid discovered theme.
- Added a Darkest-compatible emergency palette in `App.xaml` so the UI remains readable if no external theme can be loaded.
- Added build and publish packaging of `Themes\*.json` to the application's runtime `Themes` directory.
- Added `docs/ui/THEMES.md` documenting the external JSON schema, palette keys, validation, persistence, fallback behavior, and extension rules.
- Added `docs/ui/MAIN_WORKSPACE.md` documenting the permanent Cheat Engine-inspired workflow, Scan Results/Saved Addresses separation, target context, placeholder policy, and platform-neutral UI requirements.
- Added `docs/testing/APP_0.1.1_REV4_VERIFICATION.md` with build, theme, workspace, PS5 regression, and archive verification requirements.

### Changed

- Updated the host application revision from `0.1.1.rev3` to `0.1.1.rev4`; the semantic application version remains `0.1.1` while this early development line continues.
- Updated `AppInfo.FeatureTitle` to `Cheat Engine-Inspired Workspace and Color Themes`.
- Replaced the previous temporary plugin-development-centered main-window layout with the permanent scanner-oriented workspace. The already verified connection/process commands and `PluginViewModel` state are relocated and reused rather than reimplemented.
- Changed platform selection from the previous large plugin presentation to a compact selector while keeping plugin-defined connection metadata and capability-driven behavior.
- Changed process presentation from the previous large process panel to a compact target process picker plus explicit Active Target summary. Selected process and Active Target remain separate states.
- Changed plugin metadata/capability presentation from permanently visible main content to an on-demand collapsible details area.
- Changed shared UI colors from a single hardcoded application palette to theme-driven brush resources. `App.xaml` now owns only the emergency fallback values; normal startup replaces them from external theme JSON.
- Changed common WPF control styling so TextBox, ComboBox, DataGrid, and related controls participate in the same active color palette as the reusable button system.
- Updated the reusable button documentation so button colors are described as active-theme resources rather than fixed `App.xaml` colors and documented the new disabled scanner/address placeholder usage.
- Updated the early architecture document to explicitly define the Cheat Engine-inspired scanner workspace, temporary-vs-persistent list separation, Active Target behavior, shared common-control styling, presentation-only color themes, and the decision to establish the permanent workspace before further memory/scanner features.
- Updated `README.md` to describe the current workspace, runtime theme system, build structure, live PS5 process-enumeration status, and current implementation boundary without using README as revision history.
- Updated rev3 project-wide and PS5 process verification documentation with the 2026-08-30 user-reported live result that the application connects to a physical PS5 and retrieves its real process list.

### Removed

- Removed the previous requirement for the plugin list, full plugin metadata, capabilities, connection area, and process list to occupy the majority of the main window at all times.
- Removed the previous single-palette limitation from normal runtime presentation; application colors can now be selected from discovered external theme files.
- Removed ordinary main-workspace dependence on local fixed colors in favor of semantic shared brush resources.
- No verified PS5 connection behavior, PS5 process-enumeration behavior, process selection/Active Target state, Core behavior, Plugin SDK contract, plugin capability, Mock plugin memory behavior, protocol command, or verification fixture was removed.

### Version and Compatibility Notes

- Host application: `0.1.1.rev4`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- No Plugin SDK compatibility change is introduced. Themes are host-presentation data and do not become part of platform-plugin contracts.
- Neither built-in plugin receives a revision bump because their source, capabilities, protocol behavior, and plugin documentation contract are not changed by the host UI/theme implementation.

### Verification

- Reviewed `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, all C# source, XAML, project files, solution configuration, `Directory.Build.props`, and `.gitignore` from the supplied `0.1.1.rev3` baseline before changing code.
- Confirmed before implementation that the existing `PluginViewModel`, `IProcessProvider`, selected-process state, Active Target state, connection commands, and process commands can be reused by the permanent workspace without introducing parallel backend logic.
- Preserved Core, Plugin SDK, Mock plugin, PS5 plugin, and verification-test source from the supplied rev3 baseline; the revision is intentionally isolated to host application presentation/theming plus documentation.
- Recorded the user's 2026-08-30 confirmation that rev3 successfully connects to a physical PS5 and retrieves its real process list as a runtime baseline that rev4 must preserve.
- Completed 76 static source-preparation checks covering version separation, unchanged backend/plugin/test baselines, XAML/XML/JSON syntax, complete theme palettes, Darkest/fallback consistency, resource resolution, theme-driven color usage, command reuse, capability gating, project/documentation references, and release-tree cleanliness.
- Theme/runtime visual behavior requires Windows WPF verification. The source-preparation environment does not contain the .NET SDK or Windows WPF runtime, so final native compilation and live visual/theme regression checks remain documented Windows verification items.

## TeeKay87's Memory Engine 0.1.1.rev3 - PS5 Process Enumeration and Target Selection

### Added

- Added PS5 `CMD_PROC_LIST` (`0xBDAA0001`) support to the existing native C# ps5debug-NG command client.
- Added bounded parsing of the documented process-list response: wire success status, `uint32` process count, and fixed 36-byte entries containing a 32-byte process name plus signed 32-bit PID.
- Added client-side process-count validation before response-buffer allocation and negative-PID validation before converting PS5 process identifiers to the neutral unsigned `TargetProcess.Id` representation.
- Added `Ps5DebugProcessInfo` as an internal PS5-plugin transport model so the ps5debug-NG wire record remains contained inside the plugin.
- Added `IProcessProvider` implementation to `Ps5TargetSession`, mapping PS5 transport results to the already existing platform-neutral `TargetProcess` model.
- Added a generic target-process panel to the WPF shell for any connected plugin advertising `TargetCapabilities.ProcessEnumeration`.
- Added automatic process refresh after a successful connection when process enumeration is supported.
- Added a reusable process Refresh command that requests a new list through the active session's existing `IProcessProvider` service.
- Added explicit separation between the currently selected process row and the **Active Target** intended for later memory operations.
- Added **Set Active Target** using the existing shared Primary button style and **Refresh** using the existing shared Secondary button style.
- Added process-state preservation across refreshes when the same PID/name identity remains available, while clearing stale selected/active identities that no longer exist.
- Added process-list and active-target cleanup on disconnect.
- Added `TargetProcessViewModel` for presentation-only formatting of neutral process values, including hexadecimal PID display and safe fallback text for unnamed processes.
- Extended the loopback ps5debug-NG protocol fixture so it can serve deterministic process-list responses after the connection handshake.
- Added a dedicated verification check for PS5 process enumeration using representative `SceShellCore`, `eboot.bin`, and `WebProcess` entries.
- Added `docs/plugins/PS5/PROCESS_ENUMERATION_VERIFICATION.md` with deterministic and live-target verification procedures specific to the PS5 plugin.
- Added `docs/testing/APP_0.1.1_REV3_VERIFICATION.md` with project-wide build, regression, and platform-neutral UI verification requirements.

### Changed

- Updated the host application revision from `0.1.1.rev2` to `0.1.1.rev3`; the application version remains `0.1.1` because development is continuing within the same early feature line.
- Updated `AppInfo.FeatureTitle` to `PS5 Process Enumeration and Target Selection`.
- Updated the PS5 plugin's independent revision from `0.1.0.rev1` to `0.1.0.rev2`. The PS5 plugin semantic version remains `0.1.0` while the new process functionality awaits live-target verification.
- Updated the PS5 plugin capability declaration from `Connect` to `Connect | ProcessEnumeration`. Foreground-process discovery and all memory/scanner/debugger capabilities remain intentionally unadvertised.
- Updated the PS5 plugin description to reflect connection plus target process enumeration.
- Updated `Ps5TargetSession.GetService<TService>()` so the connected PS5 session exposes its newly implemented `IProcessProvider` service while continuing to expose no unimplemented services.
- Updated the generic plugin view model with process-loading state, process-specific status/error presentation, selected-process state, active-target state, and command availability tied to connection/capability state.
- Updated the process-list selection binding to explicit two-way synchronization so UI row selection and the generic `SelectedProcess` state remain intentionally coupled while the separate Active Target remains unchanged until explicitly set.
- Updated the current-development milestone text so it reflects process enumeration and explicit active-target selection while retaining the limitation that PS5 memory services are not implemented yet.
- Updated the verification executable's PS5 metadata expectations for the independent PS5 plugin revision and expanded capability set.
- Updated the PS5 connection test to expect `IProcessProvider` now that that service is implemented.
- Updated the existing PS5 protocol mapping from connection-only scope to connection plus process-list scope.
- Updated PS5 connection documentation with the successful live Windows/PS5 connection result reported on 2026-08-30, without treating untested disconnect/reconnect or process-list behavior as verified.
- Updated the reusable button documentation to include the new Refresh and Set Active Target buttons and confirm that the shared button resources are reused rather than copied.
- Updated the Plugin SDK architecture documentation to describe how the WPF shell consumes `IProcessProvider` and keeps selected-process state separate from the active target without introducing platform checks.
- Updated `README.md` to describe the current complete process-enumeration workflow, current PS5 plugin version/capabilities, verification suite, usage, and remaining limitations.

### Removed

- Removed the previous PS5-plugin limitation that process enumeration was unavailable.
- Removed the previous UI state in which a connected process-capable plugin had no generic process-selection workflow.
- No existing Core contracts, Plugin SDK contracts, mock-target process/memory behavior, plugin discovery behavior, PS5 connection-identification sequence, shared button templates, or connection-setting behavior was removed.

### Version and Compatibility Notes

- Host application: `0.1.1.rev3`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- No Plugin SDK compatibility change is introduced in this revision. Existing third-party plugins built against Plugin API `1.0.0` do not require source changes because process enumeration continues to use the pre-existing optional capability/service contract.
- A plugin only receives the process UI when it advertises `ProcessEnumeration`; the host does not check for PlayStation 5 or any other platform name.

### Verification

- Reviewed `README.md`, `CHANGELOG.md`, every Markdown document under `docs/`, all C# source, XAML, project files, solution configuration, `Directory.Build.props`, and `.gitignore` from the supplied `0.1.1.rev2` baseline before changing code.
- Confirmed before implementation that `IProcessProvider` and `TargetProcess` already provide the required neutral contract, so no duplicate Core/Plugin SDK process API was introduced.
- Checked `CMD_PROC_LIST` framing and response layout against the public ps5debug-NG protocol reference: no request body, success status, process count, then 36-byte `name[32] + int32 pid` entries.
- Preserved the previously working PS5 connection handshake implementation; process enumeration is added after the established connection rather than replacing the connection-identification flow.
- Recorded the previously reported successful Windows application launch and live PS5 connection in test documentation rather than in the changelog as a code change.
- The preparation environment does not provide the .NET SDK or Windows WPF runtime, so final compilation, verification-executable execution, visual target-selection behavior, and live PS5 process enumeration remain Windows/runtime verification items documented under `docs/testing/` and `docs/plugins/PS5/`.

## TeeKay87's Memory Engine 0.1.1.rev2 - Reusable Button Styles and States

### Added

- Added `src/TeeKay87.MemoryEngine.App/Resources/Styles/ButtonStyles.xaml` as the central application-wide resource dictionary for standard WPF buttons.
- Added `ButtonBaseStyle`, which owns the shared `ControlTemplate`, typography, padding, alignment, cursor behavior, focus rendering, and interaction-state rendering used by ordinary buttons throughout the application.
- Added `PrimaryButtonStyle` for main affirmative workflow actions such as Connect, future Scan/Apply operations, and equivalent primary actions.
- Added `SecondaryButtonStyle` for neutral/supporting actions such as Disconnect and Reload Plugins.
- Added `DangerButtonStyle` as the shared semantic style for future destructive or potentially irreversible actions instead of introducing ad-hoc red buttons in individual views.
- Added an implicit application `Button` style based on `SecondaryButtonStyle` so newly introduced standard buttons cannot silently fall back to the operating-system WPF control chrome when no explicit semantic style is supplied.
- Added shared button interaction handling for normal, mouse-hover, pressed, keyboard-focused, defaulted, and disabled states.
- Added dedicated disabled button background, border, and text theme resources that preserve a dark-theme appearance while keeping disabled labels readable.
- Added central interaction-overlay and destructive-action theme resources used by the shared control template.
- Added `docs/ui/BUTTON_STYLES.md` documenting semantic button types, interaction states, resource ownership, usage examples, and extension rules for future views.
- Added `docs/testing/APP_0.1.1_REV2_VERIFICATION.md` documenting the reported disabled-button rendering problem, implementation checks, and required Windows visual/runtime verification.

### Changed

- Updated the host application revision from `0.1.1.rev1` to `0.1.1.rev2`; the application version remains `0.1.1` because this work extends the current development line rather than representing a larger completed version milestone.
- Updated `AppInfo.FeatureTitle` to `Reusable Button Styles and States`.
- Replaced the previous property-only implicit WPF `Button` styling with an application-owned reusable control template. This prevents Windows theme rendering from replacing the intended dark-theme presentation in disabled and other interaction states.
- Updated the existing Connect button to use `PrimaryButtonStyle`.
- Updated the existing Disconnect and Reload Plugins buttons to use `SecondaryButtonStyle`.
- Updated the current-development milestone text in the WPF shell so it describes the active reusable-button feature while retaining the existing note that PS5 process and memory services are not implemented yet.
- Updated `App.xaml` to host the shared button palette and merge the reusable button resource dictionary while preserving the existing TextBlock, TextBox, general color, and brush resources.
- Updated the early-development architecture guide with the rule that ordinary WPF control styling and interaction states must be centralized rather than copied into individual views.
- Updated `README.md` to describe the current application-wide button styling system and current `0.1.1.rev2` application state without turning README into revision history.
- Kept all layout-specific values that belong to the current view, such as button margins and minimum widths, in `MainWindow.xaml`; the shared style system owns appearance and state behavior rather than view layout.

### Removed

- Removed reliance on the operating-system WPF `Button` control template for application buttons.
- Removed the previous global `Button` style from `App.xaml` because standard button behavior is now provided by the dedicated shared resource dictionary.
- Removed the possibility that the current disabled Connect/Disconnect controls render with white system chrome or effectively unreadable button text solely because `IsEnabled` changes.
- No Core logic, Plugin SDK contracts, plugin-host behavior, mock-target behavior, PS5 protocol/connection behavior, or plugin-specific documentation was removed or changed.

### Version and Compatibility Notes

- This revision changes the **host application only**. The In-Memory Test Target remains `1.0.0.rev1`, the PlayStation 5 plugin remains `0.1.0.rev1`, and the Plugin API remains `1.0.0`.
- No plugin rebuild is required because of a Plugin SDK contract change; built-in plugins are rebuilt only as part of the normal solution build/package process.
- Capability badges remain informational elements rather than buttons and therefore intentionally do not use the shared button control template.

### Verification

- Reviewed `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, the complete C#/XAML/project/solution codebase, `Directory.Build.props`, and `.gitignore` before changing the code.
- Confirmed that the requested correction can be isolated to WPF presentation resources and current button declarations without modifying previously verified connection, plugin, memory, or discovery functionality.
- Validated XAML/XML well-formedness, resource references, shared-style usage, centralized application version information, unchanged independent plugin versions, project paths, and release-tree cleanliness during source preparation.
- The source-preparation environment does not provide the .NET SDK or Windows WPF runtime, so native compilation and visual state verification must be performed on the Windows development machine. The exact checks are documented in `docs/testing/APP_0.1.1_REV2_VERIFICATION.md`.

## TeeKay87's Memory Engine 0.1.1.rev1 - PS5 Plugin and Connection Foundation

### Added

- Added the first live-target platform project, `TeeKay87.MemoryEngine.Platform.PS5`, while keeping all PlayStation 5 and ps5debug-NG transport behavior outside Core and the WPF application.
- Added a PS5 plugin-local `Ps5PluginInfo` component as the authoritative source for the PS5 plugin id, name, independent semantic version, plugin revision, and targeted Plugin API version.
- Added the first PS5 plugin version as `0.1.0.rev1`. This version is independent from the host application's `0.1.1.rev1` version and from the ps5debug-NG payload version.
- Added PS5 plugin-defined connection fields for host/IP and command-server port. The plugin supplies the labels, descriptions, requirement flags, and default port (`744`) through the Plugin SDK instead of relying on PS5-specific controls in WPF.
- Added `Ps5DebugClient`, an asynchronous C# command-channel client for the connection-identification subset of the ps5debug-NG v1.3.0 wire protocol.
- Added request framing for the 12-byte little-endian ps5debug-NG command header using packet magic `0xFFAABBCC`.
- Added connection identification through protocol-version, platform-id, branding, firmware-version, and process-NOP/liveness commands.
- Added explicit validation that the remote platform id is PlayStation 5 (`5`) and that the returned human-readable brand begins with `ps5debug-NG` before a connected target session is exposed to the host.
- Added parsing and retention of the NUL-separated ps5debug-NG branding capability-level field for later feature negotiation without advertising future capabilities early.
- Added a five-second connection/identification timeout that remains linked to caller cancellation.
- Added PS5 connection/session lifetime handling so the plugin-owned TCP resources are disposed when Disconnect is selected, a plugin is reloaded, or the application closes.
- Added the PS5 plugin to the solution and to the application's platform-plugin build/copy pipeline so its entry assembly is deployed next to the mock plugin under the output `Plugins` directory.
- Added the initial formal `PluginApiInfo` compatibility version (`1.0.0`) to the Plugin SDK.
- Added independent plugin revision and targeted Plugin API version fields to `PluginMetadata`, including `DisplayVersion` formatting as `<plugin-version>.rev<plugin-revision>`.
- Added `TargetConnectionSettingDefinition` and extended `ITargetPlugin` with plugin-owned `ConnectionSettings` metadata.
- Added `ConnectionSettingViewModel` so the WPF application can render connection fields supplied by any plugin without checking platform names.
- Added generic Connect and Disconnect commands to the plugin view model, including required-field validation, asynchronous session creation, connection-state presentation, error presentation, cancellation, and session disposal.
- Added Plugin API compatibility, plugin revision, and connection-setting validation to `PluginHost` discovery.
- Added `MockPluginInfo` so the existing In-Memory Test Target also owns its plugin version/revision and targeted Plugin API version independently from the application.
- Added a loopback `Ps5ProtocolTestServer` to the dependency-free verification executable. It validates the PS5 connection command order, packet magic, zero-length connection request bodies, representative metadata responses, and NOP success status without requiring a physical console.
- Expanded the verification executable to cover independent version domains, PS5 plugin metadata and capability scope, PS5 connection settings, the ps5debug-NG connection handshake, and discovery of both built-in plugins.
- Added `docs/plugins/Mock/` as the dedicated documentation directory for the In-Memory Test Target.
- Added `docs/plugins/PS5/` as the dedicated documentation directory for the PlayStation 5 plugin.
- Added PS5-specific documentation covering the current plugin scope, ps5debug-NG connection protocol mapping, loopback verification, and the required live-target test procedure.
- Added `docs/testing/APP_0.1.1_REV1_VERIFICATION.md` for project-wide verification of this application revision.

### Changed

- Updated the application version from `0.1.0.rev2` to `0.1.1.rev1` after the `0.1.0` foundation was reported working on the Windows development machine. Revision numbering therefore restarts at `1` for the new application version.
- Updated `AppInfo` so the central application feature title is `PS5 Plugin and Connection Foundation`.
- Extended the Plugin SDK instead of adding PlayStation-specific connection models to Core. Connection requirements are now part of the neutral plugin contract and can be reused by future Windows, Xbox 360, emulator, remote-console, or other plugins.
- Updated `PluginMetadata` so plugin versions no longer need to mirror or be inferred from host application versions.
- Updated the mock plugin to implement the new connection-settings contract with an empty settings collection. Its deterministic process, memory map, memory read, and memory write behavior remains unchanged.
- Updated the WPF plugin list and selected-plugin details to display each plugin's independent version/revision and targeted Plugin API version.
- Updated the selected-plugin panel with a generic connection section that is driven by the plugin's `Connect` capability and connection-setting definitions.
- Updated plugin reload and application shutdown behavior so active plugin sessions are disposed before plugin view models and collectible plugin load contexts are released.
- Updated the application project to build and copy both built-in platform plugin entry assemblies.
- Updated the early-development architecture documentation with conservative host versioning guidance, independent plugin version/revision rules, Plugin API compatibility separation, and the requirement for dedicated per-plugin documentation directories.
- Updated the Plugin SDK architecture documentation to describe the implemented version domains, connection-setting model, compatibility checks, WPF integration, and per-plugin documentation structure.
- Updated `README.md` to describe the complete current `0.1.1.rev1` application, the two built-in plugins, PS5 connection usage, independent version domains, current verification suite, and current limitations without using README as revision history.

### Removed

- Removed the assumption that a platform plugin's displayed version is only a single `System.Version` value without a plugin revision.
- Removed the need for the WPF application to know that PlayStation 5 requires an IP/host field and port field; those requirements are now supplied entirely by the PS5 plugin.
- No previously verified mock memory behavior, plugin discovery behavior, WPF startup behavior, project structure, or Core memory contracts were removed.

### Protocol and Compatibility Notes

- The implemented PS5 connection subset was checked against the public ps5debug-NG v1.3.0 source at commit `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86` and its protocol reference.
- Connection metadata commands implemented in this revision return their documented payload directly: protocol version as a length-prefixed UTF-8 string, platform id as `uint16`, branding as a length-prefixed byte payload, and firmware as `uint16`. `CMD_PROC_NOP` returns the bit-swapped on-wire success word `0x80000000`.
- The PS5 plugin advertises only `TargetCapabilities.Connect` in this revision. Although ps5debug-NG supports process, memory, scanner, debugger, disassembly, assembly, and other operations, those capabilities remain disabled until their Memory Engine implementations exist and are verified.
- The Plugin API compatibility model is now explicit. The host currently accepts plugins with the same Plugin API major version and the same or an older minor version; plugins requiring a newer minor version are rejected during discovery.
- The Plugin API change in this revision is the first formally versioned contract baseline. Plugins compiled against the earlier unversioned development contract should be rebuilt against the current Plugin SDK.

### Verification

- Reviewed the complete supplied `0.1.0.rev2` documentation and source tree before implementation, including README, CHANGELOG, every Markdown document under `docs/`, all C# source, XAML, project files, solution configuration, and root build configuration.
- Preserved the previously verified In-Memory Test Target process and memory behavior and extended it only where required by the new neutral Plugin SDK metadata/connection contract.
- Checked the PS5 protocol constants, response layouts, and connection command behavior against the referenced ps5debug-NG v1.3.0 source/protocol documentation.
- Added an automated loopback protocol fixture so the request framing and response parsing can be reproduced without a console once the test project is built on a .NET 9 development machine.
- Revalidated solution paths, project-reference paths, XML/XAML well-formedness, plugin documentation separation, centralized application version information, independent plugin version information, Plugin API version consistency, and release-tree cleanliness during preparation.
- The source-preparation environment does not provide the .NET SDK or Windows WPF toolchain, so native compilation and WPF execution cannot be run there. A Windows build and live PS5 connection/disconnection test remain required before this version is considered fully runtime-verified. The required procedure is documented under `docs/testing/` and `docs/plugins/PS5/`.

## TeeKay87's Memory Engine 0.1.0.rev2 - WPF Application Namespace Compile Fix

### Added

- Added `docs/testing/REV2_WPF_APPLICATION_COMPILE_FIX.md` documenting the Visual Studio CS0118 failure, its cause, the exact corrective change, static verification, and the remaining native-build verification step.

### Changed

- Changed `App.xaml.cs` to alias `System.Windows.Application` as `WpfApplication` and inherit from that alias. This removes the compiler ambiguity with the existing `TeeKay87.MemoryEngine.App.Application` namespace without moving or renaming the `AppInfo` namespace.
- Updated the centralized `AppInfo` revision from `1` to `2`.
- Updated the centralized `AppInfo` feature title to `WPF Application Namespace Compile Fix`.
- Updated `README.md` so the current-build description and documentation index reflect revision 2 while continuing to describe the complete current application rather than acting as revision history.

### Removed

- No application functionality, plugin contracts, project structure, or previously implemented behavior was removed in this revision.

### Cause and Compatibility

- Revision 1 contained `public partial class App : Application` inside the `TeeKay87.MemoryEngine.App` namespace while also defining the child namespace `TeeKay87.MemoryEngine.App.Application`. In that scope, the identifier `Application` could bind to the namespace rather than `System.Windows.Application`, producing compiler error CS0118.
- The fix is intentionally limited to explicit WPF type resolution. The existing namespace hierarchy, solution layout, Core, Plugin SDK, mock plugin, plugin host, and WPF UI remain otherwise unchanged.

### Verification

- Reviewed all existing Markdown documentation and every source, XAML, project, solution, and root build-configuration file from the rev1 baseline before applying the change.
- Confirmed statically that the ambiguous inheritance declaration is removed and that `App.xaml` continues to reference the same `TeeKay87.MemoryEngine.App.App` class.
- Revalidated XML/XAML well-formedness, project references, centralized revision declarations, documentation consistency, and archive contents after the change.
- The preparation environment still does not provide the .NET SDK or Windows WPF toolchain, so a native build could not be executed there. Final Visual Studio build verification remains required and is documented in `docs/testing/REV2_WPF_APPLICATION_COMPILE_FIX.md`.

## TeeKay87's Memory Engine 0.1.0.rev1 - Initial Architecture and Plugin Foundation

### Added

- Added the first structured solution, `TeeKay87.MemoryEngine.sln`, with separate projects for the WPF application, shared Core, public Plugin SDK, development platform plugin, and verification tests.
- Added `Directory.Build.props` so nullable reference types, explicit `using` requirements, current C# language support, deterministic builds, and warning-as-error behavior are applied consistently across projects.
- Added a centralized `AppInfo` component as the authoritative source for the application title, version, revision, current feature title, display version, and window title.
- Added the initial `TeeKay87.MemoryEngine.PluginSdk` project with platform-neutral contracts and models:
  - `ITargetPlugin` for platform plugin entry points;
  - `ITargetSession` for connected target sessions;
  - service-based session capability access through `GetService<TService>()`;
  - `IProcessProvider` and `IForegroundProcessProvider`;
  - `IMemoryReader`, `IMemoryWriter`, and `IMemoryMapProvider`;
  - `TargetSessionExtensions.GetRequiredService<TService>()` for explicit required-service access;
  - `PluginMetadata`, `TargetConnectionOptions`, `TargetArchitecture`, `TargetProcess`, and `MemoryRegion` models;
  - CPU architecture, endianness, memory protection, and standard memory-value type definitions.
- Added a `TargetCapabilities` flags model covering the planned cross-platform capability surface, including target connection, process discovery, memory operations, scanning, debugger features, assembly/disassembly, and cheat functionality.
- Added the initial Core plugin-hosting infrastructure:
  - top-level plugin-directory discovery;
  - isolated collectible `AssemblyLoadContext` loading;
  - dependency resolution through `AssemblyDependencyResolver`;
  - shared Plugin SDK assembly identity between the host and plugins;
  - public plugin type discovery;
  - plugin metadata validation;
  - duplicate plugin-id rejection;
  - structured discovery errors;
  - unload support when plugins are reloaded or the host is disposed.
- Added `TeeKay87.MemoryEngine.Platform.Mock`, a deterministic in-memory development plugin that implements the same SDK contracts intended for live targets.
- Added a mock target process and memory region with deterministic Health, Ammo, and Money values so future Core functionality can be developed and regression-tested without a physical console.
- Added functional mock implementations for process enumeration, foreground-process discovery, memory-region enumeration, memory reads, and memory writes.
- Added a WPF/MVVM application shell that:
  - loads plugins from the output `Plugins` directory;
  - lists discovered platform plugins;
  - displays platform, backend, plugin version, target architecture, assembly path, and advertised capabilities;
  - reloads plugins without restarting the application;
  - displays plugin-discovery failures;
  - obtains all displayed application version information from `AppInfo`.
- Added build integration that builds the mock platform plugin with the application and places its plugin assembly under the application's output `Plugins` directory.
- Added a dependency-free executable verification project covering plugin metadata, capability declarations, process discovery, foreground process discovery, memory maps, memory reads, memory writes with read-back verification, and runtime plugin discovery.
- Added `.gitignore` rules for common .NET, Visual Studio, JetBrains, test, coverage, and operating-system generated files.
- Added detailed current-state documentation to `README.md`.
- Added `docs/architecture/PLUGIN_SDK_FOUNDATION.md` documenting the implemented plugin boundary, service model, capability model, discovery behavior, and extension rules.
- Added `docs/testing/REV1_FOUNDATION_VERIFICATION.md` documenting verification scope and results for this revision.

### Changed

- Reorganized the original single-project WPF template into the multi-project structure defined by the early-development architecture guide.
- Replaced the original empty `MainWindow` with the first capability-driven application shell.
- Replaced the original `TeeKay87_s_Memory_Engine` namespace with the structured `TeeKay87.MemoryEngine.*` namespace hierarchy used by the new solution.
- Moved platform-neutral contracts out of the WPF application so future PS5, Windows, Xbox 360, emulator, and memory-dump implementations can target the same Plugin SDK.
- Established explicit separation between platform identity and backend identity in plugin metadata. A plugin can therefore identify a target as PlayStation 5 while separately identifying ps5debug-NG as the backend.
- Established session services as the extension mechanism for target operations so unsupported features are absent instead of requiring platform-specific conditionals or dummy implementations.

### Removed

- Removed the original root-level single WPF project and its empty template source files after their application role was migrated into `src/TeeKay87.MemoryEngine.App`.
- Removed template-generated unused namespace imports from the original WPF code as part of the project restructure.
- Removed the now-unnecessary `docs/.gitkeep` placeholder because the documentation tree contains real architecture and verification documents.

No previously verified application functionality was removed because the supplied baseline contained only the unimplemented WPF template.

### Compatibility and Development Impact

- The project now requires opening/building `TeeKay87.MemoryEngine.sln` rather than the original root-level `.csproj`.
- Future platform implementations must reference `TeeKay87.MemoryEngine.PluginSdk` and expose an `ITargetPlugin` implementation instead of adding platform-specific behavior directly to the WPF application or Core.
- The current plugin loader expects plugin entry assemblies to be placed directly in the application's `Plugins` directory. More advanced packaging or manifests may be added later without changing the Core/platform separation established here.
- The current revision intentionally does not include ps5debug-NG code. PS5 transport/protocol work remains isolated to the upcoming PS5 platform plugin milestone.

### Verification

- Reviewed the complete supplied baseline before implementation, including `README.md`, `CHANGELOG.md`, the complete early-development architecture document, and every source/project file.
- Verified the new repository structure, project references, XML project files, XAML XML structure, centralized version declarations, plugin contract separation, and documentation consistency with automated repository checks.
- Added executable runtime verification tests for the mock target and plugin host so the same checks can be run on a Windows development machine with the .NET 9 SDK.
- The execution environment used to prepare this revision does not contain a .NET SDK or Windows WPF toolchain, so a native `dotnet build` and execution of the WPF application could not be performed in that environment. This limitation is recorded in `docs/testing/REV1_FOUNDATION_VERIFICATION.md` rather than being represented as a successful runtime build.
