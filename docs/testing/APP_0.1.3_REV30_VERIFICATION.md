# TeeKay87's Memory Engine 0.1.3.rev30 Verification

## Purpose

This checklist verifies **0.1.3.rev30 — Resident Scan Results and Live Visible Values**. Rev30 addresses the remaining New Scan selection regression from rev29, avoids immediate full host materialization of very large authoritative native result sets, and adds live Value refresh for only the Scan Results rows currently realized in the WPF viewport.

The revision deliberately preserves the existing Core Scan Type semantics, disk-backed result format, native Scan Type mapping table, Saved Address edit/freeze behavior, plugin-scoped settings, PS5 connection/process/memory protocols, and Mock plugin behavior.

## Expected versions

- application: `0.1.3.rev30`;
- feature: `Resident Scan Results and Live Visible Values`;
- Plugin API: `2.9.0`;
- PS5 plugin: `0.1.0.rev21`, targeting Plugin API `2.9.0`;
- Mock plugin: `1.0.0.rev4`, targeting Plugin API `2.0.0`.

## 1. Clean Windows build

1. Clean the complete solution.
2. Rebuild the complete solution.
3. Confirm there are no compiler errors or warnings promoted to errors.
4. Confirm `MainWindow.xaml` has no unresolved designer/build diagnostics after the rebuild.
5. Start the application and confirm the title/version is `0.1.3.rev30`.
6. Confirm both built-in platform plugins are discovered.

Expected result: the host starts normally with PS5 `0.1.0.rev21` / API `2.9.0` and Mock `1.0.0.rev4` / API `2.0.0`.

## 2. Automated verification

