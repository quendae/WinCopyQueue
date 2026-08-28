namespace WinCopyQueue.Core;

public enum TransferOperation
{
    Copy,
    Move
}

public enum TransferSessionState
{
    Preparing,
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Canceled
}

public enum TransferFileState
{
    Queued,
    Running,
    Paused,
    Completed,
    Skipped,
    Canceled,
    Failed
}

public enum ConflictResolution
{
    Replace,
    ReplaceAll,
    Skip,
    SkipAll,
    CancelSession
}

public sealed record TransferRequest(
    TransferOperation Operation,
    string Destination,
    IReadOnlyList<string> Sources,
    bool VerifyIntegrity = false);

public sealed record TransferFileSnapshot(string Path, TransferFileState State, bool CanControl = true);

public sealed record FileConflictSnapshot(
    Guid SessionId,
    TransferOperation Operation,
    string SourcePath,
    string DestinationPath,
    long SourceSize,
    long DestinationSize,
    DateTime SourceModifiedUtc,
    DateTime DestinationModifiedUtc);

public sealed record TransferSessionSnapshot(
    Guid Id,
    TransferOperation Operation,
    string Destination,
    IReadOnlyList<string> Sources,
    TransferSessionState State,
    int CompletedFiles,
    int ProcessedFiles,
    long FileStateVersion,
    int TotalFiles,
    long TransferredBytes,
    long TotalBytes,
    string? CurrentFile,
    IReadOnlyList<string> PendingFiles,
    string? Error,
    bool IsVerifying = false,
    bool IntegrityVerificationEnabled = false)
{
    public double Progress => State == TransferSessionState.Completed ? 100
        : TotalBytes > 0
        ? Math.Clamp((double)TransferredBytes / TotalBytes * 100, 0, 100)
        : TotalFiles > 0
            ? Math.Clamp((double)ProcessedFiles / TotalFiles * 100, 0, 100)
            : 0;
}
