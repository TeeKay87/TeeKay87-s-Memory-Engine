# Application 0.1.7.rev24 Verification — Debugger Mode Switcher Layout Refresh

## Status

**SUPERSEDED BY REV25 AFTER PARTIAL LIVE-PS5 ACCEPTANCE.** The rev24 selector-button layout, Debugger sizing, splitters, modeless-window cleanup, and complete focused Mock Call Stack/stepping workflow were accepted in runtime. Live PS5 Call Stack/thread/navigation/native-Step-Into testing also passed. The live software-breakpoint/Run-to phase then exposed an external ps5debug-NG event-vs-live-register divergence that blocks reliable Step Over/Step Out/Run-to acceptance until the client reconciles the logical stop context.

## Candidate Metadata

| Component | rev24 value |
| --- | --- |
| Application | `0.1.7.rev24` |
| Feature | `Debugger Mode Switcher Layout Refresh` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev34` |
| Automated checks | `130` registered; **129/130 PASS** in the final rev24 run |

## Automated Verification Result

The final Windows run completed **129 of 130 checks**. The only failure was:

```text
FAIL  Debugger breakpoint manager source contract
      Debugger workspace is missing the combined Breakpoints / Watchpoints pane.
```

This was a stale source assertion, not a runtime UI failure. Rev24 intentionally replaced the old WPF TabItem with a selector Button; the verifier still searched for the former `Text="Breakpoints / Watchpoints"` shape instead of the new `Content="Breakpoints / Watchpoints"` selector. That source contract is corrected in rev25.

## Runtime UI / Lifecycle Result — PASS

The rev24 Debugger presentation was accepted:

- compact **Breakpoints / Watchpoints** and **Call Stack** selector buttons replaced the clipped WPF tab chrome;
- selected-outline switching correctly showed exactly one upper workspace at a time;
- duplicate inner title/count rows were removed as intended;
- footer actions were positioned as specified;
- the larger default Debugger size was accepted;
- all previously introduced Debugger splitters behaved correctly;
- closing MainWindow with child tools open ran their cleanup and closed them correctly without the earlier re-entry exception.

## Focused Mock Result — PASS

The complete carried-forward Mock Call Stack/stepping cycle passed in rev24 runtime:

- Call Stack population and frame details;
- thread switching with matching register/IP/call-stack context;
- frame -> Disassembler navigation;
- frame -> Memory Viewer navigation;
- native Step Into;
- Step Over over the deterministic Mock call with temporary-breakpoint cleanup;
- Step Out with temporary-breakpoint cleanup;
- Run to Address with cleanup;
- resume/pause/detach/reattach stale-session cleanup;
- ordinary software-breakpoint and hardware-watchpoint regression after stepping.

## Live PS5 Result Before Blocker

The following rev24 live PS5 checks passed:

- server-side Call Stack population;
- Call Stack switching with selected stopped thread;
- frame -> Disassembler and Memory Viewer navigation;
- selected-thread native Step Into with the expected Resumed -> StepCompleted lifecycle and refreshed stop context.

## Live PS5 Software-Breakpoint Blocker

Run to Address and direct software-breakpoint isolation showed that the breakpoint event and later live register state describe different moments.

For a normal instruction:

```text
0xE9F747  mov esi,1
0xE9F74C  xor edx,edx
```

The event reported the breakpoint at `0xE9F747`, while the subsequent visible/live RIP was already `0xE9F74C`.

For a call:

```text
0xE9F74E  call 0x1A3BC30
0xE9F753  jmp short 0xE9F731
```

The event reported `0xE9F74E`, while live RIP was already inside the callee at `0x1A3BC30`.

Review of current ps5debug-NG source confirmed the cause: on a matched software breakpoint it restores the saved byte, rewinds the packet RIP, writes that RIP to the thread, single-steps the restored instruction, waits for completion, rearms `INT3`, and only then sends the original logical breakpoint packet to the client. The packet therefore represents the pre-instruction logical stop while later GETREGS represents post-instruction live state.

The same Run-to attempt also exposed a host status race: the Debugger could already be Paused on a breakpoint event while the bottom status still read `Run to Address is running toward ...` because the awaited command wrote its older running text after the fast interrupt.

## Rev24 Completion Decision

Rev24 is **not** the accepted final Call Stack/stepping revision because the live PS5 composed-stepping/Run-to gates could not be completed reliably with the mixed breakpoint context. The accepted rev24 UI/lifecycle/Mock work is preserved. Rev25 is the focused corrective candidate for:

1. PS5 logical software-breakpoint stop-context reconciliation;
2. stale execution-status reconciliation after fast stops;
3. the stale Breakpoints / Watchpoints source contract; and
4. MainWindow bottom-status centerline alignment requested during the correction cycle.

Rev25 later passed its logical software-breakpoint stop-context gate but failed live Step Over on a breakpointed `call`. Rev26 carries the host breakpoint-aware Step Over correction, so Debugger **Integration, Export and Finalization** now moves to rev27 after rev26 acceptance.
