# PS5 x86-64 Disassembly Implementation

The complete host `0.1.6.rev14` Disassembler block is hardware-verified. Current host `0.1.7.rev36` / PS5 plugin `0.1.2.rev39` retains the same verified rev24 Iced decoder. Rev35 added a separate Iced-backed watchpoint-target resolver for Disassembler debugger actions; rev36 only corrects an `out`-parameter definite-assignment compile path in that resolver and does not change the decoder or memory-read boundary.

## Purpose

This document describes the PlayStation 5 implementation of the architecture-neutral Disassembly contracts introduced in TeeKay87's Memory Engine `0.1.6.rev1` and adopted by the PS5 plugin in host `0.1.6.rev2`. Host `0.1.6.rev3` added the first generic WPF workspace, rev4 added bidirectional context plus Scan Results/Saved Addresses entry points, and rev5 changed Core's around-origin orchestration to one continuous decode stream. Host `0.1.6.rev6` advances the plugin to `0.1.0.rev24` / Plugin API `2.11.0` so the same Iced formatter output can additionally provide architecture-neutral syntax-presentation tokens; host `0.1.6.rev7` keeps that plugin/API unchanged and adds only host/Core Memory Viewer value-span presentation. Host `0.1.6.rev11` consumes existing direct `BranchTarget` metadata, and host `0.1.6.rev12` adds shared selection/copy/export and region navigation; neither revision changes PS5 decoding semantics or target transport. The completed `0.1.6.rev14` block is fully hardware-verified. Host `0.1.7.rev1` added debugger contracts/Core coordination. Host `0.1.7.rev2` added only the generic Debugger workspace plus Mock backend. Host `0.1.7.rev3` advances PS5 to `0.1.0.rev25` / API `2.12.0` for its separate debugger provider/session, while the Iced decoding implementation and disassembly memory-read boundary remain unchanged from rev24. Host `0.1.7.rev4` changes only shared host target/header and button presentation; host `0.1.7.rev5` changes only responsive row-1 input sizing. Both leave the PS5 plugin/disassembly implementation untouched.

The implementation boundary is intentional:

```text
Core / WPF
  owns target/session selection and bounded memory reads
        |
        v
IDisassemblerProvider
  receives already-read bytes + TargetArchitecture
        |
        v
PS5 plugin
  performs x86-64 decoding only
```

No x86-64 decoder, ps5debug-NG command, PlayStation-specific process rule, or `eboot.bin` assumption belongs in the Core disassembly subsystem.

## Provider Identity

The connected PS5 session exposes:

```text
IDisassemblerProvider -> Ps5X64DisassemblerProvider
```

The plugin advertises:

```text
TargetCapabilities.Disassembly
```

The provider is stateless. It does not retain process ids, connection state, sockets, or memory-map state and does not call `Ps5DebugClient`.

## Supported Target Architecture

The provider accepts only the PS5 architecture already published by the session:

```text
CpuArchitecture:   X64
PointerWidthBits:  64
AddressWidthBits:  64
Endianness:        Little
```

A different CPU architecture, address/pointer width, or endianness is rejected with `NotSupportedException`. The provider does not silently reinterpret unsupported targets.

## Decoder

PS5 plugin `0.1.0.rev24` uses:

```text
Iced 1.21.0
```

Iced is a plugin-private dependency and is used only inside the PlayStation 5 project. Version `1.21.0` is distributed under the MIT license and targets .NET Standard 2.0, which is compatible with the plugin's .NET 9 target. The provider creates a 64-bit Iced decoder over the exact byte array supplied through `IDisassemblerProvider`, sets the decoder IP to the supplied start address, and uses the NASM formatter for operand presentation.

The neutral result keeps the formatter text split into:

```text
Mnemonic
Operands
```

while RawBytes and Length come from the exact decoded source slice. Rev24 additionally captures the NASM formatter's text-kind callbacks and maps them into Plugin API `2.11.0` neutral presentation categories:

```text
Mnemonic
FlowControlMnemonic
Register
Number
Keyword
Text
```

The plugin does not send Iced types across the SDK boundary. Adjacent formatter pieces with the same neutral category are merged, and concatenating all published token text must reproduce the exact combined `Mnemonic + Operands` presentation. WPF can therefore color the existing Instruction column without knowing x86-64 register names, opcode names, or Iced-specific enums. A provider that does not emit tokens remains valid and renders as ordinary text.

## Flow-Control Mapping

Iced flow categories are mapped to the Plugin SDK neutral `DisassemblyFlowControl` values:

| Iced flow | Neutral flow | BranchTarget |
| --- | --- | --- |
| `Call` | `Call` | direct near-branch target when available |
| `IndirectCall` | `Call` | `null` |
| `UnconditionalBranch` | `Jump` | direct near-branch target when available |
| `IndirectBranch` | `Jump` | `null` |
| `ConditionalBranch` | `ConditionalJump` | direct near-branch target when available |
| `Return` | `Return` | `null` |
| `Interrupt` | `Interrupt` | `null` |
| `Next` | `None` | `null` |
| all other categories | `Other` | `null` |

