# TeeKay87's Memory Engine 0.1.3.rev6 Verification

## Purpose

This checklist verifies **0.1.3.rev6 - Disabled Button Text Rendering Fix**. The change is limited to shared WPF button-label rendering. Scanner behavior, plugin contracts, PS5 behavior, and command availability must remain unchanged.

## Expected versions

| Component | Expected |
| --- | --- |
| TeeKay87's Memory Engine | `0.1.3.rev6` |
| Plugin API | `2.0.0` |
| PlayStation 5 plugin | `0.1.0.rev11` |
| In-Memory Test Target plugin | `1.0.0.rev3` |

## Build and automated verification

1. Build the complete solution in Release configuration.
2. Require zero compiler errors.
3. Run `tests/TeeKay87.MemoryEngine.Tests`.
4. Require `All 22 checks passed.`.

## Disabled button label verification

Repeat these checks in **Dimmed**, **Dark**, and **Light** themes.

1. Start the application without an Active Target.
2. Confirm disabled Scan buttons use the theme's `DisabledText` color for their labels, not the normal primary/secondary button text color.
3. Confirm the disabled Saved Addresses buttons (**Add Address**, **Edit**, **Remove**, **Export...**) use the same disabled label color.
4. Compare an enabled button next to a disabled button. The difference must be immediately visible in both the button surface and label foreground.
5. Connect to the Mock target and exercise the scan workflow so First Scan/Next Scan/New Scan availability changes. Confirm a label changes back to the normal semantic button foreground when enabled and returns to `DisabledText` when disabled.
6. Verify enabled Primary, Secondary, and Danger buttons retain their existing theme colors.

## Acceptance

The revision passes when all three bundled themes visibly render disabled button labels with their configured `DisabledText` brush and no existing command/scanner/plugin behavior regresses.
