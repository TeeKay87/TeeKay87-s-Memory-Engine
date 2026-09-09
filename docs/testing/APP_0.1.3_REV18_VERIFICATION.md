# TeeKay87's Memory Engine 0.1.3.rev18 Verification

## Scope

Application: `0.1.3.rev18 - Saved Addresses Workflow Refinement`

Plugin API: `2.4.0`

PS5 plugin: `0.1.0.rev16`

Mock plugin: `1.0.0.rev3`

This revision refines the Saved Addresses UI/schedulers and introduces an optional concurrent memory-writer contract so PS5 Frozen writes can continue safely during scanning when target pausing is disabled.

## Build and Existing Verification

1. Build the complete solution in Visual Studio.
2. Run `TeeKay87.MemoryEngine.Tests`.
3. Confirm the existing verification suite completes without failure.
4. Confirm the plugin-version check reports Plugin API `2.4.0`, PS5 `0.1.0.rev16`, and Mock `1.0.0.rev3`.
5. Confirm the plugin-settings persistence check preserves both `savedAddressesUpdateIntervalMilliseconds` and `frozenWriteIntervalMilliseconds` while plugin-scoped values are modified.

## Saved Addresses UI

1. Run a scan that produces at least one visible result.
2. Save a result by double-clicking and another by the **Save Address** context-menu action.
3. Confirm a newly saved row has an empty Description field rather than the literal `No description`.
4. Confirm every row has a **Remove** button at the far right.
5. Confirm the row Remove button removes only that row.
6. Confirm the row context-menu **Remove address** action still removes only that row.
7. Add at least two rows and choose **Remove All** in the Saved Addresses toolbar.
8. Confirm a Yes/No confirmation dialog appears.
9. Choose **No** and confirm nothing is removed.
10. Choose **Remove All** again, choose **Yes**, and confirm the complete Saved Addresses collection is cleared.

## Scan Results Presentation

1. Confirm Scan Results has a disabled **Export...** toolbar button aligned with the panel title/header.
2. Confirm the old small `Export` text is absent from the lower-right footer.
3. With fewer than 50,000 visible results, confirm the footer reads `Showing <count> results of <total>.`.
4. With more than 50,000 total results, confirm the footer reads `Showing 50,000 results of <total>.` while the true total remains unchanged.
5. Confirm the previous `Results remain temporary until...` footer sentence is absent.

## Raw Memory Inspector Visibility

1. Confirm **Raw Memory Read** is no longer visible in the main UI.
2. Confirm **Raw Memory Write** is no longer visible in the main UI.
3. Confirm normal scanner, Saved Addresses, and PS5 memory-write functionality remain available.
4. Source review should confirm the raw read/write ViewModel commands and diagnostic code remain present; this revision removes only their visible XAML inspectors.
5. Confirm Scan Results no longer exposes the diagnostic **Change value** context-menu action, because that action only loaded the now-hidden Raw Memory Write surface.

## Independent Settings

1. Open Settings.
2. Confirm Saved Addresses contains separate fields for **Value refresh (ms)** and **Frozen write (ms)**.
3. Confirm both default to `500` when no stored values exist.
4. Confirm both reject values below `50` and above `10,000`.
5. Set different valid values, for example refresh `750` and Frozen `125`, then Save.
6. Reopen Settings and confirm both values persisted independently.
7. Restart the application and confirm both values persist in `%LocalAppData%\TeeKay87\MemoryEngine\settings.json`.

## Refresh Does Not Flash Global UI

1. Connect to a target and save one or more addresses.
2. Set Value refresh to a visibly frequent interval such as `100 ms`.
3. Observe Connect/Disconnect, process, scan, Settings, and other ordinary controls for several seconds.
4. Confirm periodic Saved Address value refresh updates the row values without unrelated buttons visibly flashing between disabled and enabled states.

## Refresh During Scan

1. Save at least one unfrozen address.
2. Set Value refresh to a short interval such as `100 ms`.
3. Start First Scan or Next Scan and leave it active longer than several refresh intervals.
4. Confirm the Saved Address displayed value does not refresh while the scan is active.
5. Confirm value refresh resumes after the scan returns to idle.

## Frozen During Scan - Pause Off

This is the key live PS5 test for the new concurrent writer path.

1. Connect to PS5 and set an Active Target.
2. Save a writable address whose value can be safely changed for testing.
3. Enable **Frozen** and choose a Frozen write interval such as `100 ms`.
4. Leave **Pause target while scanning** unchecked.
5. Start First Scan or Next Scan and keep it active for several Frozen intervals.
6. Confirm the Frozen value continues to be reapplied while the scan is running.
7. Confirm the scan completes normally without ps5debug-NG protocol/stream errors.
8. Confirm normal target operations remain usable after the scan.

The PS5 implementation should create its secondary concurrent ps5debug-NG connection lazily when the first in-scan Frozen write is required. The primary TurboScan connection must remain intact.

## Frozen During Scan - Pause On

1. Keep a writable Saved Address Frozen.
2. Enable **Pause target while scanning**.
3. Start First Scan or Next Scan.
4. Confirm the target is suspended as before.
5. Confirm Frozen writes are not actively sent while the paused scan is running.
6. Confirm the target resumes after scan completion/cancellation.
7. Confirm Frozen scheduling resumes after the scan returns to idle.

## Regression

Confirm the following previously verified behavior remains unchanged:

- remembered PS5 host and port;
- Platform selector displays plugin `Name` only;
- disk-backed First Scan supports more than two million results;
- Scan Results UI preview remains capped at 50,000 rows while total count is preserved;
- Next Scan refines the complete disk-backed candidate set;
- Pause target while scanning still suspends/resumes correctly;
- Saved Addresses survive First Scan, Next Scan, and New Scan;
- Saved Address Address/Type/Value editing remains functional;
- target identity prevents Saved Address writes to a different Active Target;
- disconnect disables Frozen state;
- Core scan-result storage format/lifecycle is unchanged.

## Acceptance

Rev18 can be treated as verified when the solution builds, the existing verification executable passes, the UI fixes above behave correctly, and both live PS5 Frozen scan cases behave as specified without command-stream errors.

## Runtime Follow-Up

Runtime testing of rev18 found two follow-up issues that are intentionally **not** retroactively treated as rev18 requirements:

- the **Scan Results** title was not vertically centered in its toolbar row like **Saved Addresses**;
- a transient repeated Frozen write failure could clear the row's Frozen state, and ordinary Saved Addresses refresh activity could suppress a Frozen tick even on PS5 where an independent `IConcurrentMemoryWriter` was already available.

Both findings are addressed by application `0.1.3.rev19`. Rev18's documented 500 ms Frozen default remains historically correct for that revision.
