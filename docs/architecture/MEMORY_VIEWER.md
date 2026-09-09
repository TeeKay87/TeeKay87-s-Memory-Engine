# Memory Viewer Architecture

## Status

Application `0.1.5.rev1` introduced the first production **Memory Viewer Foundation** as an intentionally bounded read-only viewer. It established the shared Core read model, host-side target coordination, themed WPF presentation, and navigation entry points without mixing in disassembly, pointer work, or platform-specific behavior.

The first rev1 Windows/WPF build failed before runtime verification because the new Scan Results/Saved Addresses `MenuItem.Click` handlers were placed inside `ContextMenu` objects created through `DataGridRow` `Setter.Value`. Application `0.1.5.rev2` corrected that host-XAML integration without changing the Core Memory Viewer model. Its first Windows build then exposed `CS0136` in the new row-context helper; application `0.1.5.rev3` corrected that C# declaration-space error. Application `0.1.5.rev4` resumed functional Memory Viewer development with independent row selection, clipboard actions, Back/Forward navigation history, and a persistent green origin-row marker that remains visible when selection moves elsewhere. Runtime testing then showed that the rev4 selected-origin border changed the DataGrid row's desired size; application `0.1.5.rev5` removed that layout-affecting border so origin highlighting remains green without changing row geometry. Rev5 was then user-verified with all 51 automated checks passing and runtime confirmation that selecting the green origin row no longer changes table geometry. Application `0.1.5.rev6` added the first safe raw-byte editing path through the existing neutral memory reader/writer contracts, stale-source validation, memory-protection enforcement, and immediate read-back verification; rev6 was subsequently fully verified with 53/53 automated checks plus Mock Target and real-PS5 runtime testing. Application `0.1.5.rev7` added viewer-local bookmarks and neutral previous/start/end/next readable-region navigation while keeping all reads on the existing coordinated viewer path; the bookmark/region/history behavior subsequently passed runtime testing. Application `0.1.5.rev8` leaves those target/navigation paths unchanged and corrects the bookmark selector's closed-state display so the selected item presents its address/region label instead of the backing ViewModel type name.

The Memory Viewer uses the same neutral `IMemoryReader`, `IMemoryWriter`, `TargetProcess`, and `MemoryRegion` contracts already used by the scanner and Saved Addresses. No PS5-specific command, process assumption, address rule, or page-protection override is present in the viewer.

Application `0.1.6.rev6` kept all verified Memory Viewer read/write/bookmark/history/region-navigation behavior unchanged and added one capability-driven **Open in Disassembler** context-menu route. Application `0.1.6.rev7` adds layout-neutral **value-span highlighting**: when a viewer is opened from Scan Results or Saved Addresses, the host carries the source value's actual byte count into the viewer and highlights that complete visible span inside both Hex Bytes and ASCII. Application `0.1.6.rev8` corrects the inline WPF renderer so all six read-only `Run.Text` presentation segments are explicitly OneWay-bound; this prevents WPF from attempting to write back to read-only row properties when the viewer opens. Manual Go To, bookmarks, and region navigation use a one-byte highlight because they carry no external value-size context. Back/Forward history restores both address and highlight length. For the `0.1.6` viewer work, Plugin API remained `2.11.0`; PS5 plugin `0.1.0.rev24` and Mock plugin `1.0.0.rev7` were unchanged. Host `0.1.7.rev1` advanced the public API to `2.12.0` for the separate debugger foundation. Host `0.1.7.rev2` leaves Memory Viewer behavior unchanged while Mock advances to `1.0.0.rev8` / API `2.12.0` for its debugger backend. Host `0.1.7.rev3` likewise leaves Memory Viewer behavior unchanged; PS5 advances to `0.1.0.rev25` / API `2.12.0` only for its separate debugger transport. Host `0.1.7.rev4` preserves the same Memory Viewer implementation while correcting main-window target/header presentation. Host `0.1.7.rev5` keeps Memory Viewer unchanged while making only the row-1 target input sizing responsive. Application `0.1.6.rev9` adds manual Saved Address creation but does not change Memory Viewer behavior; a manually created row uses its current `ValueSize` through the same existing Saved Address -> Open in Memory Viewer path. Corrective `0.1.6.rev10` changes only the manual-dialog address parser definite-assignment path and leaves Memory Viewer behavior unchanged.

