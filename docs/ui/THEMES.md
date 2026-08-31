# Color Theme System

## Purpose

TeeKay87's Memory Engine uses external color themes so the application palette can be changed without allowing a theme to replace the UI or alter application behavior.

Themes are intentionally limited to named colors. Layout, WPF control templates, bindings, commands, platform capabilities, and feature behavior remain owned by the application source.

## Included Themes

The application currently ships with three themes:

| Theme | Id | Purpose |
| --- | --- | --- |
| Light | `light` | Light neutral application palette |
| Dimmed | `dimmed` | Intermediate palette between Light and Dark |
| Dark | `dark` | Original dark palette used before theme support |


The bundled source files and canonical ids are:

```text
Light.json   → id: light   → Light
Dimmed.json  → id: dimmed  → Dimmed
Dark.json    → id: dark    → Dark
```

`0.1.1.rev7` changed the canonical ids for Dimmed and Dark because rev6 reused the older ids `darker` and `darkest`. Reusing those ids made stale copies of the pre-rename `Darker.json` and `Darkest.json` files collide with the new files when Visual Studio performed an incremental build without deleting old copied content.

Compatibility is preserved through explicit legacy aliases:

```text
darker  → dimmed
darkest → dark
```

A previously persisted selection is resolved through the alias and rewritten to the canonical id at startup. The loader also ignores the obsolete bundled `Darker.json` or `Darkest.json` files when their current replacement file is present, and the application project deletes those two obsolete copied files from build/publish theme directories. This prevents incremental-build leftovers from becoming selectable themes or producing duplicate-id warnings.

The source files are stored in:

```text
src/TeeKay87.MemoryEngine.App/Themes/
```

During build they are copied to the application's runtime `Themes` directory.

## Runtime Discovery

At application startup, `ThemeManager` enumerates `*.json` files from:

```text
<App directory>\Themes\
```

Each file is independently parsed and validated. A valid file becomes available in the Theme dropdown. An invalid file is skipped and its validation error is surfaced through the host status/error presentation.

Theme files are sorted first by their numeric `order` value and then by display name.

Theme ids are case-insensitive for lookup and must be unique among all valid discovered files.

## Theme Selection

Selecting another theme in the application bar applies it immediately. No restart is required.

The theme system replaces shared `SolidColorBrush` resources in `Application.Resources`. Shared views and control templates reference those brushes through `DynamicResource`, which allows the currently open WPF visual tree to pick up the replacement resources at runtime.

The currently selected theme id is persisted in:

```text
%LocalAppData%\TeeKay87\MemoryEngine\settings.json
```

On the next application start, the saved id is selected when the same valid theme is available.

Fallback order is:

1. the previously selected valid theme;
2. the **Dark** theme (`dark` id), when available;
3. the first valid discovered theme;
4. the emergency palette embedded in `App.xaml` when no external theme can be loaded.

The embedded palette is not a fourth selectable theme. It exists only to keep the application readable when the external theme directory is missing or unusable.

## JSON Format

A theme file uses the following structure:

```json
{
  "id": "example",
  "name": "Example",
  "order": 100,
  "colors": {
    "WindowBackground": "#0B0F14"
  }
}
```

`id` is the stable machine-readable identifier used for persistence. The legacy ids `darker` and `darkest` are reserved compatibility aliases, are canonicalized to `dimmed` and `dark` when files are loaded, and must not be assigned to new custom themes.

`name` is the user-facing name shown in the Theme dropdown.

`order` determines the preferred ordering in the dropdown.

`colors` contains the complete application palette.

Color values must use either:

```text
#RRGGBB
#AARRGGBB
```

Partial themes are not supported. Every required palette key must be present so changing themes cannot leave stale colors from the previous theme.

## Required Palette Keys

Every theme currently defines:

