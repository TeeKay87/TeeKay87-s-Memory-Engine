using System;
using System.IO;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public sealed class ScanResultStorageSession : IScanResultStorageSession
{
    private readonly object _syncRoot = new();
    private readonly ScanResultStorageManager _manager;
    private ScanResultStorageManager.ScanSessionMetadata _metadata;
    private ScanResultFileWriter? _activeWriter;
    private bool _disposed;

    internal ScanResultStorageSession(
        ScanResultStorageManager manager,
        string sessionPath,
        ScanResultStorageManager.ScanSessionMetadata metadata)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionPath);
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        SessionPath = sessionPath;
    }

    public Guid ApplicationSessionId => _metadata.ApplicationSessionId;

    public Guid ScanSessionId => _metadata.ScanSessionId;

    public string SessionPath { get; }

    public string MetadataPath => Path.Combine(SessionPath, "scan.json");

    public ScanResultSessionState State
    {
        get
        {
            lock (_syncRoot)
            {
                return _metadata.State;
            }
        }
    }

    public bool IsCommitted => State == ScanResultSessionState.Committed;

    public long? ResultCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _metadata.ResultCount;
            }
        }
    }

    public IScanResultWriter CreateResultWriter(int valueSize, int alignment)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ScanResultBinaryFormat.ValidateShape(valueSize, alignment);

            if (_activeWriter is not null)
            {
                throw new InvalidOperationException(
                    $"Scan session {ScanSessionId:D} already has an active result writer.");
            }

            if (_metadata.State is not (ScanResultSessionState.Creating or ScanResultSessionState.Writing or ScanResultSessionState.Committed))
            {
                throw new InvalidOperationException(
                    $"Scan session {ScanSessionId:D} cannot create a result writer from state {_metadata.State}.");
            }

            if (_metadata.State == ScanResultSessionState.Creating)
            {
                _manager.LogCommitStarted(ScanSessionId);
                UpdateState(ScanResultSessionState.Writing, failureReason: null, resultCount: null, committedUtc: null);
            }

            int generation = checked(_metadata.ResultGeneration + 1);
            string finalFileName = $"results-{generation:D8}.bin";
            string temporaryPath = Path.Combine(
                SessionPath,
                $".{finalFileName}.{Guid.NewGuid():N}.tmp");

            ScanResultFileWriter writer = new(
                this,
                temporaryPath,
                finalFileName,
                generation,
                valueSize,
                alignment);
            _activeWriter = writer;
            return writer;
        }
    }

    public IScanResultSet OpenResultSet()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (_metadata.State != ScanResultSessionState.Committed ||
                string.IsNullOrWhiteSpace(_metadata.ResultFileName) ||
                _metadata.ResultCount is null ||
                _metadata.ResultValueSize is null ||
                _metadata.ResultAlignment is null)
            {
                throw new InvalidOperationException(
                    $"Scan session {ScanSessionId:D} does not have a committed disk-backed result set.");
            }

            string resultPath = Path.Combine(SessionPath, _metadata.ResultFileName);
            return new ScanResultFileSet(
                resultPath,
                _metadata.ResultCount.Value,
                _metadata.ResultValueSize.Value,
                _metadata.ResultAlignment.Value);
        }
    }

    public void Cancel()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            TransitionToTerminalState(
                ScanResultSessionState.Cancelled,
                failureReason: null,
                () => _manager.LogCommitCancelled(ScanSessionId));
        }
    }

    public void Fail(string? reason = null)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            TransitionToTerminalState(
                ScanResultSessionState.Failed,
                reason,
                () => _manager.LogCommitFailed(ScanSessionId, reason));
        }
    }

    public void Invalidate()
    {
        ScanResultFileWriter? writerToDispose;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            writerToDispose = _activeWriter;
            _activeWriter = null;

            ScanResultStorageManager.ScanSessionMetadata invalidated = CloneMetadata();
            invalidated.State = ScanResultSessionState.Invalidated;
            invalidated.UpdatedUtc = DateTimeOffset.UtcNow;
            invalidated.FailureReason = null;
            _metadata = invalidated;

            try
            {
                _manager.UpdateScanMetadata(SessionPath, invalidated);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ObjectDisposedException)
            {
                _manager.LogCommitFailed(ScanSessionId, $"Could not persist invalidated state: {exception.Message}");
            }

            _manager.LogSessionInvalidated(ScanSessionId);
            _disposed = true;
        }

        writerToDispose?.Dispose();
        _manager.TryDeleteScanSession(SessionPath, ScanSessionId);
    }

    public void Dispose()
    {
        Invalidate();
    }

    internal void PublishResultSet(
        string temporaryPath,
        string finalFileName,
        int generation,
        long resultCount,
        int valueSize,
        int alignment)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (_activeWriter is null)
            {
                throw new InvalidOperationException("The scan-result writer is no longer active.");
            }

            if (_metadata.State is not (ScanResultSessionState.Writing or ScanResultSessionState.Committed))
            {
                throw new InvalidOperationException(
                    $"Scan session {ScanSessionId:D} cannot publish a result set from state {_metadata.State}.");
            }

            string finalPath = Path.Combine(SessionPath, finalFileName);
            string? previousFileName = _metadata.ResultFileName;
            File.Move(temporaryPath, finalPath, overwrite: false);

            try
            {
                using (ScanResultFileSet validationSet = new(
                           finalPath,
                           resultCount,
                           valueSize,
                           alignment))
                {
                }

                DateTimeOffset committedUtc = DateTimeOffset.UtcNow;
                ScanResultStorageManager.ScanSessionMetadata committed = CloneMetadata();
                committed.State = ScanResultSessionState.Committed;
                committed.ResultCount = resultCount;
                committed.ResultFileName = finalFileName;
                committed.ResultValueSize = valueSize;
                committed.ResultAlignment = alignment;
                committed.ResultGeneration = generation;
                committed.CommittedUtc = committedUtc;
                committed.UpdatedUtc = committedUtc;
                committed.FailureReason = null;

                _manager.UpdateScanMetadata(SessionPath, committed);
                _metadata = committed;
            }
            catch
            {
                TryDeleteFile(finalPath);
                throw;
            }

            ScanResultFileWriter publishedWriter = _activeWriter
                ?? throw new InvalidOperationException("The scan-result writer disappeared before publication completed.");
            _activeWriter = null;
            publishedWriter.MarkPublished();
            _manager.LogCommitCompleted(ScanSessionId, resultCount);

            if (!string.IsNullOrWhiteSpace(previousFileName) &&
                !string.Equals(previousFileName, finalFileName, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(Path.Combine(SessionPath, previousFileName));
            }
        }
    }

    internal void ReleaseResultWriter(ScanResultFileWriter writer)
    {
        lock (_syncRoot)
        {
            if (ReferenceEquals(_activeWriter, writer))
            {
                _activeWriter = null;
            }
        }
    }

    private void TransitionToTerminalState(
        ScanResultSessionState state,
        string? failureReason,
        Action logTransition)
    {
        _activeWriter?.Dispose();
        _activeWriter = null;

        ScanResultStorageManager.ScanSessionMetadata terminal = CloneMetadata();
        terminal.State = state;
        terminal.UpdatedUtc = DateTimeOffset.UtcNow;
        terminal.FailureReason = failureReason;
        _metadata = terminal;

        try
        {
            _manager.UpdateScanMetadata(SessionPath, terminal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ObjectDisposedException)
        {
            _manager.LogCommitFailed(ScanSessionId, $"Could not persist terminal state {state}: {exception.Message}");
        }

        logTransition();
        _manager.TryDeleteScanSession(SessionPath, ScanSessionId);
        _disposed = true;
    }

    private void UpdateState(
        ScanResultSessionState state,
        string? failureReason,
        long? resultCount,
        DateTimeOffset? committedUtc)
    {
        ScanResultStorageManager.ScanSessionMetadata updated = CloneMetadata();
        updated.State = state;
        updated.UpdatedUtc = DateTimeOffset.UtcNow;
        updated.FailureReason = failureReason;
        updated.ResultCount = resultCount;
        updated.CommittedUtc = committedUtc;
        _manager.UpdateScanMetadata(SessionPath, updated);
        _metadata = updated;
    }

    private ScanResultStorageManager.ScanSessionMetadata CloneMetadata()
    {
        return new ScanResultStorageManager.ScanSessionMetadata
        {
            FormatVersion = _metadata.FormatVersion,
            ManagedBy = _metadata.ManagedBy,
            ApplicationSessionId = _metadata.ApplicationSessionId,
            ScanSessionId = _metadata.ScanSessionId,
            State = _metadata.State,
            CreatedUtc = _metadata.CreatedUtc,
            CommittedUtc = _metadata.CommittedUtc,
            UpdatedUtc = _metadata.UpdatedUtc,
            ResultCount = _metadata.ResultCount,
            PluginId = _metadata.PluginId,
            TargetProcessId = _metadata.TargetProcessId,
            ValueTypeId = _metadata.ValueTypeId,
            ScanTypeId = _metadata.ScanTypeId,
            RecordFormatVersion = _metadata.RecordFormatVersion,
            FailureReason = _metadata.FailureReason,
            ResultFileName = _metadata.ResultFileName,
            ResultValueSize = _metadata.ResultValueSize,
            ResultAlignment = _metadata.ResultAlignment,
            ResultGeneration = _metadata.ResultGeneration
        };
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
