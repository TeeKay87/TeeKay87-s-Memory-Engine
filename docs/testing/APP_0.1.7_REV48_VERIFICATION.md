# TeeKay87's Memory Engine 0.1.7.rev48 Verification

## Purpose

Verify that selecting `No group` clears both the underlying snapshot membership and the visible Group cell immediately in the already-open Call Stack Comparer window.

## Automated gate

1. Build the complete solution on Windows.
2. Run the complete automated registry and require **152/152 PASS**.

## Focused runtime gate

1. Attach the debugger and pause at a valid capture state.
2. Capture one snapshot in Call Stack Comparer.
3. Assign the snapshot to a named group.
4. Open that row's Group dropdown and select **No group**.
5. Without closing or reopening the comparer, confirm the Group cell clears immediately.
6. Open the Group dropdown again and confirm the previous named group remains available for reuse.
7. Reassign that previous group and confirm the Group cell displays it immediately, proving the existing `SelectedItem` binding still works after the ungroup action.

No broad runtime retest is required unless the automated gate or this focused check exposes a regression.
