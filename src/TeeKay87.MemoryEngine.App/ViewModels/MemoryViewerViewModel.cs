using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.MemoryViewer;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class MemoryViewerViewModel : ObservableObject, IDisposable
{
    private readonly PluginViewModel _plugin;
    private readonly TargetProcess _targetProcess;
    private readonly long _connectionGeneration;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly AsyncRelayCommand _backCommand;
    private readonly AsyncRelayCommand _forwardCommand;
    private readonly AsyncRelayCommand _goToAddressCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _previousRegionCommand;
    private readonly AsyncRelayCommand _nextRegionCommand;
    private readonly AsyncRelayCommand _regionStartCommand;
    private readonly AsyncRelayCommand _regionEndCommand;
    private readonly AsyncRelayCommand _goToBookmarkCommand;
    private readonly RelayCommand _addBookmarkCommand;
    private readonly RelayCommand _removeBookmarkCommand;
    private readonly List<MemoryViewerNavigationEntry> _navigationHistory = new();
    private ulong _currentAddress;
    private int _currentHighlightByteCount;
    private string _addressText;
    private string _regionNameText = string.Empty;
    private string _regionRangeText = string.Empty;
    private string _protectionText = string.Empty;
    private string _visibleRangeText = string.Empty;
    private string _accessModeText = "Read-only";
    private string _statusText = "Loading memory...";
    private string _errorText = string.Empty;
    private bool _isBusy;
    private bool _canEditMemory;
    private bool _initialized;
    private bool _disposed;
    private int _navigationHistoryIndex = -1;
    private MemoryRegion? _currentRegion;
    private MemoryViewerRowViewModel? _selectedRow;
    private MemoryViewerBookmarkViewModel? _selectedBookmark;

    internal MemoryViewerViewModel(
        PluginViewModel plugin,
        TargetProcess targetProcess,
        long connectionGeneration,
        ulong initialAddress,
        int initialHighlightByteCount = 1)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _targetProcess = targetProcess ?? throw new ArgumentNullException(nameof(targetProcess));
        _connectionGeneration = connectionGeneration;
        if (initialHighlightByteCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialHighlightByteCount),
                "The Memory Viewer highlight span must contain at least one byte.");
        }

        _currentAddress = initialAddress;
        _currentHighlightByteCount = initialHighlightByteCount;
        _addressText = $"0x{initialAddress:X}";

        _backCommand = new AsyncRelayCommand(GoBackAsync, CanGoBack);
        _forwardCommand = new AsyncRelayCommand(GoForwardAsync, CanGoForward);
        _goToAddressCommand = new AsyncRelayCommand(GoToAddressAsync, CanRead);
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, CanRead);
        _previousRegionCommand = new AsyncRelayCommand(GoToPreviousRegionAsync, CanGoToPreviousRegion);
        _nextRegionCommand = new AsyncRelayCommand(GoToNextRegionAsync, CanGoToNextRegion);
        _regionStartCommand = new AsyncRelayCommand(GoToRegionStartAsync, CanGoToRegionStart);
        _regionEndCommand = new AsyncRelayCommand(GoToRegionEndAsync, CanGoToRegionEnd);
        _goToBookmarkCommand = new AsyncRelayCommand(GoToBookmarkAsync, CanGoToBookmark);
        _addBookmarkCommand = new RelayCommand(AddBookmark, CanAddBookmark);
        _removeBookmarkCommand = new RelayCommand(RemoveSelectedBookmark, CanRemoveBookmark);
    }

    public string WindowTitle => $"Memory Viewer - {TargetDisplayName}";

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

    public string AccessModeText
    {
        get => _accessModeText;
        private set => SetProperty(ref _accessModeText, value);
    }

    public bool CanEditMemory
    {
        get => _canEditMemory;
        private set
        {
            if (SetProperty(ref _canEditMemory, value))
            {
                OnPropertyChanged(nameof(CanEditSelectedRow));
            }
        }
    }

    public bool CanEditSelectedRow =>
        CanEditMemory &&
        !IsBusy &&
        SelectedRow is { CanEdit: true };

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
                _backCommand.RaiseCanExecuteChanged();
                _forwardCommand.RaiseCanExecuteChanged();
                _goToAddressCommand.RaiseCanExecuteChanged();
                _refreshCommand.RaiseCanExecuteChanged();
                RaiseRegionCanExecuteChanged();
                RaiseBookmarkCanExecuteChanged();
                OnPropertyChanged(nameof(CanEditSelectedRow));
            }
        }
    }

    public ObservableCollection<MemoryViewerRowViewModel> Rows { get; } = new();

    public ObservableCollection<MemoryViewerBookmarkViewModel> Bookmarks { get; } = new();

    public MemoryViewerBookmarkViewModel? SelectedBookmark
    {
        get => _selectedBookmark;
        set
        {
            if (SetProperty(ref _selectedBookmark, value))
            {
                RaiseBookmarkCanExecuteChanged();
            }
        }
    }

    public MemoryViewerRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                OnPropertyChanged(nameof(CanEditSelectedRow));
            }
        }
    }

    public ICommand BackCommand => _backCommand;

    public ICommand ForwardCommand => _forwardCommand;

    public ICommand GoToAddressCommand => _goToAddressCommand;

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand PreviousRegionCommand => _previousRegionCommand;

    public ICommand NextRegionCommand => _nextRegionCommand;

    public ICommand RegionStartCommand => _regionStartCommand;

    public ICommand RegionEndCommand => _regionEndCommand;

    public ICommand AddBookmarkCommand => _addBookmarkCommand;

    public ICommand RemoveBookmarkCommand => _removeBookmarkCommand;

    public ICommand GoToBookmarkCommand => _goToBookmarkCommand;

    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (await ReadAddressAsync(_currentAddress, _currentHighlightByteCount).ConfigureAwait(true))
        {
            RecordNavigationState(_currentAddress, _currentHighlightByteCount);
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

    private bool CanAddBookmark()
    {
        return CanRead() &&
               _currentRegion is not null &&
               !Bookmarks.Any(bookmark => bookmark.AddressValue == _currentAddress);
    }

    private bool CanRemoveBookmark()
    {
        return !_disposed && !IsBusy && SelectedBookmark is not null;
    }

    private bool CanGoToBookmark()
    {
        return CanRead() && SelectedBookmark is not null;
    }

    private async Task GoBackAsync()
    {
        await NavigateHistoryAsync(_navigationHistoryIndex - 1).ConfigureAwait(true);
    }

    private async Task GoForwardAsync()
    {
        await NavigateHistoryAsync(_navigationHistoryIndex + 1).ConfigureAwait(true);
    }

    private async Task GoToAddressAsync()
    {
        if (!TryParseHexAddress(AddressText, out ulong address))
        {
            ErrorText = "Enter a valid hexadecimal address, for example 0x100000000.";
            StatusText = "Memory view unchanged.";
            return;
        }

        await NavigateAndRecordAsync(address, 1).ConfigureAwait(true);
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
            await NavigateAndRecordAsync(previousRegion.BaseAddress, 1).ConfigureAwait(true);
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
            await NavigateAndRecordAsync(nextRegion.BaseAddress, 1).ConfigureAwait(true);
        }
    }

    private async Task GoToRegionStartAsync()
    {
        if (_currentRegion is not null)
        {
            await NavigateAndRecordAsync(_currentRegion.BaseAddress, 1).ConfigureAwait(true);
        }
    }

    private async Task GoToRegionEndAsync()
    {
        if (_currentRegion is { Size: > 0 } region)
        {
            await NavigateAndRecordAsync(region.EndAddressExclusive - 1, 1).ConfigureAwait(true);
        }
    }

    private async Task GoToBookmarkAsync()
    {
        if (SelectedBookmark is MemoryViewerBookmarkViewModel bookmark)
        {
            await NavigateAndRecordAsync(bookmark.AddressValue, 1).ConfigureAwait(true);
        }
    }

    private void AddBookmark()
    {
        if (!CanAddBookmark())
        {
            return;
        }

        MemoryViewerBookmarkViewModel bookmark = new(_currentAddress, RegionNameText);
        Bookmarks.Add(bookmark);
        SelectedBookmark = bookmark;
        ErrorText = string.Empty;
        StatusText = $"Bookmarked {bookmark.Address}.";
        RaiseBookmarkCanExecuteChanged();
    }

    private void RemoveSelectedBookmark()
    {
        MemoryViewerBookmarkViewModel? bookmark = SelectedBookmark;
        if (bookmark is null)
        {
            return;
        }

        int index = Bookmarks.IndexOf(bookmark);
        Bookmarks.Remove(bookmark);
        SelectedBookmark = Bookmarks.Count == 0
            ? null
            : Bookmarks[Math.Clamp(index, 0, Bookmarks.Count - 1)];
        ErrorText = string.Empty;
        StatusText = $"Removed bookmark {bookmark.Address}.";
        RaiseBookmarkCanExecuteChanged();
    }

    private async Task<bool> NavigateAndRecordAsync(ulong address, int highlightByteCount)
    {
        if (!await ReadAddressAsync(address, highlightByteCount).ConfigureAwait(true))
        {
            return false;
        }

        RecordNavigationState(address, highlightByteCount);
        return true;
    }

    private async Task RefreshAsync()
    {
        await ReadAddressAsync(_currentAddress, _currentHighlightByteCount).ConfigureAwait(true);
    }

    private async Task NavigateHistoryAsync(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _navigationHistory.Count)
        {
            return;
        }

        MemoryViewerNavigationEntry entry = _navigationHistory[targetIndex];
        if (await ReadAddressAsync(entry.Address, entry.HighlightByteCount).ConfigureAwait(true))
        {
            _navigationHistoryIndex = targetIndex;
            RaiseNavigationCanExecuteChanged();
        }
    }

    private async Task<bool> ReadAddressAsync(ulong address, int highlightByteCount)
    {
        if (_disposed)
        {
            return false;
        }

        if (highlightByteCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(highlightByteCount));
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = $"Reading memory around 0x{address:X}...";

        try
        {
            int requestedWindowByteCount = GetWindowByteCount(highlightByteCount);
            MemoryViewSnapshot snapshot = await _plugin
                .ReadMemoryViewerWindowAsync(
                    _targetProcess,
                    _connectionGeneration,
                    address,
                    requestedWindowByteCount,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (_disposed)
            {
                return false;
            }

            _currentAddress = address;
            _currentHighlightByteCount = highlightByteCount;
            AddressText = $"0x{address:X}";
            ApplySnapshot(snapshot, highlightByteCount);
            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            if (!_disposed)
            {
                StatusText = "Memory Viewer read cancelled.";
            }

            return false;
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                ErrorText = exception.Message;
                StatusText = "Memory Viewer read failed. The previous view has been kept.";
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

    internal async Task WriteRowBytesAsync(
        MemoryViewerRowViewModel row,
        ReadOnlyMemory<byte> replacementBytes)
    {
        ArgumentNullException.ThrowIfNull(row);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Rows.Contains(row) || !row.CanEdit || !CanEditMemory)
        {
            ErrorText = "The selected Memory Viewer row is not currently writable.";
            StatusText = "Memory write not started.";
            return;
        }

        if (replacementBytes.Length != row.ByteCount)
        {
            ErrorText = $"Enter exactly {row.ByteCount:N0} bytes for this Memory Viewer row.";
            StatusText = "Memory write not started.";
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = $"Validating and writing {row.ByteCount:N0} bytes at {row.Address}...";

        try
        {
            MemoryViewWriteResult result = await _plugin
                .WriteMemoryViewerBytesAsync(
                    _targetProcess,
                    _connectionGeneration,
                    row.AddressValue,
                    row.Bytes,
                    replacementBytes,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (_disposed)
            {
                return;
            }

            switch (result.Outcome)
            {
                case MemoryViewWriteOutcome.NoChanges:
                    StatusText = $"No byte changes to write at {row.Address}.";
                    ErrorText = string.Empty;
                    break;

                case MemoryViewWriteOutcome.SourceChanged:
                    await ReadAddressAsync(_currentAddress, _currentHighlightByteCount).ConfigureAwait(true);
                    if (!_disposed)
                    {
                        ErrorText =
                            $"Memory at {row.Address} changed after it was displayed. No write was attempted; the viewer has been refreshed.";
                        StatusText = "Memory write blocked because the displayed bytes were stale.";
                    }
                    break;

                case MemoryViewWriteOutcome.Verified:
                    await ReadAddressAsync(_currentAddress, _currentHighlightByteCount).ConfigureAwait(true);
                    if (!_disposed)
                    {
                        ErrorText = string.Empty;
                        StatusText = $"Wrote and verified {result.ByteCount:N0} bytes at {row.Address}.";
                    }
                    break;

                case MemoryViewWriteOutcome.VerificationFailed:
                    await ReadAddressAsync(_currentAddress, _currentHighlightByteCount).ConfigureAwait(true);
                    if (!_disposed)
                    {
                        ErrorText =
                            $"The write was sent to {row.Address}, but read-back bytes did not match the requested data. The viewer has been refreshed.";
                        StatusText = "Memory write completed, but verification failed.";
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported Memory Viewer write outcome '{result.Outcome}'.");
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            if (!_disposed)
            {
                StatusText = "Memory Viewer write cancelled.";
            }
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                ErrorText = exception.Message;
                StatusText = "Memory Viewer write failed. Refresh before making another edit if the target state is uncertain.";
            }
        }
        finally
        {
            if (!_disposed)
            {
                IsBusy = false;
            }
        }
    }

    private void ApplySnapshot(MemoryViewSnapshot snapshot, int highlightByteCount)
    {
        Rows.Clear();

        MemoryRegion region = snapshot.Region;
        _currentRegion = region;
        bool canEdit =
            _plugin.SupportsMemoryRead &&
            _plugin.SupportsMemoryWrite &&
            region.Protection.HasFlag(MemoryProtection.Read) &&
            region.Protection.HasFlag(MemoryProtection.Write) &&
            !region.Protection.HasFlag(MemoryProtection.Guard);
        CanEditMemory = canEdit;
        AccessModeText = canEdit ? "Read/write" : "Read-only";

        MemoryViewHighlightSpan highlightSpan = new(snapshot.RequestedAddress, highlightByteCount);
        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;
        for (int offset = 0; offset < bytes.Length; offset += snapshot.BytesPerRow)
        {
            int rowLength = Math.Min(snapshot.BytesPerRow, bytes.Length - offset);
            Rows.Add(new MemoryViewerRowViewModel(
                checked(snapshot.StartAddress + (ulong)offset),
                bytes.Slice(offset, rowLength),
                highlightSpan,
                canEdit));
        }

        SelectedRow = Rows.FirstOrDefault(row => row.IsOriginRow);

        RegionNameText = !string.IsNullOrWhiteSpace(region.ModuleName)
            ? region.ModuleName!
            : !string.IsNullOrWhiteSpace(region.Name)
                ? region.Name!
                : string.Empty;

        ulong inclusiveEnd = region.EndAddressExclusive - 1;
        RegionRangeText =
            $"0x{region.BaseAddress:X} - 0x{inclusiveEnd:X} · {region.Size:N0} bytes";
        ProtectionText = region.Protection.ToString();

        ulong visibleInclusiveEnd = snapshot.EndAddressExclusive - 1;
        VisibleRangeText =
            $"0x{snapshot.StartAddress:X} - 0x{visibleInclusiveEnd:X} · {snapshot.Bytes.Length:N0} bytes";
        int visibleHighlightByteCount = Rows.Sum(row => row.HighlightByteCount);
        StatusText = highlightByteCount == 1
            ? $"Read {snapshot.Bytes.Length:N0} bytes around 0x{snapshot.RequestedAddress:X}."
            : visibleHighlightByteCount == highlightByteCount
                ? $"Read {snapshot.Bytes.Length:N0} bytes around 0x{snapshot.RequestedAddress:X}; highlighting the full {highlightByteCount:N0}-byte value span."
                : $"Read {snapshot.Bytes.Length:N0} bytes around 0x{snapshot.RequestedAddress:X}; {visibleHighlightByteCount:N0} of {highlightByteCount:N0} highlighted value bytes are visible.";
        ErrorText = string.Empty;
        RaiseRegionCanExecuteChanged();
        RaiseBookmarkCanExecuteChanged();
    }

    private void RecordNavigationState(ulong address, int highlightByteCount)
    {
        MemoryViewerNavigationEntry entry = new(address, highlightByteCount);
        if (_navigationHistoryIndex >= 0 &&
            _navigationHistoryIndex < _navigationHistory.Count &&
            _navigationHistory[_navigationHistoryIndex] == entry)
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

        _navigationHistory.Add(entry);
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

    private void RaiseBookmarkCanExecuteChanged()
    {
        _addBookmarkCommand.RaiseCanExecuteChanged();
        _removeBookmarkCommand.RaiseCanExecuteChanged();
        _goToBookmarkCommand.RaiseCanExecuteChanged();
    }

    internal void ReportClipboardCopy(int rowCount)
    {
        ErrorText = string.Empty;
        StatusText = rowCount == 1
            ? "Copied 1 Memory Viewer row to the clipboard."
            : $"Copied {rowCount:N0} Memory Viewer rows to the clipboard.";
    }

    internal void ReportClipboardCopy(string itemName)
    {
        ErrorText = string.Empty;
        StatusText = $"Copied {itemName} to the clipboard.";
    }

    internal void ReportClipboardFailure(string message)
    {
        ErrorText = message;
        StatusText = "Clipboard copy failed.";
    }

    private static int GetWindowByteCount(int highlightByteCount)
    {
        if (highlightByteCount <= MemoryViewerReader.DefaultWindowByteCount / 2)
        {
            return MemoryViewerReader.DefaultWindowByteCount;
        }

        // MemoryViewerReader aligns the visible window to row boundaries. Reserve two
        // rows of headroom so that alignment cannot trim the tail of a known value span.
        long desiredByteCount = checked(
            ((long)highlightByteCount * 2L) +
            (MemoryViewerReader.DefaultBytesPerRow * 2L));

        return checked((int)Math.Min(desiredByteCount, MemoryViewerReader.MaximumWindowByteCount));
    }

    private readonly record struct MemoryViewerNavigationEntry(ulong Address, int HighlightByteCount);

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
