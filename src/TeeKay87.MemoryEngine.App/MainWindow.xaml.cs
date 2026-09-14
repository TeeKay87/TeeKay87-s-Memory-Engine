using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeeKay87.MemoryEngine.App.Application;
using TeeKay87.MemoryEngine.App.Controls;
using TeeKay87.MemoryEngine.App.Dialogs;
using TeeKay87.MemoryEngine.App.Exporting;
using TeeKay87.MemoryEngine.App.Settings;
using TeeKay87.MemoryEngine.App.Theming;
using TeeKay87.MemoryEngine.App.ViewModels;
using TeeKay87.MemoryEngine.Core.Scanning.Storage;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App;

public partial class MainWindow : Window
{
    public static readonly DependencyProperty TopTargetInputWidthProperty = DependencyProperty.Register(
        nameof(TopTargetInputWidth),
        typeof(double),
        typeof(MainWindow),
        new PropertyMetadata(UiMetrics.TopTargetInputMaxWidth));

    private const double TopTargetConnectionInputSpacing = 10d;

    public double TopTargetInputWidth
    {
        get => (double)GetValue(TopTargetInputWidthProperty);
        private set => SetValue(TopTargetInputWidthProperty, value);
    }

    private readonly ApplicationSettingsStore _settingsStore;
    private readonly ToolWindowManager _toolWindowManager = new();
    private readonly ConfirmationDialogService _confirmationDialogService = new();
    private readonly DataExportDialogService _dataExportDialogService = new();
    private readonly ScanResultStorageManager? _scanResultStorageManager;
    private readonly string _storageStartupError;
    private bool _toolWindowShutdownInProgress;
    private bool _toolWindowShutdownCompleted;
    private readonly Dictionary<DataGridRow, (PluginViewModel Owner, ScanResultViewModel Result)>
        _visibleScanResultRows = new();

    internal MainWindow(
        ThemeManager themeManager,
        ApplicationSettingsStore settingsStore,
        ScanResultStorageManager? scanResultStorageManager,
        string storageStartupError)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _scanResultStorageManager = scanResultStorageManager;
        _storageStartupError = storageStartupError ?? string.Empty;

