# TeeKay87's Memory Engine 0.1.3.rev8 Verification

## Purpose

This checklist verifies the host-only **Scan Panel Overflow and Tooltip Fix** revision. It must preserve the rev7 PS5 Scan Options behavior while correcting two presentation problems found during live rev7 testing: clipped long hints and inaccessible Scan controls when the panel is shorter than its content.

Current version boundaries:

```text
Host application:             0.1.3.rev8
PlayStation 5 plugin:         0.1.0.rev12
In-Memory Test Target plugin: 1.0.0.rev3
Plugin API:                   2.1.0
```

No plugin implementation or protocol version changes in this revision.

## Build and Automated Verification

From the repository root:

```powershell
dotnet clean .\TeeKay87.MemoryEngine.sln -c Release
dotnet build .\TeeKay87.MemoryEngine.sln -c Release
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final result:

```text
All 25 checks passed.
```

## Long Tooltip / Hint Verification

1. Select **PlayStation 5 · ps5debug-NG**.
2. Hover the **Alignment** selector long enough to show its description.
3. Confirm the complete description is visible.
4. Confirm the hint wraps onto additional lines when it reaches the shared maximum width instead of being clipped on the right.
5. Repeat with Endianness, Floating-point rounding, Scan Type, Value Type, and Pause target while scanning.
6. Repeat in **Light**, **Dimmed**, and **Dark**.

Expected result: the active theme still owns tooltip background/border/text colors, and long text is fully readable within the tooltip popup.

## Scan Panel Overflow Verification

### Normal height

1. Use a window height large enough for every PS5 Scan control to fit.
2. Confirm no vertical scrollbar is shown in the Scan panel.
3. Confirm selector/button widths and normal panel alignment remain unchanged.

### Reduced height

1. Reduce the window height until the complete PS5 Scan controls cannot fit simultaneously.
2. Confirm a vertical scrollbar appears automatically inside the Scan card.
3. Confirm there is no horizontal scrollbar.
4. Scroll from top to bottom and confirm every control remains reachable, including:
   - Value;
   - Scan Type;
   - Value Type;
   - Endianness;
   - Alignment;
   - Floating-point rounding;
   - Pause target while scanning;
   - First Scan / Next Scan;
   - New Scan / Cancel Scan.
5. Confirm controls remain stretched to the available Scan-panel width rather than collapsing to their content width.
6. Confirm there is visible breathing room between the right edge of every control and the vertical scrollbar. The intended inner gap is 10 DIPs in addition to the Scan card's existing 14-DIP outer padding.
7. Confirm the scrollbar remains inside the Scan card and does not overlap the workspace splitter or card border.

Repeat in Light, Dimmed, and Dark.

## Resize Regression

- Resize the Scan panel horizontally across its existing 280–420 px range and confirm scrolling/layout remains stable.
- Resize the application back to a height where all controls fit and confirm the vertical scrollbar disappears automatically.
- Confirm the left Scan Results/Saved Addresses splitters behave exactly as before.

## Functional Regression

Confirm the rev7 controls remain functionally unchanged:

- Endianness defaults to Little Endian;
- Alignment defaults to Default;
- Floating-point rounding defaults to Strict;
- Pause target while scanning defaults Off;
- option applicability and First Scan locking are unchanged;
- New Scan unlocks the appropriate options;
- rev6 disabled-button labels remain visibly dimmed;
- First/Next/Enter workflow is unchanged;
- Cancel Scan, process pause/resume, target selection, Raw Memory Read/Write, and Safe Write Test remain unchanged.

## Acceptance

`0.1.3.rev8` can be considered verified when the solution builds, all 25 automated checks pass, long hints wrap without clipping in all bundled themes, and the Scan panel automatically exposes a correctly spaced vertical scrollbar whenever its plugin-driven content does not fit.
