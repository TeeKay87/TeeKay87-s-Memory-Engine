# TeeKay87's Memory Engine 0.1.6.rev4 Verification

## Revision

```text
Application: 0.1.6.rev4
Feature:     Bidirectional Disassembly Context and Workspace Entry Points
Plugin API:  2.10.0
Mock plugin: 1.0.0.rev5
PS5 plugin:  0.1.0.rev23
```

Expected automated checks: 65

## Purpose

This checklist verifies the first around-origin Disassembler page model and the first direct Scan Results/Saved Addresses entry points into the Disassembler.

The verified `0.1.6.rev3` base passed all **64 automated checks** on Windows and was subsequently live-verified against a physical PS5 executable region. Rev4 keeps the verified Plugin API and both plugin decoders unchanged. Core adds an around-origin read path, the WPF Disassembler switches to that path, and the two existing address lists gain capability/target-safe workspace commands.

The default Disassembler view now requests up to **512 bytes before** the requested origin and **512 bytes starting at** the origin. Both sides are clamped to the same containing readable, non-guarded region. Pre-origin bytes are decoded separately as best-effort context so an uncertain variable-length boundary cannot consume or alter the exact requested origin/forward stream.

Rev4 intentionally does **not** yet add Back/Forward history, Memory Viewer-to-Disassembler navigation, Follow Target, disassembly copy/export, assembly, instruction editing, or debugger behavior.

## 1. Clean Windows Build

From a clean source tree:

```powershell
dotnet clean .\TeeKay87.MemoryEngine.sln
dotnet build .\TeeKay87.MemoryEngine.sln -c Debug
```

Expected:

- the complete solution builds without errors;
- the WPF application output contains both platform plugins;
- the PS5 plugin deployment still contains its private Iced dependency and `.deps.json`;
- no new runtime dependency is required by Core or WPF for rev4 context handling;
- Plugin API remains `2.10.0`, Mock plugin remains `1.0.0.rev5`, and PS5 plugin remains `0.1.0.rev23`.

## 2. Automated Verification Suite

Run the verification executable exactly as for rev3.

Expected result:

```text
All 65 checks passed.
```

The 64 previously verified checks must remain green. The new check, **Core disassembly bidirectional context**, verifies:

- the default 512-byte pre-origin and 512-byte origin/forward window;
- preservation of `RequestedAddress` independently from the earlier `StartAddress`;
- an exact-origin decode record even when pre-context is also decoded;
- no pre-context instruction record crossing into the exact-origin decode stream;
- independent clamping when the requested address is close to the containing region start.

## 3. Application Version and Title Presentation

Launch the application and confirm:

- the native Windows title bar shows `TeeKay87's Memory Engine` without version/revision;
- the compact top application row also shows only the application title;
- the permanent bottom status bar shows `0.1.6.rev4`;
- the revision feature title is not displayed as persistent application chrome.

## 4. Mock Bidirectional Context

1. Select **Mock Target**.
2. Connect and set the mock process as **Active Target**.
3. Open **Disassembler...**.
4. Enter:

```text
0x10000400
```

5. Choose **Go To**.

Expected when the full default context fits inside the region:

```text
Requested origin: 0x10000400
Visible start:     0x10000200
Visible end:       0x100005FF
Visible bytes:     1024
```

The exact-origin fixture must still begin at `0x10000400` with:

| Address | Bytes | Instruction |
| --- | --- | --- |
| `0x10000400` | `10 2A` | `load r0, 0x2A` |
| `0x10000402` | `20 04` | `call 0x10000408` |
| `0x10000404` | `11 01` | `add r0, 0x01` |
| `0x10000406` | `31 02` | `jump-if 0x1000040A` |
| `0x10000408` | `40` | `return` |
| `0x10000409` | `00` | `nop` |
| `0x1000040A` | `40` | `return` |

Additional rows before and after the fixture are expected because the page is now an around-origin view.

Also confirm:

