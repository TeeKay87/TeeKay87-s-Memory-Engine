# 0.1.1.rev10 Unified Interactive Control Heights Verification

## Purpose

This document records source-level verification and required Windows runtime checks for **TeeKay87's Memory Engine 0.1.1.rev10 - Unified Interactive Control Heights**.

The rev9 splitter behavior was verified successfully. The next visible inconsistency was that ordinary single-line controls used different height rules: the Platform selector used the desired 34-unit baseline, standard buttons used a 38-unit minimum, and the Target Process selector stretched to the taller button row.

## Shared Metric

The host now defines one authoritative value:

```text
UiMetrics.StandardControlHeight = 34
```

The implicit/shared WPF styles for `Button`, `TextBox`, and `ComboBox` use that metric as a fixed `Height`. No local `MainWindow.xaml` height overrides are required for those normal controls.

Using a fixed shared height rather than independent minimum heights is important because WPF Grid rows can otherwise stretch a selector to the desired height of a taller neighboring button.

## Scope

The change is presentation-only. It does not alter:

- MainWindow workspace structure;
- horizontal 50/50 default and 20/80–80/20 splitter ratios;
- Scan-panel 280–420 px width bounds;
- themes or theme ids;
- Core;
- Plugin SDK;
- Mock plugin;
- PS5 plugin;
- plugin versions;
- Plugin API version;
- connection/process commands or behavior.

## Source-Level Verification

Release preparation must verify that:

- `AppInfo` reports `0.1.1.rev10` and feature title `Unified Interactive Control Heights`;
- `UiMetrics.StandardControlHeight` exists and equals `34d`;
- `ButtonBaseStyle` uses `UiMetrics.StandardControlHeight` for `Height` and no longer contains `MinHeight=38`;
- implicit TextBox and ComboBox styles use `UiMetrics.StandardControlHeight` for `Height` and no longer contain their old independent `MinHeight=34`;
- MainWindow does not add competing local heights to ordinary Button, TextBox, or ComboBox instances;
- the splitter definitions and rev9 proportional splitter implementation remain unchanged;
- Core, Plugin SDK, Mock plugin, PS5 plugin, theme JSON files, and backend/protocol tests are unchanged from rev9;
- plugin versions and Plugin API version remain unchanged;
- XAML/project XML parse successfully;
- release contents exclude `bin`, `obj`, `.vs`, `.user`, and `.suo` artifacts.

## Required Windows Runtime Verification

1. Run **Build -> Rebuild Solution** and confirm there are no compiler warnings or errors.
2. Start the application and compare Platform, plugin connection TextBoxes, Connect/Disconnect/Reload Plugins, Target Process, Refresh, and Set Active Target. Confirm their outer control heights are visually identical.
3. Confirm the common height matches the Platform selector height from rev9 rather than making the Platform selector taller.
4. Connect to ps5debug-NG and confirm populating the Target Process selector does not change its height.
5. Confirm the disabled Value TextBox, Scan Type selector, Value Type selector, First Scan, Next Scan, and New Scan controls in the Scan panel share the same height.
6. Confirm Add Address, Edit, Remove, and Export buttons use the same standard height.
7. Switch among Light, Dimmed, and Dark and confirm the height remains identical in every theme.
8. Confirm the 50/50 and 20/80–80/20 horizontal splitter behavior still works.
9. Confirm the Scan panel remains bounded to its existing width range.
10. Reconnect to ps5debug-NG and confirm process enumeration and Active Target behavior remain unaffected.

Runtime results should be appended here after the Windows checks are performed.

## Static Release Result

The prepared rev10 source tree passed **61/61** release checks covering XAML/project XML parsing, theme JSON parsing, centralized metric usage, removal of conflicting height declarations, absence of local standard-control height overrides in `MainWindow.xaml`, unchanged splitter behavior, byte-identical Core/Plugin SDK/plugin/theme/test baselines, version separation, documentation links, and release-tree cleanliness.

A native Windows WPF build remains required because the packaging environment does not provide the .NET Windows/WPF SDK toolchain.

## Windows Runtime Result - 2026-08-30

The rev10 build was subsequently run on the Windows development machine. The unified standard-control height was reported to look correct and cohesive across the current interface. No follow-up defect was reported for the 34-unit shared Button/TextBox/ComboBox height contract.

This establishes the rev10 visual sizing change as runtime-verified and allows subsequent revisions to leave that UI foundation unchanged while development returns to target-memory functionality.
