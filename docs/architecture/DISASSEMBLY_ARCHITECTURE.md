# Architecture-Neutral Disassembly

## Purpose

Application `0.1.6` begins the Disassembler feature block. The long-term goal is one host/Core workflow that can inspect decoded instructions for any platform plugin without embedding target-specific instruction semantics, protocol commands, module assumptions, or decoder implementations in the WPF application or Core.

Application `0.1.6.rev1` established the neutral contracts and Core read/decode foundation. Rev2 added the first real platform provider in the PS5 plugin, rev3 added the first host workspace, rev4 added bounded around-origin context and cross-workspace entry points, and rev5 corrected origin handling to one continuous decode stream. Rev6 added optional neutral syntax tokens plus successful-address Back/Forward history, rev7/rev8 improved Memory Viewer origin/value-span presentation without changing the decoder, rev9/rev10 added and corrected manual Saved Address entry, and rev11 added direct Call/Jump/ConditionalJump target following using only provider-supplied `BranchTarget` metadata. Application `0.1.6.rev12` combined the remaining Disassembler completion work: extended selection/copy, structured universal export, shared readable-region navigation, real module-relative origin presentation, and correction of the Mock provider's architecture declaration from X64 to Custom / Unknown. Its 77-check suite passed, but Mock runtime acceptance exposed a host-only context-menu selection defect. Application `0.1.6.rev13` corrected that defect by preserving an existing Extended selection when any already-selected row is right-clicked and by evaluating Follow Target for the context row without rewriting the TwoWay-bound primary selection. Rev13 then passed 78 automated checks and the focused Mock selection regression, where runtime testing exposed one remaining copy inconsistency: the four granular copy commands still projected only the context-clicked row. Application `0.1.6.rev14` corrected that final host copy path by projecting every granular clipboard format from the complete display-ordered selected-row set. The complete `0.1.6.rev14` block subsequently passed all 79 automated checks plus deterministic Mock and live-PS5 hardware acceptance and is now the verified disassembly base. Application `0.1.7.rev1` established the separate verified Debugger contracts/Core coordination. Application `0.1.7.rev2` added the modeless Debugger workspace and Mock debugger backend without changing the verified Disassembler workflow or either disassembly provider implementation. Application `0.1.7.rev3` advances the PS5 plugin to `0.1.0.rev25` only to consume Plugin API `2.12.0` debugger contracts; its rev24 Iced disassembly implementation remains unchanged. Application `0.1.7.rev4` changes only host target/header and shared button presentation, and application `0.1.7.rev5` changes only responsive row-1 target input sizing. The verified Disassembler workflow and both disassembly providers remain unchanged. Assembly, instruction editing, dynamic indirect-target resolution, and debugger execution remain outside the `0.1.6` feature block.

## Architectural Boundary

The primary rule is:

> Core defines what a disassembly workflow needs to do. A platform plugin defines how bytes for its target architecture become instructions.

Core may own:

- bounded memory reads;
- readable-region validation;
- presentation-neutral disassembly snapshots;
- instruction ordering/range validation;
- target/session identity;
- navigation/history/origin state;
- later branch-follow semantics;
- later export data.

A plugin may own:

- target-specific decoding;
- architecture-specific instruction semantics;
- backend-specific disassembly transport when a backend provides one;
- decoder integration and target-specific quirks.

Core and WPF must not contain target checks such as PS5, `eboot.bin`, ps5debug command ids, x86-64 opcode tables, or other platform-specific decode rules.

## Debugger-Owned Logical Code Overlay (`0.1.7.rev29`)

The verified `0.1.6` decoder contract remains unchanged, but host `0.1.7.rev29` adds a presentation-neutral logical-code layer between the target read and provider decode when a matching attached Debugger session exists. A Software/Execute breakpoint may be implemented by a backend by replacing the first target byte with `0xCC` (`INT3`). Those debugger-owned trap bytes are runtime instrumentation, not the game's instruction stream, and must not be presented as if they were original code.

