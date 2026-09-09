# Main Workspace Layout

## Purpose

The main TeeKay87's Memory Engine window uses a workflow intentionally familiar to Cheat Engine users while retaining the project's own modern WPF presentation and multi-platform plugin architecture.

The goal is familiarity of workflow, not a visual clone. Shared concepts that have proven useful in memory tools are kept in recognizable locations, while platform-specific connection details and capabilities remain behind the Plugin SDK.

## Permanent Layout

The main window is divided into four persistent levels, with the central work area split into left and right columns:

```text
Application / Theme bar
        ↓
Target / Connection bar
        ↓
┌────────────────────────────────────┬─────────────────────┐
│ Scan Results                       │                     │
│ temporary candidate addresses      │ Scan Controls       │
├────────────────────────────────────┤ value/type/actions  │
│ Saved Addresses                    │                     │
│ persistent selected addresses      │                     │
└────────────────────────────────────┴─────────────────────┘
        ↓
Status bar
```

This is the baseline layout for future scanner work. New scanner implementations should connect to these areas rather than create another temporary main screen.

## Application Bar

The compact top bar contains:

- application title;
- application-level **Settings** access;
- current color-theme selector.

From `0.1.6.rev2`, retained through `0.1.6.rev12`, both the native Windows title bar and the compact top application row show the application title without version/revision. The centralized `AppInfo.DisplayVersion` value is shown only at the right edge of the permanent bottom status bar; the revision feature title is not used as persistent UI chrome.

Settings and the theme selector are host application controls and remain available regardless of active platform. From `0.1.3.rev9`, Settings exposes the persisted Scan Results Storage Location while keeping that application-level concern out of platform plugins.

## Target and Connection Bar

The target area replaces the earlier large plugin-development panel and is kept vertically compact so the memory workspace begins immediately after connection/process status. Standard single-line selectors, text inputs, and command buttons in this area use the shared **34-unit** `UiMetrics.StandardControlHeight`, keeping controls aligned without local height overrides.

It contains exactly **two permanent control rows** in normal operation:

- **Row 1 — connection and target:** **Platform** -> plugin-declared connection inputs -> **Connect** -> **Disconnect** -> **Target Process** -> **Refresh** -> **Set Active Target**. The Platform selector displays only the plugin-declared `Name`, while Backend remains available in Plugin details. Platform, Target Process, and every ordinary plugin-declared TextBox/ComboBox in this row share one responsive host width capped by `UiMetrics.TopTargetInputMaxWidth = 180`, including the PS5 Port field. There is no row-1 minimum width: when available content width is reduced, the ordinary inputs shrink together after the fixed action buttons and existing spacing/margins are reserved.
- **Row 2 — host/tool actions:** **Reload Plugins** -> capability-driven **Disassembler...** -> capability-driven **Debugger...**. Future permanent top-level tool/action buttons are appended to this same second row instead of creating a third permanent row.
- **Plugin details** remains right-aligned on the second-row surface without changing the action order; passive successful memory-map count/status prose is not rendered in the permanent header;
- process/connection/memory-map **error** presentation may appear below the two permanent rows only when an actual error exists; collapsed error surfaces do not reserve normal header height;
- capability-driven memory read/write support remains available to host workflows, while the former Raw Memory Read and Raw Memory Write diagnostic panels are intentionally hidden from the main workspace.

This two-row structure is the permanent host standard for future plugins. Plugins supply connection-setting definitions and capabilities; they do not choose target-header row placement or per-field widths. A plugin with more configuration than can reasonably fit this compact connection/target surface should expose the extra configuration through an appropriate settings/details workflow instead of forcing the main target header to grow to three permanent rows.

Rev4 established the two-row composition and changed the default main-window width to `1460` device-independent units. Rev5 keeps that startup width and the existing `MinWidth=1100`, but removes the fixed-width assumption: row-1 ordinary inputs now start at no more than 180 units and automatically shrink to fit the actual available first-row content width. Rev6 retains that verified sizing/order unchanged and removes the passive successful `Memory map: <n> region(s) loaded.` text from row 2; successful map enumeration still updates the Active Target's region data, and genuine memory-map failures remain visible through the existing error surface. Because the calculation explicitly subtracts the target-bar Border left/right padding from its actual width, the existing left/right target-bar margins remain protected while the input fields absorb size reduction.

From `0.1.7.rev3`, retained by the corrected two-row `0.1.7.rev4` header and responsive `0.1.7.rev5` sizing, the redundant permanent `Active <process>` summary and the passive `Connect to enumerate processes` / `1 process loaded` / `<n> processes loaded` prose are no longer rendered. Active Target identity is still maintained internally and remains authoritative for all memory/debugger operations; removing the text does not merge Selected Process and Active Target state.

The underlying connection and process commands are the same generic commands verified before the layout redesign. The revision relocates them; it does not introduce a parallel connection implementation. From `0.1.3.rev22`, Disconnect, Refresh Processes, Set Active Target, First/Next/New Scan, and the retained diagnostic target operations are not made unclickable merely because a Saved Address refresh/Frozen operation is momentarily in flight. The accepted action reserves the foreground immediately, prevents new Saved Address timer work, lets an already-started row operation finish at a safe boundary, and then continues automatically. Genuine foreground conflicts such as an already-running scan remain disabled rather than being stored for later replay.

