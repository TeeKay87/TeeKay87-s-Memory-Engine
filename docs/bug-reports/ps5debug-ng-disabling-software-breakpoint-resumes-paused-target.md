# Disabling a software breakpoint can resume an already-paused debug target

## Summary

`CMD_DEBUG_SET_BREAKPOINT` can unexpectedly resume the debugged process when it is used to disable an enabled software breakpoint while the target is already stopped. The disable request successfully restores the original instruction byte, but the handler then unconditionally continues the process before returning success.

This makes a normal breakpoint-management operation change execution state as a side effect. A client that shows the target as paused can issue a disable/remove request and find that the process is running again even though no Continue command was requested.

## Source reviewed

The behavior below was reviewed against ps5debug-NG `v1.3.0`, commit `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86`.

## Affected command

```text
CMD_DEBUG_SET_BREAKPOINT = 0xBDBB0003
```

Request body:

```text
uint32 index
uint32 enabled
uint64 address
```

The issue is in the `enabled == 0` path of `debug_set_breakpoint_handle()` in `debugger/source/debug.c`.

## Relevant server behavior

The current handler restores the saved byte and then performs a process-wide continue:

```c
proc_write_mem((uint32_t)pid, bp_addr, 1, saved_byte_slot);

if (stuck > 0) {
    int3_resume_lwps(pid, stuck_lw, stuck_n);
}
ptrace_raw(PT_CONTINUE, pid, (void *)1, 0);
```

The final `PT_CONTINUE` runs whenever the slot was enabled and had a valid address. There is no check here for whether the target was already paused before the breakpoint-disable command arrived.

## Steps to reproduce

1. Attach the debugger to a process.
2. Install a software breakpoint with `0xBDBB0003` and `enabled = 1`.
3. Pause/stop the target and keep it stopped for inspection.
4. Send `0xBDBB0003` for the same slot with `enabled = 0`.
5. Observe that the target can resume even though the client did not request Continue.

The same behavior affects a client-side Remove action when removal is implemented by disabling the backend slot.

## Expected behavior

Disabling/removing a software breakpoint should restore the original byte and clear the slot without changing a process that was already stopped before the command. If the handler temporarily needs to resume a thread/process to get past an INT3 edge case, it should restore the previous stopped/running state before replying.

## Actual behavior

The disable path executes a process-wide `PT_CONTINUE` and returns success, leaving an already-paused target running.

## Why this matters

Debugger clients normally treat breakpoint management and execution control as separate operations. A Disable or Remove button should not act like Continue. The current behavior can also create races with register inspection, disassembly, call-stack work, or any client state that assumes the process remains stopped until an explicit resume request.

## Suggested direction

Preserve the target's entry execution state around the disable operation. If it was already stopped, restore the original byte and perform any required INT3 recovery without leaving the process running. If it was running, preserving the current behavior may still be appropriate.

A separate explicit status or documented semantic would also help clients avoid treating breakpoint disable as a state-preserving operation when it is not.
