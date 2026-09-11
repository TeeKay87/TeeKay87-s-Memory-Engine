# Application 0.1.7.rev18 Verification — Debugger Tab Theme Fix

## Status

**SUPERSEDED BEFORE COMPLETE ACCEPTANCE.** Rev17 passed **124/124** automated checks but was superseded before focused runtime acceptance after its new Debugger `TabControl` rendered with the operating-system default white surface in Dimmed theme. Rev18 corrected that system-default presentation defect. Runtime inspection of the corrected Dimmed surface then exposed the application-owned tab headers clipping their right border, and the same review established the requirement for independent modeless MainWindow/tool-window z-order with coordinated MainWindow shutdown cleanup. Those host/UI corrections move to rev19 before the carried-forward Call Stack/Stepping acceptance resumes.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev18` |
| Feature | `Debugger Tab Theme Fix` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev34` |
| Automated checks | `125` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev18 ZIP into a clean directory.
2. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

3. Confirm the final line is exactly:

```text
All 125 checks passed.
```

Do not skip or remove a failing registration. The registry must contain 125 unique names and 125 unique method targets.

## Gate B — Focused Theme Regression

Use Mock and open the Debugger before resuming the broader Call Stack/Stepping acceptance.

1. Select **Dimmed** theme, open Debugger, and inspect the upper-right Breakpoints / Watchpoints + Call Stack tab area.
2. Confirm the tab content surface follows the application panel palette and no white/system-default background remains.
3. Confirm both selected and unselected tab header text is clearly readable.
4. Switch between both tabs and confirm selected, unselected, and hover states remain visually consistent with the current theme.
5. Switch the application to **Dark** while the Debugger is open and confirm the tab surface/headers update immediately.
6. Switch to **Light** and confirm the same area remains consistent/readable without stale Dark/Dimmed colors.
7. Switch back to the preferred theme and confirm there is no layout shift, lost binding, missing tab, or broken splitter behavior.

The correction is accepted only if the full tab strip and selected-content chrome are application-themed in all three bundled themes.

## Gate C — Carried-Forward Rev17 Runtime Acceptance

After Gate B passes, continue the Call Stack/Call Frames and Stepping verification defined in `APP_0.1.7_REV17_VERIFICATION.md`, starting with the Mock workspace/call-stack gates that were not accepted before the rev17 runtime blocker. Run the complete focused Mock and live-PS5 sequence against the **rev18** package.

The carried-forward acceptance includes:

- Debugger workspace layout/splitters/capability gating;
- Mock Call Stack/frame details/thread switching/navigation;
- Mock Step Into, Step Over, Step Out, and Run to Address;
- Mock stale-session/cleanup and breakpoint/watchpoint/register regression;
- live PS5 Call Stack;
- live PS5 native Step Into;
- live PS5 Step Over, Step Out, and Run to Address through the host-composed paths;
- final live debugger cleanup/transport/regression checks.

## Completion Rule

Rev18 may be marked verified only after:

1. **125/125 PASS** on Windows;
2. focused Light/Dimmed/Dark tab-theme acceptance;
3. the complete carried-forward Mock runtime acceptance passes;
4. the complete carried-forward live-PS5 acceptance passes.

Rev18 was superseded before these completion requirements were all accepted. The next corrective revision is `0.1.7.rev19 — Debugger UI and Window Lifecycle Fixes`; Integration, Export and Finalization therefore moves to rev20.