`DebuggerDisassemblyOverlayState` therefore retains the original `DisassembledInstruction` already captured before Software/Execute breakpoint installation. `PluginViewModel` resolves an overlay only from an attached debugger session whose plugin id, process identity, and connection generation match the Disassembler target. `DisassemblyReader` performs the normal bounded target read first, creates a local byte copy only when an overlay overlaps the range, restores the known original bytes into that local copy, and sends the logical bytes to the ordinary plugin decoder. It never writes the restored bytes to target memory. Provider result validation and the resulting `DisassemblySnapshot.Bytes` are based on the logical buffer so Address, Bytes, and Instruction stay internally consistent.

The same overlay carries address-scoped `DisassemblyMarker` metadata. Rev29 uses `Breakpoint`, `Breakpoint (disabled)`, and `Watchpoint hit`; a hardware watchpoint does not alter instruction bytes, so it contributes only marker metadata at the reported stop instruction pointer. A software breakpoint removed or disabled while a paused backend cleanup is staged keeps its original-byte overlay until cleanup is known to have completed, preventing a still-armed backend `INT3` from leaking into the visible code stream.

This boundary is deliberately separate from future intentional assembly editing. A later NOP/patch workflow must model user-requested code changes explicitly and compose them with debugger instrumentation rather than treating a debugger trap byte as either original code or a user patch. Rev29 therefore establishes the original/logical instruction view needed by later cheat construction without implementing code patching itself.

## Existing Target Architecture Model

Disassembly reuses the existing Plugin SDK `TargetArchitecture` model. No parallel disassembly-only architecture definition is introduced.

The existing model already carries:

- `CpuArchitecture`;
- pointer width;
- address width;
- endianness.

`IDisassemblerProvider.SupportsArchitecture(...)` lets a provider reject an architecture it does not implement before decoding begins.

## Capability Model

`TargetCapabilities.Disassembly` already existed as a reserved neutral capability. Application `0.1.6.rev1` activated that capability for the Mock plugin and added the public provider contract that gives the capability concrete behavior. Application `0.1.6.rev2` also advertises the capability from the PS5 plugin because its connected session now exposes a real `IDisassemblerProvider`.

Capability flags remain independent. A future plugin may legitimately support a combination such as:

```text
MemoryRead       YES
MemoryWrite      NO
Disassembly      YES
Debugger         NO
```

The Disassembler entry points therefore test capabilities/services generically and do not infer support from a platform name.

## Plugin API 2.11.0 Disassembly Surface

Plugin API `2.10.0` introduced the first public disassembly contract surface:

```text
IDisassemblerProvider
DisassembledInstruction
DisassemblyFlowControl
```

Plugin API `2.11.0` extends that surface additively with:

```text
DisassemblyTextToken
DisassemblyTextTokenKind
DisassembledInstruction.SyntaxTokens
```

The original `DisassembledInstruction` constructor remains available with its previous signature and produces an empty syntax-token collection. A provider that targets an older compatible Plugin API 2.x minor therefore remains loadable and renders plain instruction text. Providers targeting `2.11.0` may opt into semantic presentation metadata without changing `IDisassemblerProvider` itself.

The host compatibility rule remains unchanged: plugins must share the host major API version and may target the same or an older minor API version. PS5 plugin `0.1.0.rev25` now targets API `2.12.0` for its separate debugger backend, while its existing rev24 Disassembler implementation remains unchanged. Mock plugin `1.0.0.rev8` also targets host API `2.12.0` for its debugger backend, while its existing Disassembler implementation and `CpuArchitecture.Unknown` declaration remain unchanged from rev7 because the deterministic instruction set is synthetic rather than x86-64.

Host `0.1.7.rev1` advanced the overall Plugin API to `2.12.0` for the debugger contract surface, and rev2-rev5 keep that API version. The disassembly contracts themselves remain the verified `2.10.0`/`2.11.0` surface; both current built-in plugins target `2.12.0` because of their separate debugger services, not because the disassembly contract changed.

## `IDisassemblerProvider`

A provider receives:

```text
start address
raw bytes
target architecture
cancellation token
```

and returns ordered `DisassembledInstruction` records.

The provider does not read target memory itself. This separation is intentional: memory I/O continues to use the existing neutral `IMemoryReader` path while the provider only interprets the bytes supplied to it.

