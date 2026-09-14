using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace TeeKay87.MemoryEngine.App.Application;

internal sealed class ToolWindowManager
{
    private readonly HashSet<Window> _windows = new();

    public int Count => _windows.Count;

    public void Show(Window window, Window placementOwner)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(placementOwner);

        if (!_windows.Add(window))
        {
            throw new InvalidOperationException("The tool window is already being tracked.");
        }

        window.Closed += ToolWindow_Closed;
        window.Owner = placementOwner;

        try
        {
            window.Show();
        }
        catch
        {
            window.Owner = null;
            Untrack(window);
            throw;
        }

        // CenterOwner is evaluated while Show() creates the native window. Clearing
        // Owner afterwards preserves that placement without forcing the modeless
        // tool window to remain above the main window.
        window.Owner = null;
    }

    public bool TryActivateDataContext<TDataContext>(Func<TDataContext, bool> predicate)
        where TDataContext : class
    {
        ArgumentNullException.ThrowIfNull(predicate);
        foreach (Window window in _windows)
        {
            if (window.DataContext is TDataContext candidate && predicate(candidate))
            {
                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                window.Activate();
                return true;
            }
        }
        return false;
    }

    public TDataContext? FindDataContext<TDataContext>(Func<TDataContext, bool> predicate)
        where TDataContext : class
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (Window window in _windows)
        {
            if (window.DataContext is TDataContext candidate && predicate(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public async Task CloseAllAsync()
    {
        Window[] windows = _windows.ToArray();
        List<Exception>? failures = null;

        foreach (Window window in windows)
        {
            if (_windows.Contains(window))
            {
                window.IsEnabled = false;
            }
        }

        foreach (Window window in windows)
        {
            try
            {
                await CleanupDataContextAsync(window.DataContext).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                (failures ??= new List<Exception>()).Add(exception);
            }

            try
            {
                if (_windows.Contains(window))
                {
                    window.Close();
                }
            }
            catch (Exception exception)
            {
                (failures ??= new List<Exception>()).Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException("One or more tool windows could not complete shutdown cleanly.", failures);
        }
    }

    private static async Task CleanupDataContextAsync(object? dataContext)
    {
        if (dataContext is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(true);
            return;
        }

        if (dataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void ToolWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            Untrack(window);
        }
    }

    private void Untrack(Window window)
    {
        window.Closed -= ToolWindow_Closed;
        _windows.Remove(window);
    }
}
