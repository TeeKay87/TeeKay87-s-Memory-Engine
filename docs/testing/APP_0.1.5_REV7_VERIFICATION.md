# TeeKay87's Memory Engine 0.1.5.rev7 Verification

## Revision Under Test

```text
Host application:             0.1.5.rev7
Feature:                      Memory Viewer Bookmarks and Region Navigation
Plugin API:                   2.9.0
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Expected automated checks:    54
```

## Purpose

Rev7 extends the already verified Memory Viewer with viewer-local exact-address bookmarks and explicit memory-region navigation. It also fixes the empty tooltip popup observed over unused Saved Addresses Frozen/Protection/Remove cell space when the row has no status text.

The revision must preserve the fully verified rev6 safe-write contract. Bookmarks and region navigation must use the existing neutral memory-map/read path and must not introduce platform-specific commands, page-protection changes, or hidden persistence.

The existing `tools/preflight/` files remain present and unchanged; further development of that helper remains paused. The Windows/Roslyn/WPF build is the authoritative compile gate.

## 1. Clean Windows Build

1. Delete stale `bin`/`obj` directories if necessary.
2. Open `TeeKay87.MemoryEngine.sln` in Visual Studio.
3. Choose **Rebuild Solution**.
4. Confirm there are no compiler/WPF markup errors and no warnings.

## 2. Automated Verification Runner

Run `TeeKay87.MemoryEngine.Tests`.

Expected ending:

```text
All 54 checks passed.
```

Rev7 adds one check:

- **Memory Viewer readable region navigation** — verifies Previous/Next region lookup on an unsorted neutral memory map, confirms guarded and unreadable regions are skipped, and confirms no previous/next result is returned outside the readable-region sequence.

All previous 53 checks must continue to pass.

## 3. Bookmark Basics on Mock Target

1. Connect to **In-Memory Test Target** and make Mock Game the Active Target.
2. Open Memory Viewer at Ammo `0x10000104`.
3. Choose **Add Current**.
4. Confirm the bookmark selector now contains the exact address `0x10000104` rather than the row base `0x10000100`.
5. Confirm **Add Current** becomes disabled while the current origin is already bookmarked.
6. Navigate to another address, for example `0x10000180`.
7. Add that address as a second bookmark.
8. Select the first bookmark and choose **Go**.
9. Confirm Memory Viewer returns to `0x10000104`, the origin row is green, and Back becomes available because bookmark navigation participates in history.
10. Remove the selected bookmark and confirm only that bookmark disappears.

Bookmarks are accepted only if adding/removing them causes no target write and no navigation until **Go** is explicitly used.

## 4. Bookmark Lifetime and Refresh/Write Regression

1. Add at least two bookmarks.
2. Use **Refresh** several times and confirm bookmarks remain.
3. Perform a verified edit on a writable row and confirm bookmarks remain after the automatic refresh.
4. Navigate Back/Forward and confirm bookmarks remain.
5. Close the Memory Viewer window and open a new viewer.
6. Confirm the new viewer starts with an empty bookmark list.

Rev7 bookmarks are intentionally window-local and must not persist through general application settings.

## 5. Region Navigation on Mock Target / Synthetic-Capable Target

For a target with neighboring readable regions, verify:

- **Previous Region** navigates to the base of the previous readable non-guarded region;
- **Region Start** navigates to the exact base of the current region;
- **Region End** navigates to the final byte of the current region and the 512-byte page clamps to the region end;
- **Next Region** navigates to the base of the next readable non-guarded region;
- Previous/Next are disabled when there is no eligible region in that direction;
- read-only and executable regions remain valid inspection destinations when they have `Read` and are not `Guard`;
- write-only/unreadable and Guard regions are skipped.

The one-region bounded reader rule must remain intact; region navigation must not concatenate bytes across map boundaries.

## 6. Region Navigation History Integration

1. Start at a known address in one region.
2. Use **Next Region**.
3. Use **Region End**.
4. Confirm **Back** walks through those successful destinations in reverse order.
5. Confirm **Forward** restores them.
6. Use Back once, then navigate with **Previous Region** or **Go** to a bookmark.
7. Confirm the obsolete Forward branch is discarded, matching existing Go To behavior.
8. Confirm ordinary row selection and **Refresh** still do not create history entries.

## 7. Live PS5 Region Navigation

Using an Active Target with a loaded PS5 memory map:

1. Open Memory Viewer from a known Scan Result or Saved Address.
2. Record the displayed current region base/end and Protection.
3. Use **Region Start** and confirm the origin address becomes the displayed base.
4. Use **Region End** and confirm the origin address becomes the final byte of that same region.
5. Use **Previous Region** and **Next Region** where available.
6. Confirm each destination reports a coherent Region / Module, range, and Protection from the current memory map.
7. Confirm navigating into a read-only region changes the viewer header to **Read-only** and disables Edit.
8. Confirm returning to a writable region restores normal rev6 edit eligibility.

