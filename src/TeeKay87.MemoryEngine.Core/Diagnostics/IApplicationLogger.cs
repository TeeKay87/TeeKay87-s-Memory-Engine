using System;

namespace TeeKay87.MemoryEngine.Core.Diagnostics;

public interface IApplicationLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception = null);
}
