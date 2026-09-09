# Application 0.1.3.rev22 Verification

## Scope

This verification plan covers **TeeKay87's Memory Engine 0.1.3.rev22 - User Intent I/O Coordination**.

The revision changes host/WPF coordination only. It does not change Core scanning/storage contracts, Plugin API `2.4.0`, PS5 plugin `0.1.0.rev16`, Mock plugin `1.0.0.rev3`, ps5debug-NG protocol framing, native TurboScan behavior, or the PS5 concurrent-writer implementation.

The primary requirement is that a transient Saved Addresses refresh/Frozen operation must not make an explicit user action disappear or require a second attempt. An already-started target transaction is still allowed to finish at its existing safe boundary; the requested foreground action then continues automatically. Real foreground conflicts remain disabled and are not replayed later from a general command queue.

## Preconditions

- Build and launch `0.1.3.rev22` on the normal Windows development/test system.
- Verify the window reports `0.1.3.rev22`.
- Connect to the intended target and set an Active Target.
- Keep at least several Saved Addresses so a refresh/Frozen cycle has enough work to overlap manual actions.
- For PS5 concurrency tests, use the already verified PS5 plugin `0.1.0.rev16`.
- Where useful, temporarily lower the Saved Addresses refresh/Frozen intervals within the supported range to make overlap easy to reproduce. Restore normal settings afterward.

## 1. Disconnect during Saved Address refresh

1. Keep several Saved Addresses visible and allow normal value refresh to run.
2. Click **Disconnect** repeatedly at different points relative to the refresh cadence.
3. Specifically try to click while a row value is visibly updating.

Expected:

- one click is sufficient;
- Disconnect does not become unavailable solely because a Saved Address timer operation is active;
- if I/O is active, connection status reports that Disconnect is waiting for the current Saved Address operation;
- no new Saved Address timer cycle starts after the foreground request has been accepted;
- an already-started row operation may finish once;
- Disconnect then runs automatically;
- active Frozen rows are disabled as part of the established disconnect lifecycle;
- the user never needs to click Disconnect a second time after Saved Address I/O becomes idle.

## 2. Disconnect during Frozen enforcement and direct Value write

Test Disconnect while:

- a Frozen write is in flight;
- a direct Saved Address Value commit is in progress;
- a direct Value write and normal background refresh overlap through the PS5 concurrent writer.

Expected:

- Disconnect is accepted once;
- already-started Saved Address user/background work is allowed to finish safely;
- no later Saved Address operation starts after the foreground reservation;
- Disconnect continues automatically when all Saved Address I/O reaches idle;
- no transport/protocol error or stale Frozen reactivation occurs.

## 3. Refresh Processes and Set Active Target during Saved Address I/O

For each command:

1. create active Saved Address refresh/Frozen traffic;
2. invoke the command during that traffic;
3. observe status and completion.

Expected:

- one user action is sufficient;
- future timer work stops as soon as the foreground operation is accepted;
- any already-running row operation completes at its safe boundary;
- the requested process operation then executes automatically;
- Saved Address rows remain associated with their original target identity;
- changing Active Target does not write an old row to the new process.

## 4. First Scan and Next Scan startup during Saved Address I/O

Run both First Scan and Next Scan while a Saved Address refresh/Frozen operation is active.

Expected:

- the scan command is accepted on the first click when Saved Address I/O is the only conflict;
- status reports that scan startup is waiting if necessary;
- no new Saved Address background operation begins while startup owns the foreground reservation;
- the scan starts automatically when Saved Address I/O is safe;
- once `IsScanningMemory` is active, the temporary foreground reservation is released;
- ordinary Saved Address value refresh remains paused throughout the scan;
- with **Pause target while scanning** Off and PS5 `IConcurrentMemoryWriter` available, Frozen writes continue during the scan exactly as before;
- with Pause On, Frozen writes remain suppressed during the suspended scan and resume afterward;
- scan buttons recover their normal enabled state without requiring Enter or unrelated keyboard input.

## 5. New Scan during Saved Address I/O

Invoke **New Scan** while Saved Address I/O is active.

Expected:

- one click is sufficient;
- existing Saved Addresses are preserved;
- current Saved Address I/O reaches a safe idle boundary before target/native scan-session reset occurs;
- temporary scan results/session state are cleared as before;
- no stale native scan session remains because of the wait.

## 6. Freeze enable during background refresh

1. Use a non-Frozen row.
2. Enable Frozen while ordinary Saved Address refresh is active.

Expected:

