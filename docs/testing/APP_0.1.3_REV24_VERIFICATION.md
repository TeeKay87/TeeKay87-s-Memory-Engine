# TeeKay87's Memory Engine 0.1.3.rev24 Verification

## Scope

This checklist verifies **TeeKay87's Memory Engine 0.1.3.rev24 - Memory Input Validation**.

Revision 24 adds a reusable live-input layer to memory-oriented TextBoxes while preserving the existing final parsers as the authoritative validation boundary. It also advances the host Plugin API to `2.5.0` by adding the optional `IMemoryValueInputPolicy` companion contract.

The PS5 plugin remains `0.1.0.rev16` targeting Plugin API `2.4.0`. The Mock plugin remains `1.0.0.rev3` targeting Plugin API `2.0.0`. Neither plugin contains platform-specific rev24 code; standard Value Types receive their input policies from the shared Plugin SDK.

## 1. Build and Automated Verification

1. Build the complete solution on Windows using the normal Visual Studio/.NET 9 workflow.
2. Confirm there are no C# or XAML compile errors.
3. Run `TeeKay87.MemoryEngine.Tests`.
4. Confirm all checks pass and the final line is:

```text
All 35 checks passed.
```

5. Confirm the plugin-version check reports host Plugin API `2.5.0`, PS5 `0.1.0.rev16` targeting `2.4.0`, and Mock `1.0.0.rev3` targeting `2.0.0`.
6. Confirm the new **Standard Value Type live-input policies** check passes.

## 2. Version and Startup Regression

1. Start the application normally.
2. Confirm the title/version surfaces show `0.1.3.rev24` and the feature title **Memory Input Validation** where the application displays its current revision feature.
3. Confirm both bundled platform plugins are discovered.
4. Confirm PS5 connection fields still render normally and remain editable as free-form plugin-owned fields.
5. Confirm the active theme, rev23 confirmation dialog, Saved Addresses timers, and scan controls load without visual regressions.

## 3. Scan Value - Signed Integer

Use the default **4 Bytes** Value Type.

1. Clear the Scan Value field. Confirm the empty edit state is allowed.
2. Enter a negative decimal value such as `-123`. Confirm the minus sign and digits are accepted.
3. Try inserting letters such as `G`, `x` in an invalid position, punctuation such as `!`, or a decimal point. Confirm the impossible character is not inserted.
4. Enter `0x1234ABCD`. Confirm the hexadecimal prefix and hexadecimal digits are accepted.
5. Try entering a non-hex letter such as `G` after `0x`. Confirm it is blocked.
6. Paste a valid decimal value and confirm it is accepted.
7. Paste a valid `0x` value and confirm it is accepted.
8. Paste text containing invalid characters and confirm the paste is rejected rather than partially corrupting the existing field.
9. Leave the field as an incomplete state such as `-` or `0x`, then invoke First Scan/Next Scan. Confirm the existing final Value Type parser rejects the value and no scan begins.
10. Enter a syntactically valid number outside the Int32 range and invoke the scan. Confirm final range validation rejects it even though the digit sequence itself was allowed while editing.

## 4. Scan Value - Unsigned Integer

Select **4 Bytes (Unsigned)**.

1. Enter `123` and confirm it is accepted.
2. Enter `+123` and confirm it is accepted by the standard unsigned syntax.
3. Try inserting `-`. Confirm it is blocked.
4. Enter `0xFFFFFFFF` and confirm it is accepted for editing.
5. Enter/paste non-hexadecimal text into a prefixed value and confirm it is rejected.
6. Confirm a syntactically valid but out-of-range value is still rejected by the final parser when a scan is requested.

## 5. Scan Value - Float and Double

Select **Float**, then repeat representative checks with **Double**.

1. Confirm ordinary invariant decimal input such as `100`, `-12.5`, `.5`, and `1.` can be entered.
2. Confirm scientific notation such as `1e3`, `1e-3`, and `-1.25e+3` can be entered.
3. Confirm temporary states needed while editing, such as `-`, `.`, `-.`, `1e`, and `1e-`, are not blocked prematurely.
4. Confirm impossible sequences such as `1.2.3`, `1ee2`, misplaced signs, and unrelated letters are blocked.
5. Confirm paste follows the same rules.
6. Commit an incomplete temporary state and confirm the final parser rejects it.

## 6. Scan Value - Array of Bytes

Select **Array of Bytes**.

1. Confirm compact input such as `DEADBEEF` can be entered.
2. Confirm spaced input such as `DE AD BE EF` can be entered.
3. Confirm the parser-supported comma and hyphen separators can be entered.
4. Confirm prefixed byte tokens such as `0xDE,0xAD` can be entered.
5. Try entering/pasting non-hexadecimal characters such as `GG` or unsupported punctuation and confirm they are blocked.
6. Enter syntax that is still incomplete at edit time but invalid at commit time, then invoke a scan. Confirm the final Array-of-Bytes parser supplies the authoritative error and no scan begins.
7. Confirm the existing 4,096-byte maximum remains enforced by the final parser.

## 7. Saved Address Address

Create at least one Saved Address from a valid Scan Result.

