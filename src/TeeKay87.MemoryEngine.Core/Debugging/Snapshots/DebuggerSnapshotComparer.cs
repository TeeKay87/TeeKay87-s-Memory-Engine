using System;
using System.Collections.Generic;
using System.Linq;

namespace TeeKay87.MemoryEngine.Core.Debugging.Snapshots;

public enum DebuggerComparisonClassification
{
    Identical,
    Different,
    StableInBothGroupsDifferentBetweenGroups,
    StableOnlyInGroupA,
    StableOnlyInGroupB,
    VariableInBothGroups,
    Unavailable
}

public sealed record DebuggerComparisonItem(
    string Category,
    string Name,
    string GroupA,
    string GroupB,
    DebuggerComparisonClassification Classification,
    string Evidence);

public sealed record DebuggerSnapshotComparison(
    IReadOnlyList<DebuggerComparisonItem> Items,
    int CommonLeadingFrames,
    int? FirstDivergentFrameIndex);

public sealed class DebuggerSnapshotComparer
{
    public DebuggerSnapshotComparison Compare(DebuggerSnapshot first, DebuggerSnapshot second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return CompareGroups([first], [second]);
    }

    public DebuggerSnapshotComparison CompareGroups(
        IReadOnlyList<DebuggerSnapshot> groupA,
        IReadOnlyList<DebuggerSnapshot> groupB)
    {
        ArgumentNullException.ThrowIfNull(groupA);
        ArgumentNullException.ThrowIfNull(groupB);
        if (groupA.Count == 0 || groupB.Count == 0)
        {
            throw new ArgumentException("Both comparison groups require at least one snapshot.");
        }

        List<DebuggerComparisonItem> items = new();
        AddCallStackResults(items, groupA, groupB, out int commonLeading, out int? firstDivergence);
        AddRegisterResults(items, groupA, groupB);
        AddContextResults(items, groupA, groupB);
        AddInstructionResults(items, groupA, groupB, trigger: true);
        AddInstructionResults(items, groupA, groupB, trigger: false);
        AddDisassemblyWindowResults(items, groupA, groupB);
        AddBreakpointContextResults(items, groupA, groupB);
        AddMemoryResults(items, groupA, groupB);
        AddCandidateSummaryResults(items);

        return new DebuggerSnapshotComparison(items.AsReadOnly(), commonLeading, firstDivergence);
    }

    private static void AddCallStackResults(
        List<DebuggerComparisonItem> items,
        IReadOnlyList<DebuggerSnapshot> groupA,
        IReadOnlyList<DebuggerSnapshot> groupB,
        out int commonLeading,
        out int? firstDivergence)
    {
        int maxDepth = groupA.Concat(groupB).Select(snapshot => snapshot.CallStack.Count).DefaultIfEmpty(0).Max();
        commonLeading = 0;
        firstDivergence = null;

        for (int index = 0; index < maxDepth; index++)
        {
            string[] a = FrameValues(groupA, index);
            string[] b = FrameValues(groupB, index);
            bool stableA = IsStableAvailable(a);
            bool stableB = IsStableAvailable(b);

            if (index == commonLeading && stableA && stableB &&
                string.Equals(a[0], b[0], StringComparison.OrdinalIgnoreCase))
            {
                commonLeading++;
                continue;
            }

            if (firstDivergence is null && index == commonLeading && stableA && stableB &&
                !string.Equals(a[0], b[0], StringComparison.OrdinalIgnoreCase))
            {
                firstDivergence = index;
            }
            break;
        }

        string[] stackA = groupA.Select(StackSignature).ToArray();
        string[] stackB = groupB.Select(StackSignature).ToArray();
        DebuggerComparisonClassification pathClassification = Classify(stackA, stackB);
        int? pairwiseAlignedFrames = groupA.Count == 1 && groupB.Count == 1
            ? LongestCommonSubsequenceLength(
                groupA[0].CallStack.Select(NormalizeFrame).ToArray(),
                groupB[0].CallStack.Select(NormalizeFrame).ToArray())
            : null;
        items.Add(new DebuggerComparisonItem(
            "Call Stack",
            "Execution path",
            Join(stackA),
            Join(stackB),
            pathClassification,
            firstDivergence.HasValue
                ? $"{commonLeading} stable common leading frame(s); first stable cross-group divergence at frame #{firstDivergence.Value}." +
                  (pairwiseAlignedFrames.HasValue ? $" {pairwiseAlignedFrames.Value} ordered frame(s) align overall." : string.Empty)
                : pathClassification == DebuggerComparisonClassification.Identical
                    ? $"All captured call-stack paths are equivalent after Module + Offset normalization ({commonLeading} stable leading frame(s))."
                    : $"{commonLeading} stable common leading frame(s); no later frame can be claimed as a stable first divergence because one or both groups vary or lack that frame."));

        if (firstDivergence.HasValue)
        {
            string[] a = FrameValues(groupA, firstDivergence.Value);
            string[] b = FrameValues(groupB, firstDivergence.Value);
            items.Add(new DebuggerComparisonItem(
                "Call Stack",
                "First divergence",
                Join(a),
                Join(b),
                DebuggerComparisonClassification.StableInBothGroupsDifferentBetweenGroups,
                "Every snapshot inside each group agrees at this frame, while the two groups resolve to different normalized code locations."));
        }

        for (int index = 0; index < maxDepth; index++)
        {
            string[] a = FrameValues(groupA, index);
            string[] b = FrameValues(groupB, index);
            AddSnapshotValue(
                items,
                "Call Stack",
                $"Frame #{index}",
                a,
                b,
                "Grouped frame comparison uses normalized Module + Offset identities when available so stable execution-path differences remain comparable across ASLR/process restarts.");
        }
    }

