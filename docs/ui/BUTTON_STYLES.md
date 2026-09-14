# Shared Button Styles

## Purpose

TeeKay87's Memory Engine uses application-wide WPF button resources instead of allowing each view to define its own button appearance.

The goal is to keep button meaning, interaction feedback, disabled behavior, spacing, focus indication, and future UI maintenance consistent across the complete application.

The shared styles are defined in:

```text
src/TeeKay87.MemoryEngine.App/Resources/Styles/ButtonStyles.xaml
```

They are loaded once through `App.xaml` and can therefore be reused by every WPF view in the application.

## Style Hierarchy

`ButtonBaseStyle` owns the common control template and interaction behavior. It is not intended to communicate a specific action type by itself.

The application currently exposes three semantic button styles:

| Style | Purpose | Typical use |
| --- | --- | --- |
| `PrimaryButtonStyle` | The main affirmative action in a group or workflow | Connect, Scan, Apply, Save |
| `SecondaryButtonStyle` | Normal supporting or neutral actions | Reload, Browse, New Scan |
| `DangerButtonStyle` | Stop, cancel, disconnect, exit, destructive, or potentially irreversible actions | Disconnect, Cancel, Abort, Exit, Delete, Remove |

`SecondaryButtonStyle` is also registered as the implicit `Button` style. A button that does not declare an explicit style therefore still uses the shared control template instead of the operating-system WPF button template.

## Standard Height

All normal application buttons use the host-wide single-line control height defined by:

```text
UiMetrics.StandardControlHeight = 34
```

The metric is shared with the implicit `TextBox` and `ComboBox` styles. It intentionally matches the established Platform selector height so buttons, selectors, and single-line inputs align when placed in the same row.

Button views should not set a local `Height` simply to match neighboring controls. A different button height should only be introduced for a deliberately different control form factor and must not replace the ordinary application-wide button baseline.

From `0.1.7.rev4`, retained unchanged by `0.1.7.rev5`, the **outer button height remains exactly 34 units**. The shared template reduces only the ordinary button's internal vertical content padding from the older `14,8` layout value to `14,2` while retaining the same horizontal padding. `HorizontalContentAlignment=Center` and `VerticalContentAlignment=Center` remain authoritative, and the generated `AccessText` / `TextBlock` string-content elements are also explicitly vertically centered. This is a text-layout correction, not a button-size redesign: borders, corner radius, semantic colors, hover/pressed/focus behavior, and normal button height are unchanged. The smaller vertical content margin leaves adequate room for descenders such as **g, j, p, q, and y** instead of letting their lower pixels be clipped.

Semantic styles should be chosen by the meaning of the action, not by the desired color.

## Compact Debugger Workspace Selectors

Application `0.1.7.rev24` adds a deliberate specialized button form factor for the Debugger's upper-right workspace selector. `DebuggerWorkspaceSwitchButtonStyle` derives from `SecondaryButtonStyle`, so it reuses the normal application button template and all standard interaction/theme behavior, but sets `Height=28`, `FontSize=12`, and compact padding. This is narrower than the ordinary 34-unit action button because it behaves as persistent workspace navigation rather than a primary workflow action.

`DebuggerBreakpointsSwitchButtonStyle` and `DebuggerCallStackSwitchButtonStyle` derive from that compact base and bind only their persistent selected state. When selected, the button keeps its normal theme surface and gains a two-unit `AccentBrush` border outline. The selection does not use `Topmost`, operating-system tab chrome, a hardcoded color, or a copied button template. The underlying ViewModel guarantees that exactly one available upper workspace is selected at a time.

This specialized 28-unit height must not be copied to ordinary buttons. New action buttons continue to use `UiMetrics.StandardControlHeight=34` unless another genuinely distinct control role is documented.

## Interaction States

The common control template handles the states used by standard buttons throughout the application:

- **Normal** — displays the semantic style's base background, border, and foreground.
- **Hover** — adds a subtle light interaction overlay without replacing the semantic base color.
- **Pressed** — adds a stronger dark overlay so mouse/touch activation is visible.
- **Keyboard focused** — displays an accent focus outline rather than relying on the default WPF focus visual.
- **Defaulted** — displays a reduced accent outline when WPF marks a button as the active default action.
- **Disabled** — uses dedicated dark-theme disabled background, border, and text brushes while keeping the button label clearly readable.

The disabled state deliberately does not use WPF's operating-system button chrome. This prevents disabled buttons from becoming bright white or losing readable text in the application's dark theme.

Disabled controls retain their normal layout size. Their cursor changes to the default arrow to avoid suggesting that the action can currently be clicked.

From `0.1.3.rev2`, disabled-state contrast is intentionally stronger across all bundled themes. Disabled buttons use the theme-owned `DisabledBackground`, `DisabledBorder`, and `DisabledText` colors, remove hover/pressed overlays, and use the normal arrow cursor. The same semantic disabled palette is shared by other host controls so unavailable actions are visually recognizable before interaction.

From `0.1.3.rev5`, the disabled button colors are also enforced directly by `ButtonBaseStyle`'s `ControlTemplate`. The rendered border and named content presenter receive the disabled background, border, and foreground brushes whenever `IsEnabled=False`. This is deliberate: semantic styles such as Primary/Secondary/Danger and workflow-local derived styles may define their own enabled Foreground/Background/BorderBrush values, so the visible disabled state must not depend solely on inherited style-setter precedence. All standard buttons therefore converge on one unmistakable disabled appearance regardless of which semantic style or command controls their enabled state.

## Theme Resources

Button colors are supplied by the active external JSON color theme through `ThemeManager`. `App.xaml` contains only the emergency fallback palette. The shared button template references application brush resources through `DynamicResource`, so changing the active theme updates existing buttons immediately without recreating the view.

