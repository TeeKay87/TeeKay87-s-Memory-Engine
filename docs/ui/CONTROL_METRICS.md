# Shared UI Control Metrics

## Purpose

TeeKay87's Memory Engine centralizes dimensions that must remain consistent across unrelated WPF views. The first shared metric is the standard height for normal single-line interactive controls.

This prevents each view or control style from inventing its own height and avoids alignment differences such as a selector being shorter than an adjacent button.

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
- Saved Addresses action buttons.


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
- specialized memory/disassembly editors that require their own density rules.

Such exceptions should be intentional and documented in the relevant shared style rather than introduced accidentally in an individual view.

## Theme Boundary

Control dimensions are host presentation metrics, not color-theme data. External JSON themes may change colors only and must not define or override `StandardControlHeight` or any other layout dimension.
