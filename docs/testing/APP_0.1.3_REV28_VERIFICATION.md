# TeeKay87's Memory Engine 0.1.3.rev28 Verification

## Purpose

This checklist verifies **TeeKay87's Memory Engine 0.1.3.rev28 - Scan Type Selection and PS5 Scan Panel Refinement**.

Rev28 fixes the stale post-scan Scan Type selector state found during live rev27 testing and adds generic Plugin API presentation/applicability metadata for plugin-owned Scan Options. The PS5 plugin uses that metadata to render Endianness as a Little-endian checkbox and to show Floating-point rounding only when it is relevant to the selected Core Scan Type and Value Type. The Scan panel also places two-value operands side-by-side.

The Core 13-mode Scan Type semantics, disk-backed result format, Core-to-native mapping table, ps5debug-NG wire protocol, Saved Addresses behavior, and native fallback rules are not intentionally changed by this revision.

## Expected version boundary

Confirm after building:

- application: `0.1.3.rev28`;
- feature title: `Scan Type Selection and PS5 Scan Panel Refinement`;
- Plugin API: `2.8.0`;
- PS5 plugin: `0.1.0.rev19`, targeting Plugin API `2.8.0`;
- Mock plugin: `1.0.0.rev4`, targeting Plugin API `2.0.0`.

The Mock plugin version does not change because it does not use the new optional Scan Option presentation/applicability contracts.

## 1. Clean Windows build

1. Clean and rebuild the complete solution in Visual Studio.
2. Confirm there are no compiler errors or warnings promoted to errors.
3. Confirm `MainWindow.xaml` loads without unresolved binding/type diagnostics after the successful build.
4. Start the application and confirm the displayed host version is `0.1.3.rev28`.
5. Confirm both built-in plugins are discovered.

Expected result: the complete solution builds and starts normally.

