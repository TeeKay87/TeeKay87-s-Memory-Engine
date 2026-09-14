# Application 0.1.7.rev40 Verification — Universal Export Option Display Fix

## Package identity

| Item | Expected |
| --- | --- |
| Application | `0.1.7.rev40` |
| Feature | `Universal Export Option Display Fix` |
| Plugin API | `2.18.0` |
| Mock | `1.0.1.rev17` |
| PS5 | `0.1.2.rev39` |
| Automated registry | `152` checks |

## Gate 1 — Clean build and automated suite

Build from a clean extraction on Windows and require **152/152 PASS**.

## Gate 2 — Shared export dialog presentation

Open export from each available consumer: Scan Results, Saved Addresses, Disassembler, Debugger, and Call Stack Comparer **Export Results**. For every dialog:

1. Scope shows only the user-facing scope name; no `ExportScopeOption { ... }` text is visible.
2. Open Scope and verify every item uses its user-facing name.
3. Format shows `JSON`, `CSV`, `TSV`, or `Markdown table`; no `ExportFormatOption { ... }` text is visible.
4. Open Format and verify all four labels.
5. Change scope and confirm description/columns still refresh correctly.
6. Cancel without creating a file.

## Gate 3 — Resume debugger Universal Export

Continue the interrupted rev39 debugger export gate. Export Threads, Registers, Breakpoints / Watchpoints, Call Stack, and Events. Exercise JSON plus at least one tabular format. Verify Events keep stop/current IP, trigger instruction, watched address/access/size, and trigger resolution separate.

## Acceptance

Rev40 is accepted when 152/152 passes, every shared export-dialog consumer renders clean Scope/Format labels, and the existing export workflow remains functional. Snapshot/comparer verification already passed through JSON round-trip, offline ownership, and invalid schema/version rejection before this presentation defect was found.
