# 0.1.1.rev8 Workspace Splitter Bounds Fix Verification

## Purpose

This document records source-level verification and required Windows runtime checks for **TeeKay87's Memory Engine 0.1.1.rev8 - Workspace Splitter Bounds Fix**.

The revision addresses the resize-boundary issue observed during live rev7 testing. It changes only host application workspace constraints, application version metadata, and related documentation. Core, Plugin SDK, Mock plugin, PS5 plugin, process enumeration, target selection, theme loading, and protocol behavior are not changed.

## Layout Constraint Correction

The vertical workspace splitter now operates inside a deliberately bounded range:

- left Scan Results/Saved Addresses workspace: minimum width **640 px**;
- splitter track: **14 px**;
- Scan panel: preferred width **310 px**, minimum width **280 px**, maximum width **420 px**.

The horizontal splitter inside the left workspace is also bounded:

- Scan Results: minimum height **240 px**;
- splitter track: **14 px**;
- Saved Addresses: preferred height **235 px**, minimum height **180 px**, maximum height **360 px**.

The existing centered 4-pixel splitter handles and 14-pixel hit areas from rev7 are preserved. The correction is limited to the `ColumnDefinition` and `RowDefinition` constraints that WPF `GridSplitter` uses when calculating valid drag deltas.

## Source-Level Verification

Release preparation verifies that:

- `AppInfo` reports `0.1.1.rev8` and feature title `Workspace Splitter Bounds Fix`;
- the main workspace left column has `MinWidth=640`;
- the Scan panel column has `Width=310`, `MinWidth=280`, and `MaxWidth=420`;
- Scan Results has `MinHeight=240`;
- Saved Addresses has `Height=235`, `MinHeight=180`, and `MaxHeight=360`;
- both splitter tracks remain 14 pixels and continue using the shared rev7 splitter styles;
- the existing Scan, Scan Results, and Saved Addresses controls are otherwise unchanged;
- XAML and project XML parse successfully;
- Core, Plugin SDK, Mock plugin, PS5 plugin, and existing backend/protocol tests are unchanged from rev7;
- plugin versions and Plugin API version remain unchanged;
- release contents exclude `bin`, `obj`, `.vs`, `.user`, and `.suo` artifacts.

## Required Windows Runtime Verification

1. Run **Build -> Rebuild Solution** and confirm there are no compiler warnings/errors.
2. Start the application at its minimum supported window size and confirm the workspace remains usable.
3. Drag the vertical splitter fully toward the Scan panel and confirm the Scan panel stops at its minimum usable width rather than collapsing.
4. Drag the vertical splitter fully toward the left workspace and confirm the Scan panel stops at its maximum width and the left-side toolbars/data grids remain intact.
5. Confirm First Scan / Next Scan / New Scan controls retain normal dimensions at both vertical splitter extremes.
6. Drag the horizontal splitter fully upward and confirm Saved Addresses stops at its maximum height rather than consuming the entire left workspace.
7. Drag the horizontal splitter fully downward and confirm Saved Addresses stops at its minimum height and Scan Results retains its minimum usable height.
8. Resize the main window larger and smaller after moving both splitters and confirm the constraints continue to hold.
9. Switch among Light, Dimmed, and Dark and confirm the splitter behavior is theme-independent.
10. Reconnect to ps5debug-NG and confirm connection, process enumeration, and Active Target behavior remain unaffected.

Runtime results should be appended here after the Windows checks are performed.

## Windows Runtime Result - 2026-08-30

The user confirmed that rev8 builds and runs, and the vertical Scan-panel limits are appropriate. Live testing also showed that the fixed pixel bounds on the horizontal Scan Results/Saved Addresses split are too restrictive, particularly in fullscreen: the lower panel can only move through a narrow absolute-height interval even when substantially more vertical workspace is available.

The required behavior was refined to an equal **50% / 50%** starting allocation with a proportional resize range of approximately **20% / 80%** through **80% / 20%**. This is a follow-up layout behavior change rather than a regression in the rev8 vertical Scan-panel fix and is implemented in `0.1.1.rev9`.
