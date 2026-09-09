# Memory Scanner Architecture

## Purpose

This document describes the scanner architecture retained by TeeKay87's Memory Engine `0.1.5.rev8`, including Core-owned Scan Type semantics, plugin-owned Value Types/options, disk-backed/backend-resident result storage, optional native streaming acceleration, memory-map Protection presentation, and the complete-result boundary consumed by universal export.

## Architectural Rule

The scanner now has a deliberate split between universal comparison behavior and platform-specific representation/transport:

```text
Core
    owns MemoryScanTypeCatalog and standard Scan Type predicates
    coordinates regions, sessions, storage, progress, and refinement
    stores neutral temporary result records

Plugin
    supplies concrete Value Types
    supplies optional Scan Options
    owns platform/native scan acceleration and mappings

Plugin SDK
    supplies the neutral contracts, optional Value Type comparison/input companions,
    and stable Scan Type ids used across the native boundary

WPF
    renders Core Scan Types plus active-plugin Value Types/options
    applies optional plugin-owned Value Type live-input policy
    materializes only a bounded result preview
```

Value Types remain plugin-owned because parsing/representation can differ by platform. Exact Value uses `IMemoryValueType.ValuesEqual` for typed equality. Changed Value and Unchanged Value compare the raw fixed-width bytes stored by the previous scan, while ordered/delta predicates use the optional `IMemoryValueComparer`. This keeps snapshot change detection representation-stable for IEEE-754 NaN payloads without changing Exact Value or ordered floating-point semantics.

## Current PS5 Scan Definitions

PS5 plugin `0.1.0.rev25` currently supplies:

```text
1 Byte (Unsigned)
1 Byte
2 Bytes (Unsigned)
2 Bytes
4 Bytes (Unsigned)
4 Bytes
8 Bytes (Unsigned)
8 Bytes
Float
Double
Array of Bytes
```

Scan Types are supplied by Core for every plugin. Plugin API `2.7.0` lets a connected plugin publish semantic native mappings for the Core predicates it can accelerate without changing their meaning. PS5 plugin `0.1.0.rev25` retains the verified mapping behavior and maps compatible Core predicates to ps5debug-NG compare operations or snapshot mode and lets unsupported/mismatched cases use the shared reader-based fallback. Signed integer helpers use the unsuffixed display names above; unsigned alternatives remain explicit. PS5 continues to declare the stable signed Int32 id (`standard.int32`) as its default Value Type, so the UI initializes to `4 Bytes`. The plugin also declares Endianness, Alignment, Floating-point rounding, and Pause target while scanning behavior through plugin/capability-owned surfaces. Rev25 changes only the separate debugger integration/API target; scanner semantics remain the verified rev22-rev24 behavior. Plugin API `2.8.0` adds optional generic Scan Option presentation/applicability metadata: PS5 uses a toggle presentation for Endianness and limits Floating-point rounding visibility to Exact Value with Float/Double.

## Shared First Scan

The shared reader path receives neutral process/region data, `IMemoryReader`, target architecture, the selected plugin-defined Value Type, Core-owned Scan Type, parsed input values, and scan options.

Core:

1. validates the Core Scan Type / plugin Value Type combination;
2. resolves value width and alignment through the Value Type and options;
3. filters readable, non-Guard regions;
4. reads bounded 256 KiB chunks;
5. evaluates candidates through the selected Scan Type;
6. writes every match to the active `IScanResultWriter` when disk-backed storage is available;
7. materializes only the configured WPF preview count.

The disk-backed path does not use `MemoryScanner.MaximumResultCount` as its data limit. The historical two-million constant remains only on legacy list/in-memory paths retained for compatibility.

## Shared Next Scan

Disk-backed Next Scan receives an `IScanResultSet` representing the complete previous committed generation.

Core validates the stored value width/alignment, then sequentially reads every stored candidate, reads its current target bytes, evaluates the selected Next Scan predicate, writes surviving candidates into a replacement result generation, and materializes only the bounded preview.

A result outside the first 50,000 displayed rows is therefore still eligible to survive Next Scan.

The previous committed generation remains authoritative until the replacement writer successfully commits. Cancellation or failure during refinement cannot promote a partial replacement file.

## Disk-Backed Result Records

Rev13 stores temporary scan candidates using a compact binary format under the active scan-session directory.

Each file contains:

- format magic/version;
- value width;
- alignment;
- committed record count;
- repeated fixed-size records containing a 64-bit address and current-value bytes.

The active scan metadata records the referenced result filename, result generation, count, width, and alignment. A new generation is flushed and validated before metadata is changed to reference it.

