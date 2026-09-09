# TeeKay87's Memory Engine 0.1.6.rev10 Verification

## Build Identity

- Application: `0.1.6.rev10`
- Feature: `Manual Saved Address Compile Fix`
- Plugin API: `2.11.0`
- Mock plugin: `1.0.0.rev6`
- PS5 plugin: `0.1.0.rev24`
- Expected automated verification count: **72**

## Scope

Revision 10 is a corrective host/WPF revision for the manual Saved Address dialog introduced in `0.1.6.rev9`. The first Windows build of rev9 exposed `CS0177` in `ManualSavedAddressDialog.TryParseHexAddress`: the method returned the result of a short-circuit `&&` expression whose right-hand `ulong.TryParse(..., out address)` call was not guaranteed to execute. When the candidate address length was invalid, control could leave the method without the C# compiler being able to prove that the `out address` parameter had been assigned.

The correction initializes `address` to zero before normalization and the short-circuit length guard. No manual-entry semantics, Saved Address model behavior, target/session safety, scanner behavior, Memory Viewer behavior, Disassembler behavior, plugin contract, or platform-specific implementation is changed.

The XAML errors shown alongside CS0177 in the rev9 Windows build are expected to be cascading designer/build errors caused by the App assembly not compiling. Rev10 must nevertheless verify that the referenced production XAML types remain present and that those errors disappear after the primary C# error is corrected.

## 1. Clean Windows Build

1. Delete stale `bin` and `obj` directories if present.
2. Restore and build the complete solution on Windows.
3. Confirm that `ManualSavedAddressDialog.xaml.cs` compiles without `CS0177`.
4. Confirm that `MainWindow.xaml` no longer reports missing `System.Object`, `ProportionalGridSplitter`, or `TextBoxInputFilter` errors once the App project builds successfully.
5. Start the application.
6. Confirm the permanent bottom status bar shows `0.1.6.rev10`.
7. Confirm the native window title and compact application-title row remain title-only.

Expected: **PASS**.

## 2. Automated Verification

Run the verification executable and confirm the final line is:

```text
All 72 checks passed.
```

Rev10 adds the check **Saved Addresses manual-entry address parser out initialization**. It reads the production dialog code-behind fixture and verifies that `TryParseHexAddress` assigns `address = 0;` before the candidate-length short-circuit guard. Existing 71 checks must remain unchanged and pass.

Expected: **72/72 PASS**.

## 3. Manual Dialog Smoke Test

With Mock Target connected and an Active Target set:

1. Click **Add Manually**.
2. Confirm the dialog opens without an exception.
3. Enter an invalid address longer than 16 hexadecimal digits and click **Add**.
4. Confirm the dialog stays open and displays its validation message rather than throwing or closing unexpectedly.
5. Enter a valid address such as `0x10000104`, choose a supported fixed-size Value Type, and add it.
6. Confirm a Saved Address row is created and receives its current value through the existing refresh path.

Expected: **PASS**.

## 4. Variable-Length Validation

1. Open **Add Manually**.
2. Select a variable-length Value Type such as Array of Bytes.
3. Verify invalid lengths below `1` or above `4096` are rejected.
4. Verify a valid length is accepted.

Expected: **PASS**.

## 5. Existing Manual-Entry UI Contract

Confirm the Saved Addresses toolbar remains:

```text
Add Manually | Remove All | Export
```

Confirm the dialog still exposes functional Address, Description, Value Type, Length, and current-value preview areas, and that the planned Hexadecimal/Binary/Text/Pointer controls remain visibly disabled.

Expected: **PASS**.

## 6. Regression Checks

Confirm the following previously verified behavior is unchanged:

- Scan Results and Saved Addresses context menus still provide **Open in Memory Viewer** and **Open in Disassembler** where applicable.
- Memory Viewer opens normally and retains value-span highlighting.
- Disassembler retains continuous around-origin decoding, syntax highlighting, and Back/Forward history.
- Scan workflow, Saved Address refresh/freeze/write behavior, and universal export continue to function normally.
- No plugin version or Plugin API version changes occur in rev10.

## Acceptance

`0.1.6.rev10` is accepted when the complete Windows solution builds cleanly, all **72/72** automated checks pass, the Add Manually dialog opens and handles both invalid and valid hexadecimal addresses without error, and the cascading XAML errors reported with rev9 are absent.