### Bottom connection-state indicator

From `0.1.7.rev3`, connection state is the leftmost item in the permanent bottom status bar instead of being repeated in the upper target area. The indicator uses only the generic `SelectedPlugin.IsConnected` state; no platform-name branch is involved. Its presentation text is deliberately binary: **Connected** or **Not connected**. Transient connection-operation prose is not used as the permanent status indicator.

The text remains theme-readable inside a compact rounded status box. Connected state uses the theme's success border/surface, while disconnected state uses the existing danger border. General host status, error text, scan status/progress, and the right-aligned application `version.revN` remain separate status-bar fields.

### Selected process versus Active Target

The process picker and Active Target remain deliberately separate.

Selecting a process only changes the current selection. It does not silently redirect future memory operations. A process becomes the Active Target only after the user explicitly chooses **Set Active Target**.

This distinction is important once memory reads/writes, scans, debugger operations, and saved addresses can operate against a live process. Setting the Active Target triggers capability-driven memory-map enumeration when available. The host caches the returned neutral regions and uses them for scanner and Saved Addresses range validation. The former Raw Memory Read/Write diagnostic code and Safe Write Test support remain in the codebase for development/regression use, but their panels are no longer exposed in the main workspace. None of these workflows adds a PS5-specific branch to the main window.


### Raw-memory diagnostics

The capability-driven Raw Memory Read and Raw Memory Write diagnostic implementations remain in the host for development and regression work, but rev18 does not expose their panels in the main workspace. Saved Addresses now provides the normal user-facing read/write workflow.

### Plugin details

Plugin metadata and capability badges remain available under the collapsible **Plugin details** area. They are useful for diagnostics and development but no longer consume the primary working area at all times. Empty connection/process error rows collapse completely instead of reserving unused height below the normal status line.

## Scan Results

The main upper-left table displays scanner results.

Scan Results contains temporary candidates generated/refined through shared Core semantics. The Core catalog now contains 13 standard Scan Types shared by every plugin. Exact Value supports all eleven ps5debug-NG value types used by the PS5 plugin: signed/unsigned 8-, 16-, 32-, and 64-bit integers, Float, Double, and Array of Bytes. Ordered/delta predicates are offered only for fixed-width comparable Value Types, Fuzzy Value is Float/Double-specific, and operand-free snapshot predicates require a fixed width. Current result rows expose:

- address;
- current value;
- previous value after a refinement scan;
- value type;
- memory protection for the containing range;
- memory region or module context.

From `0.1.2.rev4`, First Scan may use a plugin-native acceleration path when `NativeValueScanning` is advertised and the session provides `INativeValueScanner`. From `0.1.2.rev5`, a plugin may also provide `INativeValueScanRefiner`; PS5 uses that optional service for compatible resident TurboScan Next Scans. From `0.1.3.rev26`, Plugin API `2.7.0` adds `INativeScanTypeMappingProvider`: a plugin explicitly declares which Core Scan Types are semantically equivalent to its native operations for the current stage/Value Type. If no mapping exists, or the native service rejects the current runtime shape, the host uses Core fallback automatically. The visible result table does not change: Core normalizes native addresses into the same result model and preserves the same alignment, readable-region, duplicate, previous-value, and storage rules.

From `0.1.3.rev30`, Plugin API `2.9.0` can keep a complete authoritative native result set backend-resident. For result counts above the 50,000-row presentation ceiling, the host loads only the bounded preview instead of forcing an immediate full transfer to local disk. Compatible native Next Scans refine that resident set in place. A predicate that requires shared Core processing first materializes the complete current resident set through the normal transactional disk-backed writer, then continues through the existing Core refinement path.

From `0.1.4.rev3`, Scan Results also has a read-only **Protection** column showing the containing `MemoryRegion.Protection` flags for the result's complete value range. The flags originate from the neutral memory map already supplied by the active plugin. Reloading the Active Target memory map refreshes the protection displayed for currently materialized rows; this does not modify target page permissions or scan membership.

The Scan Results **Value** column also refreshes live for rows currently realized by the DataGrid viewport. Row virtualization defines the refresh set: scrolling unregisters rows that leave the realized view and registers newly realized rows, so the host does not periodically reread all 50,000 preview rows. The refresh uses the same Settings-controlled **Live value refresh** interval and serialized target-read scheduler as Saved Addresses. It updates only the current Value display; **Previous**, result membership, and the Next Scan baseline are never changed by this presentation refresh.

From `0.1.3.rev13`, the complete candidate set is retained in a committed disk-backed result generation under the host-managed scan-storage session. The WPF DataGrid materializes at most the first 50,000 rows and uses row/column virtualization, while Next Scan refines the complete committed candidate set rather than only the displayed rows. The historical 2,000,000-result safety limit remains only on legacy list/in-memory materialization paths and does not cap the streaming disk-backed path.

