# TeeKay87's Memory Engine 0.1.3.rev7 Verification

## Purpose

This checklist verifies the PS5 Scan Options revision without changing the already established plugin-owned Value Type/Scan Type model or the rev6 disabled-control presentation.

Current version boundaries:

```text
Host application:             0.1.3.rev7
PlayStation 5 plugin:         0.1.0.rev12
In-Memory Test Target plugin: 1.0.0.rev3
Plugin API:                   2.1.0
```

The Mock plugin intentionally remains targeted at Plugin API `2.0.0`. The host must continue accepting it, and it must render no Scan Option controls because the new `SupportedScanOptions` interface member has an empty default implementation.

## Build and Automated Verification

From the repository root:

```powershell
dotnet clean .\TeeKay87.MemoryEngine.sln -c Release
dotnet build .\TeeKay87.MemoryEngine.sln -c Release
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final result:

```text
All 25 checks passed.
```

The added coverage verifies:

- Plugin API `2.1.0` and PS5 plugin `0.1.0.rev12` metadata;
- PS5 declaration order/defaults/applicability for Endianness, Alignment, and Floating-point rounding;
- API `2.0.0` Mock-plugin compatibility with zero Scan Options;
- big-endian value encoding in the reusable standard value implementation;
- custom shared-scanner alignment;
- strict versus ps5debug-NG-compatible relative floating-point matching;
- native TurboScan START carrying a custom alignment value;
- tolerant Float refinement remaining on the resident native COUNT/GET path;
- all prior scanner, cancellation, memory read/write, process-control, and plugin-discovery regressions.

## UI Capability Verification

### Mock plugin

1. Select **In-Memory Test Target**.
2. Verify Scan Type and Value Type continue to work as before.
3. Verify no Endianness, Alignment, or Floating-point rounding controls are shown.

Expected result: PASS. The host must not hard-code PS5 options into the Scan panel.

### PS5 plugin

Select **PlayStation 5 · ps5debug-NG**.

The Scan panel must show, in order:

1. Endianness
2. Alignment
3. Floating-point rounding
4. Pause target while scanning

Default selections:

```text
Endianness:              Little Endian
Alignment:               Default
Floating-point rounding: Strict
Pause target:            Off
```

With `4 Bytes (Signed)` selected, Endianness and Alignment must be enabled while Floating-point rounding must be disabled. With Float or Double selected, all three option selectors must be enabled. Endianness must be disabled for one-byte and Array-of-Bytes Value Types because it has no effect on those representations.

## Session Locking

Before First Scan, applicable options must be editable. After a successful First Scan:

- Value Type remains locked as before;
- Endianness is locked;
- Alignment is locked;
- Floating-point rounding is locked;
- Pause target while scanning remains independently configurable when no scan is actively running.

Choose **New Scan**. The Value Type and all applicable Scan Options must become editable again.

A failed or cancelled First Scan must not permanently lock the Scan Options.

## Endianness Live Verification

PS5/x86-64 is natively little-endian, so ordinary game values should use **Little Endian**.

For a known multi-byte integer value:

1. Scan with Little Endian and verify the expected address can be found/refined.
2. New Scan.
3. Select Big Endian and scan the same numeric text.

The Big Endian scan is expected to search the byte-swapped representation, not return the same normal PS5 address unless that byte pattern genuinely exists.

For Float/Double with Big Endian, the host must use shared Core scanning rather than ps5debug-NG native floating-point comparison.

## Alignment Live Verification

Use a known value with a multi-byte Value Type.

1. First Scan with **Default** alignment and record result count/time.
2. New Scan and choose **1 Byte** alignment.
3. Repeat the same First Scan.

Expected behavior:

- 1-byte alignment normally produces the same or more candidates because unaligned addresses are included;
- native PS5 First Scan still executes through TurboScan;
- the option locks after First Scan;
- New Scan unlocks it.

The ps5debug-NG `SCAN_START`/`TURBOSCAN_START` protocol contains an explicit `u8 alignment` field. Memory Engine sends the selected custom value directly in that field.

## Floating-Point Rounding Live Verification

Select **Float** or **Double**.

### Strict

1. Leave Floating-point rounding on **Strict**.
2. Run First Scan on a known floating-point value.
3. Change the target value and run Next Scan.

Expected behavior: First Scan may use native TurboScan, but returned fuzzy extras are filtered to strict numeric equality. Next Scan uses the shared Core refinement path to preserve strict semantics and therefore shows determinate Core progress.

### ps5debug-NG tolerance (1e-6)

1. New Scan.
2. Select **ps5debug-NG tolerance (1e-6)**.
3. Run First Scan and Next Scan on Float/Double.

Expected behavior: the plugin intentionally accepts ps5debug-NG's native relative tolerance and compatible Next Scan remains on the resident native TurboScan path. Progress remains indeterminate for the native operation.

## Regression Checks

Confirm the already verified behavior still works:

- rev6 disabled button text remains visibly dimmed in all themes;
- Enter/highlighted First Scan/Next Scan workflow is unchanged;
- Pause target while scanning remains Off by default and always resumes the target;
- Cancel Scan preserves the command stream;
- process enumeration and preferred `eboot.bin` selection are unchanged;
- Raw Memory Read/Write and Safe Write Test remain functional;
- `Change value` remains enabled only for Int32 scan results.

## Acceptance

`0.1.3.rev7` can be considered verified when the solution builds, all 25 automated checks pass, the three PS5 Scan Options render and lock correctly, and representative live PS5 tests confirm Alignment and floating-point mode behavior without regression in the existing scanner workflow.
