# 0.1.2.rev1 Initial Memory Scanner Foundation Verification

## Purpose

This document defines the verification requirements for **TeeKay87's Memory Engine 0.1.2.rev1 - Initial Memory Scanner Foundation**.

This revision begins the scanner milestone only after the complete `0.1.1` low-level target-access chain was live-verified against a real PlayStation 5. It also extends the presentation-only theme palette so button and tooltip/hint colors are controlled by the active external theme.

## Version Boundaries

```text
Host application:             0.1.2.rev1
Host feature title:           Initial Memory Scanner Foundation
PlayStation 5 plugin:         0.1.0.rev5 (unchanged)
In-Memory Test Target plugin: 1.0.0.rev1 (unchanged)
Plugin API:                   1.0.0 (unchanged)
```

No platform plugin implementation or public Plugin SDK contract is changed by this revision.

## Scanner Scope

The first implemented scan mode is intentionally limited to:

```text
Value Type: 4 Bytes / Int32
Scan Type:  Exact Value
First Scan
Next Scan
New Scan
Cancel Scan
```

The scanner lives in Core and consumes only neutral contracts/models:

```text
TargetProcess
TargetArchitecture
MemoryRegion
IMemoryReader
```

## Deterministic Verification

The verification executable adds a shared scanner check against the Mock plugin.

Expected sequence:

1. connect to the Mock target;
2. retrieve its process and memory map;
3. run First Scan for Int32 `30`;
4. confirm `MockTargetLayout.AmmoAddress` is returned with Current = `30` and no Previous value;
5. write Int32 `25` to the Ammo address through `IMemoryWriter`;
6. run Next Scan for `25` against the previous candidate set;
7. confirm exactly one result remains;
8. confirm Address = Ammo address;
9. confirm Current = `25`;
10. confirm Previous = `30`;
11. confirm no scanner memory-read failures were reported.

## Windows Build Verification

1. Open the solution in Visual Studio.
2. Run **Build -> Rebuild Solution**.
3. Confirm zero compile errors and zero warnings treated as errors.
4. Run `TeeKay87.MemoryEngine.Tests`.
5. Confirm every verification check passes, including **Shared 4-byte exact scanner and refinement**.
6. Start the WPF application.
7. Confirm the title/version surfaces show `0.1.2.rev1`.
8. Confirm the PS5 plugin still reports `0.1.0.rev5`.
9. Confirm Mock plugin still reports `1.0.0.rev1`.
10. Confirm Plugin API remains `1.0.0`.

## Scanner UI Verification — Mock Target

Use the deterministic Mock target first.

1. Select **In-Memory Test Target**.
2. Connect.
3. Select the Mock process and choose **Set Active Target**.
4. Confirm its memory map loads.
5. Enter `30` in Value.
6. Confirm Scan Type displays **Exact Value**.
7. Confirm Value Type displays **4 Bytes**.
8. Choose **First Scan**.
9. Confirm the scan completes without error.
10. Confirm the result count is at least one.
11. Confirm address `0x0000000010000104` appears with Value `30`, Type `4 Bytes`, and region/module context.
12. Confirm Previous is blank on the First Scan result.
13. Use an existing memory-write path or deterministic test workflow to change Ammo from `30` to `25`.
14. Enter `25` in Value.
15. Choose **Next Scan**.
16. Confirm the Ammo address remains and its Previous column shows `30` while Value shows `25`.
17. Choose **New Scan**.
18. Confirm temporary results clear while Saved Addresses remains unaffected.

## Scanner UI Verification — Live PS5

The first live PS5 scan should use a value likely to produce a manageable result set rather than an extremely common value such as zero.

1. Start ps5debug-NG and a game.
2. Connect to the PS5.
3. Select `eboot.bin` and choose **Set Active Target**.
4. Confirm the memory map loads.
5. Enter a known 4-byte integer game value when one is available, or use a sufficiently distinctive test value.
6. Choose **First Scan**.
7. Confirm progress/status updates while the scan runs.
8. Confirm the application remains responsive enough to choose **Cancel Scan** while scanning.
9. If cancelled, confirm the target remains connected and a new First Scan can be started.
10. Complete a First Scan and confirm Scan Results populates.
11. Change the game value.
12. Enter the new value and choose **Next Scan**.
13. Confirm the result count decreases or remains logically consistent with the new exact value.
14. Confirm the Previous column is populated after refinement.
15. Confirm Raw Memory Read/Write diagnostics remain usable after the scan completes.