Every First Scan uses the unique application/scan-session identities introduced in `0.1.3.rev9` and commits generation 1 only after the result file is complete. Successful Next Scans publish later generations transactionally; cancellation or failure cannot replace the previous committed generation. New Scan invalidates/releases the active host storage session before best-effort cleanup, and correctness never depends on physical deletion succeeding. PS5 native streaming can keep very large TurboScan result sets target-side while Core commits bounded batches to disk, so the UI preview remains bounded without losing candidates required by later refinement.

From `0.1.2.rev2`, result addresses omit redundant fixed-width leading zeroes while preserving their complete hexadecimal value. The current row context menu provides:

- **Save Address**;
- **Open in Memory Viewer**;
- **Open in Disassembler**;
- **Copy address**;
- **Copy value**.

When the context-clicked row belongs to a multi-row Scan Results selection, **Save Address** processes the complete selected set and skips Saved Address identities that already exist. A context-click outside the existing multi-selection does not bulk-save the unrelated prior selection. Double-click remains a single-row Save Address shortcut. **Open in Memory Viewer** is the rev4 user-facing rename of the action introduced as **Browse Memory** in `0.1.5.rev1`; its Memory Viewer behavior is unchanged. **Open in Disassembler** is new in `0.1.6.rev4`, is enabled only when the current Active Target can provide the neutral Disassembly workflow, and opens the clicked result address as the Disassembler origin. From rev6, an explicitly selected row can be edited only when the resolved region is readable+writable and non-guarded; the viewer never changes page protection.

Application `0.1.5.rev2` corrects the WPF ownership of this menu after the rev1 Windows markup build rejected code-behind `Click` handlers inside a `DataGridRow` style `Setter.Value`. The menu is now a concrete `DataGrid.ContextMenu`. `ContextMenuOpening` resolves the row under the pointer and assigns it as the menu DataContext; an already-selected Scan Result retains the existing multi-selection, while a context-click outside that selection selects only the clicked row. Rev2 then exposed `CS0136` because its helper redeclared the local pattern name `contextMenu`; `0.1.5.rev3` fixes that C# scope error by reading the DataGrid context menu once and reusing that nullable local. The established row-selection/DataContext ownership behavior remains unchanged.

From `0.1.4.rev1`, the Scan Results toolbar **Export...** action opens the shared export dialog. **All Results** reads the complete authoritative result set, including disk-backed or backend-resident candidates that are not present in the 50,000-row WPF preview. **Displayed Results** exports only the currently materialized presentation rows, and **Selected Results** exports only the selected rows when a selection exists. The dialog supports JSON, CSV, TSV, and Markdown table output with per-export column selection. From rev3, Protection is selectable in every Scan Results scope: materialized scopes use the row metadata, while complete disk-backed/backend-resident scopes resolve each address against the loaded memory map while streaming. JSON is written as indented human-readable JSON directly by the streaming writer. Scan Results export is unavailable while the active scan/result operation could replace the underlying complete set.

Double-clicking a Scan Result invokes the same **Save Address** action. Exact duplicate saves for the same target/address/Value Type select the existing Saved Address row instead of adding another copy. The former diagnostic **Change value** action is not exposed because its Raw Memory Write surface is intentionally hidden; the implementation remains available in code for development and regression work.

The result table must remain distinct from Saved Addresses. Refining or starting a scan must not implicitly delete or alter the user's persistent saved-address entries.

## Scan Controls

The full-height right-side Scan panel establishes the permanent placement for:

- scan value;
- scan type;
- value type;
- plugin-provided scan options when available;
- First Scan;
- Next Scan;
- New Scan;
- Cancel Scan.

The Value field and First/Next/New/Cancel workflow require an active target exposing both `MemoryRead` and `MemoryRegionEnumeration`. From `0.1.5.rev8`, the complete Scan control column has an additional host-level Active Target gate: until the user explicitly chooses **Set Active Target**, every interactive Scan-panel control is disabled as one surface, including Value operands, Scan Type, Value Type, plugin Scan Options, pause-scanning options, and scan action buttons. Merely selecting a process in the process picker is therefore never presented as sufficient scan context. From `0.1.3.rev30`, New Scan rebuilds the valid First Scan list in place and resolves selection by the stable Core Scan Type id after stage filtering; **Exact Value** is therefore restored even when the previous selection was a Next-only predicate that disappears from the First Scan list. From `0.1.3.rev25`, **Scan Type** is populated from the Core catalog while **Value Type** remains populated from concrete definitions supplied by the active plugin. Rev26 completes that catalog with Fuzzy Value and Unknown Initial Low Value and adds generic semantic native-mapping metadata for plugins. From `0.1.3.rev7`, optional **Scan Options** are populated through `IMemoryScanOption`; Plugin API `2.8.0` adds optional generic presentation/applicability metadata without moving platform-owned options into the host. Core owns the standard Scan Type predicates and filters them by First/Next stage and selected Value Type capability. Each plugin-owned Scan Option supplies choices, a default, Value Type applicability, and session-lock behavior, and may additionally request ChoiceList/Toggle presentation or restrict itself to selected Core Scan Types/stages. The current PS5 plugin therefore shows the Core Scan Type set compatible with its selected Value Type, its eleven implemented ps5debug-NG Value Types, Alignment as a choice list, **Little-endian byte order** as a checkbox, and Floating-point rounding only for Exact Value Float/Double scans. Value Type, Endianness, and Alignment lock once First Scan creates a session until New Scan resets it. Floating-point rounding remains configurable whenever it is visible because it affects only the current Exact Value comparison. Scan Type itself is intentionally re-enabled after each completed First/Next Scan and can be changed before the next refinement. The PS5 plugin currently reuses Plugin SDK standard definitions: signed integer types are presented as **1 Byte**, **2 Bytes**, **4 Bytes**, and **8 Bytes**, while unsigned alternatives retain the explicit **(Unsigned)** suffix. The built-in PS5 and Mock plugins both default to signed Int32, so **4 Bytes** is selected when the workspace is initialized. This is presentation/default metadata only; stable ids, signed/unsigned ranges, widths, parsing, endianness handling, and native mappings remain unchanged. Integer input accepts decimal and `0x` hexadecimal, Float/Double use invariant decimal notation, and Array of Bytes accepts exact hexadecimal sequences up to 4,096 bytes.

