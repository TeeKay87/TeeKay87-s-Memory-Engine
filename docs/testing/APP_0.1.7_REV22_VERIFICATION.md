# Application 0.1.7.rev22 Verification — Debugger Tab Right Edge Layout Fix

## Status

**SUPERSEDED AFTER RUNTIME UI FAILURE.** The rev22 package launched on Windows, but the first focused workspace-tab inspection exposed a new visual regression before the carried-forward Call Stack/Stepping acceptance could continue. The Breakpoints / Watchpoints and Call Stack labels were no longer visible, and the selected Call Stack header rendered as a solid accent-colored block. The reserved-column/right-edge layout is rejected and replaced by rev23. Rev21's tested MainWindow/tool-window shutdown path remains accepted and was not implicated.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev22` |
| Feature | `Debugger Tab Right Edge Layout Fix` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev34` |
| Automated checks | `130` |

## Carried Result from rev21

Rev21 completed its Windows automated gate at **130/130 PASS**. Runtime testing then confirmed that closing MainWindow closes the open tool windows as intended, but the workspace-tab right edge remained missing. Rev22 must not reopen the accepted shutdown implementation; it rechecks that behavior only as a regression after the visual gate passes.

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev22 ZIP into a clean directory.
2. Build the solution/application normally in Visual Studio.
3. Confirm there are no warnings-as-errors or XAML compile failures.
4. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

5. Confirm the final line is exactly:

```text
All 130 checks passed.
```

Do not proceed to runtime acceptance until the WPF build and all 130 checks pass.

## Gate B — Workspace Tab Right-Edge Runtime Acceptance

Use Mock and open Debugger before attaching.

### B1 — Dimmed

1. Start with **Breakpoints / Watchpoints** selected.
2. Confirm its top, left, right, and bottom header edges are visually complete.
3. Select **Call Stack**.
4. Confirm the selected Call Stack header has a visible right vertical edge and no abrupt open-ended right side.
5. Move the pointer over both selected and unselected headers and confirm the right edge remains present through hover transitions.
6. Confirm there is no operating-system white tab surface.
7. Confirm text placement, external spacing, rounded top corners, and tab-content geometry remain consistent with the accepted themed layout.

### B2 — Dark

Repeat B1 in **Dark**. The edge must use the active theme/state border color and remain visible for selected and unselected headers.

### B3 — Light

Repeat B1 in **Light**. The edge must use the active theme/state border color and remain visible for selected and unselected headers.

### B4 — Display scaling / resize sanity

If Windows display scaling is above 100%, keep the current scaling and resize Debugger through several widths. Confirm the right edge does not disappear or shift away from the header. If practical, repeat once at 100% scaling after restarting the application. This is a sanity check for the new `UseLayoutRounding` + reserved-column geometry, not a requirement to test every DPI setting.

## Gate C — Rev21 Window-Lifecycle Regression

Rev22 does not change lifecycle code. Recheck the accepted path briefly:

1. Open Debugger plus Memory Viewer and Disassembler.
2. Activate MainWindow and at least one tool in both directions to confirm ordinary modeless z-order remains available.
3. Close MainWindow while the tool windows are open.
4. Confirm all tools close through their normal cleanup paths and the application exits without exception or orphaned windows.
5. If Debugger is attached for this check, confirm it cleans up/detaches during shutdown.

A failure here is a rev22 regression even though the relevant production files are intended to be byte-identical to rev21.

## Gate D — Carried-Forward Mock Call Stack and Stepping Acceptance

After Gates A-C pass, resume the complete focused Mock Call Stack/Call Frames and Stepping verification defined in `APP_0.1.7_REV17_VERIFICATION.md`. Cover:

- Call Stack population while paused;
- selected-thread/frame consistency;
- frame details and Disassembler/Memory Viewer navigation;
- Step Into;
- Step Over for call and non-call instructions;
- Step Out;
- Run to Address;
- temporary-breakpoint cleanup;
- Registers/Threads/Current Instruction/Call Stack refresh after stop events; and
- resume/detach/disconnect/stale-session cleanup.

## Gate E — Carried-Forward Live PS5 Acceptance

After Mock acceptance passes, execute the live-PS5 portions of `APP_0.1.7_REV17_VERIFICATION.md` against rev22:

- server-side `CMD_PROC_READ_STACK` frame retrieval;
- frame navigation;
- selected-thread native Step Into;
- Step Over;
- Step Out;
- Run to Address;
- breakpoint/watchpoint coexistence and cleanup;
- transport/event regression; and
- detach/disconnect/reconnect stale-state cleanup.

Keep the already documented PS5 register-write and individual thread-control limitations unchanged.

## Completion Rule

Rev22 may be accepted only after:

1. clean Windows WPF build;
2. **130/130 PASS**;
3. complete tab right-edge acceptance in Dimmed, Dark, and Light;
4. no regression in normal modeless z-order or MainWindow-driven tool cleanup;
5. complete carried-forward Mock Call Stack/Stepping runtime acceptance; and
6. complete carried-forward live-PS5 Call Stack/Stepping acceptance.

Rev22 required another code correction and must not be marked verified. The corrective revision is `0.1.7.rev23 — Debugger Tab Header Rendering Fix`; Integration, Export and Finalization moves to `0.1.7.rev24`.
