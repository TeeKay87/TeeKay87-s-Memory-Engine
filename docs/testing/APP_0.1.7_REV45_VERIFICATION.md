# TeeKay87's Memory Engine 0.1.7.rev45 Verification

## Automated gate

Build the solution on Windows and require all **152/152** registered checks to pass.

## Focused runtime checks

1. Open Debugger without attaching: **Export...** is disabled. Attach successfully: Export becomes enabled. Detach: Export becomes disabled again.
2. In Call Stack Comparer, assign a snapshot to a group and choose **No group**. The row becomes ungrouped immediately, the group name remains available in the session catalog, group action state updates, and any affected Group Comparison is invalidated.
3. Saved Address with a non-empty Description: row **Remove** and context-menu **Remove address** show the themed Danger confirmation and Cancel preserves the row. Confirm removes it.
4. Saved Address with empty/whitespace Description: single Remove removes it directly without confirmation.
5. Regression: comparer snapshot single Remove and Remove All confirmations remain as implemented in rev44; debugger attach/detach, Universal Export, scanning, Saved Addresses refresh/write/freeze, and existing comparer behavior remain functional.