A direct destination is accepted only when operand zero is an Iced near-branch operand. The implementation deliberately does not attempt to resolve register- or memory-indirect control flow from static bytes.

This preserves the Plugin SDK rule that a provider must not fabricate a branch/call destination it cannot know statically.

## Invalid and Truncated Bytes

x86-64 has variable-length instructions. A bounded read can end in the middle of an instruction, and a user may later ask the workspace to begin decoding from an address that is not a real instruction boundary.

If Iced reports an invalid decode, the provider returns a neutral invalid record rather than discarding the consumed source byte(s):

```text
IsValid:      false
Mnemonic:     invalid
Operands:     empty
FlowControl:  Other
BranchTarget: null
RawBytes:     exact consumed source bytes
```

The provider additionally validates that a decoder result never claims zero bytes or extends beyond the supplied byte window. Such a decoder-state violation is treated as an internal error rather than being published as target data.

## Instruction-Boundary Limitation

The provider can decode the supplied bytes accurately from the address it is given, but it cannot infer historical program flow or prove that an arbitrary input address is the intended first byte of an instruction.

Therefore:

- a known instruction address can be decoded normally;
- an arbitrary scan/saved/memory address may fall inside a multi-byte instruction;
- rev5 decodes the complete bounded around-origin range as one stream, allowing an instruction that starts before the requested byte to remain intact and receive the origin marker when the requested byte falls inside it;
- the beginning of the context window remains best-effort on x86-64 because the provider still cannot prove that the first raw byte Core supplied is a canonical program boundary;
- the Disassembler workspace displays that limitation explicitly rather than presenting heuristic/raw-byte alignment as certain program flow;
- rev11 host Follow Target navigation treats a provider-supplied direct branch target as stronger context because it is an instruction entry point by construction, while still failing safely if the destination memory is unreadable or changed.

## Memory I/O Ownership

The provider performs no target memory reads.

The normal workflow remains:

1. Core identifies the containing readable, non-guarded `MemoryRegion`;
2. Core clamps the requested byte count to that region;
3. Core reads through the session's existing neutral `IMemoryReader`;
4. Core passes only the actual bytes read to `IDisassemblerProvider`;
5. the PS5 plugin decodes those bytes locally;
6. Core validates provider addresses/order/raw bytes before publishing a `DisassemblySnapshot`.

No new ps5debug-NG request is introduced by rev2 decoding.

## ps5debug-NG Server-Side Disassembly Review

Current ps5debug-NG includes a Zydis-based server-side disassembly operation:

```text
CMD_PROC_DISASM_REGION = 0xBDAA0020
```

The upstream response is designed around fixed-size analysis records containing fields such as instruction address, RIP-relative target, memory displacement, instruction length, flow kind, register/scale metadata, and compact mnemonic metadata.

That facility is useful, but its record shape is not a direct match for the already-verified Memory Engine `IDisassemblerProvider` contract, which requires the exact raw bytes plus complete mnemonic/operand presentation for each instruction. Core also already performs region-bounded memory reads for the neutral workflow.

For rev2, a local decoder inside the PS5 plugin therefore provides the cleanest ownership model:

```text
one neutral memory-read path
+ one platform-owned decode path
+ no duplicate PS5 transport in Core/WPF
```

The upstream command remains documented in `PS5DEBUG_NG_PROTOCOL_MAPPING.md`. A future server-side bulk-analysis or xref feature may use it if that feature receives its own explicit neutral contract and its semantics justify the backend operation.

## Plugin Dependency Deployment

The application loads platform plugins through `AssemblyDependencyResolver`. A plugin with a private NuGet dependency therefore needs its dependency graph next to the plugin at runtime.

PS5 plugin `0.1.0.rev24` sets:

```xml
<EnableDynamicLoading>true</EnableDynamicLoading>
```

The shared root `Directory.Build.targets` exposes a `GetPluginDeploymentFiles` build target that returns:

- the plugin assembly;
- the plugin `.deps.json` when generated;
- private copy-local dependency assemblies.

Consumer projects mark runtime plugin project references with `DeployAsPlugin=true`. The same shared post-build deployment target then copies the reported files into that consumer's `Plugins` output directory. Both the WPF application and the verification executable use this path, so the plugin-discovery test exercises the same dependency-complete isolated layout expected by production. The shared `TeeKay87.MemoryEngine.PluginSdk` assembly is deliberately excluded from the plugin-private dependency list because the loader binds plugins to the host's contract assembly.

For the PS5 plugin, a normal application build is therefore expected to place at least these files under the output `Plugins` directory:

```text
TeeKay87.MemoryEngine.Platform.PS5.dll
TeeKay87.MemoryEngine.Platform.PS5.deps.json
Iced.dll
```

## Automated Fixture

Rev2 adds deterministic provider coverage using a fixed x86-64 byte stream containing:

```text
nop
mov register, register
add immediate
sub immediate
cmp immediate
call rel32
jmp rel8
conditional jump rel8
ret
nop
RIP-relative mov
```

