# Universal Export Architecture

## Status

Application `0.1.4.rev1` introduced the first production universal list/table export foundation. Application `0.1.4.rev2` corrected the rev1 nullable compile blocker. Application `0.1.4.rev3` keeps the same contracts and schema version while adding memory-map **Protection** as an exportable field and changing structured JSON output from compact to directly streamed, human-readable indentation.

The implementation is shared infrastructure rather than a Scan Results-specific or PlayStation 5-specific exporter. **Scan Results**, **Saved Addresses**, and from application `0.1.6.rev12` the **Disassembler** are production consumers. Future list-based tools should use the same contracts when their data fits the tabular model instead of creating independent serializers and progress/cancellation behavior.

Application `0.1.6.rev12` kept the verified Core writer/contracts/schema unchanged and added a new `DisassemblyExportSource` consumer plus a shared host destination picker. That export integration is part of the fully verified `0.1.6.rev14` Disassembler block. Host `0.1.7.rev1` advanced the public Plugin API to `2.12.0` for separate debugger contracts. Host `0.1.7.rev2` keeps the export subsystem unchanged while Mock advances to `1.0.0.rev8` / API `2.12.0` for its debugger backend and PS5 remains `0.1.0.rev24` / API `2.11.0`. Scan Results, Saved Addresses, and Disassembler export behavior are unchanged by the debugger work so far. Host `0.1.7.rev3` adds the real PS5 debugger transport without changing the existing universal export subsystem. Host `0.1.7.rev4` corrects only the shared main-window target/header and button-text presentation; host `0.1.7.rev5` changes only responsive row-1 target input sizing. Universal export remains unchanged and debugger-specific export remains planned for the final debugger integration revision.

## Ownership Boundary

The export stack is split deliberately:

```text
WPF / application
    chooses scope, format, columns, destination
    snapshots presentation-only rows when appropriate
    coordinates target state for backend-resident transfers

Core
    defines neutral export columns/cells/batches/data sources
    streams JSON / CSV / TSV / Markdown table
    reports progress
    validates emitted row count
    publishes completed files transactionally

Scanner/storage/native contracts
    expose authoritative data in bounded batches
    do not know about output file formats

Platform plugin
    remains responsible only for its existing native result-set implementation
```

There is no PS5 id, ps5debug-NG command id, platform-name branch, or export-format rule in Core's generic writer.

## Core Contracts

The baseline is intentionally small:

- `IExportDataSource` — identifies export type/schema/count/columns/metadata and yields bounded `ExportRowBatch` instances;
- `ExportColumn` — stable column id plus user-facing header;
- `ExportCellValue` — neutral typed value used by structured JSON and invariant text formatting;
- `ExportRowBatch` — flattened bounded row/column cell storage;
- `TabularExportService` — format writer, progress/cancellation, row-count validation, and safe file publication;
- `TabularExportFormat` — JSON, CSV, TSV, or Markdown table.

Data sources expose a stable advertised `Count`. The writer rejects an export that emits fewer/more rows than that count instead of silently publishing a structurally complete but incomplete file.

## Formats

### JSON

JSON is the structured format and uses schema version `1` for the current data-source envelopes.

Conceptually:

```json
{
  "type": "scan-results",
  "schemaVersion": 1,
  "exportedUtc": "2026-09-05T20:00:00+00:00",
  "rowCount": 2,
  "metadata": {
    "application": "TeeKay87's Memory Engine",
    "applicationVersion": "0.1.6.rev12",
    "pluginId": "platform.ps5.ps5debug-ng",
    "scope": "selected"
  },
  "columns": [
    { "id": "address", "header": "Address" },
    { "id": "value", "header": "Value" },
    { "id": "protection", "header": "Protection" }
  ],
  "rows": [
    { "address": "0x20752D3A0", "value": "100", "protection": "Read, Write" },
    { "address": "0x20752D3A4", "value": "95", "protection": "Read" }
  ]
}
```

