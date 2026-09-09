# TeeKay87's Memory Engine 0.1.5.rev4 Verification

## Revision Under Test

```text
Host application:             0.1.5.rev4
Feature:                      Memory Viewer Selection Copy and Navigation
Plugin API:                   2.9.0
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Expected automated checks:    51
```


## Actual Runtime Result

Rev4 reached runtime and the persistent origin marker, selection, copy, and navigation behavior were functional, but the selected-origin presentation failed the visual/layout acceptance boundary. When the green origin row itself was selected, its `DataGridRow` received `BorderThickness=1`, increasing the row's desired size by a few pixels. Rows below shifted downward and a horizontal scrollbar could appear. Rev4 is therefore superseded by `0.1.5.rev5` for this presentation defect rather than being considered fully accepted.

## Purpose

Rev4 resumes functional Memory Viewer development after the rev2-rev3 compile corrections. The Core reader and platform/plugin boundaries remain unchanged. The revision adds extended row selection, clipboard operations, successful-address Back/Forward history, and a persistent green origin-row marker that remains independent from normal DataGrid selection.

The existing files under `tools/preflight/` remain in the repository, but further development of that helper is paused. Rev4 does not add or change preflight rules. A clean Windows/Roslyn/WPF build is the authoritative compile gate.

## 1. Clean Windows Build

1. Delete stale `bin`/`obj` directories if necessary.
2. Open `TeeKay87.MemoryEngine.sln` in Visual Studio.
3. Choose **Rebuild Solution**.
4. Confirm there are no compiler or WPF markup errors and no warnings.

Pay particular attention to `MemoryViewerWindow.xaml`, `MemoryViewerWindow.xaml.cs`, `MemoryViewerViewModel.cs`, `MemoryViewerRowViewModel.cs`, and `ThemeManager.cs`, because rev4 changes host WPF/event/command presentation in those files.

## 2. Automated Verification Runner

Run `TeeKay87.MemoryEngine.Tests`.

Expected ending:

```text
All 51 checks passed.
```

Rev4 changes host/WPF presentation and navigation state only. The two existing Core Memory Viewer checks remain the automated reader boundary, and no Plugin SDK/plugin/test registration is changed.

## 3. Persistent Origin-Row Highlight

Using the In-Memory Test Target:

1. Scan for a deterministic value such as Ammo (`30`) and choose **Browse Memory** on the result at `0x10000104`.
2. Confirm the row containing `0x10000104` is selected when the viewer opens.
3. Confirm the same row is visibly highlighted with the green origin marker.
4. Click another row.
5. Confirm the clicked row receives the normal theme selection color while the original `0x10000104` row remains green.
6. Ctrl-click/Shift-select multiple other rows and confirm the origin row remains green.
7. Click the green origin row itself. This check exposed the rev4 defect: the selection-only focus/accent border changed row geometry. Rev5 removes that layout-affecting border.
8. Choose **Refresh** and confirm the origin marker remains on the same requested-address row.
9. Switch Light, Dimmed, and Dark themes while the viewer is open and confirm the origin remains clearly green/readable in all three themes.

The green marker represents the current navigation/origin address, not ordinary selection.

## 4. Row Selection and Context Menu

1. Select multiple Memory Viewer rows using Ctrl and Shift.
2. Right-click a row already inside that selection and confirm the selected set is preserved.
3. Right-click a row outside the current selection and confirm that row becomes the only selected row before row-specific copy commands execute.
4. Confirm clicking/selecting rows does not change the Address field, does not move the green origin marker, and does not trigger a memory read.

## 5. Clipboard Actions

For a known Memory Viewer row, verify:

- **Copy Address** copies only the row address.
- **Copy Hex Bytes** copies only the displayed hexadecimal byte sequence.
- **Copy ASCII** copies only the displayed ASCII representation.
- **Copy Row** copies one tab-separated `Address<TAB>Hex Bytes<TAB>ASCII` line.
- **Copy Selected** copies every selected row in visible table order, one tab-separated row per line.
- `Ctrl+C` produces the same selected-row output as **Copy Selected**.

Confirm successful copies update the status text and a clipboard failure, if forced by the environment, is reported without closing the viewer.

## 6. Navigation History

