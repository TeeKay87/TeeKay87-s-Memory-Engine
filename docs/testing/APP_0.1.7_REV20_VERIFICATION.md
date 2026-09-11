# Application 0.1.7.rev20 Verification — Debugger Tab Border and Shutdown Re-entry Fixes

## Status

**SUPERSEDED — WINDOWS BUILD FAILED.** Rev20 preserves the rev17 Call Stack/Stepping implementation and rev19 independent modeless-window lifecycle, while correcting the two runtime failures that stopped rev19 acceptance: selected/last workspace-tab right-edge clipping and MainWindow close re-entry during tracked-tool cleanup.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev20` |
| Feature | `Debugger Tab Border and Shutdown Re-entry Fixes` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev34` |
| Automated checks | `130` |

## Recorded Windows Build Result

The first clean Windows build failed before the automated registry could be accepted. The primary compiler error was:

```text
CS4014: Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
```

The error points to the rev20 final-close line in `MainWindow.xaml.cs`:

```csharp
Dispatcher.BeginInvoke(new Action(Close));
```

Because `DispatcherOperation` is awaitable and warnings are treated as errors, the intentionally queued fire-and-forget operation must be acknowledged explicitly. Visual Studio also reported XAML designer/type-resolution errors in `MainWindow.xaml`; no corresponding source type removal was found, so those are treated as build-cascade diagnostics pending the corrective build.

Rev20 must not be marked verified. The corrective revision is `0.1.7.rev21 — MainWindow Shutdown Compile Fix`, and Integration, Export and Finalization moves to rev22.

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev20 ZIP into a clean directory.
2. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

3. Confirm the final line is exactly:

```text
All 130 checks passed.
```

Do not skip or remove a failing registration. The registry must contain 130 unique names and 130 unique method targets.

## Gate B — Workspace Tab Right-Edge Regression

Use Mock and open Debugger before attaching.

1. In **Dimmed**, inspect **Breakpoints / Watchpoints** while it is selected. Confirm its top, left, right, and bottom header edges are complete.
2. Select **Call Stack**. Confirm the selected/last header has a visible right vertical edge and rounded top-right corner instead of ending abruptly.
3. Move the pointer over both selected and unselected headers. Confirm the right edge remains complete in normal, hover, and selected states.
4. Repeat in **Dark** and **Light**.
5. Confirm no operating-system white tab surface has returned.
6. Confirm header dimensions, spacing, text placement, tab content dimensions, and the right-side workspace splitter behavior remain unchanged.

**PASS requirement:** both headers have a complete outline in every tested theme/state, with particular attention to the last/selected Call Stack header that failed rev19.

## Gate C — Independent Modeless Z-Order Regression

Open Debugger, Disassembler, and Memory Viewer where capabilities permit them.

1. Activate each tool and confirm it can appear above MainWindow.
2. Activate MainWindow and confirm it can appear above every tool.
3. Move between the three modeless tools and confirm ordinary desktop activation works in both directions.
4. Confirm newly opened tools still begin centered relative to MainWindow.
5. Confirm no tool uses permanently topmost behavior.

## Gate D — MainWindow Shutdown Regression

### D1 — Detached Debugger

1. With Mock Active Target selected, open Debugger but leave it detached.
2. Close MainWindow.
3. Confirm Debugger closes and the application exits normally.
4. Confirm the rev19 exception does **not** appear:

```text
Cannot set Visibility to Visible or call Show, ShowDialog, Close, or WindowInteropHelper.EnsureHandle while a Window is closing.
```

### D2 — Attached Debugger

1. Restart, set Mock Game as Active Target, open Debugger, and Attach.
2. Leave Debugger Running or Pause it.
3. Close MainWindow directly.
4. Confirm debugger cleanup/detach completes, Debugger closes, and the application exits without exception or hang.

### D3 — Multiple tools

1. Restart and open Debugger, Disassembler, and Memory Viewer.
2. Attach Debugger.
3. Close MainWindow while all three modeless tools remain open.
4. Confirm all tools become non-interactive during shutdown, execute their cleanup, close, and leave no application process running.
5. Repeat once with Debugger paused.

## Gate E — Modal Dialog Regression

Open at least one existing modal dialog from a tool window, then cancel it normally. Confirm it remains owned/modal and centered to its invoking window. Rev20 must not turn modal dialogs into independent modeless windows.

## Gate F — Carried-Forward Call Stack/Stepping Acceptance

After Gates A-E pass, resume the complete focused Call Stack/Call Frames and Stepping verification from `APP_0.1.7_REV17_VERIFICATION.md` against rev20. This includes:

- Mock workspace/call-stack data, frame details, thread switching, and navigation;
- Mock Step Into, Step Over, Step Out, and Run to Address;
- Mock stale-session/cleanup and breakpoint/watchpoint/register regression;
- live PS5 Call Stack and frame navigation;
- live PS5 native Step Into;
- live PS5 Step Over, Step Out, and Run to Address through host-composed paths;
- final live debugger cleanup/transport/regression checks.

## Completion Rule

Rev20 may be accepted only after:

1. **130/130 PASS** on Windows;
2. Gate B complete-tab-border acceptance passes in Light/Dimmed/Dark;
3. Gate C independent modeless z-order passes;
4. Gate D detached/attached/multi-tool MainWindow shutdown passes with no close re-entry exception;
5. Gate E modal behavior remains unchanged;
6. the complete carried-forward Mock runtime acceptance passes;
7. the complete carried-forward live-PS5 acceptance passes.

Rev20 did require another code correction and is superseded before runtime acceptance. The corrective revision is `0.1.7.rev21 — MainWindow Shutdown Compile Fix`; Integration, Export and Finalization therefore moves to `0.1.7.rev22`.
