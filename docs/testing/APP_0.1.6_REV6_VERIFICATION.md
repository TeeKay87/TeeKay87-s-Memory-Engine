# TeeKay87's Memory Engine 0.1.6.rev6 Verification

## Revision

```text
Application: 0.1.6.rev6
Feature: Disassembly Syntax Highlighting and Navigation History
Plugin API: 2.11.0
Mock plugin: 1.0.0.rev6
PS5 plugin: 0.1.0.rev24
PS5 decoder: Iced 1.21.0
```

## Purpose

This revision builds on the fully verified `0.1.6.rev5` continuous around-origin decode path. It does not change Core memory acquisition, continuous instruction decoding, origin resolution, or ps5debug-NG transport.

Rev6 adds three user-facing capabilities:

1. architecture-neutral syntax highlighting inside the existing **Instruction** column;
2. successful-address **Back** / **Forward** Disassembler history;
3. **Open in Disassembler** from the Memory Viewer row context menu.

Plugin API `2.11.0` adds optional `DisassemblyTextToken` metadata. A provider may classify already-formatted text as Mnemonic, FlowControlMnemonic, Register, Number, Keyword, or Text. Providers that omit the metadata remain valid and render the exact existing plain instruction string.

The verified `0.1.6.rev5` base passed all **66 automated checks** and its continuous origin-resolution behavior was live-verified on PS5 before rev6 was created.

## 1. Clean Windows Build

From a clean extraction of this revision, remove any old `bin` and `obj` folders and build the solution normally on Windows.

Expected:

- the solution builds without errors or warnings;
- the WPF application reports `0.1.6.rev6`;
- Plugin API reports `2.11.0`;
- Mock plugin reports `1.0.0.rev6` and targets API `2.11.0`;
- PS5 plugin reports `0.1.0.rev24` and targets API `2.11.0`;
- the PS5 plugin output still deploys its existing `Iced.dll` and dependency metadata through the shared plugin-deployment target;
- no new platform-specific runtime dependency exists outside the PS5 plugin.

## 2. Automated Verification Suite

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 67 checks passed.
```

All 66 rev5 checks must remain green.

The new dedicated check is:

```text
Disassembly syntax token model
```

It verifies:

- token collections are copied into the instruction model rather than retaining a mutable caller list;
- neutral token kinds are preserved;
- concatenating token text reconstructs the existing instruction text exactly;
- the original `DisassembledInstruction` constructor remains usable and produces an empty syntax-token collection;
- empty token text is rejected.

Existing Mock and PS5 disassembly checks are extended to verify provider-generated token categories and exact text reconstruction.

## 3. Application Version and Title Presentation

Launch the application.

Expected:

- native Windows title: `TeeKay87's Memory Engine`;
- compact top application row: `TeeKay87's Memory Engine`;
- no version/revision in either title surface;
- permanent bottom status bar: `0.1.6.rev6` at the right edge;
- the revision feature title is not rendered as permanent UI chrome.

## 4. Disassembler Layout Preservation

Open the Disassembler on Mock Target or PS5.

Expected table format remains exactly:

```text
Address | Bytes | Instruction
```

Expected:

- Address column width is unchanged;
- Bytes column width is unchanged;
- Instruction remains the final fill-width column;
- row height does not change because syntax highlighting is active;
- selecting or unselecting a row does not change row width/height;
- the green origin row remains layout-neutral;
- syntax highlighting does not add a second instruction column, rich-text editor, wrapping surface, or extra row chrome;
- horizontal/vertical scrolling behavior is not degraded compared with rev5.

The implementation may use multiple text runs internally, but only foreground color should visibly change.

## 5. Mock Syntax Highlighting

Use the In-Memory Test Target.

1. Connect.
2. Select `TestGame.exe`.
3. Set it as Active Target.
4. Open **Disassembler...**.
5. Go To:

```text
0x10000400
```

Expected deterministic instructions include:

