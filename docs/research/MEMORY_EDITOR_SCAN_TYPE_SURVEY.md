# Memory Editor Scan Type Survey

## Purpose

This document records the external memory-editor survey used as a design reference for Value Type, Scan Type, and scanner-option concepts in TeeKay87's Memory Engine.

The survey originally informed the `0.1.3.rev2` Core-catalog experiment. Rev3 temporarily moved Scan Type definitions into plugins. Rev25 restores the cleaner final ownership split: Value Types remain plugin-owned, while common scan predicates are Core-owned and shared by every platform plugin. Rev26 completes the current 13-mode Core set with Fuzzy Value and Unknown Initial Low Value and adds a generic semantic Core-to-native mapping contract so plugins can accelerate equivalent backend operations without taking ownership of the predicates. The vocabulary below remains research input for future Core additions; specialized platform mechanisms should still be modeled separately rather than forcing transport behavior into a Scan Type.

The survey intentionally distinguishes between:

- **scan value types** — representations that a memory scanner can search for;
- **scan types / comparison modes** — predicates used by First Scan or Next Scan;
- **scanner options** — endianness, alignment, address ranges, writable/readable filters, rounding/tolerance, comparison baselines, and similar modifiers;
- **other reverse-engineering data types** — pointer, vector, matrix, vtable, function, enum, and structure-viewer node types that are useful elsewhere but are not necessarily scanner value types.

This distinction prevents scanner definitions from becoming a list of unrelated Memory Viewer or debugger concepts.

## Sources reviewed

### Cheat Engine

Sources:

- https://wiki.cheatengine.org/index.php?title=Cheat_Engine:Memory_Scanning
- https://wiki.cheatengine.org/index.php?title=Help_File:Value_types
- https://wiki.cheatengine.org/index.php?title=Help_File:Scan_types
- https://wiki.cheatengine.org/index.php?title=Lua:Class:MemScan
- https://github.com/cheat-engine/cheat-engine/blob/master/Cheat%20Engine/bin/defines.lua

Scanner value-type families found:

- Byte / 1 byte
- Word / 2 bytes
- Dword / 4 bytes
- Qword / 8 bytes
- Float / Single
- Double
- Text / String
- Array of Bytes
- Binary
- All
- Custom
- Grouped

Cheat Engine also has internal `vtPointer` and `vtAutoAssembler` identifiers, but its own definitions describe those as structure/autoguess or Auto Assembler concepts rather than normal memory-scan value types. They are therefore not treated as scanner Value Type entries in Memory Engine.

First-scan comparison modes found:

- Exact Value
- Bigger Than
- Smaller Than
- Value Between
- Unknown Initial Value

Next-scan comparison modes found:

- Exact Value
- Bigger Than
- Smaller Than
- Value Between
- Increased Value
- Increased Value By
- Decreased Value
- Decreased Value By
- Changed Value
- Unchanged Value
- Same as First Scan / comparison against a first or saved snapshot

Cheat Engine additionally supports percentage-tolerance variants for range and increase/decrease comparisons. The comparison baseline can be changed to the previous, first, or a saved scan. Baseline selection is a scan-state option rather than a fundamentally different memory representation.

### PINCE

Sources:

- https://github.com/korcankaraokcu/PINCE/wiki/Scanning
- https://github.com/korcankaraokcu/PINCE/wiki/Libpince-Engine
- https://github.com/korcankaraokcu/PINCE/blob/master/README.md

Scanner value-type families found:

- Int8 / Int16 / Int32 / Int64
- Float32 / Float64
- Int(any)
- Float(any)
- Any(int, float)
- String
- ByteArray
- ASCII / UTF-8 / UTF-16 / UTF-32 string encodings
- BitField, including arbitrary 1-64-bit fields

Scan types found:

- Exact
- Not
- Less Than
- More Than
- Between
- Unknown Value
- Increased
- Increased By
- Decreased
- Decreased By
- Changed
- Unchanged