- the requested origin is selected/scrolled into view after navigation;
- rows before the origin are available by scrolling upward;
- the origin marker remains theme-aware and layout-neutral;
- selecting another row does not remove the origin marker or change row dimensions.

## 5. Exact-Origin Decode Protection

Use an address that is not the start of the normal Mock fixture, such as:

```text
0x10000401
```

Expected:

- pre-origin rows may be best-effort context;
- the requested address itself begins a separate decode stream;
- no pre-origin record crosses over the requested origin;
- the workspace does not silently shift the user's requested origin to an earlier row;
- the warning explains that arbitrary pre-context on variable-length architectures cannot prove a canonical program boundary.

This rule protects the forward decode while still providing useful earlier context.

## 6. Region Boundary Clamping

Navigate to readable addresses close to both the start and end of a region.

Expected:

- unavailable pre-origin bytes are omitted at the region start rather than reading the previous mapping;
- unavailable origin/forward bytes are omitted at the region end rather than reading the next mapping;
- Visible range reports the actual bounded range;
- the returned range still contains the requested origin;
- no instruction record claims bytes outside the displayed snapshot.

## 7. Refresh Behavior

At a valid origin, choose **Refresh**.

Expected:

- the same origin remains in the Address field;
- the same around-origin context policy is applied again;
- preceding bytes are not lost on Refresh;
- Refresh does not create Back/Forward history in rev4;
- changed target bytes, if any, are reflected by the new read/decode while the origin remains unchanged.

## 8. Scan Results Context Menu

Produce at least one Scan Result and right-click a result row.

Expected menu labels include:

```text
Save Address
Open in Memory Viewer
Open in Disassembler
Copy address
Copy value
```

Confirm:

- **Browse Memory** is no longer shown as the current user-facing label;
- **Open in Memory Viewer** opens the unchanged Memory Viewer at the clicked result address;
- **Open in Disassembler** is enabled only when the current Active Target supports the complete neutral Disassembly workflow;
- **Open in Disassembler** opens the clicked result address as the Disassembler origin and the new pre-origin context is visible by scrolling upward;
- existing multi-selection **Save Address** behavior is unchanged.

## 9. Saved Addresses Context Menu

Right-click a Saved Address that belongs to the current Active Target.

Expected menu labels include:

```text
Freeze / Unfreeze
Open in Memory Viewer
Open in Disassembler
Copy address
Copy value
Remove address
```

Confirm:

- **Open in Memory Viewer** retains the Saved Address's captured target identity and existing Memory Viewer behavior;
- **Open in Disassembler** opens the Saved Address address as the Disassembler origin when that saved target still matches the current Active Target;
- the opened Disassembler uses the new bidirectional context policy.

Then retain a Saved Address while changing the Active Target to a different process, or otherwise make the saved process identity differ from the current Active Target.

Expected:

- **Open in Disassembler** is disabled for the mismatched Saved Address;
- clicking cannot silently reinterpret that saved address against the different process;
- existing Saved Address read/edit/freeze/remove behavior remains governed by its established target-safety rules.

## 10. Capability-Driven Availability

Verify the new Disassembler context-menu action does not depend on a platform name.

Expected:

- Mock and PS5 can expose **Open in Disassembler** because both advertise the neutral `Disassembly` capability and required memory services;
- a plugin without the required Disassembly capability cannot present the action as usable merely because it has readable memory;
- no `if PS5`, process-name, `eboot.bin`, Iced, or opcode-table rule exists in the WPF menu path.

## 11. Target / Connection Safety

Open a Disassembler from Scan Results or Saved Addresses, then invalidate its captured target identity by disconnecting/reconnecting or replacing the Active Target.

Use **Refresh** or **Go To** in the old window.

Expected:

- the old window does not silently follow the new process/connection generation;
- a clear stale/current-target error is reported;
- the last successful rows can remain visible for reference;
- no unrelated target read is issued.

## 12. Foreground I/O Coordination

Exercise Disassembler reads while other explicit target actions are available.

Expected:

- rev4 still uses the existing foreground target-I/O reservation;
- around-origin acquisition is one bounded memory read through the existing `IMemoryReader` path, followed by provider decoding of the pre-origin and exact-origin slices;
- no second PS5 transport or server-side disassembly command is created;
- conflicting target operations are not started concurrently merely because a Disassembler window is open.

## 13. Live PS5 Executable-Memory Verification

Use a current address inside a real `Read, Execute` region. A previously verified address may not remain stable between runs, so use an address valid for the current process/session.

1. Open the address in Memory Viewer.
2. Open the same address in Disassembler, either through **Disassembler...**, Scan Results, or Saved Addresses.
3. Compare the visible bytes before and at/after the requested origin.

Expected:

- the Disassembler Visible range extends backward by up to 512 bytes when the region permits it;
- the exact-origin/forward bytes still match Memory Viewer byte-for-byte;
- pre-origin rows correspond to the bytes in the same target range, while their canonical instruction-boundary status remains explicitly best-effort on x86-64;
- coherent x86-64 code remains visible in executable memory;
- Region / Module, Protection, and Architecture remain backend/target derived;
- direct branch/call targets already produced by the provider remain structured correctly, although Follow Target interaction is still deferred.

For a requested origin known to be a canonical instruction start, confirm the first origin/forward instruction matches the rev3 behavior exactly. For an origin known to lie inside another instruction, confirm rev4 does not pretend that the origin is canonical: it still starts the user's exact-origin stream and keeps the boundary warning visible.

## 14. Theme and Layout Verification

Repeat representative checks in:

- Light;
- Dimmed;
- Dark.

Expected:

- no new hardcoded theme color appears in the Disassembler or context menus;
- origin and invalid-instruction semantics continue using existing dynamic theme brushes;
- disabled Back/Forward controls use the normal disabled-state palette;
- both renamed/new context-menu commands render through the existing host ContextMenu/MenuItem templates;
- origin/selection presentation remains layout-neutral and does not introduce a horizontal scrollbar or row-size jump.

## 15. Regression Checks

Confirm the following existing workflows remain operational:

- plugin discovery/connect/disconnect;
- process refresh and Active Target selection;
- New Scan / First Scan / Next Scan / Cancel behavior;
- Scan Result save/copy behavior;
- Saved Addresses refresh/edit/freeze/remove/export;
- Memory Viewer read/edit/write/bookmarks/history/region navigation;
- Disassembler standalone button, Go To, Refresh, Region / Module, Protection, Architecture, and origin marker;
- universal export surfaces already implemented for Scan Results and Saved Addresses;
- settings and theme switching;
- status-bar version presentation and title-row rules.

No rev4 change is intended to redesign those verified workflows.

## Acceptance Criteria

`0.1.6.rev4` is accepted when all of the following are true:

1. The solution clean-builds on Windows.
2. All **65 automated checks** pass.
3. The Disassembler displays up to 512 bytes before and 512 bytes from the requested origin without crossing the containing readable region.
4. Pre-origin decoding cannot consume or alter the exact requested origin stream.
5. Go To and Refresh retain the around-origin behavior.
6. Scan Results shows **Open in Memory Viewer** and **Open in Disassembler**, with both actions opening the clicked result address correctly.
7. Saved Addresses shows the same two navigation labels and blocks Disassembler navigation when the saved target no longer matches the Active Target.
8. Disassembler availability remains capability-driven and platform-neutral in the host.
9. Stale target/connection generations cannot be followed silently.
10. Foreground target-I/O coordination remains intact.
11. Live PS5 executable-memory bytes match Memory Viewer at the same addresses and the exact-origin decode remains correct.
12. Light, Dimmed, and Dark render correctly without selection/origin layout changes.
13. Existing scanner, Saved Addresses, Memory Viewer, export, settings, theme, and status/title behaviors remain intact.
14. Version/revision remains absent from the native/top title presentation and appears only in the permanent status bar.

After rev4 is verified, the next Disassembler milestone can implement successful-address Back/Forward history and the remaining cross-workspace navigation, including Memory Viewer-to-Disassembler entry, before direct branch/call target following.
