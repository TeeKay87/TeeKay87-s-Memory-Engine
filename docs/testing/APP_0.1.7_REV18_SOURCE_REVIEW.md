# Application 0.1.7.rev18 Source Review — Debugger Tab Theme Fix

## Scope

Rev18 is a focused correction built directly from the `0.1.7.rev17` Call Stack, Call Frames and Stepping package after that package passed **124/124** automated checks but exposed an unthemed operating-system-default WPF tab surface during the first runtime/UI inspection.

The correction must not alter the rev17 debugger feature implementation. Only shared WPF tab presentation, the Debugger's style references, focused regression coverage, host version metadata, and documentation are in scope.

## Pre-Change Review

Before the production edit, the complete rev17 documentation tree and source/configuration tree were reviewed. The review confirmed:

- `DebuggerWindow.xaml` introduced the project's first `TabControl` but did not apply an application-owned `TabControl` or `TabItem` template;
- `ControlStyles.xaml` already owns shared TextBox, ComboBox, CheckBox, DataGrid, splitter, ToolTip, Expander, ContextMenu, and other common control presentation, but contained no tab styles;
- the existing external theme system already exposes every semantic brush needed for tabs through `DynamicResource`;
- no Core, Plugin SDK, Mock plugin, PS5 plugin, debugger ViewModel, call-stack service, step service, breakpoint/watchpoint path, or transport change is needed to correct the visual defect.

## Source Changes

### Shared control styles

`ControlStyles.xaml` now defines:

- `WorkspaceTabControlStyle`, which owns the selected-content background and border using `PanelBackgroundBrush` and `BorderBrush`;
- `WorkspaceTabItemStyle`, which owns tab header text/background/border and normal/hover/selected/disabled states using the existing theme brushes.

Both templates use `DynamicResource`; no Light/Dimmed/Dark color value is duplicated in XAML.

### Debugger workspace

The existing upper-right `TabControl` now uses `WorkspaceTabControlStyle`, and both explicit `TabItem` instances use `WorkspaceTabItemStyle`. Tab content, layout, bindings, commands, capability visibility, splitters, and debugger behavior are unchanged.

### Verification source

The test project now copies `ControlStyles.xaml` into its fixture output and registers one additional source-contract check. The check requires both shared styles, verifies that they use the expected semantic theme resources, and verifies that both Debugger tabs opt into those styles. The registry increases from **124 to 125** checks.

## Version Review

| Component | rev18 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev18` | Corrective host/UI revision |
| Feature title | `Debugger Tab Theme Fix` | New rev18 title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev34` | Unchanged |

No platform plugin is advanced because rev18 changes no platform-owned code or behavior.

## Development-Plan Effect

Rev17 is preserved as the revision that implemented Call Stack/Call Frames and stepping and passed **124/124**, but it is superseded before runtime acceptance because of the new tab presentation defect. Rev18 is the corrective candidate. The previously planned Integration, Export and Finalization milestone therefore moves to **rev19** rather than consuming the corrective revision number.

## Acceptance Requirement

Rev18 is not verified by source review alone. Windows must report **125/125**, then the Breakpoints / Watchpoints and Call Stack tab area must be visually confirmed in Light, Dimmed, and Dark with no system-white tab strip/content surface and readable selected/unselected states. After that focused visual correction passes, the remaining rev17 Mock/live-PS5 Call Stack and stepping gates resume against rev18.

## Final Static Package Review

The prepared rev18 tree contains **419 files**. Relative to the rev17 package, the rev18 delta contains **2 added files, 16 changed files, and 0 removed files**.

Static preparation checks completed before packaging:

- all **24** XAML/XML/project/props/targets files parsed successfully;
- all **3** JSON files parsed successfully;
- all **161** Markdown documents were present and the **53** relative Markdown links resolved to existing paths;
- the automated registry contains **125** unique test names and **125** unique method targets;
- both Debugger tabs reference `WorkspaceTabItemStyle`, and the upper-right `TabControl` references `WorkspaceTabControlStyle`;
- the focused rev18 tab-theme source-contract assertions pass against the prepared source fixtures;
- Core, Plugin SDK, Mock plugin source, and PS5 plugin source are byte-identical to the rev17 package.

These static checks do not replace the required Windows **125/125** build/test gate or runtime visual/Mock/live-PS5 acceptance.
## Post-Package Runtime Outcome

The rev18 source correction successfully removed the system-default white Debugger tab surface seen in rev17. Before rev18 completed its full carried-forward acceptance cycle, runtime inspection showed that the custom tab header's right-hand border was clipped at the `TabPanel` layout edge. The same review also established that WPF ownership on the modeless Debugger, Disassembler, and Memory Viewer kept those tools permanently above MainWindow, which did not match the desired desktop activation behavior.

Both corrections are host/UI concerns and require no Core, Plugin SDK, Mock, or PS5 backend change. They are assigned to `0.1.7.rev19 — Debugger UI and Window Lifecycle Fixes`. The planned Integration, Export and Finalization milestone moves to rev20.

