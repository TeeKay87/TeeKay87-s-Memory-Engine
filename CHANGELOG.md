# Changelog

## TeeKay87's Memory Engine 0.1.3.rev1 - PS5 Value Type Expansion

### Added

- Expanded the Exact Value scanner to the complete ps5debug-NG scan value-type set instead of exposing only signed Int32. The supported types are `UInt8`, `Int8`, `UInt16`, `Int16`, `UInt32`, `Int32`, `UInt64`, `Int64`, `Float`, `Double`, and `Array of Bytes`, corresponding to ps5debug-NG wire value-type ids `0` through `10`.
- Added shared Core `MemoryScanValue` and `MemoryScanValueCodec` models so scan values now carry their value type, encoded bytes, display text, width, and alignment instead of assuming a single `int` throughout the scanner.
- Added target-endian parsing and encoding for all integer and floating-point scan types. Signed and unsigned integer types accept decimal values and `0x`-prefixed hexadecimal input; Float and Double use invariant decimal notation.
- Added exact Array of Bytes input using hexadecimal byte sequences such as `DE AD BE EF`, compact forms such as `DEADBEEF`, and comma/hyphen/space separators. The current resident TurboScan boundary is 4,096 bytes. This revision uses an all-ones ps5debug-NG mask, so wildcard/masked AOB syntax is intentionally not exposed yet.
- Added a functional Value Type selector to the Scan panel. It is available before First Scan and locked after a scan session begins so Next Scan cannot silently change the active type. New Scan unlocks the selector again.
- Added value-type-aware Scan Results display for current value, previous value, and the Type column.
- Added PS5 TurboScan wire mapping for all eleven ps5debug-NG value types, dynamic START/COUNT value widths, Array of Bytes mask transmission, dynamic GET record parsing, and payload-bounded GET batching for large byte-array values.
- Added shared-Core regression coverage across the complete ps5debug-NG value-type set and PS5 protocol coverage that exercises all wire value-type ids, including Array of Bytes mask framing. The verification executable now contains 19 checks.

### Changed

- Advanced the host application from the completed/verified `0.1.2` milestone to `0.1.3.rev1`.
- Updated the PS5 plugin from `0.1.0.rev8` to `0.1.0.rev9`. Plugin API remains `1.2.0` because all required `MemoryValueType` values and the generic `NativeValueScanRequest` contract already existed; no public Plugin SDK contract change was required.
- Generalized the shared Core First Scan and Next Scan implementations from Int32-specific matching to type/width/alignment-aware Exact Value matching while retaining the existing Int32 methods as compatibility wrappers.
- Generalized PS5 TurboScan segment construction so the full value width is respected for every selected type. Large protocol segments are partitioned by candidate-start range so multi-byte Array of Bytes values are not lost at artificial `uint32` segment boundaries.
- Float and Double continue to use native TurboScan First Scan, but PS5 resident Next Scan refinement intentionally falls back to shared Core. Current ps5debug-NG uses fuzzy floating-point equality in resident refinement, so using it for Memory Engine's Exact Value mode would change the requested comparison semantics. Integer and exact Array-of-Bytes refinement remain native.
- PS5 TurboScan GET now derives record size from the resident value width rather than assuming the former 16-byte Int32 record. Batch size is also bounded by a 4 MiB payload target so large Array of Bytes values cannot create oversized temporary buffers.
- Scan status text and Value-field help now identify the selected Value Type instead of describing every scan as a four-byte signed integer scan.
- Scan Results -> **Change value** remains connected to the temporary Raw Memory Write diagnostic only for signed Int32 results. The command is disabled for other result types because Raw Memory Write remains intentionally limited to one signed Int32 value and must not reinterpret wider, floating-point, unsigned, or byte-array results incorrectly.

### Fixed

- Removed the hard-coded four-byte assumptions from native result validation, native refinement request framing, result display, and shared scanner matching.
- Preserved exact candidate coverage for values wider than their scan alignment, including Array of Bytes scans with one-byte candidate alignment.
- Prevented Value Type changes during an active scan session, avoiding native-session/type-width mismatches between First Scan and Next Scan.

### Preserved

- The fully verified `0.1.2.rev5` scan workflow remains intact: Enter in Value runs the highlighted First/Next action, scan-button emphasis follows session state, New Scan resets the workflow, Danger buttons remain theme-controlled, and the PS5 plugin still prefers `eboot.bin` without automatically changing Active Target.
- PS5 TurboScan First Scan acceleration, resident TurboScan Next Scan refinement, process pause/resume, safe deferred cancellation, raw memory access, Safe Write Test, theme behavior, process selection, plugin discovery, and Mock functionality remain intact.
- Exact Value remains the only scan comparison mode in this milestone. Unknown Initial Value, Changed/Unchanged, Increased/Decreased, range comparisons, and other comparative modes remain future work.
- Raw Memory Write remains the verified signed Int32 diagnostic; this revision does not broaden write semantics as a side effect of broadening scan value types.

### Verification Scope

- `0.1.2.rev5` was completed before this version transition: the Windows verification executable passed all 17 checks and the live PS5/UI workflow was reported working as intended, establishing `0.1.2` as the verified baseline for `0.1.3`.
- Source preparation for this revision verifies application/plugin version boundaries, the eleven ps5debug-NG value-type mappings, generic scanner/parser wiring, Array of Bytes mask framing, Value Type selector bindings, XAML/JSON/project-file syntax, and release-package integrity.
- Native Windows compilation and the 19-check verification executable must be run on the Windows development machine because the source-preparation environment does not contain the .NET SDK/WPF toolchain. The PS5 value-type protocol coverage also verifies that Float/Double resident refinement requests a safe Core fallback instead of using ps5debug-NG's fuzzy floating-point refinement semantics.
- Live PS5 verification should exercise representative signed/unsigned integer widths, Float, Double, and Array of Bytes through First Scan and Next Scan while confirming the established rev5 workflow remains unchanged.

## TeeKay87's Memory Engine 0.1.2.rev5 - Scanner Workflow and Turbo Refinement

### Added

- Added the optional Plugin SDK `INativeValueScanRefiner` contract for target-side refinement of an existing native value-scan result set. The host Plugin API is now `1.2.0`; existing `1.0.x`/`1.1.x` plugins remain compatible under the established same-major/older-minor rule.
- Added PS5 TurboScan resident Next Scan refinement through `CMD_PROC_TURBOSCAN_COUNT` (`0xBDAACC12`) and `CMD_PROC_TURBOSCAN_GET` (`0xBDAACC13`). A successful PS5 First Scan now keeps its server-resident survivor set available for subsequent Exact Value narrowing instead of ending the TurboScan session immediately.
- Added optional use of ps5debug-NG `TS_RESCAN_ALIASING` when the connected server advertises the corresponding rescan-aliasing engine bit. The normal resident refinement path remains valid when that optional engine is absent.
- Added a generic preferred-process selection step through the existing `IForegroundProcessProvider` contract. The PS5 plugin now advertises `ForegroundProcess` and returns `eboot.bin` when that process exists, allowing the host to select it automatically in **Target Process** without making it the Active Target.
- Added a scan submit command used by the Value field keyboard workflow. Pressing Enter in the Scan **Value** field now runs First Scan when no scan session exists and Next Scan after a successful First Scan.
- Added explicit scan-action presentation state so the theme-primary color follows the next logical scan action: First Scan before a session exists, then Next Scan after First Scan succeeds. **New Scan** returns the primary action to First Scan.
- Added deterministic protocol coverage for PS5 resident TurboScan refinement and preferred `eboot.bin` process discovery. The verification executable now contains 17 checks.

### Changed

- Updated the PS5 plugin from `0.1.0.rev7` to `0.1.0.rev8` and its target Plugin API from `1.1.0` to `1.2.0`.
- PS5 First Scan still uses the rev4 multi-segment server-resident TurboScan path, but the resident session is now intentionally retained after a successful scan so the next Exact Value scan can narrow it directly on the target.
- PS5 Next Scan now prefers `INativeValueScanRefiner` when available. The previous shared Core/read-based Next Scan implementation remains unchanged and is used automatically when no compatible native refinement session exists or the native session can no longer be used safely.
- Native PS5 First Scan and native PS5 Next Scan use indeterminate progress presentation because the current resident Exact Value list path does not provide a meaningful percentage stream. Shared Core fallback scans continue to display determinate percentage progress.
- **New Scan** now releases any active native resident scan session before clearing host-side candidates. Changing the Active Target also releases an available native resident scan session before switching targets.
- **Cancel Scan** now reports `Cancellation requested — waiting for the current target scan operation to finish safely...` immediately after the request. This reflects the ps5debug-NG framing requirement that an already-started streamed transaction must reach a clean protocol boundary before cancellation can return.
- `Disconnect` and `Cancel Scan` now use the existing theme-owned `DangerButtonStyle`. The bundled theme danger palettes remain the standard red stop/cancel treatment and can be independently recolored by custom themes.
- The Scan **Value** tooltip now documents the Enter-key workflow.

### Fixed

- Fixed scan-action emphasis so First Scan and Next Scan are no longer both shown as primary actions at the same time.
- Fixed native scan-session lifetime management so successful First Scan results can be refined natively, while cancelled/failed/mismatched native operations close the resident session before returning to the host or falling back to Core.
- Fixed the PS5 memory-write verification documentation's stale "current plugin" reference so current-version instructions no longer identify `0.1.0.rev6` as the current PS5 plugin.

### Preserved

- The shared Core 4-byte Int32 Exact Value First Scan and Next Scan algorithms remain available and unchanged as the compatibility fallback.
- Rev4 PS5 First Scan acceleration, transaction-safe cancellation, process pause/resume, raw read/write, Safe Write Test, Scan Results context-menu behavior, themes, plugin discovery, and Mock functionality remain intact.
- **Pause target while scanning** remains Off by default and continues to resume the Active Target from the scan cleanup path after completion, cancellation, or ordinary failure.

### Verification Scope

- Source preparation verifies version/API consistency, the new contract/service wiring, TurboScan COUNT/GET framing, preferred-process capability wiring, XAML/JSON/XML validity, and release-package integrity.
- Native Windows compilation and the 17-check verification executable must be run on the Windows development machine because the source-preparation environment does not contain the .NET SDK/WPF toolchain.
- Live PS5 verification should confirm Enter-driven scan flow, dynamic scan-button emphasis, automatic `eboot.bin` Target Process selection, theme-aware danger buttons, resident TurboScan Next Scan timing/correctness, fallback behavior, and safe deferred cancellation.

## TeeKay87's Memory Engine 0.1.2.rev4 - PS5 Scan Acceleration and Process Pause

### Added

- Added the public Plugin SDK `INativeValueScanner` service contract for optional target/plugin-side value-scan acceleration. The contract receives the neutral readable memory-region set selected by Core so platform implementations can preserve the same scan boundaries as the shared scanner.
- Added the public Plugin SDK `IProcessControl` service contract for neutral process suspend/resume operations.
- Added PS5 TurboScan capability probing through `0xBDAACC10`. The connected PS5 session exposes `INativeValueScanner` only when the server advertises TurboScan protocol version 1 or newer together with server-resident result storage and multi-segment support.
- Added ps5debug-NG scan authorization through `CMD_PROC_AUTH` (`0xBDAACCFF`) using scan flag `0x02` and the documented challenge/XOR-keystream handshake.
- Added PS5 accelerated First Scan through TurboScan START/GET/END (`0xBDAACC11`, `0xBDAACC13`, `0xBDAACC14`). Readable neutral memory regions are sent as disjoint segments and matches remain server-resident while the host retrieves only result records.
- Added PS5 `CMD_DEBUG_PROCESS_STOP` (`0xBDBB0500`) support with state `1` for suspend and state `0` for resume.
- Added capability-driven **Pause target while scanning** to the Scan panel. The option is Off by default, is shown only for plugins that advertise both `ProcessSuspend` and `ProcessResume`, and is locked while a scan is active.
- Added an indeterminate status-bar progress mode for target-side scans that do not expose incremental percentage progress while preserving live elapsed-time reporting.
- Added deterministic protocol verification for TurboScan capability negotiation, scan authorization, multi-segment resident exact-value scanning, GET result retrieval, END cleanup, cancellation-safe session reuse, and process suspend/resume.
- Added dedicated rev4 verification documentation and PS5 native scan/process-control documentation.

### Fixed

- Fixed the Scan Results context menu in Dimmed/Dark themes by replacing WPF's default `ContextMenu`, `MenuItem`, and `Separator` chrome. The operating-system light icon/checkmark gutter no longer appears beside menu items.
- Preserved command-stream synchronization when cancelling a PS5 accelerated scan. Once a TurboScan protocol transaction starts, its current response is drained to a defined command boundary and the resident scan session is closed with END before cancellation returns.
- Avoided using the legacy `CMD_PROC_SCAN` (`0xBDAA0009`) path for Memory Engine acceleration. Rev4 instead uses TurboScan's multi-segment server-resident protocol so the plugin can represent the host-selected disjoint memory regions directly and retrieve the corresponding absolute survivor addresses.

### Changed

- PS5 First Scan now prefers the plugin's `INativeValueScanner` acceleration path when TurboScan support is negotiated at connection time. The existing Core `IMemoryReader` scanner remains the platform-neutral fallback and continues to be used by Mock, older ps5debug-NG servers, and resource-fallback cases.
- Native PS5 First Scan now sends the neutral readable/non-guarded scan regions to the plugin. Core still owns scanner semantics and normalizes returned addresses into the same `MemoryScanResult` model, including alignment, containment, de-duplication, and the common result limit.
- Added automatic fallback to the shared Core First Scan when the PS5 native service reports that the current scan cannot use server-resident acceleration. The visible scan workflow does not change.
- Next Scan remains the shared Core candidate-refinement implementation. This preserves the already verified effectively-instant refinement behavior from the real-console rev3 test.
- Process pause is performed through `IProcessControl` and wrapped around both First Scan and Next Scan with a resume attempt in `finally` after success, cancellation, or failure. Resume failures are surfaced to the user.
- Plugin API advanced from `1.0.0` to `1.1.0` because rev4 adds two public optional service contracts. Existing `1.0.0` plugins remain compatible under the established same-major/older-minor compatibility rule.
- PS5 plugin revision advanced from `0.1.0.rev6` to `0.1.0.rev7` and now targets Plugin API `1.1.0`.
- Recorded completed rev3 runtime verification: all 13 automated checks passed; WPF startup/progress worked; Cancel -> Refresh/New Scan reused the same live PS5 session successfully; a real Int32 First Scan for `10002` returned 266 results from a 10,105-region target in approximately `07:04.3`; and the subsequent Next Scan completed effectively instantly. This measurement motivated the rev4 target-side First Scan acceleration.

