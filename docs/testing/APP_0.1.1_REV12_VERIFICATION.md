# 0.1.1.rev12 PS5 Raw Memory Read Verification

## Purpose

This document records project-wide verification requirements for **TeeKay87's Memory Engine 0.1.1.rev12 - PS5 Raw Memory Read**.

The revision extends the existing vertical target-access path from connection -> process enumeration -> Active Target -> memory map to the first actual target-memory access operation. The permanent UI foundation from rev10 and the splitter behavior verified in rev9 remain unchanged except for the addition of a collapsible capability-driven Raw Memory Read inspector in the target area.

PS5 wire-protocol details and live-console steps are documented in `docs/plugins/PS5/MEMORY_READ_VERIFICATION.md` and `docs/plugins/PS5/PS5DEBUG_NG_PROTOCOL_MAPPING.md`.

## Reused Architecture

No new Core or Plugin SDK abstraction is required. The foundation already contains:

- `TargetCapabilities.MemoryRead`;
- `IMemoryReader`;
- `TargetProcess`;
- `MemoryRegion` / `MemoryProtection` for optional host-side range validation;
- a deterministic Mock implementation of `IMemoryReader`.

The PS5 plugin now becomes the second implementation of the same `IMemoryReader` contract. This is another architecture checkpoint: raw target-memory access does not require a PlayStation-specific Core service or WPF packet model.

## Host Behavior

When the selected plugin advertises `MemoryRead`, the host exposes the collapsible Raw Memory Read inspector. A read requires an Active Target.

For plugins that also advertise `MemoryRegionEnumeration`, the current Active Target memory map is used to validate that the complete requested range is readable before the plugin call. The host then asks the session for `IMemoryReader`, reads into a neutral byte buffer, validates the returned byte count, and formats the result as a 16-byte-per-line hexadecimal/ASCII dump.

The manual inspector is capped at 4096 bytes per request. This prevents the diagnostic control from being used as an accidental large-buffer viewer and does not constrain scanner internals or the Plugin SDK contract.

Process Refresh, Set Active Target, and Disconnect are disabled while a manual read is in progress so the single active session is not intentionally redirected or disposed during that operation.

## Version Boundaries

Expected versions for this revision:

```text
Host application:  0.1.1.rev12
PS5 plugin:        0.1.0.rev4
Mock plugin:       1.0.0.rev1
Plugin API:        1.0.0
```

Core and Plugin SDK source should remain unchanged from rev11.

## Source-Level Verification Requirements

Release preparation must verify that:

- `AppInfo` reports `0.1.1.rev12` and feature title `PS5 Raw Memory Read`;
- PS5 plugin metadata reports independent version `0.1.0.rev4`;
- PS5 capabilities include Connect + ProcessEnumeration + MemoryRegionEnumeration + MemoryRead and no unimplemented write/scan/debug capabilities;
- Plugin API remains `1.0.0`;
- Mock plugin remains `1.0.0.rev1`;
- `Ps5TargetSession` implements the pre-existing `IMemoryReader` contract;
- `CMD_PROC_READ` remains isolated to the PS5 plugin;
- the packed request layout is 4-byte PID + 8-byte address + 4-byte length;
- raw response bytes cross the plugin boundary only through the neutral destination buffer;
- the deterministic protocol fixture verifies command serialization and returned bytes;
- the host Raw Memory Read UI is gated by the generic `MemoryRead` capability rather than a PS5 platform-name check;
- host range validation uses neutral cached `MemoryRegion` / `MemoryProtection` values;
- raw-read state is cleared when Active Target/session state is cleared;
- XAML/project XML and theme JSON remain valid;
- release contents exclude `bin`, `obj`, `.vs`, `.user`, and `.suo` artifacts.

## Preparation Verification Result