```text
0x10000400  10 2A  load r0, 0x2A
0x10000402  20 04  call 0x10000408
0x10000404  11 01  add r0, 0x01
0x10000406  31 02  jump-if 0x1000040A
0x10000408  40     return
0x10000409  00     nop
0x1000040A  40     return
```

Expected presentation:

- ordinary mnemonics use the mnemonic syntax color;
- `call`, `jump-if`, and `return` use the flow-control mnemonic syntax color;
- `r0` uses the register syntax color;
- immediate values and target addresses use the number syntax color;
- spaces, punctuation, and unclassified text remain normal text;
- the exact characters shown in each instruction are unchanged from the provider's plain text.

## 6. Bundled Theme Visual Test

With the Disassembler still open, switch through:

```text
Light
Dimmed
Dark
```

Expected in every theme:

- syntax colors are readable against normal and selected/origin row backgrounds;
- mnemonic, flow-control, register, number, and keyword categories are visually distinguishable where present;
- the existing window/card/table/status colors still follow the selected theme;
- the open Disassembler updates without closing/reopening it;
- no fixed light-theme or dark-theme color leaks into another theme;
- invalid instruction text still uses the normal application error color;
- syntax highlighting does not override the origin-row background or normal selection behavior.

## 7. Existing Custom Theme Compatibility

If an existing custom theme created before rev6 is available, use one that does **not** contain the five new optional disassembly keys.

Expected:

- the theme still loads successfully;
- it is not rejected as incomplete solely because the new syntax keys are absent;
- syntax colors are derived from the documented required-palette fallbacks;
- switching to and from the custom theme updates an already-open Disassembler.

The optional fallback mapping is:

| Optional key | Fallback |
| --- | --- |
| `DisassemblyMnemonic` | `Accent` |
| `DisassemblyFlowControl` | `WarningText` |
| `DisassemblyRegister` | `SuccessText` |
| `DisassemblyNumber` | `WarningText` |
| `DisassemblyKeyword` | `SecondaryText` |

## 8. Back / Forward History

Use a target with at least three readable addresses, referred to below as A, B, and C.

1. Open Disassembler at initial address A.
2. Go To B.
3. Go To C.
4. Choose **Back**.
5. Choose **Back** again.
6. Choose **Forward**.

Expected:

```text
A -> B -> C -> Back=B -> Back=A -> Forward=B
```

Also verify:

- `Alt+Left` performs Back when available;
- `Alt+Right` performs Forward when available;
- Back is disabled at the oldest history entry;
- Forward is disabled at the newest history entry;
- the Address field and origin marker follow the navigated history address;
- every history navigation rereads current target bytes through the normal foreground target path rather than restoring a stale cached page.

## 9. History Branching

Continue from a state where Forward history exists.

1. Navigate Back so a Forward destination is available.
2. Enter a different address D and use **Go To**.

Expected:

- D becomes the current/most recent history entry;
- the abandoned Forward branch is removed;
- Forward becomes disabled until new forward history is created by Back navigation.

## 10. Refresh and Failed-Navigation History Rules

At a valid current address:

1. choose **Refresh** one or more times;
2. then use Back/Forward.

Expected:

- Refresh rereads/redecodes the current context;
- Refresh does not create duplicate history entries.

Then attempt an invalid/unreadable/out-of-region Go To.

Expected:

- the normal error is shown;
- the failed address is not committed as a history entry;
- Back/Forward position remains the previous successful state.

## 11. Continuous Origin Resolution Regression

Use Mock Target and Go To:

```text
0x10000401
```

Expected:

- the complete two-byte `load` instruction still begins at `0x10000400`;
- the `0x10000400` instruction is the origin row because it contains `0x10000401`;
- no fabricated instruction begins at the interior origin byte;
- syntax highlighting does not change this rev5 behavior;
- Back/Forward records the exact requested address `0x10000401`, not the containing instruction's start address.

## 12. Memory Viewer -> Open in Disassembler

