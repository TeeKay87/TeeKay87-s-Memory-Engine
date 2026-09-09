# TeeKay87's Memory Engine 0.1.3.rev23 Verification

## Scope

This checklist verifies **TeeKay87's Memory Engine 0.1.3.rev23 - Themed Confirmation Dialog and Value Type Defaults**.

Rev23 has two focused user-facing changes:

1. Saved Addresses **Remove All** now uses the reusable application-owned confirmation dialog and must follow the active theme.
2. Standard signed integer Value Types use the compact labels **1 Byte**, **2 Bytes**, **4 Bytes**, and **8 Bytes**, while unsigned variants remain explicit and signed **4 Bytes**/Int32 remains the built-in default.

Rev22 user-intent/Saved Address I/O coordination is intentionally preserved and receives a focused regression check below.

## 1. Build and Automated Verification

On the normal Windows development machine:

1. clean/rebuild the solution;
2. confirm there are no compiler or XAML errors;
3. run `TeeKay87.MemoryEngine.Tests`;
4. confirm the existing verification suite still ends with:

```text
All 34 checks passed.
```

The scan-capability check must now also verify:

- PS5 `DefaultValueTypeId == standard.int32`;
- Mock `DefaultValueTypeId == standard.int32`;
- signed standard display names are `1 Byte`, `2 Bytes`, `4 Bytes`, `8 Bytes`;
- unsigned standard display names remain explicit as `1 Byte (Unsigned)`, `2 Bytes (Unsigned)`, `4 Bytes (Unsigned)`, and `8 Bytes (Unsigned)`.

## 2. Application Identity

Launch the application and verify:

- title/version surfaces show `0.1.3.rev23`;
- the application starts normally with the previously selected theme and settings;
- both built-in plugins still load with their existing independent plugin versions.

## 3. Remove All Confirmation - Theme Integration

Create at least two Saved Addresses so **Remove All** is enabled.

Repeat the following in **Light**, **Dimmed**, and **Dark**:

1. select the theme in the application;
2. click **Remove All**;
3. verify the dialog is centered on and modal to the main window;
4. verify the dialog background, card, border, text, divider, badge, and buttons belong visually to the selected application theme;
5. verify there is no bright/default Windows `MessageBox` content surface inside the application;
6. verify the title is **Remove All Saved Addresses**;
7. verify the message includes the current Saved Address count;
8. verify the buttons are **Cancel** and **Remove All**;
9. verify the Remove All button uses the application's Danger/destructive presentation.

Switch themes between tests rather than while one modal dialog is open.

## 4. Confirmation Keyboard and Cancel Safety

With Saved Addresses present:

1. open **Remove All**;
2. press **Escape** and verify the dialog closes without removing anything;
3. open it again and click **Cancel**; verify nothing is removed;
4. open it again and press **Enter** without first moving focus to the destructive button; verify the dialog takes the safe Cancel path and nothing is removed;
5. open it again and click **Remove All**; verify the confirmed removal proceeds.

The destructive affirmative action is intentionally configured with `ConfirmIsDefault = false`; the reusable dialog therefore makes Cancel the Enter-key default.

## 5. Remove All rev22 Coordination Regression

Exercise the timing case that rev22 fixed:

1. create several Saved Addresses;
2. keep normal Value refresh active and, if useful for timing, enable Frozen on one or more rows;
3. click **Remove All** and confirm while Saved Address I/O is active;
4. verify the confirmation is accepted on the first attempt;
5. verify Frozen is disabled for rows registered for removal;
6. verify already-started target I/O is allowed to finish safely;
7. verify the rows are removed automatically at the next all-Saved-Address-I/O idle boundary;
8. verify no second Remove All click is required.

Also repeat a normal individual-row Remove to ensure rev21 queued row removal remains unaffected.

## 6. Value Type Presentation

With the PS5 plugin selected, open the **Value Type** list before First Scan and verify these standard integer labels are present:

```text
1 Byte
1 Byte (Unsigned)
2 Bytes
2 Bytes (Unsigned)
4 Bytes
4 Bytes (Unsigned)
8 Bytes
8 Bytes (Unsigned)
```

The exact list order remains plugin-owned; this check concerns labels and semantics, not a new Core ordering rule.

Also verify:

- **4 Bytes** is selected by default in a fresh scan/workspace state;
- **4 Bytes** continues to accept signed Int32 values, including negative decimal input;
- **4 Bytes (Unsigned)** remains a distinct selectable type with UInt32 range semantics;
- a newly created/reloaded plugin workspace initializes from the plugin-defined Int32 default as before; New Scan continues to preserve the user's current Value Type selection, matching existing behavior.

Repeat the default/label check with the In-Memory Test Target to verify the shared standard definitions behave identically outside the PS5 plugin.

## 7. Scanner and Saved Address Semantic Regression

Because rev23 changes display metadata rather than stable ids, verify a representative signed/unsigned pair:

1. perform a **4 Bytes** Exact Value scan for a known signed value;
2. save one result to Saved Addresses;
3. verify its Type displays **4 Bytes** and its value reads/edits normally;
4. switch a suitable row to **4 Bytes (Unsigned)** and verify the type remains distinct;
5. verify signed and unsigned parsing/range validation still differ as expected;
6. verify no scan session, result storage, endianness, alignment, or PS5 native-scan behavior changed solely because the label changed.

## 8. Reusable Dialog Source/Structure Check

Before release acceptance, confirm the codebase contains the reusable component under:

```text
src/TeeKay87.MemoryEngine.App/Dialogs/
```

and that:

- `ConfirmationDialogService` is the normal caller entry point;
- `ConfirmationDialogOptions` contains caller-specific wording/tone/default behavior;
- `ConfirmationDialog.xaml` uses `x:ClassModifier="internal"`;
- theme resources are referenced through shared resources rather than hardcoded Light/Dark colors;
- no `MessageBox.Show` remains in application source;
- Remove All business logic remains outside the dialog.

## Acceptance

Rev23 is ready to be considered verified when:

- the solution builds cleanly on Windows;
- all 34 automated checks pass;
- Remove All confirmation is visually correct in Light, Dimmed, and Dark;
- cancellation/keyboard behavior cannot accidentally trigger the destructive action;
- rev22 deferred Remove All behavior still succeeds on the first confirmed attempt;
- signed integer labels and unsigned suffixes appear as specified;
- **4 Bytes**/signed Int32 is the built-in default for both current plugins;
- representative signed/unsigned scans and Saved Address operations show no semantic regression.
