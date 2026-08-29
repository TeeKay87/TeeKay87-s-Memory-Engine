using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Capabilities;

[Flags]
public enum TargetCapabilities : ulong
{
    None = 0,
    Connect = 1UL << 0,
    ProcessEnumeration = 1UL << 1,
    ForegroundProcess = 1UL << 2,
    MemoryRegionEnumeration = 1UL << 3,
    MemoryRead = 1UL << 4,
    MemoryWrite = 1UL << 5,
    MemoryAllocation = 1UL << 6,
    MemoryProtection = 1UL << 7,
    ProcessSuspend = 1UL << 8,
    ProcessResume = 1UL << 9,
    NativeValueScanning = 1UL << 10,
    AobScanning = 1UL << 11,
    NativePointerScanning = 1UL << 12,
    Disassembly = 1UL << 13,
    Assembly = 1UL << 14,
    Debugger = 1UL << 15,
    Breakpoints = 1UL << 16,
    Watchpoints = 1UL << 17,
    RegisterAccess = 1UL << 18,
    ThreadEnumeration = 1UL << 19,
    CallStack = 1UL << 20,
    StepExecution = 1UL << 21,
    CheatApplication = 1UL << 22,
    CheatValidation = 1UL << 23,
    CheatExport = 1UL << 24
}
