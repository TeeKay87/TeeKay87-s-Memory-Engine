using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TeeKay87.MemoryEngine.App.Application;
using TeeKay87.MemoryEngine.App.Exporting;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.Disassembly;
using TeeKay87.MemoryEngine.Core.Exporting;
using TeeKay87.MemoryEngine.Core.MemoryViewer;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class DisassemblerViewModel : ObservableObject, IDisposable
{
    private readonly PluginViewModel _plugin;
    private readonly TargetProcess _targetProcess;
    private readonly long _connectionGeneration;
    private readonly DisassemblySessionIdentity _sessionIdentity;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly AsyncRelayCommand _backCommand;
    private readonly AsyncRelayCommand _forwardCommand;
    private readonly AsyncRelayCommand _goToAddressCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _followTargetCommand;
    private readonly AsyncRelayCommand _previousRegionCommand;
    private readonly AsyncRelayCommand _nextRegionCommand;
    private readonly AsyncRelayCommand _regionStartCommand;
    private readonly AsyncRelayCommand _regionEndCommand;
    private readonly List<ulong> _navigationHistory = new();
    private ulong _currentAddress;
    private string _addressText;
    private string _regionNameText = string.Empty;
    private string _regionRangeText = string.Empty;
    private string _protectionText = string.Empty;
    private string _visibleRangeText = string.Empty;
    private string _architectureText = string.Empty;
    private string _moduleRelativeText = string.Empty;
    private string _statusText = "Loading disassembly...";
    private string _errorText = string.Empty;
    private bool _isBusy;
    private bool _initialized;
    private bool _disposed;
    private int _navigationHistoryIndex = -1;
    private DisassemblyInstructionViewModel? _selectedInstruction;
    private MemoryRegion? _currentRegion;
    private DisassemblySnapshot? _currentSnapshot;
    private ulong? _currentModuleBaseAddress;

    internal DisassemblerViewModel(
        PluginViewModel plugin,
        TargetProcess targetProcess,
        long connectionGeneration,
        ulong initialAddress)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _targetProcess = targetProcess ?? throw new ArgumentNullException(nameof(targetProcess));
        _connectionGeneration = connectionGeneration;
        _sessionIdentity = new DisassemblySessionIdentity(
            plugin.Metadata.Id,
            targetProcess,
            connectionGeneration);
        _currentAddress = initialAddress;
        _addressText = $"0x{initialAddress:X}";

        _backCommand = new AsyncRelayCommand(GoBackAsync, CanGoBack);
        _forwardCommand = new AsyncRelayCommand(GoForwardAsync, CanGoForward);
        _goToAddressCommand = new AsyncRelayCommand(GoToAddressAsync, CanRead);
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, CanRead);
        _followTargetCommand = new AsyncRelayCommand(FollowTargetAsync, CanFollowTarget);
        _previousRegionCommand = new AsyncRelayCommand(GoToPreviousRegionAsync, CanGoToPreviousRegion);
        _nextRegionCommand = new AsyncRelayCommand(GoToNextRegionAsync, CanGoToNextRegion);
        _regionStartCommand = new AsyncRelayCommand(GoToRegionStartAsync, CanGoToRegionStart);
        _regionEndCommand = new AsyncRelayCommand(GoToRegionEndAsync, CanGoToRegionEnd);
    }

    public string WindowTitle => $"Disassembler - {TargetDisplayName}";

    public string TargetText => $"{_plugin.Name} · {TargetDisplayName} (0x{_targetProcess.Id:X})";

    public string TargetDisplayName => !string.IsNullOrWhiteSpace(_targetProcess.DisplayName)
        ? _targetProcess.DisplayName!
        : !string.IsNullOrWhiteSpace(_targetProcess.Name)
            ? _targetProcess.Name
            : "<unnamed process>";

    public string AddressText
    {
        get => _addressText;
        set => SetProperty(ref _addressText, value ?? string.Empty);
    }

    public string RegionNameText
    {
        get => _regionNameText;
        private set => SetProperty(ref _regionNameText, value);
    }

    public string RegionRangeText
    {
        get => _regionRangeText;
        private set => SetProperty(ref _regionRangeText, value);
    }

    public string ProtectionText
    {
        get => _protectionText;
        private set => SetProperty(ref _protectionText, value);
    }

    public string VisibleRangeText
    {
        get => _visibleRangeText;
        private set => SetProperty(ref _visibleRangeText, value);
    }

    public string ArchitectureText
    {
        get => _architectureText;
        private set => SetProperty(ref _architectureText, value);
    }

    public string ModuleRelativeText
    {
        get => _moduleRelativeText;
        private set => SetProperty(ref _moduleRelativeText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseNavigationCanExecuteChanged();
                _goToAddressCommand.RaiseCanExecuteChanged();
                _refreshCommand.RaiseCanExecuteChanged();
                _followTargetCommand.RaiseCanExecuteChanged();
                RaiseRegionCanExecuteChanged();
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    public ObservableCollection<DisassemblyInstructionViewModel> Instructions { get; } = new();

    public bool CanExport => !_disposed && !IsBusy && Instructions.Count > 0;

    public bool IsCurrentRegionExecutable =>
        _currentRegion?.Protection.HasFlag(MemoryProtection.Execute) == true;

    public DisassemblyInstructionViewModel? SelectedInstruction
    {
        get => _selectedInstruction;
        set
        {
            if (SetProperty(ref _selectedInstruction, value))
            {
                _followTargetCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand BackCommand => _backCommand;

    public ICommand ForwardCommand => _forwardCommand;

    public ICommand GoToAddressCommand => _goToAddressCommand;

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand FollowTargetCommand => _followTargetCommand;

    public ICommand PreviousRegionCommand => _previousRegionCommand;

    public ICommand NextRegionCommand => _nextRegionCommand;

    public ICommand RegionStartCommand => _regionStartCommand;

    public ICommand RegionEndCommand => _regionEndCommand;

    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (await ReadAddressAsync(_currentAddress).ConfigureAwait(true))
        {
            RecordNavigationAddress(_currentAddress);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private bool CanRead()
    {
        return !_disposed && !IsBusy;
    }

    private bool CanGoBack()
    {
        return CanRead() && _navigationHistoryIndex > 0;
    }

    private bool CanGoForward()
    {
        return CanRead() &&
               _navigationHistoryIndex >= 0 &&
               _navigationHistoryIndex < _navigationHistory.Count - 1;
    }

    private bool CanGoToPreviousRegion()
    {
        return CanRead() &&
               _currentRegion is not null &&
               MemoryViewerRegionNavigator.FindPreviousReadableRegion(
                   _plugin.ActiveMemoryRegions,
                   _currentRegion) is not null;
    }

    private bool CanGoToNextRegion()
    {
        return CanRead() &&
               _currentRegion is not null &&
               MemoryViewerRegionNavigator.FindNextReadableRegion(
                   _plugin.ActiveMemoryRegions,
                   _currentRegion) is not null;
    }

    private bool CanGoToRegionStart()
    {
        return CanRead() &&
               _currentRegion is not null &&
               _currentAddress != _currentRegion.BaseAddress;
    }

    private bool CanGoToRegionEnd()
    {
        return CanRead() &&
               _currentRegion is { Size: > 0 } region &&
               _currentAddress != region.EndAddressExclusive - 1;
    }

    private async Task GoBackAsync()
    {
        await NavigateHistoryAsync(_navigationHistoryIndex - 1).ConfigureAwait(true);
    }

    private async Task GoForwardAsync()
    {
        await NavigateHistoryAsync(_navigationHistoryIndex + 1).ConfigureAwait(true);
    }

    private async Task GoToPreviousRegionAsync()
    {
        if (_currentRegion is null)
        {
            return;
        }

        MemoryRegion? previousRegion = MemoryViewerRegionNavigator.FindPreviousReadableRegion(
            _plugin.ActiveMemoryRegions,
            _currentRegion);
        if (previousRegion is not null)
        {
            await NavigateAndRecordAsync(previousRegion.BaseAddress).ConfigureAwait(true);
        }
    }

    private async Task GoToNextRegionAsync()
    {
        if (_currentRegion is null)
        {
            return;
        }

        MemoryRegion? nextRegion = MemoryViewerRegionNavigator.FindNextReadableRegion(
            _plugin.ActiveMemoryRegions,
            _currentRegion);
        if (nextRegion is not null)
        {
            await NavigateAndRecordAsync(nextRegion.BaseAddress).ConfigureAwait(true);
        }
    }

    private async Task GoToRegionStartAsync()
    {
        if (_currentRegion is not null)
        {
            await NavigateAndRecordAsync(_currentRegion.BaseAddress).ConfigureAwait(true);
        }
    }

    private async Task GoToRegionEndAsync()
    {
        if (_currentRegion is { Size: > 0 } region)
        {
            await NavigateAndRecordAsync(region.EndAddressExclusive - 1).ConfigureAwait(true);
        }
    }

    private async Task GoToAddressAsync()
    {
        if (!TryParseHexAddress(AddressText, out ulong address))
        {
            ErrorText = "Enter a valid hexadecimal address.";
            StatusText = "Disassembly navigation was not started.";
            return;
        }

        await NavigateAndRecordAsync(address).ConfigureAwait(true);
    }

    private async Task<bool> NavigateAndRecordAsync(ulong address)
    {
        if (!await ReadAddressAsync(address).ConfigureAwait(true))
        {
            return false;
        }

        RecordNavigationAddress(address);
        return true;
    }

    private async Task RefreshAsync()
    {
        await ReadAddressAsync(_currentAddress).ConfigureAwait(true);
    }

    internal bool CanFollowTarget(DisassemblyInstructionViewModel? instruction)
    {
        return CanRead() && instruction?.CanFollowTarget == true;
    }

    private bool CanFollowTarget()
    {
        return CanFollowTarget(SelectedInstruction);
    }

    private async Task FollowTargetAsync()
    {
        ulong? target = SelectedInstruction?.BranchTarget;
        if (!target.HasValue || !CanFollowTarget())
        {
            return;
        }

        await NavigateAndRecordAsync(target.Value).ConfigureAwait(true);
    }

    private async Task NavigateHistoryAsync(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _navigationHistory.Count)
        {
            return;
        }

        ulong address = _navigationHistory[targetIndex];
        if (await ReadAddressAsync(address).ConfigureAwait(true))
        {
            _navigationHistoryIndex = targetIndex;
            RaiseNavigationCanExecuteChanged();
        }
    }

    private async Task<bool> ReadAddressAsync(ulong address)
    {
        if (_disposed)
        {
            return false;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = $"Reading and decoding memory around 0x{address:X}...";

        try
        {
            _sessionIdentity.EnsureCurrent(
                _plugin.Metadata.Id,
                _targetProcess,
                _plugin.ConnectionGeneration);

            DisassemblySnapshot snapshot = await _plugin
                .ReadDisassemblyContextAsync(
                    _targetProcess,
                    _connectionGeneration,
                    address,
                    DisassemblyReader.DefaultContextByteCount,
                    DisassemblyReader.DefaultContextByteCount,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (_disposed)
            {
                return false;
            }

            _currentAddress = address;
            AddressText = $"0x{address:X}";
            ApplySnapshot(snapshot);
            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            if (!_disposed)
            {
                StatusText = "Disassembler read cancelled.";
            }

            return false;
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                ErrorText = exception.Message;
                StatusText = "Disassembler read failed. The previous view has been kept.";
            }

            return false;
        }
        finally
        {
            if (!_disposed)
            {
                IsBusy = false;
            }
        }
    }

    private void ApplySnapshot(DisassemblySnapshot snapshot)
    {
        _currentSnapshot = snapshot;
        _currentRegion = snapshot.Region;
        _currentModuleBaseAddress = ResolveModuleBaseAddress(snapshot.Region);

        IReadOnlyDictionary<ulong, DisassemblyMarker[]> markersByAddress = snapshot.Markers
            .GroupBy(marker => marker.Address)
            .ToDictionary(group => group.Key, group => group.ToArray());

        Instructions.Clear();
        foreach (DisassembledInstruction instruction in snapshot.Instructions)
        {
            markersByAddress.TryGetValue(instruction.Address, out DisassemblyMarker[]? instructionMarkers);
            Instructions.Add(new DisassemblyInstructionViewModel(
                instruction,
                snapshot.RequestedAddress,
                instructionMarkers));
        }

        SelectedInstruction = Instructions.FirstOrDefault(row => row.IsOriginRow)
            ?? Instructions.FirstOrDefault();

        MemoryRegion region = snapshot.Region;
        RegionNameText = !string.IsNullOrWhiteSpace(region.ModuleName)
            ? region.ModuleName!
            : !string.IsNullOrWhiteSpace(region.Name)
                ? region.Name!
                : string.Empty;

        ulong inclusiveRegionEnd = region.EndAddressExclusive - 1;
        RegionRangeText =
            $"0x{region.BaseAddress:X} - 0x{inclusiveRegionEnd:X} · {region.Size:N0} bytes";
        ProtectionText = region.Protection.ToString();

        ulong visibleInclusiveEnd = snapshot.EndAddressExclusive - 1;
        VisibleRangeText =
            $"0x{snapshot.StartAddress:X} - 0x{visibleInclusiveEnd:X} · {snapshot.Bytes.Length:N0} bytes";
        ArchitectureText = FormatArchitecture(snapshot.Architecture);
        ModuleRelativeText = FormatModuleRelativeAddress(
            region.ModuleName,
            _currentModuleBaseAddress,
            snapshot.RequestedAddress);

        int invalidCount = Instructions.Count(row => !row.IsValid);
        StatusText = invalidCount == 0
            ? $"Decoded {Instructions.Count:N0} instruction(s) from {snapshot.Bytes.Length:N0} bytes around 0x{snapshot.RequestedAddress:X}."
            : $"Decoded {Instructions.Count:N0} instruction(s) from {snapshot.Bytes.Length:N0} bytes around 0x{snapshot.RequestedAddress:X}; {invalidCount:N0} record(s) are invalid/undecodable.";
        ErrorText = string.Empty;
        RaiseRegionCanExecuteChanged();
        OnPropertyChanged(nameof(CanExport));
    }

    internal IReadOnlyList<ExportScopeOption> CreateExportScopes(
        IEnumerable<DisassemblyInstructionViewModel> selectedRows)
    {
        ArgumentNullException.ThrowIfNull(selectedRows);
        if (_currentSnapshot is null || Instructions.Count == 0)
        {
            return Array.Empty<ExportScopeOption>();
        }

        DisassembledInstruction[] displayed = Instructions
            .Select(row => row.SourceInstruction)
            .ToArray();
        List<ExportScopeOption> scopes = new()
        {
            new ExportScopeOption(
                "displayed",
                $"Displayed Instructions ({displayed.Length:N0})",
                "Exports every instruction currently materialized in the bounded Disassembler view.",
                new DisassemblyExportSource(
                    displayed,
                    _currentSnapshot.Region,
                    _currentModuleBaseAddress,
                    CreateExportMetadata("displayed", displayed.LongLength),
                    _currentSnapshot.Markers))
        };

        HashSet<DisassemblyInstructionViewModel> selectedSet = selectedRows
            .Where(row => row is not null && Instructions.Contains(row))
            .ToHashSet();
        DisassembledInstruction[] selected = Instructions
            .Where(selectedSet.Contains)
            .Select(row => row.SourceInstruction)
            .ToArray();
        if (selected.Length > 0)
        {
            scopes.Add(new ExportScopeOption(
                "selected",
                $"Selected Instructions ({selected.Length:N0})",
                "Exports only the selected Disassembler rows, preserving their displayed order.",
                new DisassemblyExportSource(
                    selected,
                    _currentSnapshot.Region,
                    _currentModuleBaseAddress,
                    CreateExportMetadata("selected", selected.LongLength),
                    _currentSnapshot.Markers)));
        }

        return scopes;
    }

    internal void ReportStatus(string statusText, string? errorText = null)
    {
        if (_disposed)
        {
            return;
        }

        StatusText = statusText ?? string.Empty;
        ErrorText = errorText ?? string.Empty;
    }

    private IReadOnlyDictionary<string, ExportCellValue> CreateExportMetadata(string scope, long rowCount)
    {
        Dictionary<string, ExportCellValue> metadata = new(StringComparer.Ordinal)
        {
            ["application"] = ExportCellValue.FromString(AppInfo.Title),
            ["applicationVersion"] = ExportCellValue.FromString(AppInfo.DisplayVersion),
            ["scope"] = ExportCellValue.FromString(scope),
            ["pluginId"] = ExportCellValue.FromString(_plugin.Metadata.Id),
            ["pluginName"] = ExportCellValue.FromString(_plugin.Metadata.Name),
            ["pluginVersion"] = ExportCellValue.FromString(_plugin.Metadata.DisplayVersion),
            ["platform"] = ExportCellValue.FromString(_plugin.Metadata.Platform),
            ["backend"] = ExportCellValue.FromString(_plugin.Metadata.Backend),
            ["targetProcessId"] = ExportCellValue.FromUInt64(_targetProcess.Id),
            ["targetProcessName"] = ExportCellValue.FromString(TargetDisplayName),
            ["sourceRowCount"] = ExportCellValue.FromInt64(rowCount),
            ["requestedAddress"] = ExportCellValue.FromString($"0x{_currentAddress:X}")
        };

        if (_currentSnapshot is not null)
        {
            MemoryRegion region = _currentSnapshot.Region;
            metadata["architecture"] = ExportCellValue.FromString(FormatArchitecture(_currentSnapshot.Architecture));
            metadata["regionBase"] = ExportCellValue.FromString($"0x{region.BaseAddress:X}");
            metadata["regionSize"] = ExportCellValue.FromUInt64(region.Size);
            metadata["regionName"] = ExportCellValue.FromString(region.Name);
            metadata["moduleName"] = ExportCellValue.FromString(region.ModuleName);
            metadata["protection"] = ExportCellValue.FromString(region.Protection.ToString());
            if (_currentModuleBaseAddress.HasValue)
            {
                metadata["moduleBase"] = ExportCellValue.FromString($"0x{_currentModuleBaseAddress.Value:X}");
            }
        }

        return metadata;
    }

    private ulong? ResolveModuleBaseAddress(MemoryRegion region)
    {
        if (string.IsNullOrWhiteSpace(region.ModuleName))
        {
            return null;
        }

        return _plugin.ActiveMemoryRegions
            .Where(candidate =>
                string.Equals(candidate.ModuleName, region.ModuleName, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => (ulong?)candidate.BaseAddress)
            .Min() ?? region.BaseAddress;
    }

    private static string FormatModuleRelativeAddress(
        string? moduleName,
        ulong? moduleBaseAddress,
        ulong address)
    {
        return moduleBaseAddress.HasValue &&
               !string.IsNullOrWhiteSpace(moduleName) &&
               address >= moduleBaseAddress.Value
            ? $"Origin: {moduleName} + 0x{address - moduleBaseAddress.Value:X}"
            : string.Empty;
    }

    private void RecordNavigationAddress(ulong address)
    {
        if (_navigationHistoryIndex >= 0 &&
            _navigationHistoryIndex < _navigationHistory.Count &&
            _navigationHistory[_navigationHistoryIndex] == address)
        {
            RaiseNavigationCanExecuteChanged();
            return;
        }

        int firstForwardIndex = _navigationHistoryIndex + 1;
        if (firstForwardIndex < _navigationHistory.Count)
        {
            _navigationHistory.RemoveRange(
                firstForwardIndex,
                _navigationHistory.Count - firstForwardIndex);
        }

        _navigationHistory.Add(address);
        _navigationHistoryIndex = _navigationHistory.Count - 1;
        RaiseNavigationCanExecuteChanged();
    }

    private void RaiseNavigationCanExecuteChanged()
    {
        _backCommand.RaiseCanExecuteChanged();
        _forwardCommand.RaiseCanExecuteChanged();
    }

    private void RaiseRegionCanExecuteChanged()
    {
        _previousRegionCommand.RaiseCanExecuteChanged();
        _nextRegionCommand.RaiseCanExecuteChanged();
        _regionStartCommand.RaiseCanExecuteChanged();
        _regionEndCommand.RaiseCanExecuteChanged();
    }

    private static string FormatArchitecture(TargetArchitecture architecture)
    {
        string cpuText = architecture.Cpu == CpuArchitecture.Unknown
            ? "Custom / Unknown"
            : architecture.Cpu.ToString();
        return $"{cpuText} · {architecture.AddressWidthBits}-bit addresses · " +
               $"{architecture.PointerWidthBits}-bit pointers · {architecture.Endianness} endian";
    }

    private static bool TryParseHexAddress(string text, out ulong address)
    {
        address = 0;
        string candidate = (text ?? string.Empty).Trim();
        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[2..];
        }

        return candidate.Length > 0 &&
               ulong.TryParse(
                   candidate,
                   NumberStyles.AllowHexSpecifier,
                   CultureInfo.InvariantCulture,
                   out address);
    }
}