From `0.1.3.rev24`, the Scan Value TextBox also binds to the selected Value Type's optional `IMemoryValueInputPolicy`. Typing and paste that cannot form that type's supported syntax are blocked immediately, while useful temporary edit states remain possible. The existing `IMemoryValueType.TryParse(...)` call at scan preparation remains authoritative for completed syntax, numeric range, byte width, and target-architecture conversion. A custom Value Type that does not expose the optional policy remains freely editable rather than having host syntax guessed from its id or display name.

From `0.1.3.rev8`, the full Scan-panel content is placed inside an automatic vertical scrolling viewport. This is a host layout rule, not a PS5-specific workaround: any plugin may add enough scan controls to exceed the current workspace height. The vertical scrollbar appears only when required, horizontal scrolling is disabled, the controls continue stretching to the available panel width, and a 10-DIP gap separates the control column from the scroll track while the card retains its normal 14-DIP outer padding. The bottom scan buttons must therefore remain reachable even at the application's minimum supported window height.

First Scan requires an Active Target and loaded memory map. Next Scan requires an existing scan session. New Scan clears temporary candidates while leaving Saved Addresses untouched, asks an optional native refiner to release any retained target-side scan session, rebuilds the First Scan type list, and explicitly restores Core's default **Exact Value** selection. The selection is republished after the list replacement so WPF cannot leave the ComboBox visually blank when the valid Scan Type set changes. Cancel Scan requests cancellation without disconnecting the target. For protocol transports such as PS5/ps5debug-NG, an already-started target-side scan transaction cannot be interrupted safely by cancelling the local network read; cancellation is therefore deferred until the current native operation reaches a safe protocol boundary, after which the target-side session is cleaned up and the shared command stream remains synchronized.

From `0.1.2.rev2`, scan status/errors are no longer rendered beneath the Scan buttons. They are shown in the permanent application status bar, which also displays percentage progress and elapsed scan time. From `0.1.6.rev2`, retained through `0.1.6.rev12`, the status bar's rightmost identity field is the only persistent version/revision presentation and shows `AppInfo.DisplayVersion` (for example `0.1.6.rev12`) instead of the revision feature title. The final elapsed duration remains visible after completion/cancellation until scan state is reset. The percentage is output-only ViewModel state and is explicitly OneWay-bound to `ProgressBar.Value`; the view must never attempt to write progress values back into `PluginViewModel.ScanProgressPercentage`.

From `0.1.2.rev5`, the Scan panel follows the current scan state as a keyboard-friendly workflow. Before a successful First Scan, First Scan uses the active theme's Primary button palette and pressing **Enter** while focus is in the Value field invokes First Scan. After First Scan succeeds, First Scan returns to the normal Secondary appearance, Next Scan receives Primary emphasis, and Enter in Value invokes Next Scan. New Scan resets this state. A failed or cancelled scan does not advance the primary action.

Native PS5 list-resident First/Next Scan operations use an indeterminate progress indicator because those TurboScan paths do not provide meaningful continuous percentage progress to the client. Native Unknown Initial Value uses TurboScan snapshot mode; the protocol reports target-side progress as a sentinel-terminated sequence. The host treats the operation as blocking, drains that entire progress sequence plus summary/final status, and imposes no arbitrary progress-record-count cap before publishing results. A target-side snapshot refusal can therefore fall back to Core without desynchronizing the shared PS5 connection. Strict Float/Double Exact Next Scan uses shared Core refinement and therefore determinate progress; the explicit `ps5debug-NG tolerance (1e-6)` Exact mode can remain native. Big-endian magnitude-based native comparisons and any semantically incompatible PS5 mapping fall back to Core.

The scan value TextBox and scan action buttons continue to use the same 34-unit standard control-height metric used by the target bar.

## Saved Addresses

