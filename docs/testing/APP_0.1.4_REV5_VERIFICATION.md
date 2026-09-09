# TeeKay87's Memory Engine 0.1.4.rev5 Verification

## Revision Under Test

```text
Host application:             0.1.4.rev5
Feature:                      Saved Address Protection Rendering Alignment Fix
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Plugin API:                   2.9.0
Automated checks:             49
```

## Purpose

Rev5 corrects the Saved Addresses **Protection** rendering after rev4 passed the complete 49-check automated suite but failed its required runtime visual test. Rev4 attempted to center the `DataGridTextColumn` by changing `DataGridCell.VerticalContentAlignment`; runtime inspection showed that the generated text element still rendered too high.

Rev5 removes that ambiguous generated-element path for this column. Saved Addresses Protection is now an explicit read-only `DataGridTemplateColumn`. The cell content host stretches vertically and the template-owned `TextBlock` uses `VerticalAlignment="Center"`, so the element that actually draws the Protection text is centered within the available row height.

The revision does not change Protection data, target memory behavior, export data, scanner behavior, Plugin API, or either bundled plugin.

## 1. Clean Windows Build

Rebuild the complete solution from a clean rev5 extraction.

Expected:

- zero errors;
- zero warnings;
- nullable analysis and `TreatWarningsAsErrors` remain enabled;
- no XAML designer/runtime resource errors;
- Plugin API remains `2.9.0`;
- PS5 plugin remains `0.1.0.rev22`;
- Mock plugin remains `1.0.0.rev4`.

## 2. Automated Verification Runner

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 49 checks passed.
```

Rev5 intentionally keeps the existing 49-check registry because the correction is WPF presentation only. Rev4 already demonstrated that the Protection/export/scanner data contracts pass all 49 checks; rev5 must keep that full regression suite green.

## 3. Saved Addresses Protection Vertical Centering

Use the **In-Memory Test Target** first because its deterministic writable region shows a non-empty Protection value without requiring a live console.

1. Connect to **In-Memory Test Target**.
2. Run an Exact Value scan that produces at least one result.
3. Save one result to **Saved Addresses**.
4. Confirm the Saved Addresses **Protection** value is vertically centered in the row.
5. Compare its baseline visually against the text inside the neighboring Address, Type, and Value controls. Protection must no longer sit visibly near the upper edge of the row as it did in rev4.
6. Select and deselect the row and confirm the vertical position does not move.
7. Add several Saved Addresses and confirm every Protection cell is centered identically.
8. Resize the Saved Addresses panel vertically and horizontally and confirm the alignment remains centered.
9. Click the **Protection** column header and confirm the column can still participate in normal DataGrid sorting.
10. Use the DataGrid's normal clipboard/copy path on a row and confirm the Protection cell still contributes its bound text when that path is used.

Expected:

- the Protection text is centered through the full row height;
- it remains centered when selected/unselected;
- no extra vertical clipping, baseline jump, or top alignment is visible;
- Protection retains its explicit sort member and clipboard binding after the move to a template column;
- Description, Address, Type, Value, Frozen, and Remove controls retain their existing layout.

## 4. Theme Verification

Repeat the Saved Addresses alignment check in:

- Light;
- Dimmed;
- Dark.

Expected:

- Protection remains vertically centered in every theme;
- text, selected-row background, focus behavior, borders, and other DataGrid visuals remain theme-correct;
- no operating-system/default DataGrid styling leaks into the Protection column.

## 5. Protection Data Regression

Confirm the visual fix did not alter the data path:

- In-Memory Test Target continues to show its expected Protection flags;
- PS5 Scan Results and Saved Addresses continue to show the containing region's neutral Protection when available;
- a row with no resolvable memory region can still show blank Protection;
- changing Saved Address Address or Type continues to refresh Protection as defined before rev5;
- no page protection is changed on the target;
- write/freeze permission handling remains unchanged.

## 6. Export Regression

Export Saved Addresses with Protection selected in JSON and at least one text format.

Expected:

- Protection values are unchanged from rev3/rev4;
- JSON remains indented/human-readable;
- the WPF template change has no effect on exported values;
- All/Selected scope behavior remains unchanged;
- cancellation and transactional destination publication remain unchanged.

## 7. Scan Results Regression

Confirm Scan Results remains visually and functionally unchanged:

- Scan Results Protection still displays as before;
- Scan Results row height/alignment is unchanged;
- saving a Scan Result still creates the same Saved Address data;
- First Scan, Next Scan, and New Scan behavior is unchanged.

## Static Preparation Review

Before packaging rev5:

- the supplied packaged `0.1.4.rev4` tree was used as the baseline;
- README, CHANGELOG, every Markdown file under `docs/`, and the complete source/project/resource inventory were reviewed before the code change;
- rev4's ineffective Saved Addresses Protection `DataGridTextColumn`/cell-only centering was replaced, not layered with another conflicting alignment workaround;
- the rev5 Protection column is a read-only `DataGridTemplateColumn` whose cell content is stretched vertically and whose template-owned `TextBlock` is explicitly centered;
- the change remains local to the Saved Addresses Protection column;
- Core, scanner/storage, export implementation, Plugin SDK, PS5 plugin, Mock plugin, and automated test logic are unchanged;
- AppInfo/README/CHANGELOG/current documentation references are updated to rev5;
- rev4's 49/49 automated PASS and failed visual acceptance are recorded in the rev4 verification document;
- .NET/WPF build/runtime verification remains user-run on Windows.

## Acceptance

`0.1.4.rev5` is accepted when:

- the solution builds cleanly;
- all **49/49** automated checks pass;
- Saved Addresses Protection text is visibly vertically centered in Light, Dimmed, and Dark;
- selection, resizing, and multiple rows do not alter the centering;
- Protection data/export behavior remains unchanged;
- Scan Results and all previously verified functionality remain unaffected.

## Runtime Verification Result

`0.1.4.rev5` was subsequently verified on Windows by the user:

- the complete automated verification executable reported **All 49 checks passed**;
- the Saved Addresses Protection value was visually confirmed to be vertically centered correctly at runtime after the rev5 template-based rendering fix.

The `0.1.4` universal-export feature block is therefore considered complete and verified through rev5. Development proceeds to `0.1.5` for the Memory Viewer feature block.
