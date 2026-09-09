# TeeKay87's Memory Engine 0.1.3.rev29 Verification

## Purpose

This checklist verifies **0.1.3.rev29 — New Scan Defaults and TurboScan Snapshot Recovery**. Rev29 is a focused correction to the two regressions found during rev28 acceptance testing:

- New Scan could rebuild the Scan Type list and leave the WPF selector visually blank instead of restoring Exact Value.
- a valid large ps5debug-NG Unknown Initial snapshot could exceed rev28's host-side 1,024-progress-record assumption, causing First Scan to fail before the native response was fully consumed and leaving the shared PS5 command stream desynchronized.

Rev29 must preserve the 13 Core Scan Types, Plugin API 2.8 Scan Option presentation/applicability, disk-backed scan storage, native mapping table, Saved Addresses behavior, and all previously verified scan semantics.

## Expected version boundary

Confirm after building:

- application: `0.1.3.rev29`;
- feature title: `New Scan Defaults and TurboScan Snapshot Recovery`;
- Plugin API: `2.8.0`;
- PS5 plugin: `0.1.0.rev20`, targeting Plugin API `2.8.0`;
- Mock plugin: `1.0.0.rev4`, targeting Plugin API `2.0.0`.

Plugin API does not change in this revision because both fixes are host/PS5 implementation corrections within existing contracts.

## 1. Clean Windows build

1. Clean and rebuild the complete solution in Visual Studio.
2. Confirm there are no compiler errors or warnings promoted to errors.
3. Confirm `MainWindow.xaml` loads without unresolved binding/type diagnostics after the successful build.
4. Start the application and confirm the displayed host version/title match the expected boundary above.
5. Confirm the PS5 and Mock plugins are discovered with the expected independent versions.

Expected result: the complete solution builds and starts normally.

