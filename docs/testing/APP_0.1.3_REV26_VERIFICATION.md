# TeeKay87's Memory Engine 0.1.3.rev26 Verification

## Purpose

This checklist verifies **TeeKay87's Memory Engine 0.1.3.rev26 - Native Scan Type Mapping and Complete Core Scan Types**. It completes the rev25 Scan Type work by adding the final two Core predicates, introduces the generic Core-to-plugin native mapping contract, expands the PS5 TurboScan path beyond Exact Value, and preserves a Core fallback whenever a plugin-native operation is unavailable or is not semantically equivalent.

This checklist also carries forward the rev24/rev25 input-validation verification because the scanner feature block must be verified as a whole before the application version advances.

## Expected version boundary

After building this revision, confirm:

- application: `0.1.3.rev26`;
- feature title: `Native Scan Type Mapping and Complete Core Scan Types`;
- Plugin API: `2.7.0`;
- PS5 plugin: `0.1.0.rev18`, targeting Plugin API `2.7.0`;
- Mock plugin: `1.0.0.rev4`, targeting Plugin API `2.0.0`;
- the Mock plugin still loads through the established same-major/older-minor compatibility rule.

## 1. Windows build and startup

1. Rebuild the complete solution on Windows.
2. Confirm there are no compile errors or new warnings indicating missing contracts, namespace imports, XAML bindings, or incompatible plugin metadata.
3. Start the application normally.
4. Confirm the title/version surfaces report `0.1.3.rev26`.
5. Open the plugin list/workspace and confirm both built-in plugins are discovered and load successfully.
6. Confirm PS5 reports `0.1.0.rev18` and Mock reports `1.0.0.rev4`.

