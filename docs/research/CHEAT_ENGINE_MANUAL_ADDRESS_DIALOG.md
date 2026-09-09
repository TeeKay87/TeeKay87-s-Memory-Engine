# Cheat Engine Manual Address Dialog Survey

## Purpose

This note records the user-facing concepts exposed by Cheat Engine's manual-address editor so TeeKay87's Memory Engine can adopt useful workflow ideas without copying platform-specific implementation details or enabling controls before their backing model exists.

The source review was performed against the public `cheat-engine/cheat-engine` repository at commit `ec45d5f47f92a239ba0bf51ec5d04a7509c3fd37`, primarily:

- `Cheat Engine/formAddressChangeUnit.lfm` — form layout and visible controls;
- `Cheat Engine/formAddressChangeUnit.pas` — Value Type behavior, pointer/offset handling, and type-specific option visibility.

## Cheat Engine Controls

The dialog contains the following primary fields:

- Address;
- a live resolved/current value beside the address;
- Description;
- Type;
- Hexadecimal display toggle;
- Signed toggle;
- Pointer toggle;
- OK / Cancel.

The built-in type list visible in the form resource includes:

- Binary;
- Byte;
- 2 Bytes;
- 4 Bytes;
- 8 Bytes;
- Float;
- Double;
- Text;
- Array of Bytes.

Type-dependent controls include:

- Length for Binary, Text, and Array of Bytes;
- Start bit for Binary;
- Unicode and Codepage for Text.

When Pointer is enabled, Cheat Engine dynamically exposes:

- a pointer base address;
- one or more signed offsets;
- Add Offset;
- Remove Offset;
- intermediate/final address/value information while resolving the chain.

## Mapping to Memory Engine 0.1.6.rev9

### Functional now

Memory Engine rev9 exposes the concepts that can already be represented safely by the current platform-neutral Saved Addresses model:

| Concept | rev9 behavior |
| --- | --- |
| Address | Functional hexadecimal address input with the existing reusable live input filter. |
| Description | Functional optional Saved Address description. |
| Value Type | Functional; options come only from the active plugin's existing `IMemoryValueType` declarations. |
| Length | Functional for variable-length Value Types; fixed-size types display their required byte count. |
| Current value | The new row is refreshed from the Active Target immediately after it is added. The dialog states this behavior rather than fabricating a value before creation. |
| Signedness | Already represented by distinct plugin Value Types such as `4 Bytes` and `4 Bytes (Unsigned)`; no separate Signed state is introduced. |

The default variable length is 10 bytes, matching the familiar manual-entry starting point used by Cheat Engine's form. Manual variable-length Saved Addresses are currently limited to 1-4,096 bytes to keep the existing periodic Saved Address refresh path bounded.

### Visible placeholders

The following concepts are shown as disabled **planned** controls in rev9 so the intended future workflow is visible without implying support that does not exist:

- hexadecimal display preference;
- Binary start bit;
- Text Unicode mode;
- Text code-page mode;
- Pointer mode;
- pointer base address;
- pointer offset;
- Add Offset;
- Remove Offset.

These placeholders have no effect on the Saved Address that is created.

## Why Pointer Support Is Not Enabled Yet

Pointer chains are not merely an alternate address textbox. A correct shared implementation needs a neutral model for:

- base expressions/module-relative roots where supported;
- pointer width and endianness;
- multiple signed offsets;
- safe dereference reads;
- failure/partial-resolution state;
- target/session identity;
- presentation of resolved final address;
- plugin capability declaration where a platform has additional requirements.

Enabling a Pointer checkbox before those rules exist would create platform assumptions in WPF or silently produce incorrect Saved Addresses. Rev9 therefore establishes the UI location only; pointer resolution remains a later subsystem.

## Design Rule

Future manual-address features should continue to follow the same split:

- Core/host owns generic Saved Address workflow and neutral models;
- plugins own platform-specific capabilities and concrete Value Types;
- a placeholder becomes interactive only when its underlying model and verification path are complete.


## Corrective Host Revision 0.1.6.rev10

Revision `0.1.6.rev10` does not change the dialog mapping described above. It only fixes the C# definite-assignment path in the host hexadecimal-address parser so the rev9 dialog can compile on Windows.
