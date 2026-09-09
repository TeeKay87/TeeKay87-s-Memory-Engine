# Application 0.1.7.rev5 Source Review — Responsive Target Header Input Sizing

## Review Context

This source review compares rev5 against the exact user-supplied `0.1.7.rev4 - Two-Row Target Header and Button Alignment` package used as the development baseline.

Rev4 was built and visually reviewed on Windows before its automated/live-PS5 acceptance was completed. That UI review confirmed the intended permanent two-row header composition and shared button text alignment, then exposed one remaining layout defect: the fixed 240-unit ordinary row-1 inputs could extend beyond the target bar's right edge when the main window was narrowed.

Rev5 supersedes rev4 before the remaining automated/live-PS5 gates. The last fully verified debugger revision remains rev2: **89/89 PASS** plus complete focused Mock Debugger runtime/UI acceptance. Rev5 retains the rev3 PS5 debugger backend and rev4 two-row/button changes while correcting only responsive row-1 input sizing.

This review is static and does not replace the Windows build, 97-check suite, responsive runtime/UI acceptance, or live PS5 debugger acceptance documented in `APP_0.1.7_REV5_VERIFICATION.md`.

## Result

**Pre-package static source review: passed.**

## Version and API Metadata

Confirmed:

- application metadata: `0.1.7.rev5`;
- feature title: `Responsive Target Header Input Sizing`;
- Plugin API: `2.12.0`;
- Mock: `1.0.0.rev8`, API `2.12.0`;
- PS5: `0.1.0.rev25`, API `2.12.0`.

No plugin or Plugin API revision is advanced because rev5 changes only host presentation and verification/documentation.

## Non-Documentation Source Boundary

Relative to the supplied rev4 candidate, rev5 intentionally changes only these non-documentation files:

- `src/TeeKay87.MemoryEngine.App/Application/AppInfo.cs`;
- `src/TeeKay87.MemoryEngine.App/Application/UiMetrics.cs`;
- `src/TeeKay87.MemoryEngine.App/MainWindow.xaml`;
- `src/TeeKay87.MemoryEngine.App/MainWindow.xaml.cs`;
- `tests/TeeKay87.MemoryEngine.Tests/Program.cs`.

No production source file is added or removed.

Recursive byte comparison against the supplied rev4 package confirms these trees remain unchanged:

- complete `src/TeeKay87.MemoryEngine.Core/`;
- complete `src/TeeKay87.MemoryEngine.PluginSdk/`;
- complete `src/Plugins/TeeKay87.MemoryEngine.Platform.Mock/`;
- complete `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/`.

The rev3/rev4 PS5 debugger command/event transport, TCP 755 ownership, attach/detach/pause/continue behavior, interrupt parser, capability advertisement, and cleanup semantics are therefore untouched by rev5.

The shared `ButtonStyles.xaml` and all rev4 button-content changes are also unchanged by rev5. Standard button height remains 34 units with the already-established centered `14,2` content layout.

## Permanent Two-Row Header Preservation

Static review confirms the first-row source order remains:

```text
Platform
plugin ConnectionSettings (zero or more)
Connect
Disconnect
Target Process
Refresh
Set Active Target
```

The second-row action order remains:

```text
Reload Plugins
Disassembler...
Debugger...
```

Rev5 does not add a third permanent row, alter Active Target semantics, restore the removed `Active <process>` text, restore process-count/enumeration prose, or move the bottom connection-state indicator.

## Responsive 180-Unit Maximum

`UiMetrics` now defines:

```text
TopTargetInputMaxWidth = 180d
```

The former fixed `TopTargetInputWidth = 240d` metric is no longer used by current host source.

`MainWindow` exposes one host-owned `TopTargetInputWidth` dependency property. The Platform container, each generated plugin connection-field container, and the Target Process container all bind their `Width` to this shared value and also declare `MaxWidth={x:Static application:UiMetrics.TopTargetInputMaxWidth}`.

No matching row-1 `MinWidth` is introduced. The responsive rule can therefore reduce these ordinary inputs below 180 instead of forcing overflow.