## 0.1.5 Feature Plan

The completed `0.1.5` Memory Viewer feature block grew revision by revision rather than attempting to implement every memory-editor feature at once:

- **rev1** — read-only Memory Viewer foundation implementation;
- **rev2-rev3** — corrective compile-fix revisions required to make the rev1 host integration build cleanly;
- **rev4** — selection, copy, navigation/history, and persistent origin-row highlighting;
- **rev5** — corrective layout-neutral origin-selection rendering after runtime verification exposed row-size expansion;
- **rev6** — safe raw-byte editing/write support with stale-source checks and read-back verification;
- **rev7** — viewer-local bookmarks plus previous/start/end/next readable-region navigation;
- **rev8** — bookmark selection-label correction plus host UI/state consistency fixes discovered during rev7 runtime verification; this revision completed the verified `0.1.5` feature block.

The active major feature block is now **architecture-neutral disassembly**: `0.1.6.rev1` established its shared contracts/Core foundation, `0.1.6.rev2` added the PS5 x86-64 provider, and `0.1.6.rev3` added the first standalone Disassembler workspace. `0.1.6.rev4` added Scan Results/Saved Addresses **Open in Disassembler** entry points, `0.1.6.rev5` corrected around-origin decoding so an instruction that begins before the requested byte can remain intact across the origin, and `0.1.6.rev6` added Memory Viewer **Open in Disassembler** with the same stale-session safeguards used by the viewer itself. `0.1.6.rev7` then improves Memory Viewer readability by carrying source-value size into the viewer and highlighting the complete known byte span without changing memory ownership or platform contracts. `0.1.6.rev8` is a corrective host-only revision that keeps that model intact and makes the six inline prefix/highlight/suffix bindings explicitly OneWay after Windows runtime testing exposed the default binding-mode failure. `0.1.6.rev9` leaves that viewer model unchanged while allowing new Saved Address rows to originate from the manual-entry dialog.

This sequence is a working development plan rather than a frozen public API. Individual revisions may be split further when verification reveals dependencies or safety requirements.

## Ownership Boundary

The implementation is deliberately divided by responsibility:

```text
WPF / application
    opens a viewer window from Scan Results or Saved Addresses
    owns address input, row presentation, edit dialog, status/error text, and theme integration
    coordinates viewer reads/writes with existing foreground-target/Saved Address I/O rules
    captures the connection generation and target identity for safety

Core
    chooses a bounded readable window around the requested address
    calculates row intersections for an address + byte-count highlight span without platform assumptions
    clamps the window to one readable, non-guarded MemoryRegion
    reads the bounded byte range through neutral IMemoryReader
    validates a requested edit against the currently displayed bytes
    requires one readable, writable, non-guarded MemoryRegion for the complete edit
    writes through neutral IMemoryWriter and immediately reads the range back
    returns platform-neutral read/write result models

Plugin / backend
    supplies the existing IMemoryReader, IMemoryWriter, and memory-map implementations
    remains unaware of Memory Viewer UI, row formatting, or edit-dialog behavior
```

The viewer does not introduce a new Plugin SDK contract. Rev1 reused the already-verified memory-read and memory-map services; rev6 additionally reuses the existing `IMemoryWriter`.

## Core Reader

`MemoryViewerReader` is the shared read-only Core service for the initial viewer.

The current defaults are:

```text
Bytes per row:       16
Visible byte window: 512
Maximum requested:   65,536
```

