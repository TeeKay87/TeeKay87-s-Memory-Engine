# Memory Scanner Foundation

## Purpose

The initial shared memory scanner begins in **TeeKay87's Memory Engine 0.1.2.rev1** after the complete low-level target-access chain was live-verified against a real PlayStation 5.

The scanner is a Core subsystem. Platform plugins continue to provide target access through neutral Plugin SDK services; they do not own the ordinary value-matching algorithm.

Current data paths:

```text
Generic path
Active Target
    ↓
neutral MemoryRegion map + IMemoryReader
    ↓
Core MemoryScanner
    ↓
MemoryScanResult candidates

Optional native acceleration
Active Target
    ↓
INativeValueScanner
    ↓
plugin/backend target-side scan
    ↓
matching addresses
    ↓
Core validation/normalization
    ↓
MemoryScanResult candidates
```

The generic scanner remains the cross-platform definition of the current value-scan behavior. A plugin-native scanner is an optional acceleration service. Backend packets, command ids, and raw platform data remain inside the plugin.

## Current Exact Value Support

Host `0.1.3.rev1` expands the scanner from the original Int32-only milestone to the complete value-type set exposed by ps5debug-NG:

| UI value type | Core type | ps5debug-NG wire id | Width / alignment |
| --- | --- | ---: | --- |
| 1 Byte (Unsigned) | `UInt8` | 0 | 1 / 1 |
| 1 Byte (Signed) | `Int8` | 1 | 1 / 1 |
| 2 Bytes (Unsigned) | `UInt16` | 2 | 2 / 2 |
| 2 Bytes (Signed) | `Int16` | 3 | 2 / 2 |
| 4 Bytes (Unsigned) | `UInt32` | 4 | 4 / 4 |
| 4 Bytes (Signed) | `Int32` | 5 | 4 / 4 |
| 8 Bytes (Unsigned) | `UInt64` | 6 | 8 / 8 |
| 8 Bytes (Signed) | `Int64` | 7 | 8 / 8 |
| Float | `Float32` | 8 | 4 / 4 |
| Double | `Float64` | 9 | 8 / 8 |
| Array of Bytes | `ByteArray` | 10 | variable / 1 |

The current comparison mode remains **Exact Value**. Integer fields accept decimal and `0x`-prefixed hexadecimal input. Float and Double use invariant decimal notation. Array of Bytes accepts hexadecimal bytes in spaced or compact form and is currently bounded to 4,096 bytes so the same value can remain resident in the PS5 TurboScan path.

Array of Bytes currently uses an exact all-bytes mask. ps5debug-NG supports mask-driven byte arrays, but wildcard syntax is deliberately deferred until the scanner exposes AOB-specific input semantics rather than overloading the first Exact Value implementation.

The Value Type selector is editable before First Scan and locked for the active scan session. New Scan unlocks it. This prevents a Next Scan from silently changing type or width relative to the existing candidate set.

## Region Eligibility

The generic scanner currently scans regions that:

```text
Read  = true
Guard = false
Size >= selected value width
```

Writable and executable status do not exclude a region from value scanning. Those flags are neutral memory attributes and can become user-selectable filters later.

The scanner consumes the Active Target memory map already cached by the host. It does not request or parse a platform-specific memory map itself.

## Alignment

Numeric scans use their natural width as the absolute address alignment: 1, 2, 4, or 8 bytes. Array of Bytes uses one-byte candidate alignment so an exact sequence can start at any byte address.

Alignment is part of `MemoryScanValue` / `NativeValueScanRequest`, allowing Core and optional native implementations to apply the same candidate-start rule.

## Endianness

Integer and floating-point value encoding/decoding uses `TargetArchitecture.Endianness` supplied by the active target session. Byte arrays are preserved byte-for-byte.

The Core scanner therefore does not globally assume little-endian memory. This keeps the same scanner compatible with future big-endian targets such as an Xbox 360 backend.

## Read Chunking

The scanner reads target memory in bounded chunks instead of allocating an entire process region at once.

Current chunk size:

```text
256 KiB
```

The 4096-byte cap used by the temporary Raw Memory Read diagnostic UI is not reused by the scanner.

Chunking is calculated in candidate space from the selected value width and alignment. Each read contains every byte required for its candidate starts, so wider values and one-byte-aligned byte arrays are not lost at Core chunk boundaries.

## First Scan

The generic First Scan:

1. receives the current Active Target, neutral memory map, `IMemoryReader`, target architecture, and parsed `MemoryScanValue`;
2. filters the memory map to regions that can contain the selected width;
3. reads each eligible region in bounded candidate-aware chunks;
4. compares candidates using the selected value type, width, alignment, and target endianness;
5. retains addresses equal to the requested Exact Value;
6. reports progress and candidate count;
7. returns the complete temporary candidate set to the host.

First Scan results have no previous value.

### Native First Scan acceleration