### Removed

- Removed reliance on WPF's default context-menu icon/checkmark gutter and separator rendering.
- Removed the proposed legacy `CMD_PROC_SCAN` acceleration implementation before release of rev4; it was replaced by the negotiated TurboScan path during final protocol review.
- No previously verified target-access, scanner-refinement, raw-memory, theme, splitter, or workspace functionality was removed.

### Version and Compatibility Notes

```text
Host application:             0.1.2.rev4
PlayStation 5 plugin:         0.1.0.rev7
In-Memory Test Target plugin: 1.0.0.rev1
Plugin API:                   1.1.0
```

The Mock plugin intentionally remains at Plugin API `1.0.0`; this verifies that a host exposing Plugin API `1.1.0` continues to accept plugins built against an older minor version in the same major compatibility family.

### Verification

Source/static verification for the packaged revision includes project/XML/XAML parsing, theme JSON validation, documentation-link checks, version-boundary checks, public contract/service wiring review, protocol-fixture review, and package byte comparison. The preparation environment does not provide the .NET/WPF SDK, so the Windows build and executable verification must be performed on the development machine.

The verification executable contains 16 checks. A successful run ends with:

```text
All 16 checks passed.
```

Live PS5 verification must measure the accelerated First Scan against the rev3 `07:04.3` baseline, confirm that Next Scan remains fast, verify Cancel -> Refresh/New Scan without reconnecting, and test **Pause target while scanning** for normal completion and cancellation. See `docs/testing/APP_0.1.2_REV4_VERIFICATION.md`.


## TeeKay87's Memory Engine 0.1.2.rev3 - Scan Progress Binding Fix

### Fixed

- Fixed a WPF startup failure introduced with the rev2 status-bar scan-progress presentation. `ProgressBar.Value` was bound to the output-only `PluginViewModel.ScanProgressPercentage` property without an explicit binding mode, causing WPF to reject the binding as TwoWay/OneWayToSource against a property with no public setter.
- Changed the progress binding to explicit `Mode=OneWay`, matching the intended data flow: scanner/ViewModel code owns progress updates and the progress bar only displays them.
- Preserved the existing private setter on `ScanProgressPercentage`; the ViewModel encapsulation was not weakened merely to satisfy WPF binding behavior.

### Changed

- Advanced the host revision from `0.1.2.rev2` to `0.1.2.rev3` while remaining in the same initial scanner milestone.
- Updated `AppInfo.FeatureTitle` to `Scan Progress Binding Fix`.
- Updated README current-state/version text and UI/testing documentation to record the binding contract and the rev2 Windows runtime result.
- Added `docs/testing/APP_0.1.2_REV3_VERIFICATION.md` with startup, progress, regression, and deferred live-PS5 cancellation verification steps.

### Removed

- Removed no scanner feature, result action, status-bar feature, plugin capability, protocol command, theme behavior, workspace element, or target-access functionality.

### Version and Compatibility Notes

- Host application: `0.1.2.rev3`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev6`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`.
- Core scanner code, PS5 transport code, Plugin SDK contracts, and both plugin capability sets are unchanged from rev2.

### Verification

- Started from the complete `TK87ME_0.1.2.rev2___Scanner-Workflow-and-Cancellation-Reliability.zip` baseline produced from the user's rev1 codebase and reviewed README, CHANGELOG, all Markdown under `docs/`, and the complete source/project/theme/test tree before modifying code.
- Recorded the user's Windows result that the rev2 verification executable completed successfully with **All 13 checks passed**.
- Recorded the subsequent rev2 WPF startup failure: `System.InvalidOperationException` reported that a TwoWay or OneWayToSource binding cannot work on the read-only `ScanProgressPercentage` property.
- Confirmed the only source change required to correct that exception is the status-bar `ProgressBar.Value` binding in `MainWindow.xaml`; it now explicitly specifies `Mode=OneWay`.
- Confirmed `PluginViewModel.ScanProgressPercentage` remains output-only to the view through a private setter.
- Confirmed the previously corrected output-only Raw Memory Read and Raw Memory Write result TextBoxes remain explicitly OneWay-bound.
- Confirmed editable TextBox inputs remain TwoWay where required.
- The preparation environment does not provide a Windows WPF runtime, so native WPF startup is not claimed here. Windows rebuild, the existing 13-check verification executable, WPF startup, progress presentation, and the live PS5 Cancel -> Refresh/New Scan closure remain required runtime verification.

## TeeKay87's Memory Engine 0.1.2.rev2 - Scanner Workflow and Cancellation Reliability

### Added

- Added scan progress presentation to the application status bar. Active First Scan and Next Scan operations now expose a percentage-based progress bar derived from the shared Core `MemoryScanProgress` values and an elapsed-time counter updated while the scan is running.
- Added final elapsed-time retention after scan completion or cancellation so the duration remains visible until the scan session is reset with New Scan, Active Target replacement, disconnect, or equivalent scan-state cleanup.
- Added a Scan Results row context menu with **Copy address**, **Copy value**, and **Change value** actions. Copy operations place the rendered result address/value on the Windows clipboard. Change value transfers the selected result address to the temporary Raw Memory Write diagnostic without carrying over a potentially stale write value.
- Added application-owned `ContextMenu` and `MenuItem` styles so the new Scan Results menu follows the active Light/Dimmed/Dark palette instead of falling back to unrelated operating-system colors.
- Added deterministic PS5 regression coverage for scan cancellation while a `CMD_PROC_READ` transaction is in flight. The fixture cancels the shared Core scan after the read request has reached the loopback ps5debug-NG server, then immediately performs process enumeration on the same session and requires that command to succeed.
- Extended the ps5debug-NG test server with a controllable delayed memory-read response and a follow-up process-list transaction so command-stream synchronization can be verified without a physical console.
- Added `docs/testing/APP_0.1.2_REV2_VERIFICATION.md` with the Windows, deterministic, UI, and live-PS5 verification procedure for this revision.

### Fixed

- Fixed a live PS5 cancellation defect discovered during the first real-console scanner test. Cancelling a scan could previously cancel `NetworkStream.ReadExactlyAsync` in the middle of a ps5debug-NG memory-read response. Unconsumed response bytes then remained on the shared TCP stream, causing the next process-list/read/map command to interpret target memory bytes as a command status. The observed symptom was an unexpected status such as `0xC0F6410A`, and Disconnect -> Connect restored operation only because it created a new TCP session.
- Fixed PS5 read cancellation boundaries so a `CMD_PROC_READ` transaction is now treated as one framed command once it has started. Caller cancellation is checked before sending the request; after the command begins, the request, success status, and complete requested payload are consumed before control returns. The Core scanner then observes cancellation before issuing its next read request.
- Applied the same transaction-preservation rule to the already two-phase `CMD_PROC_WRITE` path: cancellation is checked before the command starts, then both success acknowledgements and the complete payload exchange are allowed to finish so a future caller cannot leave the PS5 command stream desynchronized.
- Fixed Scan Results address presentation so values no longer contain unnecessary fixed-width leading zeroes. Addresses now render in compact hexadecimal form such as `0x10000104` while retaining the `0x` prefix and full significant address value.

### Changed

- Advanced the host revision from `0.1.2.rev1` to `0.1.2.rev2` while remaining in the same initial scanner milestone.
- Updated `AppInfo.FeatureTitle` to `Scanner Workflow and Cancellation Reliability`.
- Updated the PlayStation 5 plugin independently from `0.1.0.rev5` to `0.1.0.rev6` because the PS5 command-transport implementation changed to preserve protocol framing across cancellation. The capability set is unchanged.
- Moved scan status and scan error presentation out of the right-side Scan panel and into the permanent application status bar. The Scan panel now remains focused on user inputs/actions while current scan state, progress, elapsed time, and failures are visible globally at the bottom of the window.
- Changed the temporary Raw Memory Write diagnostic from arbitrary hexadecimal-byte entry to one signed **4 Bytes / Int32** value entered in decimal form. The host converts the decimal Int32 to exactly four bytes according to `TargetArchitecture.Endianness` before invoking the unchanged neutral `IMemoryWriter` contract.
- Updated Safe Write Test presentation to populate the decimal write-value field by decoding the selected unchanged four-byte pattern using the active target's endianness. The actual safety behavior remains same-byte write-back and immediate byte-for-byte verification.
- Updated scanner result presentation so the Address column can use less horizontal space after removal of fixed-width leading zeroes.
- Updated README, scanner architecture documentation, main-workspace documentation, PS5 plugin documentation, protocol mapping, and verification documents for the current cancellation semantics, status-bar workflow, result actions, compact addresses, and decimal diagnostic writes.

### Removed

- Removed the old scan status/error text rows from beneath the Scan buttons; the same information is now surfaced in the application status bar.
- Removed the temporary Raw Memory Write UI's arbitrary hexadecimal-byte parser and its 4096-byte manual-write input limit. The underlying `IMemoryWriter` contract remains byte-oriented and unrestricted by this UI change; the current diagnostic surface deliberately edits one signed Int32 value.
- Removed no shared scanner algorithm, scan-result candidate, plugin capability, Plugin SDK contract, Mock behavior, verified PS5 command, theme, splitter, control-height rule, Raw Memory Read diagnostic, Safe Write Test behavior, or Saved Addresses separation.

### Version and Compatibility Notes

- Host application: `0.1.2.rev2`.
- PlayStation 5 plugin: `0.1.0.rev6`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`; no public Plugin SDK contract changed.
- The shared scanner remains in Core and continues to use `IMemoryReader`, `MemoryRegion`, `TargetProcess`, and `TargetArchitecture`. The PS5 cancellation fix is isolated inside the PS5 transport implementation and does not add a platform-specific scanner branch.
- Existing theme JSON schema is unchanged from rev1.

### Verification

- Started from the user-supplied `TK87ME_0.1.2.rev1___Initial-Memory-Scanner-Foundation(1).zip` codebase/current rev1 baseline and re-read README, CHANGELOG, all Markdown under `docs/`, and the complete source/project/theme/test tree before modifying code.
- Recorded rev1 runtime results supplied before this revision: the verification executable returned **All 12 checks passed**; Mock First Scan found `0x10000104 = 30`; Raw Memory Write changed that value to `25`; Next Scan retained the correct address with Current = 25 and Previous = 30; New Scan, Cancel Scan, and the updated theme/button/tooltip presentation were initially reported working in normal Mock/UI use.
- Recorded the first live PS5 scanner success: the shared 4-byte Exact Value scanner found a real in-game money value and the value could be changed successfully through the existing memory-access chain.
- Recorded the subsequently isolated live PS5 cancellation failure: after Cancel Scan, a new scan/process refresh could fail with a malformed/unexpected ps5debug-NG status until Disconnect -> Connect. This runtime observation is the regression specifically addressed by the transport change in this revision.
- Added a thirteenth deterministic verification check named **PS5 scan cancellation preserves command stream**. The test cancels while a memory-read response is intentionally delayed, then verifies `IProcessProvider.GetProcessesAsync` succeeds immediately on the same session.
- Confirmed the modified PS5 client checks cancellation before starting a read/write command and uses a non-cancellable transport token only for completion of the already-started framed command transaction.
- Confirmed the new Raw Memory Write decimal codec uses `TargetArchitecture.Endianness` rather than assuming little-endian globally.
- Confirmed Scan Results addresses retain their complete numeric value while omitting only redundant leading zeroes.
- Confirmed the context-menu **Change value** action transfers only the result address and clears the write-value field before user input.
- The preparation environment does not provide the .NET Windows/WPF SDK toolchain, so no native Windows build/runtime result is claimed for `0.1.2.rev2`. Windows **Build -> Rebuild Solution**, the verification executable (expected **All 13 checks passed**), and the live PS5 Cancel Scan -> immediate session-reuse procedure remain required before this revision is runtime-verified.

## TeeKay87's Memory Engine 0.1.2.rev1 - Initial Memory Scanner Foundation

### Added

- Added the first shared Core memory scanner under `src/TeeKay87.MemoryEngine.Core/Scanning/`. The scanner is platform-neutral and consumes the existing `TargetProcess`, `MemoryRegion`, `TargetArchitecture`, and `IMemoryReader` contracts rather than introducing PS5-specific scan behavior.
- Added the first usable scan combination: **4 Bytes / Int32 + Exact Value**. First Scan reads eligible target memory, decodes Int32 values using the active target's declared endianness, and retains matching four-byte-aligned addresses as temporary scan candidates.
- Added **Next Scan** exact-value refinement over the complete previous candidate set. Refinement reads only memory chunks that contain existing candidates, updates Current Value, preserves the preceding value as Previous Value, and retains only candidates matching the new exact Int32 value.
- Added **New Scan** to clear the temporary scan session without affecting the separate Saved Addresses workspace.
- Added **Cancel Scan** and scan-operation cancellation tokens. A cancelled First Scan does not publish a partial result set; a cancelled Next Scan preserves the previous complete candidates.
- Added scan progress/status reporting and scan error presentation to the permanent right-side Scan panel.
- Added bounded 256 KiB scanner reads, explicit four-byte alignment, readable/non-Guard region filtering, a 2,000,000-result safety limit, and a stop condition after 32 target read failures so a stale/disconnected target cannot cause unbounded repeated failing requests.
- Added Core scan models `MemoryScanResult`, `MemoryScanExecutionResult`, `MemoryScanProgress`, and `ScanResultLimitExceededException`. These remain in Core rather than Plugin SDK because ordinary scan algorithms/session state do not cross the plugin boundary.
- Added `ScanResultViewModel` and live Scan Results bindings for Address, Value, Previous, Type, and Region / Module. The WPF grid uses row/column virtualization and materializes at most the first 50,000 presentation rows while Core retains/refines the complete candidate set.
- Added deterministic verification coverage for the shared scanner using the Mock target: First Scan finds Ammo = 30, the test writes Ammo = 25 through the existing neutral `IMemoryWriter`, and Next Scan confirms the same address survives with Current = 25 and Previous = 30.
- Added `docs/architecture/MEMORY_SCANNER_FOUNDATION.md` documenting scanner ownership, chunking, alignment, endianness, candidate storage, failure handling, cancellation, and planned expansion.
- Added `docs/testing/APP_0.1.2_REV1_VERIFICATION.md` with Windows, Mock, live-PS5 scanner, theme, preservation, and pass-criteria procedures.
- Added independent theme palette entries for Primary button background/border/text and Secondary button background/border/text. Danger buttons continue to use their existing dedicated danger palette.
- Added theme palette entries for tooltip/hint background, border, and text.
- Added an application-owned implicit WPF `ToolTip` template so hover hints use theme resources instead of falling back to operating-system tooltip chrome.

### Fixed

- Fixed Dimmed/Dark hover hints appearing as bright operating-system white tooltips with very low-contrast text by routing tooltip background, border, and text through the active application theme.
- Fixed Primary action colors being coupled to the general `Accent` resource and Secondary button colors being coupled to generic panel/text resources. Themes can now tune button surfaces/text independently without recoloring unrelated UI.
- Adjusted the bundled Light theme's Primary button palette to a lighter cyan surface with high-contrast dark text so colored actions no longer appear excessively dark in Light mode.

### Changed

- Advanced the host semantic version from `0.1.1.rev17` to `0.1.2.rev1` because the complete `0.1.1` low-level target-access milestone is now live-verified and the project is beginning the shared scanner milestone.
- Updated `AppInfo.FeatureTitle` to `Initial Memory Scanner Foundation`.
- Connected the existing permanent Scan UI to real commands/state instead of placeholder disabled controls. The Value input is active for shared scanner-capable sessions; Scan Type and Value Type remain fixed/non-editable because Exact Value + 4 Bytes is the only implemented combination in this revision.
- Changed host operation coordination so Disconnect, process Refresh, Set Active Target, Raw Memory Read, Raw Memory Write, and Safe Write Test cannot overlap an active scan.
- Changed Active Target/process cleanup to clear temporary scan state when the target disappears or a different Active Target is selected.
- Updated README to describe the complete current scanner behavior, result limits, theme palette contract, and completed low-level PS5 verification rather than treating the Scan controls as placeholders.
- Updated `docs/ui/THEMES.md`, `docs/ui/BUTTON_STYLES.md`, and `docs/ui/MAIN_WORKSPACE.md` for independently themed buttons, themed tooltips/hints, and the functional scanner workspace.
- Updated rev17 and PS5 memory-write verification documentation with the real-console result supplied before this revision: the Safe Write Test was run twice against `eboot.bin` and returned `Verification: PASS` both times.

### Removed

- Removed the disabled-placeholder behavior from the initial Value / First Scan / Next Scan / New Scan workflow now that the first scanner implementation exists.
- Removed no Plugin SDK contract, Core plugin-host behavior, PS5 transport behavior, Mock target behavior, raw read/write diagnostic, Safe Write Test, theme id, splitter behavior, Saved Addresses separation, or standard 34-unit control-height rule.
- Removed no platform scanner capability claim because the generic Core scanner does not require plugins to advertise `NativeValueScanning`; PS5 continues to advertise only the target-access capabilities it actually implements.

### Version and Compatibility Notes

- Host application: `0.1.2.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev5`; no PS5 plugin code or protocol mapping changes are required for the generic scanner.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`; the scanner reuses existing public memory/process/architecture contracts and stays in Core.
- Existing external custom themes based on the earlier palette schema must add the new Primary/Secondary button and ToolTip palette keys to load as complete themes under this revision. Bundled Light, Dimmed, and Dark files have all been updated together.

