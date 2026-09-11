# Application 0.1.7.rev22 Source Review — Debugger Tab Right Edge Layout Fix

## Scope

Rev22 is a focused WPF presentation correction built directly from the packaged `0.1.7.rev21` candidate after its Windows automated gate completed at **130/130 PASS**. Runtime testing then produced two separate outcomes: MainWindow/tool-window shutdown behaved correctly, while the right vertical edge of the reusable **Breakpoints / Watchpoints** and **Call Stack** headers remained absent. The shutdown implementation is therefore preserved; only the shared workspace-tab header geometry is changed.

No Core, Plugin SDK, Mock, PS5, debugger transport, Call Stack/Call Frames, stepping, breakpoint/watchpoint, register, scanner, Saved Addresses, Memory Viewer, Disassembler, or platform-specific behavior is in scope.

## Mandatory Pre-Change Review

Before editing production code, the complete packaged rev21 documentation tree and complete source/configuration inventory were read and reviewed. The pre-change tree contains **426 files**: 167 Markdown documents, 228 C# files, 16 XAML files, six project files, three JSON theme files, and the remaining solution/build/script files. The review specifically confirmed:

- `WorkspaceTabControlStyle` and `WorkspaceTabItemStyle` are the only application-owned TabControl/TabItem styles in the current source tree, so the correction belongs in the existing shared style rather than a new Debugger-local template;
- DebuggerWindow is the only current production consumer of those workspace-tab styles and applies the same `WorkspaceTabItemStyle` to exactly the two affected headers;
- rev21's `WorkspaceTabItemStyle` uses an unconstrained root `Grid` plus a `TabRightEdge` overlay with `HorizontalAlignment="Right"`; the overlay has no dedicated measure/arrange column of its own;
- the main `TabBorder` still fills that same grid and the explicit edge is positioned at the same right-side layout boundary that remained absent in Windows runtime testing;
- the runtime screenshot confirms the selected Call Stack header receives its accent top/left state but still has no visible final vertical edge, so the issue is geometry/render placement rather than missing selected-state theming;
- no other shared control style already implements a reserved-edge tab geometry that should be reused instead;
- `MainWindow.xaml.cs` and `Application/ToolWindowManager.cs` contain the now-working shutdown coordination and must not be changed for this visual correction;
- the existing automated tab source contracts prove only that an edge visual exists, not that WPF allocates independent width for it; those contracts need to be strengthened rather than duplicated;
- application version/revision is centralized in `Application/AppInfo.cs`; Plugin API and plugin versions do not need to change for a host-only WPF template correction.

## Runtime Failure Being Corrected

Rev21 passed the clean automated gate at **130/130**, and the application launched successfully. Runtime inspection still showed both workspace headers visually ending at their content/background boundary without a right vertical stroke. The selected Call Stack header displayed the selected accent along its top and left edges, proving that `IsSelected` and the shared `AccentBrush` state were active. The missing right side therefore survived both the rev19 inset attempt and the rev20/rev21 overlay attempt.

The rev20/rev21 template placed `TabRightEdge` as an aligned overlay inside a grid whose desired width continued to be established by the main tab border/content. That arrangement was source-valid but did not force WPF to reserve a separate layout unit for the right-edge visual. Rev22 removes that dependency.

## Production Change

`Resources/Styles/ControlStyles.xaml` keeps the existing `WorkspaceTabControlStyle` unchanged and changes only the `WorkspaceTabItemStyle` template:

1. the template root now enables `UseLayoutRounding` in addition to device-pixel snapping;
2. the root declares two columns: normal header content in the first column and a fixed `Width="1"` second column;
3. `TabBorder` spans both columns so the background, padding, corner geometry, and overall header remain one visual surface;
4. `TabBorder` draws `1,1,0,1`, deliberately omitting its right stroke so the explicit edge is the only right-side border;
5. `TabRightEdge` occupies `Grid.Column="1"`, which makes its width part of the template's measure/arrange result instead of an overlay aligned to the outer boundary;
6. the edge binds directly to the templated TabItem's `BorderBrush`, preserving normal, hover, selected, and disabled theme-state transitions;
7. `Panel.ZIndex="2"` keeps the edge above the tab background; and
8. the existing four-unit external TabItem spacing, header padding, fonts, rounded top corners, content layout, and Debugger bindings remain unchanged.

