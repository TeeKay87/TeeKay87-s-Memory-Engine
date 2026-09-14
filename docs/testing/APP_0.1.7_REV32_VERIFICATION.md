# Application 0.1.7.rev32 Verification — Watchpoint Trigger Resolution and Event Semantics

## Status

**CANDIDATE.** Rev31 is the verified baseline. Rev32 must pass the gates below before the next debugger-finalization stage begins.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev32` |
| Feature | `Watchpoint Trigger Resolution and Event Semantics` |
| Plugin API | `2.17.0` |
| Mock plugin | `1.0.0.rev17` |
| PS5 plugin | `0.1.0.rev39` |
| Automated checks | `143` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the rev32 ZIP to a clean directory and build the complete solution in Visual Studio.
2. Confirm no C#/XAML compile failure and no warning promoted to an error.
3. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

4. The final line must be exactly:

```text
All 143 checks passed.
```

The new **Core watchpoint trigger resolution** check must pass.

## Gate B — Mock Watchpoint Semantics

1. Attach the Mock debugger and create a hardware watchpoint.
2. Trigger it.
3. Confirm the event keeps two different addresses:
   - **Instruction Pointer** = post-access stop/current instruction.
   - **Trigger Instruction** = deterministic memory-access instruction.
4. Confirm **Trigger Resolution** is `BackendExact`.
5. Confirm **Watched Address** shows the watched data address, not either code address.
6. Open the same code range in Disassembler and confirm `Watchpoint hit` is on the trigger instruction, not the stop/current instruction.

## Gate C — Live PS5 Watchpoint Trigger Presentation

Use a known write where the actual memory-access instruction is already understood.

1. Attach to PS5 and add the hardware Write watchpoint.
2. Trigger the watched access.
3. Confirm Debugger Events keeps the callback/stop RIP in **Instruction Pointer**.
4. Confirm **Trigger Instruction** resolves to the preceding logical instruction that actually performed the memory access.
5. Confirm **Trigger Resolution** is `DisassemblyDerived`.
6. Open the relevant Disassembler range.
7. Confirm `Watchpoint hit` is on the trigger instruction.
8. Confirm no `Watchpoint hit` marker is shown on the post-access stop instruction.
9. Confirm register state and stepping still use the real stop/current context.

Expected shape from the verified example:

```text
Trigger: 0x8033D981  add [rax+40h],esi
Stop IP: 0x8033D984  mov rcx,[rdi+30h]
```

## Gate D — Unresolved Trigger Safety

Use Mock/source coverage or a live case where a safe previous instruction boundary cannot be established.

1. Confirm the event remains `Unresolved`.
2. Confirm the current stop IP is not copied into Trigger Instruction.
3. Confirm Disassembler uses `Watchpoint stop (trigger unresolved)` at the stop IP rather than `Watchpoint hit`.
4. Confirm debugger operation continues normally.

## Gate E — Software Breakpoint Regression

1. Keep a Software/Execute breakpoint active on a known multi-byte instruction.
2. Confirm original logical bytes/instruction remain visible; no backend `CC / int3` leaks into the game-code presentation.
3. Confirm `Breakpoint` remains on the intended breakpoint instruction.
4. Trigger the breakpoint and confirm current IP/register semantics remain unchanged.
5. Run Step Into and breakpoint-aware Step Over once.

## Gate F — Rev31 Permanent Watchpoint Cleanup Regression

Repeat the accepted rev31 safety path:

1. Leave a hardware watchpoint enabled.
2. Close the application without manually removing it.
3. Trigger the formerly watched access afterward.
4. Confirm the game continues normally.
5. Repeat once using explicit Debugger Detach.

This remains a permanent target-safety gate.

## Gate G — Multi-Watchpoint Attribution Regression

1. Create multiple active hardware watchpoints.
2. Confirm exact DR6-backed attribution still works when available.
3. Confirm the existing conservative behavior remains when ps5debug-NG does not preserve usable trigger-slot status.
4. Do not accept any host-generated guess about which watchpoint fired.

## Gate H — Final Detach/Reattach

1. Detach cleanly after the trigger tests.
2. Reattach.
3. Confirm no stale markers, watchpoints, breakpoints, or trigger metadata survive into the new debugger session.
4. Confirm new breakpoint/watchpoint creation and removal still work.

## Completion Rule

Rev32 is accepted only after a clean Windows build, **143/143**, Mock trigger/current separation PASS, live PS5 resolved-marker PASS, unresolved-safety PASS, software-breakpoint regression PASS, rev31 detach-cleanup regression PASS, conservative multi-watchpoint attribution PASS, and clean detach/reattach PASS.

After acceptance, continue the debugger-finalization handover with the immutable Debugger Snapshot model and shared capture pipeline. Do not begin snapshot JSON persistence or Call Stack comparison by duplicating live debugger collection logic.
