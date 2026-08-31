# 0.1.2.rev5 Scanner Workflow and Turbo Refinement Verification

## Purpose

This document defines verification for **TeeKay87's Memory Engine 0.1.2.rev5 - Scanner Workflow and Turbo Refinement**.

The revision builds on the runtime-verified rev4 PS5 TurboScan First Scan and adds resident native Next Scan refinement, keyboard-oriented Scan-panel workflow, semantic Danger buttons, and preferred PS5 game-process selection. The established shared Core scanner remains the fallback path.

## Version Boundary

Expected metadata:

```text
Host:        0.1.2.rev5
Feature:     Scanner Workflow and Turbo Refinement
Plugin API:  1.2.0
PS5 plugin:  0.1.0.rev8
Mock plugin: 1.0.0.rev1
```

The Plugin API minor version increases because `INativeValueScanRefiner` is a new optional service contract. Existing plugins that do not implement the service continue to use shared Core refinement.

## Deterministic Windows Verification

Rebuild the solution and run:

```powershell
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

Expected result: **17 checks pass**. The new coverage must include **PS5 native exact-value refinement protocol** in addition to the established rev4 checks.

The refinement protocol test verifies a resident First Scan, a target-side TurboScan COUNT refinement, survivor GET, Current/Previous values, and explicit session reset/END.

## Scan Workflow QoL

With an active target and no existing scan session:

1. confirm **First Scan** uses the active theme's Primary/accent button palette;
2. confirm **Next Scan** has normal Secondary appearance;
3. click the **Value** field, enter a valid Int32, and press **Enter**;
4. confirm First Scan starts;
5. after a successful First Scan, confirm First Scan becomes Secondary and Next Scan becomes Primary;
6. enter a new value in Value and press **Enter**;
7. confirm Next Scan starts;
8. choose **New Scan** and confirm First Scan becomes Primary again;
9. confirm a failed or cancelled scan does not incorrectly advance the workflow state.

Enter is intentionally scoped to the Value field. It must not globally trigger scans while focus is elsewhere.

## Danger Button Themes

In Light, Dimmed, and Dark themes:

1. confirm **Disconnect** uses the theme's Danger palette;
2. confirm **Cancel Scan** uses the same semantic Danger palette;
3. confirm hover, pressed, focus, and disabled states remain readable;
4. confirm Primary and Secondary buttons are unchanged except for the workflow emphasis described above.

The bundled themes use complementary red Danger colors. No red value is hard-coded into the Scan view.

## Preferred PS5 Target Process

1. connect to a PS5 while a game is running and refresh processes;
2. when `eboot.bin` exists and no previous Target Process selection can be restored, confirm **Target Process** automatically selects `eboot.bin`;
3. confirm this does **not** automatically set or replace the **Active Target**;
4. manually select another process and Refresh;
5. confirm the manual choice remains selected while that process still exists;
6. if the manual process disappears and `eboot.bin` is still present, confirm the preferred process may be selected again.

The host must obtain this preference through `IForegroundProcessProvider`; WPF must not contain PS5-specific process-name logic.

## PS5 Resident TurboScan Next Scan

Use a value that produces a manageable but non-trivial First Scan result set.

1. run First Scan and record elapsed time/result count;
2. change the game value naturally;
3. enter the new value and run Next Scan;
4. confirm status identifies native target refinement when the resident TurboScan session is available;
5. record elapsed time and survivor count;
6. confirm surviving addresses are a subset of the previous result set;
7. confirm **Current** shows the new exact value and **Previous** shows the value from the preceding scan;
8. run at least one additional Next Scan to confirm the resident set can be narrowed repeatedly;
9. choose New Scan and confirm a fresh First Scan still works.

Compare Next Scan against the recorded rev4 Core-refinement example of **6.6 seconds for 884 -> 8 results**. The purpose of rev5 is to reduce unnecessary client/server memory reads while preserving result semantics, not to require a fixed universal timing threshold.

## Progress Behavior

For PS5 native First Scan and resident list-mode Next Scan:

- the progress indicator should be **indeterminate**, because these operations do not provide a meaningful continuous percentage stream to the host;
- elapsed time must continue updating;
- the UI must not display a fabricated percentage.

For Mock or PS5 fallback through the shared Core scanner, determinate percentage progress remains valid and should continue to update normally.

## Cancellation and Session Safety

For a native PS5 scan long enough to press Cancel:

1. choose **Cancel Scan**;
2. confirm the UI immediately communicates that cancellation was requested and that it is waiting for the current target scan operation to finish safely;
3. allow the native operation to reach its protocol boundary;
4. confirm the final operation is reported as cancelled rather than applying the discarded result;
5. without Disconnect, Refresh processes and start a New Scan;
6. confirm no `unexpected status` or protocol desynchronization occurs.

Repeat with **Pause target while scanning** enabled and confirm the game resumes after the deferred cancellation returns.

This revision does not claim an asynchronous ps5debug-NG TurboScan abort. Deferred cancellation is intentional until the target protocol provides a safe in-flight abort mechanism.

## Fallback Regression

Verify Mock and any session where native refinement is unavailable:

1. First Scan still uses the existing compatible path;
2. Next Scan falls back to shared Core refinement;
3. Core percentage progress remains determinate;
4. result Current/Previous semantics remain unchanged;
5. New Scan and Cancel continue to function.

## Recorded Result

The revision was verified successfully before the project advanced to `0.1.3`.

```text
Windows verification executable:                     PASS - 17/17
WPF startup:                                         PASS
First/Next dynamic Primary workflow:                 PASS
Enter in Value dispatches correct scan:              PASS
Danger buttons in bundled themes:                    PASS
PS5 eboot.bin preferred Target Process:              PASS
Manual Target Process preservation:                  PASS
PS5 native resident Next Scan correctness:           PASS
Native First/Next indeterminate progress behavior:   PASS
Core fallback behavior:                              PASS
Deferred cancellation/session reuse:                 PASS
Pause/resume scan workflow:                          PASS
Existing map/read/write/process behavior:            PASS
```

The executable output reported `All 17 checks passed.` The remaining live/UI behaviors in this verification plan were subsequently reported working as intended. This completed the `0.1.2` scanner-workflow milestone and established rev5 as the baseline for `0.1.3`.

No additional exact rev5 Next Scan timing was recorded in this document; the earlier rev4 comparison point remains approximately `6.6 s` for the cited 884 -> 8 Core-refinement example.
