# TeeKay87's Memory Engine 0.1.3.rev20 Verification

Application: `0.1.3.rev20 - Saved Address I/O Coordination and Scan Command State Fix`  
Plugin API: `2.4.0`  
PS5 plugin: `0.1.0.rev16`  
Mock plugin: `1.0.0.rev3`

## Purpose

Verify the two runtime races reported against rev19 without repeating the complete scanner test matrix. Rev20 changes host/WPF Saved Address scheduling, direct Value-edit coordination, command-state invalidation, and documentation. Core, Plugin SDK, PS5 plugin, Mock plugin, scan protocols, disk-backed scan storage, and native TurboScan behavior are unchanged.

## 1. Scan Command State Recovery

1. Connect to a target and save at least one address.
2. Freeze one or more Saved Addresses at a short Frozen interval such as `100 ms`.
3. Run First Scan or Next Scan with **Pause target while scanning** disabled so Frozen writes may overlap the scan end.
4. Repeat several times if necessary to exercise the timing boundary.
5. After every completed scan, confirm the valid Scan buttons become clickable without clicking the Value field, pressing Enter, changing focus, or otherwise forcing WPF command requery.
6. Confirm routine 500 ms refresh / 100 ms Frozen ticks still do not make unrelated application buttons visibly flash disabled/enabled.

Expected: background Saved Address I/O does not create periodic UI blinking, but the transition back to Saved Address idle explicitly requeries all dependent commands so no stale disabled scan state remains.

## 2. Active Value Editor Protection

1. Save a live address whose value changes in the target.
2. Click into the Saved Addresses **Value** cell and leave the editor focused for longer than the configured refresh interval.
3. Type a new value slowly enough that one or more background refresh ticks occur before pressing Enter.
4. Repeat with the row Frozen so 100 ms Frozen writes also occur while editing.

Expected: the text being typed remains intact. Background reads/writes may update internal current bytes but must not replace the active editor text.

## 3. Direct Value Write Priority

1. Keep several addresses Frozen to make the Frozen scheduler active.
2. Edit an unfrozen Saved Address Value repeatedly and press Enter.
3. Confirm every accepted edit either reports `Wrote <value> to <address>.` or reports a concrete parse/range/transport failure; the edit must not disappear merely because a background timer was active.
4. On PS5, confirm the game value changes.

Expected on plugins exposing `IConcurrentMemoryWriter`: direct Value writes prefer that independent writer and may proceed while a primary Saved Address refresh is already in flight.

Expected on plugins without a concurrent writer: future timer ticks are stopped, the explicit user write waits for already-running Saved Address background I/O to finish, then uses the primary writer.

## 4. Editing a Frozen Value

1. Freeze a Saved Address.
2. Change its Value to a different valid value and press Enter.
3. Confirm the new value is written and becomes the new Frozen target.
4. Let the target attempt to change the value for several seconds.

Expected: an older already-started Frozen write may complete first, but the manual write is serialized after it on the concurrent writer and the newly edited value remains the authoritative value reapplied by future Frozen ticks.

## 5. Stale Refresh Protection

With Saved Address refresh active, perform several direct writes while the target is changing values. Confirm an older read that started before a successful manual/Frozen write does not complete afterward and overwrite the row display with stale data. The per-row target-write generation now covers both direct and Frozen writes.

## 6. Regression

Confirm the previously verified behaviors remain unchanged:

- First Scan / Next Scan / New Scan;
- complete disk-backed result retention and 50,000-row UI preview;
- Saved Addresses survive scan-session resets;
- Frozen writes continue during scanning when Pause is Off;
- Frozen writes are suppressed while the target is intentionally paused;
- transient Frozen write failures remain Frozen and retry;
- target/process identity safety remains enforced;
- Settings intervals remain persisted and independently configurable.

## Static Preparation

Per project workflow, the preparation environment does not attempt a .NET build/runtime run. Before packaging, review every changed code path, ensure no obsolete Saved Address generation/coordination path remains, validate XAML event-handler references, version/documentation consistency, and compare all unrelated Core/plugin files against rev19.
