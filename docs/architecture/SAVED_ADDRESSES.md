# Saved Addresses Architecture

## Status

Saved Addresses was introduced in application `0.1.3.rev17`, received independent refresh/Frozen scheduling in `0.1.3.rev18`, had its Frozen reliability behavior hardened in `0.1.3.rev19`, received explicit user-write/background-I/O coordination in `0.1.3.rev20`, added queued individual-row removal in `0.1.3.rev21`, generalized user-intent/background-I/O coordination in `0.1.3.rev22`, moved Remove All to the reusable themed confirmation surface in `0.1.3.rev23`, gained reusable Address/Value live-input filtering in `0.1.3.rev24`, rev25 closed the WPF Space-key path that could bypass those live filters, application `0.1.4.rev1` connects the table to the shared universal export infrastructure, and `0.1.4.rev3` adds current memory-map Protection presentation/export for each saved range. Application `0.1.4.rev4` attempted to correct the Saved Addresses Protection alignment through the `DataGridCell` container, but runtime verification showed the generated text remained too high. Application `0.1.4.rev5` replaces that presentation path with an explicit full-height cell template whose `TextBlock` is vertically centered. Application `0.1.5.rev1` added the first direct Memory Viewer navigation action from a Saved Address into the shared read-only Memory Viewer foundation. Corrective `0.1.5.rev2` moves the Saved Addresses context menu out of the `DataGridRow` style `Setter.Value` object graph after rev1's first WPF build rejected its new code-behind event wiring; the row DataContext is now prepared on the concrete DataGrid context menu when it opens. Corrective `0.1.5.rev3` fixes the subsequent `CS0136` compile error in that shared preparation helper by reusing one nullable ContextMenu local rather than redeclaring the same pattern-variable name. Memory Viewer selection/copy/history arrived in `0.1.5.rev4`, rev5 made the selected-origin highlight layout-neutral and was fully verified, `0.1.5.rev6` adds explicit safe byte editing through the viewer's own neutral read/write workflow without changing Saved Address row semantics, and `0.1.5.rev7` adds viewer-local bookmarks/readable-region navigation. `0.1.5.rev7` also corrects Saved Addresses row tooltip behavior: the row-level `StatusText` tooltip is suppressed when empty so hovering unused Frozen/Protection/Remove cell space no longer produces an empty hint popup, while explicit child tooltips such as the Frozen checkbox and Remove button remain unchanged. Application `0.1.6.rev9` adds target-safe manual row creation through a dedicated themed dialog while reusing the existing Saved Address model, plugin Value Types, refresh/write paths, target identity, and duplicate semantics.

The subsystem is host/shared functionality. Concrete Value Types remain plugin-owned, and normal target reads/writes continue to use the neutral Plugin SDK memory contracts. Rev18 added a separate optional concurrent-writer contract so a plugin can explicitly provide a memory-write path that is safe to use while its primary target transport is occupied by another long-running operation. Rev19 also prefers that concurrent writer for ordinary repeated Frozen writes whenever it is available, keeping the freeze cadence independent from value-refresh reads.

## User Workflow

A Saved Address can be created from a displayed Scan Result in either of two ways:

1. double-click the Scan Result row; or
2. right-click the Scan Result row and choose **Save Address**.


Application `0.1.6.rev10` is a corrective host-only revision for the manual-entry dialog. It initializes the hexadecimal parser's `out` address before the short-circuit length guard so the Windows C# compiler can prove definite assignment on every return path. The dialog controls, target-safety rules, duplicate semantics, Value Type handling, planned placeholders, and Saved Address row behavior are otherwise unchanged from rev9.

Application `0.1.6.rev9` also adds **Add Manually** to the Saved Addresses toolbar. Manual creation requires a current Active Target and memory-read capability, then accepts:

- a hexadecimal absolute Address;
- an optional Description;
- one of the active plugin's declared Value Types;
- Length when that Value Type is variable-size.

Fixed-size types use their declared byte width automatically. Variable-size manual rows currently accept 1-4,096 bytes and default to 10 bytes when the dialog first switches to a variable type. After creation the row is refreshed immediately through the same target read path used by existing Saved Addresses; unreadable memory is reported through the existing row/status mechanism rather than replaced with fabricated bytes.

The new row is added to the Saved Addresses table. Saving or manually adding the same target process, absolute address, and Value Type again selects the existing row rather than adding an accidental duplicate.

Saved Addresses are independent from the temporary scan session. First Scan, Next Scan, and New Scan may replace or remove Scan Results without deleting Saved Addresses.

