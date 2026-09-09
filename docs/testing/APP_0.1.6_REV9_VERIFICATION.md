# TeeKay87's Memory Engine 0.1.6.rev9 Verification

## Build Identity

- Application: `0.1.6.rev9`
- Feature: `Manual Saved Address Entry`
- Plugin API: `2.11.0`
- Mock plugin: `1.0.0.rev6`
- PS5 plugin: `0.1.0.rev24`
- Expected automated verification count: **71**

## Scope

Rev9 adds direct manual creation of Saved Addresses and a Cheat Engine-informed dialog layout. Existing scan-created Saved Addresses, refresh/freeze/write behavior, export, Memory Viewer, Disassembler, scanner, target-I/O coordination, and plugin contracts must remain unchanged.

The toolbar order is now:

```text
Add Manually | Remove All | Export
```

## 1. Clean Windows Build

1. Delete stale `bin` and `obj` directories if present.
2. Restore/build the solution normally on Windows.
3. Confirm there are no C# or XAML compile errors.
4. Confirm the application starts and the permanent status bar shows `0.1.6.rev9`.
5. Confirm the native window title and compact application-title row remain title-only.

Expected: **PASS**.

## 2. Automated Verification

Run the verification executable.

Expected:

```text
71 checks passed
```

The two rev9 additions verify:

- Saved Addresses toolbar order/wiring/availability binding;
- functional manual-entry fields plus visibly disabled Cheat Engine-style placeholder controls.

No rev8 check may be removed or weakened.

## 3. Toolbar State

With no Active Target:

- **Add Manually** is disabled;
- **Remove All** follows the existing collection state;
- **Export** follows the existing export state.

Connect a plugin and set an Active Target with memory-read support.

Expected:

- **Add Manually** becomes available when no conflicting foreground/user operation is active;
- the button does not flash at the Saved Address background refresh cadence.

## 4. Fixed-Size Manual Address - Mock Target

1. Connect Mock Target and set **Mock Game** active.
2. Choose **Add Manually**.
3. Enter a known readable address, for example `0x10000104`.
4. Enter a description such as `Manual 4-byte test`.
5. Choose **4 Bytes**.
6. Confirm Length is fixed/read-only at `4`.
7. Choose **Add**.

Expected:

- one new Saved Address is created;
- Description is preserved;
- Address is shown in hexadecimal form;
- Type is `4 Bytes`;
- the current value is read from Mock memory immediately after creation;
- Protection is resolved through the existing memory map;
- the row participates in the existing periodic refresh path;
- no scan session is required.

## 5. Duplicate Manual Address

Repeat the same target + address + Value Type.

Expected:

- no duplicate row is created;
- the existing row is selected;
- Saved Address status explains that the address/type is already saved for that target.

## 6. Variable-Length Manual Address

1. Choose **Add Manually**.
2. Select **Array of Bytes**.
3. Confirm Length becomes editable and defaults to `10`.
4. Enter a readable address and Length `16`.
5. Add the row.

Expected:

- the row uses `ValueSize = 16`;
- current value displays 16 bytes after the immediate refresh;
- Memory Viewer opened from that Saved Address highlights all 16 bytes using the existing rev7/rev8 span behavior.

Validation cases:

- `0` is rejected;
- values above `4096` are rejected;
- non-numeric Length input is blocked by the reusable UnsignedInteger input filter.

## 7. Invalid / Unreadable Address

### Invalid syntax

Try empty or incomplete invalid hexadecimal input.

Expected: the dialog remains open and shows validation text.

### Valid but unreadable range

Enter syntactically valid hexadecimal memory that is not currently readable.

Expected:

- the Saved Address may still be created;
- the row reports the existing readable-range/refresh failure state;
- no fabricated current value is shown;
- later periodic refresh can recover if the range becomes readable.

## 8. Existing Saved Address Behavior

For a manually added readable row verify:

- Description editing;
- Address editing;
- Value Type changing;
- Value editing/write where target protection/capability permits;
- Freeze/Unfreeze;
- Remove;
- Remove All confirmation;
- Export;
- Open in Memory Viewer;
- Open in Disassembler when the plugin advertises Disassembly.

Expected: manual rows behave exactly like scan-created rows after creation.

## 9. Cheat Engine-Informed Placeholder Contract

Open **Add Manually** and confirm the dialog visibly contains disabled planned controls for:

- Hexadecimal display;
- Start bit;
- Unicode;
- Code page;
- Pointer;
- Base address;
- Offset;
- Add Offset;
- Remove Offset.

Expected:

- all are clearly identified as planned/disabled;
- changing functional fields cannot enable them accidentally;
- they do not affect the created Saved Address;
- Signedness is explained as part of the selected Value Type rather than represented by a separate nonfunctional checkbox.

## 10. Theme and Destructive-Control Semantics

Repeat the dialog in Light, Dimmed, and Dark themes.

Expected:

- all text/input/panel surfaces remain readable;
- disabled planned controls use the normal shared disabled appearance;
- **Cancel** uses the application's Danger/dismissive styling;
- **Add** uses the Primary action style;
- **Remove All** remains Danger-styled in the Saved Addresses toolbar.

## 11. PS5 Runtime Acceptance

With a live PS5 target:

1. add a known readable address manually as the matching numeric Value Type;
2. confirm current value and Protection resolve correctly;
3. open it in Memory Viewer;
4. if appropriate, edit/freeze/unfreeze using the already-verified paths;
5. add the same address/type again and confirm duplicate prevention.

Expected: no PS5-specific manual-entry code path exists; the generic Saved Address workflow uses the existing PS5 memory reader/writer and plugin Value Types.

## Acceptance Gate

Rev9 is accepted when:

- clean Windows build passes;
- all **71/71** automated checks pass;
- fixed and variable manual entries work on Mock Target;
- duplicate prevention works;
- placeholders remain disabled and visually clear;
- existing Saved Address operations work on a manually created row;
- live PS5 manual entry confirms the same generic path without regression.
