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

## Debugger Workspace Selector Buttons

The Debugger's upper-right Breakpoints / Watchpoints and Call Stack workspace no longer uses WPF `TabControl`/`TabItem` chrome. Rev18-rev23 demonstrated that even application-owned tab templates could retain a Windows runtime edge-clipping problem. Rev24 therefore replaces that presentation with compact selector buttons while keeping the same exclusive-view behavior.

The selector styles live in `Resources/Styles/ButtonStyles.xaml` and reuse the existing `ButtonBaseStyle`/`SecondaryButtonStyle` template rather than defining another control chrome implementation. They therefore inherit the same theme-owned background, foreground, disabled state, hover/pressed overlays, and focus behavior as ordinary application buttons. The active selector adds a two-unit `AccentBrush` border outline through a ViewModel-bound selection-state trigger. No hardcoded selected color is introduced.

The compact selector base is intentionally `28` units high with `FontSize=12`; this is a specialized workspace-navigation form factor rather than a new ordinary-button baseline. The normal `UiMetrics.StandardControlHeight=34` remains authoritative everywhere else. Breakpoints / Watchpoints is selected by default when both workspaces are available; a backend that exposes only Call Stack starts on Call Stack.

`WorkspaceTabControlStyle` and `WorkspaceTabItemStyle` were removed from active shared resources in rev24 because they have no remaining production consumer. Historical rev18-rev23 source-review and verification documents retain the exact tab implementations used during those revisions. Future workspace navigation should prefer existing application controls and theme resources rather than restoring the superseded Debugger tab templates merely for visual resemblance.

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

`colors` contains the required application palette and may also contain optional semantic extension colors recognized by the current host.

Color values must use either:

```text
#RRGGBB
#AARRGGBB
```

Partial required palettes are not supported. Every required palette key below must be present so changing themes cannot leave stale colors from the previous theme. Optional semantic extension keys may be omitted; the host resolves each omitted optional key to a documented required-palette fallback.

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


## Optional Disassembly Syntax Palette Keys

Application `0.1.6.rev6` adds optional semantic colors for syntax-highlighted instruction text:

| Key | Purpose | Fallback when omitted |
| --- | --- | --- |
| `DisassemblyMnemonic` | Ordinary instruction mnemonic | `Accent` |
| `DisassemblyFlowControl` | Call/jump/conditional/return/interrupt mnemonic | `WarningText` |
| `DisassemblyRegister` | Register token | `SuccessText` |
| `DisassemblyNumber` | Immediate values and formatted addresses | `WarningText` |
| `DisassemblyKeyword` | Formatter keywords/directives/prefix-like presentation text | `SecondaryText` |

These keys are intentionally optional so an existing valid custom theme created before Plugin API `2.11.0` remains loadable. Bundled Light, Dimmed, and Dark themes define explicit values. `ThemeManager` creates the WPF brushes on every theme application and derives any omitted optional value from the active theme's required fallback color. The syntax renderer uses dynamic resource references, so an already-open Disassembler updates when the active theme changes.



## Button and Tooltip Palette Independence

From `0.1.2.rev1`, ordinary button surfaces no longer have to borrow the generic Accent, PanelSecondary, or PrimaryText palette entries. Themes can independently define background, border, and text colors for Primary and Secondary buttons. Danger buttons continue to use their existing dedicated danger palette. From `0.1.2.rev5`, that palette is explicitly the semantic surface for stop/termination actions such as Disconnect and Cancel Scan as well as destructive actions. From `0.1.5.rev8`, application-owned Remove/Delete/Cancel/Exit/Abort/Disconnect-style buttons consistently use that same Danger palette, including dialog Cancel buttons and Memory Viewer bookmark Remove. Bundled themes use complementary red Danger colors, while custom themes can replace those values without changing view XAML.

This makes it possible for a Light theme to use a lighter primary-action surface without changing every accent-colored element in the application, while Dimmed/Dark themes can keep their own button contrast rules.

Tooltips/hints also use dedicated background, border, and text colors. The host owns the WPF `ToolTip` template, so string tooltips generated by properties such as `ToolTip="..."` no longer fall back to the operating-system tooltip chrome. Changing the active theme updates already-open tooltips through the same DynamicResource mechanism as other application controls.

From `0.1.3.rev8`, string tooltip content also wraps inside the shared 420-DIP maximum width. WPF may materialize string content as either `TextBlock` or `AccessText`, so the application template applies wrapping to both generated text-element forms. Long plugin descriptions and host hints therefore remain fully readable instead of being clipped by the popup boundary.

From `0.1.2.rev4`, the host also owns the `ContextMenu`, `MenuItem`, and `Separator` templates. This removes the operating-system icon/checkmark gutter that appeared as a bright vertical strip in Dimmed/Dark scan-result context menus. Context menus use the existing theme brushes through `DynamicResource`, including the normal panel surface, border, primary/disabled text, and accent-muted hover surface. No new palette keys are required for this correction.

## Settings and Modal Dialog Surfaces

From `0.1.3.rev9`, the application-level Settings window and reusable operation-progress dialog use the same host `DynamicResource` palette and shared control styles as the main window. They do not define local light/dark palettes or plugin-specific colors. Settings therefore follows the active theme for cards, text, inputs, and buttons, while the progress dialog uses the existing Window/Input/Border/Accent resources and the shared Danger button role when cancellation is available.