From `0.1.2.rev4`, Core can also perform an Exact Value operation through the optional Plugin SDK `INativeValueScanner` service. From `0.1.3.rev1`, the same neutral path carries every supported ps5debug-NG value type through `NativeValueScanRequest`. Core supplies both the neutral scannable `MemoryRegion` set and the encoded value/type/alignment request; the plugin returns matching addresses only.

Core still owns the shared result semantics. Returned native addresses are:

- de-duplicated;
- required to satisfy the selected value type's alignment rule;
- checked against the currently cached neutral readable/non-guarded memory map;
- constrained by the same 2,000,000-result safety boundary;
- converted to the same `MemoryScanResult` model used by the generic scanner.

For PS5, plugin `0.1.0.rev9` implements this service through ps5debug-NG TurboScan. Core passes its neutral readable/non-guarded region set to the optional service; the plugin negotiates server-resident multi-segment support, authenticates scanning, executes the comparison on the target, and fetches only survivor address/value records. This directly addresses the real-console rev3 measurement where the generic First Scan took approximately `07:04.3` while Next Scan was effectively instant.

The current TurboScan resident Exact Value path does not expose a host-consumed incremental percentage stream. The host therefore presents indeterminate progress plus elapsed time for that path. If TurboScan support is absent or a resident result set cannot be retained, the host uses the same shared Core First Scan as before.

## Next Scan

Next Scan refines the existing candidate set rather than scanning every address again.

The shared Core refinement implementation remains the compatibility baseline. Candidates are ordered by address and associated with the current readable memory map; Core reads only chunks that contain existing candidates. Each surviving result stores:

```text
Address
CurrentValue
PreviousValue
RegionName
ModuleName
```

`PreviousValue` is the candidate value from the preceding scan. A candidate survives only when its newly read value, interpreted with the active scan type/width/endianness rules, equals the new Exact Value. The Value Type and width must remain compatible with the First Scan session.

### Native Next Scan refinement

From `0.1.2.rev5`, Core can also refine an existing native result set through the optional Plugin SDK `INativeValueScanRefiner` service. Core encodes the new comparison value through the same target-endian `NativeValueScanRequest`, passes the previous absolute address list for session-consistency checking, and normalizes returned survivor addresses against the previous host result dictionary. The new host result keeps region/module context and sets `PreviousValue` from the preceding host-side `CurrentValue`.

For PS5 plugin `0.1.0.rev9`, a successful TurboScan First Scan intentionally leaves the server-resident survivor set alive on the command connection. Integer and exact Array-of-Bytes Next Scan use TurboScan COUNT (`0xBDAACC12`) with `TS_SERVER_RESIDENT`, optionally add `TS_RESCAN_ALIASING` when the connected server advertises that engine, then fetch the new survivor addresses through TurboScan GET. Float/Double Next Scan closes the resident session and uses the shared Core refiner because current ps5debug-NG resident refinement applies fuzzy floating-point equality and therefore cannot represent Memory Engine's strict Exact Value semantics.

If the selected type requires strict Float/Double fallback, or if the resident result count no longer agrees with the host candidate count, the target-side session is closed and native refinement reports `NotSupportedException`; the host then runs the unchanged shared Core Next Scan. This preserves exact comparison semantics and avoids continuing with divergent target/host state.

The current PS5 resident list refinement response sends no useful percentage records, so the host displays indeterminate progress for native Next Scan. The shared Core fallback continues to report determinate percentage progress.

If the Active Target changes, the host clears the scan session and asks an available native refiner to release target-side state first. A process-list refresh that retains the same Active Target may keep the scan session and resident PS5 survivor set.

## Optional Process Pause

Plugin API `1.1.0` adds the optional `IProcessControl` service. When a plugin advertises both `ProcessSuspend` and `ProcessResume`, the Scan panel exposes **Pause target while scanning**.

The option is Off by default. When enabled, the host suspends the current Active Target immediately before First Scan or Next Scan and attempts to resume it in a `finally` path after success, cancellation, or ordinary failure. This orchestration is capability-driven; Core does not contain PS5-specific process-control logic.

A resume failure is surfaced as a scan error because leaving a target suspended is operationally significant.

## Cancellation and Command Coordination

The host owns a cancellation token for the active scan operation.

While a scan is running, operations that can invalidate target/session state are blocked, including:

- Disconnect;
- process Refresh;
- Set Active Target;
- Raw Memory Read;
- Raw Memory Write;
- Safe Write Test.

**Cancel Scan** requests cancellation without disposing the target session. A cancelled Next Scan preserves the previous complete result set. A cancelled First Scan does not publish a partial candidate set.

Cancellation is cooperative across the neutral `IMemoryReader` boundary. Core checks the cancellation token before each scanner read and passes the token to the plugin, but a transport is allowed to define a later safe cancellation boundary when abandoning an in-flight operation would corrupt protocol framing.

This became necessary during live PS5 rev1 testing. ps5debug-NG uses one shared framed TCP command stream. Cancelling `ReadExactlyAsync` after a `CMD_PROC_READ` request had already been sent could leave the remainder of that command's response on the stream. The next command then interpreted target bytes as its status word.

