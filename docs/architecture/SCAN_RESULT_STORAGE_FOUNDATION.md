# Scan Result Storage Architecture

## Purpose

TeeKay87's Memory Engine `0.1.3.rev9` established the host-side identity, lifecycle, cleanup, settings, and commit foundation for temporary scan storage. `0.1.3.rev13` extends that foundation with the compact disk-backed candidate records used by First Scan and Next Scan. `0.1.3.rev30` adds a second complete-result representation for authoritative native backends: sufficiently large result sets may remain resident in the plugin/backend until host-side processing actually requires disk materialization.

The central rule remains:

> Scan-result files are temporary implementation state for the active scanning workflow. They are not reusable user data and they never become authoritative merely because a file exists.

## Ownership Boundary

Scan-result storage belongs to Core/host infrastructure.

Platform plugins own target-specific scanning and concrete Value Type/Scan Type/Scan Option behavior. Plugins do not select Windows folders, manage stale application directories, publish host metadata, or render progress dialogs.

Core may record stable plugin-defined ids as session context but does not interpret those ids as a concrete scanner catalog.

## Configured Root

Default:

```text
%LocalAppData%\TeeKay87\MemoryEngine\ScanResults
```

The application Settings window can persist another writable root in the shared application settings document. A changed root becomes active on the next application launch; an active application session is never migrated between roots.

The path validator performs a real create/write/flush/delete probe. Startup reports/logs an invalid configured root instead of silently switching to an unrelated folder.

## Managed Layout

The current structure is:

```text
<configured root>\
    .tk87me-scan-storage.json
    <application-session-guid>\
        session.json
        <scan-session-guid>\
            scan.json
            results-00000001.bin
            results-00000002.bin
            ...
```

Only the result generation referenced by committed `scan.json` metadata is authoritative. Older files may briefly exist during replacement/cleanup, but they are never selected by filename recency.

## Application Session Identity

Each application launch creates a fresh GUID before stale cleanup. The manager only treats its own newly-created identity as current writable state.

Startup may delete other positively identified managed application-session directories, but failure to delete one cannot make it current.

## Scan Session Identity

Every First Scan creates a fresh scan-session GUID. Compatible Next Scans remain in the same logical scan session and publish new result generations inside that directory.

New Scan, Active Target replacement, plugin reload, or ViewModel disposal invalidates/releases the current session. A later First Scan receives a new GUID.

## Scan Metadata

`scan.json` contains versioned session context including:

- ownership/format version;
- application-session id;
- scan-session id;
- lifecycle state;
- timestamps;
- plugin id;
- target process id;
- plugin-defined Value Type id;
- plugin-defined Scan Type id;
- record-format version;
- committed total result count;
- active result filename;
- stored value width;
- stored alignment;
- result generation number;
- optional failure information.

A disk-backed set can be opened only when metadata is `Committed` and references a binary file whose header/length/count/shape all validate.

## Lifecycle States

```text
Creating
Writing
Committed
Failed
Cancelled
Invalidated
```

For the first generation:

```text
Creating -> Writing -> Committed
```

A failed/cancelled First Scan terminates the incomplete session.

For Next Scan, the session is already `Committed`. Creating a replacement writer does **not** demote the current committed metadata. The previous generation therefore remains valid while a replacement is being produced.

```text
Committed generation N
        |
        +--> temporary generation N+1 writing
                |
                +--> failure/cancel -> generation N stays active
                |
                +--> validate/publish -> metadata references N+1
                                         old N deleted best-effort
```

This is the transaction boundary that prevents a partial Next Scan from replacing valid state.

## Binary Result Format

Current result files use binary record format version 1.

The file begins with a fixed header containing:

- magic/signature;
- binary format version;
- value width;
- alignment;
- record size;
- committed record count.

Each following record contains:

```text
uint64 address
byte[valueSize] currentValue
```