1. Click the Address cell and edit it normally.
2. Confirm `0-9`, `A-F`, and `a-f` are accepted.
3. Confirm an optional leading `0x` or `0X` is accepted.
4. Confirm letters outside `A-F`, punctuation, spaces, and an `x` in any non-prefix position are blocked while typing.
5. Confirm paste applies the same rules.
6. Confirm more than 16 hexadecimal address digits cannot be inserted/pasted.
7. Leave an incomplete address such as `0x`, then press Enter or leave the cell. Confirm the existing final address parser rejects the commit and restores/retains the valid row address according to the established Saved Address edit behavior.
8. Enter a valid different address and commit it. Confirm the row refreshes from the new address as before.
9. Repeat while the row had been Frozen and confirm the existing rule that an Address change disables Frozen remains intact.

## 8. Saved Address Value

For a Saved Address row, test at least **4 Bytes**, **4 Bytes (Unsigned)**, **Float**, and **Array of Bytes** where appropriate for safe target memory.

1. Confirm the Value editor follows the currently selected row Type's syntax rules.
2. Confirm invalid typing/paste is blocked in the same way as the Scan Value field.
3. Change the row Type and confirm the live filter changes with the selected Value Type.
4. Confirm temporary edit states remain possible but are rejected if committed incomplete.
5. Enter a syntactically valid but out-of-range value and confirm the final `TryParse(...)` rejects it before a target write occurs.
6. Enter a valid value and confirm the existing manual write path still writes successfully and reports its normal status.
7. Repeat a valid Value edit on a Frozen row and confirm rev20-rev22 coordination remains intact: the new value becomes the Frozen target and stale refresh data does not overwrite the edit.

## 9. Settings Numeric Fields

Open Settings.

1. In **Value refresh (ms)**, confirm only decimal digits can be typed or pasted.
2. In **Frozen write (ms)**, confirm only decimal digits can be typed or pasted.
3. Confirm letters, signs, decimal points, whitespace, and other punctuation are blocked.
4. Confirm deleting all digits is allowed while editing.
5. Try saving an empty value and confirm the existing final settings validation rejects it.
6. Enter a numeric value below `50` or above `10000` and confirm Save rejects it using the established range validation.
7. Enter valid values within `50-10000`, save, and confirm the intervals apply/persist as before.

## 10. Deliberately Unrestricted Fields

1. Confirm Saved Address **Description** remains ordinary free-form text.
2. Confirm the scan-result storage path in Settings remains ordinary path text.
3. Confirm PS5 IP address/host name remains free-form at the host level.
4. Confirm the PS5 Port field has not been hardcoded into the shared WPF filter. Invalid port content should continue to be rejected by the PS5 plugin's existing connection validation rather than by platform-specific host logic.

## 11. Plugin Compatibility and Future-Type Boundary

1. Confirm the bundled PS5 plugin targeting API `2.4.0` still loads under host API `2.5.0`.
2. Confirm the bundled Mock plugin targeting API `2.0.0` still loads.
3. Confirm both plugins' standard **4 Bytes** Value fields receive live filtering even though their plugin metadata targets older API minors; the behavior comes from the shared standard Value Type objects.
4. If testing a custom compatible plugin Value Type that does not implement `IMemoryValueInputPolicy`, confirm its Value editor remains unrestricted while its existing `TryParse(...)` still controls final scan/write validity.
5. If testing a custom Value Type that implements `IMemoryValueInputPolicy`, confirm the host honors that policy without platform-name or type-id special cases.

## 12. Regression Checks

1. Run First Scan, Next Scan, and New Scan with valid values and confirm the rev13-rev15 disk-backed scanner behavior is unchanged.
2. Confirm the visible 50,000-row result preview behavior is unchanged.
3. Confirm Saved Address refresh and Frozen scheduling still use their configured intervals.
4. Confirm Disconnect and the other rev22 foreground actions still respond on the first accepted click around Saved Address I/O.
5. Confirm individual Remove and Remove All retain their queued-removal behavior.
6. Open Remove All and confirm the rev23 custom confirmation dialog still follows the active theme and has the same safe keyboard behavior.
7. Switch Light, Dimmed, and Dark themes and confirm textbox filtering has no theme-specific visual side effects.
8. Confirm valid input still reaches the same existing scan/write/address/settings code paths; rev24 must not change target byte encoding, endianness, alignment, scan semantics, or ps5debug-NG framing.

## Acceptance Criteria

Rev24 is verified when:

- the complete solution builds successfully on Windows;
- all 35 automated checks pass;
- impossible typed and pasted input is blocked in the covered fields;
- temporary edit states remain usable where required;
- final parsers still reject incomplete, out-of-range, or otherwise semantically invalid values before target operations;
- Saved Address Address accepts only the established 64-bit hexadecimal representation;
- Settings interval fields accept only digits while editing and still enforce `50-10000` on Save;
- plugin-defined/free-form fields remain unrestricted by host-specific assumptions;
- PS5 `0.1.0.rev16` and Mock `1.0.0.rev3` remain compatible under Plugin API `2.5.0`;
- rev20-rev23 Saved Address coordination and confirmation behavior remains intact;
- no regression is observed in scanning, Saved Addresses, theme switching, connection, or target I/O.
