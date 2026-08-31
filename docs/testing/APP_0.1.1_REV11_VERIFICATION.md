# 0.1.1.rev11 PS5 Memory Map Enumeration Verification

## Purpose

This document records project-wide verification for **TeeKay87's Memory Engine 0.1.1.rev11 - PS5 Memory Map Enumeration**.

The revision returns development focus from UI refinement to target-memory functionality. The verified rev10 control sizing, themes, splitters, and permanent Cheat Engine-inspired workspace are intentionally left unchanged except for a small functional status field that reports the Active Target memory-map load state.

PS5 wire-protocol details and live-target steps are documented separately under `docs/plugins/PS5/`, especially `MEMORY_MAP_VERIFICATION.md` and `PS5DEBUG_NG_PROTOCOL_MAPPING.md`.

## Reused Architecture

No new Core or Plugin SDK memory-map abstraction was required. The foundation already contains:

- `TargetCapabilities.MemoryRegionEnumeration`;
- `IMemoryMapProvider`;
- neutral `MemoryRegion`;
- neutral `MemoryProtection`;
- a deterministic Mock implementation and verification path.

The PS5 plugin now implements that existing contract. This is an important architectural checkpoint because a real second backend implementation can use the same boundary without adding PlayStation-specific types to Core or WPF.

## Host Behavior

When a selected process is made the Active Target and the plugin advertises `MemoryRegionEnumeration`, the host:

1. requests `IMemoryMapProvider` from the current session;
2. requests the active process memory regions;
3. sorts the returned neutral regions by base address and size;
4. retains them in `PluginViewModel.ActiveMemoryRegions`;
5. reports the number of loaded regions in the existing target status area.

If an Active Target survives a process-list Refresh, the host reloads its memory map. Disconnecting or losing the Active Target clears the cached region list and memory-map status. A memory-map failure is shown separately and does not falsely convert a successful target connection into a connection failure.

## Source-Level Verification Requirements

Release preparation verifies that:

- host `AppInfo` reports `0.1.1.rev11` and `PS5 Memory Map Enumeration`;
- PS5 plugin reports its independent version `0.1.0.rev3`;
- Plugin API remains `1.0.0`;
- Mock plugin remains `1.0.0.rev1`;
- PS5 capabilities are exactly Connect + ProcessEnumeration + MemoryRegionEnumeration;
- `Ps5TargetSession` implements the pre-existing `IMemoryMapProvider`;
- `CMD_PROC_MAPS` remains isolated inside the PS5 plugin;
- PS5 map entries are translated to neutral `MemoryRegion` values before crossing the plugin boundary;
- no new PS5-specific model appears in Core, Plugin SDK, or WPF;
- the deterministic protocol fixture verifies request PID/body length, response entry parsing, and protection translation;
- the rev10 UI style/theme/splitter infrastructure is not changed;
- XML/XAML/project files remain well formed;
- release contents exclude `bin`, `obj`, `.vs`, `.user`, and `.suo`.

## Required Windows Verification

1. Run **Build -> Rebuild Solution** and confirm no warnings or errors.
2. Run `TeeKay87.MemoryEngine.Tests` and confirm all verification checks pass.
3. Start the WPF application and verify the rev10 UI remains visually unchanged.
4. Connect to a PlayStation 5 running ps5debug-NG.
5. Confirm the real process list still loads.
6. Select the game process and choose **Set Active Target**.
7. Confirm the status area reports `Memory map: <N> regions loaded.` with a non-zero count.
8. Refresh processes and confirm the same Active Target remains active when present and its memory map reloads.
9. Disconnect and confirm Active Target and memory-map state clear.
10. Repeat with the In-Memory Test Target and confirm the same generic host path reports its deterministic one-region map.

## Preparation-Environment Limitation

The source-preparation environment does not provide the .NET Windows/WPF SDK or a physical PS5 target. Native compilation and live-target execution cannot be represented as completed here. Deterministic protocol and source-level checks are prepared for execution on the Windows development machine.

## Verification Scheduling Note

The live rev11 memory-map check was intentionally grouped with the immediately following rev12 raw-memory-read verification. This provides a stronger end-to-end result: the live test must both load a non-zero map and successfully read bytes from a range validated against that map. See `docs/testing/APP_0.1.1_REV12_VERIFICATION.md` and `docs/plugins/PS5/MEMORY_READ_VERIFICATION.md`.

## Windows / Live PS5 Result - 2026-08-30

The combined rev11/rev12 verification was completed successfully against a real PlayStation 5 running ps5debug-NG.

The real game Active Target returned a non-zero memory map through `IMemoryMapProvider`. The host accepted and cached the returned neutral `MemoryRegion` entries, retained the Active Target through process refresh, refreshed the map, and cleared map state correctly on disconnect. The same map was then used by the rev12 host validation path before live raw-memory reads.

**rev11 live PS5 memory-map verification: PASS.**
