# 0.1.1.rev4 Cheat Engine-Inspired Workspace and Color Themes Verification

## Purpose

This document records project-wide verification requirements for **TeeKay87's Memory Engine 0.1.1.rev4 - Cheat Engine-Inspired Workspace and Color Themes**.

The revision changes host presentation and theme infrastructure only. It must preserve the previously verified PS5 connection and process-enumeration behavior while replacing the temporary plugin-centric main layout with the permanent scanner-oriented workspace.

## Scope

The revision adds:

- a Cheat Engine-inspired but modernized main workspace;
- a compact persistent target/connection area;
- permanent Scan Results, Scan Controls, and Saved Addresses areas;
- shared theme-aware styles for common WPF controls;
- an external JSON theme loader;
- Light, Darker, and Darkest bundled themes;
- live theme application through shared dynamic brush resources;
- selected-theme persistence.

It does not add scanner logic, memory reads/writes, memory maps, saved-address behavior, or new plugin capabilities.

## Previously Verified Baseline

Before this revision, the user reported successful Windows/runtime operation for the supplied `0.1.1.rev3` baseline:

- the application runs;
- the PlayStation 5 plugin connects to a real PS5 running ps5debug-NG;
- the application retrieves a real PS5 process list.

Those behaviors are regression requirements for this revision.

## Static Verification Requirements

The release source should satisfy all of the following:

1. `AppInfo` reports `0.1.1.rev4` and feature title `Cheat Engine-Inspired Workspace and Color Themes`;
2. PS5 plugin remains independently versioned at `0.1.0.rev2`;
3. Mock plugin remains independently versioned at `1.0.0.rev1`;
4. Plugin API remains `1.0.0`;
5. Core, Plugin SDK, both plugin implementations, and existing protocol tests remain unchanged from the supplied `0.1.1.rev3` baseline;
6. the existing `PluginViewModel` connection/process commands are reused by the new layout rather than replaced with a second implementation;
7. the target process UI is still gated by `ProcessEnumeration` capability;
8. selected process and Active Target remain separate states;
9. the new scan controls are present but disabled because scanning is not implemented;
10. Saved Addresses actions are present but disabled because the address model/workflow is not implemented yet;
11. `Light.json`, `Darker.json`, and `Darkest.json` are external runtime theme files;
12. all theme files provide the complete required palette;
13. `Darkest.json` preserves the original application palette used before theme support;
14. themes contain colors only and cannot supply XAML/control templates/layout behavior;
15. the application discovers theme JSON files at startup;
16. selecting a theme updates shared `Application.Resources` brushes used through `DynamicResource`;
17. the selected theme id is persisted outside the source/application directory under the user's Local Application Data directory;
18. invalid individual theme files are skipped rather than partially applied;
19. a Darkest-compatible emergency palette remains available when no external theme can be loaded;
20. shared control styling is centralized under `Resources/Styles/`;
21. ordinary main-workspace colors are supplied from theme resources rather than duplicated as local hexadecimal colors;
22. all XAML, project XML, and JSON remain syntactically well formed;
23. all solution/project-reference paths continue to resolve;
24. release contents exclude `bin`, `obj`, `.vs`, `.user`, and `.suo` artifacts.

## Source-Preparation Result

During source preparation, **76 static checks passed**. They covered host/plugin/API version separation, byte-for-byte preservation of Core/Plugin SDK/both plugin implementations/existing tests, XAML/XML/JSON syntax, complete theme palettes, ThemeManager palette mapping, Darkest/fallback palette consistency, preservation of the previous shared dark palette, WPF resource-key resolution, absence of local hardcoded palette colors in the permanent workspace/styles, reuse of existing connection/process commands, capability gating, placeholder disabled state, build/publish theme copying, solution/project references, documentation links, plugin documentation folders, and release-tree cleanliness.

These checks do not replace the Windows WPF build and visual/runtime checks below.

## Required Windows Build Verification

Open `TeeKay87.MemoryEngine.sln` in Visual Studio 2022 and run:

```text
Build > Build Solution
```

No warnings or errors should be produced because warnings are treated as errors project-wide.

Then run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

All existing verification checks should continue to report `PASS`.

## Required Theme Runtime Verification

After launching the WPF application:

1. confirm the Theme dropdown contains **Light**, **Darker**, and **Darkest**;
2. select **Light** and confirm the complete visible workspace changes immediately without restart;
3. verify text, panels, inputs, combo boxes, data-grid headers, disabled scanner controls, disabled address buttons, borders, status text, and buttons remain readable;
4. select **Darker** and repeat the visual check;
5. select **Darkest** and confirm it visually preserves the previous dark application palette;
6. close the application while a non-default theme is selected;
7. reopen the application and verify the previous selection is restored;
8. confirm `%LocalAppData%\TeeKay87\MemoryEngine\settings.json` contains the selected theme id;
9. temporarily place an invalid JSON/theme file in the runtime `Themes` directory, restart, and verify the application still starts while surfacing a theme error;
10. remove the invalid file after the check.

## Required Workspace Runtime Verification

Verify that the new main window presents:

- application/theme bar;
- compact platform/connection controls;
- target process selector and Active Target information for process-capable plugins;
- Scan Results on the upper-left;
- Scan Controls on the upper-right;
- Saved Addresses as a distinct lower table;
- a resizable divider between scanner and Saved Addresses;
- status information at the bottom.

Confirm scanner and saved-address actions are visibly disabled rather than appearing functional.

## PS5 Regression Verification

Against a real PS5 running ps5debug-NG:

1. select PlayStation 5;
2. enter connection information;
3. connect successfully;
4. confirm the process list is retrieved in the new target bar;
5. select a process and set it as Active Target;
6. refresh the process list and confirm the UI remains responsive;
7. switch all three themes while still connected and confirm connection/target state is not reset by theme changes;
8. disconnect and confirm the existing process-state cleanup still occurs.

No theme or layout action should invoke PS5-specific protocol code directly.

## Preparation-Environment Status

The source-preparation environment does not contain the .NET SDK or Windows WPF runtime. Native WPF compilation, executable verification, and visual theme switching therefore cannot be executed there.

Before packaging, static checks should cover source structure, XAML/XML/JSON syntax, theme schema completeness, resource usage, independent versioning, baseline source preservation for Core/SDK/plugins/tests, project references, and archive integrity. Final visual/runtime results must be recorded after testing on the Windows development machine.
