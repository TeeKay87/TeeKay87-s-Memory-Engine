# Application 0.1.7.rev25 Source Review — PS5 Breakpoint Stop Context and Status Alignment Fix

## Scope Reviewed

The supplied `0.1.7.rev24` package was used as the sole application baseline. Before editing, the complete repository tree was traversed, including root README/CHANGELOG, every Markdown document under `docs/`, all C# sources, XAML views/resources, project/build files, JSON themes, tests, and scripts. The focused review then followed the verified Debugger ownership boundaries through `DebuggerViewModel`, the neutral debugger contracts/coordinator, Mock fixtures, PS5 session/protocol/register mapping, MainWindow status-bar XAML, the rev24 selector layout, and the existing source-verification registry.

The review also compared the observed live PS5 behavior with the current ps5debug-NG debugger source. The backend's matched-software-breakpoint path restores the original byte, rewinds the packet RIP by one, writes that RIP back to the stopped thread, issues `PT_STEP`, waits for that step to complete, rearms `INT3`, and only afterward sends the original 1184-byte packet to the debugger client. That means the interrupt packet intentionally describes the logical breakpoint instruction while a later live GETREGS already describes the instruction's post-step state. The live reproductions on an ordinary instruction and a `call` matched that sequence exactly.

## Runtime Evidence Carried Into Rev25

Rev24's redesigned selector-button Debugger UI and the existing splitters were accepted in runtime. The complete focused Mock Call Stack/stepping sequence passed, including Call Stack population and thread switching, frame details, frame navigation, Step Into, Step Over, Step Out, Run to Address, stale-session/cleanup, and breakpoint/watchpoint regression. Live PS5 then passed Call Stack population, thread switching, frame navigation, and native Step Into.

The live software-breakpoint isolation produced the blocker:

- breakpoint event at `0xE9F747`, later live RIP `0xE9F74C`;
- breakpoint event at `0xE9F74E` (`call 0x1A3BC30`), later live RIP `0x1A3BC30`;
- Run to Address could therefore report the requested breakpoint event while the register/call-frame views observed post-instruction state;
- a fast breakpoint stop could also leave the bottom Debugger status message at `Run to Address is running toward ...` despite the coordinator already being Paused.

The rev24 automated run was **129/130**. Its single failure was a stale source assertion that still searched for TabItem-era `Text="Breakpoints / Watchpoints"`; the accepted rev24 selector is a Button using `Content`.

## PS5 Production Correction

PS5 plugin `0.1.0.rev35` retains the existing wire protocol and Plugin API `2.15.0`. No new public SDK contract is introduced.

For an incoming interrupt attributed to a managed software breakpoint, the session caches the interrupt packet's general and FPU register blocks together with the backend thread id and breakpoint address. That snapshot is the logical pre-instruction stop advertised by the backend event. Matching register inspection consumes the cached general/FPU state rather than immediately replacing it with post-step GETREGS/GETFPREGS data; FS/GS remains a separate disposable live probe because those values are not present in the interrupt packet. Other threads and ordinary stops continue to use the existing live register service.

Call Stack similarly seeds frame zero from the logical snapshot's RIP/RBP/RSP. This keeps event, register, and top-frame semantics consistent for the selected breakpoint thread while leaving the server-side RBP-chain command and module resolution unchanged.

A second native Step Into from that logical stop would incorrectly execute one additional instruction because ps5debug-NG has already transparently stepped the original instruction. `StepAsync` therefore detects the matching snapshot, reads the current live post-step RIP, consumes the logical snapshot, and emits one neutral Resumed/StepCompleted lifecycle without sending another `CMD_DEBUG_STEP_THREAD`. Native Step Into from ordinary paused contexts remains unchanged.

The snapshot is cleared on explicit Pause, Continue, Detach, new interrupt receipt, and transparent-step consumption. This preserves the existing target/session-generation safety and prevents a packet snapshot from becoming a general-purpose register cache.

## Host Status Correction

`DebuggerViewModel` now reconciles the final coordinator state after awaited Continue, native Step Into, and Run-to execution commands. If an asynchronous stop arrived while the command was busy, the deferred stop event/message wins. This removes the observed stale `running` status text without delaying or suppressing the interrupt itself.

`MainWindow.xaml` keeps the existing status-bar column structure but explicitly vertically centers every visible status item: connection badge, general status, error text, scan status, progress/elapsed group, and display version. No binding, text, progress, or identity semantics change.

## Verification Changes

The stale Breakpoints / Watchpoints source contract now checks the selector Button `Content` introduced by rev24. Two new top-level tests raise the registry target to **132**:

- **PS5 debugger logical software-breakpoint stop context** verifies packet-snapshot register/top-frame semantics and verifies that Step Into from that logical stop does not issue a second native backend step;
- **Main status bar content alignment source contract** verifies that all status-bar content participates in the common centered row alignment.

`Ps5ProtocolTestServer` now fills the debugger interrupt's general and FPU areas with deterministic data before applying the requested interrupt RIP. The existing Run to Address source test also requires the post-execution state-reconciliation path.

## Preserved Boundaries

Core debugger models/coordinator, Plugin SDK `2.15.0`, Mock `1.0.0.rev15`, scan/export/memory/disassembly behavior, breakpoint/watchpoint legality, hardware-watchpoint transport, modeless tool-window ownership, MainWindow-driven cleanup, and the rev24 selector/splitter layout are not redesigned by this revision. The existing workaround for ps5debug-NG's paused software-breakpoint disable/resume behavior also remains unchanged.

This is a logical debugger-client reconciliation, not a rollback of an instruction that the external backend already executed. Memory/register side effects produced by ps5debug-NG's transparent step remain real.

## Version/Compatibility

| Component | rev25 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev25` | Corrective debugger/status revision |
| Feature title | `PS5 Breakpoint Stop Context and Status Alignment Fix` | New application title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev35` | Logical software-breakpoint stop reconciliation |
| Automated registry | `132` | Two new checks plus corrected existing source contract |

## Development Order

Rev25 must pass a clean Windows WPF build, **132/132** automated checks, focused live PS5 logical software-breakpoint/Run-to/Step Over/Step Out verification, and carried-forward lifecycle/cleanup regression. If no further code correction is required, the remaining Debugger-closing milestone becomes `0.1.7.rev26 — Integration, Export and Finalization`.
