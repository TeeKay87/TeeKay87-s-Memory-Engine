# TeeKay87's Memory Engine 0.1.7.rev47 Verification

## Purpose

Verify the focused Call Stack Comparer `No group` live-display synchronization correction without reopening the window.

## Automated gate

1. Build the complete solution on Windows.
2. Run the complete automated registry and require **152/152 PASS**.

## Focused runtime gate

1. Attach the debugger and capture a snapshot.
2. Assign the snapshot to a named group.
3. Select `No group` from that row's Group dropdown.
4. Confirm the Group cell clears immediately while the comparer remains open.
5. Confirm the previous group name remains available in the session group catalog.
6. Reopen the comparer and confirm the snapshot remains ungrouped.

No broad runtime retest is required for this revision unless the automated gate or focused check exposes a regression.
