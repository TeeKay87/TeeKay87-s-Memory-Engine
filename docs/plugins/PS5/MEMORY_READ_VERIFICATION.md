# PS5 Raw Memory Read Verification

## Purpose

This document defines the PlayStation 5 plugin-specific verification for raw target-memory reads introduced in **TeeKay87's Memory Engine 0.1.1.rev12** and **PlayStation 5 plugin 0.1.0.rev4**.

The implementation uses the existing Plugin SDK `IMemoryReader` contract. No PS5 packet or backend-specific model is exposed outside the PS5 plugin.

## Implemented Protocol Path

The plugin uses ps5debug-NG `CMD_PROC_READ`:

```text
Command: 0xBDAA0002
Request body: 16 bytes
    uint32 pid
    uint64 address
    uint32 length
Response:
    uint32 success status
    length raw bytes
```

The PS5 client requires the raw wire success status `0x80000000` before receiving target bytes.

## Deterministic Protocol Verification

`TeeKay87.MemoryEngine.Tests` contains a local protocol-server fixture that verifies the complete command sequence without a physical console.

The fixture:

1. completes the ps5debug-NG identification handshake;
2. returns the deterministic process list containing `eboot.bin` with PID `2222`;
3. returns the deterministic memory map used by the rev11 fixture;
4. requires a 16-byte `CMD_PROC_READ` request body;
5. verifies that the PID is `2222`;
6. decodes the 64-bit address and 32-bit requested length at the packed protocol offsets;
7. returns a success status followed by deterministic raw bytes derived from the requested address;
8. verifies that `IMemoryReader` returns the requested byte count and the exact byte sequence.

This test verifies serialization, command id, response ordering, service exposure, and the neutral plugin boundary. It does not replace live PS5 verification.

## Host-Side Read Validation

The WPF Raw Memory Read inspector is capability-driven and not PS5-specific. For a plugin that also provides `MemoryRegionEnumeration`, the host requires the entire requested range to fit inside one cached region with `MemoryProtection.Read` before invoking `IMemoryReader`.

The current inspector accepts:

- hexadecimal addresses, with or without the `0x` prefix;
- decimal byte lengths;
- `0x`-prefixed hexadecimal byte lengths;
- lengths from 1 through 4096 bytes.

The 4096-byte maximum is a manual-inspector usability limit only. It is not a PS5 plugin, Plugin SDK, or ps5debug-NG read limit.

## Combined Live Verification for rev11 and rev12

Rev11 memory-map verification was intentionally deferred so the memory map and first actual memory read can be proven together in one stronger live test.

On the Windows development machine with a PlayStation 5 running ps5debug-NG:

1. run **Build -> Rebuild Solution** and confirm there are no warnings or errors;
2. run `TeeKay87.MemoryEngine.Tests` and confirm all checks pass;
3. start the WPF application;
4. connect to the PlayStation 5;
5. confirm the real process list loads;
6. select the game process, normally `eboot.bin`, and choose **Set Active Target**;
7. confirm the target status reports `Memory map: <N> regions loaded.` with a non-zero count;
8. expand **Raw Memory Read**;
9. confirm the Address field has been initialized to the base of a readable region when one is available;
10. leave Length at `64` and choose **Read Memory**;
11. confirm the status reports that 64 bytes were read;
12. confirm the result box contains four 16-byte hex-dump rows beginning at the requested address;
13. repeat the same read and confirm it succeeds again;
14. enter an address outside the loaded map and confirm the host blocks the request with a readable-range validation message rather than sending it to the plugin;
15. refresh the process list, retain the same Active Target, and confirm the memory map reloads;
16. perform another raw read after refresh;
17. disconnect and confirm Active Target, memory-map state, and raw-read state are cleared.

A successful run verifies both the rev11 memory-map path and the rev12 raw-read path against the real console.

## Safety of the Verification

The rev12 verification itself only reads target memory. Raw memory write was added later in host rev16 / PS5 plugin rev5 and has its own protocol and live-runtime verification procedure in `MEMORY_WRITE_VERIFICATION.md`.

## Runtime Status

The combined live verification was completed successfully on 2026-08-30. A real game process returned a non-zero memory map, the default 64-byte read produced a hexadecimal/ASCII dump, a repeated read succeeded, an invalid/unmapped range was rejected before the plugin call, refresh preserved the Active Target and subsequent read behavior, and disconnect cleared target/map/read state without an exception.

**Live PS5 raw-memory-read result: PASS.**


## Rev2 Cancellation / Session-Reuse Regression

Live `0.1.2.rev1` scanner testing identified a PS5-specific cancellation defect: cancelling a First Scan while a `CMD_PROC_READ` response was in flight could leave unread payload bytes on the command stream. The next process-list or scan request could then fail with an unexpected status such as `0xC0F6410A`. Disconnect -> Connect restored operation by creating a new stream.

PS5 plugin `0.1.0.rev6` treats an already-started raw-memory read as an atomic framed command for transport purposes. Caller cancellation is checked before the request begins; after that point the success status and exact payload are fully consumed. Core observes cancellation before issuing the next scanner read.

The deterministic verification executable includes **PS5 scan cancellation preserves command stream**. Its loopback server delays the memory-read response, the test cancels after the read request reaches the server, and the same PS5 session must immediately complete `IProcessProvider.GetProcessesAsync` afterward.

The required real-console closure is:

1. start a live PS5 First Scan;
2. choose **Cancel Scan** while it is active;
3. wait for cancellation to complete;
4. do **not** disconnect;
5. immediately Refresh processes and/or start a New Scan;
6. confirm the same session remains healthy and no malformed/unexpected status is reported.

Successful completion closes the cancellation/session-reuse regression introduced by the first live scanner test.
