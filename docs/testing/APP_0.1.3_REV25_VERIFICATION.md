# TeeKay87's Memory Engine 0.1.3.rev25 Verification

## Purpose

This checklist verifies **TeeKay87's Memory Engine 0.1.3.rev25 - Core Scan Types and Whitespace Filtering Fix**. It covers the rev24 Space-key correction, the Core-owned Scan Type catalog, dynamic operand UI, previous-value scans, PS5 native fallback, disk-backed refinement, and regression behavior from the verified scanner/Saved Addresses baseline.

## 1. Version and startup

1. Build and start the application on Windows.
2. Confirm all projects compile without warnings/errors that indicate missing public contracts or bindings.
3. Confirm the application identifies itself as `0.1.3.rev25`.
4. Confirm PS5 plugin reports `0.1.0.rev17` and Mock reports `1.0.0.rev4`; both revisions remove their production Scan Type declarations because the host now owns that catalog.
5. Confirm Plugin API reports `2.6.0` and both existing plugins load successfully.

## 2. Space-key live filtering regression

With **4 Bytes** selected:

1. Click Scan **Value** and press Space at the beginning, middle, and end of a valid number. No space must be inserted.
2. Paste text containing whitespace into the same field. Invalid whitespace must remain rejected.
3. Add a Saved Address, focus **Address**, and press Space at multiple cursor positions. No space must be inserted.
4. Confirm normal hexadecimal Address input still accepts `0-9`, `A-F`, `a-f`, and optional `0x`/`0X`.
5. Select **Array of Bytes** and enter `DE AD BE EF`. Spaces must still be accepted as valid byte separators.
6. Confirm the final Scan/Write parsers still reject incomplete/out-of-range input even when the live filter allowed an intermediate edit state.

## 3. Core-owned Scan Type list

With Mock and then PS5 connected, use a numeric Value Type such as **4 Bytes**.

Before First Scan the Scan Type list must contain:

- Exact Value
- Bigger Than
- Smaller Than
- Between
- Unknown Initial Value

After a successful First Scan the Next Scan list must contain:

- Exact Value
- Bigger Than
- Smaller Than
- Between
- Increased Value
- Decreased Value
- Changed Value
- Unchanged Value
- Increased By
- Decreased By

Confirm the list is the same Core-owned workflow for both plugins rather than being reduced to the PS5 plugin's native Exact Value capability.

## 4. Dynamic operand UI

1. Select **Exact Value**, **Bigger Than**, or **Smaller Than**. Exactly one **Value** editor must be visible.
2. Select **Between**. The Scan panel must show **Value 1** and **Value 2**.
3. Enter a lower bound greater than the upper bound and start a scan. The scan must be blocked with a clear validation error.
4. Select **Unknown Initial Value** before First Scan. No Value editor should be required.
5. After First Scan, select Increased/Decreased/Changed/Unchanged. No Value editor should be required.
6. Select Increased By or Decreased By. One Value editor must be shown for the delta amount.

## 5. Direct comparison semantics

Using a controlled Mock memory value or a known stable PS5 value:

1. Verify Exact Value matches equality.
2. Verify Bigger Than excludes equal/smaller values and retains larger values.
3. Verify Smaller Than excludes equal/larger values and retains smaller values.
4. Verify Between includes both bounds and values inside the range.

## 6. Unknown Initial Value and previous snapshots

Prefer Mock first, then repeat a practical subset on PS5.

1. Start a new scan with a fixed-width numeric Value Type and **Unknown Initial Value**.
2. Change a known target value upward.
3. Run **Increased Value** and confirm the known address survives.
4. Change it downward and run **Decreased Value**; confirm it survives.
5. Change it to any different value and run **Changed Value**; confirm it survives.
6. Leave it unchanged and run **Unchanged Value**; confirm it survives.
7. Verify the Scan Results **Previous** column reflects the preceding committed generation rather than the first scan forever.

## 7. Increased By / Decreased By

1. Establish a known previous value.
2. Increase it by exactly `10`, choose **Increased By**, enter `10`, and confirm the address survives.
3. Repeat with a different delta and confirm it is filtered out when the entered amount does not match.
4. Establish another previous value, decrease it by exactly `10`, choose **Decreased By**, enter `10`, and confirm survival.
5. Confirm negative delta input does not accidentally reverse the meaning of Increased By/Decreased By.

## 8. Value Type compatibility

1. With a numeric type, confirm all ordered/delta Scan Types above are available.
2. With **Array of Bytes**, confirm numeric ordered/delta predicates are not offered.
3. Confirm Exact Value remains available for Array of Bytes.
4. Confirm operand-free snapshot predicates (Unknown Initial Value, Changed Value, Unchanged Value) are not offered for variable-width Array of Bytes because the current Value Type cannot resolve a scan width without an input value.

## 9. PS5 native fallback

1. Run an Exact Value PS5 scan and confirm native TurboScan behavior remains available where it was previously available.
2. Start a new PS5 scan with Bigger Than, Smaller Than, Between, or Unknown Initial Value.
3. Confirm the scan does not fail merely because ps5debug-NG only accelerates Exact Value.
4. Confirm the status transitions from attempted native acceleration to shared Core scanning when appropriate.
5. Confirm Next Scan with Changed/Increased/etc. similarly falls back to shared disk-backed Core refinement instead of returning a user-visible NotSupported failure.
6. Confirm the PS5 connection remains usable after these fallbacks.

## 10. Disk-backed complete-set behavior

For a scan producing more than the 50,000-row UI preview:

1. Run a First Scan that commits a large result set.
2. Apply a previous-value predicate or direct comparison that should retain a known address beyond the visible preview.
3. Confirm Next Scan refines the complete disk-backed generation, not only displayed rows.
4. Cancel a Next Scan during refinement and confirm the previous committed generation remains authoritative.
5. Confirm no new giant in-memory previous-value collection is introduced (watch process memory while refining a very large result set).

## 11. Scan session state and controls

1. Confirm Value Type remains locked after First Scan until New Scan.
2. Confirm Scan Type remains changeable among valid Next Scan predicates while not actively scanning.
3. Confirm New Scan restores First Scan Scan Types and Exact Value as the default Core Scan Type.
4. Confirm First/Next/New/Cancel command enabled states still follow scan lifecycle correctly.
5. Confirm Enter submits scans from visible Value/Value 2 editors.

## 12. Saved Addresses and rev22-rev24 regressions

1. Confirm Saved Address refresh and Frozen cadence continue operating after the Scan Type changes.
2. Confirm explicit Disconnect/Remove/Remove All/user writes retain the rev22 queued user-intent behavior.
3. Confirm the rev23 themed confirmation dialog still follows Light/Dimmed/Dark.
4. Confirm Saved Address Value typing still uses the selected Value Type policy and direct writes remain range-validated.

## 13. Automated verification executable

Run the dependency-free verification executable and confirm every check passes. Rev25 adds Core catalog/comparison coverage on top of the existing suite. Record the exact final PASS count and any runtime/live-PS5 timings in a later verification-result document rather than altering historical revision checklists.

## Completion rule

Do not advance the application version solely because rev25 was implemented. Once rev25's input validation and Core Scan Type subsystem are confirmed working on Windows and the required PS5 paths have been live-verified, the `0.1.3` feature block can be treated as complete and the next development block can begin at **0.1.4.rev1**.