## 2. Automated verification

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 40 checks passed.
```

The new check verifies the Plugin API 2.8 Scan Option presentation/applicability declarations used by the PS5 plugin, including checked/unchecked Endianness mapping and Exact-only Floating-point rounding applicability.

## 3. Post-scan Scan Type selector regression

This is the direct regression for the issue reported after rev27.

1. Connect to the PS5 and set the desired Active Target.
2. Select a numeric Value Type such as **4 Bytes**.
3. Select **Between**.
4. Enter valid lower and upper values and run **First Scan**.
5. While the scan is running, confirm Scan Type is disabled.
6. When First Scan completes, click the Scan Type selector once.
7. Confirm it opens immediately on the first click.
8. Select a different Next Scan predicate, for example **Changed Value**, **Exact Value**, **Bigger Than**, or **Between**.
9. Run Next Scan.
10. When Next Scan completes, confirm the selector again becomes interactive immediately.

Expected result: `IsScanningMemory=true` disables the selector only for the active scan. Returning to idle explicitly refreshes `CanSelectScanType`, so no keyboard input or second click is required to wake WPF state.

## 4. Core stage filtering remains correct

After a successful First Scan:

1. Open Scan Type.
2. Confirm First-only predicates such as **Unknown Initial Value** and **Unknown Initial Low Value** are not offered for Next Scan.
3. Confirm valid Next Scan predicates for the selected Value Type are offered.
4. Choose **New Scan**.
5. Confirm First Scan predicates return and Next-only predicates are removed until another scan session exists.

Expected result: rev28 changes selector notification, not the Core stage/capability filtering rules.

## 5. Multi-value operand layout

1. Start a New Scan with a numeric Value Type.
2. Select **Between**.
3. Confirm **Value 1** and **Value 2** are displayed side-by-side on the same row with equal available width and the normal gap between them.
4. Resize the main window down to the supported minimum width and confirm both fields remain usable without horizontal scrolling.
5. Select a one-operand Scan Type such as **Exact Value**.
6. Confirm the single **Value** editor expands to the normal full Scan-panel width.
7. Select a zero-operand Scan Type such as **Unknown Initial Value**.
8. Confirm the operand row disappears.

Expected result: operand count drives the layout without changing parsing or Scan Type semantics.

## 6. PS5 Endianness toggle presentation

Use a multi-byte Value Type such as **4 Bytes**.

1. Confirm Endianness is no longer rendered as a ComboBox.
2. Confirm a checkbox labeled **Little-endian byte order** appears in the checkbox group next to/above **Pause target while scanning**.
3. Confirm it is checked by default.
4. With the checkbox checked, run a known Little Endian scan and confirm normal behavior.
5. Start a New Scan, uncheck **Little-endian byte order**, and confirm the resulting scan uses the existing Big Endian choice semantics.
6. Confirm the checkbox maps back to the existing stable option choices rather than introducing new endianness ids.
7. Select **1 Byte** or **1 Byte (Unsigned)** before First Scan and confirm the Endianness checkbox is hidden because byte order is irrelevant.
8. Select **Array of Bytes** and confirm Endianness is hidden.
9. Return to a multi-byte Value Type and confirm the checkbox becomes visible again.
10. After First Scan, confirm the checkbox is disabled until **New Scan**, matching the existing session-lock behavior for Endianness.

Expected result: checked=`little`, unchecked=`big`, default checked; only presentation changed.

## 7. PS5 Floating-point rounding visibility and editability

1. Start a New Scan.
2. Select **Float** or **Double**.
3. Select **Exact Value**.
4. Confirm **Floating-point rounding** is visible and defaults to **Strict**.
5. Change Scan Type to **Fuzzy Value** and confirm Floating-point rounding disappears.
6. Select **Between** and confirm it remains hidden.
7. Select **Exact Value** again and confirm it reappears with its previous selection preserved.
8. Change Value Type to **4 Bytes** while still before First Scan and confirm Floating-point rounding is hidden even for Exact Value.
9. Return to Float/Double + Exact Value and run First Scan.
10. After First Scan, change to another Next Scan predicate and confirm the option hides.
11. Change back to **Exact Value** and confirm Floating-point rounding appears **enabled**, not locked by the existing scan session.
12. Switch between **Strict** and **ps5debug-NG tolerance (1e-6)** and run representative Exact Value Next Scans.

Expected result: visibility requires both Float/Double and Core Exact Value. The option remains configurable between scans because it affects the current comparison only; Endianness and Alignment remain session-locked.

## 8. Generic Plugin API behavior

The host must remain platform-neutral.

1. Confirm the Mock plugin still loads while targeting API `2.0.0` and renders no Scan Options.
2. Confirm no PS5 plugin id, platform name, ps5debug-NG compare id, or Endianness-specific branch was added to `MainWindow.xaml` or `PluginViewModel` to select the new presentation.
3. Confirm an option without `IMemoryScanOptionPresentation` still uses the normal choice-list editor.
4. Confirm an option without `IMemoryScanOptionApplicability` remains applicable to all Core Scan Types permitted by its existing Value Type rule.
5. Confirm malformed toggle metadata is rejected by plugin discovery rather than failing later in WPF.

Expected result: future plugins can use the same optional contracts without modifying host UI code.

## 9. Scan semantics and native mapping regression

Run representative scans from the rev26/rev27 checklist:

- Exact Value;
- Between;
- Changed/Unchanged;
- Unknown Initial Value;
- at least one PS5 native-mapped predicate;
- at least one deliberate Core-fallback predicate such as Increased By or Decreased By.

Expected result: result membership and native/Core fallback decisions remain unchanged. Rev28 does not alter Core comparison code, native mapping definitions, disk-backed records, or ps5debug-NG request framing.

## 10. Theme and layout regression

Repeat the Scan panel UI checks under **Light**, **Dimmed**, and **Dark** themes:

- choice-list controls remain themed;
- Little-endian and Pause-target checkboxes use the shared theme styles;
- hidden option rows leave no visible empty control surface;
- Between inputs remain aligned side-by-side;
- the existing vertical Scan-panel scrollbar still exposes all controls at reduced height.

## Static package-preparation result

The rev28 source tree completed the pre-package static release audit on 2026-09-05.

- 228 files are present in the complete source tree.
- All 128 C# files passed delimiter/string/comment structural checks.
- All 14 XAML/project XML files parse successfully.
- All 3 JSON files parse successfully.
- All relative Markdown links across 81 Markdown files resolve.
- The verification runner registers 40 checks.
- Host metadata resolves to `0.1.3.rev28`, Plugin API to `2.8.0`, PS5 plugin to `0.1.0.rev19` / API `2.8.0`, and Mock plugin remains `1.0.0.rev4` / API `2.0.0`.
- No `bin`, `obj`, or `.vs` build artifacts are present.
- Diff review against the supplied rev27 baseline found no removed files. The Core scanner implementation, PS5 transport/native mapping/native scanner/native refiner, and Mock plugin production tree remain byte-identical to rev27.
- Host WPF/ViewModel source contains no ps5debug-NG command constants, TurboScan flags, native compare ids, or PS5-specific scan-option branches; the new UI behavior is driven through the generic Plugin API contracts.
- The new PluginHost toggle-metadata validation was reviewed specifically for nullable warnings under the repository-wide `TreatWarningsAsErrors=true` setting.

Per project workflow, this is a static source/package-preparation audit only. The Windows .NET build, the 40-check executable verification suite, UI behavior, and live PS5 tests below remain user-run acceptance steps.

## Completion criteria

Rev28 can be accepted when:

- the Windows solution builds cleanly;
- all 40 automated checks pass;
- Scan Type re-enables immediately after First and Next Scan;
- Between operands render side-by-side;
- PS5 Endianness is a checked-by-default Little-endian checkbox with unchecked=Big Endian;
- Floating-point rounding appears only for Exact Value + Float/Double and is configurable before later Exact Next Scans;
- Endianness and Alignment still lock after First Scan;
- representative native and Core-fallback scans retain rev26/rev27 semantics;
- the Mock/older-API compatibility path remains intact.

## User-run verification result — 2026-09-05

Rev28 was exercised on Windows against the live PS5 environment after the static package audit.

### Passed

- The automated verification executable completed with **40 / 40 checks passed**.
- The post-scan Scan Type selector regression passed: after a Between First Scan, Scan Type became selectable immediately and the user could move between zero-, one-, and two-operand predicates without restarting the session.
- Dynamic operand presentation passed, including side-by-side Between inputs.
- Scan-option session behavior passed: Value Type, Endianness, and Alignment remained locked after First Scan while Pause Target and applicable Floating-point rounding behavior remained usable as designed.
- Representative live Float scans across Exact/ordered/Between predicates passed.
- Previous-value refinement passed for Increased, Decreased, Changed, Unchanged, Increased By, and Decreased By.
- Native-mapped and deliberate Core-fallback PS5 paths both completed successfully.
- A large Unknown Initial Value run was able to produce roughly 600,000,000 candidates and continue into the disk-backed result path.

### Regressions found

Two issues prevented rev28 from being accepted as the completed scanner revision:

1. **New Scan could leave Scan Type visually blank.** When New Scan rebuilt the First Scan type collection, the WPF ComboBox could clear its selection instead of visibly returning to Core's default Exact Value.
2. **Large native Unknown Initial snapshots could fail mid-response.** Some Float Unknown Initial Value scans failed with a message beginning `First Scan failed. ps5debug-NG TurboScan snapshot returned ...`. The failure was not determined by candidate count alone: another scan of the same type/value type with roughly 600 million candidates reached disk storage successfully. After the failing case, later PS5 commands were unreliable until Disconnect/Connect.

The second issue was traced to a host-side protocol assumption rather than a Core result-count limit. Rev28 stopped reading the TurboScan snapshot progress stream after more than 1,024 progress records. ps5debug-NG defines that sequence by a terminating `0xFFFFFFFFFFFFFFFF` sentinel and can legitimately emit more than 1,024 records when a large snapshot uses smaller target-side fallback I/O windows. Throwing before the sentinel left the remaining progress records, summary, and final status unread on the shared TCP command stream, which explains the required reconnect.

### Acceptance status

**Rev28 is not fully accepted.** Its successful automated/live results remain the regression baseline, but the two issues above are addressed by `0.1.3.rev29` and must be reverified there before the `0.1.3` scanner feature block can be considered complete.
