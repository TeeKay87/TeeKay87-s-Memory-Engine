# TeeKay87's Memory Engine 0.1.7.rev48 Source Review

## Scope

Focused correction of the Call Stack Comparer `No group` live-display defect, based directly on the user-supplied rev47 package. All Markdown documentation and the complete application/test source tree were read before modification.

## Runtime evidence from rev47

The rev47 runtime check showed three important facts:

1. selecting `No group` executed the action and produced the ungroup status;
2. closing and reopening Call Stack Comparer showed the snapshot with no group, proving the model/persistence path was already correct;
3. while the original window remained open, the Group ComboBox continued to display the previous group name.

This isolates the defect to the live WPF selection box rather than group storage, session-group registration, or snapshot metadata.

## Root cause

The Group ComboBox binds `SelectedItem` to the snapshot's `Group` string and its `ItemsSource` to registered group names. The ungrouped value is an empty string by design and is not inserted into that registered-group collection. Rev47 attempted to refresh the `SelectedItem` binding target after clearing the model. Because there is no empty-string item for WPF to select, that refresh did not perform the selection transition needed to clear the existing selection box content.

## Correction

`SnapshotNoGroupButton_Click` continues to call the verified `item.ClearGroup()` model path. It then clears the ComboBox's live selection with `SelectedIndex = -1` before closing the dropdown. `SelectedIndex` is not bound in this control, so this clears the visible selection without replacing or detaching the existing two-way `SelectedItem` binding. Later selection of any registered group therefore continues through the normal binding path.

No group is added to `AvailableGroups`, and the existing named group remains in the session catalog for reuse. Group-comparison invalidation and action-state updates continue to originate from the existing Group setter callback.

## Test contract

The existing comparer source-contract check now requires the explicit `SelectedIndex = -1` live-selection reset and rejects the ineffective rev47 `UpdateTarget()` workaround. The automated registry remains 152 checks.
