# TeeKay87's Memory Engine 0.1.6.rev7 Verification

## Revision Under Test

```text
Application: 0.1.6.rev7
Feature: Memory Viewer Value-Span Highlighting
Plugin API: 2.11.0
Mock plugin: 1.0.0.rev6
PS5 plugin: 0.1.0.rev24
```

## Purpose

Revision 7 improves Memory Viewer address readability without changing the established row/table format or memory safety model. A Memory Viewer opened from Scan Results or Saved Addresses now carries the exact byte size of the source value and visually marks the complete known value span inside the existing Hex Bytes and ASCII columns.

Manual Go To, bookmarks, and region navigation have no external value-size context and therefore mark one exact byte. Memory Viewer Back/Forward history records both the requested address and the highlight byte count so returning to a source-derived entry restores its original complete value span.

No Plugin API, platform plugin, memory transport, page-protection, editing, scanner, Saved Addresses, Disassembler decode, or export contract changes are introduced.

## 1. Clean Windows Build

Build the complete solution from a clean state.

Expected:

- no compile errors;
- no XAML errors;
- no new warnings caused by rev7;
- the application launches normally;
- Plugin API remains `2.11.0`;
- Mock plugin remains `1.0.0.rev6`;
- PS5 plugin remains `0.1.0.rev24`.

## 2. Automated Verification

Run the verification executable.

Expected final line:

```text
All 68 checks passed.
```

Rev7 adds one automated check:

```text
Memory Viewer value-span highlighting
```

It verifies:

- a four-byte span located wholly inside one 16-byte row;
- a four-byte span crossing a 16-byte row boundary;
- no highlight for a non-overlapping row;
- high-address calculations do not overflow near `ulong.MaxValue`;
- zero-length spans are rejected.

All 67 rev6 checks must continue to pass unchanged.

## 3. Version and Title Presentation

Expected:

- native Windows title: `TeeKay87's Memory Engine`;
- compact top application row: `TeeKay87's Memory Engine`;
- no version/revision in either title surface;
- permanent bottom status bar: `0.1.6.rev7`;
- no revision feature title rendered as permanent UI chrome.

## 4. Scan Result Four-Byte Span

Use a Scan Result with a known four-byte value, preferably the previously tested PS5 executable address:

```text
0x42D43C
Type: 4 Bytes
```

Choose:

```text
Open in Memory Viewer
```

Expected:

- the Memory Viewer origin remains the exact address `0x42D43C`;
- the row containing that address retains the existing green origin-row background;
- the four bytes beginning at `0x42D43C` are highlighted inside **Hex Bytes**;
- the corresponding four character positions are highlighted inside **ASCII**;
- no adjacent byte is included;
- Address / Hex Bytes / ASCII column widths are unchanged;
- row height is unchanged;
- selection remains independent from the origin/value highlight.

For the previously captured row beginning at `0x42D430`, the expected highlighted Hex Bytes are:

```text
00 00 00 48 8B 03 48 89 DF C7 40 28 [14 00 00 00]
```

The brackets above describe the expected visual span; literal brackets must not be added to the displayed text.

## 5. Saved Address Span

Save or use a Saved Address with a known multi-byte Value Type.

1. Confirm its current Value Type/size.
2. Choose **Open in Memory Viewer**.

Expected:

- the highlight byte count comes from the Saved Address's current `ValueSize`;
- 1-byte types mark one byte;
- 2-byte types mark two bytes;
- 4-byte/Float values mark four bytes;
- 8-byte/Double values mark eight bytes;
- Array of Bytes uses the current array length;
- changing the Saved Address Value Type before opening changes the span size accordingly once the Saved Address has that size.

The saved target-process identity and stale-session safety rules remain unchanged.

## 6. Cross-Row Span

Use a known four-byte value that begins at byte offset 14 or 15 of a 16-byte Memory Viewer row, or use an equivalent Mock/runtime fixture.

Expected for a four-byte span beginning at row offset 14:

```text
row N:     bytes 14-15 highlighted
row N + 1: bytes 0-1 highlighted
```

Expected:

- the highlight is visually continuous across the logical value even though it is rendered in two DataGrid rows;
- the origin row remains the row containing the first requested byte;
- the next row is not incorrectly treated as a second navigation origin;
- normal row selection/copy/edit behavior remains unchanged.

