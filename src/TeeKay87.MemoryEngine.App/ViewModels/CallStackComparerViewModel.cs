using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.Debugging.Snapshots;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class DebuggerSnapshotItemViewModel : ObservableObject
{
    private DebuggerSnapshot _snapshot;
    private readonly bool _isImported;
    private readonly ObservableCollection<string> _availableGroups;
    private readonly Func<string, string> _registerGroupName;
    private readonly Action<DebuggerSnapshotItemViewModel, string, string> _groupChanged;

    public DebuggerSnapshotItemViewModel(
        DebuggerSnapshot snapshot,
        ObservableCollection<string> availableGroups,
        Func<string, string> registerGroupName,
        Action<DebuggerSnapshotItemViewModel, string, string> groupChanged,
        bool isImported = false)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _availableGroups = availableGroups ?? throw new ArgumentNullException(nameof(availableGroups));
        _registerGroupName = registerGroupName ?? throw new ArgumentNullException(nameof(registerGroupName));
        _groupChanged = groupChanged ?? throw new ArgumentNullException(nameof(groupChanged));
        _isImported = isImported;

        string normalizedGroup = _registerGroupName(_snapshot.Group);
        if (!string.Equals(normalizedGroup, _snapshot.Group, StringComparison.Ordinal))
        {
            _snapshot = _snapshot.WithMetadata(group: normalizedGroup);
        }
    }

    public DebuggerSnapshot Snapshot => _snapshot;
    public Guid SnapshotId => _snapshot.SnapshotId;
    public string Captured => _snapshot.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string Source => _isImported ? "Imported" : _snapshot.CaptureMode.ToString();
    public string Event => _snapshot.Event.Kind.ToString();
    public string Trigger => _snapshot.Event.TriggerInstructionAddress is ulong value ? $"0x{value:X}" : "Unavailable";
    public string StopIp => _snapshot.Event.InstructionPointer is ulong value ? $"0x{value:X}" : "Unavailable";
    public string Process => _snapshot.Source.ProcessName;
    public ObservableCollection<string> AvailableGroups => _availableGroups;

    public string Label
    {
        get => _snapshot.Label;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (normalized == _snapshot.Label) return;
            _snapshot = _snapshot.WithMetadata(label: normalized);
            OnPropertyChanged();
        }
    }

    public string Notes
    {
        get => _snapshot.Notes;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (normalized == _snapshot.Notes) return;
            _snapshot = _snapshot.WithMetadata(notes: normalized);
            OnPropertyChanged();
        }
    }

    public string Group
    {
        get => _snapshot.Group;
        set
        {
            string raw = value?.Trim() ?? string.Empty;
            string normalized = raw.Length == 0 ? string.Empty : _registerGroupName(raw);
            string previous = _snapshot.Group;
            if (string.Equals(normalized, previous, StringComparison.Ordinal)) return;
            _snapshot = _snapshot.WithMetadata(group: normalized);
            OnPropertyChanged();
            _groupChanged(this, previous, normalized);
        }
    }

    public bool TryCreateAndAssignGroup(string value, out string assignedGroup)
    {
        assignedGroup = _registerGroupName(value ?? string.Empty);
        if (assignedGroup.Length == 0)
        {
            return false;
        }

        Group = assignedGroup;
        return true;
    }

    public void ClearGroup() => Group = string.Empty;
}

public sealed class CallStackComparerViewModel : ObservableObject
{
    private enum ComparisonKind
    {
        None,
        Pairwise,
        Group
    }

    private DebuggerViewModel? _liveDebugger;
    private readonly DebuggerSnapshotComparer _comparer = new();
    private string _statusText = "Capture or import at least two snapshots to compare debugger context.";
    private int _selectedSnapshotCount;
    private string? _selectedGroupA;
    private string? _selectedGroupB;
    private ComparisonKind _comparisonKind;
    private DebuggerSnapshotItemViewModel? _pairSnapshotA;
    private DebuggerSnapshotItemViewModel? _pairSnapshotB;
    private string _comparisonGroupA = string.Empty;
    private string _comparisonGroupB = string.Empty;

    public ObservableCollection<DebuggerSnapshotItemViewModel> Snapshots { get; } = new();
    public ObservableCollection<string> GroupNames { get; } = new();
    public ObservableCollection<DebuggerComparisonItem> Results { get; } = new();