### Verification

- Started from the exact user-supplied complete `TK87ME_0.1.1.rev17___Automatic-Safe-Write-Test(1).zip` baseline.
- Re-read README, CHANGELOG, every Markdown file under `docs/`, and reviewed the complete C#/XAML/project/theme/test source tree before modifying code.
- Recorded the supplied real-PS5 rev17 evidence: 9,301 memory regions loaded for `eboot.bin`; Safe Write Test selected `0xA069FFC`; Original/Requested/Read-back were `00 00 00 00`; `Verification: PASS`; the test was repeated twice successfully.
- Confirmed the scanner implementation lives in Core and references no PS5 project/type/command.
- Confirmed PS5 plugin version/capabilities and Plugin API remain unchanged.
- Confirmed scanner decoding uses `TargetArchitecture.Endianness` and the generic host requires `MemoryRead + MemoryRegionEnumeration` rather than platform identity.
- Confirmed theme JSON, XAML resources, MainWindow XAML, and project XML parse successfully after the palette/template changes.
- Added deterministic Mock scanner coverage to the existing verification executable.
- The preparation environment does not provide the .NET Windows/WPF SDK toolchain, so no native Windows build or runtime result is claimed for `0.1.2.rev1`. Windows **Build -> Rebuild Solution**, the verification executable, and live PS5 scanner/theme verification remain required before this revision is considered runtime-verified.

## TeeKay87's Memory Engine 0.1.1.rev17 - Automatic Safe Write Test

### Added

- Added an explicit **Safe Write Test** action to the temporary Raw Memory Write diagnostic area. The action is available only when the active plugin/session provides memory-map enumeration, memory reads, and memory writes.
- Added generic host-side automatic candidate selection using the already cached neutral `MemoryRegion` list. Candidate regions must be readable and writable and must not be executable or guarded.
- Added a conservative four-byte stability check before any automatic test write. The host samples an interior address repeatedly with short delays and rejects candidates whose bytes change between reads.
- Added a final pre-write read and equality check immediately before the write. If the candidate changed after the stability samples, the test aborts without invoking `IMemoryWriter`.
- Added same-byte write-back verification for the automatic test. The bytes read immediately before the write are written back unchanged, then read again and compared byte-for-byte. This verifies the live write transport without intentionally changing the target value.
- Added automatic population of the Raw Memory Write Address and Bytes fields with the selected test address and bytes so the exact diagnostic operation remains visible to the user.
- Added `docs/testing/APP_0.1.1_REV17_VERIFICATION.md` documenting the purpose, safety boundaries, version boundaries, preservation requirements, and Windows/live-target verification procedure for this host-only diagnostic helper.

### Fixed

- Fixed the rev16 live-verification usability gap where the user was instructed to choose a known safe readable/writable address even though the current UI does not expose the memory-region list or otherwise provide a practical way to identify such an address.

### Changed

- Updated the host application revision from `0.1.1.rev16` to `0.1.1.rev17`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Automatic Safe Write Test`.
- Extended the Raw Memory Write diagnostic row with a secondary **Safe Write Test** button while preserving the existing manual **Write + Verify** workflow.
- Updated README and PS5 write-verification documentation so the preferred real-console protocol verification no longer requires manual address discovery.
- Updated the rev16 verification document with a forward note directing rev17-or-newer runtime testing to the automatic Safe Write Test while preserving rev16 historical implementation status.
- Updated host memory-command state notifications so the new Safe Write Test command follows the same connection, Active Target, memory-map, read/write-busy, refresh, and disconnect coordination as the existing raw-memory commands.

### Removed

- Removed no application feature, Plugin SDK contract, Core behavior, PS5 transport behavior, Mock-plugin behavior, theme, splitter behavior, shared control-height rule, TextBox padding fix, scanner placeholder, Saved Addresses placeholder, or existing manual Raw Memory Read/Write functionality.
- No automatic write occurs when a target is selected. The diagnostic write is still performed only after the user explicitly invokes **Safe Write Test**.

### Version and Compatibility Notes

- Host application: `0.1.1.rev17`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev5`; rev17 does not modify the ps5debug-NG backend or plugin capability set.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`; the implementation reuses the existing neutral `IMemoryMapProvider`, `IMemoryReader`, `IMemoryWriter`, `MemoryRegion`, and `MemoryProtection` contracts.
- The diagnostic helper is platform-neutral. Any future plugin exposing the required neutral capabilities can use the same host action without platform-specific UI branches.

### Verification

- Started from the exact user-supplied complete `0.1.1.rev16 - PS5 Raw Memory Write and Read-Back` ZIP.
- Re-read `README.md`, `CHANGELOG.md`, all 36 Markdown documents under the project/root documentation tree, and reviewed the complete C#/XAML/project/solution/theme source tree before modifying code.
- Confirmed the existing rev16 PS5 write implementation remains isolated in the PS5 plugin and that no new backend protocol work, Plugin SDK contract, or Core abstraction is required for automatic test-address selection.
- Confirmed the Safe Write Test filters on neutral protection flags only: `Read` and `Write` are required, while `Execute` and `Guard` are rejected.
- Confirmed the automatic test writes exactly the bytes observed immediately before the write; it does not deliberately mutate the target value and aborts before `IMemoryWriter` if the candidate becomes unstable.
- Confirmed the existing manual **Write + Verify** implementation remains available and unchanged in behavior.
- The preparation environment does not provide the .NET Windows/WPF SDK toolchain, so no native Windows build result is claimed. **Build -> Rebuild Solution**, the existing verification executable, and the real-PS5 Safe Write Test remain required on the Windows development machine.

## TeeKay87's Memory Engine 0.1.1.rev16 - PS5 Raw Memory Write and Read-Back

### Added

- Added PlayStation 5 raw process-memory writes through ps5debug-NG `CMD_PROC_WRITE` (`0xBDAA0003`). The PS5 command client serializes the documented packed 16-byte PID/address/length request, waits for the server's first success acknowledgement, sends exactly the requested raw bytes, and consumes the second completion status before returning.
- Added `IMemoryWriter` to `Ps5TargetSession` by reusing the public Plugin SDK contract that has existed since the architecture foundation; no PS5-specific write interface or wire model was added to the shared SDK.
- Added the `MemoryWrite` capability to the PlayStation 5 plugin now that the corresponding session service and backend protocol implementation exist.
- Added a capability-driven **Raw Memory Write** inspector to the WPF target area. It accepts a hexadecimal address and raw hexadecimal byte input while retaining the established 34-unit height for ordinary single-line TextBoxes.
- Added generic host-side writable-range validation using cached neutral `MemoryRegion` objects and `MemoryProtection.Write`. For map-capable targets, a write is blocked before the plugin call unless its complete range is contained in a writable Active Target region.
- Added generic write/read-back verification. When the selected range is also readable, the host captures the original bytes through `IMemoryReader`, performs the write through `IMemoryWriter`, reads the same range back, compares it byte-for-byte, and displays the original/requested/read-back values with `PASS` or `FAIL`.
- Added bounded hexadecimal byte parsing for manual writes, supporting compact or commonly separated input with optional `0x` prefixes. The interactive write inspector is limited to 4096 bytes; this is a host diagnostic-UI limit and does not change the neutral `IMemoryWriter` contract.
- Added deterministic loopback coverage for `CMD_PROC_WRITE`, including the packed request fields, both ps5debug-NG success acknowledgements, exact write payload reception, command-stream synchronization, and immediate neutral `IMemoryReader` read-back of the bytes written through `IMemoryWriter`.
- Added `docs/plugins/PS5/MEMORY_WRITE_VERIFICATION.md` with protocol, deterministic, Windows, safe live-console write/read-back/restore, and pass-criteria documentation.
- Added `docs/testing/APP_0.1.1_REV16_VERIFICATION.md` documenting the rev16 version boundaries, preservation requirements, static release checks, and pending Windows/live PS5 closure steps.

### Fixed

- No previously reported defect is targeted by this revision. Rev16 resumes the planned backend milestone after the rev15 TextBox presentation correction was runtime-verified successfully.

### Changed

- Updated the host application revision from `0.1.1.rev15` to `0.1.1.rev16`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `PS5 Raw Memory Write and Read-Back`.
- Updated the PlayStation 5 plugin independently from `0.1.0.rev4` to `0.1.0.rev5`.
- Extended the PS5 plugin description and capability set from read-only target-memory access to implemented raw read/write access.
- Changed host command-state coordination so manual reads and writes cannot overlap each other, and Disconnect, process Refresh, and Set Active Target remain unavailable while a write is in progress. This protects the existing explicit Active Target workflow from changing underneath a write transaction.
- Changed Active Target/process/disconnect cleanup so Raw Memory Write input/result/error state is reset together with the existing process, map, and read state.
- Updated README, PS5 plugin documentation, ps5debug-NG protocol mapping, Plugin SDK architecture notes, early-development architecture status, and main-workspace documentation to reflect the new write layer.
- Updated rev15 test documentation with the successful Windows runtime result supplied before rev16 work began: the TextBox fix is visually verified while the 34-unit control-height foundation remains intact.

### Removed

- Removed raw memory write from the PS5 plugin's documented unimplemented capability list because it is now implemented.
- Removed no public Plugin SDK contract, Core feature, Mock-plugin behavior, theme, splitter behavior, control-height rule, rev15 TextBox fix, process-selection/Active Target separation, scanner placeholder, Saved Addresses placeholder, or previously verified PS5 connection/process/map/read functionality.

### Version and Compatibility Notes

- Host application: `0.1.1.rev16`.
- PlayStation 5 plugin: `0.1.0.rev5`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- Plugin API: unchanged at `1.0.0`; `MemoryWrite` and `IMemoryWriter` already existed in the public contract, so no SDK compatibility change is required.
- The PS5 plugin continues to advertise only implemented host-visible capabilities: `Connect`, `ProcessEnumeration`, `MemoryRegionEnumeration`, `MemoryRead`, and `MemoryWrite`.
- The next major subsystem remains the shared Core memory scanner after rev16's controlled live PS5 write/read-back/restore verification is completed successfully.

### Verification

- Started from the exact user-supplied complete `0.1.1.rev15 - TextBox Content Padding Fix` ZIP and reviewed its root README, CHANGELOG, all Markdown documentation under `docs/`, and the complete code/resource/project tree before modifying the revision.
- Rechecked the current public ps5debug-NG protocol documentation before implementation. `CMD_PROC_WRITE` is `0xBDAA0003`, uses a packed `{ uint32 pid; uint64 address; uint32 length; }` body, requires one success response before the client streams the write bytes, and requires a second success response after the server consumes them.
- Preserved the existing Plugin SDK `IMemoryWriter` contract and implemented the backend entirely inside the PS5 plugin plus generic host behavior.
- Extended the deterministic protocol fixture so an immediate `CMD_PROC_READ` follows the write on the same TCP stream. This verifies both the transmitted bytes and that the final write acknowledgement is consumed rather than being left to corrupt the next command.
- Recorded the supplied runtime evidence that rev15 TextBox presentation is PASS and that rev11/rev12 remain live-verified on the real PS5, including the demonstrated `eboot.bin` session with 8,750 loaded regions and a successful 64-byte read at `0x400000`.
- Completed **484/484 static release checks successfully**, covering version separation, protocol/write-state invariants, XAML/project/theme parsing, project-reference resolution, deterministic fixture integration, local Markdown links, intended-file diff boundaries, preservation of unrelated verified files, release cleanliness, and lightweight C# delimiter-balance checks.
- The release-preparation environment does not include the .NET Windows/WPF SDK toolchain. No native build result is claimed here; **Build -> Rebuild Solution**, execution of the 11-check verification project, and the controlled real-PS5 write/read-back/restore test remain required on the Windows development machine.

## TeeKay87's Memory Engine 0.1.1.rev15 - TextBox Content Padding Fix

### Added

- Added `docs/testing/APP_0.1.1_REV15_VERIFICATION.md` documenting the reported TextBox clipping defect, the shared-template root cause, the centralized correction, version boundaries, static checks, and required Windows runtime verification.
- Added `docs/testing/APP_0.1.1_REV11_REV12_LIVE_PS5_VERIFICATION.md` as the authoritative combined result for the completed real-console rev11/rev12 verification. The document records successful non-zero PS5 memory-map enumeration, default and repeated raw reads, host-side invalid-range rejection, Active Target/map retention through refresh, and disconnect cleanup.
- Added completed live-result sections to the existing rev11, rev12, rev14, PS5 memory-map, and PS5 raw-memory-read verification documents so the documentation no longer describes those checks as pending.

### Fixed

- Fixed vertically clipped text in standard single-line WPF `TextBox` controls without increasing the established 34-unit control height. The defect was visible in plugin-defined connection inputs and the Raw Memory Read Address/Length inputs.
- Reduced the shared single-line TextBox internal padding from `9,6` to `9,3`, preserving the existing horizontal spacing while restoring six additional device-independent units to the usable text viewport.
- Changed the shared TextBox `PART_ContentHost` from a vertically centered host element to a stretched host that fills the remaining padded interior. This prevents the content viewport itself from being unnecessarily constrained inside a fixed-height control.
- Bound the content host's horizontal and vertical content alignment to the templated TextBox, allowing the existing `VerticalContentAlignment=Center` rule to center normal one-line text correctly while preserving deliberate local overrides such as the top-aligned multiline Raw Memory Read result surface.

### Changed

- Updated the host application revision from `0.1.1.rev14` to `0.1.1.rev15`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `TextBox Content Padding Fix`.
- Updated README/current UI documentation to describe the corrected TextBox content layout while retaining `UiMetrics.StandardControlHeight = 34`.
- Updated the current project status to record that the rev11 memory-map and rev12 raw-memory-read paths are now live-verified on a real PlayStation 5.
- Updated the early-development architecture milestone list so memory-map enumeration and raw memory read are marked implemented and live-verified rather than pending.

### Removed

- Removed no application feature, target operation, plugin capability, Plugin SDK contract, Core behavior, PS5 protocol behavior, Mock behavior, theme, splitter, scanner placeholder, Saved Addresses placeholder, or existing control-height rule.
- No TextBox outer height was changed. `UiMetrics.StandardControlHeight` remains exactly `34d`.

### Version and Compatibility Notes

- Host application: `0.1.1.rev15`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev4` because this revision changes only host presentation resources and documentation.
- Plugin API: unchanged at `1.0.0`; no public SDK contract changed.
- The next PS5 backend implementation remains raw memory write/read-back through the already existing neutral `IMemoryWriter` contract. Because rev15 is consumed by this UI correction, that backend work will use the next available host revision rather than the previously suggested rev15 number.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and reviewed the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev14` baseline before modifying code.
- Audited every current TextBox declaration and confirmed standard one-line fields inherit the single shared implicit TextBox style; no duplicate per-view template or height workaround was required.
- Confirmed the multiline Raw Memory Read result TextBox deliberately retains its local `Height=150`, `Padding=10,8`, and `VerticalContentAlignment=Top` settings.
- Confirmed `UiMetrics.StandardControlHeight` remains `34d` and Button/ComboBox templates, workspace geometry, splitters, themes, Core, Plugin SDK, both platform plugins, and protocol tests are unchanged from rev14.
- Completed **108/108** static release checks covering XML/XAML and JSON parsing, version separation, TextBox template invariants, rev14 binding preservation, byte-identical unrelated source/test baselines, local Markdown links, and release-tree cleanliness.
- Recorded the user's completed live rev11/rev12 real-PS5 verification as PASS in testing and PS5-plugin documentation.
- The preparation environment does not provide the .NET Windows/WPF SDK, so the rev15 TextBox presentation correction still requires Windows-side rebuild/startup and visual confirmation before the revision is considered runtime-verified.

## TeeKay87's Memory Engine 0.1.1.rev14 - Raw Memory Read Result Binding Fix

### Added

- Added `docs/testing/APP_0.1.1_REV14_VERIFICATION.md` documenting the rev13 WPF startup exception, the binding-mode root cause, the exact correction, and the combined rev11/rev12 live verification that remains to be completed after startup succeeds.
- Added the observed Windows rev13 runtime result to `docs/testing/APP_0.1.1_REV13_VERIFICATION.md`, recording that the rev13 compiler fix progressed successfully to WPF startup before the separate result-binding exception occurred.

### Fixed

- Fixed the Raw Memory Read result `TextBox` startup exception by changing its `MemoryReadResultText` binding from the implicit `TextBox.Text` TwoWay default to explicit `Mode=OneWay`.
- Preserved `MemoryReadResultText` as an output-only ViewModel property with a private setter instead of weakening ViewModel encapsulation merely to satisfy the WPF binding engine.
- Confirmed the editable Raw Memory Read address and length fields remain explicitly TwoWay and that no other current `TextBox` targets a read-only ViewModel property.

### Changed

- Updated the host application revision from `0.1.1.rev13` to `0.1.1.rev14`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Raw Memory Read Result Binding Fix`.
- Updated README/current-workspace documentation to identify `0.1.1.rev14` as the current host build.
- Updated rev12 verification history to record that rev13 fixed compilation but a separate WPF result-binding issue blocked the pending combined memory-map/raw-read live test until rev14.

