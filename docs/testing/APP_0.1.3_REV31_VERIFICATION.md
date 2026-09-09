# TeeKay87's Memory Engine 0.1.3.rev31 Verification

## Purpose

This checklist verifies **0.1.3.rev31 — Native Value-Type Verification Lifecycle Fix**.

Rev31 is a narrow corrective revision built directly on the complete rev30 source package. It fixes a deterministic deadlock in the dependency-free PS5 native Value Type mapping verification fixture. The production scanner, resident-result architecture, PS5 TurboScan implementation, Scan Type mapping table, New Scan behavior, live visible Scan Result refresh, Core materialization path, disk format, and Plugin API contracts are intentionally unchanged from rev30.

The rev30 automated run reached:

```text
PASS  PS5 native custom-alignment protocol
```

and then stalled in the next registered check, **PS5 native value-type mapping protocol**. Source review established that the Float/Double branch correctly expected strict Exact native refinement to request shared-Core fallback, but did not end the resident TurboScan First Scan session before waiting for the loopback server. The server was waiting for `CMD_PROC_TURBOSCAN_END`, while the test was waiting for server completion.

Rev31 makes resident-session cleanup explicit for every Value Type fixture before the test waits for its protocol server.

## Expected versions

- application: `0.1.3.rev31`;
- feature: `Native Value-Type Verification Lifecycle Fix`;
- Plugin API: `2.9.0`;
- PS5 plugin: `0.1.0.rev21`, targeting Plugin API `2.9.0`;
- Mock plugin: `1.0.0.rev4`, targeting Plugin API `2.0.0`.

The PS5 plugin revision does not change because no PS5 production/plugin implementation changes in rev31. The Plugin API does not change because no public contract changes.

## Actual user-run result

The rev31 automated verification was run on Windows and completed successfully:

```text
All 43 checks passed.
```

This confirmed that the rev30 resident-session lifecycle test deadlock was fixed and that the runner advanced through **PS5 native value-type mapping protocol** and every later registered check.

The manual **New Scan after a Next-only Scan Type** regression also passed: after First Scan, selecting a Next-only predicate and clicking New Scan restored **Exact Value** instead of leaving the Scan Type ComboBox blank.

Large live resident scanning also confirmed the main rev30 optimization. A PS5 **Float -> Unknown Initial Value** scan retained roughly **707 million** candidates target-side without forcing immediate complete PC-disk materialization. A subsequent native **Changed Value** pass completed against the resident set and reported **66,937,361** survivors.

That live Changed pass exposed a separate semantic defect rather than a resident-lifecycle failure: the preview was dominated by `NaN` values. Current ps5debug-NG compares Float/Double Changed/Unchanged with IEEE-754 `!=`/`==`, where an unchanged NaN still satisfies `NaN != NaN`. Rev31 therefore established the resident-result architecture and lifecycle but was not accepted as the final floating Changed/Unchanged semantic baseline. The issue is addressed in rev32 by defining Changed/Unchanged as stored-byte comparisons and keeping the PS5 resident path native through same-width UInt32/UInt64 COUNT comparison.

## 1. Clean Windows build

1. Clean the complete solution.
2. Rebuild the complete solution.
3. Confirm there are no compiler errors or warnings promoted to errors.
4. Confirm `MainWindow.xaml` has no unresolved designer/build diagnostics after the rebuild.
5. Start the application and confirm the title/version is `0.1.3.rev31`.
6. Confirm both built-in plugins are discovered:
   - PlayStation 5 `0.1.0.rev21` / API `2.9.0`;
   - In-Memory Test Target `1.0.0.rev4` / API `2.0.0`.

## 2. Automated verification — primary rev31 regression