With an Active Target that supports Disassembly:

1. Open Memory Viewer at a known address.
2. Right-click a specific Memory Viewer row.
3. Confirm the first context action is **Open in Disassembler**.
4. Choose it.

Expected:

- a Disassembler window opens for the same plugin/process;
- the clicked row's address is used as the requested origin;
- the normal bounded continuous around-origin context is displayed;
- Memory Viewer remains open and its existing selection, copy, edit, bookmark, history, and region-navigation behavior is unchanged.

## 13. Memory Viewer Stale-Session Safety

Open a Memory Viewer, then make its captured session stale by disconnecting/reconnecting or otherwise changing the active connection generation/target process.

Expected:

- **Open in Disassembler** is disabled when the viewer no longer represents the current compatible Active Target;
- invoking the stale route does not open an old address against the new session;
- existing Memory Viewer stale-read/write protections remain unchanged.

Reconnect/open a fresh viewer against the valid Active Target and verify the action becomes available again.

## 14. Scan Results and Saved Addresses Regression

Verify the existing context menus still use:

```text
Open in Memory Viewer
Open in Disassembler
```

Expected:

- `Browse Memory` does not return;
- Scan Results **Open in Disassembler** still opens the clicked result address;
- Saved Addresses **Open in Disassembler** remains enabled only when the row belongs to the current compatible Active Target;
- rev6 syntax/history changes do not alter Save Address, Freeze, Remove, Copy, or export behavior.

## 15. Live PS5 Syntax Preservation

Use a real PS5 with `eboot.bin` selected as Active Target and choose a known `Read, Execute` address previously verified in rev3-rev5.

Expected:

- Memory Viewer and Disassembler bytes still match byte-for-byte;
- x86-64 instruction addresses, lengths, mnemonics, operands, direct targets, and invalid records remain unchanged from rev5;
- syntax colors improve readability but do not modify the textual instruction representation;
- registers are visually classified as registers;
- immediates/addresses are visually classified as numbers;
- call/jump/conditional/return mnemonics use the flow-control presentation;
- indirect calls/jumps still do not fabricate targets.

When practical, repeat the previously verified interior-origin case:

```text
Requested origin: 0x42D43C
Containing instruction start: 0x42D439
Bytes: C7 40 28 14 00 00 00
```

Expected: the full instruction remains intact and is the green origin row exactly as in rev5.

## 16. Target I/O Preservation

During Disassembler Go To, Refresh, Back, Forward, and Memory Viewer -> Open in Disassembler, confirm normal target behavior remains responsive.

Expected architectural behavior:

- syntax highlighting consumes provider-returned presentation metadata only;
- it does not trigger extra memory reads;
- Back/Forward performs the same single bounded around-origin read used by a normal successful Go To;
- PS5 decoding still occurs locally in the PS5 plugin from caller-supplied bytes;
- no `CMD_PROC_DISASM_REGION` or other new ps5debug-NG command is introduced;
- foreground target-operation coordination remains in force.

## 17. Acceptance Criteria

Rev6 can be considered verified when all of the following are true:

- clean Windows build succeeds;
- all **67/67** automated checks pass;
- syntax highlighting works in Light, Dimmed, and Dark;
- syntax highlighting changes color only, not list format or row geometry;
- an older custom theme without syntax keys still loads through fallbacks;
- Back/Forward and keyboard shortcuts obey successful-address history semantics;
- Refresh and failed reads remain history-neutral;
- a new Go To after Back discards Forward history;
- Memory Viewer **Open in Disassembler** uses the clicked row and rejects stale target/session state;
- Scan Results/Saved Addresses entry points remain correct;
- rev5 continuous origin resolution remains correct;
- live PS5 disassembly text/bytes/targets remain unchanged apart from syntax color;
- version/title presentation remains correct.

Direct branch/call **Follow Target**, Disassembler copy/multi-selection/export, assembler/patching, and debugger integration remain later `0.1.6` milestones.