### Removed

- Removed no application feature, Plugin SDK contract, Core behavior, PS5 protocol behavior, Mock behavior, theme, splitter behavior, control style, scanner placeholder, or saved-address placeholder.
- No raw-memory-read functionality was rolled back; the correction only changes the direction of the result-display binding.

### Version and Compatibility Notes

- Host application: `0.1.1.rev14`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev4` because no plugin code changed.
- Plugin API: unchanged at `1.0.0`.
- Core, Plugin SDK, both platform plugins, protocol fixtures, themes, splitters, and shared control resources remain functionally unchanged from rev13.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev13` baseline before modifying code.
- Traced the reported `InvalidOperationException` to the default TwoWay binding mode of `TextBox.Text` and confirmed `IsReadOnly=True` does not change that binding direction.
- Audited all current host `TextBox` bindings: editable connection/address/length values are writable and explicitly TwoWay where bound; the raw-memory result is the only read-only bound TextBox and is now explicitly OneWay.
- Confirmed no .NET SDK or Windows WPF build toolchain is available in the preparation environment; Windows-side **Build -> Rebuild Solution** and startup remain required.

## TeeKay87's Memory Engine 0.1.1.rev13 - Raw Memory Read Compile Fix

### Added

- Added `docs/testing/APP_0.1.1_REV13_VERIFICATION.md` with the exact Windows build failure observed in rev12, the root-cause analysis, the expected post-fix Designer recovery behavior, and the remaining combined rev11/rev12 runtime verification steps.
- Added the real Windows-side rev12 build result to `docs/testing/APP_0.1.1_REV12_VERIFICATION.md` so the failed compile is preserved as test documentation rather than being represented as a successful runtime verification.

### Fixed

- Fixed `CS0177` in `PluginViewModel.TryParseHexAddress(...)`. The previous short-circuit expression could return before `ulong.TryParse(..., out address)` executed, leaving the `out` parameter not definitely assigned when the address field was empty.
- Initialized the hexadecimal-address `out` value before input normalization and changed the empty-address path to an explicit `return false`, preserving the intended validation behavior while satisfying C# definite-assignment rules.
- Identified the accompanying Visual Studio XAML Designer errors for `UiMetrics`, `ProportionalGridSplitter`, `System.Object`, and `DependencyProperty.UnsetValue` as downstream design-time assembly-load failures caused by the host project's C# compile failure. The valid public XAML types and their namespaces were deliberately left unchanged instead of adding unnecessary workarounds.

### Changed

- Updated the host application revision from `0.1.1.rev12` to `0.1.1.rev13`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Raw Memory Read Compile Fix`.
- Updated README/current-workspace documentation to identify `0.1.1.rev13` as the current host build while retaining rev12 as the revision that introduced raw memory read.

### Removed

- Removed no application feature, Core contract, Plugin SDK contract, PS5 protocol behavior, Mock behavior, theme, workspace layout, splitter behavior, control style, or test fixture.
- No raw-memory-read functionality was rolled back; the revision only corrects the host compile path required to run and verify it.

### Version and Compatibility Notes

- Host application: `0.1.1.rev13`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev4` because no plugin code changed.
- Plugin API: unchanged at `1.0.0`.
- Core, Plugin SDK, both platform plugins, protocol fixtures, themes, splitters, and shared control resources remain functionally unchanged from rev12.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev12` baseline before modifying code.
- Reproduced the definite-assignment problem directly from the rev12 source: `candidate.Length > 0 && ulong.TryParse(..., out address)` can short-circuit when the candidate is empty, so `address` was not assigned on every return path.
- Confirmed `UiMetrics` is a public type in `TeeKay87.MemoryEngine.App.Application`, `ProportionalGridSplitter` is a public type in `TeeKay87.MemoryEngine.App.Controls`, and both XAML namespace mappings are correct. Their Visual Studio Designer errors are therefore treated as cascading failures until the corrected host assembly is rebuilt.
- Completed a full preparation audit covering all 64 C#/XAML/project/theme source files and all 31 README/CHANGELOG/docs Markdown files; XML/JSON structure, local documentation links, Designer type mappings, and release-tree cleanliness passed.
- Confirmed Core, Plugin SDK, both platform plugins, protocol tests, themes, the proportional splitter implementation, and shared style resources are byte-for-byte unchanged from rev12.
- Confirmed no .NET SDK or Windows WPF build toolchain is available in the preparation environment; Windows-side **Build -> Rebuild Solution** remains required to close this verification.

## TeeKay87's Memory Engine 0.1.1.rev12 - PS5 Raw Memory Read

### Added

- Added PlayStation 5 `CMD_PROC_READ` (`0xBDAA0002`) support to the native C# ps5debug-NG command client.
- Added `IMemoryReader` support to the connected PS5 target session using the Plugin SDK contract that already existed from the foundation revision.
- Added the `MemoryRead` capability to the PS5 plugin now that a real implementation exists.
- Added a capability-driven **Raw Memory Read** inspector to the host target area. The inspector is shared by every plugin exposing `MemoryRead`; it contains no PlayStation-specific platform check.
- Added hexadecimal address input, decimal or `0x`-prefixed hexadecimal byte-length input, a generic Read Memory command, operation status/error reporting, and a monospaced hexadecimal/ASCII result dump.
- Added automatic default read-address selection from the first sufficiently large readable Active Target memory region when a memory map is available.
- Added host-side readable-range validation using the neutral cached `MemoryRegion` and `MemoryProtection` models before manual reads are sent to map-capable plugins.
- Added a 4096-byte manual-inspector limit to keep interactive raw-read output bounded without imposing that limit on `IMemoryReader`, the PS5 plugin protocol implementation, or future scanner buffers.
- Added deterministic ps5debug-NG raw-memory-read protocol coverage verifying the packed 16-byte request body, PID/address/length fields, success-status ordering, returned byte count, and exact returned byte sequence.
- Added `docs/plugins/PS5/MEMORY_READ_VERIFICATION.md` with PS5-specific protocol behavior and a combined live rev11/rev12 verification procedure.
- Added `docs/testing/APP_0.1.1_REV12_VERIFICATION.md` with project-wide architecture, version, source-level, and Windows runtime verification requirements.

### Fixed

- No previously reported runtime defect is targeted by this revision. The revision extends the verified target-access architecture from memory-map enumeration to read-only memory access.

### Changed

- Updated the host application revision from `0.1.1.rev11` to `0.1.1.rev12`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `PS5 Raw Memory Read`.
- Updated the PlayStation 5 plugin independently from `0.1.0.rev3` to `0.1.0.rev4`.
- Expanded the PS5 plugin capability set from `Connect | ProcessEnumeration | MemoryRegionEnumeration` to `Connect | ProcessEnumeration | MemoryRegionEnumeration | MemoryRead`.
- Extended the PS5 target session to expose `IMemoryReader` alongside its existing `IProcessProvider` and `IMemoryMapProvider` services.
- Serialized ps5debug-NG raw-read requests as the documented packed layout: 4-byte process id, 8-byte target address, and 4-byte requested length.
- Required the ps5debug-NG wire success status before consuming the requested raw response bytes.
- Changed host command-state coordination so Disconnect, process Refresh, and Set Active Target are disabled while a manual raw-memory read is in progress, preventing intentional target/session changes during the operation.
- Changed Active Target/process clearing so raw-read input/result/error state is reset together with process and memory-map state.
- Updated README, Plugin SDK architecture notes, early development ordering, PS5 plugin documentation, ps5debug-NG protocol mapping, main-workspace documentation, rev11 verification notes, and memory-map verification notes to reflect the new read layer and the combined live verification plan.

### Removed

- Removed raw memory read from the PS5 plugin's documented unimplemented capability list because it is now implemented.
- Removed no Core contract, Plugin SDK contract, Mock-plugin behavior, theme, splitter behavior, control metric, scan placeholder, saved-address placeholder, or previously verified PS5 connection/process behavior.
- Memory write and scanning remain deliberately unimplemented and unadvertised.

### Version and Compatibility Notes

- Host application: `0.1.1.rev12`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: `0.1.0.rev4`.
- Plugin API: unchanged at `1.0.0` because `MemoryRead` and `IMemoryReader` already existed in the public contract.
- Core and Plugin SDK source are unchanged from rev11.
- The permanent CE-inspired workspace, external color themes, proportional splitters, and centralized 34-unit standard control-height system remain unchanged except for the functional Raw Memory Read expander added to the target area.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev11` baseline before modifying code.
- Confirmed the existing Plugin SDK already provides the required neutral `MemoryRead` capability and `IMemoryReader`, so no duplicate or PS5-specific public contract was introduced.
- Checked `CMD_PROC_READ` against the current public ps5debug-NG protocol documentation: command `0xBDAA0002`, packed 16-byte `{ uint32 pid; uint64 address; uint32 length; }` request, success status, then raw response bytes.
- Extended the deterministic protocol fixture so read serialization and raw response handling can be verified without a physical console.
- Prepared combined live verification so rev11 memory-map enumeration and rev12 raw-memory read are proven together on the real PS5 rather than requiring two separate stop points.
- Completed 484 static release checks covering source/resource validity, project references, version separation, capability boundaries, PS5 protocol layout, host integration, deterministic test coverage, documentation links, release cleanliness, and byte-for-byte preservation of the unchanged Core/Plugin SDK/Mock/UI-foundation files.
- Checked the raw-read receive behavior against the referenced ps5debug-NG server source, which sends the full requested byte count after `CMD_SUCCESS` while chunking internally at 64 KiB without extra per-chunk wire framing.
- Native .NET/WPF compilation and live PS5 execution remain required on the Windows development machine because the preparation environment does not contain the .NET SDK or a physical PS5.

