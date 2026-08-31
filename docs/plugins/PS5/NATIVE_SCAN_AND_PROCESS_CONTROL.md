# PS5 Native Scan and Process Control

## Purpose

This document describes the PlayStation 5-specific native value-scan and process-control implementation used by TeeKay87's Memory Engine `0.1.3.rev1` / PS5 plugin `0.1.0.rev9`.

The implementation provides:

- target-side **Exact Value** First Scan acceleration for every ps5debug-NG scan value type;
- target-side Exact Value Next Scan refinement through a retained server-resident TurboScan result set for compatible integer and exact Array-of-Bytes scans, with strict Core fallback for Float/Double;
- capability negotiation and scan authorization required by TurboScan;
- safe release of resident scan state on New Scan, target replacement, cancellation/failure, or disconnect;
- process suspend/resume for the optional **Pause target while scanning** workflow.

Cross-platform contracts remain in Plugin SDK/Core. This document covers only the PS5/ps5debug-NG mapping.

## Version Boundaries

```text
Host application:             0.1.3.rev1
PlayStation 5 plugin:         0.1.0.rev9
In-Memory Test Target plugin: 1.0.0.rev1
Plugin API:                   1.2.0
```

Plugin API history relevant to this subsystem:

- `1.1.0` introduced optional `INativeValueScanner` and `IProcessControl`;
- `1.2.0` adds optional `INativeValueScanRefiner`, including `ResetAsync` for target-side scan-session cleanup.

The Mock plugin intentionally remains on Plugin API `1.0.0`; the host accepts older minor versions within major version 1.

## Runtime Baseline

Before target-side acceleration, rev3 proved the shared scanner correct on a real PS5 but measured a costly full-process First Scan:

```text
Target:       eboot.bin
Memory map:   10,105 regions
Value type:   4 Bytes / Int32
Scan type:    Exact Value
Value:        10002
Results:      266
First Scan:   approximately 07:04.3
```

Rev4 moved First Scan comparison onto the target. Live rev4 measurements reduced comparable First Scans to approximately `15.1 s` and `14.5 s`, including a run returning 35,278 matches. Rev4's host-driven Next Scan measured approximately `6.6 s` in the reported refinement test.

Rev5 retains the proven First Scan path and moves compatible Next Scan refinement onto the resident TurboScan session as well.

## Why TurboScan Is Used

Memory Engine does not use legacy `CMD_PROC_SCAN` (`0xBDAA0009`) for its accelerated scanner.

TurboScan is a better fit because it supports:

1. the exact disjoint readable regions selected through neutral Core memory-map policy;
2. server-resident survivor storage;
3. server-side narrowing of that resident set;
4. bounded GET retrieval of survivor addresses/values;
5. optional rescan aliasing on servers that advertise it.

The shared Core scanner remains the correctness/compatibility fallback.

## Capability Negotiation

During PS5 connection setup, after the ordinary liveness probe, the client sends:

```text
0xBDAACC10  TurboScan CAPS
```

Successful response:

```text
CMD_SUCCESS
uint32 version
uint32 engines
uint32 max_threads
uint32 reserved
```

Memory Engine currently requires:

```text
version >= 1
TSE_SERVER_RESIDENT    = 0x00000004
TSE_SNAPSHOT_SEGMENTS  = 0x00000010
```

The client also remembers optional engine bits, including:

```text
TSE_RESCAN_ALIASING    = 0x00000200
```

If required support is absent, the connected session hides both `INativeValueScanner` and `INativeValueScanRefiner`, and the host uses the shared Core scanner.

## Scan Authorization

TurboScan START/COUNT/GET/END requires ps5debug-NG process-scan authorization.

Memory Engine uses:

```text
CMD_PROC_AUTH = 0xBDAACCFF
```

Packed request body:

```text
uint32 magic = 0xBB40E64D
uint32 flags = 0x00000002
```

The client completes the documented challenge/response exchange and caches successful authorization for the life of the connection.

## Native First Scan

### Commands

```text
0xBDAACC11  TurboScan START
0xBDAACC13  TurboScan GET
```

`END` (`0xBDAACC14`) is no longer sent immediately after every successful First Scan. In rev5, a successful resident First Scan is intentionally retained so a later Next Scan can narrow the same survivor set.

### Supported scan shape

The native path supports ps5debug-NG value-type ids `0..10`: UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float, Double, and Array of Bytes. Numeric types use their natural 1/2/4/8-byte alignment. Array of Bytes uses one-byte alignment and a variable value width up to the current 4,096-byte resident-session boundary.

Scan Type remains **Exact Value**. Array of Bytes currently sends an all-ones mask, so every supplied byte must match; wildcard syntax is not part of this revision.