    public bool HasSnapshots => Snapshots.Count > 0;
    public bool HasResults => Results.Count > 0;
    public bool CanComparePair => SelectedSnapshotCount == 2;
    public bool CanExportSnapshot => SelectedSnapshotCount == 1;
    public bool CanCompareGroups =>
        !string.IsNullOrWhiteSpace(SelectedGroupA) &&
        !string.IsNullOrWhiteSpace(SelectedGroupB) &&
        !string.Equals(SelectedGroupA, SelectedGroupB, StringComparison.OrdinalIgnoreCase) &&
        CountGroupMembers(SelectedGroupA) > 0 &&
        CountGroupMembers(SelectedGroupB) > 0;

    public int SelectedSnapshotCount
    {
        get => _selectedSnapshotCount;
        set
        {
            int normalized = Math.Max(0, value);
            if (!SetProperty(ref _selectedSnapshotCount, normalized)) return;
            OnPropertyChanged(nameof(CanComparePair));
            OnPropertyChanged(nameof(CanExportSnapshot));
        }
    }

    public string? SelectedGroupA
    {
        get => _selectedGroupA;
        set
        {
            if (!SetProperty(ref _selectedGroupA, value)) return;
            OnPropertyChanged(nameof(CanCompareGroups));
            InvalidateGroupComparisonForSelectionChange();
        }
    }

    public string? SelectedGroupB
    {
        get => _selectedGroupB;
        set
        {
            if (!SetProperty(ref _selectedGroupB, value)) return;
            OnPropertyChanged(nameof(CanCompareGroups));
            InvalidateGroupComparisonForSelectionChange();
        }
    }

    internal DebuggerViewModel? LiveDebugger
    {
        get => _liveDebugger;
        set
        {
            if (ReferenceEquals(_liveDebugger, value)) return;
            if (_liveDebugger is not null) _liveDebugger.PropertyChanged -= LiveDebugger_PropertyChanged;
            _liveDebugger = value;
            if (_liveDebugger is not null) _liveDebugger.PropertyChanged += LiveDebugger_PropertyChanged;
            OnPropertyChanged(nameof(CanCaptureCurrent));
        }
    }

    public bool CanCaptureCurrent => _liveDebugger?.CanCaptureSnapshot == true;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public void AddSnapshot(DebuggerSnapshot snapshot, bool isImported = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        DebuggerSnapshotItemViewModel item = new(
            snapshot,
            GroupNames,
            RegisterGroupName,
            SnapshotGroupChanged,
            isImported);
        Snapshots.Add(item);
        NotifySnapshotCollectionStateChanged();

        if (_comparisonKind == ComparisonKind.Group && IsComparedGroup(item.Group))
        {
            InvalidateComparison("Group comparison result cleared because group membership changed.");
            return;
        }

        StatusText = $"Added snapshot '{snapshot.Label}'.";
    }

    public void Remove(DebuggerSnapshotItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool invalidatesComparison = ComparisonUsesSnapshot(item);
        if (!Snapshots.Remove(item)) return;

        NotifySnapshotCollectionStateChanged();
        if (invalidatesComparison)
        {
            InvalidateComparison("Comparison result cleared because one of its source snapshots was removed.");
        }
        else
        {
            StatusText = "Snapshot removed from comparer.";
        }
    }

    public void Clear()
    {
        Snapshots.Clear();
        SelectedSnapshotCount = 0;
        ClearComparisonState();
        NotifySnapshotCollectionStateChanged();
        StatusText = "Snapshot collection cleared.";
    }

    public bool ComparePair(DebuggerSnapshotItemViewModel first, DebuggerSnapshotItemViewModel second)
    {
        if (first is null || second is null || ReferenceEquals(first, second)) return false;
        Publish(_comparer.Compare(first.Snapshot, second.Snapshot));
        _comparisonKind = ComparisonKind.Pairwise;
        _pairSnapshotA = first;
        _pairSnapshotB = second;
        _comparisonGroupA = string.Empty;
        _comparisonGroupB = string.Empty;
        StatusText = $"Pairwise comparison: '{first.Label}' vs '{second.Label}'.";
        return true;
    }