## TeeKay87's Memory Engine 0.1.1.rev11 - PS5 Memory Map Enumeration

### Added

- Added PlayStation 5 `CMD_PROC_MAPS` (`0xBDAA0004`) support to the native C# ps5debug-NG command client.
- Added PS5-specific `Ps5DebugMemoryRegionInfo` as an internal wire-model record that remains confined to the PS5 plugin.
- Added `IMemoryMapProvider` support to the connected PS5 target session using the Plugin SDK contract that already existed from the foundation revision.
- Added automatic Active Target memory-map loading in the WPF host for any plugin advertising `MemoryRegionEnumeration` and exposing `IMemoryMapProvider`.
- Added host-side `ActiveMemoryRegions`, memory-map status, and memory-map error state so the neutral region list is ready for subsequent memory read/write and scanner work.
- Added automatic memory-map refresh when a process-list refresh preserves the same Active Target.
- Added a deterministic ps5debug-NG memory-map protocol fixture covering request-body serialization, response entry parsing, empty names, size conversion, and read/write/execute protection translation.
- Added `docs/plugins/PS5/MEMORY_MAP_VERIFICATION.md` with plugin-specific protocol/runtime verification requirements.
- Added `docs/testing/APP_0.1.1_REV11_VERIFICATION.md` with host/project verification requirements.
- Added the Windows runtime result for rev10 confirming the unified 34-unit control-height foundation looks correct and cohesive.

### Fixed

- No previously reported defect is targeted by this revision. The change resumes planned backend development after the rev10 UI foundation was verified.

### Changed

- Updated the host application revision from `0.1.1.rev10` to `0.1.1.rev11`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `PS5 Memory Map Enumeration`.
- Updated the PlayStation 5 plugin independently from `0.1.0.rev2` to `0.1.0.rev3` because that plugin now implements an additional backend capability.
- Expanded the PS5 plugin capability set from `Connect | ProcessEnumeration` to `Connect | ProcessEnumeration | MemoryRegionEnumeration`.
- Extended the PS5 command sender so requests may carry a bounded body while preserving the existing zero-body path used by connection metadata, NOP, and process-list commands.
- Added parsing for packed 58-byte ps5debug-NG `proc_vm_map_entry` records containing name, start, end, offset, and protection fields.
- Added defensive validation for impossible end-before-start ranges and an upper bound of 262,144 memory-map records before allocating the response payload.
- Translated PS5 VM protection bits `0x1`, `0x2`, and `0x4` to the neutral `MemoryProtection.Read`, `Write`, and `Execute` flags. Unknown bits are not guessed into unrelated neutral flags.
- Changed Set Active Target from a synchronous host command to an asynchronous host command so activation can complete the capability-driven memory-map request without blocking the WPF UI thread.
- Updated the target status area to report the Active Target memory-map load state/count and to surface memory-map errors separately from connection/process errors.
- Updated README, Plugin SDK architecture notes, PS5 plugin documentation, PS5 protocol mapping, main-workspace documentation, and early development ordering to reflect memory maps as the current completed implementation layer before raw read/write.

### Removed

- Removed memory-map enumeration from the PS5 plugin's documented unimplemented list because it is now implemented.
- Removed no Plugin SDK contract, Core behavior, theme, splitter behavior, control style, process-selection behavior, or existing plugin feature.
- The ps5debug-NG map `offset` value is intentionally not exposed through a PS5-specific host extension; the neutral `MemoryRegion` contract remains unchanged until a shared consumer requires such data.

### Version and Compatibility Notes

- Host application: `0.1.1.rev11`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: `0.1.0.rev3`.
- Plugin API: unchanged at `1.0.0` because `MemoryRegionEnumeration`, `IMemoryMapProvider`, `MemoryRegion`, and `MemoryProtection` already existed in the public contract.
- No Core or Plugin SDK source change was required for the new PS5 implementation.
- UI themes, splitters, shared control metrics, and the permanent workspace layout remain functionally unchanged from rev10.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, and the complete C#/XAML/project/solution source from the supplied `0.1.1.rev10` baseline before modifying code.
- Confirmed the existing Plugin SDK already provides all generic memory-map abstractions required by the PS5 implementation, avoiding a duplicate or PS5-specific contract.
- Checked the `CMD_PROC_MAPS` wire layout against the public ps5debug-NG protocol reference: 4-byte PID request; success status; `uint32` count; packed 58-byte entries with 32-byte name plus start/end/offset/protection fields.
- Extended the deterministic protocol server so the new request/response path can be verified without a physical console.
- Static release checks cover version separation, capability consistency, protocol constants/entry sizes, PS5-type isolation, neutral memory-region mapping, host Active Target state handling, XML/XAML validity, documentation links, and release-tree cleanliness.
- Native .NET/WPF compilation and live PS5 memory-map verification remain required on the Windows development machine.

## TeeKay87's Memory Engine 0.1.1.rev10 - Unified Interactive Control Heights

### Added

- Added central `UiMetrics.StandardControlHeight` host UI metric with a value of `34` WPF device-independent units.
- Added `docs/ui/CONTROL_METRICS.md` documenting the application-wide sizing contract for standard single-line interactive controls and the rules future control styles must follow.
- Added `docs/testing/APP_0.1.1_REV10_VERIFICATION.md` with source-level checks and Windows runtime tests for consistent control heights across the target bar and Scan workspace.
- Added the rev9 Windows runtime result confirming that the proportional horizontal splitter and preserved Scan-panel width bounds work correctly in both windowed and fullscreen use.

### Fixed

- Fixed standard buttons being taller than the Platform selector because `ButtonBaseStyle` used an independent `MinHeight=38` while ComboBox and TextBox styles used a 34-unit baseline.
- Fixed the Target Process selector stretching to the height of the taller buttons in its Grid row, making it visibly taller than the Platform selector even though both used the same ComboBox style.
- Fixed connection TextBoxes, target selectors, scan inputs, scan selectors, and command buttons not sharing one predictable application-wide single-line control height.

### Changed

- Updated the host application revision from `0.1.1.rev9` to `0.1.1.rev10`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Unified Interactive Control Heights`.
- Changed `ButtonBaseStyle` from an independent `38`-unit minimum height to the central fixed `UiMetrics.StandardControlHeight`.
- Changed the implicit TextBox and ComboBox styles from independent 34-unit minimum heights to the same central fixed `UiMetrics.StandardControlHeight`.
- Standardized all current normal single-line `Button`, `TextBox`, and `ComboBox` instances at the Platform selector's existing 34-unit height without adding per-view height overrides.
- Established that future standard single-line interactive control styles must consume the same central metric unless a control has a deliberate documented reason to use a different form factor.
- Updated README, button-style documentation, main-workspace documentation, and testing documentation to describe the shared sizing contract.

### Removed

- Removed the independent `MinHeight=38` value from the shared button style.
- Removed the independent `MinHeight=34` declarations from the shared TextBox and ComboBox styles in favor of the central metric.
- Removed no user-facing feature, splitter behavior, theme, plugin behavior, backend capability, or existing command.

### Version and Compatibility Notes

- Host application: `0.1.1.rev10`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Theme schema, external theme files, splitter ratios/bounds, Core contracts, and plugin contracts are unchanged.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the `0.1.1.rev9` baseline before modifying code.
- Confirmed the visual mismatch from the user's live rev9 screenshot is caused by independent style heights and Grid stretch behavior, not by platform-specific layout code.
- Confirmed the Platform ComboBox's established 34-unit baseline can be preserved as the shared application metric while bringing Buttons, TextBoxes, and other ComboBoxes to the same height.
- Confirmed the change is isolated to the WPF host presentation layer; Core, Plugin SDK, Mock plugin, PS5 plugin, theme JSON files, and existing backend/protocol tests require no modification.
- Static release checks verify the centralized 34-unit metric, implicit style coverage, absence of conflicting standard-control height declarations, version separation, XML/XAML validity, unchanged backend/plugin projects, documentation links, and clean release contents.
- Native Windows WPF compilation and runtime verification remain required in Visual Studio.

## TeeKay87's Memory Engine 0.1.1.rev9 - Proportional Workspace Splitter Range

### Added

- Added reusable `ProportionalGridSplitter` host UI control for splitter pairs that must be constrained by relative workspace ratios rather than fixed pixel limits.
- Added configurable minimum/maximum previous-panel ratio properties and live star-sizing normalization so proportional splits scale with the available window size.
- Added `docs/testing/APP_0.1.1_REV9_VERIFICATION.md` with source-level checks and Windows runtime tests for the 50/50 default and 20/80–80/20 resize range.
- Added the rev8 Windows runtime observation documenting that the Scan-panel width bounds are correct while the horizontal fixed-height bounds are too restrictive.

### Fixed

- Fixed the horizontal Scan Results/Saved Addresses splitter having an unnaturally narrow resize range in fullscreen because Saved Addresses was capped by a fixed `360 px` maximum.
- Fixed the horizontal split starting from an asymmetric fixed-height Saved Addresses row instead of sharing the available workspace equally.
- Fixed horizontal splitter proportions not being defined relative to the actual available workspace height.

### Changed

- Updated the host application revision from `0.1.1.rev8` to `0.1.1.rev9`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Proportional Workspace Splitter Range`.
- Changed Scan Results and Saved Addresses from mixed star/fixed row sizing to equal `*` / `*` row sizing, producing a 50% / 50% initial allocation.
- Changed the horizontal resize model from fixed pixel minimum/preferred/maximum heights to a proportional **20% / 80% through 80% / 20%** range.
- Preserved the rev8 vertical Scan-panel bounds unchanged: preferred `310 px`, minimum `280 px`, maximum `420 px`, with a `640 px` minimum left workspace.
- Updated README and main-workspace documentation to describe proportional horizontal resizing.

### Removed

- Removed the rev8 fixed horizontal row constraints: Scan Results `MinHeight=240` and Saved Addresses `Height=235`, `MinHeight=180`, `MaxHeight=360`.
- Removed no user-facing feature, theme, plugin behavior, backend capability, splitter visual style, or vertical Scan-panel constraint.

### Version and Compatibility Notes

- Host application: `0.1.1.rev9`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Theme schema, theme ids, persisted preferences, Core contracts, and plugin contracts are unchanged.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the `0.1.1.rev8` baseline before modifying code.
- Confirmed from the user's live rev8 screenshots that the remaining problem is specific to fixed horizontal height bounds; the vertical Scan-panel width constraints should remain unchanged.
- Confirmed no existing shared Core or Plugin SDK abstraction is relevant to this host-only presentation behavior, so the proportional splitter is isolated to the WPF App project.
- Static release checks verify the 50/50 star-row default, 20/80–80/20 ratio configuration, preserved vertical bounds, version separation, XML/XAML validity, unchanged backend/plugin projects, documentation links, and clean release contents.
- Native Windows WPF compilation and runtime verification remain required in Visual Studio.

## TeeKay87's Memory Engine 0.1.1.rev8 - Workspace Splitter Bounds Fix

### Added

- Added explicit usability bounds for the vertical workspace split so the left memory workspace and Scan panel cannot be resized into impractical widths.
- Added explicit height bounds for the horizontal Scan Results/Saved Addresses split so neither table can consume an unusable share of the left workspace.
- Added `docs/testing/APP_0.1.1_REV8_VERIFICATION.md` with source-level checks and required Windows runtime verification for both splitter extremes.
- Added the rev7 Windows runtime observation documenting that the theme-id warning fix succeeded while revealing the separate splitter-boundary issue.

### Fixed

- Fixed the vertical splitter allowing the Scan panel to become excessively wide and compress the left workspace enough to clip or visually distort controls.
- Fixed the horizontal splitter allowing the Saved Addresses/Scan Results allocation to reach impractical proportions that reduced workspace usability.
- Fixed the effective resize limits being based only on permissive row/column minimums rather than on dimensions that preserve the permanent workspace layout.

### Changed

