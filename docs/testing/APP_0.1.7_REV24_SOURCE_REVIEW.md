# Application 0.1.7.rev24 Source Review — Debugger Mode Switcher Layout Refresh

## Scope Reviewed

The complete supplied `0.1.7.rev23` source/documentation tree was traversed before editing: all Markdown documentation, C# sources, XAML resources/views, project/build files, JSON themes, tests, and scripts. The review focused on the Debugger upper-right workspace, shared button/control styles, `DebuggerViewModel`, existing capability gating, splitter geometry, modeless-window lifecycle, and the rev18-rev23 tab correction history.

Runtime evidence from rev23 established two facts before this change: the Windows suite passed **130/130**, and MainWindow-driven tool cleanup still worked, but the workspace-tab right edge remained visibly clipped. Since `WorkspaceTabControlStyle`/`WorkspaceTabItemStyle` had no production consumer outside `DebuggerWindow.xaml`, another tab-template edge workaround would add complexity without preserving verified behavior. The existing shared button template already supplies theme, hover, pressed, focus, disabled, and text behavior suitable for a compact selector.

## Production Change

### Debugger workspace selection

`DebuggerViewModel` now owns the presentation-neutral selection state for the two available upper workspaces. Two RelayCommands select Breakpoints / Watchpoints or Call Stack. Exactly one available workspace is exposed as selected. When both are available, Breakpoints / Watchpoints is the initial view; a backend that exposes only Call Stack starts there automatically. This state changes only presentation visibility and does not modify debugger session state or backend data.

`DebuggerWindow.xaml` removes the active `TabControl` and `TabItem` elements. Two compact selector Buttons remain capability-gated and bind to the new commands. The two existing content grids bind visibility to the corresponding selected-state properties. No backend service is created, disposed, refreshed, or changed by the act of switching views.

### Shared selector button style

`ButtonStyles.xaml` adds `DebuggerWorkspaceSwitchButtonStyle`, derived from `SecondaryButtonStyle`. It intentionally uses 28-unit height, 12-point text, and compact padding while retaining the standard shared Button ControlTemplate. Two small derived styles bind the corresponding selected-state boolean and add a two-unit `AccentBrush` outline. No hardcoded color or duplicate button template is introduced.

The now-unused active `WorkspaceTabControlStyle` and `WorkspaceTabItemStyle` are removed from `ControlStyles.xaml`. Historical documents remain unchanged where they describe the exact older revisions.

### Content layout and size

The duplicated inner Breakpoints / Watchpoints and Call Stack title rows and their count labels are removed. Breakpoints Enable/Disable/Remove/Remove All/Disassembler remain left-aligned in the footer; Add/Refresh move to the right side of that same row. Call Stack retains SP/FP/Return details, keeps Disassembler/Memory Viewer on the left footer, and moves Refresh to the right footer. The DataGrid definitions and bindings are preserved.

Debugger default size changes from 1120x720 to 1240x780. Its minimum remains 820x520 and MainWindow remains larger at 1460x880. Existing 36/64 horizontal and 13/7 vertical split ratios remain unchanged.

## Preserved Functionality

No production change is made to Core, Plugin SDK, Mock, PS5, debugger transport, call-stack retrieval, stepping/run-to composition, breakpoints/watchpoints, register/thread services, Disassembler, Memory Viewer, target-generation safety, or window shutdown lifecycle. `MainWindow.xaml.cs` and `ToolWindowManager.cs` remain byte-identical to rev23.

## Automated Source Contracts

The top-level registry remains **130** checks. Three old tab-specific contracts are rewritten in place:

- **Debugger workspace mode switcher source contract** rejects active TabControl/TabItem usage and verifies the two selector commands plus exclusive panel visibility.
- **Debugger workspace switch-button theme source contract** verifies reuse of the shared Secondary button template, compact metrics, accent selected outlines, and removal of the unused active workspace-tab styles.
- **Debugger workspace panel layout source contract** verifies the new default size, absence of the removed inner title/count labels, and the consolidated left/right footer actions.

The existing Call Stack/Stepping contract is updated to expect selector buttons/commands instead of TabItems while preserving all functional assertions.

## Version/Compatibility

| Component | rev24 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev24` | Corrective host UI/layout revision |
| Feature title | `Debugger Mode Switcher Layout Refresh` | New host title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev34` | Unchanged |
| Automated registry | `130` | Three existing tab contracts rewritten |

## Development Order

Rev24 is consumed by the Debugger selector/layout correction. If its clean Windows build, **130/130** automated gate, focused selector/layout/window-lifecycle regression, and carried-forward Call Stack/Stepping runtime acceptance all pass without another code correction, the remaining Debugger milestone becomes `0.1.7.rev25 — Integration, Export and Finalization`.
