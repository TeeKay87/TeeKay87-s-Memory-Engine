# 0.1.2.rev4 PS5 Scan Acceleration and Process Pause Verification

## Purpose

This document defines verification for **TeeKay87's Memory Engine 0.1.2.rev4 - PS5 Scan Acceleration and Process Pause**.

Rev4 follows successful live verification of rev3 and addresses two observations from real PS5 scanner use:

1. the generic shared First Scan was correct but slow because the host transferred readable process memory over repeated `CMD_PROC_READ` requests;
2. the Scan Results context menu still exposed an operating-system light icon/checkmark gutter in Dimmed/Dark themes.

Rev4 also implements the requested optional process pause during scans.

## Verified Rev3 Baseline

Before rev4 work, the following rev3 results were reported from the Windows development machine and real PS5:

```text
Verification executable:                  13/13 PASS
WPF startup after progress binding fix:   PASS
Scan status/progress/elapsed time:         PASS
Cancel Scan -> Refresh same connection:    PASS
Cancel Scan -> New First Scan:             PASS
Real PS5 4-byte First Scan:                PASS
Real value finding/editing:                PASS
Next Scan refinement:                      PASS / effectively instant
```

The measured full-process First Scan example was:

```text
Active Target: eboot.bin
Memory regions: 10,105
Value:          10002
Value Type:     4 Bytes / Int32
Scan Type:      Exact Value
Results:        266
Elapsed:        approximately 07:04.3
```

That runtime measurement is the performance baseline for rev4.

## Version Boundaries

```text
Host application:             0.1.2.rev4
PlayStation 5 plugin:         0.1.0.rev7
In-Memory Test Target plugin: 1.0.0.rev1
Plugin API:                   1.1.0
```

The Plugin API minor version changes because the public optional `INativeValueScanner` and `IProcessControl` contracts are new. The Mock plugin intentionally remains on API `1.0.0`, which must remain compatible with the `1.1.0` host.

## Code/Architecture Verification

Confirm the following boundaries before runtime testing:

- `AppInfo` is the only host application version source and reports `0.1.2.rev4` / `PS5 Scan Acceleration and Process Pause`;
- `Ps5PluginInfo` reports `0.1.0.rev7` targeting Plugin API `1.1.0`;
- Mock remains `1.0.0.rev1` targeting Plugin API `1.0.0`;
- `PluginApiInfo.CurrentVersionText` is `1.1.0`;
- `INativeValueScanner` and `IProcessControl` live in Plugin SDK;
- TurboScan capability/authentication/START/GET/END packet handling remains inside the PS5 plugin;
- process-stop packet handling remains inside the PS5 plugin;
- generic native-result normalization remains in Core;
- Next Scan remains the existing shared Core refinement path;
- no literal PlayStation platform check is used by WPF to show the pause control or select the native scanner;
- capability/service lookup controls availability.

## Windows Build

1. Extract the complete rev4 ZIP into a clean project directory.
2. Open `TeeKay87.MemoryEngine.sln` in Visual Studio 2022.
3. Run **Build -> Rebuild Solution**.
4. Confirm zero compile errors.
5. Resolve any warnings because warnings are treated as errors project-wide.

## Verification Executable

Run:

```powershell
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

The current revision contains 16 checks. Expected result:

```text
TeeKay87's Memory Engine verification
==========================================
PASS  Plugin API and independent plugin versions
PASS  Mock plugin metadata and capabilities
PASS  Mock target process and memory map
PASS  Mock target memory read and write
PASS  Shared 4-byte exact scanner and refinement
PASS  PS5 plugin metadata and connection settings
PASS  PS5 ps5debug-NG connection handshake
PASS  PS5 process enumeration protocol
PASS  PS5 memory-map enumeration protocol
PASS  PS5 raw memory-read protocol
PASS  PS5 scan cancellation preserves command stream
PASS  PS5 native exact-value scan protocol
PASS  PS5 native scan cancellation preserves command stream
PASS  PS5 process suspend and resume protocol
PASS  PS5 raw memory-write and read-back protocol
PASS  Plugin host assembly discovery