- Updated the host application revision from `0.1.1.rev7` to `0.1.1.rev8`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Workspace Splitter Bounds Fix`.
- Increased the left workspace minimum width from `520` to `640` pixels.
- Changed the Scan panel constraints from a `250` pixel minimum with no maximum to a `280` pixel minimum and `420` pixel maximum while retaining its `310` pixel preferred width.
- Increased the Scan Results minimum height from `220` to `240` pixels.
- Changed Saved Addresses from a `150` pixel minimum with no maximum to a `180` pixel minimum and `360` pixel maximum while retaining its `235` pixel preferred height.
- Updated README and main-workspace documentation to describe the bounded splitter behavior.

### Removed

- Removed no user-facing feature, splitter, theme, memory-tool workflow, platform capability, plugin behavior, or backend functionality.
- Removed no resize capability; only layout states that made the surrounding UI unusable are no longer reachable.

### Version and Compatibility Notes

- Host application: `0.1.1.rev8`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Theme schema, canonical theme ids, and persisted theme preferences are unchanged from rev7.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the `0.1.1.rev7` baseline before modifying code.
- Confirmed the runtime symptom is caused by workspace `RowDefinition`/`ColumnDefinition` constraints rather than the reusable splitter ControlTemplates introduced in rev7.
- Preserved the rev7 14-pixel hit areas and centered 4-pixel splitter handles unchanged.
- Confirmed the fix is host-layout-only; Core, Plugin SDK, Mock plugin, PS5 plugin, and existing backend/protocol tests do not require modification.
- Static release checks verify the new width/height bounds, version separation, XML/XAML validity, unchanged backend projects, documentation links, and clean release contents.
- Native Windows WPF compilation and runtime verification remain required in Visual Studio.

## TeeKay87's Memory Engine 0.1.1.rev7 - Theme Identity and Splitter Spacing Fix

### Added

- Added canonical theme-id compatibility aliases so preferences and legacy theme definitions using `darker`/`darkest` resolve to `dimmed`/`dark`.
- Added automatic persistence migration so a successfully resolved legacy saved theme id is rewritten to its canonical replacement.
- Added runtime recognition of the obsolete bundled `Darker.json` and `Darkest.json` filenames when their current replacement files are present.
- Added build and publish cleanup targets that remove stale copies of those two legacy bundled theme files from generated `Themes` directories after incremental builds.
- Added reusable `RowWorkspaceSplitterStyle` and `ColumnWorkspaceSplitterStyle` resources. Each keeps a 14-pixel draggable track while drawing a centered 4-pixel theme-aware handle.
- Added `docs/testing/APP_0.1.1_REV7_VERIFICATION.md` and recorded the Windows runtime observations that led to this correction in the rev6 verification document.

### Fixed

- Fixed duplicate theme-id warnings observed after rev6 incremental builds. Visual Studio could leave copied `Darker.json` and `Darkest.json` files in the runtime `Themes` directory after the bundled files were renamed, causing old and current files to share ids.
- Fixed the same stale-file condition causing the Theme selector to load an obsolete `Darker` or `Darkest` display entry instead of the current Dimmed or Dark definition.
- Fixed uneven splitter spacing that made the horizontal and vertical drag handles appear attached to one neighboring panel.

### Changed

- Updated the host application revision from `0.1.1.rev6` to `0.1.1.rev7`; semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Theme Identity and Splitter Spacing Fix`.
- Changed the bundled Dimmed theme id from legacy `darker` to canonical `dimmed`.
- Changed the bundled Dark theme id from legacy `darkest` to canonical `dark` and updated the default external theme id accordingly.
- Changed both splitter layout tracks from 6 pixels to 14 pixels and added reusable shared splitter templates that center a 4-pixel visible handle inside the full interactive track with 5 pixels of breathing room on both sides.
- Removed the previous one-sided margins from Saved Addresses and the Scan panel because splitter spacing is now symmetric inside the splitter tracks.
- Updated README, theme documentation, workspace documentation, rev6 runtime observations, and rev7 verification documentation.

### Removed

- Removed no user-facing feature, memory-tool workflow, platform capability, plugin behavior, or theme palette.
- The obsolete theme ids are not accepted as canonical bundled ids anymore, but remain supported as migration aliases for preferences written by earlier revisions.

### Version and Compatibility Notes

- Host application: `0.1.1.rev7`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Existing rev4-rev6 saved theme preferences remain compatible through automatic id migration.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the `0.1.1.rev6` baseline before modifying code.
- Confirmed the runtime duplicate warning is caused by stale renamed theme files in incremental build output rather than by malformed current JSON.
- Kept duplicate-id validation active for real conflicts while isolating compatibility handling to the two known bundled legacy files/ids.
- Confirmed the change remains host-only; Core, Plugin SDK, Mock plugin, PS5 plugin, and existing backend/protocol tests do not require modification.
- Static release checks verify theme ids/aliases, stale-file cleanup, splitter dimensions/margins, XML/XAML/JSON validity, version separation, documentation links, unchanged backend projects, and clean release contents.
- Native Windows WPF compilation and runtime verification remain required in Visual Studio.

## TeeKay87's Memory Engine 0.1.1.rev6 - Workspace Layout and Theme Refinement

### Added

- Added a vertical resizable `GridSplitter` between the left-side memory lists and the Scan panel so the user can adjust the scanner-control width at runtime.
- Added a shared `ErrorMessageTextStyle` that collapses empty error messages, preventing invisible error rows from reserving unnecessary height in the target/connection area.
- Added explicit display-string behavior to `ThemeDescriptor`, `PluginViewModel`, and `TargetProcessViewModel` so the application's custom ComboBox template consistently presents the intended theme, platform, and target-process labels.
- Added project-wide verification documentation for the rev6 workspace/theme refinement under `docs/testing/APP_0.1.1_REV6_VERIFICATION.md`.

### Changed

- Updated the host application revision from `0.1.1.rev5` to `0.1.1.rev6`; the semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Workspace Layout and Theme Refinement`.
- Restructured the central workspace so **Scan Results** and **Saved Addresses** occupy the left side while the **Scan** panel spans their combined full height on the right.
- Preserved the existing horizontal splitter between Scan Results and Saved Addresses inside the left workspace and moved it into the new nested layout.
- Reduced target/connection padding and vertical spacing so the memory workspace begins closer to the normal connection/process status line.
- Renamed the user-facing **Darkest** theme to **Dark** while preserving its original dark palette.
- Renamed the user-facing **Darker** theme to **Dimmed** and changed its palette to a substantially lighter mid-dark set of surfaces, borders, inputs, selection, and disabled-state colors so it is meaningfully positioned between Light and Dark.
- Renamed the bundled external theme source files to `Light.json`, `Dimmed.json`, and `Dark.json` while deliberately retaining the established internal ids `light`, `darker`, and `darkest` for persisted-settings compatibility.
- Updated current README, architecture, UI-theme, UI-workspace, and button-style documentation for the refined layout and theme names.

### Removed

- Removed the explanatory `Classic first/next scan workflow...` development text from the permanent Scan panel.
- Removed the accent information box explaining why disabled scanner controls were visible.
- Removed unused vertical layout space caused by empty connection/process error rows.
- No scanner behavior, saved-address behavior, PS5 communication, process enumeration, target selection, plugin capability, Core contract, or Plugin SDK contract was removed.

### Version and Compatibility Notes

- Host application: `0.1.1.rev6`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- Theme ids remain `light`, `darker`, and `darkest`; only the latter two user-facing names/source filenames changed, so earlier persisted theme selections remain compatible.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev5` baseline before changing code.
- Confirmed that the requested UI work can remain entirely inside the host application and documentation; no Core, Plugin SDK, Mock plugin, PS5 plugin, or protocol changes are required.
- Confirmed the Dark palette is unchanged from rev5's Darkest theme and that Dimmed uses a distinct intermediate palette.
- Confirmed all XAML and JSON files parse successfully after restructuring.
- Static release checks cover root/workspace row/column structure, both splitters, removed placeholder copy, collapsing error rows, ComboBox display labels, theme completeness, version separation, unchanged backend projects, documentation links, and clean release contents.
- Native Windows WPF compilation and visual/runtime verification must be performed in Visual Studio because the release-preparation environment does not contain the .NET SDK or Windows WPF toolchain.

## TeeKay87's Memory Engine 0.1.1.rev5 - Theme Manager Nullability Compile Fix

### Fixed

- Fixed two nullable-reference compiler errors (`CS8600`) in `ThemeManager` that prevented the WPF host project from compiling with the project-wide `Nullable=enable` and `TreatWarningsAsErrors=true` settings.
- Fixed runtime theme lookup in `ApplyTheme` so the value returned through `Dictionary.TryGetValue` is explicitly treated as nullable until both lookup success and a non-null `LoadedTheme` instance have been established.
- Fixed persisted/startup theme lookup in `FindTheme` using the same explicit nullable guard, preserving the existing behavior of returning `null` when no valid theme matches the requested id.

### Changed

- Updated the host application revision from `0.1.1.rev4` to `0.1.1.rev5`; the semantic application version remains `0.1.1`.
- Updated `AppInfo.FeatureTitle` to `Theme Manager Nullability Compile Fix`.
- Updated `README.md` and current UI documentation to identify `0.1.1.rev5` as the active host revision.
- Added project-wide verification documentation for the rev5 compile correction.

### Removed

- No functionality was removed.
- No theme schema, theme color, theme file, WPF style, workspace control, PS5 connection behavior, process-enumeration behavior, target-selection behavior, Core contract, Plugin SDK contract, or plugin implementation was removed or redesigned.

### Version and Compatibility Notes

- Host application: `0.1.1.rev5`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- The correction is confined to host-side nullable handling in `ThemeManager`; it does not alter plugin compatibility or the external JSON theme contract.

### Verification

- Re-read `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, and the complete C#/XAML/project/solution/theme source tree from the supplied `0.1.1.rev4` baseline before changing code.
- Confirmed that both reported `CS8600` diagnostics originate from `Dictionary.TryGetValue` out values at the theme-application and theme-lookup call sites.
- Changed only the nullability handling necessary to make those lookups explicit and safe before `LoadedTheme` is dereferenced.
- Preserved the rev4 workspace, themes, Core, Plugin SDK, Mock plugin, PS5 plugin, protocol implementation, and existing verification tests.
- Static release checks verify version separation, nullable guards, project/documentation references, unchanged plugin/API versions, XML/XAML/JSON syntax, and clean release contents.
- Native Windows WPF compilation remains the authoritative verification for the reported compiler errors and must be performed in Visual Studio after applying this revision.

## TeeKay87's Memory Engine 0.1.1.rev4 - Cheat Engine-Inspired Workspace and Color Themes

### Added

- Added a permanent Cheat Engine-inspired main workspace that preserves the familiar target-first memory-tool workflow while using the application's own modern WPF presentation rather than reproducing Cheat Engine's visual chrome.
- Added a compact application bar containing the application identity/version and a runtime Theme selector.
- Added a compact target/connection area that reuses the existing generic platform selection, plugin-defined connection fields, Connect/Disconnect commands, process refresh, selected process, and explicit Active Target workflow.
- Added a dedicated **Scan Results** workspace for future temporary scan candidates, with prepared columns for address, current value, previous value, value type, and memory region/module context.
- Added a dedicated right-side **Scan** panel with prepared Value, Scan Type, Value Type, First Scan, Next Scan, and New Scan controls. These controls are intentionally disabled until scanner behavior is implemented.
- Added a separate lower **Saved Addresses** workspace with prepared Active, Description, Address, Type, Value, Frozen, and Notes columns. Add/Edit/Remove/Export actions are intentionally disabled until the persistent-address model is implemented.
- Added a resizable horizontal divider between the scanner workspace and Saved Addresses so result-heavy and address-heavy workflows can allocate space differently.
- Added collapsible **Plugin details** so diagnostic plugin metadata and capability badges remain available without permanently occupying the primary working area.
- Added `Resources/Styles/ControlStyles.xaml` with reusable theme-aware styles/templates for common WPF presentation including TextBlock, TextBox, ComboBox, ComboBoxItem, CheckBox, DataGrid, DataGrid headers/rows/cells, cards, and Expander presentation.
- Added an external JSON color-theme system under `TeeKay87.MemoryEngine.App/Theming` that discovers, validates, orders, applies, and reports theme files without allowing themes to supply XAML or replace the UI.
- Added `ThemeManager` to map a stable JSON palette contract to shared WPF `SolidColorBrush` resources and replace those resources at runtime.
- Added complete-theme validation so a malformed or partial theme is skipped instead of inheriting stale colors from the previously active theme.
- Added strict `#RRGGBB` / `#AARRGGBB` color parsing and duplicate theme-id validation.
- Added **Light**, **Darker**, and **Darkest** external JSON themes. `Darkest` preserves the original application palette used before theme support.
- Added immediate theme switching through `DynamicResource` brush replacement so the already-open main window changes palette without restarting.
- Added persistence of the selected theme id under `%LocalAppData%\TeeKay87\MemoryEngine\settings.json`.
- Added startup fallback behavior that prefers the persisted valid theme, then `Darkest`, then the first valid discovered theme.
- Added a Darkest-compatible emergency palette in `App.xaml` so the UI remains readable if no external theme can be loaded.
- Added build and publish packaging of `Themes\*.json` to the application's runtime `Themes` directory.
- Added `docs/ui/THEMES.md` documenting the external JSON schema, palette keys, validation, persistence, fallback behavior, and extension rules.
- Added `docs/ui/MAIN_WORKSPACE.md` documenting the permanent Cheat Engine-inspired workflow, Scan Results/Saved Addresses separation, target context, placeholder policy, and platform-neutral UI requirements.
- Added `docs/testing/APP_0.1.1_REV4_VERIFICATION.md` with build, theme, workspace, PS5 regression, and archive verification requirements.

### Changed

- Updated the host application revision from `0.1.1.rev3` to `0.1.1.rev4`; the semantic application version remains `0.1.1` while this early development line continues.
- Updated `AppInfo.FeatureTitle` to `Cheat Engine-Inspired Workspace and Color Themes`.
- Replaced the previous temporary plugin-development-centered main-window layout with the permanent scanner-oriented workspace. The already verified connection/process commands and `PluginViewModel` state are relocated and reused rather than reimplemented.
- Changed platform selection from the previous large plugin presentation to a compact selector while keeping plugin-defined connection metadata and capability-driven behavior.
- Changed process presentation from the previous large process panel to a compact target process picker plus explicit Active Target summary. Selected process and Active Target remain separate states.
- Changed plugin metadata/capability presentation from permanently visible main content to an on-demand collapsible details area.
- Changed shared UI colors from a single hardcoded application palette to theme-driven brush resources. `App.xaml` now owns only the emergency fallback values; normal startup replaces them from external theme JSON.
- Changed common WPF control styling so TextBox, ComboBox, DataGrid, and related controls participate in the same active color palette as the reusable button system.
- Updated the reusable button documentation so button colors are described as active-theme resources rather than fixed `App.xaml` colors and documented the new disabled scanner/address placeholder usage.
- Updated the early architecture document to explicitly define the Cheat Engine-inspired scanner workspace, temporary-vs-persistent list separation, Active Target behavior, shared common-control styling, presentation-only color themes, and the decision to establish the permanent workspace before further memory/scanner features.
- Updated `README.md` to describe the current workspace, runtime theme system, build structure, live PS5 process-enumeration status, and current implementation boundary without using README as revision history.
- Updated rev3 project-wide and PS5 process verification documentation with the 2026-08-30 user-reported live result that the application connects to a physical PS5 and retrieves its real process list.

### Removed