### START request

The packed 27-byte body is:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 address       // ignored in segmented resident mode
0x0C    4     uint32 length        // ignored in segmented resident mode
0x10    1     uint8 valueType      // 0..10, selected ps5debug-NG value type
0x11    1     uint8 compareType    // 0 = Exact Value
0x12    1     uint8 alignment      // selected type alignment
0x13    4     uint32 lenData       // selected value width
0x17    4     uint32 flags
```

Current flags:

```text
TS_SERVER_RESIDENT    = 0x00000002
TS_SNAPSHOT_SEGMENTS  = 0x00000010
flags                  = 0x00000012
```

The wire sequence is:

```text
client -> START request
server -> CMD_SUCCESS
client -> comparison bytes (+ same-length all-ones mask for Array of Bytes)
server -> CMD_SUCCESS
client -> uint32 segment_count
client -> segment_count x { uint64 address, uint32 length }
server -> resident summary
server -> CMD_SUCCESS
```

Numeric comparison bytes use target endianness; PS5 is little-endian. Array of Bytes is transmitted byte-for-byte.

### Segment construction

Core selects neutral scannable regions first. The PS5 plugin then:

- keeps only readable/non-Guarded ranges supplied by Core;
- aligns starts according to the selected value type;
- includes the full selected value width for every candidate-start range;
- splits a range when necessary to fit the protocol's `uint32 length` field without losing cross-boundary multi-byte candidates;
- sends disjoint segments without scanning gaps between them.

### Resident summary

The packed summary is:

```text
uint32 resident_stored
uint64 count
```

`resident_stored == 1` means the result set is available for GET and compatible later resident refinement. Float/Double retain the resident result only until Next Scan begins, at which point the plugin ends it and requests shared Core refinement to preserve strict equality semantics.

If the server declines resident storage, Memory Engine consumes the documented fallback framing and reports native acceleration unavailable so the host can run the unchanged shared Core First Scan.

## GET Result Retrieval

GET uses:

```text
0xBDAACC13
```

Packed request:

```text
uint32 start_index
uint32 count
uint32 flags
```

For a selected value width `N`, normal list-session records use:

```text
uint64 address
byte current_value[N]
byte previous_value[N]
```

When response-header bit 31 is set, another `N` bytes contain the retained first value. Memory Engine derives record width dynamically, caps temporary GET payloads to approximately 4 MiB, validates returned current data against the requested scan value, enforces the common 2,000,000-result safety ceiling, and returns neutral absolute addresses to Core.

Core remains responsible for final alignment, region containment, duplicate suppression, and construction of common `MemoryScanResult` instances.

## Native Next Scan Refinement

Rev5 adds the optional Plugin SDK `INativeValueScanRefiner` path.

### Command

```text
0xBDAACC12  TurboScan COUNT
```

The packed 22-byte request is:

```text
Offset  Size  Field
0x00    4     uint32 pid
0x04    8     uint64 base_address  // 0 for current resident list path
0x0C    1     uint8 valueType      // selected ps5debug-NG value type
0x0D    1     uint8 compareType    // 0 = Exact Value
0x0E    4     uint32 lenData       // selected value width
0x12    4     uint32 flags
```

Required flag:

```text
TS_SERVER_RESIDENT = 0x00000002
```

When CAPS advertised `TSE_RESCAN_ALIASING`, rev5 also sends:

```text
TS_RESCAN_ALIASING = 0x00000100
```

After the initial success acknowledgement, the client sends the new Exact Value bytes and, for Array of Bytes, the same-length all-ones mask. A resident COUNT response is:

```text
zero or more uint64 progress records
uint64 0xFFFFFFFFFFFFFFFF sentinel
uint64 new_survivor_count
CMD_SUCCESS
```

For the current resident list session created by Exact Value First Scan, ps5debug-NG emits an immediate sentinel, so there is no meaningful percentage stream. The host therefore uses indeterminate progress for native Next Scan.

After COUNT completes, Memory Engine fetches the new survivor set through GET. Core validates returned addresses against the previous host candidate dictionary and creates the next common result set with:

```text
CurrentValue  = newly requested Exact Value
PreviousValue = preceding host result's CurrentValue
```

This keeps common result semantics in Core rather than trusting backend-specific stored-value layout.

### Fallback rules

Native refinement is used only while target and host scan state agree **and** the selected value type can preserve Memory Engine's strict Exact Value semantics in ps5debug-NG resident narrowing. Current integer types and exact Array of Bytes qualify; Float/Double do not because current ps5debug-NG resident equality is fuzzy for floating-point values.

Before COUNT, the PS5 client verifies:

- the selected type is not Float/Double for strict Exact Value refinement;
- an active resident TurboScan session exists;
- it belongs to the same PID;
- the resident survivor count equals the host's previous candidate count.

If these conditions are not met, including the Float/Double strict-semantics guard, the plugin closes the resident session and reports native refinement unavailable. The host then uses the existing shared Core Next Scan.

If an error/cancellation occurs after COUNT has started, the server-side survivor set may already have mutated. The plugin therefore closes that resident session before returning the failure. Host results remain preserved; a later Next Scan can safely fall back to Core rather than continuing against divergent resident state.

## Resident Session Cleanup

`INativeValueScanRefiner.ResetAsync` maps to TurboScan END when a resident session is active:

```text
0xBDAACC14  TurboScan END
```

The host requests cleanup when:

- **New Scan** is selected;
- the Active Target is replaced;
- a native operation reaches an error/cancellation path that cannot safely preserve resident state.

Disconnect also frees server-side state as part of connection teardown.

## Cancellation and Command-Stream Integrity

ps5debug-NG uses one shared framed TCP command stream. An already-started streamed request cannot be abandoned safely while unread bytes remain.

Memory Engine therefore uses safe command boundaries:

1. observe cancellation before starting a command when possible;
2. after START/COUNT/GET is sent, consume the full current command response with a non-cancellable transport token;
3. observe caller cancellation at the next safe boundary;
4. close a resident session when its state can no longer be safely reused;
5. return cancellation only after the stream is synchronized.

The current ps5debug-NG protocol has no asynchronous TurboScan abort opcode. **Cancel Scan** can therefore appear queued while a target-side scan is running. Rev5 makes that behavior explicit in the UI immediately:

```text
Cancellation requested — waiting for the current target scan operation to finish safely...
```

This is preferable to forcibly interrupting the TCP read and recreating the command-stream corruption fixed in earlier revisions.

## Process Suspend and Resume

The PS5 process-control implementation uses:

```text
CMD_DEBUG_PROCESS_STOP = 0xBDBB0500
```

Packed request:

```text
uint32 pid
uint8  state
```

Memory Engine uses:

```text
state 0 = resume / continue
state 1 = suspend / stop
```

The session exposes these operations through neutral `IProcessControl`.

## Pause Target While Scanning

The Scan panel shows **Pause target while scanning** only when the selected plugin advertises both `ProcessSuspend` and `ProcessResume`.

The option is Off by default. When enabled:

```text
Suspend Active Target
        ↓