- the request is retained instead of being rejected;
- the Frozen checkbox/context-menu immediately reflects the requested On state while target I/O is still pending;
- future timer cycles are suppressed while the explicit freeze operation is pending;
- the current background operation finishes;
- the row is refreshed/captured, receives its initial write, and becomes Frozen automatically;
- no second checkbox click is required.

## 7. Latest Freeze request wins

1. Enable Frozen.
2. Before the initial freeze-enable read/write sequence has completed, explicitly turn Frozen Off.

Expected:

- Off takes effect immediately in the UI/state;
- an already-started target write may finish once;
- the older enable request cannot set the row back to Frozen after the newer Off request;
- no subsequent Frozen timer writes occur for that row.

Repeat normal On/Off usage afterward and confirm ordinary Frozen operation remains functional.

## 8. Address edit during background I/O

Commit a valid Address edit while refresh/Frozen background I/O is active.

Expected:

- the edit is retained;
- the row waits for background Saved Address I/O rather than requiring the user to re-enter the address;
- a Frozen row is unfrozen before its address changes;
- the new address becomes authoritative and is refreshed when safe;
- invalid hexadecimal input still reports validation failure immediately and does not queue invalid work.

## 9. Value Type edit during background I/O

Change the row Type while background Saved Address I/O is active.

Expected:

- the selected plugin-declared Value Type is retained and applied after background I/O reaches idle;
- the row refreshes using the new width/format;
- unsupported/non-plugin types remain rejected;
- no stale old-type display is published after the change.

## 10. Remove All during active I/O

1. Populate multiple Saved Addresses, including at least one Frozen row.
2. Trigger Saved Address I/O.
3. Click **Remove All** and confirm Yes while I/O is still active.

Expected:

- the confirmation is sufficient;
- all rows that existed at confirmation time are marked for pending removal;
- their Frozen state is disabled immediately;
- future timer work is suppressed;
- the already-started operation may finish;
- the confirmed rows disappear automatically when all Saved Address I/O is idle;
- no second Remove All click is required.

Also verify idle Remove All still clears immediately and reports the removed count.

## 11. Individual Remove regression

Repeat the rev21 individual-row tests during:

- refresh;
- Frozen write;
- direct Value write;
- freeze enable.

Expected:

- the row is unfrozen and queued immediately;
- duplicate Remove clicks do not create duplicate work;
- multiple different rows can be pending together;
- the row is removed at the all-I/O idle boundary;
- a stale refresh completion does not republish the pending row value.

## 12. Direct Value regression

Repeat the rev20 direct Value tests:

- edit while normal refresh is active;
- edit a Frozen row;
- type slowly in the focused Value cell while 500 ms/100 ms timers are active;
- use PS5 concurrent writer where available.

Expected:

- typed text is not overwritten while the editor owns keyboard focus;
- the manual write is not lost to a background timing window;
- a Frozen row adopts the newly entered value as its new Frozen payload;
- an older refresh completion cannot overwrite a successful manual/Frozen display update.

## 13. Genuine foreground conflicts remain guarded

Verify that rev22 did **not** create a blind FIFO command queue:

- while a scan is already running, another First/Next/New Scan cannot be stacked for automatic later execution;
- Disconnect remains unavailable while the scan itself owns the target;
- two target-level foreground transitions cannot be queued on top of one another;
- a new explicit Saved Address user operation does not start after a Disconnect/Set Active Target foreground reservation has been accepted.

Expected:

Only transient Saved Address-I/O collisions are automatically coordinated. Real target-state conflicts remain governed by normal command availability.

## 14. PS5 transport regression

On a live PS5, verify:

- native First Scan still uses the primary ps5debug-NG session;
- in-scan Frozen writes still use the existing separate concurrent-writer connection when Pause is Off;
- no request/response framing corruption appears when a foreground action is requested during Saved Address activity;
- Disconnect after such activity closes normally;
- reconnect and subsequent scanning/reading/writing still work.

## Static preparation completed before packaging

Before packaging rev22:

- the full rev21 documentation inventory and complete source inventory were reviewed;
- all host command guards that referenced Saved Address I/O were audited;
- Saved Address row edit/freeze/remove paths and both timer schedulers were traced;
- the foreground reservation was added to both timer state control and the timer execution guards so a dispatcher tick already queued before a user action cannot begin new target work afterward;
- scan startup releases the reservation only after scan state becomes active, preserving established in-scan Frozen behavior;
- no Plugin SDK, Core, PS5 plugin, Mock plugin, protocol, or scan-storage format change is required by this revision; the only XAML change switches the Frozen checkbox display binding from active Frozen state to latest requested Frozen state.

Per project workflow, .NET compilation and runtime/live-target verification are performed on the normal Windows development/test system rather than in the preparation environment.