From `0.1.3.rev17`, the lower-left table is a functional address workspace rather than a placeholder. Application `0.1.3.rev18` refined the row actions and separated value refresh from Frozen write scheduling; `0.1.3.rev19` hardened the Frozen cadence/retry behavior; `0.1.3.rev20` coordinates direct Value editing with those background schedulers and restores scan-command state after overlapping Frozen I/O finishes; `0.1.3.rev21` queues individual row removal when Saved Address I/O is active; `0.1.3.rev22` generalizes that user-priority coordination to Freeze enable, Address/Type edits, Remove All, and target-level actions that previously could lose a click during a short Saved Address I/O window; `0.1.3.rev23` moves the Remove All prompt onto the reusable theme-aware host confirmation surface; and `0.1.3.rev24` adds reusable live-input filtering for memory-oriented text fields. It sits directly below Scan Results while the Scan panel occupies the full right side of both left-side lists. Saved rows survive First Scan, Next Scan, and New Scan for the current application/plugin workspace. They are not yet serialized as a cross-launch project/cheat table. Application `0.1.6.rev9` adds **Add Manually** to the toolbar and uses the same row model/target safety for rows created without a preceding scan result. Corrective `0.1.6.rev10` fixes the manual dialog hexadecimal parser compile path without changing this toolbar or row behavior.

A row is created from Scan Results by either:

- double-clicking the result row; or
- right-clicking the result and choosing **Save Address**.

The table exposes these directly interactive columns:

- **Frozen** — a checkbox that immediately reflects the latest requested Frozen state, then captures the current live value and repeatedly writes those bytes using the separately configured Frozen write interval once activation reaches a safe I/O boundary;
- **Description** — direct text editing;
- **Address** — direct hexadecimal address editing with optional `0x`; impossible non-hex typing/paste is blocked before commit;
- **Type** — a selector populated from the active plugin's declared Value Types;
- **Value** — live target value display plus direct value editing/writing, using an optional plugin-owned `IMemoryValueInputPolicy` for live syntax filtering and `TryParse(...)` for final validity;
- **Protection** — read-only neutral memory-map protection for the complete current value range. It updates when the map/address/type range changes and is blank when the row is inactive or no containing region can be resolved. From `0.1.4.rev5`, the Protection value uses an explicit full-height template with a vertically centered `TextBlock`, so it aligns with the neighboring editable controls instead of depending on the generated `DataGridTextColumn` presentation element.

The rev9 toolbar order is **Add Manually / Remove All / Export**. Add Manually is capability/state-driven and requires a memory-readable current Active Target plus at least one plugin Value Type. Its dialog provides functional Address, Description, Value Type, and Length controls. Fixed-size types lock Length to their declared width; variable-size types accept 1-4,096 bytes. The dialog also reserves visibly disabled planned locations for hexadecimal display preference, Binary start bit, Text Unicode/code-page settings, and Pointer/base/offset controls. These placeholders cannot modify the created row and remain disabled until their backing neutral models exist. The created row is immediately refreshed through the existing Saved Address read path and then behaves like any other row.

Saved Address value parsing and formatting reuse the selected plugin-owned `IMemoryValueType`; WPF does not maintain a second concrete Value Type catalog. The same definition may optionally expose `IMemoryValueInputPolicy`; the host never infers a custom Value Type's grammar from ids or display names. Reads use neutral `IMemoryReader`; writes use `IConcurrentMemoryWriter` when the plugin exposes that safe independent path and otherwise use neutral `IMemoryWriter`; map-capable targets are checked against cached readable/writable regions before those operations are sent. Changing Address or Type disables freeze before the changed interpretation/location is used. Editing Value while Frozen changes the captured freeze target before the manual write is queued. While the Value cell is actively edited, timer-driven display updates are held out of the editor so typed text cannot be replaced by a 500 ms refresh or 100 ms Frozen completion.

The reusable textbox filter is also used by the Scan Value editor and the two numeric Saved Addresses interval fields in Settings. Paste is evaluated with the same candidate rule as typed text. Description, storage-path, and plugin-defined connection fields remain free-form at the host level because their grammar is not universal. See [`TEXT_INPUT_VALIDATION.md`](TEXT_INPUT_VALIDATION.md).

Saved Addresses use two independent `DispatcherTimer` schedules. **Value refresh** defaults to 500 ms and rereads displayed values; **Frozen write** defaults to 100 ms and reapplies the separately captured Frozen bytes. Both accept 50-10,000 ms, persist in the shared `settings.json`, and apply immediately after Settings is saved. Value refresh is stopped for the full duration of First Scan and Next Scan. Frozen writes are stopped during a scan only when **Pause target while scanning** is enabled; otherwise they continue through the optional Plugin API `2.4.0` concurrent-writer service. When that service exists, rev19 also prefers it outside scanning so a normal refresh read does not suppress the Frozen cadence. A transient write failure leaves the row Frozen and schedules another attempt on the next Frozen interval.

Each row retains the process id/name from the Active Target that produced it. If another process is active, the row remains visible but is marked inactive and no automatic write is sent to that process. Disconnecting stops the timer and explicitly disables all active freezes so an old freeze cannot silently resume after a later connection.

The Saved Address context menu currently provides:

- **Freeze / Unfreeze**;
- **Open in Memory Viewer**;
- **Open in Disassembler**;
- **Copy address**;
- **Copy value**;
- **Remove address**.

**Open in Memory Viewer** is the current label for the navigation introduced as **Browse Memory** in `0.1.5.rev1`. It opens the Memory Viewer at the Saved Address while carrying the row's saved target-process identity. Viewer reads are accepted only while the same process and the same host connection generation remain active, preventing an old viewer from silently following a different target or reconnect. Application `0.1.6.rev4` adds **Open in Disassembler** using the same saved target identity. That item is enabled only when the saved row belongs to the current Active Target and the plugin can provide the required Disassembly/memory services; the clicked address becomes the Disassembler origin. Rev2's concrete `SavedAddressesDataGrid.ContextMenu` ownership remains unchanged, and `ContextMenuOpening` continues to assign the row DataContext before target/capability-sensitive menu state is updated.

Every row has a dedicated **Remove** button. If that button or **Remove address** is used while Saved Address refresh, Frozen enforcement, or a direct Value operation is active, the request is queued rather than rejected. The row is unfrozen immediately, no new timer work is started while removal is pending, an active multi-row timer cycle stops after its current awaited operation, and the row disappears automatically when Saved Address I/O is fully idle. The toolbar **Remove All** uses the reusable application-owned confirmation dialog from rev23. The dialog follows the active theme, uses the Danger semantic role, labels the affirmative action **Remove All**, and does not make the destructive action the Enter-key default. After confirmation, rev22 coordination remains unchanged: if Saved Address I/O is active, the rows that currently exist are marked for removal, their Frozen states are disabled immediately, and removal completes automatically at the next safe idle boundary. Freeze enable plus Address and Type commits likewise wait out transient background Saved Address work rather than requiring another click/edit. The toolbar **Export** action is active from `0.1.4.rev1`; rev9 changes only the visible toolbar label from `Export...` to `Export` and supports **All Addresses** / **Selected Addresses**, JSON/CSV/TSV/Markdown table, and column selection through the same reusable dialog used by Scan Results. Rev3 adds Protection to the Saved Address export columns and writes JSON with indentation while preserving the existing streaming/transactional pipeline. Saved Address rows are snapshotted in the host before writing, so the export does not introduce new target traffic. Memory Viewer selection/copy/history is implemented in `0.1.5.rev4`; `0.1.5.rev5` makes the selected-origin highlight layout-neutral and was fully verified, `0.1.5.rev6` adds explicit stale-checked/read-back-verified byte editing for writable viewer rows, and `0.1.5.rev7` adds viewer-local bookmarks plus readable-region navigation. Rev7 also suppresses the row-level Saved Addresses tooltip when `StatusText` is empty so unused Frozen/Protection/Remove cell space does not create an empty popup while explicit child-control tooltips remain available. Pointer work, cheat/project integration, and persistent project-owned bookmarks remain future extensions. The capability-driven Disassembler workspace began in `0.1.6.rev3`, and Saved Addresses gains direct **Open in Disassembler** navigation in `0.1.6.rev4`. Detailed behavior and safety rules are documented in `docs/architecture/SAVED_ADDRESSES.md`.


## Disassembler Workspace

Application `0.1.6.rev14` is the fully tested and hardware-verified final implementation of the architecture-neutral Disassembler workspace. Rev12 supplied the combined selection/export/region-navigation/finalization feature set; rev13 corrected Extended-selection preservation during context clicks; rev14 keeps that selection behavior and makes every granular copy command consume the complete selected set instead of only the context-clicked row.

The workspace can be opened from the Target / Connection bar through **Disassembler...**, from a Scan Result or Saved Address through **Open in Disassembler**, or from Memory Viewer through **Open in Disassembler**. Availability remains capability/service/session driven and never checks a platform name.

The current workspace presents:

- Back / Forward with Alt+Left / Alt+Right;
- hexadecimal Address, Go To, and Refresh;
- Previous Region, Region Start, Region End, and Next Region;
- Region / Module plus module-relative origin when a real module base is known;
- Visible range, Protection, and Architecture;
- virtualized Address / Bytes / Instruction rows with theme-aware syntax highlighting;
- Extended Ctrl/Shift multi-selection with right-click preservation for any row already in the selected set;
- direct Follow Target for valid provider-supplied Call/Jump/ConditionalJump targets;
- Copy Address, Copy Bytes, Copy Instruction, Copy Address + Instruction, Copy Selected, and Ctrl+C;
- universal Export with Displayed/Selected scopes and JSON/CSV/TSV/Markdown table formats;
- status/error text and the explicit variable-length initial-boundary warning.

The default context remains up to 512 bytes before and 512 bytes from the requested origin, clamped to one readable non-guarded region and decoded continuously. The row containing the requested byte is the origin even when the instruction begins before that byte.

Previous/Next Region reuse Core `MemoryViewerRegionNavigator`; Region Start uses the region base and Region End uses the final byte. All successful region moves share Go To/Follow Target Back/Forward history. Refresh remains history-neutral and failed reads retain the prior successful view.

