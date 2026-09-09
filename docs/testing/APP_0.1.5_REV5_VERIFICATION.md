# TeeKay87's Memory Engine 0.1.5.rev5 Verification

## Revision Under Test

```text
Host application:             0.1.5.rev5
Feature:                      Memory Viewer Origin Selection Layout Fix
Plugin API:                   2.9.0
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Expected automated checks:    51
```

## Purpose

Rev5 is a host-WPF corrective revision for the rev4 Memory Viewer origin marker. Rev4 correctly kept the origin row green independently from DataGrid selection, but added `BorderThickness=1` when that same row was selected. WPF included that border in row measurement, changing row height/width, shifting following rows, and potentially introducing a horizontal scrollbar. Rev5 removes the layout-affecting selected-origin border while preserving the persistent green marker and all rev4 selection/copy/navigation behavior.

`tools/preflight/` remains unchanged and further development of that helper remains paused. The Windows/Roslyn/WPF build is the authoritative compile gate.

## 1. Clean Windows Build

1. Delete stale `bin`/`obj` directories if necessary.
2. Open `TeeKay87.MemoryEngine.sln` in Visual Studio.
3. Choose **Rebuild Solution**.
4. Confirm there are no compiler/WPF markup errors and no warnings.

## 2. Automated Verification Runner

Run `TeeKay87.MemoryEngine.Tests`.

Expected ending:

```text
All 51 checks passed.
```

No Core, plugin, protocol, or test behavior changes in rev5, so the registry remains at 51 checks.

## 3. Selected-Origin Geometry Regression

Using the In-Memory Test Target:

1. Scan for Ammo (`30`) and choose **Browse Memory** for `0x10000104`.
2. Confirm the containing row (`0x10000100`) is green and selected when the viewer opens.
3. Observe the DataGrid row height, column layout, visible horizontal scrollbar state, and the position of at least two rows below the green row.
4. Click another row. Confirm the origin row remains green and the other row receives ordinary selection styling.
5. Click the green origin row again.
6. Confirm the green row has **exactly the same height and width** as it had while not selected.
7. Confirm rows below it do not move vertically.
8. Confirm selecting the green row does not make a horizontal scrollbar appear or alter the current horizontal-scroll range.
9. Alternate selection between the green row and several other rows repeatedly and confirm the table geometry remains stable.
10. Repeat with Ctrl/Shift multi-selection including and excluding the green row.

The selected-origin state is accepted only when selection changes presentation without changing layout geometry.

## 4. Theme Regression

Repeat the selected-origin test in **Light**, **Dimmed**, and **Dark**. Confirm:

- the origin row remains clearly green/readable;
- switching themes does not reintroduce a border or geometry change;
- ordinary selection remains visually distinct when another row is selected.

## 5. rev4 Functional Regression

Confirm the rev4 functionality still behaves as documented:

- Extended row selection;
- Copy Address / Hex Bytes / ASCII / Row / Selected;
- `Ctrl+C`;
- Back / Forward and `Alt+Left` / `Alt+Right`;
- Go To history and forward-branch replacement;
- Refresh keeps the origin and does not add history;
- failed navigation keeps the previous snapshot/history;
- selecting rows does not trigger target I/O or change the navigation address.

## 6. Existing Memory Viewer Safety Regression

Confirm Browse Memory from Scan Results and Saved Addresses, region-boundary clamping, Guard rejection, Region / Module and Protection presentation, and target/process/connection-generation mismatch safety remain unchanged. When a live PS5 is available, perform one Browse Memory -> select origin/other row -> Go To -> Back -> Refresh sequence and then another ordinary target action to confirm the command stream remains usable.

## 7. Existing Application Regression Boundary

Confirm no regressions in:

- Scan Results multi-selection **Save Address**;
- First/Next/New Scan;
- Saved Address refresh/write/Freeze/remove;
- Protection presentation;
- universal export and pretty JSON;
- Connect/Disconnect/process/Active Target/plugin reload;
- PS5 native/resident scan behavior when live testing is available.

## Static Preparation Review

Before packaging, verify:

- `AppInfo` reports `0.1.5.rev5` and feature title `Memory Viewer Origin Selection Layout Fix`;
- the Memory Viewer origin-row style contains the persistent `SuccessMutedBrush` background trigger;
- no origin/selection trigger sets `BorderThickness`, padding, margin, height, width, or another layout-affecting row property;
- Plugin API remains `2.9.0`;
- PS5 plugin remains `0.1.0.rev22`;
- Mock plugin remains `1.0.0.rev4`;
- the automated registry remains exactly 51 checks;
- `tools/preflight/` is unchanged from rev4;
- XAML/project XML and bundled JSON parse successfully;
- all relative Markdown links resolve;
- no `bin`, `obj`, or `.vs` directories are packaged;
- ZIP extraction reproduces the release tree byte-for-byte.

## Actual Windows / Runtime Result

User verification completed before rev6 development began:

- Windows build completed successfully;
- automated verification finished with **All 51 checks passed**;
- runtime selection testing confirmed the green origin row keeps the same width/height when selected and no longer introduces a horizontal scrollbar or shifts following rows.

`0.1.5.rev5` is therefore accepted as fully verified.

## Acceptance

`0.1.5.rev5` is accepted when the solution builds cleanly on Windows, all **51/51** automated checks pass, selecting/deselecting the green origin row never changes its dimensions or the DataGrid scrollbar/layout, rev4 selection/copy/history behavior remains intact, and no previously verified scanner/Saved Address/export/PS5 behavior regresses.

After rev5 is accepted, the next planned functional Memory Viewer revision is **`0.1.5.rev6` safe editing/write support**. Bookmarks, richer region handling, and runtime-driven additions remain later `0.1.5` work; architecture-neutral disassembly remains the expected next major feature block after Memory Viewer completion and verification.
