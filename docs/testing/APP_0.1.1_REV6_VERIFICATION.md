# 0.1.1.rev6 Workspace Layout and Theme Refinement Verification

## Purpose

This document records source-level verification and required Windows runtime checks for **TeeKay87's Memory Engine 0.1.1.rev6 - Workspace Layout and Theme Refinement**.

The revision is intentionally limited to host-side presentation. It refines the permanent Cheat Engine-inspired workspace, the bundled color-theme presentation, and object-backed ComboBox labels. Core, Plugin SDK, Mock plugin, PS5 plugin, and protocol behavior are not part of this change.

## Requested UI Changes Covered

1. The user-facing `Darkest` theme name is now **Dark**.
2. The user-facing `Darker` theme name is now **Dimmed**, with a substantially lighter intermediate palette so it is visually separated from Dark.
3. A vertical `GridSplitter` now sits between the left-side memory lists and the Scan panel.
4. The Scan panel now spans the complete height of the central workspace; Scan Results and Saved Addresses are stacked on its left side.
5. Development-explanation text was removed from the Scan panel, leaving only the permanent scanner controls.
6. The target/connection area is more compact, and empty connection/process error messages collapse instead of reserving unused vertical space.

The revision also corrects the object-label presentation visible in the rev5 runtime screenshot. Platform, Theme, and Target Process selectors now render the intended display text even when the custom ComboBox template presents the selected object directly.

## Theme Compatibility

The external source filenames are now:

```text
Light.json
Dimmed.json
Dark.json
```

The machine-readable theme ids remain unchanged:

```text
Light   -> light
Dimmed  -> darker
Dark    -> darkest
```

This deliberately preserves compatibility with the theme id already stored in `%LocalAppData%\TeeKay87\MemoryEngine\settings.json` by previous revisions.

## Source-Level Verification

The release preparation verifies that:

- `AppInfo` reports `0.1.1.rev6` and the feature title `Workspace Layout and Theme Refinement`;
- MainWindow uses a four-row root layout with the main workspace contained in one central row;
- the left workspace contains Scan Results, a horizontal row splitter, and Saved Addresses;
- the right Scan card spans the same complete central-workspace height;
- a column `GridSplitter` with `ResizeDirection="Columns"` separates the left workspace and Scan panel;
- the old scanner explanatory paragraph and informational placeholder box are absent;
- empty connection/process error TextBlocks use the shared collapsing error-message style;
- `ThemeDescriptor`, `PluginViewModel`, and `TargetProcessViewModel` return their intended display labels through `ToString()`, matching the selected-item behavior of the shared ComboBox template;
- the three bundled JSON files are complete and parseable;
- Dimmed differs materially from the Dark palette while retaining readable foreground/background contrast;
- the Dark palette remains color-equivalent to the original Darkest theme from rev5;
- Core, Plugin SDK, Mock plugin, PS5 plugin, and existing tests remain unchanged from the supplied rev5 baseline;
- plugin versions and Plugin API version remain unchanged;
- no `bin`, `obj`, `.vs`, `.user`, or `.suo` files are included in the release archive.

## Required Windows Runtime Verification

A real WPF build cannot be performed in the current preparation environment because the .NET SDK/Windows WPF toolchain is unavailable. Visual Studio on Windows remains authoritative for the following checks:

1. run **Build -> Rebuild Solution** and confirm there are no warnings promoted to errors;
2. start the application and confirm the Theme selector displays **Light**, **Dimmed**, and **Dark** rather than CLR type names;
3. confirm the Platform selector displays the plugin label rather than `TeeKay87.MemoryEngine.App.ViewModels...`;
4. connect to ps5debug-NG and confirm the Target Process selector displays process names/PIDs correctly;
5. switch Light -> Dimmed -> Dark and confirm every visible surface changes immediately without restarting;
6. confirm Dimmed is visibly lighter than Dark and recognizably between Light and Dark;
7. close the application on each theme in turn, restart, and confirm the selected theme is restored;
8. drag the vertical splitter between the left lists and Scan panel in both directions;
9. drag the horizontal splitter between Scan Results and Saved Addresses in both directions;
10. resize the window and confirm the Scan panel remains full-height while both left-side tables remain usable;
11. confirm the Scan panel contains Value, Scan Type, Value Type, First Scan, Next Scan, and New Scan without the previous development-explanation text/box;
12. confirm the target/connection area no longer leaves a large unused blank area below normal status/Plugin details when no error is present;
13. reconnect to the PS5 and verify Connect, Disconnect, process refresh, process selection, and Set Active Target still behave as in rev5.

Runtime observations should be appended to this document in a later revision once they have been performed.

## Runtime Observation - 2026-08-30

Windows runtime testing confirmed that the rev6 workspace layout loads and the splitters are present, but it also revealed two refinement issues that were not detectable through the static release checks:

- incremental Visual Studio builds could leave the old copied `Darker.json` and `Darkest.json` files in the runtime `Themes` directory after the source files had been renamed to `Dimmed.json` and `Dark.json`; because rev6 deliberately reused the same internal ids, the theme loader reported duplicate-id warnings and could select the older display names depending on filename ordering;
- both splitter drag handles sat visually too close to adjacent panel borders because the spacing was effectively placed on only one side of each splitter.

These observations are addressed in `0.1.1.rev7`.
