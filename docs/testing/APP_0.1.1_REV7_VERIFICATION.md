# 0.1.1.rev7 Theme Identity and Splitter Spacing Fix Verification

## Purpose

This document records source-level verification and required Windows runtime checks for **TeeKay87's Memory Engine 0.1.1.rev7 - Theme Identity and Splitter Spacing Fix**.

The revision responds to runtime observations from rev6. It is intentionally confined to the host application's theme-loading/build-copy behavior, main-workspace splitter presentation, version metadata, and related documentation. Core, Plugin SDK, Mock plugin, PS5 plugin, process enumeration, target selection, and protocol behavior are not changed.

## Theme Identity Correction

The bundled themes now use canonical ids that match their current names:

```text
Light   -> light
Dimmed  -> dimmed
Dark    -> dark
```

Rev4-rev6 persisted ids remain supported through explicit aliases:

```text
darker  -> dimmed
darkest -> dark
```

When a saved legacy id is used successfully, the preference store rewrites it to the new canonical id. This preserves the user's selected color theme while ending the duplicate-id relationship between current and pre-rename bundled theme files.

The runtime loader also recognizes `Darker.json` and `Darkest.json` as obsolete bundled filenames when `Dimmed.json` and `Dark.json` are present and skips those obsolete copies without reporting them as invalid custom themes. The WPF project additionally deletes the two known legacy copied files from build and publish theme directories so ordinary incremental builds converge to the current three-file set.

## Splitter Spacing

The central workspace still contains the same two resizable splitters:

- horizontal between Scan Results and Saved Addresses;
- vertical between the left-side list workspace and the Scan panel.

Each splitter now uses a reusable shared style with a 14-pixel interactive track containing a centered 4-pixel visible drag handle with 5 pixels of spacing on each side. The adjacent cards no longer need one-sided compensating margins, so the separation is visually balanced. Resize direction and behavior remain unchanged.

## Source-Level Verification

Release preparation verifies that:

- `AppInfo` reports `0.1.1.rev7` and feature title `Theme Identity and Splitter Spacing Fix`;
- bundled theme files are exactly `Light.json`, `Dimmed.json`, and `Dark.json`;
- their canonical ids are `light`, `dimmed`, and `dark`;
- `ThemeManager.DefaultThemeId` is `dark`;
- legacy ids `darker` and `darkest` resolve/canonicalize to `dimmed` and `dark` for both saved preferences and loaded theme definitions;
- a successfully resolved legacy preference is persisted back using its canonical id;
- stale `Darker.json`/`Darkest.json` copies are ignored at runtime when the replacement files are present;
- the application project deletes the same stale filenames from build and publish output;
- duplicate-id validation remains active for genuine conflicting theme files;
- both splitter layout tracks are 14 pixels, shared splitter templates render 4-pixel centered handles, and both handles have symmetric 5-pixel visual spacing while the complete track remains draggable;
- XAML, project XML, and theme JSON parse successfully;
- Core, Plugin SDK, Mock plugin, PS5 plugin, and existing backend/protocol tests are unchanged from rev6;
- plugin versions and Plugin API version remain unchanged;
- release contents exclude `bin`, `obj`, `.vs`, `.user`, and `.suo` artifacts.

## Required Windows Runtime Verification

1. Run **Build -> Rebuild Solution** and confirm there are no compiler warnings/errors.
2. Start the application and confirm the Theme selector contains exactly **Light**, **Dimmed**, and **Dark** from the bundled themes.
3. Confirm the status area shows no duplicate theme-id warnings.
4. If `%LocalAppData%\TeeKay87\MemoryEngine\settings.json` contains `darker`, start the application and confirm Dimmed is selected; then confirm the stored id is rewritten to `dimmed`.
5. Repeat the migration check with `darkest` and confirm Dark is selected and persisted as `dark`.
6. Switch among Light, Dimmed, and Dark and confirm live palette changes still apply to the complete open window.
7. Drag the horizontal splitter and confirm there is equal visual spacing between its handle and both neighboring panels.
8. Drag the vertical splitter and confirm there is equal visual spacing between its handle and both neighboring panels.
9. Confirm both splitters retain their expected resize behavior and do not reduce the usability of Scan Results, Saved Addresses, or Scan.
10. Reconnect to ps5debug-NG and confirm the previously verified connection/process/Active Target workflow remains unaffected.

Runtime results should be appended here after the Windows checks are performed.

## Windows Runtime Result - 2026-08-30

Runtime testing confirmed that the rev7 theme-identity correction works: the bundled theme selector loads the current names without the duplicate theme-id warnings observed in rev6, and the revised splitter handles have the intended visual spacing.

The same runtime test exposed a separate resize-boundary issue. Both splitters could still be dragged into technically valid WPF layout states that were not usable for the application. In particular, the vertical splitter could make the Scan panel excessively wide while compressing the left workspace enough to clip or distort controls, and the horizontal splitter could allocate an impractical share of height to one of the two left-side tables. This is a layout-constraint problem rather than a splitter-template problem and is addressed in `0.1.1.rev8`.