A provider also exposes `SupportsArchitecture(TargetArchitecture)` so unsupported targets fail explicitly instead of being decoded under guessed assumptions.

## Neutral Instruction Model

`DisassembledInstruction` contains the minimum architecture-neutral data needed by the planned Disassembler workflow:

```text
Address
RawBytes
Length
Mnemonic
Operands
FlowControl
BranchTarget?
IsValid
SyntaxTokens
```

`Length` is derived from the copied raw-byte payload, preventing a provider from reporting a separate length that disagrees with the bytes it returned.

`RawBytes` are defensively copied by the model so later changes to a provider-owned source buffer cannot mutate an already-created instruction record.

`Mnemonic` and `Operands` remain separate in the model even though the WPF workspace renders them together as one Instruction column.

### Syntax/presentation tokens

Plugin API `2.11.0` adds an optional `SyntaxTokens` collection to each instruction. The collection is presentation metadata, not a replacement for the structured instruction fields. Its neutral categories are:

```text
Text
Mnemonic
FlowControlMnemonic
Register
Number
Keyword
```

The provider owns architecture-specific tokenization. For example, the PS5 plugin may learn from Iced that `rax` is a register, but WPF receives only a `Register` token and never contains a table of x86 register names. The host validates presentation defensively: the Instruction cell uses tokenized runs only when their concatenated text exactly matches the existing `Mnemonic + Operands` display string; otherwise it falls back to the original plain text. A provider may omit tokens entirely.

`FlowControlMnemonic` is deliberately separate from ordinary `Mnemonic` so calls, jumps, returns, interrupts, and other control-flow mnemonics can be distinguished visually using the already-neutral `DisassemblyFlowControl` classification without parsing mnemonic strings in the host.

### Flow control

The initial neutral flow-control categories are:

```text
None
Call
Jump
ConditionalJump
Return
Interrupt
Other
```

A direct `BranchTarget` is valid only for Call, Jump, or ConditionalJump. The target is optional because indirect flow such as a register- or memory-indirect call/jump may not have a statically known destination.

Invalid/undecodable bytes are represented with `IsValid == false`. They still retain at least one source byte and an explicit mnemonic supplied by the provider. An invalid record cannot claim a direct branch target.

The initial model deliberately does not expose a complete operand AST. Register/memory operand decomposition can be added later when debugger or cheat-building requirements justify a deliberate API extension.

## Core Disassembly Reader

`TeeKay87.MemoryEngine.Core.Disassembly.DisassemblyReader` owns the first common read/decode pipeline.

The reader keeps the original exact-start `ReadAsync` contract introduced in rev1 and adds a separate `ReadAroundAsync` context path in rev4. The WPF workspace uses `ReadAroundAsync`; existing exact-start callers/tests retain their established semantics.

The rev5 context workflow is:

```text
requested origin
      |
      v
find containing readable, non-guarded MemoryRegion
      |
      v
choose up to 512 bytes before + 512 bytes from origin
      |
      v
clamp both sides independently to the same region
      |
      v
one bounded IMemoryReader.ReadAsync(...)
      |
      v
decode the complete returned byte range as one continuous stream
      |
      v
validate instruction range/order/raw-byte mapping
      |
      v
DisassemblySnapshot
```

Rev5 deliberately changes only decode orchestration, not target I/O. The same single bounded memory read used by rev4 remains in place; Core now invokes the provider once for the whole range instead of invoking it separately for the pre-origin and origin/forward slices.

`DefaultWindowByteCount` remains 512 for the original forward-only read. `DefaultContextByteCount` is 512 per side for the current workspace, producing at most 1,024 bytes under the default policy. The absolute Core operation limit remains 65,536 bytes.

### Region rules

A decode request is accepted only when the requested address belongs to one `MemoryRegion` that:

- has non-zero size;
- includes `MemoryProtection.Read`;
- does not include `MemoryProtection.Guard`.

Execute permission is intentionally not required by Core. A memory-development tool may need to inspect bytes in a readable mapping even when the backend does not classify it as executable. Protection is retained in the snapshot so a future UI can show the backend-reported state without inventing semantics.