    private static void AddRegisterResults(
        List<DebuggerComparisonItem> items,
        IReadOnlyList<DebuggerSnapshot> groupA,
        IReadOnlyList<DebuggerSnapshot> groupB)
    {
        foreach (string id in groupA.SelectMany(snapshot => snapshot.Registers).Select(register => register.Id)
                     .Concat(groupB.SelectMany(snapshot => snapshot.Registers).Select(register => register.Id))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
        {
            string[] a = RegisterValues(groupA, id);
            string[] b = RegisterValues(groupB, id);
            if (a.Length != groupA.Count || b.Length != groupB.Count)
            {
                items.Add(new DebuggerComparisonItem(
                    "Registers", id, Join(a), Join(b), DebuggerComparisonClassification.Unavailable,
                    "Register is unavailable in one or more snapshots."));
                continue;
            }

            DebuggerComparisonClassification classification = Classify(a, b);
            items.Add(new DebuggerComparisonItem(
                "Registers",
                id,
                Join(a),
                Join(b),
                classification,
                classification == DebuggerComparisonClassification.StableInBothGroupsDifferentBetweenGroups
                    ? $"Potential discriminator: exact raw register bytes are stable inside each group and differ between groups ({a[0]} vs {b[0]})."
                    : "Classification uses exact raw register bytes, not formatted display text."));
        }
    }

    private static void AddContextResults(
        List<DebuggerComparisonItem> items,
        IReadOnlyList<DebuggerSnapshot> groupA,
        IReadOnlyList<DebuggerSnapshot> groupB)
    {
        AddSnapshotValue(items, "Context", "Trigger instruction",
            groupA.Select(snapshot => NormalizeAddress(snapshot, snapshot.Event.TriggerInstructionAddress)).ToArray(),
            groupB.Select(snapshot => NormalizeAddress(snapshot, snapshot.Event.TriggerInstructionAddress)).ToArray(),
            "Code addresses prefer captured Module + Offset so equivalent locations remain equal across ASLR/process restarts.");
        AddSnapshotValue(items, "Context", "Stop/current instruction",
            groupA.Select(snapshot => NormalizeAddress(snapshot, snapshot.Event.InstructionPointer)).ToArray(),
            groupB.Select(snapshot => NormalizeAddress(snapshot, snapshot.Event.InstructionPointer)).ToArray(),
            "Trigger and stop/current instruction are intentionally compared as separate debugger concepts.");
        AddSnapshotValue(items, "Context", "Trigger resolution",
            groupA.Select(snapshot => snapshot.Event.TriggerResolution.ToString()).ToArray(),
            groupB.Select(snapshot => snapshot.Event.TriggerResolution.ToString()).ToArray(),
            "Resolution status is preserved rather than treating derived and backend-exact triggers as identical provenance.");
    }

    private static void AddInstructionResults(
        List<DebuggerComparisonItem> items,
        IReadOnlyList<DebuggerSnapshot> groupA,
        IReadOnlyList<DebuggerSnapshot> groupB,
        bool trigger)
    {
        string name = trigger ? "Trigger instruction code" : "Stop/current instruction code";
        string[] a = groupA.Select(snapshot => InstructionSignature(snapshot,
            trigger ? snapshot.Event.TriggerInstructionAddress : snapshot.Event.InstructionPointer)).ToArray();
        string[] b = groupB.Select(snapshot => InstructionSignature(snapshot,
            trigger ? snapshot.Event.TriggerInstructionAddress : snapshot.Event.InstructionPointer)).ToArray();
        AddSnapshotValue(items, "Instructions", name, a, b,
            "Comparison uses captured logical/original instruction bytes and decoded text; debugger INT3 instrumentation is not part of the snapshot.");
    }

    private static void AddDisassemblyWindowResults(
        List<DebuggerComparisonItem> items,
        IReadOnlyList<DebuggerSnapshot> groupA,
        IReadOnlyList<DebuggerSnapshot> groupB)
    {
        AddSnapshotValue(
            items,
            "Instructions",
            "Logical disassembly window",
            groupA.Select(DisassemblySignature).ToArray(),
            groupB.Select(DisassemblySignature).ToArray(),
            "The complete bounded captured logical/original instruction stream is compared after Module + Offset normalization. Debugger instrumentation bytes are excluded by the snapshot capture pipeline.");
    }

    private static void AddCandidateSummaryResults(List<DebuggerComparisonItem> items)
    {
        static int Priority(DebuggerComparisonItem item) => item.Category switch
        {
            "Call Stack" => 0,
            "Registers" => 1,
            "Memory" => 2,
            "Instructions" => 3,
            "Context" => 4,
            "Breakpoint Context" => 5,
            _ => 10
        };

        DebuggerComparisonItem[] candidates = items
            .Where(item => item.Classification == DebuggerComparisonClassification.StableInBothGroupsDifferentBetweenGroups)
            .OrderBy(Priority)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            items.Insert(0, new DebuggerComparisonItem(
                "Summary",
                "Potential discriminators",
                "None",
                "None",
                DebuggerComparisonClassification.Unavailable,
                "No value was stable inside both groups while also differing between the groups. Variable evidence remains available in the detailed rows."));
            return;
        }

        int insertionIndex = 0;
        foreach (DebuggerComparisonItem candidate in candidates)
        {
            items.Insert(insertionIndex++, new DebuggerComparisonItem(
                "Summary",
                $"Potential discriminator — {candidate.Category} / {candidate.Name}",
                candidate.GroupA,
                candidate.GroupB,
                candidate.Classification,
                $"Stable difference promoted from {candidate.Category}. {candidate.Evidence}"));
        }
    }