## 7. Manual Go To

From a viewer that was opened with a multi-byte source span:

1. enter another address manually;
2. choose **Go To**.

Expected:

- the new address marks exactly one byte because no external value-size context exists;
- the Hex Bytes and ASCII positions match the exact requested byte;
- the green origin row still identifies the row containing that requested byte.

Bookmarks and Previous/Next/Region Start/Region End navigation should likewise use a one-byte highlight.

## 8. Back / Forward Restores Span State

Start from a Memory Viewer opened from a four-byte Scan Result or Saved Address.

1. Confirm four bytes are highlighted at address A.
2. manually Go To address B; confirm one byte is highlighted.
3. choose **Back**.
4. choose **Forward**.

Expected:

```text
A (4-byte span) -> B (1 byte) -> Back=A (4-byte span) -> Forward=B (1 byte)
```

Refresh must preserve the current entry's span size without creating another history entry.

A new navigation after Back still removes the abandoned Forward branch exactly as before.

## 9. Large Array-of-Bytes Visibility

If practical, test an Array of Bytes longer than 256 bytes.

Expected:

- the viewer requests a larger bounded window so the complete forward value span can remain visible where the containing region permits;
- the request remains capped by `MemoryViewerReader.MaximumWindowByteCount` (`65,536` bytes);
- the viewer never reads through the containing region boundary;
- if the complete source span cannot be visible because of the memory-region/address-space boundary or maximum-window limit, the status text states how many highlighted source bytes are visible rather than claiming that the complete span is shown.

Normal numeric values continue to use the established 512-byte window.

## 10. Theme and Layout

Test at least the active theme and preferably Light, Dimmed, and Dark.

Expected:

- highlighted byte text uses the existing paired Primary button background/text theme brushes;
- no hard-coded byte-highlight color exists in Memory Viewer XAML;
- the highlight remains readable over the existing green origin row and ordinary selected rows;
- theme switching updates the highlight through `DynamicResource`;
- no bold font, border thickness, margin, padding, or font-size change is used to indicate the byte span;
- row/column geometry remains identical when the highlighted span changes from one byte to several bytes or crosses a row boundary.

## 11. Clipboard and Editing Regression

Verify:

- **Copy Hex Bytes** still copies the original plain full row text, without formatting markup or inserted delimiters;
- **Copy ASCII** still copies the original plain ASCII string;
- **Copy Row**, **Copy Selected**, and `Ctrl+C` remain unchanged;
- the visual highlight does not change `MemoryViewerRowViewModel.Bytes` or `HexBytes`;
- **Edit...** / **Edit Hex Bytes...** still edits the complete selected row under the existing stale-source/read-back verification rules;
- value highlighting does not imply that only the highlighted bytes will be written by the existing row editor.

## 12. Memory Viewer -> Disassembler Regression

Right-click a Memory Viewer row and choose **Open in Disassembler**.

Expected:

- the selected row address is still passed to the Disassembler exactly as in rev6;
- the new byte-span presentation causes no additional target read for the Disassembler route;
- stale viewer/session rejection remains unchanged.

## 13. Target I/O Preservation

Expected architectural behavior:

- highlight-range calculation is host/Core presentation metadata only;
- the normal read path still uses `ReadMemoryViewerWindowAsync(...)` and the existing foreground target reservation;
- no platform-specific command or PS5 branch is added;
- normal 1/2/4/8-byte source values still use the established 512-byte bounded page;
- a larger source span may increase only the requested bounded page size needed to keep that span visible, still capped at the existing Core maximum and one containing region;
- the visual renderer itself causes no additional target I/O.

## 14. Acceptance Criteria

Rev7 can be considered verified when:

- clean Windows build succeeds;
- all **68/68** automated checks pass;
- Scan Result and Saved Address source sizes produce the correct complete visible byte span;
- cross-row spans highlight both row portions correctly;
- manual/bookmark/region navigation marks one byte;
- Back/Forward restores both address and span length;
- Hex and ASCII highlights match each other;
- highlight presentation is theme-aware and layout-neutral;
- clipboard text remains unchanged;
- Memory Viewer write safety and Disassembler routing remain unchanged;
- title/version presentation remains correct.

Direct Disassembler branch/call **Follow Target**, Disassembler copy/multi-selection/export, assembler/patching, and debugger integration remain later `0.1.6` milestones.
