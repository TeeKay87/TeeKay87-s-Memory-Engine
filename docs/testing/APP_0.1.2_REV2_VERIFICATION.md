# 0.1.2.rev2 Scanner Workflow and Cancellation Reliability Verification

## Purpose

This document defines verification for **TeeKay87's Memory Engine 0.1.2.rev2 - Scanner Workflow and Cancellation Reliability**.

The revision is intentionally focused on hardening the initial scanner after the first live PlayStation 5 use and improving the active scan/result workflow. The most important regression is that **Cancel Scan must no longer corrupt the ps5debug-NG command stream**.

Current version boundaries:

```text
Host application:             0.1.2.rev2
PlayStation 5 plugin:         0.1.0.rev6
In-Memory Test Target plugin: 1.0.0.rev1
Plugin API:                   1.0.0
```

## What Changed

The revision adds or changes:

- ps5debug-NG read/write transactions that remain protocol-complete once started, even if the caller requests cancellation;
- a deterministic cancel-during-read -> same-session process-list regression test;
- scan status/errors moved from the Scan panel to the application status bar;
- a status-bar progress bar and elapsed scan time;
- compact Scan Results addresses without fixed-width leading zeroes;
- Scan Results right-click actions for Copy address, Copy value, and Change value;
- Change value transfers the selected address into Raw Memory Write;
- Raw Memory Write now accepts one signed 4-byte Int32 value in decimal form rather than arbitrary hexadecimal byte sequences.

The shared Core scan algorithm, its 256 KiB read chunk size, result safety limit, four-byte alignment, and current Exact Value / 4 Bytes feature scope are unchanged.

## Why the Cancellation Fix Is Required

Live rev1 testing demonstrated this failure sequence:

```text
PS5 First Scan
    ↓
CMD_PROC_READ is in flight
    ↓
Cancel Scan
    ↓
NetworkStream read cancelled before full response was consumed
    ↓
remaining target bytes stay on shared TCP stream
    ↓
next command reads those bytes as a status word
    ↓
unexpected ps5debug-NG status / session unusable until reconnect
```

The user reproduced the problem after Cancel Scan and confirmed that Disconnect -> Connect restored scanning. This indicates command-stream framing rather than scanner candidate state.

Rev2 changes the PS5 transport cancellation boundary:

```text
check cancellation before command starts
    ↓
start CMD_PROC_READ / CMD_PROC_WRITE
    ↓
complete the full framed protocol transaction
    ↓
return to Core
    ↓
Core observes cancellation before next scanner request
```

Cancel may therefore wait for the current target read to finish. That short delay is intentional and preferable to corrupting the shared command connection.

## Deterministic Verification Executable

Run from the repository root:

