# Software breakpoint single-step consumes an overlapping hardware watchpoint hit

## Summary

When a software breakpoint is placed on an instruction that also writes to an active hardware watchpoint, the software breakpoint event is delivered, but the hardware watchpoint event for that same instruction is not delivered after the client continues.

The write itself still happens. The missing part is the separate watchpoint notification.

## Tested setup

- Backend: ps5debug-NG
- Repository: `OpenSourcereR-dev/ps5debug-NG`
- Revision reviewed: `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86`
- Debugger command path: `debugger/source/debug.c`
- Software breakpoint and hardware write watchpoint active at the same time

The concrete test used this instruction:

```asm
0x8033D981  01 70 40  add [rax+40h],esi
```

`RAX + 0x40` resolved to the watched data address. With only the hardware watchpoint active, the backend reported the expected watchpoint stop after this write. With a software breakpoint also active at `0x8033D981`, only the software breakpoint event was observed.

## Reproduction

1. Attach the debugger to a running target.
2. Add a hardware **Write** watchpoint for a data address written by a known instruction.
3. Verify that changing the value produces a watchpoint event.
4. Add a software breakpoint at the exact instruction that performs the watched write.
5. Continue until the software breakpoint is hit.
6. Continue again so the backend executes the original instruction and rearms the software breakpoint.
7. Observe that no separate hardware watchpoint event is delivered for the write performed during that continue.

## Expected behavior

The client should receive both logical stops in sequence:

1. the software breakpoint stop before the instruction executes; and
2. the hardware watchpoint stop caused by the write when that instruction is executed during breakpoint recovery.

If the backend intentionally collapses these events, the debugger protocol needs to expose enough information for the client to know that the transparent breakpoint step also triggered a hardware watchpoint.

## Actual behavior

The software breakpoint stop is delivered normally. After Continue, the original instruction executes and changes the watched data, but the watchpoint stop is not delivered to the client.

Removing the software breakpoint makes the hardware watchpoint event observable again.

## Relevant backend path

The matched software-breakpoint path restores the saved byte, rewinds the thread RIP and executes the original instruction with `PT_STEP`:

```c
proc_write_mem((uint32_t)pid, bp_addr, 1, saved_byte_ptr);
*(uint64_t *)(pkt + 0x030 + 0x88) -= 1;
...
ptrace_raw(PT_STEP, lwpid, (void *)1, 0);
```

It then waits for that step synchronously:

```c
int s_step = 0;
while (wait4(pid, &s_step, 1, NULL) == 0) sceKernelUsleep(50);
```

Only after that wait has been consumed does the function continue with the normal debugger-event path and eventually rearm the `INT3`:

```c
proc_write_mem((uint32_t)pid, bp_addr, 1, &int3);
```

Hardware-watchpoint attribution is processed later from debug-register state. Because the stop produced by the internal `PT_STEP` has already been consumed by the synchronous `wait4`, an overlapping hardware-watchpoint stop does not reach the normal client event path as its own event.

## Why this matters

A common debugger workflow is to use a data watchpoint to identify an instruction and then place an execute breakpoint on that instruction for register/call-stack analysis. Keeping both active should not silently suppress a real data-watchpoint hit.

This also makes event behavior dependent on whether a software breakpoint happens to be armed at the same instruction, even though the watched memory access still occurs.

## Suggested direction

The transparent software-breakpoint step could inspect the stop consumed by its `wait4` before discarding it. If that stop contains a hardware-watchpoint condition, either:

- forward the watchpoint stop to the client after the logical software-breakpoint stop; or
- include the watchpoint trigger information in the logical breakpoint event so the client can surface both causes.

The important part is that the watchpoint trigger must not disappear solely because the write was executed as part of software-breakpoint recovery.
