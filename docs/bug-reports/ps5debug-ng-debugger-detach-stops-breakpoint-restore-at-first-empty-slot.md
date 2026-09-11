# Debugger detach can skip active software breakpoints after an empty slot

## Summary

`debug_full_teardown()` stops walking the software-breakpoint table as soon as it encounters the first slot whose address is zero. Because `CMD_DEBUG_SET_BREAKPOINT` accepts a caller-selected slot index and the disable path clears that slot's address, the table can contain a hole followed by another active breakpoint.

In that state, debugger detach restores only the slots before the first hole. A later active slot can keep its patched `0xCC` byte in target memory after teardown.

## Source reviewed

ps5debug-NG `v1.3.0`, commit:

```text
d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86
```

Relevant file:

```text
debugger/source/debug.c
```

## Relevant code

The detach handler delegates cleanup to `debug_full_teardown()`:

```c
int debug_detach_handle(int fd, struct cmd_packet *packet) {
    (void)packet;
    debug_full_teardown(curdbgctx);
    net_send_int32(fd, CMD_SUCCESS);
    return 0;
}
```

The software-breakpoint restore loop in `debug_full_teardown()` stops on the first zero address:

```c
char *rbx     = (char *)svc + 0x18;
char *bp_end  = (char *)svc + 0x2E8;
uint64_t teardown_bp_addrs[INT3_REWIND_MAX];
int teardown_n_bps = 0;
while (rbx != bp_end) {
    uint64_t address = *(uint64_t *)(rbx - 8);
    if (address == 0) break;
    proc_write_mem((uint32_t)pid, address, 1, rbx);
    if (teardown_n_bps < INT3_REWIND_MAX) {
        teardown_bp_addrs[teardown_n_bps++] = address;
    }
    rbx += 0x18;
}
```

The normal disable path clears the slot address:

```c
*(uint32_t *)(bp_entry + 0x08) = 0;
*(uint64_t *)(bp_entry + 0x10) = 0;
```

That makes sparse breakpoint tables possible without any malformed state.

## Example sequence

1. Enable software breakpoint slot `0` at address A.
2. Enable software breakpoint slot `1` at address B.
3. Disable slot `0`. Its address field becomes zero while slot `1` remains active.
4. Detach the debugger.
5. `debug_full_teardown()` reads slot `0`, sees address `0`, and executes `break`.
6. Slot `1` is never visited by the restore loop, so B's saved instruction byte is not restored by this loop.

The same issue applies to any active slot located after the first empty slot.

## Expected behavior

Debugger teardown should inspect every software-breakpoint slot and restore each active/non-zero entry, regardless of holes in the table.

## Actual behavior

The restore loop treats the first zero address as the end of the active table even though breakpoint slots are index-addressable and may be sparse.

## Why this matters

Software breakpoints patch target code with `0xCC`. Leaving an active slot unrestored during detach can leave target code modified after debugger ownership is released. It also makes cleanup depend on the historical order in which breakpoint slots were enabled and disabled rather than on the actual active slots at teardown time.

## Suggested fix

Walk the complete software-breakpoint slot range and skip empty entries instead of terminating at the first one. In other words, an empty address should behave like `continue`, not `break`.

The teardown sweep should also collect every restored active address so the existing `int3_iterative_sweep()` receives the complete set.