    private static void AddBreakpointContextResults(
        List<DebuggerComparisonItem> items,
        IReadOnlyList<DebuggerSnapshot> groupA,
        IReadOnlyList<DebuggerSnapshot> groupB)
    {
        AddSnapshotValue(items, "Breakpoint Context", "Watched address",
            groupA.Select(snapshot => NormalizeDataAddress(snapshot.Event.WatchedAddress)).ToArray(),
            groupB.Select(snapshot => NormalizeDataAddress(snapshot.Event.WatchedAddress)).ToArray(),
            "Captured watched memory addresses are retained separately from trigger and stop instruction addresses.");
        AddSnapshotValue(items, "Breakpoint Context", "Watchpoint access",
            groupA.Select(snapshot => snapshot.Event.WatchpointAccess?.ToString() ?? "Unavailable").ToArray(),
            groupB.Select(snapshot => snapshot.Event.WatchpointAccess?.ToString() ?? "Unavailable").ToArray(),
            "Captured event access mode is compared directly.");
        AddSnapshotValue(items, "Breakpoint Context", "Watchpoint size",
            groupA.Select(snapshot => snapshot.Event.WatchpointSize?.ToString() ?? "Unavailable").ToArray(),
            groupB.Select(snapshot => snapshot.Event.WatchpointSize?.ToString() ?? "Unavailable").ToArray(),
            "Captured watchpoint width is compared directly.");
        AddSnapshotValue(items, "Breakpoint Context", "Managed breakpoint/watchpoint set",
            groupA.Select(BreakpointSignature).ToArray(),
            groupB.Select(BreakpointSignature).ToArray(),
            "Set comparison includes address, kind, access, size, enabled state, lifetime, and triggered-item state.");
    }

