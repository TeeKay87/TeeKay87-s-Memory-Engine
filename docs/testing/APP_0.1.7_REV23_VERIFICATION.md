# Application 0.1.7.rev23 Verification — Debugger Tab Header Rendering Fix

## Status

**SUPERSEDED AFTER AUTOMATED/RUNTIME UI GATE.** The Windows automated suite passed **130/130**. Runtime inspection confirmed that the header text and selected-state rendering were restored, and the previously accepted MainWindow/tool-window cleanup remained correct, but the selected/unselected workspace-tab right edge still rendered abruptly clipped. Rev23 therefore cannot be marked verified. The corrective revision is `0.1.7.rev24 — Debugger Mode Switcher Layout Refresh`, which removes the active WPF tab chrome instead of adding another edge workaround.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev23` |
| Feature | `Debugger Tab Header Rendering Fix` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev34` |
| Automated checks | `130` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev23 ZIP into a clean directory.
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

## Gate B — Workspace Tab Header Runtime Acceptance

Start with Mock, open Debugger before attaching, and use the normal application theme selector.

### B1 — Dimmed

1. Confirm **Breakpoints / Watchpoints** text is visible and centered normally.
2. Confirm **Call Stack** text is visible and centered normally.
3. Select each tab in turn. The selected background must remain the normal raised Dimmed surface; the entire header must **not** turn accent blue.
4. Confirm both selected and unselected headers have a visible right vertical edge rather than an abrupt open end.
5. Move the pointer over each tab and confirm text, right edge, and theme colors remain stable through hover.
6. Confirm padding, inter-tab spacing, rounded top corners, and the content workspace below the headers remain unchanged.

### B2 — Dark

Repeat B1 in **Dark**. Header text must remain readable and selected/hover/border colors must come from the active theme.

### B3 — Light

Repeat B1 in **Light**. There must be no operating-system white/default TabControl surface beyond the application's own Light theme.

### B4 — Resize / display scaling sanity

Resize Debugger through several practical widths at the current Windows display scaling. If practical, repeat once at 100% scaling after restarting the app. Confirm that labels remain visible and the right edge does not disappear.

## Gate C — Window-Lifecycle Regression

Rev23 does not change lifecycle code, but briefly recheck the accepted behavior:

1. Open Debugger plus Memory Viewer and Disassembler.
2. Activate MainWindow and tools in both directions; none must remain permanently above MainWindow.
3. Close MainWindow while the tools are open.
4. Confirm all modeless tools run their normal cleanup/close paths and the application exits without exception or orphaned windows.
5. If Debugger is attached, confirm shutdown cleanup detaches/releases it normally.

## Gate D — Carried-Forward Mock Call Stack and Stepping Acceptance

After Gates A-C pass, resume the complete focused Mock verification from `APP_0.1.7_REV17_VERIFICATION.md`, including Call Stack population/thread selection, frame details and Disassembler/Memory Viewer navigation, Step Into, Step Over call/non-call paths, Step Out, Run to Address, temporary-breakpoint cleanup, stop-context refresh, and stale-session/detach/disconnect cleanup.

## Gate E — Carried-Forward Live PS5 Acceptance

After Mock acceptance passes, execute the live-PS5 portions of `APP_0.1.7_REV17_VERIFICATION.md`: server-side stack retrieval, frame navigation, selected-thread native Step Into, Step Over, Step Out, Run to Address, breakpoint/watchpoint coexistence, transport/event regression, and detach/disconnect/reconnect cleanup.

## Recorded Windows Result

- Clean Windows automated verification: **PASS — 130/130**.
- MainWindow-driven modeless-tool cleanup regression: **PASS**; all open tool windows closed through their normal cleanup paths.
- Workspace-tab header labels/selected fill: restored from the rev22 regression.
- Workspace-tab right edge: **FAIL**; the abrupt clipped right edge remained visible in runtime.
- Carried-forward Call Stack/Stepping runtime acceptance: not resumed because the UI gate failed first.

Rev23 is therefore superseded by rev24 and Integration, Export and Finalization moves to rev25.

## Completion Rule

Rev23 may be accepted only after:

1. clean Windows WPF build;
2. **130/130 PASS**;
3. readable and correctly themed tab headers with complete right edges in Dimmed, Dark, and Light;
4. no regression in modeless z-order or MainWindow-driven tool cleanup;
5. complete carried-forward Mock Call Stack/Stepping acceptance; and
6. complete carried-forward live-PS5 Call Stack/Stepping acceptance.

Rev23 required another code correction and is superseded. The next corrective revision is `0.1.7.rev24 — Debugger Mode Switcher Layout Refresh`; Integration, Export and Finalization moves to `0.1.7.rev25`.
