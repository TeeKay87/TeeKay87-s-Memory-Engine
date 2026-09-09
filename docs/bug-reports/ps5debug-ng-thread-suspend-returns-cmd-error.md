# `CMD_DEBUG_SUSPEND_THREAD` returns `CMD_ERROR` for an enumerated thread

## Summary

`CMD_DEBUG_SUSPEND_THREAD` is accepted by the ps5debug-NG command connection, but the request returns `CMD_ERROR` when the supplied LWP id comes directly from `CMD_DEBUG_GET_THREAD_LIST`.

The failure is clean on the client side. The game keeps running, the debugger stays attached, and the PS5 does not hang or crash.

I hit this while testing thread control in a debugger client. Thread enumeration and thread-info requests work against the same attached process.

## Environment

- Target: PlayStation 5
- Target process: `eboot.bin`
- Process id during the test: `0x60`
- Threads returned by the target during the test: 82
- Thread used for the suspend test: `0x18E74` (`Job.Worker 0`)
- ps5debug-NG runtime build: the exact branding/build string was not captured during this test
- Source/protocol reference used for comparison: ps5debug-NG v1.3.0, commit `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86`

## Steps to reproduce

1. Attach the debugger to `eboot.bin`.
2. Call `CMD_DEBUG_GET_THREAD_LIST` (`0xBDBB0005`).
3. Optionally call `CMD_DEBUG_THREAD_INFO` (`0xBDBB0011`) for the returned ids.
4. Pick a returned worker thread. In this test the selected id was `0x18E74`.
5. Send `CMD_DEBUG_SUSPEND_THREAD` (`0xBDBB0006`) with a 4-byte little-endian body containing that id.
6. Read the returned status word.

## Expected result

The protocol documentation describes the request body as a single `uint32_t lwpid` and the successful response as `CMD_SUCCESS`.

A valid thread id returned by `CMD_DEBUG_GET_THREAD_LIST` should therefore be suspendable, assuming there is no target-side restriction that prevents that specific thread from being suspended.

## Actual result

The server returns:

```text
0xF0000001
```

This is the on-wire value for `CMD_ERROR`.

The client reports the failure and does not change its local thread state. The debugger session remains usable and ordinary Pause/Continue, thread enumeration, memory reads, and other debugger traffic continue working.

No game crash, PS5 crash, or connection loss was observed.

## Request sent by the client

The request follows the layout currently documented by ps5debug-NG:

```text
magic:   0xFFAABBCC
command: 0xBDBB0006
length:  4
body:    uint32 little-endian LWP id
```

For the reproduced case, the body contains `0x18E74`.

## Relevant server code

The current handler in `debugger/source/debug.c` calls:

```c
if (ptrace_elev(PT_SUSPEND, *(int *)data, NULL, 0) == -1) {
    net_send_int32(fd, CMD_ERROR);
    return 0;
}
```

`CMD_DEBUG_RESUME_THREAD` uses the corresponding `PT_RESUME` call in the same style.

I did not force a Resume test after the failure because the thread was never successfully suspended.

## Notes

The thread id itself appears valid. It came from the same debugger attachment through `CMD_DEBUG_GET_THREAD_LIST`, and `CMD_DEBUG_THREAD_INFO` succeeds for the enumerated thread set.

One area worth checking is the target-side `PT_SUSPEND`/`PT_RESUME` invocation and the exact argument semantics expected by the PS5/FreeBSD ptrace implementation. The client request appears to match the documented wire format, so there is no obvious framing or byte-order mismatch on the client side.

It would also be useful if the handler exposed enough diagnostic information to distinguish an invalid LWP id from another ptrace failure. At the moment every `PT_SUSPEND` failure is reduced to the same `CMD_ERROR` status.

## Compatibility impact

Clients can still safely expose thread enumeration. Individual thread Suspend/Resume currently cannot be treated as working on the tested payload because the suspend request is rejected by the backend.

If the fix can be made behind the existing `0xBDBB0006` / `0xBDBB0007` wire contract, existing clients that already send the documented 4-byte LWP request should not require a protocol change.
