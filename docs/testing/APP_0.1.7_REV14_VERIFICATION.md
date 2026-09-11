# Application 0.1.7.rev14 Verification — PS5 Breakpoint Capability Verification Fix

## Status

**Superseded after automated and runtime acceptance.** Rev14 corrected the final stale PS5 capability expectation, passed the complete Windows verification suite, and then completed comprehensive Mock/live-PS5 Breakpoint Manager runtime testing. The runtime cycle confirmed the breakpoint transport and lifecycle design but exposed two remaining implementation defects that are corrected by rev15.

## Metadata

| Component | Value |
| --- | --- |
| Application | `0.1.7.rev14` |
| Feature | `PS5 Breakpoint Capability Verification Fix` |
| Plugin API | `2.14.0` |
| Mock plugin | `1.0.0.rev12` |
| PS5 plugin | `0.1.0.rev31` |
| Automated checks | `114` |

## Gate A — Windows Build and Automated Verification

The supplied Windows run completed successfully with:

```text
All 114 checks passed.
```

This confirms that rev14 corrected the PS5 capability-set expectation without weakening the exact capability comparison and that all previously registered debugger/breakpoint checks passed.

## Mock Runtime Acceptance

The focused Mock workflow passed the following runtime checks:

- persistent software execute breakpoint add/hit;
- repeated persistent hits without removing the breakpoint;
- temporary breakpoint first-hit cleanup;
- Enable, Disable, Remove, and confirmed Remove All;
- neutral Breakpoint event, stop reason, thread, and instruction-point presentation;
- selected breakpoint -> Disassembler navigation;
- Detach/Reattach with no stale breakpoint state;
- Debugger window-close cleanup and clean reopen;
- right-side Breakpoints/Events splitter default near 65/35, drag limits, adaptive resizing, and theme behavior.

During invalid-address coverage, Mock accepted `0x80000104`, which lies outside the target memory map. The review also confirmed that mapped data address `0x10000104` is Ammo, not executable Mock code, and that Software/Execute breakpoints should not be used as data-write watchpoints. This became the first rev15 correction.

## Live PS5 Runtime Acceptance

Live testing against `eboot.bin` passed:

- persistent software execute breakpoint installation in an executable region;
- correct Breakpoint event/stop reason/thread/instruction-point mapping;
- repeated hits of the same persistent breakpoint;
- temporary breakpoint hit followed by automatic manager removal;
- Disable while Paused without an unexpected backend Resume;
- Enable after Disable while still Paused, before Continue;
- Remove while Paused without an unexpected Resume;
- Remove All while Paused followed by correct staged cleanup on Continue;
- slot/address reuse after cleanup;
- Detach with an active breakpoint and clean Reattach;
- closing an attached Debugger window with an active breakpoint and clean reopen;
- Disconnect/Reconnect connection-generation invalidation;
- duplicate breakpoint rejection on a valid address;
- selected breakpoint -> Disassembler navigation;
- ordinary Pause/Continue/register transport regression, including the established successful 76-row register surface and no framing/hang regression.

Invalid/unmapped breakpoint addresses were deliberately not sent to the real PS5 backend during runtime testing because the implementation did not yet validate them before the INT3 write command.

## Runtime Defect 1 — Breakpoint Address Validation

The rev14 breakpoint services validate the one-byte Software/Execute shape and duplicate state but do not consistently validate that the requested address belongs to executable target memory before backend mutation.

Required rev15 correction:

- Mock rejects addresses outside its target map and mapped data addresses outside its explicit synthetic code fixture;
- PS5 validates against the current target memory map and requires an executable, non-guarded region before `CMD_DEBUG_SET_BREAKPOINT` is sent;
- invalid requests must not allocate/mutate backend breakpoint state.

## Runtime Defect 2 — Immediate Re-hit Register Refresh

A repeatable live PS5 issue was found when the target was already Paused on an enabled breakpoint:

1. Registers were populated normally.
2. Continue moved the target briefly to Running and correctly cleared Registers.
3. The same breakpoint re-hit almost immediately.
4. State returned to Paused, but Registers sometimes remained empty.

Manual Pause populated Registers correctly, as did ordinary breakpoint hits that were not racing the Continue command. Source review identified that the Paused breakpoint event could arrive while `DebuggerViewModel.IsBusy` was still true for Continue, causing the event-driven stop-context refresh to be skipped rather than delayed.

Required rev15 correction: retain the newest Continue-time Paused stop context and replay the existing stop-context refresh after Continue leaves its busy state, provided the same debugger session is still current and Paused.

## Acceptance Result

Rev14 proves the core Breakpoint Manager, PS5 software-breakpoint transport, paused cleanup staging, breakpoint hit mapping, and lifecycle integration. It is nevertheless **superseded by rev15** because the two runtime defects above remain in the packaged rev14 implementation.

The requested removal of the Disassembler visible-range explanatory paragraph is a separate rev15 UI cleanup request and is not recorded as a rev14 defect.

Hardware Watchpoints moves to **0.1.7.rev16** after the focused rev15 correction cycle is verified.