Run from the repository root:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 43 checks passed.
```

Pay particular attention to this sequence:

```text
PASS  PS5 native custom-alignment protocol
PASS  PS5 native value-type mapping protocol
PASS  PS5 native exact-value refinement protocol
```

The runner must not stall between custom alignment and value-type mapping.

### What the corrected Value Type fixture proves

`PS5 native value-type mapping protocol` iterates the current PS5-native Value Types:

```text
UInt8
Int8
UInt16
Int16
UInt32
Int32
UInt64
Int64
Float
Double
Array of Bytes
```

For every fixture:

1. a native Exact First Scan creates the resident TurboScan session;
2. the returned Value Type and display value are verified;
3. Float/Double strict Exact refinement must still throw `NotSupportedException`, proving the shared-Core fallback boundary remains intact;
4. the test explicitly calls `INativeValueScanRefiner.ResetAsync(...)` for the resident session;
5. only then does it await loopback-server completion.

For Float/Double, `ResetAsync(...)` is cleanup of the First Scan resident session; it is **not** a change to the semantic decision that strict Exact refinement belongs to Core.

## 3. Rev30 New Scan regression

Because rev30's automated run was interrupted before live acceptance, repeat the rev30 production regression on rev31.

1. Connect to an active target.
2. Select **4 Bytes → Exact Value**, enter a valid value, and complete First Scan.
3. Change Scan Type to a Next-only predicate such as **Changed Value**.
4. Click **New Scan**.

Confirm:

- Scan Type immediately displays **Exact Value**;
- the Scan Type ComboBox is not blank;
- one Value input is visible;
- Value Type and session-locked Scan Options are released for the new session.

Repeat from at least two other Next-only predicates.

## 4. Rev30 large resident First Scan regression

Use **Float → Unknown Initial Value** or another large native snapshot case.

For a result count above 50,000, confirm:

- First Scan completes without immediately transferring the complete native survivor set to PC disk merely to populate Scan Results;
- the UI displays at most the 50,000-row preview while reporting the complete result count;
- the connection remains responsive;
- the old full-transfer **Writing scan results to disk...** phase does not appear solely because a large resident First Scan completed.

## 5. Resident native Next Scan

Continue from the large resident result set.

1. Use a PS5-native-mapped Next predicate such as Changed, Unchanged, Increased, or Decreased.
2. Run Next Scan.
3. Confirm the prior complete resident set is not first materialized to PC disk.
4. Confirm survivor count and preview update correctly.
5. Confirm `Previous` reflects the target-provided previous generation.
6. Run another compatible native Next Scan without reconnecting.

## 6. Core fallback materialization

Use a resident result count that is practical to transfer completely.

1. Select a predicate deliberately kept as Core fallback, such as **Increased By** or **Decreased By**.
2. Run Next Scan.
3. Confirm **Materializing Scan Results** appears.
4. Confirm the complete resident set is written transactionally before Core refinement.
5. Confirm result count is not limited to the 50,000-row preview.
6. Confirm the target remains usable afterward.

## 7. Visible Scan Results live Value refresh

With a known Scan Result visible:

1. use an observable Live value refresh interval such as 500 ms;
2. change the target value without running Next Scan;
3. confirm the visible Scan Result row's **Value** refreshes automatically;
4. confirm **Previous** does not change;
5. scroll the row out of the realized viewport and confirm newly realized rows become the refresh set;
6. scroll back and confirm the original row refreshes again when realized.

Also confirm Saved Addresses still use the same configured Live value refresh interval and that automatic Scan Result reads pause during an active scan.

## 8. Resident-session cleanup regression

From a resident PS5 scan:

1. click New Scan and start a fresh scan;
2. disconnect/reconnect and scan again;
3. where practical, change Active Target and start a new scan.

Expected: no stale resident session or stale result handle prevents subsequent target commands.

## 9. PS5 Float/Double semantic boundary

The rev31 test fix must not accidentally change production floating-point behavior.

Verify representative Float or Double behavior:

- **Strict + Exact Value** remains strict Core semantics where the native resident comparison would use ps5debug-NG's relative tolerance;
- **ps5debug-NG tolerance (1e-6) + Exact Value** may use the declared native path;
- switching between relevant Scan Types still controls Floating-point rounding visibility as defined by rev28;
- Endianness and Alignment session locking remain unchanged.

## 10. Theme/UI and compatibility regression

Confirm in Light, Dimmed, and Dark where practical:

- Scan Results virtualization remains usable;
- visible-row live refresh does not alter row membership or Previous;
- 0/1/2-input Scan Type layouts remain correct;
- Saved Addresses edit/freeze/remove behavior remains intact;
- Mock plugin loads normally;
- no PS5-specific behavior has moved into Core or WPF.

## Static release-preparation boundary

The rev31 source preparation must establish all of the following before packaging:

- host metadata is `0.1.3.rev31` / `Native Value-Type Verification Lifecycle Fix`;
- Plugin API remains `2.9.0`;
- PS5 remains `0.1.0.rev21` targeting `2.9.0`;
- Mock remains `1.0.0.rev4` targeting `2.0.0`;
- the test registry still contains exactly **43** checks;
- `VerifyPs5NativeValueTypesAsync` performs one explicit resident-session `ResetAsync(...)` for every Value Type fixture before `server.Completion`;
- no production PS5/Core/Plugin SDK/Mock behavior changes relative to rev30 other than centralized host revision metadata;
- all C# sources pass structural review with required namespace imports present;
- XAML/project XML and JSON files parse;
- relative Markdown links resolve;
- no `bin`, `obj`, `.vs`, draft files, or nested release ZIPs are included;
- the final ZIP extracts cleanly and every extracted file is byte-identical to the audited source tree.

### Static preparation result

The rev31 pre-package audit completed successfully:

- **232 files** in the release tree;
- **129 C# files** structurally checked;
- **14 XAML/project XML files** parsed successfully;
- **3 JSON files** parsed successfully;
- relative links across **84 Markdown files** resolve;
- exactly **43** verification checks are registered;
- host/Plugin API/PS5/Mock version metadata matches the expected boundaries above;
- no build/editor/draft/nested-ZIP artifacts are present;
- no production PS5/Core/Plugin SDK/Mock source differs from rev30; only centralized AppInfo changes in production code.

Per project workflow, the preparation environment does not claim a .NET/WPF runtime build. The clean Windows build, **43/43** executable, and live rev30 feature acceptance remain user-run verification.

## Completion criteria

Rev31 can be accepted when:

- the Windows solution rebuilds cleanly;
- the automated runner advances past **PS5 native custom-alignment protocol**;
- **PS5 native value-type mapping protocol** reports PASS rather than hanging;
- all **43 checks** pass;
- the rev30 New Scan regression passes live;
- large resident First/Next Scan behavior passes live;
- Core fallback materialization passes;
- visible Scan Result live refresh passes;
- no previously verified PS5/Core/Saved Address/UI behavior regresses.
