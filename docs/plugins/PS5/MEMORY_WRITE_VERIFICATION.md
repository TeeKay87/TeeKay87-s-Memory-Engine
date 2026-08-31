# PS5 Raw Memory Write and Read-Back Verification

## Purpose

This document defines PlayStation 5 plugin-specific verification for raw target-memory writes introduced in **TeeKay87's Memory Engine 0.1.1.rev16** and **PlayStation 5 plugin 0.1.0.rev5**.

The implementation reuses the Plugin SDK `IMemoryWriter` contract that already existed before the PS5 write backend was added. No PS5 wire structure is added to the Plugin SDK or exposed to the WPF host.

## Protocol Under Test

The PS5 plugin uses ps5debug-NG `CMD_PROC_WRITE` (`0xBDAA0003`). The packed request body is 16 bytes:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 address
0x0C    4     uint32 length
```

The transaction is two-phase:

```text
client -> command header + 16-byte write request
server -> CMD_SUCCESS
client -> exactly length raw bytes
server -> CMD_SUCCESS
```

The plugin must consume both wire success statuses (`0x80000000`). In particular, the final status cannot be left on the TCP stream because the next command would then parse it as the beginning of a new response.

## Deterministic Fixture Verification

The loopback `Ps5ProtocolTestServer` fixture performs a complete protocol-level write/read-back sequence:

1. complete the normal ps5debug-NG identification handshake;
2. enumerate the deterministic process list and select PID `2222`;
3. return the deterministic three-region memory map;
4. receive `CMD_PROC_WRITE` and validate the 16-byte body;
5. require the expected PID and a bounded non-zero payload length;
6. send the first success acknowledgement;
7. receive exactly the declared raw payload bytes;
8. send the final success acknowledgement;
9. accept the immediately following `CMD_PROC_READ` on the same TCP connection;
10. return the bytes written at the matching address;
11. verify through neutral `IMemoryWriter` + `IMemoryReader` services that the read-back bytes exactly equal the requested bytes.

The immediate read after the write also proves stream synchronization: if the write client failed to consume the second success status, the subsequent read transaction would fail.

## Generic Host Verification

The WPF Raw Memory Write inspector is capability-driven and not PS5-specific. Host rev17 adds a **Safe Write Test** action specifically to close the live transport verification without requiring the user to discover or intentionally modify a game value.

Its expected behavior is:

- the inspector is visible only when the selected plugin advertises `MemoryWrite`;
- an Active Target is required;
- addresses are parsed as hexadecimal with or without `0x`;
- from host `0.1.2.rev2`, the temporary manual write field accepts one signed 4-byte Int32 in decimal form; the address remains hexadecimal;
- the host encodes the Int32 according to `TargetArchitecture.Endianness` before calling the unchanged byte-oriented `IMemoryWriter`;
- when a memory map is available, the complete range must fit inside one region carrying `MemoryProtection.Write`;
- a blocked/unmapped/non-writable request must not invoke the plugin;
- when the range is readable and `IMemoryReader` exists, original bytes are captured before writing;
- after writing, the same byte range is read back and compared byte-for-byte;
- the output reports Address, Original, Requested, Read-back, and Verification;
- read, write, process refresh, Active Target change, and disconnect commands cannot overlap a write transaction;
- **Safe Write Test** requires memory-map, read, and write support;
- automatic candidates must be `Read + Write` and must not carry `Execute` or `Guard`;
- the selected four-byte interior range must remain identical across repeated stability reads;
- the automatic path performs one final read immediately before writing and aborts if the candidate changed;
- the automatic path writes exactly the bytes that are already present and then performs immediate read-back verification.

## Required Windows Build Verification

Before live target testing:

1. run **Build -> Rebuild Solution** in Visual Studio;
2. confirm zero compile errors;
3. run `TeeKay87.MemoryEngine.Tests`;
4. confirm all verification checks pass, including **PS5 raw memory-write and read-back protocol**;
5. start the WPF application and confirm the current PS5 plugin shows `0.1.0.rev8` and advertises `Memory Write`;
6. confirm **Raw Memory Write** appears for PS5 and the existing read/map UI remains intact.

## Required Live PS5 Verification

Host rev17 provides the preferred live-verification path. It deliberately avoids asking the user to identify a game value or intentionally change target memory.

Recommended procedure:

1. start ps5debug-NG and a game;
2. connect to the PS5 from Memory Engine;
3. select the game's `eboot.bin` process and choose **Set Active Target**;
4. confirm the memory map loads without error;
5. expand **Raw Memory Write** and choose **Safe Write Test**;
6. confirm the host automatically selects a candidate only from a region carrying `Read + Write` while excluding `Execute` and `Guard`;
7. confirm the test does not report candidate instability. If a sampled address changes during the stability checks, the host must skip/abort without writing that candidate;
8. confirm Address is populated automatically and the decimal Value field represents the same unchanged four-byte pattern used by the test;
9. confirm the result shows identical **Original**, **Requested**, and **Read-back** byte sequences and `Verification: PASS`;
10. confirm the status reports **Safe write test PASS** and identifies the chosen address/region;
11. run **Safe Write Test** a second time and confirm the session remains synchronized and usable;
12. test host-side rejection by entering an address that is outside the loaded map, for example `0xFFFFFFFFFFFFFFFF`, with a decimal Int32 value such as `0`, then choose the manual **Write + Verify** action;
13. confirm the host blocks that request before the plugin call and the PS5 session remains connected;
14. perform a normal Raw Memory Read afterward and confirm the read path still works;
15. disconnect and confirm target/map/read/write state is cleared without an exception.

The automatic test is intentionally conservative, but no generic host can prove that an arbitrary mapped address is semantically harmless to a running target. The safety property used here is narrower and verifiable: the host excludes executable/guarded mappings, checks short-term byte stability, and writes back exactly the bytes observed immediately before the write. No deliberate value mutation or restore step is required.

## Pass Criteria

The live rev16 layer is considered verified only when all of the following are true:

```text
PS5 CMD_PROC_WRITE transport:          PASS
Automatic RW candidate selection:       PASS
Stable same-byte write -> read-back:    PASS
Host writable-range rejection:          PASS
Session remains usable:                 PASS
```

## Recorded Live Result — 2026-08-31

The controlled rev17 Safe Write Test was completed against a real PlayStation 5 with `eboot.bin` active. During the captured run, the host loaded 9,301 memory regions and selected address `0xA069FFC`. The observed operation was:

```text
Original:     00 00 00 00
Requested:    00 00 00 00
Read-back:    00 00 00 00
Verification: PASS
```

The Safe Write Test was executed **twice** and returned `PASS` both times. The command session remained usable. PS5 `MemoryWrite` is therefore live-verified in addition to deterministic protocol coverage.


## Current Cancellation Boundary

PS5 plugin `0.1.0.rev6` also makes an already-started `CMD_PROC_WRITE` transport transaction non-cancellable until both acknowledgements and the complete payload exchange have finished. This mirrors the raw-read stream-preservation rule and prevents future callers from leaving the shared ps5debug-NG command stream between write phases. The wire command and `IMemoryWriter` contract are otherwise unchanged.
