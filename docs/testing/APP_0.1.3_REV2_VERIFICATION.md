# TeeKay87's Memory Engine 0.1.3.rev2 Verification

## Purpose

This checklist verifies the capability-driven Value Type and Scan Type catalogs introduced in `0.1.3.rev2`, the built-in plugin declarations that consume those catalogs, and the revised disabled-control presentation.

This revision does **not** add new scan algorithms. The PlayStation 5 and Mock plugins continue to advertise only the Exact Value scanner functionality that was already implemented before this revision. The larger Core catalogs are shared vocabulary for current and future plugins; entries that a plugin does not declare must not appear in the Scan panel.

## Expected identities

Before functional testing, confirm the following versions are shown by the application/plugin metadata:

| Component | Expected version |
| --- | --- |
| TeeKay87's Memory Engine | `0.1.3.rev2` |
| Plugin API | `1.3.0` |
| PlayStation 5 plugin | `0.1.0.rev10` |
| In-Memory Test Target plugin | `1.0.0.rev2` |

## 1. Build and automated verification

From the repository root on Windows:

```powershell
dotnet build .\TeeKay87.MemoryEngine.sln -c Release
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Required result:

```text
All 22 checks passed.
```

The automated suite must include successful checks for:

- complete Core Value Type catalog coverage;
- complete Core Scan Type catalog coverage, including First Scan / Next Scan stage metadata;
- built-in plugin-declared scanner capabilities;
- all previously retained Plugin API, process, memory-map, memory-read/write, native PS5 scan, cancellation, process-control, and plugin-discovery checks.

A build failure or any failed verification check blocks revision acceptance.

## 2. PlayStation 5 capability-driven selectors

Connect to the PlayStation 5 plugin and select an Active Target.

### Value Type

The Value Type selector must contain exactly these eleven entries:

1. `1 Byte (Unsigned)`
2. `1 Byte (Signed)`
3. `2 Bytes (Unsigned)`
4. `2 Bytes (Signed)`
5. `4 Bytes (Unsigned)`
6. `4 Bytes (Signed)`
7. `8 Bytes (Unsigned)`
8. `8 Bytes (Signed)`
9. `Float`
10. `Double`
11. `Array of Bytes`

Core-only catalog entries such as 3-byte integers, Half Float, Real48, Extended Float, Boolean, Pointer, strings, aggregate types, grouped/structure types, and custom types must **not** appear because the PS5 plugin does not advertise them.

### Scan Type

The Scan Type selector must contain only:

```text
Exact Value
```

No Core-only scan mode such as Unknown Initial Value, Changed Value, Increased Value, Between, Formula, Fuzzy Value, or Regular Expression may appear.

Because only one Scan Type is available, the selector must be disabled and visually look disabled rather than active.

## 3. Scan-selector lifecycle

With the PS5 plugin active:

1. Before First Scan, Value Type must be selectable because the plugin exposes multiple supported Value Types.
2. Scan Type must remain disabled because only `Exact Value` is available.
3. Run a successful First Scan.
4. Value Type must become locked for that scan session.
5. Scan Type must remain disabled for PS5 because `Exact Value` is still its only applicable Next Scan choice.
6. Run a Next Scan and verify the selected Value Type does not change.
7. Choose **New Scan**.
8. Value Type must become selectable again.
9. Scan Type must still remain disabled because it still has only one available First Scan choice.

This verifies that capability filtering, First/Next-stage filtering, and Value Type session locking are independent concepts. A future plugin that advertises several Next Scan predicates should be able to change Scan Type after First Scan without changing Value Type.

## 4. Disabled-state presentation

Repeat the visual checks in **Light**, **Dimmed**, and **Dark** themes.

Disabled controls must be immediately distinguishable from enabled controls without relying on clicking them.

Verify at least:

- a disabled ordinary button uses the theme's darker/lower-contrast surface and dimmed text;
- the single-choice Scan Type ComboBox uses dimmed text, border/background, and drop-down arrow;
- a disabled Value Type ComboBox after First Scan uses the same disabled presentation;
- **Pause target while scanning** is visibly dimmed whenever it is present but unavailable;
- on a non-Int32 scan result, Scan Results -> **Change value** is visibly dimmed and cannot be invoked;
- on a `4 Bytes (Signed)` / Int32 result, **Change value** remains visibly enabled and usable;
- enabled controls remain clearly distinguishable and retain their normal hover/focus behavior.

Disabled styling must not change command behavior; it is presentation only.

## 5. Representative PS5 scanner regression

The scanner algorithms were not changed by this revision, so a representative live regression is sufficient after the automated protocol tests pass.

Recommended minimum:

1. New Scan -> `4 Bytes (Signed)` -> `Exact Value`.
2. Search a known value with First Scan.
3. Change or retain the target value as appropriate.
4. Run Next Scan.
5. Confirm results refine correctly and Current/Previous values remain correct.
6. Confirm Enter in the Value field still invokes the highlighted First/Next Scan action.
7. Confirm New Scan resets the session without reconnecting.

If desired, repeat a Float or Double First/Next Scan to reconfirm the established native-First/Core-refinement fallback path, but that path is unchanged in this revision.

## 6. Existing workflow regression

Confirm the previously verified application behavior remains intact:

- First Scan / Next Scan highlighting works as before;
- Cancel Scan leaves the PS5 command stream/session reusable;
- `Pause target while scanning` remains Off by default and resumes the process after success, cancellation, or failure;
- preferred `eboot.bin` target behavior remains unchanged;
- Raw Memory Read still works;
- Raw Memory Write and Safe Write Test still work and read-back verification passes.

## 7. Known unchanged boundaries

The following are **not failures of 0.1.3.rev2** because this revision deliberately does not change them:

- the current 2,000,000-result scan safety limit remains in place;
- the UI still displays at most the first 50,000 candidates;
- the `0.1.3.rev1` live 1-byte scan demonstrated 16,211,407 native matches and was blocked by the existing 2,000,000-result safety limit;
- live Array-of-Bytes verification remains deferred until there is a practical way to choose a known byte sequence;
- disk-backed massive-result storage, configurable scan-result storage location, stale-session cleanup, and the reusable modal progress dialog are future revisions.

## Acceptance criteria

`0.1.3.rev2` can be accepted when all of the following are true:

| Verification | Required result |
| --- | --- |
| Release build | PASS |
| Automated verification | 22/22 PASS |
| Application/plugin/API identities | PASS |
| PS5 Value Type list contains only 11 declared types | PASS |
| PS5 Scan Type list contains only Exact Value | PASS |
| Core-only catalog entries stay hidden for PS5 | PASS |
| Value Type locking / Scan Type stage filtering / New Scan lifecycle | PASS |
| Disabled-state styling in all three themes | PASS |
| Int32-only Change value state remains correct | PASS |
| Representative PS5 First/Next Scan | PASS |
| Existing workflow regression | PASS |
