# Text Input Validation

## Purpose

TeeKay87's Memory Engine uses layered validation for editable text fields whose syntax is known by the host or by a plugin-owned Value Type.

The goal is to prevent obviously impossible text from being entered or pasted while preserving the final parser as the authoritative source for range, width, target architecture, and operation-specific validity.

Live filtering is therefore a usability layer, not a replacement for validation at the point where a scan, memory write, address change, or settings save is committed.

## Reusable WPF Filter

The application owns a reusable attached behavior under:

```text
src/TeeKay87.MemoryEngine.App/Input/TextBoxInputFilter.cs
```

The behavior evaluates the complete candidate text after the current selection would be replaced. The same check is used for ordinary text composition and clipboard paste.

Current host-owned modes are:

- `UnsignedInteger` — decimal digits only, with an empty edit state allowed;
- `HexAddress` — an optional `0x` prefix followed by hexadecimal digits, with at most 16 hexadecimal digits for the current neutral `ulong` address representation.

Deletion/backspace remains unrestricted so the user can freely restructure a value. Any incomplete or otherwise invalid text that remains when an operation is committed is still rejected by the existing final parser.

## Plugin-Owned Value Input Policy

Plugin API `2.5.0` adds the optional `IMemoryValueInputPolicy` contract:

```csharp
bool IsPotentiallyValidInput(string text);
```

An `IMemoryValueType` may also implement this interface when it can describe safe live-editing syntax.

The method is intentionally about **potential** validity. It should accept temporary edit states that are not yet complete but can become valid through additional typing. Examples include:

- `-` for a signed integer;
- `0x` before hexadecimal digits are entered;
- `1.` while entering a floating-point value;
- `1e-` while entering a scientific-notation exponent.

Final semantic validity must remain in `IMemoryValueType.TryParse(...)`.

A custom plugin Value Type is not required to implement `IMemoryValueInputPolicy`. When the policy is absent, the host does not guess or hardcode the plugin's syntax; the field remains freely editable and `TryParse(...)` performs the definitive validation when the value is used.

## Standard Value Type Policies

The reusable standard Value Types implement the optional policy.

### Signed integers

Accepted live syntax includes:

- decimal digits with an optional leading `+` or `-`;
- `0x`/`0X` followed by hexadecimal digits;
- temporary empty/sign/prefix states needed while editing.

Range validation remains in `TryParse(...)`. For example, the live filter may allow a decimal digit sequence that is syntactically numeric but outside Int32 range; First Scan/Next Scan or Saved Address write then rejects it with the existing Value Type error.

### Unsigned integers

Accepted live syntax includes:

- decimal digits with an optional leading `+`;
- `0x`/`0X` followed by hexadecimal digits;
- temporary empty/positive-sign/prefix states.

A leading minus is blocked by the live policy.

### Float and Double

Accepted live syntax includes invariant decimal/scientific notation with the sign, decimal point, and exponent placed where they can still form a valid value. Incomplete states used during normal editing are allowed.

The invariant `NaN`/`Infinity` token family is not blocked by the live policy because it is part of the underlying .NET floating-point textual vocabulary. Whether a completed token is accepted for the selected operation remains controlled by the final Value Type parser and comparison behavior.

### Array of Bytes

The live policy permits:

- hexadecimal digits;
- whitespace;
- comma and hyphen separators already supported by the parser;
- `0x`/`0X` token prefixes in positions where a prefixed byte token can begin.

The final parser still enforces complete bytes, compact-string parity, individual byte width, and the maximum Array-of-Bytes length.

## Current Consumers

### Scan Value

The Scan panel binds the live filter to the currently selected plugin Value Type. Changing Value Type changes the live syntax policy with it.

The scan-start path still calls the selected `IMemoryValueType.TryParse(...)` before any scan begins. Invalid or out-of-range values therefore cannot reach Core/native scanning even if they passed the live syntax filter.

### Saved Address Value

Each Saved Address Value editor binds to that row's selected plugin Value Type. The final write path still parses the completed text before writable-region checks or `IMemoryWriter`/`IConcurrentMemoryWriter` calls.

### Saved Address Address

The Address editor uses the host `HexAddress` mode. It allows the existing optional `0x` prefix and at most 16 hexadecimal digits. The existing `ulong` hexadecimal parser remains the authoritative commit check and restores the previous address if the edit is incomplete or invalid.

### Manual Saved Address Entry

Application `0.1.6.rev9` reuses the same host-owned filters in the **Add Saved Address Manually** dialog. Corrective `0.1.6.rev10` keeps the same filters and parsing rules, but initializes the parser `out` address before its short-circuit length guard so all invalid-input paths satisfy C# definite-assignment rules. **Address** uses `HexAddress`; **Length** uses `UnsignedInteger`. Dialog commit then performs its own authoritative parsing: Address must fit the current neutral `ulong` address representation, fixed-size plugin Value Types ignore arbitrary length edits and use their declared width, and variable-size Value Types currently require 1-4,096 bytes. Description remains free-form.

### Settings Intervals

Saved Addresses Value refresh and Frozen write interval fields use the `UnsignedInteger` mode. The Settings Save path still enforces the existing 50-10,000 ms range.

## Fields Deliberately Not Live-Filtered

The following fields remain unrestricted at the host presentation layer:

- Saved Address Description;
- Scan Results storage path;
- plugin-defined connection setting fields.

Connection-field syntax belongs to the plugin. The host does not assume that a field called Port, Host, Token, Device, or any future plugin-defined setting has a universal character grammar. The plugin's existing connection validation remains authoritative unless a separate plugin-declared connection-input contract is introduced later.

## Extension Rules

When adding another textbox:

1. determine which layer owns the field's grammar;
2. use a host mode only for genuinely application-owned syntax;
3. use `IMemoryValueInputPolicy` for plugin-owned Value Type syntax;
4. do not infer syntax from display names or platform ids;
5. do not remove final parsing/range validation because a live filter exists;
6. make paste follow the same rule as keyboard text input;
7. allow intermediate edit states when blocking them would make ordinary editing awkward;
8. keep free-form fields free-form unless their owning contract explicitly defines a grammar.


## Rev25 Space-key correction

WPF does not reliably route the Space key through `PreviewTextInput` for every TextBox path. Rev24 therefore allowed a literal space to enter otherwise filtered numeric Value and hexadecimal Address editors even though their candidate validators rejected whitespace and paste already used the correct policy. Rev25 additionally hooks `PreviewKeyDown` for `Key.Space` and validates the complete candidate before allowing the key.

This is policy-aware rather than a blanket space ban: numeric Value Types and `HexAddress` reject the candidate, while Array of Bytes accepts it because whitespace is a supported separator. Final `TryParse`/address parsing remains the authoritative safety boundary.
