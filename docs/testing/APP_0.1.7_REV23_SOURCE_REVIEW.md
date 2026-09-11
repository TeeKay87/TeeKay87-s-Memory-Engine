# Application 0.1.7.rev23 Source Review — Debugger Tab Header Rendering Fix

## Scope

Rev23 is a narrow host-WPF correction built directly from the packaged `0.1.7.rev22` source after Windows runtime inspection showed that rev22's reserved right-edge Grid column broke the tab headers. The screenshot demonstrated two regressions at once: neither header label was rendered, and the selected Call Stack header became a solid accent-filled rectangle. The previously accepted MainWindow/tool-window shutdown behavior worked correctly and is explicitly outside this code change.

The review covered the complete source/documentation tree before editing, with particular attention to `ControlStyles.xaml`, `DebuggerWindow.xaml`, the two existing workspace-tab source contracts, AppInfo/version presentation, window-lifecycle code, and the rev17 Call Stack/Stepping paths. No second production tab template exists and both affected Debugger headers consume the same shared `WorkspaceTabItemStyle`, so the correction belongs only in that shared style.

## Production Change

`Resources/Styles/ControlStyles.xaml` removes the rev22 two-column header template and the separate `TabRightEdge` visual. The replacement deliberately returns to the simpler themed structure that preserved header content correctly:

1. one `TabBorder` owns background and border rendering;
2. the normal `ContentPresenter` remains directly inside that border and uses `ContentSource="Header"`;
3. hover, selected, and disabled border colors target `TabBorder` directly;
4. `BorderThickness="1,1,2,1"` widens only the right side, leaving the other three sides at one unit;
5. `SnapsToDevicePixels=True` and `UseLayoutRounding=True` remain active on the actual rendered border; and
6. no Grid column, aligned overlay, templated-parent border-brush background, or separate z-order edge participates in header measurement/rendering.

The two-unit right stroke is intentional. The previous one-unit Border edge was visually lost at the Windows tab-header boundary. Widening the rendered Border itself keeps at least an in-bounds portion of the right side visible without changing the content tree or adding a second visual that can interfere with measurement. Runtime verification remains authoritative for the final appearance.

`Application/AppInfo.cs` advances the host to `0.1.7.rev23` with feature title `Debugger Tab Header Rendering Fix`. No plugin metadata is changed.

## Preserved Functionality

The following production areas were compared against rev22 and are intentionally unchanged:

- `MainWindow.xaml.cs` and `Application/ToolWindowManager.cs`, including the runtime-confirmed MainWindow shutdown cleanup path;
- `DebuggerWindow.xaml` and its Call Stack/Breakpoints content/layout;
- Core and Plugin SDK;
- Mock plugin `1.0.0.rev15`;
- PS5 plugin `0.1.0.rev34`;
- debugger attach/pause/continue/detach, event transport, Threads, Registers, software breakpoints, hardware watchpoints, Call Stack/Call Frames, stepping, Run to Address, Disassembler, and Memory Viewer behavior.

## Automated Source Contracts

The top-level registry remains **130** checks. The existing workspace-tab checks are strengthened in place:

- **Debugger workspace-tab complete border source contract** requires the single `TabBorder`, `BorderThickness="1,1,2,1"`, visible header `ContentPresenter`, layout rounding, and rejects rev22's Grid column plus separate `TabRightEdge`.
- **Debugger selected-tab right-edge rendering source contract** requires selected background/border state to target `TabBorder` directly, requires header foreground inheritance, and rejects the rev22 border-brush-as-background path.

This is preferable to adding another nominal test count because the failed assertions already own the exact presentation contract; their previous implementation was too structural and did not protect header visibility.

## Version/Compatibility

| Component | rev23 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev23` | Corrective host UI revision |
| Feature title | `Debugger Tab Header Rendering Fix` | New host title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev34` | Unchanged |
| Automated registry | `130` | Existing tab contracts strengthened |

## Development Order

Rev23 is consumed by the tab-header correction. If its clean Windows build, **130/130** automated gate, focused UI/lifecycle regression, and carried-forward Call Stack/Stepping runtime acceptance all pass without another code correction, the remaining Debugger milestone becomes `0.1.7.rev24 — Integration, Export and Finalization`.

## Packaging-Environment Static Verification

Before ZIP creation, the completed rev23 tree was checked in the packaging environment:

- complete tree contains **430 files**;
- all **171 Markdown** files were traversed;
- all **228 C#** files and all **16 XAML** files were traversed;
- all **24 XAML/XML/project/props/targets** files parsed successfully as XML;
- all **3 JSON** files parsed successfully;
- all **53 relative Markdown links** resolved to existing repository paths;
- the test registry contains exactly **130 unique check names** and **130 unique method targets**, with every target present in `Program.cs`;
- the rev23 workspace-tab source assertions confirm the single `TabBorder`, visible header presenter, `1,1,2,1` border thickness, direct selected-theme targeting, and absence of the failed rev22 column/`TabRightEdge` structures;
- production-source comparison against rev22 shows changes only in `Application/AppInfo.cs` and `Resources/Styles/ControlStyles.xaml`; and
- no `bin` or `obj` directories are included.

These checks do not replace the required clean Windows WPF build or runtime visual acceptance. The authoritative automated gate remains the Windows **130/130** run defined in the rev23 verification document.
