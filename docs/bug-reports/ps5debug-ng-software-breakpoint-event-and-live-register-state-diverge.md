# Software-breakpoint event snapshot and live register state describe different instruction boundaries

## Summary

A software breakpoint hit is reported to the debugger client with the breakpoint address in the interrupt packet, but by the time the client performs a normal register read the stopped thread has already executed the original instruction.

The result is two different views of the same stop:

- the 1184-byte debugger interrupt packet describes the thread **before** the breakpointed instruction executes;
- `CMD_DEBUG_GET_REGISTERS` immediately afterward describes the thread **after** that instruction has executed.

This is especially visible on control-flow instructions. A breakpoint on a `call` is reported at the call address, while an immediate register read can already show RIP at the callee entry point.

## Source reviewed

The behavior below was reviewed against ps5debug-NG `v1.3.0`, commit `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86`.

Relevant source file:

```text
debugger/source/debug.c
```

## Relevant server behavior

`dispatch_debug_events()` first captures the stopped thread state into the outgoing packet and identifies a software-breakpoint hit from `RIP - 1`.

For a matched software breakpoint, the handler then restores the original instruction byte and rewinds the packet/live thread RIP to the breakpoint address:

```c
proc_write_mem((uint32_t)pid, bp_addr, 1, saved_byte_ptr);
*(uint64_t *)(pkt + 0x030 + 0x88) -= 1;

if (use_kernel) {
    uint64_t _new_rip = *(uint64_t *)(pkt + 0x030 + 0x88);
    if (kern_set_rip_fast(pid, lwpid, _new_rip) != 0) {
        if (kern_apply_thread_dbgctx(pid, lwpid, pkt + 0x030) != 0) {
            ptrace_raw(PT_SETREGS, lwpid, pkt + 0x030, 0);
        }
    }
} else {
    ptrace_raw(PT_SETREGS, lwpid, pkt + 0x030, 0);
}
```

Before that packet is sent to the client, the same handler single-steps the restored instruction and waits for the step to complete:

```c
ptrace_raw(PT_STEP, lwpid, (void *)1, 0);

int s_step = 0;
while (wait4(pid, &s_step, 1, NULL) == 0) sceKernelUsleep(50);
```

The breakpoint byte is then installed again. The packet containing the earlier rewound register snapshot is finally sent afterward:

```c
proc_write_mem((uint32_t)pid, bp_addr, 1, &int3);

net_send_all(DBGCTX()->dbgfd, pkt, 1184);
resume_app_via_self_id((int)DBGCTX()->pid);
```

The packet and the live stopped thread therefore intentionally represent different execution points.

## Reproduction 1: ordinary instruction

The following executable instructions were used:

```text
0xE9F747  mov esi,1
0xE9F74C  xor edx,edx
0xE9F74E  call 0x1A3BC30
```

1. Attach the debugger.
2. Install a software breakpoint at `0xE9F747`.
3. Continue execution until the breakpoint interrupt arrives.
4. Observe that the interrupt reports `0xE9F747`.
5. While the target remains paused, immediately request the thread's general registers with `CMD_DEBUG_GET_REGISTERS`.
6. Observe RIP at `0xE9F74C`.

The `mov esi,1` instruction at the breakpoint address has already executed even though the breakpoint event presents `0xE9F747` as the stop address.

## Reproduction 2: call instruction

The following call was also tested:

```text
0xE9F74E  call 0x1A3BC30
0xE9F753  jmp short 0xE9F731
```

1. Install a software breakpoint at `0xE9F74E`.
2. Continue until the breakpoint interrupt arrives.
3. Observe that the interrupt reports `0xE9F74E`.
4. Immediately read the same thread's general registers.
5. Observe RIP at `0x1A3BC30`, the call target.

This makes the difference between the event snapshot and live state particularly clear.

## Expected behavior

A debugger client should be able to obtain one authoritative stopped context for a software-breakpoint hit. Ideally, the target remains stopped at the breakpoint instruction until the client explicitly requests Continue or Step.

If transparent single-stepping before notification is required by the server design, the protocol should clearly distinguish the pre-step breakpoint snapshot from the current live post-step state so clients do not accidentally combine the two.

## Actual behavior

The server single-steps the breakpointed instruction before sending the event packet. The packet still contains the earlier rewound snapshot, while a normal register request returns the newer post-step state.

A client can therefore display:

```text
Breakpoint event: 0xE9F747
Live RIP:         0xE9F74C
```

or, for a call:

```text
Breakpoint event: 0xE9F74E
Live RIP:         0x1A3BC30
```

without issuing any explicit Step command.

## Why this matters

This affects more than the displayed RIP. Breakpoint-based debugger operations commonly use the stopped register/call-stack context to implement:

- Run to Address;
- Step Over;
- Step Out;
- register inspection;
- call-stack inspection;
- instruction-aware navigation.

If an interrupt snapshot is combined with subsequent live register reads, those operations can reason about two different instruction boundaries. A Step command issued after the event can also execute one instruction too many because the server has already performed the transparent step.

## Suggested direction

The cleanest behavior would be to notify the client while the traced thread is still stopped at the restored breakpoint address, and defer execution of the original instruction until an explicit Continue/Step operation.

If that is not practical, an explicit protocol-level indication that the interrupt packet is a logical pre-instruction breakpoint snapshot while the live thread has already advanced would let clients handle the state safely. The important part is that clients should not have to infer this difference from an event RIP that disagrees with an immediate register read.