Saved Addresses currently exist only in the current application/plugin workspace. They are not written to `settings.json`, and they are not yet persisted as a project/cheat table across application launches or Plugin Reload.

## Row Model and Visible Columns

Each row stores:

- target process id;
- target process name/display name;
- absolute address;
- user description;
- selected plugin-owned Value Type;
- value width and alignment;
- target architecture/endianness used for value interpretation;
- last successfully read current value bytes/display text;
- Frozen state;
- captured frozen bytes;
- nullable resolved memory-map protection for the complete current value range;
- row status/error text.

The visible columns are:

```text
Frozen | Description | Address | Type | Value | Protection | Remove
```

There is intentionally no separate `Active` column. **Frozen** is the user-controlled repeated-write state. New rows start with an empty Description; the Description cell itself is the editable field rather than displaying a stored `No description` value.

## Saved Address Actions

Each row has a dedicated **Remove** button at the far right. The row context menu also provides:

```text
Freeze / Unfreeze
Open in Memory Viewer
Open in Disassembler
-----------------
Copy address
Copy value
-----------------
Remove address
```

The toolbar order in application `0.1.6.rev9` is **Add Manually / Remove All / Export**. **Add Manually** is enabled only when the selected plugin exposes memory read plus at least one Value Type, a safe Active Target exists, and no incompatible foreground Saved Address operation is in progress. The dialog captures the target/session identity before commit and revalidates it after any in-progress background Saved Address I/O reaches its normal idle boundary, so a process or connection change cannot redirect the requested address to another target.

The toolbar-level removal action is **Remove All**. It always requests confirmation before clearing the collection. From rev23, that prompt uses the application-owned reusable confirmation dialog rather than an operating-system `MessageBox`; it follows the active theme, uses the Danger semantic role, names the destructive button **Remove All**, and leaves the destructive action out of the Enter-key default. Removing a single row does not require the Remove All confirmation.

An individual Remove request is never discarded merely because Saved Address I/O happens to be active at that instant. If a refresh, Frozen write, or explicit Value write is already in progress, rev21 marks the row for pending removal, disables its Frozen state immediately, stops scheduling new Saved Address timer work, and lets only already-started target I/O reach its normal protocol-safe completion. A multi-row refresh/Frozen cycle exits after its currently awaited row once a pending removal exists. The pending row is then removed automatically when both background Saved Address I/O and any explicit user Value write are idle. Repeated Remove requests for the same row are coalesced, and several rows may be queued during the same busy interval.

Rev21 initially limited pending removal to individual rows. Rev22 extended the same safe-idle removal mechanism to a confirmed **Remove All** set. Rev23 changes only the confirmation surface; the rev22 deferred-removal behavior remains authoritative.

The toolbar **Export** action is active from application `0.1.4.rev1`; rev9 removes the ellipsis from the toolbar label without changing the export dialog or pipeline. It uses the shared export dialog and Core writer pipeline rather than a Saved Address-specific serializer. The user can export **All Addresses** or **Selected Addresses**, choose JSON/CSV/TSV/Markdown table, and select any subset of Frozen, Description, Address, Type, Value, and Protection. The export source snapshots the current host rows when the dialog is opened, so subsequent target refresh timing cannot change rows halfway through the file and no target read is required to perform the export.

The action is temporarily unavailable while a direct Saved Address user operation is committing an Address, Type, Value, or related target-backed change. WPF LostFocus can begin such a commit immediately before a toolbar click; gating export until that operation completes ensures the snapshot cannot capture the stale pre-commit value. Ordinary background refresh/Frozen cadence does not require the same restriction because snapshot construction is synchronous on the UI thread and file writing uses only the captured host data.

## Plugin-Owned Value Types

Saved Addresses must not introduce a second hardcoded concrete Value Type catalog in Core or WPF.

The row receives the Value Type definitions already declared by the active platform plugin. The selected `IMemoryValueType` remains authoritative for:

- stable Value Type id;
- display name;
- fixed or variable width;
- default alignment;
- parsing edited user text;
- creating/displaying values from bytes;
- target-endianness interpretation.

A future plugin-defined Value Type can therefore participate in Saved Addresses without adding a platform-specific branch to the host, provided its normal `IMemoryValueType` implementation supports the required parse/create operations. The rev9 manual-entry dialog consumes this same list directly; it does not maintain a second manual-only type catalog or infer width from display names.

## Manual Entry Dialog (0.1.6.rev9)

