using System;
using System.IO;
using TeeKay87.MemoryEngine.Core.Diagnostics;

namespace TeeKay87.MemoryEngine.App.Diagnostics;

internal sealed class FileApplicationLogger : IApplicationLogger
{
    private readonly object _syncRoot = new();
    private readonly string _logPath;

    public FileApplicationLogger(string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);
        _logPath = logPath;
    }

    public void Info(string message)
    {
        Write("INFO", message, exception: null);
    }

    public void Warning(string message)
    {
        Write("WARN", message, exception: null);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    private void Write(string level, string message, Exception? exception)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            lock (_syncRoot)
            {
                string? directory = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string exceptionText = exception is null
                    ? string.Empty
                    : $" | {exception.GetType().Name}: {exception.Message}";
                string line = $"{DateTimeOffset.UtcNow:O} [{level}] {message.Trim()}{exceptionText}{Environment.NewLine}";
                File.AppendAllText(_logPath, line);
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
