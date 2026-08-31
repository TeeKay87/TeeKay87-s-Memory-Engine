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
- host version/revision;
- current color-theme selector.

The theme selector is part of the host application and remains available regardless of active platform.

## Target and Connection Bar

The target area replaces the earlier large plugin-development panel and is kept vertically compact so the memory workspace begins immediately after connection/process status. Standard single-line selectors, text inputs, and command buttons in this area use the shared **34-unit** `UiMetrics.StandardControlHeight`, keeping controls aligned without local height overrides.

It contains:

- platform/plugin selector;
- generic connection fields supplied by the selected plugin;
- Connect and Disconnect actions;
- plugin reload action;
- target process selector when `ProcessEnumeration` is advertised;
- process refresh action;
- explicit Set Active Target action;
- current Active Target summary;
- connection/process status and errors;
- Active Target memory-map load status/errors when `MemoryRegionEnumeration` is available;
- a collapsible Raw Memory Read inspector when `MemoryRead` is available;
- a collapsible Raw Memory Write inspector when `MemoryWrite` is available.

The underlying connection and process commands are the same generic commands verified before the layout redesign. The revision relocates them; it does not introduce a parallel connection implementation.

### Selected process versus Active Target

The process picker and Active Target remain deliberately separate.

Selecting a process only changes the current selection. It does not silently redirect future memory operations. A process becomes the Active Target only after the user explicitly chooses **Set Active Target**.

This distinction is important once memory reads/writes, scans, debugger operations, and saved addresses can operate against a live process. Setting the Active Target triggers capability-driven memory-map enumeration when available. The host caches the returned neutral regions: the Raw Memory Read inspector uses them to validate readable manual ranges, while the Raw Memory Write inspector uses them to block ranges that are not fully writable. Rev17 also uses the same neutral region list for the temporary Safe Write Test, selecting only Read + Write regions that are not Execute or Guard. None of these workflows adds a PS5-specific branch to the main window.


### Raw Memory Read inspector

When the selected plugin advertises `MemoryRead`, a collapsible **Raw Memory Read** inspector is available in the target area. It is intentionally a small known-address diagnostic surface rather than the final Memory Viewer or scanner.

The inspector accepts a hexadecimal address and a byte length, calls the neutral `IMemoryReader` service for the Active Target, and displays the returned bytes as a monospaced hexadecimal/ASCII dump. If memory-region enumeration is available, the requested range must fit completely inside a readable cached region before the read is sent to the plugin. Manual inspector reads are capped at 4096 bytes so the control remains responsive and readable; scanner buffering will use the same `IMemoryReader` contract directly and is not constrained by this UI limit.

The inspector is collapsed by default so the established target bar and CE-inspired scanner workspace retain their normal proportions when raw inspection is not being used.

### Raw Memory Write inspector

When the selected plugin advertises `MemoryWrite`, a separate collapsible **Raw Memory Write** inspector is available. It accepts a hexadecimal address and, from `0.1.2.rev2`, one signed 4-byte Int32 value in decimal form. The host encodes that value according to the active target architecture before using the neutral `IMemoryWriter` service. The inspector remains a controlled known-address diagnostic before Saved Addresses/value editing are implemented.

For map-capable targets, the complete range must be contained in a writable cached region before any plugin write is sent. When the range is also readable and `IMemoryReader` is available, the host first captures the original bytes, writes the requested bytes, reads the same range again, and reports `PASS` or `FAIL` from a byte-for-byte comparison. The result field is explicitly OneWay-bound because it is output-only ViewModel state. The rev2 UI deliberately edits one four-byte value; this does not constrain the byte-oriented shared writer contract or future Saved Addresses/Memory Viewer editing.

Read and write operations are mutually exclusive in the host command state, and process/target changes are disabled while either operation is active so an operation cannot silently switch targets mid-transaction.

From `0.1.2.rev5`, process refresh may also use the optional neutral `IForegroundProcessProvider` service when there is no previous process selection to restore. The PS5 plugin resolves that preference to `eboot.bin` when it is present, so the Target Process selector starts on the usual game process without automatically changing the Active Target. A manual process choice is preserved across refresh while that process still exists.

### Plugin details

Plugin metadata and capability badges remain available under the collapsible **Plugin details** area. They are useful for diagnostics and development but no longer consume the primary working area at all times. Empty connection/process error rows collapse completely instead of reserving unused height below the normal status line.

## Scan Results

The main upper-left table displays scanner results.

Scan Results contains temporary candidates generated/refined through shared Core semantics. From `0.1.3.rev1`, Exact Value supports all eleven ps5debug-NG value types: signed/unsigned 8-, 16-, 32-, and 64-bit integers, Float, Double, and Array of Bytes. Current result rows expose:

- address;
- current value;
- previous value after a refinement scan;
- value type;
- memory region or module context.

From `0.1.2.rev4`, First Scan may use a plugin-native acceleration path when `NativeValueScanning` is advertised and the session provides `INativeValueScanner`. From `0.1.2.rev5`, a plugin may also provide `INativeValueScanRefiner`; PS5 uses that optional service for compatible resident TurboScan Next Scans. In `0.1.3.rev1`, integer and exact Array-of-Bytes refinement remain native, while Float/Double intentionally use shared Core refinement because current ps5debug-NG resident narrowing applies fuzzy floating-point equality. The visible result table does not change: Core normalizes native addresses into the same result model and preserves the same alignment, readable-region, duplicate, previous-value, and result-limit rules.

The complete candidate set is retained in host scan state up to the current 2,000,000-result safety limit. The WPF DataGrid materializes at most the first 50,000 rows and uses row/column virtualization; Next Scan always refines the complete candidate set rather than only the displayed rows, whether refinement runs through Core or an optional native refiner.

From `0.1.2.rev2`, result addresses omit redundant fixed-width leading zeroes while preserving their complete hexadecimal value. A row context menu provides:

- **Copy address**;
- **Copy value**;
- **Change value**.

Change value transfers an **Int32** result address to Raw Memory Write and clears the diagnostic's value field so the user must explicitly enter the replacement decimal Int32. It does not modify memory by itself. Because Raw Memory Write remains Int32-only, the command is disabled for every other scan value type.

The result table must remain distinct from Saved Addresses. Refining or starting a scan must not implicitly delete or alter the user's persistent saved-address entries.

## Scan Controls

The full-height right-side Scan panel establishes the permanent placement for:

- scan value;
- scan type;
- value type;
- First Scan;
- Next Scan;
- New Scan;
- Cancel Scan.

The Value field and First/Next/New/Cancel workflow require an active target exposing both `MemoryRead` and `MemoryRegionEnumeration`. From `0.1.3.rev1`, **Scan Type remains fixed to Exact Value**, while Value Type is an active selector containing the complete ps5debug-NG scan type set. Value Type is editable before First Scan and locked while a scan session exists; New Scan unlocks it. Integer input accepts decimal and `0x` hexadecimal, Float/Double use invariant decimal notation, and Array of Bytes accepts exact hexadecimal sequences up to 4,096 bytes.

First Scan requires an Active Target and loaded memory map. Next Scan requires an existing scan session. New Scan clears temporary candidates while leaving Saved Addresses untouched and also asks an optional native refiner to release any retained target-side scan session. Cancel Scan requests cancellation without disconnecting the target. For protocol transports such as PS5/ps5debug-NG, an already-started target-side scan transaction cannot be interrupted safely by cancelling the local network read; cancellation is therefore deferred until the current native operation reaches a safe protocol boundary, after which the target-side session is cleaned up and the shared command stream remains synchronized.

From `0.1.2.rev2`, scan status/errors are no longer rendered beneath the Scan buttons. They are shown in the permanent application status bar, which also displays percentage progress and elapsed scan time. The final elapsed duration remains visible after completion/cancellation until scan state is reset. The percentage is output-only ViewModel state and is explicitly OneWay-bound to `ProgressBar.Value`; the view must never attempt to write progress values back into `PluginViewModel.ScanProgressPercentage`.

From `0.1.2.rev5`, the Scan panel follows the current scan state as a keyboard-friendly workflow. Before a successful First Scan, First Scan uses the active theme's Primary button palette and pressing **Enter** while focus is in the Value field invokes First Scan. After First Scan succeeds, First Scan returns to the normal Secondary appearance, Next Scan receives Primary emphasis, and Enter in Value invokes Next Scan. New Scan resets this state. A failed or cancelled scan does not advance the primary action.

Native PS5 First Scan and native resident Next Scan use an indeterminate progress indicator because list-resident TurboScan operations do not provide meaningful continuous percentage progress to the client. Float/Double Next Scan uses shared Core refinement and therefore shows determinate percentage progress; other Core fallback operations do the same when real processed/total counts are available.

The scan value TextBox and scan action buttons continue to use the same 34-unit standard control-height metric used by the target bar.

## Saved Addresses

The lower-left table is the persistent address workspace, analogous to the address table used by established memory tools. It sits directly below Scan Results while the Scan panel occupies the full right side of both left-side lists.

Future rows are expected to support operations such as:

- user description;
- address display;
- value type;
- current value;
- freeze/active state;
- editing;
- removal;
- memory browsing;
- disassembly/navigation;
- pointer work;
- cheat/project integration;
- export.

The table currently exists as a disabled/empty UI foundation. Add, Edit, Remove, and Export controls are intentionally unavailable until the corresponding data model and behavior are implemented.

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
