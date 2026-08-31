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

Semantic styles should be chosen by the meaning of the action, not by the desired color.

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
- `SecondaryButtonStyle` for **New Scan**, plus disabled future **Add Address**, **Edit**, and **Export** actions;
- disabled `DangerButtonStyle` for the future **Remove** saved-address action.

From `0.1.2.rev5`, scanner emphasis follows the workflow rather than assigning Primary permanently to both scan buttons. New Scan resets the state so First Scan becomes the emphasized action again. This visual state is theme-driven and does not hard-code a color in the Scan panel.

Danger is also the semantic style for stop/termination actions, not only deletion. Current themes therefore use their existing complementary red Danger palette for Disconnect and Cancel Scan, and future Abort/Exit-style actions should reuse the same semantic style unless a different application-wide meaning is deliberately introduced. Saved-address placeholders continue to provide a visual regression check for disabled-state readability in Light, Dimmed, and Dark themes.

Capability elements under **Plugin details** are informational badges implemented as `Border` elements. They are not buttons and should not receive button interaction states unless they become interactive controls in a future revision.

## Extension Rules

When new WPF views are added:

1. reuse one of the shared semantic button styles whenever it represents the action correctly;
2. do not copy the shared `ControlTemplate` into a view;
3. do not introduce view-local hover, pressed, focus, or disabled colors for ordinary buttons;
4. add a new application-wide semantic style only when an action category genuinely requires behavior or visual meaning not covered by Primary, Secondary, or Danger;
5. keep layout decisions such as margins and `MinWidth` local when they are specific to a particular view, but reuse `UiMetrics.StandardControlHeight` for ordinary single-line buttons;
6. preserve readable disabled content and keyboard focus indication in every future button variant;
7. use a different control type for non-button concepts such as badges, chips, toggles, check boxes, or menu items rather than styling them to behave like standard buttons.
