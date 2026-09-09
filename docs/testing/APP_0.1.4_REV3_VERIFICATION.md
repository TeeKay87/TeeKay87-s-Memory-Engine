# TeeKay87's Memory Engine 0.1.4.rev3 Verification

## Revision Under Test

```text
Host application:             0.1.4.rev3
Feature:                      Memory Protection Columns and Pretty JSON Export
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Plugin API:                   2.9.0
Automated checks:             49
```

## Purpose

Rev3 extends the verified Universal Export foundation without changing the Plugin API, PS5 protocol, scan-result disk format, scanner predicates, or Saved Address write/freeze rules. It exposes the neutral memory-map `MemoryProtection` associated with addresses and makes structured JSON readable without a second whole-file formatting pass.

The intended changes are:

- a read-only **Protection** column in Scan Results;
- a read-only **Protection** column in Saved Addresses;
- Protection as a selectable export column for both consumers;
- complete disk-backed/backend-resident Scan Results exports resolve Protection from the loaded memory map while streaming;
- JSON remains schema version 1 and streaming/transactional, but is written with indentation directly by `Utf8JsonWriter`.

## 1. Clean Windows Build

Rebuild the complete solution from a clean rev3 extraction.

Expected:

- zero errors;
- zero warnings;
- nullable analysis and `TreatWarningsAsErrors` remain enabled;
- no new XAML designer/runtime resource error;
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

Rev3 keeps the same top-level check count but strengthens existing checks. Confirm that the output still includes PASS for:

- Universal export text formats;
- Universal export structured JSON;
- Universal export cancellation is transactional;
- Disk-backed Scan Results export exceeds display preview;
- Resident Scan Results export exceeds display preview;
- Shared 4-byte exact scanner and refinement;
- all prior PS5/plugin/storage regression checks.

The strengthened assertions cover Protection preservation/resolution and indented JSON while keeping the existing row-count and cancellation guarantees.

## 3. Scan Results Protection

Using the Mock target first, then a live PS5 when available:

1. Run a scan that returns results.
2. Confirm the Scan Results table contains a read-only **Protection** column.
3. Confirm rows display the containing memory region's neutral flags, for example `Read`, `Read, Write`, or `Read, Execute`.
4. Confirm **Region / Module** behavior is unchanged; unnamed regions remain blank rather than receiving a fabricated name.
5. Refresh/reload the Active Target memory map and confirm materialized Scan Results update Protection from the current map without changing result membership, Previous, or current scan baseline.
6. If a region with no `Write` flag is available, confirm its Scan Result visibly lacks `Write`.

Protection is informational only. Rev3 must not change page permissions or make a protected address writable.

## 4. Saved Addresses Protection

1. Save a Scan Result and confirm the Saved Addresses table contains **Protection**.
2. Confirm the saved row initially matches the containing region's protection.
3. Edit the Saved Address to another valid address in a region with different protection and confirm the column updates after the commit.
4. Change Value Type where its size changes the covered range and confirm Protection is recalculated for the full new range.
5. Switch to a different Active Target and confirm a row bound to the previous target does not present unrelated protection data.
6. Switch back/reload the map and confirm Protection is restored from the matching target map.
7. Confirm existing readable/writable validation is unchanged: a row without `Write` must still be rejected/deferred by the existing direct-write/freeze path rather than having its protection modified.

## 5. Export Protection

### Scan Results

Verify **Protection** can be selected in:

- All Results;
- Displayed Results;
- Selected Results.

For a materialized scope, compare exported Protection with the visible row.

For a disk-backed or backend-resident **All Results** scope above 50,000 rows, select Protection and confirm:

- export continues past the presentation preview boundary;
- Protection is populated from the loaded memory map where the full exported value range is contained in a region;
- no Previous or Region / Module field is falsely manufactured for the complete stored source;
- the scan-result storage files/contracts have not changed solely to carry Protection.

### Saved Addresses

Verify Protection can be selected for both All Addresses and Selected Addresses and matches the snapshot shown when export begins. Export must not perform a new target memory read merely to create the file.

## 6. Pretty JSON

Export both Scan Results and Saved Addresses as JSON.

Expected:

- JSON spans multiple lines with normal indentation;
- `type`, `schemaVersion`, metadata, columns, and rows remain the existing schema version 1 structure;
- selected Protection values appear as strings such as `Read, Write`;
- unavailable nullable fields remain JSON `null`;
- the file parses as valid JSON;
- no separate beautifier/post-processing delay occurs after the streamed export finishes.

For a large export, confirm progress/cancellation remains responsive. Pretty printing may increase file size because of whitespace, but it must not require the full result set or completed JSON document to be loaded into memory.

## 7. Transactional Cancellation Regression

Start a sufficiently large export and cancel it. Confirm:

- the modal reaches a safe cancellation boundary;
- no partial destination is published;
- an existing completed destination remains intact if replacement was cancelled;
- temporary output is cleaned up best-effort;
- normal Scan Results/Saved Addresses refresh resumes after cancellation;
- a resident PS5 export does not destabilize the connection.

## 8. UI and Theme Regression

Check Light, Dimmed, and Dark themes. Confirm:

- both Protection columns remain readable;
- DataGrid layout remains usable with the additional column;
- export dialog column list includes Protection where applicable;
- pretty JSON does not alter the export dialog/progress behavior;
- Scan Results and Saved Addresses resizing/selection/editing remain unchanged.

## Static Preparation Review

Before packaging rev3:

- the rev2 source is the baseline;
- root documentation and the project documentation set were reviewed before implementation;
- `MemoryProtection` was confirmed to already exist in the neutral Plugin SDK and PS5 already maps ps5debug-NG R/W/X flags into it;
- no Plugin API or platform-plugin code change is required;
- complete stored exports derive Protection from the supplied memory map rather than widening the verified scan-result record format;
- JSON indentation is produced by the existing streaming writer, not by reading/reformatting the completed file;
- automated .NET/WPF build/runtime claims remain user-run on Windows.

## Acceptance

`0.1.4.rev3` is accepted when:

- the solution builds cleanly;
- all **49/49** automated checks pass;
- Scan Results and Saved Addresses show correct Protection data;
- Protection exports correctly across the supported scopes;
- JSON is human-readable and still valid/streamed/transactional;
- no verified rev2 export/scanner/Saved Address behavior regresses.
