# TeeKay87's Memory Engine 0.1.6.rev8 Verification

## Revision Under Test

```text
Application: 0.1.6.rev8
Feature: Memory Viewer Value-Span Binding Fix
Plugin API: 2.11.0
Mock plugin: 1.0.0.rev6
PS5 plugin: 0.1.0.rev24
```

## Purpose

Revision 8 is a corrective host/WPF revision for the value-span renderer introduced in `0.1.6.rev7`.

The rev7 Core range calculations and value-size propagation are retained. Windows runtime testing exposed a presentation-layer exception when Memory Viewer attempted to instantiate the new inline Hex Bytes/ASCII templates:

```text
System.InvalidOperationException
A TwoWay or OneWayToSource binding cannot work on the read-only property 'HexPrefix'
of type 'TeeKay87.MemoryEngine.App.ViewModels.MemoryViewerRowViewModel'.
```

The six inline row segments are intentionally read-only presentation values. Rev8 therefore makes every corresponding `Run.Text` binding explicitly `Mode=OneWay` instead of allowing WPF to choose a writable default binding mode.

No Plugin API, platform plugin, memory transport, Memory Viewer read/write model, span calculation, Disassembler, scanner, Saved Addresses, or export behavior changes in this revision.

## 1. Clean Windows Build

Build the complete solution from a clean state.

Expected:

- no compile errors;
- no XAML errors;
- no new warnings caused by rev8;
- the application launches normally;
- Plugin API remains `2.11.0`;
- Mock plugin remains `1.0.0.rev6`;
- PS5 plugin remains `0.1.0.rev24`.

## 2. Automated Verification

Run the verification executable.

Expected final line:

```text
All 69 checks passed.
```

Rev8 adds one automated check:

```text
Memory Viewer value-span bindings are OneWay
```

It verifies that the production `MemoryViewerWindow.xaml` copied into the verification output explicitly OneWay-binds all six read-only inline presentation properties:

```text
HexPrefix
HighlightedHexBytes
HexSuffix
AsciiPrefix
HighlightedAscii
AsciiSuffix
```

All 68 rev7 checks remain registered.

## 3. Version and Title Presentation

Expected:

- native Windows title: `TeeKay87's Memory Engine`;
- compact top application row: `TeeKay87's Memory Engine`;
- no version/revision in either title surface;
- permanent bottom status bar: `0.1.6.rev8`;
- no revision feature title rendered as permanent UI chrome.

## 4. Memory Viewer Opening Regression

Use either Scan Results or Saved Addresses and choose:

```text
Open in Memory Viewer
```

Expected:

- Memory Viewer opens without `InvalidOperationException`;
- no binding error references `HexPrefix`, `HighlightedHexBytes`, `HexSuffix`, `AsciiPrefix`, `HighlightedAscii`, or `AsciiSuffix`;
- the viewer reads the requested address normally;
- the existing Address / Hex Bytes / ASCII table renders immediately.

This is the primary acceptance gate for rev8.

## 5. Four-Byte Value Span

Repeat the previously useful PS5 case if available:

```text
Address: 0x42D43C
Type:    4 Bytes
```

Open the Scan Result in Memory Viewer.

Expected for the row beginning at `0x42D430`:

```text
00 00 00 48 8B 03 48 89 DF C7 40 28 [14 00 00 00]
```

The brackets describe the expected visual highlight only; literal brackets must not appear.

Expected:

- the complete four-byte span is highlighted in Hex Bytes;
- the corresponding four positions are highlighted in ASCII;
- the row containing the requested address keeps the independent green origin background;
- no adjacent byte is included;
- table geometry remains unchanged.

## 6. Cross-Row Span

Use a multi-byte value beginning at byte offset 14 or 15 of a 16-byte row.

Expected:

- the end of the first row is highlighted;
- the remaining bytes at the start of the next row are highlighted;
- only the first row is the navigation origin;
- no binding exception occurs while either row template is created.

## 7. Manual Navigation and History

From a viewer opened with a known multi-byte source span:

1. Go To another address manually.
2. Confirm one byte is highlighted.
3. Choose Back.
4. Choose Forward.
5. Use Refresh.

Expected:

```text
source entry -> multi-byte span
manual Go To -> one-byte span
Back -> source span restored
Forward -> one-byte span restored
Refresh -> current span preserved without new history entry
```

Bookmarks and region navigation remain one-byte highlight operations.

## 8. Clipboard and Editing Regression

Verify:

- Copy Hex Bytes returns the original plain row string;
- Copy ASCII returns the original plain ASCII string;
- Copy Row / Copy Selected / Ctrl+C remain unchanged;
- highlight rendering inserts no markup or brackets into copied data;
- Edit... / Edit Hex Bytes... still edits the complete selected displayed row under the existing stale-source and read-back verification rules;
- the OneWay presentation fix does not alter write payloads.

## 9. Theme and Layout

Test the active theme and optionally Light, Dimmed, and Dark.

Expected:

- highlighted bytes continue to use the existing Primary button background/text brushes;
- the green origin row remains visible;
- theme switching updates through DynamicResource;
- no font weight, border, padding, margin, row height, or column width changes are introduced by rev8;
- explicit OneWay binding direction has no visual effect compared with the intended rev7 design.

## 10. Memory Viewer -> Disassembler Regression

Right-click a Memory Viewer row and choose **Open in Disassembler**.

Expected:

- the same selected row address is passed to the Disassembler;
- stale viewer/session protection remains unchanged;
- the value-span binding correction causes no extra target I/O and no Disassembler behavior change.

## 11. Architectural Preservation

Confirm from source/build behavior:

- `MemoryViewHighlightSpan` and `MemoryViewRowHighlight` are unchanged from rev7;
- source-size propagation from Scan Results/Saved Addresses is unchanged;
- Plugin SDK/Core disassembly contracts are unchanged;
- PS5 and Mock plugin sources are unchanged;
- the corrective runtime change is limited to explicit OneWay binding metadata in the Memory Viewer inline renderer;
- the verification project only copies the production XAML as a fixture and does not introduce a dependency on the WPF application assembly.

## 12. Acceptance Criteria

Rev8 can be considered verified when:

- clean Windows build succeeds;
- all **69/69** automated checks pass;
- Memory Viewer opens from Scan Results and Saved Addresses without the read-only binding exception;
- known multi-byte values highlight the complete expected Hex/ASCII span;
- cross-row highlighting still works;
- manual navigation/history behavior remains correct;
- clipboard/edit behavior remains unchanged;
- Memory Viewer -> Disassembler remains functional;
- title/version presentation remains correct.

After rev8 is verified, `0.1.6` can return to the planned Disassembler milestones, with direct branch/call target navigation next unless runtime testing identifies another prerequisite.
