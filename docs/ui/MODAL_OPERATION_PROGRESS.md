# Reusable Modal Operation Progress

## Purpose

TeeKay87's Memory Engine `0.1.3.rev9` introduced a generic modal progress component for host operations that must block interaction with the main window while work is in progress.

The component is intentionally not scan-specific. It is used by scan-result transfer/materialization workflows and can also be reused by future import/export, cache, project-maintenance, memory, or plugin-coordinated workflows.

## Separation of Concerns

Long-running operation code reports neutral progress through Core:

```csharp
new OperationProgress(
    Status: "Writing scan results to disk...",
    Fraction: 0.67,
    Detail: "10,862,943 / 16,211,407 results")
```

The operation does not access WPF controls.

The host presentation layer translates that model into a modal dialog. This preserves the boundary:

```text
operation
    -> IProgress<OperationProgress>
    -> WPF progress service/dialog
```

## OperationProgress Contract

`OperationProgress` contains:

- `Status` — optional primary status text;
- `Fraction` — optional progress fraction in the inclusive range `0.0` to `1.0`;
- `Detail` — optional secondary text.

A null fraction means that the operation cannot currently provide meaningful progress and the WPF progress bar is indeterminate.

Invalid fractions below zero, above one, or NaN are rejected when the model is created.

## WPF Service

`OperationProgressDialogService` exposes asynchronous `RunAsync` overloads for operations with or without a return value.

A caller supplies:

- owner window;
- dialog title;
- initial status;
- whether cancellation is supported;
- asynchronous operation delegate receiving progress and a cancellation token.

The service opens a modal owner-bound window, starts the operation after the dialog is loaded, and returns/throws only after the operation and controlled dialog closure have completed.

## Determinate Mode

When `Fraction` is non-null:

- the progress bar is determinate;
- the fraction is converted to percentage;
- percentage text is displayed;
- status/detail text can continue updating independently.

The detail text is caller-owned and can represent counts, current phase, filenames, or another useful operation-specific description.

## Indeterminate Mode

When `Fraction` is null:

- the progress bar uses WPF indeterminate mode;
- percentage text is hidden;
- status/detail text remains available.

A caller may move between indeterminate and determinate progress during a single operation by reporting another `OperationProgress` instance.

## Cancellation

Cancellation is explicitly opt-in.

When unsupported:

- the Cancel button is hidden;
- closing the dialog while work is active is prevented;
- the owner remains blocked until the operation finishes or fails.

When supported:

1. Cancel requests cancellation through the `CancellationTokenSource`;
2. the Cancel button is disabled to prevent repeated requests;
3. the dialog displays a cancellation-requested status;
4. the dialog remains open while the underlying operation reaches a safe stop;
5. only the operation can complete the cancellation path by observing the token and exiting.

The dialog never disappears while work silently continues mutating state.

## Error Propagation

Exceptions are captured only long enough to close the modal in a controlled manner. The service then propagates the exception to the caller.

This allows the calling workflow to own its error message and cleanup policy. The dialog itself does not decide whether a failed operation should retry, roll back, or display a scan-specific message.

## Theme Integration

The dialog uses existing application `DynamicResource` keys and shared button/text styles. It does not define an independent palette.

The current dialog consumes the same theme-owned resources used throughout the host, including window background, primary text, muted text, accent, input/panel/border surfaces, and the shared Danger button style for an available Cancel action.

The component must remain readable and consistent in Light, Dimmed, and Dark.

## Modality

The progress window is owner-bound and opened with `ShowDialog`. The main window cannot be interacted with while the operation is active.

This behavior is intentional for operations whose consistency depends on application state not changing underneath them.

## Scan-Result Use in Rev13

`0.1.3.rev13` uses this existing generic component for large native result transfers into the disk-backed result store. The caller supplies scan-specific title/status/detail text, but the dialog/service remains unaware of scanning or result-file formats.

When a native result stream advertises more rows than the WPF preview ceiling, the host can show determinate received/stored progress such as:

```text
Saving Scan Results
Writing scan results to disk...
10,862,943 results stored • 10,862,943 / 16,211,407 received
```

Cancellation is linked to the active scan cancellation source. The dialog does not close merely because Cancel was clicked; it remains modal until the native/storage operation reaches a safe cancellation boundary and the incomplete writer is disposed. A partial replacement generation is never promoted as the active scan result set.

## Resident Result Materialization in Rev30

Application `0.1.3.rev30` changes when this dialog is needed for native scans. A large authoritative `INativeValueScanResidentResultSet` no longer triggers an immediate full transfer merely because First Scan completed. The host loads only the bounded UI preview and keeps complete result membership in the backend.

If a later Next Scan requires Core fallback, the host opens the generic dialog as **Materializing Scan Results** and transfers the complete current resident set into the existing disk-backed writer. Progress reports the full resident count as work size and the number of records received/stored. Cancellation remains linked to the active scan cancellation source; an incomplete materialization is never committed.

After a successful complete transfer, the host commits the local generation and resets/releases the native resident session before shared Core refinement continues. The dialog still knows nothing about PS5, TurboScan, Scan Types, or the binary result format.


## Universal Export Use in 0.1.4.rev1

The shared export pipeline is another production caller of `OperationProgressDialogService`. Scan Results and Saved Addresses exports report the source row count, rows written, and determinate fractional progress while Core streams bounded batches to the destination-side temporary file.

Cancellation remains modal until the writer and current data-source batch reach a safe cancellation boundary. A cancelled or failed export deletes its temporary file and never publishes that partial content as the requested destination. If a completed destination already exists, it is left untouched unless the new export reaches successful final publication.

For backend-resident Scan Results, the modal also prevents the user from changing scan/target state while the complete resident set is being transferred. The dialog remains platform-neutral and has no knowledge of ps5debug-NG or the scan-result representation.
