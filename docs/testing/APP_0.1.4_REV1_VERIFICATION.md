# TeeKay87's Memory Engine 0.1.4.rev1 Verification

## Revision Under Test

- Application: `0.1.4.rev1 - Universal Export Foundation`
- Plugin API: `2.9.0` (unchanged)
- PS5 plugin: `0.1.0.rev22` (unchanged)
- Mock plugin: `1.0.0.rev4` (unchanged)

`0.1.3.rev32` was user-verified before this version block started. The purpose of this checklist is therefore to verify the new shared export subsystem while explicitly guarding the already-verified scanner/Saved Addresses behavior against regression.

## 1. Clean Windows Build

Build the full solution in Release configuration.

Expected:

- no compile errors;
- no warnings (warnings are treated as errors);
- WPF XAML compiles, including `DataExportDialog`;
- plugins are copied to the normal runtime `Plugins` directory;
- no Plugin API compatibility warning is introduced by rev1.

## 2. Automated Verification Runner

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 49 checks passed.
```

The five new export checks must pass:

1. **Universal export text formats**
   - CSV header/escaping;
   - TSV embedded-tab quoting;
   - Markdown pipe escaping.
2. **Universal export structured JSON**
   - type/schema/row count metadata;
   - selected columns only;
   - source metadata preservation;
   - missing Previous remains JSON `null`.
3. **Universal export cancellation is transactional**
   - cancellation propagates;
   - an existing completed destination is not replaced;
   - temporary partial output is removed.
4. **Disk-backed Scan Results export exceeds display preview**
   - 50,005 stored results produce 50,005 data rows, proving All Results is not the 50,000-row preview.
5. **Resident Scan Results export exceeds display preview**
   - 50,005 backend-resident results produce 50,005 data rows;
   - the resident source is asked for the authoritative complete count.

All 44 checks inherited from verified rev32 must remain PASS.

## 3. Export Dialog and Theme Smoke Test

Repeat in Light, Dimmed, and Dark at least once across the following checks.

For both Scan Results and Saved Addresses:

- **Export...** is enabled only when that workspace has exportable rows;
- for Saved Addresses, begin editing Value/Address/Type and commit it by moving focus to **Export...**; verify Export does not open until the direct commit has completed, then becomes available again with the committed value visible;
- dialog opens owner-centered and uses current theme resources;
- Scope descriptions wrap without being clipped;
- Format lists JSON, CSV, TSV, Markdown table;
- changing Scope updates the available Columns list;
- every column starts selected;
- manually clear/select columns and verify Continue refuses zero columns;
- **Select All** restores every available column;
- Cancel closes without creating a file;
- Continue opens an owner-bound Save dialog with the correct extension/filter.

## 4. Saved Addresses Export

Create several Saved Addresses with deliberately different:

- Frozen state;
- Description, including comma/pipe characters;
- Address;
- Type;
- Value.

### All Addresses

Export all addresses in each format.

Verify:

- row count equals the Saved Addresses table count;
- order matches the current table order;
- all five columns are available;
- JSON contains `type: "saved-addresses"`, schema version 1, metadata, columns, and rows;
- CSV/TSV quoting is correct for delimiter-containing descriptions;
- Markdown does not split a cell containing `|` into another column.

### Selected Addresses

Select more than one row and choose Selected Addresses.

Verify only those rows are exported.

### Column Selection

Repeat with only Address and Value selected.

Verify no other row columns are present.

Saved Address export should not require the target to perform a new memory read. It is an export of the current host snapshot.

## 5. Materialized / Displayed Scan Results Export

Run a small scan that produces a manageable number of results.

Verify All Results and Displayed Results both expose:

- Address;
- Value;
- Previous;
- Type;
- Region / Module.

After a Next Scan, verify Previous is populated where the result model contains it.

Select several rows and verify **Selected Results** contains exactly that selection.

Change the current Scan Type selector after a completed scan without running another Next Scan, export JSON, and verify metadata describes it as `selectedScanType` rather than claiming it was necessarily the last executed predicate.

## 6. Disk-Backed Complete Scan Export

Use a scan whose authoritative result count exceeds 50,000 and is disk-backed.

Open Export and compare scopes:

- **All Results** count must equal the true total result count;
- **Displayed Results** count must equal the currently materialized presentation count and must not exceed 50,000;
- All Results must expose Address, Value, Type;
- Displayed Results may additionally expose Previous and Region / Module.

Export **All Results** to CSV or TSV and verify the completed file contains the true full row count rather than only 50,000 data rows.

The existing disk-backed scan generation/session must remain usable for a subsequent Next Scan after export.

## 7. Live PS5 Backend-Resident Export

This is the critical live acceptance path for the rev30 architecture plus rev1 export.

Create a PS5 native TurboScan result set larger than 50,000 that remains backend-resident.

Verify the Export scope labels distinguish:

- the full **All Results** count;
- the bounded **Displayed Results** count.

Export **All Results** to CSV/TSV first. Verify:

- progress uses the true resident count;
- export begins without first materializing the whole set into the WPF DataGrid;
- output continues beyond row 50,000;
- Address, Value, Type are present;
- no Previous/Region column is offered for this complete resident scope in rev1.

Cancel a second large resident export after progress has started. Verify:

- the dialog remains until cancellation reaches a safe boundary;
- no partial destination is published;
- the PS5 connection remains synchronized/usable;
- the resident scan session is still valid if cancellation itself does not require ending it;
- a later compatible Next Scan or New Scan still works normally.

## 8. Resident Export I/O Coordination

With at least one Saved Address visible and one Frozen address active:

1. start a large backend-resident Scan Results export;
2. observe the Saved Address Value display during the transfer;
3. after export, confirm live refresh resumes;
4. confirm target/process/scan actions cannot race the resident export through the primary command stream.

When the PS5 concurrent writer is available and **Pause target while scanning** is not relevant to the export:

- Frozen enforcement should remain eligible through the independent concurrent writer;
- ordinary refresh must not share the resident primary stream during the export.

After export completes/cancels, normal refresh/freeze cadence must resume without reconnecting.

## 9. Transactional Destination Safety

Choose an existing export file as destination and allow overwrite.

### Successful replacement

Run a successful export and verify the old file is replaced by the new complete file.

### Cancelled replacement

Create/restore a known existing file, begin a sufficiently large export to that path, then cancel.

Verify the original file is still intact.

### Failure path

If practical, provoke a destination permission/write failure.

Verify:

- failure is surfaced in the relevant status area;
- no partial requested destination is presented as successful;
- a temporary export file is not left behind under normal cleanup conditions.

## 10. Regression Checks From 0.1.3.rev32

At minimum:

- New Scan restores Exact Value;
- First Scan / Next Scan / New Scan still operate normally;
- 50,000-row WPF preview boundary remains presentation-only;
- large resident Next Scan stays native where semantic mapping allows it;
- Float Unknown Initial -> Changed/Unchanged no longer treats an identical NaN payload as changed;
- visible Scan Result live Value refresh resumes after scans;
- Saved Address direct Value editing still writes correctly;
- Frozen state/cadence still behaves correctly;
- Remove / Remove All coordination remains intact;
- Disconnect/reconnect and Active Target selection remain functional;
- Light/Dimmed/Dark theme switching remains immediate.

## Reported Windows Build Result

Windows build verification was attempted on **2026-09-05** and rev1 did **not** reach runtime verification.

Visual Studio reported the primary Core diagnostic:

```text
CS8600 TabularExportService.cs(136): Converting null literal or possible null value to non-nullable type.
```

Because warnings are intentionally treated as errors, `TeeKay87.MemoryEngine.Core.dll` was not produced. The App project then reported the expected dependent metadata error (`CS0006`) plus XAML designer/markup diagnostics for `System.Object`, `ProportionalGridSplitter`, and `TextBoxInputFilter` members in `MainWindow.xaml`.

Source review confirmed the Core failure is the root blocker: the referenced WPF control/attached-property types still exist with the namespaces used by `MainWindow.xaml`. Rev1 therefore remains **unverified** and no XAML behavior change is inferred from the cascading designer errors.

The nullable lookup is corrected in **0.1.4.rev2 - Tabular Export Nullability Compile Fix**. Rev2 must be built before the remaining 49-check and runtime/export sections of this checklist are continued.

## Acceptance

`0.1.4.rev1` is accepted when:

- the solution builds cleanly on Windows;
- all **49/49** automated checks pass;
- Scan Results All/Displayed/Selected semantics are correct;
- a complete result set larger than 50,000 exports beyond the UI preview in both disk-backed and live resident paths;
- cancellation does not publish partial data or destabilize the PS5 connection;
- Saved Addresses All/Selected exports are correct;
- no verified scanner, Saved Addresses, freeze, connection, or theme behavior regresses.