    public bool CompareGroups(string groupA, string groupB)
    {
        string a = groupA?.Trim() ?? string.Empty;
        string b = groupB?.Trim() ?? string.Empty;
        if (a.Length == 0 || b.Length == 0 || string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return false;
        DebuggerSnapshot[] left = Snapshots.Where(s => string.Equals(s.Group, a, StringComparison.OrdinalIgnoreCase)).Select(s => s.Snapshot).ToArray();
        DebuggerSnapshot[] right = Snapshots.Where(s => string.Equals(s.Group, b, StringComparison.OrdinalIgnoreCase)).Select(s => s.Snapshot).ToArray();
        if (left.Length == 0 || right.Length == 0) return false;
        Publish(_comparer.CompareGroups(left, right));
        _comparisonKind = ComparisonKind.Group;
        _pairSnapshotA = null;
        _pairSnapshotB = null;
        _comparisonGroupA = ResolveRegisteredGroupName(a);
        _comparisonGroupB = ResolveRegisteredGroupName(b);
        StatusText = $"Group comparison: {a} ({left.Length}) vs {b} ({right.Length}).";
        return true;
    }

    public void ReportStatus(string status) => StatusText = status;

    internal void DetachLiveDebugger() => LiveDebugger = null;

    private string RegisterGroupName(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        string? existing = GroupNames.FirstOrDefault(
            groupName => string.Equals(groupName, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        GroupNames.Add(normalized);
        return normalized;
    }

    private string ResolveRegisteredGroupName(string value)
    {
        return GroupNames.FirstOrDefault(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) ?? value;
    }

    private int CountGroupMembers(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return 0;
        return Snapshots.Count(snapshot => string.Equals(snapshot.Group, groupName, StringComparison.OrdinalIgnoreCase));
    }

    private void SnapshotGroupChanged(DebuggerSnapshotItemViewModel item, string previousGroup, string currentGroup)
    {
        OnPropertyChanged(nameof(CanCompareGroups));

        if (_comparisonKind != ComparisonKind.Group)
        {
            return;
        }

        bool previousWasCompared = IsComparedGroup(previousGroup);
        bool currentIsCompared = IsComparedGroup(currentGroup);
        if (previousWasCompared || currentIsCompared)
        {
            InvalidateComparison("Group comparison result cleared because group membership changed.");
        }
    }

    private bool ComparisonUsesSnapshot(DebuggerSnapshotItemViewModel item)
    {
        return _comparisonKind switch
        {
            ComparisonKind.Pairwise => ReferenceEquals(item, _pairSnapshotA) || ReferenceEquals(item, _pairSnapshotB),
            ComparisonKind.Group => IsComparedGroup(item.Group),
            _ => false
        };
    }

    private bool IsComparedGroup(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return false;
        return string.Equals(groupName, _comparisonGroupA, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(groupName, _comparisonGroupB, StringComparison.OrdinalIgnoreCase);
    }

    private void InvalidateGroupComparisonForSelectionChange()
    {
        if (_comparisonKind != ComparisonKind.Group)
        {
            return;
        }

        if (!string.Equals(SelectedGroupA, _comparisonGroupA, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(SelectedGroupB, _comparisonGroupB, StringComparison.OrdinalIgnoreCase))
        {
            InvalidateComparison("Group comparison result cleared because the selected groups changed.");
        }
    }

    private void InvalidateComparison(string status)
    {
        if (_comparisonKind == ComparisonKind.None && Results.Count == 0)
        {
            return;
        }

        ClearComparisonState();
        StatusText = status;
    }

    private void ClearComparisonState()
    {
        Results.Clear();
        _comparisonKind = ComparisonKind.None;
        _pairSnapshotA = null;
        _pairSnapshotB = null;
        _comparisonGroupA = string.Empty;
        _comparisonGroupB = string.Empty;
        OnPropertyChanged(nameof(HasResults));
    }

    private void NotifySnapshotCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSnapshots));
        OnPropertyChanged(nameof(CanCompareGroups));
    }

    private void LiveDebugger_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null || e.PropertyName == nameof(DebuggerViewModel.CanCaptureSnapshot))
        {
            OnPropertyChanged(nameof(CanCaptureCurrent));
        }
    }

    private void Publish(DebuggerSnapshotComparison comparison)
    {
        Results.Clear();
        foreach (DebuggerComparisonItem item in comparison.Items) Results.Add(item);
        OnPropertyChanged(nameof(HasResults));
    }
}