The manual dialog deliberately separates controls that are functional now from controls that only reserve a future workflow location.

Functional controls are:

- **Address** — host-owned hexadecimal syntax with optional `0x` prefix and up to 16 hexadecimal digits;
- **Description** — free-form optional label;
- **Value Type** — active-plugin declarations only;
- **Length** — read-only required size for fixed-size types, editable 1-4,096 byte count for variable-size types;
- **Current value** — the dialog states that the row is read immediately after creation; the dialog does not invent a preview value before a target read has occurred.

The dialog also displays disabled **planned** placeholders for hexadecimal display preference, Binary start bit, Text Unicode/code-page settings, and Pointer/base-address/offset controls with Add Offset / Remove Offset. These controls have no effect on the row. Pointer chains require a neutral pointer-expression/dereference model and remain outside rev9 rather than being implemented as PS5-specific WPF behavior. The source survey behind these placeholders is documented in [`../research/CHEAT_ENGINE_MANUAL_ADDRESS_DIALOG.md`](../research/CHEAT_ENGINE_MANUAL_ADDRESS_DIALOG.md).

Signedness does not need a separate manual-dialog checkbox in the current architecture: signed and unsigned representations are already distinct plugin Value Types, so introducing another Saved Address signed flag would duplicate type semantics.

## Direct Cell Interaction

### Frozen

Clicking the checkbox requests freeze/unfreeze.

When freeze is enabled, the host first refreshes the address from the live target, captures those current bytes, performs an immediate write of the captured bytes, and then marks the row Frozen. This avoids treating an older scan snapshot as the freeze baseline.

### Description

Description is edited directly in the table. New rows use an empty description. The field is user metadata and does not cause target memory access.

### Address

Address accepts hexadecimal text with or without a `0x` prefix. The reusable textbox filter blocks non-hexadecimal typing and paste and limits the live payload to 16 hexadecimal digits, matching the current target-neutral `ulong` address representation. Empty or otherwise incomplete edit states can still exist while the user restructures the text; the existing final hexadecimal parser remains authoritative and restores the last valid address when a commit is invalid. Changing Address disables an existing freeze before the new location is used.

### Type

Type is selected from the plugin's declared Value Types. Changing Type disables freeze, updates width/alignment according to the new definition, and schedules a refresh using the new interpretation.

The host does not maintain a parallel list of legal Value characters. From rev24, a selected Value Type may optionally implement Plugin API `2.5.0` `IMemoryValueInputPolicy`; the shared WPF editor consumes that policy directly. This keeps live syntax ownership beside the same plugin-owned Value Type that already owns final parsing and formatting.

### Value

Value displays the last successful target read. When the selected plugin Value Type implements the optional Plugin API `2.5.0` `IMemoryValueInputPolicy`, keyboard text and paste are filtered to syntax that can still become valid while ordinary transitional states such as `-`, `0x`, `1.`, or `1e-` remain editable. A custom Value Type that does not implement the optional policy remains freely editable. Editing Value always uses the selected plugin Value Type's existing `TryParse(...)` as the authoritative commit/range validation before any bytes are sent through a neutral writer service. If the active plugin exposes `IConcurrentMemoryWriter`, direct Value edits prefer it so a user write does not collide with periodic reads or Frozen traffic on the primary transport; otherwise the host pauses future Saved Address ticks and waits for an already-running background cycle to return to idle before using `IMemoryWriter`.

While the Value TextBox has keyboard focus, background refresh/Frozen completions continue updating the row's internal current bytes but do not replace the text the user is actively editing. Losing focus or pressing Enter commits the captured edit. A direct edit on a Frozen row replaces the captured Frozen bytes before the write is queued, so the user's new value becomes the authoritative freeze target even when an older Frozen write was already in flight.

### Protection

Protection is read-only target metadata. The host resolves the Saved Address's complete current value range against the cached neutral `MemoryRegion` map and displays the containing region's `MemoryProtection` flags, for example `Read`, `Read, Write`, or `Read, Execute`. The value is recalculated when the Active Target memory map changes and after Address, Value Type, or successful variable-size Value changes that can alter the covered range.

If the Saved Address belongs to another Active Target, no memory map is available, or the complete range no longer fits one region, Protection is left blank. The column is informational: it does not change page protection and does not bypass the existing readable/writable checks. From `0.1.4.rev5`, the Saved Addresses Protection value is rendered by an explicit full-height `DataGridTemplateColumn` cell template with its `TextBlock` vertically centered. This avoids relying on `DataGridTextColumn`'s generated presentation element and aligns the read-only metadata with the taller TextBox/ComboBox-based row controls; this is a WPF presentation change only. A row without `Write` remains subject to the same write/freeze rejection or deferral rules as before.