| Key | Purpose |
| --- | --- |
| `WindowBackground` | Main application/window background |
| `PanelBackground` | Primary panel/card background |
| `PanelSecondary` | Secondary panel/header background |
| `SurfaceRaised` | Raised control/table header surface |
| `Border` | Normal separators and borders |
| `BorderStrong` | Emphasized/focused borders |
| `PrimaryText` | Main text |
| `SecondaryText` | Muted/supporting text |
| `Accent` | Primary accent and primary action color |
| `AccentMuted` | Low-emphasis accent surface |
| `Selection` | Selected list/table row surface |
| `InputBackground` | TextBox/ComboBox input background |
| `InputBorder` | Normal input border |
| `PrimaryButtonBackground` | Primary action button background |
| `PrimaryButtonBorder` | Primary action button border |
| `PrimaryButtonText` | Primary action button text |
| `SecondaryButtonBackground` | Secondary/neutral button background |
| `SecondaryButtonBorder` | Secondary/neutral button border |
| `SecondaryButtonText` | Secondary/neutral button text |
| `DisabledBackground` | Disabled control/button background |
| `DisabledBorder` | Disabled control/button border |
| `DisabledText` | Disabled text |
| `DangerBackground` | Stop/cancel/disconnect/destructive action surface |
| `DangerBorder` | Stop/cancel/disconnect/destructive action border |
| `DangerText` | Stop/cancel/disconnect/destructive action text |
| `ErrorText` | Error status text |
| `SuccessText` | Success status text |
| `WarningText` | Warning status text |
| `ToolTipBackground` | Tooltip/hint background |
| `ToolTipBorder` | Tooltip/hint border |
| `ToolTipText` | Tooltip/hint text |
| `HoverOverlay` | Button hover overlay color |
| `PressedOverlay` | Button pressed overlay color |

The mapping from these stable JSON keys to WPF brush resource keys is centralized in `ThemeManager`.


## Button and Tooltip Palette Independence

From `0.1.2.rev1`, ordinary button surfaces no longer have to borrow the generic Accent, PanelSecondary, or PrimaryText palette entries. Themes can independently define background, border, and text colors for Primary and Secondary buttons. Danger buttons continue to use their existing dedicated danger palette. From `0.1.2.rev5`, that palette is explicitly the semantic surface for stop/termination actions such as Disconnect and Cancel Scan as well as destructive actions. Bundled themes use complementary red Danger colors, while custom themes can replace those values without changing view XAML.

This makes it possible for a Light theme to use a lighter primary-action surface without changing every accent-colored element in the application, while Dimmed/Dark themes can keep their own button contrast rules.

Tooltips/hints also use dedicated background, border, and text colors. The host owns the WPF `ToolTip` template, so string tooltips generated by properties such as `ToolTip="..."` no longer fall back to the operating-system tooltip chrome. Changing the active theme updates already-open tooltips through the same DynamicResource mechanism as other application controls.

From `0.1.2.rev4`, the host also owns the `ContextMenu`, `MenuItem`, and `Separator` templates. This removes the operating-system icon/checkmark gutter that appeared as a bright vertical strip in Dimmed/Dark scan-result context menus. Context menus use the existing theme brushes through `DynamicResource`, including the normal panel surface, border, primary/disabled text, and accent-muted hover surface. No new palette keys are required for this correction.

## Adding a Theme

To add another color theme:

1. copy one of the existing theme JSON files;
2. give it a unique stable `id`;
3. provide a user-facing `name`;
4. choose an `order` value;
5. define every required color key;
6. keep all values in `#RRGGBB` or `#AARRGGBB` format;
7. place the file in the runtime `Themes` directory, or in the source `Themes` directory when it should ship with the application;
8. restart the application so startup discovery can find the new file.

The application currently discovers theme files at startup. Changing the selected theme is live, but adding, removing, or editing theme files while the application is already running does not trigger automatic file-system reload.

## Design Rules

A color theme must never contain:

- XAML;
- control templates;
- styles;
- layout dimensions;
- commands;
- bindings;
- plugin configuration;
- platform-specific behavior;
- executable code.

If a new UI component needs another semantic color, the application palette contract should be deliberately extended and all bundled themes updated together. Views should not work around a missing theme color by introducing local hardcoded colors.

Plugin-specific themes are not part of the architecture. The active color theme belongs to the host presentation layer and applies consistently regardless of which platform plugin is active.