1. Open Memory Viewer at address A.
2. Confirm **Back** and **Forward** are initially disabled.
3. Go To a valid address B and confirm the origin marker moves to B.
4. Go To a valid address C and confirm the origin marker moves to C.
5. Choose **Back** and confirm the viewer rereads B, restores B in the Address field, and moves the green marker to B.
6. Choose **Back** again and confirm A is restored.
7. Choose **Forward** and confirm B is restored.
8. Verify `Alt+Left` and `Alt+Right` invoke the same Back/Forward behavior.
9. Choose **Refresh** and confirm it does not add a new history entry.
10. Click several rows and confirm selection does not alter Back/Forward history.
11. While positioned at B after navigating Back, Go To a new valid address D. Confirm Forward is disabled afterward because the old C branch was replaced.
12. Go To the address already at the current history position and confirm it does not create a duplicate history entry.
13. Enter an invalid/unreadable address and confirm the failed read leaves the previous visible snapshot, green origin marker, and history position unchanged.

## 7. Existing Memory Viewer Safety Regression

Repeat the rev1/rev3 safety checks:

- Browse Memory from Scan Results;
- Browse Memory from Saved Addresses;
- valid Go To;
- Refresh;
- invalid/out-of-region handling;
- readable-region boundary clamping;
- Guarded-region rejection;
- Region / Module and Protection presentation;
- target/process/connection-generation mismatch rejection;
- close one modeless viewer without affecting the main workspace.

When a live PS5 is available, perform at least one Browse Memory -> Go To -> Back -> Refresh sequence against `eboot.bin`, then run another normal target operation to confirm the command stream remains usable.

## 8. Scan Results Multi-Selection Save Address Regression

Because rev4 changes only Memory Viewer selection, confirm the existing main-workspace behavior remains intact:

1. Produce multiple Scan Results.
2. Select multiple rows.
3. Right-click one selected row and choose **Save Address**.
4. Confirm all selected current results are processed and duplicates are skipped.
5. Confirm double-click still saves only the double-clicked Scan Result.

## 9. Existing Application Regression Boundary

Confirm no regressions in:

- First/Next/New Scan;
- 50,000-row preview/complete-result separation;
- Saved Address refresh, direct write, Freeze, Remove, and Remove All;
- Protection presentation;
- universal export and pretty JSON;
- Connect/Disconnect/process/Active Target/plugin reload;
- PS5 native/resident scan behavior when live testing is available.

## Static Preparation Review

Before packaging, verify:

- `AppInfo` reports `0.1.5.rev4` and feature title `Memory Viewer Selection Copy and Navigation`;
- Plugin API remains `2.9.0`;
- PS5 plugin remains `0.1.0.rev22`;
- Mock plugin remains `1.0.0.rev4`;
- Memory Viewer uses `SelectionMode="Extended"`;
- `IsOriginRow` is independent from `SelectedRow` and drives the green row style;
- Back/Forward/Go To/Refresh commands are all wired to the current ViewModel;
- clipboard event handlers exist in the concrete `MemoryViewerWindow` code-behind and no new event is placed inside a style `Setter.Value` object graph;
- the current history is modified only after successful reads;
- `SuccessMutedBrush` is derived at theme application time from `SuccessText` and does not become a new required theme JSON key;
- `tools/preflight/Invoke-SourcePreflight.ps1` and `tools/preflight/Run-SourcePreflight.cmd` are unchanged from rev3;
- XAML/project XML and bundled JSON parse successfully;
- all relative Markdown links resolve;
- the test registry remains exactly 51 checks;
- no `bin`, `obj`, or `.vs` directories are packaged;
- ZIP extraction reproduces the release tree byte-for-byte.

## Acceptance

`0.1.5.rev4` is accepted when the solution builds cleanly on Windows, all **51/51** automated checks pass, origin highlighting remains green while other rows are selected, all copy actions work, Back/Forward history follows the rules above, and no previously verified scanner/Saved Address/export/PS5 behavior regresses.

Rev4 requires the rev5 layout correction before the feature stage can be considered visually accepted. After rev5 is accepted, the next planned functional Memory Viewer stage is **safe editing/write support** in rev6. Bookmarks, richer region handling, and runtime-driven additions follow later in `0.1.5`. Architecture-neutral disassembly remains the expected next major feature block after the Memory Viewer is complete and verified.