Only selected columns are included in `columns` and each row object. Null remains JSON `null` instead of becoming an empty string.

JSON is streamed directly through `Utf8JsonWriter`; the writer does not build the full document in memory first. From `0.1.4.rev3`, `JsonWriterOptions.Indented` is enabled so the file is readable in an ordinary text editor without a separate beautifier pass. The additional whitespace increases file size, but memory usage remains bounded because indentation is produced while each batch is written rather than by loading and reformatting the completed document.

Schema version 1 is an export schema, **not** yet an import/project persistence contract. No re-import guarantee is made by rev1.

### CSV and TSV

CSV uses comma separation; TSV uses tab separation. Fields containing the active delimiter, quotes, CR, or LF are quoted and embedded quotes are doubled. Cell values use invariant formatting.

### Markdown table

Markdown export writes a normal header/separator table. Pipe characters are escaped, backslashes are preserved safely, and embedded line breaks are represented as `<br>` so a row does not corrupt the table structure.

Markdown is intended for readable sharing/documentation, not as the most efficient format for extremely large result sets.

## Export Dialog

The application-owned export dialog is reusable across list consumers. It exposes:

- **Scope**;
- **Format**;
- **Columns**;
- Select All for the current scope's available columns.

Changing scope rebuilds the available column list because a complete stored result set may guarantee fewer fields than a materialized presentation row.

The destination chooser runs only after the user confirms scope/format/columns and uses the matching default file extension.

## Scan Results Scopes

### All Results

**All Results** means the authoritative complete scan set.

The source is selected from current scanner state:

1. fully materialized complete results;
2. disk-backed `IScanResultSet`;
3. backend-resident `INativeValueScanResidentResultSet`.

The latter two are read in bounded batches. The 50,000-row WPF presentation preview is never substituted for All Results.

Complete disk-backed and resident result exports currently guarantee:

- Address;
- Value;
- Type;
- Protection.

The verified disk/native result records still store only the address and value data required by their existing scan contracts. `0.1.4.rev3` does **not** widen the scan-result record format. When Protection is selected for a complete disk-backed or backend-resident export, Core resolves each exported address against the memory-map snapshot supplied by the host and requires the complete value range to fit inside that region. If no containing region is available, the exported Protection value is null/empty for that row. Previous and Region / Module remain presentation metadata that are not manufactured for complete stored result sets.

### Displayed Results

**Displayed Results** exports the materialized rows currently loaded for presentation. Large complete sets can therefore have a larger total count than this scope; the UI description explicitly states the 50,000-row preview boundary.

Displayed materialized rows can export:

- Address;
- Value;
- Previous;
- Type;
- Protection;
- Region / Module.

### Selected Results

When one or more displayed rows are selected, **Selected Results** exports only that stable materialized selection with the same six presentation columns.

Scan Results export cannot begin while First/Next Scan or another foreground result-state operation is changing the same result state.

## Saved Addresses Scopes

Saved Addresses supports:

- **All Addresses**;
- **Selected Addresses** when rows are selected.

The export source snapshots rows in the host when the export UI is created. Export writing therefore does not trigger memory reads and is not affected by a later live-value timer tick halfway through the file.

The toolbar action is temporarily gated while a direct Saved Address user operation is committing an Address, Type, or Value change. This closes the WPF LostFocus edge where clicking Export immediately after editing could otherwise build the snapshot before the asynchronous target-backed commit had finished. Background refresh/Frozen work does not require that gate because snapshot construction itself is synchronous on the UI thread.

Available columns are:

- Frozen;
- Description;
- Address;
- Type;
- Value;
- Protection.

Protection is presentation/export metadata derived from the cached Active Target memory map. A Saved Address stores a nullable resolved protection value for its complete current Value Type range; it is recomputed when the active memory map, address, or Value Type/range changes and is blank when the row is inactive or no containing region can be resolved. Export snapshots that resolved value together with the other Saved Address fields and does not issue a new target request.