## Open in Memory Viewer and Disassembler

The Saved Address context menu exposes **Open in Memory Viewer**. This is the current label for the Memory Viewer navigation introduced as **Browse Memory** in `0.1.5.rev1`. The viewer opens at the row's current absolute address and carries the row's saved target process id/name rather than assuming the currently selected process. From application `0.1.6.rev7`, the navigation payload also carries the row's current neutral `ValueSize`, so Memory Viewer can highlight the complete known value-byte span in Hex Bytes and ASCII. If the Value Type/array length changes before opening the viewer, the current size is used rather than a stale captured display size.

Application `0.1.6.rev4` adds **Open in Disassembler** beside that action. The Disassembler entry uses the same saved target identity and is enabled only when:

- the selected plugin advertises neutral Disassembly plus the required memory-read/map services;
- a usable Active Target/memory map exists;
- the row's saved process id/name matches the current Active Target;
- no conflicting foreground target operation currently blocks the read.

The clicked Saved Address becomes the Disassembler origin. The Disassembler then applies its shared bounded context policy around that address; the Saved Addresses subsystem does not perform decoding itself.

In rev2 the context menu itself was moved to the concrete `SavedAddressesDataGrid.ContextMenu` rather than a row-style `Setter.Value`. Rev4 continues to reuse that proven ownership pattern: `ContextMenuOpening` resolves the row under the pointer, selects it when necessary, assigns the row as the menu DataContext, and updates capability/target-sensitive Disassembler availability before the menu is shown.

The Memory Viewer itself was read-only in rev1. From rev6, writable Memory Viewer rows can be edited through the viewer's explicit **Edit Hex Bytes** workflow, but that path remains separate from Saved Address `Value`/Frozen state: it uses the neutral primary-session `IMemoryReader`/`IMemoryWriter`, requires the current region to be readable+writable and non-guarded, rereads the displayed bytes before sending a write, and verifies the result with immediate read-back. It never bypasses Protection or changes page protection. Reads and writes are routed through the owning plugin workspace and accepted only when the same target process and the same connection generation remain active.

Neither navigation action changes the Saved Address persistence model: rows are still application-session workspace data and are not yet restored across launches.

## Independent Refresh and Frozen Schedulers

Rev18 separates Saved Address reads from Frozen writes.

Two application-level settings are stored in the existing shared settings document:

```text
savedAddressesUpdateIntervalMilliseconds
frozenWriteIntervalMilliseconds
```

The current defaults are intentionally different:

```text
Value refresh default: 500 ms
Frozen write default: 100 ms
Minimum for either: 50 ms
Maximum for either: 10,000 ms
```

The 100 ms Frozen default follows Cheat Engine's current address-list freeze default. The two intervals remain independently configurable.

The settings are available from the application Settings window and are applied to already-loaded plugin workspaces immediately after Save.

### Value Refresh

A WPF `DispatcherTimer` schedules ordinary Saved Address reads:

```text
refresh timer
    -> IMemoryReader.ReadAsync
    -> plugin-owned IMemoryValueType.CreateValue
    -> update Value display
```

Value refresh is fully stopped while First Scan or Next Scan is active. This avoids background read traffic competing with scanning and removes the need to toggle global UI command availability for every refresh tick.

### Frozen Writes

A separate `DispatcherTimer` schedules Frozen writes:

```text
frozen timer
    -> IMemoryWriter / IConcurrentMemoryWriter
    -> write captured frozen bytes
```

When the active plugin exposes `IConcurrentMemoryWriter`, repeated Frozen writes prefer that service both during ordinary idle operation and, when allowed, during scanning. This keeps the Frozen cadence independent from an in-flight Saved Addresses refresh read. Plugins without a concurrent writer fall back to serialized `IMemoryWriter` use outside scanning.

During First Scan or Next Scan:

- if **Pause target while scanning** is enabled, Frozen writes are suppressed because the target process is intentionally suspended;
- if **Pause target while scanning** is disabled, the Frozen scheduler remains active;
- while a scan is active, a concurrent writer is required. If a plugin does not provide one, its Frozen rows remain Frozen but the individual in-scan write is deferred rather than risking protocol corruption by interleaving writes on an unsafe primary command stream.

