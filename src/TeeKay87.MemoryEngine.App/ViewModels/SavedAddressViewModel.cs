using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.Scanning;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class SavedAddressViewModel : ObservableObject
{
    private readonly Action<string> _copyTextAction;
    private readonly Action<SavedAddressViewModel> _removeAction;
    private readonly Action<SavedAddressViewModel> _toggleFrozenAction;
    private string _description;
    private ulong _address;
    private ScanValueTypeViewModel _selectedValueType;
    private int _valueSize;
    private int _alignment;
    private string _valueText;
    private string _currentValueDisplayText;
    private MemoryProtection? _protection;
    private bool _isValueEditing;
    private byte[]? _currentBytes;
    private byte[]? _frozenBytes;
    private bool _isFrozen;
    private long _freezeRequestGeneration;
    private bool _requestedFrozenState;
    private long _targetWriteGeneration;
    private string _statusText = string.Empty;

    public SavedAddressViewModel(
        MemoryScanResult source,
        TargetProcess targetProcess,
        TargetArchitecture valueArchitecture,
        IReadOnlyList<ScanValueTypeViewModel> valueTypeOptions,
        Action<string> copyTextAction,
        Action<SavedAddressViewModel> removeAction,
        Action<SavedAddressViewModel> toggleFrozenAction)
        : this(
            CreateScanInitialization(source, targetProcess, valueArchitecture, valueTypeOptions),
            copyTextAction,
            removeAction,
            toggleFrozenAction)
    {
    }

    internal SavedAddressViewModel(
        ulong address,
        string description,
        TargetProcess targetProcess,
        TargetArchitecture valueArchitecture,
        IReadOnlyList<ScanValueTypeViewModel> valueTypeOptions,
        ScanValueTypeViewModel selectedValueType,
        int valueSize,
        Action<string> copyTextAction,
        Action<SavedAddressViewModel> removeAction,
        Action<SavedAddressViewModel> toggleFrozenAction)
        : this(
            CreateManualInitialization(
                address,
                description,
                targetProcess,
                valueArchitecture,
                valueTypeOptions,
                selectedValueType,
                valueSize),
            copyTextAction,
            removeAction,
            toggleFrozenAction)
    {
    }

    private SavedAddressViewModel(
        SavedAddressInitialization initialization,
        Action<string> copyTextAction,
        Action<SavedAddressViewModel> removeAction,
        Action<SavedAddressViewModel> toggleFrozenAction)
    {
        ArgumentNullException.ThrowIfNull(initialization);
        _copyTextAction = copyTextAction ?? throw new ArgumentNullException(nameof(copyTextAction));
        _removeAction = removeAction ?? throw new ArgumentNullException(nameof(removeAction));
        _toggleFrozenAction = toggleFrozenAction ?? throw new ArgumentNullException(nameof(toggleFrozenAction));

        ValueTypeOptions = initialization.ValueTypeOptions;
        _selectedValueType = initialization.SelectedValueType;
        ValueArchitecture = initialization.ValueArchitecture;
        TargetProcessId = initialization.TargetProcess.Id;
        TargetProcessName = initialization.TargetProcess.Name;
        TargetProcessDisplayName = !string.IsNullOrWhiteSpace(initialization.TargetProcess.DisplayName)
            ? initialization.TargetProcess.DisplayName!
            : !string.IsNullOrWhiteSpace(initialization.TargetProcess.Name)
                ? initialization.TargetProcess.Name
                : "<unnamed process>";

        _description = initialization.Description;
        _address = initialization.Address;
        _valueSize = initialization.ValueSize;
        _alignment = initialization.Alignment;
        _valueText = initialization.CurrentValueDisplayText;
        _currentValueDisplayText = initialization.CurrentValueDisplayText;
        _currentBytes = initialization.CurrentBytes?.ToArray();
        _protection = initialization.Protection;

        CopyAddressCommand = new RelayCommand(CopyAddress);
        CopyValueCommand = new RelayCommand(CopyValue);
        ToggleFrozenCommand = new RelayCommand(ToggleFrozen);
        RemoveCommand = new RelayCommand(Remove);
    }

    private static SavedAddressInitialization CreateScanInitialization(
        MemoryScanResult source,
        TargetProcess targetProcess,
        TargetArchitecture valueArchitecture,
        IReadOnlyList<ScanValueTypeViewModel> valueTypeOptions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(targetProcess);
        ArgumentNullException.ThrowIfNull(valueArchitecture);
        ArgumentNullException.ThrowIfNull(valueTypeOptions);
        ScanValueTypeViewModel selectedValueType = ResolveValueType(
            valueTypeOptions,
            source.ValueTypeId,
            $"The scan-result Value Type '{source.ValueTypeId}' is no longer exposed by the active plugin.");

        return new SavedAddressInitialization(
            targetProcess,
            valueArchitecture,
            valueTypeOptions,
            selectedValueType,
            source.Address,
            source.CurrentValue.Size,
            source.CurrentValue.Alignment,
            string.Empty,
            source.CurrentValue.DisplayText,
            source.CurrentValue.Bytes.ToArray(),
            source.Protection);
    }

    private static SavedAddressInitialization CreateManualInitialization(
        ulong address,
        string description,
        TargetProcess targetProcess,
        TargetArchitecture valueArchitecture,
        IReadOnlyList<ScanValueTypeViewModel> valueTypeOptions,
        ScanValueTypeViewModel selectedValueType,
        int valueSize)
    {
        ArgumentNullException.ThrowIfNull(targetProcess);
        ArgumentNullException.ThrowIfNull(valueArchitecture);
        ArgumentNullException.ThrowIfNull(valueTypeOptions);
        ArgumentNullException.ThrowIfNull(selectedValueType);

        ScanValueTypeViewModel resolvedValueType = ResolveValueType(
            valueTypeOptions,
            selectedValueType.Id,
            $"The Value Type '{selectedValueType.Id}' is no longer exposed by the active plugin.");
        if (resolvedValueType.ValueType.FixedSize is int fixedSize)
        {
            valueSize = fixedSize;
        }
        else if (valueSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueSize), "A variable-length Saved Address requires a positive byte count.");
        }

        return new SavedAddressInitialization(
            targetProcess,
            valueArchitecture,
            valueTypeOptions,
            resolvedValueType,
            address,
            valueSize,
            resolvedValueType.ValueType.DefaultAlignment,
            description?.Trim() ?? string.Empty,
            "...",
            CurrentBytes: null,
            Protection: null);
    }

    private static ScanValueTypeViewModel ResolveValueType(
        IReadOnlyList<ScanValueTypeViewModel> valueTypeOptions,
        string valueTypeId,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(valueTypeOptions);
        if (valueTypeOptions.Count == 0)
        {
            throw new ArgumentException("Saved Addresses requires at least one plugin-defined Value Type.", nameof(valueTypeOptions));
        }

        return valueTypeOptions.FirstOrDefault(option =>
                string.Equals(option.Id, valueTypeId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(errorMessage);
    }

    private sealed record SavedAddressInitialization(
        TargetProcess TargetProcess,
        TargetArchitecture ValueArchitecture,
        IReadOnlyList<ScanValueTypeViewModel> ValueTypeOptions,
        ScanValueTypeViewModel SelectedValueType,
        ulong Address,
        int ValueSize,
        int Alignment,
        string Description,
        string CurrentValueDisplayText,
        byte[]? CurrentBytes,
        MemoryProtection? Protection);

    public TargetArchitecture ValueArchitecture { get; }

    public ulong TargetProcessId { get; }

    public string TargetProcessName { get; }

    public string TargetProcessDisplayName { get; }

    public IReadOnlyList<ScanValueTypeViewModel> ValueTypeOptions { get; }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value ?? string.Empty);
    }

    public ulong Address => _address;

    public string AddressText => $"0x{_address:X}";

    public ScanValueTypeViewModel SelectedValueType => _selectedValueType;

    public int ValueSize => _valueSize;

    public int Alignment => _alignment;

    public string ValueText
    {
        get => _valueText;
        private set => SetProperty(ref _valueText, value);
    }

    public MemoryProtection? ProtectionValue => _protection;

    public string Protection => _protection?.ToString() ?? string.Empty;

    internal long TargetWriteGeneration => _targetWriteGeneration;

    public string FrozenMenuHeader => IsFrozenRequested ? "Unfreeze" : "Freeze";

    public bool IsFrozenRequested => _requestedFrozenState;

    public bool IsFrozen
    {
        get => _isFrozen;
        private set => SetProperty(ref _isFrozen, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public ICommand CopyAddressCommand { get; }

    public ICommand CopyValueCommand { get; }

    public ICommand ToggleFrozenCommand { get; }

    public ICommand RemoveCommand { get; }

    public bool MatchesTarget(TargetProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return process.Id == TargetProcessId &&
               string.Equals(process.Name, TargetProcessName, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesSavedIdentity(TargetProcess process, MemoryScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MatchesSavedIdentity(process, result.Address, result.ValueTypeId);
    }

    public bool MatchesSavedIdentity(TargetProcess process, ulong address, string valueTypeId)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueTypeId);

        return MatchesTarget(process) &&
               Address == address &&
               string.Equals(SelectedValueType.Id, valueTypeId, StringComparison.OrdinalIgnoreCase);
    }

    internal void SetAddress(ulong address)
    {
        if (_address == address)
        {
            return;
        }

        _address = address;
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(AddressText));
        SetStatus(string.Empty);
    }

    internal void SetValueType(ScanValueTypeViewModel valueType)
    {
        ArgumentNullException.ThrowIfNull(valueType);

        if (ReferenceEquals(_selectedValueType, valueType) ||
            string.Equals(_selectedValueType.Id, valueType.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _selectedValueType = valueType;
        _alignment = valueType.ValueType.DefaultAlignment;
        if (valueType.ValueType.FixedSize is int fixedSize)
        {
            _valueSize = fixedSize;
        }
        else if (_currentBytes is not null && _currentBytes.Length > 0)
        {
            _valueSize = _currentBytes.Length;
        }

        _currentBytes = null;
        _currentValueDisplayText = "...";
        SetFrozen(false);
        ValueText = _currentValueDisplayText;
        SetStatus("Value Type changed. Waiting for the next refresh.");
        OnPropertyChanged(nameof(SelectedValueType));
        OnPropertyChanged(nameof(ValueSize));
        OnPropertyChanged(nameof(Alignment));
    }

    internal void SetCurrentValue(MemoryScanValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _currentBytes = value.Bytes.ToArray();
        _valueSize = value.Size;
        _alignment = value.Alignment;
        _currentValueDisplayText = value.DisplayText;
        if (!_isValueEditing)
        {
            ValueText = _currentValueDisplayText;
        }
        OnPropertyChanged(nameof(ValueSize));
        OnPropertyChanged(nameof(Alignment));
    }

    internal void SetProtection(MemoryProtection? protection)
    {
        if (_protection == protection)
        {
            return;
        }

        _protection = protection;
        OnPropertyChanged(nameof(ProtectionValue));
        OnPropertyChanged(nameof(Protection));
    }

    internal bool TryCaptureFrozenValue()
    {
        if (_currentBytes is null || _currentBytes.Length == 0)
        {
            return false;
        }

        _frozenBytes = _currentBytes.ToArray();
        return true;
    }

    internal bool TryGetFrozenValue(out ReadOnlyMemory<byte> value)
    {
        if (_frozenBytes is null || _frozenBytes.Length == 0)
        {
            value = default;
            return false;
        }

        value = _frozenBytes;
        return true;
    }

    internal void RecordTargetWriteSuccess(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        _targetWriteGeneration++;
        MemoryScanValue value = SelectedValueType.ValueType.CreateValue(
            bytes,
            Alignment,
            ValueArchitecture);
        SetCurrentValue(value);
    }

    internal void ReplaceFrozenValue(ReadOnlySpan<byte> bytes)
    {
        _frozenBytes = bytes.ToArray();
    }

    internal long RegisterFreezeRequest(bool frozen)
    {
        SetRequestedFrozenState(frozen);
        return ++_freezeRequestGeneration;
    }

    internal bool IsFreezeRequestCurrent(long generation, bool frozen)
    {
        return _freezeRequestGeneration == generation && _requestedFrozenState == frozen;
    }

    internal void SetFrozen(bool value)
    {
        SetRequestedFrozenState(value);
        IsFrozen = value;
        if (!value)
        {
            _frozenBytes = null;
        }
    }

    private void SetRequestedFrozenState(bool value)
    {
        if (_requestedFrozenState == value)
        {
            return;
        }

        _requestedFrozenState = value;
        OnPropertyChanged(nameof(IsFrozenRequested));
        OnPropertyChanged(nameof(FrozenMenuHeader));
    }

    internal void SetStatus(string message)
    {
        StatusText = message ?? string.Empty;
    }

    internal void RefreshAddressText()
    {
        OnPropertyChanged(nameof(AddressText));
    }

    internal void BeginValueEdit()
    {
        _isValueEditing = true;
    }

    internal void EndValueEdit()
    {
        _isValueEditing = false;
    }

    internal void RefreshValueText()
    {
        if (!_isValueEditing)
        {
            ValueText = _currentValueDisplayText;
        }
        else
        {
            OnPropertyChanged(nameof(ValueText));
        }
    }

    internal void RefreshSelectedValueType()
    {
        OnPropertyChanged(nameof(SelectedValueType));
    }

    private void CopyAddress()
    {
        _copyTextAction(AddressText);
    }

    private void CopyValue()
    {
        _copyTextAction(ValueText);
    }

    private void ToggleFrozen()
    {
        _toggleFrozenAction(this);
    }

    private void Remove()
    {
        _removeAction(this);
    }
}
