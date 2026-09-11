# Application 0.1.7.rev20 Source Review — Debugger Tab Border and Shutdown Re-entry Fixes

## Scope

Rev20 is a focused corrective host/WPF revision built directly from the `0.1.7.rev19` package after rev19 passed **128/128** automated checks but failed the first focused runtime UI/shutdown inspection. Two defects are in scope:

1. the selected/last Breakpoints / Watchpoints + Call Stack workspace header must always render its complete right vertical edge; and
2. closing MainWindow with tracked modeless tools open must never call `Close()` re-entrantly while WPF is still processing the original MainWindow close request.

No debugger contracts, backend transport, Call Stack data, stepping semantics, breakpoint/watchpoint behavior, register logic, scanner logic, Memory Viewer data/navigation, or Disassembler data/navigation are changed.

## Pre-Change Review

Before production edits, the complete rev19 Markdown documentation tree and complete source/configuration inventory were reviewed. The review confirmed:

- `WorkspaceTabItemStyle` is the sole reusable style used by both Debugger workspace headers; no second tab template exists that should be changed instead;
- the rev19 `Margin="0,0,1,0"` lived on `TabBorder` itself and the runtime screenshot proved that this did not guarantee the selected/last header's right stroke;
- all tab colors/states already flow through shared `DynamicResource` brushes, so the fix should change geometry/rendering only rather than add palette values;
- `ToolWindowManager` already centralizes all three modeless launch paths and already performs the intended DataContext cleanup/child-window close sequence;
- `DebuggerViewModel.DisposeAsync()` is idempotent, and Disassembler/Memory Viewer disposal remains idempotent;
- the runtime shutdown exception can occur even with an otherwise idle/detached Debugger because `CloseAllAsync()` may complete synchronously;
- rev19 `async void OnClosing` then executed a direct `Close()` in `finally`, allowing the second close request to happen before the first `OnClosing` invocation had returned;
- no Core, Plugin SDK, Mock, or PS5 change is required for either defect.

The complete code inventory was also reviewed for an existing general tab-right-edge helper or an existing dispatcher-based application-shutdown coordinator. None exists beyond the shared tab style and `ToolWindowManager`, so rev20 extends those existing host paths rather than introducing duplicate infrastructure.

## Source Changes

### Explicit in-bounds workspace-tab right edge

`WorkspaceTabItemStyle` retains its existing shared theme ownership and interaction model. The template now uses a root `Grid` with a one-unit right inset. Inside it:

- `TabBorder` continues to draw the normal complete header surface, border, rounded top corners, padding, and content;
- `TabRightEdge` is a dedicated one-unit visual aligned to the right edge inside the template allocation;
- the right-edge visual binds to the TabItem's `BorderBrush`, so it follows normal, hover, selected, and disabled state changes automatically;
- hover/selected/disabled triggers now set the templated parent's `BorderBrush`, allowing both `TabBorder` and `TabRightEdge` to consume the same state brush;
- the selected state continues to use `AccentBrush`; no literal theme color or Debugger-local style is introduced.

The explicit edge begins below the rounded top-right corner and stops above the bottom border so it complements rather than overwrites the existing border geometry.

### MainWindow close re-entry prevention

`MainWindow.OnClosing` is now a synchronous override. When tracked modeless tools remain:

1. the first close request is cancelled;
2. the existing shutdown-in-progress guard prevents duplicate coordination;
3. MainWindow is disabled;
4. `CompleteToolWindowShutdownAsync()` is started;
5. the helper awaits the unchanged `ToolWindowManager.CloseAllAsync()` cleanup path;
6. the completed guard is armed; and
7. the final `Close()` is posted with `Dispatcher.BeginInvoke(new Action(Close))`.

The dispatcher post is the key correction. Even if tracked tool cleanup completes synchronously, the final close callback cannot run until the dispatcher regains control after the original `OnClosing` call has returned. The next `OnClosing` invocation sees `_toolWindowShutdownCompleted == true` and follows the normal base close path.

`System.Threading.Tasks` is added because MainWindow now has an explicit `Task`-returning shutdown helper. No additional namespace or platform dependency is introduced.

## Regression Boundaries