- Removed the previous requirement for the plugin list, full plugin metadata, capabilities, connection area, and process list to occupy the majority of the main window at all times.
- Removed the previous single-palette limitation from normal runtime presentation; application colors can now be selected from discovered external theme files.
- Removed ordinary main-workspace dependence on local fixed colors in favor of semantic shared brush resources.
- No verified PS5 connection behavior, PS5 process-enumeration behavior, process selection/Active Target state, Core behavior, Plugin SDK contract, plugin capability, Mock plugin memory behavior, protocol command, or verification fixture was removed.

### Version and Compatibility Notes

- Host application: `0.1.1.rev4`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: unchanged at `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- No Plugin SDK compatibility change is introduced. Themes are host-presentation data and do not become part of platform-plugin contracts.
- Neither built-in plugin receives a revision bump because their source, capabilities, protocol behavior, and plugin documentation contract are not changed by the host UI/theme implementation.

### Verification

- Reviewed `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, all C# source, XAML, project files, solution configuration, `Directory.Build.props`, and `.gitignore` from the supplied `0.1.1.rev3` baseline before changing code.
- Confirmed before implementation that the existing `PluginViewModel`, `IProcessProvider`, selected-process state, Active Target state, connection commands, and process commands can be reused by the permanent workspace without introducing parallel backend logic.
- Preserved Core, Plugin SDK, Mock plugin, PS5 plugin, and verification-test source from the supplied rev3 baseline; the revision is intentionally isolated to host application presentation/theming plus documentation.
- Recorded the user's 2026-08-30 confirmation that rev3 successfully connects to a physical PS5 and retrieves its real process list as a runtime baseline that rev4 must preserve.
- Completed 76 static source-preparation checks covering version separation, unchanged backend/plugin/test baselines, XAML/XML/JSON syntax, complete theme palettes, Darkest/fallback consistency, resource resolution, theme-driven color usage, command reuse, capability gating, project/documentation references, and release-tree cleanliness.
- Theme/runtime visual behavior requires Windows WPF verification. The source-preparation environment does not contain the .NET SDK or Windows WPF runtime, so final native compilation and live visual/theme regression checks remain documented Windows verification items.

## TeeKay87's Memory Engine 0.1.1.rev3 - PS5 Process Enumeration and Target Selection

### Added

- Added PS5 `CMD_PROC_LIST` (`0xBDAA0001`) support to the existing native C# ps5debug-NG command client.
- Added bounded parsing of the documented process-list response: wire success status, `uint32` process count, and fixed 36-byte entries containing a 32-byte process name plus signed 32-bit PID.
- Added client-side process-count validation before response-buffer allocation and negative-PID validation before converting PS5 process identifiers to the neutral unsigned `TargetProcess.Id` representation.
- Added `Ps5DebugProcessInfo` as an internal PS5-plugin transport model so the ps5debug-NG wire record remains contained inside the plugin.
- Added `IProcessProvider` implementation to `Ps5TargetSession`, mapping PS5 transport results to the already existing platform-neutral `TargetProcess` model.
- Added a generic target-process panel to the WPF shell for any connected plugin advertising `TargetCapabilities.ProcessEnumeration`.
- Added automatic process refresh after a successful connection when process enumeration is supported.
- Added a reusable process Refresh command that requests a new list through the active session's existing `IProcessProvider` service.
- Added explicit separation between the currently selected process row and the **Active Target** intended for later memory operations.
- Added **Set Active Target** using the existing shared Primary button style and **Refresh** using the existing shared Secondary button style.
- Added process-state preservation across refreshes when the same PID/name identity remains available, while clearing stale selected/active identities that no longer exist.
- Added process-list and active-target cleanup on disconnect.
- Added `TargetProcessViewModel` for presentation-only formatting of neutral process values, including hexadecimal PID display and safe fallback text for unnamed processes.
- Extended the loopback ps5debug-NG protocol fixture so it can serve deterministic process-list responses after the connection handshake.
- Added a dedicated verification check for PS5 process enumeration using representative `SceShellCore`, `eboot.bin`, and `WebProcess` entries.
- Added `docs/plugins/PS5/PROCESS_ENUMERATION_VERIFICATION.md` with deterministic and live-target verification procedures specific to the PS5 plugin.
- Added `docs/testing/APP_0.1.1_REV3_VERIFICATION.md` with project-wide build, regression, and platform-neutral UI verification requirements.

### Changed

- Updated the host application revision from `0.1.1.rev2` to `0.1.1.rev3`; the application version remains `0.1.1` because development is continuing within the same early feature line.
- Updated `AppInfo.FeatureTitle` to `PS5 Process Enumeration and Target Selection`.
- Updated the PS5 plugin's independent revision from `0.1.0.rev1` to `0.1.0.rev2`. The PS5 plugin semantic version remains `0.1.0` while the new process functionality awaits live-target verification.
- Updated the PS5 plugin capability declaration from `Connect` to `Connect | ProcessEnumeration`. Foreground-process discovery and all memory/scanner/debugger capabilities remain intentionally unadvertised.
- Updated the PS5 plugin description to reflect connection plus target process enumeration.
- Updated `Ps5TargetSession.GetService<TService>()` so the connected PS5 session exposes its newly implemented `IProcessProvider` service while continuing to expose no unimplemented services.
- Updated the generic plugin view model with process-loading state, process-specific status/error presentation, selected-process state, active-target state, and command availability tied to connection/capability state.
- Updated the process-list selection binding to explicit two-way synchronization so UI row selection and the generic `SelectedProcess` state remain intentionally coupled while the separate Active Target remains unchanged until explicitly set.
- Updated the current-development milestone text so it reflects process enumeration and explicit active-target selection while retaining the limitation that PS5 memory services are not implemented yet.
- Updated the verification executable's PS5 metadata expectations for the independent PS5 plugin revision and expanded capability set.
- Updated the PS5 connection test to expect `IProcessProvider` now that that service is implemented.
- Updated the existing PS5 protocol mapping from connection-only scope to connection plus process-list scope.
- Updated PS5 connection documentation with the successful live Windows/PS5 connection result reported on 2026-08-30, without treating untested disconnect/reconnect or process-list behavior as verified.
- Updated the reusable button documentation to include the new Refresh and Set Active Target buttons and confirm that the shared button resources are reused rather than copied.
- Updated the Plugin SDK architecture documentation to describe how the WPF shell consumes `IProcessProvider` and keeps selected-process state separate from the active target without introducing platform checks.
- Updated `README.md` to describe the current complete process-enumeration workflow, current PS5 plugin version/capabilities, verification suite, usage, and remaining limitations.

### Removed

- Removed the previous PS5-plugin limitation that process enumeration was unavailable.
- Removed the previous UI state in which a connected process-capable plugin had no generic process-selection workflow.
- No existing Core contracts, Plugin SDK contracts, mock-target process/memory behavior, plugin discovery behavior, PS5 connection-identification sequence, shared button templates, or connection-setting behavior was removed.

### Version and Compatibility Notes

- Host application: `0.1.1.rev3`.
- In-Memory Test Target plugin: unchanged at `1.0.0.rev1`.
- PlayStation 5 plugin: `0.1.0.rev2`.
- Plugin API: unchanged at `1.0.0`.
- No Plugin SDK compatibility change is introduced in this revision. Existing third-party plugins built against Plugin API `1.0.0` do not require source changes because process enumeration continues to use the pre-existing optional capability/service contract.
- A plugin only receives the process UI when it advertises `ProcessEnumeration`; the host does not check for PlayStation 5 or any other platform name.

### Verification

- Reviewed `README.md`, `CHANGELOG.md`, every Markdown document under `docs/`, all C# source, XAML, project files, solution configuration, `Directory.Build.props`, and `.gitignore` from the supplied `0.1.1.rev2` baseline before changing code.
- Confirmed before implementation that `IProcessProvider` and `TargetProcess` already provide the required neutral contract, so no duplicate Core/Plugin SDK process API was introduced.
- Checked `CMD_PROC_LIST` framing and response layout against the public ps5debug-NG protocol reference: no request body, success status, process count, then 36-byte `name[32] + int32 pid` entries.
- Preserved the previously working PS5 connection handshake implementation; process enumeration is added after the established connection rather than replacing the connection-identification flow.
- Recorded the previously reported successful Windows application launch and live PS5 connection in test documentation rather than in the changelog as a code change.
- The preparation environment does not provide the .NET SDK or Windows WPF runtime, so final compilation, verification-executable execution, visual target-selection behavior, and live PS5 process enumeration remain Windows/runtime verification items documented under `docs/testing/` and `docs/plugins/PS5/`.

## TeeKay87's Memory Engine 0.1.1.rev2 - Reusable Button Styles and States

### Added

- Added `src/TeeKay87.MemoryEngine.App/Resources/Styles/ButtonStyles.xaml` as the central application-wide resource dictionary for standard WPF buttons.
- Added `ButtonBaseStyle`, which owns the shared `ControlTemplate`, typography, padding, alignment, cursor behavior, focus rendering, and interaction-state rendering used by ordinary buttons throughout the application.
- Added `PrimaryButtonStyle` for main affirmative workflow actions such as Connect, future Scan/Apply operations, and equivalent primary actions.
- Added `SecondaryButtonStyle` for neutral/supporting actions such as Disconnect and Reload Plugins.
- Added `DangerButtonStyle` as the shared semantic style for future destructive or potentially irreversible actions instead of introducing ad-hoc red buttons in individual views.
- Added an implicit application `Button` style based on `SecondaryButtonStyle` so newly introduced standard buttons cannot silently fall back to the operating-system WPF control chrome when no explicit semantic style is supplied.
- Added shared button interaction handling for normal, mouse-hover, pressed, keyboard-focused, defaulted, and disabled states.
- Added dedicated disabled button background, border, and text theme resources that preserve a dark-theme appearance while keeping disabled labels readable.
- Added central interaction-overlay and destructive-action theme resources used by the shared control template.
- Added `docs/ui/BUTTON_STYLES.md` documenting semantic button types, interaction states, resource ownership, usage examples, and extension rules for future views.
- Added `docs/testing/APP_0.1.1_REV2_VERIFICATION.md` documenting the reported disabled-button rendering problem, implementation checks, and required Windows visual/runtime verification.

### Changed

- Updated the host application revision from `0.1.1.rev1` to `0.1.1.rev2`; the application version remains `0.1.1` because this work extends the current development line rather than representing a larger completed version milestone.
- Updated `AppInfo.FeatureTitle` to `Reusable Button Styles and States`.
- Replaced the previous property-only implicit WPF `Button` styling with an application-owned reusable control template. This prevents Windows theme rendering from replacing the intended dark-theme presentation in disabled and other interaction states.
- Updated the existing Connect button to use `PrimaryButtonStyle`.
- Updated the existing Disconnect and Reload Plugins buttons to use `SecondaryButtonStyle`.
- Updated the current-development milestone text in the WPF shell so it describes the active reusable-button feature while retaining the existing note that PS5 process and memory services are not implemented yet.
- Updated `App.xaml` to host the shared button palette and merge the reusable button resource dictionary while preserving the existing TextBlock, TextBox, general color, and brush resources.
- Updated the early-development architecture guide with the rule that ordinary WPF control styling and interaction states must be centralized rather than copied into individual views.
- Updated `README.md` to describe the current application-wide button styling system and current `0.1.1.rev2` application state without turning README into revision history.
- Kept all layout-specific values that belong to the current view, such as button margins and minimum widths, in `MainWindow.xaml`; the shared style system owns appearance and state behavior rather than view layout.

### Removed

- Removed reliance on the operating-system WPF `Button` control template for application buttons.
- Removed the previous global `Button` style from `App.xaml` because standard button behavior is now provided by the dedicated shared resource dictionary.
- Removed the possibility that the current disabled Connect/Disconnect controls render with white system chrome or effectively unreadable button text solely because `IsEnabled` changes.
- No Core logic, Plugin SDK contracts, plugin-host behavior, mock-target behavior, PS5 protocol/connection behavior, or plugin-specific documentation was removed or changed.

### Version and Compatibility Notes

- This revision changes the **host application only**. The In-Memory Test Target remains `1.0.0.rev1`, the PlayStation 5 plugin remains `0.1.0.rev1`, and the Plugin API remains `1.0.0`.
- No plugin rebuild is required because of a Plugin SDK contract change; built-in plugins are rebuilt only as part of the normal solution build/package process.
- Capability badges remain informational elements rather than buttons and therefore intentionally do not use the shared button control template.

### Verification

- Reviewed `README.md`, `CHANGELOG.md`, all Markdown documentation under `docs/`, the complete C#/XAML/project/solution codebase, `Directory.Build.props`, and `.gitignore` before changing the code.
- Confirmed that the requested correction can be isolated to WPF presentation resources and current button declarations without modifying previously verified connection, plugin, memory, or discovery functionality.
- Validated XAML/XML well-formedness, resource references, shared-style usage, centralized application version information, unchanged independent plugin versions, project paths, and release-tree cleanliness during source preparation.
- The source-preparation environment does not provide the .NET SDK or Windows WPF runtime, so native compilation and visual state verification must be performed on the Windows development machine. The exact checks are documented in `docs/testing/APP_0.1.1_REV2_VERIFICATION.md`.

## TeeKay87's Memory Engine 0.1.1.rev1 - PS5 Plugin and Connection Foundation

### Added

