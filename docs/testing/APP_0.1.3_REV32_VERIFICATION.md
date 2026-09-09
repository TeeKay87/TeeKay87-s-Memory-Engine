# TeeKay87's Memory Engine 0.1.3.rev32 Verification

## Purpose

This checklist verifies **0.1.3.rev32 — PS5 Floating Changed-Unchanged Semantics Fix**.

Rev32 is a focused semantic correction discovered during rev31 live PS5 acceptance. The resident-result architecture itself worked: a Float Unknown Initial scan retained roughly 707 million candidates target-side and the following Changed Value pass refined them without first materializing the complete set to PC disk. The remaining 66,937,361-result preview was dominated by `NaN`, exposing ps5debug-NG's IEEE-754 Changed/Unchanged behavior (`NaN != NaN`) rather than a resident-storage failure.

Rev32 defines Changed Value and Unchanged Value as comparisons of the stored fixed-width bytes from consecutive scan generations. For PS5 Float/Double resident refinement, the plugin asks ps5debug-NG to perform compare types 9/10 using same-width unsigned integer wire Value Types, preserving exact four-/eight-byte equality while keeping the resident session and displayed Value Type floating-point.

## Expected versions

- application: `0.1.3.rev32`;
- feature: `PS5 Floating Changed-Unchanged Semantics Fix`;
- Plugin API: `2.9.0`;
- PS5 plugin: `0.1.0.rev22`, targeting Plugin API `2.9.0`;
- Mock plugin: `1.0.0.rev4`, targeting Plugin API `2.0.0`.

No Plugin API revision is required because rev32 changes shared Scan Type semantics and a PS5-private native request mapping, not a public plugin contract.

## 1. Clean Windows build

1. Clean the complete solution.
2. Rebuild the complete solution.
3. Confirm there are no compiler errors or warnings promoted to errors.
4. Confirm `MainWindow.xaml` has no unresolved designer/build diagnostics after the rebuild.
5. Start the application and confirm the title/version is `0.1.3.rev32`.
6. Confirm both built-in plugins are discovered:
   - PlayStation 5 `0.1.0.rev22` / API `2.9.0`;
   - In-Memory Test Target `1.0.0.rev4` / API `2.0.0`.

## 2. Automated verification