The storage files remain disposable implementation state. They are scoped to the current application-session and scan-session GUIDs and are never treated as persistent user data.

## UI Result Boundary

The WPF Scan Results table materializes at most:

```text
50,000 rows
```

The total result count is tracked separately as a 64-bit value. When the complete set is larger than the preview, the UI reports the true count and states that only the first 50,000 results are shown.

This presentation limit is not a scan-session data limit.

From rev30, the DataGrid also uses row virtualization as the boundary for live current-value refresh. Only `ScanResultViewModel` rows that are currently realized in the viewport are registered with the shared live-value scheduler. Their displayed `Value` is reread through neutral `IMemoryReader`; off-screen preview rows are not periodically read, and the live refresh never changes result membership, total count, or the stored/native Previous-scan baseline.

### Universal export consumption

Application `0.1.4.rev1` reuses the scanner's existing complete-result abstractions for export. **All Results** streams the authoritative complete set and never treats the 50,000-row WPF preview as the data source merely because the set is large. Displayed/Selected scopes intentionally work from materialized presentation rows. From `0.1.4.rev3`, `MemoryScanResult` also carries the neutral `MemoryProtection` of its containing region for presentation; materialized rows refresh that value when the Active Target memory map is reloaded. Complete disk-backed/backend-resident exports derive Protection from the supplied memory map by address/range instead of widening the compact result records. No Scan Type predicate, result-membership rule, native mapping, or scan-storage format is changed by the export integration.


## Legacy In-Memory Safety Boundary

The existing constant remains:

```text
MemoryScanner.MaximumResultCount = 2,000,000
```

It protects legacy APIs that must materialize an entire result list in memory. It no longer rejects the disk-backed shared/streaming path.

This distinction is important for the live PS5 scalability case discovered earlier:

```text
Value Type: 1 Byte (Signed)
Value: 50
Native PS5 result count: 16,211,407
```

Rev13 is designed to receive/store such a result set in bounded batches rather than build 16 million `MemoryScanResult` objects.

## Native Scan Contracts

Plugin API `2.2.0` retains the earlier list-based native contracts:

```text
INativeValueScanner
INativeValueScanRefiner
```

and adds optional streaming contracts:

```text
INativeValueScanStreamProvider
INativeValueScanResultStream
INativeValueScanStreamRefiner
INativeValueScanCandidateSource
NativeValueScanResultBatch
```

Plugin API `2.9.0` adds the optional complete-result companion:

```text
INativeValueScanResidentResultSet
```

A normal streaming result advertises its source-result count and yields bounded batches containing absolute addresses plus packed current-value bytes. `NativeValueScanResultBatch` can also carry packed Previous-value bytes when a resident backend exposes the previous scan baseline directly. Core validates batch width, mapped/readable address membership, alignment, strict ascending order, duplicate addresses, and source-window completeness before accepting those records.

`IScanResultSet` implements `INativeValueScanCandidateSource`, so a plugin that needs the previous candidate addresses can consume them in bounded batches without Core constructing a giant address list. `INativeValueScanResidentResultSet` extends that candidate-source boundary with stable Value Size/alignment, authoritative membership, bounded result-window reads, and a `CanRefine(...)` query. Core can therefore treat a complete backend-resident set as authoritative without first copying every row into the host.

## PS5 Native First Scan

The current PS5 plugin probes ps5debug-NG TurboScan runtime capabilities. When server-resident multi-segment scanning is available, the session exposes both legacy and streaming native services.

The streaming path:

1. authenticates TurboScan;
2. maps the plugin-owned Value Type plus Core Scan Type id/options to supported ps5debug-NG behavior;
3. submits the readable region list as disjoint TurboScan segments;
4. keeps the successful native result set resident server-side;
5. exposes that session through `INativeValueScanResidentResultSet` when the native membership is already semantically authoritative;
6. when the complete count exceeds the 50,000-row presentation ceiling, fetches only the bounded TurboScan GET preview and keeps the rest target-resident;
7. when the result set is small or cannot remain authoritative natively, consumes bounded result batches into the existing Core disk-backed writer;
8. retains only the WPF preview as `MemoryScanResult` objects in either representation.

The legacy `INativeValueScanner` surface remains available for compatibility but still refuses to materialize more than 2,000,000 native records into one list. The host scan workflow prefers the streaming service when present.

