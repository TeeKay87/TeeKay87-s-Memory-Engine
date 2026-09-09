# Reusable Confirmation Dialog

## Purpose

TeeKay87's Memory Engine `0.1.3.rev23` introduces a host-owned confirmation dialog for actions that require an explicit user decision before they continue.

The component replaces operating-system `MessageBox` confirmation surfaces in the application. It is intentionally generic: a caller supplies the wording and semantic tone, while the dialog owns the common WPF presentation, modality, keyboard behavior, theme integration, and button layout.

The first production use is **Remove All** in Saved Addresses.

## Components

The reusable confirmation surface is implemented under:

```text
src/TeeKay87.MemoryEngine.App/Dialogs/
├── ConfirmationDialog.xaml
├── ConfirmationDialog.xaml.cs
├── ConfirmationDialogOptions.cs
└── ConfirmationDialogService.cs
```

`ConfirmationDialog` is an internal WPF window. Application workflows should open it through `ConfirmationDialogService` rather than constructing the window directly.

## Request Model

`ConfirmationDialogOptions` carries the operation-specific presentation:

- `Title` — dialog/window title;
- `Message` — primary confirmation text;
- `ConfirmButtonText` — text for the affirmative action;
- `CancelButtonText` — text for the safe/negative action;
- `Tone` — semantic visual treatment;
- `ConfirmIsDefault` — whether Enter should invoke the affirmative action.

The current semantic tones are:

```text
Information
Warning
Danger
```

The tone affects the dialog badge and the affirmative button role. Information and Warning use the normal Primary action style. Danger uses the shared Danger button style so destructive confirmations use the same semantic surface as Remove, Disconnect, Cancel Scan, and other destructive/termination actions.

The options model deliberately contains no target, plugin, scanner, or Saved Addresses concepts. Future host workflows can reuse the same component without adding feature-specific behavior to the dialog.

## Modality and Ownership

`ConfirmationDialogService.Show(...)` requires a WPF owner window and must be called from the UI thread.

The service:

1. validates the owner and request;
2. creates the reusable confirmation window;
3. assigns the supplied owner;
4. opens the window with `ShowDialog()`;
5. returns `true` only when the affirmative action is selected.

The owner is disabled by WPF for the duration of the modal dialog. The confirmation window is centered on its owner and does not appear as a separate taskbar item.

Escape/cancel closes the dialog with a negative result. Callers decide whether the affirmative action should be the default Enter action through `ConfirmIsDefault`; when it is false, the dialog makes the Cancel button the safe Enter-key default.

## Theme Integration

The dialog contains no fixed application palette.

Its window, card, divider, text, badge, and buttons use the same shared WPF resources as the rest of the host, including:

- `WindowBackgroundBrush`;
- `PanelBackgroundBrush`;
- `BorderBrush`;
- `PrimaryTextBrush`;
- `AccentBrush` / `AccentMutedBrush`;
- `WarningTextBrush`;
- `DangerButtonBackgroundBrush` / `DangerButtonBorderBrush` / `DangerButtonTextBrush`;
- `PrimaryButtonStyle`;
- `SecondaryButtonStyle`;
- `DangerButtonStyle`.

The resources are resolved through `DynamicResource` either directly in XAML or through resource references selected for the active tone. The dialog therefore inherits the active Light, Dimmed, or Dark theme automatically and does not need theme-specific branches.

No new color keys are introduced in rev23.

## Remove All Saved Addresses

The Saved Addresses **Remove All** action is the first caller of the service.

It uses:

```text
Title:        Remove All Saved Addresses
Confirm:      Remove All
Cancel:       Cancel
Tone:         Danger
Default:      Cancel
```

The confirmation wording includes the current Saved Addresses count. Confirming continues into the existing rev22 `RemoveAllSavedAddresses()` coordination path; cancelling makes no change.

Only the confirmation surface changed. The rev22 behavior that can defer the confirmed removal set until Saved Address I/O reaches a safe idle boundary remains unchanged.

## Reuse Rules

Future confirmation workflows should:

1. use `ConfirmationDialogService` instead of `MessageBox.Show`;
2. keep operation-specific wording in the caller;
3. select the semantic tone that matches the action;
4. use Danger for destructive/irreversible confirmation actions;
5. avoid making a destructive action the default Enter action unless there is a deliberate UX reason;
6. keep business logic outside the dialog;
7. reuse the existing theme resources rather than adding fixed colors to a new confirmation surface.

If a future workflow requires more than a binary confirm/cancel decision, text input, a selectable list, or long-running progress, it should use an appropriate dedicated reusable component rather than overloading this confirmation dialog.