Right-clicking a row already inside the current multi-selection keeps the full selection intact; right-clicking an unselected row intentionally replaces the old selection with that row. Follow Target remains context-row-specific, but all clipboard commands use the complete current selected set in displayed order: Copy Address, Copy Bytes, Copy Instruction, Copy Address + Instruction, Copy Selected, and Ctrl+C. When a right-click first selected an unselected row, the current set naturally contains only that row. Selected Instructions export likewise uses the complete selected set. Context-menu opening evaluates Follow Target capability without rewriting the TwoWay-bound primary selection. Copy/export uses only already materialized rows and causes no target traffic. Disassembler export uses the existing generic export dialog/writer/progress/cancellation pipeline. Structured columns include Address, Bytes, Instruction, Mnemonic, Operands, Length, Flow Control, Branch Target, Valid, Region / Module, Protection, and Module Relative.

Mock Target now displays **Custom / Unknown** rather than X64 because its deterministic disassembler uses a synthetic instruction set. PS5 continues to display X64 and decode with the plugin-owned Iced provider.

Architecture and verification details are in `docs/architecture/DISASSEMBLY_ARCHITECTURE.md` and `docs/testing/APP_0.1.6_REV14_VERIFICATION.md`.

Host `0.1.7.rev1` established the verified Debugger Plugin SDK/Core foundation. Host `0.1.7.rev2` added and fully verified the capability-driven **Debugger...** entry plus first modeless Debugger workspace against Mock. Host `0.1.7.rev3` added the first real PS5 debugger backend. Host `0.1.7.rev4` established the permanent two-row placement; host `0.1.7.rev5` keeps the same generic workspace/backend and places **Debugger...** after **Reload Plugins** and **Disassembler...** on the permanent left-aligned second target row. The entry remains visible only for plugins advertising `Debugger`, requires a valid current Active Target to open, and binds the new window to that process plus connection generation. Existing Disassembler, Memory Viewer, Scan Results, Scan controls, and Saved Addresses behavior is otherwise preserved.

## Memory Viewer

Application `0.1.5.rev1` introduces a modeless bounded Memory Viewer window reached through **Browse Memory** from Scan Results and Saved Addresses. It does not replace the main scanner workspace; it is an owned inspection surface for the currently interesting address. Corrective `0.1.5.rev2-rev3` repair the WPF/C# compile issues discovered when that foundation first reached Windows. Application `0.1.5.rev4` resumes functional viewer work with extended row selection, copy actions, Back/Forward successful-address history, and a persistent green origin-row marker that remains separate from ordinary DataGrid selection. Application `0.1.5.rev5` removes the rev4 selection-only row border after runtime verification showed it changed the origin row's measured height/width and could create a horizontal scrollbar; rev5 was subsequently user-verified. Application `0.1.5.rev6` adds the first explicit raw-byte editing path for writable rows while preserving the DataGrid itself as a read-only presentation surface. Application `0.1.5.rev7` adds viewer-local bookmarks and Previous/Next Region plus Region Start/End navigation through the neutral memory-map model. Application `0.1.5.rev8` keeps those behaviors unchanged while making the closed bookmark selector render the same address/region label as the expanded list and applying the shared Danger semantic to bookmark Remove. Host `0.1.6.rev7` adds a layout-neutral exact value-span overlay inside the existing Hex Bytes and ASCII columns. Scan Results supply `CurrentValue.Size`, Saved Addresses supply current `ValueSize`, and a span can continue across row boundaries. Manual Go To/bookmark/region navigation uses one byte; Back/Forward restores both address and span length. Host `0.1.6.rev8` keeps that behavior and explicitly OneWay-binds the six read-only inline text segments after the rev7 runtime path exposed WPF's incompatible default writable binding mode.

The viewer presents:

- Back and Forward navigation actions;
- Previous Region, Region Start, Region End, and Next Region actions;
- viewer-local bookmark Add Current / selector / Go / Remove controls;
- hexadecimal Go To address input;
- Go To and Refresh actions;
- target/plugin identity;
- Region / Module;
- containing region range;
- memory Protection;
- visible range;
- virtualized presentation-only Address / Hex Bytes / ASCII rows;
- a dynamic Read-only/Read/write access label;
- an **Edit...** action enabled only for a writable selected row.

Core reads a bounded 512-byte window and clamps that view to one readable non-guarded `MemoryRegion`. The row width is 16 bytes. The requested address's containing row is selected and scrolled into view after each successful read. Rev4 also marks that origin row with a persistent theme-aware green background; selecting one or several other rows does not remove the origin marker. Rev5 ensures selecting the origin row itself changes no layout-affecting row property, so its dimensions remain identical to the unselected green state. Region / Module follows the existing Scan Results naming rule: module name is preferred, then region name, and anonymous mappings remain blank. The DataGrid disables auto-generated model-property columns and continues to expose only Address, Hex Bytes, and ASCII.