    private static void AddMemoryResults(
        List<DebuggerComparisonItem> items,
        IReadOnlyList<DebuggerSnapshot> groupA,
        IReadOnlyList<DebuggerSnapshot> groupB)
    {
        if (groupA.All(snapshot => snapshot.Memory.Count == 0) && groupB.All(snapshot => snapshot.Memory.Count == 0))
        {
            return;
        }

        AddSnapshotValue(items, "Memory", "Standard stack window",
            groupA.Select(StackMemorySignature).ToArray(),
            groupB.Select(StackMemorySignature).ToArray(),
            "Standard snapshots compare the bounded captured stack bytes only; no recursive pointer interpretation is performed.");
    }

    private static string[] FrameValues(IReadOnlyList<DebuggerSnapshot> snapshots, int index) => snapshots
        .Select(snapshot => index < snapshot.CallStack.Count ? NormalizeFrame(snapshot.CallStack[index]) : "Unavailable")
        .ToArray();

    private static bool IsStableAvailable(string[] values) =>
        values.Length > 0 &&
        values.All(value => value != "Unavailable") &&
        values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;

    private static string StackSignature(DebuggerSnapshot snapshot) => snapshot.CallStack.Count == 0
        ? "Unavailable"
        : string.Join(" > ", snapshot.CallStack.Select(NormalizeFrame));

    private static string DisassemblySignature(DebuggerSnapshot snapshot) => snapshot.Disassembly.Count == 0
        ? "Unavailable"
        : string.Join(" ; ", snapshot.Disassembly
            .OrderBy(instruction => instruction.Module, StringComparer.OrdinalIgnoreCase)
            .ThenBy(instruction => instruction.ModuleOffset ?? instruction.Address)
            .Select(instruction =>
            {
                string location = !string.IsNullOrWhiteSpace(instruction.Module) && instruction.ModuleOffset.HasValue
                    ? $"{instruction.Module}+0x{instruction.ModuleOffset.Value:X}"
                    : $"0x{instruction.Address:X}";
                return $"{location}|{instruction.Bytes}|{instruction.InstructionText}";
            }));

    private static string[] RegisterValues(IReadOnlyList<DebuggerSnapshot> snapshots, string id) => snapshots
        .Select(snapshot => snapshot.Registers.FirstOrDefault(register => string.Equals(register.Id, id, StringComparison.OrdinalIgnoreCase))?.RawBytes)
        .Where(value => value is not null)
        .Cast<string>()
        .ToArray();

    private static void AddSnapshotValue(
        List<DebuggerComparisonItem> items,
        string category,
        string name,
        string[] a,
        string[] b,
        string evidence)
    {
        if (a.Any(value => value == "Unavailable") || b.Any(value => value == "Unavailable"))
        {
            items.Add(new DebuggerComparisonItem(category, name, Join(a), Join(b), DebuggerComparisonClassification.Unavailable, evidence));
            return;
        }

        items.Add(new DebuggerComparisonItem(category, name, Join(a), Join(b), Classify(a, b), evidence));
    }

    private static DebuggerComparisonClassification Classify(string[] a, string[] b)
    {
        bool stableA = a.Length > 0 && a.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
        bool stableB = b.Length > 0 && b.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
        if (stableA && stableB)
        {
            return string.Equals(a[0], b[0], StringComparison.OrdinalIgnoreCase)
                ? DebuggerComparisonClassification.Identical
                : DebuggerComparisonClassification.StableInBothGroupsDifferentBetweenGroups;
        }
        if (stableA) return DebuggerComparisonClassification.StableOnlyInGroupA;
        if (stableB) return DebuggerComparisonClassification.StableOnlyInGroupB;
        return DebuggerComparisonClassification.VariableInBothGroups;
    }