- Added the first live-target platform project, `TeeKay87.MemoryEngine.Platform.PS5`, while keeping all PlayStation 5 and ps5debug-NG transport behavior outside Core and the WPF application.
- Added a PS5 plugin-local `Ps5PluginInfo` component as the authoritative source for the PS5 plugin id, name, independent semantic version, plugin revision, and targeted Plugin API version.
- Added the first PS5 plugin version as `0.1.0.rev1`. This version is independent from the host application's `0.1.1.rev1` version and from the ps5debug-NG payload version.
- Added PS5 plugin-defined connection fields for host/IP and command-server port. The plugin supplies the labels, descriptions, requirement flags, and default port (`744`) through the Plugin SDK instead of relying on PS5-specific controls in WPF.
- Added `Ps5DebugClient`, an asynchronous C# command-channel client for the connection-identification subset of the ps5debug-NG v1.3.0 wire protocol.
- Added request framing for the 12-byte little-endian ps5debug-NG command header using packet magic `0xFFAABBCC`.
- Added connection identification through protocol-version, platform-id, branding, firmware-version, and process-NOP/liveness commands.
- Added explicit validation that the remote platform id is PlayStation 5 (`5`) and that the returned human-readable brand begins with `ps5debug-NG` before a connected target session is exposed to the host.
- Added parsing and retention of the NUL-separated ps5debug-NG branding capability-level field for later feature negotiation without advertising future capabilities early.
- Added a five-second connection/identification timeout that remains linked to caller cancellation.
- Added PS5 connection/session lifetime handling so the plugin-owned TCP resources are disposed when Disconnect is selected, a plugin is reloaded, or the application closes.
- Added the PS5 plugin to the solution and to the application's platform-plugin build/copy pipeline so its entry assembly is deployed next to the mock plugin under the output `Plugins` directory.
- Added the initial formal `PluginApiInfo` compatibility version (`1.0.0`) to the Plugin SDK.
- Added independent plugin revision and targeted Plugin API version fields to `PluginMetadata`, including `DisplayVersion` formatting as `<plugin-version>.rev<plugin-revision>`.
- Added `TargetConnectionSettingDefinition` and extended `ITargetPlugin` with plugin-owned `ConnectionSettings` metadata.
- Added `ConnectionSettingViewModel` so the WPF application can render connection fields supplied by any plugin without checking platform names.
- Added generic Connect and Disconnect commands to the plugin view model, including required-field validation, asynchronous session creation, connection-state presentation, error presentation, cancellation, and session disposal.
- Added Plugin API compatibility, plugin revision, and connection-setting validation to `PluginHost` discovery.
- Added `MockPluginInfo` so the existing In-Memory Test Target also owns its plugin version/revision and targeted Plugin API version independently from the application.
- Added a loopback `Ps5ProtocolTestServer` to the dependency-free verification executable. It validates the PS5 connection command order, packet magic, zero-length connection request bodies, representative metadata responses, and NOP success status without requiring a physical console.
- Expanded the verification executable to cover independent version domains, PS5 plugin metadata and capability scope, PS5 connection settings, the ps5debug-NG connection handshake, and discovery of both built-in plugins.
- Added `docs/plugins/Mock/` as the dedicated documentation directory for the In-Memory Test Target.
- Added `docs/plugins/PS5/` as the dedicated documentation directory for the PlayStation 5 plugin.
- Added PS5-specific documentation covering the current plugin scope, ps5debug-NG connection protocol mapping, loopback verification, and the required live-target test procedure.
- Added `docs/testing/APP_0.1.1_REV1_VERIFICATION.md` for project-wide verification of this application revision.

### Changed

- Updated the application version from `0.1.0.rev2` to `0.1.1.rev1` after the `0.1.0` foundation was reported working on the Windows development machine. Revision numbering therefore restarts at `1` for the new application version.
- Updated `AppInfo` so the central application feature title is `PS5 Plugin and Connection Foundation`.
- Extended the Plugin SDK instead of adding PlayStation-specific connection models to Core. Connection requirements are now part of the neutral plugin contract and can be reused by future Windows, Xbox 360, emulator, remote-console, or other plugins.
- Updated `PluginMetadata` so plugin versions no longer need to mirror or be inferred from host application versions.
- Updated the mock plugin to implement the new connection-settings contract with an empty settings collection. Its deterministic process, memory map, memory read, and memory write behavior remains unchanged.
- Updated the WPF plugin list and selected-plugin details to display each plugin's independent version/revision and targeted Plugin API version.
- Updated the selected-plugin panel with a generic connection section that is driven by the plugin's `Connect` capability and connection-setting definitions.
- Updated plugin reload and application shutdown behavior so active plugin sessions are disposed before plugin view models and collectible plugin load contexts are released.
- Updated the application project to build and copy both built-in platform plugin entry assemblies.
- Updated the early-development architecture documentation with conservative host versioning guidance, independent plugin version/revision rules, Plugin API compatibility separation, and the requirement for dedicated per-plugin documentation directories.
- Updated the Plugin SDK architecture documentation to describe the implemented version domains, connection-setting model, compatibility checks, WPF integration, and per-plugin documentation structure.
- Updated `README.md` to describe the complete current `0.1.1.rev1` application, the two built-in plugins, PS5 connection usage, independent version domains, current verification suite, and current limitations without using README as revision history.

### Removed

- Removed the assumption that a platform plugin's displayed version is only a single `System.Version` value without a plugin revision.
- Removed the need for the WPF application to know that PlayStation 5 requires an IP/host field and port field; those requirements are now supplied entirely by the PS5 plugin.
- No previously verified mock memory behavior, plugin discovery behavior, WPF startup behavior, project structure, or Core memory contracts were removed.

### Protocol and Compatibility Notes

- The implemented PS5 connection subset was checked against the public ps5debug-NG v1.3.0 source at commit `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86` and its protocol reference.
- Connection metadata commands implemented in this revision return their documented payload directly: protocol version as a length-prefixed UTF-8 string, platform id as `uint16`, branding as a length-prefixed byte payload, and firmware as `uint16`. `CMD_PROC_NOP` returns the bit-swapped on-wire success word `0x80000000`.
- The PS5 plugin advertises only `TargetCapabilities.Connect` in this revision. Although ps5debug-NG supports process, memory, scanner, debugger, disassembly, assembly, and other operations, those capabilities remain disabled until their Memory Engine implementations exist and are verified.
- The Plugin API compatibility model is now explicit. The host currently accepts plugins with the same Plugin API major version and the same or an older minor version; plugins requiring a newer minor version are rejected during discovery.
- The Plugin API change in this revision is the first formally versioned contract baseline. Plugins compiled against the earlier unversioned development contract should be rebuilt against the current Plugin SDK.

### Verification

- Reviewed the complete supplied `0.1.0.rev2` documentation and source tree before implementation, including README, CHANGELOG, every Markdown document under `docs/`, all C# source, XAML, project files, solution configuration, and root build configuration.
- Preserved the previously verified In-Memory Test Target process and memory behavior and extended it only where required by the new neutral Plugin SDK metadata/connection contract.
- Checked the PS5 protocol constants, response layouts, and connection command behavior against the referenced ps5debug-NG v1.3.0 source/protocol documentation.
- Added an automated loopback protocol fixture so the request framing and response parsing can be reproduced without a console once the test project is built on a .NET 9 development machine.
- Revalidated solution paths, project-reference paths, XML/XAML well-formedness, plugin documentation separation, centralized application version information, independent plugin version information, Plugin API version consistency, and release-tree cleanliness during preparation.
- The source-preparation environment does not provide the .NET SDK or Windows WPF toolchain, so native compilation and WPF execution cannot be run there. A Windows build and live PS5 connection/disconnection test remain required before this version is considered fully runtime-verified. The required procedure is documented under `docs/testing/` and `docs/plugins/PS5/`.

## TeeKay87's Memory Engine 0.1.0.rev2 - WPF Application Namespace Compile Fix

### Added

- Added `docs/testing/REV2_WPF_APPLICATION_COMPILE_FIX.md` documenting the Visual Studio CS0118 failure, its cause, the exact corrective change, static verification, and the remaining native-build verification step.

### Changed

- Changed `App.xaml.cs` to alias `System.Windows.Application` as `WpfApplication` and inherit from that alias. This removes the compiler ambiguity with the existing `TeeKay87.MemoryEngine.App.Application` namespace without moving or renaming the `AppInfo` namespace.
- Updated the centralized `AppInfo` revision from `1` to `2`.
- Updated the centralized `AppInfo` feature title to `WPF Application Namespace Compile Fix`.
- Updated `README.md` so the current-build description and documentation index reflect revision 2 while continuing to describe the complete current application rather than acting as revision history.

### Removed

- No application functionality, plugin contracts, project structure, or previously implemented behavior was removed in this revision.

### Cause and Compatibility

- Revision 1 contained `public partial class App : Application` inside the `TeeKay87.MemoryEngine.App` namespace while also defining the child namespace `TeeKay87.MemoryEngine.App.Application`. In that scope, the identifier `Application` could bind to the namespace rather than `System.Windows.Application`, producing compiler error CS0118.
- The fix is intentionally limited to explicit WPF type resolution. The existing namespace hierarchy, solution layout, Core, Plugin SDK, mock plugin, plugin host, and WPF UI remain otherwise unchanged.

### Verification

- Reviewed all existing Markdown documentation and every source, XAML, project, solution, and root build-configuration file from the rev1 baseline before applying the change.
- Confirmed statically that the ambiguous inheritance declaration is removed and that `App.xaml` continues to reference the same `TeeKay87.MemoryEngine.App.App` class.
- Revalidated XML/XAML well-formedness, project references, centralized revision declarations, documentation consistency, and archive contents after the change.
- The preparation environment still does not provide the .NET SDK or Windows WPF toolchain, so a native build could not be executed there. Final Visual Studio build verification remains required and is documented in `docs/testing/REV2_WPF_APPLICATION_COMPILE_FIX.md`.

## TeeKay87's Memory Engine 0.1.0.rev1 - Initial Architecture and Plugin Foundation

### Added

- Added the first structured solution, `TeeKay87.MemoryEngine.sln`, with separate projects for the WPF application, shared Core, public Plugin SDK, development platform plugin, and verification tests.
- Added `Directory.Build.props` so nullable reference types, explicit `using` requirements, current C# language support, deterministic builds, and warning-as-error behavior are applied consistently across projects.
- Added a centralized `AppInfo` component as the authoritative source for the application title, version, revision, current feature title, display version, and window title.
- Added the initial `TeeKay87.MemoryEngine.PluginSdk` project with platform-neutral contracts and models:
  - `ITargetPlugin` for platform plugin entry points;
  - `ITargetSession` for connected target sessions;
  - service-based session capability access through `GetService<TService>()`;
  - `IProcessProvider` and `IForegroundProcessProvider`;
  - `IMemoryReader`, `IMemoryWriter`, and `IMemoryMapProvider`;
  - `TargetSessionExtensions.GetRequiredService<TService>()` for explicit required-service access;
  - `PluginMetadata`, `TargetConnectionOptions`, `TargetArchitecture`, `TargetProcess`, and `MemoryRegion` models;
  - CPU architecture, endianness, memory protection, and standard memory-value type definitions.
- Added a `TargetCapabilities` flags model covering the planned cross-platform capability surface, including target connection, process discovery, memory operations, scanning, debugger features, assembly/disassembly, and cheat functionality.
- Added the initial Core plugin-hosting infrastructure:
  - top-level plugin-directory discovery;
  - isolated collectible `AssemblyLoadContext` loading;
  - dependency resolution through `AssemblyDependencyResolver`;
  - shared Plugin SDK assembly identity between the host and plugins;
  - public plugin type discovery;
  - plugin metadata validation;
  - duplicate plugin-id rejection;
  - structured discovery errors;
  - unload support when plugins are reloaded or the host is disposed.
- Added `TeeKay87.MemoryEngine.Platform.Mock`, a deterministic in-memory development plugin that implements the same SDK contracts intended for live targets.
- Added a mock target process and memory region with deterministic Health, Ammo, and Money values so future Core functionality can be developed and regression-tested without a physical console.
- Added functional mock implementations for process enumeration, foreground-process discovery, memory-region enumeration, memory reads, and memory writes.
- Added a WPF/MVVM application shell that:
  - loads plugins from the output `Plugins` directory;
  - lists discovered platform plugins;
  - displays platform, backend, plugin version, target architecture, assembly path, and advertised capabilities;
  - reloads plugins without restarting the application;
  - displays plugin-discovery failures;
  - obtains all displayed application version information from `AppInfo`.
- Added build integration that builds the mock platform plugin with the application and places its plugin assembly under the application's output `Plugins` directory.
- Added a dependency-free executable verification project covering plugin metadata, capability declarations, process discovery, foreground process discovery, memory maps, memory reads, memory writes with read-back verification, and runtime plugin discovery.
- Added `.gitignore` rules for common .NET, Visual Studio, JetBrains, test, coverage, and operating-system generated files.
- Added detailed current-state documentation to `README.md`.
- Added `docs/architecture/PLUGIN_SDK_FOUNDATION.md` documenting the implemented plugin boundary, service model, capability model, discovery behavior, and extension rules.
- Added `docs/testing/REV1_FOUNDATION_VERIFICATION.md` documenting verification scope and results for this revision.

### Changed

- Reorganized the original single-project WPF template into the multi-project structure defined by the early-development architecture guide.
- Replaced the original empty `MainWindow` with the first capability-driven application shell.
- Replaced the original `TeeKay87_s_Memory_Engine` namespace with the structured `TeeKay87.MemoryEngine.*` namespace hierarchy used by the new solution.
- Moved platform-neutral contracts out of the WPF application so future PS5, Windows, Xbox 360, emulator, and memory-dump implementations can target the same Plugin SDK.
- Established explicit separation between platform identity and backend identity in plugin metadata. A plugin can therefore identify a target as PlayStation 5 while separately identifying ps5debug-NG as the backend.
- Established session services as the extension mechanism for target operations so unsupported features are absent instead of requiring platform-specific conditionals or dummy implementations.

### Removed

- Removed the original root-level single WPF project and its empty template source files after their application role was migrated into `src/TeeKay87.MemoryEngine.App`.
- Removed template-generated unused namespace imports from the original WPF code as part of the project restructure.
- Removed the now-unnecessary `docs/.gitkeep` placeholder because the documentation tree contains real architecture and verification documents.

No previously verified application functionality was removed because the supplied baseline contained only the unimplemented WPF template.

### Compatibility and Development Impact

- The project now requires opening/building `TeeKay87.MemoryEngine.sln` rather than the original root-level `.csproj`.
- Future platform implementations must reference `TeeKay87.MemoryEngine.PluginSdk` and expose an `ITargetPlugin` implementation instead of adding platform-specific behavior directly to the WPF application or Core.
- The current plugin loader expects plugin entry assemblies to be placed directly in the application's `Plugins` directory. More advanced packaging or manifests may be added later without changing the Core/platform separation established here.
- The current revision intentionally does not include ps5debug-NG code. PS5 transport/protocol work remains isolated to the upcoming PS5 platform plugin milestone.

### Verification

- Reviewed the complete supplied baseline before implementation, including `README.md`, `CHANGELOG.md`, the complete early-development architecture document, and every source/project file.
- Verified the new repository structure, project references, XML project files, XAML XML structure, centralized version declarations, plugin contract separation, and documentation consistency with automated repository checks.
- Added executable runtime verification tests for the mock target and plugin host so the same checks can be run on a Windows development machine with the .NET 9 SDK.
- The execution environment used to prepare this revision does not contain a .NET SDK or Windows WPF toolchain, so a native `dotnet build` and execution of the WPF application could not be performed in that environment. This limitation is recorded in `docs/testing/REV1_FOUNDATION_VERIFICATION.md` rather than being represented as a successful runtime build.