The reader never stitches bytes across regions. For `ReadAroundAsync`, unavailable pre-origin and post-origin context are clamped independently; Core does not compensate for missing bytes on one side by crossing a region boundary or expanding the other side beyond the caller's requested context.

### Partial reads

An `IMemoryReader` may return fewer bytes than requested. Core accepts a positive partial read only when the returned contiguous range still contains the requested origin, shrinks the decode buffer to the actual byte count, and validates every returned instruction against that actual range. Zero-byte, negative-count, oversized-count, and partial ranges that stop before the origin are rejected.

### Provider result validation

Core validates provider output before it becomes a `DisassemblySnapshot`:

- the instruction collection must not be null;
- every instruction must stay inside the actual bytes read;
- instructions must be in ascending non-overlapping address order;
- each instruction's `RawBytes` must exactly match target bytes at its reported address.

A provider may omit bytes if it intentionally does not produce a record for them, but it may not reorder, overlap, or fabricate the bytes represented by a record.

## Disassembly Snapshot

`DisassemblySnapshot` is presentation-neutral and retains:

- requested address;
- decode start address;
- copied raw bytes;
- containing `MemoryRegion`;
- `TargetArchitecture`;
- decoded instruction records.

The requested and start addresses are distinct properties. In the current context path, `StartAddress` may be up to 512 bytes before `RequestedAddress`; the snapshot enforces that the requested origin is actually contained by the returned byte range. The requested address remains the user's navigation origin even when the continuous decode resolves it to an instruction whose `Address` is earlier. This separation was intentionally established in rev1 so pre-context and later origin resolution could be added without changing the public Plugin SDK instruction model.

## Instruction Boundaries

The subsystem does not claim that an arbitrary requested address or an arbitrary pre-context start is a real instruction boundary.

This is particularly important for x86-64 because instructions are variable length. Rev4 protected the exact requested byte by forcing a second decode stream at `RequestedAddress`, but live PS5 testing showed the downside of that approach: if the requested byte was inside a legitimate multi-byte instruction, the pre-origin stream was truncated and the origin stream decoded the interior bytes as unrelated instructions.

Rev5 removes that artificial seam. `ReadAroundAsync` now decodes the full bounded context as one continuous provider stream. When that stream contains an instruction whose address range covers `RequestedAddress`, the WPF row model marks that instruction as the origin row even if the instruction started before the requested byte. The user's requested address itself is not rewritten or silently snapped backward.

The remaining limitation moves to the beginning of the context window: raw bytes alone still cannot prove that `StartAddress` is a canonical instruction boundary on a variable-length architecture. The first rows are therefore best-effort. With hundreds of bytes of preceding context, a decoder may naturally regain the intended stream before the origin, but the host does not claim mathematical certainty without stronger architecture/provider metadata. The workspace states this limitation explicitly.

## Target and Connection Identity

`DisassemblySessionIdentity` captures:

```text
PluginId
ProcessId
ProcessName
ConnectionGeneration
```

The process display label is deliberately excluded from identity because it is presentation data. The stable host comparison uses plugin id, process id, process name, and connection generation.

This gives the Disassembler workspace the same safety concept already proven by Memory Viewer: an analysis window opened for one plugin/process/connection generation rejects refresh/navigation if the host has switched to another Active Target or reconnected.

Rev1 provided the neutral identity primitive and automated verification. Rev3 applies it in the WPF workspace and revalidates the current target/connection generation before each read.

## I/O Coordination

`DisassemblyReader` depends only on `IMemoryReader`; it does not create a transport, socket, or platform command stream.

Rev3 routes workspace reads through the existing foreground-target reservation used by explicit target operations. `PluginViewModel` performs the host/session/capability/current-target checks, snapshots the current memory map, and invokes `DisassemblyReader` through neutral `IMemoryReader` and `IDisassemblerProvider` services. The UI does not call ps5debug or any other platform transport directly.

The PS5 provider continues to use Iced only on the byte window supplied by Core and sends no additional ps5debug-NG command while decoding.

## PS5 x86-64 Provider

