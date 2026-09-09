# TeeKay87's Memory Engine 0.1.5.rev1 Verification

## Revision Under Test

```text
Host application:             0.1.5.rev1
Feature:                      Memory Viewer Foundation
Plugin API:                   2.9.0
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Expected automated checks:    51
```


## Verification Result

**FAILED at the first clean Windows/WPF build.**

The automated/runtime sections were not reached because WPF markup compilation stopped with `MC6007`: `ScanResultsSaveAddressMenuItem_Click` was reported as an invalid `Click` member while compiling `MainWindow.xaml`. The root cause was the new rev1 `MenuItem.Click` code-behind wiring inside `ContextMenu` objects instantiated through `DataGridRow` `Setter.Value`. The remaining `System.Object`, `ProportionalGridSplitter`, `TextBoxInputFilter`, and `UiMetrics` designer/build messages were cascading errors after the application markup build failed.

The correction is implemented in `0.1.5.rev2`, which moves those context menus to concrete `DataGrid.ContextMenu` instances and introduces the repository source-preflight gate. Rev1 must therefore not be treated as build-verified.

## Purpose

This revision begins the `0.1.5` Memory Viewer feature block with a bounded read-only viewer and also fixes Scan Results multi-selection Save Address behavior.

The verification goal is to prove that the new viewer consumes existing neutral memory services without regressing scanner, Saved Addresses, export, plugin, or PS5 protocol behavior.

## 1. Clean Windows Build

1. Remove previous `bin` and `obj` directories if necessary.
2. Open `TeeKay87.MemoryEngine.sln` in Visual Studio.
3. Rebuild the complete solution.
4. Confirm there are no compiler warnings/errors. Warnings are treated as errors by the repository build configuration.

**Pass condition:** the application, Core, Plugin SDK, plugins, and verification executable build successfully.

## 2. Automated Verification Runner

Run `TeeKay87.MemoryEngine.Tests`.

A successful run must end with:

```text
All 51 checks passed.
```

The two new Core checks are:

- **Memory Viewer bounded readable window** — verifies the requested address remains inside the returned page, default page size is bounded, and reads near both ends of a region stay inside that region;
- **Memory Viewer guarded-region rejection** — verifies a Read+Guard region is not accepted as a viewer-readable range.

All previous 49 scanner/export/plugin/PS5 checks must remain green.

## 3. Mock Target Memory Viewer

Use **In-Memory Test Target** for the first UI verification.

1. Connect.
2. Set `Mock Game` as Active Target.
3. Run an Exact Value scan that produces at least one result, for example the existing mock Ammo value.
4. Right-click the result and choose **Browse Memory**.
5. Confirm a separate Memory Viewer window opens.
6. Confirm the target line identifies the Mock plugin and Mock Game.
7. Confirm the initial address field contains the selected Scan Result address.
8. Confirm the table shows Address, Hex Bytes, and ASCII columns.
9. Confirm each normal row represents 16 bytes.
10. Confirm the row containing the requested address is selected and automatically scrolled into view.
11. Confirm the table contains only the declared Address, Hex Bytes, and ASCII columns; no model-property columns are auto-generated.
12. Confirm Region / Module, region range, Protection, and visible range are populated from the mock memory map.

## 4. Go To and Refresh

In the Mock Memory Viewer:

1. Enter another valid hexadecimal address inside Mock Main Memory.
2. Press Enter or choose **Go To**.
3. Confirm the viewer moves to that address and selects its containing row.
4. Choose **Refresh** and confirm the same bounded range is reread.
5. Enter invalid/non-hexadecimal text and confirm live input filtering/final validation prevents an invalid target read.
6. Enter an address outside the loaded readable region and confirm the previous view remains visible while the viewer reports a readable-region error.

## 5. Region Boundary Verification

Use addresses close to the beginning and end of Mock Main Memory.

Confirm:

- the visible range never begins before the region base;
- the visible range never ends after the region end;
- the viewer does not invent data from a neighboring/unmapped range;
- a near-end request remains inside the returned page even when a synthetic containing-region boundary is not 16-byte aligned;
- Region / Module and Protection continue to describe the complete visible page.

## 6. Saved Addresses Browse Memory

1. Save a scan result into Saved Addresses.
2. Right-click the Saved Address and choose **Browse Memory**.
3. Confirm the initial address matches the Saved Address.
4. Confirm the viewer reads only while the Saved Address target is the current Active Target.
5. Change Active Target to another process where a suitable multi-process plugin is available, or disconnect/reconnect.
6. Refresh the old viewer.

**Pass condition:** the old viewer must not silently redirect to the new/reconnected target. It must report that the original target/connection is no longer valid for that viewer.

## 7. Scan Results Multi-Selection Save Address

1. Produce at least three Scan Results.
2. Select three rows using Ctrl/Shift multi-selection.
3. Right-click **one of the selected rows**.
4. Choose **Save Address**.
5. Confirm all three selected rows are added to Saved Addresses.
6. Repeat the same action with those rows still selected.
7. Confirm no duplicate Saved Address rows are created and the status reports that the addresses already exist.
8. Select multiple rows, right-click a row that is **not** part of the existing selection if the WPF selection behavior allows it, and confirm the action does not unexpectedly bulk-save an unrelated old selection.
9. Double-click one Scan Result and confirm the existing single-row Save Address shortcut remains single-row.

## 8. PS5 Live Viewer

With a real PS5/ps5debug-NG target:

1. Connect normally and set `eboot.bin` Active Target.
2. Use a known readable Scan Result or Saved Address.
3. Choose **Browse Memory**.
4. Confirm the viewer loads data around that address.
5. Confirm Region / Module remains blank when the PS5 map entry has no name rather than manufacturing one.
6. Confirm Protection matches the memory-map flags already shown in Scan Results/Saved Addresses.
7. Use **Refresh** several times and confirm the ps5debug-NG command stream remains usable afterward.
8. Close the viewer and run First/Next Scan or Saved Address refresh/write operations to smoke-test the shared target-operation coordination.

## 9. Theme Verification

Open the Memory Viewer in:

- Light;
- Dimmed;
- Dark.

Confirm the window uses the existing themed surfaces, text, borders, inputs, buttons, selection, and error/status presentation without operating-system light chrome appearing inside the content area.

## 10. Regression Checks

Confirm the following remain unchanged:

- First Scan / Next Scan / New Scan;
- 50,000-row presentation boundary and complete-result semantics;
- Saved Address value refresh;
- Saved Address direct writes and Frozen behavior;
- Protection columns;
- Scan Results and Saved Addresses export;
- indented JSON export;
- Connect/Disconnect and Set Active Target;
- plugin reload;
- PS5 native scanning and resident-result behavior.

## Static Preparation Review

Before packaging, verify:

- `AppInfo` is `0.1.5.rev1` with feature title `Memory Viewer Foundation`;
- Plugin API and plugin version constants are unchanged;
- no PS5 plugin source change was required by Memory Viewer;
- all new C# files contain the required explicit `using` directives;
- all XAML/project XML parses successfully;
- bundled JSON theme files parse successfully;
- relative Markdown links resolve;
- the automated test registry contains exactly 51 checks;
- release tree contains no `bin`, `obj`, or `.vs` artifacts;
- ZIP extraction reproduces the release tree byte-for-byte.

## Acceptance

`0.1.5.rev1` is accepted when:

- the complete solution builds on Windows;
- all **51/51** automated checks pass;
- the Mock and live-target Memory Viewer behavior above is correct;
- multi-selected Scan Results Save Address saves the selected set without duplicates;
- an old Memory Viewer cannot silently move to another/reconnected target;
- no previously verified scanner, Saved Address, export, or PS5 behavior regresses.
