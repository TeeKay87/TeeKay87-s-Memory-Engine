# 0.1.1.rev9 Proportional Workspace Splitter Range Verification

## Purpose

This document records source-level verification and required Windows runtime checks for **TeeKay87's Memory Engine 0.1.1.rev9 - Proportional Workspace Splitter Range**.

Rev8 successfully protected the full-height Scan panel from destructive width changes, but live Windows testing showed that fixed pixel bounds were the wrong model for the horizontal Scan Results/Saved Addresses split. The useful vertical range needs to scale with the available workspace, especially in fullscreen.

## Proportional Horizontal Split

The left workspace now uses:

- Scan Results row: `*`;
- splitter track: `14 px`;
- Saved Addresses row: `*`.

This produces a **50% / 50%** initial allocation of the height available to the two data panels.

The horizontal splitter is a reusable host UI control, `ProportionalGridSplitter`, configured with:

- minimum previous-panel ratio: **0.20**;
- maximum previous-panel ratio: **0.80**.

For the current layout, the previous panel is Scan Results. Saved Addresses always receives the complementary share. The reachable range is therefore:

- Scan Results **20%** / Saved Addresses **80%**;
- through the 50% / 50% default;
- to Scan Results **80%** / Saved Addresses **20%**.

The horizontal splitter uses live resizing (`ShowsPreview=False`), and the control normalizes both neighboring definitions to star sizing during drag changes. This avoids a fixed pixel maximum and allows the selected proportion to continue scaling when the main window grows or shrinks.

## Preserved Vertical Split

The Scan-panel width rules from rev8 are intentionally unchanged:

- left workspace minimum width: **640 px**;
- splitter track: **14 px**;
- Scan panel preferred width: **310 px**;
- Scan panel minimum width: **280 px**;
- Scan panel maximum width: **420 px**.

The user specifically confirmed that these Scan-panel limits can remain.

## Source-Level Verification

Release preparation must verify that:

- `AppInfo` reports `0.1.1.rev9` and feature title `Proportional Workspace Splitter Range`;
- the two left-side content rows both use `Height="*"`;
- the horizontal divider uses `ProportionalGridSplitter` with `MinimumPreviousRatio="0.2"`, `MaximumPreviousRatio="0.8"`, and live resizing (`ShowsPreview="False"`);
- the old `240 px`, `180 px`, `235 px`, and `360 px` horizontal row constraints are no longer present in `MainWindow.xaml`;
- the vertical Scan-panel width definitions remain unchanged from rev8;
- the shared splitter visual styles remain unchanged from rev8;
- Core, Plugin SDK, Mock plugin, PS5 plugin, themes, and backend/protocol tests are unchanged from rev8;
- plugin versions and Plugin API version remain unchanged;
- XAML/project XML parse successfully;
- release contents exclude `bin`, `obj`, `.vs`, `.user`, and `.suo` artifacts.

## Required Windows Runtime Verification

1. Run **Build -> Rebuild Solution** and confirm there are no compiler warnings or errors.
2. Start the application and confirm Scan Results and Saved Addresses initially divide the available left-side vertical workspace approximately 50% / 50%.
3. Drag the horizontal splitter upward until it stops and confirm the visible allocation is approximately 20% Scan Results / 80% Saved Addresses.
4. Drag the horizontal splitter downward until it stops and confirm the visible allocation is approximately 80% Scan Results / 20% Saved Addresses.
5. Confirm neither panel can be dragged beyond those ratio limits.
6. Repeat the two extremes in fullscreen and confirm the available resize distance grows with the larger workspace instead of stopping at the old fixed pixel limits.
7. Set an intermediate ratio, resize the main window, and confirm the two panels continue to scale proportionally.
8. Confirm the Scan panel still remains between 280 px and 420 px and its controls do not distort at either vertical-splitter extreme.
9. Switch among Light, Dimmed, and Dark and confirm both splitter behaviors are theme-independent.
10. Reconnect to ps5debug-NG and confirm connection, process enumeration, and Active Target behavior remain unaffected.

Runtime results should be appended here after the Windows checks are performed.


## Windows Runtime Result - 2026-08-30

The proportional splitter behavior was verified in the Windows WPF application. The user confirmed that the splitters now work well: Scan Results and Saved Addresses start at the intended equal allocation and the horizontal range behaves correctly, while the Scan-panel width bounds remain suitable.

The same runtime pass exposed a separate presentation inconsistency for the next revision: standard controls in the target/connection area did not share one height. The Platform selector established the desired height, while buttons were taller and the Target Process selector stretched to the taller row. This observation is addressed separately in `0.1.1.rev10`; it does not invalidate the rev9 splitter result.