Run from the repository root:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 44 checks passed.
```

The new check is:

```text
PASS  PS5 native floating Changed/Unchanged bitwise protocol
```

It verifies two independent resident cases:

- Float Unknown Initial -> Changed Value sends TurboScan COUNT Value Type **UInt32** while preserving a four-byte Float resident result and previous/current payloads;
- Double Unknown Initial -> Unchanged Value sends TurboScan COUNT Value Type **UInt64** while preserving an eight-byte Double resident result and previous/current payloads.

The established Core semantic check also verifies that an identical Float NaN byte payload is Unchanged and not Changed, while a different NaN payload is Changed.

## 3. Primary live PS5 regression — Float Unknown Initial -> Changed

Repeat the case that exposed the defect.

1. Connect to PS5 and select the same representative target/process used for the rev31 run where practical.
2. Click **New Scan**.
3. Select **Float**.
4. Select **Unknown Initial Value**.
5. Run First Scan over a large normal scan range.
6. Confirm the complete result count can remain backend-resident and that the UI only fetches its bounded preview.
7. Without forcing materialization, select **Changed Value** and run Next Scan.

Confirm:

- Next Scan operates directly on the resident TurboScan set;
- the old full-result **Writing scan results to disk...** phase does not appear merely to run Changed Value;
- the result preview is no longer flooded by unchanged `NaN` entries solely because IEEE NaN compares unequal to itself;
- the connection remains responsive and another native Next Scan can be run without reconnecting.

The exact survivor count is game/state dependent and is **not** expected to match the prior 66,937,361 value. The acceptance criterion is semantic: unchanged NaN payloads must not survive Changed simply because they decode as NaN.

## 4. Known Float value — Changed and Unchanged

Use a Float address whose value can be observed and intentionally changed.

1. Establish a scan session that contains the known address.
2. Leave its raw value unchanged and run **Unchanged Value**.
3. Confirm the address survives.
4. Change the value and run **Changed Value**.
5. Confirm the address survives the Changed pass.
6. Leave the new value unchanged and run **Changed Value** again.
7. Confirm the address is removed.

This verifies ordinary floating-point values after the byte-comparison change, not only NaN payloads.

## 5. NaN representation behavior

When a naturally occurring NaN candidate is available in the resident preview, use repeated passes to confirm the intended snapshot semantics:

- identical raw NaN bytes across generations -> **Unchanged**;
- identical raw NaN bytes across generations -> not **Changed**;
- a different NaN payload -> **Changed**.

`NaN` is still a valid display string for memory whose bytes decode as a floating-point NaN. Rev32 fixes membership semantics; it does not hide or rewrite NaN values.

Because Changed/Unchanged are byte-based, representation changes are also real changes: for example, `+0.0` and `-0.0` have different bit patterns even though normal floating numeric equality considers them equal.

## 6. Double regression

Where practical, repeat a smaller representative session with **Double**:

1. First Scan with Unknown Initial Value or another path that establishes previous values.
2. Run **Unchanged Value** without modifying a known Double.
3. Confirm it survives.
4. Modify the Double and run **Changed Value**.
5. Confirm it survives the Changed pass.

This validates the eight-byte UInt64 COUNT reinterpretation in live use.

## 7. Resident-result regression

Rev32 must not regress rev30's large-result optimization.

For a result count above 50,000, confirm:

- the complete count is reported while at most 50,000 rows are presented;
- compatible native Next Scans refine the complete resident set, not only the preview;
- no immediate complete PC-disk materialization is required for native Changed/Unchanged;
- `Previous` values returned in the bounded preview remain the previous scan generation;
- another compatible native Next Scan can run on the refined resident set.

## 8. Core fallback materialization regression

Use a resident set of practical size and select a deliberate Core fallback such as **Increased By** or **Decreased By**.

Confirm:

- **Materializing Scan Results** appears only when Core requires the complete set;
- the complete resident membership is transferred transactionally rather than only the preview;
- Core refinement runs after materialization;
- Changed/Unchanged Float/Double no longer trigger this fallback solely to obtain correct NaN semantics.

## 9. Exact/ordered floating semantics are preserved

Rev32 must not broaden the byte-comparison rule beyond Changed/Unchanged.

Verify representative Float/Double behavior:

- Strict **Exact Value** retains its established exact/Core behavior where required;
- **ps5debug-NG tolerance (1e-6)** retains its explicitly tolerant native Exact behavior;
- Bigger Than / Smaller Than / Between remain numeric comparisons;
- Increased Value / Decreased Value remain ordered numeric comparisons;
- Fuzzy Value remains floating-point only;
- Floating-point rounding visibility remains limited to the configured Exact Float/Double applicability.

## 10. New Scan regression

Repeat the already-passed rev31 New Scan case:

1. complete First Scan;
2. select a Next-only predicate such as Changed Value;
3. click **New Scan**.

Confirm **Exact Value** is selected immediately, the Scan Type box is not blank, and session-locked Value Type/options are released.

## 11. Visible Scan Results live Value refresh

With a known visible Scan Result row:

1. use a visible Live value refresh interval such as 500 ms;
2. modify the target value without running Next Scan;
3. confirm only the displayed **Value** refreshes automatically;
4. confirm **Previous** is unchanged;
5. scroll rows into/out of the virtualized viewport and confirm refresh follows realized rows;
6. confirm automatic reads pause during active scan operations.

Saved Addresses must continue to use the same configured refresh interval.

## 12. Cleanup, theme, and compatibility regression

Confirm where practical:

- New Scan releases resident state cleanly;
- disconnect/reconnect works after resident Changed/Unchanged;
- Light, Dimmed, and Dark render Scan Results normally;
- Mock plugin remains functional;
- Saved Addresses behavior remains intact;
- no PS5-specific controls/protocol ids appear in Core or WPF.


## User-run acceptance result

On 2026-09-05, the user confirmed that `0.1.3.rev32` had been verified and that the application appeared to work as intended. This closes the `0.1.3` scanner feature block and allows development to advance to `0.1.4.rev1`. The confirmation is recorded at the revision level; this document does not infer additional per-test measurements beyond the user-reported acceptance.

## Static release-preparation boundary

The rev32 source package must establish all of the following before delivery:

- host metadata is `0.1.3.rev32` / `PS5 Floating Changed-Unchanged Semantics Fix`;
- Plugin API remains `2.9.0`;
- PS5 is `0.1.0.rev22` targeting `2.9.0`;
- Mock remains `1.0.0.rev4` targeting `2.0.0`;
- exactly **44** verification checks are registered;
- Core Changed/Unchanged compare stored bytes, including explicit NaN regression coverage;
- PS5 TurboScan COUNT uses UInt32 only for Float Changed/Unchanged and UInt64 only for Double Changed/Unchanged;
- other Float/Double native predicates retain their established wire Value Types;
- Plugin SDK contracts and disk format are unchanged;
- C# source structure/imports, XAML/project XML, JSON, and relative Markdown links pass static validation;
- no `bin`, `obj`, `.vs`, draft files, or nested release ZIPs are included;
- final ZIP extraction is byte-identical to the audited release tree.

Per project workflow, static preparation does not substitute for the clean Windows build, **44/44** executable verification run, or live PS5 acceptance above.

## Static preparation result

The rev32 pre-package static audit completed successfully:

- **233 files** in the release tree;
- **129 C# files** passed structural delimiter/string/comment review;
- **14 XAML/project XML files** parsed successfully;
- **3 JSON files** parsed successfully;
- relative links across **85 Markdown files** resolve;
- exactly **44** verification checks are registered;
- host, Plugin API, PS5, and Mock version metadata matches the expected boundaries above;
- the PS5 protocol constants used by the workaround remain UInt32=`4`, UInt64=`6`, Float=`8`, and Double=`9`;
- diff review against the user-supplied rev31 package shows **15 intentionally changed files, one new rev32 verification document, and no removed files**;
- no newly introduced ps5debug-NG/TurboScan protocol identifiers appear in generic Core/WPF code; the COUNT wire reinterpretation remains PS5-plugin-local;
- no `bin`, `obj`, `.vs`, draft files, or nested release ZIPs are present.

The intended production-code change boundary is limited to Core Changed/Unchanged snapshot semantics, PS5 TurboScan refinement Value Type selection, PS5 plugin revision metadata, and centralized host revision metadata. Plugin SDK contracts, disk storage format, WPF production code, Mock production code, project files, themes, and all unrelated production paths remain byte-identical to rev31.

## Completion criteria

Rev32 can be accepted when:

- the Windows solution rebuilds cleanly;
- all **44 checks** pass;
- Float Unknown Initial -> Changed no longer retains unchanged NaN payloads because of IEEE NaN inequality;
- ordinary Float and representative Double Changed/Unchanged behavior is correct;
- the corrected predicates remain resident/native and avoid unnecessary complete materialization;
- Exact, ordered, Fuzzy, delta, New Scan, visible live refresh, cleanup, Saved Addresses, UI/theme, and older-plugin compatibility show no regression.