No C# production file requires a new `using` directive because the production correction is XAML-only apart from centralized AppInfo metadata.

## Verification Contract Change

The registry remains **130** top-level checks. Two existing source contracts are strengthened:

- **Debugger workspace complete tab-border source contract** now requires the explicit column definitions, a fixed one-unit edge column, `TabBorder` spanning both columns, and the main border omitting its own right stroke;
- **Debugger selected-tab right-edge rendering source contract** now requires `TabRightEdge` to occupy the fixed column, bind directly to the templated parent's `BorderBrush`, and render above the tab background.

This replaces the previous assertions for root-grid right margin, `HorizontalAlignment="Right"`, and the overlay's template binding. Keeping the same top-level check count avoids representing a correction to an existing requirement as a new feature.

## Regression Boundaries

Rev22 must preserve the following production files byte-for-byte from rev21:

- `src/TeeKay87.MemoryEngine.App/MainWindow.xaml.cs`;
- `src/TeeKay87.MemoryEngine.App/Application/ToolWindowManager.cs`;
- `src/TeeKay87.MemoryEngine.App/DebuggerWindow.xaml`;
- the complete Core production tree;
- the complete Plugin SDK production tree;
- the complete Mock plugin production tree; and
- the complete PS5 plugin production tree.

The only production-source changes allowed are the shared `WorkspaceTabItemStyle` geometry and `AppInfo` revision/feature identity.

## Version Review

| Component | rev22 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev22` | Corrective host UI revision |
| Feature title | `Debugger Tab Right Edge Layout Fix` | New rev22 title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev34` | Unchanged |
| Automated registry | `130` | Existing tab contracts strengthened |

## Development-Plan Effect

Rev22 is consumed by the tab-layout correction. The planned **Integration, Export and Finalization** milestone moves to `0.1.7.rev23`. The Call Stack/Stepping implementation remains the rev17 feature set; rev18-rev22 are corrective host UI/lifecycle/build revisions required before its runtime acceptance can finish.

## Package Review Requirement

Before packaging, validate all XAML/XML/project files, JSON, Markdown links, version identity, test-registry uniqueness/method existence, and the complete production diff against rev21. The release ZIP must be CRC-tested and compared file-for-file against the release tree. The Windows WPF build and visual runtime gate remain authoritative for the tab edge because the packaging environment cannot render the application UI.

## Static Release-Tree Review

The completed rev22 working tree passed the repository-level checks available in the packaging environment before ZIP creation:

- **428 files** total, including 169 Markdown documents, 228 C# source files, and 16 XAML files;
- all **24** XAML/XML/project-format files parse successfully;
- all **3** JSON theme files parse successfully;
- all **53** relative Markdown links resolve to existing files;
- the automated registry contains exactly **130** unique check names and **130** unique method targets, and every registered target exists in the test source;
- the strengthened tab contracts require the dedicated one-unit right-edge column, the spanning main border with no outer right stroke, direct templated-parent border-brush binding, and explicit edge z-order;
- the complete production diff from packaged rev21 is limited to `Application/AppInfo.cs` and `Resources/Styles/ControlStyles.xaml`;
- `MainWindow.xaml.cs`, `Application/ToolWindowManager.cs`, and `DebuggerWindow.xaml` are byte-identical to rev21;
- the complete Core, Plugin SDK, Mock plugin, and PS5 plugin production trees are byte-identical to rev21; and
- no `bin` or `obj` directories are present.

These checks do not substitute for the mandatory Windows WPF build or visual Gate B. Rev22 specifically exists because earlier source-only checks could not prove the final tab-edge rendering at runtime.