PINCE also demonstrates that endianness, alignment, scan scope, and region exclusion are orthogonal scanner settings and should not be represented as separate Value Type or Scan Type entries.

### scanmem / GameConqueror

Sources:

- https://github.com/scanmem/scanmem
- https://github.com/scanmem/scanmem/blob/main/gui/GameConqueror.py
- https://github.com/scanmem/scanmem/issues/360

Scanner value-type families found:

- int / integer aggregate
- int8 / uint8
- int16 / uint16
- int32 / uint32
- int64 / uint64
- float / floating-point aggregate
- float32 / float64
- number / numeric aggregate
- bytearray
- string

Search predicates exposed by scanmem syntax include:

- Exact
- Range
- Unknown Initial Value
- Increased
- Decreased
- Unchanged
- Changed
- Greater Than
- Less Than
- Increased By
- Decreased By
- Not Equal

### Squalr

Sources:

- https://github.com/Squalr/Squalr
- https://github.com/Squalr/Squalr/releases

Relevant scanner capabilities found:

- signed and unsigned primitive integer scans
- Float32 / Float64 scans
- separate string encodings
- Array of Bytes scans
- masked byte scans
- generic typed-array scans
- struct scans
- bitfield scans
- plugin-defined data types
- a 24-bit integer data-type plugin

Squalr's extensible data-type model reinforces the separation used by Memory Engine: Core owns a common vocabulary, while a target/plugin decides what it can actually provide.

### ArtMoney

Sources:

- https://www.artmoney.ru/manual/english/advantages.htm
- https://www.artmoney.ru/manual/english/faq.htm
- https://www.artmoney.ru/manual/english/help.htm
- https://www.artmoney.ru/manual/english/formula.htm
- https://www.artmoney.ru/eng.htm

Distinct scanner value types found:

- Integer 1 byte
- Integer 2 bytes
- Integer 3 bytes
- Integer 4 bytes
- Integer 8 bytes
- Float 4 bytes
- Float 6 bytes / Real48
- Float 8 bytes
- Float 10 bytes / 80-bit extended precision
- All / automatic type detection

Search methods found:

- Exact Value
- Sequence of Values
- Hex Sequence
- Range of Values
- Unknown Value
- Encoded Value
- Structure Search
- Formula

The 3-byte integer, 6-byte Real48, and 10-byte extended-float representations are the main value-type additions that are easy to miss when designing only around modern x86/x64 primitive types.

### GameGuardian

Source:

- https://gameguardian.net/forum/files/

Scanner value-type families advertised by GameGuardian include:

- Byte
- Word
- Dword
- Qword
- Float
- Double
- XOR
- Auto
- Text / String / Hex / Array-of-Bytes style searches

Search behavior also includes:

- unknown-value searches with a specified difference
- fuzzy numeric searches
- address-mask searches
- greater-than / less-than result filtering

The survey treats XOR as an encoded-value family and records fuzzy/difference/address-mask concepts as useful Scan Type references, even though no current built-in plugin exposes them.

### MemoryEngine360

Source:

- https://github.com/AngryCarrot789/MemoryEngine360/releases

Its Unknown data-type mode tries multiple representations, with the documented default order including:

- I32
- I16
- I8
- I64
- Float
- Double
- String

This is covered by Memory Engine's aggregate integer, floating-point, numeric, and All value-type families rather than by duplicating a platform-specific "Unknown" value type.

### DijoScan

Source:

- https://github.com/Dijosto/DijoScan

Scanner value types found:

- Int8 / Int16 / Int32 / Int64
- Float / Double
- String (ASCII / Unicode)
- Array of Bytes

Scan types found:

- Exact
- Bigger / Smaller
- Between
- Unknown
- Increased / Decreased
- Changed / Unchanged

### Bit Slicer

Sources:

- https://github.com/zorgiepoo/Bit-Slicer/wiki/Data-Types
- https://github.com/zorgiepoo/Bit-Slicer/wiki/Search-Windows
- https://github.com/zorgiepoo/Bit-Slicer/wiki/Storing-All-Values

