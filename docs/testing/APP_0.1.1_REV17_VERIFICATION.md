# 0.1.1.rev17 Automatic Safe Write Test Verification

## Purpose

This document records the implementation and verification requirements for **TeeKay87's Memory Engine 0.1.1.rev17 - Automatic Safe Write Test**.

Rev17 is a host-only diagnostic usability revision built directly on the user-supplied complete `0.1.1.rev16 - PS5 Raw Memory Write and Read-Back` source ZIP. It does not replace or alter the rev16 PS5 write protocol implementation. Its purpose is to remove the remaining manual-address-discovery problem from the real-console write verification.

The Raw Memory Read and Raw Memory Write areas remain temporary development/verification surfaces. The new Safe Write Test is intentionally small and should not be treated as a permanent scanner or Memory Viewer feature.

## Problem Being Corrected

The rev16 live-test procedure originally required the tester to choose a known-safe address inside a readable and writable region.

That instruction was impractical in the current UI because:

- the host caches the complete neutral memory map but does not expose a region browser yet;
- the Raw Memory Write diagnostic requires an address but does not tell the user which regions are writable;
- asking the tester to guess a writable game address would be unsafe and unnecessary for a transport-level protocol test.

Rev17 moves that selection into the host.

## Version Boundaries

```text
Host application:             0.1.1.rev17
Host feature title:           Automatic Safe Write Test
PlayStation 5 plugin:         0.1.0.rev5 (unchanged)
In-Memory Test Target plugin: 1.0.0.rev1 (unchanged)
Plugin API:                   1.0.0 (unchanged)
```

No Plugin SDK contract changes are required. The helper reuses:

```text
IMemoryMapProvider
IMemoryReader
IMemoryWriter
MemoryRegion
MemoryProtection
```

## Automatic Candidate Rules

A candidate region is eligible only when all of the following are true:

```text
Size >= 4 bytes
Read    = true
Write   = true
Execute = false
Guard   = false
```

The algorithm is platform-neutral. It does not branch on PlayStation 5, ps5debug-NG, process names, or PS5-specific memory-map fields.

For each candidate, the host chooses a four-byte address inside the region rather than relying on the region boundary itself.

## Stability Gate

The host must not write merely because a region is writable.

Before a candidate can be used:

1. read four bytes;
2. wait briefly;
3. read the same four bytes again;
4. require exact equality;
5. wait briefly;
6. read the same four bytes a third time;
7. require exact equality again.

Candidates that cannot be read or whose bytes change are skipped.

Immediately before writing, the host performs one final read. If those bytes differ from the stable sample, the test aborts and **no write is attempted**.

This stability gate does not mathematically prove that an address is semantically harmless. It reduces the chance of selecting actively changing data and is combined with same-byte write-back so the diagnostic does not intentionally modify the target value.

## Same-Byte Write Test

After the final pre-write read succeeds:

```text
Current target bytes
        ↓
Write exactly those same bytes through IMemoryWriter
        ↓
Read the same address through IMemoryReader
        ↓
Compare byte-for-byte
        ↓
PASS / FAIL
```

The host populates the existing Raw Memory Write Address and Bytes fields with the chosen address/data so the exact operation remains visible.

A successful automatic test should therefore show:

```text
Original:     AA BB CC DD
Requested:    AA BB CC DD
Read-back:    AA BB CC DD
Verification: PASS
```

No deliberate replacement value is introduced and no restore operation is required.

## Preservation Requirements

Rev17 must preserve:

- PS5 connection and identification;
- real process enumeration;
- Selected Process versus explicit Active Target separation;
- live-verified memory-map enumeration;
- live-verified raw memory reads;
- rev16 `CMD_PROC_WRITE` transport;
- the existing manual `Write + Verify` workflow;
- writable-range validation;
- read/write command mutual exclusion;
- disconnect/process-refresh/target-change command coordination;
- the Mock plugin;
- Plugin API `1.0.0`;
- PS5 plugin `0.1.0.rev5`;
- the permanent scanner workspace;
- Scan Results/Saved Addresses separation;
- themes, splitters, 34-unit interactive control heights, and rev15 TextBox content correction.

## Windows Build Verification

1. Open the supplied rev17 solution in Visual Studio.
2. Run **Build -> Rebuild Solution**.
3. Confirm zero compile errors.
4. Run `TeeKay87.MemoryEngine.Tests`.
5. Confirm all existing verification checks still pass.
6. Start the WPF application.
7. Confirm the window/version surfaces show `0.1.1.rev17`.
8. Confirm the PS5 plugin still reports `0.1.0.rev5`.
9. Confirm Raw Memory Write contains both **Safe Write Test** and the existing **Write + Verify** action.

## Live PS5 Verification

1. Start ps5debug-NG and a game.
2. Connect to the PS5.
3. Select the game's `eboot.bin` process.
4. Choose **Set Active Target**.
5. Confirm the memory map loads successfully.
6. Choose **Safe Write Test**.
7. Confirm the test automatically fills Address and Bytes.
8. Confirm no error reports an unstable candidate.
9. Confirm the result shows identical Original, Requested, and Read-back values.
10. Confirm `Verification: PASS`.
11. Confirm the status begins with `Safe write test PASS`.
12. Run **Safe Write Test** again and confirm the session remains usable.
13. Enter `0xFFFFFFFFFFFFFFFF` and byte `00` in the manual write fields.
14. Choose **Write + Verify**.
15. Confirm the host rejects the range using the loaded memory map and does not disconnect.
16. Perform a Raw Memory Read from a known readable address and confirm it still succeeds.
17. Disconnect and confirm process, Active Target, map, read, and write diagnostic state clears without an exception.

## Pass Criteria

```text
Windows rebuild:                       PASS
Existing verification executable:      PASS
Safe Write Test visible/enabled:       PASS
Automatic neutral RW selection:        PASS
Execute/Guard exclusion:               PASS
Stability gate before write:            PASS
Same-byte live PS5 write/read-back:    PASS
Unmapped write blocked by host:        PASS
Session usable after both tests:       PASS
Disconnect cleanup:                    PASS
```

## Recorded Runtime Result — 2026-08-31

The rev17 Safe Write Test was subsequently exercised against a real PlayStation 5 with `eboot.bin` selected as the Active Target. The loaded target memory map contained 9,301 regions during the captured verification run.

The automatic Safe Write Test selected address `0xA069FFC`, captured `00 00 00 00`, wrote the same four bytes back unchanged, read the same range again, and reported:

```text
Original:     00 00 00 00
Requested:    00 00 00 00
Read-back:    00 00 00 00
Verification: PASS
```

The user ran **Safe Write Test twice**, and both runs returned `PASS`. The session remained usable. This closes the remaining live PS5 raw-write/read-back verification required for the `0.1.1` low-level target-access milestone.

Runtime status for the core rev17 objective: **PASS**.

## Next Step After PASS

The Safe Write Test has now produced repeated real-console `PASS` results, so the complete low-level target-access chain is live-verified:

```text
Connect
→ Process Enumeration
→ Active Target
→ Memory Map
→ Raw Memory Read
→ Raw Memory Write
→ Immediate Read-Back Verification
```

The planned `0.1.1` low-level target-access milestone is therefore closed. Development proceeds with `0.1.2.rev1 - Initial Memory Scanner Foundation`.
