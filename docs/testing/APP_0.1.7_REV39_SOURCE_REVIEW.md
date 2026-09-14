# Application 0.1.7.rev39 Source Review — Call Stack Comparer Session Persistence and Explicit Capture

## Baseline

Source baseline: user-supplied `TK87ME_0.1.7.rev38___Disassembler-Arbitrary-Row-Watchpoint-Resolution(1).zip`.

Before changes, README, CHANGELOG, the full development action plan, every Markdown document under `docs/`, and the complete `src/` + `tests/` code/project surface were reviewed. Existing snapshot/comparer, debugger-session, modeless-window, and automated source-contract paths were reused rather than duplicated.

## Problems reproduced in the rev38 source

### Comparer window owned the only snapshot collection instance

`OpenComparerFromDebuggerAsync()` searched only currently open tool windows. Once the comparer window closed, its `CallStackComparerViewModel` was no longer tracked and the next open created a new view model with an empty `Snapshots` collection.

### Opening the comparer mutated evidence

The same open path called `CaptureSnapshotAsync()` whenever `CanCaptureSnapshot` was true and inserted that snapshot before opening/activating the comparer. This made simply opening an analysis window create evidence without an explicit capture action.

### Capture availability did not refresh when a new stop context arrived

`ApplyCoordinatorState(Paused)` notified `CanCaptureSnapshot` before the corresponding paused debugger event had installed `_latestStopContext`. `OnCoordinatorEventReceived` then assigned the new stop context without raising the derived property again, so the comparer could remain disabled after Continue -> Pause.

## Rev39 implementation

- `OpenDebuggerButton_Click` now retains one `CallStackComparerViewModel? comparerWorkspace` in the lifetime closure of that Debugger window/session.
- `OpenComparerFromDebuggerAsync()` creates the workspace lazily, activates an already-open window when present, or creates a new presentation window bound to the same retained workspace.
- The open path no longer calls `CaptureSnapshotAsync()` and no longer calls `AddSnapshot(...)` implicitly.
- `CallStackComparerWindow` no longer detaches `LiveDebugger` when only the comparer presentation window closes.
- The Debugger window close path detaches the retained workspace from the live `DebuggerViewModel`, allowing an already-open comparer to remain offline without retaining live authority.
- `DebuggerViewModel.OnCoordinatorEventReceived` now raises `CanCaptureSnapshot` when a Running event clears the stop context and immediately after a Paused event installs the new `_latestStopContext`. Existing `IsBusy` notifications remain the final guard when stop refresh is deferred through an in-progress command.
- Existing snapshot schema, snapshot capture service, serializer, comparison engine, plugin contracts, PS5 code, and Mock code are unchanged.
- The existing Call Stack Comparer source-contract check is strengthened to require workspace persistence, explicit-only capture, debugger-close detachment, and paused-stop re-evaluation. The registry remains 152 checks.

## Version surface

| Component | Rev39 |
| --- | --- |
| Application | `0.1.7.rev39` |
| Feature | `Call Stack Comparer Session Persistence and Explicit Capture` |
| Plugin API | `2.18.0` |
| Mock | `1.0.1.rev17` |
| PS5 | `0.1.2.rev39` |
| Snapshot schema | version `1` |
| Automated checks | `152` |

No platform plugin source changed, so plugin semantic versions are intentionally unchanged.

## Static package checks required before publication

- XML/XAML/project files parse successfully.
- JSON files parse successfully.
- no merge-conflict markers exist;
- no build-output directories are packaged;
- AppInfo is the central host version source and reports `0.1.7.rev39`;
- Call Stack Comparer source contract references the new persistence/explicit-capture behavior;
- automated registry count remains 152.

The current environment does not provide the Windows/.NET WPF toolchain, so the package must still pass the clean Windows build plus all 152 checks before runtime verification resumes.
