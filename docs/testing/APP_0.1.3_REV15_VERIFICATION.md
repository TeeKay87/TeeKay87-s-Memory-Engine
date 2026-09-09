# TeeKay87's Memory Engine 0.1.3.rev15 Verification

## Revision Under Test

```text
Host application:             0.1.3.rev15
Feature:                      TurboScan Survivor Retrieval Fix
PlayStation 5 plugin:         0.1.0.rev14
In-Memory Test Target plugin: 1.0.0.rev3
Plugin API:                   2.2.0
```

## Purpose

Rev15 corrects the live PS5 native Next Scan failure discovered after rev14 successfully retained `10,874,862` disk-backed results.

TurboScan COUNT had already narrowed the server-resident candidate set, but the PS5 client then compared every GET `current_value` against the requested Exact Value a second time. A returned value difference caused the entire replacement generation to fail even though the address had already been reported as a survivor.

Rev15 makes resident START/COUNT authoritative for survivor membership while preserving the one GET-side filter that is still required for Strict Float/Double First Scan semantics.

## Source Changes

### PS5 TurboScan retrieval

`Ps5DebugClient.FetchTurboScanResultBatchesAsync` now:

- validates GET framing, record counts, record widths, and completion as before;
- preserves addresses/current-value bytes returned for server-declared survivors;
- does not run a second Exact Value predicate for integer, Array-of-Bytes, or ps5debug-NG-tolerance floating-point results;
- still exact-filters Strict Float/Double First Scan rows because ps5debug-NG's native floating comparison uses relative `1e-6` tolerance.

The obsolete generic `TurboScanValueMatches` path and its unused fuzzy comparison helpers were removed. `StrictFloatingPointValueMatches` now represents the only client-side GET predicate check.

### Regression fixture

The existing PS5 native exact-value refinement verification now makes the deterministic test server:

1. select the survivor using the requested Next Scan comparison value;
2. return a deliberately different `current_value` in the following GET record;
3. require Memory Engine to retain the survivor and preserve that returned current-value payload.

The verification count remains 33; the existing native exact-value refinement check now covers the live failure mode instead of adding another top-level test.

### Settings text

The Scan Results Storage Location description now reflects current behavior: complete result sets are disk-backed while the DataGrid keeps only a bounded preview in memory.

## Source Review Before Packaging

The rev14 codebase was reviewed before the change and the affected paths were traced after the change.

Confirmed:

- no Plugin API contract change is required;
- Plugin API remains `2.2.0`;
- PS5 plugin revision advances to `0.1.0.rev14`;
- Core disk-backed result format and commit lifecycle are unchanged;
- the complete previous result set is still cross-checked during native Next Scan;
- the 50,000-row limit remains presentation-only;
- the `2,000,000` limit remains only on legacy list/in-memory materialization paths;
- strict Float/Double Next Scan still uses shared Core fallback;
- tolerant Float/Double native refinement remains available;
- the removed fuzzy helpers had no remaining caller after the generic GET revalidation was removed;
- no other scan, memory read/write, process-control, theme, or layout path needs to change for this correction.

## Windows Verification

Keep verification focused:

1. Build/rebuild the solution and confirm there are no compiler errors.
2. Run `TeeKay87.MemoryEngine.Tests` and confirm:

```text
All 33 checks passed.
```

3. Repeat the live PS5 massive-result case:

```text
Value Type: 1 Byte (Signed)
Scan Type:  Exact Value
Value:      50
```

4. Confirm First Scan still succeeds above two million results and still shows only the bounded 50,000-row preview.
5. Run Next Scan against that resident result set.
6. Confirm the previous `ps5debug-NG TurboScan returned a mismatched value...` failure does not recur.
7. Confirm Next Scan completes using the complete previous candidate count and publishes the refined result generation.

## Acceptance

Rev15 can be considered verified when:

- the solution builds;
- all 33 deterministic checks pass;
- a live >2M PS5 First Scan still succeeds;
- native Next Scan completes without the redundant GET current-value rejection;
- the full candidate set, not only the 50,000 visible rows, remains the Next Scan input;
- failed/cancelled replacement behavior continues to preserve the previous committed generation.

## Completed Live Verification - 2026-09-03

Rev15 subsequently completed the intended physical-PS5 verification.

### Automated verification

The verification executable completed with:

```text
All 33 checks passed.
```

This included the disk-backed generation-commit check, complete-set Next Scan check using a result beyond the first 50,000 displayed rows, and the synthetic native stream exceeding the legacy two-million-result limit.

### Massive First Scan

A live PS5 Exact Value scan using:

```text
Value Type: 1 Byte (Signed)
Value:      50
```

committed:

```text
10,953,954 results
```

The disk-backed generation was structurally validated after the run. The UI retained the bounded 50,000-row preview while the complete committed set remained in the scan-result storage session.

### Massive Next Scan

A subsequent Next Scan with the same Value Type, Exact Value predicate, and value `50` refined the complete stored candidate set to:

```text
8,796,420 results
```

The replacement result set committed as generation 2 in the same scan session. The survivor set was a subset of generation 1, and survivors included addresses that were far beyond the first 50,000 rows shown in the First Scan UI preview. This live result confirms that Next Scan uses the complete disk-backed candidate set rather than the presentation subset.

### Pause target while scanning

First Scan and Next Scan were also repeated with **Pause target while scanning** enabled. Both completed successfully and the target pause/resume workflow remained functional with the disk-backed massive-result path.

### Final rev15 status

The rev13 massive-result architecture and rev15 TurboScan survivor retrieval correction are therefore live-verified on a physical PS5 for:

- First Scan above two million results;
- bounded 50,000-row UI preview with complete result retention;
- complete-set disk-backed Next Scan;
- committed generation replacement;
- corrected TurboScan survivor retrieval;
- First Scan with target pause/resume;
- Next Scan with target pause/resume.
