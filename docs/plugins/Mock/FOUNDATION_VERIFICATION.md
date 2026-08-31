# In-Memory Test Target Foundation Verification

## Scope

This document records the plugin-specific verification established with the initial application foundation and retained in the current source tree.

## Verified Behavior

The dependency-free verification project checks that the mock plugin:

1. exposes stable plugin metadata and a Plugin API version accepted by the host;
2. requires no connection settings;
3. creates a connected target session;
4. exposes exactly one deterministic process;
5. returns that process as the foreground process;
6. exposes the expected deterministic memory region;
7. reads the initialized Health value as `100.0f`;
8. writes a new Money value and returns it on read-back;
9. is discoverable through the runtime `PluginHost` rather than requiring direct host integration.

## Regression Role

These checks remain part of `tests/TeeKay87.MemoryEngine.Tests`. They should continue passing when live target plugins are added. A platform-specific implementation must not require changes that break the deterministic mock behavior unless an intentional Plugin SDK contract change is required and documented first.