The PS5 plugin provides `IConcurrentMemoryWriter` through a dedicated, lazily connected ps5debug-NG client. The native scan continues to use the already verified primary ps5debug-NG command stream, while Frozen writes use the secondary connection. This prevents unrelated request/response frames from being mixed on one TCP stream.

A Frozen row stores its captured bytes separately from the latest display refresh. Every successful repeated write reapplies those captured bytes and updates the displayed/current row value to the value just written. A generation marker prevents an older read that was already in flight from completing afterward and overwriting that write-side display with stale bytes. A transient write exception does **not** clear `Frozen`; the row records the failure and retries automatically on the next Frozen interval. Frozen is disabled only when the user requests it or when an explicit lifecycle/invariant rule requires it, such as disconnect or loss of the captured frozen value.

## Background I/O and User Intent Coordination

Routine Saved Address refresh/Frozen timer ticks are background maintenance, not foreground user commands. They therefore do not raise the application's global command-state notifications when they begin. This preserves the rev18 no-blinking behavior: Connect/Disconnect, process controls, scan buttons, and other target controls do not visibly flash disabled/enabled at the refresh cadence. The final background-I/O transition back to idle still raises one dependent-command requery so WPF cannot retain a stale command state after an overlapping operation finishes.

From rev22, transient Saved Address I/O is no longer itself a reason to reject target-level user actions. The host uses a foreground-target reservation for actions that must own or transition the primary target state:

- Disconnect;
- Refresh Processes;
- Set Active Target;
- First Scan;
- Next Scan;
- New Scan;
- retained diagnostic Raw Memory Read/Write and Safe Write Test commands.

When one of these actions is accepted, `_foregroundTargetOperationPending` is set immediately. This has four effects:

1. future Saved Address refresh/Frozen timer ticks are stopped;
2. a multi-row timer cycle already in progress exits after its currently awaited row operation;
3. another conflicting foreground target command cannot be accepted on top of the pending transition;
4. the requested operation waits until both background Saved Address I/O and any already-started explicit Saved Address user operation are idle, then continues automatically without requiring a second click.

The reservation is a coordination state, not a general FIFO command queue. Genuine foreground conflicts remain unavailable through their normal `CanExecute` rules. For example, a second scan is not remembered while another scan is running, and Disconnect is not queued behind an active scan. This avoids replaying stale commands after the target state has materially changed.

First Scan and Next Scan use the reservation only for their startup boundary. Once Saved Address I/O is idle and `IsScanningMemory` has been set, the foreground reservation is released. This deliberately preserves the established scan behavior: value refresh remains paused for the scan, while Frozen writes may continue through `IConcurrentMemoryWriter` when **Pause target while scanning** is Off.

### Explicit Saved Address user operations

Rev22 generalizes the rev20 direct-Value-write state into a Saved Address user-operation state. Explicit Saved Address actions take priority over future periodic ticks:

- **Freeze enable** waits for an already-running background Saved Address cycle, captures the current value, performs the initial write, and only then enables repeated Frozen enforcement. The checkbox and context-menu label reflect the latest requested Frozen state immediately while activation is pending. A per-row freeze-request generation makes that latest requested state authoritative, so a later explicit **Off** request prevents an older in-flight enable sequence from re-enabling Frozen afterward. Unfreeze itself remains immediate.
- **Address changes** and **Value Type changes** wait for background Saved Address I/O to become idle instead of rejecting the edit because a timer happened to be active. A Frozen row is unfrozen before its address/type interpretation changes.
- **Direct Value writes** retain rev20 behavior. A plugin-provided `IConcurrentMemoryWriter` may safely perform the explicit write while a primary refresh is still in flight; plugins without one stop future ticks and wait for background I/O before using the primary writer. Successful manual/Frozen writes share the target-write generation that rejects stale older refresh completions.
- **Individual Remove** retains rev21 behavior: removal is recorded as pending, the row is unfrozen immediately, scheduled work is suppressed, and the row is removed at the next all-Saved-Address-I/O idle boundary.
- **Remove All** now applies the same rule. If Saved Address I/O is active when the user confirms, the rows that exist at confirmation time are marked for pending removal and unfrozen. The confirmation is therefore sufficient; the user does not have to click Remove All a second time after the I/O window closes.

Only one explicit Saved Address user operation owns the primary Saved Address user-operation slot at a time. This is intentional: background timer collisions are automatically coordinated, while genuinely overlapping foreground edits are not blindly accumulated for later replay. A later target-level transition such as Disconnect is allowed to reserve the foreground while an already-started Saved Address user operation finishes; no new Saved Address user operation starts after that reservation.