```powershell
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

Expected final result:

```text
All 13 checks passed.
```

The new regression check must appear as:

```text
PASS  PS5 scan cancellation preserves command stream
```

The test deliberately delays a loopback `CMD_PROC_READ`, requests cancellation after the server has received that read command, waits for the shared Core scan to observe cancellation, and then immediately calls `IProcessProvider.GetProcessesAsync` on the same PS5 session. The process list must still parse correctly.

This test proves the specific protocol-stream invariant without requiring a physical console.

## Windows Build Verification

1. Open `TeeKay87.MemoryEngine.sln` in Visual Studio.
2. Run **Build -> Rebuild Solution**.
3. Confirm zero build errors and zero warnings, because warnings are treated as errors project-wide.
4. Start the application and confirm the title shows `0.1.2.rev2`.
5. Open Plugin details for PlayStation 5 and confirm plugin version `0.1.0.rev6`.
6. Confirm Mock remains `1.0.0.rev1` and Plugin API remains `1.0.0`.

## Mock Scanner Regression

Use the In-Memory Test Target:

1. connect;
2. set `TestGame.exe` as Active Target;
3. First Scan for `30` using Exact Value / 4 Bytes;
4. confirm the result address is displayed as `0x10000104`, not `0x0000000010000104`;
5. confirm Value = `30` and Region / Module = `TestGame.exe`;
6. right-click the row and choose **Copy address**; paste into a text field and confirm `0x10000104`;
7. right-click and choose **Copy value**; paste and confirm `30`;
8. right-click and choose **Change value**;
9. expand Raw Memory Write and confirm Address = `0x10000104` while the decimal Value field is empty;
10. enter decimal `25` and choose **Write + Verify**;
11. confirm verification passes;
12. set Scan Value to `25` and choose **Next Scan**;
13. confirm the same address remains with Value = `25` and Previous = `30`;
14. choose **New Scan** and confirm temporary results/progress state reset.

## Raw Memory Write Decimal Verification

The temporary diagnostic now edits one signed Int32 value.

Verify with the Mock target:

1. set Address to `0x10000104`;
2. enter `25` in **Value (decimal)**;
3. choose **Write + Verify**;
4. confirm Requested and Read-back represent the same four bytes and Verification = PASS;
5. read the address or scan for `25` to confirm the value changed;
6. test `-1` and confirm the operation is accepted as a signed Int32;
7. test an out-of-range value such as `2147483648` and confirm the host rejects it before invoking the writer;
8. confirm the address itself remains hexadecimal.

The host must encode/decode according to `TargetArchitecture.Endianness`; the UI must not assume little-endian globally.

## Status Bar Progress and Elapsed Time

Start a scan long enough to observe progress.

Confirm:

1. there is no longer a dedicated scan status/error block beneath the Scan buttons;
2. current scan text is shown in the application status bar;
3. a progress bar is visible while a scan is active;
4. progress advances as scanner work is reported;
5. elapsed time updates while the scan is running;
6. when the scan completes, the progress reaches 100% and the final elapsed duration remains visible;
7. after **New Scan**, progress/time presentation resets;
8. if a scan fails, the scan status in the status bar uses the error presentation rather than silently hiding the failure.

## Required Live PS5 Cancellation Regression

This is the primary runtime closure for rev2.

1. Start ps5debug-NG and a game on the PS5.
2. Connect from Memory Engine.
3. Select the game's `eboot.bin` and choose **Set Active Target**.
4. Confirm the real memory map loads.
5. Enter a common 4-byte value expected to produce a sufficiently long First Scan.
6. Choose **First Scan**.
7. While the scan is actively reading memory, choose **Cancel Scan**.
8. Confirm the status changes to `Cancelling scan after the current memory-read request completes...` or equivalent.
9. Allow the current read transaction to finish; confirm the scan then reports cancelled without a process-list/protocol error.
10. **Do not Disconnect.**
11. Immediately choose **Refresh** for the process list.
12. Confirm process enumeration succeeds on the same connection and no unexpected status such as `0xC0F6410A` appears.
13. Confirm the same Active Target can remain/restored and its memory map can be loaded again.
14. Choose **New Scan**, enter a value, and start another **First Scan** without reconnecting.
15. Confirm the new scan begins and reads memory normally.
16. Optionally cancel a second time and repeat Refresh/New Scan to prove repeatability.

### Core pass criterion

```text
Cancel Scan
→ current PS5 memory-read transaction drains completely
→ scan cancellation completes
→ same TCP session remains usable
→ Refresh / New Scan works without Disconnect -> Connect
```

## Live PS5 Scanner Preservation

Rev1 already demonstrated that the shared scanner can locate a real game value. Rev2 must preserve that behavior.

After the cancellation regression test:

1. perform a normal First Scan for a known 4-byte game value;
2. refine as needed;
3. confirm the correct value can still be located;
4. confirm Scan Results addresses are compact but numerically complete;
5. use **Change value** on the result;
6. confirm Raw Memory Write receives the correct address;
7. enter the desired replacement as decimal and use **Write + Verify**;
8. confirm the game value changes as expected.

## Preservation Requirements

The revision must not regress:

- PS5 connection/identification;
- process enumeration;
- Selected Process versus Active Target separation;
- memory-map enumeration;
- raw memory read;
- raw memory write/read-back;
- Safe Write Test;
- shared First Scan and Next Scan logic;
- Mock deterministic layout;
- Plugin API `1.0.0`;
- theme palette behavior from rev1;
- 34-unit single-line control height;
- TextBox padding fix;
- splitters/workspace geometry;
- Scan Results versus Saved Addresses separation.

## Pass Criteria

```text
Windows rebuild:                               PASS
Verification executable (13/13):               PASS
PS5 cancellation stream regression test:       PASS
Mock First/Next Scan regression:                PASS
Compact result addresses:                      PASS
Copy address / Copy value:                     PASS
Change value address transfer:                 PASS
Decimal Int32 Raw Memory Write:                 PASS
Status-bar scan status:                         PASS
Progress bar:                                   PASS
Elapsed scan time:                              PASS
Live PS5 Cancel -> Refresh without reconnect:   PASS
Live PS5 Cancel -> New Scan without reconnect:  PASS
Normal live PS5 scanning preserved:             PASS
Previously verified target-access behavior:     PASS
```

Until the Windows build, 13-check verification executable, and live PS5 cancellation/session-reuse test are completed, rev2 is source-complete but not runtime-verified.


## Windows Runtime Result - 2026-08-31

The dependency-free verification executable was run on the Windows development machine and returned:

```text
All 13 checks passed.
```

All protocol, shared scanner, cancellation-regression, and plugin-discovery checks therefore passed. However, WPF startup then failed before the live rev2 PS5 cancellation workflow could be executed. The reported exception was:

```text
System.InvalidOperationException:
A TwoWay or OneWayToSource binding cannot work on the read-only property
'ScanProgressPercentage' of type
'TeeKay87.MemoryEngine.App.ViewModels.PluginViewModel'.
```

The failure is presentation-only: `PluginViewModel.ScanProgressPercentage` is output state with a private setter, while `ProgressBar.Value` used its default binding mode and therefore attempted to establish a source-writing binding. This is corrected by host `0.1.2.rev3`; no rev2 Core scanner or PS5 transport behavior was changed as part of that correction.

**rev2 automated verification: PASS (13/13).**  
**rev2 WPF startup: FAIL, superseded by 0.1.2.rev3.**  
**rev2 live PS5 Cancel -> Refresh/New Scan closure: deferred until rev3 starts successfully.**