Rev20 must preserve:

- all rev19 independent modeless z-order behavior;
- initial CenterOwner placement before owner release;
- normal `Window.Close()` on each tracked child after cleanup;
- Debugger asynchronous detach/session release;
- Disassembler and Memory Viewer synchronous disposal;
- modal dialog ownership/blocking;
- all debugger Call Stack and stepping behavior;
- all breakpoint/watchpoint/register behavior;
- Plugin API `2.15.0`;
- Mock `1.0.0.rev15`;
- PS5 `0.1.0.rev34`.

## Verification Coverage

The existing rev19 source checks are strengthened rather than discarded. Two additional checks are registered:

- **Debugger selected-tab right-edge rendering source contract** requires the explicit `TabRightEdge`, one-unit width, right alignment, template-bound border brush, and selected accent state routing.
- **Main-window shutdown re-entry guard source contract** requires a dedicated asynchronous helper, dispatcher-posted final close, completed guard, and rejects a direct standalone `Close()` inside that helper.

The verification registry therefore increases from **128 to 130** checks.

## Version Review

| Component | rev20 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev20` | Corrective host/UI revision |
| Feature title | `Debugger Tab Border and Shutdown Re-entry Fixes` | New rev20 title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev34` | Unchanged |

## Development-Plan Effect

Rev20 is consumed by the runtime corrections above. The planned **Integration, Export and Finalization** revision moves to `0.1.7.rev21`. Rev17 remains the revision that introduced Call Stack/Stepping; rev18-rev20 are corrective host/UI/lifecycle revisions required before that runtime acceptance can finish.

## Static Review Requirement

Before packaging, validate all XAML/XML/project files, JSON files, Markdown links, code delimiters for changed C# files, test registry uniqueness/method existence, and the production diff against rev19. The packaged ZIP must then be CRC-tested and compared file-for-file against the release tree.

## Final Static Package Review

The completed rev20 source tree contains **424 files**, including **165 Markdown documents**, **228 C# source files**, and **16 XAML files**. Final static preparation confirmed:

- all **24** XML-based XAML/project/build files parse successfully;
- all **3** JSON files parse successfully;
- all **53** relative Markdown links resolve to existing files;
- the automated verification registry contains exactly **130 unique test names** and **130 unique test method targets**, and every registered target exists in the test source;
- changed C# files have balanced code delimiters after comments and string/character literals are excluded from the check;
- the production source diff against rev19 is limited to `Application/AppInfo.cs`, `MainWindow.xaml.cs`, and `Resources/Styles/ControlStyles.xaml` in the WPF host;
- Core, Plugin SDK, Mock plugin, and PS5 plugin production trees are byte-for-byte unchanged from rev19;
- no `bin` or `obj` directory is present in the release tree.

A .NET/Windows WPF runtime is not available in the packaging environment, so the static review does **not** replace Gate A or either focused runtime regression. The packaged candidate must still produce **130/130 PASS** on Windows, then pass the complete tab-edge and MainWindow-shutdown checks before the carried-forward Call Stack/Stepping acceptance resumes.

## Post-Package Windows Build Result

The first Windows build of the packaged rev20 candidate did not reach the 130-check automated gate. Visual Studio reported `CS4014` in `MainWindow.xaml.cs` on the final `Dispatcher.BeginInvoke(new Action(Close))` call. `Dispatcher.BeginInvoke` returns an awaitable `DispatcherOperation`; because the repository enables `TreatWarningsAsErrors`, leaving that operation unacknowledged is a build-stopping error.

The Error List simultaneously showed XAML designer/type-resolution diagnostics in `MainWindow.xaml` for framework and application types including `System.Object`, `UiMetrics`, `ProportionalGridSplitter`, and `TextBoxInputFilter`. The rev20 source tree still contains those types and their existing namespaces, so no separate XAML type removal was identified during source review. The clean rev21 Windows build remains the authoritative test of whether those designer diagnostics disappear once the application assembly compiles.

Rev20 is therefore superseded before automated/runtime acceptance. Its tab-right-edge and shutdown-sequencing code is carried unchanged into rev21 except for the explicit acknowledgement of the dispatcher operation.