Scanner value-type families found:

- signed/unsigned 8-, 16-, 32-, and 64-bit integers
- Float / Double
- 8-bit and 16-bit strings
- Byte Arrays with wildcard support
- Pointer values whose width follows the target architecture

Search behavior includes direct equality/inequality and greater/less comparisons, bounded between searches, floating-point epsilon/range handling, and comparisons against stored snapshots. Pointer-chain discovery remains a separate pointer-scanning subsystem even though architecture-sized Pointer is also a meaningful value representation for ordinary value searches.

### PyMemoryEditor

Sources:

- https://github.com/JeanExtreme002/PyMemoryEditor
- https://github.com/JeanExtreme002/PyMemoryEditor/blob/main/docs/app.md

The current scanner exposes all normal integer widths, Float, Double, Boolean, UTF-8 String, and Byte Array values. It also exposes range, IDA-style AOB/signature, regular-expression search, and the standard First Scan / Next Scan comparison workflow. Boolean therefore merits a shared semantic Value Type, and regular-expression matching merits a distinct shared Scan Type rather than being confused with literal string search.

### ReClass.NET

Source:

- https://github.com/ReClassNET/ReClass.NET

ReClass.NET was reviewed because it is a major memory-editing and structure-analysis program. It exposes many useful memory-node types such as Bool, Bits, Enum, pointers, vectors, matrices, strings, virtual tables, and functions.

Those node types should **not be copied wholesale into a plugin's scanner Value Type list**. A Vector3, matrix, pointer-to-text, vtable, or function node describes how a Memory Viewer / structure dissector interprets a layout. It is not automatically a distinct memory-search representation. A plugin should expose such a representation as an `IMemoryValueType` only when its scanner can meaningfully parse, scan, compare, and format it.

## Value Type reference vocabulary

The following normalized names cover the distinct scanner representation families found in the survey. They are useful names when designing reusable SDK helpers or plugin-specific definitions, but none is required to exist in Core:

### Integer

- `UInt8`
- `Int8`
- `UInt16`
- `Int16`
- `UInt24`
- `Int24`
- `UInt32`
- `Int32`
- `UInt64`
- `Int64`

### Floating point

- `Float16`
- `Float32`
- `Float48`
- `Float64`
- `Float80`

`Float16` is included as the standard IEEE half-precision representation even though it was not the distinguishing feature of the surveyed desktop tools. It is increasingly common in graphics, machine-learning, emulator, and packed-data workloads and fits the same platform-neutral primitive family.

### Bits / encoded data

- `Boolean`
- `Binary`
- `BitField`
- `XorEncoded`

`Boolean` is kept separate from raw Binary/BitField searching because some scanners expose it as a semantic true/false value type with a concrete storage representation. A plugin advertising it is responsible for supporting the representation expected by its scanner path.

### Bytes and text

- `ByteArray`
- `Ascii`
- `Utf8`
- `Utf16`
- `Utf32`
- `Pointer`

`Pointer` represents an architecture-sized address value and follows the target's pointer width and endianness. Pointer-chain discovery remains a separate subsystem; the Value Type exists for ordinary searches where the value stored at an address is itself a pointer.

Generic UI labels such as "String", "Text", "Unicode", or "Wide String" are normalized to explicit encodings. A future plugin should advertise the concrete string encodings it actually supports instead of relying on an ambiguous generic string representation.

### Aggregate / automatic modes

- `AnyInteger`
- `AnyFloat`
- `AnyNumber`
- `All`

These cover labels such as `int`, `float`, `number`, `Auto`, `Unknown data type`, and `All` when the tool is really searching several concrete representations together.

### Composite / extensible modes

- `Grouped`
- `TypedArray`
- `Structure`
- `Custom`

## Scan Type reference vocabulary

The survey found the following comparison modes and special search methods. This is a research vocabulary, not a plugin-owned production registry. General comparison modes that Memory Engine chooses to support belong in Core; plugin-specific transport/native mechanics remain separate.

