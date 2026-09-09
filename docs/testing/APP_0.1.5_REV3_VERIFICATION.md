# TeeKay87's Memory Engine 0.1.5.rev3 Verification

## Revision Under Test

```text
Host application:             0.1.5.rev3
Feature:                      Memory Viewer C# Scope Compile Fix and Preflight Hardening
Plugin API:                   2.9.0
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Expected automated checks:    51
```

## Purpose

Rev3 is a corrective revision over the rev1 Memory Viewer foundation and rev2 XAML/context-menu correction. Rev2 removed the WPF `MC6007` failure but its first Windows build exposed `CS0136` in `TryPrepareRowContextMenu<TItem>()` because the same `contextMenu` pattern-variable name was declared twice in overlapping C# declaration spaces.

Rev3 does not redesign the Memory Viewer or row context menus. It reads the DataGrid context menu once into a nullable local and reuses that local. Source preflight is hardened with a conservative repeated typed-pattern-variable rule so the rev2 failure class is rejected before the next Windows build.

## 1. Source Preflight

From the repository root run:

```text
tools\preflight\Run-SourcePreflight.cmd
```

Expected ending:

```text
Source preflight passed. A real Windows/WPF build is still required before release verification.
```

Confirm PASS entries include:

- XAML/XML structure and event wiring;
- StaticResource definitions;
- source-backed `clr-namespace` XAML types;
- **C# pattern-variable declaration spaces**;
- JSON syntax;
- project references;
- version/revision/verification consistency;
- release-tree artifact check.

The current source must not report repeated `contextMenu` pattern variables in `TryPrepareRowContextMenu<TItem>()`.

## 2. Clean Windows Build

1. Delete stale `bin`/`obj` directories if necessary.
2. Open `TeeKay87.MemoryEngine.sln` in Visual Studio.
3. Choose **Rebuild Solution**.
4. Confirm there are no compiler or WPF markup errors and no warnings.

Specifically confirm that rev2's:

```text
CS0136 ... 'contextMenu' ... MainWindow.xaml.cs
```

is gone. Once the App project compiles, the cascading `System.Object`, `ProportionalGridSplitter`, `TextBoxInputFilter`, `UiMetrics`, and related designer errors must also be absent.

## 3. Automated Verification Runner

Run `TeeKay87.MemoryEngine.Tests`.

Expected ending:

```text
All 51 checks passed.
```

Rev3 changes no Core/plugin protocol semantics and therefore does not add or remove automated checks.

## 4. Scan Results Context Menu and Multi-Selection

With the In-Memory Test Target:

1. Produce at least three Scan Results.
2. Select multiple rows with Ctrl/Shift.
3. Right-click one selected row and choose **Save Address**.
4. Confirm all selected rows are added and duplicates are skipped on a repeated action.
5. Right-click a row outside the previous selection and confirm only that row becomes the context row.
6. Confirm **Browse Memory**, **Copy address**, and **Copy value** target the correct row.
7. Confirm double-click remains a single-row Save Address shortcut.

## 5. Saved Addresses Context Menu

1. Right-click a Saved Address.
2. Confirm Freeze/Unfreeze, **Browse Memory**, **Copy address**, **Copy value**, and **Remove address** target the row that was clicked.
3. Confirm no stale row DataContext survives when a context menu is opened without a valid row under the pointer.

## 6. Memory Viewer Regression

Repeat the rev1/rev2 Mock smoke test:

- Browse Memory from Scan Results;
- Browse Memory from Saved Addresses;
- valid Go To;
- Refresh;
- invalid/out-of-region handling;
- requested-row selection/scrolling;
- readable-region boundary clamping;
- Region / Module and Protection presentation;
- close the modeless viewer without affecting the main workspace.

When a live PS5 is available, repeat at least one Browse Memory + Refresh sequence against `eboot.bin` and then perform another normal target operation to confirm the command stream remains usable.

## 7. Existing Regression Boundary

Confirm no regressions in First/Next/New Scan, 50,000-row preview/complete-result separation, Saved Address refresh/write/Freeze/Remove/Remove All, Protection presentation, universal export and pretty JSON, Connect/Disconnect/process/Active Target/plugin reload, and PS5 native/resident scan behavior when live testing is available.

## Static Preparation Review

Before packaging, verify:

- `AppInfo` is `0.1.5.rev3` with feature title `Memory Viewer C# Scope Compile Fix and Preflight Hardening`;
- Plugin API remains `2.9.0`;
- PS5 plugin remains `0.1.0.rev22`;
- Mock plugin remains `1.0.0.rev4`;
- the row-context helper contains one nullable ContextMenu local rather than duplicate pattern declarations;
- the preflight rule flags the exact rev2 `contextMenu` conflict under regression analysis and reports none in rev3;
- XAML/project XML and bundled JSON parse successfully;
- all relative Markdown links resolve;
- the test registry remains exactly 51 checks;
- no `bin`, `obj`, or `.vs` directories are packaged;
- ZIP extraction reproduces the release tree byte-for-byte.

## Acceptance

`0.1.5.rev3` is accepted when source preflight passes, the complete solution builds cleanly on Windows, all **51/51** automated checks pass, the corrected context menus preserve multi-selection/row targeting, Memory Viewer still works from both entry points, and no previously verified scanner/Saved Address/export/PS5 behavior regresses.

After rev3 is accepted, `0.1.5.rev4` should resume the planned selection/copy/navigation-history stage.
