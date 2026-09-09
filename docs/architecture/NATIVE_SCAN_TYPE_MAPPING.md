# Native Scan Type Mapping

## Purpose

TeeKay87's Memory Engine keeps standard Scan Type identity and comparison semantics in Core. Platform plugins may accelerate those Core predicates with target-native scan facilities, but native support is an optimization rather than a second source of scan semantics.

Plugin API `2.7.0` formalizes that boundary with an optional semantic mapping contract:

```text
Core Scan Type
    -> optional plugin-declared semantic mapping
        -> native scanner when the mapping and runtime shape are compatible
        -> shared Core scanner otherwise
```

The design is intentionally platform-neutral. A PlayStation 5 plugin may map a Core predicate to a ps5debug-NG compare id or snapshot mode; a future PC or Xbox 360 plugin may map the same Core predicate to a completely different enum, opcode, driver request, or library call without changing Core or WPF.

## Ownership

### Core owns

- the standard Scan Type catalog;
- stable Core Scan Type ids;
- First/Next Scan availability;
- required operand count;
- Value Type compatibility rules;
- authoritative shared comparison semantics;
- the native-delegation resolver;
- the shared reader-based fallback scanner.

### Plugin SDK owns

- the public optional `INativeScanTypeMappingProvider` contract;
- the neutral `NativeScanTypeMapping` model;
- stable cross-boundary Core Scan Type ids in `StandardMemoryScanTypeIds`;
- native scan service contracts such as `INativeValueScanner` and the streaming variants.

### Platform plugin owns

- the target/native implementation;
- the plugin's native identifiers and protocol values;
- which Core Scan Types are genuinely semantically equivalent to native operations;
- stage and Value Type restrictions for those mappings;
- runtime capability/option checks that cannot be expressed statically in the mapping table;
- translation from a Core Scan Type id to the actual native request.

### WPF host owns

- combining the selected Core Scan Type, plugin Value Type, current Scan Options, and connected session;
- asking Core whether native execution should be attempted;
- falling back to the shared Core scanner when native execution is not mapped or rejects the current runtime shape.

## Public Mapping Contract

A connected session may expose:

```csharp
INativeScanTypeMappingProvider
```

which supplies:

```csharp
IReadOnlyList<NativeScanTypeMapping> NativeScanTypeMappings
```

Each mapping contains:

- `CoreScanTypeId` — the stable Core-owned predicate id;
- `NativeScanTypeId` — an opaque plugin-owned identifier;
- `AvailableForFirstScan`;
- `AvailableForNextScan`;
- optional `SupportedValueTypeIds`.

An empty Value Type restriction means the mapping itself does not narrow Value Types. The native scanner may still reject an individual request because of runtime capabilities, width, endianness, alignment, server resources, or another target-specific condition.

`NativeScanTypeId` is deliberately opaque. Core and WPF never parse it, compare numeric meanings, or assume a particular protocol. It exists so plugin metadata can describe which native operation backs the mapping while the plugin remains responsible for translating the Core request into that operation.

## Semantic Equivalence Rule

A plugin must publish a mapping only when the native operation preserves the Core predicate's meaning for the declared stage and Value Types.

A similar name is not enough.

Examples:

- if Core `Unknown Initial Value` includes zero but a backend's native `UnknownInitial` excludes zero, that native compare operation must not be mapped directly;
- if Core `Increased By` is directional and non-wrapping but a backend performs target-width wrapping arithmetic, the operations are not equivalent at numeric boundaries and must not be mapped;
- if a backend's floating predicate uses absolute magnitude while Core requires a positive value, that floating native predicate must fall back even if its integer form is equivalent.

This rule prevents acceleration from silently changing scan results.

## Resolution and Fallback

`NativeScanTypeResolver` in Core is the shared delegation gate.

For a session that implements `INativeScanTypeMappingProvider`:

1. Core locates a mapping for the selected Core Scan Type id.
2. The mapping must allow the current First/Next stage.
3. The selected Value Type must satisfy any mapping restriction.
4. Only then does the host request a native scanner service.
5. The plugin performs its remaining runtime checks.
6. A `NotSupportedException` from the native path returns execution to the established shared Core scanner/refiner.