        InitializeComponent();
        DataContext = new MainWindowViewModel(
            themeManager,
            settingsStore.SharedStore,
            scanResultStorageManager,
            _storageStartupError,
            settingsStore.LoadSavedAddressesUpdateIntervalMilliseconds(),
            settingsStore.LoadFrozenWriteIntervalMilliseconds());
    }

    private void TopTargetFirstRow_LayoutUpdated(object? sender, EventArgs e)
    {
        double rowWidth = Math.Max(
            0d,
            TargetConnectionBar.ActualWidth -
            TargetConnectionBar.Padding.Left -
            TargetConnectionBar.Padding.Right);
        if (rowWidth <= 0d)
        {
            return;
        }

        int connectionInputCount = TopTargetConnectionSettings.Items.Count;
        bool hasProcessControls = TargetProcessPanel.Visibility == Visibility.Visible;
        int ordinaryInputCount = 1 + connectionInputCount + (hasProcessControls ? 1 : 0);

        double fixedWidth = GetHorizontalOuterWidth(ConnectButton) +
                            GetHorizontalOuterWidth(DisconnectButton);
        double spacing = TopTargetConnectionSettings.Margin.Left +
                         TopTargetConnectionSettings.Margin.Right +
                         connectionInputCount * TopTargetConnectionInputSpacing;

        if (hasProcessControls)
        {
            fixedWidth += GetHorizontalOuterWidth(RefreshProcessesButton) +
                          GetHorizontalOuterWidth(SetActiveTargetButton);
            spacing += TargetProcessPanel.Margin.Left + TargetProcessPanel.Margin.Right;
        }

        double availableInputWidth = Math.Max(0d, rowWidth - fixedWidth - spacing);
        double responsiveWidth = Math.Min(
            UiMetrics.TopTargetInputMaxWidth,
            availableInputWidth / ordinaryInputCount);

        if (Math.Abs(TopTargetInputWidth - responsiveWidth) > 0.25d)
        {
            TopTargetInputWidth = responsiveWidth;
        }
    }

    private static double GetHorizontalOuterWidth(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.ActualWidth + element.Margin.Left + element.Margin.Right;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_toolWindowShutdownCompleted || _toolWindowManager.Count == 0)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);

        if (_toolWindowShutdownInProgress)
        {
            return;
        }

        _toolWindowShutdownInProgress = true;
        IsEnabled = false;
        _ = CompleteToolWindowShutdownAsync();
    }

    private async Task CompleteToolWindowShutdownAsync()
    {
        try
        {
            await _toolWindowManager.CloseAllAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                $"Tool-window shutdown cleanup reported an error: {exception}");
        }
        finally
        {
            _toolWindowShutdownCompleted = true;
            _toolWindowShutdownInProgress = false;

            // Always queue the final close. CloseAllAsync can complete synchronously
            // when every tool is already detached/disposed; calling Close() directly
            // from the original OnClosing stack would re-enter WPF while the window is
            // still processing that close request.
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach ((PluginViewModel owner, ScanResultViewModel result) in _visibleScanResultRows.Values)
        {
            owner.UnregisterVisibleScanResult(result);
        }

        _visibleScanResultRows.Clear();

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private void OpenDebuggerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedPlugin(out PluginViewModel? plugin) ||
            !plugin.CanOpenDebugger ||
            plugin.ActiveProcess is not TargetProcessViewModel activeProcess)
        {
            return;
        }

        TargetProcess targetProcess = activeProcess.Process;
        long connectionGeneration = plugin.ConnectionGeneration;
        DebuggerViewModel viewModel = new(
            plugin,
            targetProcess,
            connectionGeneration);

        bool CanOpenDisassemblerFromDebugger()
        {
            return plugin.ConnectionGeneration == connectionGeneration &&
                   plugin.CanOpenDisassemblerForTarget(targetProcess);
        }

        void OpenDisassemblerFromDebugger(ulong address)
        {
            if (CanOpenDisassemblerFromDebugger())
            {
                OpenDisassembler(plugin, targetProcess, address);
            }
        }

        bool CanOpenMemoryViewerFromDebugger()
        {
            return plugin.ConnectionGeneration == connectionGeneration &&
                   plugin.CanOpenMemoryViewerForTarget(targetProcess);
        }

        void OpenMemoryViewerFromDebugger(ulong address)
        {
            if (CanOpenMemoryViewerFromDebugger())
            {
                OpenMemoryViewer(plugin, targetProcess, address);
            }
        }

        CallStackComparerViewModel? comparerWorkspace = null;

        Task OpenComparerFromDebuggerAsync()
        {
            comparerWorkspace ??= new CallStackComparerViewModel { LiveDebugger = viewModel };

            if (_toolWindowManager.TryActivateDataContext<CallStackComparerViewModel>(
                    candidate => ReferenceEquals(candidate, comparerWorkspace)))
            {
                return Task.CompletedTask;
            }

            CallStackComparerWindow comparerWindow = new(
                comparerWorkspace,
                () => viewModel.CaptureSnapshotAsync());
            _toolWindowManager.Show(comparerWindow, this);
            return Task.CompletedTask;
        }

        DebuggerWindow window = new(
            viewModel,
            CanOpenDisassemblerFromDebugger,
            OpenDisassemblerFromDebugger,
            CanOpenMemoryViewerFromDebugger,
            OpenMemoryViewerFromDebugger,
            OpenComparerFromDebuggerAsync);
        window.Closed += (_, _) => comparerWorkspace?.DetachLiveDebugger();
        _toolWindowManager.Show(window, this);
        e.Handled = true;
    }

    private void OpenDisassemblerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedPlugin(out PluginViewModel? plugin) ||
            !plugin.CanOpenDisassembler ||
            plugin.ActiveProcess is not TargetProcessViewModel activeProcess)
        {
            return;
        }

        MemoryRegion? initialRegion = plugin.ActiveMemoryRegions
            .Where(region =>
                region.Size > 0 &&
                region.Protection.HasFlag(MemoryProtection.Read) &&
                !region.Protection.HasFlag(MemoryProtection.Guard))
            .OrderByDescending(region => region.Protection.HasFlag(MemoryProtection.Execute))
            .ThenByDescending(region => !string.IsNullOrWhiteSpace(region.ModuleName))
            .ThenByDescending(region => !string.IsNullOrWhiteSpace(region.Name))
            .ThenBy(region => region.BaseAddress)
            .FirstOrDefault();

        if (initialRegion is null)
        {
            return;
        }

        OpenDisassembler(plugin, activeProcess.Process, initialRegion.BaseAddress);
        e.Handled = true;
    }

    private async void AddManualSavedAddressButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedPlugin(out PluginViewModel? plugin) ||
            !plugin.CanAddSavedAddressManually ||
            plugin.ActiveProcess is not TargetProcessViewModel activeProcess)
        {
            return;
        }

        ManualSavedAddressDialog dialog = new(
            $"{plugin.Platform} · {activeProcess.SelectionDisplay}",
            plugin.ScanValueTypes,
            plugin.SelectedScanValueType)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || dialog.Result is not ManualSavedAddressDialogResult result)
        {
            return;
        }

        await plugin
            .AddManualSavedAddressAsync(
                result.Address,
                result.Description,
                result.ValueType,
                result.ValueSize)
            .ConfigureAwait(true);
        e.Handled = true;
    }

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsWindow window = new(
            _settingsStore,
            _scanResultStorageManager?.RootPath,
            _storageStartupError)
        {
            Owner = this
        };
        if (window.ShowDialog() == true && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetSavedAddressUpdateInterval(window.SavedAddressesUpdateIntervalMilliseconds);
            viewModel.SetFrozenWriteInterval(window.FrozenWriteIntervalMilliseconds);
        }
    }

    private void ScanResultsDataGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not ScanResultViewModel scanResult ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin))
        {
            return;
        }

        if (_visibleScanResultRows.Remove(e.Row, out var previous))
        {
            previous.Owner.UnregisterVisibleScanResult(previous.Result);
        }

        _visibleScanResultRows[e.Row] = (plugin, scanResult);
        plugin.RegisterVisibleScanResult(scanResult);
    }

    private void ScanResultsDataGrid_UnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (_visibleScanResultRows.Remove(e.Row, out var tracked))
        {
            tracked.Owner.UnregisterVisibleScanResult(tracked.Result);
        }
    }

    private void ScanResultsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid ||
            e.OriginalSource is not DependencyObject originalSource ||
            ItemsControl.ContainerFromElement(dataGrid, originalSource) is not DataGridRow
            { DataContext: ScanResultViewModel scanResult } ||
            !scanResult.SaveAddressCommand.CanExecute(null))
        {
            return;
        }

        scanResult.SaveAddressCommand.Execute(null);
        e.Handled = true;
    }

    private void ScanResultsDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGrid dataGrid ||
            !TryPrepareRowContextMenu<ScanResultViewModel>(dataGrid, out ScanResultViewModel? scanResult) ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin) ||
            plugin.ActiveProcess is not TargetProcessViewModel activeProcess)
        {
            e.Handled = true;
            return;
        }

        ContextMenuUtilities.SetItemEnabled(
            dataGrid.ContextMenu,
            "OpenDisassembler",
            plugin.CanOpenDisassembler);

        UpdateDebuggerAddressActionAvailability(
            dataGrid.ContextMenu,
            plugin,
            activeProcess.Process,
            scanResult.Result.Address,
            scanResult.Result.CurrentValue.Size);
    }

    private void SavedAddressesDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGrid dataGrid ||
            !TryPrepareRowContextMenu<SavedAddressViewModel>(dataGrid, out SavedAddressViewModel? savedAddress) ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin))
        {
            e.Handled = true;
            return;
        }

        TargetProcess savedTarget = CreateSavedAddressTarget(savedAddress);
        ContextMenuUtilities.SetItemEnabled(
            dataGrid.ContextMenu,
            "OpenDisassembler",
            plugin.CanOpenDisassemblerForTarget(savedTarget));

        UpdateDebuggerAddressActionAvailability(
            dataGrid.ContextMenu,
            plugin,
            savedTarget,
            savedAddress.Address,
            savedAddress.ValueSize);
    }

    private static bool TryPrepareRowContextMenu<TItem>(
        DataGrid dataGrid,
        [NotNullWhen(true)] out TItem? item)
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(dataGrid);

        item = null;
        ContextMenu? contextMenu = dataGrid.ContextMenu;
        if (Mouse.DirectlyOver is not DependencyObject source ||
            ItemsControl.ContainerFromElement(dataGrid, source) is not DataGridRow { DataContext: TItem rowItem })
        {
            if (contextMenu is not null)
            {
                contextMenu.DataContext = null;
            }

            return false;
        }

        if (!dataGrid.SelectedItems.Contains(rowItem))
        {
            dataGrid.SelectedItems.Clear();
            dataGrid.SelectedItem = rowItem;
        }

        if (contextMenu is null)
        {
            return false;
        }

        contextMenu.DataContext = rowItem;
        item = rowItem;
        return true;
    }

    private void ScanResultsSaveAddressMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ScanResultViewModel clickedResult } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin))
        {
            return;
        }

        ScanResultViewModel[] selectedResults = ScanResultsDataGrid.SelectedItems
            .OfType<ScanResultViewModel>()
            .ToArray();

        if (selectedResults.Length > 1 && selectedResults.Contains(clickedResult))
        {
            plugin.SaveScanResultsToSavedAddresses(selectedResults);
        }
        else
        {
            plugin.SaveScanResultsToSavedAddresses(new[] { clickedResult });
        }

        e.Handled = true;
    }

    private void ScanResultOpenMemoryViewerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ScanResultViewModel scanResult } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin) ||
            plugin.ActiveProcess is not TargetProcessViewModel activeProcess)
        {
            return;
        }

        OpenMemoryViewer(
            plugin,
            activeProcess.Process,
            scanResult.Result.Address,
            scanResult.Result.CurrentValue.Size);
        e.Handled = true;
    }

    private void ScanResultOpenDisassemblerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ScanResultViewModel scanResult } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin) ||
            !plugin.CanOpenDisassembler ||
            plugin.ActiveProcess is not TargetProcessViewModel activeProcess)
        {
            return;
        }

        OpenDisassembler(plugin, activeProcess.Process, scanResult.Result.Address);
        e.Handled = true;
    }

    private async void ScanResultAddBreakpointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ScanResultViewModel scanResult } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin) ||
            plugin.ActiveProcess is not TargetProcessViewModel activeProcess)
        {
            return;
        }

        await AddDebuggerAddressActionAsync(
                plugin,
                activeProcess.Process,
                CreateSoftwareExecuteBreakpointRequest(scanResult.Result.Address))
            .ConfigureAwait(true);
        e.Handled = true;
    }

    private async void ScanResultAddWatchpointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ScanResultViewModel scanResult } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin) ||
            plugin.ActiveProcess is not TargetProcessViewModel activeProcess)
        {
            return;
        }

        await AddDebuggerAddressActionAsync(
                plugin,
                activeProcess.Process,
                CreateWriteWatchpointRequest(scanResult.Result.Address, scanResult.Result.CurrentValue.Size))
            .ConfigureAwait(true);
        e.Handled = true;
    }

    private void SavedAddressOpenMemoryViewerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SavedAddressViewModel savedAddress } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin))
        {
            return;
        }

        OpenMemoryViewer(
            plugin,
            CreateSavedAddressTarget(savedAddress),
            savedAddress.Address,
            savedAddress.ValueSize);
        e.Handled = true;
    }

    private void SavedAddressOpenDisassemblerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SavedAddressViewModel savedAddress } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin))
        {
            return;
        }

        TargetProcess savedTarget = CreateSavedAddressTarget(savedAddress);
        if (!plugin.CanOpenDisassemblerForTarget(savedTarget))
        {
            return;
        }

        OpenDisassembler(plugin, savedTarget, savedAddress.Address);
        e.Handled = true;
    }

    private async void SavedAddressAddBreakpointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SavedAddressViewModel savedAddress } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin))
        {
            return;
        }

        await AddDebuggerAddressActionAsync(
                plugin,
                CreateSavedAddressTarget(savedAddress),
                CreateSoftwareExecuteBreakpointRequest(savedAddress.Address))
            .ConfigureAwait(true);
        e.Handled = true;
    }

    private async void SavedAddressAddWatchpointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SavedAddressViewModel savedAddress } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin))
        {
            return;
        }

        await AddDebuggerAddressActionAsync(
                plugin,
                CreateSavedAddressTarget(savedAddress),
                CreateWriteWatchpointRequest(savedAddress.Address, savedAddress.ValueSize))
            .ConfigureAwait(true);
        e.Handled = true;
    }

    private void UpdateDebuggerAddressActionAvailability(
        ContextMenu? contextMenu,
        PluginViewModel plugin,
        TargetProcess targetProcess,
        ulong address,
        int valueSize)
    {
        DebuggerViewModel? debugger = FindAttachedDebugger(plugin, targetProcess);
        bool canAddBreakpoint = debugger?.ValidateAddressActionRequest(
            CreateSoftwareExecuteBreakpointRequest(address)).IsValid == true;
        bool canAddWatchpoint = debugger?.ValidateAddressActionRequest(
            CreateWriteWatchpointRequest(address, valueSize)).IsValid == true;

        ContextMenuUtilities.SetItemEnabled(contextMenu, "AddBreakpoint", canAddBreakpoint);
        ContextMenuUtilities.SetItemEnabled(contextMenu, "AddWatchpoint", canAddWatchpoint);
    }

    private async Task AddDebuggerAddressActionAsync(
        PluginViewModel plugin,
        TargetProcess targetProcess,
        DebuggerBreakpointRequest request)
    {
        DebuggerViewModel? debugger = FindAttachedDebugger(plugin, targetProcess);
        if (debugger is null || !debugger.ValidateAddressActionRequest(request).IsValid)
        {
            return;
        }

        await debugger.AddBreakpointAsync(request).ConfigureAwait(true);
    }

    private DebuggerViewModel? FindAttachedDebugger(
        PluginViewModel plugin,
        TargetProcess targetProcess)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(targetProcess);

        long connectionGeneration = plugin.ConnectionGeneration;
        return _toolWindowManager.FindDataContext<DebuggerViewModel>(debugger =>
            debugger.IsAttachedTo(plugin, targetProcess, connectionGeneration));
    }

    private static DebuggerBreakpointRequest CreateSoftwareExecuteBreakpointRequest(ulong address)
    {
        return new DebuggerBreakpointRequest(
            address,
            1,
            DebuggerBreakpointKind.Software,
            DebuggerBreakpointAccess.Execute);
    }

    private static DebuggerBreakpointRequest CreateWriteWatchpointRequest(ulong address, int size)
    {
        return new DebuggerBreakpointRequest(
            address,
            size,
            DebuggerBreakpointKind.Hardware,
            DebuggerBreakpointAccess.Write);
    }

    private static TargetProcess CreateSavedAddressTarget(SavedAddressViewModel savedAddress)
    {
        ArgumentNullException.ThrowIfNull(savedAddress);

        return new TargetProcess(
            savedAddress.TargetProcessId,
            savedAddress.TargetProcessName,
            savedAddress.TargetProcessDisplayName);
    }

    private void OpenDisassembler(
        PluginViewModel plugin,
        TargetProcess targetProcess,
        ulong address)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(targetProcess);

        long connectionGeneration = plugin.ConnectionGeneration;
        DisassemblerViewModel viewModel = new(
            plugin,
            targetProcess,
            connectionGeneration,
            address);

        DebuggerViewModel? GetDebugger()
        {
            if (plugin.ConnectionGeneration != connectionGeneration)
            {
                return null;
            }

            return FindAttachedDebugger(plugin, targetProcess);
        }

        bool CanAddBreakpoint(ulong rowAddress)
        {
            DebuggerViewModel? debugger = GetDebugger();
            return debugger?.ValidateAddressActionRequest(
                CreateSoftwareExecuteBreakpointRequest(rowAddress)).IsValid == true;
        }

        DebuggerBreakpointRequest? CreateResolvedWatchpointRequest(DisassembledInstruction instruction)
        {
            DebuggerViewModel? debugger = GetDebugger();
            if (debugger is null || !debugger.CanResolveDisassemblyWatchpoint())
            {
                return null;
            }

            DisassemblyWatchpointTarget? target = plugin.ResolveDisassemblyWatchpointTarget(
                instruction,
                debugger.Registers.Select(register => register.Register).ToArray());
            if (target is null)
            {
                return null;
            }

            DebuggerBreakpointRequest request = new(
                target.Address,
                target.Size,
                DebuggerBreakpointKind.Hardware,
                target.Access);
            return debugger.ValidateAddressActionRequest(request).IsValid
                ? request
                : null;
        }

        bool CanAddWatchpoint(DisassembledInstruction instruction)
        {
            return CreateResolvedWatchpointRequest(instruction) is not null;
        }

        async Task AddBreakpointAsync(ulong rowAddress)
        {
            DebuggerViewModel? debugger = GetDebugger();
            DebuggerBreakpointRequest request = CreateSoftwareExecuteBreakpointRequest(rowAddress);
            if (debugger is null || !debugger.ValidateAddressActionRequest(request).IsValid)
            {
                return;
            }

            await debugger.AddBreakpointAsync(request).ConfigureAwait(true);
        }

        async Task AddWatchpointAsync(DisassembledInstruction instruction)
        {
            DebuggerViewModel? debugger = GetDebugger();
            DebuggerBreakpointRequest? request = CreateResolvedWatchpointRequest(instruction);
            if (debugger is null || request is null)
            {
                return;
            }

            await debugger.AddBreakpointAsync(request).ConfigureAwait(true);
        }

        DisassemblerWindow window = new(
            viewModel,
            CanAddBreakpoint,
            CanAddWatchpoint,
            AddBreakpointAsync,
            AddWatchpointAsync);
        _toolWindowManager.Show(window, this);
    }

    private void OpenMemoryViewer(
        PluginViewModel plugin,
        TargetProcess targetProcess,
        ulong address,
        int highlightByteCount = 1)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(targetProcess);

        long connectionGeneration = plugin.ConnectionGeneration;
        MemoryViewerViewModel viewModel = new(
            plugin,
            targetProcess,
            connectionGeneration,
            address,
            highlightByteCount);

        bool CanOpenDisassemblerFromViewer()
        {
            return plugin.ConnectionGeneration == connectionGeneration &&
                   plugin.CanOpenDisassemblerForTarget(targetProcess);
        }

        void OpenDisassemblerFromViewer(ulong rowAddress)
        {
            if (CanOpenDisassemblerFromViewer())
            {
                OpenDisassembler(plugin, targetProcess, rowAddress);
            }
        }

        MemoryViewerWindow window = new(
            viewModel,
            CanOpenDisassemblerFromViewer,
            OpenDisassemblerFromViewer);
        _toolWindowManager.Show(window, this);
    }

    private async void SavedAddressFrozenCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: SavedAddressViewModel savedAddress } checkBox ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin))
        {
            return;
        }

        bool requestedState = checkBox.IsChecked == true;
        await plugin.SetSavedAddressFrozenAsync(savedAddress, requestedState).ConfigureAwait(true);
    }

    private async void SavedAddressAddressTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: SavedAddressViewModel savedAddress } textBox &&
            TryGetSelectedPlugin(out PluginViewModel? plugin))
        {
            await plugin.CommitSavedAddressAddressAsync(savedAddress, textBox.Text).ConfigureAwait(true);
        }
    }

    private void SavedAddressAddressTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }

        e.Handled = true;
        textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private async void SavedAddressValueTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox
            {
                DataContext: SavedAddressViewModel savedAddress,
                SelectedItem: ScanValueTypeViewModel selectedValueType
            } ||
            !TryGetSelectedPlugin(out PluginViewModel? plugin) ||
            string.Equals(
                savedAddress.SelectedValueType.Id,
                selectedValueType.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await plugin
            .ChangeSavedAddressValueTypeAsync(savedAddress, selectedValueType)
            .ConfigureAwait(true);
    }

    private void SavedAddressValueTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: SavedAddressViewModel savedAddress })
        {
            savedAddress.BeginValueEdit();
        }
    }

    private async void SavedAddressValueTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: SavedAddressViewModel savedAddress } textBox)
        {
            return;
        }

        string editedText = textBox.Text;
        string displayedBeforeEdit = savedAddress.ValueText;
        savedAddress.EndValueEdit();

        if (TryGetSelectedPlugin(out PluginViewModel? plugin) &&
            !string.Equals(editedText, displayedBeforeEdit, StringComparison.Ordinal))
        {
            await plugin.CommitSavedAddressValueAsync(savedAddress, editedText).ConfigureAwait(true);
        }
        else
        {
            savedAddress.RefreshValueText();
        }
    }

    private void SavedAddressValueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }

        e.Handled = true;
        textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private async void ExportScanResultsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedPlugin(out PluginViewModel? plugin) || !plugin.CanExportScanResults)
        {
            return;
        }

        ScanResultViewModel[] selectedRows = ScanResultsDataGrid.SelectedItems
            .OfType<ScanResultViewModel>()
            .ToArray();
        IReadOnlyList<ExportScopeOption> scopes = plugin.CreateScanResultExportScopes(selectedRows);
        DataExportDialogResult? selection = _dataExportDialogService.Show(this, "Export Scan Results", scopes);
        if (selection is null || !ExportDestinationPicker.TryChoose(this, "scan-results", selection.Format, out string? path))
        {
            return;
        }

        await plugin.ExportDataAsync(this, selection, path).ConfigureAwait(true);
    }

    private async void ExportSavedAddressesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedPlugin(out PluginViewModel? plugin) || !plugin.CanExportSavedAddresses)
        {
            return;
        }

        SavedAddressViewModel[] selectedRows = SavedAddressesDataGrid.SelectedItems
            .OfType<SavedAddressViewModel>()
            .ToArray();
        IReadOnlyList<ExportScopeOption> scopes = plugin.CreateSavedAddressExportScopes(selectedRows);
        DataExportDialogResult? selection = _dataExportDialogService.Show(this, "Export Saved Addresses", scopes);
        if (selection is null || !ExportDestinationPicker.TryChoose(this, "saved-addresses", selection.Format, out string? path))
        {
            return;
        }

        await plugin.ExportDataAsync(this, selection, path).ConfigureAwait(true);
    }


    private void SavedAddressRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SavedAddressViewModel savedAddress })
        {
            RemoveSavedAddressWithOptionalConfirmation(savedAddress);
        }
    }

    private void SavedAddressRemoveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SavedAddressViewModel savedAddress })
        {
            RemoveSavedAddressWithOptionalConfirmation(savedAddress);
        }
    }

    private void RemoveSavedAddressWithOptionalConfirmation(SavedAddressViewModel savedAddress)
    {
        string description = savedAddress.Description?.Trim() ?? string.Empty;
        if (description.Length > 0)
        {
            bool confirmed = _confirmationDialogService.Show(
                this,
                new ConfirmationDialogOptions(
                    title: "Remove Saved Address",
                    message: $"Remove saved address '{description}'?",
                    confirmButtonText: "Remove",
                    cancelButtonText: "Cancel",
                    tone: ConfirmationDialogTone.Danger,
                    confirmIsDefault: false));

            if (!confirmed)
            {
                return;
            }
        }

        if (savedAddress.RemoveCommand.CanExecute(null))
        {
            savedAddress.RemoveCommand.Execute(null);
        }
    }

    private void RemoveAllSavedAddressesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedPlugin(out PluginViewModel? plugin) || plugin.SavedAddresses.Count == 0)
        {
            return;
        }

        bool confirmed = _confirmationDialogService.Show(
            this,
            new ConfirmationDialogOptions(
                title: "Remove All Saved Addresses",
                message: $"Remove all {plugin.SavedAddresses.Count:N0} saved address(es)?",
                confirmButtonText: "Remove All",
                cancelButtonText: "Cancel",
                tone: ConfirmationDialogTone.Danger,
                confirmIsDefault: false));

        if (confirmed)
        {
            plugin.RemoveAllSavedAddresses();
        }
    }

    private bool TryGetSelectedPlugin([NotNullWhen(true)] out PluginViewModel? plugin)
    {
        plugin = (DataContext as MainWindowViewModel)?.SelectedPlugin;
        return plugin is not null;
    }
}
