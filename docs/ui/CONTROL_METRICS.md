# Shared UI Control Metrics

## Purpose

TeeKay87's Memory Engine centralizes dimensions that must remain consistent across unrelated WPF views. Shared metrics currently cover the standard height for normal single-line interactive controls and the standard width for ordinary TextBox/ComboBox inputs in the permanent main-window target header.

This prevents each view or plugin-rendered field from inventing its own dimensions and avoids alignment differences such as a selector being shorter than an adjacent button or one plugin's top connection fields using unrelated widths.

## Standard Single-Line Control Height

The authoritative value is defined in:

```text
src/TeeKay87.MemoryEngine.App/Application/UiMetrics.cs
```

Current value:

```text
UiMetrics.StandardControlHeight = 34
```

The value is expressed in WPF device-independent units. It matches the height used by the Platform selector before the metric was centralized.

## Responsive Top-Target Input Width

Application `0.1.7.rev5` defines the current authoritative host metric for ordinary row-1 target inputs:

```text
UiMetrics.TopTargetInputMaxWidth = 180
```

This is a **maximum**, not a fixed width. It applies to ordinary TextBox and ComboBox inputs rendered in the permanent main target/connection row 1. The current consumers are:

- Platform ComboBox;
- every plugin-declared `ConnectionSettings` TextBox, including the PS5 host/IP and Port fields;
- Target Process ComboBox.

All of those controls share one host-computed width. At normal/wide window sizes the width is capped at 180 device-independent units. When row 1 becomes constrained, the host recalculates the common width from the target bar's actual width after its left/right padding is removed after reserving the fixed action buttons and the existing inter-control spacing/margins. The ordinary inputs then shrink together. No minimum width is imposed by this row-1 rule, so they may become narrower than 180 as necessary instead of forcing controls past the right edge of the supported main-window layout.

The calculation uses the target bar's actual width and explicitly subtracts the Border's left/right padding before distributing space. This means the permanent left/right target-bar margins remain reserved automatically; the responsive field calculation does not consume or bypass them.

The rule is deliberately platform-neutral. Future plugins contribute connection-setting definitions only and inherit the same responsive host sizing. Plugins must not encode one-off WPF widths for ordinary top-row TextBox/ComboBox fields. This rule does **not** apply to unrelated application-bar controls such as the Theme selector, nor to specialized editors elsewhere in the application.

The target header remains a compact connection/target surface. If a future plugin requires enough configuration that the fixed two-row header becomes impractical even with responsive ordinary inputs, additional configuration belongs in a plugin/settings/details surface rather than creating a third permanent row.

## Current Consumers

The application-wide implicit styles for these normal controls use the shared height directly:

- `Button` through `ButtonBaseStyle`;
- `TextBox`;
- `ComboBox`.

Because the styles are application-wide, existing controls and future ordinary instances of these WPF types inherit the same height automatically unless a deliberately different style is applied.

This includes the current:

- Theme selector;
- Platform selector;
- plugin-defined connection TextBoxes;
- Target Process selector;
- Connect, Disconnect, Reload Plugins, Refresh, and Set Active Target buttons;
- scanner Value TextBox and fixed Scan Type / Value Type ComboBoxes;
- First Scan, Next Scan, and New Scan buttons;
- Saved Addresses action buttons;
- Memory Viewer Address TextBox plus Go To and Refresh buttons from `0.1.5.rev1`.


## TextBox Content Spacing

The standard 34-unit TextBox height is intentionally preserved. Single-line TextBoxes use compact internal padding of:

```text
9 horizontal / 3 vertical
```

The shared TextBox control template stretches `PART_ContentHost` across the padded interior and binds its horizontal and vertical content alignment to the owning TextBox. This gives the text line enough vertical viewport space while still allowing the normal `VerticalContentAlignment=Center` rule to center single-line text.

This is a content-layout rule, not a new control-height rule. The outer TextBox remains 34 units high. Multiline TextBoxes may continue to provide deliberate local padding and vertical alignment when their role requires it.

## Future-Control Rule

When a new standard single-line interactive control type is introduced, its application-wide style should use `UiMetrics.StandardControlHeight` rather than define another independent height. Examples may include future numeric inputs, editable selectors, toolbar fields, or other one-line command controls.

A view should normally specify only layout-specific properties such as width, minimum width, margin, column placement, or alignment. It should not add a local `Height` merely to make an ordinary control line up with its neighbors.

## Intentional Exceptions

Not every interactive element should be forced to 34 units. A different size is appropriate when the control has a genuinely different role or form factor, for example:

- multiline text editors;
- table rows/cells;
- check boxes embedded in a data grid;
- menu items or popup list items;
- large icon/tile actions;
- splitters;
- specialized memory/disassembly editors that require their own density rules;
- the rev24 Debugger workspace selector buttons, which intentionally use a compact 28-unit height and 12-point text while inheriting the normal shared button template.

Such exceptions should be intentional and documented in the relevant shared style rather than introduced accidentally in an individual view.

## Theme Boundary

Control dimensions are host presentation metrics, not color-theme data. External JSON themes may change colors only and must not define or override `StandardControlHeight`, `TopTargetInputMaxWidth`, or any other layout dimension.


> Host `0.1.7.rev6` builds on the fully verified rev5 baseline. Its Threads/Thread Control and passive header-status cleanup do not redesign the subsystem documented here.