For snapshot-based Unknown Initial Value, ps5debug-NG returns a plan followed by a sentinel-terminated sequence of 64-bit progress records, then a snapshot summary and final status. The host does not impose a progress-record-count limit: large snapshots may legitimately emit more than 1,024 records when the target falls back from its normal large snapshot I/O buffers to smaller windows. The client drains the complete response through the sentinel, summary, and final status before it validates snapshot acceptance. A target-side `snapshot_ok == 0` therefore becomes a synchronized `NotSupportedException` and Core fallback rather than leaving unread TurboScan bytes on the shared command connection.

## PS5 Native Next Scan

Compatible resident TurboScan refinement uses `INativeValueScanStreamRefiner` together with the API 2.9 resident-result handle.

Before native refinement, Core and the PS5 plugin require the resident result count, process, Value Type, width, alignment, active TurboScan generation, and semantic mapping to remain compatible. TurboScan COUNT narrows the target-resident set without uploading the previous address list. For Float/Double Changed Value and Unchanged Value only, the PS5 client transmits the same-width unsigned integer wire Value Type (`UInt32`/`UInt64`) on COUNT. ps5debug-NG then compares the four/eight stored bytes rather than applying IEEE-754 `==`/`!=`, so an unchanged NaN payload is not treated as changed. The resident session width is unchanged and GET results continue to be decoded as the user-selected Float/Double Value Type. The replacement resident handle becomes the authoritative generation, and TurboScan GET is used only for the bounded UI window that is actually required. For Next Scan preview rows, GET transports both current and target-stored previous values so the `Previous` column is preserved without a local previous-generation file.

When a requested predicate cannot be refined natively, Core does not discard the scan session. It first materializes the complete current resident set through bounded result windows into the transactional disk-backed writer, resets/releases the backend resident session, and then performs the existing shared disk-backed refinement. Large materialization uses the reusable modal operation-progress component and can be cancelled without publishing a partial generation.

If a native refinement returns 50,000 or fewer survivors, rev30 may materialize that small replacement immediately into the normal disk-backed representation; larger authoritative replacements remain resident. This keeps the host-side representation proportional to the amount of data actually needed while preserving the same complete-result semantics.

The resident START/COUNT operation is authoritative for survivor membership. The PS5 client therefore does not run a second integer, Array-of-Bytes, or tolerant floating-point Exact Value predicate against the value payload returned by GET. Strict Float/Double First Scan remains the deliberate exception because ps5debug-NG's native comparison is tolerant while the host's default Strict mode requires exact equality; such a stream is marked non-authoritative and follows the existing host filtering/materialization path.

## Floating-Point Semantics

ps5debug-NG native Float/Double Exact Value comparison uses relative `1e-6` tolerance.

The PS5 plugin exposes that behavior explicitly. In default **Strict** mode, incompatible native refinement is closed and the host uses shared Core refinement. Selecting **ps5debug-NG tolerance (1e-6)** intentionally keeps native semantics.

Strict host-side filtering can make the host committed count differ from the native resident count. The next native refinement then detects the mismatch and safely falls back to shared disk-backed refinement.

## Scan Session State

The host storage session is independent from any plugin-native resident session.

- each application launch has a new application-session GUID;
- each First Scan has a new scan-session GUID;
- compatible Next Scans retain that logical scan-session GUID;
- each successful refinement publishes a new result generation inside the same session;
- New Scan, Active Target replacement, plugin reload, or ViewModel disposal invalidates/releases the session;
- failed/cancelled First Scan discards its incomplete session;
- failed/cancelled Next Scan preserves the previous committed generation.

Value Type remains locked after successful First Scan because changing type/width would invalidate stored records. Scan Type availability is governed by Core stage/value compatibility and the selector is explicitly re-enabled when each scan leaves the running state, allowing the next refinement predicate to change without New Scan. Plugin-defined Scan Options keep their own lock policy: PS5 Endianness and Alignment lock with the session, while Floating-point rounding remains changeable because it affects only Exact Value comparison semantics.

## Progress and Cancellation

Shared scans report candidate progress through `MemoryScanProgress`, whose result count is now 64-bit.

Large native result transfers use the reusable host modal operation-progress component. The dialog reports received/stored counts and can request cancellation. Cancellation is linked to the actual scan token; the dialog does not simply disappear while storage/protocol work continues.

PS5 still observes its transaction-safe cancellation rules: once a framed command has started, the client drains the protocol transaction where necessary before surfacing cancellation so the shared TCP stream remains reusable.

## Read Failure Handling

Shared target reads retain the established maximum of 32 read failures before the scan aborts with guidance to refresh the map/check connection health.

## Key Core Types

