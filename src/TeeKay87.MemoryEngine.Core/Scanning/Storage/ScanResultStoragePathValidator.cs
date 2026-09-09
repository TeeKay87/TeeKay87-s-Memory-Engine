using System;
using System.IO;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public static class ScanResultStoragePathValidator
{
    public static string ValidateAndPrepare(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        string fullPath = Path.GetFullPath(expanded);

        if (File.Exists(fullPath))
        {
            throw new IOException($"The scan-results storage path points to a file: {fullPath}");
        }

        Directory.CreateDirectory(fullPath);

        string probePath = Path.Combine(
            fullPath,
            $".tk87me-write-test-{Guid.NewGuid():N}.tmp");

        try
        {
            using FileStream stream = new(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
            stream.WriteByte(0x54);
            stream.Flush(flushToDisk: true);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    public static bool TryValidateAndPrepare(
        string path,
        out string validatedPath,
        out string error)
    {
        try
        {
            validatedPath = ValidateAndPrepare(path);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            validatedPath = string.Empty;
            error = exception.Message;
            return false;
        }
    }
}