The checks verify instruction ordering/count/length, expected mnemonics, register operand formatting, direct branch/call target calculation, Return classification, RIP-relative decode, validity, exact consumption of the supplied byte array, and rev24 syntax-token reconstruction/classification for ordinary instructions and flow-control.

A separate safety check verifies:

- the connected PS5 architecture is accepted;
- 32-bit x86 is rejected;
- big-endian x86-64 is rejected;
- truncated input becomes a bounded invalid record;
- invalid input does not receive a fabricated branch target;
- indirect CALL/JMP retain their neutral flow category while leaving `BranchTarget` empty;
- pre-cancelled decoding returns cancellation;
- ARM64 input is rejected.

## Host Workspace Integration and Deferred UI Work

Host `0.1.6.rev6` consumes the provider through the same neutral path:

```text
DisassemblerWindow / DisassemblerViewModel
        |
        v
PluginViewModel foreground-target coordination
        |
        v
Core DisassemblyReader
        |
        +--> IMemoryReader
        +--> IDisassemblerProvider
```

The host workspace provides Address/Go To/Refresh, working Back/Forward history, region/protection/architecture context, Address/Bytes/Instruction rows, a layout-neutral origin marker, and up to 512 bytes of pre-origin context plus 512 bytes from the requested origin. The complete returned range remains one provider decode, so an instruction may begin before the requested address and continue across it; the row containing the requested byte becomes the origin row. The first rows at the arbitrary context start remain best-effort on x86-64.

Rev6 keeps the same three-column list but consumes optional neutral syntax tokens for theme-aware Instruction text. Scan Results, Saved Addresses, and Memory Viewer expose capability-/session-driven **Open in Disassembler** entry points. The PS5 provider remains decode-only and is not referenced directly by WPF.

Host rev11 now adds direct branch/call target following by consuming the neutral provider records already returned here; the PS5 provider itself remains unchanged. Rev11 still does not add:

- dynamic/indirect target resolution without a static `BranchTarget`;
- assembler/instruction editing;
- debugger, breakpoint, watchpoint, register, thread, step, or call-stack behavior.

Those later features must continue to reuse the neutral provider/Core foundation rather than call this PS5 provider directly from WPF.


> Host `0.1.7.rev6` builds on the fully verified rev5 baseline. Its Threads/Thread Control and passive header-status cleanup do not redesign the subsystem documented here.

## Host Logical Breakpoint Presentation (`0.1.7.rev29`)

PS5 plugin `0.1.0.rev36` and its Iced x86-64 decoder are unchanged in rev29. ps5debug-NG may implement a Software/Execute breakpoint by writing `0xCC` to the first byte of the instruction. The host now prevents that debugger-owned trap byte from changing the user-visible disassembly: Core applies the original instruction bytes captured before breakpoint installation to a local copy of the bounded read buffer before calling the PS5 decoder. The decoder still receives ordinary caller-supplied bytes and contains no breakpoint-specific branch.

This does not restore or patch PS5 target memory. The backend breakpoint remains armed exactly as before. `Breakpoint`, `Breakpoint (disabled)`, and `Watchpoint hit` are neutral host markers and do not become PS5-specific instruction metadata. Future intentional assembly/NOP patching must remain distinguishable from this debugger-only logical view.


## Rev35 Disassembler Watchpoint Target Resolution

Plugin API `2.18.0` adds the optional neutral `IDisassemblyWatchpointResolver`. PS5 implements it with the same Iced decoder already used by `Ps5DisassemblerProvider`, keeping x86-64 operand interpretation platform-specific. The host supplies one logical `DisassembledInstruction` plus the current paused debugger register snapshot and receives either a neutral `DisassemblyWatchpointTarget` or no result.

The resolver requires exactly one explicit memory operand. It derives the hardware-watchpoint width from Iced memory-size metadata and accepts only the PS5 debugger-supported 1, 2, 4, and 8-byte widths. Effective address resolution supports ordinary 64-bit GPR base/index/scale/displacement addressing, RIP-relative operands, and FS/GS addressing only when the corresponding neutral base register is present. If a required register is unavailable or the address cannot be established without guessing, resolution fails.

Memory access classification uses Iced instruction-info operand access. Definite writes map to neutral `Write`; reads and read/write accesses map to `ReadWrite` because amd64 hardware data breakpoints cannot express a read-only trap independently of writes. Address-only instructions such as `lea`, non-memory instructions, ambiguous/multiple explicit memory operands, unsupported sizes, and unsafe self-modifying address-register cases are rejected.

This service only derives a candidate. The WPF host still requires exactly one Disassembler row, a matching attached and Paused debugger with a current register snapshot, and successful validation through the existing `IDebuggerBreakpointValidationService` before **Add Watchpoint** becomes available. Host rev38 no longer restricts derivation to the current stop/trigger row: any single instruction in the current view may be evaluated. For base/index addressing the result is explicitly derived from the debugger's current paused register state, while RIP-relative operands can resolve directly from instruction metadata. Unsafe or ambiguous cases remain unavailable rather than guessed.