Rev4 adds **Copy Address**, **Copy Hex Bytes**, **Copy ASCII**, **Copy Row**, and **Copy Selected** to the viewer context menu; `Ctrl+C` copies the selected rows. Back/Forward (`Alt+Left` / `Alt+Right`) navigates successful address history, Refresh does not create history, clicking rows does not navigate, and a new successful navigation after Back discards the old forward branch. Rev6 adds **Edit...** / **Edit Hex Bytes...**. The edit dialog begins from the exact displayed row bytes, requires the same byte count, blocks the write if a pre-read shows the target changed, then sends the write through the existing foreground target path and reads it back immediately. No page-protection override is provided. Rev7 adds viewer-local exact-address bookmarks and Core-owned previous/start/end/next readable-region navigation; bookmark/region destinations enter the same Back/Forward history, while add/remove bookmark operations do not. The current plan is documented in `docs/architecture/MEMORY_VIEWER.md`.

## Resizing

Two splitters are available in the permanent work area:

- a horizontal splitter between **Scan Results** and **Saved Addresses**, allowing the two left-side tables to share vertical space according to the current task;
- a vertical splitter between the left-side lists and the full-height **Scan** panel, allowing scanner-control width to be adjusted independently.

Both splitters use a 14-pixel interactive track with a centered 4-pixel visual handle, leaving 5 pixels of visible breathing room on either side. The transparent remainder of the track stays draggable, so the cleaner spacing does not reduce the resize hit area.

Resize ranges are deliberately bounded to protect the rest of the workspace while still scaling with the window:

- the left Scan Results/Saved Addresses workspace has a minimum width of **640 px**;
- the Scan panel has a preferred width of **310 px** and may be resized only between **280 px and 420 px**;
- Scan Results and Saved Addresses both use star-sized rows and therefore start at **50% / 50%** of the available left-workspace height;
- the horizontal divider constrains Scan Results to **20%–80%** of the combined table height, which automatically constrains Saved Addresses to the complementary **80%–20%** range;
- no fixed maximum pixel height is applied to either left-side table, so fullscreen and other larger window sizes can use the additional vertical space naturally.

The horizontal ratio is enforced by the reusable `ProportionalGridSplitter` host control. During horizontal dragging it normalizes the two neighboring definitions back to star sizing and clamps their proportional allocation, so the 20/80 limits apply live and the selected ratio is retained when the application window is resized. The vertical Scan-panel splitter keeps the rev8 pixel bounds because that panel does not benefit from becoming wider than its controls require.

These are usability constraints rather than fixed panel sizes. Future layout changes should preserve the same principle: protect controls from destructive resize states without unnecessarily limiting the useful range available on larger displays.

## Cheat Engine Influence

The following workflow concepts are intentionally retained because they are useful and familiar:

- select a target before memory work;
- temporary scan result list;
- dedicated scan controls;
- First Scan / Next Scan / New Scan mental model;
- separate saved-address list;
- persistent target context while working.

The application does not attempt to duplicate Cheat Engine's exact chrome, dimensions, iconography, menus, or dialog structure. The shared workflow is modernized around WPF, reusable styles, color themes, MVVM, and capability-driven platform plugins.

## Platform Neutrality

The main workspace must not branch on literal platform names such as `PlayStation 5`, `PC`, or `Xbox 360` to decide whether generic tools are available.

Availability should be driven by:

- target capabilities;
- session services;
- current connection state;
- active target state;
- current tool state.

Platform-specific controls should only be introduced through deliberate extension points when the operation cannot be represented by a shared tool contract.

## Placeholder Rule

A permanently positioned control may be shown disabled before its underlying feature is implemented when doing so establishes the intended workflow and avoids another temporary UI later.

Such controls must remain clearly disabled and must not imply that the operation is already supported. Placeholder controls should be removed from the disabled state only when their actual implementation and required verification are complete.


### Rev25-rev26 dynamic Scan input layout

The Scan panel renders operands from the selected Core Scan Type. Rev28 also treats Scan Type selection as a derived UI state of the scan lifecycle: the selector is disabled while a scan is actively running and receives an explicit property notification when scanning returns to idle, so the first post-scan click is never lost to stale WPF state. Zero-operand predicates — Unknown Initial Value, Increased Value, Decreased Value, Changed Value, and Unchanged Value — hide the Value editor. One-operand predicates — Exact Value, Fuzzy Value, Bigger Than, Smaller Than, Unknown Initial Low Value, Increased By, and Decreased By — render **Value**. Between renders **Value 1** and **Value 2** side-by-side on one row. One-operand modes keep a single full-width editor. Both Value editors use the selected plugin Value Type's live input policy. The WPF Space-key path is intercepted in addition to text composition/paste, so numeric Value and hexadecimal Address fields cannot accept whitespace while Array of Bytes retains whitespace as a valid byte separator.

Rev26 does not expose plugin-native compare names in this UI. The user always selects the Core predicate. `NativeScanTypeResolver` and the connected plugin's optional `INativeScanTypeMappingProvider` decide whether that same predicate can be accelerated natively; otherwise the shared Core scanner executes it.


> Host `0.1.7.rev7` keeps the verified two-row target header and responsive row-1 sizing unchanged. Registers and stop context live inside the modeless Debugger workspace; no register/status field is added to the permanent main header.