The 512-byte window remains the normal bounded view. Rev7 keeps that size for ordinary 1/2/4/8-byte values and one-byte navigation. When a source value is larger than half of the normal page, the host may request a larger bounded window (up to the existing 65,536-byte Core maximum) so the complete forward value span can remain visible where the containing region permits. The WPF viewer still never attempts to materialize an entire process or arbitrary memory region.

For each request Core:

1. resolves the requested address to a containing memory region;
2. requires the region to include `MemoryProtection.Read`;
3. rejects a region carrying `MemoryProtection.Guard`;
4. chooses a window centered around the requested address where possible;
5. clamps the start/end to the containing region;
6. aligns the start toward the 16-byte row boundary only when the aligned page still contains the requested address and stays inside the region;
7. issues one bounded `IMemoryReader.ReadAsync(...)` request;
8. validates the returned byte count;
9. returns `MemoryViewSnapshot` containing the requested address, actual start address, bytes, region, and row width.

A short read is represented by the actual returned bytes rather than by manufacturing data that was never received, but it is accepted only when those returned bytes still contain the requested address. A zero-byte/invalid count or a partial response that stops before the requested address is treated as a failed viewer read so a successful snapshot always has an origin row.

## Region Boundaries

Rev1 never reads through the end of the containing region simply to fill the requested 512-byte window.

Near a region boundary the visible range shifts or shrinks so that every byte shown belongs to the same resolved `MemoryRegion`. This gives the UI an unambiguous Region / Module and Protection context for the complete visible page.

The viewer does not currently stitch adjacent readable regions together. Richer cross-region navigation can be evaluated later without changing the rev1 safety rule.

## Target and Connection Safety

A Memory Viewer window captures both:

- the target process identity (`Id` + `Name`);
- the host connection generation active when the window was opened.

Every read and rev6 write is routed back through the owning `PluginViewModel`, which verifies that:

- the plugin workspace is still connected;
- the same connection generation is active;
- the same process is still the explicit Active Target;
- the required memory read/write/map capabilities remain available for the requested operation;
- a current memory map is loaded.

This prevents a viewer opened for one target from silently redirecting itself to a different process or to a later reconnect that happens to reuse the same process id/name. After reconnecting, the user must open a new Memory Viewer window.

## Foreground Target I/O Coordination

Viewer reads and explicit rev6 writes use the same host foreground-target reservation used by existing target-level operations.

Before reading or writing, the host:

- rejects the read while another incompatible foreground operation such as scanning/read/write is already active;
- reserves the foreground immediately;
- prevents new Saved Address timer work from starting;
- waits for already-running Saved Address I/O to reach its normal safe idle boundary;
- performs the bounded viewer read or the complete pre-read/write/read-back sequence on the plugin's primary session services;
- releases the foreground reservation afterward.

The viewer therefore does not send an unsynchronized ps5debug-NG command beside the existing refresh/scan/export command stream. Memory Viewer writes deliberately use the primary session writer under this foreground reservation rather than the optional concurrent Frozen-write channel.

This is host coordination, not PS5-specific logic. Any future platform plugin exposing the same neutral services follows the same viewer workflow.

## WPF Presentation

`MemoryViewerWindow` is modeless and owned by the main application window. Multiple viewer windows may exist, but each read still has to reserve the shared target operation path.

The current window contains:

- target/plugin identity;
- **Back** and **Forward** navigation;
- a hexadecimal **Address** field;
- **Go To**;
- **Refresh**;
- **Edit...** for an editable selected row;
- **Previous Region**, **Region Start**, **Region End**, and **Next Region** navigation;
- viewer-local bookmark add/select/go/remove controls;
- a dynamic **Read-only** / **Read/write** access label;
- Region / Module display;
- complete containing-region range;
- current memory `Protection` flags;
- current visible byte range;
- a presentation-only virtualized DataGrid with:
  - Address;
  - Hex Bytes;
  - ASCII.