The result file deliberately stores compact neutral bytes rather than serialized `MemoryScanResult`/JSON objects. Region names and formatted display strings are presentation data and are reconstructed only for the bounded UI preview.

A one-byte scan therefore requires only the fixed header plus nine bytes per committed candidate.

## Result Writers

`IScanResultWriter` provides:

- 64-bit `Count`;
- `ValueSize` and `Alignment`;
- single-record writes;
- bounded batch writes;
- asynchronous commit.

The writer initially targets a uniquely named temporary file inside the same scan-session directory. Commit:

1. writes the final record count into the header;
2. flushes the stream and requests flush-to-disk;
3. closes the stream;
4. moves the complete temporary file to its generation filename;
5. reopens/validates header, record shape, count, and expected file length;
6. atomically publishes updated `scan.json` metadata referencing that generation;
7. only then releases the previous generation for best-effort deletion.

If any step before metadata publication fails, the new generation is not authoritative.

## Result Readers

`IScanResultSet` exposes:

- 64-bit total count;
- stored value width/alignment;
- bounded `ScanResultRecordBatch` enumeration;
- bounded address-only enumeration through `INativeValueScanCandidateSource`.

The reader does not keep an open file handle for the lifetime of the set object. Each enumeration opens the committed file read-only and streams batches sequentially. This keeps long-lived scan-session state lightweight and allows prior generations to be removed after a successful replacement once active enumeration has completed.

## First Scan Integration

When storage is available, shared Core First Scan writes every accepted candidate directly to an `IScanResultWriter` while retaining only the first 50,000 `MemoryScanResult` objects for WPF. A rev30 authoritative native resident scan instead reads only that same bounded preview until local materialization is required.

When a plugin exposes API 2.2 native streaming, Core consumes native result batches and writes accepted records directly to the same writer. The host does not first build one giant native result list.

Legacy list-based native scanning remains a compatibility fallback. If it is used, the list can be persisted into the same disk format, but legacy APIs retain their historical materialization safety limit.

## Next Scan Integration

Disk-backed Next Scan reads the complete prior result file. It does not use the WPF preview list as the candidate source.

For shared refinement, current target bytes are read for every stored candidate and survivors are written to a replacement generation.

For compatible native streaming refinement, `IScanResultSet` acts as the neutral previous-candidate source and Core cross-checks each streamed survivor address against the complete prior set before writing it.

## Total vs Displayed Results

Rev13 separates:

```text
Total result count   = complete committed set
Stored result count  = complete committed set
Displayed result count <= 50,000
```

The UI ceiling is a presentation policy only. A candidate that is never displayed can still survive a later Next Scan.

## Backend-Resident Complete Result Sets

Plugin API `2.9.0` allows a native backend to expose `INativeValueScanResidentResultSet`. This does not change the binary format or weaken the transactional storage rules. Instead, the active scan session can have one of two complete-result representations:

1. a committed host `IScanResultSet` generation; or
2. an authoritative native resident handle plus a bounded WPF preview.

For a large resident set, no complete local result file is created merely to populate Scan Results. The host reads at most the presentation window, tracks the full resident `Count`, and can issue a compatible native Next Scan directly against backend state.

If a later operation requires shared Core refinement, the host opens a normal result writer, transfers every current resident record in bounded batches, validates result shape/address ordering, commits the complete generation transactionally, releases the backend resident session, and only then runs shared refinement. Cancellation or transfer failure cannot publish a partial generation.

The scan-session directory may therefore exist before it contains a committed result generation. Session identity/lifecycle metadata still protects ownership and cleanup. Backend resident state is released through the plugin's native reset/disposal path on New Scan, target changes, disconnect, or scan teardown.

## Complete-Set Export Integration (0.1.4.rev1-rev3)

Universal Scan Results export consumes the existing complete-result abstractions; it does not introduce another result store or change binary record format version 1.

For **All Results**:

