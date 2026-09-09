namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public enum ScanResultSessionState
{
    Creating,
    Writing,
    Committed,
    Failed,
    Cancelled,
    Invalidated
}
