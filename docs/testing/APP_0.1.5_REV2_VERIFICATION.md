# TeeKay87's Memory Engine 0.1.5.rev2 Verification

## Revision Under Test

```text
Host application:             0.1.5.rev2
Feature:                      Memory Viewer XAML Compile Fix and Source Preflight
Plugin API:                   2.9.0
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Expected automated checks:    51
```

## Purpose

Rev2 is a corrective revision for the `0.1.5.rev1` Memory Viewer foundation. Rev1's first Windows build failed in WPF markup compilation because newly added `MenuItem.Click` handlers were placed inside `ContextMenu` objects created through `DataGridRow` `Setter.Value`. The XAML was valid XML, so the previous source-only structural checks did not detect the problem.

Rev2 keeps the Memory Viewer/Core design and the requested Scan Results multi-selection Save Address behavior unchanged. It moves the affected context menus to concrete `DataGrid.ContextMenu` instances, prepares their row DataContext when the menu opens, and adds the repository source-preflight tool as a new source-preparation gate.


## Observed Windows Build Result

The first Windows build of the packaged rev2 source **FAILED** before automated/runtime acceptance. The rev1 `MC6007` event-placement error was removed, but Roslyn reported:

```text
CS0136 A local or parameter named 'contextMenu' cannot be declared in this scope because that name is used in an enclosing local scope to define a local or parameter.
MainWindow.xaml.cs line 151
```

The surrounding `System.Object`, `ProportionalGridSplitter`, `TextBoxInputFilter`, `UiMetrics`, and related XAML/designer diagnostics are treated as cascading build failures until the host C# project compiles. The correction is implemented in `0.1.5.rev3`. Rev2 must therefore not be recorded as build-verified or accepted.

## 1. Source Preflight

From the repository root run:

```text
tools\preflight\Run-SourcePreflight.cmd
```

Expected ending:

```text
Source preflight passed. A real Windows/WPF build is still required before release verification.
```

Confirm the output contains PASS entries for:

- XAML/XML structure and event wiring;
- StaticResource definitions;
- source-backed `clr-namespace` XAML types;
- JSON syntax;
- project references;
- version/revision/verification consistency;
- release-tree artifact check.

The preflight may print a warning only if `PresentationFramework` reflection cannot be loaded. On the normal Windows development environment used for this project, WPF reflection should be available.

## 2. Clean Windows Build

1. Delete previous `bin` and `obj` directories if necessary.
2. Open `TeeKay87.MemoryEngine.sln` in Visual Studio.
3. Choose **Rebuild Solution**.
4. Confirm there are no compiler or WPF markup errors and no warnings.

The rev1 failure must no longer occur. In particular, confirm there is no `MC6007` involving `ScanResultsSaveAddressMenuItem_Click`/`MenuItem.Click`, and no cascading false errors for `System.Object`, `ProportionalGridSplitter`, `TextBoxInputFilter`, or `UiMetrics` caused by a failed application markup build.

## 3. Automated Verification Runner

Run `TeeKay87.MemoryEngine.Tests`.

Expected ending:

```text
All 51 checks passed.
```

Rev2 does not change Core Memory Viewer semantics, scanner/export behavior, Plugin SDK contracts, or PS5 protocol behavior, so the automated registry remains 51 checks.

## 4. Scan Results Context Menu and Multi-Selection

With the In-Memory Test Target:

1. Connect and set `Mock Game` as Active Target.
2. Produce at least three Scan Results.
3. Select three rows with Ctrl/Shift.
4. Right-click one of the selected rows.
5. Confirm the context menu opens and shows **Save Address**, **Browse Memory**, **Copy address**, and **Copy value**.
6. Choose **Save Address**.
7. Confirm all selected rows are added and duplicates are not created on a repeated action.
8. Select multiple rows and then right-click a row outside that selection.
9. Confirm that outside row becomes the context row instead of bulk-saving the unrelated previous selection.
10. Confirm **Copy address** and **Copy value** still operate on the row that supplied the context menu.
11. Confirm double-click remains a single-row Save Address shortcut.

## 5. Scan Results Browse Memory

1. Right-click a Scan Result.
2. Choose **Browse Memory**.
3. Confirm the read-only Memory Viewer opens at the clicked address.
4. Confirm the viewer still shows Address/Hex Bytes/ASCII, Region / Module, Protection, visible range, Go To, and Refresh as specified by rev1.

## 6. Saved Addresses Context Menu

1. Save at least one result.
2. Right-click the Saved Address.
3. Confirm the context menu shows the expected Freeze/Unfreeze action, **Browse Memory**, **Copy address**, **Copy value**, and **Remove address**.
4. Verify Freeze/Unfreeze still targets the context row.
5. Verify Copy address/value still target the context row.
6. Verify Remove address still targets the context row.
7. Choose **Browse Memory** and confirm the viewer opens at that Saved Address.

## 7. Memory Viewer Regression

Repeat the rev1 Mock viewer smoke test:

- valid Go To;
- Refresh;
- invalid/out-of-region address handling;
- requested row selection/scrolling;
- readable-region boundary clamping;
- Region / Module and Protection presentation;
- closing the modeless viewer without affecting the main workspace.

When a live PS5 is available, repeat at least one Browse Memory + Refresh sequence against `eboot.bin` and then run another normal target operation to confirm the command stream remains usable.

## 8. Existing Regression Boundary

Confirm no regressions in:

- First/Next/New Scan;
- 50,000-row display/complete-result separation;
- Saved Address refresh, direct write, Freeze, Remove, and Remove All;
- Protection columns;
- Scan Results and Saved Addresses export;
- pretty JSON export;
- Connect/Disconnect, process refresh, Active Target, and plugin reload;
- PS5 native/resident scan behavior when live testing is available.

## Static Preparation Review

Before packaging, verify:

- `AppInfo` is `0.1.5.rev2` with feature title `Memory Viewer XAML Compile Fix and Source Preflight`;
- Plugin API remains `2.9.0`;
- PS5 plugin remains `0.1.0.rev22`;
- Mock plugin remains `1.0.0.rev4`;
- Core/Plugin SDK/plugin/test production behavior is unchanged except for the intended host XAML/context-menu fix;
- the old `MenuItem.Click` handlers no longer exist beneath a `Setter.Value` object graph;
- the source preflight contains a rule rejecting that pattern;
- XAML/project XML and bundled JSON parse successfully;
- all relative Markdown links resolve;
- the test registry remains exactly 51 checks;
- no `bin`, `obj`, or `.vs` directories are packaged;
- ZIP extraction reproduces the release tree byte-for-byte.

## Acceptance

`0.1.5.rev2` is accepted when:

- source preflight passes;
- the complete solution builds cleanly on Windows;
- all **51/51** automated checks pass;
- Scan Results multi-selection Save Address works through the corrected context menu;
- Browse Memory works from both Scan Results and Saved Addresses;
- existing row context actions still target the correct row;
- no previously verified scanner, Saved Address, export, or PS5 behavior regresses.

Once rev2 is accepted, the next functional Memory Viewer stage should continue with selection/copy/navigation history. Because rev2 is a corrective revision, that planned feature stage moves to the next revision rather than being mixed into the compile fix.
