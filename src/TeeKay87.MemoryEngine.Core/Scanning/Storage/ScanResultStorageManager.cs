using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeeKay87.MemoryEngine.Core.Diagnostics;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public sealed class ScanResultStorageManager : IScanResultStorage
{
    internal const string ManagedByValue = "TeeKay87.MemoryEngine.ScanResultStorage";
    internal const int StorageFormatVersion = 1;
    private const string RootMarkerFileName = ".tk87me-scan-storage.json";
    private const string ApplicationMetadataFileName = "session.json";
    private readonly IApplicationLogger _logger;
    private bool _disposed;

    public ScanResultStorageManager(string rootPath, IApplicationLogger? logger = null)
    {
        ApplicationSessionId = Guid.NewGuid();
        _logger = logger ?? NullApplicationLogger.Instance;
        RootPath = ScanResultStoragePathValidator.ValidateAndPrepare(rootPath);

        EnsureManagedRoot();
        CleanupStaleApplicationSessions();

        ApplicationSessionPath = Path.Combine(RootPath, ApplicationSessionId.ToString("D"));
        Directory.CreateDirectory(ApplicationSessionPath);
        WriteApplicationMetadata(new ApplicationSessionMetadata
        {
            FormatVersion = StorageFormatVersion,
            ManagedBy = ManagedByValue,
            ApplicationSessionId = ApplicationSessionId,
            CreatedUtc = DateTimeOffset.UtcNow,
            State = ApplicationSessionState.Active
        });

        _logger.Info(
            $"Application scan-storage session created. Session={ApplicationSessionId:D}; Root={RootPath}");
    }

    public Guid ApplicationSessionId { get; }

    public string RootPath { get; }

    public string ApplicationSessionPath { get; }

    public IScanResultStorageSession CreateScanSession(ScanResultSessionContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        context.Validate();

        Guid scanSessionId = Guid.NewGuid();
        string scanSessionPath = Path.Combine(ApplicationSessionPath, scanSessionId.ToString("D"));
        Directory.CreateDirectory(scanSessionPath);

        ScanSessionMetadata metadata = new()
        {
            FormatVersion = StorageFormatVersion,
            ManagedBy = ManagedByValue,
            ApplicationSessionId = ApplicationSessionId,
            ScanSessionId = scanSessionId,
            State = ScanResultSessionState.Creating,
            CreatedUtc = DateTimeOffset.UtcNow,
            PluginId = context.PluginId,
            TargetProcessId = context.TargetProcessId,
            ValueTypeId = context.ValueTypeId,
            ScanTypeId = context.ScanTypeId,
            RecordFormatVersion = context.RecordFormatVersion
        };

        WriteScanMetadata(scanSessionPath, metadata);
        _logger.Info(
            $"Scan-result session created. ApplicationSession={ApplicationSessionId:D}; ScanSession={scanSessionId:D}; Plugin={context.PluginId}; TargetProcess=0x{context.TargetProcessId:X}");

        return new ScanResultStorageSession(this, scanSessionPath, metadata);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            ApplicationSessionMetadata closedMetadata = new()
            {
                FormatVersion = StorageFormatVersion,
                ManagedBy = ManagedByValue,
                ApplicationSessionId = ApplicationSessionId,
                CreatedUtc = ReadCurrentApplicationCreatedUtc(),
                ClosedUtc = DateTimeOffset.UtcNow,
                State = ApplicationSessionState.Closed
            };
            WriteApplicationMetadata(closedMetadata);
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            _logger.Warning(
                $"Could not mark application scan-storage session {ApplicationSessionId:D} as closed: {exception.Message}");
        }

        if (TryDeleteManagedDirectory(ApplicationSessionPath, RootPath, "application session", out string? error))
        {
            _logger.Info($"Application scan-storage session cleanup completed. Session={ApplicationSessionId:D}");
        }
        else
        {
            _logger.Warning(
                $"Application scan-storage session cleanup failed. Session={ApplicationSessionId:D}; {error}");
        }
    }

    internal void UpdateScanMetadata(string scanSessionPath, ScanSessionMetadata metadata)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteScanMetadata(scanSessionPath, metadata);
    }

    internal bool TryDeleteScanSession(string scanSessionPath, Guid scanSessionId)
    {
        if (TryDeleteManagedDirectory(scanSessionPath, ApplicationSessionPath, "scan session", out string? error))
        {
            _logger.Info($"Scan-result session cleanup completed. ScanSession={scanSessionId:D}");
            return true;
        }

        _logger.Warning($"Scan-result session cleanup failed. ScanSession={scanSessionId:D}; {error}");
        return false;
    }

    internal void LogCommitStarted(Guid scanSessionId)
    {
        _logger.Info($"Scan-result commit started. ScanSession={scanSessionId:D}");
    }

    internal void LogCommitCompleted(Guid scanSessionId, long resultCount)
    {
        _logger.Info(
            $"Scan-result commit completed. ScanSession={scanSessionId:D}; ResultCount={resultCount}");
    }

    internal void LogCommitCancelled(Guid scanSessionId)
    {
        _logger.Info($"Scan-result commit cancelled. ScanSession={scanSessionId:D}");
    }

    internal void LogCommitFailed(Guid scanSessionId, string? reason)
    {
        string suffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $"; Reason={reason}";
        _logger.Warning($"Scan-result commit failed. ScanSession={scanSessionId:D}{suffix}");
    }

    internal void LogSessionInvalidated(Guid scanSessionId)
    {
        _logger.Info($"Scan-result session invalidated. ScanSession={scanSessionId:D}");
    }

    private void EnsureManagedRoot()
    {
        string markerPath = Path.Combine(RootPath, RootMarkerFileName);
        if (!File.Exists(markerPath))
        {
            WriteJsonAtomically(markerPath, new StorageRootMetadata
            {
                FormatVersion = StorageFormatVersion,
                ManagedBy = ManagedByValue,
                CreatedUtc = DateTimeOffset.UtcNow
            });
            return;
        }

        StorageRootMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<StorageRootMetadata>(
                File.ReadAllText(markerPath),
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The configured scan-results location contains an invalid {RootMarkerFileName} marker.",
                exception);
        }

        if (metadata is null ||
            !string.Equals(metadata.ManagedBy, ManagedByValue, StringComparison.Ordinal) ||
            metadata.FormatVersion is < 1 or > StorageFormatVersion)
        {
            throw new InvalidDataException(
                $"The configured scan-results location contains an incompatible {RootMarkerFileName} marker.");
        }
    }

    private void CleanupStaleApplicationSessions()
    {
        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(RootPath).ToArray();
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            _logger.Warning($"Could not enumerate stale scan-storage sessions under {RootPath}: {exception.Message}");
            return;
        }

        foreach (string directory in directories)
        {
            string directoryName = Path.GetFileName(directory);
            if (!Guid.TryParse(directoryName, out Guid directorySessionId) ||
                directorySessionId == ApplicationSessionId)
            {
                continue;
            }

            if (!TryReadManagedApplicationMetadata(directory, out ApplicationSessionMetadata? metadata) ||
                metadata is null ||
                metadata.ApplicationSessionId != directorySessionId)
            {
                continue;
            }

            _logger.Info($"Stale scan-storage session detected. Session={directorySessionId:D}");
            if (TryDeleteManagedDirectory(directory, RootPath, "stale application session", out string? error))
            {
                _logger.Info($"Stale scan-storage session deleted. Session={directorySessionId:D}");
            }
            else
            {
                _logger.Warning(
                    $"Stale scan-storage session cleanup failed. Session={directorySessionId:D}; {error}");
            }
        }
    }

    private bool TryReadManagedApplicationMetadata(
        string applicationSessionPath,
        out ApplicationSessionMetadata? metadata)
    {
        string metadataPath = Path.Combine(applicationSessionPath, ApplicationMetadataFileName);
        metadata = null;

        try
        {
            if (!File.Exists(metadataPath))
            {
                return false;
            }

            metadata = JsonSerializer.Deserialize<ApplicationSessionMetadata>(
                File.ReadAllText(metadataPath),
                JsonOptions);
            return metadata is not null &&
                   metadata.FormatVersion == StorageFormatVersion &&
                   string.Equals(metadata.ManagedBy, ManagedByValue, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.Warning(
                $"Ignored an unrecognized scan-storage directory '{applicationSessionPath}': {exception.Message}");
            metadata = null;
            return false;
        }
    }

    private DateTimeOffset ReadCurrentApplicationCreatedUtc()
    {
        try
        {
            string metadataPath = Path.Combine(ApplicationSessionPath, ApplicationMetadataFileName);
            ApplicationSessionMetadata? metadata = JsonSerializer.Deserialize<ApplicationSessionMetadata>(
                File.ReadAllText(metadataPath),
                JsonOptions);
            return metadata?.CreatedUtc ?? DateTimeOffset.UtcNow;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return DateTimeOffset.UtcNow;
        }
    }

    private void WriteApplicationMetadata(ApplicationSessionMetadata metadata)
    {
        WriteJsonAtomically(
            Path.Combine(ApplicationSessionPath, ApplicationMetadataFileName),
            metadata);
    }

    private static void WriteScanMetadata(string scanSessionPath, ScanSessionMetadata metadata)
    {
        WriteJsonAtomically(Path.Combine(scanSessionPath, "scan.json"), metadata);
    }

    private static void WriteJsonAtomically<T>(string path, T value)
    {
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Could not resolve the parent directory for '{path}'.");
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            string json = JsonSerializer.Serialize(value, JsonOptions);
            using (FileStream stream = new(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (StreamWriter writer = new(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool TryDeleteManagedDirectory(
        string directoryPath,
        string expectedParent,
        string description,
        out string? error)
    {
        try
        {
            string fullDirectoryPath = Path.GetFullPath(directoryPath);
            string? actualParent = Directory.GetParent(fullDirectoryPath)?.FullName;
            string fullExpectedParent = Path.GetFullPath(expectedParent);

            if (actualParent is null ||
                !string.Equals(
                    Path.TrimEndingDirectorySeparator(actualParent),
                    Path.TrimEndingDirectorySeparator(fullExpectedParent),
                    StringComparison.OrdinalIgnoreCase))
            {
                error = $"Refused to delete {description} outside its managed parent directory.";
                return false;
            }

            if (!Directory.Exists(fullDirectoryPath))
            {
                error = null;
                return true;
            }

            FileAttributes attributes = File.GetAttributes(fullDirectoryPath);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = $"Refused to recursively delete {description} because it is a filesystem reparse point.";
                return false;
            }

            Directory.Delete(fullDirectoryPath, recursive: true);
            error = null;
            return true;
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool IsStorageException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or InvalidDataException;
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    internal sealed class ScanSessionMetadata
    {
        public int FormatVersion { get; set; }

        public string ManagedBy { get; set; } = string.Empty;

        public Guid ApplicationSessionId { get; set; }

        public Guid ScanSessionId { get; set; }

        public ScanResultSessionState State { get; set; }

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset? CommittedUtc { get; set; }

        public DateTimeOffset? UpdatedUtc { get; set; }

        public long? ResultCount { get; set; }

        public string PluginId { get; set; } = string.Empty;

        public ulong TargetProcessId { get; set; }

        public string ValueTypeId { get; set; } = string.Empty;

        public string ScanTypeId { get; set; } = string.Empty;

        public int RecordFormatVersion { get; set; }

        public string? ResultFileName { get; set; }

        public int? ResultValueSize { get; set; }

        public int? ResultAlignment { get; set; }

        public int ResultGeneration { get; set; }

        public string? FailureReason { get; set; }
    }

    private sealed class StorageRootMetadata
    {
        public int FormatVersion { get; set; }

        public string ManagedBy { get; set; } = string.Empty;

        public DateTimeOffset CreatedUtc { get; set; }
    }

    private sealed class ApplicationSessionMetadata
    {
        public int FormatVersion { get; set; }

        public string ManagedBy { get; set; } = string.Empty;

        public Guid ApplicationSessionId { get; set; }

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset? ClosedUtc { get; set; }

        public ApplicationSessionState State { get; set; }
    }

    private enum ApplicationSessionState
    {
        Active,
        Closed
    }

    private sealed class NullApplicationLogger : IApplicationLogger
    {
        public static NullApplicationLogger Instance { get; } = new();

        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