### Direct comparisons

- `ExactValue`
- `NotEqual`
- `GreaterThan`
- `GreaterThanOrEqual`
- `LessThan`
- `LessThanOrEqual`
- `Between`
- `OutsideRange`

### Snapshot / unknown-value comparisons

- `UnknownInitialValue`
- `IncreasedValue`
- `IncreasedValueBy`
- `IncreasedValueByPercent`
- `DecreasedValue`
- `DecreasedValueBy`
- `DecreasedValueByPercent`
- `ChangedValue`
- `UnchangedValue`
- `ChangedValueBy`
- `ChangedValueByPercent`
- `SameAsFirstScan`
- `SameAsSavedScan`
- `WithinPercentRange`
- `UnknownValueByDifference`

### Specialized search methods

- `SequenceOfValues`
- `HexSequence`
- `EncodedValue`
- `Formula`
- `StructureSearch`
- `AddressMask`
- `FuzzyValue`
- `RegularExpression`

Some tools implement these specialized operations as separate search dialogs, syntax modifiers, or filters instead of placing them in a Scan Type dropdown. Memory Engine does not force every surveyed term into the Core catalog. A new entry should be added only when it has clear cross-platform semantics appropriate to the main Scan workflow.

## Concepts that should remain scanner options or separate subsystems

The following are separate subsystems or scanner options, not Scan Type predicates:

- pointer-chain discovery / multi-level pointer scanning;
- memory-region scope and protection filters;
- alignment;
- endianness;
- hexadecimal display/input mode;
- floating-point rounding or tolerance configuration;
- pause-target-while-scanning;
- process selection;
- Memory Viewer / disassembler searches;
- file scanning;
- debugger breakpoints and find-what-accesses/writes operations.

They should receive their own capability contracts or plugin-driven settings when implemented rather than being overloaded into `IMemoryScanType`.

## Ownership rule

Core owns the standard comparison predicates used by the main Scan workflow. Plugins own Value Types, target transport/native acceleration, and platform-specific Scan Options. A plugin does not need to redeclare Exact/Changed/Increased/etc.

A Core Scan Type can be offered only when the selected Value Type provides the semantics it needs. Equality-based predicates use `IMemoryValueType.ValuesEqual`; ordered/delta predicates require the optional `IMemoryValueComparer`. A future custom plugin Value Type can therefore participate in the same universal Scan Type list by implementing the relevant Value Type contracts, without duplicating Scan Type objects.

The current PS5 and Mock plugins expose their Value Types but no production Scan Type list. Plugin API `2.7.0` allows a connected plugin to expose `INativeScanTypeMappingProvider`; each `NativeScanTypeMapping` states which Core predicate, stage, and Value Types are semantically equivalent to one opaque plugin-native operation. PS5 uses this to accelerate Exact, Fuzzy, ordered, previous-value, integer Unknown Initial Low, and snapshot-based Unknown Initial Value paths where ps5debug-NG semantics match Core. Increased By/Decreased By and floating Unknown Initial Low deliberately remain Core fallback because the upstream edge semantics differ.

## Future survey maintenance

This document should continue to record useful concepts found in other memory editors, but new findings do not automatically create application-level types.

When a genuinely useful representation or comparison mode is discovered:

1. record it here if it improves the research reference;
2. decide whether it is a general predicate, a Value Type, a Scan Option, or a separate subsystem;
3. add a general predicate to Core only when its semantics are useful and stable across platforms;
4. keep new memory representations and platform-specific options in the relevant plugin (or a reusable SDK helper when appropriate);
5. let plugins publish semantic native mappings for Core predicates they can accelerate instead of duplicating those predicates;
6. never add a platform-specific backend compare id/opcode to Core merely to gain acceleration.

Aliases or UI wording from another tool should not create duplicate reusable definitions when the underlying representation or predicate is already covered. For example, Qword commonly maps to a 64-bit integer representation, Single maps to Float32, and Wide String should be made explicit as the encoding a plugin actually supports.
