# Data watchpoint interrupt packet clears DR6 trigger-slot status before it is sent

## Summary

The debugger event path clears `DR6` in the interrupt packet before the original hardware-watchpoint trigger bits are preserved. A client receiving the normal 1184-byte debugger interrupt can therefore lose the `B0`-`B3` bits that identify which of the four hardware watchpoint slots caused the stop.

This becomes ambiguous as soon as more than one data watchpoint is active. The target still stops, but the client cannot safely determine which configured watchpoint fired from the packet it receives.

## Source reviewed

- Repository: `OpenSourcereR-dev/ps5debug-NG`
- Branch: `master`
- Commit reviewed: `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86`
- File: `debugger/source/debug.c`
- Protocol command: `CMD_DEBUG_SET_WATCHPOINT` (`0xBDBB0004`)
- Debug interrupt packet: 1184 bytes, with the 128-byte debug-register block at offset `0x420`

## Relevant code

`dispatch_debug_events()` first obtains the debug-register block and copies it into `pkt`:

```c
memcpy(pkt + 0x420, dbreg_buf, 128);

uint64_t *pkt_dr = (uint64_t *)(pkt + 0x420);
```

For a stop with the debugger trap reason used by the watchpoint path, `DR6` is then cleared immediately:

```c
if (*(int32_t *)(lwpi + 0x38) == 2) {
    pkt_dr[6] = 0;
}
```

Later, the data-watchpoint cleanup path attempts to save and clear the same value:

```c
uint64_t _saved_dr6 = pkt_dr[6];
pkt_dr[6] = 0;
```

At that point `_saved_dr6` is already zero because the earlier block has overwritten `pkt_dr[6]`. Restoring `_saved_dr6` therefore does not restore the original `DR6` trigger bits to the packet.

## Expected behavior

When a hardware data watchpoint causes the debugger stop, the interrupt packet should preserve the original `DR6` trigger status long enough for the client to identify the responsible slot:

- `B0` -> DR0 / watchpoint slot 0
- `B1` -> DR1 / watchpoint slot 1
- `B2` -> DR2 / watchpoint slot 2
- `B3` -> DR3 / watchpoint slot 3

The backend may still clear the live thread's debug status before resuming. The packet sent to the client should retain the original event status.

## Actual behavior

The packet-side `DR6` value is cleared before the later code saves it. A client can consequently receive zero in the debug-status field even though a data watchpoint caused the trap.

With one active watchpoint a client can infer the likely source, but with two or more active watchpoints exact attribution is no longer safe.

## Suggested reproduction

1. Attach the debugger to a process.
2. Configure two data watchpoints with `CMD_DEBUG_SET_WATCHPOINT` in different DR slots.
3. Trigger only one of the watched addresses.
4. Read the normal 1184-byte debugger interrupt packet.
5. Inspect the 64-bit `DR6` value at packet offset `0x450` (`0x420 + 6 * 8`).

Expected: the corresponding low trigger bit is present.

Current source path: the value can be zero because the packet copy is cleared before the original value is preserved.

## Impact

Clients cannot reliably map a data-watchpoint stop to a specific configured watchpoint when multiple hardware watchpoints are active. This also prevents safe client-side behavior such as removing only the temporary watchpoint that actually fired.

## Possible fix direction

Preserve the event-time `DR6` value before clearing any live debug-register state. Use the cleared value only when updating the target thread, then write the preserved event value back into the outgoing packet before `net_send_all()`.

The important distinction is that clearing the target's pending debug status and preserving the diagnostic status delivered to the client are separate operations.
