using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TeeKay87.MemoryEngine.App.Application;
using TeeKay87.MemoryEngine.App.Dialogs;
using TeeKay87.MemoryEngine.App.Exporting;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.App.SavedAddresses;
using TeeKay87.MemoryEngine.Core.Debugging;
using TeeKay87.MemoryEngine.Core.Disassembly;
using TeeKay87.MemoryEngine.Core.Exporting;
using TeeKay87.MemoryEngine.Core.MemoryViewer;
using TeeKay87.MemoryEngine.Core.Operations;
using TeeKay87.MemoryEngine.Core.Plugins;
using TeeKay87.MemoryEngine.Core.Scanning;
using TeeKay87.MemoryEngine.Core.Scanning.Storage;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;
using TeeKay87.MemoryEngine.PluginSdk.Scanning;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed partial class PluginViewModel : ObservableObject, IDisposable
{
    private const int DefaultMemoryReadLength = 64;
    private const int MaximumInspectorReadLength = 4096;
    private const int SafeWriteTestLength = 4;
    private const int SafeWriteStabilityDelayMilliseconds = 50;
    private const int MaximumSafeWriteCandidateRegions = 64;
    private const int MaximumDisplayedScanResults = 50_000;

    private readonly AsyncRelayCommand _connectCommand;
    private readonly AsyncRelayCommand _disconnectCommand;
    private readonly AsyncRelayCommand _refreshProcessesCommand;
    private readonly AsyncRelayCommand _setActiveProcessCommand;
    private readonly AsyncRelayCommand _readMemoryCommand;
    private readonly AsyncRelayCommand _writeMemoryCommand;
    private readonly AsyncRelayCommand _safeWriteTestCommand;
    private readonly AsyncRelayCommand _firstScanCommand;
    private readonly AsyncRelayCommand _nextScanCommand;
    private readonly AsyncRelayCommand _submitScanCommand;
    private readonly AsyncRelayCommand _newScanCommand;
    private readonly RelayCommand _cancelScanCommand;
    private readonly MemoryScanner _memoryScanner = new();
    private readonly DisassemblyReader _disassemblyReader = new();
    private readonly MemoryViewerReader _memoryViewerReader = new();
    private readonly MemoryViewerWriter _memoryViewerWriter = new();
    private readonly TabularExportService _tabularExportService = new();
    private readonly IScanResultStorage? _scanResultStorageManager;
    private readonly OperationProgressDialogService? _operationProgressDialogService;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Stopwatch _scanStopwatch = new();
    private readonly DispatcherTimer _scanElapsedTimer;
    private readonly DispatcherTimer _savedAddressUpdateTimer;
    private readonly DispatcherTimer _frozenWriteTimer;
    private CancellationTokenSource? _scanCancellation;
    private bool _savedAddressRefreshInProgress;
    private bool _savedAddressFreezeWriteInProgress;
    private bool _savedAddressUserOperationInProgress;
    private TaskCompletionSource<bool>? _savedAddressBackgroundIoIdleSource;
    private TaskCompletionSource<bool>? _savedAddressUserOperationIdleSource;
    private readonly HashSet<SavedAddressViewModel> _savedAddressesPendingRemoval = new();
    private readonly HashSet<ScanResultViewModel> _visibleScanResults = new();
    private readonly HashSet<DebuggerSessionCoordinator> _debuggerSessions = new();
    private bool _savedAddressTargetStateTransitionInProgress;
    private bool _foregroundTargetOperationPending;
    private bool _isExportingResidentScanResults;
    private IScanResultStorageSession? _scanResultStorageSession;
    private IScanResultSet? _scanResultSet;
    private INativeValueScanResidentResultSet? _nativeResidentResultSet;
    private ITargetSession? _session;
    private long _connectionGeneration;
    private TargetProcessViewModel? _selectedProcess;
    private TargetProcessViewModel? _activeProcess;
    private IReadOnlyList<MemoryRegion> _activeMemoryRegions = Array.Empty<MemoryRegion>();
    private string _connectionStatusText = "Not connected.";
    private string _connectionErrorText = string.Empty;
    private string _processStatusText = "Connect to enumerate processes.";
    private string _processErrorText = string.Empty;
    private string _memoryRegionStatusText = string.Empty;
    private string _memoryRegionErrorText = string.Empty;
    private string _memoryReadAddressText = string.Empty;
    private string _memoryReadLengthText = DefaultMemoryReadLength.ToString(CultureInfo.InvariantCulture);
    private string _memoryReadStatusText = "Set an Active Target to read memory.";
    private string _memoryReadErrorText = string.Empty;
    private string _memoryReadResultText = string.Empty;
    private string _memoryWriteAddressText = string.Empty;
    private string _memoryWriteValueText = string.Empty;
    private string _memoryWriteStatusText = "Set an Active Target to write memory.";
    private string _memoryWriteErrorText = string.Empty;
    private string _memoryWriteResultText = string.Empty;
    private IReadOnlyList<MemoryScanResult> _scanCandidates = Array.Empty<MemoryScanResult>();
    private long _scanResultCount;
    private IReadOnlyList<ScanResultViewModel> _scanResults = Array.Empty<ScanResultViewModel>();
    private readonly IReadOnlyList<ScanTypeViewModel> _supportedScanTypes;
    private readonly ObservableCollection<ScanTypeViewModel> _scanTypes = new();
    private readonly ReadOnlyObservableCollection<ScanTypeViewModel> _readOnlyScanTypes;
    private readonly IReadOnlyList<ScanValueTypeViewModel> _scanValueTypes;
    private readonly IReadOnlyList<ScanOptionViewModel> _scanOptions;
    private readonly IReadOnlyList<ScanOptionViewModel> _choiceScanOptions;
    private readonly IReadOnlyList<ScanOptionViewModel> _toggleScanOptions;
    private ScanTypeViewModel? _selectedScanType;
    private ScanValueTypeViewModel? _selectedScanValueType;
    private string _scanValueText = string.Empty;
    private string _scanSecondaryValueText = string.Empty;
    private string _scanStatusText = "Set an Active Target to begin scanning.";
    private string _scanErrorText = string.Empty;
    private double _scanProgressPercentage;
    private string _scanElapsedText = "00:00.0";
    private bool _hasScanProgress;
    private bool _isScanProgressIndeterminate;
    private bool _pauseTargetWhileScanning;
    private bool _hasScanSession;
    private bool _isConnecting;
    private bool _isConnected;
    private bool _isRefreshingProcesses;
    private bool _isLoadingMemoryRegions;
    private bool _isReadingMemory;
    private bool _isWritingMemory;
    private bool _isScanningMemory;
    private SavedAddressViewModel? _selectedSavedAddress;
    private string _savedAddressStatusText = "No saved addresses.";
    private int _savedAddressUpdateIntervalMilliseconds;
    private int _frozenWriteIntervalMilliseconds;
    private bool _disposed;

    public PluginViewModel(
        DiscoveredPlugin discoveredPlugin,
        IScanResultStorage? scanResultStorageManager,
        int savedAddressUpdateIntervalMilliseconds,
        int frozenWriteIntervalMilliseconds,
        OperationProgressDialogService? operationProgressDialogService = null)
    {
        ArgumentNullException.ThrowIfNull(discoveredPlugin);

        _scanResultStorageManager = scanResultStorageManager;
        _operationProgressDialogService = operationProgressDialogService;
        if (savedAddressUpdateIntervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(savedAddressUpdateIntervalMilliseconds));
        }

        if (frozenWriteIntervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frozenWriteIntervalMilliseconds));
        }

        _savedAddressUpdateIntervalMilliseconds = savedAddressUpdateIntervalMilliseconds;
        _frozenWriteIntervalMilliseconds = frozenWriteIntervalMilliseconds;
        Instance = discoveredPlugin.Instance;
        AssemblyPath = discoveredPlugin.AssemblyPath;
        Metadata = Instance.Metadata;
        Capabilities = EnumerateCapabilities(Instance.Capabilities);
        ConnectionSettings = new ObservableCollection<ConnectionSettingViewModel>(
            Instance.ConnectionSettings.Select(setting => new ConnectionSettingViewModel(setting)));

        _scanValueTypes = Instance.SupportedValueTypes
            .Select(valueType => new ScanValueTypeViewModel(valueType))
            .ToArray();
        _selectedScanValueType = _scanValueTypes.FirstOrDefault(option =>
                string.Equals(option.Id, Instance.DefaultValueTypeId, StringComparison.OrdinalIgnoreCase))
            ?? _scanValueTypes.FirstOrDefault();

        _supportedScanTypes = MemoryScanTypeCatalog.All
            .Select(scanType => new ScanTypeViewModel(scanType))
            .ToArray();
        _readOnlyScanTypes = new ReadOnlyObservableCollection<ScanTypeViewModel>(_scanTypes);
        RefreshScanTypesForCurrentStage(notify: false);

        _scanOptions = Instance.SupportedScanOptions
            .Select(option => new ScanOptionViewModel(option))
            .ToArray();
        _choiceScanOptions = _scanOptions.Where(option => option.IsChoiceList).ToArray();
        _toggleScanOptions = _scanOptions.Where(option => option.IsToggle).ToArray();
        foreach (ScanOptionViewModel scanOption in _scanOptions)
        {
            scanOption.SelectionChanged += OnScanOptionSelectionChanged;
        }
        RefreshScanOptionStates();

        _scanElapsedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _scanElapsedTimer.Tick += OnScanElapsedTimerTick;

        _savedAddressUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(savedAddressUpdateIntervalMilliseconds)
        };
        _savedAddressUpdateTimer.Tick += OnSavedAddressUpdateTimerTick;

        _frozenWriteTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(frozenWriteIntervalMilliseconds)
        };
        _frozenWriteTimer.Tick += OnFrozenWriteTimerTick;

        _connectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        _disconnectCommand = new AsyncRelayCommand(DisconnectAsync, CanDisconnect);
        _refreshProcessesCommand = new AsyncRelayCommand(RefreshProcessesAsync, CanRefreshProcesses);
        _setActiveProcessCommand = new AsyncRelayCommand(SetActiveProcessAsync, CanSetActiveProcess);
        _readMemoryCommand = new AsyncRelayCommand(ReadMemoryAsync, CanReadMemory);
        _writeMemoryCommand = new AsyncRelayCommand(WriteMemoryAsync, CanWriteMemory);
        _safeWriteTestCommand = new AsyncRelayCommand(RunSafeWriteTestAsync, CanRunSafeWriteTest);
        _firstScanCommand = new AsyncRelayCommand(FirstScanAsync, CanFirstScan);
        _nextScanCommand = new AsyncRelayCommand(NextScanAsync, CanNextScan);
        _submitScanCommand = new AsyncRelayCommand(SubmitScanAsync, CanSubmitScan);
        _newScanCommand = new AsyncRelayCommand(NewScanAsync, CanStartNewScan);
        _cancelScanCommand = new RelayCommand(CancelScan, CanCancelScan);
    }

    public ITargetPlugin Instance { get; }

    public PluginMetadata Metadata { get; }

    public string AssemblyPath { get; }

    public string Name => Metadata.Name;

    public override string ToString() => Name;

    public string Platform => Metadata.Platform;

    public string Backend => Metadata.Backend;

    public string Version => Metadata.DisplayVersion;

    public string ApiVersion => Metadata.ApiVersion.ToString();

    public string Description => Metadata.Description;

    public string ArchitectureDisplay =>
        $"{Metadata.Architecture.Cpu} · {Metadata.Architecture.PointerWidthBits}-bit pointers · " +
        $"{Metadata.Architecture.Endianness} endian";

    public IReadOnlyList<string> Capabilities { get; }

    public ObservableCollection<ConnectionSettingViewModel> ConnectionSettings { get; }

    public ObservableCollection<TargetProcessViewModel> Processes { get; } = new();

    public bool SupportsConnection => Instance.Capabilities.HasFlag(TargetCapabilities.Connect);

    public bool SupportsProcessEnumeration =>
        Instance.Capabilities.HasFlag(TargetCapabilities.ProcessEnumeration);

    public bool SupportsMemoryRegionEnumeration =>
        Instance.Capabilities.HasFlag(TargetCapabilities.MemoryRegionEnumeration);

    public bool SupportsMemoryRead =>
        Instance.Capabilities.HasFlag(TargetCapabilities.MemoryRead);

    public bool SupportsMemoryWrite =>
        Instance.Capabilities.HasFlag(TargetCapabilities.MemoryWrite);

    public bool SupportsDisassembly =>
        Instance.Capabilities.HasFlag(TargetCapabilities.Disassembly);

    public bool SupportsDebugger =>
        Instance.Capabilities.HasFlag(TargetCapabilities.Debugger);

    public bool CanOpenDebugger =>
        SupportsDebugger &&
        IsConnected &&
        ActiveProcess is not null &&
        !_foregroundTargetOperationPending &&
        !_isExportingResidentScanResults &&
        !IsConnecting &&
        !IsRefreshingProcesses &&
        !IsLoadingMemoryRegions &&
        !IsReadingMemory &&
        !IsWritingMemory &&
        !IsScanningMemory;

    public bool CanOpenDisassembler =>
        SupportsDisassembly &&
        SupportsMemoryRegionEnumeration &&
        ActiveMemoryRegions.Any(region =>
            region.Size > 0 &&
            region.Protection.HasFlag(MemoryProtection.Read) &&
            !region.Protection.HasFlag(MemoryProtection.Guard)) &&
        CanReadMemory();

    internal bool CanOpenDisassemblerForTarget(TargetProcess targetProcess)
    {
        ArgumentNullException.ThrowIfNull(targetProcess);

        return CanOpenDisassembler &&
               ActiveProcess is TargetProcessViewModel activeProcess &&
               AreSameProcess(activeProcess.Process, targetProcess);
    }

    internal bool CanOpenMemoryViewerForTarget(TargetProcess targetProcess)
    {
        ArgumentNullException.ThrowIfNull(targetProcess);

        return SupportsMemoryRead &&
               SupportsMemoryRegionEnumeration &&
               ActiveMemoryRegions.Any(region =>
                   region.Size > 0 &&
                   region.Protection.HasFlag(MemoryProtection.Read) &&
                   !region.Protection.HasFlag(MemoryProtection.Guard)) &&
               CanReadMemory() &&
               ActiveProcess is TargetProcessViewModel activeProcess &&
               AreSameProcess(activeProcess.Process, targetProcess);
    }

    internal bool IsCurrentDebuggerTarget(TargetProcess targetProcess, long connectionGeneration)
    {
        ArgumentNullException.ThrowIfNull(targetProcess);

        return !_disposed &&
               SupportsDebugger &&
               IsConnected &&
               connectionGeneration == _connectionGeneration &&
               _session is { IsConnected: true } &&
               ActiveProcess is TargetProcessViewModel activeProcess &&
               AreSameProcess(activeProcess.Process, targetProcess);
    }

    public bool SupportsNativeValueScanning =>
        Instance.Capabilities.HasFlag(TargetCapabilities.NativeValueScanning);

    public bool SupportsScanPause =>
        Instance.Capabilities.HasFlag(TargetCapabilities.ProcessSuspend) &&
        Instance.Capabilities.HasFlag(TargetCapabilities.ProcessResume);

    public bool SupportsSharedScanner =>
        SupportsMemoryRead &&
        SupportsMemoryRegionEnumeration &&
        _supportedScanTypes.Count > 0 &&
        _scanValueTypes.Count > 0;

    public bool CanConfigureScanPause => SupportsScanPause && !IsScanningMemory;

    public bool IsConnecting
    {
        get => _isConnecting;
        private set
        {
            if (SetProperty(ref _isConnecting, value))
            {
                OnPropertyChanged(nameof(CanOpenDebugger));
                RaiseConnectionCommandStates();
                RaiseProcessCommandStates();
                RaiseMemoryCommandStates();
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                if (!value)
                {
                    DisableSavedAddressFreezesForDisconnect();
                }

                RaiseConnectionCommandStates();
                RaiseProcessCommandStates();
                RaiseMemoryCommandStates();
                UpdateSavedAddressTimerState();
            }
        }
    }

    public bool IsRefreshingProcesses
    {
        get => _isRefreshingProcesses;
        private set
        {
            if (SetProperty(ref _isRefreshingProcesses, value))
            {
                RaiseProcessCommandStates();
                RaiseMemoryCommandStates();
            }
        }
    }

    public bool IsLoadingMemoryRegions
    {
        get => _isLoadingMemoryRegions;
        private set
        {
            if (SetProperty(ref _isLoadingMemoryRegions, value))
            {
                RaiseProcessCommandStates();
                RaiseMemoryCommandStates();
            }
        }
    }

    public bool IsReadingMemory
    {
        get => _isReadingMemory;
        private set
        {
            if (SetProperty(ref _isReadingMemory, value))
            {
                RaiseConnectionCommandStates();
                RaiseProcessCommandStates();
                RaiseMemoryCommandStates();
            }
        }
    }

    public bool IsWritingMemory
    {
        get => _isWritingMemory;
        private set
        {
            if (SetProperty(ref _isWritingMemory, value))
            {
                RaiseConnectionCommandStates();
                RaiseProcessCommandStates();
                RaiseMemoryCommandStates();
            }
        }
    }

    public bool IsScanningMemory
    {
        get => _isScanningMemory;
        private set
        {
            if (SetProperty(ref _isScanningMemory, value))
            {
                RaiseConnectionCommandStates();
                RaiseProcessCommandStates();
                RaiseMemoryCommandStates();
                RaiseScanCommandStates();
                OnPropertyChanged(nameof(CanConfigureScanPause));
                OnPropertyChanged(nameof(CanSelectScanType));
                OnPropertyChanged(nameof(CanSelectScanValueType));
                OnPropertyChanged(nameof(CanExportScanResults));
                RefreshScanOptionStates();
                UpdateSavedAddressTimerState();
            }
        }
    }

    public IReadOnlyList<MemoryRegion> ActiveMemoryRegions
    {
        get => _activeMemoryRegions;
        private set
        {
            if (SetProperty(ref _activeMemoryRegions, value))
            {
                RaiseMemoryCommandStates();
                RefreshScanResultProtections();
                RefreshSavedAddressProtections();
            }
        }
    }

    public TargetProcessViewModel? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (SetProperty(ref _selectedProcess, value))
            {
                _setActiveProcessCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public TargetProcessViewModel? ActiveProcess
    {
        get => _activeProcess;
        private set
        {
            if (SetProperty(ref _activeProcess, value))
            {
                OnPropertyChanged(nameof(ActiveProcessText));
                OnPropertyChanged(nameof(HasActiveTarget));
                OnPropertyChanged(nameof(CanOpenDebugger));
                _setActiveProcessCommand.RaiseCanExecuteChanged();
                RaiseMemoryCommandStates();
                RefreshSavedAddressTargetStates();
                UpdateSavedAddressTimerState();
            }
        }
    }

    public string ActiveProcessText => ActiveProcess is null
        ? "No active target selected."
        : $"{ActiveProcess.DisplayName} ({ActiveProcess.ProcessIdDisplay})";

    public bool HasActiveTarget => ActiveProcess is not null;

    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        private set => SetProperty(ref _connectionStatusText, value);
    }

    public string ConnectionErrorText
    {
        get => _connectionErrorText;
        private set => SetProperty(ref _connectionErrorText, value);
    }

    public string ProcessStatusText
    {
        get => _processStatusText;
        private set => SetProperty(ref _processStatusText, value);
    }

    public string ProcessErrorText
    {
        get => _processErrorText;
        private set => SetProperty(ref _processErrorText, value);
    }

    public string MemoryRegionStatusText
    {
        get => _memoryRegionStatusText;
        private set => SetProperty(ref _memoryRegionStatusText, value);
    }

    public string MemoryRegionErrorText
    {
        get => _memoryRegionErrorText;
        private set => SetProperty(ref _memoryRegionErrorText, value);
    }

    public string MemoryReadAddressText
    {
        get => _memoryReadAddressText;
        set => SetProperty(ref _memoryReadAddressText, value ?? string.Empty);
    }

    public string MemoryReadLengthText
    {
        get => _memoryReadLengthText;
        set => SetProperty(ref _memoryReadLengthText, value ?? string.Empty);
    }

    public string MemoryReadStatusText
    {
        get => _memoryReadStatusText;
        private set => SetProperty(ref _memoryReadStatusText, value);
    }

    public string MemoryReadErrorText
    {
        get => _memoryReadErrorText;
        private set => SetProperty(ref _memoryReadErrorText, value);
    }

    public string MemoryReadResultText
    {
        get => _memoryReadResultText;
        private set => SetProperty(ref _memoryReadResultText, value);
    }

    public string MemoryWriteAddressText
    {
        get => _memoryWriteAddressText;
        set => SetProperty(ref _memoryWriteAddressText, value ?? string.Empty);
    }

    public string MemoryWriteValueText
    {
        get => _memoryWriteValueText;
        set => SetProperty(ref _memoryWriteValueText, value ?? string.Empty);
    }

    public string MemoryWriteStatusText
    {
        get => _memoryWriteStatusText;
        private set => SetProperty(ref _memoryWriteStatusText, value);
    }

    public string MemoryWriteErrorText
    {
        get => _memoryWriteErrorText;
        private set => SetProperty(ref _memoryWriteErrorText, value);
    }

    public string MemoryWriteResultText
    {
        get => _memoryWriteResultText;
        private set => SetProperty(ref _memoryWriteResultText, value);
    }

    public string ScanValueText
    {
        get => _scanValueText;
        set => SetProperty(ref _scanValueText, value ?? string.Empty);
    }

    public string ScanSecondaryValueText
    {
        get => _scanSecondaryValueText;
        set => SetProperty(ref _scanSecondaryValueText, value ?? string.Empty);
    }

    public int ScanInputValueCount => SelectedScanType?.ScanType.InputValueCount ?? 0;

    public bool ShowsPrimaryScanValue => ScanInputValueCount >= 1;

    public bool ShowsSecondaryScanValue => ScanInputValueCount >= 2;

    public string PrimaryScanValueLabel => ShowsSecondaryScanValue ? "Value 1" : "Value";

    public string SecondaryScanValueLabel => "Value 2";

    public ReadOnlyObservableCollection<ScanTypeViewModel> ScanTypes => _readOnlyScanTypes;

    public ScanTypeViewModel? SelectedScanType
    {
        get => _selectedScanType;
        set
        {
            if (value is null)
            {
                return;
            }

            if (!CanSelectScanType && value != _selectedScanType)
            {
                return;
            }

            if (SetProperty(ref _selectedScanType, value))
            {
                ScanErrorText = string.Empty;
                RaiseScanInputLayoutProperties();
                RefreshScanOptionStates();
                OnPropertyChanged(nameof(ScanValueToolTip));
            }
        }
    }

    public bool CanSelectScanType =>
        SupportsSharedScanner && _scanTypes.Count > 1 && !IsScanningMemory;

    public IReadOnlyList<ScanValueTypeViewModel> ScanValueTypes => _scanValueTypes;

    public IReadOnlyList<ScanOptionViewModel> ScanOptions => _scanOptions;

    public IReadOnlyList<ScanOptionViewModel> ChoiceScanOptions => _choiceScanOptions;

    public IReadOnlyList<ScanOptionViewModel> ToggleScanOptions => _toggleScanOptions;

    public ScanValueTypeViewModel? SelectedScanValueType
    {
        get => _selectedScanValueType;
        set
        {
            if (value is null)
            {
                return;
            }

            if (!CanSelectScanValueType && value != _selectedScanValueType)
            {
                return;
            }

            if (SetProperty(ref _selectedScanValueType, value))
            {
                ScanErrorText = string.Empty;
                RefreshScanTypesForCurrentStage(notify: true);
                RefreshScanOptionStates();
                OnPropertyChanged(nameof(ScanValueToolTip));
            }
        }
    }

    public bool CanSelectScanValueType =>
        SupportsMemoryRead &&
        SupportsMemoryRegionEnumeration &&
        _supportedScanTypes.Count > 0 &&
        _scanValueTypes.Count > 1 &&
        !IsScanningMemory &&
        !_hasScanSession;

    public string ScanValueToolTip =>
        SelectedScanValueType is null
            ? "The active plugin does not expose a compatible Value Type."
            : ScanInputValueCount == 0
                ? $"{SelectedScanType?.Description} Press Enter on the highlighted scan action to run it."
                : $"{SelectedScanValueType.InputDescription} Press Enter to run the highlighted scan action.";

    public string ScanStatusText
    {
        get => _scanStatusText;
        private set
        {
            if (SetProperty(ref _scanStatusText, value))
            {
                OnPropertyChanged(nameof(ScanStatusBarText));
            }
        }
    }

    public string ScanErrorText
    {
        get => _scanErrorText;
        private set
        {
            if (SetProperty(ref _scanErrorText, value))
            {
                OnPropertyChanged(nameof(ScanStatusBarText));
                OnPropertyChanged(nameof(HasScanError));
            }
        }
    }

    public string ScanStatusBarText => string.IsNullOrWhiteSpace(ScanErrorText)
        ? ScanStatusText
        : $"{ScanStatusText} {ScanErrorText}";

    public bool HasScanError => !string.IsNullOrWhiteSpace(ScanErrorText);

    public double ScanProgressPercentage
    {
        get => _scanProgressPercentage;
        private set => SetProperty(ref _scanProgressPercentage, value);
    }

    public string ScanElapsedText
    {
        get => _scanElapsedText;
        private set => SetProperty(ref _scanElapsedText, value);
    }

    public bool HasScanProgress
    {
        get => _hasScanProgress;
        private set => SetProperty(ref _hasScanProgress, value);
    }

    public bool IsScanProgressIndeterminate
    {
        get => _isScanProgressIndeterminate;
        private set => SetProperty(ref _isScanProgressIndeterminate, value);
    }

    public bool PauseTargetWhileScanning
    {
        get => _pauseTargetWhileScanning;
        set
        {
            if (!CanConfigureScanPause && value != _pauseTargetWhileScanning)
            {
                return;
            }

            if (SetProperty(ref _pauseTargetWhileScanning, value))
            {
                UpdateSavedAddressTimerState();
            }
        }
    }

    public IReadOnlyList<ScanResultViewModel> ScanResults
    {
        get => _scanResults;
        private set
        {
            if (ReferenceEquals(_scanResults, value))
            {
                return;
            }

            _visibleScanResults.Clear();
            if (SetProperty(ref _scanResults, value))
            {
                OnPropertyChanged(nameof(HasVisibleScanResults));
                OnPropertyChanged(nameof(CanExportScanResults));
                UpdateSavedAddressTimerState();
            }
        }
    }

    public bool HasVisibleScanResults => ScanResults.Count > 0;

    public bool CanExportScanResults =>
        HasVisibleScanResults &&
        !IsScanningMemory &&
        !_foregroundTargetOperationPending &&
        !_isExportingResidentScanResults;

    public ObservableCollection<SavedAddressViewModel> SavedAddresses { get; } = new();

    public SavedAddressViewModel? SelectedSavedAddress
    {
        get => _selectedSavedAddress;
        set
        {
            if (SetProperty(ref _selectedSavedAddress, value))
            {
                    }
        }
    }

    public bool HasSavedAddresses => SavedAddresses.Count > 0;

    public bool CanExportSavedAddresses =>
        HasSavedAddresses &&
        !_savedAddressUserOperationInProgress;

    public bool CanAddSavedAddressManually =>
        SupportsMemoryRead &&
        _scanValueTypes.Count > 0 &&
        CanBeginSavedAddressUserOperation() &&
        CanUseSavedAddressTargetForUserOperation() &&
        !IsReadingMemory &&
        !IsWritingMemory;

    public string SavedAddressCountText => SavedAddresses.Count == 1
        ? "1 address"
        : $"{SavedAddresses.Count:N0} addresses";

    public string SavedAddressStatusText
    {
        get => _savedAddressStatusText;
        private set => SetProperty(ref _savedAddressStatusText, value);
    }

    public int SavedAddressUpdateIntervalMilliseconds => _savedAddressUpdateIntervalMilliseconds;

    public int FrozenWriteIntervalMilliseconds => _frozenWriteIntervalMilliseconds;

    public string SavedAddressUpdateIntervalText =>
        $"Refresh: {_savedAddressUpdateIntervalMilliseconds:N0} ms • Frozen: {_frozenWriteIntervalMilliseconds:N0} ms";

    public bool IsFirstScanPrimaryAction => !_hasScanSession;

    public bool IsNextScanPrimaryAction => _hasScanSession;

    public string ScanResultCountText => $"{_scanResultCount:N0} results";

    public string ScanEmptyMessage => _hasScanSession
        ? "No matching addresses found."
        : "No scan has been started.";

    public string ScanResultFooterText
    {
        get
        {
            long displayedCount = Math.Min(_scanResultCount, MaximumDisplayedScanResults);
            return $"Showing {displayedCount:N0} results of {_scanResultCount:N0}.";
        }
    }

    public ICommand ConnectCommand => _connectCommand;

    public ICommand DisconnectCommand => _disconnectCommand;

    public ICommand RefreshProcessesCommand => _refreshProcessesCommand;

    public ICommand SetActiveProcessCommand => _setActiveProcessCommand;

    public ICommand ReadMemoryCommand => _readMemoryCommand;

    public ICommand WriteMemoryCommand => _writeMemoryCommand;

    public ICommand SafeWriteTestCommand => _safeWriteTestCommand;

    public ICommand FirstScanCommand => _firstScanCommand;

    public ICommand NextScanCommand => _nextScanCommand;

    public ICommand ScanSubmitCommand => _submitScanCommand;

    public ICommand NewScanCommand => _newScanCommand;

    public ICommand CancelScanCommand => _cancelScanCommand;


    internal void RegisterVisibleScanResult(ScanResultViewModel scanResult)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        if (_disposed || !ScanResults.Contains(scanResult))
        {
            return;
        }

        if (_visibleScanResults.Add(scanResult))
        {
            UpdateSavedAddressTimerState();
        }
    }

    internal void UnregisterVisibleScanResult(ScanResultViewModel scanResult)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        if (_visibleScanResults.Remove(scanResult))
        {
            UpdateSavedAddressTimerState();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanElapsedTimer.Stop();
        _scanElapsedTimer.Tick -= OnScanElapsedTimerTick;
        _savedAddressUpdateTimer.Stop();
        _savedAddressUpdateTimer.Tick -= OnSavedAddressUpdateTimerTick;
        _frozenWriteTimer.Stop();
        _frozenWriteTimer.Tick -= OnFrozenWriteTimerTick;
        _scanStopwatch.Stop();
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = null;
        ReleaseScanResultStorageSession();
        _lifetimeCancellation.Cancel();
        _savedAddressesPendingRemoval.Clear();

        DisposeDebuggerSessionsAsync().GetAwaiter().GetResult();

        if (_session is not null)
        {
            _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _session = null;
        }

        _lifetimeCancellation.Dispose();
    }

    private bool CanConnect()
    {
        return !_disposed &&
               !_foregroundTargetOperationPending &&
               !_isExportingResidentScanResults &&
               SupportsConnection &&
               !IsConnecting &&
               !IsConnected &&
               !IsScanningMemory;
    }

    private bool CanDisconnect()
    {
        return !_disposed &&
               !_foregroundTargetOperationPending &&
               !_isExportingResidentScanResults &&
               !IsConnecting &&
               !IsReadingMemory &&
               !IsWritingMemory &&
               !IsScanningMemory &&
               IsConnected;
    }

    private bool CanRefreshProcesses()
    {
        return !_disposed &&
               !_foregroundTargetOperationPending &&
               !_isExportingResidentScanResults &&
               SupportsProcessEnumeration &&
               IsConnected &&
               !IsConnecting &&
               !IsRefreshingProcesses &&
               !IsLoadingMemoryRegions &&
               !IsReadingMemory &&
               !IsWritingMemory &&
               !IsScanningMemory;
    }

    private bool CanSetActiveProcess()
    {
        return !_disposed &&
               !_foregroundTargetOperationPending &&
               !_isExportingResidentScanResults &&
               SupportsProcessEnumeration &&
               IsConnected &&
               !IsConnecting &&
               !IsRefreshingProcesses &&
               !IsLoadingMemoryRegions &&
               !IsReadingMemory &&
               !IsWritingMemory &&
               !IsScanningMemory &&
               SelectedProcess is not null &&
               !AreSameProcess(SelectedProcess.Process, ActiveProcess?.Process);
    }

    private bool CanReadMemory()
    {
        return !_disposed &&
               !_foregroundTargetOperationPending &&
               !_isExportingResidentScanResults &&
               SupportsMemoryRead &&
               IsConnected &&
               !IsConnecting &&
               !IsRefreshingProcesses &&
               !IsLoadingMemoryRegions &&
               !IsReadingMemory &&
               !IsWritingMemory &&
               !IsScanningMemory &&
               ActiveProcess is not null;
    }

    private bool CanWriteMemory()
    {
        return !_disposed &&
               !_foregroundTargetOperationPending &&
               !_isExportingResidentScanResults &&
               SupportsMemoryWrite &&
               IsConnected &&
               !IsConnecting &&
               !IsRefreshingProcesses &&
               !IsLoadingMemoryRegions &&
               !IsReadingMemory &&
               !IsWritingMemory &&
               !IsScanningMemory &&
               ActiveProcess is not null;
    }

    private bool CanRunSafeWriteTest()
    {
        return CanWriteMemory() &&
               SupportsMemoryRead &&
               SupportsMemoryRegionEnumeration &&
               ActiveMemoryRegions.Count > 0;
    }

    private bool CanFirstScan()
    {
        return !_disposed &&
               !_foregroundTargetOperationPending &&
               !_isExportingResidentScanResults &&
               SupportsSharedScanner &&
               IsConnected &&
               !IsConnecting &&
               !IsRefreshingProcesses &&
               !IsLoadingMemoryRegions &&
               !IsReadingMemory &&
               !IsWritingMemory &&
               !IsScanningMemory &&
               ActiveProcess is not null &&
               ActiveMemoryRegions.Count > 0;
    }

    private bool CanNextScan()
    {
        return CanFirstScan() && _hasScanSession;
    }

    private bool CanSubmitScan()
    {
        return _hasScanSession ? CanNextScan() : CanFirstScan();
    }

    private bool CanStartNewScan()
    {
        return !_disposed &&
               !_foregroundTargetOperationPending &&
               !_isExportingResidentScanResults &&
               !IsScanningMemory &&
               _hasScanSession;
    }

    private bool CanCancelScan()
    {
        return !_disposed &&
               IsScanningMemory &&
               _scanCancellation is not null &&
               !_scanCancellation.IsCancellationRequested;
    }


    private async Task ConnectAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string? validationError = ValidateConnectionSettings();
        if (validationError is not null)
        {
            ConnectionErrorText = validationError;
            ConnectionStatusText = "Connection settings are incomplete.";
            return;
        }

        IsConnecting = true;
        ConnectionErrorText = string.Empty;
        ConnectionStatusText = "Connecting...";

        if (SupportsProcessEnumeration)
        {
            ClearProcessState("Connecting before process enumeration...");
        }

        try
        {
            TargetConnectionOptions options = new(
                ConnectionSettings.Select(setting =>
                    new KeyValuePair<string, string>(setting.Key, setting.Value)));

            ITargetSession session = await Instance
                .ConnectAsync(options, _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (!session.IsConnected)
            {
                await session.DisposeAsync().ConfigureAwait(true);
                throw new InvalidOperationException("The plugin returned a target session that is not connected.");
            }

            if (_session is not null)
            {
                await DisposeDebuggerSessionsAsync().ConfigureAwait(true);
                await _session.DisposeAsync().ConfigureAwait(true);
            }

            _session = session;
            _connectionGeneration++;
            IsConnected = true;
            ConnectionStatusText = "Connected.";

            if (SupportsProcessEnumeration)
            {
                await RefreshProcessesAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            ConnectionStatusText = "Connection cancelled.";
        }
        catch (Exception exception)
        {
            ConnectionErrorText = exception.Message;
            ConnectionStatusText = "Connection failed.";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private async Task DisconnectAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        bool waitingForSavedAddressIo = IsSavedAddressIoInProgress;
        ConnectionErrorText = string.Empty;
        if (waitingForSavedAddressIo)
        {
            ConnectionStatusText = "Waiting for the current Saved Address operation before disconnecting...";
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            return;
        }

        SetSavedAddressTargetStateTransition(true);

        try
        {
            if (_session is not null)
            {
                await DisposeDebuggerSessionsAsync().ConfigureAwait(true);
                await _session.DisposeAsync().ConfigureAwait(true);
                _session = null;
            }

            IsConnected = false;
            ConnectionStatusText = "Not connected.";

            if (SupportsProcessEnumeration)
            {
                ClearProcessState("Connect to enumerate processes.");
            }
        }
        catch (Exception exception)
        {
            ConnectionErrorText = exception.Message;
            ConnectionStatusText = "Disconnect failed.";
        }
        finally
        {
            SetSavedAddressTargetStateTransition(false);
            EndForegroundTargetOperation();
        }
    }

    private async Task RefreshProcessesAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null || !IsConnected)
        {
            ProcessStatusText = "Connect to enumerate processes.";
            return;
        }

        bool waitingForSavedAddressIo = IsSavedAddressIoInProgress;
        ProcessErrorText = string.Empty;
        if (waitingForSavedAddressIo)
        {
            ProcessStatusText = "Waiting for the current Saved Address operation before refreshing processes...";
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            return;
        }

        IsRefreshingProcesses = true;
        ProcessStatusText = "Loading processes...";

        TargetProcess? selectedIdentity = SelectedProcess?.Process;
        TargetProcess? activeIdentity = ActiveProcess?.Process;

        try
        {
            IProcessProvider? processProvider = _session.GetService<IProcessProvider>();
            if (processProvider is null)
            {
                throw new InvalidOperationException(
                    "The plugin advertises process enumeration but the active session does not provide IProcessProvider.");
            }

            IReadOnlyList<TargetProcess> processes = await processProvider
                .GetProcessesAsync(_lifetimeCancellation.Token)
                .ConfigureAwait(true);

            IReadOnlyList<TargetProcessViewModel> ordered = processes
                .OrderBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(process => process.Id)
                .Select(process => new TargetProcessViewModel(process))
                .ToArray();

            Processes.Clear();
            foreach (TargetProcessViewModel process in ordered)
            {
                Processes.Add(process);
            }

            SelectedProcess = FindMatchingProcess(selectedIdentity);
            ActiveProcess = FindMatchingProcess(activeIdentity);

            if (SelectedProcess is null &&
                Instance.Capabilities.HasFlag(TargetCapabilities.ForegroundProcess))
            {
                IForegroundProcessProvider? foregroundProcessProvider =
                    _session.GetService<IForegroundProcessProvider>();
                if (foregroundProcessProvider is null)
                {
                    throw new InvalidOperationException(
                        "The plugin advertises foreground-process selection but the active session does not provide IForegroundProcessProvider.");
                }

                TargetProcess? preferredProcess = await foregroundProcessProvider
                    .GetForegroundProcessAsync(_lifetimeCancellation.Token)
                    .ConfigureAwait(true);
                SelectedProcess = FindMatchingProcess(preferredProcess);
            }

            if (SelectedProcess is null && Processes.Count == 1)
            {
                SelectedProcess = Processes[0];
            }

            ProcessStatusText = Processes.Count switch
            {
                0 => "No processes were returned by the target.",
                1 => "1 process loaded.",
                _ => $"{Processes.Count} processes loaded."
            };

            if (ActiveProcess is null)
            {
                await ResetNativeScanSessionIfAvailableAsync(CancellationToken.None).ConfigureAwait(true);
                ClearMemoryRegionState();
                ClearMemoryReadState(clearAddress: true);
                ClearMemoryWriteState(clearInputs: true);
                ClearScanState();
            }
            else if (SupportsMemoryRegionEnumeration)
            {
                await RefreshMemoryRegionsAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            ProcessStatusText = "Process enumeration cancelled.";
        }
        catch (Exception exception)
        {
            ProcessErrorText = exception.Message;
            ProcessStatusText = "Process enumeration failed.";
        }
        finally
        {
            IsRefreshingProcesses = false;
            EndForegroundTargetOperation();
        }
    }

    private async Task SetActiveProcessAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (SelectedProcess is null)
        {
            return;
        }

        bool waitingForSavedAddressIo = IsSavedAddressIoInProgress;
        if (waitingForSavedAddressIo)
        {
            ProcessStatusText = "Waiting for the current Saved Address operation before changing Active Target...";
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            return;
        }

        SetSavedAddressTargetStateTransition(true);
        try
        {
            await DisposeDebuggerSessionsAsync().ConfigureAwait(true);
            await ResetNativeScanSessionIfAvailableAsync(CancellationToken.None).ConfigureAwait(true);

            ActiveProcess = SelectedProcess;
            ProcessErrorText = string.Empty;
            ClearMemoryReadState(clearAddress: true);
            ClearMemoryWriteState(clearInputs: true);
            ClearScanState();

            if (SupportsMemoryRegionEnumeration)
            {
                await RefreshMemoryRegionsAsync().ConfigureAwait(true);
            }
            else
            {
                ClearMemoryRegionState();
            }
        }
        finally
        {
            SetSavedAddressTargetStateTransition(false);
            EndForegroundTargetOperation();
        }
    }

    private async Task RefreshMemoryRegionsAsync()
    {
        if (_session is null || ActiveProcess is null || !IsConnected)
        {
            ClearMemoryRegionState();
            return;
        }

        IsLoadingMemoryRegions = true;
        MemoryRegionErrorText = string.Empty;
        MemoryRegionStatusText = "Loading memory regions...";

        try
        {
            IMemoryMapProvider? memoryMapProvider = _session.GetService<IMemoryMapProvider>();
            if (memoryMapProvider is null)
            {
                throw new InvalidOperationException(
                    "The plugin advertises memory-region enumeration but the active session does not provide IMemoryMapProvider.");
            }

            IReadOnlyList<MemoryRegion> regions = await memoryMapProvider
                .GetMemoryRegionsAsync(ActiveProcess.Process, _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            ActiveMemoryRegions = regions
                .OrderBy(region => region.BaseAddress)
                .ThenBy(region => region.Size)
                .ToArray();

            MemoryRegionStatusText = ActiveMemoryRegions.Count switch
            {
                0 => "Memory map: no regions returned.",
                1 => "Memory map: 1 region loaded.",
                _ => $"Memory map: {ActiveMemoryRegions.Count} regions loaded."
            };

            EnsureMemoryReadDefaultAddress();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            ClearMemoryRegionState("Memory-map enumeration cancelled.");
        }
        catch (Exception exception)
        {
            ActiveMemoryRegions = Array.Empty<MemoryRegion>();
            MemoryRegionErrorText = exception.Message;
            MemoryRegionStatusText = "Memory-map enumeration failed.";
        }
        finally
        {
            IsLoadingMemoryRegions = false;
            _setActiveProcessCommand.RaiseCanExecuteChanged();
        }
    }

    internal long ConnectionGeneration => _connectionGeneration;

    internal DebuggerSessionCoordinator CreateDebuggerSessionCoordinator(
        TargetProcess targetProcess,
        long connectionGeneration)
    {
        ArgumentNullException.ThrowIfNull(targetProcess);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsCurrentDebuggerTarget(targetProcess, connectionGeneration) ||
            _session is not ITargetSession session)
        {
            throw new InvalidOperationException(
                "The Debugger target is no longer the current Active Target. Reconnect or open a new Debugger window for the current target.");
        }

        if (!CanOpenDebugger)
        {
            throw new InvalidOperationException(
                "The Debugger cannot attach while another target operation is in progress.");
        }

        IDebuggerProvider? provider = session.GetService<IDebuggerProvider>();
        if (provider is null)
        {
            throw new InvalidOperationException(
                "The active plugin advertises debugger support but the connected target session does not provide an IDebuggerProvider.");
        }

        DebuggerSessionIdentity identity = new(Metadata.Id, targetProcess, connectionGeneration);
        DebuggerSessionCoordinator coordinator = new(identity, targetProcess, provider);
        _debuggerSessions.Add(coordinator);
        return coordinator;
    }

    internal async Task ReleaseDebuggerSessionAsync(DebuggerSessionCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        if (_debuggerSessions.Remove(coordinator))
        {
            await coordinator.DisposeAsync().ConfigureAwait(true);
        }
    }

    private async Task DisposeDebuggerSessionsAsync()
    {
        if (_debuggerSessions.Count == 0)
        {
            return;
        }

        DebuggerSessionCoordinator[] sessions = _debuggerSessions.ToArray();
        _debuggerSessions.Clear();

        foreach (DebuggerSessionCoordinator coordinator in sessions)
        {
            await coordinator.DisposeAsync().ConfigureAwait(true);
        }
    }

    private DisassemblyOverlay? GetDebuggerDisassemblyOverlay(
        TargetProcess targetProcess,
        long connectionGeneration)
    {
        DebuggerSessionCoordinator? coordinator = _debuggerSessions
            .FirstOrDefault(session =>
                session.IsAttached &&
                session.Identity.Matches(Metadata.Id, targetProcess, connectionGeneration));
        if (coordinator is null)
        {
            return null;
        }

        DisassemblyOverlay overlay = coordinator.DisassemblyOverlayState.CreateOverlay();
        return overlay.IsEmpty ? null : overlay;
    }

    internal async Task<DisassemblySnapshot> ReadDisassemblyContextAsync(
        TargetProcess targetProcess,
        long connectionGeneration,
        ulong address,
        int beforeByteCount,
        int afterByteCount,
        CancellationToken cancellationToken)
    {
        return await ReadDisassemblyAsync(
                targetProcess,
                connectionGeneration,
                address,
                beforeByteCount,
                afterByteCount,
                useContextWindow: true,
                cancellationToken)
            .ConfigureAwait(true);
    }

    internal async Task<DisassemblySnapshot> ReadDisassemblyWindowAsync(
        TargetProcess targetProcess,
        long connectionGeneration,
        ulong address,
        int byteCount,
        CancellationToken cancellationToken)
    {
        return await ReadDisassemblyAsync(
                targetProcess,
                connectionGeneration,
                address,
                beforeByteCount: 0,
                afterByteCount: byteCount,
                useContextWindow: false,
                cancellationToken)
            .ConfigureAwait(true);
    }

    private async Task<DisassemblySnapshot> ReadDisassemblyAsync(
        TargetProcess targetProcess,
        long connectionGeneration,
        ulong address,
        int beforeByteCount,
        int afterByteCount,
        bool useContextWindow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetProcess);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is not ITargetSession session || !IsConnected)
        {
            throw new InvalidOperationException(
                "The Disassembler connection is no longer active. Reconnect and open a new Disassembler window.");
        }

        if (connectionGeneration != _connectionGeneration)
        {
            throw new InvalidOperationException(
                "This Disassembler belongs to an earlier target connection. Close it and open a new Disassembler window for the current connection.");
        }

        if (ActiveProcess is not TargetProcessViewModel activeProcess ||
            !AreSameProcess(activeProcess.Process, targetProcess))
        {
            throw new InvalidOperationException(
                "The Disassembler target is no longer the current Active Target. Reactivate the original target before refreshing this window.");
        }

        if (!SupportsDisassembly || !SupportsMemoryRead || !SupportsMemoryRegionEnumeration)
        {
            throw new InvalidOperationException(
                "The active plugin does not provide the disassembly, memory-read, and memory-map capabilities required by Disassembler.");
        }

        if (ActiveMemoryRegions.Count == 0)
        {
            throw new InvalidOperationException("The Active Target does not currently have a loaded memory map.");
        }

        if (!CanReadMemory())
        {
            throw new InvalidOperationException(
                "Disassembler cannot read while another foreground target operation is active. Try Refresh again after the current operation finishes.");
        }

        IMemoryReader? reader = session.GetService<IMemoryReader>();
        IDisassemblerProvider? disassembler = session.GetService<IDisassemblerProvider>();
        if (reader is null || disassembler is null)
        {
            throw new InvalidOperationException(
                "The active session does not provide the memory reader and disassembly provider required by Disassembler.");
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            throw new OperationCanceledException("Disassembler could not reserve the Active Target for reading.");
        }

        IsReadingMemory = true;
        try
        {
            if (_session is not ITargetSession currentSession ||
                ActiveProcess is not TargetProcessViewModel currentActiveProcess ||
                !IsConnected ||
                connectionGeneration != _connectionGeneration ||
                !AreSameProcess(currentActiveProcess.Process, targetProcess) ||
                !ReferenceEquals(session, currentSession))
            {
                throw new InvalidOperationException(
                    "The Active Target changed before Disassembler could begin reading.");
            }

            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
            MemoryRegion[] memoryMapSnapshot = ActiveMemoryRegions.ToArray();
            DisassemblyOverlay? disassemblyOverlay = GetDebuggerDisassemblyOverlay(
                targetProcess,
                connectionGeneration);

            if (useContextWindow)
            {
                return await _disassemblyReader
                    .ReadAroundAsync(
                        reader,
                        disassembler,
                        targetProcess,
                        memoryMapSnapshot,
                        session.Architecture,
                        address,
                        beforeByteCount,
                        afterByteCount,
                        disassemblyOverlay,
                        linkedCancellation.Token)
                    .ConfigureAwait(true);
            }

            return await _disassemblyReader
                .ReadAsync(
                    reader,
                    disassembler,
                    targetProcess,
                    memoryMapSnapshot,
                    session.Architecture,
                    address,
                    afterByteCount,
                    disassemblyOverlay,
                    linkedCancellation.Token)
                .ConfigureAwait(true);
        }
        finally
        {
            IsReadingMemory = false;
            EndForegroundTargetOperation();
        }
    }

    internal async Task<MemoryViewSnapshot> ReadMemoryViewerWindowAsync(
        TargetProcess targetProcess,
        long connectionGeneration,
        ulong address,
        int byteCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetProcess);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is not ITargetSession session || !IsConnected)
        {
            throw new InvalidOperationException(
                "The Memory Viewer connection is no longer active. Reconnect and open a new Memory Viewer window.");
        }

        if (connectionGeneration != _connectionGeneration)
        {
            throw new InvalidOperationException(
                "This Memory Viewer belongs to an earlier target connection. Close it and open a new Memory Viewer window for the current connection.");
        }

        if (ActiveProcess is not TargetProcessViewModel activeProcess ||
            !AreSameProcess(activeProcess.Process, targetProcess))
        {
            throw new InvalidOperationException(
                "The Memory Viewer target is no longer the current Active Target. Reactivate the original target before refreshing this viewer.");
        }

        if (!SupportsMemoryRead || !SupportsMemoryRegionEnumeration)
        {
            throw new InvalidOperationException(
                "The active plugin does not provide the memory-read and memory-map capabilities required by Memory Viewer.");
        }

        if (ActiveMemoryRegions.Count == 0)
        {
            throw new InvalidOperationException("The Active Target does not currently have a loaded memory map.");
        }

        if (!CanReadMemory())
        {
            throw new InvalidOperationException(
                "Memory Viewer cannot read while another foreground target operation is active. Try Refresh again after the current operation finishes.");
        }

        IMemoryReader? reader = session.GetService<IMemoryReader>();
        if (reader is null)
        {
            throw new InvalidOperationException(
                "The plugin advertises memory read support but the active session does not provide IMemoryReader.");
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            throw new OperationCanceledException("Memory Viewer could not reserve the Active Target for reading.");
        }

        IsReadingMemory = true;
        try
        {
            if (_session is not ITargetSession currentSession ||
                ActiveProcess is not TargetProcessViewModel currentActiveProcess ||
                !IsConnected ||
                connectionGeneration != _connectionGeneration ||
                !AreSameProcess(currentActiveProcess.Process, targetProcess) ||
                !ReferenceEquals(session, currentSession))
            {
                throw new InvalidOperationException(
                    "The Active Target changed before Memory Viewer could begin reading.");
            }

            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
            MemoryRegion[] memoryMapSnapshot = ActiveMemoryRegions.ToArray();

            return await _memoryViewerReader
                .ReadWindowAsync(
                    reader,
                    targetProcess,
                    memoryMapSnapshot,
                    address,
                    byteCount,
                    MemoryViewerReader.DefaultBytesPerRow,
                    linkedCancellation.Token)
                .ConfigureAwait(true);
        }
        finally
        {
            IsReadingMemory = false;
            EndForegroundTargetOperation();
        }
    }

    internal async Task<MemoryViewWriteResult> WriteMemoryViewerBytesAsync(
        TargetProcess targetProcess,
        long connectionGeneration,
        ulong address,
        ReadOnlyMemory<byte> expectedBytes,
        ReadOnlyMemory<byte> replacementBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetProcess);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is not ITargetSession session || !IsConnected)
        {
            throw new InvalidOperationException(
                "The Memory Viewer connection is no longer active. Reconnect and open a new Memory Viewer window.");
        }

        if (connectionGeneration != _connectionGeneration)
        {
            throw new InvalidOperationException(
                "This Memory Viewer belongs to an earlier target connection. Close it and open a new Memory Viewer window for the current connection.");
        }

        if (ActiveProcess is not TargetProcessViewModel activeProcess ||
            !AreSameProcess(activeProcess.Process, targetProcess))
        {
            throw new InvalidOperationException(
                "The Memory Viewer target is no longer the current Active Target. Reactivate the original target before editing memory.");
        }

        if (!SupportsMemoryRead || !SupportsMemoryWrite || !SupportsMemoryRegionEnumeration)
        {
            throw new InvalidOperationException(
                "Safe Memory Viewer editing requires memory-read, memory-write, and memory-map capabilities from the active plugin.");
        }

        if (ActiveMemoryRegions.Count == 0)
        {
            throw new InvalidOperationException("The Active Target does not currently have a loaded memory map.");
        }

        if (!CanWriteMemory())
        {
            throw new InvalidOperationException(
                "Memory Viewer cannot write while another foreground target operation is active. Try the edit again after the current operation finishes.");
        }

        IMemoryReader? reader = session.GetService<IMemoryReader>();
        IMemoryWriter? writer = session.GetService<IMemoryWriter>();
        if (reader is null || writer is null)
        {
            throw new InvalidOperationException(
                "The active session does not provide the memory reader and writer required for safe Memory Viewer editing.");
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            throw new OperationCanceledException("Memory Viewer could not reserve the Active Target for writing.");
        }

        IsWritingMemory = true;
        try
        {
            if (_session is not ITargetSession currentSession ||
                ActiveProcess is not TargetProcessViewModel currentActiveProcess ||
                !IsConnected ||
                connectionGeneration != _connectionGeneration ||
                !AreSameProcess(currentActiveProcess.Process, targetProcess) ||
                !ReferenceEquals(session, currentSession))
            {
                throw new InvalidOperationException(
                    "The Active Target changed before Memory Viewer could begin writing.");
            }

            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
            MemoryRegion[] memoryMapSnapshot = ActiveMemoryRegions.ToArray();

            return await _memoryViewerWriter
                .WriteAndVerifyAsync(
                    reader,
                    writer,
                    targetProcess,
                    memoryMapSnapshot,
                    address,
                    expectedBytes,
                    replacementBytes,
                    linkedCancellation.Token)
                .ConfigureAwait(true);
        }
        finally
        {
            IsWritingMemory = false;
            EndForegroundTargetOperation();
        }
    }

    private async Task ReadMemoryAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is not ITargetSession session ||
            ActiveProcess is not TargetProcessViewModel activeProcess ||
            !IsConnected)
        {
            MemoryReadStatusText = "Set an Active Target to read memory.";
            return;
        }

        MemoryReadErrorText = string.Empty;
        MemoryReadResultText = string.Empty;

        if (!TryParseHexAddress(MemoryReadAddressText, out ulong address))
        {
            MemoryReadErrorText = "Enter a valid hexadecimal address, for example 0x100000000.";
            MemoryReadStatusText = "Memory read not started.";
            return;
        }

        if (!TryParseReadLength(MemoryReadLengthText, out int length))
        {
            MemoryReadErrorText = $"Length must be between 1 and {MaximumInspectorReadLength} bytes.";
            MemoryReadStatusText = "Memory read not started.";
            return;
        }

        MemoryRegion? readableRegion = null;
        if (SupportsMemoryRegionEnumeration)
        {
            readableRegion = FindReadableRegion(address, length);
            if (readableRegion is null)
            {
                MemoryReadErrorText =
                    "The requested range is not fully contained in a readable Active Target memory region.";
                MemoryReadStatusText = "Memory read blocked by the current memory map.";
                return;
            }
        }

        IMemoryReader? reader = session.GetService<IMemoryReader>();
        if (reader is null)
        {
            MemoryReadErrorText =
                "The plugin advertises memory read support but the active session does not provide IMemoryReader.";
            MemoryReadStatusText = "Memory read unavailable.";
            return;
        }

        bool waitingForSavedAddressIo = IsSavedAddressIoInProgress;
        if (waitingForSavedAddressIo)
        {
            MemoryReadStatusText = $"Waiting for the current Saved Address operation before reading 0x{address:X}...";
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            return;
        }

        IsReadingMemory = true;
        MemoryReadStatusText = $"Reading {length} bytes from 0x{address:X}...";

        try
        {
            byte[] buffer = new byte[length];
            int bytesRead = await reader
                .ReadAsync(
                    activeProcess.Process,
                    address,
                    buffer,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (bytesRead < 0 || bytesRead > buffer.Length)
            {
                throw new InvalidOperationException(
                    $"The memory reader returned an invalid byte count of {bytesRead} for a {buffer.Length}-byte destination.");
            }

            MemoryReadResultText = FormatHexDump(address, buffer.AsSpan(0, bytesRead));
            string regionText = readableRegion is null
                ? string.Empty
                : $" in {FormatRegionName(readableRegion)}";
            MemoryReadStatusText = $"Read {bytesRead} bytes from 0x{address:X}{regionText}.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            MemoryReadStatusText = "Memory read cancelled.";
        }
        catch (Exception exception)
        {
            MemoryReadErrorText = exception.Message;
            MemoryReadStatusText = "Memory read failed.";
        }
        finally
        {
            IsReadingMemory = false;
            EndForegroundTargetOperation();
        }
    }

    private async Task WriteMemoryAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is not ITargetSession session ||
            ActiveProcess is not TargetProcessViewModel activeProcess ||
            !IsConnected)
        {
            MemoryWriteStatusText = "Set an Active Target to write memory.";
            return;
        }

        MemoryWriteErrorText = string.Empty;
        MemoryWriteResultText = string.Empty;

        if (!TryParseHexAddress(MemoryWriteAddressText, out ulong address))
        {
            MemoryWriteErrorText = "Enter a valid hexadecimal address, for example 0x100000000.";
            MemoryWriteStatusText = "Memory write not started.";
            return;
        }

        if (!TryParseInt32DecimalValue(MemoryWriteValueText, out int requestedValue))
        {
            MemoryWriteErrorText =
                "Enter a signed 4-byte integer value in decimal form, from -2147483648 through 2147483647.";
            MemoryWriteStatusText = "Memory write not started.";
            return;
        }

        byte[] requestedBytes = EncodeInt32(requestedValue, session.Architecture.Endianness);

        MemoryRegion? writableRegion = null;
        if (SupportsMemoryRegionEnumeration)
        {
            writableRegion = FindWritableRegion(address, requestedBytes.Length);
            if (writableRegion is null)
            {
                MemoryWriteErrorText =
                    "The requested range is not fully contained in a writable Active Target memory region.";
                MemoryWriteStatusText = "Memory write blocked by the current memory map.";
                return;
            }
        }

        IMemoryWriter? writer = session.GetService<IMemoryWriter>();
        if (writer is null)
        {
            MemoryWriteErrorText =
                "The plugin advertises memory write support but the active session does not provide IMemoryWriter.";
            MemoryWriteStatusText = "Memory write unavailable.";
            return;
        }

        IMemoryReader? reader = SupportsMemoryRead
            ? session.GetService<IMemoryReader>()
            : null;

        bool canReadBack = reader is not null &&
                           (!SupportsMemoryRegionEnumeration ||
                            FindReadableRegion(address, requestedBytes.Length) is not null);

        bool waitingForSavedAddressIo = IsSavedAddressIoInProgress;
        if (waitingForSavedAddressIo)
        {
            MemoryWriteStatusText = $"Waiting for the current Saved Address operation before writing 0x{address:X}...";
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            return;
        }

        IsWritingMemory = true;
        MemoryWriteStatusText = $"Writing value {requestedValue} to 0x{address:X}...";

        byte[]? originalBytes = null;
        bool writeCompleted = false;

        try
        {
            if (canReadBack)
            {
                originalBytes = new byte[requestedBytes.Length];
                int originalRead = await reader!
                    .ReadAsync(
                        activeProcess.Process,
                        address,
                        originalBytes,
                        _lifetimeCancellation.Token)
                    .ConfigureAwait(true);

                if (originalRead != originalBytes.Length)
                {
                    throw new InvalidOperationException(
                        $"Could not read the complete original range before writing. Expected {originalBytes.Length} bytes but received {originalRead}.");
                }

                // Preserve the captured bytes in the UI before the write is attempted so they remain
                // available to the user even if transport or read-back verification fails afterward.
                MemoryWriteResultText = FormatMemoryWriteResult(
                    address,
                    originalBytes,
                    requestedBytes,
                    readBackBytes: null,
                    verificationPassed: null);
            }

            await writer
                .WriteAsync(
                    activeProcess.Process,
                    address,
                    requestedBytes,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);
            writeCompleted = true;

            if (!canReadBack)
            {
                MemoryWriteResultText = FormatMemoryWriteResult(
                    address,
                    originalBytes,
                    requestedBytes,
                    readBackBytes: null,
                    verificationPassed: null);

                string regionText = writableRegion is null
                    ? string.Empty
                    : $" in {FormatRegionName(writableRegion)}";
                MemoryWriteStatusText =
                    $"Wrote {requestedBytes.Length} bytes to 0x{address:X}{regionText}. Read-back verification was not available.";
                return;
            }

            byte[] readBackBytes = new byte[requestedBytes.Length];
            int readBackCount = await reader!
                .ReadAsync(
                    activeProcess.Process,
                    address,
                    readBackBytes,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (readBackCount != readBackBytes.Length)
            {
                throw new InvalidOperationException(
                    $"Read-back returned {readBackCount} bytes; expected {readBackBytes.Length}.");
            }

            bool verificationPassed = requestedBytes.SequenceEqual(readBackBytes);
            MemoryWriteResultText = FormatMemoryWriteResult(
                address,
                originalBytes,
                requestedBytes,
                readBackBytes,
                verificationPassed);

            string verifiedRegionText = writableRegion is null
                ? string.Empty
                : $" in {FormatRegionName(writableRegion)}";

            if (verificationPassed)
            {
                MemoryWriteStatusText =
                    $"Wrote and verified {requestedBytes.Length} bytes at 0x{address:X}{verifiedRegionText}.";
            }
            else
            {
                MemoryWriteErrorText =
                    "The write command completed, but the bytes read back from the target do not match the requested bytes.";
                MemoryWriteStatusText = "Memory write completed, but read-back verification failed.";
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            MemoryWriteStatusText = writeCompleted
                ? "Memory write completed, but read-back verification was cancelled."
                : "Memory write cancelled.";
        }
        catch (Exception exception)
        {
            MemoryWriteErrorText = exception.Message;
            MemoryWriteStatusText = writeCompleted
                ? "Memory write completed, but read-back verification failed."
                : "Memory write failed.";
        }
        finally
        {
            IsWritingMemory = false;
            EndForegroundTargetOperation();
        }
    }

    private async Task RunSafeWriteTestAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is not ITargetSession session ||
            ActiveProcess is not TargetProcessViewModel activeProcess ||
            !IsConnected)
        {
            MemoryWriteStatusText = "Set an Active Target to run the safe write test.";
            return;
        }

        IMemoryReader? reader = session.GetService<IMemoryReader>();
        IMemoryWriter? writer = session.GetService<IMemoryWriter>();

        if (reader is null || writer is null)
        {
            MemoryWriteErrorText =
                "The safe write test requires both IMemoryReader and IMemoryWriter from the active session.";
            MemoryWriteStatusText = "Safe write test unavailable.";
            return;
        }

        if (!SupportsMemoryRegionEnumeration || ActiveMemoryRegions.Count == 0)
        {
            MemoryWriteErrorText =
                "The safe write test requires a loaded memory map so it can select a readable and writable non-executable region.";
            MemoryWriteStatusText = "Safe write test unavailable.";
            return;
        }

        MemoryWriteErrorText = string.Empty;
        MemoryWriteResultText = string.Empty;

        bool waitingForSavedAddressIo = IsSavedAddressIoInProgress;
        if (waitingForSavedAddressIo)
        {
            MemoryWriteStatusText = "Waiting for the current Saved Address operation before running the safe write test...";
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            return;
        }

        IsWritingMemory = true;
        MemoryWriteStatusText = "Finding a stable read/write region for the safe write test...";

        try
        {
            (MemoryRegion Region, ulong Address, byte[] Bytes)? candidate =
                await FindStableSafeWriteCandidateAsync(
                        reader,
                        activeProcess.Process,
                        _lifetimeCancellation.Token)
                    .ConfigureAwait(true);

            if (candidate is null)
            {
                MemoryWriteErrorText =
                    "No stable readable and writable non-executable memory range could be found automatically. No write was attempted.";
                MemoryWriteStatusText = "Safe write test not started.";
                return;
            }

            MemoryRegion region = candidate.Value.Region;
            ulong address = candidate.Value.Address;

            // Re-read immediately before the write and use exactly the bytes that are currently present.
            // This keeps the diagnostic test from intentionally changing the target value.
            byte[] bytesToWrite = new byte[SafeWriteTestLength];
            int finalOriginalRead = await reader
                .ReadAsync(
                    activeProcess.Process,
                    address,
                    bytesToWrite,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (finalOriginalRead != bytesToWrite.Length)
            {
                throw new InvalidOperationException(
                    $"The final pre-write read returned {finalOriginalRead} bytes; expected {bytesToWrite.Length}. No write was attempted.");
            }

            if (!candidate.Value.Bytes.SequenceEqual(bytesToWrite))
            {
                MemoryWriteErrorText =
                    "The selected range changed after the stability check. No write was attempted; run Safe Write Test again.";
                MemoryWriteStatusText = "Safe write test blocked because the candidate became active.";
                return;
            }

            MemoryWriteAddressText = $"0x{address:X}";
            MemoryWriteValueText = DecodeInt32(bytesToWrite, session.Architecture.Endianness).ToString(CultureInfo.InvariantCulture);
            MemoryWriteResultText = FormatMemoryWriteResult(
                address,
                bytesToWrite,
                bytesToWrite,
                readBackBytes: null,
                verificationPassed: null);

            MemoryWriteStatusText =
                $"Writing the same {SafeWriteTestLength} bytes back to 0x{address:X} in {FormatRegionName(region)}...";

            await writer
                .WriteAsync(
                    activeProcess.Process,
                    address,
                    bytesToWrite,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            byte[] readBackBytes = new byte[bytesToWrite.Length];
            int readBackCount = await reader
                .ReadAsync(
                    activeProcess.Process,
                    address,
                    readBackBytes,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (readBackCount != readBackBytes.Length)
            {
                throw new InvalidOperationException(
                    $"Safe write read-back returned {readBackCount} bytes; expected {readBackBytes.Length}.");
            }

            bool verificationPassed = bytesToWrite.SequenceEqual(readBackBytes);
            MemoryWriteResultText = FormatMemoryWriteResult(
                address,
                bytesToWrite,
                bytesToWrite,
                readBackBytes,
                verificationPassed);

            if (verificationPassed)
            {
                MemoryWriteStatusText =
                    $"Safe write test PASS: wrote and verified {bytesToWrite.Length} unchanged bytes at 0x{address:X} in {FormatRegionName(region)}.";
            }
            else
            {
                MemoryWriteErrorText =
                    "The safe write command completed, but immediate read-back did not match the unchanged bytes that were written.";
                MemoryWriteStatusText = "Safe write test completed, but read-back verification failed.";
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            MemoryWriteStatusText = "Safe write test cancelled.";
        }
        catch (Exception exception)
        {
            MemoryWriteErrorText = exception.Message;
            MemoryWriteStatusText = "Safe write test failed.";
        }
        finally
        {
            IsWritingMemory = false;
            EndForegroundTargetOperation();
        }
    }

    private async Task<(MemoryRegion Region, ulong Address, byte[] Bytes)?> FindStableSafeWriteCandidateAsync(
        IMemoryReader reader,
        TargetProcess process,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MemoryRegion> candidates = ActiveMemoryRegions
            .Where(IsSafeWriteCandidateRegion)
            .OrderBy(region => region.Protection.HasFlag(MemoryProtection.Shared))
            .ThenByDescending(region => region.Protection.HasFlag(MemoryProtection.Private))
            .ThenBy(region => region.BaseAddress)
            .Take(MaximumSafeWriteCandidateRegions)
            .ToArray();

        foreach (MemoryRegion region in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong address = GetSafeWriteCandidateAddress(region);

            try
            {
                byte[] firstRead = new byte[SafeWriteTestLength];
                if (await reader
                        .ReadAsync(process, address, firstRead, cancellationToken)
                        .ConfigureAwait(true) != firstRead.Length)
                {
                    continue;
                }

                await Task
                    .Delay(SafeWriteStabilityDelayMilliseconds, cancellationToken)
                    .ConfigureAwait(true);

                byte[] secondRead = new byte[SafeWriteTestLength];
                if (await reader
                        .ReadAsync(process, address, secondRead, cancellationToken)
                        .ConfigureAwait(true) != secondRead.Length ||
                    !firstRead.SequenceEqual(secondRead))
                {
                    continue;
                }

                await Task
                    .Delay(SafeWriteStabilityDelayMilliseconds, cancellationToken)
                    .ConfigureAwait(true);

                byte[] thirdRead = new byte[SafeWriteTestLength];
                if (await reader
                        .ReadAsync(process, address, thirdRead, cancellationToken)
                        .ConfigureAwait(true) != thirdRead.Length ||
                    !secondRead.SequenceEqual(thirdRead))
                {
                    continue;
                }

                return (region, address, thirdRead);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // A candidate that cannot be read reliably is unsuitable for this diagnostic test.
                // Continue searching without changing the target.
            }
        }

        return null;
    }

    private static bool IsSafeWriteCandidateRegion(MemoryRegion region)
    {
        MemoryProtection protection = region.Protection;

        return region.Size >= SafeWriteTestLength &&
               protection.HasFlag(MemoryProtection.Read) &&
               protection.HasFlag(MemoryProtection.Write) &&
               !protection.HasFlag(MemoryProtection.Execute) &&
               !protection.HasFlag(MemoryProtection.Guard);
    }

    private static ulong GetSafeWriteCandidateAddress(MemoryRegion region)
    {
        ulong usableOffset = region.Size - SafeWriteTestLength;
        ulong centeredOffset = usableOffset / 2;
        ulong alignedOffset = centeredOffset & ~3UL;

        return checked(region.BaseAddress + alignedOffset);
    }

    private async Task FirstScanAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!TryPrepareScan(
                out ITargetSession? session,
                out TargetProcessViewModel? activeProcess,
                out IMemoryReader? reader,
                out TargetArchitecture? scanArchitecture,
                out MemoryScanOptions? scanOptions,
                out IReadOnlyList<MemoryScanValue>? inputValues))
        {
            return;
        }

        IMemoryValueType valueType = SelectedScanValueType!.ValueType;
        IMemoryScanType scanType = SelectedScanType!.ScanType;

        bool nativeScanMapped = SupportsNativeValueScanning &&
                                NativeScanTypeResolver.ShouldAttemptNativeScan(session!, scanType, valueType, MemoryScanStage.FirstScan);
        INativeValueScanStreamProvider? nativeStreamScanner = nativeScanMapped
            ? session!.GetService<INativeValueScanStreamProvider>()
            : null;
        INativeValueScanner? nativeScanner = nativeScanMapped
            ? session!.GetService<INativeValueScanner>()
            : null;
        ClearScanResultsForOperation();
        string storageWarning = BeginScanResultStorageSession(activeProcess!, valueType, scanType);
        bool useNativeScanner = _scanResultStorageSession is not null
            ? nativeStreamScanner is not null || nativeScanner is not null
            : nativeScanner is not null;

        bool waitingForSavedAddressIo = IsSavedAddressIoInProgress;
        if (waitingForSavedAddressIo)
        {
            ScanStatusText = "Waiting for the current Saved Address operation before starting First Scan...";
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            return;
        }

        try
        {
            BeginScanProgress(useNativeScanner);
            IsScanningMemory = true;
        }
        finally
        {
            EndForegroundTargetOperation();
        }

        string scanValueDescription = BuildScanValueDescription(scanType, inputValues!);
        ScanStatusText = useNativeScanner
            ? $"Scanning target natively for {scanValueDescription}..."
            : $"Scanning readable memory for {scanValueDescription}...";

        using CancellationTokenSource scanCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _scanCancellation = scanCancellation;
        RaiseScanCommandStates();

        IProcessControl? processControl = null;
        bool targetSuspended = false;

        try
        {
            processControl = await SuspendTargetForScanIfRequestedAsync(
                    session!,
                    activeProcess!,
                    scanCancellation.Token)
                .ConfigureAwait(true);
            targetSuspended = processControl is not null;

            MemoryScanExecutionResult result;
            if (_scanResultStorageSession is not null)
            {
                result = await ExecuteFirstScanToStorageAsync(
                        activeProcess!,
                        reader!,
                        scanArchitecture!,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions!,
                        nativeStreamScanner,
                        nativeScanner,
                        scanValueDescription,
                        scanCancellation)
                    .ConfigureAwait(true);
            }
            else
            {
                result = await ExecuteFirstScanInMemoryAsync(
                        activeProcess!,
                        reader!,
                        scanArchitecture!,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions!,
                        nativeScanner,
                        scanValueDescription,
                        scanCancellation.Token)
                    .ConfigureAwait(true);
            }

            ApplyScanResult(result, isFirstScan: true);
            if (!string.IsNullOrWhiteSpace(storageWarning))
            {
                ScanErrorText = storageWarning;
            }

            CompleteScanProgress();
        }
        catch (OperationCanceledException) when (scanCancellation.IsCancellationRequested)
        {
            CancelScanResultStorageSession();
            StopScanProgress();
            ScanStatusText = "Scan cancelled.";
        }
        catch (Exception exception)
        {
            FailScanResultStorageSession(exception.Message);
            StopScanProgress();
            ScanErrorText = exception.Message;
            ScanStatusText = "First Scan failed.";
        }
        finally
        {
            if (targetSuspended && processControl is not null)
            {
                await ResumeTargetAfterScanAsync(processControl, activeProcess!).ConfigureAwait(true);
            }

            _scanCancellation = null;
            IsScanningMemory = false;
            RaiseScanCommandStates();
        }
    }

    private async Task NextScanAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_hasScanSession)
        {
            ScanStatusText = "Run First Scan before Next Scan.";
            return;
        }

        if (!TryPrepareScan(
                out ITargetSession? session,
                out TargetProcessViewModel? activeProcess,
                out IMemoryReader? reader,
                out TargetArchitecture? scanArchitecture,
                out MemoryScanOptions? scanOptions,
                out IReadOnlyList<MemoryScanValue>? inputValues))
        {
            return;
        }

        IMemoryValueType valueType = SelectedScanValueType!.ValueType;
        IMemoryScanType scanType = SelectedScanType!.ScanType;

        bool nativeScanMapped = SupportsNativeValueScanning &&
                                NativeScanTypeResolver.ShouldAttemptNativeScan(session!, scanType, valueType, MemoryScanStage.NextScan);
        INativeValueScanStreamRefiner? nativeStreamRefiner = nativeScanMapped
            ? session!.GetService<INativeValueScanStreamRefiner>()
            : null;
        INativeValueScanRefiner? nativeRefiner = nativeScanMapped
            ? session!.GetService<INativeValueScanRefiner>()
            : null;
        bool hasResidentResults = _nativeResidentResultSet is not null && _scanResultStorageSession is not null;
        bool hasDiskBackedResults = _scanResultSet is not null && _scanResultStorageSession is not null;
        bool residentCanRefineNatively = hasResidentResults &&
            nativeStreamRefiner is not null &&
            _memoryScanner.CanRefineNativeResidentResultSet(
                _nativeResidentResultSet!,
                scanArchitecture!,
                valueType,
                scanType,
                inputValues!,
                scanOptions!);
        bool useNativeRefiner = hasResidentResults
            ? residentCanRefineNatively
            : hasDiskBackedResults
                ? nativeStreamRefiner is not null
                : nativeRefiner is not null;

        bool waitingForSavedAddressIo = IsSavedAddressIoInProgress;
        if (waitingForSavedAddressIo)
        {
            ScanStatusText = "Waiting for the current Saved Address operation before starting Next Scan...";
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            return;
        }

        try
        {
            BeginScanProgress(useNativeRefiner);
            IsScanningMemory = true;
        }
        finally
        {
            EndForegroundTargetOperation();
        }

        ScanErrorText = string.Empty;
        string scanValueDescription = BuildScanValueDescription(scanType, inputValues!);
        ScanStatusText = useNativeRefiner
            ? $"Refining {_scanResultCount:N0} candidates natively for {scanValueDescription}..."
            : $"Refining {_scanResultCount:N0} candidates for {scanValueDescription}...";

        using CancellationTokenSource scanCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _scanCancellation = scanCancellation;
        RaiseScanCommandStates();

        IProcessControl? processControl = null;
        bool targetSuspended = false;

        try
        {
            processControl = await SuspendTargetForScanIfRequestedAsync(
                    session!,
                    activeProcess!,
                    scanCancellation.Token)
                .ConfigureAwait(true);
            targetSuspended = processControl is not null;

            MemoryScanExecutionResult result;
            if (hasResidentResults)
            {
                result = await ExecuteNextScanFromResidentAsync(
                        activeProcess!,
                        reader!,
                        scanArchitecture!,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions!,
                        nativeStreamRefiner,
                        residentCanRefineNatively,
                        scanValueDescription,
                        scanCancellation)
                    .ConfigureAwait(true);
            }
            else if (hasDiskBackedResults)
            {
                result = await ExecuteNextScanFromStorageAsync(
                        activeProcess!,
                        reader!,
                        scanArchitecture!,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions!,
                        nativeStreamRefiner,
                        scanValueDescription,
                        scanCancellation)
                    .ConfigureAwait(true);
            }
            else
            {
                result = await ExecuteNextScanInMemoryAsync(
                        activeProcess!,
                        reader!,
                        scanArchitecture!,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions!,
                        nativeRefiner,
                        scanValueDescription,
                        scanCancellation.Token)
                    .ConfigureAwait(true);
            }

            ApplyScanResult(result, isFirstScan: false);
            CompleteScanProgress();
        }
        catch (OperationCanceledException) when (scanCancellation.IsCancellationRequested)
        {
            StopScanProgress();
            ScanStatusText = "Scan cancelled. Previous results were preserved.";
        }
        catch (Exception exception)
        {
            StopScanProgress();
            ScanErrorText = exception.Message;
            ScanStatusText = "Next Scan failed. Previous results were preserved.";
        }
        finally
        {
            if (targetSuspended && processControl is not null)
            {
                await ResumeTargetAfterScanAsync(processControl, activeProcess!).ConfigureAwait(true);
            }

            _scanCancellation = null;
            IsScanningMemory = false;
            RaiseScanCommandStates();
        }
    }

    private async Task<MemoryScanExecutionResult> ExecuteFirstScanToStorageAsync(
        TargetProcessViewModel activeProcess,
        IMemoryReader reader,
        TargetArchitecture scanArchitecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        INativeValueScanStreamProvider? nativeStreamScanner,
        INativeValueScanner? nativeScanner,
        string scanValueDescription,
        CancellationTokenSource scanCancellation)
    {
        IScanResultStorageSession storageSession = _scanResultStorageSession
            ?? throw new InvalidOperationException("The disk-backed scan-result session is not available.");
        MemoryScanShape shape = _memoryScanner.GetScanShape(valueType, inputValues, scanArchitecture, scanOptions);

        if (nativeStreamScanner is not null)
        {
            try
            {
                INativeValueScanResultStream nativeResults = await _memoryScanner
                    .StartFirstScanNativeStreamAsync(
                        activeProcess.Process,
                        ActiveMemoryRegions,
                        nativeStreamScanner,
                        scanArchitecture,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions,
                        scanCancellation.Token)
                    .ConfigureAwait(true);

                if (nativeResults is INativeValueScanResidentResultSet
                    { IsAuthoritative: true } residentResults &&
                    residentResults.Count > MaximumDisplayedScanResults)
                {
                    try
                    {
                        MemoryScanExecutionResult residentPreview = await _memoryScanner
                            .ReadNativeResidentPreviewAsync(
                                ActiveMemoryRegions,
                                residentResults,
                                scanArchitecture,
                                valueType,
                                MaximumDisplayedScanResults,
                                includePreviousValues: false,
                                scanCancellation.Token)
                            .ConfigureAwait(true);
                        _nativeResidentResultSet = residentResults;
                        ScanStatusText =
                            $"Native scan retained {residentResults.Count:N0} result(s) on the target; " +
                            $"loaded {residentPreview.Results.Count:N0} result(s) for display.";
                        return residentPreview;
                    }
                    catch
                    {
                        await residentResults.DisposeAsync().ConfigureAwait(true);
                        throw;
                    }
                }

                using IScanResultWriter writer = storageSession.CreateResultWriter(shape.ValueSize, shape.Alignment);
                MemoryScanExecutionResult result = await ConsumeNativeStreamAndCommitAsync(
                        nativeResults,
                        writer,
                        previousResults: null,
                        scanArchitecture,
                        valueType,
                        isFirstScan: true,
                        scanCancellation)
                    .ConfigureAwait(true);
                ActivateCommittedResultSet();
                return result;
            }
            catch (NotSupportedException) when (!scanCancellation.IsCancellationRequested)
            {
                IsScanProgressIndeterminate = false;
                ScanProgressPercentage = 0;
                ScanStatusText = $"Native acceleration unavailable for this scan; scanning readable memory for {scanValueDescription}...";
            }
        }
        else if (nativeScanner is not null)
        {
            try
            {
                MemoryScanExecutionResult result = await _memoryScanner
                    .FirstScanNativeAsync(
                        activeProcess.Process,
                        ActiveMemoryRegions,
                        nativeScanner,
                        scanArchitecture,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions,
                        scanCancellation.Token)
                    .ConfigureAwait(true);

                await PersistMaterializedResultsAsync(
                        result,
                        shape,
                        scanCancellation)
                    .ConfigureAwait(true);
                ActivateCommittedResultSet();
                return result;
            }
            catch (NotSupportedException) when (!scanCancellation.IsCancellationRequested)
            {
                IsScanProgressIndeterminate = false;
                ScanProgressPercentage = 0;
                ScanStatusText = $"Native acceleration unavailable for this scan; scanning readable memory for {scanValueDescription}...";
            }
        }

        using IScanResultWriter sharedWriter = storageSession.CreateResultWriter(shape.ValueSize, shape.Alignment);
        Progress<MemoryScanProgress> progress = new(UpdateFirstScanProgress);
        MemoryScanExecutionResult sharedResult = await _memoryScanner
            .FirstScanToStorageAsync(
                activeProcess.Process,
                ActiveMemoryRegions,
                reader,
                sharedWriter,
                scanArchitecture,
                valueType,
                scanType,
                inputValues,
                scanOptions,
                MaximumDisplayedScanResults,
                progress,
                scanCancellation.Token)
            .ConfigureAwait(true);
        await sharedWriter.CommitAsync(scanCancellation.Token).ConfigureAwait(true);
        ActivateCommittedResultSet();
        return sharedResult;
    }

    private async Task<MemoryScanExecutionResult> ExecuteFirstScanInMemoryAsync(
        TargetProcessViewModel activeProcess,
        IMemoryReader reader,
        TargetArchitecture scanArchitecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        INativeValueScanner? nativeScanner,
        string scanValueDescription,
        CancellationToken cancellationToken)
    {
        if (nativeScanner is not null)
        {
            try
            {
                return await _memoryScanner
                    .FirstScanNativeAsync(
                        activeProcess.Process,
                        ActiveMemoryRegions,
                        nativeScanner,
                        scanArchitecture,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions,
                        cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (NotSupportedException) when (!cancellationToken.IsCancellationRequested)
            {
                IsScanProgressIndeterminate = false;
                ScanProgressPercentage = 0;
                ScanStatusText = $"Native acceleration unavailable for this scan; scanning readable memory for {scanValueDescription}...";
            }
        }

        Progress<MemoryScanProgress> progress = new(UpdateFirstScanProgress);
        return await _memoryScanner
            .FirstScanAsync(
                activeProcess.Process,
                ActiveMemoryRegions,
                reader,
                scanArchitecture,
                valueType,
                scanType,
                inputValues,
                scanOptions,
                progress,
                cancellationToken)
            .ConfigureAwait(true);
    }

    private async Task<MemoryScanExecutionResult> ExecuteNextScanFromResidentAsync(
        TargetProcessViewModel activeProcess,
        IMemoryReader reader,
        TargetArchitecture scanArchitecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        INativeValueScanStreamRefiner? nativeStreamRefiner,
        bool canRefineNatively,
        string scanValueDescription,
        CancellationTokenSource scanCancellation)
    {
        IScanResultStorageSession storageSession = _scanResultStorageSession
            ?? throw new InvalidOperationException("The scan-result storage session is not available.");
        INativeValueScanResidentResultSet residentResults = _nativeResidentResultSet
            ?? throw new InvalidOperationException("The native resident result set is not available.");
        MemoryScanShape shape = _memoryScanner.GetScanShape(valueType, inputValues, scanArchitecture, scanOptions);

        if (canRefineNatively && nativeStreamRefiner is not null)
        {
            try
            {
                INativeValueScanResultStream nativeResults = await _memoryScanner
                    .StartNextScanNativeStreamAsync(
                        activeProcess.Process,
                        residentResults,
                        nativeStreamRefiner,
                        scanArchitecture,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions,
                        scanCancellation.Token)
                    .ConfigureAwait(true);

                if (nativeResults is INativeValueScanResidentResultSet
                    { IsAuthoritative: true } refinedResident &&
                    refinedResident.Count > MaximumDisplayedScanResults)
                {
                    try
                    {
                        MemoryScanExecutionResult residentPreview = await _memoryScanner
                            .ReadNativeResidentPreviewAsync(
                                ActiveMemoryRegions,
                                refinedResident,
                                scanArchitecture,
                                valueType,
                                MaximumDisplayedScanResults,
                                includePreviousValues: true,
                                scanCancellation.Token)
                            .ConfigureAwait(true);

                        _nativeResidentResultSet = refinedResident;
                        await residentResults.DisposeAsync().ConfigureAwait(true);
                        ScanStatusText =
                            $"Native refinement retained {refinedResident.Count:N0} result(s) on the target; " +
                            $"loaded {residentPreview.Results.Count:N0} result(s) for display.";
                        return residentPreview;
                    }
                    catch
                    {
                        await nativeResults.DisposeAsync().ConfigureAwait(true);
                        throw;
                    }
                }

                using IScanResultWriter writer = storageSession.CreateResultWriter(shape.ValueSize, shape.Alignment);
                MemoryScanExecutionResult materializedResult = await ConsumeNativeStreamAndCommitAsync(
                        nativeResults,
                        writer,
                        previousResults: null,
                        scanArchitecture,
                        valueType,
                        isFirstScan: false,
                        scanCancellation)
                    .ConfigureAwait(true);
                ActivateCommittedResultSet();
                _nativeResidentResultSet = null;
                await ResetNativeResidentSessionAfterMaterializationAsync(
                        nativeStreamRefiner,
                        residentResults)
                    .ConfigureAwait(true);
                return materializedResult;
            }
            catch (NotSupportedException) when (!scanCancellation.IsCancellationRequested)
            {
                IsScanProgressIndeterminate = false;
                ScanProgressPercentage = 0;
                ScanStatusText =
                    $"Native resident refinement unavailable; materializing {_scanResultCount:N0} candidates for shared refinement...";
            }
        }

        await MaterializeResidentResultsAsync(
                storageSession,
                residentResults,
                shape,
                nativeStreamRefiner,
                scanCancellation)
            .ConfigureAwait(true);

        ScanStatusText = $"Refining {_scanResultCount:N0} materialized candidates for {scanValueDescription}...";
        return await ExecuteNextScanFromStorageAsync(
                activeProcess,
                reader,
                scanArchitecture,
                valueType,
                scanType,
                inputValues,
                scanOptions,
                nativeStreamRefiner: null,
                scanValueDescription,
                scanCancellation)
            .ConfigureAwait(true);
    }

    private async Task MaterializeResidentResultsAsync(
        IScanResultStorageSession storageSession,
        INativeValueScanResidentResultSet residentResults,
        MemoryScanShape shape,
        INativeValueScanStreamRefiner? nativeStreamRefiner,
        CancellationTokenSource scanCancellation)
    {
        using IScanResultWriter writer = storageSession.CreateResultWriter(shape.ValueSize, shape.Alignment);

        async Task MaterializeAsync(
            IProgress<OperationProgress>? operationProgress,
            CancellationToken cancellationToken)
        {
            Progress<MemoryScanProgress> progress = new(scanProgress =>
            {
                UpdateNextScanProgress(scanProgress);
                if (operationProgress is null)
                {
                    return;
                }

                double fraction = scanProgress.UnitsTotal == 0
                    ? 1d
                    : Math.Clamp(
                        scanProgress.UnitsProcessed / (double)scanProgress.UnitsTotal,
                        0d,
                        1d);
                operationProgress.Report(new OperationProgress(
                    "Transferring resident scan results to local storage...",
                    fraction,
                    $"{scanProgress.ResultsFound:N0} result(s) stored • " +
                    $"{scanProgress.UnitsProcessed:N0} / {scanProgress.UnitsTotal:N0} received"));
            });

            await _memoryScanner
                .MaterializeNativeResidentResultSetAsync(
                    ActiveMemoryRegions,
                    residentResults,
                    writer,
                    progress,
                    cancellationToken)
                .ConfigureAwait(true);
            await writer.CommitAsync(cancellationToken).ConfigureAwait(true);
        }

        if (residentResults.Count > MaximumDisplayedScanResults &&
            _operationProgressDialogService is not null &&
            System.Windows.Application.Current?.MainWindow is Window owner)
        {
            await _operationProgressDialogService
                .RunAsync(
                    owner,
                    "Materializing Scan Results",
                    "Transferring resident scan results to local storage...",
                    allowCancellation: true,
                    async (operationProgress, dialogCancellation) =>
                    {
                        using CancellationTokenRegistration registration =
                            dialogCancellation.Register(scanCancellation.Cancel);
                        using CancellationTokenSource linkedCancellation =
                            CancellationTokenSource.CreateLinkedTokenSource(
                                scanCancellation.Token,
                                dialogCancellation);
                        await MaterializeAsync(operationProgress, linkedCancellation.Token).ConfigureAwait(true);
                    })
                .ConfigureAwait(true);
        }
        else
        {
            await MaterializeAsync(operationProgress: null, scanCancellation.Token).ConfigureAwait(true);
        }

        ActivateCommittedResultSet();
        _nativeResidentResultSet = null;
        await ResetNativeResidentSessionAfterMaterializationAsync(
                nativeStreamRefiner,
                residentResults)
            .ConfigureAwait(true);
    }

    private static async Task ResetNativeResidentSessionAfterMaterializationAsync(
        INativeValueScanStreamRefiner? nativeStreamRefiner,
        INativeValueScanResidentResultSet residentResults)
    {
        try
        {
            if (nativeStreamRefiner is not null)
            {
                await nativeStreamRefiner.ResetAsync(CancellationToken.None).ConfigureAwait(true);
            }
        }
        finally
        {
            await residentResults.DisposeAsync().ConfigureAwait(true);
        }
    }

    private async Task<MemoryScanExecutionResult> ExecuteNextScanFromStorageAsync(
        TargetProcessViewModel activeProcess,
        IMemoryReader reader,
        TargetArchitecture scanArchitecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        INativeValueScanStreamRefiner? nativeStreamRefiner,
        string scanValueDescription,
        CancellationTokenSource scanCancellation)
    {
        IScanResultStorageSession storageSession = _scanResultStorageSession
            ?? throw new InvalidOperationException("The disk-backed scan-result session is not available.");
        IScanResultSet previousResults = _scanResultSet
            ?? throw new InvalidOperationException("The committed disk-backed result set is not available.");
        MemoryScanShape shape = _memoryScanner.GetScanShape(valueType, inputValues, scanArchitecture, scanOptions);

        if (nativeStreamRefiner is not null)
        {
            try
            {
                INativeValueScanResultStream nativeResults = await _memoryScanner
                    .StartNextScanNativeStreamAsync(
                        activeProcess.Process,
                        previousResults,
                        nativeStreamRefiner,
                        scanArchitecture,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions,
                        scanCancellation.Token)
                    .ConfigureAwait(true);

                using IScanResultWriter writer = storageSession.CreateResultWriter(shape.ValueSize, shape.Alignment);
                MemoryScanExecutionResult result = await ConsumeNativeStreamAndCommitAsync(
                        nativeResults,
                        writer,
                        previousResults,
                        scanArchitecture,
                        valueType,
                        isFirstScan: false,
                        scanCancellation)
                    .ConfigureAwait(true);
                ActivateCommittedResultSet();
                return result;
            }
            catch (NotSupportedException) when (!scanCancellation.IsCancellationRequested)
            {
                await nativeStreamRefiner.ResetAsync(CancellationToken.None).ConfigureAwait(true);
                IsScanProgressIndeterminate = false;
                ScanProgressPercentage = 0;
                ScanStatusText = $"Native refinement unavailable; refining {_scanResultCount:N0} candidates for {scanValueDescription}...";
            }
        }

        using IScanResultWriter sharedWriter = storageSession.CreateResultWriter(shape.ValueSize, shape.Alignment);
        Progress<MemoryScanProgress> progress = new(UpdateNextScanProgress);
        MemoryScanExecutionResult sharedResult = await _memoryScanner
            .NextScanFromStorageAsync(
                activeProcess.Process,
                ActiveMemoryRegions,
                previousResults,
                reader,
                sharedWriter,
                scanArchitecture,
                valueType,
                scanType,
                inputValues,
                scanOptions,
                MaximumDisplayedScanResults,
                progress,
                scanCancellation.Token)
            .ConfigureAwait(true);
        await sharedWriter.CommitAsync(scanCancellation.Token).ConfigureAwait(true);
        ActivateCommittedResultSet();
        return sharedResult;
    }

    private async Task<MemoryScanExecutionResult> ExecuteNextScanInMemoryAsync(
        TargetProcessViewModel activeProcess,
        IMemoryReader reader,
        TargetArchitecture scanArchitecture,
        IMemoryValueType valueType,
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues,
        MemoryScanOptions scanOptions,
        INativeValueScanRefiner? nativeRefiner,
        string scanValueDescription,
        CancellationToken cancellationToken)
    {
        if (nativeRefiner is not null)
        {
            try
            {
                return await _memoryScanner
                    .NextScanNativeAsync(
                        activeProcess.Process,
                        ActiveMemoryRegions,
                        _scanCandidates,
                        nativeRefiner,
                        scanArchitecture,
                        valueType,
                        scanType,
                        inputValues,
                        scanOptions,
                        cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (NotSupportedException) when (!cancellationToken.IsCancellationRequested)
            {
                IsScanProgressIndeterminate = false;
                ScanProgressPercentage = 0;
                ScanStatusText = $"Native refinement unavailable; refining {_scanResultCount:N0} candidates for {scanValueDescription}...";
            }
        }

        Progress<MemoryScanProgress> progress = new(UpdateNextScanProgress);
        return await _memoryScanner
            .NextScanAsync(
                activeProcess.Process,
                ActiveMemoryRegions,
                _scanCandidates,
                reader,
                scanArchitecture,
                valueType,
                scanType,
                inputValues,
                scanOptions,
                progress,
                cancellationToken)
            .ConfigureAwait(true);
    }

    private async Task<MemoryScanExecutionResult> ConsumeNativeStreamAndCommitAsync(
        INativeValueScanResultStream nativeResults,
        IScanResultWriter writer,
        IScanResultSet? previousResults,
        TargetArchitecture scanArchitecture,
        IMemoryValueType valueType,
        bool isFirstScan,
        CancellationTokenSource scanCancellation)
    {
        await using (nativeResults)
        {
            Func<IProgress<MemoryScanProgress>?> createScanProgress = () =>
                new Progress<MemoryScanProgress>(isFirstScan ? UpdateFirstScanProgress : UpdateNextScanProgress);

            if (nativeResults.SourceResultCount > MaximumDisplayedScanResults &&
                _operationProgressDialogService is not null &&
                System.Windows.Application.Current?.MainWindow is Window owner)
            {
                return await _operationProgressDialogService
                    .RunAsync(
                        owner,
                        "Saving Scan Results",
                        "Writing scan results to disk...",
                        allowCancellation: true,
                        async (operationProgress, dialogCancellation) =>
                        {
                            using CancellationTokenRegistration registration =
                                dialogCancellation.Register(scanCancellation.Cancel);
                            using CancellationTokenSource linkedCancellation =
                                CancellationTokenSource.CreateLinkedTokenSource(
                                    scanCancellation.Token,
                                    dialogCancellation);

                            Progress<MemoryScanProgress> scanProgress = new(progress =>
                            {
                                if (isFirstScan)
                                {
                                    UpdateFirstScanProgress(progress);
                                }
                                else
                                {
                                    UpdateNextScanProgress(progress);
                                }

                                double fraction = progress.UnitsTotal == 0
                                    ? 1d
                                    : Math.Clamp(
                                        progress.UnitsProcessed / (double)progress.UnitsTotal,
                                        0d,
                                        1d);
                                operationProgress.Report(new OperationProgress(
                                    "Writing scan results to disk...",
                                    fraction,
                                    $"{progress.ResultsFound:N0} result(s) stored • {progress.UnitsProcessed:N0} / {progress.UnitsTotal:N0} received"));
                            });

                            MemoryScanExecutionResult result = await _memoryScanner
                                .ConsumeNativeScanStreamAsync(
                                    ActiveMemoryRegions,
                                    nativeResults,
                                    writer,
                                    scanArchitecture,
                                    valueType,
                                    MaximumDisplayedScanResults,
                                    previousResults,
                                    scanProgress,
                                    linkedCancellation.Token)
                                .ConfigureAwait(true);

                            operationProgress.Report(new OperationProgress(
                                "Finalizing scan results...",
                                1d,
                                $"{result.TotalResultCount:N0} result(s)"));
                            await writer.CommitAsync(linkedCancellation.Token).ConfigureAwait(true);
                            return result;
                        })
                    .ConfigureAwait(true);
            }

            IProgress<MemoryScanProgress>? progress = createScanProgress();
            MemoryScanExecutionResult directResult = await _memoryScanner
                .ConsumeNativeScanStreamAsync(
                    ActiveMemoryRegions,
                    nativeResults,
                    writer,
                    scanArchitecture,
                    valueType,
                    MaximumDisplayedScanResults,
                    previousResults,
                    progress,
                    scanCancellation.Token)
                .ConfigureAwait(true);
            await writer.CommitAsync(scanCancellation.Token).ConfigureAwait(true);
            return directResult;
        }
    }

    private async Task PersistMaterializedResultsAsync(
        MemoryScanExecutionResult result,
        MemoryScanShape shape,
        CancellationTokenSource scanCancellation)
    {
        IScanResultStorageSession storageSession = _scanResultStorageSession
            ?? throw new InvalidOperationException("The disk-backed scan-result session is not available.");
        if (result.TotalResultCount != result.Results.Count)
        {
            throw new InvalidOperationException(
                "A legacy materialized native scan cannot be persisted as a complete disk-backed result set because its total count differs from the materialized result count.");
        }

        MemoryScanResult[] orderedResults = result.Results
            .OrderBy(candidate => candidate.Address)
            .ToArray();
        using IScanResultWriter writer = storageSession.CreateResultWriter(shape.ValueSize, shape.Alignment);

        async Task PersistAsync(IProgress<OperationProgress>? operationProgress, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                for (int index = 0; index < orderedResults.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    MemoryScanResult candidate = orderedResults[index];
                    writer.Write(candidate.Address, candidate.CurrentValue.Bytes.Span);

                    if (operationProgress is not null &&
                        (index % 8192 == 0 || index + 1 == orderedResults.Length))
                    {
                        double fraction = orderedResults.Length == 0
                            ? 1d
                            : (index + 1) / (double)orderedResults.Length;
                        operationProgress.Report(new OperationProgress(
                            "Writing scan results to disk...",
                            fraction,
                            $"{index + 1:N0} / {orderedResults.Length:N0} results"));
                    }
                }
            }, cancellationToken).ConfigureAwait(true);

            await writer.CommitAsync(cancellationToken).ConfigureAwait(true);
        }

        if (result.Results.Count > MaximumDisplayedScanResults &&
            _operationProgressDialogService is not null &&
            System.Windows.Application.Current?.MainWindow is Window owner)
        {
            await _operationProgressDialogService
                .RunAsync(
                    owner,
                    "Saving Scan Results",
                    "Writing scan results to disk...",
                    allowCancellation: true,
                    async (operationProgress, dialogCancellation) =>
                    {
                        using CancellationTokenRegistration registration =
                            dialogCancellation.Register(scanCancellation.Cancel);
                        using CancellationTokenSource linkedCancellation =
                            CancellationTokenSource.CreateLinkedTokenSource(
                                scanCancellation.Token,
                                dialogCancellation);
                        await PersistAsync(operationProgress, linkedCancellation.Token).ConfigureAwait(true);
                    })
                .ConfigureAwait(true);
        }
        else
        {
            await PersistAsync(null, scanCancellation.Token).ConfigureAwait(true);
        }
    }

    private void ActivateCommittedResultSet()
    {
        IScanResultStorageSession storageSession = _scanResultStorageSession
            ?? throw new InvalidOperationException("The disk-backed scan-result session is not available.");

        IScanResultSet committed = storageSession.OpenResultSet();
        _scanResultSet?.Dispose();
        _scanResultSet = committed;
    }

    private async Task<IProcessControl?> SuspendTargetForScanIfRequestedAsync(
        ITargetSession session,
        TargetProcessViewModel activeProcess,
        CancellationToken cancellationToken)
    {
        if (!PauseTargetWhileScanning)
        {
            return null;
        }

        IProcessControl? processControl = session.GetService<IProcessControl>();
        if (processControl is null)
        {
            throw new NotSupportedException(
                "The plugin advertises process suspend/resume support but the active session does not provide IProcessControl.");
        }

        ScanStatusText = $"Pausing {activeProcess.DisplayName} before scan...";
        await processControl
            .SuspendAsync(activeProcess.Process, cancellationToken)
            .ConfigureAwait(true);

        return processControl;
    }

    private async Task ResumeTargetAfterScanAsync(
        IProcessControl processControl,
        TargetProcessViewModel activeProcess)
    {
        try
        {
            await processControl
                .ResumeAsync(activeProcess.Process, CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            string resumeError = $"Could not resume Active Target after scan: {exception.Message}";
            ScanErrorText = string.IsNullOrWhiteSpace(ScanErrorText)
                ? resumeError
                : $"{ScanErrorText} {resumeError}";
        }
    }

    private static string BuildScanValueDescription(
        IMemoryScanType scanType,
        IReadOnlyList<MemoryScanValue> inputValues)
    {
        return inputValues.Count switch
        {
            0 => scanType.DisplayName,
            1 => $"{scanType.DisplayName}: {inputValues[0].DisplayText}",
            2 => $"{scanType.DisplayName}: {inputValues[0].DisplayText} to {inputValues[1].DisplayText}",
            _ => scanType.DisplayName
        };
    }

    private bool TryPrepareScan(
        out ITargetSession? session,
        out TargetProcessViewModel? activeProcess,
        out IMemoryReader? reader,
        out TargetArchitecture? scanArchitecture,
        out MemoryScanOptions? scanOptions,
        [NotNullWhen(true)] out IReadOnlyList<MemoryScanValue>? inputValues)
    {
        session = _session;
        activeProcess = ActiveProcess;
        reader = null;
        scanArchitecture = null;
        scanOptions = null;
        inputValues = null;
        ScanErrorText = string.Empty;

        if (session is null || activeProcess is null || !IsConnected)
        {
            ScanStatusText = "Set an Active Target to begin scanning.";
            return false;
        }

        if (SelectedScanType is null || SelectedScanValueType is null)
        {
            ScanErrorText = "No compatible Core Scan Type and plugin Value Type combination is available.";
            ScanStatusText = "Scan unavailable.";
            return false;
        }

        if (!SelectedScanType.ScanType.SupportsValueType(SelectedScanValueType.ValueType))
        {
            ScanErrorText = $"{SelectedScanType.DisplayName} does not support the selected Value Type, {SelectedScanValueType.DisplayName}.";
            ScanStatusText = "Scan unavailable.";
            return false;
        }

        int requiredInputCount = SelectedScanType.ScanType.InputValueCount ?? 0;
        if (requiredInputCount is < 0 or > 2)
        {
            ScanErrorText = $"{SelectedScanType.DisplayName} requires an unsupported input layout.";
            ScanStatusText = "Scan unavailable.";
            return false;
        }

        scanOptions = BuildScanOptions();
        scanArchitecture = ResolveScanArchitecture(session.Architecture, scanOptions);

        List<MemoryScanValue> parsedInputs = new(requiredInputCount);
        if (requiredInputCount >= 1)
        {
            if (!SelectedScanValueType.ValueType.TryParse(
                    ScanValueText,
                    scanArchitecture,
                    out MemoryScanValue? primaryValue,
                    out string parseError) ||
                primaryValue is null)
            {
                ScanErrorText = parseError;
                ScanStatusText = "Scan not started.";
                return false;
            }

            parsedInputs.Add(primaryValue);
        }

        if (requiredInputCount >= 2)
        {
            if (!SelectedScanValueType.ValueType.TryParse(
                    ScanSecondaryValueText,
                    scanArchitecture,
                    out MemoryScanValue? secondaryValue,
                    out string parseError) ||
                secondaryValue is null)
            {
                ScanErrorText = parseError;
                ScanStatusText = "Scan not started.";
                return false;
            }

            parsedInputs.Add(secondaryValue);
        }

        if (!SelectedScanType.ScanType.TryValidateInputValues(
                SelectedScanValueType.ValueType,
                parsedInputs,
                scanArchitecture,
                _hasScanSession ? MemoryScanStage.NextScan : MemoryScanStage.FirstScan,
                out string inputValidationError))
        {
            ScanErrorText = inputValidationError;
            ScanStatusText = "Scan not started.";
            return false;
        }

        inputValues = parsedInputs;

        if (ActiveMemoryRegions.Count == 0)
        {
            ScanErrorText = "The Active Target does not currently have a loaded memory map.";
            ScanStatusText = "Scan not started.";
            return false;
        }

        reader = session.GetService<IMemoryReader>();
        if (reader is null)
        {
            ScanErrorText = "The plugin advertises memory read support but the active session does not provide IMemoryReader.";
            ScanStatusText = "Scan unavailable.";
            return false;
        }

        return true;
    }

    private void CopyScanResultText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            ScanErrorText = string.Empty;
        }
        catch (Exception exception)
        {
            ScanErrorText = $"Could not copy to the clipboard: {exception.Message}";
            ScanStatusText = "Clipboard copy failed.";
        }
    }

    private void PrepareMemoryWriteForScanResult(ScanResultViewModel scanResult)
    {
        ArgumentNullException.ThrowIfNull(scanResult);

        if (!string.Equals(scanResult.Result.ValueTypeId, StandardMemoryValueTypeIds.Int32, StringComparison.OrdinalIgnoreCase))
        {
            ScanErrorText = "Change value is currently available only for 4 Bytes (Signed) scan results because Raw Memory Write is still an Int32 diagnostic tool.";
            return;
        }

        MemoryWriteAddressText = $"0x{scanResult.Result.Address:X}";
        MemoryWriteValueText = string.Empty;
        MemoryWriteErrorText = string.Empty;
        MemoryWriteResultText = string.Empty;
        MemoryWriteStatusText =
            $"Address {MemoryWriteAddressText} loaded from Scan Results. Enter a decimal 4-byte value and choose Write + Verify.";
    }

    private void SaveScanResultToSavedAddresses(ScanResultViewModel scanResult)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        SaveScanResultsToSavedAddresses(new[] { scanResult });
    }

    internal void SaveScanResultsToSavedAddresses(IEnumerable<ScanResultViewModel> scanResults)
    {
        ArgumentNullException.ThrowIfNull(scanResults);

        if (ActiveProcess is not TargetProcessViewModel activeProcess)
        {
            ScanErrorText = "Scan Results can only be saved while their Active Target is still selected.";
            return;
        }

        ScanResultViewModel[] requestedResults = scanResults
            .Where(scanResult => scanResult is not null && ScanResults.Contains(scanResult))
            .Distinct()
            .ToArray();
        if (requestedResults.Length == 0)
        {
            return;
        }

        TargetArchitecture valueArchitecture = _session is null
            ? Metadata.Architecture
            : ResolveScanArchitecture(_session.Architecture, BuildScanOptions());

        int addedCount = 0;
        int existingCount = 0;
        SavedAddressViewModel? lastRelevantAddress = null;

        foreach (ScanResultViewModel scanResult in requestedResults)
        {
            SavedAddressViewModel? existing = SavedAddresses.FirstOrDefault(savedAddress =>
                savedAddress.MatchesSavedIdentity(activeProcess.Process, scanResult.Result));
            if (existing is not null)
            {
                existingCount++;
                lastRelevantAddress = existing;
                continue;
            }

            SavedAddressViewModel saved = new(
                scanResult.Result,
                activeProcess.Process,
                valueArchitecture,
                _scanValueTypes,
                CopySavedAddressText,
                RemoveSavedAddress,
                ToggleSavedAddressFrozenFromCommand);

            SavedAddresses.Add(saved);
            UpdateSavedAddressProtection(saved);
            lastRelevantAddress = saved;
            addedCount++;
        }

        if (lastRelevantAddress is not null)
        {
            SelectedSavedAddress = lastRelevantAddress;
        }

        if (addedCount > 0)
        {
            OnSavedAddressCollectionChanged();
        }

        if (requestedResults.Length == 1)
        {
            SavedAddressViewModel singleAddress = lastRelevantAddress!;
            SavedAddressStatusText = addedCount == 1
                ? $"Saved {singleAddress.AddressText} from Scan Results."
                : $"{singleAddress.AddressText} is already saved for {singleAddress.TargetProcessDisplayName}.";
        }
        else if (addedCount == 0)
        {
            SavedAddressStatusText = $"All {existingCount:N0} selected Scan Results are already saved.";
        }
        else if (existingCount == 0)
        {
            SavedAddressStatusText = $"Saved all {addedCount:N0} selected Scan Results.";
        }
        else
        {
            SavedAddressStatusText =
                $"Saved {addedCount:N0} selected Scan Result(s); {existingCount:N0} already saved.";
        }

        ScanErrorText = string.Empty;
        UpdateSavedAddressTimerState();
    }

    internal async Task AddManualSavedAddressAsync(
        ulong address,
        string description,
        ScanValueTypeViewModel valueType,
        int valueSize)
    {
        ArgumentNullException.ThrowIfNull(valueType);

        if (_session is not ITargetSession session ||
            ActiveProcess is not TargetProcessViewModel activeProcess ||
            !CanAddSavedAddressManually)
        {
            SavedAddressStatusText = "Connect, set an Active Target, and wait for the current target operation to finish before adding an address manually.";
            return;
        }

        ScanValueTypeViewModel? resolvedValueType = _scanValueTypes.FirstOrDefault(option =>
            string.Equals(option.Id, valueType.Id, StringComparison.OrdinalIgnoreCase));
        if (resolvedValueType is null)
        {
            SavedAddressStatusText = "The selected Value Type is no longer exposed by the active plugin.";
            return;
        }

        if (resolvedValueType.ValueType.FixedSize is int fixedSize)
        {
            valueSize = fixedSize;
        }
        else if (valueSize is < ManualSavedAddressEntryLimits.MinimumVariableLength or > ManualSavedAddressEntryLimits.MaximumVariableLength)
        {
            SavedAddressStatusText =
                $"Variable-length manual Saved Addresses must use between {ManualSavedAddressEntryLimits.MinimumVariableLength:N0} and {ManualSavedAddressEntryLimits.MaximumVariableLength:N0} bytes.";
            return;
        }

        SavedAddressViewModel? existing = SavedAddresses.FirstOrDefault(savedAddress =>
            savedAddress.MatchesSavedIdentity(activeProcess.Process, address, resolvedValueType.Id));
        if (existing is not null)
        {
            SelectedSavedAddress = existing;
            SavedAddressStatusText =
                $"0x{address:X} is already saved as {resolvedValueType.DisplayName} for {existing.TargetProcessDisplayName}.";
            return;
        }

        ITargetSession capturedSession = session;
        TargetProcess capturedTarget = activeProcess.Process;
        TargetArchitecture valueArchitecture = ResolveScanArchitecture(
            capturedSession.Architecture,
            BuildScanOptions());
        bool waitingForBackgroundIo = IsSavedAddressBackgroundIoInProgress;
        BeginSavedAddressUserOperation();
        try
        {
            if (waitingForBackgroundIo)
            {
                SavedAddressStatusText =
                    $"Waiting for the current Saved Address update before adding 0x{address:X}...";
            }

            await WaitForSavedAddressBackgroundIoIdleAsync().ConfigureAwait(true);

            if (_session is not ITargetSession currentSession ||
                !ReferenceEquals(capturedSession, currentSession) ||
                ActiveProcess is not TargetProcessViewModel currentActiveProcess ||
                !AreSameProcess(currentActiveProcess.Process, capturedTarget) ||
                !CanUseSavedAddressTargetForUserOperation())
            {
                SavedAddressStatusText = "The Active Target changed before the manual Saved Address could be added.";
                return;
            }

            existing = SavedAddresses.FirstOrDefault(savedAddress =>
                savedAddress.MatchesSavedIdentity(capturedTarget, address, resolvedValueType.Id));
            if (existing is not null)
            {
                SelectedSavedAddress = existing;
                SavedAddressStatusText =
                    $"0x{address:X} is already saved as {resolvedValueType.DisplayName} for {existing.TargetProcessDisplayName}.";
                return;
            }

            SavedAddressViewModel saved = new(
                address,
                description,
                capturedTarget,
                valueArchitecture,
                _scanValueTypes,
                resolvedValueType,
                valueSize,
                CopySavedAddressText,
                RemoveSavedAddress,
                ToggleSavedAddressFrozenFromCommand);

            SavedAddresses.Add(saved);
            UpdateSavedAddressProtection(saved);
            SelectedSavedAddress = saved;
            OnSavedAddressCollectionChanged();

            bool refreshed = await RefreshSavedAddressCoreAsync(saved).ConfigureAwait(true);
            if (refreshed)
            {
                SavedAddressStatusText =
                    $"Added {saved.AddressText} manually as {saved.SelectedValueType.DisplayName}.";
            }
            else
            {
                SavedAddressStatusText =
                    $"Added {saved.AddressText} manually; the current value could not be read yet.";
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            EndSavedAddressUserOperation();
            UpdateSavedAddressTimerState();
        }
    }

    private void CopySavedAddressText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            SavedAddressStatusText = "Copied to the clipboard.";
        }
        catch (Exception exception)
        {
            SavedAddressStatusText = $"Clipboard copy failed: {exception.Message}";
        }
    }

    private void ToggleSavedAddressFrozenFromCommand(SavedAddressViewModel savedAddress)
    {
        _ = SetSavedAddressFrozenAsync(savedAddress, !savedAddress.IsFrozenRequested);
    }

    internal async Task SetSavedAddressFrozenAsync(SavedAddressViewModel savedAddress, bool frozen)
    {
        ArgumentNullException.ThrowIfNull(savedAddress);
        long freezeRequestGeneration = savedAddress.RegisterFreezeRequest(frozen);

        if (!frozen)
        {
            savedAddress.SetFrozen(false);
            savedAddress.SetStatus("Freeze disabled.");
            SavedAddressStatusText = $"Freeze disabled for {savedAddress.AddressText}.";
            UpdateSavedAddressTimerState();
            return;
        }

        if (!TryGetSavedAddressTarget(savedAddress, out ITargetSession? session, out TargetProcessViewModel? activeProcess))
        {
            savedAddress.SetFrozen(false);
            return;
        }

        IMemoryWriter? writer = session.GetService<IMemoryWriter>();
        if (writer is null)
        {
            savedAddress.SetFrozen(false);
            savedAddress.SetStatus("The active target does not provide memory write support.");
            SavedAddressStatusText = $"Cannot freeze {savedAddress.AddressText}: memory write is unavailable.";
            return;
        }

        if (!CanBeginSavedAddressUserOperation() || !CanUseSavedAddressTargetForUserOperation())
        {
            savedAddress.SetFrozen(false);
            savedAddress.SetStatus("Wait for the current target operation to finish before enabling freeze.");
            return;
        }

        bool waitingForBackgroundIo = IsSavedAddressBackgroundIoInProgress;
        BeginSavedAddressUserOperation();
        try
        {
            if (waitingForBackgroundIo)
            {
                savedAddress.SetStatus("Freeze queued. Waiting for the current Saved Address update to finish.");
                SavedAddressStatusText = $"Freeze queued for {savedAddress.AddressText}.";
            }

            await WaitForSavedAddressBackgroundIoIdleAsync().ConfigureAwait(true);

            if (!SavedAddresses.Contains(savedAddress) ||
                _savedAddressesPendingRemoval.Contains(savedAddress) ||
                !savedAddress.IsFreezeRequestCurrent(freezeRequestGeneration, frozen))
            {
                savedAddress.SetFrozen(false);
                return;
            }

            SetSavedAddressRefreshInProgress(true);
            try
            {
                bool refreshed = await RefreshSavedAddressCoreAsync(savedAddress).ConfigureAwait(true);
                if (!SavedAddresses.Contains(savedAddress) ||
                    _savedAddressesPendingRemoval.Contains(savedAddress) ||
                    !savedAddress.IsFreezeRequestCurrent(freezeRequestGeneration, frozen))
                {
                    savedAddress.SetFrozen(false);
                    return;
                }

                if (!refreshed || !savedAddress.TryCaptureFrozenValue() ||
                    !savedAddress.TryGetFrozenValue(out ReadOnlyMemory<byte> frozenValue))
                {
                    savedAddress.SetFrozen(false);
                    savedAddress.SetStatus("A current readable value is required before this address can be frozen.");
                    SavedAddressStatusText = $"Could not freeze {savedAddress.AddressText}.";
                    return;
                }

                if (SupportsMemoryRegionEnumeration &&
                    ActiveMemoryRegions.Count > 0 &&
                    FindWritableRegion(savedAddress.Address, frozenValue.Length) is null)
                {
                    savedAddress.SetFrozen(false);
                    savedAddress.SetStatus("The saved range is not writable in the active target memory map.");
                    SavedAddressStatusText = $"Cannot freeze {savedAddress.AddressText}: the range is not writable.";
                    return;
                }

                await writer.WriteAsync(
                        activeProcess.Process,
                        savedAddress.Address,
                        frozenValue,
                        _lifetimeCancellation.Token)
                    .ConfigureAwait(true);

                if (!SavedAddresses.Contains(savedAddress) ||
                    _savedAddressesPendingRemoval.Contains(savedAddress) ||
                    !savedAddress.IsFreezeRequestCurrent(freezeRequestGeneration, frozen))
                {
                    savedAddress.SetFrozen(false);
                    return;
                }

                savedAddress.RecordTargetWriteSuccess(frozenValue.Span);
                savedAddress.SetFrozen(true);
                savedAddress.SetStatus($"Frozen. Reapplied every {_frozenWriteIntervalMilliseconds:N0} ms.");
                SavedAddressStatusText = $"Freeze enabled for {savedAddress.AddressText}.";
            }
            finally
            {
                SetSavedAddressRefreshInProgress(false);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            savedAddress.SetFrozen(false);
        }
        catch (Exception exception)
        {
            savedAddress.SetFrozen(false);
            savedAddress.SetStatus($"Freeze failed: {exception.Message}");
            SavedAddressStatusText = $"Freeze failed for {savedAddress.AddressText}.";
        }
        finally
        {
            EndSavedAddressUserOperation();
            UpdateSavedAddressTimerState();
        }
    }

    internal async Task CommitSavedAddressAddressAsync(SavedAddressViewModel savedAddress, string text)
    {
        ArgumentNullException.ThrowIfNull(savedAddress);

        if (!TryParseHexAddress(text, out ulong address))
        {
            savedAddress.SetStatus("Enter a hexadecimal address, with or without a 0x prefix.");
            SavedAddressStatusText = $"'{text}' is not a valid hexadecimal address.";
            savedAddress.RefreshAddressText();
            return;
        }

        if (!CanBeginSavedAddressUserOperation())
        {
            savedAddress.SetStatus("Wait for the current target operation to finish before changing the address.");
            savedAddress.RefreshAddressText();
            return;
        }

        bool waitingForBackgroundIo = IsSavedAddressBackgroundIoInProgress;
        BeginSavedAddressUserOperation();
        try
        {
            if (waitingForBackgroundIo)
            {
                savedAddress.SetStatus("Address change queued. Waiting for the current Saved Address update to finish.");
                SavedAddressStatusText = $"Address change queued for {savedAddress.AddressText}.";
            }

            await WaitForSavedAddressBackgroundIoIdleAsync().ConfigureAwait(true);

            if (!SavedAddresses.Contains(savedAddress) ||
                _savedAddressesPendingRemoval.Contains(savedAddress))
            {
                return;
            }

            if (savedAddress.IsFrozen)
            {
                savedAddress.SetFrozen(false);
            }

            savedAddress.SetAddress(address);
            UpdateSavedAddressProtection(savedAddress);
            savedAddress.RefreshAddressText();
            SavedAddressStatusText = $"Address changed to {savedAddress.AddressText}.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            savedAddress.RefreshAddressText();
        }
        finally
        {
            EndSavedAddressUserOperation();
        }

        await RefreshSavedAddressAsync(savedAddress).ConfigureAwait(true);
    }

    internal async Task CommitSavedAddressValueAsync(SavedAddressViewModel savedAddress, string text)
    {
        ArgumentNullException.ThrowIfNull(savedAddress);

        if (!TryGetSavedAddressTarget(savedAddress, out ITargetSession? session, out TargetProcessViewModel? activeProcess))
        {
            savedAddress.RefreshValueText();
            return;
        }

        if (!savedAddress.SelectedValueType.ValueType.TryParse(
                text,
                savedAddress.ValueArchitecture,
                out MemoryScanValue? parsedValue,
                out string error) ||
            parsedValue is null)
        {
            savedAddress.SetStatus(error);
            SavedAddressStatusText = $"Value change rejected for {savedAddress.AddressText}.";
            savedAddress.RefreshValueText();
            return;
        }

        if (SupportsMemoryRegionEnumeration &&
            ActiveMemoryRegions.Count > 0 &&
            FindWritableRegion(savedAddress.Address, parsedValue.Size) is null)
        {
            savedAddress.SetStatus("The saved range is not writable in the active target memory map.");
            SavedAddressStatusText = $"Cannot change {savedAddress.AddressText}: the range is not writable.";
            savedAddress.RefreshValueText();
            return;
        }

        TryGetConcurrentMemoryWriter(out IConcurrentMemoryWriter? concurrentWriter);
        IMemoryWriter? writer = concurrentWriter ?? session.GetService<IMemoryWriter>();
        if (writer is null)
        {
            savedAddress.SetStatus("The active target does not provide memory write support.");
            SavedAddressStatusText = $"Cannot change {savedAddress.AddressText}: memory write is unavailable.";
            savedAddress.RefreshValueText();
            return;
        }

        if (!CanBeginSavedAddressUserOperation() || !CanUseSavedAddressTargetForUserOperation())
        {
            savedAddress.SetStatus("Wait for the current target operation to finish before changing the value.");
            SavedAddressStatusText = $"Cannot write {savedAddress.AddressText} while another target operation is active.";
            savedAddress.RefreshValueText();
            return;
        }

        bool userWriteStarted = false;
        try
        {
            BeginSavedAddressUserOperation();
            userWriteStarted = true;

            if (concurrentWriter is null)
            {
                SavedAddressStatusText = $"Waiting for the current Saved Address update before writing {savedAddress.AddressText}...";
                await WaitForSavedAddressBackgroundIoIdleAsync().ConfigureAwait(true);

                if (!CanContinueSavedAddressPrimaryUserOperation())
                {
                    savedAddress.SetStatus("The target became busy before the value could be written.");
                    SavedAddressStatusText = $"Value write deferred for {savedAddress.AddressText}.";
                    savedAddress.RefreshValueText();
                    return;
                }
            }

            // For a Frozen row the edit is the user's new freeze target. Update the captured
            // payload before queuing the write so any later Frozen tick can only reapply the
            // newly requested value. The concurrent writer serializes an already in-flight
            // older Frozen write ahead of this manual write, leaving the user's value last.
            if (savedAddress.IsFrozen)
            {
                savedAddress.ReplaceFrozenValue(parsedValue.Bytes.Span);
            }

            await writer.WriteAsync(
                    activeProcess.Process,
                    savedAddress.Address,
                    parsedValue.Bytes,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            savedAddress.RecordTargetWriteSuccess(parsedValue.Bytes.Span);
            UpdateSavedAddressProtection(savedAddress);
            savedAddress.RefreshValueText();

            savedAddress.SetStatus(savedAddress.IsFrozen
                ? $"Value written and new frozen value captured. Reapplied every {_frozenWriteIntervalMilliseconds:N0} ms."
                : "Value written successfully.");
            SavedAddressStatusText = $"Wrote {parsedValue.DisplayText} to {savedAddress.AddressText}.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            savedAddress.SetStatus(savedAddress.IsFrozen
                ? $"Value write failed; Frozen will retry the new value: {exception.Message}"
                : $"Value write failed: {exception.Message}");
            SavedAddressStatusText = savedAddress.IsFrozen
                ? $"Value write failed for {savedAddress.AddressText}; Frozen retry scheduled."
                : $"Value write failed for {savedAddress.AddressText}.";
            savedAddress.RefreshValueText();
        }
        finally
        {
            if (userWriteStarted)
            {
                EndSavedAddressUserOperation();
            }
        }
    }

    internal async Task ChangeSavedAddressValueTypeAsync(
        SavedAddressViewModel savedAddress,
        ScanValueTypeViewModel valueType)
    {
        ArgumentNullException.ThrowIfNull(savedAddress);
        ArgumentNullException.ThrowIfNull(valueType);

        if (!savedAddress.ValueTypeOptions.Any(option =>
                string.Equals(option.Id, valueType.Id, StringComparison.OrdinalIgnoreCase)))
        {
            savedAddress.SetStatus("The selected Value Type is not exposed by the active plugin.");
            return;
        }

        if (!CanBeginSavedAddressUserOperation())
        {
            savedAddress.SetStatus("Wait for the current target operation to finish before changing the Value Type.");
            savedAddress.RefreshSelectedValueType();
            return;
        }

        bool waitingForBackgroundIo = IsSavedAddressBackgroundIoInProgress;
        BeginSavedAddressUserOperation();
        try
        {
            if (waitingForBackgroundIo)
            {
                savedAddress.SetStatus("Value Type change queued. Waiting for the current Saved Address update to finish.");
                SavedAddressStatusText = $"Value Type change queued for {savedAddress.AddressText}.";
            }

            await WaitForSavedAddressBackgroundIoIdleAsync().ConfigureAwait(true);

            if (!SavedAddresses.Contains(savedAddress) ||
                _savedAddressesPendingRemoval.Contains(savedAddress))
            {
                return;
            }

            savedAddress.SetValueType(valueType);
            UpdateSavedAddressProtection(savedAddress);
            SavedAddressStatusText = $"{savedAddress.AddressText} now uses {valueType.DisplayName}.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            savedAddress.RefreshSelectedValueType();
        }
        finally
        {
            EndSavedAddressUserOperation();
        }

        await RefreshSavedAddressAsync(savedAddress).ConfigureAwait(true);
    }

    public void SetSavedAddressUpdateInterval(int intervalMilliseconds)
    {
        if (intervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds));
        }

        _savedAddressUpdateIntervalMilliseconds = intervalMilliseconds;
        _savedAddressUpdateTimer.Interval = TimeSpan.FromMilliseconds(intervalMilliseconds);
        OnPropertyChanged(nameof(SavedAddressUpdateIntervalMilliseconds));
        OnPropertyChanged(nameof(SavedAddressUpdateIntervalText));
    }

    public void SetFrozenWriteInterval(int intervalMilliseconds)
    {
        if (intervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds));
        }

        _frozenWriteIntervalMilliseconds = intervalMilliseconds;
        _frozenWriteTimer.Interval = TimeSpan.FromMilliseconds(intervalMilliseconds);
        OnPropertyChanged(nameof(FrozenWriteIntervalMilliseconds));
        OnPropertyChanged(nameof(SavedAddressUpdateIntervalText));

        foreach (SavedAddressViewModel savedAddress in SavedAddresses.Where(item => item.IsFrozen))
        {
            savedAddress.SetStatus($"Frozen. Reapplied every {intervalMilliseconds:N0} ms.");
        }
    }

    private bool TryGetSavedAddressTarget(
        SavedAddressViewModel savedAddress,
        [NotNullWhen(true)] out ITargetSession? session,
        [NotNullWhen(true)] out TargetProcessViewModel? activeProcess)
    {
        session = _session;
        activeProcess = ActiveProcess;

        if (!IsConnected || session is null || activeProcess is null)
        {
            savedAddress.SetStatus("Connect and set the saved address's target process active to edit or freeze it.");
            SavedAddressStatusText = "Saved Address target is not active.";
            return false;
        }

        if (!savedAddress.MatchesTarget(activeProcess.Process))
        {
            savedAddress.SetStatus(
                $"Inactive for current target. Saved for {savedAddress.TargetProcessDisplayName} (0x{savedAddress.TargetProcessId:X}).");
            SavedAddressStatusText = $"{savedAddress.AddressText} belongs to a different Active Target.";
            return false;
        }

        return true;
    }

    private bool CanRunSavedAddressOperation()
    {
        return !_disposed &&
               IsConnected &&
               !_savedAddressTargetStateTransitionInProgress &&
               !_foregroundTargetOperationPending &&
               !IsConnecting &&
               !IsRefreshingProcesses &&
               !IsLoadingMemoryRegions &&
               !IsReadingMemory &&
               !IsWritingMemory &&
               !IsScanningMemory &&
               !IsSavedAddressIoInProgress &&
               ActiveProcess is not null;
    }

    private async void OnSavedAddressUpdateTimerTick(object? sender, EventArgs e)
    {
        if (!CanRunSavedAddressOperation() ||
            (SavedAddresses.Count == 0 && _visibleScanResults.Count == 0))
        {
            return;
        }

        SetSavedAddressRefreshInProgress(true);
        try
        {
            foreach (SavedAddressViewModel savedAddress in SavedAddresses.ToArray())
            {
                if (_lifetimeCancellation.IsCancellationRequested ||
                    _foregroundTargetOperationPending ||
                    _savedAddressUserOperationInProgress ||
                    _savedAddressesPendingRemoval.Count > 0)
                {
                    break;
                }

                if (!SavedAddresses.Contains(savedAddress))
                {
                    continue;
                }

                await RefreshSavedAddressCoreAsync(savedAddress).ConfigureAwait(true);
            }

            ScanResultViewModel[] visibleResults = _visibleScanResults.ToArray();
            foreach (ScanResultViewModel scanResult in visibleResults)
            {
                if (_lifetimeCancellation.IsCancellationRequested ||
                    _foregroundTargetOperationPending ||
                    _savedAddressUserOperationInProgress ||
                    IsScanningMemory)
                {
                    break;
                }

                if (!_visibleScanResults.Contains(scanResult))
                {
                    continue;
                }

                await RefreshVisibleScanResultCoreAsync(scanResult).ConfigureAwait(true);
            }
        }
        finally
        {
            SetSavedAddressRefreshInProgress(false);
        }
    }

    private async void OnFrozenWriteTimerTick(object? sender, EventArgs e)
    {
        if (!CanRunFrozenWriteCycle())
        {
            return;
        }

        SavedAddressViewModel[] frozenAddresses = SavedAddresses
            .Where(savedAddress => savedAddress.IsFrozen)
            .ToArray();
        if (frozenAddresses.Length == 0)
        {
            UpdateSavedAddressTimerState();
            return;
        }

        SetSavedAddressFreezeWriteInProgress(true);
        try
        {
            foreach (SavedAddressViewModel savedAddress in frozenAddresses)
            {
                if (_lifetimeCancellation.IsCancellationRequested ||
                    _foregroundTargetOperationPending ||
                    _savedAddressUserOperationInProgress ||
                    _savedAddressesPendingRemoval.Count > 0)
                {
                    break;
                }

                if (!SavedAddresses.Contains(savedAddress) || !savedAddress.IsFrozen)
                {
                    continue;
                }

                await ApplyFrozenValueCoreAsync(savedAddress).ConfigureAwait(true);
            }
        }
        finally
        {
            SetSavedAddressFreezeWriteInProgress(false);
            UpdateSavedAddressTimerState();
        }
    }

    private async Task<bool> RefreshSavedAddressAsync(SavedAddressViewModel savedAddress)
    {
        if (IsSavedAddressIoInProgress || !CanRunSavedAddressOperation())
        {
            return false;
        }

        SetSavedAddressRefreshInProgress(true);
        try
        {
            return await RefreshSavedAddressCoreAsync(savedAddress).ConfigureAwait(true);
        }
        finally
        {
            SetSavedAddressRefreshInProgress(false);
        }
    }

    private async Task<bool> RefreshSavedAddressCoreAsync(SavedAddressViewModel savedAddress)
    {
        if (_session is not ITargetSession session ||
            ActiveProcess is not TargetProcessViewModel activeProcess ||
            !IsConnected ||
            !savedAddress.MatchesTarget(activeProcess.Process))
        {
            savedAddress.SetStatus(
                $"Inactive for current target. Saved for {savedAddress.TargetProcessDisplayName} (0x{savedAddress.TargetProcessId:X}).");
            return false;
        }

        IMemoryReader? reader = session.GetService<IMemoryReader>();
        if (reader is null)
        {
            savedAddress.SetStatus("The active target does not provide memory read support.");
            return false;
        }

        int valueSize = savedAddress.ValueSize;
        if (valueSize <= 0)
        {
            savedAddress.SetStatus("The selected Value Type does not currently resolve to a readable size.");
            return false;
        }

        if (SupportsMemoryRegionEnumeration &&
            ActiveMemoryRegions.Count > 0 &&
            FindReadableRegion(savedAddress.Address, valueSize) is null)
        {
            savedAddress.SetStatus("The saved range is not readable in the active target memory map.");
            return false;
        }

        try
        {
            long targetWriteGeneration = savedAddress.TargetWriteGeneration;
            byte[] bytes = new byte[valueSize];
            int bytesRead = await reader.ReadAsync(
                    activeProcess.Process,
                    savedAddress.Address,
                    bytes,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (_savedAddressesPendingRemoval.Contains(savedAddress))
            {
                return false;
            }

            if (bytesRead != valueSize)
            {
                savedAddress.SetStatus($"Refresh returned {bytesRead} of {valueSize} expected byte(s).");
                return false;
            }

            MemoryScanValue currentValue = savedAddress.SelectedValueType.ValueType.CreateValue(
                bytes,
                savedAddress.Alignment,
                savedAddress.ValueArchitecture);

            if (savedAddress.TargetWriteGeneration != targetWriteGeneration)
            {
                // Any successful target write (manual or Frozen) that completed while this read
                // was in flight is newer than this read. Do not let the stale completion overwrite
                // the value that was just written and presented to the user.
                return true;
            }

            savedAddress.SetCurrentValue(currentValue);
            savedAddress.SetStatus(savedAddress.IsFrozen
                ? $"Frozen. Reapplied every {_frozenWriteIntervalMilliseconds:N0} ms."
                : string.Empty);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            savedAddress.SetStatus($"Refresh failed: {exception.Message}");
            return false;
        }

        return true;
    }

    private async Task<bool> RefreshVisibleScanResultCoreAsync(ScanResultViewModel scanResult)
    {
        if (_session is not ITargetSession session ||
            ActiveProcess is not TargetProcessViewModel activeProcess ||
            !IsConnected ||
            !_hasScanSession ||
            !_visibleScanResults.Contains(scanResult))
        {
            return false;
        }

        IMemoryReader? reader = session.GetService<IMemoryReader>();
        if (reader is null)
        {
            return false;
        }

        MemoryScanValue existingValue = scanResult.Result.CurrentValue;
        ScanValueTypeViewModel? valueTypeOption = _scanValueTypes.FirstOrDefault(option =>
            string.Equals(option.Id, existingValue.ValueTypeId, StringComparison.OrdinalIgnoreCase));
        if (valueTypeOption is null || existingValue.Size <= 0)
        {
            return false;
        }

        if (SupportsMemoryRegionEnumeration &&
            ActiveMemoryRegions.Count > 0 &&
            FindReadableRegion(scanResult.Result.Address, existingValue.Size) is null)
        {
            return false;
        }

        try
        {
            byte[] bytes = new byte[existingValue.Size];
            int bytesRead = await reader.ReadAsync(
                    activeProcess.Process,
                    scanResult.Result.Address,
                    bytes,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);
            if (bytesRead != bytes.Length ||
                !_visibleScanResults.Contains(scanResult))
            {
                return false;
            }

            TargetArchitecture architecture = ResolveScanArchitecture(
                session.Architecture,
                BuildScanOptions());
            MemoryScanValue currentValue = valueTypeOption.ValueType.CreateValue(
                bytes,
                existingValue.Alignment,
                architecture);
            scanResult.UpdateCurrentValue(currentValue);
            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool CanRunFrozenWriteCycle()
    {
        if (_disposed ||
            !IsConnected ||
            _savedAddressTargetStateTransitionInProgress ||
            _foregroundTargetOperationPending ||
            IsConnecting ||
            IsRefreshingProcesses ||
            IsLoadingMemoryRegions ||
            _savedAddressFreezeWriteInProgress ||
            _savedAddressUserOperationInProgress ||
            ActiveProcess is null)
        {
            return false;
        }

        if (IsScanningMemory && PauseTargetWhileScanning)
        {
            return false;
        }

        bool hasConcurrentWriter = TryGetConcurrentMemoryWriter(out _);
        if (IsScanningMemory)
        {
            return hasConcurrentWriter;
        }

        if (hasConcurrentWriter)
        {
            return true;
        }

        return !IsReadingMemory &&
               !IsWritingMemory &&
               !_savedAddressRefreshInProgress;
    }

    private bool TryGetConcurrentMemoryWriter(
        [NotNullWhen(true)] out IConcurrentMemoryWriter? writer)
    {
        writer = null;
        if (_session is not ITargetSession session)
        {
            return false;
        }

        try
        {
            writer = session.GetService<IConcurrentMemoryWriter>();
            return writer is not null;
        }
        catch (ObjectDisposedException)
        {
            writer = null;
            return false;
        }
    }

    private async Task<bool> ApplyFrozenValueCoreAsync(SavedAddressViewModel savedAddress)
    {
        if (_session is not ITargetSession session ||
            ActiveProcess is not TargetProcessViewModel activeProcess ||
            !IsConnected ||
            !savedAddress.IsFrozen ||
            !savedAddress.MatchesTarget(activeProcess.Process))
        {
            return false;
        }

        TryGetConcurrentMemoryWriter(out IConcurrentMemoryWriter? concurrentWriter);
        IMemoryWriter? writer = concurrentWriter;
        if (writer is null && !IsScanningMemory)
        {
            writer = session.GetService<IMemoryWriter>();
        }

        if (writer is null)
        {
            savedAddress.SetStatus(IsScanningMemory
                ? "Frozen write deferred because the active plugin does not expose a concurrent memory writer for use during scanning."
                : "Frozen write deferred because memory write is currently unavailable. The address remains Frozen and will retry.");
            return false;
        }

        if (!savedAddress.TryGetFrozenValue(out ReadOnlyMemory<byte> frozenValue))
        {
            savedAddress.SetFrozen(false);
            savedAddress.SetStatus("Freeze disabled because the frozen value is unavailable.");
            return false;
        }

        if (SupportsMemoryRegionEnumeration &&
            ActiveMemoryRegions.Count > 0 &&
            FindWritableRegion(savedAddress.Address, frozenValue.Length) is null)
        {
            savedAddress.SetStatus(
                "Frozen write deferred because the saved range is not writable in the current memory map. The address remains Frozen and will retry.");
            return false;
        }

        try
        {
            await writer.WriteAsync(
                    activeProcess.Process,
                    savedAddress.Address,
                    frozenValue,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (SavedAddresses.Contains(savedAddress) && savedAddress.IsFrozen)
            {
                savedAddress.RecordTargetWriteSuccess(frozenValue.Span);
                savedAddress.SetStatus($"Frozen. Reapplied every {_frozenWriteIntervalMilliseconds:N0} ms.");
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            // Keep the Frozen state after a transient write failure and retry on the next
            // configured interval. A single transport hiccup must not silently unfreeze
            // an address that the user explicitly chose to keep frozen.
            savedAddress.SetStatus(
                $"Frozen write failed and will retry in {_frozenWriteIntervalMilliseconds:N0} ms: {exception.Message}");
            SavedAddressStatusText = $"Frozen write failed for {savedAddress.AddressText}; retry scheduled.";
            return false;
        }

        return true;
    }

    private void DisableSavedAddressFreezesForDisconnect()
    {
        foreach (SavedAddressViewModel savedAddress in SavedAddresses.Where(item => item.IsFrozen))
        {
            savedAddress.SetFrozen(false);
            savedAddress.SetStatus("Freeze disabled because the target connection ended.");
        }
    }

    private void RefreshSavedAddressTargetStates()
    {
        TargetProcess? activeProcess = ActiveProcess?.Process;
        foreach (SavedAddressViewModel savedAddress in SavedAddresses)
        {
            UpdateSavedAddressProtection(savedAddress);
            if (activeProcess is null || !savedAddress.MatchesTarget(activeProcess))
            {
                savedAddress.SetStatus(
                    $"Inactive for current target. Saved for {savedAddress.TargetProcessDisplayName} (0x{savedAddress.TargetProcessId:X}).");
            }
        }
    }

    private void RefreshScanResultProtections()
    {
        if (ActiveMemoryRegions.Count == 0)
        {
            return;
        }

        foreach (ScanResultViewModel scanResult in ScanResults)
        {
            MemoryRegion? region = FindContainingRegion(
                scanResult.Result.Address,
                scanResult.Result.CurrentValue.Size);
            if (region is not null)
            {
                scanResult.UpdateProtection(region.Protection);
            }
        }
    }

    private void RefreshSavedAddressProtections()
    {
        foreach (SavedAddressViewModel savedAddress in SavedAddresses)
        {
            UpdateSavedAddressProtection(savedAddress);
        }
    }

    private void UpdateSavedAddressProtection(SavedAddressViewModel savedAddress)
    {
        ArgumentNullException.ThrowIfNull(savedAddress);

        if (!SupportsMemoryRegionEnumeration ||
            ActiveMemoryRegions.Count == 0 ||
            ActiveProcess is not TargetProcessViewModel activeProcess ||
            !savedAddress.MatchesTarget(activeProcess.Process) ||
            savedAddress.ValueSize <= 0)
        {
            savedAddress.SetProtection(null);
            return;
        }

        MemoryRegion? region = FindContainingRegion(
            savedAddress.Address,
            savedAddress.ValueSize);
        savedAddress.SetProtection(region?.Protection);
    }

    private void SetSavedAddressRefreshInProgress(bool value)
    {
        if (_savedAddressRefreshInProgress == value)
        {
            return;
        }

        bool wasBusy = IsSavedAddressBackgroundIoInProgress;
        _savedAddressRefreshInProgress = value;
        HandleSavedAddressBackgroundIoTransition(wasBusy);
    }

    private void SetSavedAddressFreezeWriteInProgress(bool value)
    {
        if (_savedAddressFreezeWriteInProgress == value)
        {
            return;
        }

        bool wasBusy = IsSavedAddressBackgroundIoInProgress;
        _savedAddressFreezeWriteInProgress = value;
        HandleSavedAddressBackgroundIoTransition(wasBusy);
    }

    private void HandleSavedAddressBackgroundIoTransition(bool wasBusy)
    {
        bool isBusy = IsSavedAddressBackgroundIoInProgress;
        if (!wasBusy && isBusy)
        {
            _savedAddressBackgroundIoIdleSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return;
        }

        if (wasBusy && !isBusy)
        {
            TaskCompletionSource<bool>? idleSource = _savedAddressBackgroundIoIdleSource;
            _savedAddressBackgroundIoIdleSource = null;
            idleSource?.TrySetResult(true);
            ProcessPendingSavedAddressRemovals();

            // Background ticks intentionally do not invalidate command state when they start;
            // doing so caused the entire UI to flash disabled/enabled at refresh cadence. They
            // must, however, invalidate once they return to idle. A scan command can finish while
            // a Frozen write is still in flight, otherwise leaving its cached CanExecute state
            // disabled until unrelated keyboard input forces WPF to query it again.
            RaiseSavedAddressDependentCommandStates();
        }
    }

    private async Task WaitForSavedAddressBackgroundIoIdleAsync()
    {
        while (IsSavedAddressBackgroundIoInProgress)
        {
            Task? idleTask = _savedAddressBackgroundIoIdleSource?.Task;
            if (idleTask is null)
            {
                await Task.Yield();
                continue;
            }

            await idleTask.WaitAsync(_lifetimeCancellation.Token).ConfigureAwait(true);
        }
    }

    private async Task WaitForSavedAddressIoIdleAsync()
    {
        while (IsSavedAddressIoInProgress)
        {
            if (IsSavedAddressBackgroundIoInProgress)
            {
                await WaitForSavedAddressBackgroundIoIdleAsync().ConfigureAwait(true);
                continue;
            }

            Task? idleTask = _savedAddressUserOperationIdleSource?.Task;
            if (idleTask is null)
            {
                await Task.Yield();
                continue;
            }

            await idleTask.WaitAsync(_lifetimeCancellation.Token).ConfigureAwait(true);
        }
    }

    private async Task<bool> TryBeginForegroundTargetOperationAsync()
    {
        if (_disposed || _foregroundTargetOperationPending)
        {
            return false;
        }

        _foregroundTargetOperationPending = true;
        OnPropertyChanged(nameof(CanExportScanResults));
        OnPropertyChanged(nameof(CanOpenDisassembler));
        OnPropertyChanged(nameof(CanOpenDebugger));
        UpdateSavedAddressTimerState();
        RaiseSavedAddressDependentCommandStates();
        RaiseScanCommandStates();

        try
        {
            await WaitForSavedAddressIoIdleAsync().ConfigureAwait(true);
            ProcessPendingSavedAddressRemovals();
            return !_disposed;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            EndForegroundTargetOperation();
            return false;
        }
    }

    private void EndForegroundTargetOperation()
    {
        if (!_foregroundTargetOperationPending)
        {
            return;
        }

        _foregroundTargetOperationPending = false;
        OnPropertyChanged(nameof(CanExportScanResults));
        OnPropertyChanged(nameof(CanOpenDisassembler));
        OnPropertyChanged(nameof(CanOpenDebugger));
        UpdateSavedAddressTimerState();
        RaiseSavedAddressDependentCommandStates();
        RaiseScanCommandStates();
    }

    private bool CanBeginSavedAddressUserOperation()
    {
        return !_disposed &&
               !_foregroundTargetOperationPending &&
               !_isExportingResidentScanResults &&
               !_savedAddressUserOperationInProgress;
    }

    private bool CanContinueSavedAddressPrimaryUserOperation()
    {
        return CanUseSavedAddressTargetForUserOperation() &&
               !IsReadingMemory &&
               !IsWritingMemory &&
               !IsSavedAddressBackgroundIoInProgress;
    }

    private bool CanUseSavedAddressTargetForUserOperation()
    {
        return !_disposed &&
               IsConnected &&
               !_savedAddressTargetStateTransitionInProgress &&
               !IsConnecting &&
               !IsRefreshingProcesses &&
               !IsLoadingMemoryRegions &&
               !IsScanningMemory &&
               !_isExportingResidentScanResults &&
               ActiveProcess is not null;
    }

    private void BeginSavedAddressUserOperation()
    {
        _savedAddressUserOperationIdleSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _savedAddressUserOperationInProgress = true;
        OnPropertyChanged(nameof(CanExportSavedAddresses));
        UpdateSavedAddressTimerState();
        RaiseSavedAddressDependentCommandStates();
    }

    private void EndSavedAddressUserOperation()
    {
        _savedAddressUserOperationInProgress = false;
        OnPropertyChanged(nameof(CanExportSavedAddresses));
        TaskCompletionSource<bool>? idleSource = _savedAddressUserOperationIdleSource;
        _savedAddressUserOperationIdleSource = null;
        ProcessPendingSavedAddressRemovals();
        UpdateSavedAddressTimerState();
        RaiseSavedAddressDependentCommandStates();
        idleSource?.TrySetResult(true);
    }

    private void RaiseSavedAddressDependentCommandStates()
    {
        RaiseConnectionCommandStates();
        RaiseProcessCommandStates();
        RaiseMemoryCommandStates();
    }

    private bool IsSavedAddressBackgroundIoInProgress =>
        _savedAddressRefreshInProgress || _savedAddressFreezeWriteInProgress;

    private bool IsSavedAddressIoInProgress =>
        IsSavedAddressBackgroundIoInProgress || _savedAddressUserOperationInProgress;

    private void UpdateSavedAddressTimerState()
    {
        bool canUseTarget = !_disposed &&
                            !_savedAddressTargetStateTransitionInProgress &&
                            !_foregroundTargetOperationPending &&
                            IsConnected &&
                            ActiveProcess is not null;
        bool hasRefreshTargets = SavedAddresses.Count > 0 || _visibleScanResults.Count > 0;

        bool shouldRefresh = canUseTarget &&
                             hasRefreshTargets &&
                             !IsScanningMemory &&
                             !_isExportingResidentScanResults &&
                             !_savedAddressUserOperationInProgress &&
                             _savedAddressesPendingRemoval.Count == 0;
        SetTimerState(_savedAddressUpdateTimer, shouldRefresh);

        bool hasFrozenAddresses = SavedAddresses.Any(savedAddress => savedAddress.IsFrozen);
        bool canWriteFrozenDuringScan =
            !IsScanningMemory ||
            (!PauseTargetWhileScanning && TryGetConcurrentMemoryWriter(out _));
        bool canWriteFrozenDuringResidentExport =
            !_isExportingResidentScanResults || TryGetConcurrentMemoryWriter(out _);
        bool shouldWriteFrozen = canUseTarget &&
                                 hasFrozenAddresses &&
                                 canWriteFrozenDuringScan &&
                                 canWriteFrozenDuringResidentExport &&
                                 !_savedAddressUserOperationInProgress &&
                                 _savedAddressesPendingRemoval.Count == 0;
        SetTimerState(_frozenWriteTimer, shouldWriteFrozen);
    }

    private static void SetTimerState(DispatcherTimer timer, bool shouldRun)
    {
        if (shouldRun)
        {
            if (!timer.IsEnabled)
            {
                timer.Start();
            }
        }
        else if (timer.IsEnabled)
        {
            timer.Stop();
        }
    }

    private void SetSavedAddressTargetStateTransition(bool value)
    {
        if (_savedAddressTargetStateTransitionInProgress == value)
        {
            return;
        }

        _savedAddressTargetStateTransitionInProgress = value;
        UpdateSavedAddressTimerState();
    }


    private void RemoveSavedAddress(SavedAddressViewModel savedAddress)
    {
        ArgumentNullException.ThrowIfNull(savedAddress);

        if (!SavedAddresses.Contains(savedAddress))
        {
            _savedAddressesPendingRemoval.Remove(savedAddress);
            return;
        }

        if (IsSavedAddressIoInProgress)
        {
            savedAddress.SetFrozen(false);
            _savedAddressesPendingRemoval.Add(savedAddress);
            savedAddress.SetStatus("Removal queued. Waiting for the current Saved Address operation to finish.");
            SavedAddressStatusText = $"Removal queued for {savedAddress.AddressText}.";
            UpdateSavedAddressTimerState();
            return;
        }

        RemoveSavedAddressCore(savedAddress);
    }

    private void ProcessPendingSavedAddressRemovals()
    {
        if (IsSavedAddressIoInProgress || _savedAddressesPendingRemoval.Count == 0)
        {
            return;
        }

        SavedAddressViewModel[] pending = _savedAddressesPendingRemoval
            .Where(SavedAddresses.Contains)
            .ToArray();
        _savedAddressesPendingRemoval.Clear();

        if (pending.Length == 0)
        {
            return;
        }

        bool selectedAddressRemoved = pending.Any(savedAddress =>
            ReferenceEquals(SelectedSavedAddress, savedAddress));
        int removedCount = 0;
        string? removedAddressText = null;

        foreach (SavedAddressViewModel savedAddress in pending)
        {
            savedAddress.SetFrozen(false);
            if (SavedAddresses.Remove(savedAddress))
            {
                removedCount++;
                removedAddressText ??= savedAddress.AddressText;
            }
        }

        if (removedCount == 0)
        {
            return;
        }

        if (selectedAddressRemoved)
        {
            SelectedSavedAddress = SavedAddresses.FirstOrDefault();
        }

        OnSavedAddressCollectionChanged();
        SavedAddressStatusText = removedCount == 1
            ? $"Removed {removedAddressText}."
            : $"Removed {removedCount:N0} queued saved address(es).";
        UpdateSavedAddressTimerState();
    }

    private void RemoveSavedAddressCore(SavedAddressViewModel savedAddress)
    {
        _savedAddressesPendingRemoval.Remove(savedAddress);
        savedAddress.SetFrozen(false);

        if (!SavedAddresses.Remove(savedAddress))
        {
            return;
        }

        if (ReferenceEquals(SelectedSavedAddress, savedAddress))
        {
            SelectedSavedAddress = SavedAddresses.FirstOrDefault();
        }

        OnSavedAddressCollectionChanged();
        SavedAddressStatusText = $"Removed {savedAddress.AddressText}.";
        UpdateSavedAddressTimerState();
    }

    internal void RemoveAllSavedAddresses()
    {
        if (SavedAddresses.Count == 0)
        {
            return;
        }

        if (IsSavedAddressIoInProgress)
        {
            SavedAddressViewModel[] addressesToRemove = SavedAddresses.ToArray();
            foreach (SavedAddressViewModel savedAddress in addressesToRemove)
            {
                savedAddress.SetFrozen(false);
                _savedAddressesPendingRemoval.Add(savedAddress);
            }

            SavedAddressStatusText =
                $"Removal queued for all {addressesToRemove.Length:N0} current saved address(es).";
            UpdateSavedAddressTimerState();
            return;
        }

        int removedCount = SavedAddresses.Count;
        _savedAddressesPendingRemoval.Clear();
        foreach (SavedAddressViewModel savedAddress in SavedAddresses)
        {
            savedAddress.SetFrozen(false);
        }

        SavedAddresses.Clear();
        SelectedSavedAddress = null;
        OnSavedAddressCollectionChanged();
        SavedAddressStatusText = $"Removed all {removedCount:N0} saved address(es).";
        UpdateSavedAddressTimerState();
    }

    private void OnSavedAddressCollectionChanged()
    {
        OnPropertyChanged(nameof(HasSavedAddresses));
        OnPropertyChanged(nameof(CanExportSavedAddresses));
        OnPropertyChanged(nameof(SavedAddressCountText));
    }

    internal IReadOnlyList<ExportScopeOption> CreateScanResultExportScopes(
        IEnumerable<ScanResultViewModel> selectedRows)
    {
        ArgumentNullException.ThrowIfNull(selectedRows);
        if (_scanResultCount <= 0 || ScanResults.Count == 0)
        {
            return Array.Empty<ExportScopeOption>();
        }

        RefreshScanResultProtections();

        MemoryScanResultExportSource allSource = CreateCompleteScanResultExportSource(
            CreateExportMetadata("all", _scanResultCount, includeScanMetadata: true));
        MemoryScanResult[] displayedResults = ScanResults
            .Select(result => result.Result)
            .ToArray();
        MemoryScanResultExportSource displayedSource = MemoryScanResultExportSource.FromMaterialized(
            displayedResults,
            CreateExportMetadata("displayed", displayedResults.LongLength, includeScanMetadata: true),
            ActiveMemoryRegions);

        List<ExportScopeOption> scopes = new()
        {
            new ExportScopeOption(
                "all",
                $"All Results ({_scanResultCount:N0})",
                allSource.Columns.Any(column =>
                    string.Equals(column.Id, MemoryScanResultExportColumnIds.Previous, StringComparison.OrdinalIgnoreCase))
                    ? "Exports the complete result set, including every currently available Scan Results column."
                    : "Exports the complete result set. Complete disk-backed or backend-resident results guarantee Address, Value, Type, and Protection; Previous and Region / Module remain presentation data unless the complete set is materialized.",
                allSource),
            new ExportScopeOption(
                "displayed",
                $"Displayed Results ({displayedResults.Length:N0})",
                $"Exports the rows currently loaded for presentation. Large scans are limited to the {MaximumDisplayedScanResults:N0}-row display preview; this scope does not represent the complete result set when the total count is larger.",
                displayedSource)
        };

        ScanResultViewModel[] selected = selectedRows
            .Where(row => row is not null && ScanResults.Contains(row))
            .Distinct()
            .ToArray();
        if (selected.Length > 0)
        {
            MemoryScanResult[] selectedResults = selected
                .Select(result => result.Result)
                .ToArray();
            scopes.Add(new ExportScopeOption(
                "selected",
                $"Selected Results ({selectedResults.Length:N0})",
                "Exports only the selected Scan Results rows, including Protection and their currently presented Previous and Region / Module data when available.",
                MemoryScanResultExportSource.FromMaterialized(
                    selectedResults,
                    CreateExportMetadata("selected", selectedResults.LongLength, includeScanMetadata: true),
                    ActiveMemoryRegions)));
        }

        return scopes;
    }

    internal IReadOnlyList<ExportScopeOption> CreateSavedAddressExportScopes(
        IEnumerable<SavedAddressViewModel> selectedRows)
    {
        ArgumentNullException.ThrowIfNull(selectedRows);
        if (SavedAddresses.Count == 0)
        {
            return Array.Empty<ExportScopeOption>();
        }

        SavedAddressViewModel[] allRows = SavedAddresses.ToArray();
        List<ExportScopeOption> scopes = new()
        {
            new ExportScopeOption(
                "all",
                $"All Addresses ({allRows.Length:N0})",
                "Exports a stable snapshot of every Saved Address in the current plugin workspace.",
                new SavedAddressExportDataSource(
                    allRows,
                    CreateExportMetadata("all", allRows.LongLength, includeScanMetadata: false)))
        };

        SavedAddressViewModel[] selected = selectedRows
            .Where(row => row is not null && SavedAddresses.Contains(row))
            .Distinct()
            .ToArray();
        if (selected.Length > 0)
        {
            scopes.Add(new ExportScopeOption(
                "selected",
                $"Selected Addresses ({selected.Length:N0})",
                "Exports a stable snapshot of only the selected Saved Addresses.",
                new SavedAddressExportDataSource(
                    selected,
                    CreateExportMetadata("selected", selected.LongLength, includeScanMetadata: false))));
        }

        return scopes;
    }

    internal async Task ExportDataAsync(
        Window owner,
        DataExportDialogResult selection,
        string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        IExportDataSource source = selection.Scope.Source;
        bool scanExport = string.Equals(source.Type, "scan-results", StringComparison.Ordinal);
        bool residentExport = source is MemoryScanResultExportSource { UsesNativeResidentSource: true };
        bool residentExportStarted = false;

        if (residentExport)
        {
            if (IsSavedAddressIoInProgress)
            {
                ScanStatusText = "Waiting for the current Saved Address operation before exporting resident Scan Results...";
            }

            if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
            {
                return;
            }

            try
            {
                _isExportingResidentScanResults = true;
                OnPropertyChanged(nameof(CanExportScanResults));
                OnPropertyChanged(nameof(CanOpenDisassembler));
                residentExportStarted = true;
                UpdateSavedAddressTimerState();
                RaiseSavedAddressDependentCommandStates();
                RaiseScanCommandStates();
            }
            finally
            {
                EndForegroundTargetOperation();
            }
        }

        if (scanExport)
        {
            ScanErrorText = string.Empty;
            ScanStatusText = $"Exporting {source.Count:N0} Scan Result(s)...";
        }
        else
        {
            SavedAddressStatusText = $"Exporting {source.Count:N0} saved address(es)...";
        }

        try
        {
            string title = scanExport ? "Exporting Scan Results" : "Exporting Saved Addresses";
            string initialStatus = scanExport
                ? "Writing Scan Results export..."
                : "Writing Saved Addresses export...";

            TabularExportResult result;
            if (_operationProgressDialogService is not null)
            {
                result = await _operationProgressDialogService
                    .RunAsync(
                        owner,
                        title,
                        initialStatus,
                        allowCancellation: true,
                        (progress, cancellationToken) => _tabularExportService.ExportAsync(
                            destinationPath,
                            selection.Format,
                            source,
                            selection.ColumnIds,
                            progress,
                            cancellationToken))
                    .ConfigureAwait(true);
            }
            else
            {
                result = await _tabularExportService
                    .ExportAsync(
                        destinationPath,
                        selection.Format,
                        source,
                        selection.ColumnIds,
                        progress: null,
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }

            string fileName = Path.GetFileName(destinationPath);
            if (scanExport)
            {
                ScanStatusText = $"Export complete: {result.RowsWritten:N0} Scan Result(s) written to {fileName}.";
            }
            else
            {
                SavedAddressStatusText = $"Export complete: {result.RowsWritten:N0} saved address(es) written to {fileName}.";
            }
        }
        catch (OperationCanceledException)
        {
            if (scanExport)
            {
                ScanStatusText = "Scan Results export cancelled. No partial export was published.";
            }
            else
            {
                SavedAddressStatusText = "Saved Addresses export cancelled. No partial export was published.";
            }
        }
        catch (Exception exception)
        {
            if (scanExport)
            {
                ScanErrorText = exception.Message;
                ScanStatusText = "Scan Results export failed. No partial export was published.";
            }
            else
            {
                SavedAddressStatusText = $"Saved Addresses export failed: {exception.Message}";
            }
        }
        finally
        {
            if (residentExportStarted)
            {
                _isExportingResidentScanResults = false;
                OnPropertyChanged(nameof(CanExportScanResults));
                OnPropertyChanged(nameof(CanOpenDisassembler));
                UpdateSavedAddressTimerState();
                RaiseSavedAddressDependentCommandStates();
                RaiseScanCommandStates();
            }
        }
    }

    private MemoryScanResultExportSource CreateCompleteScanResultExportSource(
        IReadOnlyDictionary<string, ExportCellValue> metadata)
    {
        if (_scanCandidates.Count == _scanResultCount)
        {
            return MemoryScanResultExportSource.FromMaterialized(
                _scanCandidates.ToArray(),
                metadata,
                ActiveMemoryRegions);
        }

        IMemoryValueType valueType = SelectedScanValueType?.ValueType
            ?? throw new InvalidOperationException("The active Scan Results Value Type is unavailable.");

        if (_scanResultSet is not null)
        {
            return MemoryScanResultExportSource.FromDiskBacked(
                _scanResultSet,
                valueType,
                Metadata.Architecture,
                ActiveMemoryRegions,
                metadata);
        }

        if (_nativeResidentResultSet is not null)
        {
            return MemoryScanResultExportSource.FromNativeResident(
                _nativeResidentResultSet,
                valueType,
                Metadata.Architecture,
                ActiveMemoryRegions,
                metadata);
        }

        throw new InvalidOperationException(
            "The complete Scan Results set is unavailable for export. Use Displayed Results instead.");
    }

    private IReadOnlyDictionary<string, ExportCellValue> CreateExportMetadata(
        string scope,
        long rowCount,
        bool includeScanMetadata)
    {
        Dictionary<string, ExportCellValue> metadata = new(StringComparer.Ordinal)
        {
            ["application"] = ExportCellValue.FromString(AppInfo.Title),
            ["applicationVersion"] = ExportCellValue.FromString(AppInfo.DisplayVersion),
            ["scope"] = ExportCellValue.FromString(scope),
            ["pluginId"] = ExportCellValue.FromString(Metadata.Id),
            ["pluginName"] = ExportCellValue.FromString(Metadata.Name),
            ["pluginVersion"] = ExportCellValue.FromString(Metadata.DisplayVersion),
            ["platform"] = ExportCellValue.FromString(Metadata.Platform),
            ["backend"] = ExportCellValue.FromString(Metadata.Backend),
            ["sourceRowCount"] = ExportCellValue.FromInt64(rowCount)
        };

        if (includeScanMetadata)
        {
            metadata["totalScanResultCount"] = ExportCellValue.FromInt64(_scanResultCount);
            if (ActiveProcess is not null)
            {
                metadata["targetProcessId"] = ExportCellValue.FromUInt64(ActiveProcess.Process.Id);
                metadata["targetProcessName"] = ExportCellValue.FromString(ActiveProcess.DisplayName);
            }

            if (SelectedScanValueType is not null)
            {
                metadata["valueTypeId"] = ExportCellValue.FromString(SelectedScanValueType.Id);
                metadata["valueType"] = ExportCellValue.FromString(SelectedScanValueType.DisplayName);
            }

            if (SelectedScanType is not null)
            {
                metadata["selectedScanTypeId"] = ExportCellValue.FromString(SelectedScanType.Id);
                metadata["selectedScanType"] = ExportCellValue.FromString(SelectedScanType.DisplayName);
            }
        }

        return metadata;
    }

    private void ApplyScanResult(MemoryScanExecutionResult result, bool isFirstScan)
    {
        _scanCandidates = result.Results;
        _scanResultCount = result.TotalResultCount;
        SetHasScanSession(true);
        ScanResults = _scanCandidates
            .Take(MaximumDisplayedScanResults)
            .Select(candidate => new ScanResultViewModel(
                candidate,
                CopyScanResultText,
                PrepareMemoryWriteForScanResult,
                SaveScanResultToSavedAddresses,
                () => SupportsMemoryWrite && string.Equals(candidate.ValueTypeId, StandardMemoryValueTypeIds.Int32, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        OnPropertyChanged(nameof(ScanResultCountText));
        OnPropertyChanged(nameof(ScanEmptyMessage));
        OnPropertyChanged(nameof(ScanResultFooterText));

        string operation = isFirstScan ? "First Scan" : "Next Scan";
        string failureText = result.ReadFailureCount == 0
            ? string.Empty
            : $" {result.ReadFailureCount:N0} memory read request(s) failed and were skipped.";

        ScanStatusText = $"{operation} complete: {_scanResultCount:N0} result(s).{failureText}";
        RaiseScanCommandStates();
    }

    private void UpdateFirstScanProgress(MemoryScanProgress progress)
    {
        UpdateScanProgress(progress);
        string percentage = FormatProgressPercentage(progress.UnitsProcessed, progress.UnitsTotal);
        ScanStatusText = $"First Scan {percentage}: {progress.ResultsFound:N0} result(s) found.";
    }

    private void UpdateNextScanProgress(MemoryScanProgress progress)
    {
        UpdateScanProgress(progress);
        string percentage = FormatProgressPercentage(progress.UnitsProcessed, progress.UnitsTotal);
        ScanStatusText = $"Next Scan {percentage}: {progress.ResultsFound:N0} result(s) remain.";
    }

    private async Task SubmitScanAsync()
    {
        if (_hasScanSession)
        {
            await NextScanAsync().ConfigureAwait(true);
        }
        else
        {
            await FirstScanAsync().ConfigureAwait(true);
        }
    }

    private async Task NewScanAsync()
    {
        bool waitingForSavedAddressIo = IsSavedAddressIoInProgress;
        if (waitingForSavedAddressIo)
        {
            ScanStatusText = "Waiting for the current Saved Address operation before starting a new scan session...";
        }

        if (!await TryBeginForegroundTargetOperationAsync().ConfigureAwait(true))
        {
            return;
        }

        Exception? resetError = null;
        SetSavedAddressTargetStateTransition(true);
        try
        {
            try
            {
                await ResetNativeScanSessionIfAvailableAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                resetError = exception;
            }

            ClearScanState();

            if (resetError is not null)
            {
                ScanErrorText = $"Target scan-session cleanup failed: {resetError.Message}";
                ScanStatusText = "New Scan reset locally, but target cleanup failed.";
            }
        }
        finally
        {
            SetSavedAddressTargetStateTransition(false);
            EndForegroundTargetOperation();
        }
    }

    private void CancelScan()
    {
        if (_scanCancellation is null)
        {
            return;
        }

        ScanStatusText = "Cancellation requested — waiting for the current target scan operation to finish safely...";
        _scanCancellation.Cancel();
        RaiseScanCommandStates();
    }

    private void ClearScanResultsForOperation()
    {
        _scanCandidates = Array.Empty<MemoryScanResult>();
        _scanResultCount = 0;
        SetHasScanSession(false);
        ScanResults = Array.Empty<ScanResultViewModel>();
        ScanErrorText = string.Empty;
        OnPropertyChanged(nameof(ScanResultCountText));
        OnPropertyChanged(nameof(ScanEmptyMessage));
        OnPropertyChanged(nameof(ScanResultFooterText));
        RaiseScanCommandStates();
    }

    private void ClearScanState()
    {
        ReleaseScanResultStorageSession();
        ResetScanProgress();
        _scanCandidates = Array.Empty<MemoryScanResult>();
        _scanResultCount = 0;
        SetHasScanSession(false, resetScanTypeToDefault: true);
        ScanResults = Array.Empty<ScanResultViewModel>();
        ScanErrorText = string.Empty;
        ScanStatusText = ActiveProcess is null
            ? "Set an Active Target to begin scanning."
            : "Ready for First Scan.";
        OnPropertyChanged(nameof(ScanResultCountText));
        OnPropertyChanged(nameof(ScanEmptyMessage));
        OnPropertyChanged(nameof(ScanResultFooterText));
        RaiseScanCommandStates();
    }

    private string BeginScanResultStorageSession(
        TargetProcessViewModel activeProcess,
        IMemoryValueType valueType,
        IMemoryScanType scanType)
    {
        ReleaseScanResultStorageSession();

        if (_scanResultStorageManager is null)
        {
            return string.Empty;
        }

        try
        {
            _scanResultStorageSession = _scanResultStorageManager.CreateScanSession(
                new ScanResultSessionContext(
                    Metadata.Id,
                    activeProcess.Process.Id,
                    valueType.Id,
                    scanType.Id));
            return string.Empty;
        }
        catch (Exception exception)
        {
            _scanResultStorageSession = null;
            return $"Scan-result storage session could not be created: {exception.Message}";
        }
    }

    private void CancelScanResultStorageSession()
    {
        _nativeResidentResultSet = null;
        _scanResultSet?.Dispose();
        _scanResultSet = null;

        if (_scanResultStorageSession is null)
        {
            return;
        }

        _scanResultStorageSession.Cancel();
        _scanResultStorageSession = null;
    }

    private void FailScanResultStorageSession(string? reason)
    {
        _nativeResidentResultSet = null;
        _scanResultSet?.Dispose();
        _scanResultSet = null;

        if (_scanResultStorageSession is null)
        {
            return;
        }

        _scanResultStorageSession.Fail(reason);
        _scanResultStorageSession = null;
    }

    private void ReleaseScanResultStorageSession()
    {
        _nativeResidentResultSet = null;
        _scanResultSet?.Dispose();
        _scanResultSet = null;

        if (_scanResultStorageSession is null)
        {
            return;
        }

        _scanResultStorageSession.Invalidate();
        _scanResultStorageSession = null;
    }

    private void SetHasScanSession(bool value, bool resetScanTypeToDefault = false)
    {
        if (_hasScanSession == value)
        {
            if (resetScanTypeToDefault)
            {
                RefreshScanTypesForCurrentStage(notify: true, preferDefault: true);
                RefreshScanOptionStates();
            }

            return;
        }

        _hasScanSession = value;
        RefreshScanTypesForCurrentStage(notify: true, preferDefault: resetScanTypeToDefault);
        RefreshScanOptionStates();
        OnPropertyChanged(nameof(IsFirstScanPrimaryAction));
        OnPropertyChanged(nameof(IsNextScanPrimaryAction));
        OnPropertyChanged(nameof(ScanEmptyMessage));
        OnPropertyChanged(nameof(CanSelectScanValueType));
        RaiseScanCommandStates();
    }

    private void RefreshScanTypesForCurrentStage(bool notify, bool preferDefault = false)
    {
        IMemoryValueType? selectedValueType = _selectedScanValueType?.ValueType;
        ScanTypeViewModel[] availableScanTypes = _supportedScanTypes
            .Where(option =>
                (_hasScanSession
                    ? option.ScanType.AvailableForNextScan
                    : option.ScanType.AvailableForFirstScan) &&
                (selectedValueType is null || option.ScanType.SupportsValueType(selectedValueType)))
            .ToArray();

        ScanTypeViewModel? defaultSelection = availableScanTypes.FirstOrDefault(option =>
                string.Equals(option.Id, MemoryScanTypeCatalog.DefaultScanTypeId, StringComparison.OrdinalIgnoreCase))
            ?? availableScanTypes.FirstOrDefault();
        string? previousSelectionId = _selectedScanType?.Id;
        ScanTypeViewModel? selected = !preferDefault && previousSelectionId is not null
            ? availableScanTypes.FirstOrDefault(option =>
                string.Equals(option.Id, previousSelectionId, StringComparison.OrdinalIgnoreCase)) ?? defaultSelection
            : defaultSelection;

        bool selectionChanged = !string.Equals(
            _selectedScanType?.Id,
            selected?.Id,
            StringComparison.OrdinalIgnoreCase);

        // Keep one persistent collection instance so WPF never receives a replacement
        // ItemsSource while it is simultaneously invalidating a Next-only SelectedItem.
        // Clear the backing selection before changing the collection, repopulate the valid
        // stage list, then publish the resolved selection only after Exact/default is present.
        _selectedScanType = null;
        _scanTypes.Clear();
        foreach (ScanTypeViewModel option in availableScanTypes)
        {
            _scanTypes.Add(option);
        }

        _selectedScanType = selected;

        if (!notify)
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedScanType));

        if (selectionChanged || preferDefault)
        {
            RaiseScanInputLayoutProperties();
            OnPropertyChanged(nameof(ScanValueToolTip));
        }

        OnPropertyChanged(nameof(CanSelectScanType));
        RaiseScanCommandStates();
    }

    private void RaiseScanInputLayoutProperties()
    {
        OnPropertyChanged(nameof(ScanInputValueCount));
        OnPropertyChanged(nameof(ShowsPrimaryScanValue));
        OnPropertyChanged(nameof(ShowsSecondaryScanValue));
        OnPropertyChanged(nameof(PrimaryScanValueLabel));
        OnPropertyChanged(nameof(SecondaryScanValueLabel));
    }

    private MemoryScanOptions BuildScanOptions()
    {
        return new MemoryScanOptions(_scanOptions.Select(option =>
            new KeyValuePair<string, string>(option.Id, option.SelectedChoice.Id)));
    }

    private static TargetArchitecture ResolveScanArchitecture(
        TargetArchitecture architecture,
        MemoryScanOptions scanOptions)
    {
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(scanOptions);

        Endianness endianness = architecture.Endianness;
        if (scanOptions.TryGetValue(StandardMemoryScanOptionIds.Endianness, out string choiceId))
        {
            if (string.Equals(choiceId, StandardMemoryScanOptionChoiceIds.LittleEndian, StringComparison.OrdinalIgnoreCase))
            {
                endianness = Endianness.Little;
            }
            else if (string.Equals(choiceId, StandardMemoryScanOptionChoiceIds.BigEndian, StringComparison.OrdinalIgnoreCase))
            {
                endianness = Endianness.Big;
            }
        }

        return endianness == architecture.Endianness
            ? architecture
            : new TargetArchitecture(
                architecture.Cpu,
                architecture.PointerWidthBits,
                architecture.AddressWidthBits,
                endianness);
    }

    private void RefreshScanOptionStates()
    {
        IMemoryValueType? selectedValueType = _selectedScanValueType?.ValueType;
        IMemoryScanType? selectedScanType = _selectedScanType?.ScanType;
        MemoryScanStage stage = _hasScanSession ? MemoryScanStage.NextScan : MemoryScanStage.FirstScan;

        foreach (ScanOptionViewModel option in _scanOptions)
        {
            option.RefreshState(selectedValueType, selectedScanType, stage, IsScanningMemory, _hasScanSession);
        }
    }

    private void OnScanOptionSelectionChanged(object? sender, EventArgs e)
    {
        ScanErrorText = string.Empty;
        RaiseScanCommandStates();
    }

    private async Task ResetNativeScanSessionIfAvailableAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return;
        }

        INativeValueScanStreamRefiner? streamingRefiner = _session.GetService<INativeValueScanStreamRefiner>();
        if (streamingRefiner is not null)
        {
            await streamingRefiner.ResetAsync(cancellationToken).ConfigureAwait(true);
            return;
        }

        INativeValueScanRefiner? nativeRefiner = _session.GetService<INativeValueScanRefiner>();
        if (nativeRefiner is not null)
        {
            await nativeRefiner.ResetAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private void BeginScanProgress(bool isIndeterminate)
    {
        _scanElapsedTimer.Stop();
        _scanStopwatch.Restart();
        ScanProgressPercentage = 0;
        IsScanProgressIndeterminate = isIndeterminate;
        ScanElapsedText = FormatElapsedTime(TimeSpan.Zero);
        HasScanProgress = true;
        _scanElapsedTimer.Start();
    }

    private void CompleteScanProgress()
    {
        IsScanProgressIndeterminate = false;
        ScanProgressPercentage = 100;
        StopScanProgress();
    }

    private void StopScanProgress()
    {
        _scanElapsedTimer.Stop();
        _scanStopwatch.Stop();
        IsScanProgressIndeterminate = false;
        ScanElapsedText = FormatElapsedTime(_scanStopwatch.Elapsed);
    }

    private void ResetScanProgress()
    {
        _scanElapsedTimer.Stop();
        _scanStopwatch.Reset();
        ScanProgressPercentage = 0;
        IsScanProgressIndeterminate = false;
        ScanElapsedText = FormatElapsedTime(TimeSpan.Zero);
        HasScanProgress = false;
    }

    private void UpdateScanProgress(MemoryScanProgress progress)
    {
        ScanProgressPercentage = progress.UnitsTotal == 0
            ? 0
            : Math.Clamp(progress.UnitsProcessed * 100d / progress.UnitsTotal, 0d, 100d);
        ScanElapsedText = FormatElapsedTime(_scanStopwatch.Elapsed);
    }

    private void OnScanElapsedTimerTick(object? sender, EventArgs e)
    {
        if (_scanStopwatch.IsRunning)
        {
            ScanElapsedText = FormatElapsedTime(_scanStopwatch.Elapsed);
        }
    }

    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss\.f", CultureInfo.InvariantCulture)
            : elapsed.ToString(@"mm\:ss\.f", CultureInfo.InvariantCulture);
    }

    private static string FormatProgressPercentage(ulong processed, ulong total)
    {
        if (total == 0)
        {
            return "0%";
        }

        double percentage = Math.Min(100d, processed * 100d / total);
        return $"{percentage:0.0}%";
    }

    private void EnsureMemoryReadDefaultAddress()
    {
        if (!SupportsMemoryRead || ActiveProcess is null || !string.IsNullOrWhiteSpace(MemoryReadAddressText))
        {
            return;
        }

        MemoryRegion? region = ActiveMemoryRegions.FirstOrDefault(candidate =>
            candidate.Size >= DefaultMemoryReadLength &&
            candidate.Protection.HasFlag(MemoryProtection.Read));

        if (region is not null)
        {
            MemoryReadAddressText = $"0x{region.BaseAddress:X}";
            MemoryReadLengthText = DefaultMemoryReadLength.ToString(CultureInfo.InvariantCulture);
            MemoryReadStatusText = "Ready to read from the Active Target.";
        }
    }

    private void ClearMemoryReadState(bool clearAddress)
    {
        if (clearAddress)
        {
            MemoryReadAddressText = string.Empty;
            MemoryReadLengthText = DefaultMemoryReadLength.ToString(CultureInfo.InvariantCulture);
        }

        MemoryReadResultText = string.Empty;
        MemoryReadErrorText = string.Empty;
        MemoryReadStatusText = ActiveProcess is null
            ? "Set an Active Target to read memory."
            : "Ready to read from the Active Target.";
    }

    private void ClearMemoryWriteState(bool clearInputs)
    {
        if (clearInputs)
        {
            MemoryWriteAddressText = string.Empty;
            MemoryWriteValueText = string.Empty;
        }

        MemoryWriteResultText = string.Empty;
        MemoryWriteErrorText = string.Empty;
        MemoryWriteStatusText = ActiveProcess is null
            ? "Set an Active Target to write memory."
            : "Ready to write to the Active Target.";
    }

    private MemoryRegion? FindContainingRegion(ulong address, int length)
    {
        return FindRegionWithProtection(address, length, MemoryProtection.None);
    }

    private MemoryRegion? FindReadableRegion(ulong address, int length)
    {
        return FindRegionWithProtection(address, length, MemoryProtection.Read);
    }

    private MemoryRegion? FindWritableRegion(ulong address, int length)
    {
        return FindRegionWithProtection(address, length, MemoryProtection.Write);
    }

    private MemoryRegion? FindRegionWithProtection(
        ulong address,
        int length,
        MemoryProtection requiredProtection)
    {
        ulong requestedLength = checked((ulong)length);

        foreach (MemoryRegion region in ActiveMemoryRegions)
        {
            if (!region.Protection.HasFlag(requiredProtection) || address < region.BaseAddress)
            {
                continue;
            }

            ulong offset = address - region.BaseAddress;
            if (offset <= region.Size && requestedLength <= region.Size - offset)
            {
                return region;
            }
        }

        return null;
    }

    private static bool TryParseHexAddress(string text, out ulong address)
    {
        address = 0;

        string candidate = text.Trim();
        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[2..];
        }

        if (candidate.Length == 0)
        {
            return false;
        }

        return ulong.TryParse(
            candidate,
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out address);
    }

    private static bool TryParseReadLength(string text, out int length)
    {
        string candidate = text.Trim();
        NumberStyles styles = NumberStyles.None;

        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[2..];
            styles = NumberStyles.AllowHexSpecifier;
        }

        return int.TryParse(candidate, styles, CultureInfo.InvariantCulture, out length) &&
               length is >= 1 and <= MaximumInspectorReadLength;
    }

    private static bool TryParseInt32DecimalValue(string text, out int value)
    {
        return int.TryParse(
            text.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static byte[] EncodeInt32(int value, Endianness endianness)
    {
        byte[] bytes = new byte[sizeof(int)];

        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        }

        return bytes;
    }

    private static int DecodeInt32(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        if (bytes.Length != sizeof(int))
        {
            throw new ArgumentException("A 4-byte value requires exactly four bytes.", nameof(bytes));
        }

        return endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt32BigEndian(bytes)
            : BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static string FormatMemoryWriteResult(
        ulong address,
        byte[]? originalBytes,
        byte[] requestedBytes,
        byte[]? readBackBytes,
        bool? verificationPassed)
    {
        StringBuilder builder = new();
        builder.Append("Address:      0x");
        builder.AppendLine(address.ToString("X16", CultureInfo.InvariantCulture));
        builder.Append("Original:     ");
        builder.AppendLine(originalBytes is null ? "not read" : FormatByteSequence(originalBytes));
        builder.Append("Requested:    ");
        builder.AppendLine(FormatByteSequence(requestedBytes));
        builder.Append("Read-back:    ");
        builder.AppendLine(readBackBytes is null ? "not available" : FormatByteSequence(readBackBytes));
        builder.Append("Verification: ");
        builder.Append(verificationPassed switch
        {
            true => "PASS",
            false => "FAIL",
            null => "NOT AVAILABLE"
        });

        return builder.ToString();
    }

    private static string FormatByteSequence(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        StringBuilder builder = new(checked(bytes.Length * 3 - 1));

        for (int index = 0; index < bytes.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string FormatHexDump(ulong baseAddress, ReadOnlySpan<byte> bytes)
    {
        const int bytesPerLine = 16;
        StringBuilder builder = new();

        for (int offset = 0; offset < bytes.Length; offset += bytesPerLine)
        {
            int lineLength = Math.Min(bytesPerLine, bytes.Length - offset);
            ulong lineAddress = checked(baseAddress + (ulong)offset);
            builder.Append(lineAddress.ToString("X16", CultureInfo.InvariantCulture));
            builder.Append("  ");

            for (int index = 0; index < bytesPerLine; index++)
            {
                if (index < lineLength)
                {
                    builder.Append(bytes[offset + index].ToString("X2", CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append("  ");
                }

                if (index != bytesPerLine - 1)
                {
                    builder.Append(' ');
                }
            }

            builder.Append("  |");
            for (int index = 0; index < lineLength; index++)
            {
                byte value = bytes[offset + index];
                builder.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
            }

            builder.Append('|');
            if (offset + lineLength < bytes.Length)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static string FormatRegionName(MemoryRegion region)
    {
        return string.IsNullOrWhiteSpace(region.Name)
            ? $"region 0x{region.BaseAddress:X}-0x{region.EndAddressExclusive:X}"
            : $"'{region.Name}'";
    }

    private string? ValidateConnectionSettings()
    {
        ConnectionSettingViewModel? missing = ConnectionSettings
            .FirstOrDefault(setting => setting.IsRequired && string.IsNullOrWhiteSpace(setting.Value));

        return missing is null
            ? null
            : $"{missing.Definition.Label} is required.";
    }

    private void ClearProcessState(string statusText)
    {
        Processes.Clear();
        SelectedProcess = null;
        ActiveProcess = null;
        ProcessErrorText = string.Empty;
        ProcessStatusText = statusText;
        ClearMemoryRegionState();
        ClearMemoryReadState(clearAddress: true);
        ClearMemoryWriteState(clearInputs: true);
        ClearScanState();
    }

    private void ClearMemoryRegionState(string statusText = "")
    {
        ActiveMemoryRegions = Array.Empty<MemoryRegion>();
        MemoryRegionErrorText = string.Empty;
        MemoryRegionStatusText = statusText;
    }

    private TargetProcessViewModel? FindMatchingProcess(TargetProcess? identity)
    {
        return identity is null
            ? null
            : Processes.FirstOrDefault(process => AreSameProcess(process.Process, identity));
    }

    private void RaiseConnectionCommandStates()
    {
        _connectCommand.RaiseCanExecuteChanged();
        _disconnectCommand.RaiseCanExecuteChanged();
    }

    private void RaiseProcessCommandStates()
    {
        _refreshProcessesCommand.RaiseCanExecuteChanged();
        _setActiveProcessCommand.RaiseCanExecuteChanged();
    }

    private void RaiseMemoryCommandStates()
    {
        _readMemoryCommand.RaiseCanExecuteChanged();
        _writeMemoryCommand.RaiseCanExecuteChanged();
        _safeWriteTestCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanOpenDisassembler));
        OnPropertyChanged(nameof(CanOpenDebugger));
        RaiseScanCommandStates();
    }

    private void RaiseScanCommandStates()
    {
        _firstScanCommand.RaiseCanExecuteChanged();
        _nextScanCommand.RaiseCanExecuteChanged();
        _submitScanCommand.RaiseCanExecuteChanged();
        _newScanCommand.RaiseCanExecuteChanged();
        _cancelScanCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddSavedAddressManually));
    }

    private static bool AreSameProcess(TargetProcess? left, TargetProcess? right)
    {
        return left is not null &&
               right is not null &&
               left.Id == right.Id &&
               string.Equals(left.Name, right.Name, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> EnumerateCapabilities(TargetCapabilities capabilities)
    {
        return Enum.GetValues<TargetCapabilities>()
            .Where(capability =>
                capability != TargetCapabilities.None &&
                IsSingleFlag(capability) &&
                capabilities.HasFlag(capability))
            .Select(capability => SplitPascalCaseRegex().Replace(capability.ToString(), " $1"))
            .ToArray();
    }

    private static bool IsSingleFlag(TargetCapabilities capability)
    {
        ulong value = (ulong)capability;
        return value != 0 && (value & (value - 1)) == 0;
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex SplitPascalCaseRegex();
}