PS5 plugin `0.1.0.rev24` is the first real architecture provider. It accepts only the target architecture already declared by the PS5 session:

```text
CPU:            X64
Pointer width:  64 bits
Address width:  64 bits
Endianness:     Little
```

The provider uses Iced `1.21.0` inside the PS5 plugin. Iced is not referenced by Core, the WPF host, or the Plugin SDK. The provider uses a 64-bit decoder and NASM formatter to populate the neutral instruction model. In rev24 it also captures Iced formatter token kinds and maps them to Plugin API `2.11.0` presentation categories before returning the instruction. Iced-specific `FormatterTextKind` values never leave the PS5 project.

Flow-control translation is intentionally conservative:

| Decoder classification | Neutral classification | Direct target |
| --- | --- | --- |
| direct call | `Call` | only when statically encoded |
| indirect call | `Call` | none |
| direct unconditional branch | `Jump` | only when statically encoded |
| indirect branch | `Jump` | none |
| conditional branch | `ConditionalJump` | when statically encoded |
| return | `Return` | none |
| interrupt | `Interrupt` | none |
| ordinary fall-through | `None` | none |
| other flow | `Other` | none |

Invalid or truncated input is retained as bounded invalid instruction data instead of being silently discarded. Cancellation is checked before decoding starts and between decoded records. Unsupported architectures fail explicitly.

### Backend decision

Current ps5debug-NG also contains a Zydis-based server-side disassembly-region operation, `CMD_PROC_DISASM_REGION` (`0xBDAA0020`). Its fixed-size analysis records expose useful server-side metadata such as instruction address/length, flow information, RIP-relative or memory-displacement details, and compact mnemonic metadata.

That command is not used by the rev2 provider. The neutral contract already verified in rev1 requires the exact raw instruction bytes plus complete mnemonic/operand presentation, and Core already owns the bounded memory read that supplies those bytes. Using Iced on the supplied window keeps the normal workflow on one neutral I/O path and avoids adding a second PS5-specific read/disassembly transport to the host.

The upstream operation remains relevant for possible future server-side analysis or xref-oriented work. Such use must be added as a separate capability/service only if it provides semantics needed by a future feature; it must not replace the existing neutral decoder contract implicitly.

### Plugin dependency deployment

The host loads platform plugins through `AssemblyDependencyResolver`. Because the PS5 plugin now has a private package dependency, its build enables dynamic loading metadata. Platform `ProjectReference` entries used as runtime plugins are marked `DeployAsPlugin=true`; the shared root deployment target then copies the plugin assembly, its `.deps.json`, and private copy-local dependencies such as `Iced.dll` into the consuming application's or verification executable's `Plugins` directory. `TeeKay87.MemoryEngine.PluginSdk.dll` remains a host-shared contract assembly and is not copied as a private plugin dependency.

## Mock Provider

Mock plugin `1.0.0.rev8` retains the deterministic provider for the neutral disassembly contract. It is intentionally deterministic and exists to test the shared architecture without real hardware.

The Mock target exposes a small byte fixture at `MockTargetLayout.CodeAddress`. `MockDisassemblerProvider` interprets those bytes using a synthetic Mock instruction set containing examples of:

- ordinary instructions;
- direct call;
- direct conditional jump;
- return;
- no-op;
- invalid/truncated instruction representation.

This provider is **not** an x86-64 decoder. Mock rev7 therefore declares a custom/unknown CPU architecture while retaining 64-bit address width, 64-bit pointer width, and little-endian byte order. The provider explicitly accepts that descriptor and rejects real X64. It remains intentionally separate from the PS5 plugin's real x86-64 provider so generic contract/Core tests do not depend on a platform decoder. It also emits deterministic neutral syntax tokens (`Mnemonic`, `FlowControlMnemonic`, `Register`, `Number`, and `Text`) for the same synthetic instructions so presentation metadata can be verified without hardware.

The existing Mock health/ammo/money addresses, memory-region shape, read/write behavior, Value Types, and scanner services remain unchanged.

## Current Host Workspace

