using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.Plugins;
using TeeKay87.MemoryEngine.Core.Scanning;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

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
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Stopwatch _scanStopwatch = new();
    private readonly DispatcherTimer _scanElapsedTimer;
    private CancellationTokenSource? _scanCancellation;
    private ITargetSession? _session;
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
    private IReadOnlyList<ScanResultViewModel> _scanResults = Array.Empty<ScanResultViewModel>();
    private readonly IReadOnlyList<ScanValueTypeViewModel> _scanValueTypes;
    private ScanValueTypeViewModel _selectedScanValueType;
    private string _scanValueText = string.Empty;
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
    private bool _disposed;

    public PluginViewModel(DiscoveredPlugin discoveredPlugin)
    {
        ArgumentNullException.ThrowIfNull(discoveredPlugin);

        Instance = discoveredPlugin.Instance;
        AssemblyPath = discoveredPlugin.AssemblyPath;
        Metadata = Instance.Metadata;
        Capabilities = EnumerateCapabilities(Instance.Capabilities);
        ConnectionSettings = new ObservableCollection<ConnectionSettingViewModel>(
            Instance.ConnectionSettings.Select(setting => new ConnectionSettingViewModel(setting)));

        _scanValueTypes = MemoryScanValueCodec.SupportedExactValueTypes
            .Select(ScanValueTypeViewModel.Create)
            .ToArray();
        _selectedScanValueType = _scanValueTypes.First(option => option.ValueType == MemoryValueType.Int32);

        _scanElapsedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _scanElapsedTimer.Tick += OnScanElapsedTimerTick;

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

    public string SelectorDisplay => $"{Name} · {Backend}";

    public override string ToString() => SelectorDisplay;

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

    public bool SupportsNativeValueScanning =>
        Instance.Capabilities.HasFlag(TargetCapabilities.NativeValueScanning);

    public bool SupportsScanPause =>
        Instance.Capabilities.HasFlag(TargetCapabilities.ProcessSuspend) &&
        Instance.Capabilities.HasFlag(TargetCapabilities.ProcessResume);

    public bool SupportsSharedScanner =>
        SupportsMemoryRead && SupportsMemoryRegionEnumeration;

    public bool CanConfigureScanPause => SupportsScanPause && !IsScanningMemory;

    public bool IsConnecting
    {
        get => _isConnecting;
        private set
        {
            if (SetProperty(ref _isConnecting, value))
            {
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
                RaiseConnectionCommandStates();
                RaiseProcessCommandStates();
                RaiseMemoryCommandStates();
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
                OnPropertyChanged(nameof(CanSelectScanValueType));
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
                _setActiveProcessCommand.RaiseCanExecuteChanged();
                RaiseMemoryCommandStates();
            }
        }
    }

    public string ActiveProcessText => ActiveProcess is null
        ? "No active target selected."
        : $"{ActiveProcess.DisplayName} ({ActiveProcess.ProcessIdDisplay})";

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

    public IReadOnlyList<ScanValueTypeViewModel> ScanValueTypes => _scanValueTypes;

    public ScanValueTypeViewModel SelectedScanValueType
    {
        get => _selectedScanValueType;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (!CanSelectScanValueType && value != _selectedScanValueType)
            {
                return;
            }

            if (SetProperty(ref _selectedScanValueType, value))
            {
                ScanErrorText = string.Empty;
                OnPropertyChanged(nameof(ScanValueToolTip));
            }
        }
    }

    public bool CanSelectScanValueType => SupportsSharedScanner && !IsScanningMemory && !_hasScanSession;

    public string ScanValueToolTip =>
        $"{MemoryScanValueCodec.GetInputDescription(SelectedScanValueType.ValueType)} Press Enter to run the highlighted scan action.";

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

            SetProperty(ref _pauseTargetWhileScanning, value);
        }
    }

    public IReadOnlyList<ScanResultViewModel> ScanResults
    {
        get => _scanResults;
        private set
        {
            if (SetProperty(ref _scanResults, value))
            {
                OnPropertyChanged(nameof(HasVisibleScanResults));
            }
        }
    }

    public bool HasVisibleScanResults => ScanResults.Count > 0;

    public bool IsFirstScanPrimaryAction => !_hasScanSession;

    public bool IsNextScanPrimaryAction => _hasScanSession;

    public string ScanResultCountText => $"{_scanCandidates.Count:N0} results";

    public string ScanEmptyMessage => _hasScanSession
        ? "No matching addresses found."
        : "No scan has been started.";

    public string ScanResultFooterText
    {
        get
        {
            if (_scanCandidates.Count > MaximumDisplayedScanResults)
            {
                return $"Showing the first {MaximumDisplayedScanResults:N0} of {_scanCandidates.Count:N0} temporary results.";
            }

            return "Results remain temporary until they are added to Saved Addresses.";
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanElapsedTimer.Stop();
        _scanElapsedTimer.Tick -= OnScanElapsedTimerTick;
        _scanStopwatch.Stop();
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = null;
        _lifetimeCancellation.Cancel();

        if (_session is not null)
        {
            _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _session = null;
        }

        _lifetimeCancellation.Dispose();
    }

    private bool CanConnect()
    {
        return !_disposed && SupportsConnection && !IsConnecting && !IsConnected && !IsScanningMemory;
    }

    private bool CanDisconnect()
    {
        return !_disposed &&
               !IsConnecting &&
               !IsReadingMemory &&
               !IsWritingMemory &&
               !IsScanningMemory &&
               IsConnected;
    }

    private bool CanRefreshProcesses()
    {
        return !_disposed &&
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
        return !_disposed && !IsScanningMemory && _hasScanSession;
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
                await _session.DisposeAsync().ConfigureAwait(true);
            }

            _session = session;
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

        ConnectionErrorText = string.Empty;

        try
        {
            if (_session is not null)
            {
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
    }

    private async Task RefreshProcessesAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null || !IsConnected)
        {
            ProcessStatusText = "Connect to enumerate processes.";
            return;
        }

        IsRefreshingProcesses = true;
        ProcessErrorText = string.Empty;
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
        }
    }

    private async Task SetActiveProcessAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (SelectedProcess is null)
        {
            return;
        }

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

        if (!TryPrepareScan(out ITargetSession? session, out TargetProcessViewModel? activeProcess, out IMemoryReader? reader, out MemoryScanValue? targetValue))
        {
            return;
        }

        INativeValueScanner? nativeScanner = SupportsNativeValueScanning
            ? session!.GetService<INativeValueScanner>()
            : null;
        bool useNativeScanner = nativeScanner is not null;

        ClearScanResultsForOperation();
        BeginScanProgress(useNativeScanner);
        IsScanningMemory = true;
        string scanValueDescription = $"{MemoryScanValueCodec.GetDisplayName(targetValue!.ValueType)} value {targetValue.DisplayText}";
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
            ScanStatusText = useNativeScanner
                ? $"Scanning target natively for {scanValueDescription}..."
                : $"Scanning readable memory for {scanValueDescription}...";

            MemoryScanExecutionResult result;
            if (useNativeScanner)
            {
                try
                {
                    result = await _memoryScanner
                        .FirstScanExactNativeAsync(
                            activeProcess!.Process,
                            ActiveMemoryRegions,
                            nativeScanner!,
                            targetValue!,
                            scanCancellation.Token)
                        .ConfigureAwait(true);
                }
                catch (NotSupportedException) when (!scanCancellation.IsCancellationRequested)
                {
                    IsScanProgressIndeterminate = false;
                    ScanProgressPercentage = 0;
                    ScanStatusText = $"Native acceleration unavailable for this scan; scanning readable memory for {scanValueDescription}...";
                    Progress<MemoryScanProgress> fallbackProgress = new(UpdateFirstScanProgress);
                    result = await _memoryScanner
                        .FirstScanExactAsync(
                            activeProcess!.Process,
                            ActiveMemoryRegions,
                            reader!,
                            session!.Architecture,
                            targetValue!,
                            fallbackProgress,
                            scanCancellation.Token)
                        .ConfigureAwait(true);
                }
            }
            else
            {
                Progress<MemoryScanProgress> progress = new(UpdateFirstScanProgress);
                result = await _memoryScanner
                    .FirstScanExactAsync(
                        activeProcess!.Process,
                        ActiveMemoryRegions,
                        reader!,
                        session!.Architecture,
                        targetValue!,
                        progress,
                        scanCancellation.Token)
                    .ConfigureAwait(true);
            }

            ApplyScanResult(result, isFirstScan: true);
            CompleteScanProgress();
        }
        catch (OperationCanceledException) when (scanCancellation.IsCancellationRequested)
        {
            StopScanProgress();
            ScanStatusText = "Scan cancelled.";
        }
        catch (Exception exception)
        {
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

        if (!TryPrepareScan(out ITargetSession? session, out TargetProcessViewModel? activeProcess, out IMemoryReader? reader, out MemoryScanValue? targetValue))
        {
            return;
        }

        INativeValueScanRefiner? nativeRefiner = SupportsNativeValueScanning
            ? session!.GetService<INativeValueScanRefiner>()
            : null;
        bool useNativeRefiner = nativeRefiner is not null;

        BeginScanProgress(useNativeRefiner);
        IsScanningMemory = true;
        ScanErrorText = string.Empty;
        string scanValueDescription = $"{MemoryScanValueCodec.GetDisplayName(targetValue!.ValueType)} value {targetValue.DisplayText}";
        ScanStatusText = useNativeRefiner
            ? $"Refining {_scanCandidates.Count:N0} candidates natively for {scanValueDescription}..."
            : $"Refining {_scanCandidates.Count:N0} candidates for {scanValueDescription}...";

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
            ScanStatusText = useNativeRefiner
                ? $"Refining {_scanCandidates.Count:N0} candidates natively for {scanValueDescription}..."
                : $"Refining {_scanCandidates.Count:N0} candidates for {scanValueDescription}...";

            MemoryScanExecutionResult result;
            if (useNativeRefiner)
            {
                try
                {
                    result = await _memoryScanner
                        .NextScanExactNativeAsync(
                            activeProcess!.Process,
                            ActiveMemoryRegions,
                            _scanCandidates,
                            nativeRefiner!,
                            targetValue!,
                            scanCancellation.Token)
                        .ConfigureAwait(true);
                }
                catch (NotSupportedException) when (!scanCancellation.IsCancellationRequested)
                {
                    IsScanProgressIndeterminate = false;
                    ScanProgressPercentage = 0;
                    ScanStatusText = $"Native refinement unavailable; refining {_scanCandidates.Count:N0} candidates for {scanValueDescription}...";
                    Progress<MemoryScanProgress> fallbackProgress = new(UpdateNextScanProgress);
                    result = await _memoryScanner
                        .NextScanExactAsync(
                            activeProcess!.Process,
                            ActiveMemoryRegions,
                            _scanCandidates,
                            reader!,
                            session!.Architecture,
                            targetValue!,
                            fallbackProgress,
                            scanCancellation.Token)
                        .ConfigureAwait(true);
                }
            }
            else
            {
                Progress<MemoryScanProgress> progress = new(UpdateNextScanProgress);
                result = await _memoryScanner
                    .NextScanExactAsync(
                        activeProcess!.Process,
                        ActiveMemoryRegions,
                        _scanCandidates,
                        reader!,
                        session!.Architecture,
                        targetValue!,
                        progress,
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

    private bool TryPrepareScan(
        out ITargetSession? session,
        out TargetProcessViewModel? activeProcess,
        out IMemoryReader? reader,
        out MemoryScanValue? targetValue)
    {
        session = _session;
        activeProcess = ActiveProcess;
        reader = null;
        targetValue = null;
        ScanErrorText = string.Empty;

        if (session is null || activeProcess is null || !IsConnected)
        {
            ScanStatusText = "Set an Active Target to begin scanning.";
            return false;
        }

        if (!MemoryScanValueCodec.TryParse(
                ScanValueText,
                SelectedScanValueType.ValueType,
                session.Architecture,
                out targetValue,
                out string parseError) ||
            targetValue is null)
        {
            ScanErrorText = parseError;
            ScanStatusText = "Scan not started.";
            return false;
        }

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

        if (scanResult.Result.ValueType != MemoryValueType.Int32)
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

    private void ApplyScanResult(MemoryScanExecutionResult result, bool isFirstScan)
    {
        _scanCandidates = result.Results;
        SetHasScanSession(true);
        ScanResults = _scanCandidates
            .Take(MaximumDisplayedScanResults)
            .Select(candidate => new ScanResultViewModel(
                candidate,
                CopyScanResultText,
                PrepareMemoryWriteForScanResult,
                () => SupportsMemoryWrite && candidate.ValueType == MemoryValueType.Int32))
            .ToArray();

        OnPropertyChanged(nameof(ScanResultCountText));
        OnPropertyChanged(nameof(ScanEmptyMessage));
        OnPropertyChanged(nameof(ScanResultFooterText));

        string operation = isFirstScan ? "First Scan" : "Next Scan";
        string failureText = result.ReadFailureCount == 0
            ? string.Empty
            : $" {result.ReadFailureCount:N0} memory read request(s) failed and were skipped.";

        ScanStatusText = $"{operation} complete: {_scanCandidates.Count:N0} result(s).{failureText}";
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
        Exception? resetError = null;
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
        ResetScanProgress();
        _scanCandidates = Array.Empty<MemoryScanResult>();
        SetHasScanSession(false);
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

    private void SetHasScanSession(bool value)
    {
        if (_hasScanSession == value)
        {
            return;
        }

        _hasScanSession = value;
        OnPropertyChanged(nameof(IsFirstScanPrimaryAction));
        OnPropertyChanged(nameof(IsNextScanPrimaryAction));
        OnPropertyChanged(nameof(ScanEmptyMessage));
        OnPropertyChanged(nameof(CanSelectScanValueType));
        RaiseScanCommandStates();
    }

    private async Task ResetNativeScanSessionIfAvailableAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
        {
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
        RaiseScanCommandStates();
    }

    private void RaiseScanCommandStates()
    {
        _firstScanCommand.RaiseCanExecuteChanged();
        _nextScanCommand.RaiseCanExecuteChanged();
        _submitScanCommand.RaiseCanExecuteChanged();
        _newScanCommand.RaiseCanExecuteChanged();
        _cancelScanCommand.RaiseCanExecuteChanged();
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