Rows use 16 bytes. Non-printable ASCII values are displayed as `.`. After a successful read, the row containing the requested address is selected and scrolled into view. From rev4 that row also carries an independent **origin** state rendered with a green success-color background. The origin marker is not the DataGrid selection: clicking or multi-selecting other rows leaves the origin row green, while ordinary selected rows continue to use the normal theme selection color. Rev5 makes this marker explicitly layout-neutral: selecting the origin row does not add border thickness or change padding, margin, row height, column width, or any other geometry.

Application `0.1.6.rev7` adds a second, byte-level presentation layer without changing the row model or table geometry. `MemoryViewHighlightSpan` in Core represents an exact address + byte count and calculates the overlap with each visible 16-byte row. The WPF row model splits the already-existing plain Hex Bytes and ASCII strings into prefix/highlight/suffix presentation segments; the underlying `Bytes`, `HexBytes`, `Ascii`, clipboard text, and edit payload remain unchanged. Highlighted bytes use the existing Primary button background/text brush pair through `DynamicResource`, so the span remains visible over the origin row or ordinary selection without a new required theme key, font-weight change, border, or padding. A span that crosses a row boundary highlights the tail of the first row and the head of the next row. Host `0.1.6.rev8` keeps those row properties read-only and binds every `Run.Text` segment with explicit `Mode=OneWay`; presentation bindings must not rely on WPF default modes when the source has no setter.

A new manual Go To, bookmark destination, or region navigation uses a one-byte highlight. Refresh keeps the current span. Back/Forward restores the span that belonged to each successful navigation entry. The DataGrid still uses only the three explicitly declared columns and keeps WPF auto-generation disabled.

Region / Module follows the same naming rule as the existing scanner workspace: module name is preferred, then region name, and the field remains blank when the backend supplies neither. The viewer does not manufacture a name for anonymous mappings.

The window and rev6 **Edit Hex Bytes** modal use the application's shared WPF resources and `DynamicResource` theme brushes. The 0.1.5.rev7 region/bookmark controls reuse the same shared button/input/ComboBox styles. Host `0.1.6.rev7` value-span highlighting reuses the existing Primary button background/text brush pair and introduces no separate light/dark palette or required custom-theme key. Host `0.1.6.rev8` changes only binding direction and introduces no new visual resource.

## Safe Editing and Write Verification

Application `0.1.5.rev6` adds an explicit byte-editing workflow without making the DataGrid itself an inline editor. The table remains `IsReadOnly=True`; the user selects one row and opens **Edit...** or **Edit Hex Bytes...** to edit exactly the bytes represented by that row. A normal full row contains 16 bytes, while a boundary-truncated final row may contain fewer.

A row is editable only when the current snapshot's complete containing region is reported as:

- readable;
- writable;
- not guarded;
- backed by an active plugin session that advertises both memory read and memory write.

The viewer does not change page protection and does not offer an override for a read-only/guarded region.

The write sequence is intentionally optimistic, guarded, and ordered:

1. the dialog starts from a copy of the exact bytes displayed for the selected row;
2. the replacement must contain the same byte count and valid hexadecimal bytes;
3. Core resolves the complete row range against a current memory-map snapshot and requires one readable/writable/non-guarded region;
4. the host reserves the Active Target and rereads the exact range immediately before writing;
5. if those live bytes no longer match the bytes that were displayed when the editor opened, the operation returns `SourceChanged` and **no write is sent**;
6. if the source still matches, `IMemoryWriter.WriteAsync(...)` sends the replacement bytes;
7. the same range is reread immediately;
8. the result is reported as `Verified` only when every read-back byte matches the requested replacement; otherwise it is reported as `VerificationFailed`;
9. the visible Memory Viewer page is refreshed after stale-source detection and after an issued write so the user sees the target's current state.

