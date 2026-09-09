# TeeKay87's Memory Engine 0.1.4.rev4 Verification

## Revision Under Test

```text
Host application:             0.1.4.rev4
Feature:                      Saved Address Protection Vertical Alignment Fix
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Plugin API:                   2.9.0
Automated checks:             49
```

## Purpose

Rev4 is a narrowly scoped WPF presentation correction on top of rev3. The read-only **Protection** value in the Saved Addresses table was positioned at the top of the taller row because that column used the default `DataGridCell` vertical content alignment while the neighboring editable columns use 34-unit TextBox/ComboBox controls.

Rev4 centers only the Saved Addresses Protection cell content vertically. It does not change memory-map resolution, `MemoryProtection`, Scan Results, export data, JSON formatting, Saved Address reads/writes/freezing, the Plugin API, or either bundled platform plugin.

## Observed Verification Result

Rev4 completed the existing automated Windows verification suite successfully:

```text
All 49 checks passed
```

The required manual Saved Addresses Protection alignment check **failed** at runtime. In the Dimmed theme, the Protection text still rendered too high within the taller row even though the column applied `DataGridCell.VerticalContentAlignment=Center`. The generated `DataGridTextColumn` presentation element therefore did not follow the intended centering path.

Rev4 is **not accepted as visually verified** and is superseded by the explicit template-based correction in `0.1.4.rev5`. Protection data, export behavior, and the 49 automated regression checks were not implicated by the visual failure.

## 1. Clean Windows Build

Rebuild the complete solution from a clean rev4 extraction.

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

Rev4 intentionally does not add a source-layout assertion to the platform-neutral verification executable. The executable does not reference the WPF application project, and the correction is visual presentation rather than Core/plugin behavior. The existing 49 checks must remain green to prove that the rev3 Protection/export/scanner contracts have not regressed.

## 3. Saved Addresses Protection Alignment

1. Connect to a target and create at least one Saved Address with a non-empty Protection value such as `Read, Write`.
2. Confirm the **Protection** text is vertically centered within the Saved Addresses row.
3. Compare it with the neighboring Description, Address, Type, Value, Frozen, and Remove controls. The text should no longer sit against the upper edge of the row.
4. Resize the Saved Addresses area vertically and horizontally and confirm the alignment remains centered.
5. Add several Saved Addresses and confirm every row uses the same alignment.

The fix is intentionally limited to Saved Addresses. Scan Results retains its existing layout.

## 4. Theme Regression

Repeat the Saved Addresses visual check in:

- Light;
- Dimmed;
- Dark.

Expected:

- Protection remains vertically centered;
- foreground/background/selection colors remain unchanged;
- selected rows remain legible;
- no operating-system DataGrid styling leaks into the column.

## 5. Protection Data Regression

Confirm the value itself still behaves as rev3 defined:

- saving a Scan Result carries/resolves the correct Protection;
- changing Address or Type refreshes the containing-range Protection as before;
- a row with no resolvable region can still show a blank Protection value;
- no memory protection is modified on the target;
- write/freeze permission checks remain unchanged.

## 6. Export Regression

Export Saved Addresses with Protection selected in at least JSON and one text format.

Expected:

- Protection values remain unchanged from rev3;
- JSON remains indented/human-readable;
- alignment has no effect on exported values;
- cancellation and transactional publication behavior are unchanged.

## Static Preparation Review

Before packaging rev4:

- the rev3 source is the exact baseline;
- all project Markdown documentation and the complete source/project inventory were reviewed before the code change;
- only the Saved Addresses Protection `DataGridTextColumn` receives a local cell style setting `VerticalContentAlignment` to `Center`;
- the style is based on the existing application `DataGridCell` style so theme/selection visuals are preserved;
- Scan Results, Core export implementation, scanner/storage code, Plugin SDK, PS5 plugin, Mock plugin, and verification-test logic are unchanged;
- AppInfo/README/CHANGELOG/current documentation references are updated to rev4;
- .NET/WPF build/runtime verification remains user-run on Windows.

## Acceptance

`0.1.4.rev4` would have been accepted when:

- the solution builds cleanly;
- all **49/49** automated checks pass;
- Saved Addresses Protection text is vertically centered in Light, Dimmed, and Dark;
- Protection data/export behavior remains unchanged;
- no verified rev3 behavior regresses.

## Final Rev4 Status

- Automated verification: **PASS (49/49)**.
- Saved Addresses Protection vertical alignment: **FAIL**.
- Overall acceptance: **NOT ACCEPTED** because the required visual criterion did not pass.
- Corrective revision: `0.1.4.rev5`.