From host rev2 / PS5 plugin rev6, the PS5 client therefore:

1. checks caller cancellation before a read/write command starts;
2. once the command starts, completes the full framed read/write transaction without mid-response cancellation;
3. returns to Core;
4. Core observes the requested cancellation before issuing another scanner read.

This can make Cancel Scan wait for the current memory-read request to finish, but the existing TCP session remains synchronized and reusable. The rule is PS5 transport behavior, not a PS5 condition inside Core.

The same principle applies to PS5 TurboScan. There is no asynchronous abort opcode for an already-running resident START/COUNT transaction on the shared connection. Rev5 therefore reports the cancellation request immediately in the status bar, finishes consuming the current target operation to a clean protocol boundary, closes any resident session that can no longer be safely continued, and only then returns cancellation. The button is responsive even when cancellation itself must be deferred for transport safety.

## Failure Handling

Individual target read failures are counted so an isolated inaccessible range does not immediately destroy an otherwise useful scan.

To prevent a disconnected or badly stale target from causing thousands of repeated failing requests, the current scanner stops after:

```text
32 memory-read failures
```

The host surfaces the error and instructs the user to refresh the target memory map and verify connection health.

## Result Safety Limit

The initial Core scanner refuses to retain more than:

```text
2,000,000 candidates
```

This prevents an extremely broad value such as zero from unintentionally consuming unbounded host memory during the early scanner milestone.

The limit is a current implementation safety boundary, not part of the Plugin SDK contract and not a statement about the long-term scanner architecture.

## WPF Result Presentation

The Core retains the complete candidate set up to the safety limit.

The WPF Scan Results DataGrid uses row/column virtualization and materializes at most the first:

```text
50,000 rows
```

for display. The header continues to report the complete Core candidate count. Next Scan always refines the complete candidate set, not merely the displayed subset.

This separation avoids creating hundreds of thousands or millions of WPF presentation objects simply to perform a Core operation.

From `0.1.2.rev2`, address presentation is compact rather than fixed-width: redundant high-order zeroes are omitted while the complete significant hexadecimal address remains visible. Scan Result rows also expose presentation-layer context actions for copying the address/value and transferring an address to the temporary Raw Memory Write diagnostic. These actions operate on `MemoryScanResult` data and do not change Core scanner storage.

Scan status, errors, progress, and elapsed time are presented in the application's permanent status bar. Generic Core scans use percentage progress from `MemoryScanProgress`; native scans without a backend progress stream use an indeterminate progress presentation. Timing remains presentation/session state owned by the host.

## Core Models

The current scanner implementation is located under:

```text
src/TeeKay87.MemoryEngine.Core/Scanning/
```

Key types:

```text
MemoryScanner
MemoryScanResult
MemoryScanExecutionResult
MemoryScanProgress
ScanResultLimitExceededException
```

These types remain in Core because the generic scan algorithm and scan-session state do not cross the platform-plugin boundary.

Plugin API `1.1.0` was introduced in `0.1.2.rev4` for `INativeValueScanner` and `IProcessControl`. Plugin API `1.2.0` was introduced in `0.1.2.rev5` for the optional `INativeValueScanRefiner` contract. Host `0.1.3.rev1` does not require another Plugin API bump because the existing `MemoryValueType` enum and generic native scan request already contain every value type required by ps5debug-NG. The concrete parsing/display models remain in Core. Existing plugins targeting older minor versions within Plugin API major version 1 remain compatible under the same-major/older-minor compatibility rule.

## Verification Target

The deterministic Mock plugin provides known values:

```text
Health: 0x10000100 = 100.0f
Ammo:   0x10000104 = 30
Money:  0x10000108 = 5000
```

The verification executable now tests the shared scanner by:

1. running First Scan for Int32 `30`;
2. confirming the Ammo address is returned;
3. changing Ammo through the existing neutral `IMemoryWriter` to `25`;
4. running Next Scan for Int32 `25`;
5. confirming only the Ammo candidate remains;
6. confirming Current = `25` and Previous = `30`.

This test exercises the same Core scanner that the PS5 UI uses without requiring a live console.

## Next Scanner Milestones

After rev5 native First/Next Scan refinement and the updated scan workflow are live-verified, scanner development can expand incrementally. Likely next steps include:

- additional integer and floating-point value codecs;
- Unknown Initial Value;
- Changed / Unchanged;
- Increased / Decreased;
- Increased By / Decreased By;
- configurable alignment/Fast Scan;
- writable/executable/private/shared filters;
- selected region/module ranges;
- improved large-result storage;
- Saved Addresses integration.

Native/plugin-side scanning is now an implemented optional acceleration path for PS5 First Scan and Next Scan refinement. The shared Core scanner remains the fallback and continues to define the common result/refinement behavior. Future scanner work should preserve this separation rather than moving shared scan-session semantics into platform plugins.
