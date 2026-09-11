# Application 0.1.7.rev19 Source Review — Debugger UI and Window Lifecycle Fixes

## Scope

Rev19 is a focused corrective host/UI revision built directly from the `0.1.7.rev18` package before the carried-forward Call Stack/Stepping runtime acceptance resumed. Two items are in scope:

1. keep the complete right-hand border of the application-owned Breakpoints / Watchpoints and Call Stack tab headers visible; and
2. make the modeless Debugger, Disassembler, and Memory Viewer participate in normal desktop z-order while preserving deterministic cleanup/closure when MainWindow exits.

No Core, Plugin SDK, Mock plugin, PS5 plugin, debugger ViewModel behavior, call-stack service, stepping service, breakpoint/watchpoint logic, register path, scanner behavior, memory-access behavior, or disassembly logic is changed.

## Pre-Change Review

Before production edits, the complete rev18 documentation tree and source/configuration tree were reviewed. The source review confirmed:

- `WorkspaceTabControlStyle` / `WorkspaceTabItemStyle` already own all Debugger tab palette and interaction states; only the final right-hand `TabBorder` stroke was visually clipped at the `TabPanel` allocation edge;
- the Debugger, Disassembler, and Memory Viewer are the only application windows opened with modeless `Show()` from MainWindow;
- all three modeless launch paths set `Owner = this`, and WPF's owned-window z-order rule therefore keeps those tools above MainWindow;
- modal workflows use `ShowDialog()` and must keep their existing owner relationships;
- `DebuggerViewModel` implements `IAsyncDisposable` and its disposal releases the debugger coordinator/session asynchronously;
- `DisassemblerViewModel` and `MemoryViewerViewModel` implement idempotent `IDisposable` cleanup;
- DebuggerWindow, DisassemblerWindow, and MemoryViewerWindow already execute those cleanup paths from their normal `Closed` handlers;
- there was no existing shared modeless-window lifecycle manager to reuse.

## Source Changes

### Workspace tab border

`WorkspaceTabItemStyle` retains its existing theme-aware template, brush bindings, padding, external spacing, corner radius, and normal/hover/selected/disabled triggers. The rendered `TabBorder` now has a one-unit right inset (`Margin="0,0,1,0"`) so the complete right vertical stroke remains inside the header layout allocation instead of being clipped at the edge.

### Shared modeless tool-window manager

`Application/ToolWindowManager.cs` centralizes the modeless host-window lifecycle:

- tracks every Debugger, Disassembler, and Memory Viewer opened through MainWindow;
- temporarily assigns MainWindow as `Owner` only while `Show()` establishes the existing `CenterOwner` placement;
- clears `Owner` immediately after modeless Show so normal desktop activation/z-order applies;
- never uses `Topmost`;
- removes windows from the tracked set when they close normally;
- on coordinated shutdown, disables all currently tracked tools before any asynchronous cleanup can yield, invokes `IAsyncDisposable` DataContext cleanup before `IDisposable`, then calls normal `Window.Close()`;
- attempts the cleanup/close sequence for every tracked tool and reports aggregated shutdown errors to the diagnostic trace.

This behavior remains entirely in the WPF host. It does not create a Core or Plugin SDK window concept.

### MainWindow shutdown coordination

MainWindow now owns one `ToolWindowManager` instance and routes all three modeless launch paths through it. Modal dialogs remain unchanged.

When MainWindow receives a close request while tracked tools remain open, it cancels that first close, disables the main surface, and awaits `CloseAllAsync()`. The manager disables the tracked tool windows before its first asynchronous cleanup operation so no navigation or secondary-tool launch can race shutdown. After tool cleanup and closure finish, MainWindow issues its final close. This keeps the WPF dispatcher alive while Debugger asynchronous detach/session-release cleanup runs. The final MainWindow close uses the existing `OnClosed` path for visible-row deregistration and `MainWindowViewModel` disposal.

If no modeless tool is open, MainWindow follows its prior direct close path.

## Regression Boundaries

Rev19 must not change:

- debugger attach/pause/continue/detach commands or transport;
- Call Stack, Step Into, Step Over, Step Out, or Run to Address behavior;
- breakpoint/watchpoint state or temporary-breakpoint cleanup semantics;
- Disassembler or Memory Viewer data/navigation semantics;
- modal dialog ownership/blocking;
- Core or Plugin SDK public contracts;
- Mock or PS5 plugin versions/capabilities.

## Version Review

| Component | rev19 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev19` | Corrective host/UI revision |
| Feature title | `Debugger UI and Window Lifecycle Fixes` | New rev19 title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev34` | Unchanged |

## Development-Plan Effect

Rev17 remains the revision that implemented Call Stack/Call Frames and stepping and passed **124/124** automated checks. Rev18 remains the first tab-theme correction. Rev19 is now the corrective candidate required before those runtime gates resume. Integration, Export and Finalization therefore moves to **rev20**.

## Acceptance Requirement

Rev19 requires a clean Windows automated result of **128/128**, then focused runtime acceptance of:

- complete tab header borders in Light, Dimmed, and Dark;
- MainWindow and each modeless tool moving above one another through ordinary activation;
- multiple modeless tools remaining independently activatable;
- MainWindow shutdown closing every open tool after its established cleanup, including an attached Debugger;
- unchanged modal-dialog ownership behavior.

After those corrective gates pass, the complete carried-forward Mock/live-PS5 Call Stack and stepping acceptance resumes against rev19.

## Final Static Package Review

The completed rev19 source tree contains **422 files**, including **163 Markdown documents**, **228 C# source files**, and **16 XAML files**. Final static review confirmed:

- all **24** XML-based XAML/project/build files parse successfully;
- all **3** JSON files parse successfully;
- all **53** relative Markdown links resolve to existing files;
- the automated test registry contains exactly **128 unique test names** and **128 unique test method targets**, and every registered target exists in the test source;
- the three new rev19 source-contract checks match the completed tab-border, modeless z-order, and coordinated-shutdown implementation;
- changed C# files have balanced code delimiters after comments/string literals are excluded from the check;
- the production diff against the rev18 base is limited to `AppInfo`, `MainWindow`, `ControlStyles`, and the new host-only `ToolWindowManager`; Core, Plugin SDK, Mock, and PS5 production sources are byte-for-byte unchanged.

A .NET SDK/Windows WPF toolchain is not available in the packaging environment, so this static review does **not** replace Gate A. The packaged candidate must still produce **128/128 PASS** with the normal Windows verification runner before focused runtime acceptance begins.

## Runtime Follow-up

Rev19 later passed **128/128** automated checks but did not pass focused runtime acceptance. The selected/last workspace tab still clipped its right edge, demonstrating that the source-level one-unit `TabBorder` margin assertion was insufficient as a visual guarantee. MainWindow shutdown with Debugger open also raised WPF `InvalidOperationException` because a direct final `Close()` could be issued before the original `OnClosing` stack had unwound. These two findings are intentionally corrected in rev20 rather than rewriting rev19's historical implementation record.