Release preparation completed **484 static checks successfully**. The checks covered XML/XAML and JSON parsing, solution/project-reference resolution, host/plugin/API version separation, PS5 capability declarations, `CMD_PROC_READ` serialization and response handling, capability-driven host integration, deterministic protocol-fixture coverage, platform-specific type isolation, local Markdown links, release-tree cleanliness, and byte-for-byte preservation of Core, Plugin SDK, the Mock plugin, themes, shared UI resources, splitter behavior, and control metrics from rev11.

The raw-read response behavior was also checked against the referenced ps5debug-NG server source. After sending `CMD_SUCCESS`, the server transmits exactly the requested number of bytes, internally chunking requests larger than 64 KiB without introducing additional wire headers. This matches the client's exact-length receive behavior.

## Required Windows Verification

The preferred live verification combines rev11 and rev12:

1. **Build -> Rebuild Solution** with zero warnings/errors.
2. Run the verification executable and confirm every check passes.
3. Connect to a real PS5 running ps5debug-NG.
4. Load the real process list and set the game process as Active Target.
5. Confirm a non-zero memory-map region count.
6. Expand Raw Memory Read and read the default 64 bytes from the auto-selected readable region.
7. Confirm the hex/ASCII dump is populated.
8. Repeat the read.
9. Verify an unmapped/non-readable range is rejected before the plugin read.
10. Refresh the process list and verify the retained Active Target can read again.
11. Disconnect and confirm target/map/read state clears.
12. Optionally repeat with the In-Memory Test Target to verify that the same host inspector operates through the Mock plugin's existing `IMemoryReader` implementation.

## Preparation-Environment Limitation

The source-preparation environment does not provide the .NET SDK, Windows WPF build toolchain, or a physical PS5. Native compilation and live target execution therefore remain Windows-side verification items. The deterministic protocol fixture and static release checks are included so the intended behavior is reproducible and auditable.

## Observed Windows Build Result

The first Windows-side build attempt for rev12 did **not** complete successfully. Visual Studio reported the following host compile error:

```text
CS0177: The out parameter 'address' must be assigned to before control leaves the current method.
File: src/TeeKay87.MemoryEngine.App/ViewModels/PluginViewModel.cs
Method: TryParseHexAddress(...)
```

The failing implementation combined an empty-input guard and `ulong.TryParse(..., out address)` with the short-circuit `&&` operator. When the normalized address string was empty, the right-hand `TryParse` expression was skipped and the `out` parameter was therefore not assigned on that return path.

Visual Studio simultaneously reported XAML Designer errors involving `UiMetrics`, `ProportionalGridSplitter`, `System.Object`, and `DependencyProperty.UnsetValue`. Source inspection confirmed that `UiMetrics` and `ProportionalGridSplitter` are public, compiled host types and that their XAML namespaces are correct. These messages are treated as downstream Designer failures caused by the host assembly not compiling, rather than independent XAML defects.

Because the host did not build, the combined live rev11/rev12 PS5 memory-map/raw-read test was not performed on this attempt. The compile defect was corrected in **0.1.1.rev13 - Raw Memory Read Compile Fix**. The Windows rev13 rebuild then progressed to WPF startup, where a separate default-TwoWay binding on the read-only raw-memory result property caused an `InvalidOperationException`. That presentation-layer defect is corrected in **0.1.1.rev14 - Raw Memory Read Result Binding Fix**. The combined runtime steps in this document remain the required verification for the raw-read layer after rev14 starts successfully.

## Windows / Live PS5 Result - 2026-08-30

After the rev13 compile correction and rev14 binding correction, the combined rev11/rev12 verification was completed successfully against a real PlayStation 5 running ps5debug-NG.

The Raw Memory Read inspector auto-selected a readable mapped address and used the default 64-byte length. The read completed successfully and produced the expected hexadecimal/ASCII output. A repeated read also succeeded without corrupting the session. An invalid/unmapped range was rejected by the host before a plugin read was issued. After process refresh, the retained Active Target could still read successfully, and disconnect cleared the target/map/read state without an exception.

**rev12 live PS5 raw-memory-read verification: PASS.**