Run from the repository root:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 43 checks passed.
```

Rev30 adds two checks to the 41-check rev29 baseline:

- **Generic resident native result contract** — bounded preview reads, Previous-value transport, refinement eligibility, and complete on-demand materialization through the shared disk-backed writer;
- **PS5 resident TurboScan bounded preview and refinement** — resident handle exposure, bounded GET, a 1,500-record COUNT progress stream, Previous-value payloads, and generation replacement after native refinement.

## 3. New Scan default-selection regression

This is the exact rev29 failure case.

1. Connect to an active target.
2. Select **4 Bytes → Exact Value**, enter a valid value, and complete First Scan.
3. Change Scan Type to a **Next-only** predicate such as **Changed Value**.
4. Click **New Scan**.

Confirm immediately:

- Scan Type displays **Exact Value**;
- the ComboBox is not blank;
- one Value input is visible;
- Value Type and session-locked Scan Options are released for the new session.

Repeat from at least two other Next-only predicates, for example Increased Value, Decreased Value, Unchanged Value, Increased By, or Decreased By. Also repeat from a First-compatible predicate such as Between.

Expected result: every New Scan returns to a valid First Scan state with Exact Value selected on the first UI update.

## 4. Large PS5 resident First Scan

Use the same class of live case that exposed rev29's full-transfer cost. Float → Unknown Initial Value is the primary regression path.

1. Connect to PS5 and select an appropriate active target.
2. Run **New Scan → Float → Unknown Initial Value** across the normal readable regions.
3. Allow the native snapshot to complete.

For a result count above 50,000, confirm:

- the application does **not** immediately open the old full **Saving Scan Results / Writing scan results to disk...** transfer for the complete native survivor count;
- no hundreds-of-millions record GET is required merely to finish First Scan;
- Scan Results displays at most the 50,000-row preview;
- the UI still reports the complete native result count rather than 50,000;
- the target remains connected and responsive after First Scan.

If the same target/regions produce a count comparable to the rev29 baseline of 705,163,264 survivors, record the First Scan completion time for comparison. The exact count is target-state dependent and is not itself a pass criterion.

## 5. Native Next Scan against a resident result set

Continue directly from the large resident First Scan.

1. Change a known target value.
2. Choose a PS5-native-mapped Next predicate such as **Changed Value**, **Unchanged Value**, **Increased Value**, or **Decreased Value**.
3. Run Next Scan.

Confirm:

- the Next Scan operates without first materializing the complete prior resident set to host disk;
- the total survivor count narrows correctly;
- only the bounded preview is loaded when the remaining count is still above 50,000;
- the preview's **Previous** column contains the value stored by the target-side previous generation;
- another compatible native Next Scan can run immediately afterward.

Expected result: compatible native refinement remains target-resident across generations.

## 6. On-demand Core fallback materialization

Use a resident result set small enough that deliberately transferring the complete set is practical.

1. Create an authoritative resident PS5 result set with more than 50,000 results.
2. Select a predicate that deliberately requires Core fallback, such as **Increased By** or **Decreased By**.
3. Run Next Scan.

Confirm:

- a modal **Materializing Scan Results** operation appears before Core refinement;
- progress reflects records received/stored out of the complete current resident count;
- the complete resident set is committed through the normal disk-backed generation path;
- the native resident session is released only after successful materialization;
- Core Next Scan then produces the expected survivors;
- the result total is not limited to the 50,000-row preview.

Repeat with cancellation if practical. Cancelling during materialization must not publish a partial replacement generation or leave the target command stream unusable.

## 7. Visible Scan Results live Value refresh

Use a scan result list containing an address whose value can be changed in the target. Set **Live value refresh** to an easily observed interval such as 500 ms.

1. Leave the known address visible in the Scan Results DataGrid.
2. Change its value in the target without running Next Scan.
3. Confirm the row's **Value** updates automatically.
4. Confirm **Previous** does not change.
5. Scroll so the row leaves the realized viewport and bring other rows into view.
6. Change a newly visible known value and confirm it begins refreshing.
7. Scroll back and confirm the original row refreshes again when realized.

Also confirm:

- Saved Addresses continue using the same configured Live value refresh interval;
- Saved Address Frozen behavior remains independent;
- automatic Scan Results refresh stops while First/Next Scan is active and resumes afterward;
- refreshing visible Scan Results does not alter candidate membership, result count, or Next Scan baseline;
- there is no periodic traversal/read of all 50,000 preview rows merely because the result list contains that many rows.

## 8. Resident-session lifecycle and cleanup

From a resident PS5 scan session:

1. Click **New Scan** and immediately run a fresh First Scan.
2. Repeat after changing Active Target where practical.
3. Disconnect and reconnect, then run another scan.

Expected result: old resident state is released through the native reset/disposal path, stale handles cannot terminate a newer generation, and no manual Disconnect/Connect is required to recover from normal New Scan use.

## 9. Snapshot/refinement protocol recovery regression

The automated fixture already covers more than 1,024 progress records for both snapshot START and resident COUNT. During live use, if ps5debug-NG refuses a snapshot or another native operation falls back:

- the current response must be fully drained;
- the connection must remain synchronized;
- Refresh/process enumeration and a subsequent ordinary memory/scan operation must work on the same connection.

Expected result: rev29's command-stream recovery remains intact after the rev30 resident-result changes.

## 10. UI, theme, and compatibility regression

Repeat the Scan workspace in Light, Dimmed, and Dark themes and resize the main window. Confirm:

- Scan Results virtualization/scrolling remains smooth;
- no row remains registered after it is unrealized/recycled;
- the Scan Results/Saved Addresses splitters still work;
- the Scan panel scrollbar still reaches all controls;
- dynamic 0/1/2-value Scan Type inputs remain correct;
- PS5 Endianness/Alignment/Floating-point option applicability remains as accepted in rev28;
- Mock plugin loads normally and exposes no PS5-specific resident behavior;
- older Plugin API 2.x compatibility behavior remains unchanged.

## Static package-preparation result

The rev30 source tree completed the pre-package static release audit on 2026-09-05.

- **231 files** are present in the complete source tree.
- All **129 C# files** passed delimiter/string/comment structural checks.
- All **14 XAML/project XML files** parse successfully.
- All **3 JSON files** parse successfully.
- All relative Markdown links across **83 Markdown files** resolve.
- The verification runner registers **43 checks**.
- Host metadata resolves to `0.1.3.rev30`; Plugin API resolves to `2.9.0`; PS5 resolves to `0.1.0.rev21` / API `2.9.0`; Mock remains `1.0.0.rev4` / API `2.0.0`.
- No `bin`, `obj`, `.vs`, or rev30 draft documentation files are present.
- Diff review against the exact user-supplied rev29 baseline found **no removed files**, two intended additions (`INativeValueScanResidentResultSet.cs` and this verification document), and only the documented production/test/documentation files changed.
- The complete Mock production tree and `Ps5NativeScanTypeMappings.cs` are byte-identical to rev29.
- Generic App/Core/Plugin SDK production code contains no ps5debug-NG/TurboScan protocol identifiers or PS5 plugin-id branches; platform-specific resident transport/state remains inside the PS5 plugin.
- Modified C# paths were rechecked for required namespace imports and nullable-sensitive call sites under the repository-wide `TreatWarningsAsErrors=true` policy.

Per project workflow, this is a static source/package-preparation audit only. The Windows .NET build, **43-check** executable, exact New Scan UI regression, visible-row live refresh, performance comparison, and live PS5 resident-session tests remain user-run acceptance steps.

## Completion criteria

Rev30 can be accepted when:

- the Windows solution builds cleanly;
- all **43** automated checks pass;
- New Scan restores **Exact Value** after any Next-only Scan Type;
- a large authoritative PS5 First Scan no longer performs immediate complete host materialization merely to display results;
- compatible native Next Scans refine the resident set without that materialization;
- Core fallback materializes the full resident set transactionally and then refines it correctly;
- only currently realized Scan Results rows receive automatic live Value refresh and Previous remains unchanged;
- resident reset/disconnect/target-change lifecycle remains safe;
- rev29 snapshot/command-stream recovery and the previously accepted scanner semantics remain intact.

## Recorded Windows automated-verification result — 2026-09-05

The rev30 verification executable was run on the Windows development machine. The runner reported PASS for every check through:

```text
PASS  PS5 native custom-alignment protocol
```

No result was printed for the following registered check, **PS5 native value-type mapping protocol**, even after an extended wait. The run was therefore stopped and rev30 did not complete the 43-check acceptance boundary.

Source review isolated the stall to the Float/Double branch of `VerifyPs5NativeValueTypesAsync`. Every native First Scan in that fixture creates an authoritative resident TurboScan session. For integer and Array-of-Bytes fixtures the test explicitly called `INativeValueScanRefiner.ResetAsync(...)` before awaiting the loopback server. For Float/Double, the fixture correctly verified that strict Exact refinement throws `NotSupportedException` so the application can fall back to shared Core semantics, but the test then waited for `server.Completion` without ending the still-resident TurboScan session. The loopback server was simultaneously waiting for `CMD_PROC_TURBOSCAN_END`, producing a deterministic verification-harness deadlock.

This finding concerns the dependency-free protocol fixture lifecycle. It does not change the intended production behavior: strict Float/Double Exact refinement remains a Core-fallback case, and the resident native First Scan session remains valid until the host explicitly resets/materializes/replaces it according to the normal scan lifecycle.

Rev31 corrects the fixture so every Value Type path explicitly resets the resident native session before waiting for protocol-server completion. The rev30 live acceptance sections after automated verification were not treated as completed from this interrupted run.