From `0.1.3.rev23`, the host also owns a reusable confirmation dialog. Its window/card/divider/text surfaces resolve the same shared resources, while Information, Warning, and Danger tones select existing semantic brushes and button styles through resource references. The dialog introduces no new theme keys. The Saved Addresses **Remove All** confirmation is its first production use and uses the Danger tone.

These owned modal surfaces must be checked in Light, Dimmed, and Dark whenever their templates change. A new binary confirmation should reuse `ConfirmationDialogService`; a different dialog type should still reuse the central palette rather than introduce fixed color values.

From `0.1.5.rev1`, the modeless **Memory Viewer** is also entirely host-themed. Its window, cards, Address input, command buttons, DataGrid, status text, and error text reuse the same shared styles and `DynamicResource` palette as the main workspace. Application `0.1.5.rev4` adds a persistent green origin-row marker. `ThemeManager` derives a translucent `SuccessMutedBrush` from each active theme's existing `SuccessText` color, so the marker updates immediately with theme changes without adding a new required JSON palette key or breaking existing custom theme files. Application `0.1.5.rev5` keeps that same palette behavior but removes the selected-origin border that altered DataGrid layout; selection must not change the marker row's height or width. Application `0.1.5.rev6` adds the owned **Edit Hex Bytes** modal, which reuses `CardBorderStyle`, semantic button styles, shared input styling, and the existing Accent/Error/Border/Window resources rather than introducing edit-specific palette keys. Application `0.1.5.rev7` adds region-navigation buttons and bookmark controls using the same existing semantic button, ComboBox, input, and tooltip resources; no new palette key is introduced. Application `0.1.5.rev8` keeps that palette unchanged, moves bookmark **Remove** onto the existing Danger semantic, and corrects selected-bookmark text without introducing bookmark-specific brushes/templates. The viewer and edit dialog must be checked in Light, Dimmed, and Dark whenever this presentation changes.

Application `0.1.6.rev3` applies the same rule to the modeless **Disassembler**, `0.1.6.rev4` expands the visible range to bidirectional context and adds Scan Results/Saved Addresses entry points, and `0.1.6.rev5` keeps the same presentation resources while correcting continuous decode/origin resolution behavior. Application `0.1.6.rev6` added optional semantic syntax brushes to the original Address / Bytes / Instruction DataGrid. Host `0.1.7.rev29` inserts the neutral `Markers` text column between Bytes and Instruction without adding a new theme color or control template. Mnemonics, flow-control mnemonics, registers, numbers, and keywords can receive separate theme-aware foregrounds; plain text inherits the normal cell foreground, invalid instructions continue to use `ErrorTextBrush`, and the decoded instruction containing the requested address continues to use `SuccessMutedBrush` as the origin row. Back/Forward are active in rev6 and use the ordinary shared button/disabled-state palette. No fixed syntax color is placed in Disassembler XAML; bundled themes supply explicit optional values and older custom themes use `ThemeManager` fallbacks. Origin, selection, syntax, and Markers presentation must remain layout-neutral in Light, Dimmed, and Dark. Marker text inherits the normal themed DataGrid foreground; no marker-specific hardcoded color is introduced.

Application `0.1.6.rev7` adds Memory Viewer value-span highlighting without adding a new palette key. Highlighted Hex Bytes and corresponding ASCII characters reuse the existing `PrimaryButtonBackgroundBrush` + `PrimaryButtonTextBrush` pair through `DynamicResource`, while the containing origin row continues to use `SuccessMutedBrush`. The byte span is indicated only by inline foreground/background brushes: no bold weight, border, margin, padding, font-size, or row-height change is used. Existing custom themes therefore inherit the feature automatically from their already-required Primary button palette. Application `0.1.6.rev8` keeps those visuals unchanged and only makes the six inline text bindings explicitly OneWay so the read-only presentation properties render without a WPF binding exception. Application `0.1.6.rev9` adds the manual Saved Address dialog using only existing `WindowBackground`, card/panel, border, text, input, Primary/Secondary, Danger, and disabled-control resources. Corrective `0.1.6.rev10` changes no dialog visuals or theme resources. Its planned controls intentionally rely on the normal disabled presentation, so rev9 introduces no new required theme key and custom themes remain compatible.


## Disabled-State Contrast

From `0.1.3.rev2`, the bundled themes deliberately increase the visual difference between enabled and disabled controls. `DisabledBackground`, `DisabledBorder`, and `DisabledText` are used by buttons, text inputs, ComboBoxes, menu items, and other shared controls. ComboBox arrows inherit the disabled foreground, disabled CheckBox content is reduced in opacity, and disabled context-menu items dim the entire row.

From `0.1.3.rev5`, standard button templates apply those disabled brushes directly to their rendered border and content when disabled. This guarantees that Primary, Secondary, Danger, and derived workflow button styles all visibly honor the active theme's disabled palette even when their enabled semantic style also sets foreground/background/border values. From `0.1.3.rev6`, string labels materialized by WPF as `AccessText` or `TextBlock` also receive `DisabledButtonTextBrush` directly, ensuring the disabled text palette is visible rather than falling back to the normal application foreground.

A custom theme should choose disabled colors that remain readable but clearly lower-contrast than the corresponding enabled surface. Disabled styling is semantic application state; themes may change its colors but must not remove or override whether a control is enabled.

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
