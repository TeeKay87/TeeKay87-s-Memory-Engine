# TeeKay87's Memory Engine 0.1.7.rev44 Verification

## Purpose

Verify that the rev43 verification-project compile regression is corrected without changing the rev43 runtime feature set.

## Windows automated gate

1. Open/build the solution on the normal Windows development machine.
2. Confirm the previous `CS0103` errors for `pluginViewModelSourcePath` and `mainWindowXamlPath` are gone.
3. Confirm whether the previous XAML designer `XLS0414` diagnostic disappears after a successful project build. If it remains as a real build error, stop and investigate it separately.
4. Run the complete automated verification executable.
5. Expected result: **152/152 PASS**.

## Runtime gate after automated PASS

Continue with the focused rev43 runtime/UI verification because rev44 intentionally changes no runtime behavior. Verify the comparer action gating, removal confirmation/invalidation, No group behavior, Universal Export Select None/live Continue state, empty-data export gating, and Main Window Primary styling for Disassembler/Debugger.