## 2. Automated verification executable

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 39 checks passed.
```

The rev26-specific automated coverage must include:

- all 13 Core Scan Type ids and their stage/input metadata;
- Fuzzy Value semantics for Float32/Float64;
- Unknown Initial Low Value Core semantics;
- Array of Bytes exclusion from fixed-width ordered/delta predicates;
- the generic `INativeScanTypeMappingProvider`/`NativeScanTypeMapping` contract;
- stage and Value Type restrictions in a generic mapping;
- PS5 semantic mapping metadata;
- PS5 native Between payload construction with two operands;
- PS5 native Changed Value refinement with zero operands;
- PS5 Unknown Initial Value snapshot creation with include-zero semantics followed by native refinement.

Record the full output if any check fails before continuing to live-target tests.

## 3. Complete Core Scan Type catalog

### 3.1 Integer First Scan list

Connect the Mock target, select **4 Bytes**, and start a fresh scan session. Before First Scan, the Scan Type selector should offer:

- Exact Value;
- Bigger Than;
- Smaller Than;
- Between;
- Unknown Initial Value;
- Unknown Initial Low Value.

**Fuzzy Value** must not be offered for integer Value Types.

### 3.2 Float/Double First Scan list

Select **Float** or **Double**. Before First Scan, the selector should offer:

- Exact Value;
- Fuzzy Value;
- Bigger Than;
- Smaller Than;
- Between;
- Unknown Initial Value;
- Unknown Initial Low Value.

### 3.3 Array of Bytes First Scan list

Select **Array of Bytes**. Confirm **Exact Value** remains available and numeric ordered/delta predicates are not offered. Variable-width Array of Bytes must not expose operand-free unknown/previous-value workflows because no fixed candidate width can be inferred without an input pattern.

### 3.4 Next Scan list

After a successful fixed-width numeric First Scan, confirm valid Next Scan choices include the applicable direct predicates plus:

- Increased Value;
- Decreased Value;
- Changed Value;
- Unchanged Value;
- Increased By;
- Decreased By.

For Float/Double, **Fuzzy Value** should also remain available as a direct Next Scan predicate. First-only Unknown Initial Value and Unknown Initial Low Value must disappear after the initial generation is committed.

## 4. Dynamic Scan input layout

Verify the Scan panel exposes only the operands required by the selected Core predicate:

- **Unknown Initial Value**, **Increased Value**, **Decreased Value**, **Changed Value**, **Unchanged Value**: no Value editor;
- **Exact Value**, **Fuzzy Value**, **Bigger Than**, **Smaller Than**, **Unknown Initial Low Value**, **Increased By**, **Decreased By**: one Value editor;
- **Between**: two editors, **Value 1** and **Value 2**.

Also confirm:

1. Between blocks a lower bound greater than its upper bound.
2. Unknown Initial Low blocks a zero or negative upper limit.
3. Increased By/Decreased By keep their non-negative delta validation.
4. Enter submits a scan from whichever visible Value editor currently has focus.
5. New Scan restores a valid First Scan selection and unlocks Value Type/options as before.

## 5. Fuzzy Value Core semantics

Use the Mock target because its deterministic Health value is Float32 `100.0` at `0x10000100`.

1. Select **Float**, **Fuzzy Value**, enter `100.5`, and First Scan.
2. Confirm `0x10000100` is retained because `|100.0 - 100.5| < 1.0`.
3. New Scan, use Fuzzy Value `101.0`, and confirm the exact `100.0` value is not retained because the boundary is strict: a difference of exactly `1.0` does not match.
4. Repeat a representative test with **Double** if convenient.
5. Confirm Fuzzy Value is not available for integer or Array of Bytes Value Types.

## 6. Unknown Initial Value semantics

Core defines Unknown Initial Value as a true fixed-width snapshot: **zero-valued candidates are included**.

### Mock/Core path

1. Start a fresh **4 Bytes -> Unknown Initial Value** scan on Mock.
2. Confirm the scan completes without a Value operand.
3. Choose a known candidate/address whose initial 4-byte value is zero, if practical, and confirm it is not automatically excluded from the committed generation.
4. Change a known retained value, run **Changed Value**, and confirm it survives.
5. Run another Changed/Unchanged cycle and confirm comparison is against the immediately previous committed generation, not permanently against the first scan.

### PS5/native snapshot path

1. Connect a PS5 running a current ps5debug-NG build that advertises TurboScan snapshot support.
2. Select a fixed-width numeric Value Type and **Unknown Initial Value**.
3. Start First Scan and confirm it completes through the native snapshot path rather than failing because no comparison operand exists.
4. Confirm the snapshot includes zeros. The automated protocol test verifies the required `TS_SNAPSHOT_INCLUDE_ZEROS` flag; for a live smoke test, use a known zero slot when one can be identified safely.
5. Change a retained value and run **Changed Value**. Confirm native refinement can continue from the resident snapshot and returns the expected survivor.
6. Confirm the PS5 connection remains usable after New Scan/Disconnect following a snapshot session.

A direct mapping from Core Unknown Initial Value to ps5debug-NG `compareType 11` is **not** acceptable because that comparator excludes zero. The plugin must use snapshot/include-zero mode or fall back to Core.

## 7. Unknown Initial Low Value semantics

Core defines Unknown Initial Low Value as a First Scan predicate:

```text
0 < current <= upperLimit
```

The upper bound is inclusive and must be positive.

### Mock/Core path

1. Select **4 Bytes -> Unknown Initial Low Value**.
2. Enter an upper limit above a known positive value and confirm that address survives.
3. Use an upper limit equal to the known value and confirm the equal boundary survives.
4. Use a lower upper limit and confirm the address is removed.
5. Confirm zero-valued candidates are excluded.

### Float/Double

1. Confirm Unknown Initial Low remains available for Float/Double in the Core UI.
2. Verify a positive nonzero floating value at or below the upper limit survives.
3. Verify zero and negative values do not satisfy the Core predicate.

### PS5 native/fallback boundary

- Integer Unknown Initial Low may use ps5debug-NG `compareType 12` natively where current option/endian restrictions allow it.
- Float/Double Unknown Initial Low must use Core fallback because ps5debug-NG compares absolute floating magnitude, which is not semantically equivalent to Core's positive-only definition.

Confirm both paths complete without a user-visible NotSupported error.

## 8. Previous-value predicates

Using Mock first, then repeat representative cases on PS5:

1. Create a First Scan that retains a known address.
2. Increase its value and run **Increased Value**; the address must survive.
3. Decrease it and run **Decreased Value**; the address must survive.
4. Change it to any different value and run **Changed Value**; the address must survive.
5. Leave it unchanged and run **Unchanged Value**; the address must survive.
6. Confirm the Scan Results **Previous** column reflects the preceding committed generation.
7. Repeat several Next Scans in sequence and confirm each comparison advances its baseline after a successful generation commit.

## 9. Increased By / Decreased By Core fallback semantics

These two predicates deliberately remain Core fallback on PS5 because ps5debug-NG target-width arithmetic can wrap at integer boundaries while Core uses directional, non-wrapping delta semantics.

1. Establish a known previous integer value.
2. Increase it by exactly `10`, run **Increased By = 10**, and confirm it survives.
3. Repeat with a non-matching delta and confirm it is filtered out.
4. Establish a new baseline, decrease by exactly `10`, run **Decreased By = 10**, and confirm it survives.
5. Verify the PS5 path completes successfully even though no native mapping exists.
6. If practical, test a value near an integer boundary and confirm Core does not reinterpret overflow/wrap as a valid directional delta.

## 10. Generic Core-to-plugin native mapping behavior

The mapping mechanism must be platform-neutral.

1. Confirm the Core/WPF Scan Type list is unchanged when switching between Mock and PS5: Core owns the predicates.
2. Confirm the PS5 plugin declares native equivalence through `INativeScanTypeMappingProvider`; Core/WPF must not contain ps5debug-NG compare ids.
3. Confirm a mapped stage/Value Type combination attempts native execution.
4. Confirm an unmapped combination routes directly to Core without first requiring a failing native request.
5. Confirm the Mock plugin, which targets API `2.0.0` and does not implement the new mapping provider, still loads and scans normally.
6. Confirm a missing optional mapping provider does not become a plugin-load requirement for compatible 2.x plugins.

The intended future-plugin rule is: a plugin maps only native operations that are genuinely equivalent to a Core predicate. Similar naming by itself is not sufficient.

## 11. PS5 native mapping matrix

Live-smoke the practical mapped paths where a stable target value is available.

| Core Scan Type | Expected PS5 route |
| --- | --- |
| Exact Value | Native where existing runtime/option rules allow it |
| Fuzzy Value | Native Float/Double where runtime/option rules allow it |
| Bigger Than | Native for semantically compatible numeric shapes |
| Smaller Than | Native for semantically compatible numeric shapes |
| Between | Native for semantically compatible numeric shapes |
| Unknown Initial Value | Native snapshot with include-zero semantics when snapshot capability exists |
| Unknown Initial Low Value | Native for integer shapes only; Float/Double Core fallback |
| Increased Value | Native Next Scan where mapped |
| Decreased Value | Native Next Scan where mapped |
| Changed Value | Native Next Scan where mapped |
| Unchanged Value | Native Next Scan where mapped |
| Increased By | Core fallback |
| Decreased By | Core fallback |

For every mapped scan used in this section, confirm New Scan and later normal target commands still work. A protocol framing problem must not be masked by a correct result count.

## 12. Endianness and floating-point routing

Rev26 makes native routing predicate-aware rather than assuming every mapped type is safe under every option combination.

1. With **Little Endian**, verify representative native magnitude predicates can run normally.
2. With **Big Endian** and a multi-byte integer magnitude predicate such as Bigger Than/Between, confirm the operation completes through Core fallback rather than using a little-endian native numeric comparison.
3. Confirm endian-independent integer equality/inequality operations may still use native routing when permitted.
4. Confirm Unknown Initial snapshot remains valid under Big Endian because it stores raw fixed-width slots rather than interpreting numeric magnitude during the initial snapshot.
5. With Float/Double Big Endian, confirm multi-byte native floating comparisons fall back to Core.
6. With Float/Double **Exact Value + Strict**, confirm the existing strict host filtering semantics remain intact.
7. With the explicit ps5debug-NG tolerance option, confirm the existing native tolerance behavior remains available for Exact Value.
8. Confirm **Fuzzy Value** keeps its own `< 1.0` predicate and is not accidentally passed through Exact Value's strict/tolerance post-filter logic.

## 13. Native capability refusal and fallback

Where practical, test with a ps5debug-NG build/runtime state that lacks a particular TurboScan engine or force a safe native resource refusal.

Expected behavior:

- unavailable native service/capability -> shared Core path;
- no semantic mapping -> shared Core path;
- native request rejects a supported-but-unavailable runtime shape with `NotSupportedException` -> established Core fallback;
- no stale unread bytes remain on the PS5 command connection after fallback;
- the next process refresh, memory read, scan, New Scan, or Disconnect remains usable.

If a missing-capability PS5 environment is not available, the automated protocol tests plus the normal live fallback cases are sufficient for this revision; record the unavailable scenario as not exercised rather than inventing a PASS.

## 14. Disk-backed complete-set regression

For a scan that produces more than the 50,000-row UI preview:

1. Commit a large First Scan generation.
2. Refine with a Core predicate that should retain a known address outside the visible 50,000 rows.
3. Confirm Next Scan evaluates the full disk-backed generation, not only the UI preview.
4. Cancel a Next Scan and confirm the previous committed generation remains authoritative.
5. Confirm previous-value predicates do not create a giant host-side dictionary/list proportional to the complete result set.

The existing disk record already carries the current value that becomes the next generation's previous-value baseline; rev26 must not replace that storage model.

## 15. Input validation regression from rev24/rev25

### Numeric Scan/Saved Address Value

1. With a numeric Value Type, press Space at the beginning, middle, and end of the field. No space may be inserted.
2. Paste text containing invalid whitespace and confirm it is rejected.
3. Confirm valid intermediate edit states such as `-`, `0x`, `1.`, or exponent fragments remain available only where the active Value Type policy permits them.
4. Confirm the final parser/range check still blocks incomplete or out-of-range values before Scan/Write.

### Saved Address Address

1. Confirm Space cannot be typed or pasted.
2. Confirm only hexadecimal digits plus the optional `0x`/`0X` prefix are accepted live.
3. Confirm the final 64-bit address parser remains authoritative.

### Array of Bytes

1. Enter `DE AD BE EF`.
2. Confirm spaces remain accepted as valid byte separators.
3. Confirm invalid non-byte characters are still rejected.

## 16. Saved Addresses and user-intent regressions

1. Confirm Saved Address refresh continues at the configured interval when idle.
2. Confirm Frozen writes still use the configured cadence.
3. Confirm direct Value write, Freeze enable/disable, Address/Type edit, Remove, and Remove All retain the rev20-rev22 user-intent/I/O coordination behavior.
4. Confirm a queued row removal still completes automatically after active Saved Address I/O becomes idle.
5. Confirm Disconnect registers on the first accepted click even if a Saved Address background tick is active.
6. Confirm the rev23 themed confirmation dialog still follows Light, Dimmed, and Dark themes.
7. Confirm PS5 Frozen writes during a non-paused scan can still use `IConcurrentMemoryWriter` as before.

## 17. Scan lifecycle and cleanup

1. Confirm First Scan locks Value Type and lock-after-first Scan Options.
2. Confirm Next Scan permits changing among valid Next Scan predicates.
3. Confirm New Scan ends any active native/resident PS5 scan session and restores First Scan state.
4. Confirm Disconnect tears down the active native scan session without hanging.
5. Confirm cancelling a scan does not corrupt the PS5 command stream.
6. Confirm closing the application with a connected target does not leave a visible stale scan session on restart.

## Completion criteria

Rev26 can be accepted when:

- the Windows build succeeds;
- all **39 automated checks** pass;
- rev24/rev25 whitespace/input behavior is confirmed;
- the 13 Core Scan Types behave according to their documented semantics;
- PS5 native mapping uses only semantically equivalent operations;
- Unknown Initial Value is live-smoked through the include-zero snapshot path on a compatible ps5debug-NG build;
- representative mapped native First/Next Scan paths work on a real PS5;
- deliberately unmapped or incompatible shapes fall back to Core cleanly;
- existing disk-backed scan, Saved Addresses, target I/O, and cancellation behavior remains intact.

Once the complete rev24-rev26 input/scanner feature block is verified, the current `0.1.3` development block may be treated as complete. The next feature block can then begin as **0.1.4.rev1** rather than continuing to increase the `0.1.3` revision number.

## Windows verification result - 2026-09-05

The first Windows build attempt of the packaged rev26 source **did not reach runtime verification**. The solution was blocked by four nullable warnings promoted to errors by `TreatWarningsAsErrors=true`:

- `CS8604` at the First Scan disk-backed execution call;
- `CS8604` at the First Scan in-memory execution call;
- `CS8604` at the Next Scan disk-backed execution call;
- `CS8604` at the Next Scan in-memory execution call.

All four diagnostics concern the same `inputValues` result from `TryPrepareScan(...)`. The method guarantees a non-null parsed list on its successful return path, but rev26 did not annotate that bool/out relationship for nullable-flow analysis. Because the App project therefore failed to build, Visual Studio also surfaced XAML designer errors for `System.Object`, `ProportionalGridSplitter`, and the `TextBoxInputFilter` attached properties. Those XAML files/types were not independently implicated; the host assembly could not be produced for the designer to resolve.

This compile blocker is corrected in **0.1.3.rev27 - Native Scan Mapping Compile Fix**. Rev26 must therefore not be marked verified; continue the checklist using rev27.