When a mapping is absent, the host does not intentionally call the native scanner for that Core predicate.

For older compatible Plugin API 2.x plugins that do not implement the optional mapping provider, Core retains the previous behavior: the host may try the plugin's native service and rely on `NotSupportedException` for fallback. This keeps existing plugins compatible while new plugins can publish precise capability metadata.

## Current Core Scan Types

| Core Scan Type | Stage | Operands | Core meaning |
| --- | --- | ---: | --- |
| Exact Value | First + Next | 1 | Current equals supplied value according to the active Value Type / scan option semantics |
| Fuzzy Value | First + Next | 1 | Float/Double absolute difference from supplied value is strictly less than `1.0` |
| Bigger Than | First + Next | 1 | Current is strictly greater than supplied value |
| Smaller Than | First + Next | 1 | Current is strictly smaller than supplied value |
| Between | First + Next | 2 | `lower <= current <= upper` |
| Unknown Initial Value | First | 0 | Keep every fixed-width candidate, including zero |
| Unknown Initial Low Value | First | 1 | Keep positive nonzero numeric values where `current <= upperLimit`; upper limit must be positive |
| Increased Value | Next | 0 | Current is greater than previous scan value |
| Decreased Value | Next | 0 | Current is smaller than previous scan value |
| Changed Value | Next | 0 | Current differs from previous scan value |
| Unchanged Value | Next | 0 | Current equals previous scan value |
| Increased By | Next | 1 | Current increased by exactly the supplied non-negative amount without relying on target-width wrapping |
| Decreased By | Next | 1 | Current decreased by exactly the supplied non-negative amount without relying on target-width wrapping |

Core's disk-backed result records already store address plus the current value bytes. On the next generation those bytes are the previous-scan baseline, so native mapping does not require a separate in-memory previous-value dictionary or a new result-file format.

## Current PS5 Mapping

PS5 plugin `0.1.0.rev24` targets Plugin API `2.11.0` and continues to use the API `2.7.0` native-mapping contract to publish the following semantic mappings for ps5debug-NG:

| Core Scan Type | PS5 native operation | Native stages | Value Types | Notes |
| --- | --- | --- | --- | --- |
| Exact Value | `compareType 0` | First + Next | Standard numeric + Array of Bytes | Strict Float/Double Exact may still apply host filtering/Core refinement as required by rounding option |
| Fuzzy Value | `compareType 1` | First + Next | Float, Double | Native absolute-difference `< 1.0` semantics match Core |
| Bigger Than | `compareType 2` | First + Next | Standard numeric | Big-endian multi-byte numeric requests fall back |
| Smaller Than | `compareType 3` | First + Next | Standard numeric | Big-endian multi-byte numeric requests fall back |
| Between | `compareType 4` | First + Next | Standard numeric | Two comparison operands are sent in native payload |
| Unknown Initial Value | TurboScan snapshot + include zeros | First | Standard numeric | Uses snapshot mode instead of direct `compareType 11` so zero-valued candidates are retained |
| Unknown Initial Low Value | `compareType 12` | First | Integer types | Float/Double fallback because upstream floating semantics use absolute magnitude |
| Increased Value | `compareType 5` | Next | Standard numeric | Uses resident previous value |
| Decreased Value | `compareType 7` | Next | Standard numeric | Uses resident previous value |
| Changed Value | `compareType 9` | Next | Standard numeric | Float/Double COUNT reuses same-width UInt32/UInt64 wire type so comparison is raw-byte inequality |
| Unchanged Value | `compareType 10` | Next | Standard numeric | Float/Double COUNT reuses same-width UInt32/UInt64 wire type so comparison is raw-byte equality |
| Increased By | Core fallback | Next | Standard numeric | ps5debug-NG `compareType 6` can use target-width wrapping arithmetic, not fully equivalent |
| Decreased By | Core fallback | Next | Standard numeric | ps5debug-NG `compareType 8` can use target-width wrapping arithmetic, not fully equivalent |

