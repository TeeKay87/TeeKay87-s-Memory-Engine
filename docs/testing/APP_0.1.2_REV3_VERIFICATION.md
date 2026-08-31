# 0.1.2.rev3 Scan Progress Binding Fix Verification

## Purpose

This document defines verification for **TeeKay87's Memory Engine 0.1.2.rev3 - Scan Progress Binding Fix**.

Rev3 is a narrow WPF host correction made after the rev2 dependency-free verification executable passed all 13 checks but the main application failed during startup. It does not change the shared scanner algorithm, PS5 cancellation fix, ps5debug-NG transport, Plugin SDK, plugin capabilities, themes, result actions, decimal Raw Memory Write behavior, or workspace geometry.

## Reported Runtime Failure

After `0.1.2.rev2` returned `All 13 checks passed.`, starting the WPF application produced:

```text
System.InvalidOperationException:
A TwoWay or OneWayToSource binding cannot work on the read-only property
'ScanProgressPercentage' of type
'TeeKay87.MemoryEngine.App.ViewModels.PluginViewModel'.
```

The exception occurred while WPF was attaching the bindings for `MainWindow` during application startup.

## Root Cause

`PluginViewModel.ScanProgressPercentage` is application-owned output state:

```csharp
public double ScanProgressPercentage
{
    get => _scanProgressPercentage;
    private set => SetProperty(ref _scanProgressPercentage, value);
}
```

The view must only read that value. `ProgressBar.Value`, however, was bound without an explicit mode:

```xml
Value="{Binding SelectedPlugin.ScanProgressPercentage}"
```

The binding therefore used the dependency property's default source-update behavior and WPF rejected the attempt to bind back to a property without a public setter. This is the same class of binding error previously corrected for the output-only Raw Memory Read result in host rev14.

## Correction

The status-bar progress binding is now explicit:

```xml
Value="{Binding SelectedPlugin.ScanProgressPercentage, Mode=OneWay}"
```

`ScanProgressPercentage` remains encapsulated with its private setter. No ViewModel setter was made public merely to satisfy the binding engine.

The correction is intentionally local to the WPF presentation binding. Scanner progress calculation, elapsed-time tracking, Core `MemoryScanProgress`, cancellation behavior, and PS5 transport framing are unchanged from rev2.

## Version Boundaries

```text
Host application:             0.1.2.rev3
PlayStation 5 plugin:         0.1.0.rev6
In-Memory Test Target plugin: 1.0.0.rev1
Plugin API:                   1.0.0
```

No plugin revision changes because no plugin code changes in rev3.

## Required Windows Verification

1. Open `TeeKay87.MemoryEngine.sln`.
2. Run **Build -> Rebuild Solution**.
3. Confirm zero errors and zero warnings.
4. Run the verification executable:

```powershell
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

5. Confirm all existing checks still pass:

```text
All 13 checks passed.
```

6. Start `TeeKay87.MemoryEngine.App`.
7. Confirm the main window opens without the `ScanProgressPercentage` binding exception.
8. Connect to the Mock target and start a First Scan.
9. Confirm the status-bar progress bar becomes visible and advances.
10. Confirm elapsed scan time remains visible and no binding exception is produced when progress changes.
11. Confirm New Scan resets progress/time presentation as documented for rev2.

## Required Live PS5 Cancellation Closure

After startup/progress verification succeeds, continue the rev2 live regression that was blocked by the startup exception:

1. connect to the PS5 and set the game's `eboot.bin` as Active Target;
2. start a First Scan that runs long enough to cancel;
3. choose **Cancel Scan** while a scan is active;
4. wait for cancellation to complete;
5. do **not** disconnect;
6. choose **Refresh** and confirm process enumeration succeeds on the same connection;
7. choose **New Scan** and start another First Scan without reconnecting;
8. confirm no `unexpected status` error occurs and the scanner works normally.

## Pass Criteria

```text
Windows rebuild:                              PASS
Verification executable (13/13):              PASS
WPF startup without binding exception:        PASS
Status-bar progress updates:                   PASS
Elapsed scan time updates:                     PASS
New Scan progress reset:                       PASS
Live PS5 Cancel -> Refresh without reconnect:  PASS
Live PS5 Cancel -> New Scan without reconnect: PASS
Previously verified scanner behavior:          PASS
Previously verified target-access behavior:    PASS
```

The required Windows startup and live PS5 cancellation/session-reuse steps were subsequently completed successfully; see the Completed Runtime Result below.

## Completed Runtime Result

Rev3 was subsequently runtime-verified successfully on the Windows development machine and a real PS5.

Reported results:

```text
Verification executable:                  All 13 checks passed
WPF startup:                              PASS
Status-bar progress / elapsed time:       PASS
Cancel Scan -> Refresh same PS5 session:  PASS
Cancel Scan -> New Scan same PS5 session: PASS
Real First Scan/value editing:            PASS
```

A real `eboot.bin` scan with **10,105 loaded memory regions** searched 4-byte Int32 Exact Value `10002`, returned **266 results**, and required approximately **07:04.3**. The subsequent Next Scan was reported as effectively instant. This established the primary rev4 optimization target: First Scan memory acquisition on PS5, not candidate refinement.

The Scan Results context menu also exposed a presentation defect during this verification: under Dimmed/Dark, WPF's default menu template showed a light operating-system icon/checkmark gutter. Host rev4 replaces that default context-menu chrome.

**Final rev3 runtime status: PASS.**