Core represents the four outcomes explicitly as `NoChanges`, `SourceChanged`, `Verified`, and `VerificationFailed`. A failure after the write was already sent is never described as though no target mutation could have occurred; the UI tells the user that target state may be uncertain and requires a refresh before another edit.

The initial rev6 editor intentionally writes the complete displayed row rather than an arbitrary sub-range. This keeps the first production write contract simple and ensures the stale-source check protects every byte that will be sent. Finer byte/cell selection can be introduced later without weakening this safety boundary.

## Selection and Clipboard Behavior

Application `0.1.5.rev4` changes the Memory Viewer DataGrid from single-row to extended full-row selection. Selection is presentation state only; it never changes the current navigation/origin address and never triggers target I/O.

The Memory Viewer row context menu provides:

- **Copy Address**;
- **Copy Hex Bytes**;
- **Copy ASCII**;
- **Copy Row**;
- **Copy Selected**.

**Copy Row** and **Copy Selected** use tab-separated `Address`, `Hex Bytes`, and `ASCII` fields with one selected row per line. `Ctrl+C` is mapped to the same **Copy Selected** behavior. Right-clicking a row already inside a multi-selection preserves the selected set; right-clicking outside the current selection selects only the context row before row-specific copy commands run. Clipboard work is host-only and causes no memory read/write traffic.

## Navigation History

The viewer keeps an in-window history of successful navigation states. From host `0.1.6.rev7`, each entry stores both the requested address and the highlight byte count so a source-derived value span is restored correctly after Back/Forward.

- **Back** navigates to the previous successful address.
- **Forward** navigates to the next address after a Back operation.
- `Alt+Left` and `Alt+Right` invoke the same commands.
- **Refresh** rereads the current address without creating a history entry.
- Clicking/selecting rows does not create history entries.
- Going to the current history state (same address and same highlight byte count) does not add a duplicate entry.
- A successful Go To, bookmark navigation, or region navigation after moving Back discards the obsolete forward branch before recording the new address.
- Adding or removing a bookmark does not create history by itself.
- A failed target read does not move the history index or discard the previous visible snapshot.

Navigation continues to use the same target/process/connection-generation and foreground-I/O safety checks as the rev1 reader. History is intentionally scoped to the lifetime of one Memory Viewer window.


## Region Navigation

Application `0.1.5.rev7` adds neutral navigation across the Active Target memory map through `MemoryViewerRegionNavigator` in Core. The navigator accepts only `MemoryRegion` models and therefore has no knowledge of PS5, process modules, backend commands, or WPF.

The available actions are:

- **Previous Region** — navigate to the base address of the nearest earlier readable, non-guarded region;
- **Region Start** — navigate to the current region's exact base address;
- **Region End** — navigate to the final byte (`EndAddressExclusive - 1`) of the current region;
- **Next Region** — navigate to the base address of the nearest later readable, non-guarded region.

Previous/Next ignore mappings that have no `Read` permission or carry `Guard`. They do not require `Write`; executable and read-only mappings remain valid inspection destinations. The current memory-map list may be unsorted, so Core orders eligible candidates by base address before choosing a neighbor. The bounded `MemoryViewerReader` still resolves and clamps the actual visible page after navigation; region navigation therefore never stitches regions together or bypasses the existing one-region read rule.

Every successful region action uses the same `ReadMemoryViewerWindowAsync(...)` host path and is recorded in the same Back/Forward history as Go To. A failed region read leaves the previous page/history state intact.

## Bookmarks

Application `0.1.5.rev7` adds lightweight **viewer-local bookmarks** for exact Memory Viewer origin addresses.