This is a list export, not Saved Address project persistence. Process binding, pointer expressions, groups, hotkeys, notes, project serialization, and cross-launch restoration remain separate future concerns.

## Disassembler Scopes

Application `0.1.6.rev12` adds Disassembler export without changing the universal writer contract.

The available scopes are:

- **Displayed Instructions** — every instruction currently materialized in the bounded Disassembler snapshot;
- **Selected Instructions** — current multi-selection, preserving displayed order.

The source snapshots existing neutral `DisassembledInstruction` records and does not read target memory during file writing. Available columns are:

- Address;
- Bytes;
- Instruction;
- Mnemonic;
- Operands;
- Length;
- Flow Control;
- Branch Target;
- Valid;
- Region / Module;
- Protection;
- Module Relative.

JSON therefore preserves structured instruction fields rather than relying only on the combined UI text. Missing Branch Target and module-relative data stay null/blank; the exporter never parses operands to fabricate them. Region/module/protection metadata comes from the current Disassembler snapshot, and a module-relative address is emitted only when a real module name and module base are available.

The host uses the same `DataExportDialog`, `OperationProgressDialogService`, `TabularExportService`, and shared `ExportDestinationPicker` as the existing consumers. The transactional destination rule is unchanged.

## Streaming and Memory Behavior

Core requests a bounded row batch (currently 4,096 rows) and writes it before requesting the next batch.

The writer never converts a complete large result set into an extra WPF collection or a single giant serializer object. Text formats build only the current bounded output batch; JSON flushes between source batches.

This behavior is required for result sets containing millions or hundreds of millions of rows.

## Progress and Cancellation

Exports reuse `OperationProgressDialogService`.

Progress is based on the data source's authoritative row count and reports rows written. Cancel requests propagate to the active source/writer and the modal remains open until the operation reaches a safe cancellation boundary.

A backend-resident Scan Results export uses the plugin's existing resident-result read contract. While that transfer owns the plugin's primary command stream:

- ordinary Saved Address / visible Scan Result refresh is paused;
- target/process/scan user commands that would conflict with the resident session are disabled;
- Frozen writes may continue only through an already-declared `IConcurrentMemoryWriter`, preserving the independent-channel rule already used during scans.

No new PS5-specific concurrency contract is introduced.

## Transactional Destination Publication

The requested destination path is not written directly.

Core writes to a GUID-named temporary file in the destination directory. Only after:

1. all source rows have been consumed;
2. the emitted row count exactly matches the source's advertised count;
3. cancellation has not been requested;

is the temporary file moved/replaced into the requested destination.

On cancellation or failure, the temporary file is deleted best-effort and an existing completed destination is left untouched. This prevents a partial export from looking like a successful file.

## Metadata

The current application adds neutral context when available, including:

- application title/version;
- scope;
- plugin id/name/version;
- platform/backend labels;
- source row count;
- total Scan Results count for scan exports;
- Active Target process id/name for scan exports;
- selected Value Type;
- selected Scan Type;
- Disassembler requested origin, architecture, region base/size/name/module/protection, and resolved module base where available.

The Scan Type metadata is intentionally named **selectedScanType**, not “last scan type”: the selector can be changed after a completed scan before another Next Scan is run, so export must not make a stronger historical claim than current state supports.

## Current Non-Goals

The current application (`0.1.7.rev5`) retains the verified `0.1.4` export foundation and the verified `0.1.6` Disassembler export integration unchanged. It still does not implement:

- automatic export integration for every future list/table;
- a generic “currently filtered rows” scope where a view has a real filter model;
- a single application-wide Copy As / Export Selected command abstraction across every DataGrid;
- import/re-import of generic JSON exports;
- spreadsheet formats such as XLSX;
- plugin-specific cheat/project exporters;
- changing scan storage to retain Previous/Region metadata solely for export.

Those can be added when their owning subsystems exist and their semantics are known.


> Host `0.1.7.rev6` builds on the fully verified rev5 baseline. Its Threads/Thread Control and passive header-status cleanup do not redesign the subsystem documented here.
