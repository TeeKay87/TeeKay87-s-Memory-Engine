# PS5 Memory Map Enumeration Verification

## Purpose

This document defines verification for the PlayStation 5 memory-map implementation introduced with host application **0.1.1.rev11 - PS5 Memory Map Enumeration** and PS5 plugin **0.1.0.rev3**.

The feature deliberately reuses the pre-existing Plugin SDK `IMemoryMapProvider`, `MemoryRegion`, `MemoryProtection`, and `MemoryRegionEnumeration` capability. No PS5-specific memory-map type is exposed outside the PS5 plugin.

## Protocol Path

For an active PS5 process, the plugin performs:

```text
Host Active Target
    ↓
IMemoryMapProvider.GetMemoryRegionsAsync(TargetProcess)
    ↓
Ps5TargetSession
    ↓
Ps5DebugClient.GetMemoryRegionsAsync(pid)
    ↓
CMD_PROC_MAPS (0xBDAA0004)
    ↓
ps5debug-NG
```

The request uses a 4-byte PID body. The response must contain the wire success status, a bounded `uint32` region count, and exactly that many packed 58-byte map entries.

## Deterministic Verification Fixture

`Ps5ProtocolTestServer` now supports a memory-map exchange after the normal handshake and process-list exchange. The fixture verifies that the client sends PID `2222` and returns three representative regions:

1. named executable region with read/execute protection;
2. named writable data region with read/write protection;
3. unnamed read-only region.

The verification executable confirms:

- request command id and declared 4-byte body length;
- correct PID serialization;
- response count parsing;
- 58-byte entry parsing;
- start/end to base/size conversion;
- empty-name handling;
- read/write/execute protection translation;
- `IMemoryMapProvider` exposure from the connected PS5 session;
- PS5 plugin capability includes `MemoryRegionEnumeration`.

## Required Live PS5 Verification

After a successful Windows build:

1. start ps5debug-NG on the PlayStation 5;
2. connect with the PlayStation 5 plugin;
3. refresh/select the intended game process;
4. choose **Set Active Target**;
5. confirm the target status area reports `Memory map: <N> regions loaded.` with a non-zero count for the game process;
6. select another valid process and activate it, then confirm the memory-map count refreshes for that process;
7. use **Refresh** while an Active Target remains present and confirm its memory map is reloaded;
8. disconnect and confirm the active-target memory-map status and cached neutral region list are cleared;
9. confirm no PS5-specific memory-map type or wire detail appears in Core or Plugin SDK.

If the target returns a malformed map or the command fails, the host should keep the process Active Target selection but clear the cached region list and show the memory-map error separately from the successful connection/process state.

## Preparation-Environment Status

The source-preparation environment does not include the .NET Windows/WPF SDK or a physical PlayStation 5. Source-level and deterministic protocol checks can be prepared here, but native compilation and live-target verification must be completed on the Windows development machine.

## Combined Verification with Raw Read

The live memory-map test for rev11 is intentionally performed together with the rev12 raw-memory-read test. A successful combined run must report a non-zero map for the real Active Target and then successfully read bytes from a range contained in one of those readable regions. This proves more of the map data than a region-count-only check. See `MEMORY_READ_VERIFICATION.md`.

## Live PS5 Result - 2026-08-30

The combined rev11/rev12 test was completed successfully on a real PlayStation 5 running ps5debug-NG. A real game process was made the Active Target and returned a non-zero region list. The host retained the neutral map, refreshed it when the Active Target survived process refresh, and cleared it on disconnect. The map also successfully provided the readable-range validation used by the subsequent raw-memory read.

**Live PS5 memory-map result: PASS.**
