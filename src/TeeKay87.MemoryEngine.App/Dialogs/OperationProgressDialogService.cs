using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TeeKay87.MemoryEngine.Core.Operations;

namespace TeeKay87.MemoryEngine.App.Dialogs;

public sealed class OperationProgressDialogService
{
    public async Task RunAsync(
        Window owner,
        string title,
        string initialStatus,
        bool allowCancellation,
        Func<IProgress<OperationProgress>, CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await RunAsync<object?>(
                owner,
                title,
                initialStatus,
                allowCancellation,
                async (progress, cancellationToken) =>
                {
                    await operation(progress, cancellationToken).ConfigureAwait(true);
                    return null;
                })
            .ConfigureAwait(true);
    }

    public async Task<T> RunAsync<T>(
        Window owner,
        string title,
        string initialStatus,
        bool allowCancellation,
        Func<IProgress<OperationProgress>, CancellationToken, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(operation);

        if (!owner.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException("Operation progress dialogs must be opened from the WPF UI thread.");
        }

        using CancellationTokenSource cancellationSource = new();
        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        OperationProgressDialog dialog = new(title, initialStatus, allowCancellation)
        {
            Owner = owner
        };

        dialog.CancellationRequested += (_, _) => cancellationSource.Cancel();

        bool operationStarted = false;
        Progress<OperationProgress> progress = new(dialog.ApplyProgress);

        dialog.Loaded += async (_, _) =>
        {
            if (operationStarted)
            {
                return;
            }

            operationStarted = true;
            try
            {
                T result = await operation(progress, cancellationSource.Token).ConfigureAwait(true);
                completion.TrySetResult(result);
            }
            catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationSource.Token);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                dialog.CompleteAndClose();
            }
        };

        dialog.ShowDialog();
        return await completion.Task.ConfigureAwait(true);
    }
}
