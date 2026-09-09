# TeeKay87's Memory Engine 0.1.3.rev19 Verification

Application: `0.1.3.rev19 - Frozen Value Reliability and Header Alignment`  
Plugin API: `2.4.0`  
PS5 plugin: `0.1.0.rev16`  
Mock plugin: `1.0.0.rev3`

## Purpose

Verify the focused rev19 changes without repeating the complete historical scanner test matrix. Rev19 changes only host/WPF Saved Addresses behavior, Settings defaults, presentation, and documentation; Core, Plugin SDK, PS5 plugin, Mock plugin, and scan protocols are unchanged from rev18.

## Build and Existing Verification

1. Build the solution in Visual Studio.
2. Run `TeeKay87.MemoryEngine.Tests`.
3. Confirm the existing verification suite still passes.

## Scan Results Header Alignment

1. Start the application and view the main workspace.
2. Compare the **Scan Results** and **Saved Addresses** header rows.
3. Confirm **Scan Results** is vertically centered in its header just like **Saved Addresses**.
4. Confirm the disabled **Export...** button remains unchanged.

## Settings Defaults

Use a settings document that does not already contain explicit Saved Addresses interval values, or temporarily remove only those two keys for this test.

1. Open Settings.
2. Confirm **Value refresh (ms)** defaults to `500`.
3. Confirm **Frozen write (ms)** defaults to `100`.
4. Confirm both still accept `50-10,000 ms`.
5. Save custom values, restart, and confirm explicitly saved values are preserved rather than overwritten by the new default.

## Frozen Value Reliability

1. Connect to a target and save a writable address whose value naturally changes.
2. Set the Frozen interval to `100 ms`.
3. Freeze the row at a known value.
4. Let the target attempt to change the value repeatedly.
5. Confirm the value is repeatedly restored to the captured Frozen value.
6. Confirm the checkbox remains Frozen if one repeated write temporarily fails; the row should report that it will retry rather than silently unfreezing.
7. Confirm a later successful Frozen tick restores/maintains the captured value.
8. Edit the Value cell while the row is Frozen and confirm the successful edit becomes the new Frozen value.

## Refresh/Frozen Independence

This is especially important on PS5, where `IConcurrentMemoryWriter` uses a second ps5debug-NG connection.

1. Set Value refresh to a short interval such as `100 ms`.
2. Keep a writable address Frozen with Frozen write also set to `100 ms`.
3. Observe the value while both schedules are active.
4. Confirm ordinary refresh does not cause Frozen enforcement to stop for long periods.
5. Confirm the displayed value does not remain at an older read value after a successful Frozen write completed while that read was in flight.

## Scan Interaction Regression

### Pause Off

1. Keep a Saved Address Frozen.
2. Leave **Pause target while scanning** unchecked.
3. Start First Scan or Next Scan.
4. Confirm Saved Addresses display refresh pauses during the scan.
5. Confirm Frozen writes continue through the concurrent writer and the scan remains protocol-stable.

### Pause On

1. Keep a Saved Address Frozen.
2. Enable **Pause target while scanning**.
3. Start First Scan or Next Scan.
4. Confirm the process is suspended and no Frozen writes are actively sent while it is paused.
5. Confirm the process resumes and Frozen scheduling continues after scan completion/cancellation.

## Acceptance

Rev19 is ready to become the verified baseline when the solution builds, the existing verification executable passes, the header alignment is correct, and a live Frozen address reliably returns to/holds its captured value without silently unfreezing after a transient repeated-write failure.


## Runtime Follow-up Leading to rev20

Later live use of rev19 exposed two host-side coordination races that are intentionally fixed in rev20 rather than rewriting rev19 history:

- after a completed scan, the Scan-panel buttons could remain visually disabled if a Frozen write was still active at the instant scan state returned to idle; clicking the Value field and pressing Enter forced WPF to requery the commands and made them available again;
- direct Saved Address Value edits could be lost when a 500 ms refresh or 100 ms Frozen cycle overlapped the edit/commit window. In particular, background updates could replace the text of an actively edited Value cell, and `CommitSavedAddressValueAsync` rejected the write outright whenever periodic Saved Address I/O happened to be active.

These observations are the regression basis for `APP_0.1.3_REV20_VERIFICATION.md`.