Important button-specific resources include:

```text
PrimaryButtonBackgroundBrush
PrimaryButtonBorderBrush
PrimaryButtonTextBrush
SecondaryButtonBackgroundBrush
SecondaryButtonBorderBrush
SecondaryButtonTextBrush
DisabledButtonBackgroundBrush
DisabledButtonBorderBrush
DisabledButtonTextBrush
DangerButtonBackgroundBrush
DangerButtonBorderBrush
DangerButtonTextBrush
ButtonHoverOverlayBrush
ButtonPressedOverlayBrush
```

The JSON-to-WPF resource mapping is centralized in `ThemeManager`. Every bundled theme must define the complete button palette, including separate Primary and Secondary background/border/text colors, Danger colors, disabled colors, and interaction-state overlays. Changing themes therefore changes the relevant button states throughout the application without editing individual views. Primary-button color is deliberately independent from the general `Accent` palette entry so a theme can tune action-button contrast without recoloring unrelated accent UI. See `docs/ui/THEMES.md` for the theme schema and palette rules.

## Usage

Primary action:

```xml
<Button Style="{StaticResource PrimaryButtonStyle}"
        Command="{Binding ConnectCommand}"
        Content="Connect" />
```

Secondary action:

```xml
<Button Style="{StaticResource SecondaryButtonStyle}"
        Command="{Binding ReloadCommand}"
        Content="Reload" />
```

Destructive action:

```xml
<Button Style="{StaticResource DangerButtonStyle}"
        Command="{Binding DeleteCommand}"
        Content="Delete" />
```

A plain `<Button>` is allowed when the intended semantic type is Secondary, because the implicit application style resolves to `SecondaryButtonStyle`. Explicit style names are preferred in views where the action's semantic role should be immediately obvious during code review.

## Current Application Usage

The current main workspace uses the shared styles for every standard button:

- `PrimaryButtonStyle` for **Connect** and **Set Active Target**;
- `DangerButtonStyle` for **Disconnect** and **Cancel Scan**;
- `SecondaryButtonStyle` for **Reload Plugins** and process **Refresh**;
- workflow-aware Primary/Secondary styling for **First Scan** and **Next Scan**: before a scan session exists First Scan uses the active theme's Primary palette, while a successful First Scan transfers that emphasis to Next Scan;
- from `0.1.7.rev43`, the permanent Main Window **Disassembler...** and **Debugger...** tool-entry actions use `PrimaryButtonStyle`, matching the active First Scan visual role and automatically following Light, Dimmed, and Dark theme palettes;
- `SecondaryButtonStyle` for **New Scan** and the active Scan Results / Saved Addresses **Export...** actions;
- `DangerButtonStyle` for Saved Addresses **Remove/Remove All**, Memory Viewer bookmark **Remove**, **Disconnect**, **Cancel Scan**, and all application-owned **Cancel** dialog actions.

From `0.1.2.rev5`, scanner emphasis follows the workflow rather than assigning Primary permanently to both scan buttons. New Scan resets the state so First Scan becomes the emphasized action again. This visual state is theme-driven and does not hard-code a color in the Scan panel.

Danger is the semantic style for destructive **and dismissive/termination** actions, not only deletion. From `0.1.5.rev8`, this rule is applied consistently to application-owned **Remove, Remove All, Cancel, Cancel Scan, Disconnect**, and analogous Delete/Abort/Exit actions. That includes Cancel buttons in Settings, export, edit, confirmation, and operation-progress dialogs as well as the Memory Viewer bookmark Remove action. These controls always resolve the active theme's Danger palette rather than choosing a local red or falling back to Secondary styling. Future Remove/Cancel/Delete/Abort/Exit/Disconnect buttons must use `DangerButtonStyle` unless the application deliberately redefines that action's semantic role. Disabled states for current workflow actions, including capability/state-gated Export and Remove All buttons, continue to provide a visual regression check for disabled-state readability in Light, Dimmed, and Dark themes.

Capability elements under **Plugin details** are informational badges implemented as `Border` elements. They are not buttons and should not receive button interaction states unless they become interactive controls in a future revision.

## Extension Rules

When new WPF views are added:

1. reuse one of the shared semantic button styles whenever it represents the action correctly;
2. do not copy the shared `ControlTemplate` into a view;
3. do not introduce view-local hover, pressed, focus, or disabled colors for ordinary buttons;
4. add a new application-wide semantic style only when an action category genuinely requires behavior or visual meaning not covered by Primary, Secondary, or Danger;
5. keep layout decisions such as margins and `MinWidth` local when they are specific to a particular view, but reuse `UiMetrics.StandardControlHeight` for ordinary single-line buttons;
6. preserve the shared centered-content rule and compact vertical content padding; do not compensate for text baseline problems by changing the standard 34-unit button height;
7. preserve readable disabled content and keyboard focus indication in every future button variant;
8. use a different control type for non-button concepts such as badges, chips, toggles, check boxes, or menu items rather than styling them to behave like standard buttons.

### Generated text content and disabled foreground

WPF may create an `AccessText` or `TextBlock` internally when a button's `Content` is a string. The shared button template therefore keeps local styles for both generated text-element types. Their normal foreground follows the containing button's semantic `Foreground`, while an ancestor `IsEnabled=False` trigger applies `DisabledButtonTextBrush` directly to the generated label. This prevents application-level text styling or WPF content materialization from leaving a disabled button label in the enabled text color.


> Host `0.1.7.rev6` builds on the fully verified rev5 baseline. Its Threads/Thread Control and passive header-status cleanup do not redesign the subsystem documented here.
