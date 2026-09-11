# Application 0.1.7.rev21 Source Review — MainWindow Shutdown Compile Fix

## Scope

Rev21 is a corrective host-only revision built directly from the packaged `0.1.7.rev20` candidate after the first Windows build failed with `CS4014` in `MainWindow.xaml.cs`. The intended rev20 runtime behavior remains the target: the Debugger workspace tab headers use the explicit in-bounds right edge, modeless tools use normal desktop z-order, and MainWindow waits for tool cleanup before posting its final close. Rev21 changes only the compile boundary around that queued close.

No Core, Plugin SDK, Mock, PS5, debugger transport, Call Stack, stepping, breakpoint/watchpoint, register, scanner, Saved Addresses, Memory Viewer, or Disassembler behavior is in scope.

## Mandatory Pre-Change Review

Before editing production code, the complete rev20 documentation tree and complete source/configuration inventory were read and reviewed. The review covered 165 Markdown documents and 257 source/resource/build files, including 228 C# files and 16 XAML files. The full inventory confirmed:

- `Directory.Build.props` enables `TreatWarningsAsErrors` for all projects;
- `MainWindow.OnClosing` intentionally starts `CompleteToolWindowShutdownAsync()` through an explicit discard, so that fire-and-forget task is already acknowledged;
- `CompleteToolWindowShutdownAsync()` awaits `ToolWindowManager.CloseAllAsync()` and then calls `Dispatcher.BeginInvoke(new Action(Close))` in `finally`;
- WPF `Dispatcher.BeginInvoke` returns an awaitable `DispatcherOperation`, making the unacknowledged call eligible for `CS4014`;
- awaiting that dispatcher operation inside the helper would be contrary to the rev20 sequencing purpose because the final close must run only after the original synchronous `OnClosing` callback has unwound;
- assigning the returned operation to discard is the minimal acknowledgement that preserves the queued semantics;
- `UiMetrics`, `ProportionalGridSplitter`, and `TextBoxInputFilter` remain present in the same application namespaces referenced by `MainWindow.xaml`, so the simultaneous XAML designer errors do not justify unrelated XAML changes before a clean corrective build;
- the rev20 `WorkspaceTabItemStyle` is unchanged by this compile fix because its explicit `TabRightEdge` has not yet received runtime verification;
- no duplicate shutdown coordinator or alternative tool-window manager exists that should be used instead.

## Root Cause

The failing rev20 line was:

```csharp
Dispatcher.BeginInvoke(new Action(Close));
```

The call is intentionally not awaited, but the returned `DispatcherOperation` is awaitable. With warnings treated as errors, C# reports `CS4014` unless the result is explicitly acknowledged. The runtime design itself does not require a different scheduling primitive.

## Production Change

The final post is now:

```csharp
_ = Dispatcher.BeginInvoke(new Action(Close));
```

This is intentionally a discard rather than an `await`:

1. the original MainWindow close request is cancelled;
2. tracked tool cleanup is awaited;
3. the completed guard is set;
4. the final close callback is posted to the dispatcher;
5. the helper returns and the original WPF closing call stack can unwind; and
6. the queued callback issues the final `Close()`, whose second `OnClosing` pass sees the completed guard and follows the normal close path.

No `using` directive is added or removed.

## Verification Contract Change

The existing **Main-window shutdown re-entry guard source contract** is strengthened to require the exact explicit-discard form. The test registry remains at **130** unique top-level checks because this is a correction to an existing requirement, not a new independent feature.

The Windows build remains an independent mandatory gate because the test project consumes `MainWindow.xaml.cs` as a text fixture rather than compiling the WPF application assembly.

## Regression Boundaries

Rev21 must preserve byte-for-byte production behavior outside the host version identity and one MainWindow statement. In particular:

- `Resources/Styles/ControlStyles.xaml` must remain identical to rev20 so the pending `TabRightEdge` fix is tested exactly as packaged in rev20;
- `ToolWindowManager.cs` must remain identical to rev20;
- Core, Plugin SDK, Mock, and PS5 production trees must remain identical to rev20;
- Plugin API stays `2.15.0`;
- Mock stays `1.0.0.rev15`;
- PS5 stays `0.1.0.rev34`.

## Version Review

| Component | rev21 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev21` | Corrective host compile revision |
| Feature title | `MainWindow Shutdown Compile Fix` | New rev21 title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev34` | Unchanged |
| Automated registry | `130` | Existing shutdown contract strengthened |

## Development-Plan Effect

Rev21 is consumed by the compile correction. The planned **Integration, Export and Finalization** milestone moves to `0.1.7.rev22`. The Call Stack/Stepping implementation remains the rev17 feature set; rev18-rev21 are corrective host presentation/lifecycle/build revisions required before its runtime acceptance can finish.

## Package Review Requirement

Before packaging, validate XAML/XML/project files, JSON, Markdown links, version identity, test registry uniqueness/method existence, and the complete production diff against rev20. The release ZIP must be CRC-tested and compared file-for-file against the release tree. A clean Windows WPF build and runtime acceptance remain required after packaging.

## Static Release-Tree Review

The completed rev21 release tree passed the repository-level checks available in the packaging environment:

- 426 files total, including 167 Markdown documents, 228 C# source files, and 16 XAML files;
- all 24 XAML/XML/project-format files parse successfully;
- all 3 JSON files parse successfully;
- all 53 relative Markdown links resolve to existing files;
- the automated registry contains exactly 130 unique check names and 130 unique method targets, and every registered target is present in the test source;
- `MainWindow.xaml.cs` contains the explicit `_ = Dispatcher.BeginInvoke(new Action(Close));` acknowledgement and no remaining standalone form of that call;
- `UiMetrics`, `ProportionalGridSplitter`, and `TextBoxInputFilter` remain present in the namespaces referenced by `MainWindow.xaml`;
- `MainWindow.xaml`, `Resources/Styles/ControlStyles.xaml`, and `Application/ToolWindowManager.cs` are byte-identical to the packaged rev20 candidate;
- the complete Core, Plugin SDK, Mock plugin, and PS5 plugin production trees are byte-identical to rev20;
- the only production-source differences from rev20 are the centralized AppInfo revision/title update and the one-line MainWindow dispatcher-operation acknowledgement;
- no `bin` or `obj` directories are included in the release tree.

These checks do not substitute for the mandatory Windows WPF build. The clean Windows build in Gate A is specifically required to confirm that `CS4014` is resolved and that the XAML designer diagnostics disappear after the application assembly builds successfully.