- a fully materialized result list is read from its in-memory snapshot;
- a disk-backed `IScanResultSet` is streamed through `ReadBatchesAsync(...)`;
- an authoritative `INativeValueScanResidentResultSet` is streamed through bounded `ReadResultBatchesAsync(...)` windows.

This distinction is hidden behind a neutral export source. Core's writer therefore does not know whether the complete result set came from PS5 TurboScan, another future backend, or local storage. The WPF 50,000-row preview is never substituted for the complete set when the user selects **All Results**.

Binary format version 1 still stores only address plus current-value bytes. From `0.1.4.rev3`, complete disk-backed and backend-resident exports guarantee **Address**, **Value**, **Type**, and **Protection**: Protection is resolved at export time by matching each address/value range against the memory-map snapshot supplied by the host. Materialized Displayed/Selected rows may additionally expose **Previous** and **Region / Module**. The existing verified scan-storage format is deliberately not widened merely to make Protection or those presentation fields resident in every result record.

Backend-resident export reads the plugin's primary resident-result stream. The host pauses ordinary Saved Address / visible Scan Result refresh while that transfer owns the primary target path. Frozen writes may continue only when the plugin exposes the already-existing independent `IConcurrentMemoryWriter`; this preserves the transport-serialization rules established before export.


## Two-Million Legacy Limit

`MemoryScanner.MaximumResultCount = 2,000,000` is intentionally retained for legacy in-memory/list APIs.

The disk-backed shared path and API 2.2 native streaming path do not reject a valid set solely because it exceeds that count. Rev13 includes a deterministic synthetic 2,000,001-result regression check for this distinction.

## Deletion-Independent Correctness

Deletion remains best effort. Correctness comes from explicit active identities and committed metadata, not cleanup success.

A stale application/scan directory can remain on disk because of antivirus, indexing, locks, permissions, or filesystem failure without becoming eligible for use by a new application session.

## Startup Cleanup

Startup cleanup is conservative:

1. create the new application-session identity;
2. validate the configured root/managed marker;
3. inspect immediate child directories only;
4. require valid Memory Engine ownership/session metadata;
5. attempt stale managed-directory deletion;
6. ignore/log failures;
7. never recursively delete unrelated user content.

Reparse-point protections and managed-parent checks remain in effect.

## Cancellation and Failure

### First Scan

Cancellation/failure before first commit leaves no valid disk-backed set. The scan session is cancelled/failed and removed best effort.

### Next Scan

Cancellation/failure while producing the replacement generation leaves the previous `Committed` metadata untouched. The temporary writer is disposed/deleted best effort and the user can retry refinement from the previous valid set.

### Modal cancellation

Large native result transfers use the generic modal operation-progress component. Pressing Cancel requests the real scan cancellation token. The dialog remains until the underlying writer/native operation reaches a safe terminal state.

## Crash Safety

If the process crashes during a result write, the next application launch gets a new application-session GUID. The old directory is stale by identity.

Within the interrupted session, any temporary/unreferenced result file is not authoritative because `scan.json` never published it as the active committed generation.

## Logging

Storage lifecycle events continue to be written to:

```text
%LocalAppData%\TeeKay87\MemoryEngine\Logs\MemoryEngine.log
```

Result records themselves are never logged individually.

## Verification Targets

Rev13 automated coverage adds:

- successful generation replacement plus abandoned replacement safety;
- a >50,000-result Mock First Scan followed by Next Scan finding a survivor outside the visible preview;
- a synthetic 2,000,001-result native stream written, committed, and re-read completely.

The original live scalability reference remains the earlier 16,211,407-result signed-byte scan. Rev14 confirmed the disk-backed First Scan/preview path with an equivalent physical-PS5 result set of `10,874,862` rows, which committed without the old application-level rejection while only 50,000 rows were materialized for WPF. Rev15 carries the follow-up TurboScan survivor-retrieval correction needed to complete the live resident Next Scan verification.