The PS5 plugin still defines protocol constants for the complete upstream `compareType 0..12` set. A constant's existence does not imply that Core declares it semantically equivalent.

## Unknown Initial Value on PS5

ps5debug-NG exposes two different mechanisms that are relevant to unknown-value scanning:

1. direct `compareType 11`, which keeps nonzero values;
2. TurboScan snapshot mode, which can explicitly include zero-valued slots.

Core defines Unknown Initial Value as a true initial snapshot of every fixed-width candidate. The PS5 mapping therefore uses snapshot mode with the include-zero flag. This provides native target-side snapshot/resident refinement while preserving Core semantics.

If the connected ps5debug-NG server does not advertise the snapshot engine, cannot create the snapshot, or refuses the current resource shape, the plugin returns `NotSupportedException` after keeping the command stream synchronized and the host runs the Core fallback. Snapshot progress is sentinel-terminated by the protocol; the host consumes every progress record, the sentinel, summary, and final status before deciding whether native execution succeeded. There is intentionally no host-side progress-record-count ceiling because the payload may use smaller snapshot I/O windows and emit more than 1,024 legitimate progress records on a large target.

For Core **Changed Value** and **Unchanged Value**, equality is defined by the fixed-width bytes retained from the previous scan. This differs intentionally from numeric Float/Double equality: IEEE-754 NaN values are not equal under `==`, so using ps5debug-NG's floating comparator directly would report an unchanged NaN payload as changed on every pass. PS5 plugin `0.1.0.rev22` preserves the Core meaning without materializing the resident set by sending `valueType = UInt32` for Float and `valueType = UInt64` for Double on TurboScan COUNT for compare types 9/10 only. Width, addresses, stored previous bytes, result GET decoding, and the user-selected Value Type remain unchanged.

## Runtime Restrictions

A static mapping is necessary but not sufficient for every request. The plugin can reject an otherwise mapped request when runtime details differ.

Current PS5 examples include:

- required TurboScan server-resident/segment capabilities are absent;
- Unknown Initial Value requires the snapshot engine;
- a native value width exceeds the resident-session limit;
- a selected alignment cannot be represented by the protocol;
- a big-endian multi-byte numeric predicate would be interpreted as little-endian by the native backend;
- strict Float/Double Exact refinement would use ps5debug-NG's tolerant native comparison;
- resident native state no longer matches the host's scan session.

All such cases use the same Core fallback rather than changing the selected Scan Type.

## Guidance for Future Plugins

When implementing a new platform plugin:

1. Reuse Core Scan Type ids; do not recreate the standard predicate catalog in the plugin.
2. Determine which native backend operations have exactly the same meaning as each Core predicate.
3. Publish only those matches through `INativeScanTypeMappingProvider`.
4. Restrict mappings by First/Next stage and Value Type where needed.
5. Keep native ids plugin-owned and opaque.
6. Translate the Core request into native protocol details inside the plugin.
7. Reject incompatible runtime shapes with `NotSupportedException` so Core fallback remains authoritative.
8. Do not publish a mapping to gain speed when boundary conditions or value semantics differ.

A plugin may publish no mappings at all and still receive the complete Core scanner feature set through `IMemoryReader` and `IMemoryMapProvider`.

## Resident Result Integration (Plugin API 2.9)

The semantic mapping decision remains separate from result storage. A mapped native operation may produce an ordinary stream, a host-materialized disk set, or an `INativeValueScanResidentResultSet`. Rev30 does not add PS5 Scan Type mappings and does not change any ps5debug-NG compare id.

When the mapped native result is authoritative and large, Core can retain the complete survivor set in the backend and read only the bounded WPF preview. A later Next Scan stays native only when both the semantic mapping and the resident handle's `CanRefine(...)` check agree. If either check fails, Core materializes the complete current resident generation into the shared disk-backed representation before applying the Core predicate.

This preserves the core rule: native acceleration is an optimization, never a different definition of a Scan Type. It also prevents the first 50,000 displayed rows from becoming an accidental candidate-set boundary.