### Safety-Limit Verification

A deliberately broad value may produce more than the current Core safety limit of 2,000,000 candidates. If that occurs, confirm the scan stops with a clear error rather than exhausting application memory.

This is not required for every live test and should not be forced by scanning a value known to create an excessive result set.

## Theme Verification

### Button palette

For each bundled theme — Light, Dimmed, Dark:

1. switch the theme while the application is open;
2. confirm Primary buttons update their background, border, and text color immediately;
3. confirm Secondary buttons update independently from general panel colors;
4. confirm Danger buttons remain readable;
5. confirm disabled button presentation remains readable;
6. confirm the Light theme's Primary action buttons are visibly lighter than in rev17 and no longer appear excessively dark.

### Tooltip / hint palette

For each theme:

1. hover a control with a tooltip, such as the Safe Write Test button or Raw Memory Read Length field;
2. confirm the tooltip uses the application's themed background rather than the operating-system white tooltip chrome in Dimmed/Dark;
3. confirm tooltip text uses the theme-defined tooltip text color;
4. confirm tooltip border follows the theme-defined tooltip border color;
5. switch themes and verify the tooltip palette changes accordingly.

## Preservation Requirements

This revision must preserve the already verified behavior of:

- PS5 connection and identification;
- process enumeration;
- explicit Selected Process versus Active Target separation;
- memory-map enumeration;
- raw memory reads;
- raw memory writes;
- Safe Write Test and immediate read-back verification;
- Mock memory read/write behavior;
- plugin discovery and independent versioning;
- Plugin API `1.0.0`;
- 34-unit standard interactive-control height;
- rev15 TextBox content-padding correction;
- current splitters and workspace geometry;
- Light/Dimmed/Dark theme ids and persisted theme selection;
- Scan Results versus Saved Addresses separation.

## Pass Criteria

```text
Windows rebuild:                         PASS
Verification executable:                 PASS
Mock First Scan exact Int32:             PASS
Mock Next Scan refinement:               PASS
Current/Previous result values:          PASS
New Scan reset:                          PASS
Cancel Scan keeps session healthy:       FAIL on live PS5; fixed in rev2
Live PS5 First Scan:                     PASS
Live PS5 Next Scan:                      PASS
Theme-controlled Primary buttons:        PASS
Theme-controlled Secondary buttons:      PASS
Theme-controlled tooltip background:     PASS
Theme-controlled tooltip text:           PASS
Verified 0.1.1 target-access behavior:   PASS
```

The original pre-runtime verification boundary above is preserved for historical context. The recorded runtime outcome below supersedes that pending status.

## Recorded Runtime Outcome — 2026-08-31

The rev1 verification was subsequently performed on Windows and against both the deterministic Mock target and a real PlayStation 5.

Recorded results:

- `dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj` completed with **All 12 checks passed**;
- Mock First Scan for `30` returned the expected Ammo address `0x10000104`;
- the Mock value was changed to `25` through Raw Memory Write and Next Scan retained the same address with Current = `25` and Previous = `30`;
- New Scan and the scanner UI workflow behaved as intended during normal Mock use;
- Light, Dimmed, and Dark button/tooltip palette changes were confirmed visually;
- the shared scanner completed a real PS5 scan, found an in-game money value, and the located value could be changed successfully.

A cancellation-specific live PS5 defect was also isolated after the successful scanner test. Cancelling a scan could leave the ps5debug-NG command TCP stream desynchronized. A subsequent scan or process Refresh could then fail with an unexpected status value such as `0xC0F6410A`. Performing Disconnect -> Connect created a fresh command stream and restored scanning. Repeated testing narrowed the trigger to **Cancel Scan** rather than New Scan itself.

The Core First Scan/Next Scan functionality is therefore runtime-proven, but rev1's PS5 cancellation/session-reuse behavior is **FAIL** and is superseded by the transaction-preservation fix in `0.1.2.rev2` / PS5 plugin `0.1.0.rev6`.
