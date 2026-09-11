# Debugger detach can leave hardware watchpoints active after the client exits

## Summary

A hardware watchpoint can remain armed after the debugger client detaches or exits. The target may continue running normally until the watched access occurs again, at which point the process can terminate because the CPU still raises a debug exception even though the debugger session is gone.

The teardown path does contain code intended to clear debug registers on every LWP, but that cleanup is conditional on a preliminary `PT_GETDBREGS` probe. The probe result is not checked before the code inspects the zero-initialized buffer. If that read fails, or does not represent an LWP that carries the active debug-register state, teardown can conclude that no hardware debug registers are active and skip the per-thread clear entirely.

## Source reviewed

ps5debug-NG `v1.3.0`, commit:

```text
d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86
```

Relevant file:

```text
debugger/source/debug.c
```

## Reproduction

1. Attach a debugger client to a running target process.
2. Install a hardware Write or Read/Write watchpoint through `CMD_DEBUG_SET_WATCHPOINT`.
3. Verify that the watchpoint triggers normally while the debugger is attached.
4. Leave the watchpoint enabled.
5. Close the debugger client or detach without explicitly disabling the watchpoint first.
6. Let the target continue until the watched access occurs again.
7. The target process can terminate when the stale debug-register condition is triggered.

Explicitly disabling the watchpoint before detach avoids the problem in the same test scenario.

## Relevant watchpoint setup path

`debug_set_watchpoint_handle()` updates the debugger context and then writes the complete debug-register block to every LWP:

```c
char     *ctx     = (char *)curdbgctx;
uint64_t *dr_addr = (uint64_t *)(ctx + 0x2D8);
uint64_t *dr7     = (uint64_t *)(ctx + 0x310);

int idx = (int)wp->index;
int cl1 = idx * 2;
int cl2 = idx * 4 + 16;

uint64_t mask = ((uint64_t)0xFu << cl2)
              | (uint64_t)(int64_t)(int32_t)(3 << cl1);
*dr7 &= ~mask;

if (wp->enabled) {
    dr_addr[idx] = wp->address;
    uint64_t low_bits  = (uint64_t)(int64_t)(int32_t)(3 << cl1);
    uint64_t high_bits = (((uint64_t)wp->length << 2) | (uint64_t)wp->breaktype) << cl2;
    *dr7 |= low_bits | high_bits;
} else {
    dr_addr[idx] = 0;
}

void *dr_block = (void *)(ctx + 0x2D8);
for (int i = 0; i < count; i++) {
    if (ptrace_raw(PT_SETDBREGS, lwpids[i], dr_block, 0) == -1) {
        ...
    }
}
```

This makes the watchpoint state explicitly per-thread.

## Relevant detach path

`debug_detach_handle()` delegates teardown to `debug_full_teardown()`:

```c
int debug_detach_handle(int fd, struct cmd_packet *packet) {
    (void)packet;
    debug_full_teardown(curdbgctx);
    net_send_int32(fd, CMD_SUCCESS);
    return 0;
}
```

The teardown routine first zeroes a local buffer, attempts a debug-register read using the process id, and then checks only the low byte of DR7:

```c
char dr_buf[0x100];
memset(dr_buf, 0, sizeof(dr_buf));
ptrace_raw(PT_GETDBREGS, pid, dr_buf, 0);

uint8_t dr7_low = (uint8_t)dr_buf[0x38];
int     have_active_dr =
    (dr7_low & 0x03) || (dr7_low & 0x0C) ||
    (dr7_low & 0x30) || (dr7_low & 0xC0);
```

The return value from `PT_GETDBREGS` is not checked. Because `dr_buf` was initialized to zero, a failed read is indistinguishable from a successful read reporting no active local/global enable bits.

If `have_active_dr` is false, the code bypasses the LWP enumeration and per-thread `PT_SETDBREGS` clearing path and proceeds toward detach:

```c
if (!have_active_dr) {
    int rc = (int)ptrace_raw(PT_GETNUMLWPS, pid, NULL, 0);
    ...
    goto free_and_detach;
}
```

Only the `have_active_dr` path enumerates LWPs and writes an all-zero debug-register block to each one:

```c
memset(dr_buf, 0, sizeof(dr_buf));
bool use_kernel_path = fw_uses_kernel_dbreg_path();
for (int i = 0; i < count; i++) {
    int lwpid = lwpids[i];
    if (use_kernel_path) {
        if (kern_set_dbregs(pid, lwpid, dr_buf) != 0) {
            ptrace_raw(PT_SETDBREGS, lwpid, dr_buf, 0);
        }
    } else {
        ptrace_raw(PT_SETDBREGS, lwpid, dr_buf, 0);
    }
}
```

## Expected behavior

Debugger teardown should clear hardware breakpoint/watchpoint state from every LWP before `PT_DETACH`, regardless of whether a preliminary debug-register probe succeeds.

After detach, triggering the formerly watched memory access should behave exactly as it did before the watchpoint was installed.

## Actual behavior

A watchpoint can survive debugger teardown. The target continues running after the debugger exits, but a later access to the watched address can generate an unhandled debug exception and terminate the process.

## Suggested direction

The teardown path should not use a best-effort single `PT_GETDBREGS` result as the gate for whether per-thread cleanup is required.

A safer teardown sequence would be:

1. enumerate every current LWP;
2. write a fully cleared debug-register block to every LWP unconditionally, or at least whenever the debugger context records any configured hardware breakpoint/watchpoint slot;
3. verify or handle failures per LWP;
4. only then perform `PT_DETACH`.

If a preliminary read is retained, its return value should be checked and a failed read should fall back to the conservative cleanup path rather than being treated as "no active debug registers".
