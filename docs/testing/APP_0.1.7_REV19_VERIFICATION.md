# Application 0.1.7.rev19 Verification — Debugger UI and Window Lifecycle Fixes

## Status

**SUPERSEDED AFTER AUTOMATED GATE.** Rev19 passed its complete Windows automated gate at **128/128 PASS**, but the first focused runtime inspection failed two acceptance requirements. The selected/last **Call Stack** header still lost its right vertical border, and closing MainWindow while Debugger remained open raised WPF `InvalidOperationException` because the final `Close()` could re-enter the MainWindow while its original close request was still active. Rev20 contains the corrective host-only fixes.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev19` |
| Feature | `Debugger UI and Window Lifecycle Fixes` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev34` |
| Automated checks | `128` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev19 ZIP into a clean directory.
2. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

3. Confirm the final line is exactly:

```text
All 128 checks passed.
```

Do not skip or remove a failing registration. The registry must contain 128 unique names and 128 unique method targets.

## Gate B — Workspace Tab Visual Regression

Use Mock and open the Debugger.

1. In **Dimmed**, inspect both **Breakpoints / Watchpoints** and **Call Stack** headers.
2. Confirm each tab has a complete visible right-hand border and rounded top-right corner rather than ending abruptly.
3. Switch selected tabs and hover both headers. Confirm the right edge remains present in normal, hover, and selected states.
4. Repeat in **Dark** and **Light**.
5. Confirm the rev18 fix remains intact: no operating-system white tab strip/content surface reappears and header text remains readable.
6. Confirm no tab-content layout, splitter, binding, or command shifted because of the one-unit border inset.

## Gate C — Independent Modeless Z-Order

Open **Debugger**, **Disassembler**, and **Memory Viewer** from MainWindow where capabilities allow them.

For each tool individually and then with all three open:

1. Activate the tool and confirm it appears above MainWindow.
2. Click MainWindow and confirm MainWindow can appear above that tool.
3. Click the tool again and confirm it can appear above MainWindow again.
4. Repeat between the different tool windows and confirm no one modeless tool behaves as permanently topmost.
5. Confirm newly opened tools still begin with the expected centered-relative-to-MainWindow placement.

Modal dialogs are a separate behavior: an application-owned modal dialog should remain owned/modal to the window that opened it. Rev19 must not make modal dialogs independently activatable while they are open.

## Gate D — MainWindow Shutdown Cleanup

### D1 — Basic multi-window shutdown

1. Open Debugger, Disassembler, and Memory Viewer with Mock.
2. Leave all three open.
3. Close **MainWindow**, not the individual tools.
4. Confirm all three tools close and the process exits normally.
5. Confirm the open tool windows become non-interactive while shutdown cleanup is in progress and that there is no exception dialog, hung process, or tool window left running after MainWindow disappears.

### D2 — Attached Debugger cleanup

1. Restart with Mock, set the Mock process as Active Target, and open Debugger.
2. Attach the Debugger. It may be Running or Paused for this shutdown test.
3. Optionally open Disassembler and Memory Viewer as additional tracked tools.
4. Close MainWindow directly.
5. Confirm the Debugger closes as part of application shutdown without a debugger transport/session error or hang and all other open tools close as well.

The important requirement is that MainWindow does not simply terminate underneath a live modeless Debugger: the tracked tool cleanup path must be allowed to complete before the final application window closes.

## Gate E — Carried-Forward Call Stack/Stepping Acceptance

After Gates A-D pass, continue the complete focused Call Stack/Call Frames and Stepping verification from `APP_0.1.7_REV17_VERIFICATION.md` against the **rev19** package. The sequence includes:

- Mock workspace/call-stack frame details, thread switching, and navigation;
- Mock Step Into, Step Over, Step Out, and Run to Address;
- Mock stale-session/cleanup and breakpoint/watchpoint/register regression;
- live PS5 Call Stack and frame navigation;
- live PS5 native Step Into;
- live PS5 Step Over, Step Out, and Run to Address through the host-composed paths;
- final live debugger cleanup/transport/regression checks.

## Completion Rule

Rev19 may be marked verified only after:

1. **128/128 PASS** on Windows;
2. Gate B complete-tab-border/theme acceptance passes;
3. Gate C independent modeless z-order passes;
4. Gate D MainWindow shutdown cleanup passes;
5. the complete carried-forward Mock runtime acceptance passes;
6. the complete carried-forward live-PS5 acceptance passes.

Rev19 did require another code correction. Its historical automated result remains **128/128 PASS**, but it must not be relabeled VERIFIED because Gates B and D failed. The immediate corrective revision was `0.1.7.rev20 — Debugger Tab Border and Shutdown Re-entry Fixes`. Rev20 later failed its first Windows build and required rev21, so Integration, Export and Finalization is now scheduled for rev22.

## Runtime Follow-up Leading to rev20

The rev19 Windows verification runner completed successfully:

```text
All 128 checks passed.
```

Focused runtime acceptance then stopped immediately on two host/WPF defects:

1. **Workspace tab right edge still incomplete.** In the themed Debugger workspace, the selected **Call Stack** header still ended abruptly on its right side. The rev19 one-unit `TabBorder` inset did not reliably preserve the final vertical stroke for the selected/last TabItem.
2. **MainWindow close re-entry exception.** Closing MainWindow with Debugger open raised:

```text
System.InvalidOperationException
Cannot set Visibility to Visible or call Show, ShowDialog, Close, or WindowInteropHelper.EnsureHandle while a Window is closing.
```

Source review traced the second failure to rev19 calling `Close()` directly from the asynchronous continuation reached by `OnClosing`. When `CloseAllAsync()` completed synchronously, the continuation could execute before the original WPF close stack had unwound, causing a second `Close()` against the same MainWindow while it was still closing.

No Call Stack/Stepping behavior was runtime-tested past this point. Rev20 was intended to re-run the affected tab and shutdown gates but failed its first Windows build; those gates and the carried-forward debugger acceptance now continue against rev21.