## 2. Automated verification

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 41 checks passed.
```

The rev29 additions specifically verify:

- a successful native Unknown Initial snapshot with **1,500 progress records**, proving the old 1,024-record ceiling is gone;
- a native snapshot refusal after **1,500 progress records**, followed immediately by another PS5 command on the **same connection**, proving the complete refusal response was consumed and the command stream remained synchronized.

## 3. New Scan restores Exact Value

This is the direct regression for the blank Scan Type selector found in rev28.

1. Connect to the PS5 and choose an Active Target.
2. Select a First Scan type other than Exact Value, for example **Between** or **Unknown Initial Value**.
3. Run a First Scan so a scan session exists.
4. Optionally choose a Next Scan-only type such as **Changed Value**.
5. Click **New Scan**.
6. Confirm the Scan Type field is immediately populated with **Exact Value**.
7. Confirm the First Scan type list is restored and no Next-only type remains selected.
8. Repeat New Scan several times after changing Value Type and Scan Type before/after a session.

Expected result: New Scan always restores the Core-declared default Exact Value and the ComboBox never remains blank after its ItemsSource changes.

## 4. Rev28 Scan panel regression

Repeat the accepted rev28 UI behavior:

- Scan Type re-enables immediately after First/Next Scan.
- Value Type remains locked during an active scan session.
- Between renders Value 1 and Value 2 side-by-side.
- zero-operand types hide value inputs; one-operand types use one full-width input.
- PS5 **Little-endian byte order** is a checked-by-default checkbox for applicable multi-byte types.
- Endianness and Alignment lock after First Scan and unlock on New Scan.
- Floating-point rounding is visible only for Float/Double + Exact Value and remains configurable between scans when applicable.
- Pause target while scanning remains independently configurable.

Expected result: rev29's default-selection fix does not regress rev28's accepted UI behavior.

## 5. Large Unknown Initial snapshot — progress-count regression

Use a live PS5 target large enough to exercise native snapshot mode substantially.

1. New Scan.
2. Select **Float** (the value type used when the rev28 issue was observed).
3. Select **Unknown Initial Value**.
4. Run First Scan over the normal large target/module selection.
5. Allow the scan to complete even if it runs for a long time or creates hundreds of millions of candidates.
6. Confirm there is no failure caused merely by the number of TurboScan progress records.
7. Confirm large results can proceed into the established disk-backed result storage path.
8. Run a compatible Next Scan such as Changed Value or Exact Value and confirm the resulting session is usable.

Expected result: snapshot progress is consumed until the protocol sentinel with no host-side record-count ceiling. Candidate count remains governed by the established disk-backed architecture, not by the number of progress notifications.

## 6. Native snapshot refusal and Core fallback recovery

This verifies the failure boundary that required Disconnect/Connect in rev28.

A target-side snapshot can still legitimately report that it could not allocate/store the native snapshot. That is not itself a protocol error.

1. If a naturally resource-constrained Unknown Initial scan produces `snapshot_ok == 0`, allow it to occur. Do not disconnect manually.
2. Confirm the host either continues through the shared Core fallback or reports the native refusal without corrupting the connection.
3. Immediately use another target operation on the same connection, such as Refresh Processes, Refresh Memory Regions, a small memory read, or a normal scan.
4. Confirm the operation succeeds without Disconnect/Connect.
5. If the native refusal cannot be reproduced naturally, rely on the automated protocol fixture for the exact `snapshot_ok == 0` same-connection framing case and still perform the live post-error stability checks for any naturally occurring scan error.

Expected result: a fully framed native refusal cannot leave unread snapshot bytes in the command stream. Core fallback and later PS5 commands remain possible on the existing connection.

## 7. Failure-boundary connection stability

For any live First/Next Scan failure encountered during rev29 testing:

1. dismiss/report the scan error;
2. do **not** Disconnect/Connect;
3. refresh the process list;
4. reload/refresh the active target memory map if appropriate;
5. perform a small raw memory read or another known-safe scan.

Expected result: recoverable scan failures do not require reconnecting merely because the client abandoned a partially read TurboScan response.

## 8. Representative Scan Type regression

Repeat a compact live subset of the already accepted rev28 scanner behavior:

- Exact Value;
- Bigger Than or Smaller Than;
- Between;
- Changed/Unchanged;
- Increased/Decreased;
- Increased By or Decreased By (Core fallback);
- Unknown Initial Value followed by a previous-value Next Scan;
- Fuzzy Value for Float/Double where useful.

Expected result: rev29 does not alter Core comparison semantics or the Core↔PS5 native mapping table.

## 9. Disk-backed massive-result regression

Run or retain evidence from a large scan that exceeds the 50,000-row WPF preview and, when practical, the former 2,000,000-result in-memory boundary.

Confirm:

- the reported total is not truncated to the preview count;
- only the bounded preview is materialized in WPF;
- the complete committed generation is retained on disk;
- Next Scan can refine candidates that were never displayed;
- New Scan invalidates only the temporary scan session and leaves Saved Addresses intact.

Expected result: rev29's native-response recovery does not change disk-backed storage semantics.

## 10. Static package scope

The packaged revision should show only the intended implementation/documentation/test changes relative to rev28:

Production code:

- host application metadata;
- `PluginViewModel` New Scan/default Scan Type refresh logic;
- PS5 `Ps5DebugClient` TurboScan snapshot response handling and adjacent resident-response cleanup;
- PS5 plugin metadata revision.

Verification code:

- PS5 protocol fixture support for long snapshot-progress streams and snapshot refusal;
- the new same-connection recovery check;
- the existing Unknown Initial native test expanded beyond 1,024 progress records.

Core Scan Type definitions, Plugin SDK/API contracts, PS5 native mapping definitions, Mock production code, disk-backed result format, Saved Addresses code, and unrelated UI behavior must remain unchanged.

## Static package-preparation result

The rev29 source tree completed the pre-package static release audit on 2026-09-05.

- **229 files** are present in the complete source tree.
- All **128 C# files** passed delimiter/string/comment structural checks.
- All **14 XAML/project XML files** parse successfully.
- All **3 JSON files** parse successfully.
- All relative Markdown links across **82 Markdown files** resolve.
- The verification runner registers **41 checks**.
- Host metadata resolves to `0.1.3.rev29`; Plugin API remains `2.8.0`; PS5 resolves to `0.1.0.rev20` / API `2.8.0`; Mock remains `1.0.0.rev4` / API `2.0.0`.
- No `bin`, `obj`, or `.vs` build artifacts are present.
- Diff review against the supplied rev28 baseline found **no removed files**, one added rev29 verification document, and only the documented host/PS5/test/documentation files changed.
- The complete Core production tree, complete Plugin SDK production tree, complete Mock production tree, and `Ps5NativeScanTypeMappings.cs` are byte-identical to rev28.
- The old snapshot START `progressRecordCount > 1024` rejection is absent. New Scan explicitly requests the Core default selection when clearing the real scan session.
- Modified C# files were rechecked for nullable/using requirements under the repository-wide `TreatWarningsAsErrors=true` policy.

Per project workflow, this is a static source/package-preparation audit only. The Windows .NET build, 41-check executable, UI regression, and live PS5 recovery tests remain user-run acceptance steps.

## User-run verification result (2026-09-05)

The Windows verification executable completed successfully with **41/41 checks passed**. This includes the rev29 long Unknown Initial snapshot-progress regression and the same-connection snapshot-refusal recovery check.

Live acceptance did **not** fully accept rev29:

- **New Scan default regression still reproducible:** after completing First Scan, selecting a Scan Type that is valid only for Next Scan, and then clicking **New Scan**, the Scan Type ComboBox became empty instead of selecting Core's **Exact Value** default. The failure depends on the previous selection being removed by the stage transition, which narrowed the remaining defect to WPF collection/selection synchronization rather than the Core catalog itself.
- **Large Unknown Initial transfer cost exposed:** a live PS5 Float → Unknown Initial Value scan successfully reached the native snapshot/result stage and advertised **705,163,264** survivors. Rev29 then began downloading/materializing the entire result set to the host. At the captured point it had stored **9,076,736 / 705,163,264** results (1%) after approximately 31 seconds. The scan was used as the performance baseline rather than waiting for the complete transfer.
- Because the full-transfer behavior made that live path impractical, the remaining large-result acceptance work moved to rev30's backend-resident result design. The automated rev29 command-stream recovery result remains valid, but rev29 itself is not considered the accepted endpoint of the scanner feature block.

These findings directly define the rev30 regressions: stable First Scan default selection after a Next-only predicate, deferred materialization of large authoritative native result sets, and live-value refresh limited to rows actually visible in Scan Results.

## Completion criteria

Rev29 can be accepted when:

- the Windows solution builds cleanly;
- all **41** automated checks pass;
- New Scan reliably restores **Exact Value** and never leaves Scan Type blank;
- a large native Unknown Initial snapshot is not rejected because it emits more than 1,024 progress records;
- a native snapshot refusal leaves the connection synchronized and does not require Disconnect/Connect;
- representative native and Core-fallback scans retain their previous semantics;
- disk-backed massive-result behavior remains intact;
- no regression appears in rev28's accepted Scan panel option/layout behavior.