## Available-Width Calculation

`TopTargetFirstRow_LayoutUpdated` recalculates the common width whenever layout changes.

The source review confirms the calculation:

1. starts from `TargetConnectionBar.ActualWidth`;
2. explicitly subtracts `TargetConnectionBar.Padding.Left` and `.Right`, preserving the existing outer target-bar margins;
3. counts the current plugin-generated connection inputs through `TopTargetConnectionSettings.Items.Count`;
4. includes Platform and, when visible, Target Process in the number of ordinary inputs sharing the width;
5. reserves the actual outer widths of Connect and Disconnect;
6. when process controls are present, also reserves Refresh and Set Active Target plus the Target Process panel's existing margin;
7. reserves the existing plugin-input lane/per-input spacing;
8. clamps remaining ordinary-input space at zero;
9. divides the remainder evenly across the ordinary inputs;
10. clamps the result to `UiMetrics.TopTargetInputMaxWidth`.

The effective common-width range is therefore `0..180` device-independent units. The fixed action buttons retain their established sizes while ordinary inputs absorb horizontal contraction.

The default main-window width remains `1460`, and the existing `MinWidth=1100` remains unchanged. Rev5 solves the rev4 overflow through responsive input sizing rather than by increasing the minimum supported window width.

## Future-Plugin Boundary

The responsive behavior remains host-owned. Plugins continue to provide connection-setting definitions only; no plugin metadata or WPF contract is added for widths.

The living UI/Plugin SDK documentation now states that future ordinary TextBox/ComboBox fields rendered in target row 1:

- participate in the same shared host-computed width;
- may never exceed 180 units;
- have no host row-1 minimum width;
- shrink together when horizontal space is constrained;
- must not introduce plugin-specific one-off widths or minimums;
- must not force additional permanent target rows.

## Verification Registry Preservation

The rev4 candidate contains **96** registered checks. Rev5 contains **97** unique registrations:

- every rev4 check name remains present;
- no prior check is removed;
- no duplicate check name is introduced;
- exactly one new check is added: `Main workspace responsive target input widths`.

The existing `Main workspace two-row target header standard` registration is retained and updated to require the 180-unit maximum plus the shared responsive binding.

The new responsive source contract verifies the target-bar width/padding calculation, dynamic plugin field count, fixed action reservations, zero-floor/180-ceiling behavior, shared MaxWidth usage, and absence of an erroneous 180-unit minimum-width rule.

## Project and Documentation Structure

Static review verifies before packaging:

- all **21** WPF/project/props/targets/XML files are structurally well-formed;
- no `bin` or `obj` build output is included;
- all **97** verification registrations are unique;
- current AppInfo/README/CHANGELOG/development-plan metadata agrees on rev5;
- rev4 verification/source-review documents remain historical records and are explicitly marked superseded by rev5;
- current UI, Plugin SDK, debugger, Mock, and PS5 documentation describes rev5 as the current host while preserving subsystem ownership boundaries.

Final documentation validation checked **40** relative Markdown links across **131** Markdown files; all resolved inside the packaged tree, including the README links to the rev5 verification and source-review records.

## Remaining Verification

External gates remain:

1. clean Windows Release build;
2. warnings-as-errors compile acceptance;
3. verification executable ending with `All 97 checks passed.`;
4. responsive header runtime review with Mock and PS5 from normal/default width down to `MinWidth=1100` and back up;
5. Light, Dimmed, and Dark theme review;
6. confirmation that ordinary row-1 inputs stay equal-width, never exceed 180, shrink below 180 when required, preserve target-bar left/right padding, and never overlap or cross the right edge;
7. retained shared-button centering/descender regression check;
8. full live PS5 debugger acceptance inherited from the unverified rev3/rev4 candidates;
9. focused Mock/PS5 regression smoke testing.

## Status

**Static source review: passed.**

**Windows build/97-check verification: pending.**

**Responsive main-window runtime/UI verification: pending.**

**Live PS5 debugger hardware verification: pending.**