- **Add Current** records the current origin address, not merely the selected 16-byte row base.
- The same exact address cannot be added twice in one viewer window.
- A bookmark keeps the backend-supplied Region / Module label when one exists; anonymous mappings remain address-only.
- The selector's selected/closed state uses the bookmark's same display label as the expanded list. `MemoryViewerBookmarkViewModel.ToString()` deliberately returns `DisplayText` so the shared custom ComboBox template cannot fall back to the CLR type name when rendering the selected object.
- **Go** navigates to the selected bookmark through the normal safe viewer read path and records that successful destination in Back/Forward history.
- **Remove** deletes only the selected bookmark and causes no target I/O.
- Bookmarks survive Refresh, writes, Back/Forward, and region navigation for as long as that Memory Viewer window remains open.

Rev7 deliberately does **not** persist bookmarks to general application settings. Persistent bookmarks logically belong with later project/cheat persistence or another deliberate saved-data design so temporary memory-analysis state is not silently written into unrelated settings storage.

## Navigation Entry Points

Rev1 added the Memory Viewer action now labeled **Open in Memory Viewer** to the context menus for:

- Scan Results;
- Saved Addresses.

The selected row's address becomes the viewer's initial requested address. From host `0.1.6.rev7`, Scan Results also pass `CurrentValue.Size` and Saved Addresses pass their current `ValueSize`, allowing the Memory Viewer to mark the full known value span instead of only the containing row.

Application `0.1.6.rev4` renames the user-facing context-menu label from **Browse Memory** to **Open in Memory Viewer** for both Scan Results and Saved Addresses. Host `0.1.6.rev7` retains those target-safety semantics and augments the navigation payload only with the already-known value byte count used for presentation highlighting.

A Saved Address viewer carries the Saved Address target identity. If that Saved Address is not for the current Active Target, the window remains safe and reports the mismatch instead of reading the current process.

### Rev2 context-menu ownership correction

Rev1 originally instantiated both row context menus through `DataGridRow` style `Setter.Value` and attached new code-behind `Click` handlers inside that setter-created object graph. The first Windows markup build rejected that wiring. Rev2 moves each menu to the concrete owning `DataGrid.ContextMenu` property. A `ContextMenuOpening` handler resolves the row currently under the pointer, assigns that row as the menu DataContext, and preserves an existing Scan Results multi-selection when the context row already belongs to it. A right-click on a row outside the current selection clears the unrelated selection and selects the context row before menu commands run.

This is a host-WPF correction only. The Memory Viewer address/target inputs, Core read service, Saved Address identity rule, and plugin boundary do not change.

## Scan Results Multi-Selection Save Behavior

The same revision corrects an existing Scan Results interaction that became more visible once row context actions expanded.

When multiple Scan Results rows are selected and the user right-clicks one of those selected rows and chooses **Save Address**, the host now processes the complete selected set. Previously only the row that supplied the context menu was saved.

Rules are:

- one selected/clicked row retains the existing single-row behavior;
- when the context-clicked row is part of a multi-row selection, all selected current Scan Results are processed;
- duplicate Saved Address identities are skipped rather than duplicated;
- derived Saved Address count/export-state properties are refreshed once after the bulk additions;
- the status text reports how many selected rows were added and how many were already saved;
- double-click remains the existing single-row Save Address shortcut.

The Saved Address identity rule itself is unchanged: target process + address + Value Type.

## Deliberately Deferred After rev8

The current revision does **not** implement:

- byte-level/cell-level editing selection beyond the current row-selection model;
- page stepping beyond explicit address/history/region/bookmark navigation;
- persistent bookmarks across viewer windows/application launches;
- live periodic refresh;
- changing page protection;
- disassembly or assembly;
- Find What Writes / Accesses;
- pointer interpretation;
- text encoding selection beyond the ASCII presentation column;
- Memory Viewer export.

These remain deferred so the bounded read/write safety rules, target coordination, and presentation behavior can be verified before richer navigation, debugger, or disassembly functionality is layered on top.


> Host `0.1.7.rev6` builds on the fully verified rev5 baseline. Its Threads/Thread Control and passive header-status cleanup do not redesign the subsystem documented here.
