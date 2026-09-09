# Application 0.1.7.rev4 Source Review — Two-Row Target Header and Button Alignment

> **Status:** Historical source review. Rev4 passed this static review but was superseded by `0.1.7.rev5` after Windows UI review exposed fixed-width first-row overflow at narrower window sizes. Rev5 preserves the PS5 debugger backend, permanent two-row order, and button alignment changes while replacing only the fixed 240-unit row-1 sizing.


## Review Context

This source review compares rev4 against the exact user-supplied `0.1.7.rev3 - PS5 Debug Transport and Target UI Cleanup` package used as the development baseline.

Rev3 was not verified before its first Windows UI review exposed that the target/connection header did not match the intended permanent two-row composition. Rev4 supersedes that candidate before live debugger testing. The last fully verified revision remains rev2: **89/89 PASS** plus complete Mock Debugger runtime/UI acceptance.

This source review is static and does not replace the Windows build, 96-check suite, visual UI acceptance, or live PS5 debugger acceptance documented in `APP_0.1.7_REV4_VERIFICATION.md`.

## Result

**Pre-package static source review: passed.**

## Version and API Metadata

Confirmed:

- application metadata: `0.1.7.rev4`;
- feature title: `Two-Row Target Header and Button Alignment`;
- Plugin API: `2.12.0`;
- Mock: `1.0.0.rev8`, API `2.12.0`;
- PS5: `0.1.0.rev25`, API `2.12.0`.

No plugin/API revision is advanced by rev4 because no platform contract or backend behavior is added.

## Non-Documentation Source Boundary

Relative to the supplied rev3 candidate, rev4 intentionally changes host/test presentation files only:

- `src/TeeKay87.MemoryEngine.App/Application/AppInfo.cs`;
- `src/TeeKay87.MemoryEngine.App/Application/UiMetrics.cs`;
- `src/TeeKay87.MemoryEngine.App/MainWindow.xaml`;
- `src/TeeKay87.MemoryEngine.App/Resources/Styles/ButtonStyles.xaml`;
- `src/TeeKay87.MemoryEngine.App/Dialogs/DataExportDialog.xaml`;
- `tests/TeeKay87.MemoryEngine.Tests/Program.cs`;
- `tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj`.

No production source file is added or removed.

The default main-window startup width changes from `1380` to `1460` so the fixed 240-unit PS5 fields plus existing first-row action buttons fit the intended two-row composition at startup. `MinWidth=1100` is deliberately unchanged.

## Verified Backend/Foundation Preservation

Recursive byte comparison against the supplied rev3 candidate confirms these production trees remain unchanged:

- complete `src/TeeKay87.MemoryEngine.Core/`;
- complete `src/TeeKay87.MemoryEngine.PluginSdk/`;
- complete `src/Plugins/TeeKay87.MemoryEngine.Platform.Mock/`;
- complete `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/`.

Rev4 therefore does not modify the rev3 PS5 debugger command/event transport, TCP 755 ownership, attach/detach/pause/continue behavior, interrupt parser, plugin capability advertisement, or cleanup semantics.

## Permanent Two-Row Header Review

Static review confirms `MainWindow.xaml` defines one permanent first target row and one permanent second action row.

First-row source order is:

```text
Platform
plugin ConnectionSettings (zero or more)
Connect
Disconnect
Target Process
Refresh
Set Active Target
```

Second-row left action order is:

```text
Reload Plugins
Disassembler...
Debugger...
```

Future host/tool buttons are documented to append to row 2. Connection/process/memory-region error TextBlocks remain conditional/collapsed and are not a third permanent control row.

## Shared 240-Unit Input Metric

`UiMetrics.TopTargetInputWidth` is centralized at `240d` and consumed by:

- Platform selector container;
- every generic plugin `ConnectionSettings` field container;
- Target Process selector container.

PS5 host/IP and Port therefore receive identical ordinary-input width. The plugin contracts do not carry WPF sizing data, preserving the platform-neutral host-rendering boundary for future plugins.

The Theme ComboBox lives in the separate application bar and is intentionally outside this target/plugin metric.

## Shared Button Layout Review

The shared ordinary `ButtonBaseStyle` retains:

- `Height={x:Static application:UiMetrics.StandardControlHeight}`;
- `StandardControlHeight = 34d`;
- existing semantic colors, templates, corners, interaction overlays, disabled behavior, and horizontal padding.

Only internal vertical content layout changes:

- common Padding: `14,8` -> `14,2`;
- `HorizontalContentAlignment=Center` retained;
- `VerticalContentAlignment=Center` retained;
- generated `AccessText` and `TextBlock` content receives explicit `VerticalAlignment=Center`.

The normal-height Data Export **Select All** local override changes `8,3` -> `8,2`. The intentionally compact 28-unit Saved Address Remove button remains 28 units with its existing `8,2` padding.

## Verification Registry Preservation

The rev3 candidate contains **94** registered checks. Rev4 contains **96**:

- every rev3 check name remains present;
- no prior check is removed;
- no duplicate check name is introduced;
- exactly two new checks are added:
  1. `Main workspace two-row target header standard`;
  2. `Shared button content alignment and vertical padding`.

The existing main-workspace source contract is also updated to validate the corrected row ordering rather than the superseded rev3 layout.

## Project and Documentation Structure

Static review verifies before packaging:

- all **21** WPF/project/props/targets XML files are structurally well-formed;
- all checked relative Markdown links resolve (**39** relative links across the current documentation set);
- no `bin` or `obj` build output is included;
- current AppInfo/README/CHANGELOG/development-plan metadata agrees on rev4;
- current UI, Plugin SDK, debugger, Mock, and PS5 documentation describes the rev4 boundary;
- rev3 testing/source-review documents remain historical records and are status-annotated as superseded; they are not rewritten as if rev3 had passed acceptance.

## Remaining Verification

External gates remain:

1. clean Windows Release build;
2. warnings-as-errors compile acceptance;
3. verification executable ending with `All 96 checks passed.`;
4. runtime review of the exact two-row/240-unit layout and button alignment in all themes;
5. full live PS5 debugger acceptance inherited from the unverified rev3 candidate;
6. focused Mock/PS5 regression smoke testing.

## Status

**Static source review: passed.**

**Windows build/96-check verification: pending.**

**Main-window/button runtime UI verification: pending.**

**Live PS5 debugger hardware verification: pending.**
