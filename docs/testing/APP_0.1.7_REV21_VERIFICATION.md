# Application 0.1.7.rev21 Verification — MainWindow Shutdown Compile Fix

## Status

**SUPERSEDED AFTER PARTIAL RUNTIME ACCEPTANCE.** The Windows application built and the automated registry completed at **130/130 PASS**. Runtime testing confirmed that closing MainWindow now closes the open tool windows as intended, resolving the previous shutdown close re-entry failure. The workspace-tab right-edge gate still failed: the right side of the Breakpoints / Watchpoints and Call Stack headers remained visually absent. Rev21 therefore cannot be marked verified and is superseded by rev22.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev21` |
| Feature | `MainWindow Shutdown Compile Fix` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev34` |
| Automated checks | `130` |


## Recorded Verification Outcome

- **Gate A — PASS:** the rev21 application built successfully and the complete automated suite reported `All 130 checks passed.`
- **Gate B — FAIL:** runtime inspection still showed the tab headers ending without a visible right vertical edge. The source-level `TabRightEdge` assertion was therefore insufficient to prove actual Windows layout/rendering.
- **Gate D — PASS for the reported shutdown scenario:** closing MainWindow with tool windows open closed those windows as intended and did not reproduce the previous `InvalidOperationException` close re-entry failure.
- Gates C, E, and the carried-forward Call Stack/Stepping gates were not used to accept rev21 because Gate B already required another code revision.
- Corrective revision: `0.1.7.rev22 — Debugger Tab Right Edge Layout Fix`.
- Planned Integration, Export and Finalization moves to `0.1.7.rev23`.

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev21 ZIP into a clean directory.
2. Build the application/solution normally in Visual Studio or run a clean solution build.
3. Confirm the rev20 `CS4014` error on `MainWindow.xaml.cs` is gone.
4. Confirm the cascade XAML designer/type-resolution errors reported with rev20 are no longer present after a clean build/reload. If a real XAML compiler error remains, stop and record the exact first build error rather than treating designer diagnostics as verified.
5. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

6. Confirm the final line is exactly:

```text
All 130 checks passed.
```

Do not proceed to runtime acceptance until both the WPF application build and all 130 checks pass.

## Gate B — Workspace Tab Right-Edge Regression

Rev20 never reached runtime, so its tab correction receives its first runtime acceptance here. Use Mock and open Debugger before attaching.

1. In **Dimmed**, inspect **Breakpoints / Watchpoints** while selected and confirm top, left, right, and bottom header edges are complete.
2. Select **Call Stack** and confirm the selected/last header has a visible right vertical edge and rounded top-right corner instead of ending abruptly.
3. Verify normal, hover, and selected states for both headers.
4. Repeat in **Dark** and **Light**.
5. Confirm no operating-system white tab surface has returned.
6. Confirm header dimensions, spacing, text placement, tab content layout, and splitters remain unchanged.

## Gate C — Independent Modeless Z-Order Regression

Open Debugger, Disassembler, and Memory Viewer where capabilities permit them.

1. Activate each tool and confirm it can appear above MainWindow.
2. Activate MainWindow and confirm it can appear above every modeless tool.
3. Move between tools and MainWindow repeatedly; ordinary desktop activation must work in both directions.
4. Confirm newly opened tools still begin centered relative to MainWindow.
5. Confirm no tool uses permanent topmost behavior.

## Gate D — MainWindow Shutdown Regression

### D1 — Detached Debugger

1. With Mock Active Target selected, open Debugger but leave it detached.
2. Close MainWindow.
3. Confirm Debugger closes and the application exits normally.
4. Confirm neither `CS4014` nor the previous runtime close re-entry exception is involved at runtime.

### D2 — Attached Debugger

1. Restart, set Mock Game as Active Target, open Debugger, and Attach.
2. Leave Debugger Running or Pause it.
3. Close MainWindow directly.
4. Confirm debugger cleanup/detach completes, Debugger closes, and the application exits without exception or hang.

### D3 — Multiple modeless tools

1. Restart and open Debugger, Disassembler, and Memory Viewer.
2. Attach Debugger.
3. Close MainWindow while all three remain open.
4. Confirm all tools become non-interactive during shutdown, execute their established cleanup, close normally, and leave no application process running.
5. Repeat once with Debugger paused.

## Gate E — Modal Dialog Regression

Open at least one existing modal dialog from a tool window and cancel it normally. Confirm it remains owned/modal and centered to its invoking window.

## Gate F — Carried-Forward Call Stack/Stepping Acceptance

After Gates A-E pass, resume the complete focused Call Stack/Call Frames and Stepping verification from `APP_0.1.7_REV17_VERIFICATION.md` against rev21. This includes Mock Call Stack data/navigation/thread switching, all four stepping/run-to operations, stale-session/cleanup regressions, and the complete live-PS5 Call Stack/stepping/transport cycle.

## Completion Rule

Rev21 may be accepted only after:

1. clean Windows WPF application build with the rev20 `CS4014` failure resolved;
2. **130/130 PASS**;
3. complete tab-border acceptance in Light/Dimmed/Dark;
4. independent modeless z-order acceptance;
5. detached/attached/multi-tool MainWindow shutdown acceptance with all tool cleanup executed;
6. modal behavior remains unchanged;
7. complete carried-forward Mock Call Stack/Stepping runtime acceptance; and
8. complete carried-forward live-PS5 Call Stack/Stepping acceptance.

Rev21 required another code correction after Gate B failed. It must remain superseded rather than verified. The corrective revision is `0.1.7.rev22 — Debugger Tab Right Edge Layout Fix`; Integration, Export and Finalization moves to `0.1.7.rev23`.