Pending removals reuse the same coordination boundary rather than adding another target-I/O path. The pending set is drained only when `IsSavedAddressIoInProgress` is false. A refresh that completes after its row has been marked for removal does not publish a new display value, and an in-progress freeze-enable sequence observes both pending removal and the latest freeze-request generation before it can enable Frozen.

## Target Safety

A Saved Address is associated with the Active Target process from which the Scan Result was saved.

The current target identity check uses both process id and process name. If another process is Active Target:

- the row remains visible;
- its status identifies the process it belongs to;
- automatic refresh/write for that row is skipped;
- direct write/freeze requests are rejected for that row.

Switching back to the matching process during the same connection makes the row eligible again.

When the target connection ends, both schedulers stop and every active Frozen state is disabled. Repeated writes therefore cannot silently resume against a later connection merely because a process happens to reuse the same numeric id/name.

## Memory-Map Validation

When the active plugin exposes memory-region enumeration and a map is loaded:

- the read-only Protection column reflects the containing region's current neutral protection flags for the full row range;
- refresh requires the full value range to be readable;
- direct value edits require the full value range to be writable;
- freeze requires the frozen byte range to be writable.

For plugins without memory-region enumeration, the host delegates the operation to the normal service contract and handles failures without crashing the Saved Addresses workspace.

## Scan Lifecycle Independence

The Saved Addresses collection is not cleared by the scan-state reset path.

Therefore:

```text
First Scan
    -> Save Address
    -> Next Scan
    -> New Scan
```

leaves the Saved Address intact.

The disk-backed Scan Results generation remains temporary scan-session state and is released by New Scan. Saved Addresses hold their own row state and do not depend on the old scan-result file after being created.

## Export Integration (0.1.4.rev1-rev3)

Saved Addresses is the first persistent-row consumer of the universal export pipeline. The application-owned source converts the current row snapshot into neutral `IExportDataSource` batches while Core owns format writing, progress, cancellation, row-count validation, and safe destination publication.

The exported Saved Address fields are presentation/workspace data, not a project persistence format. From rev3, Protection is included as an optional selected column from the same stable row snapshot. JSON uses the generic export envelope and schema version 1, but the `0.1.4` export block does not define Saved Address re-import, pointer-expression persistence, hotkeys, groups, or cross-launch restoration. Those remain project/address-table concerns rather than being inferred from a list export.


## Current Non-Goals

The current Saved Addresses implementation does not add:

- address/module expressions such as `eboot.bin + offset`;
- pointer-chain addresses;
- groups or nested records;
- hotkeys;
- Notes;
- deeper Memory Viewer integration beyond the existing Open in Memory Viewer and safe raw-byte edit entry points;
- project/cheat persistence across launches;
- batched multi-address target reads/writes.

These should extend the shared Saved Addresses model rather than replacing it with platform-specific implementations.

## Shared Live-Value Scheduler and Scan Results

Application `0.1.3.rev30` reuses the Saved Addresses value-refresh scheduler for visible Scan Results. This does not merge the data models: Saved Addresses remain persistent workspace rows with edit/freeze behavior, while Scan Results remain temporary candidates owned by the active scan session.

The shared timer now runs when either Saved Addresses exist or the Scan Results DataGrid has realized visible rows. It continues to serialize ordinary target reads, pauses during active scanning/foreground target operations, and uses the Settings-controlled **Live value refresh** interval. DataGrid virtualization defines the Scan Results refresh set, so off-screen rows are not periodically reread. Live Scan Result refresh changes only the displayed current Value; it does not modify Previous, candidate membership, or the scan baseline used by Next Scan.


## Rev30 Debugger Address Actions

Saved Address row context menus now include **Add Breakpoint** and **Add Watchpoint**. These actions do not open or attach the Debugger automatically. They are enabled only when an already-open Debugger is attached to the same saved target identity on the current connection generation and its backend exposes Plugin API `2.16.0` request validation.

**Add Breakpoint** requests a persistent Software/Execute breakpoint of size 1 at the saved address. **Add Watchpoint** requests a persistent Hardware/Write watchpoint using the row's current `ValueSize`. Each item is disabled independently if that exact request is illegal or unavailable. This includes stale target identity, non-executable breakpoint addresses, unsupported watchpoint width/access, natural-alignment failure, unmapped/guarded ranges, exhausted backend slots, duplicates, or pending cleanup. The existing Debugger Add dialog remains the explicit path for choosing other access modes, sizes, or temporary lifetime.
