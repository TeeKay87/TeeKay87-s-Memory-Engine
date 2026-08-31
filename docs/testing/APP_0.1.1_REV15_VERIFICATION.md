# 0.1.1.rev15 TextBox Content Padding Fix Verification

## Purpose

This document records verification requirements for **TeeKay87's Memory Engine 0.1.1.rev15 - TextBox Content Padding Fix**.

The revision corrects a presentation defect visible in standard single-line TextBoxes: text could be vertically clipped even though the established 34-unit outer control height was correct. The fix is intentionally centralized in the existing implicit TextBox style so current and future standard TextBoxes receive the same correction without local view-specific workarounds.

## Reported UI Defect

On the rev14 Windows build, text inside connection fields and Raw Memory Read inputs was visibly clipped vertically. The outer TextBox dimensions aligned correctly with adjacent controls, so increasing the control height would have regressed the rev10 unified-control-height contract rather than addressing the internal layout problem.

## Root Cause

The shared TextBox style used:

```text
Height:  34
Padding: 9,6
```

Its custom template then applied that padding as the margin of `PART_ContentHost` while also centering the content host itself vertically. This reserved 12 device-independent units for top/bottom padding inside an already fixed-height control and reduced the text viewport unnecessarily. The template also did not bind the content host's content-alignment properties back to the owning TextBox.

## Correction

The standard TextBox outer height remains unchanged:

```text
UiMetrics.StandardControlHeight = 34
```

The implicit TextBox style now uses:

```text
Padding: 9,3
```

`PART_ContentHost` now stretches through the remaining padded interior and receives the owning TextBox's horizontal and vertical content alignment through `TemplateBinding`. Standard single-line TextBoxes therefore keep centered content with more vertical viewport space. Multiline TextBoxes can still supply deliberate local padding/alignment overrides; the existing Raw Memory Read result surface remains a 150-unit, top-aligned multiline control.

## Scope

This revision changes only host presentation resources, host version metadata, and documentation. It does not change:

- `UiMetrics.StandardControlHeight`;
- MainWindow layout dimensions;
- button or ComboBox dimensions/templates;
- splitters;
- themes or theme ids;
- Core;
- Plugin SDK;
- Mock plugin;
- PS5 plugin or ps5debug-NG protocol behavior;
- memory-map or raw-read logic;
- plugin versions;
- Plugin API version.

## Version Boundaries

```text
Host application:  0.1.1.rev15
PS5 plugin:        0.1.0.rev4
Mock plugin:       1.0.0.rev1
Plugin API:        1.0.0
```

## Static Verification Requirements

Release preparation must verify that:

- `AppInfo` reports `0.1.1.rev15` and `TextBox Content Padding Fix`;
- `UiMetrics.StandardControlHeight` remains exactly `34d`;
- the implicit TextBox style retains `Height={x:Static application:UiMetrics.StandardControlHeight}`;
- standard TextBox padding is `9,3`;
- `PART_ContentHost` stretches horizontally and vertically;
- `PART_ContentHost` binds horizontal/vertical content alignment from the templated TextBox;
- the multiline Raw Memory Read result keeps its local `Height=150`, `Padding=10,8`, and `VerticalContentAlignment=Top`;
- Core, Plugin SDK, Mock plugin, PS5 plugin, protocol fixture, themes, splitter code, button styles, and `UiMetrics.cs` remain unchanged from rev14;
- XAML/project XML/theme JSON remain well formed;
- release contents exclude build/editor artifacts.

## Required Windows Runtime Verification

1. Rebuild the solution with zero warnings/errors.
2. Start the application and inspect the PS5 host/IP and port TextBoxes. Confirm the complete text line is visible vertically.
3. Connect to PS5 and inspect the Raw Memory Read Address and Length fields. Confirm their text is fully visible.
4. Confirm all standard TextBoxes remain exactly the same outer height as the 34-unit ComboBoxes and buttons.
5. Confirm text remains vertically centered rather than moving to the top or bottom of the input.
6. Inspect the disabled scanner Value TextBox and confirm its presentation remains aligned with the scanner selectors/buttons.
7. Expand Raw Memory Read after a successful read and confirm the 150-unit multiline result TextBox remains top-aligned and scrollable.
8. Switch Light, Dimmed, and Dark themes and confirm the fix is theme-independent.
9. Reconfirm Connect, process enumeration, Active Target, memory-map enumeration, and raw-memory read still work.

## Preparation Result

Release preparation completed **108/108 static checks successfully**. The checks covered XML/XAML and theme JSON parsing, host/plugin/API version separation, preservation of the 34-unit control-height metric, the corrected TextBox padding/content-host template, preservation of the rev14 OneWay raw-result binding, the deliberate multiline TextBox override, byte-for-byte preservation of all unrelated source/test files, local Markdown links, and release-tree cleanliness.

A native Windows WPF runtime check remains required because the preparation environment does not contain the .NET Windows/WPF SDK toolchain.


## Windows Runtime Result

The rev15 presentation correction was runtime-verified successfully on Windows on 2026-08-31. The user confirmed that the standard TextBox text is fully visible and vertically centered while the established 34-unit control height remains unchanged. The supplied verification screenshot also shows the corrected PS5 IP/port fields, Raw Memory Read address/length fields, and disabled scanner Value field rendering without the rev14 clipping defect.

The same runtime session reconfirmed the previously completed rev11/rev12 PS5 path: the application connected to a real PlayStation 5, loaded 86 processes, activated `eboot.bin`, loaded 8,750 memory regions, and successfully displayed a 64-byte raw read from `0x400000`.

**rev15 Windows TextBox presentation verification: PASS.**
