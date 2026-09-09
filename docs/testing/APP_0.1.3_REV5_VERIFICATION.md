# TeeKay87's Memory Engine 0.1.3.rev5 Verification

## Purpose

This checklist verifies **0.1.3.rev5 - Global Disabled Button State Fix**. The revision is deliberately presentation-only: it corrects how the shared WPF button template renders `IsEnabled=false` and does not change command availability, scanner behavior, plugin contracts, PS5 transport, or target memory operations.

## Expected identities

| Component | Expected version |
| --- | --- |
| TeeKay87's Memory Engine | `0.1.3.rev5` |
| Plugin API | `2.0.0` |
| PlayStation 5 plugin | `0.1.0.rev11` |
| In-Memory Test Target plugin | `1.0.0.rev3` |

Only the host revision changes.

## Root-cause boundary

Before rev5, `ButtonBaseStyle` supplied disabled Foreground/Background/BorderBrush values through a base style trigger. Semantic styles and workflow-local derived styles also supply those same properties for their enabled appearance. Runtime testing showed that standard buttons could therefore keep their enabled semantic colors while disabled, even though other controls such as ComboBox visibly used the disabled palette.

Rev5 retains the semantic styles but additionally enforces the visible disabled colors inside the shared `ControlTemplate` itself:

- `ButtonBorder.Background` → `DisabledButtonBackgroundBrush`;
- `ButtonBorder.BorderBrush` → `DisabledButtonBorderBrush`;
- the named content presenter `TextElement.Foreground` → `DisabledButtonTextBrush`;
- hover/pressed overlay and focus border remain suppressed while disabled.

No individual Scan-panel button receives a one-off fix.

## 1. Build and automated regression

Run:

```powershell
dotnet clean .\TeeKay87.MemoryEngine.sln -c Release
dotnet build .\TeeKay87.MemoryEngine.sln -c Release
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Required result:

```text
All 22 checks passed.
```

The automated count remains 22 because the changed behavior is WPF rendering rather than scanner/plugin logic.

## 2. Initial Scan-panel disabled state

Start the application and select/connect the In-Memory Test Target as needed so the Scan panel is visible in its initial no-scan state.

Confirm:

- **First Scan** is enabled and retains the current theme's Primary appearance;
- **Next Scan** is disabled and its label is visibly dimmed;
- **New Scan** is disabled and its label is visibly dimmed;
- **Cancel Scan** is disabled and does **not** retain the enabled Danger red/text presentation;
- disabled button surfaces/borders use the lower-contrast disabled palette;
- moving the pointer over a disabled button does not produce hover/pressed feedback;
- the cursor does not suggest a clickable action.

## 3. Global button coverage

Inspect the permanently disabled Saved Addresses placeholder actions:

- **Add Address**;
- **Edit**;
- **Remove**;
- **Export...**.

All must show the same common disabled palette appropriate to the active theme. `Remove` must not remain visually Danger-red merely because it is based on `DangerButtonStyle`.

This proves the correction applies through `ButtonBaseStyle`, not through Scan-panel-specific XAML.

## 4. Command-driven state transitions

Run a normal scan workflow:

1. enter a valid value;
2. run **First Scan**;
3. confirm **Next Scan** becomes enabled and receives normal workflow emphasis;
4. confirm **First Scan** is no longer the active primary action when the workflow says Next Scan is next;
5. run/refine as appropriate;
6. use **New Scan** and confirm the initial command states return;
7. start a sufficiently long scan and confirm **Cancel Scan** becomes enabled with its normal Danger appearance only while cancellation is actually available;
8. after completion/cancellation, confirm it returns immediately to the common disabled presentation.

The fix must not change which command is enabled; only its disabled rendering is being verified.

## 5. Theme coverage

Repeat sections 2 and 3 in:

- Light;
- Dimmed;
- Dark.

For each theme, disabled labels must be visibly lower-emphasis than enabled labels while remaining readable. Background and border differences must also be apparent.

## 6. Other disabled controls regression

Confirm existing non-button disabled styling remains intact:

- the Scan Type ComboBox still uses its disabled foreground/arrow presentation when not selectable;
- disabled Value Type behavior remains correct when a scan session locks it;
- disabled context-menu entries such as non-Int32 **Change value** remain visibly dimmed;
- disabled CheckBox/TextBox behavior is unchanged where applicable.

## Acceptance criteria

| Verification | Required result |
| --- | --- |
| Release build | PASS |
| Automated verification | 22/22 PASS |
| Initial Scan buttons visibly distinguish enabled vs disabled | PASS |
| Disabled Primary/Secondary/Danger buttons all use common disabled palette | PASS |
| Saved Addresses placeholder buttons use common disabled palette | PASS |
| Command-driven enable/disable transitions render immediately | PASS |
| Light theme | PASS |
| Dimmed theme | PASS |
| Dark theme | PASS |
| ComboBox/menu/other disabled controls unchanged | PASS |
| Scanner/plugin behavior unchanged | PASS |