All 16 checks passed.
```

The three new rev4 checks specifically verify:

- correct TurboScan capability negotiation, authorization, multi-segment START, resident summary, GET records, END cleanup, and Core normalization;
- cancellation requested while TurboScan START is in flight is returned only after the current response is drained and the resident session is closed, after which process-list enumeration succeeds on the same TCP session;
- process-control commands send state `1` then state `0` for the expected process.

## WPF Startup and Theme Regression

1. Start `TeeKay87.MemoryEngine.App`.
2. Confirm the main window opens without binding exceptions.
3. Switch between Light, Dimmed, and Dark.
4. Confirm existing buttons and tooltips remain correctly themed.
5. Connect to Mock or PS5 and produce at least one Scan Result.
6. Right-click a result row in Dimmed.
7. Confirm the context menu has no white/light operating-system gutter at the left edge.
8. Confirm menu background, text, separator, hover state, and disabled state remain legible.
9. Repeat in Dark and Light.

## Capability/UI Verification

With the Mock plugin selected:

1. confirm **Pause target while scanning** is not shown because Mock does not advertise both process-control capabilities;
2. confirm the existing shared First Scan still works;
3. confirm Next Scan still works.

With the PS5 plugin selected/connected:

1. open Plugin details;
2. confirm plugin version `0.1.0.rev7`;
3. confirm `NativeValueScanning`, `ProcessSuspend`, and `ProcessResume` are advertised;
4. confirm **Pause target while scanning** appears in the Scan panel;
5. confirm it is unchecked by default;
6. confirm it cannot be changed while a scan is active.

## Live PS5 Native First Scan Performance

Use the same or a comparable game/value used for the rev3 performance measurement.

1. connect to PS5 and select `eboot.bin`;
2. choose **Set Active Target**;
3. confirm a non-zero memory map loads;
4. leave **Pause target while scanning** Off;
5. enter a known 4-byte Int32 value;
6. choose **First Scan**;
7. confirm the status reports a native target scan (which means the connected server passed TurboScan capability negotiation);
8. confirm the status-bar progress bar is indeterminate rather than a fake percentage;
9. confirm elapsed time updates;
10. record elapsed time and result count;
11. compare the time against the rev3 baseline of approximately `07:04.3` for the recorded 10,105-region/10002 scan when the same scenario is available;
12. confirm result addresses and values appear normally;
13. change the target value naturally in the game;
14. enter the new value and run **Next Scan**;
15. confirm Next Scan remains fast and correctly refines the native First Scan candidate set.

The main performance pass criterion is a material reduction in First Scan time. A precise threshold is not hard-coded until live measurements establish the native command's real performance across games.


## Recorded Live rev4 Results

The following results were reported from the real PS5 runtime verification after the deterministic suite passed 16/16:

- native First Scan completed in **15.1 seconds** with **884** matches in one measured run;
- a second native First Scan completed in **14.5 seconds** with **35,278** matches;
- this replaced the earlier rev3 full-process measurement of approximately **07:04.3**, confirming the target-side TurboScan acceleration delivered a major practical improvement;
- the rev4 shared-Core Next Scan refinement measured **6.6 seconds** when narrowing 884 candidates to 8;
- **Pause target while scanning** visibly paused the game and the game resumed immediately after a normally completed scan;
- Cancel Scan was observed to behave as a deferred cancellation request: the current native scan finishes to its safe protocol boundary before the host reports cancellation. This matches the intended stream-preservation design rather than providing an asynchronous target-side abort.

These measurements establish the performance baseline for the rev5 resident TurboScan refinement work.

## Live Native-Scan Cancellation / Session Reuse

1. start a native First Scan expected to run long enough to press Cancel;
2. choose **Cancel Scan**;
3. allow the current native command to reach its safe cancellation boundary;
4. confirm the UI reports cancellation;
5. do **not** Disconnect;
6. choose **Refresh**;
7. confirm process enumeration succeeds on the same session;
8. choose **New Scan**;
9. start another First Scan;
10. confirm no `unexpected status` or command-stream corruption occurs.

TurboScan uses a shared command stream and the current START/GET transactions must be consumed to a defined protocol boundary before cancellation can return. Cancel may therefore wait for the current in-flight TurboScan transaction to finish, but the plugin must close any established resident session with END and leave the connection synchronized/reusable.

## Live Pause Target While Scanning

### Normal completion

1. ensure no important unsaved game progress is at risk;
2. enable **Pause target while scanning**;
3. start First Scan;
4. confirm the target game visibly stops progressing while the scan is active;
5. allow the scan to complete;
6. confirm the game automatically resumes;
7. confirm no process-resume error appears;
8. perform a normal target operation afterward to confirm session health.

### Cancellation

1. enable **Pause target while scanning**;
2. begin a scan;
3. choose **Cancel Scan**;
4. wait for cancellation to return at the safe command boundary;
5. confirm the game resumes automatically afterward;
6. choose **Refresh** and confirm the same session remains usable.

### Pause Off regression

1. disable **Pause target while scanning**;
2. begin another scan;
3. confirm the game continues running during the scan;
4. confirm the option remains Off for a fresh application launch unless explicitly changed in the current session. Rev4 does not persist this diagnostic/user preference.

## Pass Criteria

```text
Windows rebuild:                                  PASS
Verification executable (16/16):                  PASS
WPF startup:                                      PASS
Mock generic scanner regression:                  PASS
ContextMenu Light/Dimmed/Dark:                    IMPLEMENTED; live visual recheck recommended
PS5 native First Scan correctness:                PASS
PS5 native First Scan performance improvement:    PASS
PS5 native candidate -> Core Next Scan:            PASS
Native Cancel -> Refresh same session:             deterministic PASS; live deferred-cancel behavior observed
Native Cancel -> new scan same session:            deterministic PASS; live recheck recommended
Pause checkbox Off by default:                    PASS
Target pauses when checkbox is enabled:           PASS
Target resumes after normal completion:           PASS
Target resumes after cancellation:                deterministic coverage; live recheck recommended
Existing read/write/map/process behavior:         PASS
```

## Notes for Follow-up

If native First Scan is still unexpectedly slow, record exact elapsed time, game, region count, result count, and whether pause was enabled before changing scanner architecture again.

If target resume fails, do not treat the scan as cleanly verified. Record the exact scan error/status and restore the target state before additional testing.