```text
MemoryScanner
MemoryScanShape
MemoryScanExecutionResult
MemoryScanProgress
MemoryScanResult
IScanResultStorageSession
IScanResultWriter
IScanResultSet
ScanResultFileWriter
ScanResultFileSet
```

Public plugin-side scan contracts remain under `TeeKay87.MemoryEngine.PluginSdk`.

## Verification

Rev13 adds deterministic verification for:

- replacement-generation transactional behavior;
- Next Scan using a candidate beyond the first 50,000 materialized rows;
- a synthetic 2,000,001-record native stream committing and re-reading the complete set with only a 32-row preview;
- PS5 streaming service capability exposure/hiding.

The original `16,211,407` signed-byte result remains the regression reference. Rev14 subsequently live-verified the same massive-result path with `10,874,862` PS5 results: First Scan committed successfully and the UI remained bounded to 50,000 rows. Rev15's remaining live acceptance point is successful resident Next Scan over the complete stored set.

## Later Scanner Work

Rev13 deliberately does not add infinite scrolling or arbitrary browsing through millions of results. Future work may add paging/virtualized random access, additional Core scan predicates or plugin-defined value representations, and later memory-viewer/debugging workflows without changing the current complete-set/preview separation.


## Core Scan Type Catalog (0.1.3.rev26)

Core owns the standard comparison catalog so all platform plugins receive the same scan workflow automatically.

| Scan Type | First | Next | Operands | Notes |
| --- | :---: | :---: | ---: | --- |
| Exact Value | Yes | Yes | 1 | Value Type equality / selected floating Exact option |
| Fuzzy Value | Yes | Yes | 1 | Float/Double only; absolute difference `< 1.0` |
| Bigger Than | Yes | Yes | 1 | Ordered fixed-width values |
| Smaller Than | Yes | Yes | 1 | Ordered fixed-width values |
| Between | Yes | Yes | 2 | Inclusive lower/upper bounds |
| Unknown Initial Value | Yes | No | 0 | Keeps every fixed-width candidate, including zero |
| Unknown Initial Low Value | Yes | No | 1 | Keeps positive nonzero numeric values up to a positive upper limit |
| Increased Value | No | Yes | 0 | Compares against previous scan value |
| Decreased Value | No | Yes | 0 | Compares against previous scan value |
| Changed Value | No | Yes | 0 | Raw stored bytes differ from previous scan |
| Unchanged Value | No | Yes | 0 | Raw stored bytes match previous scan |
| Increased By | No | Yes | 1 | Exact directional positive delta |
| Decreased By | No | Yes | 1 | Exact directional positive delta |

The disk-backed record format already stores each candidate's current bytes. During Next Scan those stored bytes become the candidate's previous snapshot, so snapshot comparisons do not require a new in-memory dictionary or a new result-file record layout. Unknown Initial Value therefore writes `address + current value bytes` for every eligible fixed-width candidate and later refinement compares current target bytes against those stored bytes.

`Between` uses two user operands and the host presents those two editors side-by-side in the Scan panel. Snapshot predicates that do not need an operand hide the Value editor. Ordered/delta predicates require a fixed-width Value Type that implements `IMemoryValueComparer`; standard numeric types satisfy that requirement. Variable-width Array of Bytes remains an Exact Value representation in the current scanner.

## Native Scan Type Mapping

Native acceleration is selected through the optional Plugin API `2.7.0` `INativeScanTypeMappingProvider`. Core remains authoritative for Scan Type meaning; the plugin table states only which Core ids have semantically equivalent native implementations for the current platform, stage, and Value Type. `NativeScanTypeResolver` uses that table before the host requests a native scanner service. A missing mapping routes directly to the shared scanner, while a mapped request can still return `NotSupportedException` for runtime capability/option/resource constraints and fall back safely.

The native id carried by `NativeScanTypeMapping` is opaque to Core. This allows PS5, PC, Xbox 360, and future plugins to use unrelated backend enums/opcodes while sharing the same Core Scan Type vocabulary.

For PS5, Unknown Initial Value is accelerated through TurboScan snapshot mode with explicit zero inclusion rather than direct ps5debug-NG `compareType 11`, preserving Core's include-zero semantics. Increased By/Decreased By remain Core fallback because upstream target-width wrapping is not fully equivalent to Core's directional non-wrapping semantics. Floating Unknown Initial Low also remains Core fallback because ps5debug-NG compares absolute magnitude for that mode.

See [`NATIVE_SCAN_TYPE_MAPPING.md`](NATIVE_SCAN_TYPE_MAPPING.md) for the generic contract and exact current PS5 mapping table.
