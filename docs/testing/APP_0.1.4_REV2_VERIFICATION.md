# TeeKay87's Memory Engine 0.1.4.rev2 Verification

## Revision Under Test

```text
Host application:             0.1.4.rev2
Feature:                      Tabular Export Nullability Compile Fix
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Plugin API:                   2.9.0
Automated checks:             49
```

## Purpose

Rev2 is a narrowly scoped compile correction for the Universal Export implementation introduced in rev1. It does not redesign the export subsystem or reopen the user-verified `0.1.3.rev32` scanner behavior.

The first Windows build of rev1 stopped in `TeeKay87.MemoryEngine.Core` with `CS8600` at the selected export-column lookup in `TabularExportService.ResolveColumns(...)`. Because the repository intentionally treats warnings as errors, Core did not produce its assembly. Visual Studio then displayed dependent `CS0006` and WPF designer/markup errors in `MainWindow.xaml` because the App project could not resolve its compiled dependencies.

## Reported Rev1 Build Failure

Primary diagnostic:

```text
CS8600 TabularExportService.cs(136): Converting null literal or possible null value to non-nullable type.
```

Representative downstream diagnostics included:

```text
CS0006: Metadata file '...TeeKay87.MemoryEngine.Core.dll' could not be found
XLS0414: The type 'System.Object' was not found
XDG0008: The name 'ProportionalGridSplitter' does not exist in TeeKay87.MemoryEngine.App.Controls
XDG0008: The name 'TextBoxInputFilter' does not exist in TeeKay87.MemoryEngine.App.Input
XDG0008: Cannot set unknown member 'TextBoxInputFilter.Mode'
XDG0008: Cannot set unknown member 'TextBoxInputFilter.MemoryValueType'
```

`ProportionalGridSplitter` and `TextBoxInputFilter` remain valid public source types in the namespaces referenced by `MainWindow.xaml`. Rev2 therefore does not add an XAML workaround for those cascading errors.

## Correction

The rev1 lookup used an explicitly non-nullable `ExportColumn` as the `out` target of `Dictionary<string, ExportColumn>.TryGetValue(...)`.

Rev2 receives that value as nullable and validates it before assignment:

```csharp
if (!byId.TryGetValue(columnId, out ExportColumn? column) || column is null)
{
    throw new ArgumentException(
        $"Export column '{columnId}' is not available from this data source.",
        nameof(columnIds));
}

resolved[index] = column;
```

This matches the nullable contract of `TryGetValue(...)`, preserves the existing invalid-column error path, and does not use `!`, disable nullable analysis, or relax `TreatWarningsAsErrors`.

## 1. Clean Windows Build

Use a clean extraction of rev2 or remove stale build output, then rebuild the complete solution in Visual Studio / .NET 9.

Expected:

- zero `CS8600` diagnostics in `TabularExportService.cs`;
- zero warnings because warnings remain treated as errors;
- `TeeKay87.MemoryEngine.Core.dll` is produced normally;
- the dependent `CS0006` metadata error disappears;
- `MainWindow.xaml` resolves `ProportionalGridSplitter` and `TextBoxInputFilter` after the corrected assemblies build;
- `DataExportDialog.xaml` compiles;
- no Plugin API compatibility warning is introduced.

If Visual Studio continues to display stale XAML designer errors after a successful clean build, clear `bin`/`obj` and reload/restart the designer before treating those diagnostics as a separate source defect.

## 2. Automated Verification Runner

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 49 checks passed.
```

### Reported result

The user ran the rev2 verification executable on Windows and reported **all 49 checks PASS**. This confirms the compile correction allowed the complete automated suite to execute and that the Universal Export checks plus the inherited scanner/plugin/protocol regressions passed together. Manual/live export acceptance remains separate from that automated result.

Rev2 adds no new runtime behavior check and removes none. The five Universal Export checks from rev1 must now compile and execute together with all 44 checks inherited from verified rev32:

1. Universal export text formats;
2. Universal export structured JSON;
3. Universal export cancellation is transactional;
4. Disk-backed Scan Results export exceeds display preview;
5. Resident Scan Results export exceeds display preview.

## 3. Universal Export Runtime Verification

After the clean build and 49/49 automated result, continue the complete rev1 functional verification because rev2 changes only the compile path.

At minimum verify:

- Scan Results **Export...** opens and offers All / Displayed / Selected as applicable;
- Saved Addresses **Export...** opens and offers All / Selected;
- JSON, CSV, TSV, and Markdown table exports complete;
- column selection works and zero-column export is rejected;
- a disk-backed result set above 50,000 exports the complete authoritative set through **All Results**;
- a live PS5 backend-resident result set above 50,000 exports beyond the WPF preview without full UI materialization;
- cancellation does not publish a partial destination;
- an existing completed file remains intact when replacement export is cancelled;
- Saved Address export snapshots current rows and does not perform a new target read;
- resident export I/O coordination does not destabilize the PS5 connection and normal live refresh resumes afterward.

The detailed steps remain in `APP_0.1.4_REV1_VERIFICATION.md`; its reported build result now records why rev1 itself could not proceed to those sections.

## 4. Regression Boundary

Confirm the compile correction does not alter previously verified behavior:

- New Scan restores Exact Value;
- First / Next / New Scan remain functional;
- the 50,000-row WPF preview remains presentation-only;
- backend-resident refinement remains native where mappings allow it;
- Float/Double Changed/Unchanged retain byte-stable NaN semantics from rev32;
- Scan Result live Value refresh resumes normally;
- Saved Address edit/write/freeze/remove coordination remains unchanged;
- Disconnect/reconnect and Active Target selection remain functional;
- Light/Dimmed/Dark switching remains immediate.

## Static Preparation Review

Before packaging rev2:

- README, CHANGELOG, all Markdown files under `docs/`, and the complete source/project/resource tree were re-read from the packaged rev1 baseline;
- the Core export code and its callers were audited for another copy of the reported non-nullable `TryGetValue` pattern;
- the fix is limited to the nullable selected-column lookup plus host version/documentation metadata;
- `Directory.Build.props` still enables nullable analysis and `TreatWarningsAsErrors`;
- no Plugin SDK or platform-plugin source is changed;
- `ProportionalGridSplitter` and `TextBoxInputFilter` declarations/namespaces remain unchanged;
- no scanner/result-storage/freeze protocol or data-format change is part of rev2.

## Acceptance

`0.1.4.rev2` is accepted when:

- the complete solution builds with zero errors and zero warnings on Windows;
- the rev1 cascading missing-assembly/XAML designer errors disappear after the successful clean build;
- all **49/49** automated checks pass;
- the rev1 Universal Export manual/live acceptance paths pass;
- no verified rev32 scanner/Saved Addresses behavior regresses.

Only after these checks should the `0.1.4` Universal Export feature block be treated as verified and complete.