No region-navigation action should change target memory or page protection.

## 8. Saved Addresses Empty Tooltip Fix

Use at least one Saved Address whose `StatusText` is empty.

Hover the unused cell area in:

- **Frozen** outside the checkbox;
- **Protection**;
- the Remove column outside the button.

Confirm **no empty tooltip popup appears**.

Then hover:

- directly over the Frozen checkbox;
- directly over the Remove button.

Confirm their existing meaningful tooltips still appear.

If a Saved Address later has non-empty row status text, confirm the row-level status tooltip may still appear over row/cell background as before.

Repeat in Light, Dimmed, and Dark to ensure the fix does not introduce theme-specific popup behavior.

## 9. Rev6 Safe-Write Regression

Confirm the fully verified rev6 boundary remains unchanged:

- writable region reports **Read/write** and enables Edit;
- read-only region reports **Read-only** and disables Edit;
- a normal write is read back and verified;
- stale displayed bytes block the write and refresh the view;
- successful/stale-write refresh does not remove bookmarks or create a history entry by itself;
- Saved Addresses continues to observe a changed underlying value normally.

A full destructive/live write re-test is not required for every rev7 build if the 53 prior automated checks pass, but at least one controlled Mock-target edit should be run after the new navigation UI is accepted.

## 10. Selection / Copy / Origin Regression

Confirm:

- the green origin row remains independent from normal selection;
- selecting the green origin row changes no row geometry and creates no horizontal scrollbar;
- multi-selection remains available;
- Copy Address / Hex Bytes / ASCII / Row / Selected continue to work;
- `Ctrl+C`, `Alt+Left`, and `Alt+Right` retain their rev4 behavior;
- bookmark/region controls do not change selection semantics.

## 11. Existing Application Regression Boundary

Confirm no regressions in:

- Scan Results First/Next/New Scan;
- multi-selected Scan Results **Save Address**;
- Saved Address Address/Type/Value edits, refresh, Freeze, Protection, Remove, and Remove All;
- universal export and pretty JSON;
- Connect/Disconnect/process/Active Target/plugin reload;
- PS5 native/resident scanning when live testing is available.

## Static Preparation Review

Before packaging, verify:

- `AppInfo` reports `0.1.5.rev7` and feature title `Memory Viewer Bookmarks and Region Navigation`;
- `MemoryViewerRegionNavigator` is in Core and depends only on neutral `MemoryRegion` models;
- `MemoryViewerReader` reuses the same readable-region eligibility rule instead of duplicating it;
- bookmark state is held by the Memory Viewer ViewModel and is not written to settings or plugin storage;
- Previous/Next Region skip non-readable and Guard regions;
- Region Start/End and bookmark Go use the same safe viewer read path and successful-address history;
- the Saved Addresses row tooltip becomes null when `StatusText` is empty while explicit child tooltips are unchanged;
- Plugin API remains `2.9.0`;
- PS5 plugin remains `0.1.0.rev22`;
- Mock plugin remains `1.0.0.rev4`;
- automated registry contains exactly 54 checks;
- `tools/preflight/` is byte-identical to rev6;
- XAML/project XML and bundled JSON parse successfully;
- all relative Markdown links resolve;
- no `bin`, `obj`, or `.vs` directories are packaged;
- ZIP extraction reproduces the release tree byte-for-byte.

## Acceptance

`0.1.5.rev7` is accepted when the Windows solution builds cleanly, all **54/54** automated checks pass, bookmarks preserve exact addresses and remain window-local, region navigation behaves correctly on neutral and live memory maps, history/origin behavior remains coherent, the Saved Addresses empty-tooltip bug is gone without removing real child/status tooltips, and no previously verified rev6 write/scanner/export/PS5 behavior regresses.

## Runtime Result Recorded During rev8 Preparation

Runtime validation reported after the 54/54 automated suite confirmed that the rev7 functional changes behaved correctly: Memory Viewer bookmarks, region navigation, Back/Forward integration, and the Saved Addresses empty-tooltip correction all worked as intended in normal use.

That validation also exposed several host presentation/state issues which are intentionally corrected in `0.1.5.rev8` rather than changing the verified rev7 Core/navigation behavior:

- the closed bookmark selector displayed the backing ViewModel type name instead of the bookmark label;
- destructive/dismissive button styling was not yet consistent across every application-owned Remove/Cancel action;
- Scan controls could still appear interactive before an explicit Active Target was set;
- the native main-window title still included version/revision;
- the status bar showed the revision feature title instead of version/revision.

Rev7 should therefore be treated as functionally runtime-verified for its bookmark/region/tooltip feature work, with these UI/state presentation issues superseded by rev8.
