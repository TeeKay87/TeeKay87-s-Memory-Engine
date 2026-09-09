# TeeKay87's Memory Engine 0.1.6.rev11 Verification

## Scope

Application `0.1.6.rev11` adds direct branch/call target navigation to the existing architecture-neutral Disassembler workspace. The revision does not change Core disassembly reads, the Plugin SDK contract, either platform plugin, the x86-64 decoder, Memory Viewer, scanner behavior, Saved Addresses, or universal export.

A Disassembler instruction is followable only when all of the following are true:

- the decoded instruction record is valid;
- the neutral provider supplied a non-null `BranchTarget`;
- `FlowControl` is `Call`, `Jump`, or `ConditionalJump`.

This deliberately excludes indirect flow such as `call rax`, `call [rax]`, and `jmp rbx` because a static destination is not known without future debugger/register context.

## Automated verification

1. Perform a clean Windows build of the complete solution.
2. Run `TeeKay87.MemoryEngine.Tests`.
3. Expected result: **73/73 checks passed**.

The new check, **Disassembler direct target navigation contract**, validates the production Disassembler source fixtures and verifies that:

- the context menu contains **Follow Target**;
- double-click and Enter are wired to target following;
- host eligibility requires a known direct `BranchTarget` and call/jump flow-control classification;
- Follow Target reuses the existing `NavigateAndRecordAsync` history path;
- host target-following code contains no PS5 or Iced-specific behavior.

No existing verification check is removed or weakened.

## Mock Target runtime verification

1. Connect to **In-Memory Test Target**.
2. Set **Mock Game** as Active Target.
3. Open the Disassembler at `0x10000400`.
4. Confirm the deterministic fixture includes direct-flow instructions such as:
   - `call 0x10000408`;
   - `jump-if 0x1000040A`.
5. Select the `call` row and right-click it.
6. Confirm **Follow Target** is enabled.
7. Activate **Follow Target**.
8. Confirm the Disassembler origin moves to `0x10000408` and normal bounded around-origin context is shown.
9. Use **Back** and confirm the origin returns to `0x10000400`.
10. Use **Forward** and confirm the origin returns to `0x10000408`.
11. Return Back, then double-click the `jump-if` row.
12. Confirm the origin moves to `0x1000040A`.
13. Repeat using **Enter** on a known direct-flow row.

## PS5 direct CALL verification

Use an executable `Read, Execute` region in `eboot.bin`.

1. Navigate to a block containing a direct `call` instruction. A previously observed example around `0x42D41C` / `0x42D42E` may be used if the current game/build still contains that code.
2. Select a direct `call` whose operand is rendered as a concrete address and whose provider record therefore has a `BranchTarget`.
3. Confirm **Follow Target** is enabled in the row context menu.
4. Record the displayed target address.
5. Follow the target.
6. Confirm:
   - Address field becomes the target address;
   - origin highlighting moves to the instruction containing that target address;
   - Region / Module, Protection, visible range, and syntax highlighting remain correct;
   - no separate/disconnected Disassembler window is created.
7. Press **Back** and confirm the original call site returns.
8. Press **Forward** and confirm the call target returns.

## PS5 direct JMP / conditional branch verification

1. Select a direct `jmp`, `je`, `jne`, `jb`, or another conditional jump with a known static target.
2. Confirm **Follow Target** is enabled.
3. Follow it with each supported interaction in separate trials:
   - context menu;
   - double-click;
   - Enter.
4. Confirm every successful follow enters the same Back/Forward history used by Go To.

## Indirect flow safety

Find or use a row such as:

```text
call qword [rax]
jmp rbx
```

Expected behavior:

- **Follow Target** remains disabled because `BranchTarget` is null;
- double-click does not navigate;
- Enter does not invoke target navigation;
- no destination is fabricated from operand text;
- current origin/history remains unchanged.

This behavior is mandatory even when the operand visually contains a register or memory expression that could theoretically be resolved by future debugger state.

## Failed target navigation

If a direct target points to memory that is not currently inside a readable non-guarded region:

1. attempt **Follow Target**;
2. confirm the Disassembler reports the read/navigation failure;
3. confirm the previous instruction list remains visible;
4. confirm the previous current address remains the active history position;
5. confirm no failed history entry is created.

## History branching

1. Start at address A.
2. Follow a direct target to B.
3. Follow a direct target to C.
4. Back to B.
5. Back to A.
6. Forward to B.
7. From B, perform a new successful Go To or Follow Target to D.
8. Confirm the previous forward branch to C is discarded.

Refresh and ordinary row selection must continue to create no history entries.

## Theme and layout regression

Repeat target following in at least the active preferred theme and confirm:

- Address / Bytes / Instruction remains the same three-column layout;
- syntax highlighting remains unchanged;
- origin highlighting remains layout-neutral;
- selection does not change row height/width;
- no new hardcoded colors appear;
- context-menu styling follows the current theme.

## Cross-workspace regression

Smoke-test the already verified entry points:

- Scan Results -> **Open in Disassembler**;
- Saved Addresses -> **Open in Disassembler**;
- Memory Viewer -> **Open in Disassembler**.

Then use Follow Target and Back. The Disassembler must remain bound to the process/connection generation captured when it opened.

## Acceptance criteria

Rev11 is accepted when:

- clean Windows build succeeds;
- all **73 checks** pass;
- direct Call, Jump, and ConditionalJump targets can be followed when known;
- context menu, double-click, and Enter all use the same navigation semantics;
- indirect/unknown targets cannot be followed and no address is fabricated;
- Back/Forward includes successful Follow Target navigation;
- failed target reads preserve the previous view/history;
- existing syntax highlighting, continuous decode/origin resolution, and cross-workspace entry points remain intact.