Application `0.1.6.rev14` remains the fully tested and hardware-verified final implementation of the standalone `0.1.6` Disassembler block. Host `0.1.7.rev29` extends that workspace only for Debugger integration by adding logical original-byte overlays and address-scoped Markers. The main workspace exposes **Disassembler...** only when the selected plugin advertises `TargetCapabilities.Disassembly` and the current Active Target can provide the neutral memory/disassembly services. The workspace captures plugin id, process identity, and connection generation and revalidates them before every read so reconnect/target replacement cannot silently redirect an existing window.

Each successful navigation requests up to 512 bytes before and 512 bytes from the requested address, independently clamped to the same readable non-guarded region. Core performs one bounded read and one continuous provider decode. The decoded instruction containing the requested byte is the origin row, even when that instruction begins before the requested address. The first decoded row of an arbitrary variable-length context remains explicitly best-effort because raw bytes alone cannot prove a canonical boundary outside the visible buffer.

The user-facing table is Address / Bytes / Markers / Instruction from host `0.1.7.rev29`. `Markers` carries debugger presentation metadata while Address, Bytes, and Instruction continue to describe the logical/original instruction stream. Syntax highlighting is driven only by optional neutral tokens. Host `0.1.7.rev15` removes the long visible-range explanatory paragraph from the Disassembler and its dedicated spacer row; Region / Module, Visible Range, Protection, Architecture, navigation controls, and the instruction table remain otherwise unchanged. Rev12 changes selection to Extended and adds host-only copy actions without changing row geometry or target I/O behavior. Rev13 preserves that Extended selection during context-menu opening: right-clicking any row already contained in a multi-selection leaves the complete `SelectedItems` set intact, while right-clicking outside the selection intentionally selects only the new context row. Context-menu capability evaluation must not mutate the TwoWay-bound `SelectedInstruction` merely to inspect the clicked row. Rev14 makes Copy Address, Copy Bytes, Copy Instruction, Copy Address + Instruction, and Copy Selected all consume the same display-ordered selected-row set. A right-click outside the current selection still reduces that set to the clicked row before any copy command executes.

Successful Go To, Follow Target, Previous Region, Region Start, Region End, and Next Region operations all use the same navigation/history path. Refresh is history-neutral. Previous/Next Region reuse Core `MemoryViewerRegionNavigator`, so the Disassembler and Memory Viewer share the same readable/non-guarded region policy. Region End means `EndAddressExclusive - 1`.

When the current memory map contains a real `ModuleName`, the host resolves the lowest mapped base for that module and presents the requested origin as `<module> + 0x<offset>`. Anonymous regions remain anonymous. This is presentation metadata; no module name or executable assumption is fabricated in Core.

Rev12 also connects the Disassembler to the existing universal export architecture. The workspace provides Displayed Instructions and, when rows are selected, Selected Instructions. `DisassemblyExportSource` exposes structured Address, Bytes, Instruction, Mnemonic, Operands, Length, Flow Control, Branch Target, Valid, Region / Module, Protection, and Module Relative columns through `IExportDataSource`. The normal JSON/CSV/TSV/Markdown writers, column selector, progress/cancellation UI, and transactional destination publication are reused. Export reads the already materialized Disassembler snapshot and does not issue additional target reads.

Direct Follow Target remains gated solely by valid neutral Call/Jump/ConditionalJump records with a non-null provider-supplied `BranchTarget`. Indirect branches/calls remain unresolved; WPF never derives a destination from operand text.

## Deferred Work After 0.1.6

Rev12 deliberately leaves the following for later feature blocks:

- dynamic/indirect branch target resolution requiring debugger/register context;
- assembly or instruction editing/patching;
- breakpoints, watchpoints, registers, threads, stepping, or call stacks;
- Find What Writes/Accesses;
- decompilation or symbol-server support;
- broader project/cheat construction workflows that consume disassembly.

Selection, context-menu-safe multi-selection, selection-consistent copy, universal export, readable-region navigation, module-relative origin presentation, static direct-target following, syntax highlighting, around-origin context, and session-safe cross-workspace navigation are part of the rev14 final implementation candidate and are no longer deferred.

## Extension Rules

Future disassembly development should preserve these rules:

1. Do not put architecture-specific opcode logic in Core or WPF.
2. Do not let a decoder initiate target I/O when Core can supply bounded bytes through `IMemoryReader`.
3. Do not create a second target-architecture model.
4. Do not infer a direct branch target for an indirect instruction.
5. Do not cross memory-region boundaries to fill a decode window.
6. Do not silently follow a changed Active Target or connection generation.
7. Keep provider results presentation-neutral so debugger and export subsystems can reuse them.
8. Keep PS5 implementation documentation under `docs/plugins/PS5/` once the PS5 provider is added.
9. Keep the around-origin stream continuous. Treat the beginning of arbitrary variable-length context as best-effort, and mark the decoded instruction that contains the requested origin without silently rewriting the user's requested address.
10. Keep syntax token kinds architecture-neutral. A host renderer may color `Register` or `Number`, but it must not identify architecture-specific register names or parse opcode syntax itself.
11. Keep syntax metadata optional and preserve plain-text fallback so older compatible providers remain usable.
12. Do not rewrite the TwoWay-bound primary instruction selection merely to evaluate a context-clicked row; row-specific context capability checks must preserve an existing Extended selection.


> Host `0.1.7.rev6` builds on the fully verified rev5 baseline. Its Threads/Thread Control and passive header-status cleanup do not redesign the subsystem documented here.

## 0.1.7.rev32 — Watchpoint Trigger Resolution

The logical disassembly pipeline is now also used as evidence for watchpoint-trigger resolution. Core does not decode x86 instructions itself. A paused unresolved watchpoint event is paired with a bounded logical `DisassemblySnapshot`; if exactly one valid instruction ends at the debugger's real stop/current instruction pointer, that instruction becomes the `DisassemblyDerived` trigger. Existing byte overlays therefore continue to hide backend `INT3` instrumentation from this analysis.

A resolved watchpoint marker is attached to the trigger instruction. An unresolved stop is explicitly labeled as unresolved at the current instruction instead of reusing the current IP as a false trigger address. This is presentation metadata only and does not write target memory.

## 0.1.7.rev33 / rev38 — Direct Debugger Address Action

The Disassembler context menu exposes separate **Add Breakpoint** and **Add Watchpoint** commands. Exactly one selected instruction is required; an Extended multi-selection keeps both entries visible but disabled. Add Breakpoint is enabled only for a valid instruction in the current executable region and only when the already attached Debugger for the same plugin/process/connection generation accepts the neutral Software/Execute request. Add Watchpoint is intentionally stricter: the debugger must be Paused, current registers must be available from the matching paused debugger, and the plugin's optional `IDisassemblyWatchpointResolver` must resolve exactly one safe effective data address/width/access tuple. For base/index addressing, the derived address reflects the current paused register values and is therefore a current-context candidate rather than proof that an unrelated instruction previously executed with those values. RIP-relative addressing can resolve without dynamic GPR state. The resulting Hardware request still passes the existing plugin validator before add. No debugger is implicitly attached from Disassembler and no architecture-specific operand logic moves into Core/WPF.

This action does not change the Disassembler's multi-row copy/export semantics. It also does not bypass logical byte overlays: Software/Execute debugger instrumentation remains presentation state rather than original instruction data.


## Watchpoint hit and stop highlighting

Watchpoint presentation deliberately separates the instruction that caused the memory access from the instruction where the backend reports execution stopped.

- **Green / `SuccessMutedBrush`** marks a resolved **Hit / Trigger Instruction** and carries the `Watchpoint hit` marker.
- **Yellow / `WarningMutedBrush`** marks the real **Stop / Current IP** when it differs from the trigger and carries the `Stop / Current IP` marker.
- If trigger resolution fails, no green hit is invented. The current/stop instruction alone is shown in yellow with `Watchpoint stop (trigger unresolved)`.
- Software breakpoint presentation remains unchanged; the breakpoint/origin instruction retains the existing green presentation and logical/original bytes continue masking physical debugger instrumentation.

`WarningMutedBrush` is derived from each theme's `WarningText` palette value at runtime, matching the existing derived `SuccessMutedBrush` approach so Light, Dimmed, and Dark keep the same semantics without hard-coded view colors.
