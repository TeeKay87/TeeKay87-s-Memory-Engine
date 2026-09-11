# GETDBREGS can block indefinitely after the debugger has already paused the target

## Summary

`CMD_DEBUG_GETDBREGS` (`0xBDBB000C`) can block indefinitely when it is called after the debugger has already paused the target process.

The problem is reproducible from the current debugger control flow: the pause operation waits for and consumes the stop notification, while `debug_getdbregs_handle` later asks `is_process_stopped()` to detect the existing stop by calling `wait4()` again. If there is no unread stop notification left, that helper reports the process as not stopped. `debug_getdbregs_handle` then sends another `SIGSTOP` to the already stopped process and waits indefinitely for a new stop transition that may never arrive.

This also blocks other debugger commands because the debug command path is executed while the server's debugger/process mutexes are held.

## Affected source

Observed against ps5debug-NG source at commit:

```text
d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86
```

Relevant files:

```text
debugger/source/debug.c
debugger/source/kern_dbreg.c
```

## Reproduction

1. Attach the debugger to a process.
2. Pause the process through the debugger continue/control command with the pause action.
3. Confirm the target is stopped.
4. Send `CMD_DEBUG_GETDBREGS` (`0xBDBB000C`) for an LWP returned by the debugger thread list.
5. Wait for the normal status + 128-byte debug-register response.

### Actual result

The GETDBREGS request can remain pending indefinitely. The process is still stopped, but the command connection does not receive the expected reply. Other debugger commands can also stop progressing while the blocked handler owns the global debug locks.

### Expected result

GETDBREGS should recognize that the process is already stopped and return the selected thread's debug-register block without waiting for a second stop transition.

## Source-level cause

The debugger pause path explicitly sends the stop signal and waits for the stop event:

```c
kill(pid, 0x11);
wait4(pid, NULL, 0, NULL);
```

`is_process_stopped()` later checks for a stop by calling `wait4()` again with non-blocking/status flags:

```c
if (wait4(pid, &status, 7, NULL) <= 0) return false;
```

`debug_getdbregs_handle()` treats a false result as proof that the process is running and performs another stop/wait sequence:

```c
kill(pid, 17);
wait4(pid, NULL, 0, NULL);
```

At that point the process can already be stopped and the earlier stop status has already been consumed. Sending `SIGSTOP` again does not necessarily create a new waitable state transition, so the blocking `wait4()` can wait indefinitely.

## Why this is especially disruptive

The main command loop serializes debugger commands behind the debugger/process mutexes. A GETDBREGS handler blocked in `wait4()` therefore does not only delay that one response; it can also prevent later debugger control commands from acquiring the same locks.

Closing the client socket is not a reliable recovery mechanism because the blocked server thread is waiting on the process state, not on socket I/O.

## Suggested fix

Avoid using a consumptive `wait4()` result as the sole test for whether the process is currently stopped.

Possible approaches include:

- track debugger stop/run state explicitly and update it in the existing pause/continue/event paths;
- query a non-consumptive kernel/process state source before deciding to issue another `SIGSTOP`;
- or restructure GETDBREGS so it does not perform a second stop/wait cycle when the debugger session is already in a known stopped state.

The important requirement is that GETDBREGS must never issue a blocking wait for a new stop transition solely because a previously consumed stop status is no longer returned by `wait4()`.

## Additional note

The same stopped-target assumption should be reviewed anywhere else a handler first tests state with `wait4(..., WNOHANG/WUNTRACED/WCONTINUED)` and then unconditionally sends another stop signal followed by a blocking `wait4()`.
