# Cheat Engine Frozen Address Behavior Reference

## Purpose

This note records the source-level behavior reviewed before the `0.1.3.rev19` Saved Addresses Frozen reliability changes. It is a design reference, not a requirement to copy Cheat Engine's UI or implementation language. Memory Engine keeps its own plugin-safe C#/WPF architecture.

## Source Baseline

Repository: `https://github.com/cheat-engine/cheat-engine`

Source revision reviewed: `ec45d5f47f92a239ba0bf51ec5d04a7509c3fd37`

Relevant files:

- `Cheat Engine/formsettingsunit.lrt`
- `Cheat Engine/MainUnit.pas`
- `Cheat Engine/addresslist.pas`
- `Cheat Engine/MemoryRecordUnit.pas`

## Observed Behavior

### Separate refresh and freeze intervals

Cheat Engine exposes an address-list update interval and a separate freeze interval. The reviewed settings resource uses:

```text
Update interval: 500 ms
Freeze interval: 100 ms
```

The freeze setting describes how often frozen addresses are reset to their captured/original frozen value.

### A dedicated repeated-freeze loop

Cheat Engine's freeze thread repeatedly invokes the address list's `ApplyFreeze` path and waits for the configured freeze interval. The address list then applies freeze to active memory records. This keeps value-display refresh and repeated freeze enforcement as separate concerns.

### Frozen value is captured and retained independently

When a normal memory record is activated/frozen, Cheat Engine obtains the current value, writes it, stores that value as the record's `FrozenValue`, and marks the record active. Later freeze passes reapply the stored `FrozenValue`. Editing a frozen record's value can replace the stored frozen value after a successful write.

### A transient repeated-write failure does not silently unfreeze

The reviewed `ApplyFreeze` path catches failures while reapplying a frozen value without clearing the record's active/frozen state. A later freeze pass can therefore retry the same stored frozen value.

## Memory Engine Adaptation

Application `0.1.3.rev19` follows the useful behavioral principles above while respecting remote/plugin transport constraints:

- Value refresh remains a separate 500 ms default schedule.
- Frozen write defaults to 100 ms and remains independently configurable.
- Each row keeps separately captured Frozen bytes.
- Successful Frozen writes update the displayed/current row value to the bytes just written.
- A generation marker prevents an older in-flight refresh completion from overwriting that write-side display.
- A transient repeated-write failure leaves `Frozen` enabled and retries on the next interval.
- Where a plugin exposes `IConcurrentMemoryWriter`, repeated Frozen writes prefer it so value-refresh reads do not delay the freeze cadence.
- During a scan with **Pause target while scanning** enabled, Frozen writes remain suppressed because the target process is intentionally suspended.
- During a scan with Pause disabled, PS5 Frozen writes continue through the independent concurrent ps5debug-NG connection introduced in rev18.

Memory Engine does not adopt Cheat Engine's process-specific implementation details, threading primitives, or UI structure.