First Scan or Next Scan
        ↓
Resume Active Target in finally
```

Resume is attempted after successful completion, cancellation, and ordinary failure. The checkbox is locked while a scan is active.

## Deterministic Verification

Rev5 keeps all rev4 protocol checks and adds resident refinement coverage.

The loopback fixture verifies:

- TurboScan CAPS and required engine negotiation;
- scan authorization;
- segmented resident START framing;
- initial GET retrieval;
- retained resident state after successful First Scan;
- packed resident COUNT framing and comparison value;
- resident COUNT response/sentinel/count handling;
- refined GET retrieval;
- explicit END cleanup through `ResetAsync`;
- native cancellation followed by reuse of the same command connection;
- process suspend and resume framing.

The `0.1.3.rev1` verification executable contains 19 checks. New coverage exercises shared scanning across all eleven ps5debug-NG value types and PS5 native wire mapping for ids `0..10`, including Array of Bytes mask framing, while retaining all rev5 resident-refinement/cancellation/process-control checks.

## Live Verification

After Windows build/tests pass, verify the value-type expansion on a physical PS5 while retaining the established resident/cancellation/process-control checks:

```text
Representative integer widths First/Next Scan:           PASS / FAIL
Float First Scan + Core-fallback Next Scan:            PASS / FAIL
Double First Scan + Core-fallback Next Scan:             PASS / FAIL
Array of Bytes First/Next Scan:                          PASS / FAIL
First Scan correctness and expected Turbo performance:   PASS / FAIL
Next Scan native refinement correctness:                 PASS / FAIL
Next Scan timing compared with rev4 ~6.6 s observation:  PASS / FAIL
New Scan releases/restarts native session:               PASS / FAIL
Cancel status appears immediately:                       PASS / FAIL
Cancel returns safely after current target operation:    PASS / FAIL
Pause option defaults Off:                               PASS / FAIL
Pause enabled stops target during scan:                  PASS / FAIL
Target resumes after normal scan:                        PASS / FAIL
Target resumes after cancelled scan:                     PASS / FAIL
```

Record runtime results in `docs/testing/APP_0.1.3_REV1_VERIFICATION.md`.