    private static int CountCommonLeadingFrames(DebuggerSnapshot a, DebuggerSnapshot b)
    {
        int count = 0;
        for (int index = 0; index < Math.Min(a.CallStack.Count, b.CallStack.Count); index++)
        {
            if (!string.Equals(NormalizeFrame(a.CallStack[index]), NormalizeFrame(b.CallStack[index]), StringComparison.OrdinalIgnoreCase)) break;
            count++;
        }
        return count;
    }

    private static int LongestCommonSubsequenceLength(string[] left, string[] right)
    {
        int[,] lengths = new int[left.Length + 1, right.Length + 1];
        for (int i = 1; i <= left.Length; i++)
        {
            for (int j = 1; j <= right.Length; j++)
            {
                lengths[i, j] = string.Equals(left[i - 1], right[j - 1], StringComparison.OrdinalIgnoreCase)
                    ? lengths[i - 1, j - 1] + 1
                    : Math.Max(lengths[i - 1, j], lengths[i, j - 1]);
            }
        }
        return lengths[left.Length, right.Length];
    }

    private static string NormalizeFrame(DebuggerSnapshotCallFrame frame) =>
        !string.IsNullOrWhiteSpace(frame.Module) && frame.ModuleOffset.HasValue
            ? $"{frame.Module}+0x{frame.ModuleOffset.Value:X}"
            : $"0x{frame.InstructionAddress:X}";

    private static string DisplayFrame(DebuggerSnapshotCallFrame frame)
    {
        string location = NormalizeFrame(frame);
        return string.IsNullOrWhiteSpace(frame.Symbol) ? location : $"{location} ({frame.Symbol})";
    }

    private static string NormalizeAddress(DebuggerSnapshot snapshot, ulong? address)
    {
        if (!address.HasValue) return "Unavailable";
        DebuggerSnapshotInstruction? instruction = snapshot.Disassembly.FirstOrDefault(candidate => candidate.Address == address.Value);
        if (instruction is not null && !string.IsNullOrWhiteSpace(instruction.Module) && instruction.ModuleOffset.HasValue)
        {
            return $"{instruction.Module}+0x{instruction.ModuleOffset.Value:X}";
        }
        return $"0x{address.Value:X}";
    }

    private static string NormalizeDataAddress(ulong? address) => address.HasValue
        ? $"0x{address.Value:X}"
        : "Unavailable";

    private static string InstructionSignature(DebuggerSnapshot snapshot, ulong? address)
    {
        if (!address.HasValue) return "Unavailable";
        DebuggerSnapshotInstruction? instruction = snapshot.Disassembly.FirstOrDefault(candidate => candidate.Address == address.Value);
        if (instruction is null) return "Unavailable";
        string location = !string.IsNullOrWhiteSpace(instruction.Module) && instruction.ModuleOffset.HasValue
            ? $"{instruction.Module}+0x{instruction.ModuleOffset.Value:X}"
            : $"0x{instruction.Address:X}";
        return $"{location} | {instruction.Bytes} | {instruction.InstructionText}";
    }

    private static string BreakpointSignature(DebuggerSnapshot snapshot)
    {
        if (snapshot.Breakpoints.Count == 0) return "None";
        return string.Join("; ", snapshot.Breakpoints.OrderBy(item => item.Address).ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => $"0x{item.Address:X}:{item.Type}:{item.Mechanism}:{item.Access}:{item.Size}:{item.Enabled}:{item.Lifetime}:{item.IsTriggeredItem}"));
    }

    private static string StackMemorySignature(DebuggerSnapshot snapshot)
    {
        DebuggerSnapshotMemoryBlock? block = snapshot.Memory.FirstOrDefault(item => string.Equals(item.Kind, "Stack", StringComparison.OrdinalIgnoreCase));
        return block is null ? "Unavailable" : block.Bytes;
    }

    private static string Join(IEnumerable<string> values) => string.Join(", ", values.Distinct(StringComparer.OrdinalIgnoreCase));
}
